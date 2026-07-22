using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[System.Serializable]
public class FixedBlacksmithMaterialRequirement
{
    public StatItemData item;
    [Min(1)] public int amount = 1;
}

[DisallowMultipleComponent]
public partial class NpcFixedBlacksmithController : MonoBehaviour
{
    public enum ForgeCycleState
    {
        NeedMaterials,
        BuyingMaterials,
        Forging,
        ReadyToSell,
        Selling
    }

    [Header("Auto Setup")]
    public bool autoConfigureVillager = true;
    public bool disableLegacyForgeComponents = true;
    public string professionName = "Lo Ren";
    public bool suppressBaseTimeRestRules = true;
    public bool useDedicatedRoutine = true;

    [Header("Economy")]
    [Min(0)] public int startingMoney = 100000;
    public bool grantStartingMoneyOnStart = true;
    public StatItemData forgedItem;
    [Min(0)] public int materialCost = 100000;
    [Min(0)] public int salePrice = 200000;
    public bool buyMaterialsAtVanBaoLau = true;
    public bool sellAtVanBaoLau = true;
    public NpcMapZone preferredTradeZone = NpcMapZone.VanBaoLau;
    public List<FixedBlacksmithMaterialRequirement> materialRequirements =
        new List<FixedBlacksmithMaterialRequirement>();
    public bool autoPlanLowGradeBatches = true;
    [Min(2)] public int randomMaterialKindsMin = 2;
    [Min(2)] public int randomMaterialKindsMax = 3;
    [Min(0)] public int craftingLaborFee = 500;

    [Header("Production")]
    [Min(1)] public int craftDays = 3;
    [Min(1)] public int haCraftDays = 1;
    [Min(1)] public int trungCraftDays = 3;
    [Min(1)] public int thuongCraftDays = 5;
    [Range(1f, 24f)] public float workHoursPerDay = 8f;
    [Min(0.25f)] public float buyDurationSeconds = 5f;
    [Min(0.25f)] public float sellDurationSeconds = 10f;

    [Header("Schedule")]
    [Range(0f, 24f)] public float sleepStart = 20f;
    [Range(0f, 24f)] public float sleepEnd = 5f;
    [Range(0f, 24f)] public float tradeStart = 5.0833335f;
    [Range(0f, 24f)] public float morningWorkStart = 7.0833335f;
    [Range(0f, 24f)] public float morningWorkEnd = 11f;
    [Range(0f, 24f)] public float afternoonWorkStart = 14.083333f;
    [Range(0f, 24f)] public float afternoonWorkEnd = 20f;

    [Header("Points")]
    public Transform forgePointOverride;
    public Transform marketPointOverride;
    public Transform buyApproachPointOverride;
    public Transform buyPointOverride;
    public Transform sellApproachPointOverride;
    public Transform sellPointOverride;

    [Header("Runtime")]
    public ForgeCycleState state = ForgeCycleState.NeedMaterials;
    [Min(0f)] public float forgedWorkHours;
    [Min(0)] public int completedCycles;
    public float lastProgressWorldHour = -1f;
    public float stateStartedAtRealtime = -1f;
    public int lastPurchaseDay = -1;
    public int lastSaleDay = -1;
    public int lastBatchMaterialBudget;
    public int lastBatchMinimumSaleValue;
    public bool debugLogs;
    float nextRoutineRefreshRealtime = -1f;

