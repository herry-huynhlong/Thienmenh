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

public class WorldEventSystem : MonoBehaviour
{
    public static WorldEventSystem Instance { get; private set; }

    public float eventCheckHours = 3f;
    [Range(0f, 1f)]
    public float eventChance = 0.18f;
    public string currentEvent = "";

    float nextCheckWorldHour;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ScheduleNextCheck();
    }

    void Update()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return;
        }

        float worldHour =
            timeSystem.CurrentDay * 24f +
            timeSystem.CurrentHour;

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

        switch (eventType)
        {
            case WorldEventType.BeastWave:
                MakeBeastsAggressive(35f);
                break;
            case WorldEventType.Festival:
                ImproveVillagerMood(20f);
                break;
            case WorldEventType.HeavenlyTribulation:
                StrikeStrongestVisibleCultivator();
                break;
        }
    }

    WorldEventType RollEvent(WorldTimeSystem timeSystem)
    {
        if (timeSystem != null && timeSystem.IsDangerousNight() && Random.value < 0.55f)
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
        MonsterAI[] monsters = Object.FindObjectsByType<MonsterAI>(FindObjectsSortMode.None);
        foreach (MonsterAI monster in monsters)
        {
            if (monster == null || monster.IsDead)
            {
                continue;
            }

            monster.aggression = Mathf.Clamp(monster.aggression + amount, 0f, 100f);
            monster.bloodlust = Mathf.Clamp(monster.bloodlust + amount * 0.5f, 0f, 100f);
        }
    }

    void ImproveVillagerMood(float amount)
    {
        VillagerAI[] villagers = Object.FindObjectsByType<VillagerAI>(FindObjectsSortMode.None);
        foreach (VillagerAI villager in villagers)
        {
            if (villager == null || villager.IsDead || villager.entityProfile == null)
            {
                continue;
            }

            villager.entityProfile.emotion.happiness =
                Mathf.Clamp(villager.entityProfile.emotion.happiness + amount, 0f, 100f);
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
                Object.FindObjectsByType<CharacterStats>(FindObjectsSortMode.None));

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
            ? timeSystem.CurrentDay * 24f + timeSystem.CurrentHour
            : 0f;
        nextCheckWorldHour = worldHour + Mathf.Max(0.25f, eventCheckHours);
    }
}
