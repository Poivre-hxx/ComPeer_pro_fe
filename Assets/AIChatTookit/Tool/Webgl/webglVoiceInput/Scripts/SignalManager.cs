using System;
using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// SignalManager - 专门为Android平台设计
/// </summary>
public class SignalManager : MonoBehaviour
{
    #region Public Fields
    /// <summary>
    /// 语音录制完成回调
    /// </summary>
    public Action<AudioClip> onAudioClipDone;
    
    /// <summary>
    /// 录制状态变化回调
    /// </summary>
    public Action<bool> onRecordingStateChanged;
    #endregion
    
    #region Private Fields
    private bool m_IsRecording = false;
    private string m_MicrophoneDevice = null;
    private AudioSource m_AudioSource;
    private const int MAX_RECORD_TIME = 30; // 最大录制时间（秒）
    private const int SAMPLE_RATE = 44100;  // 采样率
    #endregion
    
    #region Unity Lifecycle
    void Start()
    {
        try
        {
            Debug.Log("SignalManager Android版本启动");
            InitializeAudioSource();
            CheckMicrophoneDevices();
            
            // 如果没有麦克风权限，先请求
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.Log("请求麦克风权限...");
                Permission.RequestUserPermission(Permission.Microphone);
            }
            
            Debug.Log("SignalManager初始化完成");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SignalManager启动失败: {e.Message}");
        }
    }
    
    void OnDestroy()
    {
        try
        {
            // 确保停止录音
            if (m_IsRecording)
            {
                StopRecordBinding();
            }
            Debug.Log("SignalManager已清理");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SignalManager清理失败: {e.Message}");
        }
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// 开始录制
    /// </summary>
    public void StartRecordBinding()
    {
        try
        {
            Debug.Log("尝试开始录制");
            
            if (m_IsRecording)
            {
                Debug.LogWarning("录音已在进行中");
                return;
            }
            
            // 检查权限
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.LogError("没有麦克风权限");
                Permission.RequestUserPermission(Permission.Microphone);
                return;
            }
            
            // 检查麦克风设备
            if (string.IsNullOrEmpty(m_MicrophoneDevice))
            {
                Debug.LogError("没有可用的麦克风设备");
                return;
            }
            
            // 开始录音
            m_AudioSource.clip = Microphone.Start(m_MicrophoneDevice, false, MAX_RECORD_TIME, SAMPLE_RATE);
            
            if (m_AudioSource.clip == null)
            {
                Debug.LogError("麦克风启动失败");
                return;
            }
            
            m_IsRecording = true;
            Debug.Log($"录音开始 - 设备: {m_MicrophoneDevice}, 最大时长: {MAX_RECORD_TIME}秒");
            
            // 触发状态变化回调
            onRecordingStateChanged?.Invoke(true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"开始录制失败: {e.Message}");
            m_IsRecording = false;
            onRecordingStateChanged?.Invoke(false);
        }
    }
    
    /// <summary>
    /// 停止录制
    /// </summary>
    public void StopRecordBinding()
    {
        try
        {
            Debug.Log("尝试停止录制");
            
            if (!m_IsRecording)
            {
                Debug.LogWarning("当前没有在录音");
                return;
            }
            
            // 停止录音
            Microphone.End(m_MicrophoneDevice);
            m_IsRecording = false;
            
            // 获取录制的音频
            if (m_AudioSource != null && m_AudioSource.clip != null)
            {
                AudioClip recordedClip = m_AudioSource.clip;
                Debug.Log($"录音完成 - 时长: {recordedClip.length:F2}秒, 采样数: {recordedClip.samples}");
                
                // 触发录音完成回调
                try
                {
                    onAudioClipDone?.Invoke(recordedClip);
                    Debug.Log("录音回调执行成功");
                }
                catch (System.Exception callbackException)
                {
                    Debug.LogError($"录音回调执行失败: {callbackException.Message}");
                }
            }
            else
            {
                Debug.LogError("录音数据无效");
            }
            
            // 触发状态变化回调
            onRecordingStateChanged?.Invoke(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"停止录制失败: {e.Message}");
            m_IsRecording = false;
            onRecordingStateChanged?.Invoke(false);
        }
    }
    
    /// <summary>
    /// 获取录音状态
    /// </summary>
    public bool IsRecording()
    {
        return m_IsRecording;
    }
    
    /// <summary>
    /// 获取录音音量（用于UI显示）
    /// </summary>
    public float GetRecordingVolume()
    {
        if (!m_IsRecording || m_AudioSource.clip == null)
            return 0f;
        
        try
        {
            int micPosition = Microphone.GetPosition(m_MicrophoneDevice);
            if (micPosition < 128) return 0f;
            
            float[] samples = new float[128];
            m_AudioSource.clip.GetData(samples, micPosition - 128);
            
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += Mathf.Abs(samples[i]);
            }
            
            return sum / samples.Length;
        }
        catch
        {
            return 0f;
        }
    }
    
    /// <summary>
    /// 播放录制的音频
    /// </summary>
    public void PlayRecordedAudio()
    {
        if (m_AudioSource.clip != null && !m_IsRecording)
        {
            m_AudioSource.Play();
            Debug.Log("播放录制的音频");
        }
        else
        {
            Debug.LogWarning("没有可播放的音频或正在录制中");
        }
    }
    
    /// <summary>
    /// 停止播放
    /// </summary>
    public void StopPlayback()
    {
        if (m_AudioSource.isPlaying)
        {
            m_AudioSource.Stop();
            Debug.Log("停止播放");
        }
    }
    
    /// <summary>
    /// 保存录音到文件
    /// </summary>
    public string SaveRecordingToFile()
    {
        if (m_AudioSource.clip == null)
        {
            Debug.LogWarning("没有录音数据可保存");
            return null;
        }
        
        try
        {
            string fileName = $"recording_{System.DateTime.Now:yyyyMMdd_HHmmss}.wav";
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            
            SaveAudioClipAsWav(m_AudioSource.clip, filePath);
            Debug.Log($"录音已保存至: {filePath}");
            
            return filePath;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存录音失败: {e.Message}");
            return null;
        }
    }
    #endregion
    
    #region Private Methods
    /// <summary>
    /// 初始化AudioSource组件
    /// </summary>
    private void InitializeAudioSource()
    {
        m_AudioSource = GetComponent<AudioSource>();
        if (m_AudioSource == null)
        {
            m_AudioSource = gameObject.AddComponent<AudioSource>();
            Debug.Log("创建了新的AudioSource组件");
        }
        
        // 配置AudioSource
        m_AudioSource.playOnAwake = false;
        m_AudioSource.volume = 1.0f;
    }
    
    /// <summary>
    /// 检查麦克风设备
    /// </summary>
    private void CheckMicrophoneDevices()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogError("没有找到麦克风设备");
            return;
        }
        
        m_MicrophoneDevice = Microphone.devices[0];
        Debug.Log($"找到麦克风设备: {m_MicrophoneDevice}");
        
        // 打印所有可用设备
        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            Debug.Log($"麦克风设备 {i}: {Microphone.devices[i]}");
        }
    }
    
    /// <summary>
    /// 保存AudioClip为WAV文件
    /// </summary>
    private void SaveAudioClipAsWav(AudioClip clip, string filePath)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        
        using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
        using (var writer = new System.IO.BinaryWriter(fileStream))
        {
            // WAV文件头
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + samples.Length * 2);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(samples.Length * 2);
            
            // 音频数据
            foreach (var sample in samples)
            {
                writer.Write((short)(sample * 32767));
            }
        }
    }
    #endregion
}