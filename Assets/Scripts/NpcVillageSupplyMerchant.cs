using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ItemInventory))]
[RequireComponent(typeof(NpcTradeAgent))]
public class NpcVillageSupplyMerchant : MonoBehaviour
{
    enum DailyMerchantPhase
    {
        HomeHidden,
        VanBaoLauTrade,
        LocalMarket
    }

    [Header("References")]
    public ItemInventory inventory;
    public NpcTradeAgent tradeAgent;
    public NpcCounterBroker localBroker;
    public SimpleItemShop destinationShop;
    public Transform homePoint;
    public Transform marketPoint;

    [Header("Daily Lifecycle")]
    public bool autoFindDestinationShop = true;
    public NpcMapZone preferredTradeZone = NpcMapZone.VanBaoLau;
    [Range(0f, 24f)] public float vanBaoLauStartHour = 9f;
    [Range(0f, 24f)] public float vanBaoLauEndHour = 12f;
    [Range(0f, 24f)] public float middayHideHour = 13f;
    [Range(0f, 24f)] public float marketOpenHour = 16f;
    [Range(0f, 24f)] public float marketCloseHour = 20f;
    [Min(0.2f)] public float moveSpeed = 2.1f;
    [Min(0.1f)] public float arriveDistance = 0.45f;
    public bool hideAtHome = true;
    public bool debugLogs;

    [Header("Sell Outbound Goods")]
    public bool sellMaterials = true;
    public bool sellFood = true;
    public StatItemData[] extraSellItems;

    [Header("Village Restock")]
    public bool restockLowGradeGoods = true;
    public bool autoRestockFromDestinationShop = true;
    public StatItemData[] restockItems;
    [Min(1)] public int restockTriggerAmount = 10;
    [Min(1)] public int restockTargetAmount = 20;
    [Min(1)] public int maxRestockKindsPerTrip = 6;
    public bool restockLowGradePills = true;
    public bool restockLowGradeFood = false;
    public bool restockLowGradeMaterials = false;
    public bool restockLowGradeEquipment = false;
    public bool restockLowGradeManuals = false;

    DailyMerchantPhase currentPhase;
    VillagerAI villager;
    NPCVisualAnimation visualAnimation;
    NpcCounterBroker destinationBroker;
    Vector3 lastPosition;
    Vector2 visualDirection = Vector2.down;
    bool inventoryConfigured;
    bool isHiddenAtHome;
    Renderer[] cachedRenderers;
    Collider2D[] cachedColliders;

    void Awake()
    {
        EnsureReferences();
        lastPosition = transform.position;
    }

    void OnEnable()
    {
        EnsureReferences();
        ApplyVillagerLifecycleOwnership(true);
    }

    void OnDisable()
    {
        SetLocalBrokerRequests(false);
        SetLocalBrokerStationary(false);
        ApplyVillagerLifecycleOwnership(false);
        SetHiddenAtHome(false);
    }

    void Start()
    {
        EnsureReferences();
        SetIdleAction();
        UpdateVisualAnimation(true);
    }

    void Update()
    {
        EnsureReferences();

        if (NpcRoleUtility.IsDead(gameObject))
        {
            return;
        }

        UpdateDailyLifecycle();
        UpdateVisualAnimation(isHiddenAtHome);
    }

    [ContextMenu("Debug Start Supply Route")]
    public void DebugStartSupplyRoute()
    {
        EnsureReferences();
        currentPhase = DailyMerchantPhase.VanBaoLauTrade;
        SetHiddenAtHome(false);
    }

