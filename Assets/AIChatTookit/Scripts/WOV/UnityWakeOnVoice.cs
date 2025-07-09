using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Unity语音唤醒功能 支持Windows和Android
/// </summary>
public class UnityWakeOnVoice : WOV
{
    /// <summary>
    /// 关键词数组
    /// </summary>
    [SerializeField]
    private string[] m_Keywords = { "小雅", "你好", "开始", "唤醒" };//关键词数组
    
    /// <summary>
    /// 是否自动开始识别
    /// </summary>
    [SerializeField]
    private bool m_AutoStart = true;

#if UNITY_STANDALONE_WIN
    /// <summary>
    /// Windows语音识别器对象
    /// </summary>
    private KeywordRecognizer m_Recognizer;
    
    // Use this for initialization
    void Start()
    {
        //初始化语音识别器
        m_Recognizer = new KeywordRecognizer(m_Keywords);
        Debug.Log("Windows语音识别器初始化");
        m_Recognizer.OnPhraseRecognized += OnPhraseRecognized;
        
        if (m_AutoStart)
        {
            StartRecognizer();
        }
    }
    
    /// <summary>
    /// 开始识别
    /// </summary>
    public override void StartRecognizer()
    {
        if (m_Recognizer == null)
            return;

        m_Recognizer.Start();
    }
    
    /// <summary>
    /// 停止识别
    /// </summary>
    public override void StopRecognizer()
    {
        if (m_Recognizer == null)
            return;

        m_Recognizer.Stop();
    }

    /// <summary>
    /// Windows语音识别回调
    /// </summary>
    /// <param name="args"></param>
    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendFormat("{0}", args.text);
        string _keyWord = builder.ToString();
        Debug.Log("语音识别到关键词："+_keyWord);
        OnAwakeOnVoice(_keyWord);
    }
    
