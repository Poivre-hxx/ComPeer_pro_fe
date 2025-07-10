using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using Rokid.UXR.Module;

/// <summary>
/// Rokid离线语音唤醒实现
/// </summary>
public class RokidWOV : MonoBehaviour
{
    private bool isInit = false;
    
    [Header("连接的语音处理器")]
    public RTSpeechHandler m_SpeechHandler;

    private void Awake()
    {
        // 请求录音权限
        if (!Permission.HasUserAuthorizedPermission("android.permission.RECORD_AUDIO"))
        {
            Permission.RequestUserPermission("android.permission.RECORD_AUDIO");
        }
    }

    void Start()
    {
        // 检查权限
        if (!Permission.HasUserAuthorizedPermission("android.permission.RECORD_AUDIO"))
        {
            Debug.LogError("RokidWOV: 没有录音权限!");
            return;
        }

        // 自动寻找RTSpeechHandler组件
        if (m_SpeechHandler == null)
        {
            m_SpeechHandler = FindObjectOfType<RTSpeechHandler>();
            if (m_SpeechHandler != null)
            {
                Debug.Log("RokidWOV: Auto-connected to RTSpeechHandler");
            }
            else
            {
                Debug.LogWarning("RokidWOV: RTSpeechHandler not found");
            }
        }

        initVoice();
        OfflineVoiceModule.Instance.AddInstruct(LANGUAGE.CHINESE, "胡桃", "hu tao", this.gameObject.name, "OnReceive");
        OfflineVoiceModule.Instance.Commit();
    }

    private void OnDestroy()
    {
        Debug.Log("RokidWOV: OnDestroy");

        if (isInit)
        {
            OfflineVoiceModule.Instance.ClearAllInstruct();
            OfflineVoiceModule.Instance.Commit();
        }
    }
    
    /// <summary>
    /// 语音识别回调
    /// </summary>
    /// <param name="msg"></param>
    void OnReceive(string msg)
    {
        Debug.Log("RokidWOV: On Voice Response received : " + msg);

        if (string.Equals("胡桃", msg))
        {
            // 调用RTSpeechHandler的唤醒方法
            if (m_SpeechHandler != null)
            {
                m_SpeechHandler.AwakeCallBack(msg);
                Debug.Log("RokidWOV: Triggered RTSpeechHandler wake callback");
            }
            else
            {
                Debug.LogError("RokidWOV: RTSpeechHandler not connected");
            }
        }
        else
        {
            Debug.Log("RokidWOV: voice OnResponse: " + msg);
        }
    }
    
    /// <summary>
    /// 初始化声音模块 
    /// </summary>
    private void initVoice()
    {
        // Start plugin `VoiceControlFragment` , init once.
        if (!isInit)
        {
            Debug.Log("RokidWOV: start init voice.");
            ModuleManager.Instance.RegistModule("com.rokid.voicecommand.VoiceCommandHelper", false);
              
            //Should choose one of the language to use
            OfflineVoiceModule.Instance.ChangeVoiceCommandLanguage(LANGUAGE.CHINESE); //Support for CHINESE.
            //OfflineVoiceModule.Instance.ChangeVoiceCommandLanguage(LANGUAGE.ENGLISH); //Support for ENGLISH.

            isInit = true;
        }
    }
}