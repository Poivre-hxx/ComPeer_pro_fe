using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AI聊天应用主控制器
/// </summary>
public class RTChatSample : MonoBehaviour
{
    #region 聊天设置
    /// <summary>
    /// 聊天配置
    /// </summary>
    [SerializeField] private ChatSetting m_ChatSettings;
    #endregion
    
    #region UI组件
    [Header("UI组件")]
    /// <summary>
    /// 聊天UI面板
    /// </summary>
    [SerializeField] private GameObject m_ChatPanel;
    /// <summary>
    /// 输入框组件
    /// </summary>
    [SerializeField] public InputField m_InputWord;
    /// <summary>
    /// 回复显示文本
    /// </summary>
    [SerializeField] private Text m_TextBack;
    /// <summary>
    /// 音频播放组件
    /// </summary>
    [SerializeField] private AudioSource m_AudioSource;
    /// <summary>
    /// 发送消息按钮
    /// </summary>
    [SerializeField] private Button m_CommitMsgBtn;
    #endregion

    #region 角色动画
    [Header("角色动画")]
    /// <summary>
    /// 角色动画控制器
    /// </summary>
    [SerializeField] private Animator m_Animator;
    /// <summary>
    /// 语音模式：设置为false，走通用文字合成
    /// </summary>
    [Header("是否启用语音合成播放回复")]
    [SerializeField] private bool m_IsVoiceMode = true;
    /// <summary>
    /// AI回复播放完毕后回调
    /// </summary>
    public Action OnAISpeakDone;
    #endregion

    #region 语音输入
    [Header("语音输入")]
    /// <summary>
    /// 语音识别返回的文本是否直接发送给LLM
    /// </summary>
    [SerializeField] private bool m_AutoSend = true;
    /// <summary>
    /// 语音提示面板
    /// </summary>
    [SerializeField] private GameObject m_VoiceTipPanel;
    /// <summary>
    /// 录音状态提示信息
    /// </summary>
    [SerializeField] private Text m_RecordTips;
    #endregion

    #region 聊天历史
    [Header("聊天历史")]
    /// <summary>
    /// 聊天历史记录
    /// </summary>
    [SerializeField] private List<string> m_ChatHistory = new List<string>();
    /// <summary>
    /// 临时聊天气泡对象列表
    /// </summary>
    [SerializeField] private List<GameObject> m_TempChatBox = new List<GameObject>();
    /// <summary>
    /// 聊天历史显示面板
    /// </summary>
    [SerializeField] private GameObject m_HistoryPanel;
    /// <summary>
    /// 聊天气泡父级容器
    /// </summary>
    [SerializeField] private RectTransform m_rootTrans;
    /// <summary>
    /// 用户消息预制体
    /// </summary>
    [SerializeField] private ChatPrefab m_PostChatPrefab;
    /// <summary>
    /// AI回复消息预制体
    /// </summary>
    [SerializeField] private ChatPrefab m_RobotChatPrefab;
    /// <summary>
    /// 滚动视图
    /// </summary>
    [SerializeField] private ScrollRect m_ScroTectObject;
    #endregion

    #region 打字效果
    [Header("打字效果")]
    /// <summary>
    /// 逐字显示的时间间隔
    /// </summary>
    [SerializeField] private float m_WordWaitTime = 0.2f;
    /// <summary>
    /// 是否正在显示文字
    /// </summary>
    [SerializeField] private bool m_WriteState = false;
    #endregion

    #region Unity生命周期
    private void Awake()
    {
        try
        {
            // 安全地添加按钮监听器
            if (m_CommitMsgBtn != null)
            {
                m_CommitMsgBtn.onClick.RemoveAllListeners();
                m_CommitMsgBtn.onClick.AddListener(() => SendData());
                Debug.Log("发送按钮监听器添加成功");
            }
            else
            {
                Debug.LogError("发送按钮未赋值！请在Inspector中设置m_CommitMsgBtn");
            }
            
            InputSettingWhenWebgl();
            
            // 初始化组件检查
            ValidateComponents();
            
            Debug.Log("RTChatSample初始化完成");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RTChatSample初始化失败: {e.Message}\n{e.StackTrace}");
        }
    }

    void Start()
    {
        try
        {
            // 连接SignalManager的录音完成事件
            ConnectToSignalManager();
            
            // 初始化UI状态
            InitializeUI();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RTChatSample启动失败: {e.Message}");
        }
    }
    #endregion

