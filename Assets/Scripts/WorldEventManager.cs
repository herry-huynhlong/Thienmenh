using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LogEntry
{
    public string timestamp; 
    public string content;   
    public int logColorType; // 0: Thường, 1: Đặc biệt, 2: Nguy hiểm

    public LogEntry(string timestamp, string content, int logColorType)
    {
        this.timestamp = timestamp;
        this.content = content;
        this.logColorType = logColorType;
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

        // Tạo sẵn dữ liệu mẫu bằng số int để test UI cuốn sổ
        AddLog("Chào mừng đến với Hoang Cổ Đại Lục! Linh khí an lành, trời quang mây tạnh.", 0);
        AddLog("Dị động tại phương Đông! Kết giới Thượng Cổ Bí Cảnh có dấu hiệu suy yếu.", 1);
        AddLog("Yêu khí ngập trời! Thú Triều đang rục rịch chuẩn bị tấn công thôn làng!", 2);
    }

    // HÀM 1: Nhận số int (Dùng để bạn tự gõ test UI cho nhanh)
    public void AddLog(string content, int colorType)
    {
        string timeStr = GetWorldTimeFormatted();
        LogEntry newLog = new LogEntry(timeStr, content, colorType);
        worldLogs.Insert(0, newLog); 
        LogAdded?.Invoke(newLog);
        OnLogUpdated?.Invoke();     
    }

    // HÀM 2: Nhận Enum WorldEventType (Cứu cánh dòng 99 đang bị lỗi convert int của bạn)
    public void AddLog(string content, WorldEventType eventType)
    {
        int convertedColor = 0; // Mặc định là màu thường

        // Tự động phân loại màu dựa theo Enum hệ thống truyền vào
        switch (eventType)
        {
            case WorldEventType.SecretRealmOpen:
                convertedColor = 1; // Màu xanh lam cơ duyên
                break;
            case WorldEventType.BeastWave:
            case WorldEventType.HeavenlyTribulation:
                convertedColor = 2; // Màu đỏ cam nguy hiểm
                break;
        }

        string timeStr = GetWorldTimeFormatted();
        LogEntry newLog = new LogEntry(timeStr, content, convertedColor);
        worldLogs.Insert(0, newLog); 
        LogAdded?.Invoke(newLog);
        OnLogUpdated?.Invoke(); 
    }

    // Hàm phụ trợ bốc thời gian từ hệ thống để tránh trùng lặp code
    private string GetWorldTimeFormatted()
    {
        if (WorldTimeSystem.Instance != null)
        {
            int year = WorldTimeSystem.Instance.currentYear;
            int month = WorldTimeSystem.Instance.currentMonth;
            int day = WorldTimeSystem.Instance.currentDay;
            return $"[Năm {year}-T{month}-N{day}] ";
        }
        return "[Thời Không] ";
    }

    public List<LogEntry> GetLogs()
    {
        return worldLogs;
    }
}
