using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

#if !UNITY_WEBGL
/// <summary>
/// 语音处理器
/// </summary>
public class RTSpeechHandler : MonoBehaviour
{
    /// <summary>
    /// 语音设备名称
    /// </summary>
    public string m_MicrophoneName = null;
    
    /// <summary>
    /// 用于判定是否处于静音状态，低于此值认为静音状态
    /// </summary>
    public float m_SilenceThreshold = 0.01f;
    
    /// <summary>
    /// 沉默限制时长
    /// </summary>
    [Header("设置静音检测间隔没有声音，就停止录音状态")]
    public float m_RecordingTimeLimit = 2.0f;
    
    /// <summary>
    /// 对话状态保持时长
    /// </summary>
    [Header("设置对话状态保持时间")]
    public float m_LossAwakeTimeLimit = 10f;
    
    /// <summary>
    /// 锁定状态（处理中，不检测静音）
    /// </summary>
    [SerializeField]private bool m_LockState = false;
    
    /// <summary>
    /// 音频
    /// </summary>
    private AudioClip m_RecordedClip;

    /// <summary>
    /// 唤醒状态
    /// </summary>
    [Header("标识当前是否处于唤醒状态")]
    [SerializeField]private bool m_AwakeState = false;
    
    /// <summary>
    /// 录音状态
    /// </summary>
    [SerializeField] private bool m_IsRecording = false;
    
    /// <summary>
    /// 沉默计时器
    /// </summary>
    [SerializeField]private float m_SilenceTimer = 0.0f;
    
    /// <summary>
    /// 聊天接口
    /// </summary>
    [SerializeField]private RTChatSample m_ChatSample;
    
    /// <summary>
    /// 语音唤醒
    /// </summary>
    [SerializeField] private RokidWOV m_VoiceAWake;
    
    [Header("连续对话设置")]
    [SerializeField] private bool m_EnableContinuousDialog = true;  // 是否启用连续对话
    private float m_LastInteractionTime = 0f;                       // 最后一次交互时间
    private Coroutine m_DetectCoroutine = null;                    // 检测协程引用

    [SerializeField]private AudioSource m_Greeting;
    [SerializeField] private AudioClip m_GreatingVoice;
    
    [SerializeField] private Text m_PrintText;

    private void Awake()
    {
        OnInit();
    }

    private void OnInit()
    {
        //AI回复结束时回调
        m_ChatSample.OnAISpeakDone += SpeachDoneCallBack;
    }

    private void Start()
    {
        if (m_MicrophoneName == null)
        {
            // 如果没有指定麦克风名称，则使用第一个可用麦克风
            if (Microphone.devices.Length > 0)
            {
                m_MicrophoneName = Microphone.devices[0];
            }
            else
            {
                Debug.LogError("没有检测到麦克风设备！");
                PrintLog("错误：没有检测到麦克风");
                return;
            }
        }

        // 不要在这里启动麦克风，等待唤醒后再启动
        StartVoiceListening();
    }

    /// <summary>
    /// 唤醒回调 - 这个方法名必须与RokidWOV中的唤醒逻辑匹配
    /// </summary>
    public void AwakeCallBack(string _msg)
    {
        if (!m_AwakeState)
        {
            m_AwakeState = true;
            Debug.Log("识别到关键词：" + _msg);
            PrintLog($"Link->已识别: {_msg}");
            
            // 记录唤醒时间
            m_LastInteractionTime = Time.time;
            
            if (m_Greeting)
            {
                m_Greeting.clip = m_GreatingVoice;
                m_Greeting.Play();
            }
            
            // 唤醒后启动麦克风
            StartMicrophoneForDialog();
        }
    }
    
    /// <summary>
    /// 为对话启动麦克风
    /// </summary>
    private void StartMicrophoneForDialog()
    {
        if (Microphone.IsRecording(m_MicrophoneName))
        {
            Microphone.End(m_MicrophoneName);
        }
        
        m_RecordedClip = Microphone.Start(m_MicrophoneName, false, 30, 16000);
        while (Microphone.GetPosition(null) <= 0) { }
        
        // 如果检测协程未运行，启动它
        if (m_DetectCoroutine == null)
        {
            m_DetectCoroutine = StartCoroutine(DetectRecording());
        }
    }
    
    /// <summary>
    /// 开始监听语音关键词
    /// </summary>
    private void StartVoiceListening()
    {
        // RokidWOV会自动开始监听，不需要手动启动
        PrintLog("开始->识别语音关键词");
    }

    /// <summary>
    /// 停止唤醒监听
    /// </summary>
    private void StopVoiceListening()
    {
        // RokidWOV会持续监听，不需要手动停止
        PrintLog("结束->语音关键词识别");
    }

    /// <summary>
    /// 开始监听说话声音
    /// </summary>
    private void StartRecording()
    {
        m_SilenceTimer = 0.0f; // 重置静音计时器
        m_IsRecording = true;
        PrintLog("正在录音对话...");
        
        // 更新最后交互时间
        m_LastInteractionTime = Time.time;
        
        // 停止监听，并重新开始录音，避免之前唤醒的内容声音丢失
        Microphone.End(m_MicrophoneName);
        m_RecordedClip = Microphone.Start(m_MicrophoneName, false, 30, 16000);
    }
    