    #region 组件验证和初始化
    /// <summary>
    /// 验证必要组件
    /// </summary>
    private void ValidateComponents()
    {
        if (m_ChatSettings == null)
            Debug.LogWarning("ChatSettings未设置！某些功能可能无法使用");
        
        if (m_InputWord == null)
            Debug.LogError("输入框组件未设置！");
        
        if (m_TextBack == null)
            Debug.LogError("回复文本组件未设置！");
        
        if (m_AudioSource == null)
            Debug.LogWarning("音频播放组件未设置！语音功能将无法使用");
    }

    /// <summary>
    /// 初始化UI状态
    /// </summary>
    private void InitializeUI()
    {
        if (m_TextBack != null)
            m_TextBack.text = "你好！我是AI助手，有什么可以帮助您的吗？";
        
        if (m_RecordTips != null)
            m_RecordTips.text = "";
        
        if (m_VoiceTipPanel != null)
            m_VoiceTipPanel.SetActive(false);
        
        // 确保聊天面板显示，历史面板隐藏
        if (m_ChatPanel != null) m_ChatPanel.SetActive(true);
        if (m_HistoryPanel != null) m_HistoryPanel.SetActive(false);
    }

    /// <summary>
    /// 连接到SignalManager
    /// </summary>
    private void ConnectToSignalManager()
    {
        SignalManager signalManager = FindObjectOfType<SignalManager>();
        if (signalManager != null)
        {
            // 订阅录音完成事件
            signalManager.onAudioClipDone += AcceptClip;
            Debug.Log("成功连接到SignalManager");
        }
        else
        {
            Debug.LogWarning("场景中没有找到SignalManager，语音录制功能将无法使用");
        }
    }
    #endregion

    #region WebGL输入支持
    /// <summary>
    /// WebGL平台输入框支持
    /// </summary>
    private void InputSettingWhenWebgl()
    {
        #if UNITY_WEBGL
        if (m_InputWord != null)
        {
            m_InputWord.gameObject.AddComponent<WebGLSupport.WebGLInput>();
        }
        #endif
    }
    #endregion

