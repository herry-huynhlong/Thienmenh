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
    public static WorldTimeSystem Instance { get; private set; }

    [Tooltip("Mot ngay trong game bang 15 phut ngoai doi.")]
    public float realMinutesPerGameDay = 15f;
    [Range(0f, 24f)]
    public float startHour = 6f;

    public int CurrentDay { get; private set; } = 1;
    public float CurrentHour { get; private set; }
    public int CurrentHourInt { get; private set; }
    public WorldTimePhase CurrentPhase { get; private set; }

    public event Action<int> OnHourChanged;
    public event Action<WorldTimePhase> OnPhaseChanged;
    public event Action<int> OnDayChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentHour = startHour;
        CurrentHourInt = Mathf.FloorToInt(CurrentHour);
        CurrentPhase = GetPhase(CurrentHour);
    }

    void Update()
    {
        float dayDuration = Mathf.Max(1f, realMinutesPerGameDay * 60f);
        float previousHour = CurrentHour;
        int previousHourInt = CurrentHourInt;
        CurrentHour += Time.deltaTime * 24f / dayDuration;

        if (CurrentHour >= 24f)
        {
            CurrentHour -= 24f;
            CurrentDay++;
            if (OnDayChanged != null)
            {
                OnDayChanged(CurrentDay);
            }
        }

        CurrentHourInt = Mathf.FloorToInt(CurrentHour);
        if (CurrentHourInt != previousHourInt)
        {
            if (OnHourChanged != null)
            {
                OnHourChanged(CurrentHourInt);
            }
        }

        WorldTimePhase nextPhase = GetPhase(CurrentHour);
        if (nextPhase != CurrentPhase || previousHour > CurrentHour)
        {
            CurrentPhase = nextPhase;
            if (OnPhaseChanged != null)
            {
                OnPhaseChanged(CurrentPhase);
            }
        }
    }

    public bool IsTavernPeakTime()
    {
        return CurrentPhase == WorldTimePhase.Evening ||
            (CurrentHour >= 18f && CurrentHour <= 23f);
    }

    public bool IsDangerousNight()
    {
        return CurrentPhase == WorldTimePhase.Night;
    }

    public string GetClockText()
    {
        return "Ngay " +
            CurrentDay +
            " - " +
            CurrentHourInt.ToString("00") +
            ":00";
    }

    public static WorldTimePhase GetPhase(float hour)
    {
        if (hour < 5f) return WorldTimePhase.Night;
        if (hour < 7f) return WorldTimePhase.Dawn;
        if (hour < 11f) return WorldTimePhase.Morning;
        if (hour < 14f) return WorldTimePhase.Noon;
        if (hour < 18f) return WorldTimePhase.Afternoon;
        if (hour < 22f) return WorldTimePhase.Evening;
        return WorldTimePhase.Night;
    }
}
