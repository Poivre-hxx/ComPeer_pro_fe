using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

#if !UNITY_WEBGL
/// <summary>
/// ����������
/// </summary>
public class RTSpeechHandler : MonoBehaviour
{
    /// <summary>
    /// �����豸����
    /// </summary>
    public string m_MicrophoneName = null;
    
    /// <summary>
    /// �����ж��Ƿ��ھ���״̬�����ڴ�ֵ��Ϊ����״̬
    /// </summary>
    public float m_SilenceThreshold = 0.01f;
    
    /// <summary>
    /// ��Ĭ����ʱ��
    /// </summary>
    [Header("���þ��������û����������ֹͣ¼��״̬")]
    public float m_RecordingTimeLimit = 2.0f;
    
    /// <summary>
    /// �Ի�״̬����ʱ��
    /// </summary>
    [Header("���öԻ�״̬����ʱ��")]
    public float m_LossAwakeTimeLimit = 10f;
    
    /// <summary>
    /// ����״̬�������У�����⾲����
    /// </summary>
    [SerializeField]private bool m_LockState = false;
    
    /// <summary>
    /// ��Ƶ
    /// </summary>
    private AudioClip m_RecordedClip;

    /// <summary>
    /// ����״̬
    /// </summary>
    [Header("��ʶ��ǰ�Ƿ��ڻ���״̬")]
    [SerializeField]private bool m_AwakeState = false;
    
    /// <summary>
    /// ¼��״̬
    /// </summary>
    [SerializeField] private bool m_IsRecording = false;
    
    /// <summary>
    /// ��Ĭ��ʱ��
    /// </summary>
    [SerializeField]private float m_SilenceTimer = 0.0f;
    
    /// <summary>
    /// ����ӿ�
    /// </summary>
    [SerializeField]private RTChatSample m_ChatSample;
    
    /// <summary>
    /// ��������
    /// </summary>
    [SerializeField] private RokidWOV m_VoiceAWake;
    
    [Header("�����Ի�����")]
    [SerializeField] private bool m_EnableContinuousDialog = true;  // �Ƿ����������Ի�
    private float m_LastInteractionTime = 0f;                       // ���һ�ν���ʱ��
    private Coroutine m_DetectCoroutine = null;                    // ���Э������

    [SerializeField]private AudioSource m_Greeting;
    [SerializeField] private AudioClip m_GreatingVoice;
    
    [SerializeField] private Text m_PrintText;

    private void Awake()
    {
        OnInit();
    }

    private void OnInit()
    {
        //AI�ظ�����ʱ�ص�
        m_ChatSample.OnAISpeakDone += SpeachDoneCallBack;
    }

    private void Start()
    {
        if (m_MicrophoneName == null)
        {
            // ���û��ָ����˷����ƣ���ʹ�õ�һ��������˷�
            if (Microphone.devices.Length > 0)
            {
                m_MicrophoneName = Microphone.devices[0];
            }
            else
            {
                Debug.LogError("û�м�⵽��˷��豸��");
                PrintLog("����û�м�⵽��˷�");
                return;
            }
        }

        // ��Ҫ������������˷磬�ȴ����Ѻ�������
        StartVoiceListening();
    }

    /// <summary>
    /// ���ѻص� - ���������������RokidWOV�еĻ����߼�ƥ��
    /// </summary>
    public void AwakeCallBack(string _msg)
    {
        if (!m_AwakeState)
        {
            m_AwakeState = true;
            Debug.Log("ʶ�𵽹ؼ��ʣ�" + _msg);
            PrintLog($"Link->��ʶ��: {_msg}");
            
            // ��¼����ʱ��
            m_LastInteractionTime = Time.time;
            
            if (m_Greeting)
            {
                m_Greeting.clip = m_GreatingVoice;
                m_Greeting.Play();
            }
            
            // ���Ѻ�������˷�
            StartMicrophoneForDialog();
        }
    }
    
    /// <summary>
    /// Ϊ�Ի�������˷�
    /// </summary>
    private void StartMicrophoneForDialog()
    {
        if (Microphone.IsRecording(m_MicrophoneName))
        {
            Microphone.End(m_MicrophoneName);
        }
        
        m_RecordedClip = Microphone.Start(m_MicrophoneName, false, 30, 16000);
        while (Microphone.GetPosition(null) <= 0) { }
        
        // ������Э��δ���У�������
        if (m_DetectCoroutine == null)
        {
            m_DetectCoroutine = StartCoroutine(DetectRecording());
        }
    }
    
    /// <summary>
    /// ��ʼ���������ؼ���
    /// </summary>
    private void StartVoiceListening()
    {
        // RokidWOV���Զ���ʼ����������Ҫ�ֶ�����
        PrintLog("��ʼ->ʶ�������ؼ���");
    }

