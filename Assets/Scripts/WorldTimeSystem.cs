using System;
using UnityEngine;

public enum WorldTimePhase
{
    Dawn,       
    Morning,    
    Noon,       
    Afternoon,  
    Evening,    
    Night       
}

public class WorldTimeSystem : MonoBehaviour
{
    public static WorldTimeSystem Instance;

    [Header("Cấu hình Đại Lục")]
    public string continentName =
        "Hoang Cổ Đại Lục";

    [Header("1 ngày game = 15 phút ngoài đời")]
    public float realSecondsPerGameDay = 900f;

    [Header("Runtime Variables")]

    // Không hiển thị ngay từ đầu
    // nhưng vẫn lưu để sau này dùng
    public int currentYear = 1;

    public int currentMonth = 1;

    public int currentDay = 1;

    // Bắt đầu lúc 6h sáng
    public float currentHour = 6f;

    // 30 ngày / tháng
    private const int DAYS_IN_MONTH = 30;

    // 12 tháng / năm
    private const int MONTHS_IN_YEAR = 12;

    // Event cho hệ thống khác
    public event Action<int> OnHourChanged;

    public event Action<int> OnDayChanged;

    private int lastTriggeredHour = -1;

    private int lastTriggeredDay = -1;
    private float saveTimer;
    public float autoSaveInterval = 10f;

    // AI đọc buổi trong ngày
    public WorldTimePhase CurrentPhase =>
        GetCurrentPhase();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        UpdateTime();
    }

    void UpdateTime()
    {
        // 24 giờ game / 900 giây thật
        float hoursPerSecond =
            24f / realSecondsPerGameDay;

        currentHour +=
            Time.deltaTime *
            hoursPerSecond;

        // Qua ngày
        if (currentHour >= 24f)
        {
            currentHour -= 24f;

            currentDay++;

            // Qua tháng
            if (currentDay > DAYS_IN_MONTH)
            {
                currentDay = 1;

                currentMonth++;

                // Qua năm
                if (currentMonth > MONTHS_IN_YEAR)
                {
                    currentMonth = 1;

                    currentYear++;
                }
            }
        }

        // Event đổi giờ
        int intHour =
            Mathf.FloorToInt(currentHour);

        if (intHour != lastTriggeredHour)
        {
            lastTriggeredHour = intHour;

            OnHourChanged?.Invoke(
                lastTriggeredHour
            );
        }

        // Event đổi ngày
        if (currentDay != lastTriggeredDay)
        {
            lastTriggeredDay = currentDay;

            OnDayChanged?.Invoke(
                lastTriggeredDay
            );
        }
    }

    // =========================
    // UI CLOCK
    // =========================

    public string GetClockText()
    {
        int hour =
            Mathf.FloorToInt(currentHour);

        int minute =
            Mathf.FloorToInt(
                (currentHour - hour) * 60f
            );

        string dateText =
            "Ngày " + currentDay;

        // Chỉ hiện tháng
        // khi đã qua tháng đầu tiên
        if (currentMonth > 1)
        {
            dateText =
                "Tháng " +
                currentMonth +
                " - " +
                dateText;
        }

        // Chỉ hiện năm
        // khi đã qua năm đầu tiên
        if (currentYear > 1)
        {
            dateText =
                "Năm " +
                currentYear +
                " - " +
                dateText;
        }

        return
            continentName +
            "\n" +
            dateText +
            "\n" +
            hour.ToString("00") +
            "h" +
            minute.ToString("00");
    }

    // =========================
    // PHÂN BUỔI
    // =========================

    private WorldTimePhase GetCurrentPhase()
    {
        int hour =
            Mathf.FloorToInt(currentHour);

        if (hour >= 5 && hour < 8)
            return WorldTimePhase.Dawn;

        if (hour >= 8 && hour < 11)
            return WorldTimePhase.Morning;

        if (hour >= 11 && hour < 13)
            return WorldTimePhase.Noon;

        if (hour >= 13 && hour < 17)
            return WorldTimePhase.Afternoon;

        if (hour >= 17 && hour < 21)
            return WorldTimePhase.Evening;

        return WorldTimePhase.Night;
    }

    // =========================
    // FIX LỖI CODE CŨ
    // =========================

    public int CurrentDay =>
        currentDay;

    public float CurrentHour =>
        currentHour;

    public bool IsDangerousNight()
    {
        int hour =
            Mathf.FloorToInt(currentHour);

        return hour >= 21 ||
               hour < 5;
    }
}