    VillagerAI villager;
    ItemInventory inventory;
    NpcScheduleController schedule;
    Collider2D[] selfColliders;
    string lastTradeDestinationSource = "none";
    string lastTradeDestinationDetail = "none";
    string lastTradeShopName = "none";
    string lastBrokerApproachDetail = "none";
    NpcCounterBroker cachedBrokerApproachBroker;
    Vector3 cachedBrokerApproachPosition;
    ForgeCycleState cachedBrokerApproachState;
    bool hasCachedBrokerApproachPosition;
    Transform runtimeTradeApproachAnchor;
    static readonly MethodInfo villagerIsMoveTargetFeasibleMethod =
        typeof(VillagerAI).GetMethod(
            "IsMoveTargetFeasible",
            BindingFlags.Instance |
            BindingFlags.NonPublic);
    static readonly MethodInfo villagerTryFindClearPointNearMethod =
        typeof(VillagerAI).GetMethod(
            "TryFindClearPointNear",
            BindingFlags.Instance |
            BindingFlags.NonPublic);
    static readonly MethodInfo villagerHasClearLineToMethod =
        typeof(VillagerAI).GetMethod(
            "HasClearLineTo",
            BindingFlags.Instance |
            BindingFlags.NonPublic,
            null,
            new[] { typeof(Vector3) },
            null);
    static readonly FieldInfo villagerHasRoadPreferenceField =
        typeof(VillagerAI).GetField(
            "hasRoadPreference",
            BindingFlags.Instance |
            BindingFlags.NonPublic);
    static readonly FieldInfo villagerPrefersRoadForCurrentRouteField =
        typeof(VillagerAI).GetField(
            "prefersRoadForCurrentRoute",
            BindingFlags.Instance |
            BindingFlags.NonPublic);
    static readonly FieldInfo villagerRoadPreferenceTargetField =
        typeof(VillagerAI).GetField(
            "roadPreferenceTarget",
            BindingFlags.Instance |
            BindingFlags.NonPublic);

    public bool SuppressBaseTimeRestRules => suppressBaseTimeRestRules;
    public bool UseDedicatedRoutine => useDedicatedRoutine;
    public string DebugTradeDestinationSource => lastTradeDestinationSource;
    public string DebugTradeShopName => lastTradeShopName;
    public int DebugMaterialRequirementCount =>
        materialRequirements != null
            ? materialRequirements.Count
            : 0;
    public bool DebugHasConfiguredMaterialRequirements =>
        HasConfiguredMaterialRequirements();
    string BuyAction => NpcText.Action("fixedBlacksmithBuyMaterials");
    string ForgeAction => NpcText.Action("fixedBlacksmithForging");
    string SellAction => NpcText.Action("fixedBlacksmithSellGoods");
    string WaitAction => NpcText.Action("fixedBlacksmithWaitNextCycle");

    public bool ShouldKeepTradeRouteActive()
    {
        CacheReferences();

        if (villager == null ||
            !enabled ||
            !isActiveAndEnabled)
        {
            return false;
        }

        switch (state)
        {
            case ForgeCycleState.NeedMaterials:
            case ForgeCycleState.BuyingMaterials:
                if (TryGetBuyDestination(
                        out Vector3 buyPosition,
                        out _,
                        out bool isBuyBrokerTarget,
                        out NpcCounterBroker buyBroker))
                {
                    return !HasArrivedAtTradeDestination(
                        buyPosition,
                        isBuyBrokerTarget,
                        buyBroker);
                }

                return false;

            case ForgeCycleState.ReadyToSell:
            case ForgeCycleState.Selling:
                if (TryGetSellDestination(
                        out Vector3 sellPosition,
                        out _,
                        out bool isSellBrokerTarget,
                        out NpcCounterBroker sellBroker))
                {
                    return !HasArrivedAtTradeDestination(
                        sellPosition,
                        isSellBrokerTarget,
                        sellBroker);
                }

                return false;

            default:
                return false;
        }
    }

    public bool TryRunDedicatedRoutine()
    {
        CacheReferences();
        EnsureRecommendedScheduleConfigured();

        if (!useDedicatedRoutine ||
            villager == null ||
            !enabled ||
            !isActiveAndEnabled)
        {
            return false;
        }

        float currentHour = GetCurrentClockHour();
        if (IsDedicatedRestWindow(currentHour))
        {
            villager.GoHomeToRest();
            return true;
        }

        return TryRunWorkCycle();
    }

    int GetCraftDaysForCurrentItem()
    {
        if (forgedItem == null)
        {
            return Mathf.Max(1, craftDays);
        }

        switch (forgedItem.grade)
        {
            case ItemGrade.Ha:
                return Mathf.Max(1, haCraftDays);
            case ItemGrade.Trung:
                return Mathf.Max(1, trungCraftDays);
            case ItemGrade.Thuong:
                return Mathf.Max(1, thuongCraftDays);
            default:
                return Mathf.Max(1, craftDays);
        }
    }

    float RequiredWorkHours =>
        GetCraftDaysForCurrentItem() * Mathf.Max(1f, workHoursPerDay);

}
