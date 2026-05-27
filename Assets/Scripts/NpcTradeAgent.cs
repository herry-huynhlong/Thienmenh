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

        foreach (ItemStack stack in seller.inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !stack.item.CanUseOn(gameObject))
            {
                continue;
            }

            int price =
                Mathf.Max(1, stack.item.price);

            if (GetMoney() < price)
            {
                continue;
            }

            itemToBuy = stack.item;
            priceToPay = price;
            break;
        }

        if (itemToBuy == null ||
            !seller.inventory.RemoveItem(itemToBuy, 1))
        {
            return false;
        }

        AddMoney(-priceToPay);
        seller.AddMoney(priceToPay);
        inventory.AddItem(itemToBuy, 1);
        itemToBuy.ApplyTo(gameObject);

        if (itemToBuy.consumeOnUse)
        {
            inventory.RemoveItem(itemToBuy, 1);
        }

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
                        Mathf.Max(1, stack.item.price) *
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

    int GetMoney()
    {
        VillagerAI villager =
            GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.money;
        }

        SmartNpcAI smartNpc =
            GetComponent<SmartNpcAI>();

        return smartNpc != null ? smartNpc.money : 0;
    }

    void AddMoney(int amount)
    {
        VillagerAI villager =
            GetComponent<VillagerAI>();

        if (villager != null)
        {
            villager.money = Mathf.Max(0, villager.money + amount);
            return;
        }

        SmartNpcAI smartNpc =
            GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            smartNpc.money = Mathf.Max(0, smartNpc.money + amount);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, tradeRadius);
    }
}
