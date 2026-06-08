using UnityEngine;

public class NpcCounterBroker : MonoBehaviour
{
    public static NpcCounterBroker Active { get; private set; }

    [Header("Broker")]
    public ItemInventory inventory;
    public bool receiveAllNpcRequests = true;
    public bool buyGoodsFromNpcs = true;
    public bool sellUsefulItemsToNpcs = true;
    public bool acceptAllMaterials = true;
    public StatItemData[] acceptedItems;
    public int maxUnitsPerRequest = 4;
    public int maxTransactionsPerVisit = 3;
    public float noDealCooldownMultiplier = 0.4f;

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

    [Header("Wallet")]
    [InspectorName("Linh Thạch ban đầu")]
    public int startingMoney = 100000;
    [InspectorName("Dự trữ Linh Thạch tối thiểu")]
    public int minimumMoneyReserve = 50000;
    public bool refillMoneyWhenLow = true;
    [SerializeField, InspectorName("Linh Thạch dịch vụ")] int serviceMoney;

    Rigidbody2D rb;
    Vector3 stationaryPosition;
    RigidbodyConstraints2D originalConstraints;
    RigidbodyType2D originalBodyType;
    bool capturedRigidbodySettings;

    public int CurrentMoney => GetBrokerMoney();

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
    }

    void OnEnable()
    {
        Active = this;
        EnsureInventory();
        CaptureStationaryPosition();
        ConfigureStationaryBroker();
    }

    void Start()
    {
        CaptureStationaryPosition();
        ConfigureStationaryBroker();
        KeepBrokerAtStation();
    }

    void FixedUpdate()
    {
        KeepBrokerAtStation();
    }

    void LateUpdate()
    {
        KeepBrokerAtStation();
    }

    void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
    }


    public Vector3 CustomerPosition
    {
        get
        {
            return customerPoint != null
                ? customerPoint.position
                : transform.position;
        }
    }

    public float CustomerServiceRadius
    {
        get
        {
            return allowMultipleCustomers
                ? Mathf.Max(customerArriveDistance, multiCustomerServiceRadius)
                : customerArriveDistance;
        }
    }

    public Vector3 GetCustomerPositionFor(GameObject npc)
    {
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

    public bool IsCustomerAtCounter(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        return Vector2.Distance(
            npc.transform.position,
            transform.position) <= Mathf.Max(0.05f, CustomerServiceRadius);
    }
    public static bool TryTradeWithActiveBroker(NpcTradeAgent npc)
    {
        if (Active == null ||
            !Active.receiveAllNpcRequests)
        {
            return false;
        }

        return Active.TryTradeWithNpc(npc);
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
            seller.money += totalPrice;
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

    void AddBrokerMoney(int amount)
    {
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
}