    void EnsureReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }

        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        if (!inventoryConfigured &&
            inventory != null)
        {
            inventory.UsePrivateNpcRuntimeItems(false);
            inventoryConfigured = true;
        }

        if (tradeAgent == null)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
        }

        if (tradeAgent == null)
        {
            tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
        }

        tradeAgent.inventory = inventory;

        if (villager == null)
        {
            villager = GetComponent<VillagerAI>();
        }

        if (villager != null)
        {
            if (homePoint != null)
            {
                villager.homePoint = homePoint;
            }

            if (marketPoint != null)
            {
                villager.marketPoint = marketPoint;
            }
        }

        if (localBroker == null)
        {
            localBroker = GetComponent<NpcCounterBroker>();
        }

        if (visualAnimation == null)
        {
            visualAnimation = NPCVisualAnimation.EnsureOn(gameObject);
        }

        CacheVisibilityTargets();

        if (destinationShop == null &&
            autoFindDestinationShop)
        {
            destinationShop = FindPreferredDestinationShop();
        }
    }

    void UpdateDailyLifecycle()
    {
        DailyMerchantPhase targetPhase =
            ResolveCurrentPhase();
        currentPhase = targetPhase;

        switch (targetPhase)
        {
            case DailyMerchantPhase.VanBaoLauTrade:
                UpdateVanBaoLauTrade();
                break;

            case DailyMerchantPhase.LocalMarket:
                UpdateLocalMarket();
                break;

            default:
                UpdateHomeHiddenPhase();
                break;
        }
    }

    DailyMerchantPhase ResolveCurrentPhase()
    {
        float hour = GetCurrentWorldHour();
        if (IsHourInRange(
                hour,
                vanBaoLauStartHour,
                vanBaoLauEndHour))
        {
            return DailyMerchantPhase.VanBaoLauTrade;
        }

        if (IsHourInRange(
                hour,
                marketOpenHour,
                marketCloseHour))
        {
            return DailyMerchantPhase.LocalMarket;
        }

        return DailyMerchantPhase.HomeHidden;
    }

    void UpdateVanBaoLauTrade()
    {
        SetHiddenAtHome(false);
        SetLocalBrokerRequests(false);
        SetLocalBrokerStationary(false);

        if (!TryGetDestinationPosition(
                out Vector3 destination,
                out NpcMapZone? destinationZone))
        {
            SetIdleAction();
            return;
        }

        if (!HasArrivedAtDestination(
                destination,
                destinationZone))
        {
            NpcMapZone? currentZone =
                NpcMapNavigator.ResolveActorZone(gameObject);
            if (destinationZone.HasValue &&
                currentZone.HasValue &&
                destinationZone.Value != currentZone.Value &&
                !NpcGateTravelPolicy.AllowsAutomaticGateTravel(gameObject))
            {
                LogDebug(
                    "VanBaoLauTrade",
                    "gateTravelDisabled fallback=LocalMarket destinationZone=" +
                    destinationZone.Value +
                    " currentZone=" +
                    currentZone.Value);
                UpdateLocalMarket();
                return;
            }

            if (!MoveTowards(
                    destination,
                    NpcText.Action("goVanBaoLauBroker"),
                    destinationZone))
            {
                LogDebug(
                    "VanBaoLauTrade",
                    "routeUnavailable fallback=LocalMarket");
                UpdateLocalMarket();
            }
            return;
        }

        bool soldAny = SellOutboundGoods();
        bool boughtAny = BuyVillageRestock();

        if (boughtAny)
        {
            SetAction(NpcText.Action("boughtGoods"));
            return;
        }

        if (soldAny)
        {
            SetAction(NpcText.Action("soldGoods"));
            return;
        }

        SetIdleAction();
    }

    void UpdateLocalMarket()
    {
        SetHiddenAtHome(false);
        SetLocalBrokerRequests(true);

        if (!TryGetLocalMarketPosition(
                out Vector3 marketPosition,
                out NpcMapZone? marketZone))
        {
            SetLocalBrokerStationary(false);
            SetIdleAction();
            return;
        }

        bool hasArrived =
            IsAtLocalMarketPosition(
                marketPosition,
                marketZone);

        SetLocalBrokerStationary(hasArrived);

        if (!hasArrived)
        {
            if (!MoveTowards(
                    marketPosition,
                    NpcText.Action("goMarketTrade"),
                    marketZone))
            {
                SetAction(NpcText.Action("goMarketTrade"));
            }
            return;
        }

        SetAction(NpcText.Action("trading"));
    }

    bool IsAtLocalMarketPosition(
        Vector3 marketPosition,
        NpcMapZone? marketZone)
    {
        if (marketZone.HasValue)
        {
            NpcMapZone? currentZone =
                NpcMapNavigator.ResolveActorZone(gameObject);
            if (currentZone.HasValue &&
                currentZone.Value != marketZone.Value)
            {
                return false;
            }
        }

        return
            Vector2.Distance(transform.position, marketPosition) <=
            Mathf.Max(0.1f, arriveDistance);
    }

    void UpdateHomeHiddenPhase()
    {
        SetLocalBrokerRequests(false);
        SetLocalBrokerStationary(false);

        if (homePoint == null)
        {
            SetHiddenAtHome(ShouldHideAtCurrentHour());
            SetIdleAction();
            return;
        }

        float hideThreshold =
            Mathf.Repeat(middayHideHour, 24f);
        float hour = GetCurrentWorldHour();
        bool shouldHideNow =
            hour >= hideThreshold ||
            hour < vanBaoLauStartHour;

        if (Vector2.Distance(transform.position, homePoint.position) >
            Mathf.Max(0.1f, arriveDistance))
        {
            SetHiddenAtHome(false);
            MoveTowards(
                homePoint.position,
                NpcText.Action("goMarketTrade"));
            return;
        }

        SetHiddenAtHome(shouldHideNow);
        SetIdleAction();
    }

    SimpleItemShop ResolveDestinationShop()
    {
        if (destinationShop != null)
        {
            return destinationShop;
        }

        return FindPreferredDestinationShop();
    }

    SimpleItemShop FindPreferredDestinationShop()
    {
        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude);

        SimpleItemShop best = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (candidate == null ||
                candidate == GetComponent<SimpleItemShop>() ||
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
                    transform.position,
                    candidate.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    NpcCounterBroker ResolveDestinationBroker(
        SimpleItemShop shop)
    {
        if (shop == null)
        {
            return null;
        }

        NpcCounterBroker broker =
            shop.GetComponent<NpcCounterBroker>();
        if (broker != null &&
            broker.receiveAllNpcRequests)
        {
            return broker;
        }

        if (shop.sellerObject != null)
        {
            broker =
                shop.sellerObject.GetComponent<NpcCounterBroker>();
            if (broker != null &&
                broker.receiveAllNpcRequests)
            {
                return broker;
            }
        }

        if (shop.sellerInventory != null)
        {
            broker =
                shop.sellerInventory.GetComponent<NpcCounterBroker>();
            if (broker != null &&
                broker.receiveAllNpcRequests)
            {
                return broker;
            }
        }

        return null;
    }

    bool TryGetDestinationPosition(
        out Vector3 position,
        out NpcMapZone? destinationZone)
    {
        position = transform.position;
        destinationZone = null;
        destinationShop = ResolveDestinationShop();
        destinationBroker = ResolveDestinationBroker(destinationShop);

        if (destinationBroker != null)
        {
            if (destinationBroker.customerPoint != null)
            {
                destinationZone =
                    NpcMapNavigator.GetDestinationZone(
                        destinationBroker.customerPoint);
            }

            if (!destinationZone.HasValue)
            {
                destinationZone =
                    ResolveShopZone(destinationShop);
            }

            position =
                destinationBroker.GetCustomerPositionFor(
                    gameObject);
            return true;
        }

        if (destinationShop != null)
        {
            destinationZone =
                ResolveShopZone(destinationShop);
            position = destinationShop.transform.position;
            return true;
        }

        return false;
    }

    bool HasArrivedAtDestination(
        Vector3 destination,
        NpcMapZone? destinationZone)
    {
        if (destinationZone.HasValue)
        {
            NpcMapZone? currentZone =
                NpcMapNavigator.ResolveActorZone(gameObject);
            if (currentZone.HasValue &&
                currentZone.Value != destinationZone.Value)
            {
                return false;
            }
        }

        if (destinationBroker != null)
        {
            return destinationBroker.IsCustomerAtCounter(
                gameObject);
        }

        return Vector2.Distance(
            transform.position,
            destination) <= Mathf.Max(0.1f, arriveDistance);
    }

    bool HasOutboundGoodsToSell()
    {
        if (inventory == null)
        {
            return false;
        }

        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack = inventory.items[i];
            if (IsOutboundSellItem(stack))
            {
                return true;
            }
        }

        return false;
    }

    bool IsOutboundSellItem(ItemStack stack)
    {
        if (stack == null ||
            stack.item == null ||
            stack.amount <= 0 ||
            !NpcEconomy.CanTradeNormally(stack.item))
        {
            return false;
        }

        if (ContainsRestockItem(stack.item))
        {
            return false;
        }

        if (sellMaterials &&
            stack.item.itemType == ItemType.VatLieu)
        {
            return true;
        }

        if (sellFood &&
            stack.item.itemType == ItemType.ThucPham)
        {
            return true;
        }

        if (extraSellItems != null)
        {
            for (int i = 0; i < extraSellItems.Length; i++)
            {
                if (extraSellItems[i] == stack.item)
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool SellOutboundGoods()
    {
        if (destinationShop == null ||
            inventory == null)
        {
            return false;
        }

        bool soldAny = false;
        destinationShop.RefreshFromSellerInventory();

        for (int i = inventory.items.Count - 1; i >= 0; i--)
        {
            if (i >= inventory.items.Count)
            {
                continue;
            }

            ItemStack stack = inventory.items[i];
            if (!IsOutboundSellItem(stack))
            {
                continue;
            }

            int desiredAmount =
                Mathf.Max(1, stack.amount);
            if (!destinationShop.SellNpcItemFromInventory(
                    stack.item,
                    gameObject,
                    inventory,
                    desiredAmount,
                    out int soldAmount,
                    out int totalPrice) ||
                soldAmount <= 0)
            {
                continue;
            }

            if (!HasEconomyWallet() &&
                tradeAgent != null)
            {
                tradeAgent.AddMoney(totalPrice);
            }

            soldAny = true;
            LogDebug(
                "SellOutboundGoods",
                stack.item.itemName +
                " amount=" + soldAmount +
                " price=" + totalPrice);
        }

        return soldAny;
    }

    bool NeedsVillageRestock()
    {
        if (!restockLowGradeGoods ||
            inventory == null)
        {
            return false;
        }

        List<StatItemData> candidates =
            GetRestockCandidates();
        for (int i = 0; i < candidates.Count; i++)
        {
            StatItemData item = candidates[i];
            if (item == null)
            {
                continue;
            }

            if (inventory.GetAmount(item) <
                Mathf.Max(1, restockTriggerAmount))
            {
                return true;
            }
        }

        return false;
    }

    bool BuyVillageRestock()
    {
        if (!restockLowGradeGoods ||
            destinationShop == null ||
            inventory == null ||
            tradeAgent == null)
        {
            return false;
        }

        destinationShop.RefreshFromSellerInventory();

        List<StatItemData> candidates =
            GetRestockCandidates();
        bool boughtAny = false;
        int boughtKinds = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (boughtKinds >= Mathf.Max(1, maxRestockKindsPerTrip))
            {
                break;
            }

            StatItemData item = candidates[i];
            if (item == null)
            {
                continue;
            }

            int currentAmount =
                inventory.GetAmount(item);
            if (currentAmount >=
                Mathf.Max(1, restockTriggerAmount))
            {
                continue;
            }

            int desiredAmount =
                Mathf.Max(
                    Mathf.Max(1, restockTargetAmount),
                    currentAmount) -
                currentAmount;
            if (desiredAmount <= 0)
            {
                continue;
            }

            int purchasedAmount = 0;
            int totalPrice = 0;

            if (destinationBroker != null)
            {
                if (!destinationBroker.TrySellSpecificItemTo(
                        tradeAgent,
                        item,
                        desiredAmount,
                        true))
                {
                    continue;
                }

                purchasedAmount =
                    Mathf.Max(
                        0,
                        inventory.GetAmount(item) -
                        currentAmount);
                totalPrice =
                    purchasedAmount *
                    destinationShop.GetNpcBuyPrice(
                        item,
                        gameObject);
            }
            else
            {
                if (!HasEconomyWallet())
                {
                    continue;
                }

                int itemIndex =
                    destinationShop.FindItemIndex(item);
                if (itemIndex < 0)
                {
                    continue;
                }

                if (!destinationShop.BuyNpcItemToInventory(
                        itemIndex,
                        gameObject,
                        inventory,
                        desiredAmount,
                        out purchasedAmount,
                        out totalPrice) ||
                    purchasedAmount <= 0)
                {
                    continue;
                }
            }

            if (purchasedAmount <= 0)
            {
                continue;
            }

            boughtAny = true;
            boughtKinds++;
            LogDebug(
                "BuyVillageRestock",
                item.itemName +
                " amount=" + purchasedAmount +
                " price=" + totalPrice);
        }

        return boughtAny;
    }

    List<StatItemData> GetRestockCandidates()
    {
        List<StatItemData> result =
            new List<StatItemData>();
        HashSet<StatItemData> seen =
            new HashSet<StatItemData>();

        if (restockItems != null &&
            restockItems.Length > 0)
        {
            for (int i = 0; i < restockItems.Length; i++)
            {
                AddRestockCandidate(
                    restockItems[i],
                    seen,
                    result);
            }
        }

        if (result.Count == 0 &&
            autoRestockFromDestinationShop &&
            destinationShop != null &&
            destinationShop.items != null)
        {
            for (int i = 0; i < destinationShop.items.Count; i++)
            {
                ShopItemSlot slot = destinationShop.items[i];
                AddRestockCandidate(
                    slot != null
                        ? slot.item
                        : null,
                    seen,
                    result);
            }
        }

        result.Sort(CompareRestockPriority);
        return result;
    }

    void AddRestockCandidate(
        StatItemData item,
        HashSet<StatItemData> seen,
        List<StatItemData> result)
    {
        if (item == null ||
            seen.Contains(item) ||
            item.grade != ItemGrade.Ha ||
            !NpcEconomy.CanTradeNormally(item) ||
            !CanRestockItemType(item))
        {
            return;
        }

        seen.Add(item);
        result.Add(item);
    }

    bool CanRestockItemType(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        switch (item.itemType)
        {
            case ItemType.DanDuoc:
                return restockLowGradePills;

            case ItemType.ThucPham:
                return restockLowGradeFood;

            case ItemType.VatLieu:
                return restockLowGradeMaterials;

            case ItemType.PhapBao:
                return restockLowGradeEquipment;

            case ItemType.CongPhap:
                return restockLowGradeManuals;
        }

        return false;
    }

    int CompareRestockPriority(
        StatItemData a,
        StatItemData b)
    {
        int amountA =
            inventory != null && a != null
                ? inventory.GetAmount(a)
                : 0;
        int amountB =
            inventory != null && b != null
                ? inventory.GetAmount(b)
                : 0;
        int amountCompare =
            amountA.CompareTo(amountB);
        if (amountCompare != 0)
        {
            return amountCompare;
        }

        int valueCompare =
            NpcEconomy.GetItemValue(a).CompareTo(
                NpcEconomy.GetItemValue(b));
        if (valueCompare != 0)
        {
            return valueCompare;
        }

        return string.Compare(
            a != null ? a.itemName : "",
            b != null ? b.itemName : "",
            System.StringComparison.Ordinal);
    }

    bool ContainsRestockItem(StatItemData item)
    {
        if (item == null ||
            !restockLowGradeGoods)
        {
            return false;
        }

        List<StatItemData> candidates =
            GetRestockCandidates();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == item)
            {
                return true;
            }
        }

        return false;
    }

    bool MoveTowards(
        Vector3 targetPosition,
        string action,
        NpcMapZone? forcedTargetZone = null)
    {
        Vector3 current = transform.position;
        Vector3 delta = targetPosition - current;
        if (delta.sqrMagnitude > 0.0001f)
        {
            visualDirection =
                ((Vector2)delta).normalized;
        }

        bool usingTeleportRoute;
        string routeAction;
        NpcRouteStatus routeStatus;
        Vector3 moveTarget =
            NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                targetPosition,
                forcedTargetZone,
                out usingTeleportRoute,
                out routeAction,
                out routeStatus);

        if (routeStatus == NpcRouteStatus.NoGate ||
            routeStatus == NpcRouteStatus.InvalidGate)
        {
            // Scene route metadata can be incomplete for shop/market anchors.
            // Do not let the merchant freeze for the whole trade window.
            LogDebug(
                "MoveTowards",
                "routeStatus=" + routeStatus + " fallback=Direct");
            usingTeleportRoute = false;
            routeAction = string.Empty;
            moveTarget = targetPosition;
        }

        SetAction(
            usingTeleportRoute &&
            !string.IsNullOrEmpty(routeAction)
                ? routeAction
                : action);

        if (usingTeleportRoute &&
            TryForceTeleportRouteUseNearTarget(
                moveTarget,
                forcedTargetZone))
        {
            return true;
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                moveTarget,
                moveSpeed * Time.deltaTime);

        if (usingTeleportRoute &&
            TryForceTeleportRouteUseNearTarget(
                moveTarget,
                forcedTargetZone))
        {
            return true;
        }

        return true;
    }

    bool TryForceTeleportRouteUseNearTarget(
        Vector3 moveTarget,
        NpcMapZone? forcedTargetZone)
    {
        if (!forcedTargetZone.HasValue)
        {
            return false;
        }

        NpcMapZone? currentZone =
            NpcMapNavigator.ResolveActorZone(gameObject);
        if (!currentZone.HasValue ||
            currentZone.Value == forcedTargetZone.Value)
        {
            return false;
        }

        for (int i = 0; i < NpcTeleportGate.Gates.Count; i++)
        {
            NpcTeleportGate gate =
                NpcTeleportGate.Gates[i];
            if (gate == null ||
                !gate.TryGetTeleportRouteForZone(
                    currentZone.Value,
                    out Vector3 gateApproach,
                    out _,
                    out _))
            {
                continue;
            }

            if (Vector2.Distance(
                    gateApproach,
                    moveTarget) > 0.25f)
            {
                continue;
            }

            float distanceToGate =
                Vector2.Distance(
                    transform.position,
                    gateApproach);
            if (distanceToGate > Mathf.Max(
                    arriveDistance,
                    gate.npcAutoUseRadius))
            {
                continue;
            }

            if (!gate.TryForceNpcUse(gameObject))
            {
                continue;
            }

            LogDebug(
                "GateRoute",
                "forcedTeleport gate=" +
                gate.name +
                " currentZone=" +
                currentZone.Value +
                " moveTarget=" +
                moveTarget);
            return true;
        }

        return false;
    }

    void SetAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        NpcRoleUtility.SetAction(
            gameObject,
            action);

        if (villager != null)
        {
            villager.SetCurrentActionState(
                NpcActionState.FromDisplayText(action));
        }
    }

    void SetIdleAction()
    {
        SetAction(
            currentPhase == DailyMerchantPhase.LocalMarket
                ? NpcText.Action("trading")
                : NpcText.Action("idle"));
    }

    void UpdateVisualAnimation(bool preferIdle)
    {
        if (visualAnimation == null)
        {
            lastPosition = transform.position;
            return;
        }

        Vector3 delta =
            transform.position - lastPosition;
        if (delta.sqrMagnitude > 0.0001f)
        {
            visualDirection =
                ((Vector2)delta).normalized;
        }

        bool isIdle =
            preferIdle ||
            delta.sqrMagnitude <= 0.0001f;
        visualAnimation.UpdateNPCAnimation(
            isIdle
                ? Vector2.zero
                : visualDirection,
            isIdle,
            ResolveVisualAction());
        lastPosition = transform.position;
    }

    string ResolveVisualAction()
    {
        string action =
            ResolveCurrentActionText();
        if (!string.IsNullOrWhiteSpace(action) &&
            action != NpcText.Action("idle"))
        {
            return action;
        }

        switch (currentPhase)
        {
            case DailyMerchantPhase.VanBaoLauTrade:
                return NpcText.Action("goVanBaoLauBroker");

            case DailyMerchantPhase.LocalMarket:
                return NpcText.Action("trading");

            default:
                return NpcText.Action("idle");
        }
    }

    string ResolveCurrentActionText()
    {
        VillagerAI actionVillager =
            villager != null
                ? villager
                : GetComponent<VillagerAI>();
        if (actionVillager != null &&
            !string.IsNullOrWhiteSpace(actionVillager.currentAction))
        {
            return actionVillager.currentAction;
        }

        SmartNpcAI smartNpc =
            GetComponent<SmartNpcAI>();
        if (smartNpc != null &&
            !string.IsNullOrWhiteSpace(smartNpc.currentAction))
        {
            return smartNpc.currentAction;
        }

        NpcData npcData =
            GetComponent<NpcData>();
        return npcData != null
            ? npcData.currentAction
            : "";
    }

    void SetLocalBrokerRequests(bool enabled)
    {
        if (localBroker == null)
        {
            return;
        }

        localBroker.receiveAllNpcRequests = enabled;
    }

    void SetLocalBrokerStationary(bool enabled)
    {
        if (localBroker == null)
        {
            return;
        }

        localBroker.SetStationaryMode(enabled);
    }

    NpcMapZone? ResolveShopZone(
        SimpleItemShop shop)
    {
        if (shop == null)
        {
            return null;
        }

        NpcCounterBroker shopBroker =
            ResolveDestinationBroker(shop);
        if (shopBroker != null &&
            shopBroker.customerPoint != null)
        {
            NpcMapZone? brokerZone =
                NpcMapNavigator.GetDestinationZone(
                    shopBroker.customerPoint);
            if (brokerZone.HasValue)
            {
                return brokerZone;
            }
        }

        if (shop.sellerObject != null)
        {
            NpcMapZone? sellerZone =
                NpcMapNavigator.GetDestinationZone(
                    shop.sellerObject.transform);
            if (sellerZone.HasValue)
            {
                return sellerZone;
            }
        }

        if (shop.sellerInventory != null)
        {
            NpcMapZone? inventoryZone =
                NpcMapNavigator.GetDestinationZone(
                    shop.sellerInventory.transform);
            if (inventoryZone.HasValue)
            {
                return inventoryZone;
            }
        }

        NpcMapZone? shopZone =
            NpcMapNavigator.GetDestinationZone(
                shop.transform);
        if (shopZone.HasValue)
        {
            return shopZone;
        }

        NpcMapArea area =
            NpcMapArea.FindArea(shop.transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(
                shop.transform.position);
        }

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem =
            WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.FloorToInt(Time.time / 900f);
    }

    bool IsHourInRange(
        float hour,
        float startHour,
        float endHour)
    {
        float start = Mathf.Repeat(startHour, 24f);
        float end = Mathf.Repeat(endHour, 24f);

        if (Mathf.Approximately(start, end))
        {
            return true;
        }

        if (start < end)
        {
            return hour >= start &&
                hour < end;
        }

        return hour >= start ||
            hour < end;
    }

    bool TryGetLocalMarketPosition(
        out Vector3 position,
        out NpcMapZone? marketZone)
    {
        position = transform.position;
        marketZone = null;

        if (IsExternalMarketAnchor(marketPoint))
        {
            position = marketPoint.position;
            marketZone =
                ResolvePointZone(marketPoint) ??
                NpcMapZone.Lang;
            return true;
        }

        if (localBroker != null)
        {
            if (IsExternalMarketAnchor(
                    localBroker.brokerStandPoint))
            {
                position = localBroker.brokerStandPoint.position;
                marketZone =
                    ResolvePointZone(localBroker.brokerStandPoint) ??
                    NpcMapZone.Lang;
                return true;
            }

            if (IsExternalMarketAnchor(
                    localBroker.customerPoint))
            {
                position =
                    localBroker.GetCustomerPositionFor(
                        gameObject);
                marketZone =
                    ResolvePointZone(localBroker.customerPoint) ??
                    NpcMapZone.Lang;
                return true;
            }
        }

        return false;
    }

    NpcMapZone? ResolvePointZone(Transform point)
    {
        if (point == null)
        {
            return null;
        }

        NpcMapZone? zone =
            NpcMapNavigator.GetDestinationZone(point);
        if (zone.HasValue)
        {
            return zone;
        }

        NpcMapArea area =
            NpcMapArea.FindArea(point.position);
        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(point.position);
        }

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    bool IsExternalMarketAnchor(Transform point)
    {
        return point != null &&
            point != transform &&
            !point.IsChildOf(transform);
    }

    bool ShouldHideAtCurrentHour()
    {
        if (!hideAtHome)
        {
            return false;
        }

        float hour = GetCurrentWorldHour();
        return hour >= middayHideHour ||
            hour < vanBaoLauStartHour;
    }

    void CacheVisibilityTargets()
    {
        if (cachedRenderers == null ||
            cachedRenderers.Length == 0)
        {
            cachedRenderers =
                GetComponentsInChildren<Renderer>(true);
        }

        if (cachedColliders == null ||
            cachedColliders.Length == 0)
        {
            cachedColliders =
                GetComponentsInChildren<Collider2D>(true);
        }
    }

    void SetHiddenAtHome(bool hidden)
    {
        if (isHiddenAtHome == hidden)
        {
            return;
        }

        isHiddenAtHome = hidden;

        if (villager != null)
        {
            villager.ForceHiddenAtHome(hidden);
        }
        else
        {
            CacheVisibilityTargets();

            if (cachedRenderers != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    Renderer renderer = cachedRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.enabled = !hidden;
                }
            }

            if (cachedColliders != null)
            {
                for (int i = 0; i < cachedColliders.Length; i++)
                {
                    Collider2D collider = cachedColliders[i];
                    if (collider == null)
                    {
                        continue;
                    }

                    collider.enabled = !hidden;
                }
            }
        }

        LogDebug(
            "SetHiddenAtHome",
            "hidden=" + (hidden ? 1 : 0));
    }

    void ApplyVillagerLifecycleOwnership(bool owned)
    {
        if (villager == null)
        {
            return;
        }

        villager.homeRoutineManagedExternally = owned;

        if (owned)
        {
            villager.StopMoving();
        }
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem =
            WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentHour
            : Mathf.Repeat(Time.time * 24f / 900f, 24f);
    }

    void LogDebug(string stage, string detail)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log(
            "[NpcVillageSupplyMerchant] " +
            name +
            " stage=" + stage +
            " detail=" + detail,
            this);
    }

    bool HasEconomyWallet()
    {
        return GetComponent<VillagerAI>() != null ||
            GetComponent<SmartNpcAI>() != null;
    }
}
