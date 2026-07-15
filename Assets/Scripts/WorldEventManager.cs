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

    [Header("World Log")]
    [Min(1)] public int maxLogEntries = 200;

    private readonly List<LogEntry> worldLogs = new List<LogEntry>();

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
    }

    void OnValidate()
    {
        maxLogEntries = Mathf.Max(1, maxLogEntries);
        TrimToLimit();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void AddLog(string content, int colorType, bool isStoryLog = false)
    {
        string timeStr = GetWorldTimeFormatted();

        LogEntry newLog = new LogEntry(timeStr, content, colorType, isStoryLog);
        AddEntry(newLog);
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
        AddEntry(newLog);
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
        return CaptureLogs();
    }

    public List<LogEntry> CaptureLogs()
    {
        List<LogEntry> captured = new List<LogEntry>(worldLogs.Count);
        for (int i = 0; i < worldLogs.Count; i++)
        {
            LogEntry entry = worldLogs[i];
            if (entry != null &&
                ShouldPersistLogEntry(entry))
            {
                captured.Add(CloneEntry(entry));
            }
        }

        return captured;
    }

    public void RestoreLogs(IList<LogEntry> savedLogs)
    {
        worldLogs.Clear();

        int limit = Mathf.Max(1, maxLogEntries);
        if (savedLogs != null)
        {
            for (int i = 0; i < savedLogs.Count && worldLogs.Count < limit; i++)
            {
                LogEntry entry = savedLogs[i];
                if (entry != null &&
                    ShouldPersistLogEntry(entry))
                {
                    worldLogs.Add(CloneEntry(entry));
                }
            }
        }

        OnLogUpdated?.Invoke();
    }

    void AddEntry(LogEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        worldLogs.Insert(0, entry);
        TrimToLimit();

        LogAdded?.Invoke(entry);
        OnLogUpdated?.Invoke();
    }

    void TrimToLimit()
    {
        int limit = Mathf.Max(1, maxLogEntries);
        if (worldLogs.Count > limit)
        {
            worldLogs.RemoveRange(limit, worldLogs.Count - limit);
        }
    }

    static LogEntry CloneEntry(LogEntry entry)
    {
        return new LogEntry(
            entry.timestamp ?? "",
            entry.content ?? "",
            entry.logColorType,
            entry.isStoryLog);
    }

    static bool ShouldPersistLogEntry(LogEntry entry)
    {
        if (entry == null ||
            string.IsNullOrWhiteSpace(entry.content))
        {
            return false;
        }

        string content = entry.content.Trim();
        return !IsBootstrapIntroLog(content);
    }

    static bool IsBootstrapIntroLog(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return string.Equals(
                content,
                UiText.Get("worldStory", "introLogWelcome"),
                StringComparison.Ordinal) ||
            string.Equals(
                content,
                UiText.Get("worldStory", "introLogSecretRealm"),
                StringComparison.Ordinal) ||
            string.Equals(
                content,
                UiText.Get("worldStory", "introLogBeastWave"),
                StringComparison.Ordinal);
    }
}
