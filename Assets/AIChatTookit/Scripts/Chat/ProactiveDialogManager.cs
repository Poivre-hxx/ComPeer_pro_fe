using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主动对话管理器 - 管理定时问候和提醒事项
/// </summary>
public class ProactiveDialogManager : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private RTChatSample m_ChatSample;
    [SerializeField] private RTSpeechHandler m_SpeechHandler;
    
    [Header("定时问候设置")]
    [SerializeField] private bool m_EnableTimeGreeting = true;
    [SerializeField] private int m_MorningGreetingHour = 8;   // 早上问候时间（小时）
    [SerializeField] private int m_EveningGreetingHour = 20;  // 晚上问候时间（小时）
    
    [Header("提醒事项设置")]
    [SerializeField] private List<ReminderItem> m_Reminders = new List<ReminderItem>();
    
    // 提醒事项类
    [Serializable]
    public class ReminderItem
    {
        [Header("提醒内容")]
        public string content = "记得吃药";
        
        [Header("提醒时间")]
        [Range(0, 23)]
        public int hour = 12;
        [Range(0, 59)]
        public int minute = 0;
        
        [Header("是否启用")]
        public bool enabled = true;
        
        [Header("是否每天重复")]
        public bool repeatDaily = true;
        
        [HideInInspector]
        public string lastTriggerDate = "";
    }
    
    // 记录上次问候时间，避免重复
    private const string MORNING_GREETING_KEY = "LastMorningGreeting";
    private const string EVENING_GREETING_KEY = "LastEveningGreeting";
    
    // 状态标记
    private bool m_IsProactiveDialogActive = false;
    
    private void Start()
    {
        // 确保组件引用
        if (m_ChatSample == null) m_ChatSample = FindObjectOfType<RTChatSample>();
        if (m_SpeechHandler == null) m_SpeechHandler = FindObjectOfType<RTSpeechHandler>();
        
        // 开始检查触发条件
        StartCoroutine(CheckTriggers());
        
        // 打印配置的提醒事项
        Debug.Log($"ProactiveDialogManager: 已配置 {m_Reminders.Count} 个提醒事项");
        foreach (var reminder in m_Reminders)
        {
            if (reminder.enabled)
            {
                Debug.Log($"提醒：{reminder.hour:D2}:{reminder.minute:D2} - {reminder.content}");
            }
        }
    }
    
    /// <summary>
    /// 检查所有触发条件
    /// </summary>
    private IEnumerator CheckTriggers()
    {
        while (true)
        {
            if (!m_IsProactiveDialogActive)
            {
                DateTime now = DateTime.Now;
                string todayDateString = now.ToString("yyyy-MM-dd");
                
                // 检查定时问候
                if (m_EnableTimeGreeting)
                {
                    // 早上问候（8:00）
                    if (now.Hour == m_MorningGreetingHour && now.Minute == 0)
                    {
                        string lastMorningGreeting = PlayerPrefs.GetString(MORNING_GREETING_KEY, "");
                        if (lastMorningGreeting != todayDateString)
                        {
                            PlayerPrefs.SetString(MORNING_GREETING_KEY, todayDateString);
                            StartProactiveDialog(GenerateMorningGreetingPrompt());
                            yield return new WaitForSeconds(60f); // 等待1分钟，避免重复触发
                            continue;
                        }
                    }
                    
                    // 晚上问候（20:00）
                    if (now.Hour == m_EveningGreetingHour && now.Minute == 0)
                    {
                        string lastEveningGreeting = PlayerPrefs.GetString(EVENING_GREETING_KEY, "");
                        if (lastEveningGreeting != todayDateString)
                        {
                            PlayerPrefs.SetString(EVENING_GREETING_KEY, todayDateString);
                            StartProactiveDialog(GenerateEveningGreetingPrompt());
                            yield return new WaitForSeconds(60f); // 等待1分钟，避免重复触发
                            continue;
                        }
                    }
                }
                
                // 检查提醒事项
                foreach (var reminder in m_Reminders)
                {
                    if (!reminder.enabled) continue;
                    
                    // 检查是否到达提醒时间
                    if (now.Hour == reminder.hour && now.Minute == reminder.minute)
                    {
                        // 检查是否今天已经触发过
                        if (reminder.lastTriggerDate != todayDateString)
                        {
                            reminder.lastTriggerDate = todayDateString;
                            StartProactiveDialog(GenerateReminderPrompt(reminder.content));
                            Debug.Log($"触发提醒：{reminder.content}");
                            yield return new WaitForSeconds(60f); // 等待1分钟，避免重复触发
                            break; // 一次只处理一个提醒
                        }
                        else if (!reminder.repeatDaily)
                        {
                            // 如果不是每天重复，则禁用该提醒
                            reminder.enabled = false;
                        }
                    }
                }
            }
            
            yield return new WaitForSeconds(30f); // 每30秒检查一次
        }
    }
    
    /// <summary>
    /// 开始主动对话
    /// </summary>
    private void StartProactiveDialog(string prompt)
    {
        if (m_IsProactiveDialogActive) return;
        
        m_IsProactiveDialogActive = true;
        Debug.Log($"ProactiveDialogManager: 开始主动对话 - {prompt}");
        
        // 直接模拟唤醒状态并发送消息
        StartCoroutine(ExecuteProactiveDialog(prompt));
    }
    
    /// <summary>
    /// 执行主动对话
    /// </summary>
    private IEnumerator ExecuteProactiveDialog(string prompt)
    {
        // 等待一小段时间，确保系统准备就绪
        yield return new WaitForSeconds(0.5f);
        
        // 激活语音处理器的对话模式（模拟唤醒）
        if (m_SpeechHandler != null)
        {
            m_SpeechHandler.AwakeCallBack("主动对话");
        }
        
        // 再等待一下，确保麦克风启动
        yield return new WaitForSeconds(0.5f);
        
        // 发送消息到 LLM
        if (m_ChatSample != null)
        {
            m_ChatSample.SendData(prompt);
        }
        
        // 设置一个超时，确保状态能够恢复
        StartCoroutine(ResetProactiveDialogState());
    }
    
    /// <summary>
    /// 重置主动对话状态
    /// </summary>
    private IEnumerator ResetProactiveDialogState()
    {
        // 等待足够长的时间（考虑到对话可能持续的时间）
        yield return new WaitForSeconds(15f);
        
        // 重置状态
        m_IsProactiveDialogActive = false;
        Debug.Log("ProactiveDialogManager: 主动对话状态已重置");
    }
    
    /// <summary>
    /// 生成早上问候的 Prompt
    /// </summary>
    private string GenerateMorningGreetingPrompt()
    {
        string date = DateTime.Now.ToString("MM月dd日");
        string dayOfWeek = GetChineseDayOfWeek(DateTime.Now.DayOfWeek);
        
        return $"现在是早上8点，{date} {dayOfWeek}，你是孙女胡桃，请想你的爷爷问好，表达早安。" +
               $"20字以内，自然一些。";
    }
    
    /// <summary>
    /// 生成晚上问候的 Prompt
    /// </summary>
    private string GenerateEveningGreetingPrompt()
    {
        return $"现在是晚上8点，你是孙女胡桃，请用温暖关怀的语气向你的爷爷问候，表达晚安。" +
               $"可以关心爷爷今天的情况，20字以内，轻松自然。";
    }
    
    /// <summary>
    /// 生成提醒事项的 Prompt
    /// </summary>
    private string GenerateReminderPrompt(string reminderContent)
    {
        return $"你是孙女胡桃，请友好地提醒爷爷：{reminderContent}。" +
               $"语气要自然亲切，20字以内。";
    }
    
    /// <summary>
    /// 获取中文星期
    /// </summary>
    private string GetChineseDayOfWeek(DayOfWeek dayOfWeek)
    {
        switch (dayOfWeek)
        {
            case DayOfWeek.Monday: return "星期一";
            case DayOfWeek.Tuesday: return "星期二";
            case DayOfWeek.Wednesday: return "星期三";
            case DayOfWeek.Thursday: return "星期四";
            case DayOfWeek.Friday: return "星期五";
            case DayOfWeek.Saturday: return "星期六";
            case DayOfWeek.Sunday: return "星期日";
            default: return "";
        }
    }
    
    /// <summary>
    /// 添加新的提醒事项（可通过代码调用）
    /// </summary>
    public void AddReminder(string content, int hour, int minute, bool repeatDaily = true)
    {
        m_Reminders.Add(new ReminderItem
        {
            content = content,
            hour = hour,
            minute = minute,
            enabled = true,
            repeatDaily = repeatDaily
        });
        
        Debug.Log($"添加提醒：{hour:D2}:{minute:D2} - {content}");
    }
}