    /// <summary>
    /// 结束说话
    /// </summary>
    private void StopRecording()
    {
        m_IsRecording = false;
        PrintLog("话音录音结束...");

        // 停止麦克风监听
        Microphone.End(m_MicrophoneName);

        // 处理音频数据
        SetRecordedAudio();
    }

    /// <summary>
    /// 开始录音监听
    /// </summary>
    public void ReStartRecord()
    {
        m_RecordedClip = Microphone.Start(m_MicrophoneName, true, 30, 16000);
        m_LockState = false;
    }

    private void SetRecordedAudio()
    {
        m_LockState = true;
        m_ChatSample.AcceptClip(m_RecordedClip);
    }

    /// <summary>
    /// 更新交互时间（公开方法，供其他组件调用）
    /// </summary>
    public void UpdateInteractionTime()
    {
        m_LastInteractionTime = Time.time;
    }

    /// <summary>
    /// 对话结束时回调，启动麦克风检测
    /// </summary>
    private void SpeachDoneCallBack()
    {
        // 更新最后交互时间（AI说话结束时）
        m_LastInteractionTime = Time.time;
        
        m_LockState = false;
        
        if (m_EnableContinuousDialog && m_AwakeState)
        {
            // 连续对话模式：保持麦克风开启
            PrintLog($"请继续说话 (或等待{m_LossAwakeTimeLimit}秒退出)");
            
            // 重新启动麦克风
            ReStartRecord();
        }
        else
        {
            // 单次对话模式：关闭麦克风
            if (Microphone.IsRecording(m_MicrophoneName))
            {
                Microphone.End(m_MicrophoneName);
            }
            
            m_AwakeState = false;
            m_IsRecording = false;
            
            if (m_DetectCoroutine != null)
            {
                StopCoroutine(m_DetectCoroutine);
                m_DetectCoroutine = null;
            }
            
            PrintLog("等待唤醒词...");
        }
    }

    /// <summary>
    /// 开始检测声音
    /// </summary>
    /// <returns></returns>
    private IEnumerator DetectRecording()
    {
        while (true)
        {
            // 检查是否在对话会话中
            if (m_EnableContinuousDialog && m_AwakeState)
            {
                // 检查会话是否超时
                if (Time.time - m_LastInteractionTime > m_LossAwakeTimeLimit)
                {
                    // 会话超时，退出对话模式
                    m_AwakeState = false;
                    m_IsRecording = false;
                    m_LockState = false;
                    
                    if (Microphone.IsRecording(m_MicrophoneName))
                    {
                        Microphone.End(m_MicrophoneName);
                    }
                    
                    PrintLog("对话超时，等待唤醒词...");
                    yield break; // 结束协程
                }
            }
            
            float[] samples = new float[128]; // 采样缓冲区大小
            int position = Microphone.GetPosition(null);
            
            // 确保有足够的数据可以读取
            if (position < samples.Length)
            {
                yield return null;
                continue;
            }

            // 检查AudioClip是否有效且有足够数据
            if (m_RecordedClip == null || m_RecordedClip.samples < samples.Length)
            {
                yield return null;
                continue;
            }

            // 确保读取位置有效
            int readPosition = position - samples.Length;
            if (readPosition < 0 || readPosition + samples.Length > m_RecordedClip.samples)
            {
                yield return null;
                continue;
            }

            // 尝试更稳健的数据读取处理
            bool dataReadSuccess = false;
            try 
            { 
                m_RecordedClip.GetData(samples, readPosition);
                dataReadSuccess = true;
            } 
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to read audio data: {e.Message}");
                dataReadSuccess = false;
            }

            // 没有成功读取数据则进行下次处理
            if (!dataReadSuccess)
            {
                yield return null;
                continue;
            }

            float rms = 0.0f;
            foreach (float sample in samples)
            {
                rms += sample * sample;
            }

            rms = Mathf.Sqrt(rms / samples.Length);

            if (rms > m_SilenceThreshold)
            {
                m_SilenceTimer = 0.0f; // 重置静音计时器

                // 已唤醒，启动录音
                if (m_AwakeState && !m_IsRecording && !m_LockState)
                {
                    StartRecording();
                }
            }
            else
            {
                if (!m_LockState)
                {
                    m_SilenceTimer += Time.deltaTime;
                }
                
                // 唤醒状态，结束说话
                if (m_AwakeState && m_IsRecording && m_SilenceTimer >= m_RecordingTimeLimit)
                {
                    StopRecording();
                }
            }
            
            // 显示剩余会话时间
            if (m_AwakeState && !m_IsRecording && !m_LockState)
            {
                float remainingTime = m_LossAwakeTimeLimit - (Time.time - m_LastInteractionTime);
                if (remainingTime > 0)
                {
                    PrintLog($"对话模式 (剩余: {remainingTime:F0}秒)");
                }
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// 打印日志
    /// </summary>
    /// <param name="_log"></param>
    private void PrintLog(string _log)
    {
        if (m_PrintText != null)
        {
            m_PrintText.text = _log;
        }
        Debug.Log($"RTSpeechHandler: {_log}");
    }
    
    private void OnDestroy()
    {
        // 清理资源
        if (Microphone.IsRecording(m_MicrophoneName))
        {
            Microphone.End(m_MicrophoneName);
        }
        
        if (m_DetectCoroutine != null)
        {
            StopCoroutine(m_DetectCoroutine);
        }
    }
}
#endif