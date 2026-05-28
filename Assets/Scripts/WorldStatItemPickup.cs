using System;
using UnityEngine;

public class WorldStatItemPickup : MonoBehaviour
{
    public StatItemData item;
    public int amount = 1;
    public bool allowNpcPickup = true;
    public bool allowPlayerPickup = true;
    public bool destroyWhenEmpty = true;
    public event Action OnDepleted;

    public bool TryTake(int takeAmount)
    {
        if (item == null ||
            takeAmount <= 0 ||
            amount < takeAmount)
        {
            return false;
        }

        amount -= takeAmount;

        if (amount <= 0)
        {
            OnDepleted?.Invoke();

            if (destroyWhenEmpty)
            {
                Destroy(gameObject);
            }
        }

        return true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryGiveToNpc(other);
    }

    void TryGiveToNpc(Collider2D other)
    {
        if (!allowNpcPickup ||
            item == null ||
            other == null)
        {
            return;
        }

        Transform target =
            GetNpcTarget(other);

        if (target == null)
        {
            return;
        }

        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory =
                target.gameObject.AddComponent<ItemInventory>();

            inventory.shareRuntimeItems = false;
        }

        StatItemData pickedItem = item;

        if (!TryTake(1))
        {
            return;
        }

        inventory.AddItem(pickedItem, 1);
    }

    Transform GetNpcTarget(Collider2D other)
    {
        VillagerAI villager =
            other.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.transform;
        }

        SmartNpcAI smartNpc =
            other.GetComponentInParent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        return null;
    }
}
