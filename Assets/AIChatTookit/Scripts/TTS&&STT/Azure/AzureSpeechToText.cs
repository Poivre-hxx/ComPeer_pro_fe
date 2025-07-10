using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(AzureSettings))]
public class AzureSpeechToText : STT
{
    /// <summary>
    /// Azure配置组件
    /// </summary>
    [SerializeField] private AzureSettings m_AzureSettings;
    public string mode = "conversation";

    private void Awake()
    {
        // Initialize the audio source
        m_AzureSettings = this.GetComponent<AzureSettings>();
        GetUrl();
    }
    
    /// <summary>
    /// 拼接URL
    /// </summary>
    private void GetUrl()
    {
        if (m_AzureSettings == null)
            return;

        m_SpeechRecognizeURL = "https://" +
            m_AzureSettings.serviceRegion +
            ".stt.speech.microsoft.com/speech/recognition/" + mode + "/cognitiveservices/v1?language=" +
            m_AzureSettings.language;
            
        Debug.Log("STT URL: " + m_SpeechRecognizeURL);
    }
    
    /// <summary>
    /// 语音识别
    /// </summary>
    /// <param name="_clip"></param>
    /// <param name="_callback"></param>
    public override void SpeechToText(AudioClip _clip, Action<string> _callback)
    {
        byte[] _audioData = WavUtility.FromAudioClip(_clip);
        StartCoroutine(SendAudioData(_audioData, _callback));
    }

    /// <summary>
    /// 语音识别
    /// </summary>
    /// <param name="_audioData"></param>
    /// <param name="_callback"></param>
    public override void SpeechToText(byte[] _audioData, Action<string> _callback)
    {
        StartCoroutine(SendAudioData(_audioData, _callback));
    }

    /// <summary>
    /// 识别音频
    /// </summary>
    /// <param name="audioData"></param>
    /// <param name="_callback"></param>
    /// <returns></returns>
    private IEnumerator SendAudioData(byte[] audioData, Action<string> _callback)
    {
        stopwatch.Restart();
        
        // 🔍 音频数据验证
        Debug.Log("=== 音频数据检查 ===");
        Debug.Log("音频数据大小: " + (audioData?.Length ?? 0) + " bytes");
        
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError("❌ 音频数据为空！");
            yield break;
        }
        
        if (audioData.Length < 1000)
        {
            Debug.LogWarning("⚠️ 音频数据太小，可能录音时间不够: " + audioData.Length + " bytes");
        }
        
        // 检查WAV文件头
        if (audioData.Length > 12)
        {
            string header = System.Text.Encoding.ASCII.GetString(audioData, 0, 4);
            Debug.Log("音频文件头: " + header);
            if (header != "RIFF")
            {
                Debug.LogWarning("⚠️ 可能不是有效的WAV文件格式");
            }
        }
        
        // 🔧 修复1: 使用和TTS相同的UnityWebRequest创建方式
        using (UnityWebRequest request = new UnityWebRequest(m_SpeechRecognizeURL, "POST"))
        {
            // 🔧 修复2: 正确设置uploadHandler和downloadHandler
            request.uploadHandler = new UploadHandlerRaw(audioData);
            request.downloadHandler = new DownloadHandlerBuffer();

            // 🔧 修复3: 根据实际录音设置调整请求头
            request.SetRequestHeader("Ocp-Apim-Subscription-Key", m_AzureSettings.subscriptionKey);
            
            // 尝试不同的Content-Type格式
            request.SetRequestHeader("Content-Type", "audio/wav");
            // 备用格式：request.SetRequestHeader("Content-Type", "audio/wav; codecs=audio/pcm; samplerate=44100");
            
            request.SetRequestHeader("Accept", "application/json");

            Debug.Log("发送STT请求到: " + m_SpeechRecognizeURL);

            // Send the request and wait for the response
            yield return request.SendWebRequest();

            // 详细的响应检查
            Debug.Log("=== STT响应检查 ===");
            Debug.Log("响应码: " + request.responseCode);
            Debug.Log("响应状态: " + request.result);
            
            // Check for errors
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Speech recognition request failed: " + request.error);
                Debug.LogError("Response Code: " + request.responseCode);
                if (request.downloadHandler != null)
                {
                    Debug.LogError("Response: " + request.downloadHandler.text);
                }
                yield break;
            }

            // Parse the response JSON and extract the recognition result
            string json = request.downloadHandler.text;
            Debug.Log("STT完整响应: " + json);
            
            // 检查响应是否为空
            if (string.IsNullOrEmpty(json) || json.Trim() == "")
            {
                Debug.LogError("❌ 服务器返回空响应！可能是音频格式问题。");
                Debug.LogError("建议检查：1.录音时长是否足够 2.音频格式是否正确 3.是否包含实际语音内容");
                yield break;
            }
            
            try
            {
                SpeechRecognitionResult result = JsonUtility.FromJson<SpeechRecognitionResult>(json);
                
                Debug.Log("识别状态: " + result.RecognitionStatus);
                Debug.Log("识别文本: " + result.DisplayText);
                
                if (string.IsNullOrEmpty(result.DisplayText))
                {
                    Debug.LogWarning("⚠️ 识别结果为空，可能原因：1.音频中没有清晰语音 2.语言设置不匹配 3.音频质量太差");
                }
                
                _callback(result.DisplayText ?? "");
            }
            catch (Exception e)
            {
                Debug.LogError("解析JSON失败: " + e.Message);
                Debug.LogError("JSON内容: " + json);
                
                // 尝试手动解析
                if (json.Contains("DisplayText"))
                {
                    Debug.Log("尝试手动提取DisplayText...");
                    // 简单的字符串提取作为备用方案
                    int start = json.IndexOf("\"DisplayText\":\"") + 15;
                    if (start > 14)
                    {
                        int end = json.IndexOf("\"", start);
                        if (end > start)
                        {
                            string text = json.Substring(start, end - start);
                            Debug.Log("手动提取的文本: " + text);
                            _callback(text);
                        }
                    }
                }
            }
        }

        stopwatch.Stop();
        Debug.Log("Azure语音识别耗时：" + stopwatch.Elapsed.TotalSeconds);
    }
}

[System.Serializable]
public class SpeechRecognitionResult
{
    public string RecognitionStatus;
    public string DisplayText;
}