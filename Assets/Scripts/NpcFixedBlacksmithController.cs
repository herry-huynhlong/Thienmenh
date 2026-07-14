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
public class NpcFixedBlacksmithController : MonoBehaviour
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

    [Header("Production")]
    [Min(1)] public int craftDays = 3;
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
    public bool debugLogs;

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

    float RequiredWorkHours =>
        Mathf.Max(1, craftDays) * Mathf.Max(1f, workHoursPerDay);

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

        if (disableLegacyForgeComponents)
        {
            DisableLegacySystems();
        }
    }

    void Start()
    {
        CacheReferences();
        EnsureRecommendedScheduleConfigured();

        if (grantStartingMoneyOnStart)
        {
            EnsureStartingMoney();
        }

        if (lastProgressWorldHour < 0f)
        {
            lastProgressWorldHour = GetCurrentWorldHour();
        }

        SyncSleepVisibility();
    }

    void Update()
    {
        SyncSleepVisibility();
    }

    [ContextMenu("Blacksmith/Debug Current Trade Route")]
    public void DebugCurrentTradeRoute()
    {
        CacheReferences();

        if (villager == null)
        {
            Debug.LogWarning(
                "[NpcFixedBlacksmith] missing VillagerAI for " + name,
                this);
            return;
        }

        if (!TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker broker))
        {
            Debug.LogWarning(
                "[NpcFixedBlacksmith] no buy destination for " + name,
                this);
            return;
        }

        Transform approachPoint = buyApproachPointOverride;
        Transform buyPoint = buyPointOverride;
        float probeRadius = Mathf.Max(0.12f, GetApproachClearanceRadius());

        string detail =
            "actorPos=" + transform.position +
            " actorZone=" + GetZoneText(GetCurrentZone()) +
            " state=" + state +
            " action=" + villager.currentAction +
            " buyApproachPoint=" +
            (approachPoint != null ? approachPoint.position.ToString() : "none") +
            " buyApproachFeasible=" +
            (approachPoint != null &&
                IsVillagerMoveTargetFeasible(approachPoint.position) ? 1 : 0) +
            " buyApproachClearLine=" +
            (approachPoint != null &&
                HasVillagerClearLineTo(approachPoint.position) ? 1 : 0) +
            " buyApproachHits=" +
            DescribeBlockingCollidersAtPoint(
                approachPoint != null ? approachPoint.position : transform.position,
                probeRadius) +
            " buyPoint=" +
            (buyPoint != null ? buyPoint.position.ToString() : "none") +
            " resolvedBuy=" + buyPosition +
            " sourceDetail=" + lastTradeDestinationDetail +
            " buyZone=" + GetZoneText(buyZone) +
            " isBrokerTarget=" + (isBrokerTarget ? 1 : 0) +
            " resolvedBuyFeasible=" +
            (IsVillagerMoveTargetFeasible(buyPosition) ? 1 : 0) +
            " resolvedBuyClearLine=" +
            (HasVillagerClearLineTo(buyPosition) ? 1 : 0) +
            " resolvedBuyHits=" +
            DescribeBlockingCollidersAtPoint(
                buyPosition,
                probeRadius) +
            " rayToApproach=" +
            DescribeRaycastBlockersTo(
                approachPoint != null ? approachPoint.position : buyPosition,
                probeRadius) +
            " rayToResolvedBuy=" +
            DescribeRaycastBlockersTo(
                buyPosition,
                probeRadius);

        if (broker != null)
        {
            BoxCollider2D customerZone = broker.GetCustomerZoneCollider();
            detail +=
                " broker=" + broker.name +
                " customerCenter=" + broker.CustomerPosition +
                " customerRadius=" + broker.CustomerServiceRadius.ToString("0.00");

            if (customerZone != null)
            {
                Bounds bounds = customerZone.bounds;
                detail +=
                    " customerZoneMin=" + bounds.min +
                    " customerZoneMax=" + bounds.max +
                    " approachInsideZone=" +
                    (approachPoint != null && customerZone.OverlapPoint(approachPoint.position) ? 1 : 0) +
                    " resolvedInsideZone=" +
                    (customerZone.OverlapPoint(buyPosition) ? 1 : 0);
            }
        }

        Debug.LogWarning(
            "[NpcFixedBlacksmith] " + name + " DebugCurrentTradeRoute " + detail,
            this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ClampConfig();
        CacheReferences();

        if (!Application.isPlaying &&
            autoConfigureVillager)
        {
            ApplyRecommendedSetup();
        }
    }
