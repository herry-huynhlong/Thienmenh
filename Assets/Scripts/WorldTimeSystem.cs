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

    const int DaysInMonth = 30;
    const int MonthsInYear = 12;

    [Header("World")]
    public string continentName = "Hoang Co Dai Luc";

    [Header("Clock")]
    [Min(1f)] public float realSecondsPerGameDay = 900f;

    [Header("Runtime")]
    public int currentYear = 1;
    public int currentMonth = 1;
    public int currentDay = 1;
    public float currentHour = 6f;

    [Header("Save")]
    public bool loadSavedTimeOnAwake = true;
    public bool autoSaveWorldTime = true;
    [Min(0f)] public float autoSaveInterval = 10f;

    public event Action<int> OnHourChanged;
    public event Action<int> OnDayChanged;

    int lastTriggeredHour = -1;
    int lastTriggeredDateCode = -1;
    float saveTimer;
    bool createdAtRuntime;

    public WorldTimePhase CurrentPhase => GetCurrentPhase();
    public int CurrentDay => CurrentAbsoluteDay;
    public float CurrentHour => currentHour;
    public int CurrentAbsoluteDay =>
        ((currentYear - 1) * MonthsInYear + (currentMonth - 1)) *
        DaysInMonth +
        currentDay;
    public float CurrentWorldHour =>
        (CurrentAbsoluteDay - 1) * 24f + currentHour;

    public static WorldTimeSystem EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        WorldTimeSystem[] systems = FindObjectsByType<WorldTimeSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (WorldTimeSystem system in systems)
        {
            if (system != null)
            {
                Instance = system;
                return Instance;
            }
        }

        GameObject timeObject = new GameObject("WorldTimeSystem");
        WorldTimeSystem created = timeObject.AddComponent<WorldTimeSystem>();
        created.createdAtRuntime = true;
        return created;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance.createdAtRuntime && transform.childCount > 0)
            {
                Destroy(Instance.gameObject);
                Instance = this;
            }
            else
            {
                Instance.ApplyConfigFrom(this);
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Instance = this;
        }

        DontDestroyOnLoad(gameObject);
        NormalizeDateTime();

        if (loadSavedTimeOnAwake &&
            GameSaveSystem.TryLoadWorldTime(
                out int year,
                out int month,
                out int day,
                out float hour))
        {
            SetTime(year, month, day, hour, false);
        }

        TriggerTimeEvents(true);
    }

    void Update()
    {
        AdvanceTime(Time.deltaTime);
        TriggerTimeEvents(false);
        AutoSaveTime(Time.unscaledDeltaTime);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveNow();
        }
    }

    void OnApplicationQuit()
    {
        SaveNow();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetTime(
        int year,
        int month,
        int day,
        float hour,
        bool triggerEvents = true)
    {
        currentYear = year;
        currentMonth = month;
        currentDay = day;
        currentHour = hour;

        NormalizeDateTime();
        saveTimer = 0f;

        if (triggerEvents)
        {
            TriggerTimeEvents(true);
        }
    }

    public void SaveNow()
    {
        if (!autoSaveWorldTime)
        {
            return;
        }

        GameSaveSystem.SaveWorldTime(
            currentYear,
            currentMonth,
            currentDay,
            currentHour);
    }

    public string GetClockText()
    {
        int hour = Mathf.FloorToInt(currentHour);
        int minute = Mathf.FloorToInt((currentHour - hour) * 60f);

        string dateText = "Ngay " + currentDay;
        if (currentMonth > 1 || currentYear > 1)
        {
            dateText = "Thang " + currentMonth + " - " + dateText;
        }

        if (currentYear > 1)
        {
            dateText = "Nam " + currentYear + " - " + dateText;
        }

        return continentName +
            "\n" +
            dateText +
            "\n" +
            hour.ToString("00") +
            "h" +
            minute.ToString("00");
    }

    public bool IsDangerousNight()
    {
        int hour = Mathf.FloorToInt(currentHour);
        return hour >= 21 || hour < 5;
    }

    void AdvanceTime(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || realSecondsPerGameDay <= 0f)
        {
            return;
        }

        currentHour += deltaSeconds * (24f / realSecondsPerGameDay);
        NormalizeDateTime();
    }

    void AutoSaveTime(float deltaSeconds)
    {
        if (!autoSaveWorldTime || autoSaveInterval <= 0f)
        {
            return;
        }

        saveTimer += deltaSeconds;
        if (saveTimer < autoSaveInterval)
        {
            return;
        }

        saveTimer = 0f;
        SaveNow();
    }

    void NormalizeDateTime()
    {
        if (float.IsNaN(currentHour) || float.IsInfinity(currentHour))
        {
            currentHour = 6f;
        }

        currentYear = Mathf.Max(1, currentYear);
        currentMonth = Mathf.Max(1, currentMonth);
        currentDay = Mathf.Max(1, currentDay);

        while (currentHour >= 24f)
        {
            currentHour -= 24f;
            currentDay++;
        }

        while (currentHour < 0f)
        {
            currentHour += 24f;
            currentDay--;
        }

        while (currentDay > DaysInMonth)
        {
            currentDay -= DaysInMonth;
            currentMonth++;
        }

        while (currentDay < 1)
        {
            currentDay += DaysInMonth;
            currentMonth--;
        }

        while (currentMonth > MonthsInYear)
        {
            currentMonth -= MonthsInYear;
            currentYear++;
        }

        while (currentMonth < 1)
        {
            currentMonth += MonthsInYear;
            currentYear = Mathf.Max(1, currentYear - 1);
        }

        currentHour = Mathf.Clamp(currentHour, 0f, 23.999f);
    }

    void TriggerTimeEvents(bool force)
    {
        int hour = Mathf.FloorToInt(currentHour);
        if (force || hour != lastTriggeredHour)
        {
            lastTriggeredHour = hour;
            OnHourChanged?.Invoke(hour);
        }

        int dateCode = currentYear * 10000 + currentMonth * 100 + currentDay;
        if (force || dateCode != lastTriggeredDateCode)
        {
            lastTriggeredDateCode = dateCode;
            OnDayChanged?.Invoke(CurrentAbsoluteDay);
        }
    }

    void ApplyConfigFrom(WorldTimeSystem source)
    {
        if (source == null)
        {
            return;
        }

        continentName = source.continentName;
        realSecondsPerGameDay = source.realSecondsPerGameDay;
        loadSavedTimeOnAwake = source.loadSavedTimeOnAwake;
        autoSaveWorldTime = source.autoSaveWorldTime;
        autoSaveInterval = source.autoSaveInterval;
    }

    WorldTimePhase GetCurrentPhase()
    {
        int hour = Mathf.FloorToInt(currentHour);

        if (hour >= 5 && hour < 8)
        {
            return WorldTimePhase.Dawn;
        }

        if (hour >= 8 && hour < 11)
        {
            return WorldTimePhase.Morning;
        }

        if (hour >= 11 && hour < 13)
        {
            return WorldTimePhase.Noon;
        }

        if (hour >= 13 && hour < 17)
        {
            return WorldTimePhase.Afternoon;
        }

        if (hour >= 17 && hour < 21)
        {
            return WorldTimePhase.Evening;
        }

        return WorldTimePhase.Night;
    }
}
