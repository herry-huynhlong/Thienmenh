using UnityEngine;

public class NpcTradeAgent : MonoBehaviour
{
    public ItemInventory inventory;
    public LayerMask npcLayers = ~0;
    public float tradeRadius = 1.2f;
    public float tradeInterval = 5f;
    [Range(0, 100)]
    public int tradeChance = 25;

    float tradeTimer;

    void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
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

            if (TryBuyOneUsefulItem(seller))
            {
                return;
            }
        }
    }

    bool TryBuyOneUsefulItem(NpcTradeAgent seller)
    {
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
                Mathf.Max(0, stack.item.price);

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
