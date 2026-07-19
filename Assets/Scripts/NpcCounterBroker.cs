using System.Collections.Generic;
using UnityEngine;

public class NpcCounterBroker : MonoBehaviour
{
    public static NpcCounterBroker Active { get; private set; }
    static readonly List<NpcCounterBroker> activeBrokers =
        new List<NpcCounterBroker>();

    [Header("Broker")]
    public ItemInventory inventory;
    public bool receiveAllNpcRequests = true;
    public bool receiveVillagerRequests = true;
    public bool receiveSmartNpcRequests = true;
    public bool buyGoodsFromNpcs = true;
    public bool sellUsefulItemsToNpcs = true;
    public bool acceptAllMaterials = true;
    public bool acceptAllSellableItems = true;
    public StatItemData[] acceptedItems;
    public int maxUnitsPerRequest = 4;
    public int maxTransactionsPerVisit = 3;
    public float noDealCooldownMultiplier = 0.4f;
    public bool useSpiritStoneCurrency = true;

    [Header("Counter Placement")]
    public bool keepBrokerStationary = true;
    public Transform brokerStandPoint;
    public Transform customerPoint;
    public float customerArriveDistance = 0.45f;
    public bool allowMultipleCustomers = true;
    public float multiCustomerServiceRadius = 1.4f;
    public float multiCustomerStandRadius = 0.65f;
    public bool requireCustomerAtPoint = false;
    public bool disableBaseAiWhileStationary = true;
    public bool hardFreezeRigidbodyWhileStationary = true;
    public bool useKinematicBodyWhileStationary = true;
    public bool disableBoundaryClampWhileStationary = true;
    public bool disableMovementAnimatorWhileStationary = true;
    public bool debugCounterChecks = false;
    public bool forceCustomerZoneTrigger = true;

    [Header("Wallet")]
    [InspectorName("Linh Thạch ban đầu")]
    public int startingMoney = 100000;
    [InspectorName("Dự trữ Linh Thạch tối thiểu")]
    public int minimumMoneyReserve = 50000;
    public bool refillMoneyWhenLow = true;
    [SerializeField, InspectorName("Linh Thạch dịch vụ")] int serviceMoney;
    [SerializeField, InspectorName("Linh Thạch dịch vụ")] int serviceSpiritStone;

    Rigidbody2D rb;
    Vector3 stationaryPosition;
    RigidbodyConstraints2D originalConstraints;
    RigidbodyType2D originalBodyType;
    bool capturedRigidbodySettings;
    bool lastKeepBrokerStationary;

    public int CurrentMoney => GetBrokerMoney();

    NpcInteractionPoint GetInteractionPoint(Transform point)
    {
        if (point == null)
        {
            return null;
        }

        NpcInteractionPoint interactionPoint =
            point.GetComponent<NpcInteractionPoint>();
        if (interactionPoint == null && Application.isPlaying)
        {
            interactionPoint =
                point.gameObject.AddComponent<NpcInteractionPoint>();
            interactionPoint.interactionRadius =
                Mathf.Max(0.45f, customerArriveDistance);
            interactionPoint.standSpacing =
                Mathf.Max(0.85f, multiCustomerStandRadius);
            interactionPoint.reservationSpacingRadius = 0.75f;
            interactionPoint.reservationHoldSeconds = 15f;
        }

        return interactionPoint;
    }

    void LogCounterDebug(string stage, string detail, Object context = null)
    {
        if (!debugCounterChecks)
        {
            return;
        }

        Debug.LogWarning(
            "[NpcCounterBroker] " +
            name +
            " stage=" + stage +
            " detail=" + detail,
            context != null ? context : this);
    }

    public BoxCollider2D GetCustomerZoneCollider()
    {
        if (customerPoint == null)
        {
            LogCounterDebug("GetCustomerZoneCollider", "customerPoint=null");
            return null;
        }

        BoxCollider2D box = customerPoint.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            LogCounterDebug(
                "GetCustomerZoneCollider",
                "source=customerPoint box=" + box.name +
                " enabled=" + (box.enabled ? 1 : 0) +
                " center=" + box.bounds.center +
                " size=" + box.bounds.size,
                box);
            return box;
        }

        NpcInteractionPoint interactionPoint =
            GetInteractionPoint(customerPoint);
        if (interactionPoint != null)
        {
            box = interactionPoint.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                LogCounterDebug(
                    "GetCustomerZoneCollider",
                    "source=interactionPoint box=" + box.name +
                    " enabled=" + (box.enabled ? 1 : 0) +
                    " center=" + box.bounds.center +
                    " size=" + box.bounds.size,
                    box);
                return box;
            }

