using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LogEntry
{
    public string timestamp;
    public string content;
    public int logColorType; // 0: Thường, 1: Đặc biệt, 2: Nguy hiểm
    public bool isStoryLog;

    public LogEntry(string timestamp, string content, int logColorType, bool isStoryLog)
    {
        this.timestamp = timestamp;
        this.content = content;
        this.logColorType = logColorType;
        this.isStoryLog = isStoryLog;
    }
}

public class WorldEventManager : MonoBehaviour
{
    public static WorldEventManager Instance;

    private List<LogEntry> worldLogs = new List<LogEntry>();

    public static event Action OnLogUpdated;
    public static event Action<LogEntry> LogAdded;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        AddLog(UiText.Get("worldStory", "introLogWelcome"), 0, true);
        AddLog(UiText.Get("worldStory", "introLogSecretRealm"), 1, true);
        AddLog(UiText.Get("worldStory", "introLogBeastWave"), 2, true);
    }

    public void AddLog(string content, int colorType, bool isStoryLog = false)
    {
        string timeStr = GetWorldTimeFormatted();

        LogEntry newLog = new LogEntry(timeStr, content, colorType, isStoryLog);

        worldLogs.Insert(0, newLog);

        LogAdded?.Invoke(newLog);
        OnLogUpdated?.Invoke();
    }

    public void AddLog(string content, WorldEventType eventType, bool isStoryLog = true)
    {
        int convertedColor = 0;

        switch (eventType)
        {
            case WorldEventType.SecretRealmOpen:
                convertedColor = 1;
                break;

            case WorldEventType.BeastWave:
            case WorldEventType.HeavenlyTribulation:
                convertedColor = 2;
                break;
        }

        string timeStr = GetWorldTimeFormatted();

        LogEntry newLog = new LogEntry(timeStr, content, convertedColor, isStoryLog);

        worldLogs.Insert(0, newLog);

        LogAdded?.Invoke(newLog);
        OnLogUpdated?.Invoke();
    }

    public void AddLog(string content, WorldEventType eventType)
    {
        AddLog(content, eventType, false);
    }

    private string GetWorldTimeFormatted()
    {
        if (WorldTimeSystem.Instance == null)
        {
            return UiText.Get("worldStory", "timeUnknown");
        }

        int month = WorldTimeSystem.Instance.currentMonth;
        int day = WorldTimeSystem.Instance.currentDay;

        /*
         * Logic hiển thị:
         * 
         * Nếu WorldTimeSystem đang bắt đầu ở:
         * currentMonth = 1, currentDay = 1
         * 
         * Thì mình coi tháng hiển thị = currentMonth - 1.
         * 
         * Tức là:
         * currentMonth 1 → chỉ hiện Ngày 1, Ngày 2...
         * currentMonth 2 → hiện Tháng 1 · Ngày 1
         * currentMonth 3 → hiện Tháng 2 · Ngày 1
         */

        int displayMonth = month - 1;

        if (displayMonth <= 0)
        {
            return $"[N{day}]";
        }

        return $"[T{displayMonth}-N{day}]";
    }

    public List<LogEntry> GetStoryLogs()
    {
        List<LogEntry> storyLogs = new List<LogEntry>();

        foreach (LogEntry entry in worldLogs)
        {
            if (entry != null && entry.isStoryLog)
            {
                storyLogs.Add(entry);
            }
        }

        return storyLogs;
    }

    public List<LogEntry> GetLogs()
    {
        return worldLogs;
    }
}
