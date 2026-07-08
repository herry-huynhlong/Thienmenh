using System.Collections.Generic;
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
    public Transform buyPointOverride;
    public Transform sellPointOverride;

    [Header("Actions")]
    public string buyAction = "Di mua nguyen lieu ren";
    public string forgeAction = "Dang ren phap bao";
    public string sellAction = "Di ban phap bao";
    public string waitAction = "Cho ca ren tiep theo";

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
    string lastTradeDestinationSource = "none";
    string lastTradeShopName = "none";

    public bool SuppressBaseTimeRestRules => suppressBaseTimeRestRules;
    public string DebugTradeDestinationSource => lastTradeDestinationSource;
    public string DebugTradeShopName => lastTradeShopName;
    public int DebugMaterialRequirementCount =>
        materialRequirements != null
            ? materialRequirements.Count
            : 0;
    public bool DebugHasConfiguredMaterialRequirements =>
        HasConfiguredMaterialRequirements();

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
            villager.SetActionImmediate("Da du nguyen lieu de ren", 1f);
            LogDebug("NeedMaterials", "resumeForgingFromInventory=1");
            return true;
        }

        int minimumBudget =
            GetEstimatedMaterialBudget();

        if (minimumBudget > 0 &&
            NpcEconomy.GetNpcMoney(gameObject) < minimumBudget)
        {
            villager.SetActionImmediate("Thieu linh thach de mua nguyen lieu", 2f);
            return true;
        }

        if (!TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker buyBroker))
        {
            villager.SetActionImmediate(
                "Khong tim thay diem mua o " +
                GetZoneLabel(preferredTradeZone),
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
            villager.ForceJobMoveTo(
                buyPosition,
                buyAction,
                buyZone,
                !isBrokerTarget);
            return true;
        }

        BeginBuyingMaterials();
        return true;
    }

    void BeginBuyingMaterials()
    {
        state = ForgeCycleState.BuyingMaterials;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate("Dang mua nguyen lieu ren", buyDurationSeconds);
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
                villager.ForceJobMoveTo(
                    buyPosition,
                    buyAction,
                    buyZone,
                    !isBrokerTarget);
                return true;
            }
        }

        if (!HasActionFinished(buyDurationSeconds))
        {
            villager.SetActionImmediate("Dang mua nguyen lieu ren", buyDurationSeconds);
            return true;
        }

        if (NpcEconomy.GetNpcMoney(gameObject) < materialCost)
        {
            if (!HasConfiguredMaterialRequirements())
            {
                state = ForgeCycleState.NeedMaterials;
                villager.SetActionImmediate("Khong du linh thach mua nguyen lieu", 2f);
                return true;
            }
        }

        string purchaseDetail = string.Empty;
        if (HasConfiguredMaterialRequirements())
        {
            if (!TryPurchaseConfiguredMaterials(out purchaseDetail))
            {
                state = ForgeCycleState.NeedMaterials;
                villager.SetActionImmediate("Chua mua du nguyen lieu", 2f);
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
        villager.SetActionImmediate("Da mua xong nguyen lieu", 1f);
        LogDebug(
            "BuyComplete",
            "money=" + NpcEconomy.GetNpcMoney(gameObject) +
            " materials=" + purchaseDetail);
        return true;
    }

    bool HandleForging()
    {
        Transform forgePoint = GetForgePoint();
        float currentWorldHour = GetCurrentWorldHour();

        if (forgePoint != null &&
            !IsNear(forgePoint.position))
        {
            lastProgressWorldHour = currentWorldHour;
            villager.ForceJobMoveTo(
                forgePoint.position,
                forgeAction,
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
            forgeAction + " (" +
            Mathf.CeilToInt(forgedWorkHours) + "/" +
            Mathf.CeilToInt(RequiredWorkHours) + "h)",
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
            villager.SetActionImmediate("Thieu nguyen lieu de tiep tuc ren", 2f);
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
        villager.SetActionImmediate("Da ren xong, chuan bi dem ban", 1f);
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
                "Khong tim thay diem ban o " +
                GetZoneLabel(preferredTradeZone),
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
            villager.ForceJobMoveTo(
                sellPosition,
                sellAction,
                sellZone,
                !isBrokerTarget);
            return true;
        }

        BeginSelling();
        return true;
    }

    void BeginSelling()
    {
        state = ForgeCycleState.Selling;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate("Dang ban phap bao", sellDurationSeconds);
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
            villager.ForceJobMoveTo(
                sellPosition,
                sellAction,
                sellZone,
                !isBrokerTarget);
            return true;
        }

        if (!HasActionFinished(sellDurationSeconds))
        {
            villager.SetActionImmediate("Dang ban phap bao", sellDurationSeconds);
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
        villager.SetActionImmediate(waitAction, 1f);
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

        NpcCounterBroker pointBroker =
            pointOverride.GetComponentInParent<NpcCounterBroker>();
        if (pointBroker != null &&
            pointBroker.customerPoint == pointOverride)
        {
            targetPosition = GetBrokerApproachPosition(pointBroker);
            targetZone =
                ResolveBrokerZone(pointBroker) ??
                ResolveZoneForTransform(pointOverride);
            isBrokerTarget = pointBroker.receiveAllNpcRequests;
            broker = pointBroker;
            lastTradeDestinationSource =
                sourceLabel + ":brokerCustomerPoint";
            return true;
        }

        targetPosition = pointOverride.position;
        targetZone = ResolveZoneForTransform(pointOverride);
        lastTradeDestinationSource = sourceLabel;
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
            approachPosition.z = customerCenter.z;
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
        return approachPosition;
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
            broker.GetCustomerPositionFor(gameObject);
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

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : 0;
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

    static string GetZoneLabel(NpcMapZone zone)
    {
        switch (zone)
        {
            case NpcMapZone.VanBaoLau:
                return "Van Bao Lau";
            case NpcMapZone.MaThuSonMach:
                return "Ma Thu Son Mach";
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
