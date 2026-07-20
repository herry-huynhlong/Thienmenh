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
    public int lastBatchMaterialBudget;
    public int lastBatchMinimumSaleValue;
    public bool debugLogs;

    VillagerAI villager;
    ItemInventory inventory;
    NpcScheduleController schedule;
    NpcAlchemyAgent alchemyAgent;
    NpcTradeAgent tradeAgent;
    string lastTradeDestinationSource = "none";
    string lastTradeShopName = "none";

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
    }

    System.Collections.IEnumerator ApplyRecommendedSetupNextFrame()
    {
        yield return null;
        ApplyRecommendedSetup();
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

            state = AlchemyCycleState.ReadyToSell;
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

        if (HasProductsReadyToSell())
        {
            state = AlchemyCycleState.ReadyToSell;
            return true;
        }

        if (autoPlanLowGradeBatches &&
            !HasConfiguredMaterialRequirements())
        {
            villager.SetActionImmediate(
                NpcText.Action("fixedAlchemistCouldNotBuyMaterials"),
                2f);
            return true;
        }

        if (NeedsMaterialsForNextBatch())
        {
            if (!NpcScheduleController.AllowsTrade(gameObject))
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

        if (!NpcScheduleController.AllowsAlchemy(gameObject))
        {
            villager.SetActionImmediate(WaitAction, villager.thinkInterval);
            return true;
        }

        if (!MoveVillagerToAlchemyPointIfNeeded())
        {
            return true;
        }

        if (HasConfiguredMaterialRequirements())
        {
            if (!ConsumeConfiguredMaterials())
            {
                villager.SetActionImmediate(
                    NpcText.Action("fixedAlchemistMissingMaterialsContinue"),
                    2f);
                return true;
            }

            if (alchemyAgent.TryStartFixedAlchemy(
                    refinedItem,
                    Mathf.Max(1, refinedItemAmount)))
            {
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

        if (HasConfiguredMaterialRequirements())
        {
            boughtAllMaterials =
                TryPurchaseConfiguredMaterials(out _);
            traded = boughtAllMaterials;
        }
        else if (tradeAgent != null)
        {
            if (buyBroker != null &&
                buyBroker.receiveAllNpcRequests)
            {
                traded = buyBroker.TryTradeWithNpc(tradeAgent);
            }
            else
            {
                traded = NpcCounterBroker.TryTradeWithActiveBroker(tradeAgent);
            }
        }

        lastPurchaseDay = GetCurrentDay();
        state = AlchemyCycleState.NeedMaterials;

        if (!NeedsMaterialsForNextBatch())
        {
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
            state = alchemyAgent.HasSellableFinishedGoodsAvailable
                ? AlchemyCycleState.ReadyToSell
                : AlchemyCycleState.NeedMaterials;
            return true;
        }

        MoveVillagerToAlchemyPointIfNeeded();
        villager.SetActionImmediate(
            RefineAction,
            Mathf.Max(1f, villager.thinkInterval));
        return true;
    }

    bool HandleReadyToSell()
    {
        if (!HasProductsReadyToSell())
        {
            state = AlchemyCycleState.NeedMaterials;
            return true;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
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

        bool sold = alchemyAgent.TrySellFinishedGoods();
        lastSaleDay = GetCurrentDay();

        state = HasProductsReadyToSell()
            ? AlchemyCycleState.ReadyToSell
            : AlchemyCycleState.NeedMaterials;

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
        Transform alchemyPoint = GetAlchemyPoint();
        if (alchemyPoint == null)
        {
            return true;
        }

        if (IsNear(alchemyPoint.position))
        {
            return true;
        }

        villager.ForceJobMoveTo(
            alchemyPoint.position,
            RefineAction,
            NpcMapNavigator.GetDestinationZone(alchemyPoint),
            true);
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

        if (pointOverride != null)
        {
            broker = pointOverride.GetComponentInParent<NpcCounterBroker>();
            if (broker != null)
            {
                targetPosition = broker.GetCustomerPositionFor(gameObject);
                targetZone = ResolveBrokerZone(broker) ??
                    ResolveZoneForTransform(pointOverride);
                isBrokerTarget = broker.receiveAllNpcRequests;
                lastTradeDestinationSource = sourceLabel + ":broker";
                lastTradeShopName = broker.name;
                return true;
            }

            targetPosition = pointOverride.position;
            targetZone = ResolveZoneForTransform(pointOverride);
            lastTradeDestinationSource = sourceLabel;
            return true;
        }

        if (allowBroker)
        {
            broker = NpcCounterBroker.Active;
            if (broker != null &&
                broker.receiveAllNpcRequests)
            {
                targetPosition = broker.GetCustomerPositionFor(gameObject);
                targetZone = ResolveBrokerZone(broker);
                isBrokerTarget = true;
                lastTradeDestinationSource = "activeBroker";
                lastTradeShopName = broker.name;
                return true;
            }
        }

        Transform marketPoint = GetMarketPoint();
        if (marketPoint != null)
        {
            targetPosition = marketPoint.position;
            targetZone = ResolveZoneForTransform(marketPoint);
            lastTradeDestinationSource = marketPointOverride != null
                ? "marketPointOverride"
                : "marketPoint";
            return true;
        }

        lastTradeDestinationSource = "missingTradePoint";
        return false;
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
            return broker.IsCustomerAtCounter(gameObject);
        }

        return IsNear(targetPosition);
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
            villager.ForceJobMoveTo(
                approachPosition,
                action,
                approachZone,
                true);
            return;
        }

        villager.ForceJobMoveTo(
            targetPosition,
            action,
            targetZone,
            isBrokerTarget);
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
            GetExpectedAlchemySaleValue(refinedItem) >=
            GetEstimatedMaterialBudget() + Mathf.Max(0, refiningLaborFee))
        {
            return;
        }

        TryGenerateDynamicAlchemyBatchPlan();
    }

    bool TryGenerateDynamicAlchemyBatchPlan()
    {
        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop) ||
            shop == null ||
            shop.items == null ||
            shop.items.Count == 0 ||
            alchemyAgent == null ||
            alchemyAgent.alchemyCatalogItems == null ||
            alchemyAgent.alchemyCatalogItems.Count == 0)
        {
            return false;
        }

        System.Collections.Generic.List<StatItemData> materialPool =
            new System.Collections.Generic.List<StatItemData>();

        for (int i = 0; i < shop.items.Count; i++)
        {
            ShopItemSlot slot = shop.items[i];
            if (slot == null ||
                slot.item == null ||
                slot.amount <= 0 ||
                !IsValidLowGradeAlchemyMaterial(slot.item))
            {
                continue;
            }

            if (!materialPool.Contains(slot.item))
            {
                materialPool.Add(slot.item);
            }
        }

        if (materialPool.Count < Mathf.Max(1, randomMaterialKindsMin))
        {
            return false;
        }

        ShuffleItemList(materialPool);

        int plannedKinds =
            Mathf.Clamp(
                Random.Range(
                    Mathf.Max(1, randomMaterialKindsMin),
                    Mathf.Max(randomMaterialKindsMin, randomMaterialKindsMax) + 1),
                1,
                materialPool.Count);

        System.Collections.Generic.List<FixedAlchemistMaterialRequirement>
            plannedRequirements =
                new System.Collections.Generic.List<FixedAlchemistMaterialRequirement>();
        int totalCost = 0;

        for (int i = 0; i < plannedKinds; i++)
        {
            StatItemData item = materialPool[i];
            int unitPrice = shop.GetNpcBuyPrice(item, gameObject);
            if (unitPrice <= 0)
            {
                continue;
            }

            plannedRequirements.Add(
                new FixedAlchemistMaterialRequirement
                {
                    item = item,
                    amount = 1
                });
            totalCost += unitPrice;
        }

        if (plannedRequirements.Count < Mathf.Max(1, randomMaterialKindsMin))
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
                candidate.grade != ItemGrade.Ha ||
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

        if (refinedItem != null &&
            inventory.GetAmount(refinedItem) > 0)
        {
            return true;
        }

        return alchemyAgent != null &&
            alchemyAgent.HasSellableFinishedGoodsAvailable;
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
            if (itemIndex < 0)
            {
                detail = "missingStock=" + requirement.item.itemName;
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
                detail = "buyFailed=" + requirement.item.itemName;
                return false;
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
            shop = marketPoint.GetComponent<SimpleItemShop>();
            if (shop == null)
            {
                shop = marketPoint.GetComponentInParent<SimpleItemShop>();
            }

            if (shop != null)
            {
                lastTradeShopName = shop.name;
                return true;
            }
        }

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        float bestDistance = float.MaxValue;

        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (candidate == null)
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveZoneForTransform(candidate.transform);
            if (candidateZone.HasValue &&
                candidateZone.Value != preferredTradeZone)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    transform.position,
                    candidate.transform.position);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            shop = candidate;
        }

        if (shop != null)
        {
            lastTradeShopName = shop.name;
            return true;
        }

        return false;
    }

    bool IsValidLowGradeAlchemyMaterial(StatItemData item)
    {
        return item != null &&
            item.grade == ItemGrade.Ha &&
            item.canBeRefinedIntoPill &&
            item.canBeSold &&
            item.itemType == ItemType.VatLieu;
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

    void EnsureStartingMoney()
    {
        if (startingMoney <= 0 ||
            NpcEconomy.GetNpcMoney(gameObject) >= startingMoney)
        {
            return;
        }

        NpcEconomy.AddNpcMoney(
            gameObject,
            startingMoney - NpcEconomy.GetNpcMoney(gameObject));
    }

    void ApplyRecommendedSetup()
    {
        CacheReferences();
        EnsureRecommendedScheduleConfigured();

        if (villager != null)
        {
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
            tradeAgent.buyUsefulItemsFromMarketTrader = true;
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
        return target != null
            ? NpcMapNavigator.GetDestinationZone(target)
            : null;
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
