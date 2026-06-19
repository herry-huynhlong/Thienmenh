using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class SellerJob : MonoBehaviour
{
    public bool enabledSellerJob = true;
    public Transform sellingPoint;
    public float arriveDistance = 1.2f;
    public NpcJobState currentState = NpcJobState.Idle;

    VillagerAI villager;
    ItemInventory inventory;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
        inventory = GetComponent<ItemInventory>();
    }

    public bool TryRun()
    {
        if (!enabledSellerJob || villager == null)
        {
            return false;
        }

        currentState = NpcJobState.Moving;
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }

        if (VillageStorage.Instance != null &&
            inventory != null &&
            inventory.items.Count > 0)
        {
            villager.currentAction = NpcText.Action("goSellGoods");
            if (Vector2.Distance(transform.position, VillageStorage.Instance.transform.position) <= arriveDistance)
            {
                VillageStorage.Instance.Store(inventory);
                currentState = NpcJobState.Trading;
                villager.currentAction = NpcText.Action("sellGoods");
                return true;
            }
        }

        return false;
    }
}
