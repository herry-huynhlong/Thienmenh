using UnityEngine;

public class NpcTradeAgent : MonoBehaviour
{
    public ItemInventory inventory;
    public LayerMask npcLayers = ~0;
    public float tradeRadius = 1.2f;
    public float tradeInterval = 5f;
    [Range(0, 100)]
    public int tradeChance = 25;
    public bool buyUsefulItemsFromMarketTrader = true;

    [Header("Market Trader")]
    public bool isMarketTrader;
    public bool buyProduceFromVillagers = true;
    public bool acceptAllMaterials = true;
    public StatItemData[] acceptedProduce;
    public int maxProduceUnitsPerTrade = 4;
    [Range(1, 100)]
    public int buyPricePercent = 70;

    float tradeTimer;

    public bool IsMarketTrader => isMarketTrader;

    void Awake()
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

    void Update()
    {
        tradeTimer += Time.deltaTime;

        if (tradeTimer < tradeInterval)
        {
            return;
        }

        tradeTimer = 0f;

        if (Random.Range(0, 100) >= tradeChance)
        {
            return;
        }

        TryTradeWithNearbyNpc();
    }

    void TryTradeWithNearbyNpc()
    {
        if (inventory == null)
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                tradeRadius,
                npcLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.gameObject == gameObject)
            {
                continue;
            }

            NpcTradeAgent seller =
                hit.GetComponentInParent<NpcTradeAgent>();

            if (seller == null ||
                seller == this ||
                seller.inventory == null)
            {
                continue;
            }

            if (!isMarketTrader &&
                buyUsefulItemsFromMarketTrader &&
                !seller.isMarketTrader)
            {
                continue;
            }

            if (TryBuyOneUsefulItem(seller))
            {
                return;
            }
        }
    }

    bool TryBuyOneUsefulItem(NpcTradeAgent seller)
    {
        if (isMarketTrader)
        {
            return false;
        }

        StatItemData itemToBuy = null;
        int priceToPay = 0;
        float bestScore = 0f;

        foreach (ItemStack stack in seller.inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.CanUseOn(gameObject))
            {
                continue;
            }

            int price =
                NpcEconomy.GetNpcBuyPrice(
                    stack.item,
                    gameObject,
                    NpcTradeContext.NpcToNpc);

            if (GetMoney() < price)
            {
                continue;
            }

            float score =
                GetBuyScore(stack.item, price);

            if (score <= bestScore)
            {
                continue;
            }

            itemToBuy = stack.item;
            priceToPay = price;
            bestScore = score;
        }

        if (itemToBuy == null ||
            !RemoveSellerItem(seller, itemToBuy))
        {
            return false;
        }

        AddMoney(-priceToPay);
        seller.AddMoney(priceToPay);
        ReceiveBoughtItem(itemToBuy);

        return true;
    }

    public bool TryBuyProduceFrom(
        VillagerAI seller,
        ItemInventory sellerInventory)
    {
        if (!isMarketTrader ||
            !buyProduceFromVillagers ||
            seller == null ||
            sellerInventory == null ||
            inventory == null)
        {
            return false;
        }

        int boughtUnits = 0;

        for (int i = sellerInventory.items.Count - 1; i >= 0; i--)
        {
            if (boughtUnits >= maxProduceUnitsPerTrade)
            {
                break;
            }

            ItemStack stack =
                sellerInventory.items[i];

            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !AcceptsProduce(stack.item))
            {
                continue;
            }

            int amount =
                Mathf.Min(
                    stack.amount,
                    maxProduceUnitsPerTrade - boughtUnits);

            int unitPrice =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        NpcEconomy.GetTradePrice(
                            stack.item,
                            NpcTradeContext.ProduceBuy) *
                        buyPricePercent / 100f));

            int totalPrice =
                unitPrice * amount;

            if (GetMoney() < totalPrice)
            {
                continue;
            }

            if (!sellerInventory.RemoveItem(stack.item, amount))
            {
                continue;
            }

            AddMoney(-totalPrice);
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

    bool AcceptsProduce(StatItemData item)
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

        if (acceptedProduce == null)
        {
            return false;
        }

        foreach (StatItemData accepted in acceptedProduce)
        {
            if (accepted == item)
            {
                return true;
            }
        }

        return false;
    }

    bool RemoveSellerItem(
        NpcTradeAgent seller,
        StatItemData item)
    {
        NpcItemCollector sellerCollector =
            seller.GetComponent<NpcItemCollector>();

        if (sellerCollector != null)
        {
            return sellerCollector.RemoveOwnedItem(
                item,
                1,
                ItemLifecycleEventType.Sold);
        }

        return seller.inventory.RemoveItem(item, 1);
    }

    void ReceiveBoughtItem(StatItemData item)
    {
        NpcItemCollector collector =
            GetComponent<NpcItemCollector>();

        if (collector != null)
        {
            collector.ReceiveItem(
                item,
                ItemLifecycleEventType.Sold,
                true);
            return;
        }

        inventory.AddItem(item, 1);
    }

    float GetBuyScore(StatItemData item, int price)
    {
        if (item == null ||
            price <= 0)
        {
            return 0f;
        }

        float score = 1f;

        if (item.itemType == ItemType.DanDuoc)
        {
            score += NpcEconomy.IsNearBreakthrough(gameObject)
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
            GetMoney() / Mathf.Max(1f, price);

        return score * Mathf.Clamp(wealthRatio, 0.1f, 5f);
    }

    int GetMoney()
    {
        return NpcEconomy.GetNpcMoney(gameObject);
    }

    void AddMoney(int amount)
    {
        NpcEconomy.AddNpcMoney(gameObject, amount);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, tradeRadius);
    }
}
