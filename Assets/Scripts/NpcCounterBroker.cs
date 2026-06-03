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

    [Header("Wallet")]
    public int startingMoney = 100000;
    public int minimumMoneyReserve = 50000;
    public bool refillMoneyWhenLow = true;
    [SerializeField] int serviceMoney;

    public int CurrentMoney => GetBrokerMoney();

    [Header("Pricing")]
    public NpcTradeContext sellToNpcContext =
        NpcTradeContext.CounterBrokerBuy;
    public NpcTradeContext buyFromNpcContext =
        NpcTradeContext.CounterBrokerSell;

    void Awake()
    {
        EnsureInventory();
    }

    void OnEnable()
    {
        Active = this;
        EnsureInventory();
    }

    void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
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

    public bool TryTradeWithNpc(NpcTradeAgent npc)
    {
        if (npc == null ||
            npc.gameObject == gameObject)
        {
            return false;
        }

        if (sellUsefulItemsToNpcs &&
            TrySellUsefulItemTo(npc))
        {
            return true;
        }

        if (buyGoodsFromNpcs &&
            TryBuyItemFromNpc(npc))
        {
            return true;
        }

        return false;
    }

    public bool TrySellUsefulItemTo(NpcTradeAgent buyer)
    {
        EnsureInventory();

        if (buyer == null ||
            buyer.inventory == null ||
            inventory == null)
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

            float score =
                GetBuyScoreForNpc(
                    stack.item,
                    buyer.gameObject,
                    price,
                    buyer.GetMoney());

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

    public bool TryBuyProduceFrom(
        VillagerAI seller,
        ItemInventory sellerInventory)
    {
        if (!buyGoodsFromNpcs ||
            seller == null ||
            sellerInventory == null)
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
            seller.inventory == null)
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
                !AcceptsItem(stack.item))
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
