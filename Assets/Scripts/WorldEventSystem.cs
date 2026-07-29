using System.Collections.Generic;
using UnityEngine;

public enum WorldEventType
{
    BeastWave,
    Festival,
    SecretRealmOpen,
    Plague,
    SectConflict,
    HeavenlyTribulation
}

[System.Serializable]
public class WorldEventPersistentState
{
    public bool hasState = true;
    public string currentEvent = "";
    public float nextCheckWorldHour;
}

public class WorldEventSystem : MonoBehaviour
{
    public static WorldEventSystem Instance { get; private set; }
    bool createdAtRuntime;

    public float eventCheckHours = 3f;
    [Range(0f, 1f)]
    public float eventChance = 0.18f;
    public string currentEvent = "";

    float nextCheckWorldHour;

    public float NextCheckWorldHour => nextCheckWorldHour;

    public WorldEventPersistentState CapturePersistentState()
    {
        return new WorldEventPersistentState
        {
            hasState = true,
            currentEvent = currentEvent ?? "",
            nextCheckWorldHour = nextCheckWorldHour
        };
    }

    public void RestorePersistentState(WorldEventPersistentState saved)
    {
        if (saved == null || !saved.hasState)
        {
            return;
        }

        currentEvent = saved.currentEvent ?? "";
        nextCheckWorldHour = saved.nextCheckWorldHour;
        if (float.IsNaN(nextCheckWorldHour) ||
            float.IsInfinity(nextCheckWorldHour))
        {
            ScheduleNextCheck();
        }
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
        ScheduleNextCheck();
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

        float worldHour = timeSystem.CurrentWorldHour;

        if (worldHour < nextCheckWorldHour)
        {
            return;
        }

        ScheduleNextCheck();

        if (Random.value > eventChance)
        {
            currentEvent = "";
            return;
        }

        TriggerEvent(RollEvent(timeSystem));
    }

    public void TriggerEvent(WorldEventType eventType)
    {
        currentEvent = eventType.ToString();

        if (WorldEventManager.Instance != null)
        {
            string logMessage = GetStoryLogMessage(eventType);
            if (!string.IsNullOrWhiteSpace(logMessage))
            {
                WorldEventManager.Instance.AddLog(logMessage, eventType, true);
            }
        }

        switch (eventType)
        {
            case WorldEventType.BeastWave:
                MakeBeastsAggressive(35f);
                break;
            case WorldEventType.HeavenlyTribulation:
                StrikeStrongestVisibleCultivator();
                break;
        }
    }

    WorldEventType RollEvent(WorldTimeSystem timeSystem)
    {
        if (timeSystem != null &&
            timeSystem.IsDangerousNight() &&
            Random.value < 0.55f)
        {
            return WorldEventType.BeastWave;
        }

        float roll = Random.value;
        if (roll < 0.25f) return WorldEventType.Festival;
        if (roll < 0.45f) return WorldEventType.SecretRealmOpen;
        if (roll < 0.62f) return WorldEventType.Plague;
        if (roll < 0.82f) return WorldEventType.SectConflict;
        return WorldEventType.HeavenlyTribulation;
    }

    void MakeBeastsAggressive(float amount)
    {
        MonsterAI[] monsters =
            Object.FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);
        foreach (MonsterAI monster in monsters)
        {
            if (monster == null || monster.IsDead)
            {
                continue;
            }

            monster.ApplyTemperamentSurge(amount, amount * 0.5f);
        }
    }

    void StrikeStrongestVisibleCultivator()
    {
        HeavenSystem heaven = HeavenSystem.Instance;
        if (heaven == null)
        {
            return;
        }

        List<CharacterStats> stats =
            new List<CharacterStats>(
                Object.FindObjectsByType<CharacterStats>(FindObjectsInactive.Exclude));

        CharacterStats strongest = null;
        int strongestPower = 0;
        foreach (CharacterStats stat in stats)
        {
            if (stat == null || stat.IsDead)
            {
                continue;
            }

            int power = stat.attack + stat.defense + stat.finalHP / 10;
            if (power > strongestPower)
            {
                strongestPower = power;
                strongest = stat;
            }
        }

        if (strongest != null)
        {
            heaven.StrikeEntity(strongest.gameObject);
        }
    }

    void ScheduleNextCheck()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float worldHour = timeSystem != null
            ? timeSystem.CurrentWorldHour
            : 0f;
        nextCheckWorldHour = worldHour + Mathf.Max(0.25f, eventCheckHours);
    }

    static string GetStoryLogMessage(WorldEventType eventType)
    {
        switch (eventType)
        {
            case WorldEventType.BeastWave:
                return UiText.Get("worldEvents", "beastWaveStory", "");
            case WorldEventType.Festival:
                return UiText.Get("worldEvents", "festivalStory", "");
            case WorldEventType.SecretRealmOpen:
                return UiText.Get("worldEvents", "secretRealmStory", "");
            case WorldEventType.Plague:
                return UiText.Get("worldEvents", "plagueStory", "");
            case WorldEventType.SectConflict:
                return UiText.Get("worldEvents", "sectConflictStory", "");
            case WorldEventType.HeavenlyTribulation:
                return UiText.Get("worldEvents", "heavenlyTribulationStory", "");
            default:
                return "";
        }
    }
}
