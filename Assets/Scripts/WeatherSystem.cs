using System;
using UnityEngine;

public enum WorldWeather
{
    Clear,
    Rain,
    Thunder,
    Snow,
    DenseSpiritualQi
}

[Serializable]
public class WeatherPersistentState
{
    public bool hasState = true;
    public int currentWeather;
    public bool manualOverrideActive;
    public float nextChangeWorldHour;
    public bool hasScheduledChange;
}

public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }
    bool createdAtRuntime;

    public WorldWeather CurrentWeather { get; private set; } = WorldWeather.Clear;
    public bool ManualOverrideActive { get; private set; }

    [Header("Calendar Schedule")]
    public bool useCalendarSchedule = true;
    [Min(1)] public int rainEveryDays = 5;
    [Range(0f, 24f)] public float rainDurationHours = 24f;
    [Range(1, 30)] public int snowDayOfMonth = 15;
    [Range(0f, 24f)] public float snowDurationHours = 24f;

    [Header("Random Fallback")]
    public float weatherDurationHours = 4f;

    public event Action<WorldWeather> OnWeatherChanged;

    float nextChangeWorldHour;
    bool hasScheduledChange;

    public WeatherPersistentState CapturePersistentState()
    {
        return new WeatherPersistentState
        {
            hasState = true,
            currentWeather = (int)CurrentWeather,
            manualOverrideActive = ManualOverrideActive,
            nextChangeWorldHour = nextChangeWorldHour,
            hasScheduledChange = hasScheduledChange
        };
    }

    public void RestorePersistentState(WeatherPersistentState saved)
    {
        if (saved == null || !saved.hasState)
        {
            return;
        }

        CurrentWeather = Enum.IsDefined(
                typeof(WorldWeather),
                saved.currentWeather)
            ? (WorldWeather)saved.currentWeather
            : WorldWeather.Clear;
        ManualOverrideActive = saved.manualOverrideActive;
        nextChangeWorldHour = saved.nextChangeWorldHour;
        hasScheduledChange =
            saved.hasScheduledChange &&
            !float.IsNaN(nextChangeWorldHour) &&
            !float.IsInfinity(nextChangeWorldHour);

        OnWeatherChanged?.Invoke(CurrentWeather);
    }

    public void MarkCreatedAtRuntime()
    {
        createdAtRuntime = true;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance.createdAtRuntime && !createdAtRuntime)
            {
                Destroy(Instance.gameObject);
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Instance = this;
        }

        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return;
        }

        if (ManualOverrideActive)
        {
            return;
        }

        if (useCalendarSchedule)
        {
            SetWeather(ResolveScheduledWeather(timeSystem), false);
            return;
        }

        if (!hasScheduledChange)
        {
            ScheduleNextChange(timeSystem);
            return;
        }

        float currentWorldHour = timeSystem.CurrentWorldHour;

        if (currentWorldHour >= nextChangeWorldHour)
        {
            RollWeather();
            ScheduleNextChange(timeSystem);
        }
    }

    public void SetWeather(WorldWeather weather, bool reschedule = true)
    {
        if (CurrentWeather != weather)
        {
            CurrentWeather = weather;
            OnWeatherChanged?.Invoke(CurrentWeather);
        }

        if (reschedule && !useCalendarSchedule && WorldTimeSystem.Instance != null)
        {
            ScheduleNextChange(WorldTimeSystem.Instance);
        }
    }

    public void SetManualWeather(WorldWeather weather)
    {
        ManualOverrideActive = true;
        SetWeather(weather, false);
    }

    public void ClearManualWeatherOverride()
    {
        ManualOverrideActive = false;

        if (useCalendarSchedule && WorldTimeSystem.Instance != null)
        {
            SetWeather(ResolveScheduledWeather(WorldTimeSystem.Instance), false);
        }
        else if (WorldTimeSystem.Instance != null)
        {
            ScheduleNextChange(WorldTimeSystem.Instance);
        }
    }

    public float MoodModifier()
    {
        switch (CurrentWeather)
        {
            case WorldWeather.Rain:
                return -8f;
            case WorldWeather.Thunder:
                return -14f;
            case WorldWeather.Snow:
                return -5f;
            case WorldWeather.DenseSpiritualQi:
                return 8f;
            default:
                return 0f;
        }
    }

    public float MoveSpeedMultiplier()
    {
        return CurrentWeather == WorldWeather.Snow ? 0.8f : 1f;
    }

    public float CultivationMultiplier()
    {
        return CurrentWeather == WorldWeather.DenseSpiritualQi ? 1.8f : 1f;
    }

    public float BeastAggressionBonus()
    {
        return CurrentWeather == WorldWeather.Thunder ? 25f : 0f;
    }

    WorldWeather ResolveScheduledWeather(WorldTimeSystem timeSystem)
    {
        if (timeSystem == null)
        {
            return CurrentWeather;
        }

        if (IsSnowTime(timeSystem))
        {
            return WorldWeather.Snow;
        }

        if (IsRainTime(timeSystem))
        {
            return WorldWeather.Rain;
        }

        return WorldWeather.Clear;
    }

    bool IsSnowTime(WorldTimeSystem timeSystem)
    {
        int scheduledSnowDay =
            Mathf.Clamp(
                snowDayOfMonth,
                1,
                timeSystem != null
                    ? timeSystem.DaysPerMonth
                    : 30);
        float duration = Mathf.Clamp(snowDurationHours, 0f, 24f);

        return duration > 0f &&
            timeSystem.currentDay == scheduledSnowDay &&
            timeSystem.CurrentHour < duration;
    }

    bool IsRainTime(WorldTimeSystem timeSystem)
    {
        int interval = Mathf.Max(1, rainEveryDays);
        float duration = Mathf.Clamp(rainDurationHours, 0f, 24f);

        return duration > 0f &&
            timeSystem.CurrentAbsoluteDay % interval == 0 &&
            timeSystem.CurrentHour < duration;
    }

    void RollWeather()
    {
        float roll = UnityEngine.Random.value;
        WorldWeather nextWeather;
        if (roll < 0.50f) nextWeather = WorldWeather.Clear;
        else if (roll < 0.72f) nextWeather = WorldWeather.Rain;
        else if (roll < 0.84f) nextWeather = WorldWeather.Thunder;
        else if (roll < 0.94f) nextWeather = WorldWeather.Snow;
        else nextWeather = WorldWeather.DenseSpiritualQi;

        if (nextWeather == CurrentWeather)
        {
            return;
        }

        CurrentWeather = nextWeather;
        OnWeatherChanged?.Invoke(CurrentWeather);
    }

    void ScheduleNextChange(WorldTimeSystem timeSystem)
    {
        if (timeSystem == null)
        {
            return;
        }

        float currentWorldHour = timeSystem.CurrentWorldHour;
        nextChangeWorldHour =
            currentWorldHour +
            Mathf.Max(1f, weatherDurationHours);
        hasScheduledChange = true;
    }
}