#elif UNITY_ANDROID
    
    /// <summary>
    /// Android语音识别器对象
    /// </summary>
    private AndroidJavaObject m_SpeechRecognizer;
    private AndroidJavaObject m_Activity;
    private AndroidJavaObject m_Context;
    private bool m_IsListening = false;
    
    void Start()
    {
        Debug.Log("Android语音识别器初始化");
        CheckAndroidPermissions();
        InitializeAndroidRecognizer();
        
        if (m_AutoStart)
        {
            StartRecognizer();
        }
    }
    
    void OnDestroy()
    {
        StopRecognizer();
        CleanupAndroidObjects();
    }
    
    /// <summary>
    /// 检查Android权限
    /// </summary>
    private void CheckAndroidPermissions()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
    }
    
    /// <summary>
    /// 初始化Android语音识别
    /// </summary>
    private void InitializeAndroidRecognizer()
    {
        try
        {
            // 获取当前Activity和Context
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                m_Activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                m_Context = m_Activity.Call<AndroidJavaObject>("getApplicationContext");
            }
            
            // 检查语音识别是否可用
            using (AndroidJavaClass speechRecognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer"))
            {
                bool isAvailable = speechRecognizerClass.CallStatic<bool>("isRecognitionAvailable", m_Context);
                if (!isAvailable)
                {
                    Debug.LogError("设备不支持语音识别");
                    return;
                }
                
                m_SpeechRecognizer = speechRecognizerClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", m_Context);
            }
            
            // 设置识别监听器
            AndroidRecognitionListener listener = new AndroidRecognitionListener(this);
            m_SpeechRecognizer.Call("setRecognitionListener", listener);
            
            Debug.Log("Android语音识别器初始化成功");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Android语音识别器初始化失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 开始识别
    /// </summary>
    public override void StartRecognizer()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Debug.LogError("没有麦克风权限！");
            return;
        }
        
        if (m_IsListening)
        {
            Debug.LogWarning("语音识别器已在运行中！");
            return;
        }
        
        if (m_SpeechRecognizer == null)
        {
            InitializeAndroidRecognizer();
        }
        
        try
        {
            // 创建识别Intent
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                using (AndroidJavaClass recognizerIntent = new AndroidJavaClass("android.speech.RecognizerIntent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", recognizerIntent.GetStatic<string>("ACTION_RECOGNIZE_SPEECH"));
                    intent.Call<AndroidJavaObject>("putExtra", recognizerIntent.GetStatic<string>("EXTRA_LANGUAGE_MODEL"), 
                        recognizerIntent.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));
                    intent.Call<AndroidJavaObject>("putExtra", recognizerIntent.GetStatic<string>("EXTRA_CALLING_PACKAGE"), 
                        m_Context.Call<string>("getPackageName"));
                    intent.Call<AndroidJavaObject>("putExtra", recognizerIntent.GetStatic<string>("EXTRA_PARTIAL_RESULTS"), true);
                    // 设置语言为中文
                    intent.Call<AndroidJavaObject>("putExtra", recognizerIntent.GetStatic<string>("EXTRA_LANGUAGE"), "zh-CN");
                }
                
                m_SpeechRecognizer.Call("startListening", intent);
                m_IsListening = true;
                Debug.Log("Android语音识别器启动成功");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"启动Android语音识别器失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 停止识别
    /// </summary>
    public override void StopRecognizer()
    {
        if (!m_IsListening || m_SpeechRecognizer == null)
            return;
        
        try
        {
            m_SpeechRecognizer.Call("stopListening");
            m_IsListening = false;
            Debug.Log("Android语音识别器已停止");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"停止Android语音识别器失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 处理Android识别结果
    /// </summary>
    public void OnAndroidRecognitionResult(string[] results)
    {
        if (results == null || results.Length == 0) return;
        
        foreach (string result in results)
        {
            Debug.Log($"Android识别结果: {result}");
            
            // 检查是否包含关键词
            if (IsKeywordMatch(result))
            {
                Debug.Log("语音识别到关键词：" + result);
                OnAwakeOnVoice(result);
                break;
            }
        }
        
        // 重新开始识别（实现持续监听）
        if (m_IsListening)
        {
            StartCoroutine(RestartRecognitionAfterDelay());
        }
    }
    
    /// <summary>
    /// 检查关键词是否匹配
    /// </summary>
    private bool IsKeywordMatch(string recognizedText)
    {
        if (string.IsNullOrEmpty(recognizedText)) return false;
        
        string lowerText = recognizedText.ToLower();
        
        foreach (string keyword in m_Keywords)
        {
            if (lowerText.Contains(keyword.ToLower()))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 延迟重启识别
    /// </summary>
    private IEnumerator RestartRecognitionAfterDelay()
    {
        yield return new WaitForSeconds(1.0f);
        if (m_IsListening)
        {
            StartRecognizer();
        }
    }
    
    /// <summary>
    /// 语音识别出错处理
    /// </summary>
    public void OnAndroidRecognitionError(int errorCode)
    {
        string errorMsg = GetAndroidErrorMessage(errorCode);
        Debug.LogError($"Android语音识别错误: {errorMsg}");
        
        // 某些错误后重新开始识别
        if (errorCode == 6 || errorCode == 7) // 语音超时或没有匹配
        {
            StartCoroutine(RestartRecognitionAfterDelay());
        }
    }
    
    /// <summary>
    /// 获取Android错误信息
    /// </summary>
    private string GetAndroidErrorMessage(int errorCode)
    {
        switch (errorCode)
        {
            case 1: return "网络超时";
            case 2: return "网络错误";
            case 3: return "音频错误";
            case 4: return "服务器错误";
            case 5: return "客户端错误";
            case 6: return "语音超时";
            case 7: return "没有匹配";
            case 8: return "识别服务繁忙";
            case 9: return "权限不足";
            default: return $"未知错误 ({errorCode})";
        }
    }
    
    /// <summary>
    /// 清理Android对象
    /// </summary>
    private void CleanupAndroidObjects()
    {
        if (m_SpeechRecognizer != null)
        {
            m_SpeechRecognizer.Call("destroy");
            m_SpeechRecognizer.Dispose();
            m_SpeechRecognizer = null;
        }
        
        if (m_Activity != null)
        {
            m_Activity.Dispose();
            m_Activity = null;
        }
        
        if (m_Context != null)
        {
            m_Context.Dispose();
            m_Context = null;
        }
    }
    
    /// <summary>
    /// 获取识别状态
    /// </summary>
    public bool IsListening => m_IsListening;
    
#else
    // 其他平台的空实现
    void Start()
    {
        Debug.LogWarning("当前平台不支持语音识别功能！");
    }
    
    public override void StartRecognizer()
    {
        Debug.LogWarning("当前平台不支持语音识别功能！");
    }
    
    public override void StopRecognizer()
    {
        Debug.LogWarning("当前平台不支持语音识别功能！");
    }
#endif
}

#if UNITY_ANDROID
/// <summary>
/// Android识别监听器
/// </summary>
public class AndroidRecognitionListener : AndroidJavaProxy
{
    private UnityWakeOnVoice m_VoiceWake;
    
    public AndroidRecognitionListener(UnityWakeOnVoice voiceWake) : base("android.speech.RecognitionListener")
    {
        m_VoiceWake = voiceWake;
    }
    
    public void onReadyForSpeech(AndroidJavaObject @params)
    {
        Debug.Log("Android语音识别准备就绪");
    }
    
    public void onBeginningOfSpeech()
    {
        Debug.Log("Android开始语音输入");
    }
    
    public void onRmsChanged(float rmsdB)
    {
        // 音量变化
    }
    
    public void onBufferReceived(byte[] buffer)
    {
        // 缓冲区接收
    }
    
    public void onEndOfSpeech()
    {
        Debug.Log("Android语音输入结束");
    }
    
    public void onError(int error)
    {
        m_VoiceWake.OnAndroidRecognitionError(error);
    }
    
    public void onResults(AndroidJavaObject results)
    {
        if (results != null)
        {
            using (AndroidJavaObject arrayList = results.Call<AndroidJavaObject>("getStringArrayList", "results_recognition"))
            {
                if (arrayList != null)
                {
                    int size = arrayList.Call<int>("size");
                    string[] recognitionResults = new string[size];
                    
                    for (int i = 0; i < size; i++)
                    {
                        recognitionResults[i] = arrayList.Call<string>("get", i);
                    }
                    
                    m_VoiceWake.OnAndroidRecognitionResult(recognitionResults);
                }
            }
        }
    }
    
    public void onPartialResults(AndroidJavaObject partialResults)
    {
        // 部分结果处理
    }
    
    public void onEvent(int eventType, AndroidJavaObject @params)
    {
        // 事件处理
    }
}
#endif