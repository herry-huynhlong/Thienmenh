using System.Reflection;
using UnityEngine;

[System.Serializable]
public class FixedAlchemistMaterialRequirement
{
    public StatItemData item;
    [Min(1)] public int amount = 1;
}

[RequireComponent(typeof(VillagerAI))]
[RequireComponent(typeof(ItemInventory))]
[RequireComponent(typeof(NpcTradeAgent))]
[RequireComponent(typeof(NpcAlchemyAgent))]
[RequireComponent(typeof(NpcSpecialProfession))]
[DisallowMultipleComponent]
public class NpcFixedAlchemistController : MonoBehaviour
{
    public enum AlchemyCycleState
    {
        NeedMaterials,
        BuyingMaterials,
        Refining,
        ReadyToSell,
        Selling
    }

    [Header("Auto Setup")]
    public bool autoConfigureVillager = true;
    public bool disableLegacyAlchemyAutomation = true;
    public string professionName = "Luyen Dan Su";
    public bool suppressBaseTimeRestRules = true;
    public bool useDedicatedRoutine = true;

    [Header("Economy")]
    [Min(0)] public int startingMoney = 100000;
    public bool grantStartingMoneyOnStart = true;
    public StatItemData refinedItem;
    [Min(1)] public int refinedItemAmount = 1;
    [Min(0)] public int materialCost = 100000;
    [Min(0)] public int salePrice = 200000;
    public bool buyMaterialsAtVanBaoLau = true;
    public bool sellAtVanBaoLau = true;
    public NpcMapZone preferredTradeZone = NpcMapZone.VanBaoLau;
    public System.Collections.Generic.List<FixedAlchemistMaterialRequirement>
        materialRequirements =
            new System.Collections.Generic.List<FixedAlchemistMaterialRequirement>();
    public bool autoPlanLowGradeBatches = true;
    [Min(2)] public int randomMaterialKindsMin = 2;
    [Min(2)] public int randomMaterialKindsMax = 3;
    [Min(0)] public int refiningLaborFee = 300;

    [Header("Daily Supplies")]
    public bool buyDailyConsumables = true;
    public StatItemData dailyRiceItem;
    public StatItemData dailyFishItem;
    public StatItemData dailyMeatItem;
    [Min(0)] public int dailyRiceAmount = 1;
    [Min(0)] public int dailyProteinAmount = 1;

    [Header("Production")]
    [Min(0.25f)] public float buyDurationSeconds = 5f;
    [Min(0.25f)] public float sellDurationSeconds = 8f;

    [Header("Schedule")]
    [Range(0f, 24f)] public float sleepStart = 20f;
    [Range(0f, 24f)] public float sleepEnd = 5f;
    [Range(0f, 24f)] public float tradeStart = 5.0833335f;
    [Range(0f, 24f)] public float morningWorkStart = 7.0833335f;
    [Range(0f, 24f)] public float morningWorkEnd = 11f;
    [Range(0f, 24f)] public float afternoonWorkStart = 14.083333f;
    [Range(0f, 24f)] public float afternoonWorkEnd = 20f;

    [Header("Points")]
    public Transform alchemyPointOverride;
    public Transform marketPointOverride;
    public Transform buyApproachPointOverride;
    public Transform buyPointOverride;
    public Transform sellApproachPointOverride;
    public Transform sellPointOverride;

    [Header("Runtime")]
    public AlchemyCycleState state = AlchemyCycleState.NeedMaterials;
    public float stateStartedAtRealtime = -1f;
    public int lastPurchaseDay = -1;
    public int lastSaleDay = -1;
    public int lastDailyConsumableTradeDay = -1;
    public int lastBatchMaterialBudget;
    public int lastBatchMinimumSaleValue;
    public bool debugLogs;
    float nextRoutineRefreshRealtime = -1f;

    VillagerAI villager;
    ItemInventory inventory;
    NpcScheduleController schedule;
    NpcAlchemyAgent alchemyAgent;
    NpcTradeAgent tradeAgent;
    string lastTradeDestinationSource = "none";
    string lastTradeDestinationDetail = "none";
    string lastTradeShopName = "none";
    string lastBrokerApproachDetail = "none";
    NpcCounterBroker cachedBrokerApproachBroker;
    Vector3 cachedBrokerApproachPosition;
    AlchemyCycleState cachedBrokerApproachState;
    bool hasCachedBrokerApproachPosition;
    Vector3 cachedAlchemyRouteAnchorPosition;
    NpcMapZone? cachedAlchemyRouteAnchorZone;
    bool hasCachedAlchemyRouteAnchor;
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
    string BuyAction => NpcText.Action("fixedAlchemistBuyMaterials");
    string MoveToAlchemyAction => NpcText.Action("goAlchemy");
    string RefineAction => NpcText.Action("fixedAlchemistRefining");
    string SellAction => NpcText.Action("fixedAlchemistSellGoods");
    string WaitAction => NpcText.Action("fixedAlchemistWaitNextCycle");

    void Reset()
    {
        CacheReferences();
        ApplyRecommendedSetup();
    }

    void Awake()
    {
        CacheReferences();

        if (autoConfigureVillager)
        {
            ApplyRecommendedSetup();
        }
        else
        {
            EnsureRecommendedScheduleConfigured();
        }
    }

    void Start()
    {
        CacheReferences();
        if (autoConfigureVillager)
        {
            ApplyRecommendedSetup();
            StartCoroutine(ApplyRecommendedSetupNextFrame());
        }
        else
        {
            EnsureRecommendedScheduleConfigured();
        }

        if (grantStartingMoneyOnStart)
        {
            EnsureStartingMoney();
        }

        if (villager != null)
        {
            villager.EnsureHomePointResolved();
        }

        CacheAlchemyRouteAnchor();
        SyncSleepVisibility();
    }

    void Update()
    {
        SyncSleepVisibility();
        TickDedicatedRoutineDriver();
    }

    System.Collections.IEnumerator ApplyRecommendedSetupNextFrame()
    {
        yield return null;
        ApplyRecommendedSetup();
    }

    void TickDedicatedRoutineDriver()
    {
        if (!Application.isPlaying ||
            !useDedicatedRoutine ||
            Time.time < nextRoutineRefreshRealtime)
        {
            return;
        }

        CacheReferences();
        nextRoutineRefreshRealtime =
            Time.time +
            Mathf.Max(
                0.1f,
                villager != null
                    ? villager.thinkInterval * 0.5f
                    : 0.2f);

        if (villager == null ||
            !enabled ||
            !isActiveAndEnabled ||
            NpcRoleUtility.IsDead(gameObject) ||
            NpcRoleUtility.IsInCombat(gameObject))
        {
            return;
        }

        if (grantStartingMoneyOnStart)
        {
            EnsureStartingMoney();
        }

        TryRunDedicatedRoutine();
    }

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
            case AlchemyCycleState.NeedMaterials:
            case AlchemyCycleState.BuyingMaterials:
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

            case AlchemyCycleState.ReadyToSell:
            case AlchemyCycleState.Selling:
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

    public bool TryRunWorkCycle()
    {
        CacheReferences();

        if (villager == null ||
            alchemyAgent == null ||
            !enabled ||
            !isActiveAndEnabled)
        {
            return false;
        }

        if (alchemyAgent.IsRefining)
        {
            state = AlchemyCycleState.Refining;
        }
        else if (HasProductsReadyToSell())
        {
            if (state == AlchemyCycleState.Refining)
            {
                villager.SetActionImmediate(
                    NpcText.Action("fixedAlchemistRefineComplete"),
                    1f);
            }

            if (state != AlchemyCycleState.Selling)
            {
                state = AlchemyCycleState.ReadyToSell;
            }
        }

        switch (state)
        {
            case AlchemyCycleState.NeedMaterials:
                return HandleNeedMaterials();
            case AlchemyCycleState.BuyingMaterials:
                return HandleBuyingMaterials();
            case AlchemyCycleState.Refining:
                return HandleRefining();
            case AlchemyCycleState.ReadyToSell:
                return HandleReadyToSell();
            case AlchemyCycleState.Selling:
                return HandleSelling();
            default:
                return false;
        }
    }

    bool HandleNeedMaterials()
    {
        if (alchemyAgent == null)
        {
            return false;
        }

        EnsureDynamicAlchemyBatchPlan();
        bool needsDailyConsumables =
            NeedsDailyConsumableTradeToday();

        if (HasProductsReadyToSell())
        {
            state = AlchemyCycleState.ReadyToSell;
            return true;
        }

        if (autoPlanLowGradeBatches &&
            !HasConfiguredMaterialRequirements() &&
            !needsDailyConsumables)
        {
            villager.SetActionImmediate(
                NpcText.Action("fixedAlchemistCouldNotBuyMaterials"),
                2f);
            return true;
        }

        if (NeedsMaterialsForNextBatch() ||
            needsDailyConsumables)
        {
            if (!CanLeaveForProductionTrade())
            {
                villager.SetActionImmediate(WaitAction, villager.thinkInterval);
                return true;
            }

            if (!TryGetBuyDestination(
                    out Vector3 buyPosition,
                    out NpcMapZone? buyZone,
                    out bool isBrokerTarget,
                    out NpcCounterBroker buyBroker))
            {
                villager.SetActionImmediate(
                    NpcText.Action("fixedAlchemistMissingBuyPoint"),
                    Mathf.Max(1f, villager.thinkInterval));
                return true;
            }

            if (!HasArrivedAtTradeDestination(
                    buyPosition,
                    isBrokerTarget,
                    buyBroker))
            {
                MoveVillagerToTradeTarget(
                    buyPosition,
                    BuyAction,
                    buyZone,
                    isBrokerTarget);
                return true;
            }

            BeginBuyingMaterials();
            return true;
        }

        if (!MoveVillagerToAlchemyPointIfNeeded())
        {
            return true;
        }

        if (!CanPerformAlchemyWorkNow())
        {
            villager.SetActionImmediate(WaitAction, villager.thinkInterval);
            return true;
        }

        if (HasConfiguredMaterialRequirements())
        {
            StatItemData batchResult = refinedItem;
            int batchAmount =
                Mathf.Max(1, refinedItemAmount);
            if (!ConsumeConfiguredMaterials())
            {
                villager.SetActionImmediate(
                    NpcText.Action("fixedAlchemistMissingMaterialsContinue"),
                    2f);
                return true;
            }

            ConfigureCurrentBatchRefineDuration(batchResult);
            if (alchemyAgent.TryStartFixedAlchemy(
                    batchResult,
                    batchAmount))
            {
                if (autoPlanLowGradeBatches)
                {
                    ResetDynamicAlchemyBatchPlan();
                }

                state = AlchemyCycleState.Refining;
                stateStartedAtRealtime = Time.time;
                villager.SetActionImmediate(RefineAction, 1f);
                return true;
            }

            villager.SetActionImmediate(
                NpcText.Action("fixedAlchemistCannotStartBatch"),
                2f);
            return true;
        }

        if (autoPlanLowGradeBatches)
        {
            villager.SetActionImmediate(WaitAction, villager.thinkInterval);
            return true;
        }

        if (alchemyAgent.TryStartAnyAlchemy())
        {
            state = AlchemyCycleState.Refining;
            stateStartedAtRealtime = Time.time;
            villager.SetActionImmediate(RefineAction, 1f);
            return true;
        }

        villager.SetActionImmediate(WaitAction, villager.thinkInterval);
        return true;
    }

    void BeginBuyingMaterials()
    {
        state = AlchemyCycleState.BuyingMaterials;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate(BuyAction, buyDurationSeconds);
    }

    bool HandleBuyingMaterials()
    {
        if (!TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker buyBroker))
        {
            state = AlchemyCycleState.NeedMaterials;
            return true;
        }

        if (!HasArrivedAtTradeDestination(
                buyPosition,
                isBrokerTarget,
                buyBroker))
        {
            MoveVillagerToTradeTarget(
                buyPosition,
                BuyAction,
                buyZone,
                isBrokerTarget);
            return true;
        }