#endif

    [ContextMenu("Blacksmith/Apply Recommended Setup")]
    public void ApplyRecommendedSetup()
    {
        CacheReferences();
        ClampConfig();

        if (villager != null)
        {
            villager.job = VillagerJob.Blacksmith;
            villager.keepInspectorJob = true;
            villager.hideAtHome = true;
            villager.homeRoutineManagedExternally = false;
            villager.autonomousWorkEnabled = true;
            villager.dailyRoutineEnabled = false;
            villager.dailyTaskPlanEnabled = false;
        }

        NpcSpecialProfession specialProfession =
            GetComponent<NpcSpecialProfession>();
        if (specialProfession != null)
        {
            specialProfession.professionName = professionName;
            specialProfession.lockVillagerJob = true;
            specialProfession.villagerJob = VillagerJob.Blacksmith;
        }

        if (schedule != null)
        {
            schedule.enforceSchedule = true;
            schedule.autoBuildDefaultSchedule = false;
            schedule.lifePath = NpcLifePath.Commoner;
            schedule.canCultivate = false;
            schedule.slots = BuildRecommendedSlots();
        }
    }

    [ContextMenu("Blacksmith/Reset Cycle")]
    public void ResetCycle()
    {
        state = ForgeCycleState.NeedMaterials;
        forgedWorkHours = 0f;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        lastPurchaseDay = -1;
        lastSaleDay = -1;
    }

    public bool TryRunWorkCycle()
    {
        CacheReferences();
        EnsureRecommendedScheduleConfigured();

        if (villager == null ||
            !enabled ||
            !isActiveAndEnabled)
        {
            return false;
        }

        switch (state)
        {
            case ForgeCycleState.BuyingMaterials:
                return HandleBuyingMaterials();
            case ForgeCycleState.Forging:
                return HandleForging();
            case ForgeCycleState.ReadyToSell:
                return HandleReadyToSell();
            case ForgeCycleState.Selling:
                return HandleSelling();
            default:
                return HandleNeedMaterials();
        }
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
        selfColliders = selfColliders != null &&
            selfColliders.Length > 0
            ? selfColliders
            : GetComponentsInChildren<Collider2D>(true);
    }

    bool IsSelfCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.transform == transform ||
            hit.transform.IsChildOf(transform))
        {
            return true;
        }

        if (selfColliders == null ||
            selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < selfColliders.Length; i++)
        {
            if (selfColliders[i] == hit)
            {
                return true;
            }
        }

        return false;
    }

    void ClampConfig()
    {
        craftDays = Mathf.Max(1, craftDays);
        workHoursPerDay = Mathf.Clamp(workHoursPerDay, 1f, 24f);
        buyDurationSeconds = Mathf.Max(0.25f, buyDurationSeconds);
        sellDurationSeconds = Mathf.Max(0.25f, sellDurationSeconds);
        tradeStart = Mathf.Max(sleepEnd, tradeStart);
        morningWorkStart =
            Mathf.Max(tradeStart + 0.1f, morningWorkStart);
        morningWorkEnd = Mathf.Max(morningWorkStart + 0.1f, morningWorkEnd);
        afternoonWorkStart = Mathf.Max(morningWorkEnd + 0.1f, afternoonWorkStart);
        afternoonWorkEnd =
            Mathf.Max(afternoonWorkStart + 0.1f, afternoonWorkEnd);
    }

    List<NpcScheduleSlot> BuildRecommendedSlots()
    {
        return new List<NpcScheduleSlot>
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
        if (schedule == null ||
            schedule.slots == null ||
            schedule.slots.Count != 6)
        {
            return false;
        }

        return IsMatchingSlot(schedule.slots[0], NpcScheduleActivity.Sleep, sleepStart, sleepEnd) &&
            IsMatchingSlot(schedule.slots[1], NpcScheduleActivity.ReturnHome, sleepEnd, tradeStart) &&
            IsMatchingSlot(schedule.slots[2], NpcScheduleActivity.TradeBuySell, tradeStart, morningWorkStart) &&
            IsMatchingSlot(schedule.slots[3], NpcScheduleActivity.Work, morningWorkStart, morningWorkEnd) &&
            IsMatchingSlot(schedule.slots[4], NpcScheduleActivity.ReturnHome, morningWorkEnd, afternoonWorkStart) &&
            IsMatchingSlot(schedule.slots[5], NpcScheduleActivity.Work, afternoonWorkStart, afternoonWorkEnd);
    }

    static bool IsMatchingSlot(
        NpcScheduleSlot slot,
        NpcScheduleActivity activity,
        float startHour,
        float endHour)
    {
        return slot != null &&
            slot.activity == activity &&
            Mathf.Abs(slot.startHour - startHour) <= 0.01f &&
            Mathf.Abs(slot.endHour - endHour) <= 0.01f;
    }

    void DisableLegacySystems()
    {
        DisableComponent(GetComponent<NpcForgeAgent>());
        DisableComponent(GetComponent<NpcTradeAgent>());
        DisableComponent(GetComponent<NpcForgeRole>());
    }

    static void DisableComponent(Behaviour behaviour)
    {
        if (behaviour != null)
        {
            behaviour.enabled = false;
        }
    }

    void EnsureStartingMoney()
    {
        int currentMoney = NpcEconomy.GetNpcMoney(gameObject);
        if (currentMoney >= startingMoney)
        {
            return;
        }

        NpcEconomy.AddNpcMoney(gameObject, startingMoney - currentMoney);
        LogDebug("SeedMoney", "money=" + NpcEconomy.GetNpcMoney(gameObject));
    }

    bool HandleNeedMaterials()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (HasConfiguredMaterialRequirements() &&
            HasAllRequiredMaterials())
        {
            state = ForgeCycleState.Forging;
            forgedWorkHours = 0f;
            stateStartedAtRealtime = -1f;
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithMaterialsReady"),
                1f);
            LogDebug("NeedMaterials", "resumeForgingFromInventory=1");
            return true;
        }

        int minimumBudget =
            GetEstimatedMaterialBudget();

        if (minimumBudget > 0 &&
            NpcEconomy.GetNpcMoney(gameObject) < minimumBudget)
        {
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithNeedMoneyToBuy"),
                2f);
            return true;
        }

        if (!TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker buyBroker))
        {
            villager.SetActionImmediate(
                NpcText.ActionFormat(
                    "fixedBlacksmithMissingBuyPoint",
                    GetZoneLabel(preferredTradeZone)),
                Mathf.Max(1f, villager.thinkInterval));
            LogDebug(
                "NeedMaterials",
                "missingBuyDestination preferredZone=" + preferredTradeZone);
            return true;
        }

        bool arrivedAtBuyTarget =
            HasArrivedAtTradeDestination(
                buyPosition,
                isBrokerTarget,
                buyBroker);

        LogDebug(
            "BuyRoute",
            DescribeTradeTarget(
                buyPosition,
                buyZone,
                isBrokerTarget,
                buyBroker,
                arrivedAtBuyTarget));

        if (!arrivedAtBuyTarget)
        {
            LogDebug(
                "BuyMove",
                "forceMove=1 " +
                DescribeTradeTarget(
                    buyPosition,
                    buyZone,
                    isBrokerTarget,
                    buyBroker,
                    false));
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

    void BeginBuyingMaterials()
    {
        state = ForgeCycleState.BuyingMaterials;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate(
            NpcText.Action("fixedBlacksmithBuyingMaterials"),
            buyDurationSeconds);
        LogDebug("State", "BuyingMaterials");
    }

    bool HandleBuyingMaterials()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker buyBroker))
        {
            bool arrivedAtBuyTarget =
                HasArrivedAtTradeDestination(
                buyPosition,
                isBrokerTarget,
                buyBroker);

            LogDebug(
                "BuyingRoute",
                DescribeTradeTarget(
                    buyPosition,
                    buyZone,
                    isBrokerTarget,
                    buyBroker,
                    arrivedAtBuyTarget));

            if (!arrivedAtBuyTarget)
            {
                LogDebug(
                    "BuyingMove",
                    "forceMove=1 " +
                    DescribeTradeTarget(
                        buyPosition,
                        buyZone,
                        isBrokerTarget,
                        buyBroker,
                        false));
                MoveVillagerToTradeTarget(
                    buyPosition,
                    BuyAction,
                    buyZone,
                    isBrokerTarget);
                return true;
            }
        }

        if (!HasActionFinished(buyDurationSeconds))
        {
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithBuyingMaterials"),
                buyDurationSeconds);
            return true;
        }

        if (NpcEconomy.GetNpcMoney(gameObject) < materialCost)
        {
            if (!HasConfiguredMaterialRequirements())
            {
                state = ForgeCycleState.NeedMaterials;
                villager.SetActionImmediate(
                    NpcText.Action("fixedBlacksmithNotEnoughMoneyToBuy"),
                    2f);
                return true;
            }
        }

        string purchaseDetail = string.Empty;
        if (HasConfiguredMaterialRequirements())
        {
            if (!TryPurchaseConfiguredMaterials(out purchaseDetail))
            {
                state = ForgeCycleState.NeedMaterials;
                villager.SetActionImmediate(
                    NpcText.Action("fixedBlacksmithMaterialsPurchaseIncomplete"),
                    2f);
                LogDebug("BuyPending", purchaseDetail);
                return true;
            }
        }
        else
        {
            NpcEconomy.AddNpcMoney(gameObject, -materialCost);
            purchaseDetail =
                "fallbackMoney=" +
                NpcEconomy.GetNpcMoney(gameObject);
        }

        state = ForgeCycleState.Forging;
        forgedWorkHours = 0f;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        lastPurchaseDay = GetCurrentWorldDay();
        villager.SetActionImmediate(
            NpcText.Action("fixedBlacksmithBoughtMaterials"),
            1f);
        LogDebug(
            "BuyComplete",
            "money=" + NpcEconomy.GetNpcMoney(gameObject) +
            " materials=" + purchaseDetail);
        return true;
    }

    bool HandleForging()
    {
        Transform forgePoint = GetForgePoint();
        Vector3 forgePosition = forgePoint != null
            ? GetSafeForgePosition(forgePoint.position)
            : transform.position;
        float currentWorldHour = GetCurrentWorldHour();

        if (forgePoint != null &&
            !IsNear(forgePosition))
        {
            lastProgressWorldHour = currentWorldHour;
            villager.ForceJobMoveTo(
                forgePosition,
                ForgeAction,
                NpcMapNavigator.GetDestinationZone(forgePoint));
            return true;
        }

        if (lastProgressWorldHour < 0f)
        {
            lastProgressWorldHour = currentWorldHour;
        }

        float gainedWorkHours =
            CalculateWorkHoursBetween(lastProgressWorldHour, currentWorldHour);
        lastProgressWorldHour = currentWorldHour;

        if (gainedWorkHours > 0f)
        {
            forgedWorkHours =
                Mathf.Min(
                    RequiredWorkHours,
                    forgedWorkHours + gainedWorkHours);
        }

        if (forgedWorkHours >= RequiredWorkHours)
        {
            CompleteForging();
            return true;
        }

        villager.SetActionImmediate(
            NpcText.ActionFormat(
                "fixedBlacksmithForgingProgress",
                ForgeAction,
                Mathf.CeilToInt(forgedWorkHours),
                Mathf.CeilToInt(RequiredWorkHours)),
            Mathf.Max(1f, villager.thinkInterval));
        return true;
    }

    void CompleteForging()
    {
        string consumedDetail = "none";
        if (HasConfiguredMaterialRequirements() &&
            !TryConsumeConfiguredMaterials(out consumedDetail))
        {
            state = ForgeCycleState.NeedMaterials;
            forgedWorkHours = 0f;
            stateStartedAtRealtime = -1f;
            lastProgressWorldHour = GetCurrentWorldHour();
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithMissingMaterialsContinue"),
                2f);
            LogDebug("ForgeBlocked", consumedDetail);
            return;
        }

        if (inventory != null &&
            forgedItem != null)
        {
            inventory.AddItem(forgedItem, 1);
        }

        state = ForgeCycleState.ReadyToSell;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        villager.SetActionImmediate(
            NpcText.Action("fixedBlacksmithForgeComplete"),
            1f);
        LogDebug(
            "ForgeComplete",
            "cycles=" + completedCycles +
            " item=" + (forgedItem != null ? forgedItem.itemName : "null") +
            " consumed=" + consumedDetail);
    }

    bool HandleReadyToSell()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (!TryGetSellDestination(
                out Vector3 sellPosition,
                out NpcMapZone? sellZone,
                out bool isBrokerTarget,
                out NpcCounterBroker sellBroker))
        {
            villager.SetActionImmediate(
                NpcText.ActionFormat(
                    "fixedBlacksmithMissingSellPoint",
                    GetZoneLabel(preferredTradeZone)),
                Mathf.Max(1f, villager.thinkInterval));
            LogDebug(
                "ReadyToSell",
                "missingSellDestination preferredZone=" + preferredTradeZone);
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

        BeginSelling();
        return true;
    }

    void BeginSelling()
    {
        state = ForgeCycleState.Selling;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate(
            NpcText.Action("fixedBlacksmithSellingGoods"),
            sellDurationSeconds);
        LogDebug("State", "Selling");
    }

    bool HandleSelling()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (TryGetSellDestination(
                out Vector3 sellPosition,
                out NpcMapZone? sellZone,
                out bool isBrokerTarget,
                out NpcCounterBroker sellBroker) &&
            !HasArrivedAtTradeDestination(
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
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithSellingGoods"),
                sellDurationSeconds);
            return true;
        }

        int earnedMoney = salePrice;
        string sellDetail = "fallback";
        bool soldToShop = false;

        if (inventory != null &&
            forgedItem != null)
        {
            soldToShop =
                TrySellForgedItemToMarket(
                    forgedItem,
                    out earnedMoney,
                    out sellDetail);

            if (!soldToShop)
            {
                inventory.RemoveItem(forgedItem, 1);
                sellDetail =
                    "fallbackSalePrice=" +
                    salePrice;
            }
        }

        if (!soldToShop)
        {
            NpcEconomy.AddNpcMoney(gameObject, earnedMoney);
        }
        completedCycles++;
        state = ForgeCycleState.NeedMaterials;
        forgedWorkHours = 0f;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        lastSaleDay = GetCurrentWorldDay();
        villager.SetActionImmediate(WaitAction, 1f);
        LogDebug(
            "SellComplete",
            "money=" + NpcEconomy.GetNpcMoney(gameObject) +
            " cycles=" + completedCycles +
            " detail=" + sellDetail);
        return true;
    }

    bool HasActionFinished(float durationSeconds)
    {
        if (stateStartedAtRealtime < 0f)
        {
            return true;
        }

        return Time.time - stateStartedAtRealtime >= Mathf.Max(0.01f, durationSeconds);
    }

    Transform GetForgePoint()
    {
        if (forgePointOverride != null)
        {
            return forgePointOverride;
        }

        return villager != null
            ? villager.workPoint
            : null;
    }

    Vector3 GetSafeForgePosition(Vector3 preferredPosition)
    {
        if (IsForgePositionClear(preferredPosition))
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
                if (IsForgePositionClear(candidate))
                {
                    return candidate;
                }
            }
        }

        return preferredPosition;
    }

    bool IsForgePositionClear(Vector3 position)
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
                !hit.enabled ||
                hit.isTrigger ||
                hit.transform.IsChildOf(transform) ||
                hit.GetComponentInParent<VillagerAI>() != null ||
                hit.GetComponentInParent<SmartNpcAI>() != null ||
                hit.GetComponentInParent<MonsterAI>() != null)
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

        if (villager != null &&
            villager.marketPoint != null)
        {
            return villager.marketPoint;
        }

        return GetForgePoint();
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

        NpcCounterBroker pointBroker =
            pointOverride.GetComponentInParent<NpcCounterBroker>();
        if (pointBroker == null)
        {
            pointBroker =
                FindBrokerForManualTradePoint(
                    pointOverride.position,
                    targetZone);
        }

        if (pointBroker != null &&
            pointBroker.customerPoint == pointOverride)
        {
            targetPosition = GetBrokerApproachPosition(pointBroker);
            targetZone =
                ResolveBrokerZone(pointBroker) ??
                targetZone;
            isBrokerTarget = pointBroker.receiveAllNpcRequests;
            broker = pointBroker;
            lastTradeDestinationSource =
                sourceLabel + ":brokerCustomerPoint";
            lastTradeDestinationDetail =
                "overridePoint=" + pointOverride.position +
                " overrideZone=" + GetZoneText(targetZone) +
                " broker=" + pointBroker.name +
                " customerPoint=" + pointBroker.customerPoint.position +
                " resolved=" + targetPosition +
                " approachDetail=" + lastBrokerApproachDetail;
            return true;
        }

        if (pointBroker != null &&
            TryGetClearCustomerZonePreferredPoint(
                pointBroker,
                pointOverride.position,
                out Vector3 resolvedPoint))
        {
            targetPosition = resolvedPoint;
            targetZone =
                ResolveBrokerZone(pointBroker) ??
                targetZone;
            broker = pointBroker;
            lastTradeDestinationSource =
                sourceLabel + ":customerZonePreferred";
            lastTradeDestinationDetail =
                "overridePoint=" + pointOverride.position +
                " overrideZone=" + GetZoneText(targetZone) +
                " broker=" + pointBroker.name +
                " preferred=" + pointOverride.position +
                " resolved=" + resolvedPoint +
                " customerPoint=" +
                (pointBroker.customerPoint != null
                    ? pointBroker.customerPoint.position.ToString()
                    : "none");
            return true;
        }

        targetPosition = pointOverride.position;
        broker = pointBroker;
        lastTradeDestinationSource = sourceLabel;
        lastTradeDestinationDetail =
            "overridePoint=" + pointOverride.position +
            " overrideZone=" + GetZoneText(targetZone) +
            " broker=" + (pointBroker != null ? pointBroker.name : "none") +
            " resolved=manualOverride";
        return true;
    }

    bool HasConfiguredMaterialRequirements()
    {
        return materialRequirements != null &&
            materialRequirements.Count > 0;
    }

    int GetEstimatedMaterialBudget()
    {
        if (!HasConfiguredMaterialRequirements())
        {
            return materialCost;
        }

        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop))
        {
            return materialCost;
        }

        int total = 0;
        foreach (FixedBlacksmithMaterialRequirement requirement in materialRequirements)
        {
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            int missing =
                GetMissingMaterialAmount(requirement);
            if (missing <= 0)
            {
                continue;
            }

            int unitPrice =
                shop.GetNpcBuyPrice(
                    requirement.item,
                    gameObject);

            if (unitPrice <= 0)
            {
                unitPrice = Mathf.Max(
                    1,
                    NpcEconomy.GetNpcBuyPrice(
                        requirement.item,
                        gameObject,
                        NpcTradeContext.MarketBuy));
            }

            total += unitPrice * missing;
        }

        return Mathf.Max(0, total);
    }

    int GetMissingMaterialAmount(
        FixedBlacksmithMaterialRequirement requirement)
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
            requirement.amount -
            inventory.GetAmount(requirement.item));
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
        if (!TryFindPreferredTradeShop(out shop))
        {
            detail = "missingShop";
            return false;
        }

        List<string> purchases = new List<string>();

        foreach (FixedBlacksmithMaterialRequirement requirement in materialRequirements)
        {
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            int missing =
                GetMissingMaterialAmount(requirement);
            if (missing <= 0)
            {
                continue;
            }

            int itemIndex =
                shop.FindItemIndex(
                    requirement.item);

            if (itemIndex < 0)
            {
                detail =
                    "missingStock=" +
                    requirement.item.itemName;
                return false;
            }

            int boughtAmount;
            int totalPrice;
            if (!shop.BuyNpcItemToInventory(
                    itemIndex,
                    gameObject,
                    inventory,
                    missing,
                    out boughtAmount,
                    out totalPrice))
            {
                detail =
                    "buyFailed=" +
                    requirement.item.itemName;
                return false;
            }

            purchases.Add(
                requirement.item.itemName +
                "x" + boughtAmount +
                " price=" + totalPrice);
        }

        bool hasAllMaterials =
            HasAllRequiredMaterials();

        detail =
            purchases.Count > 0
                ? string.Join("; ", purchases)
                : "alreadyReady";

        return hasAllMaterials;
    }

    bool HasAllRequiredMaterials()
    {
        if (!HasConfiguredMaterialRequirements() ||
            inventory == null)
        {
            return false;
        }

        foreach (FixedBlacksmithMaterialRequirement requirement in materialRequirements)
        {
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

    bool TryConsumeConfiguredMaterials(out string detail)
    {
        detail = "noRequirements";

        if (!HasConfiguredMaterialRequirements())
        {
            return true;
        }

        if (!HasAllRequiredMaterials())
        {
            detail = "missingOwnedMaterials";
            return false;
        }

        List<string> consumed = new List<string>();

        foreach (FixedBlacksmithMaterialRequirement requirement in materialRequirements)
        {
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
                detail =
                    "consumeFailed=" +
                    requirement.item.itemName;
                return false;
            }

            consumed.Add(
                requirement.item.itemName +
                "x" + requirement.amount);
        }

        detail = string.Join("; ", consumed);
        return true;
    }

    bool TrySellForgedItemToMarket(
        StatItemData item,
        out int earnedMoney,
        out string detail)
    {
        earnedMoney = salePrice;
        detail = "missingItem";

        if (item == null ||
            inventory == null ||
            inventory.GetAmount(item) <= 0)
        {
            return false;
        }

        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop))
        {
            detail = "missingShop";
            return false;
        }

        int soldAmount;
        int totalPrice;
        if (!shop.SellNpcItemFromInventory(
                item,
                gameObject,
                inventory,
                1,
                out soldAmount,
                out totalPrice) ||
            soldAmount <= 0)
        {
            detail = "sellFailed";
            return false;
        }

        earnedMoney = totalPrice;
        detail =
            item.itemName +
            "x" + soldAmount +
            " price=" + totalPrice;
        return true;
    }

    bool TryFindPreferredTradeShop(out SimpleItemShop shop)
    {
        shop = null;
        lastTradeShopName = "none";

        Transform marketPoint = GetMarketPoint();
        if (marketPoint != null)
        {
            shop = marketPoint.GetComponent<SimpleItemShop>();
            if (shop != null)
            {
                lastTradeShopName = shop.name;
                return true;
            }

            shop = marketPoint.GetComponentInParent<SimpleItemShop>();
            if (shop != null)
            {
                lastTradeShopName = shop.name;
                return true;
            }
        }

        if (TryFindPreferredBrokerBackedShop(out shop))
        {
            lastTradeShopName = shop.name;
            return true;
        }

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

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

            float distance =
                Vector2.Distance(
                    searchOrigin,
                    candidate.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                shop = candidate;
            }
        }

        if (shop != null)
        {
            lastTradeShopName = shop.name;
        }

        return shop != null;
    }

    bool TryFindPreferredBrokerBackedShop(out SimpleItemShop shop)
    {
        shop = null;

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            NpcCounterBroker broker =
                candidate.GetComponent<NpcCounterBroker>();
            if (broker == null ||
                !broker.isActiveAndEnabled ||
                !broker.receiveAllNpcRequests)
            {
                continue;
            }

            Vector3 targetPosition =
                broker.customerPoint != null
                    ? broker.customerPoint.position
                    : broker.CustomerPosition;

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
        if (!TryFindPreferredTradeShop(out shop) ||
            shop == null)
        {
            lastTradeDestinationSource = "missingPreferredShop";
            return false;
        }

        broker = shop.GetComponent<NpcCounterBroker>();
        if (broker != null &&
            broker.customerPoint != null)
        {
            lastTradeDestinationSource = "preferredShopBroker";
            targetPosition =
                GetBrokerApproachPosition(broker);
            targetZone = ResolveBrokerZone(broker);
            isBrokerTarget = broker.receiveAllNpcRequests;
            return true;
        }

        lastTradeDestinationSource = "preferredShopRoot";
        targetPosition = shop.transform.position;
        targetZone = ResolveShopZone(shop);
        isBrokerTarget = false;
        return true;
    }

    bool TryGetBuyDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        lastTradeDestinationDetail = "none";

        if (TryGetManualTradePoint(
                buyPointOverride,
                "buyPointOverride",
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (HasConfiguredMaterialRequirements() &&
            TryGetPreferredTradeDestination(
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        Transform marketPoint = GetMarketPoint();
        NpcMapZone? marketZone = ResolveZoneForTransform(marketPoint);

        if (marketPointOverride != null &&
            marketPoint != null &&
            marketZone.HasValue)
        {
            lastTradeDestinationSource = "marketPointOverride";
            targetPosition = marketPoint.position;
            targetZone = marketZone;
            isBrokerTarget = false;
            broker = null;
            return true;
        }

        if (TryGetBrokerDestination(
                buyMaterialsAtVanBaoLau,
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (marketPoint == null ||
            (buyMaterialsAtVanBaoLau &&
            marketZone.HasValue &&
            marketZone.Value != preferredTradeZone))
        {
            return TryGetZoneFallbackDestination(
                buyMaterialsAtVanBaoLau
                    ? preferredTradeZone
                    : ResolveFallbackZone(marketZone),
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker);
        }

        lastTradeDestinationSource = "marketPoint";
        targetPosition = marketPoint.position;
        targetZone = buyMaterialsAtVanBaoLau
            ? preferredTradeZone
            : marketZone;
        isBrokerTarget = false;
        broker = null;
        return true;
    }

    bool TryGetSellDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        lastTradeDestinationDetail = "none";

        if (TryGetManualTradePoint(
                sellPointOverride,
                "sellPointOverride",
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
        NpcMapZone? marketZone = ResolveZoneForTransform(marketPoint);

        if (marketPointOverride != null &&
            marketPoint != null &&
            marketZone.HasValue)
        {
            lastTradeDestinationSource = "marketPointOverride";
            targetPosition = marketPoint.position;
            targetZone = marketZone;
            isBrokerTarget = false;
            broker = null;
            return true;
        }

        if (TryGetBrokerDestination(
                sellAtVanBaoLau,
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (marketPoint == null ||
            (sellAtVanBaoLau &&
            marketZone.HasValue &&
            marketZone.Value != preferredTradeZone))
        {
            return TryGetZoneFallbackDestination(
                sellAtVanBaoLau
                    ? preferredTradeZone
                    : ResolveFallbackZone(marketZone),
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker);
        }

        lastTradeDestinationSource = "marketPoint";
        targetPosition = marketPoint.position;
        targetZone = sellAtVanBaoLau
            ? preferredTradeZone
            : marketZone;
        isBrokerTarget = false;
        broker = null;
        return true;
    }

    bool TryGetZoneFallbackDestination(
        NpcMapZone? zone,
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        broker = null;
        isBrokerTarget = false;

        if (zone.HasValue)
        {
            NpcMapArea area =
                NpcMapArea.FindNearestAreaInZone(
                    zone.Value,
                    transform.position);
            if (area != null &&
                area.areaBounds != null)
            {
                lastTradeDestinationSource =
                    "zoneFallback:" +
                    GetZoneText(zone);
                targetPosition = area.areaBounds.bounds.center;
                targetZone = zone.Value;
                return true;
            }
        }

        lastTradeDestinationSource =
            "missingZoneFallback:" +
            GetZoneText(zone);
        targetPosition = Vector3.zero;
        targetZone = null;
        return false;
    }

    NpcMapZone? ResolveFallbackZone(NpcMapZone? zone)
    {
        if (zone.HasValue)
        {
            return zone;
        }

        Transform marketPoint = GetMarketPoint();
        if (marketPoint != null)
        {
            NpcMapArea nearest =
                NpcMapArea.FindNearestArea(marketPoint.position);
            if (nearest != null)
            {
                return nearest.zone;
            }
        }

        return null;
    }

    bool TryGetBrokerDestination(
        bool allowBroker,
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        broker = null;

        if (allowBroker &&
            TryFindBrokerInZone(
                preferredTradeZone,
                out broker))
        {
            lastTradeDestinationSource =
                "preferredZoneBroker:" +
                GetZoneLabel(preferredTradeZone);
            targetPosition = GetBrokerApproachPosition(broker);
            targetZone = preferredTradeZone;
            isBrokerTarget = true;
            return true;
        }

        lastTradeDestinationSource =
            allowBroker
                ? "missingPreferredZoneBroker:" +
                    GetZoneLabel(preferredTradeZone)
                : "brokerDisabled";
        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        return false;
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

        NpcMapZone? currentZone = ResolveZoneForPosition(transform.position);
        return !currentZone.HasValue ||
            currentZone.Value != targetZone.Value;
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

        approachPosition = point.position;
        approachZone =
            ResolveZoneForTransform(point) ??
            fallbackZone;

        return !IsNear(approachPosition);
    }

    Transform GetActiveTradeApproachPoint()
    {
        switch (state)
        {
            case ForgeCycleState.NeedMaterials:
            case ForgeCycleState.BuyingMaterials:
                return buyApproachPointOverride;

            case ForgeCycleState.ReadyToSell:
            case ForgeCycleState.Selling:
                return sellApproachPointOverride;

            default:
                return null;
        }
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
            Mathf.Max(0.05f, Mathf.Min(extents.x, extents.y) - 0.02f));
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
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

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

        if (targetZone.HasValue &&
            TryFindBrokerInZone(targetZone.Value, out NpcCounterBroker zoneBroker))
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

    bool HasArrivedAtTradeDestination(
        Vector3 targetPosition,
        bool isBrokerTarget,
        NpcCounterBroker broker)
    {
        if (isBrokerTarget)
        {
            if (broker != null &&
                broker.receiveAllNpcRequests)
            {
                return broker.IsCustomerAtCounter(gameObject);
            }
        }

        return IsNear(targetPosition);
    }

    string DescribeTradeTarget(
        Vector3 targetPosition,
        NpcMapZone? targetZone,
        bool isBrokerTarget,
        NpcCounterBroker broker,
        bool arrived)
    {
        string detail =
            "actorPos=" + transform.position +
            " currentZone=" + GetZoneText(GetCurrentZone()) +
            " target=" + targetPosition +
            " targetZone=" + GetZoneText(targetZone) +
            " source=" + lastTradeDestinationSource +
            " sourceDetail=" + lastTradeDestinationDetail +
            " shop=" + lastTradeShopName +
            " isBrokerTarget=" + (isBrokerTarget ? 1 : 0) +
            " arrived=" + (arrived ? 1 : 0) +
            " distTarget=" +
            Vector2.Distance(transform.position, targetPosition).ToString("0.00") +
            " arriveDistance=" +
            GetArrivalDistance().ToString("0.00");

        if (broker == null)
        {
            return detail + " broker=null";
        }

        Vector3 brokerCenter = broker.CustomerPosition;
        Vector3 brokerStand =
            GetBrokerApproachPosition(broker);
        BoxCollider2D customerZone =
            broker.GetCustomerZoneCollider();

        string zoneDetail = " customerZone=none";
        if (customerZone != null &&
            customerZone.enabled)
        {
            Bounds bounds = customerZone.bounds;
            zoneDetail =
                " customerZoneMin=" + bounds.min +
                " customerZoneMax=" + bounds.max +
                " customerZoneCenter=" + bounds.center;
        }

        return detail +
            " broker=" + broker.name +
            " brokerZone=" + GetZoneText(ResolveBrokerZone(broker)) +
            " brokerCenter=" + brokerCenter +
            " brokerStand=" + brokerStand +
            " brokerRadius=" +
            broker.CustomerServiceRadius.ToString("0.00") +
            " distCenter=" +
            Vector2.Distance(transform.position, brokerCenter).ToString("0.00") +
            " distStand=" +
            Vector2.Distance(transform.position, brokerStand).ToString("0.00") +
            " atCounter=" +
            (broker.IsCustomerAtCounter(gameObject) ? 1 : 0) +
            zoneDetail;
    }

    bool TryFindBrokerInZone(
        NpcMapZone zone,
        out NpcCounterBroker broker)
    {
        broker = null;

        NpcCounterBroker[] brokers =
            FindObjectsByType<NpcCounterBroker>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

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

    NpcMapZone? ResolveBrokerZone(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return null;
        }

        if (broker.customerPoint != null)
        {
            NpcMapZone? customerZone =
                ResolveZoneForTransform(broker.customerPoint);
            if (customerZone.HasValue)
            {
                return customerZone;
            }
        }

        return ResolveZoneForTransform(broker.transform);
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

    bool IsNear(Vector3 targetPosition)
    {
        if (villager == null)
        {
            return Vector2.Distance(transform.position, targetPosition) <= 0.25f;
        }

        return Vector2.Distance(transform.position, targetPosition) <=
            Mathf.Max(0.25f, villager.arriveDistance);
    }

    float GetArrivalDistance()
    {
        return villager != null
            ? Mathf.Max(0.25f, villager.arriveDistance)
            : 0.25f;
    }

    NpcMapZone? GetCurrentZone()
    {
        NpcMapArea area =
            NpcMapArea.FindArea(transform.position);
        if (area != null)
        {
            return area.zone;
        }

        area =
            NpcMapArea.FindNearestArea(transform.position);
        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    float CalculateWorkHoursBetween(float startWorldHour, float endWorldHour)
    {
        if (endWorldHour <= startWorldHour)
        {
            return 0f;
        }

        float total = 0f;
        int startDayIndex = Mathf.FloorToInt(startWorldHour / 24f);
        int endDayIndex = Mathf.FloorToInt(endWorldHour / 24f);

        for (int dayIndex = startDayIndex; dayIndex <= endDayIndex; dayIndex++)
        {
            float dayBaseHour = dayIndex * 24f;
            total += Overlap(
                startWorldHour,
                endWorldHour,
                dayBaseHour + morningWorkStart,
                dayBaseHour + morningWorkEnd);
            total += Overlap(
                startWorldHour,
                endWorldHour,
                dayBaseHour + afternoonWorkStart,
                dayBaseHour + afternoonWorkEnd);
        }

        return total;
    }

    static float Overlap(
        float rangeStart,
        float rangeEnd,
        float slotStart,
        float slotEnd)
    {
        float start = Mathf.Max(rangeStart, slotStart);
        float end = Mathf.Min(rangeEnd, slotEnd);
        return Mathf.Max(0f, end - start);
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentWorldHour
            : 0f;
    }

    float GetCurrentClockHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentHour
            : 0f;
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : 0;
    }

    bool IsDedicatedRestWindow(float hour)
    {
        if (IsHourInRange(hour, sleepStart, sleepEnd))
        {
            return true;
        }

        return IsHourInRange(
            hour,
            morningWorkEnd,
            afternoonWorkStart);
    }

    static bool IsHourInRange(
        float hour,
        float startHour,
        float endHour)
    {
        if (Mathf.Approximately(startHour, endHour))
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

    void LogDebug(string stage, string detail)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.LogWarning(
            "[NpcFixedBlacksmith] " +
            gameObject.name +
            " stage=" + stage +
            " state=" + state +
            " detail=" + detail,
            this);
    }

    string DescribeBlockingCollidersAtPoint(
        Vector3 position,
        float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
        if (hits == null || hits.Length == 0)
        {
            return "none";
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();
        bool wroteAny = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                hit.isTrigger ||
                IsSelfCollider(hit))
            {
                continue;
            }

            if (wroteAny)
            {
                builder.Append(" | ");
            }

            builder.Append(hit.name)
                .Append("@")
                .Append(hit.bounds.center)
                .Append(" layer=")
                .Append(hit.gameObject.layer);
            wroteAny = true;
        }

        return wroteAny ? builder.ToString() : "none";
    }

    string DescribeRaycastBlockersTo(
        Vector3 targetPosition,
        float radius)
    {
        Vector2 origin = transform.position;
        Vector2 delta = (Vector2)targetPosition - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return "none";
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin,
            Mathf.Max(0.01f, radius),
            delta.normalized,
            distance);
        if (hits == null || hits.Length == 0)
        {
            return "none";
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();
        bool wroteAny = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null ||
                hit.isTrigger ||
                IsSelfCollider(hit))
            {
                continue;
            }

            if (wroteAny)
            {
                builder.Append(" | ");
            }

            builder.Append(hit.name)
                .Append("@")
                .Append(hit.bounds.center)
                .Append(" dist=")
                .Append(hits[i].distance.ToString("0.00"));
            wroteAny = true;
        }

        return wroteAny ? builder.ToString() : "none";
    }

    static string GetZoneLabel(NpcMapZone zone)
    {
        switch (zone)
        {
            case NpcMapZone.VanBaoLau:
                return "Van Bao Lau";
            case NpcMapZone.MaThuSonMach:
                return "Ma Thu Son Mach";
            case NpcMapZone.BichAnh:
                return "Bich Anh";
            default:
                return "Lang";
        }
    }

    static string GetZoneText(NpcMapZone? zone)
    {
        return zone.HasValue
            ? GetZoneLabel(zone.Value)
            : "None";
    }

    void SyncSleepVisibility()
    {
        if (villager == null)
        {
            return;
        }

        bool shouldHide =
            IsHideAtHomeScheduleActive() &&
            IsAtHomePoint();

        if (shouldHide)
        {
            if (!villager.IsHiddenAtHome)
            {
                villager.ForceHiddenAtHome(true);
                LogDebug("Visibility", "hideAtHome scheduleSlot=1");
            }

            return;
        }

        if (villager.IsHiddenAtHome)
        {
            villager.ForceHiddenAtHome(false);
            LogDebug("Visibility", "hideAtHome scheduleSlot=0");
        }
    }

    bool IsHideAtHomeScheduleActive()
    {
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
            Mathf.Max(0.25f, villager.arriveDistance);
    }
}
