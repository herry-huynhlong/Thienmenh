using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    public string continentName = "";

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
    [FormerlySerializedAs("autoSaveInterval")]
    [Min(0f)] public float autoSaveIntervalUnscaledSeconds = 10f;

    public event Action<int> OnHourChanged;
    public event Action<int> OnDayChanged;

    long lastTriggeredAbsoluteHour = long.MinValue;
    long lastTriggeredAbsoluteDay = long.MinValue;
    float saveTimer;
    bool createdAtRuntime;

    public WorldTimePhase CurrentPhase => GetCurrentPhase();
    public int CurrentDay => CurrentAbsoluteDay;
    public float CurrentHour => currentHour;
    public int CurrentAbsoluteDay => ToPublicAbsoluteDay(GetAbsoluteDay());
    public double CurrentWorldHourExact =>
        (GetAbsoluteDay() - 1L) * 24d + currentHour;
    public float CurrentWorldHour => (float)CurrentWorldHourExact;

    public static WorldTimeSystem EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        WorldTimeSystem[] systems = FindObjectsByType<WorldTimeSystem>(
            FindObjectsInactive.Include);

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
        AdvanceTime(GameTime.ScaledDeltaSeconds);
        TriggerTimeEvents(false);
        AutoSaveTime(GameTime.UnscaledDeltaSeconds);
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

        if (!triggerEvents)
        {
            ResetEventCursorToCurrentTime();
            return;
        }

        long targetAbsoluteHour = GetAbsoluteHour();
        bool canCatchUp =
            lastTriggeredAbsoluteHour != long.MinValue &&
            targetAbsoluteHour > lastTriggeredAbsoluteHour;

        TriggerTimeEvents(!canCatchUp);
    }

    /// <summary>
    /// Restores an exact saved timestamp without simulating all elapsed hours
    /// between the current scene time and the saved time.
    /// </summary>
    public void RestoreTime(
        int year,
        int month,
        int day,
        float hour,
        bool notifyEvents = true)
    {
        currentYear = year;
        currentMonth = month;
        currentDay = day;
        currentHour = hour;

        NormalizeDateTime();
        saveTimer = 0f;
        ResetEventCursorToCurrentTime();

        if (notifyEvents)
        {
            TriggerTimeEvents(true);
        }
    }

    public void SetCurrentDayHour(float hour, bool triggerEvents = true)
    {
        SetTime(currentYear, currentMonth, currentDay, hour, triggerEvents);
    }

    [ContextMenu("Debug/Set Time 00:30")]
    void DebugSetTime0030()
    {
        SetCurrentDayHour(0.5f);
    }

    [ContextMenu("Debug/Set Time 07:30")]
    void DebugSetTime0730()
    {
        SetCurrentDayHour(7.5f);
    }

    [ContextMenu("Debug/Set Time 13:30")]
    void DebugSetTime1330()
    {
        SetCurrentDayHour(13.5f);
    }

    [ContextMenu("Debug/Set Time 18:30")]
    void DebugSetTime1830()
    {
        SetCurrentDayHour(18.5f);
    }

    [ContextMenu("Debug/Set Time 22:30")]
    void DebugSetTime2230()
    {
        SetCurrentDayHour(22.5f);
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

        string dateText = UiText.Format("worldClock", "dayFormat", currentDay);
        if (currentMonth > 1 || currentYear > 1)
        {
            dateText = UiText.Format(
                "worldClock",
                "monthFormat",
                currentMonth,
                dateText);
        }

        if (currentYear > 1)
        {
            dateText = UiText.Format(
                "worldClock",
                "yearFormat",
                currentYear,
                dateText);
        }

        string resolvedContinentName =
            UiText.Get("worldClock", "continentName", continentName);

        return UiText.Format(
            "worldClock",
            "clockFormat",
            resolvedContinentName,
            dateText,
            hour,
            minute);
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
        if (!autoSaveWorldTime || autoSaveIntervalUnscaledSeconds <= 0f)
        {
            return;
        }

        saveTimer += deltaSeconds;
        if (saveTimer < autoSaveIntervalUnscaledSeconds)
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
        long totalMonths =
            ((long)currentYear - 1L) * MonthsInYear +
            currentMonth -
            1L;
        long totalDays =
            totalMonths * DaysInMonth +
            currentDay -
            1L;

        double dayOffset = Math.Floor(currentHour / 24.0);
        double hourOfDay = currentHour - dayOffset * 24.0;
        totalDays += (long)dayOffset;

        if (totalDays < 0L)
        {
            totalDays = 0L;
            hourOfDay = 0.0;
        }

        if (hourOfDay < 0.0)
        {
            hourOfDay = 0.0;
        }

        long normalizedYear =
            totalDays / (MonthsInYear * DaysInMonth) + 1L;
        long dayInYear =
            totalDays % (MonthsInYear * DaysInMonth);

        currentYear =
            normalizedYear > int.MaxValue
                ? int.MaxValue
                : (int)normalizedYear;
        currentMonth = (int)(dayInYear / DaysInMonth) + 1;
        currentDay = (int)(dayInYear % DaysInMonth) + 1;
        currentHour = Mathf.Clamp((float)hourOfDay, 0f, 23.999f);
    }

    void TriggerTimeEvents(bool force)
    {
        if (force || lastTriggeredAbsoluteHour == long.MinValue)
        {
            NotifyCurrentTimeWithoutDailyTicks();
            return;
        }

        int targetYear = currentYear;
        int targetMonth = currentMonth;
        int targetDay = currentDay;
        float targetHourOfDay = currentHour;
        long targetAbsoluteHour = GetAbsoluteHour();
        long targetAbsoluteDay = GetAbsoluteDay();

        if (targetAbsoluteHour < lastTriggeredAbsoluteHour)
        {
            NotifyCurrentTimeWithoutDailyTicks();
            return;
        }

        if (targetAbsoluteHour == lastTriggeredAbsoluteHour)
        {
            return;
        }

        long nextAbsoluteHour = lastTriggeredAbsoluteHour + 1L;
        try
        {
            while (nextAbsoluteHour <= targetAbsoluteHour)
            {
                bool isTargetHour = nextAbsoluteHour == targetAbsoluteHour;
                if (isTargetHour)
                {
                    SetRawTime(
                        targetYear,
                        targetMonth,
                        targetDay,
                        targetHourOfDay);
                }
                else
                {
                    SetTimeFromAbsoluteHour(nextAbsoluteHour);
                }

                lastTriggeredAbsoluteHour = nextAbsoluteHour;
                OnHourChanged?.Invoke(Mathf.FloorToInt(currentHour));

                long eventAbsoluteDay = GetAbsoluteDay();
                if (eventAbsoluteDay != lastTriggeredAbsoluteDay)
                {
                    lastTriggeredAbsoluteDay = eventAbsoluteDay;
                    OnDayChanged?.Invoke(ToPublicAbsoluteDay(eventAbsoluteDay));
                    RunDailySimulationTicks();
                }

                if (nextAbsoluteHour == long.MaxValue)
                {
                    break;
                }

                nextAbsoluteHour++;
            }
        }
        finally
        {
            SetRawTime(
                targetYear,
                targetMonth,
                targetDay,
                targetHourOfDay);
        }

        lastTriggeredAbsoluteHour = targetAbsoluteHour;
        lastTriggeredAbsoluteDay = targetAbsoluteDay;
    }

    void NotifyCurrentTimeWithoutDailyTicks()
    {
        ResetEventCursorToCurrentTime();
        OnHourChanged?.Invoke(Mathf.FloorToInt(currentHour));
        OnDayChanged?.Invoke(ToPublicAbsoluteDay(lastTriggeredAbsoluteDay));
    }

    void RunDailySimulationTicks()
    {
        VillagerRelationshipManager relationshipManager =
            VillagerRelationshipManager.EnsureInstance();
        if (relationshipManager != null)
        {
            relationshipManager.DailyRelationshipTick();
        }

        VillagerBirthManager birthManager =
            VillagerBirthManager.EnsureInstance();
        if (birthManager != null)
        {
            birthManager.DailyBirthTick();
        }
    }

    void ResetEventCursorToCurrentTime()
    {
        lastTriggeredAbsoluteHour = GetAbsoluteHour();
        lastTriggeredAbsoluteDay = GetAbsoluteDay();
    }

    long GetAbsoluteDay()
    {
        return
            (((long)currentYear - 1L) * MonthsInYear +
             (currentMonth - 1L)) *
            DaysInMonth +
            currentDay;
    }

    long GetAbsoluteHour()
    {
        return (GetAbsoluteDay() - 1L) * 24L +
               Mathf.FloorToInt(currentHour);
    }

    void SetTimeFromAbsoluteHour(long absoluteHour)
    {
        long zeroBasedDay = absoluteHour / 24L;
        int hour = (int)(absoluteHour % 24L);
        long zeroBasedYear =
            zeroBasedDay / (MonthsInYear * DaysInMonth);
        long dayInYear =
            zeroBasedDay % (MonthsInYear * DaysInMonth);

        currentYear =
            zeroBasedYear >= int.MaxValue
                ? int.MaxValue
                : (int)zeroBasedYear + 1;
        currentMonth = (int)(dayInYear / DaysInMonth) + 1;
        currentDay = (int)(dayInYear % DaysInMonth) + 1;
        currentHour = hour;
    }

    void SetRawTime(int year, int month, int day, float hour)
    {
        currentYear = year;
        currentMonth = month;
        currentDay = day;
        currentHour = hour;
    }

    static int ToPublicAbsoluteDay(long absoluteDay)
    {
        if (absoluteDay <= 0L)
        {
            return 1;
        }

        return absoluteDay > int.MaxValue
            ? int.MaxValue
            : (int)absoluteDay;
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
        autoSaveIntervalUnscaledSeconds =
            source.autoSaveIntervalUnscaledSeconds;
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