    /// <summary>
    /// ֹͣ���Ѽ���
    /// </summary>
    private void StopVoiceListening()
    {
        // RokidWOV���������������Ҫ�ֶ�ֹͣ
        PrintLog("����->�����ؼ���ʶ��");
    }

    /// <summary>
    /// ��ʼ����˵������
    /// </summary>
    private void StartRecording()
    {
        m_SilenceTimer = 0.0f; // ���þ�����ʱ��
        m_IsRecording = true;
        PrintLog("����¼���Ի�...");
        
        // ������󽻻�ʱ��
        m_LastInteractionTime = Time.time;
        
        // ֹͣ�����������¿�ʼ¼��������֮ǰ���ѵ�����������ʧ
        Microphone.End(m_MicrophoneName);
        m_RecordedClip = Microphone.Start(m_MicrophoneName, false, 30, 16000);
    }
    
    /// <summary>
    /// ����˵��
    /// </summary>
    private void StopRecording()
    {
        m_IsRecording = false;
        PrintLog("����¼������...");

        // ֹͣ��˷����
        Microphone.End(m_MicrophoneName);

        // ������Ƶ����
        SetRecordedAudio();
    }

    /// <summary>
    /// ��ʼ¼������
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
    /// ���½���ʱ�䣨����������������������ã�
    /// </summary>
    public void UpdateInteractionTime()
    {
        m_LastInteractionTime = Time.time;
    }

    /// <summary>
    /// �Ի�����ʱ�ص���������˷���
    /// </summary>
    private void SpeachDoneCallBack()
    {
        // ������󽻻�ʱ�䣨AI˵������ʱ��
        m_LastInteractionTime = Time.time;
        
        m_LockState = false;
        
        if (m_EnableContinuousDialog && m_AwakeState)
        {
            // �����Ի�ģʽ��������˷翪��
            PrintLog($"�����˵�� (��ȴ�{m_LossAwakeTimeLimit}���˳�)");
            
            // ����������˷�
            ReStartRecord();
        }
        else
        {
            // ���ζԻ�ģʽ���ر���˷�
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
            
            PrintLog("�ȴ����Ѵ�...");
        }
    }

    /// <summary>
    /// ��ʼ�������
    /// </summary>
    /// <returns></returns>
    private IEnumerator DetectRecording()
    {
        while (true)
        {
            // ����Ƿ��ڶԻ��Ự��
            if (m_EnableContinuousDialog && m_AwakeState)
            {
                // ���Ự�Ƿ�ʱ
                if (Time.time - m_LastInteractionTime > m_LossAwakeTimeLimit)
                {
                    // �Ự��ʱ���˳��Ի�ģʽ
                    m_AwakeState = false;
                    m_IsRecording = false;
                    m_LockState = false;
                    
                    if (Microphone.IsRecording(m_MicrophoneName))
                    {
                        Microphone.End(m_MicrophoneName);
                    }
                    
                    PrintLog("�Ի���ʱ���ȴ����Ѵ�...");
                    yield break; // ����Э��
                }
            }
            
            float[] samples = new float[128]; // ������������С
            int position = Microphone.GetPosition(null);
            
            // ȷ�����㹻�����ݿ��Զ�ȡ
            if (position < samples.Length)
            {
                yield return null;
                continue;
            }

            // ���AudioClip�Ƿ���Ч�����㹻����
            if (m_RecordedClip == null || m_RecordedClip.samples < samples.Length)
            {
                yield return null;
                continue;
            }

            // ȷ����ȡλ����Ч
            int readPosition = position - samples.Length;
            if (readPosition < 0 || readPosition + samples.Length > m_RecordedClip.samples)
            {
                yield return null;
                continue;
            }

            // ���Ը��Ƚ������ݶ�ȡ����
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

            // û�гɹ���ȡ����������´δ���
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
                m_SilenceTimer = 0.0f; // ���þ�����ʱ��

                // �ѻ��ѣ�����¼��
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
                
                // ����״̬������˵��
                if (m_AwakeState && m_IsRecording && m_SilenceTimer >= m_RecordingTimeLimit)
                {
                    StopRecording();
                }
            }
            
            // ��ʾʣ��Ựʱ��
            if (m_AwakeState && !m_IsRecording && !m_LockState)
            {
                float remainingTime = m_LossAwakeTimeLimit - (Time.time - m_LastInteractionTime);
                if (remainingTime > 0)
                {
                    PrintLog($"�Ի�ģʽ (ʣ��: {remainingTime:F0}��)");
                }
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// ��ӡ��־
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
        // ������Դ
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