            box = interactionPoint.GetComponentInParent<BoxCollider2D>();
            LogCounterDebug(
                "GetCustomerZoneCollider",
                box != null
                    ? "source=interactionPointParent box=" + box.name +
                        " enabled=" + (box.enabled ? 1 : 0) +
                        " center=" + box.bounds.center +
                        " size=" + box.bounds.size
                    : "source=interactionPointParent box=null",
                box != null ? box : interactionPoint);
            return box;
        }

        box = customerPoint.GetComponentInParent<BoxCollider2D>();
        LogCounterDebug(
            "GetCustomerZoneCollider",
            box != null
                ? "source=customerPointParent box=" + box.name +
                    " enabled=" + (box.enabled ? 1 : 0) +
                    " center=" + box.bounds.center +
                    " size=" + box.bounds.size
                : "source=customerPointParent box=null",
            box != null ? box : customerPoint);
        return box;
    }

    [Header("Pricing")]
    public NpcTradeContext sellToNpcContext =
        NpcTradeContext.CounterBrokerBuy;
    public NpcTradeContext buyFromNpcContext =
        NpcTradeContext.CounterBrokerSell;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        EnsureInventory();
        CaptureStationaryPosition();
        ConfigureStationaryBroker();
        lastKeepBrokerStationary = keepBrokerStationary;
    }

    void OnEnable()
    {
        Active = this;
        if (!activeBrokers.Contains(this))
        {
            activeBrokers.Add(this);
        }
        EnsureInventory();
        CaptureStationaryPosition();
        ConfigureStationaryBroker();
        lastKeepBrokerStationary = keepBrokerStationary;
    }

    void Start()
    {
        CaptureStationaryPosition();
        ConfigureStationaryBroker();
        lastKeepBrokerStationary = keepBrokerStationary;
        KeepBrokerAtStation();
    }

    void FixedUpdate()
    {
        UpdateStationaryMode();
        KeepBrokerAtStation();
    }

    void LateUpdate()
    {
        UpdateStationaryMode();
        KeepBrokerAtStation();
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        if (!keepBrokerStationary)
        {
            return;
        }

        keepBrokerStationary = false;
        ReleaseStationaryBrokerLock();
        lastKeepBrokerStationary = keepBrokerStationary;
    }

    void OnDisable()
    {
        activeBrokers.Remove(this);
        if (Active == this)
        {
            Active = null;
        }
    }

    public static NpcCounterBroker FindBestBrokerForNpc(
        GameObject npc,
        bool requireRequests = true)
    {
        if (npc == null)
        {
            return requireRequests &&
                Active != null &&
                !Active.receiveAllNpcRequests
                ? null
                : Active;
        }

        NpcMapZone? actorZone =
            NpcMapNavigator.ResolveActorZone(npc);
        if (!actorZone.HasValue)
        {
            NpcMapArea actorArea =
                NpcMapArea.FindArea(npc.transform.position);
            if (actorArea == null)
            {
                actorArea = NpcMapArea.FindNearestArea(
                    npc.transform.position);
            }

            if (actorArea != null)
            {
                actorZone = actorArea.zone;
            }
        }

        NpcCounterBroker bestSameZone = null;
        float bestSameZoneDistance = float.PositiveInfinity;
        NpcCounterBroker bestAnyZone = null;
        float bestAnyZoneDistance = float.PositiveInfinity;

        for (int i = 0; i < activeBrokers.Count; i++)
        {
            NpcCounterBroker broker = activeBrokers[i];
            if (broker == null ||
                !broker.isActiveAndEnabled ||
                !broker.gameObject.activeInHierarchy ||
                (requireRequests &&
                !broker.receiveAllNpcRequests) ||
                !broker.CanReceiveRequestFrom(npc))
            {
                continue;
            }

            if (npc.GetComponent<VillagerAI>() != null &&
                broker.IsVanBaoLauOnlyBroker())
            {
                continue;
            }

            Vector3 brokerPosition =
                broker.GetCustomerPositionFor(npc);
            float distance =
                Vector2.Distance(
                    npc.transform.position,
                    brokerPosition);
            NpcMapZone? brokerZone =
                broker.ResolveBrokerZone();

            if (actorZone.HasValue &&
                brokerZone.HasValue &&
                actorZone.Value == brokerZone.Value &&
                distance < bestSameZoneDistance)
            {
                bestSameZone = broker;
                bestSameZoneDistance = distance;
            }

            if (distance < bestAnyZoneDistance)
            {
                bestAnyZone = broker;
                bestAnyZoneDistance = distance;
            }
        }

        if (npc.GetComponent<VillagerAI>() != null)
        {
            return bestSameZone;
        }

        return bestSameZone != null
            ? bestSameZone
            : bestAnyZone;
    }


    public Vector3 CustomerPosition
    {
        get
        {
            NpcInteractionPoint interactionPoint =
                GetInteractionPoint(customerPoint);

            if (interactionPoint != null)
            {
                return interactionPoint.transform.position;
            }

            BoxCollider2D customerZone =
                GetCustomerZoneCollider();
            if (customerZone != null &&
                customerZone.enabled)
            {
                Vector3 center = customerZone.bounds.center;
                center.z = customerPoint != null
                    ? customerPoint.position.z
                    : transform.position.z;
                return center;
            }

            return customerPoint != null
                ? customerPoint.position
                : transform.position;
        }
    }

    public float CustomerServiceRadius
    {
        get
        {
            NpcInteractionPoint interactionPoint =
                GetInteractionPoint(customerPoint);
            float pointRadius =
                interactionPoint != null
                ? interactionPoint.interactionRadius
                : customerArriveDistance;

            if (requireCustomerAtPoint)
            {
                return pointRadius;
            }

            return allowMultipleCustomers
                ? Mathf.Max(pointRadius, multiCustomerServiceRadius)
                : pointRadius;
        }
    }

    public Vector3 GetCustomerPositionFor(GameObject npc)
    {
        NpcInteractionPoint interactionPoint =
            GetInteractionPoint(customerPoint);
        BoxCollider2D customerZone =
            GetCustomerZoneCollider();

        if (requireCustomerAtPoint)
        {
            if (interactionPoint != null)
            {
                if (customerZone != null &&
                    customerZone.enabled)
                {
                    return interactionPoint.GetStandPositionFor(npc);
                }

                return interactionPoint.transform.position;
            }

            if (customerZone != null &&
                customerZone.enabled)
            {
                return GetCustomerStandPositionInZone(
                    customerZone,
                    npc);
            }

            return CustomerPosition;
        }

        if (interactionPoint != null)
        {
            return interactionPoint.GetStandPositionFor(npc);
        }

        if (customerZone != null &&
            customerZone.enabled)
        {
            return GetCustomerStandPositionInZone(
                customerZone,
                npc);
        }

        Vector3 center = CustomerPosition;
        if (!allowMultipleCustomers ||
            npc == null ||
            multiCustomerStandRadius <= 0.01f)
        {
            return center;
        }

        int hash = Mathf.Abs(npc.name.GetHashCode());
        float angle = (hash % 360) * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
            Mathf.Max(0f, multiCustomerStandRadius);

        return center + (Vector3)offset;
    }

    Vector3 GetCustomerStandPositionInZone(
        BoxCollider2D customerZone,
        GameObject npc)
    {
        Bounds bounds = customerZone.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        float marginX = Mathf.Clamp(
            extents.x * 0.12f,
            0.05f,
            Mathf.Max(0.05f, extents.x - 0.02f));
        float marginY = Mathf.Clamp(
            extents.y * 0.12f,
            0.05f,
            Mathf.Max(0.05f, extents.y - 0.02f));

        float minX = center.x - extents.x + marginX;
        float maxX = center.x + extents.x - marginX;
        float minY = center.y - extents.y + marginY;
        float maxY = center.y + extents.y - marginY;

        if (minX > maxX)
        {
            minX = maxX = center.x;
        }

        if (minY > maxY)
        {
            minY = maxY = center.y;
        }

        float standX = Mathf.Clamp(center.x, minX, maxX);
        float standY = Mathf.Clamp(center.y, minY, maxY);

        if (allowMultipleCustomers && npc != null)
        {
            float width = Mathf.Max(0.01f, maxX - minX);
            float height = Mathf.Max(0.01f, maxY - minY);
            int hash = Mathf.Abs(
                UnityObjectIdUtility.GetRuntimeId(npc));
            float normalized =
                ((hash % 1000) / 999f);

            if (width >= height)
            {
                standX = Mathf.Lerp(
                    minX + width * 0.15f,
                    maxX - width * 0.15f,
                    normalized);
            }
            else
            {
                standY = Mathf.Lerp(
                    minY + height * 0.15f,
                    maxY - height * 0.15f,
                    normalized);
            }
        }

        Vector3 standPosition = new Vector3(
            standX,
            standY,
            customerPoint != null
                ? customerPoint.position.z
                : transform.position.z);

        return standPosition;
    }

    public bool IsCustomerAtCounter(GameObject npc)
    {
        if (npc == null)
        {
            LogCounterDebug("IsCustomerAtCounter", "npc=null");
            return false;
        }

        BoxCollider2D customerZone = GetCustomerZoneCollider();
        if (customerZone != null &&
            customerZone.enabled)
        {
            bool inside =
                customerZone.OverlapPoint(npc.transform.position);
            LogCounterDebug(
                "IsCustomerAtCounter",
                "npc=" + npc.name +
                " mode=zone" +
                " npcPos=" + npc.transform.position +
                " zoneCenter=" + customerZone.bounds.center +
                " zoneSize=" + customerZone.bounds.size +
                " inside=" + (inside ? 1 : 0),
                npc);
            return inside;
        }

        float serviceRadius =
            Mathf.Max(0.05f, CustomerServiceRadius);
        float distance = Vector2.Distance(
            npc.transform.position,
            CustomerPosition);
        bool withinDistance = distance <= serviceRadius;
        LogCounterDebug(
            "IsCustomerAtCounter",
            "npc=" + npc.name +
            " mode=distance" +
            " npcPos=" + npc.transform.position +
            " customerPos=" + CustomerPosition +
            " distance=" + distance.ToString("0.00") +
            " radius=" + serviceRadius.ToString("0.00") +
            " within=" + (withinDistance ? 1 : 0) +
            " customerPoint=" +
            (customerPoint != null
                ? customerPoint.position.ToString()
                : "none") +
            " requireAtPoint=" + (requireCustomerAtPoint ? 1 : 0) +
            " allowMultiple=" + (allowMultipleCustomers ? 1 : 0),
            npc);
        return withinDistance;
    }
    public static bool TryTradeWithActiveBroker(NpcTradeAgent npc)
    {
        if (npc == null)
        {
            return false;
        }

        NpcCounterBroker broker =
            FindBestBrokerForNpc(
                npc.gameObject,
                true);
        return broker != null &&
            broker.TryTradeWithNpc(npc);
    }

    public static bool TryTradeWithBestBroker(NpcTradeAgent npc)
    {
        if (npc == null)
        {
            return false;
        }

        NpcCounterBroker broker =
            FindBestBrokerForNpc(
                npc.gameObject,
                true);
        return broker != null &&
            broker.TryTradeWithNpc(npc);
    }


    public bool CanTradeWithNpc(NpcTradeAgent npc)
    {
        EnsureInventory();

        if (npc == null ||
            npc.gameObject == gameObject ||
            npc.inventory == null ||
            inventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(npc.gameObject) ||
            !npc.CanUseCounterTrade())
        {
            return false;
        }

        if (npc.GetComponent<VillagerAI>() != null &&
            IsVanBaoLauOnlyBroker())
        {
            return false;
        }

        if (!CanReceiveRequestFrom(npc.gameObject))
        {
            return false;
        }

        return CanSellUsefulItemTo(npc) ||
            CanBuyItemFromNpc(npc);
    }

    bool CanSellUsefulItemTo(NpcTradeAgent buyer)
    {
        if (!sellUsefulItemsToNpcs ||
            buyer == null ||
            buyer.inventory == null ||
            inventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(buyer.gameObject) ||
            buyer.GetMoney() <= 0)
        {
            return false;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.CanUseOn(buyer.gameObject))
            {
                continue;
            }

            int price = NpcEconomy.GetNpcBuyPrice(
                stack.item,
                buyer.gameObject,
                sellToNpcContext);

            if (buyer.GetBuyScore(stack.item, price) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    bool CanBuyItemFromNpc(NpcTradeAgent seller)
    {
        if (!buyGoodsFromNpcs ||
            seller == null ||
            seller.inventory == null ||
            inventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(seller.gameObject))
        {
            return false;
        }

        foreach (ItemStack stack in seller.inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !AcceptsItem(stack.item) ||
                !seller.ShouldSellToCounter(stack.item))
            {
                continue;
            }

            int price = NpcEconomy.GetTradePrice(
                stack.item,
                buyFromNpcContext);

            if (GetBrokerMoney() >= price || refillMoneyWhenLow)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryTradeWithNpc(NpcTradeAgent npc)
    {
        if (npc == null ||
            npc.gameObject == gameObject)
        {
            return false;
        }

        if (IsBusyForTrade(gameObject) ||
            IsBusyForTrade(npc.gameObject))
        {
            return false;
        }

        if (!IsCustomerAtCounter(npc.gameObject))
        {
            return false;
        }

        if (!npc.CanUseCounterTrade())
        {
            return false;
        }

        int transactions = 0;
        int maxTransactions = Mathf.Max(1, maxTransactionsPerVisit);

        if (sellUsefulItemsToNpcs)
        {
            while (transactions < maxTransactions &&
                TrySellUsefulItemTo(npc))
            {
                transactions++;
            }
        }

        if (buyGoodsFromNpcs &&
            transactions < maxTransactions &&
            TryBuyItemFromNpc(npc))
        {
            transactions++;
        }

        npc.MarkCounterTradeHandled(
            transactions > 0 ? 1f : noDealCooldownMultiplier);

        return transactions > 0;
    }
    public bool TrySellUsefulItemTo(NpcTradeAgent buyer)
    {
        EnsureInventory();

        if (buyer == null ||
            buyer.inventory == null ||
            inventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(buyer.gameObject))
        {
            return false;
        }

        StatItemData itemToSell = null;
        int priceToPay = 0;
        float bestScore = 0f;

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.CanUseOn(buyer.gameObject))
            {
                continue;
            }

            int price =
                NpcEconomy.GetNpcBuyPrice(
                    stack.item,
                    buyer.gameObject,
                    sellToNpcContext);

            if (buyer.GetMoney() < price)
            {
                continue;
            }

            float score = buyer.GetBuyScore(stack.item, price);

            if (score <= bestScore)
            {
                continue;
            }

            itemToSell = stack.item;
            priceToPay = price;
            bestScore = score;
        }

        if (itemToSell == null ||
            !RemoveBrokerItem(itemToSell, 1))
        {
            return false;
        }

        buyer.AddMoney(-priceToPay);
        AddBrokerMoney(priceToPay);
        buyer.ReceiveBoughtItem(itemToSell);
        NpcSocialEventBus.PublishTradeCompleted(
            buyer.gameObject,
            gameObject,
            itemToSell,
            priceToPay);

        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Sold,
            itemToSell,
            gameObject,
            buyer.gameObject);

        return true;
    }

    public bool CanSellUsefulItemTo(VillagerAI buyer)
    {
        EnsureInventory();

        if (!sellUsefulItemsToNpcs ||
            buyer == null ||
            buyer.inventory == null ||
            inventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(buyer.gameObject))
        {
            return false;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.CanUseOn(buyer.gameObject))
            {
                continue;
            }

            int price = NpcEconomy.GetNpcBuyPrice(
                stack.item,
                buyer.gameObject,
                sellToNpcContext);

            if (GetBuyScoreForNpc(
                    stack.item,
                    buyer.gameObject,
                    price,
                    buyer.spiritStone) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    public bool TrySellUsefulItemTo(VillagerAI buyer)
    {
        EnsureInventory();

        if (!sellUsefulItemsToNpcs ||
            buyer == null ||
            buyer.inventory == null ||
            inventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(buyer.gameObject))
        {
            return false;
        }

        ItemStack bestStack = null;
        int bestPrice = 0;
        float bestScore = 0f;

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.CanUseOn(buyer.gameObject))
            {
                continue;
            }

            int price = NpcEconomy.GetNpcBuyPrice(
                stack.item,
                buyer.gameObject,
                sellToNpcContext);

            float score =
                GetBuyScoreForNpc(
                    stack.item,
                    buyer.gameObject,
                    price,
                    buyer.spiritStone);

            if (score <= bestScore)
            {
                continue;
            }

            bestStack = stack;
            bestPrice = price;
            bestScore = score;
        }

        if (bestStack == null ||
            buyer.spiritStone < bestPrice ||
            !RemoveBrokerItem(bestStack.item, 1))
        {
            return false;
        }

        buyer.spiritStone = Mathf.Max(0, buyer.spiritStone - bestPrice);
        if (buyer.entityProfile != null)
        {
            buyer.entityProfile.stats.spiritStone = buyer.spiritStone;
        }
        AddBrokerMoney(bestPrice);
        buyer.inventory.AddItem(bestStack.item, 1);
        NpcSocialEventBus.PublishTradeCompleted(
            buyer.gameObject,
            gameObject,
            bestStack.item,
            bestPrice);

        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Sold,
            bestStack.item,
            gameObject,
            buyer.gameObject);

        return true;
    }

    public bool CanBuyProduceFrom(
        VillagerAI seller,
        ItemInventory sellerInventory)
    {
        if (!buyGoodsFromNpcs ||
            seller == null ||
            sellerInventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(seller.gameObject))
        {
            return false;
        }

        EnsureInventory();

        if (inventory == null)
        {
            return false;
        }

        foreach (ItemStack stack in sellerInventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !AcceptsItem(stack.item))
            {
                continue;
            }

            int unitPrice =
                NpcEconomy.GetTradePrice(
                    stack.item,
                    buyFromNpcContext);

            if (GetBrokerMoney() >= unitPrice ||
                refillMoneyWhenLow)
            {
                return true;
            }
        }

        return false;
    }
    public bool TryBuyProduceFrom(
        VillagerAI seller,
        ItemInventory sellerInventory)
    {
        if (!buyGoodsFromNpcs ||
            seller == null ||
            sellerInventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(seller.gameObject))
        {
            return false;
        }

        EnsureInventory();

        if (inventory == null)
        {
            return false;
        }

        int boughtUnits = 0;

        for (int i = sellerInventory.items.Count - 1; i >= 0; i--)
        {
            if (boughtUnits >= maxUnitsPerRequest)
            {
                break;
            }

            ItemStack stack =
                sellerInventory.items[i];

            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !AcceptsItem(stack.item))
            {
                continue;
            }

            int amount =
                Mathf.Min(
                    stack.amount,
                    maxUnitsPerRequest - boughtUnits);

            int unitPrice =
                NpcEconomy.GetTradePrice(
                    stack.item,
                    buyFromNpcContext);

            int totalPrice =
                unitPrice * amount;

            if (GetBrokerMoney() < totalPrice)
            {
                EnsureMoney(totalPrice);
            }

            if (GetBrokerMoney() < totalPrice)
            {
                continue;
            }

            if (!sellerInventory.RemoveItem(stack.item, amount))
            {
                continue;
            }

            AddBrokerMoney(-totalPrice);
            seller.spiritStone += totalPrice;
            if (seller.entityProfile != null)
            {
                seller.entityProfile.stats.spiritStone = seller.spiritStone;
            }
            inventory.AddItem(stack.item, amount);
            NpcSocialEventBus.PublishTradeCompleted(
                gameObject,
                seller.gameObject,
                stack.item,
                totalPrice);

            ItemLifecycleSystem.Notify(
                ItemLifecycleEventType.Sold,
                stack.item,
                seller.gameObject,
                gameObject);

            boughtUnits += amount;
        }

        return boughtUnits > 0;
    }

    bool TryBuyItemFromNpc(NpcTradeAgent seller)
    {
        if (seller == null ||
            seller.inventory == null ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(seller.gameObject))
        {
            return false;
        }

        EnsureInventory();

        foreach (ItemStack stack in seller.inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !AcceptsItem(stack.item) ||
                !seller.ShouldSellToCounter(stack.item))
            {
                continue;
            }

            int price =
                NpcEconomy.GetTradePrice(
                    stack.item,
                    buyFromNpcContext);

            if (GetBrokerMoney() < price)
            {
                EnsureMoney(price);
            }

            if (GetBrokerMoney() < price ||
                !seller.RemoveOwnedItem(
                    stack.item,
                    1,
                    ItemLifecycleEventType.Sold))
            {
                continue;
            }

            AddBrokerMoney(-price);
            seller.AddMoney(price);
            ItemEffectSpawner.PlayPickupEffect(stack.item, gameObject.transform);
            inventory.AddItem(stack.item, 1);
            NpcSocialEventBus.PublishTradeCompleted(
                gameObject,
                seller.gameObject,
                stack.item,
                price);

            return true;
        }

        return false;
    }

    bool AcceptsItem(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (acceptAllSellableItems &&
            item.canBeSold)
        {
            return true;
        }

        if (acceptAllMaterials &&
            (item.itemType == ItemType.VatLieu ||
            item.itemType == ItemType.ThucPham))
        {
            return true;
        }

        if (acceptedItems == null)
        {
            return false;
        }

        foreach (StatItemData accepted in acceptedItems)
        {
            if (accepted == item)
            {
                return true;
            }
        }

        return false;
    }

    bool IsBusyForTrade(GameObject target)
    {
        return target == null ||
            NpcRoleUtility.IsDead(target) ||
            NpcRoleUtility.IsInCombat(target);
    }

    public bool TrySellSpecificItemTo(
        NpcTradeAgent buyer,
        StatItemData item,
        int amount)
    {
        return TrySellSpecificItemTo(buyer, item, amount, true);
    }

    public bool TrySellSpecificItemTo(
        NpcTradeAgent buyer,
        StatItemData item,
        int amount,
        bool allowAutoUse)
    {
        EnsureInventory();

        if (buyer == null ||
            buyer.inventory == null ||
            inventory == null ||
            item == null ||
            amount <= 0 ||
            IsBusyForTrade(gameObject) ||
            IsBusyForTrade(buyer.gameObject) ||
            !NpcEconomy.CanTradeNormally(item))
        {
            return false;
        }

        int available =
            inventory.GetAmount(item);

        if (available <= 0)
        {
            return false;
        }

        int buyAmount =
            Mathf.Min(amount, available);

        int unitPrice =
            NpcEconomy.GetNpcBuyPrice(
                item,
                buyer.gameObject,
                sellToNpcContext);

        int affordableAmount =
            unitPrice <= 0
            ? buyAmount
            : Mathf.Min(buyAmount, buyer.GetMoney() / unitPrice);

        if (affordableAmount <= 0 ||
            !RemoveBrokerItem(item, affordableAmount))
        {
            return false;
        }

        int totalPrice =
            unitPrice * affordableAmount;

        buyer.AddMoney(-totalPrice);
        AddBrokerMoney(totalPrice);

        for (int i = 0; i < affordableAmount; i++)
        {
            buyer.ReceiveBoughtItem(item, allowAutoUse);
        }

        NpcSocialEventBus.PublishTradeCompleted(
            buyer.gameObject,
            gameObject,
            item,
            totalPrice);

        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Sold,
            item,
            gameObject,
            buyer.gameObject);

        return true;
    }

    bool RemoveBrokerItem(StatItemData item, int amount)
    {
        NpcItemCollector collector =
            GetComponent<NpcItemCollector>();

        if (collector != null)
        {
            return collector.RemoveOwnedItem(
                item,
                amount,
                ItemLifecycleEventType.Sold);
        }

        return inventory != null &&
            inventory.RemoveItem(item, amount);
    }

    int GetBrokerMoney()
    {
        if (useSpiritStoneCurrency)
        {
            return serviceSpiritStone;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.money;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.money;
        }

        EnsureMoney(0);
        return serviceMoney;
    }

    public bool TrySpendBrokerMoney(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0)
        {
            return true;
        }

        EnsureMoney(amount);
        if (GetBrokerMoney() < amount)
        {
            return false;
        }

        AddBrokerMoney(-amount);
        return true;
    }

    public void AddBrokerRevenue(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        AddBrokerMoney(amount);
    }

    void AddBrokerMoney(int amount)
    {
        if (useSpiritStoneCurrency)
        {
            serviceSpiritStone = Mathf.Max(0, serviceSpiritStone + amount);
            EnsureMoney(0);
            return;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.money = Mathf.Max(0, villager.money + amount);
            EnsureMoney(0);
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.money = Mathf.Max(0, smartNpc.money + amount);
            EnsureMoney(0);
            return;
        }

        serviceMoney = Mathf.Max(0, serviceMoney + amount);
        EnsureMoney(0);
    }

    void EnsureMoney(int requiredAmount)
    {
        int target = Mathf.Max(startingMoney, minimumMoneyReserve, requiredAmount);

        if (useSpiritStoneCurrency)
        {
            if (refillMoneyWhenLow &&
                serviceSpiritStone < target)
            {
                serviceSpiritStone = target;
            }
            return;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            if (refillMoneyWhenLow && villager.money < target)
            {
                villager.money = target;
            }
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            if (refillMoneyWhenLow && smartNpc.money < target)
            {
                smartNpc.money = target;
            }
            return;
        }

        if (refillMoneyWhenLow && serviceMoney < target)
        {
            serviceMoney = target;
        }
    }

    float GetBuyScoreForNpc(
        StatItemData item,
        GameObject buyer,
        int price,
        int buyerMoney)
    {
        if (item == null ||
            price <= 0)
        {
            return 0f;
        }

        float score = 1f;

        if (item.itemType == ItemType.DanDuoc)
        {
            score += NpcEconomy.IsNearBreakthrough(buyer)
                ? 8f
                : 2f;
        }
        else if (item.itemType == ItemType.PhapBao)
        {
            score += 2.2f;
        }
        else if (item.itemType == ItemType.CongPhap)
        {
            score += 2f;
        }
        score += Mathf.Max(
            item.GetNpcUseScore(),
            item.GetNpcConversionScore() * 0.2f);

        if (item.ShouldNpcPreferSell())
        {
            score *= 0.5f;
        }

        float wealthRatio =
            buyerMoney / Mathf.Max(1f, price);

        return score * Mathf.Clamp(wealthRatio, 0.1f, 5f);
    }


    void CaptureStationaryPosition()
    {
        stationaryPosition = brokerStandPoint != null
            ? brokerStandPoint.position
            : transform.position;
    }

    void ConfigureStationaryBroker()
    {
        if (!keepBrokerStationary)
        {
            return;
        }

        CaptureRigidbodySettings();
        ApplyStationaryRigidbodyLock();

        NpcMapMover2D mover = GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.enabled = false;
        }

        if (disableBoundaryClampWhileStationary)
        {
            NpcMapBoundaryClamp boundaryClamp = GetComponent<NpcMapBoundaryClamp>();
            if (boundaryClamp != null)
            {
                boundaryClamp.enabled = false;
            }
        }

        if (disableMovementAnimatorWhileStationary)
        {
            CharacterMovementAnimator movementAnimator = GetComponent<CharacterMovementAnimator>();
            if (movementAnimator != null)
            {
                movementAnimator.enabled = false;
            }
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.autonomousActivitiesEnabled = false;
            smartNpc.currentTarget = null;
            if (disableBaseAiWhileStationary)
            {
                smartNpc.enabled = false;
            }
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.currentTarget = null;
            villager.StopMoving();
            if (disableBaseAiWhileStationary)
            {
                villager.enabled = false;
            }
        }

        ConfigureCustomerZoneCollider();
    }

    void ConfigureCustomerZoneCollider()
    {
        if (!forceCustomerZoneTrigger)
        {
            return;
        }

        BoxCollider2D customerZone = GetCustomerZoneCollider();
        if (customerZone == null)
        {
            return;
        }

        if (!customerZone.isTrigger)
        {
            customerZone.isTrigger = true;
            LogCounterDebug(
                "ConfigureCustomerZoneCollider",
                "forcedTrigger=1 box=" + customerZone.name,
                customerZone);
        }
    }

    void CaptureRigidbodySettings()
    {
        if (capturedRigidbodySettings)
        {
            return;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb == null)
        {
            return;
        }

        originalConstraints = rb.constraints;
        originalBodyType = rb.bodyType;
        capturedRigidbodySettings = true;
    }

    void ApplyStationaryRigidbodyLock()
    {
        if (!hardFreezeRigidbodyWhileStationary)
        {
            return;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezePositionX |
            RigidbodyConstraints2D.FreezePositionY |
            RigidbodyConstraints2D.FreezeRotation;

        if (useKinematicBodyWhileStationary)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    void ReleaseStationaryBrokerLock()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null && capturedRigidbodySettings)
        {
            rb.constraints = originalConstraints;
            rb.bodyType = originalBodyType;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        NpcMapMover2D mover = GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.enabled = true;
        }

        NpcMapBoundaryClamp boundaryClamp =
            GetComponent<NpcMapBoundaryClamp>();
        if (boundaryClamp != null)
        {
            boundaryClamp.enabled = true;
        }

        CharacterMovementAnimator movementAnimator =
            GetComponent<CharacterMovementAnimator>();
        if (movementAnimator != null)
        {
            movementAnimator.enabled = true;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.enabled = true;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.enabled = true;
        }
    }

    public void SetStationaryMode(bool stationary)
    {
        if (keepBrokerStationary == stationary &&
            lastKeepBrokerStationary == stationary &&
            stationary)
        {
            return;
        }

        keepBrokerStationary = stationary;

        if (stationary)
        {
            CaptureStationaryPosition();
            ConfigureStationaryBroker();
        }
        else
        {
            ReleaseStationaryBrokerLock();
        }

        lastKeepBrokerStationary = stationary;
    }

    void UpdateStationaryMode()
    {
        if (keepBrokerStationary == lastKeepBrokerStationary)
        {
            return;
        }

        if (keepBrokerStationary)
        {
            CaptureStationaryPosition();
            ConfigureStationaryBroker();
        }
        else
        {
            ReleaseStationaryBrokerLock();
        }

        lastKeepBrokerStationary = keepBrokerStationary;
    }

    void KeepBrokerAtStation()
    {
        if (!keepBrokerStationary)
        {
            return;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            ApplyStationaryRigidbodyLock();
            rb.position = stationaryPosition;
        }

        transform.position = new Vector3(
            stationaryPosition.x,
            stationaryPosition.y,
            transform.position.z);
    }

    void EnsureInventory()
    {
        EnsureMoney(0);

        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }

        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }
    }

    public NpcMapZone? ResolveBrokerZone()
    {
        if (customerPoint != null)
        {
            NpcMapZone? customerZone =
                NpcMapNavigator.GetDestinationZone(
                    customerPoint);
            if (customerZone.HasValue)
            {
                return customerZone;
            }
        }

        NpcMapZone? brokerZone =
            NpcMapNavigator.GetDestinationZone(transform);
        if (brokerZone.HasValue)
        {
            return brokerZone;
        }

        NpcMapArea area =
            NpcMapArea.FindArea(transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(
                transform.position);
        }

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    bool IsVanBaoLauOnlyBroker()
    {
        NpcMapZone? zone =
            ResolveBrokerZone();
        return zone.HasValue &&
            zone.Value == NpcMapZone.VanBaoLau;
    }

    bool CanReceiveRequestFrom(GameObject npc)
    {
        if (npc == null)
        {
            return true;
        }

        if (npc.GetComponent<VillagerAI>() != null)
        {
            return receiveVillagerRequests;
        }

        if (npc.GetComponent<SmartNpcAI>() != null)
        {
            return receiveSmartNpcRequests;
        }

        return true;
    }
}
