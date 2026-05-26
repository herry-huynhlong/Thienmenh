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

public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    public WorldWeather CurrentWeather { get; private set; } = WorldWeather.Clear;
    public float weatherDurationHours = 4f;

    public event Action<WorldWeather> OnWeatherChanged;

    float nextChangeWorldHour;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ScheduleNextChange();
    }

    void Update()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return;
        }

        float currentWorldHour =
            timeSystem.CurrentDay * 24f +
            timeSystem.CurrentHour;

        if (currentWorldHour >= nextChangeWorldHour)
        {
            RollWeather();
            ScheduleNextChange();
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
        if (OnWeatherChanged != null)
        {
            OnWeatherChanged(CurrentWeather);
        }
    }

    void ScheduleNextChange()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float currentWorldHour = timeSystem != null
            ? timeSystem.currentDay * 24f + timeSystem.currentHour
            : 0f;
        nextChangeWorldHour =
            currentWorldHour +
            Mathf.Max(1f, weatherDurationHours);
    }
}
