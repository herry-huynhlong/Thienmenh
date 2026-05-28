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

        StatItemData pickedItem = item;

        if (!TryTake(1))
        {
            return;
        }

        NpcItemCollector collector =
            target.GetComponent<NpcItemCollector>();

        if (collector != null)
        {
            collector.ReceiveItem(
                pickedItem,
                ItemLifecycleEventType.Picked,
                true);
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

        inventory.AddItem(pickedItem, 1);
        TreasureHeatSystem.NotifyNpcReceivedItem(target.gameObject, pickedItem);
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
