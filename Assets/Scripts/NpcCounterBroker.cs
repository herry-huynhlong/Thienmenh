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

            if (NpcEconomy.GetNpcMoney(gameObject) < totalPrice)
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

            if (NpcEconomy.GetNpcMoney(gameObject) < price ||
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

    void AddBrokerMoney(int amount)
    {
        NpcEconomy.AddNpcMoney(gameObject, amount);
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

        float wealthRatio =
            buyerMoney / Mathf.Max(1f, price);

        return score * Mathf.Clamp(wealthRatio, 0.1f, 5f);
    }

    void EnsureInventory()
    {
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