        if (!HasActionFinished(buyDurationSeconds))
        {
            villager.SetActionImmediate(BuyAction, buyDurationSeconds);
            return true;
        }

        bool boughtAllMaterials = false;
        bool traded = false;
        bool attemptedDailyConsumables =
            NeedsDailyConsumableTradeToday();
        string dailyConsumableDetail = "skipped";

        if (HasConfiguredMaterialRequirements())
        {
            string purchaseDetail;
            boughtAllMaterials =
                TryPurchaseConfiguredMaterials(
                    out purchaseDetail);
            traded =
                boughtAllMaterials ||
                DidPurchaseAnyConfiguredMaterials(
                    purchaseDetail);
        }

        if (attemptedDailyConsumables)
        {
            TryPurchaseDailyConsumables(
                out dailyConsumableDetail);
            lastDailyConsumableTradeDay = GetCurrentDay();
        }

        lastPurchaseDay = GetCurrentDay();
        state = AlchemyCycleState.NeedMaterials;

        if (!NeedsMaterialsForNextBatch())
        {
            if (!MoveVillagerToAlchemyPointIfNeeded())
            {
                return true;
            }

            villager.SetActionImmediate(
                NpcText.Action("fixedAlchemistBoughtMaterials"),
                1f);
            return true;
        }