    #region 消息发送
    /// <summary>
    /// 发送输入框中的消息
    /// </summary>
    public void SendData()
    {
        try
        {
            if (m_InputWord == null || string.IsNullOrEmpty(m_InputWord.text.Trim()))
            {
                Debug.LogWarning("输入内容为空");
                return;
            }

            string message = m_InputWord.text.Trim();
            SendData(message);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"发送消息失败: {e.Message}");
        }
    }

    /// <summary>
    /// 发送指定字符串消息
    /// </summary>
    /// <param name="_postWord">要发送的消息</param>
    public void SendData(string _postWord)
    {
        try
        {
            if (string.IsNullOrEmpty(_postWord?.Trim()))
            {
                Debug.LogWarning("发送消息为空");
                return;
            }

            // 添加到聊天记录
            m_ChatHistory.Add(_postWord);
            Debug.Log($"用户发送: {_postWord}");

            // 检查ChatSettings和ChatModel
            if (m_ChatSettings == null || m_ChatSettings.m_ChatModel == null)
            {
                Debug.LogError("ChatSettings或ChatModel未配置！");
                ShowErrorMessage("AI配置错误，请检查设置");
                return;
            }

            // 发送给AI模型
            m_ChatSettings.m_ChatModel.PostMsg(_postWord, CallBack);

            // 清空输入框并显示等待状态
            if (m_InputWord != null) m_InputWord.text = "";
            if (m_TextBack != null) m_TextBack.text = "AI正在思考中...";

            // 切换到思考动画状态
            SetAnimator("state", 1);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"发送消息异常: {e.Message}");
            ShowErrorMessage("发送失败，请重试");
        }
    }

    /// <summary>
    /// AI回复消息的回调
    /// </summary>
    /// <param name="_response">AI回复内容</param>
    private void CallBack(string _response)
    {
        try
        {
            _response = _response?.Trim() ?? "";
            
            if (string.IsNullOrEmpty(_response))
            {
                Debug.LogWarning("AI回复为空");
                ShowErrorMessage("AI回复异常");
                return;
            }

            if (m_TextBack != null) m_TextBack.text = "";

            Debug.Log($"收到AI回复: {_response}");

            // 记录回复
            m_ChatHistory.Add(_response);

            // 根据语音模式选择处理方式
            if (!m_IsVoiceMode || m_ChatSettings?.m_TextToSpeech == null)
            {
                // 直接显示文本
                StartTypeWords(_response);
                return;
            }

            // 语音合成
            try
            {
                m_ChatSettings.m_TextToSpeech.Speak(_response, PlayVoice);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"语音合成失败: {e.Message}");
                // 降级到文本显示
                StartTypeWords(_response);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理AI回复失败: {e.Message}");
            ShowErrorMessage("处理回复失败");
        }
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    private void ShowErrorMessage(string errorMsg)
    {
        if (m_TextBack != null)
        {
            m_TextBack.text = errorMsg;
        }
        
        // 切换到等待动画状态
        SetAnimator("state", 0);
    }
    #endregion

    #region 语音输入处理
    /// <summary>
    /// 接收录制的音频剪辑
    /// </summary>
    /// <param name="_audioClip">录制的音频</param>
    public void AcceptClip(AudioClip _audioClip)
    {
        try
        {
            if (_audioClip == null)
            {
                Debug.LogWarning("接收到的音频剪辑为空");
                return;
            }

            if (m_ChatSettings?.m_SpeechToText == null)
            {
                Debug.LogError("语音转文字组件未配置！");
                ShowRecordTip("语音识别功能未配置");
                return;
            }

            Debug.Log($"接收到音频剪辑，时长: {_audioClip.length:F2}秒");
            ShowRecordTip("正在进行语音识别...");
            
            // 显示语音提示面板
            if (m_VoiceTipPanel != null)
                m_VoiceTipPanel.SetActive(true);

            // 进行语音转文字
            m_ChatSettings.m_SpeechToText.SpeechToText(_audioClip, DealingTextCallback);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理音频剪辑失败: {e.Message}");
            ShowRecordTip("语音处理失败");
        }
    }

    /// <summary>
    /// 处理语音识别到的文本
    /// </summary>
    /// <param name="_msg">识别到的文字</param>
    private void DealingTextCallback(string _msg)
    {
        try
        {
            if (string.IsNullOrEmpty(_msg?.Trim()))
            {
                Debug.LogWarning("语音识别结果为空");
                ShowRecordTip("未识别到有效内容");
                StartCoroutine(HideRecordTipAfterDelay());
                return;
            }

            Debug.Log($"语音识别结果: {_msg}");
            ShowRecordTip($"识别结果: {_msg}");
            
            // 根据设置决定是否自动发送
            if (m_AutoSend)
            {
                // 自动发送给AI
                SendData(_msg);
                StartCoroutine(HideRecordTipAfterDelay());
            }
            else
            {
                // 填入输入框，等待用户确认
                if (m_InputWord != null)
                    m_InputWord.text = _msg;
                StartCoroutine(HideRecordTipAfterDelay());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理语音识别结果失败: {e.Message}");
            ShowRecordTip("处理识别结果失败");
        }
    }

    /// <summary>
    /// 显示录音提示
    /// </summary>
    private void ShowRecordTip(string tip)
    {
        if (m_RecordTips != null)
        {
            m_RecordTips.text = tip;
        }
    }

    /// <summary>
    /// 延迟隐藏录音提示
    /// </summary>
    private IEnumerator HideRecordTipAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        
        if (m_RecordTips != null)
            m_RecordTips.text = "";
        
        if (m_VoiceTipPanel != null)
            m_VoiceTipPanel.SetActive(false);
    }
    #endregion

    #region 语音播放
    /// <summary>
    /// 播放AI回复的语音
    /// </summary>
    /// <param name="_clip">语音剪辑</param>
    /// <param name="_response">对应的文本</param>
    private void PlayVoice(AudioClip _clip, string _response)
    {
        try
        {
            if (_clip == null)
            {
                Debug.LogWarning("语音剪辑为空，直接显示文本");
                StartTypeWords(_response);
                return;
            }

            if (m_AudioSource == null)
            {
                Debug.LogError("音频播放组件未设置！");
                StartTypeWords(_response);
                return;
            }

            m_AudioSource.clip = _clip;
            m_AudioSource.Play();
            Debug.Log($"播放语音，时长: {_clip.length:F2}秒");
            
            // 开始逐字显示回复文本
            StartTypeWords(_response);
            
            // 切换到说话动画状态
            SetAnimator("state", 2);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"播放语音失败: {e.Message}");
            StartTypeWords(_response);
        }
    }
    #endregion

    #region 逐字显示效果
    /// <summary>
    /// 开始逐字打印效果
    /// </summary>
    /// <param name="_msg">要显示的消息</param>
    private void StartTypeWords(string _msg)
    {
        try
        {
            if (string.IsNullOrEmpty(_msg))
            {
                Debug.LogWarning("要显示的消息为空");
                return;
            }

            m_WriteState = true;
            StartCoroutine(SetTextPerWord(_msg));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"开始打字效果失败: {e.Message}");
            // 直接显示完整文本作为降级方案
            if (m_TextBack != null)
                m_TextBack.text = _msg;
        }
    }

    /// <summary>
    /// 逐字显示文本的协程
    /// </summary>
    private IEnumerator SetTextPerWord(string _msg)
    {
        int currentPos = 0;
        
        while (m_WriteState && currentPos < _msg.Length)
        {
            yield return new WaitForSeconds(m_WordWaitTime);
            currentPos++;
            
            // 逐字显示文本
            if (m_TextBack != null)
            {
                m_TextBack.text = _msg.Substring(0, currentPos);
            }
            
            m_WriteState = currentPos < _msg.Length;
        }

        // 切换到等待动画状态
        SetAnimator("state", 0);

        // 回复完成回调
        try
        {
            OnAISpeakDone?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AI回复完成回调失败: {e.Message}");
        }
    }
    #endregion

    #region 聊天历史
    /// <summary>
    /// 打开聊天历史面板
    /// </summary>
    public void OpenAndGetHistory()
    {
        try
        {
            if (m_ChatPanel != null) m_ChatPanel.SetActive(false);
            if (m_HistoryPanel != null) m_HistoryPanel.SetActive(true);

            ClearChatBox();
            StartCoroutine(GetHistoryChatInfo());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"打开聊天历史失败: {e.Message}");
        }
    }

    /// <summary>
    /// 返回聊天模式
    /// </summary>
    public void BackChatMode()
    {
        try
        {
            if (m_ChatPanel != null) m_ChatPanel.SetActive(true);
            if (m_HistoryPanel != null) m_HistoryPanel.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"返回聊天模式失败: {e.Message}");
        }
    }

    /// <summary>
    /// 清除已创建的对话框
    /// </summary>
    private void ClearChatBox()
    {
        try
        {
            foreach (GameObject chatBox in m_TempChatBox)
            {
                if (chatBox != null)
                {
                    Destroy(chatBox);
                }
            }
            m_TempChatBox.Clear();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"清除聊天框失败: {e.Message}");
        }
    }

    /// <summary>
    /// 获取聊天历史记录列表
    /// </summary>
    private IEnumerator GetHistoryChatInfo()
    {
        yield return new WaitForEndOfFrame();

        try
        {
            for (int i = 0; i < m_ChatHistory.Count; i++)
            {
                if (i % 2 == 0)
                {
                    // 用户消息
                    if (m_PostChatPrefab != null && m_rootTrans != null)
                    {
                        ChatPrefab sendChat = Instantiate(m_PostChatPrefab, m_rootTrans.transform);
                        sendChat.SetText(m_ChatHistory[i]);
                        m_TempChatBox.Add(sendChat.gameObject);
                    }
                }
                else
                {
                    // AI回复
                    if (m_RobotChatPrefab != null && m_rootTrans != null)
                    {
                        ChatPrefab reChat = Instantiate(m_RobotChatPrefab, m_rootTrans.transform);
                        reChat.SetText(m_ChatHistory[i]);
                        m_TempChatBox.Add(reChat.gameObject);
                    }
                }
            }

            // 刷新布局并滚动到最新消息
            if (m_rootTrans != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_rootTrans);
            
            StartCoroutine(TurnToLastLine());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取聊天历史失败: {e.Message}");
        }
    }

    /// <summary>
    /// 滚动到最新消息
    /// </summary>
    private IEnumerator TurnToLastLine()
    {
        yield return new WaitForEndOfFrame();
        
        try
        {
            if (m_ScroTectObject != null)
            {
                m_ScroTectObject.verticalNormalizedPosition = 0;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"滚动到最新消息失败: {e.Message}");
        }
    }
    #endregion

    #region 动画控制
    /// <summary>
    /// 设置动画参数
    /// </summary>
    /// <param name="_para">参数名</param>
    /// <param name="_value">参数值</param>
    private void SetAnimator(string _para, int _value)
    {
        try
        {
            if (m_Animator != null)
            {
                m_Animator.SetInteger(_para, _value);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"设置动画参数失败: {e.Message}");
        }
    }
    #endregion

    #region 清理
    void OnDestroy()
    {
        try
        {
            // 取消订阅SignalManager事件
            SignalManager signalManager = FindObjectOfType<SignalManager>();
            if (signalManager != null)
            {
                signalManager.onAudioClipDone -= AcceptClip;
            }
            
            Debug.Log("RTChatSample已清理");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RTChatSample清理失败: {e.Message}");
        }
    }
    #endregion
}