using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class VillagerDailyTradePlan : MonoBehaviour
{
    [Range(0f, 1f)] public float baseDailyTradeChance = 0.3f;
    [Range(0f, 1f)] public float goodsTradeBonusChance = 0.35f;

    VillagerAI villager;
    int lastEvaluatedDay = int.MinValue;
    bool willTradeToday;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
    }

    public bool ShouldParticipateToday()
    {
        if (villager == null)
        {
            return false;
        }

        if (villager.job == VillagerJob.Trader)
        {
            return true;
        }

        RefreshTodayPlanIfNeeded();
        return willTradeToday;
    }

    void RefreshTodayPlanIfNeeded()
    {
        int currentDay = GetCurrentDay();
        if (currentDay == lastEvaluatedDay)
        {
            return;
        }

        lastEvaluatedDay = currentDay;

        float tradeChance =
            Mathf.Clamp01(baseDailyTradeChance);
        if (villager != null &&
            villager.HasProducedGoodsForSale())
        {
            tradeChance = Mathf.Clamp01(
                tradeChance + goodsTradeBonusChance);
        }

        int hash = BuildDailyTradeHash(currentDay);
        float roll = Mathf.Abs(hash % 1000) / 999f;
        willTradeToday = roll <= tradeChance;
    }

    int BuildDailyTradeHash(int currentDay)
    {
        string key = string.Empty;
        NPCIdentity identity = GetComponent<NPCIdentity>();
        if (identity != null &&
            !string.IsNullOrWhiteSpace(identity.npcId))
        {
            key = identity.npcId;
        }
        else if (villager != null &&
            !string.IsNullOrWhiteSpace(villager.villagerName))
        {
            key = villager.villagerName;
        }
        else
        {
            key = gameObject.name;
        }

        unchecked
        {
            return (key.GetHashCode() * 397) ^ currentDay;
        }
    }

    int GetCurrentDay()
    {
        WorldTimeSystem timeSystem =
            WorldTimeSystem.Instance;

        return timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.Max(1, Mathf.FloorToInt(Time.time / 900f) + 1);
    }
}