        villager.SetActionImmediate(
            NpcText.Action(
                traded
                    ? "fixedAlchemistMaterialsStillMissing"
                    : "fixedAlchemistCouldNotBuyMaterials"),
            2f);
        return true;
    }

    bool HandleRefining()
    {
        if (!alchemyAgent.IsRefining)
        {
            state = HasProductsReadyToSell()
                ? AlchemyCycleState.ReadyToSell
                : AlchemyCycleState.NeedMaterials;
            return true;
        }

        if (MoveVillagerToAlchemyPointIfNeeded())
        {
            villager.SetActionImmediate(
                GetRefiningProgressAction(),
                Mathf.Max(1f, villager.thinkInterval));
        }

        return true;
    }

    bool HandleReadyToSell()
    {
        if (!HasProductsReadyToSell())
        {
            state = AlchemyCycleState.NeedMaterials;
            return true;
        }

        if (!CanLeaveForProductionTrade())
        {
            villager.SetActionImmediate(WaitAction, villager.thinkInterval);
            return true;
        }

        if (!TryGetSellDestination(
                out Vector3 sellPosition,
                out NpcMapZone? sellZone,
                out bool isBrokerTarget,
                out NpcCounterBroker sellBroker))
        {
            villager.SetActionImmediate(
                NpcText.Action("fixedAlchemistMissingSellPoint"),
                Mathf.Max(1f, villager.thinkInterval));
            return true;
        }

        if (!HasArrivedAtTradeDestination(
                sellPosition,
                isBrokerTarget,
                sellBroker))
        {
            MoveVillagerToTradeTarget(
                sellPosition,
                SellAction,
                sellZone,
                isBrokerTarget);
            return true;
        }

        state = AlchemyCycleState.Selling;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate(SellAction, sellDurationSeconds);
        return true;
    }

    bool HandleSelling()
    {
        if (!TryGetSellDestination(
                out Vector3 sellPosition,
                out NpcMapZone? sellZone,
                out bool isBrokerTarget,
                out NpcCounterBroker sellBroker))
        {
            state = AlchemyCycleState.NeedMaterials;
            return true;
        }

        if (!HasArrivedAtTradeDestination(
                sellPosition,
                isBrokerTarget,
                sellBroker))
        {
            MoveVillagerToTradeTarget(
                sellPosition,
                SellAction,
                sellZone,
                isBrokerTarget);
            return true;
        }

        if (!HasActionFinished(sellDurationSeconds))
        {
            villager.SetActionImmediate(SellAction, sellDurationSeconds);
            return true;
        }

        bool sold =
            TrySellFinishedGoodsToMarket(
                out string sellDetail);
        if (sold)
        {
            lastSaleDay = GetCurrentDay();
        }

        state = HasProductsReadyToSell()
            ? AlchemyCycleState.ReadyToSell
            : AlchemyCycleState.NeedMaterials;
        stateStartedAtRealtime = -1f;

        if (sold &&
            !HasProductsReadyToSell() &&
            autoPlanLowGradeBatches)
        {
            ResetDynamicAlchemyBatchPlan();
        }

        villager.SetActionImmediate(
            NpcText.Action(
                sold
                    ? "fixedAlchemistSoldGoods"
                    : "fixedAlchemistCouldNotSellGoods"),
            1f);
        return true;
    }

    bool MoveVillagerToAlchemyPointIfNeeded()
    {
        if (!TryGetAlchemyRouteTarget(
                out Vector3 alchemyPosition,
                out NpcMapZone? alchemyZone))
        {
            return true;
        }

        Vector3 safeAlchemyPosition =
            GetSafeAlchemyPosition(alchemyPosition);
        if (IsNear(safeAlchemyPosition))
        {
            return true;
        }

        villager.ForceJobMoveTo(
            safeAlchemyPosition,
            MoveToAlchemyAction,
            alchemyZone);
        return false;
    }

    bool TryGetBuyDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        return TryGetTradeDestination(
            buyPointOverride,
            "buyPointOverride",
            buyMaterialsAtVanBaoLau,
            out targetPosition,
            out targetZone,
            out isBrokerTarget,
            out broker);
    }

    bool TryGetSellDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        return TryGetTradeDestination(
            sellPointOverride,
            "sellPointOverride",
            sellAtVanBaoLau,
            out targetPosition,
            out targetZone,
            out isBrokerTarget,
            out broker);
    }

    bool TryGetTradeDestination(
        Transform pointOverride,
        string sourceLabel,
        bool allowBroker,
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        broker = null;
        lastTradeShopName = "none";

        if (TryGetManualTradePoint(
                pointOverride,
                sourceLabel,
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (allowBroker &&
            IsSellTradeState() &&
            TryGetBrokerDestination(
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (TryGetPreferredTradeDestination(
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        Transform marketPoint = GetMarketPoint();
        NpcMapZone? marketZone =
            ResolveZoneForTransform(marketPoint);

        if (marketPointOverride != null &&
            marketPoint != null &&
            marketZone.HasValue)
        {
            targetPosition = marketPoint.position;
            targetZone = marketZone;
            isBrokerTarget = false;
            broker = null;
            lastTradeDestinationSource = "marketPointOverride";
            return true;
        }

        if (allowBroker)
        {
            if (TryGetBrokerDestination(
                    out targetPosition,
                    out targetZone,
                    out isBrokerTarget,
                    out broker))
            {
                return true;
            }
        }

        if (marketPoint != null)
        {
            targetPosition = marketPoint.position;
            targetZone = allowBroker && marketZone.HasValue
                ? preferredTradeZone
                : marketZone;
            lastTradeDestinationSource = "marketPoint";
            return true;
        }

        return TryGetZoneFallbackDestination(
            preferredTradeZone,
            out targetPosition,
            out targetZone,
            out isBrokerTarget,
            out broker);
    }

    bool HasArrivedAtTradeDestination(
        Vector3 targetPosition,
        bool isBrokerTarget,
        NpcCounterBroker broker)
    {
        if (isBrokerTarget &&
            broker != null &&
            broker.receiveAllNpcRequests)
        {
            return broker.IsCustomerAtCounter(gameObject) ||
                IsNear(targetPosition);
        }

        return IsNear(targetPosition);
    }

    bool IsSellTradeState()
    {
        return state == AlchemyCycleState.ReadyToSell ||
            state == AlchemyCycleState.Selling;
    }

    void MoveVillagerToTradeTarget(
        Vector3 targetPosition,
        string action,
        NpcMapZone? targetZone,
        bool isBrokerTarget)
    {
        if (villager == null)
        {
            return;
        }

        if (TryGetExactTradeApproachTarget(
                targetZone,
                out Vector3 approachPosition,
                out NpcMapZone? approachZone))
        {
            ForceVillagerRoadPreference(approachPosition);
            villager.ForceJobMoveTo(
                approachPosition,
                action,
                approachZone,
                true);
            return;
        }

        if (isBrokerTarget)
        {
            ForceVillagerRoadPreference(targetPosition);
        }

        villager.ForceJobMoveTo(
            targetPosition,
            action,
            targetZone,
            isBrokerTarget ||
            ShouldUseRoadForTradeMove(
                targetZone,
                isBrokerTarget));
    }

    bool TryGetExactTradeApproachTarget(
        NpcMapZone? fallbackZone,
        out Vector3 approachPosition,
        out NpcMapZone? approachZone)
    {
        approachPosition = Vector3.zero;
        approachZone = fallbackZone;

        Transform point = GetActiveTradeApproachPoint();
        if (point == null)
        {
            return false;
        }

        if (!IsTradeApproachPointCompatible(point, fallbackZone))
        {
            return false;
        }

        approachPosition = point.position;
        approachZone = ResolveZoneForTransform(point) ?? fallbackZone;
        return !IsNear(approachPosition);
    }

    Transform GetActiveTradeApproachPoint()
    {
        switch (state)
        {
            case AlchemyCycleState.NeedMaterials:
            case AlchemyCycleState.BuyingMaterials:
                return buyApproachPointOverride;
            case AlchemyCycleState.ReadyToSell:
            case AlchemyCycleState.Selling:
                return sellApproachPointOverride;
            default:
                return null;
        }
    }

    Transform GetActiveTradePointOverride()
    {
        switch (state)
        {
            case AlchemyCycleState.NeedMaterials:
            case AlchemyCycleState.BuyingMaterials:
                return buyPointOverride;
            case AlchemyCycleState.ReadyToSell:
            case AlchemyCycleState.Selling:
                return sellPointOverride;
            default:
                return null;
        }
    }

    bool IsTradeApproachPointCompatible(
        Transform approachPoint,
        NpcMapZone? fallbackZone)
    {
        if (approachPoint == null)
        {
            return false;
        }

        Transform targetPoint = GetActiveTradePointOverride();
        if (targetPoint == null)
        {
            return true;
        }

        if (approachPoint == targetPoint ||
            approachPoint.IsChildOf(targetPoint) ||
            targetPoint.IsChildOf(approachPoint) ||
            approachPoint.parent == targetPoint.parent)
        {
            return true;
        }

        NpcCounterBroker approachBroker =
            approachPoint.GetComponentInParent<NpcCounterBroker>();
        NpcCounterBroker targetBroker =
            targetPoint.GetComponentInParent<NpcCounterBroker>();
        if (approachBroker != null &&
            approachBroker == targetBroker)
        {
            return true;
        }

        NpcMapZone? approachZone =
            ResolveZoneForTransform(approachPoint) ??
            fallbackZone;
        NpcMapZone? targetZone =
            ResolveZoneForTransform(targetPoint) ??
            fallbackZone;
        if (approachZone.HasValue &&
            targetZone.HasValue &&
            approachZone.Value == targetZone.Value &&
            Vector2.Distance(
                approachPoint.position,
                targetPoint.position) <= 6f)
        {
            return true;
        }

        return false;
    }

    bool TryGetManualTradePoint(
        Transform pointOverride,
        string sourceLabel,
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        broker = null;

        if (pointOverride == null)
        {
            return false;
        }

        targetZone = ResolveZoneForTransform(pointOverride);
        broker = pointOverride.GetComponentInParent<NpcCounterBroker>();

        if (broker == null)
        {
            broker = FindBrokerForManualTradePoint(
                pointOverride.position,
                targetZone);
        }

        if (broker != null)
        {
            Vector3 preferredPosition = pointOverride.position;
            targetZone = ResolveBrokerZone(broker) ?? targetZone;
            isBrokerTarget = broker.receiveAllNpcRequests;
            targetPosition =
                TryGetClearCustomerZonePreferredPoint(
                    broker,
                    preferredPosition,
                    out Vector3 approachPosition)
                    ? approachPosition
                    : GetBrokerApproachPosition(broker);
            lastTradeDestinationSource = sourceLabel + ":broker";
            lastTradeDestinationDetail =
                "manualPoint=" + pointOverride.position +
                " approachDetail=" + lastBrokerApproachDetail;
            lastTradeShopName = broker.name;
            return true;
        }

        targetPosition = pointOverride.position;
        lastTradeDestinationSource = sourceLabel;
        lastTradeDestinationDetail =
            "manualPointNoBroker=" + pointOverride.position;
        return true;
    }

    bool ShouldUseRoadForTradeMove(
        NpcMapZone? targetZone,
        bool isBrokerTarget)
    {
        if (!isBrokerTarget)
        {
            return true;
        }

        if (!targetZone.HasValue)
        {
            return false;
        }

        NpcMapZone? currentZone =
            ResolveZoneForPosition(transform.position);
        return !currentZone.HasValue ||
            currentZone.Value != targetZone.Value;
    }

    Vector3 GetBrokerApproachPosition(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            lastBrokerApproachDetail = "broker=null";
            return Vector3.zero;
        }

        Vector3 customerCenter = broker.CustomerPosition;
        Vector3 approachPosition =
            broker.GetCustomerPositionFor(gameObject);
        BoxCollider2D customerZone =
            broker.GetCustomerZoneCollider();

        if (customerZone != null &&
            customerZone.enabled)
        {
            if (customerZone.OverlapPoint(transform.position))
            {
                hasCachedBrokerApproachPosition = false;
                approachPosition = transform.position;
                approachPosition.z = customerCenter.z;
                lastBrokerApproachDetail =
                    "mode=actorInsideCustomerZone actorPos=" +
                    transform.position +
                    " resolved=" + approachPosition;
                return approachPosition;
            }

            if (hasCachedBrokerApproachPosition &&
                cachedBrokerApproachBroker == broker &&
                cachedBrokerApproachState == state &&
                customerZone.OverlapPoint(cachedBrokerApproachPosition))
            {
                lastBrokerApproachDetail =
                    "mode=cached state=" + cachedBrokerApproachState +
                    " resolved=" + cachedBrokerApproachPosition;
                return cachedBrokerApproachPosition;
            }

            if (!TryGetClearCustomerZoneApproachPosition(
                    broker,
                    customerZone,
                    customerCenter,
                    out approachPosition))
            {
                approachPosition =
                    GetRandomCustomerZoneApproachPosition(
                        customerZone,
                        customerCenter);
                lastBrokerApproachDetail =
                    "mode=randomCustomerZone center=" +
                    customerCenter +
                    " resolved=" + approachPosition;
            }
            else
            {
                lastBrokerApproachDetail =
                    "mode=clearCustomerZone center=" +
                    customerCenter +
                    " preferred=" +
                    broker.GetCustomerPositionFor(gameObject) +
                    " resolved=" + approachPosition;
            }

            cachedBrokerApproachBroker = broker;
            cachedBrokerApproachPosition = approachPosition;
            cachedBrokerApproachState = state;
            hasCachedBrokerApproachPosition = true;
            return approachPosition;
        }

        float serviceRadius =
            Mathf.Max(0.1f, broker.CustomerServiceRadius);
        Vector2 offset =
            (Vector2)(approachPosition - customerCenter);

        if (offset.sqrMagnitude >
            serviceRadius * serviceRadius)
        {
            approachPosition =
                customerCenter +
                (Vector3)(offset.normalized * serviceRadius * 0.85f);
        }

        approachPosition.z = customerCenter.z;
        lastBrokerApproachDetail =
            "mode=serviceRadiusClamp center=" + customerCenter +
            " preferred=" + broker.GetCustomerPositionFor(gameObject) +
            " resolved=" + approachPosition +
            " radius=" + serviceRadius.ToString("0.00");
        return approachPosition;
    }

    void ForceVillagerRoadPreference(Vector3 targetPosition)
    {
        if (villager == null)
        {
            return;
        }

        villagerHasRoadPreferenceField?.SetValue(
            villager,
            true);
        villagerPrefersRoadForCurrentRouteField?.SetValue(
            villager,
            true);
        villagerRoadPreferenceTargetField?.SetValue(
            villager,
            targetPosition);
    }

    NpcMapZone? ResolveZoneForPosition(Vector3 position)
    {
        NpcMapArea area = NpcMapArea.FindArea(position);
        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(position);
        }

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    Vector3 GetRandomCustomerZoneApproachPosition(
        BoxCollider2D customerZone,
        Vector3 customerCenter)
    {
        Bounds bounds = customerZone.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float marginX = Mathf.Clamp(
            extents.x * 0.08f,
            0.05f,
            Mathf.Max(0.05f, extents.x - 0.02f));
        float marginY = Mathf.Clamp(
            extents.y * 0.08f,
            0.05f,
            Mathf.Max(0.05f, extents.y - 0.02f));
        float minX = center.x - extents.x + marginX;
        float maxX = center.x + extents.x - marginX;
        float minY = center.y - extents.y + marginY;
        float maxY = center.y + extents.y - marginY;
        float edgeInset = Mathf.Clamp(
            GetArrivalDistance() + 0.2f,
            0.25f,
            Mathf.Max(
                0.25f,
                Mathf.Min(extents.x, extents.y) - 0.05f));

        float safeMinX = Mathf.Min(maxX, minX + edgeInset);
        float safeMaxX = Mathf.Max(minX, maxX - edgeInset);
        float safeMinY = Mathf.Min(maxY, minY + edgeInset);
        float safeMaxY = Mathf.Max(minY, maxY - edgeInset);

        if (safeMinX > safeMaxX)
        {
            float midX = (minX + maxX) * 0.5f;
            safeMinX = midX;
            safeMaxX = midX;
        }

        if (safeMinY > safeMaxY)
        {
            float midY = (minY + maxY) * 0.5f;
            safeMinY = midY;
            safeMaxY = midY;
        }

        float standX =
            Mathf.Approximately(safeMinX, safeMaxX)
                ? safeMinX
                : Random.Range(safeMinX, safeMaxX);
        float standY =
            Mathf.Approximately(safeMinY, safeMaxY)
                ? safeMinY
                : Random.Range(safeMinY, safeMaxY);

        return new Vector3(
            standX,
            standY,
            customerCenter.z);
    }

    bool TryGetClearCustomerZonePreferredPoint(
        NpcCounterBroker broker,
        Vector3 preferredPosition,
        out Vector3 approachPosition)
    {
        approachPosition = preferredPosition;

        if (broker == null)
        {
            return false;
        }

        BoxCollider2D customerZone =
            broker.GetCustomerZoneCollider();
        Vector3 customerCenter = broker.CustomerPosition;

        return TryGetClearCustomerZoneApproachPosition(
            broker,
            customerZone,
            customerCenter,
            preferredPosition,
            out approachPosition);
    }

    bool TryGetClearCustomerZoneApproachPosition(
        NpcCounterBroker broker,
        BoxCollider2D customerZone,
        Vector3 customerCenter,
        out Vector3 approachPosition)
    {
        Vector3 preferred =
            broker != null
                ? broker.GetCustomerPositionFor(gameObject)
                : customerCenter;

        return TryGetClearCustomerZoneApproachPosition(
            broker,
            customerZone,
            customerCenter,
            preferred,
            out approachPosition);
    }

    bool TryGetClearCustomerZoneApproachPosition(
        NpcCounterBroker broker,
        BoxCollider2D customerZone,
        Vector3 customerCenter,
        Vector3 preferred,
        out Vector3 approachPosition)
    {
        approachPosition = customerCenter;

        if (customerZone == null ||
            !customerZone.enabled)
        {
            return false;
        }

        preferred.z = customerCenter.z;

        if (TryResolveReachableCustomerZonePoint(
                preferred,
                customerZone,
                out Vector3 resolvedPreferred))
        {
            approachPosition = resolvedPreferred;
            return true;
        }

        Bounds bounds = customerZone.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float margin = Mathf.Clamp(
            GetApproachClearanceRadius() * 0.75f,
            0.05f,
            Mathf.Max(
                0.05f,
                Mathf.Min(extents.x, extents.y) - 0.02f));
        float minX = center.x - extents.x + margin;
        float maxX = center.x + extents.x - margin;
        float minY = center.y - extents.y + margin;
        float maxY = center.y + extents.y - margin;
        float step = Mathf.Max(
            0.14f,
            GetApproachClearanceRadius() * 0.85f);
        float bestDistance = float.PositiveInfinity;
        bool found = false;

        for (float y = minY; y <= maxY; y += step)
        {
            for (float x = minX; x <= maxX; x += step)
            {
                Vector3 candidate = new Vector3(
                    x,
                    y,
                    customerCenter.z);
                if (!TryResolveReachableCustomerZonePoint(
                        candidate,
                        customerZone,
                        out Vector3 resolvedCandidate))
                {
                    continue;
                }

                float distance =
                    (resolvedCandidate - preferred).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    approachPosition = resolvedCandidate;
                    found = true;
                }
            }
        }

        return found;
    }

    NpcCounterBroker FindBrokerForManualTradePoint(
        Vector3 position,
        NpcMapZone? targetZone)
    {
        NpcCounterBroker[] brokers =
            FindObjectsByType<NpcCounterBroker>(
                FindObjectsInactive.Exclude);

        NpcCounterBroker bestBroker = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < brokers.Length; i++)
        {
            NpcCounterBroker candidate = brokers[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveBrokerZone(candidate);
            if (targetZone.HasValue &&
                candidateZone.HasValue &&
                candidateZone.Value != targetZone.Value)
            {
                continue;
            }

            BoxCollider2D customerZone =
                candidate.GetCustomerZoneCollider();
            bool coversPoint =
                customerZone != null &&
                customerZone.enabled &&
                customerZone.bounds.Contains(position);
            if (!coversPoint)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    position,
                    candidate.CustomerPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestBroker = candidate;
            }
        }

        if (bestBroker != null)
        {
            return bestBroker;
        }

        NpcCounterBroker zoneBroker = null;
        if (targetZone.HasValue &&
            TryFindBrokerInZone(targetZone.Value, out zoneBroker))
        {
            return zoneBroker;
        }

        return null;
    }

    bool TryResolveReachableCustomerZonePoint(
        Vector3 candidate,
        BoxCollider2D customerZone,
        out Vector3 resolvedPoint)
    {
        resolvedPoint = candidate;

        if (!IsCustomerZoneApproachClear(
                candidate,
                customerZone))
        {
            return false;
        }

        if (IsVillagerMoveTargetFeasible(candidate) &&
            HasVillagerClearLineTo(candidate))
        {
            return true;
        }

        if (!TryFindVillagerClearPointNear(
                candidate,
                customerZone,
                out Vector3 clearPoint))
        {
            return false;
        }

        clearPoint.z = candidate.z;
        if (!IsCustomerZoneApproachClear(
                clearPoint,
                customerZone) ||
            !HasVillagerClearLineTo(clearPoint))
        {
            return false;
        }

        resolvedPoint = clearPoint;
        return true;
    }

    bool IsCustomerZoneApproachClear(
        Vector3 candidate,
        BoxCollider2D customerZone)
    {
        if (customerZone == null ||
            !customerZone.enabled)
        {
            return false;
        }

        Bounds bounds = customerZone.bounds;
        if (!bounds.Contains(candidate))
        {
            return false;
        }

        float clearanceRadius = GetApproachClearanceRadius();
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            candidate,
            clearanceRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                hit.isTrigger ||
                hit == customerZone ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<VillagerAI>() != null ||
                hit.GetComponentInParent<SmartNpcAI>() != null ||
                hit.GetComponentInParent<NpcMapMover2D>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    bool IsVillagerMoveTargetFeasible(Vector3 candidate)
    {
        if (villager == null ||
            villagerIsMoveTargetFeasibleMethod == null)
        {
            return true;
        }

        object result =
            villagerIsMoveTargetFeasibleMethod.Invoke(
                villager,
                new object[] { candidate });
        return result is bool feasible &&
            feasible;
    }

    bool HasVillagerClearLineTo(Vector3 candidate)
    {
        if (villager == null ||
            villagerHasClearLineToMethod == null)
        {
            return true;
        }

        object result =
            villagerHasClearLineToMethod.Invoke(
                villager,
                new object[] { candidate });
        return result is bool clear &&
            clear;
    }

    bool TryFindVillagerClearPointNear(
        Vector3 candidate,
        BoxCollider2D customerZone,
        out Vector3 clearPoint)
    {
        clearPoint = candidate;

        if (villager == null ||
            customerZone == null ||
            !customerZone.enabled ||
            villagerTryFindClearPointNearMethod == null)
        {
            return false;
        }

        object[] args =
        {
            candidate,
            candidate
        };
        object result =
            villagerTryFindClearPointNearMethod.Invoke(
                villager,
                args);
        if (!(result is bool found) ||
            !found)
        {
            return false;
        }

        if (!(args[1] is Vector3 resolved))
        {
            return false;
        }

        if (!customerZone.bounds.Contains(resolved))
        {
            return false;
        }

        clearPoint = resolved;
        return true;
    }

    float GetApproachClearanceRadius()
    {
        float radius =
            Mathf.Max(0.12f, GetArrivalDistance() * 0.8f);
        Collider2D[] ownColliders =
            GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D own = ownColliders[i];
            if (own == null ||
                own.isTrigger)
            {
                continue;
            }

            Bounds bounds = own.bounds;
            radius = Mathf.Max(
                radius,
                Mathf.Min(
                    0.35f,
                    Mathf.Max(
                        bounds.extents.x,
                        bounds.extents.y)));
        }

        return Mathf.Clamp(radius, 0.12f, 0.35f);
    }

    Transform GetAlchemyPoint()
    {
        if (alchemyPointOverride != null)
        {
            return alchemyPointOverride;
        }

        if (alchemyAgent != null &&
            alchemyAgent.alchemyStandPoint != null)
        {
            return alchemyAgent.alchemyStandPoint;
        }

        return villager != null
            ? villager.workPoint
            : null;
    }

    void CacheAlchemyRouteAnchor()
    {
        hasCachedAlchemyRouteAnchor = false;
        cachedAlchemyRouteAnchorZone = null;

        Transform alchemyPoint = GetAlchemyPoint();
        if (alchemyPoint == null ||
            !ShouldUseStaticAlchemyRouteAnchor(alchemyPoint))
        {
            return;
        }

        cachedAlchemyRouteAnchorPosition = alchemyPoint.position;
        cachedAlchemyRouteAnchorZone =
            ResolveZoneForTransform(alchemyPoint) ??
            ResolveZoneForPosition(cachedAlchemyRouteAnchorPosition);
        hasCachedAlchemyRouteAnchor = true;
    }

    bool TryGetAlchemyRouteTarget(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone)
    {
        targetPosition = transform.position;
        targetZone = null;

        Transform alchemyPoint = GetAlchemyPoint();
        if (alchemyPoint == null)
        {
            return false;
        }

        if (ShouldUseStaticAlchemyRouteAnchor(alchemyPoint))
        {
            if (!hasCachedAlchemyRouteAnchor)
            {
                CacheAlchemyRouteAnchor();
            }

            if (hasCachedAlchemyRouteAnchor)
            {
                targetPosition = cachedAlchemyRouteAnchorPosition;
                targetZone = cachedAlchemyRouteAnchorZone;
                return true;
            }
        }

        targetPosition = alchemyPoint.position;
        targetZone =
            ResolveZoneForTransform(alchemyPoint) ??
            ResolveZoneForPosition(targetPosition);
        return true;
    }

    bool ShouldUseStaticAlchemyRouteAnchor(Transform alchemyPoint)
    {
        return alchemyPoint != null &&
            (alchemyPoint == transform ||
             alchemyPoint.IsChildOf(transform));
    }

    Vector3 GetSafeAlchemyPosition(Vector3 preferredPosition)
    {
        if (IsAlchemyPositionClear(preferredPosition))
        {
            return preferredPosition;
        }

        for (int radiusStep = 0; radiusStep < 10; radiusStep++)
        {
            float radius = 0.65f + radiusStep * 0.22f;
            for (int angleStep = 0; angleStep < 20; angleStep++)
            {
                float angle =
                    (angleStep / 20f) * Mathf.PI * 2f +
                    radiusStep * 0.19f;
                Vector3 candidate =
                    preferredPosition +
                    new Vector3(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle),
                        0f) * radius;
                candidate.z = preferredPosition.z;
                if (IsAlchemyPositionClear(candidate))
                {
                    return candidate;
                }
            }
        }

        return preferredPosition;
    }

    bool IsAlchemyPositionClear(Vector3 position)
    {
        float clearanceRadius =
            Mathf.Max(
                0.6f,
                GetApproachClearanceRadius() + 0.2f);
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(position, clearanceRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                hit.isTrigger ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<VillagerAI>() != null ||
                hit.GetComponentInParent<SmartNpcAI>() != null ||
                hit.GetComponentInParent<NpcMapMover2D>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    Transform GetMarketPoint()
    {
        if (marketPointOverride != null)
        {
            return marketPointOverride;
        }

        return villager != null
            ? villager.marketPoint
            : null;
    }

    void CacheReferences()
    {
        villager = villager != null
            ? villager
            : GetComponent<VillagerAI>();
        inventory = inventory != null
            ? inventory
            : GetComponent<ItemInventory>();
        schedule = schedule != null
            ? schedule
            : GetComponent<NpcScheduleController>();
        alchemyAgent = alchemyAgent != null
            ? alchemyAgent
            : GetComponent<NpcAlchemyAgent>();
        tradeAgent = tradeAgent != null
            ? tradeAgent
            : GetComponent<NpcTradeAgent>();
    }

    bool HasConfiguredMaterialRequirements()
    {
        return materialRequirements != null &&
            materialRequirements.Count > 0 &&
            refinedItem != null;
    }

    void EnsureDynamicAlchemyBatchPlan()
    {
        if (!autoPlanLowGradeBatches)
        {
            return;
        }

        if (HasConfiguredMaterialRequirements() &&
            IsCurrentAlchemyBatchCompatibleWithProfessionGrade() &&
            GetExpectedAlchemySaleValue(refinedItem) >=
            GetEstimatedMaterialBudget() + Mathf.Max(0, refiningLaborFee))
        {
            return;
        }

        if (!IsCurrentAlchemyBatchCompatibleWithProfessionGrade())
        {
            ResetDynamicAlchemyBatchPlan();
        }

        TryGenerateDynamicAlchemyBatchPlan();
    }

    bool TryGenerateDynamicAlchemyBatchPlan()
    {
        if (!TryFindDynamicAlchemyPlanningShop(out SimpleItemShop shop))
        {
            return false;
        }

        return TryGenerateDynamicAlchemyBatchPlan(shop);
    }

    bool TryGenerateDynamicAlchemyBatchPlan(SimpleItemShop shop)
    {
        for (ItemGrade grade = GetTargetAlchemyGrade();
            ;
            grade = GetLowerAlchemyGrade(grade))
        {
            if (TryGenerateDynamicAlchemyBatchPlan(
                    shop,
                    grade))
            {
                return true;
            }

            if (grade == ItemGrade.Ha)
            {
                break;
            }
        }

        return false;
    }

    bool TryGenerateDynamicAlchemyBatchPlan(
        SimpleItemShop shop,
        ItemGrade planGrade)
    {
        if (shop == null ||
            alchemyAgent == null ||
            alchemyAgent.alchemyCatalogItems == null ||
            alchemyAgent.alchemyCatalogItems.Count == 0)
        {
            return false;
        }

        System.Collections.Generic.List<StatItemData> materialPool =
            BuildDynamicAlchemyMaterialPool(
                shop,
                planGrade);
        PrependOwnedDynamicAlchemyMaterials(
            materialPool,
            planGrade);
        if (materialPool.Count < 2)
        {
            materialPool =
                BuildDynamicAlchemyMaterialPoolFromAllShops(
                    planGrade);
            PrependOwnedDynamicAlchemyMaterials(
                materialPool,
                planGrade);
        }

        if (materialPool.Count < 2)
        {
            materialPool =
                BuildDynamicAlchemyMaterialPoolFromKnownItems(
                    planGrade);
            PrependOwnedDynamicAlchemyMaterials(
                materialPool,
                planGrade);
        }

        if (materialPool.Count < 2)
        {
            return false;
        }

        System.Collections.Generic.List<FixedAlchemistMaterialRequirement>
            plannedRequirements =
                new System.Collections.Generic.List<FixedAlchemistMaterialRequirement>();
        int totalCost = 0;

        StatItemData primaryHerb =
            ChoosePrimaryHerbMaterial(materialPool);
        if (!TryAddDynamicAlchemyRequirement(
                plannedRequirements,
                shop,
                primaryHerb,
                ref totalCost))
        {
            return false;
        }

        StatItemData catalystMaterial =
            ChooseCatalystMaterial(
                materialPool,
                primaryHerb);
        if (!TryAddDynamicAlchemyRequirement(
                plannedRequirements,
                shop,
                catalystMaterial,
                ref totalCost))
        {
            return false;
        }

        int minimumSaleValue =
            totalCost + Mathf.Max(0, refiningLaborFee);
        System.Collections.Generic.List<StatItemData> resultCandidates =
            new System.Collections.Generic.List<StatItemData>();

        for (int i = 0; i < alchemyAgent.alchemyCatalogItems.Count; i++)
        {
            StatItemData candidate =
                alchemyAgent.alchemyCatalogItems[i];
            if (candidate == null ||
                candidate.itemType != ItemType.DanDuoc ||
                candidate.grade != planGrade ||
                !candidate.canBeSold)
            {
                continue;
            }

            if (GetExpectedAlchemySaleValue(candidate) < minimumSaleValue)
            {
                continue;
            }

            resultCandidates.Add(candidate);
        }

        if (resultCandidates.Count == 0)
        {
            for (int i = 0; i < alchemyAgent.alchemyCatalogItems.Count; i++)
            {
                StatItemData candidate =
                    alchemyAgent.alchemyCatalogItems[i];
                if (candidate == null ||
                    candidate.itemType != ItemType.DanDuoc ||
                    candidate.grade != planGrade ||
                    !candidate.canBeSold)
                {
                    continue;
                }

                resultCandidates.Add(candidate);
            }
        }

        if (resultCandidates.Count == 0)
        {
            return false;
        }

        refinedItem =
            resultCandidates[
                Random.Range(0, resultCandidates.Count)];
        refinedItemAmount = 1;
        materialRequirements = plannedRequirements;
        materialCost = totalCost;
        salePrice = Mathf.Max(
            minimumSaleValue,
            GetExpectedAlchemySaleValue(refinedItem));
        lastBatchMaterialBudget = totalCost;
        lastBatchMinimumSaleValue = minimumSaleValue;
        return true;
    }

    bool TryFindDynamicAlchemyPlanningShop(out SimpleItemShop shop)
    {
        shop = null;

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude);
        int bestSupportScore = -1;
        int bestMaterialCount = -1;
        float bestDistance = float.PositiveInfinity;
        Transform marketPoint = GetMarketPoint();
        Vector3 searchOrigin =
            marketPoint != null
                ? marketPoint.position
                : transform.position;

        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveShopZone(candidate);
            if (candidateZone.HasValue &&
                candidateZone.Value != preferredTradeZone)
            {
                continue;
            }

            int supportScore =
                GetDynamicAlchemyShopSupportScore(
                    candidate,
                    out int materialCount);
            if (supportScore < 0)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    searchOrigin,
                    candidate.transform.position);
            if (supportScore < bestSupportScore)
            {
                continue;
            }

            if (supportScore == bestSupportScore &&
                materialCount < bestMaterialCount)
            {
                continue;
            }

            if (supportScore == bestSupportScore &&
                materialCount == bestMaterialCount &&
                distance >= bestDistance)
            {
                continue;
            }

            bestSupportScore = supportScore;
            bestMaterialCount = materialCount;
            bestDistance = distance;
            shop = candidate;
        }

        if (shop != null)
        {
            FinalizePreferredTradeShop(shop);
            return true;
        }

        return TryFindPreferredTradeShop(out shop);
    }

    int GetDynamicAlchemyShopSupportScore(
        SimpleItemShop shop,
        out int materialCount)
    {
        materialCount = 0;

        if (shop == null ||
            !shop.isActiveAndEnabled)
        {
            return -1;
        }

        for (ItemGrade grade = GetTargetAlchemyGrade();
            ;
            grade = GetLowerAlchemyGrade(grade))
        {
            System.Collections.Generic.List<StatItemData> materialPool =
                BuildDynamicAlchemyMaterialPool(
                    shop,
                    grade);
            materialCount = materialPool.Count;
            if (materialCount >= 2)
            {
                bool hasHerb = false;
                bool hasCatalyst = false;

                for (int i = 0; i < materialPool.Count; i++)
                {
                    StatItemData item = materialPool[i];
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.materialKind == MaterialKind.Herb)
                    {
                        hasHerb = true;
                    }
                    else
                    {
                        hasCatalyst = true;
                    }
                }

                if (hasHerb)
                {
                    return hasCatalyst ? 2 : 1;
                }
            }

            if (grade == ItemGrade.Ha)
            {
                break;
            }
        }

        materialCount = 0;
        return -1;
    }

    System.Collections.Generic.List<StatItemData>
        BuildDynamicAlchemyMaterialPool(
            SimpleItemShop shop,
            ItemGrade grade)
    {
        System.Collections.Generic.List<StatItemData> materialPool =
            new System.Collections.Generic.List<StatItemData>();
        if (shop == null ||
            shop.items == null)
        {
            return materialPool;
        }

        RefreshTradeShopStock(shop);

        for (int i = 0; i < shop.items.Count; i++)
        {
            ShopItemSlot slot = shop.items[i];
            if (slot == null ||
                slot.item == null ||
                !IsValidAlchemyMaterialForGrade(slot.item, grade) ||
                materialPool.Contains(slot.item))
            {
                continue;
            }

            materialPool.Add(slot.item);
        }

        ShuffleItemList(materialPool);
        return materialPool;
    }

    void PrependOwnedDynamicAlchemyMaterials(
        System.Collections.Generic.List<StatItemData> materialPool,
        ItemGrade grade)
    {
        if (materialPool == null ||
            inventory == null)
        {
            return;
        }

        System.Collections.Generic.List<StatItemData> ownedMaterials =
            new System.Collections.Generic.List<StatItemData>();

        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack = inventory.items[i];
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !IsValidAlchemyMaterialForGrade(
                    stack.item,
                    grade))
            {
                continue;
            }

            if (ownedMaterials.Contains(stack.item))
            {
                continue;
            }

            ownedMaterials.Add(stack.item);
        }

        if (ownedMaterials.Count <= 0)
        {
            return;
        }

        ShuffleItemList(ownedMaterials);

        for (int i = ownedMaterials.Count - 1; i >= 0; i--)
        {
            StatItemData item = ownedMaterials[i];
            materialPool.Remove(item);
            materialPool.Insert(0, item);
        }
    }

    System.Collections.Generic.List<StatItemData>
        BuildDynamicAlchemyMaterialPoolFromAllShops(
            ItemGrade grade)
    {
        System.Collections.Generic.List<StatItemData> materialPool =
            new System.Collections.Generic.List<StatItemData>();
        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude);

        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop shop = shops[i];
            if (shop == null ||
                !shop.isActiveAndEnabled)
            {
                continue;
            }

            NpcMapZone? shopZone =
                ResolveShopZone(shop);
            if (shopZone.HasValue &&
                shopZone.Value != preferredTradeZone)
            {
                continue;
            }

            RefreshTradeShopStock(shop);

            for (int slotIndex = 0; slotIndex < shop.items.Count; slotIndex++)
            {
                ShopItemSlot slot = shop.items[slotIndex];
                if (slot == null ||
                    slot.item == null ||
                    !IsValidAlchemyMaterialForGrade(slot.item, grade) ||
                    materialPool.Contains(slot.item))
                {
                    continue;
                }

                materialPool.Add(slot.item);
            }
        }

        ShuffleItemList(materialPool);
        return materialPool;
    }

    System.Collections.Generic.List<StatItemData>
        BuildDynamicAlchemyMaterialPoolFromKnownItems(
            ItemGrade grade)
    {
        System.Collections.Generic.List<StatItemData> materialPool =
            new System.Collections.Generic.List<StatItemData>();
        StatItemData[] loadedItems =
            Resources.FindObjectsOfTypeAll<StatItemData>();

        for (int i = 0; i < loadedItems.Length; i++)
        {
            StatItemData item = loadedItems[i];
            if (item == null ||
                !IsValidAlchemyMaterialForGrade(item, grade) ||
                materialPool.Contains(item))
            {
                continue;
            }

            materialPool.Add(item);
        }

        ShuffleItemList(materialPool);
        return materialPool;
    }

    StatItemData ChoosePrimaryHerbMaterial(
        System.Collections.Generic.List<StatItemData> materialPool)
    {
        if (materialPool == null)
        {
            return null;
        }

        for (int i = 0; i < materialPool.Count; i++)
        {
            StatItemData item = materialPool[i];
            if (item != null &&
                item.materialKind == MaterialKind.Herb)
            {
                return item;
            }
        }

        return null;
    }

    StatItemData ChooseCatalystMaterial(
        System.Collections.Generic.List<StatItemData> materialPool,
        StatItemData primaryHerb)
    {
        StatItemData candidate =
            FindDynamicAlchemyCandidate(
                materialPool,
                primaryHerb,
                item => item.materialKind != MaterialKind.Herb &&
                    item.materialKind != MaterialKind.None);
        if (candidate != null)
        {
            return candidate;
        }

        candidate =
            FindDynamicAlchemyCandidate(
                materialPool,
                primaryHerb,
                item => item.materialKind != MaterialKind.Herb);
        if (candidate != null)
        {
            return candidate;
        }

        candidate =
            FindDynamicAlchemyCandidate(
                materialPool,
                primaryHerb,
                item => item.materialKind == MaterialKind.Herb);
        if (candidate != null)
        {
            return candidate;
        }

        return FindDynamicAlchemyCandidate(
            materialPool,
            primaryHerb,
            item => true);
    }

    StatItemData ChooseExtraDynamicMaterial(
        System.Collections.Generic.List<StatItemData> materialPool,
        System.Collections.Generic.List<FixedAlchemistMaterialRequirement>
            plannedRequirements)
    {
        if (materialPool == null)
        {
            return null;
        }

        for (int i = 0; i < materialPool.Count; i++)
        {
            StatItemData item = materialPool[i];
            if (item == null ||
                ContainsDynamicRequirement(
                    plannedRequirements,
                    item))
            {
                continue;
            }

            return item;
        }

        return null;
    }

    StatItemData FindDynamicAlchemyCandidate(
        System.Collections.Generic.List<StatItemData> materialPool,
        StatItemData excludedItem,
        System.Predicate<StatItemData> predicate)
    {
        if (materialPool == null)
        {
            return null;
        }

        for (int i = 0; i < materialPool.Count; i++)
        {
            StatItemData item = materialPool[i];
            if (item == null ||
                item == excludedItem)
            {
                continue;
            }

            if (predicate == null ||
                predicate(item))
            {
                return item;
            }
        }

        return null;
    }

    bool TryAddDynamicAlchemyRequirement(
        System.Collections.Generic.List<FixedAlchemistMaterialRequirement>
            plannedRequirements,
        SimpleItemShop shop,
        StatItemData item,
        ref int totalCost)
    {
        if (plannedRequirements == null ||
            shop == null ||
            item == null ||
            ContainsDynamicRequirement(
                plannedRequirements,
                item))
        {
            return false;
        }

        int unitPrice = shop.GetNpcBuyPrice(item, gameObject);
        if (unitPrice <= 0)
        {
            unitPrice =
                Mathf.Max(
                    1,
                    NpcEconomy.GetTradePrice(
                        item,
                        NpcTradeContext.MarketBuy));
        }

        plannedRequirements.Add(
            new FixedAlchemistMaterialRequirement
            {
                item = item,
                amount = 1
            });
        totalCost += unitPrice;
        return true;
    }

    bool ContainsDynamicRequirement(
        System.Collections.Generic.List<FixedAlchemistMaterialRequirement>
            plannedRequirements,
        StatItemData item)
    {
        if (plannedRequirements == null ||
            item == null)
        {
            return false;
        }

        for (int i = 0; i < plannedRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                plannedRequirements[i];
            if (requirement != null &&
                requirement.item == item)
            {
                return true;
            }
        }

        return false;
    }

    bool NeedsMaterialsForNextBatch()
    {
        if (autoPlanLowGradeBatches)
        {
            return !HasConfiguredMaterialRequirements() ||
                !HasAllRequiredMaterials();
        }

        if (HasConfiguredMaterialRequirements())
        {
            return !HasAllRequiredMaterials();
        }

        return alchemyAgent != null &&
            alchemyAgent.NeedsMoreMaterials();
    }

    bool HasProductsReadyToSell()
    {
        if (inventory == null)
        {
            return false;
        }

        StatItemData sellItem;
        return TryGetSellableFinishedGoods(out sellItem);
    }

    bool TrySellFinishedGoodsToMarket(
        out string detail)
    {
        detail = "missingInventory";

        if (inventory == null)
        {
            return false;
        }

        if (!TryGetSellableFinishedGoods(
                out StatItemData sellItem))
        {
            detail = "noSellableGoods";
            return false;
        }

        bool soldAny = false;
        System.Collections.Generic.List<string> saleDetails =
            new System.Collections.Generic.List<string>();
        SimpleItemShop shop;
        bool hasShop =
            TryFindPreferredSellTradeShop(out shop) &&
            shop != null;
        if (hasShop)
        {
            RefreshTradeShopStock(shop);
        }

        int guard = 0;
        while (guard++ < 32 &&
            TryGetSellableFinishedGoods(out sellItem))
        {
            int availableAmount =
                inventory.GetAmount(sellItem);
            if (availableAmount <= 0)
            {
                continue;
            }

            int fallbackUnitPrice =
                GetExpectedAlchemySaleValue(sellItem);
            if (sellItem == refinedItem)
            {
                fallbackUnitPrice =
                    Mathf.Max(
                        fallbackUnitPrice,
                        salePrice);
            }

            int remainingAmount = availableAmount;
            if (hasShop)
            {
                if (shop.SellNpcItemFromInventory(
                        sellItem,
                        gameObject,
                        inventory,
                        remainingAmount,
                        out int soldAmount,
                        out int totalPrice) &&
                    soldAmount > 0)
                {
                    soldAny = true;
                    remainingAmount -= soldAmount;
                    saleDetails.Add(
                        "shop=" + shop.name +
                        " item=" + sellItem.itemName +
                        " amount=" + soldAmount +
                        " price=" + totalPrice);
                }

                fallbackUnitPrice =
                    Mathf.Max(
                        fallbackUnitPrice,
                        shop.GetSellPrice(sellItem));
            }

            if (remainingAmount <= 0)
            {
                continue;
            }

            if (!inventory.RemoveItem(
                    sellItem,
                    remainingAmount))
            {
                if (!soldAny)
                {
                    detail =
                        "removeFailed=" +
                        sellItem.itemName;
                    return false;
                }

                break;
            }

            int earnedMoney =
                Mathf.Max(
                    1,
                    fallbackUnitPrice > 0
                        ? fallbackUnitPrice
                        : salePrice) *
                remainingAmount;
            NpcEconomy.AddNpcMoney(
                gameObject,
                earnedMoney);
            soldAny = true;
            saleDetails.Add(
                "fallback item=" + sellItem.itemName +
                " amount=" + remainingAmount +
                " price=" + earnedMoney);
        }

        if (!soldAny)
        {
            detail = "noCompletedSale";
            return false;
        }

        if (villager != null)
        {
            villager.GainProfessionExpForJob(
                VillagerJob.Alchemist);
        }

        detail = string.Join("; ", saleDetails);
        return true;
    }

    bool TryGetSellableFinishedGoods(
        out StatItemData sellItem)
    {
        sellItem = null;

        if (inventory == null)
        {
            return false;
        }

        if (IsSellableFinishedGood(refinedItem) &&
            inventory.GetAmount(refinedItem) > 0)
        {
            sellItem = refinedItem;
            return true;
        }

        int bestValue = int.MinValue;
        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack = inventory.items[i];
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !IsSellableFinishedGood(stack.item))
            {
                continue;
            }

            int candidateValue =
                GetExpectedAlchemySaleValue(stack.item);
            if (candidateValue < bestValue)
            {
                continue;
            }

            bestValue = candidateValue;
            sellItem = stack.item;
        }

        return sellItem != null;
    }

    bool IsSellableFinishedGood(StatItemData item)
    {
        return item != null &&
            item.itemType == ItemType.DanDuoc;
    }

    bool HasAllRequiredMaterials()
    {
        if (!HasConfiguredMaterialRequirements() ||
            inventory == null)
        {
            return false;
        }

        for (int i = 0; i < materialRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                materialRequirements[i];
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            if (inventory.GetAmount(requirement.item) < requirement.amount)
            {
                return false;
            }
        }

        return true;
    }

    int GetEstimatedMaterialBudget()
    {
        if (!HasConfiguredMaterialRequirements())
        {
            return materialCost;
        }

        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop) ||
            shop == null)
        {
            return materialCost;
        }

        int total = 0;
        for (int i = 0; i < materialRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                materialRequirements[i];
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            int missing = GetMissingMaterialAmount(requirement);
            if (missing <= 0)
            {
                continue;
            }

            total +=
                Mathf.Max(
                    1,
                    shop.GetNpcBuyPrice(
                        requirement.item,
                        gameObject)) * missing;
        }

        return Mathf.Max(0, total);
    }

    int GetMissingMaterialAmount(
        FixedAlchemistMaterialRequirement requirement)
    {
        if (requirement == null ||
            requirement.item == null ||
            requirement.amount <= 0 ||
            inventory == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            requirement.amount - inventory.GetAmount(requirement.item));
    }

    bool TryPurchaseConfiguredMaterials(out string detail)
    {
        detail = "noRequirements";

        if (!HasConfiguredMaterialRequirements())
        {
            return true;
        }

        if (inventory == null)
        {
            detail = "missingInventory";
            return false;
        }

        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop) ||
            shop == null)
        {
            detail = "missingShop";
            return false;
        }

        System.Collections.Generic.List<string> purchases =
            new System.Collections.Generic.List<string>();

        for (int i = 0; i < materialRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                materialRequirements[i];
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            int missing = GetMissingMaterialAmount(requirement);
            if (missing <= 0)
            {
                continue;
            }

            int itemIndex = shop.FindItemIndex(requirement.item);
            int boughtAmount = 0;
            int totalPrice = 0;
            bool purchased = false;
            if (itemIndex >= 0)
            {
                purchased =
                    shop.BuyNpcItemToInventory(
                        itemIndex,
                        gameObject,
                        inventory,
                        missing,
                        out boughtAmount,
                        out totalPrice);
            }

            if (!purchased)
            {
                if (!shop.ProvisionNpcItemToInventory(
                        requirement.item,
                        gameObject,
                        inventory,
                        missing,
                        out boughtAmount,
                        out totalPrice))
                {
                    detail =
                        (itemIndex < 0
                            ? "missingStock="
                            : "buyFailed=") +
                        requirement.item.itemName;
                    return false;
                }

                purchases.Add(
                    requirement.item.itemName +
                    "x" + boughtAmount +
                    " price=" + totalPrice +
                    " source=provision");
                continue;
            }

            purchases.Add(
                requirement.item.itemName +
                "x" + boughtAmount +
                " price=" + totalPrice);
        }

        detail =
            purchases.Count > 0
                ? string.Join("; ", purchases)
                : "alreadyReady";
        return HasAllRequiredMaterials();
    }

    bool DidPurchaseAnyConfiguredMaterials(
        string detail)
    {
        return !string.IsNullOrWhiteSpace(detail) &&
            detail != "alreadyReady" &&
            detail != "noRequirements" &&
            !detail.StartsWith(
                "missingShop",
                System.StringComparison.OrdinalIgnoreCase) &&
            !detail.StartsWith(
                "missingInventory",
                System.StringComparison.OrdinalIgnoreCase) &&
            !detail.StartsWith(
                "missingStock=",
                System.StringComparison.OrdinalIgnoreCase) &&
            !detail.StartsWith(
                "buyFailed=",
                System.StringComparison.OrdinalIgnoreCase) &&
            !detail.StartsWith(
                "dynamicPlanUnavailable",
                System.StringComparison.OrdinalIgnoreCase);
    }

    bool NeedsDailyConsumableTradeToday()
    {
        if (!buyDailyConsumables)
        {
            return false;
        }

        EnsureDailyConsumableItemsResolved();
        if ((dailyRiceItem == null ||
            dailyRiceAmount <= 0) &&
            (GetDailyProteinItemForDay(
                GetCurrentDay()) == null ||
            dailyProteinAmount <= 0))
        {
            return false;
        }

        return lastDailyConsumableTradeDay !=
            GetCurrentDay();
    }

    void EnsureDailyConsumableItemsResolved()
    {
        if (dailyRiceItem == null)
        {
            dailyRiceItem =
                FindLoadedItemByNames(
                    "Linh_Me",
                    "Linh Mễ",
                    "Linh Me");
        }

        if (dailyFishItem == null)
        {
            dailyFishItem =
                FindLoadedItemByNames(
                    "ca",
                    "Cá",
                    "Ca");
        }

        if (dailyMeatItem == null)
        {
            dailyMeatItem =
                FindLoadedItemByNames(
                    "thit",
                    "Thịt",
                    "Thit");
        }
    }

    StatItemData FindLoadedItemByNames(
        params string[] names)
    {
        if (names == null ||
            names.Length <= 0)
        {
            return null;
        }

        StatItemData[] loadedItems =
            Resources.FindObjectsOfTypeAll<StatItemData>();
        for (int i = 0; i < loadedItems.Length; i++)
        {
            StatItemData candidate = loadedItems[i];
            if (candidate == null)
            {
                continue;
            }

            for (int nameIndex = 0;
                nameIndex < names.Length;
                nameIndex++)
            {
                string itemName = names[nameIndex];
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                if (string.Equals(
                        candidate.name,
                        itemName,
                        System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        candidate.itemName,
                        itemName,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    StatItemData GetDailyProteinItemForDay(int day)
    {
        EnsureDailyConsumableItemsResolved();
        return day % 2 == 0
            ? dailyFishItem != null
                ? dailyFishItem
                : dailyMeatItem
            : dailyMeatItem != null
                ? dailyMeatItem
                : dailyFishItem;
    }

    bool TryPurchaseDailyConsumables(out string detail)
    {
        detail = "disabled";

        if (!buyDailyConsumables)
        {
            return true;
        }

        EnsureDailyConsumableItemsResolved();
        int currentDay = GetCurrentDay();
        StatItemData proteinItem =
            GetDailyProteinItemForDay(currentDay);

        if ((dailyRiceItem == null ||
            dailyRiceAmount <= 0) &&
            (proteinItem == null ||
            dailyProteinAmount <= 0))
        {
            detail = "noDailyItems";
            return false;
        }

        if (inventory == null)
        {
            detail = "missingInventory";
            return false;
        }

        if (!TryFindPreferredTradeShop(out SimpleItemShop shop) ||
            shop == null)
        {
            detail = "missingShop";
            return false;
        }

        System.Collections.Generic.List<string> purchases =
            new System.Collections.Generic.List<string>();
        bool boughtAny = false;

        if (TryPurchaseDailyConsumableItem(
                shop,
                dailyRiceItem,
                dailyRiceAmount,
                purchases))
        {
            boughtAny = true;
        }

        if (TryPurchaseDailyConsumableItem(
                shop,
                proteinItem,
                dailyProteinAmount,
                purchases))
        {
            boughtAny = true;
        }

        detail =
            purchases.Count > 0
                ? string.Join("; ", purchases)
                : "dailyNoDeal";
        return boughtAny;
    }

    bool TryPurchaseDailyConsumableItem(
        SimpleItemShop shop,
        StatItemData item,
        int amount,
        System.Collections.Generic.List<string> purchases)
    {
        if (shop == null ||
            item == null ||
            amount <= 0 ||
            inventory == null)
        {
            return false;
        }

        int itemIndex = shop.FindItemIndex(item);
        int boughtAmount = 0;
        int totalPrice = 0;
        bool purchased = false;
        if (itemIndex >= 0)
        {
            purchased =
                shop.BuyNpcItemToInventory(
                    itemIndex,
                    gameObject,
                    inventory,
                    amount,
                    out boughtAmount,
                    out totalPrice);
        }

        if (!purchased)
        {
            purchased =
                shop.ProvisionNpcItemToInventory(
                    item,
                    gameObject,
                    inventory,
                    amount,
                    out boughtAmount,
                    out totalPrice);
        }

        if (!purchased ||
            boughtAmount <= 0)
        {
            return false;
        }

        purchases.Add(
            item.itemName +
            "x" + boughtAmount +
            " price=" + totalPrice);
        return true;
    }

    bool DoesShopCoverCurrentRequirements(
        SimpleItemShop shop)
    {
        if (shop == null ||
            !HasConfiguredMaterialRequirements())
        {
            return false;
        }

        RefreshTradeShopStock(shop);

        for (int i = 0; i < materialRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                materialRequirements[i];
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            int missing = GetMissingMaterialAmount(requirement);
            if (missing <= 0)
            {
                continue;
            }

            if (!CanShopFulfillAlchemyRequirement(
                    shop,
                    requirement))
            {
                return false;
            }
        }

        return true;
    }

    bool CanShopFulfillAlchemyRequirement(
        SimpleItemShop shop,
        FixedAlchemistMaterialRequirement requirement)
    {
        if (shop == null ||
            requirement == null ||
            requirement.item == null)
        {
            return false;
        }

        int itemIndex =
            shop.FindItemIndex(requirement.item);
        if (itemIndex >= 0)
        {
            return true;
        }

        return NpcEconomy.CanTradeNormally(
            requirement.item);
    }

    bool ConsumeConfiguredMaterials()
    {
        if (!HasAllRequiredMaterials())
        {
            return false;
        }

        for (int i = 0; i < materialRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                materialRequirements[i];
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            if (!inventory.RemoveItem(
                    requirement.item,
                    requirement.amount))
            {
                return false;
            }
        }

        return true;
    }

    bool TryFindPreferredTradeShop(out SimpleItemShop shop)
    {
        shop = null;
        lastTradeShopName = "none";

        Transform marketPoint = GetMarketPoint();
        if (marketPoint != null)
        {
            SimpleItemShop directShop =
                marketPoint.GetComponent<SimpleItemShop>();
            if (IsImmediatePreferredTradeShopCandidate(directShop))
            {
                shop = directShop;
                FinalizePreferredTradeShop(shop);
                return true;
            }

            SimpleItemShop parentShop =
                marketPoint.GetComponentInParent<SimpleItemShop>();
            if (IsImmediatePreferredTradeShopCandidate(parentShop))
            {
                shop = parentShop;
                FinalizePreferredTradeShop(shop);
                return true;
            }
        }

        if (TryFindPreferredBrokerBackedShop(out SimpleItemShop brokerShop) &&
            IsImmediatePreferredTradeShopCandidate(brokerShop))
        {
            shop = brokerShop;
            FinalizePreferredTradeShop(shop);
            return true;
        }

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude);
        int bestCoverage = -1;
        int bestCoveredUnits = -1;
        float bestDistance = float.PositiveInfinity;
        Vector3 searchOrigin =
            marketPoint != null
                ? marketPoint.position
                : transform.position;

        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveShopZone(candidate);
            if (candidateZone.HasValue &&
                candidateZone.Value != preferredTradeZone)
            {
                continue;
            }

            int coverage =
                GetTradeShopCoverageScore(
                    candidate,
                    out int coveredUnits);
            if (coverage < 0)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    searchOrigin,
                    candidate.transform.position);
            if (coverage < bestCoverage)
            {
                continue;
            }

            if (coverage == bestCoverage &&
                coveredUnits < bestCoveredUnits)
            {
                continue;
            }

            if (coverage == bestCoverage &&
                coveredUnits == bestCoveredUnits &&
                distance >= bestDistance)
            {
                continue;
            }

            bestCoverage = coverage;
            bestCoveredUnits = coveredUnits;
            bestDistance = distance;
            shop = candidate;
        }

        if (shop != null)
        {
            FinalizePreferredTradeShop(shop);
            return true;
        }

        return false;
    }

    bool TryFindPreferredSellTradeShop(out SimpleItemShop shop)
    {
        shop = null;
        lastTradeShopName = "none";

        Transform marketPoint = GetMarketPoint();
        if (marketPoint != null)
        {
            SimpleItemShop directShop =
                marketPoint.GetComponent<SimpleItemShop>();
            if (IsPreferredTradeShopCandidate(directShop))
            {
                shop = directShop;
                FinalizePreferredTradeShop(shop);
                return true;
            }

            SimpleItemShop parentShop =
                marketPoint.GetComponentInParent<SimpleItemShop>();
            if (IsPreferredTradeShopCandidate(parentShop))
            {
                shop = parentShop;
                FinalizePreferredTradeShop(shop);
                return true;
            }
        }

        if (TryFindPreferredBrokerBackedShop(out SimpleItemShop brokerShop) &&
            IsPreferredTradeShopCandidate(brokerShop))
        {
            shop = brokerShop;
            FinalizePreferredTradeShop(shop);
            return true;
        }

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude);
        bool bestHasBroker = false;
        float bestDistance = float.PositiveInfinity;
        Vector3 searchOrigin =
            marketPoint != null
                ? marketPoint.position
                : transform.position;

        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (!IsPreferredTradeShopCandidate(candidate))
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveShopZone(candidate);
            if (candidateZone.HasValue &&
                candidateZone.Value != preferredTradeZone)
            {
                continue;
            }

            bool hasBroker =
                candidate.GetComponent<NpcCounterBroker>() != null;
            float distance =
                Vector2.Distance(
                    searchOrigin,
                    candidate.transform.position);

            if (shop == null ||
                (hasBroker && !bestHasBroker) ||
                (hasBroker == bestHasBroker &&
                distance < bestDistance))
            {
                shop = candidate;
                bestHasBroker = hasBroker;
                bestDistance = distance;
            }
        }

        if (shop != null)
        {
            FinalizePreferredTradeShop(shop);
            return true;
        }

        return false;
    }

    bool IsPreferredTradeShopCandidate(
        SimpleItemShop shop)
    {
        return GetTradeShopCoverageScore(
                shop,
                out _) >= 0;
    }

    bool IsImmediatePreferredTradeShopCandidate(
        SimpleItemShop shop)
    {
        if (GetTradeShopCoverageScore(
                shop,
                out _) < 0)
        {
            return false;
        }

        return !RequiresConfiguredMaterialCoverage() ||
            DoesShopCoverCurrentRequirements(shop);
    }

    bool RequiresConfiguredMaterialCoverage()
    {
        if (!HasConfiguredMaterialRequirements())
        {
            return false;
        }

        for (int i = 0; i < materialRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                materialRequirements[i];
            if (GetMissingMaterialAmount(requirement) > 0)
            {
                return true;
            }
        }

        return false;
    }

    int GetTradeShopCoverageScore(
        SimpleItemShop shop,
        out int coveredUnits)
    {
        coveredUnits = 0;

        if (shop == null ||
            !shop.isActiveAndEnabled)
        {
            return -1;
        }

        RefreshTradeShopStock(shop);

        if (!HasConfiguredMaterialRequirements())
        {
            return 0;
        }

        int coveredKinds = 0;

        for (int i = 0; i < materialRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                materialRequirements[i];
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            int missing = GetMissingMaterialAmount(requirement);
            if (missing <= 0)
            {
                continue;
            }

            if (!CanShopFulfillAlchemyRequirement(
                    shop,
                    requirement))
            {
                continue;
            }

            coveredKinds++;
            coveredUnits += missing;
        }

        return coveredKinds;
    }

    void RefreshTradeShopStock(
        SimpleItemShop shop)
    {
        if (shop == null)
        {
            return;
        }

        shop.RefreshFromSellerInventory();
    }

    void FinalizePreferredTradeShop(
        SimpleItemShop shop)
    {
        if (shop == null)
        {
            return;
        }

        RefreshTradeShopStock(shop);
        lastTradeShopName = shop.name;
    }

    bool TryFindPreferredBrokerBackedShop(out SimpleItemShop shop)
    {
        shop = null;

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude);

        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            NpcCounterBroker candidateBroker =
                candidate.GetComponent<NpcCounterBroker>();
            if (candidateBroker == null ||
                !candidateBroker.isActiveAndEnabled ||
                !candidateBroker.receiveAllNpcRequests)
            {
                continue;
            }

            Vector3 targetPosition =
                candidateBroker.customerPoint != null
                    ? candidateBroker.customerPoint.position
                    : candidateBroker.CustomerPosition;
            float distance =
                Vector2.Distance(
                    transform.position,
                    targetPosition);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                shop = candidate;
            }
        }

        return shop != null;
    }

    NpcMapZone? ResolveShopZone(SimpleItemShop shop)
    {
        if (shop == null)
        {
            return null;
        }

        NpcCounterBroker broker =
            shop.GetComponent<NpcCounterBroker>();
        if (broker != null)
        {
            NpcMapZone? brokerZone =
                ResolveBrokerZone(broker);
            if (brokerZone.HasValue)
            {
                return brokerZone;
            }
        }

        if (shop.sellerObject != null)
        {
            NpcMapZone? sellerZone =
                ResolveZoneForTransform(
                    shop.sellerObject.transform);
            if (sellerZone.HasValue)
            {
                return sellerZone;
            }
        }

        if (shop.sellerInventory != null)
        {
            NpcMapZone? inventoryZone =
                ResolveZoneForTransform(
                    shop.sellerInventory.transform);
            if (inventoryZone.HasValue)
            {
                return inventoryZone;
            }
        }

        return ResolveZoneForTransform(shop.transform);
    }

    bool TryGetPreferredTradeDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        broker = null;

        SimpleItemShop shop;
        if (!(IsSellTradeState()
                ? TryFindPreferredSellTradeShop(out shop)
                : TryFindPreferredTradeShop(out shop)) ||
            shop == null)
        {
            lastTradeDestinationSource = "missingPreferredShop";
            return false;
        }

        broker = shop.GetComponent<NpcCounterBroker>();
        if (broker != null &&
            broker.customerPoint != null)
        {
            targetPosition =
                GetBrokerApproachPosition(broker);
            targetZone = ResolveBrokerZone(broker);
            isBrokerTarget = broker.receiveAllNpcRequests;
            lastTradeDestinationSource = "preferredShopBroker";
            return true;
        }

        targetPosition = shop.transform.position;
        targetZone = ResolveShopZone(shop);
        isBrokerTarget = false;
        broker = null;
        lastTradeDestinationSource = "preferredShopRoot";
        return true;
    }

    bool TryGetZoneFallbackDestination(
        NpcMapZone zone,
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        broker = null;
        isBrokerTarget = false;

        NpcMapArea area =
            NpcMapArea.FindNearestAreaInZone(
                zone,
                transform.position);
        if (area != null &&
            area.areaBounds != null)
        {
            targetPosition = area.areaBounds.bounds.center;
            targetZone = zone;
            lastTradeDestinationSource =
                "zoneFallback:" + zone;
            return true;
        }

        targetPosition = Vector3.zero;
        targetZone = null;
        lastTradeDestinationSource =
            "missingZoneFallback:" + zone;
        return false;
    }

    bool TryGetBrokerDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        broker = NpcCounterBroker.Active;

        if (broker == null ||
            !broker.receiveAllNpcRequests)
        {
            return false;
        }

        targetPosition = GetBrokerApproachPosition(broker);
        targetZone = ResolveBrokerZone(broker);
        isBrokerTarget = true;
        lastTradeDestinationSource = "activeBroker";
        lastTradeShopName = broker.name;
        return true;
    }

    bool TryFindBrokerInZone(
        NpcMapZone zone,
        out NpcCounterBroker broker)
    {
        broker = null;

        NpcCounterBroker[] brokers =
            FindObjectsByType<NpcCounterBroker>(
                FindObjectsInactive.Exclude);

        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < brokers.Length; i++)
        {
            NpcCounterBroker candidate = brokers[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.receiveAllNpcRequests)
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveBrokerZone(candidate);
            if (!candidateZone.HasValue ||
                candidateZone.Value != zone)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    transform.position,
                    candidate.CustomerPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                broker = candidate;
            }
        }

        if (broker != null)
        {
            return true;
        }

        NpcCounterBroker activeBroker = NpcCounterBroker.Active;
        if (activeBroker != null &&
            activeBroker.receiveAllNpcRequests)
        {
            NpcMapZone? activeZone =
                ResolveBrokerZone(activeBroker);
            if (activeZone.HasValue &&
                activeZone.Value == zone)
            {
                broker = activeBroker;
                return true;
            }
        }

        for (int i = 0; i < brokers.Length; i++)
        {
            NpcCounterBroker candidate = brokers[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.receiveAllNpcRequests)
            {
                continue;
            }

            broker = candidate;
            return true;
        }

        return false;
    }

    void SyncSleepVisibility()
    {
        if (villager == null)
        {
            return;
        }

        bool shouldRemainHidden =
            IsHideAtHomeScheduleActive() &&
            IsAtHomePoint();

        // Let VillagerAI hide the NPC when it actually completes the
        // return-home flow. This controller only clears stale hidden state.
        if (villager.IsHiddenAtHome &&
            !shouldRemainHidden)
        {
            villager.ForceHiddenAtHome(false);
        }
    }

    bool IsHideAtHomeScheduleActive()
    {
        if (useDedicatedRoutine)
        {
            return IsDedicatedRestWindow(GetCurrentClockHour());
        }

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        return schedule.CurrentActivity == NpcScheduleActivity.Sleep ||
            schedule.CurrentActivity == NpcScheduleActivity.ReturnHome;
    }

    bool IsAtHomePoint()
    {
        if (villager == null ||
            villager.homePoint == null)
        {
            return false;
        }

        return Vector2.Distance(
            transform.position,
            villager.homePoint.position) <=
            Mathf.Max(0.75f, villager.arriveDistance);
    }

    bool IsValidLowGradeAlchemyMaterial(StatItemData item)
    {
        return item != null &&
            item.grade == ItemGrade.Ha &&
            item.canBeRefinedIntoPill &&
            item.canBeSold &&
            item.itemType == ItemType.VatLieu;
    }

    bool IsValidAlchemyMaterialForTargetGrade(StatItemData item)
    {
        return IsValidAlchemyMaterialForGrade(
            item,
            GetTargetAlchemyGrade());
    }

    bool IsValidAlchemyMaterialForGrade(
        StatItemData item,
        ItemGrade grade)
    {
        return item != null &&
            item.grade == grade &&
            item.canBeRefinedIntoPill &&
            item.canBeSold &&
            item.itemType == ItemType.VatLieu;
    }

    ItemGrade GetTargetAlchemyGrade()
    {
        int level = GetCurrentProfessionSkillLevel();
        if (level < 3)
        {
            return ItemGrade.Ha;
        }

        if (level < 7)
        {
            return ItemGrade.Trung;
        }

        return ItemGrade.Thuong;
    }

    int GetCurrentProfessionSkillLevel()
    {
        int level =
            villager != null
                ? Mathf.Max(1, villager.professionLevel)
                : 1;

        NpcSpecialProfession specialProfession =
            GetComponent<NpcSpecialProfession>();
        if (specialProfession != null)
        {
            level = Mathf.Max(level, specialProfession.jobLevel);
        }

        return level;
    }

    bool IsCurrentAlchemyBatchCompatibleWithProfessionGrade()
    {
        if (!HasConfiguredMaterialRequirements() ||
            refinedItem == null)
        {
            return false;
        }

        ItemGrade targetGrade =
            GetTargetAlchemyGrade();
        if (refinedItem.itemType != ItemType.DanDuoc ||
            !IsAlchemyGradeAllowedForProfession(
                refinedItem.grade,
                targetGrade) ||
            !refinedItem.canBeSold)
        {
            return false;
        }

        for (int i = 0; i < materialRequirements.Count; i++)
        {
            FixedAlchemistMaterialRequirement requirement =
                materialRequirements[i];
            if (requirement == null ||
                requirement.item == null)
            {
                return false;
            }

            if (!IsValidAlchemyMaterialForTargetGrade(
                    requirement.item) &&
                !IsValidAlchemyMaterialForGrade(
                    requirement.item,
                    refinedItem.grade))
            {
                return false;
            }
        }

        return true;
    }

    bool IsAlchemyGradeAllowedForProfession(
        ItemGrade batchGrade,
        ItemGrade targetGrade)
    {
        return GetAlchemyGradeRank(batchGrade) <=
            GetAlchemyGradeRank(targetGrade);
    }

    ItemGrade GetLowerAlchemyGrade(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Thuong:
                return ItemGrade.Trung;

            case ItemGrade.Trung:
                return ItemGrade.Ha;

            default:
                return ItemGrade.Ha;
        }
    }

    int GetAlchemyGradeRank(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Thuong:
                return 3;

            case ItemGrade.Trung:
                return 2;

            default:
                return 1;
        }
    }

    void ResetDynamicAlchemyBatchPlan()
    {
        refinedItem = null;
        refinedItemAmount = 1;
        materialRequirements =
            new System.Collections.Generic.List<FixedAlchemistMaterialRequirement>();
        materialCost = 0;
        salePrice = 0;
        lastBatchMaterialBudget = 0;
        lastBatchMinimumSaleValue = 0;
    }

    int GetExpectedAlchemySaleValue(StatItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        return Mathf.Max(
            1,
            NpcEconomy.GetTradePrice(
                item,
                NpcTradeContext.MarketSell));
    }

    void ConfigureCurrentBatchRefineDuration(
        StatItemData batchResult)
    {
        if (alchemyAgent == null)
        {
            return;
        }

        ItemGrade grade =
            batchResult != null &&
            batchResult.itemType == ItemType.DanDuoc
                ? batchResult.grade
                : GetTargetAlchemyGrade();
        float refineHours =
            GetRefineDurationHoursForGrade(grade);
        alchemyAgent.refineDurationMinGameHours =
            refineHours;
        alchemyAgent.refineDurationMaxGameHours =
            refineHours;
    }

    float GetRefineDurationHoursForGrade(
        ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return 20f;

            case ItemGrade.Thuong:
                return 40f;

            case ItemGrade.Ha:
            default:
                return 10f;
        }
    }

    string GetRefiningProgressAction()
    {
        if (alchemyAgent == null ||
            !alchemyAgent.IsRefining)
        {
            return RefineAction;
        }

        int totalHours =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    alchemyAgent.CurrentRefineTotalGameHours));
        int elapsedHours =
            Mathf.Clamp(
                alchemyAgent.CurrentRefineDisplayedElapsedHours,
                0,
                totalHours);
        return NpcText.ActionFormat(
            "fixedBlacksmithForgingProgress",
            RefineAction,
            elapsedHours,
            totalHours);
    }

    static void ShuffleItemList<T>(System.Collections.Generic.List<T> items)
    {
        if (items == null)
        {
            return;
        }

        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T temp = items[i];
            items[i] = items[swapIndex];
            items[swapIndex] = temp;
        }
    }

    void ApplyRecommendedSetup()
    {
        CacheReferences();
        EnsureRecommendedScheduleConfigured();

        if (villager != null)
        {
            if (Application.isPlaying)
            {
                villager.EnsureHomePointResolved();
            }

            villager.job = VillagerJob.Alchemist;
            villager.keepInspectorJob = true;
            villager.hideAtHome = true;
            villager.homeRoutineManagedExternally = false;
            villager.autonomousWorkEnabled = true;
            villager.dailyRoutineEnabled = false;
            villager.dailyTaskPlanEnabled = false;
        }

        if (tradeAgent != null)
        {
            tradeAgent.inventory = inventory;
            tradeAgent.tradeChance = 0;
            tradeAgent.buyUsefulItemsFromMarketTrader = false;
            tradeAgent.buyProduceFromVillagers = false;
        }

        if (alchemyAgent != null &&
            disableLegacyAlchemyAutomation)
        {
            alchemyAgent.inventory = inventory;
            alchemyAgent.tradeAgent = tradeAgent;
            alchemyAgent.autoAlchemy = false;
            alchemyAgent.autoSellFinishedGoods = false;
            alchemyAgent.autoBuyMaterialsFromMarketTraders = false;
            alchemyAgent.preferCultivateWhenIdle = false;
            alchemyAgent.snapToAlchemyStandPoint = false;
            alchemyAgent.keepAtAlchemyStandPoint = false;
            if (alchemyPointOverride != null)
            {
                alchemyAgent.alchemyStandPoint = alchemyPointOverride;
            }
        }

        NpcAlchemyRole alchemyRole =
            GetComponent<NpcAlchemyRole>();
        if (alchemyRole != null)
        {
            alchemyRole.forceVillagerJobProfession = true;
            alchemyRole.professionName = professionName;
            if (disableLegacyAlchemyAutomation)
            {
                alchemyRole.configureTradeAgent = false;
                alchemyRole.configureAlchemyAgent = false;
                alchemyRole.autoBuyMaterialsFromMarketTraders = false;
                alchemyRole.autoAlchemy = false;
                alchemyRole.preferCultivateWhenIdle = false;
                alchemyRole.sellToVanBaoLau = true;
            }

            if (alchemyPointOverride != null)
            {
                alchemyRole.alchemyStandPoint = alchemyPointOverride;
            }
        }

        NpcSpecialProfession specialProfession =
            GetComponent<NpcSpecialProfession>();
        if (specialProfession != null)
        {
            specialProfession.professionName = professionName;
            specialProfession.lockVillagerJob = true;
            specialProfession.villagerJob = VillagerJob.Alchemist;
        }
    }

    void EnsureStartingMoney()
    {
        if (startingMoney <= 0)
        {
            return;
        }

        int currentMoney =
            NpcEconomy.GetNpcMoney(gameObject);
        int walletAmount = Mathf.Max(currentMoney, startingMoney);
        SyncProfileWallet(walletAmount);
    }

    void SyncProfileWallet(int walletAmount)
    {
        NpcEconomy.SetDedicatedProfessionWalletAmount(
            gameObject,
            walletAmount);
    }

    void EnsureRecommendedScheduleConfigured()
    {
        if (schedule == null)
        {
            return;
        }

        schedule.enforceSchedule = true;
        schedule.autoBuildDefaultSchedule = false;
        schedule.lifePath = NpcLifePath.Commoner;
        schedule.canCultivate = false;

        if (HasRecommendedSchedule())
        {
            return;
        }

        schedule.slots = BuildRecommendedSlots();
    }

    bool HasRecommendedSchedule()
    {
        return schedule != null &&
            schedule.slots != null &&
            schedule.slots.Count == 6;
    }

    System.Collections.Generic.List<NpcScheduleSlot> BuildRecommendedSlots()
    {
        return new System.Collections.Generic.List<NpcScheduleSlot>
        {
            CreateSlot(NpcScheduleActivity.Sleep, sleepStart, sleepEnd),
            CreateSlot(NpcScheduleActivity.ReturnHome, sleepEnd, tradeStart),
            CreateSlot(NpcScheduleActivity.TradeBuySell, tradeStart, morningWorkStart),
            CreateSlot(NpcScheduleActivity.Work, morningWorkStart, morningWorkEnd),
            CreateSlot(NpcScheduleActivity.ReturnHome, morningWorkEnd, afternoonWorkStart),
            CreateSlot(NpcScheduleActivity.Work, afternoonWorkStart, afternoonWorkEnd)
        };
    }

    static NpcScheduleSlot CreateSlot(
        NpcScheduleActivity activity,
        float startHour,
        float endHour)
    {
        return new NpcScheduleSlot
        {
            activity = activity,
            startHour = startHour,
            endHour = endHour,
            allowDangerInterrupt = true,
            allowHungerInterrupt = true,
            allowFatigueInterrupt = true,
            allowSocialInterrupt = false
        };
    }

    bool HasActionFinished(float durationSeconds)
    {
        return stateStartedAtRealtime >= 0f &&
            Time.time - stateStartedAtRealtime >=
            Mathf.Max(0.01f, durationSeconds);
    }

    bool IsNear(Vector3 targetPosition)
    {
        return Vector2.Distance(transform.position, targetPosition) <=
            GetArrivalDistance();
    }

    float GetArrivalDistance()
    {
        return villager != null
            ? Mathf.Max(0.12f, villager.arriveDistance)
            : 0.25f;
    }

    bool IsDedicatedRestWindow(float hour)
    {
        float startHour = Mathf.Repeat(sleepStart, 24f);
        float endHour = Mathf.Repeat(sleepEnd, 24f);

        if (startHour == endHour)
        {
            return false;
        }

        if (startHour < endHour)
        {
            return hour >= startHour &&
                hour < endHour;
        }

        return hour >= startHour ||
            hour < endHour;
    }

    bool CanLeaveForProductionTrade()
    {
        return !IsDedicatedRestWindow(GetCurrentClockHour());
    }

    bool CanPerformAlchemyWorkNow()
    {
        if (IsDedicatedRestWindow(GetCurrentClockHour()))
        {
            return false;
        }

        if (NpcScheduleController.AllowsAlchemy(gameObject))
        {
            return true;
        }

        NpcScheduleController activeSchedule =
            schedule != null
                ? schedule
                : NpcScheduleController.GetSchedule(gameObject);
        return activeSchedule == null ||
            !activeSchedule.enforceSchedule ||
            activeSchedule.CurrentActivity == NpcScheduleActivity.Work;
    }

    float GetCurrentClockHour()
    {
        return WorldTimeSystem.Instance != null
            ? WorldTimeSystem.Instance.CurrentHour
            : 12f;
    }

    int GetCurrentDay()
    {
        return WorldTimeSystem.Instance != null
            ? WorldTimeSystem.Instance.CurrentDay
            : 0;
    }

    NpcMapZone? ResolveZoneForTransform(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        NpcMapZone? destinationZone =
            NpcMapNavigator.GetDestinationZone(target);
        if (destinationZone.HasValue)
        {
            return destinationZone;
        }

        NpcMapArea area = NpcMapArea.FindArea(target.position);
        if (area != null)
        {
            return area.zone;
        }

        area = NpcMapArea.FindNearestArea(target.position);
        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    NpcMapZone? ResolveBrokerZone(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return null;
        }

        if (broker.customerPoint != null)
        {
            NpcMapZone? zone =
                ResolveZoneForTransform(broker.customerPoint);
            if (zone.HasValue)
            {
                return zone;
            }
        }

        return NpcMapNavigator.ResolveActorZone(broker.gameObject);
    }
}
