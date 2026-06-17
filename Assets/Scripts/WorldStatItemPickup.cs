using System;
using UnityEngine;

public class WorldStatItemPickup : MonoBehaviour
{
    public StatItemData item;
    public int amount = 1;
    public bool allowNpcPickup = true;
    public bool allowPlayerPickup = false;
    public bool destroyWhenEmpty = true;
    public bool requireNpcHarvestAction;
    public float harvestDuration = 8f;
    public event Action OnDepleted;

    GameObject reservedBy;
    float reservationExpiresAt;

    public bool RequiresNpcHarvestAction()
    {
        return requireNpcHarvestAction ||
            GetComponent<WorldResourceNode>() != null;
    }

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

    public bool TryReserve(GameObject reserver, float duration)
    {
        if (reserver == null ||
            item == null ||
            amount <= 0)
        {
            return false;
        }

        if (reservedBy != null &&
            reservedBy != reserver &&
            reservationExpiresAt > Time.time)
        {
            return false;
        }

        reservedBy = reserver;
        reservationExpiresAt = Time.time + Mathf.Max(0.25f, duration);
        return true;
    }

    public bool IsReservedByOther(GameObject requester)
    {
        if (reservedBy == null ||
            reservedBy == requester ||
            reservationExpiresAt <= Time.time)
        {
            return false;
        }

        return true;
    }

    public void RefreshReservation(GameObject reserver, float duration)
    {
        if (reserver == null ||
            reservedBy != reserver)
        {
            return;
        }

        reservationExpiresAt = Time.time + Mathf.Max(0.25f, duration);
    }

    public void ClearReservation(GameObject reserver)
    {
        if (reserver == null ||
            reservedBy != reserver)
        {
            return;
        }

        reservedBy = null;
        reservationExpiresAt = 0f;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryGiveToWorldActor(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryGiveToWorldActor(other);
    }

    public bool TryGiveToWorldActor(Collider2D other)
    {
        if (item == null ||
            other == null)
        {
            return false;
        }

        if (allowPlayerPickup &&
            TryGiveToPlayer(other))
        {
            return true;
        }

        if (!allowNpcPickup ||
            RequiresNpcHarvestAction() ||
            item == null ||
            other == null)
        {
            return false;
        }

        Transform target =
            GetWorldActorTarget(other);

        if (target == null)
        {
            return false;
        }

        if (IsReservedByOther(target.gameObject))
        {
            return false;
        }

        StatItemData pickedItem = item;

        if (!TryTake(1))
        {
            return false;
        }

        NpcItemCollector collector =
            target.GetComponent<NpcItemCollector>();

        if (collector != null)
        {
            collector.ReceiveItem(
                pickedItem,
                ItemLifecycleEventType.Picked,
                false);
            return true;
        }

        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory =
                target.gameObject.AddComponent<ItemInventory>();

            inventory.shareRuntimeItems = false;
        }

        ItemEffectSpawner.PlayPickupEffect(pickedItem, target);
        inventory.AddItem(pickedItem, 1);
        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Picked,
            pickedItem,
            target.gameObject);
        TreasureHeatSystem.NotifyNpcReceivedItem(target.gameObject, pickedItem);
        return true;
    }

    bool TryGiveToPlayer(Collider2D other)
    {
        Transform target =
            GetPlayerTarget(other);

        if (target == null)
        {
            return false;
        }

        StatItemData pickedItem = item;

        if (!TryTake(1))
        {
            return false;
        }

        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory =
                target.gameObject.AddComponent<ItemInventory>();
        }

        ItemEffectSpawner.PlayPickupEffect(pickedItem, target);
        inventory.AddItem(pickedItem, 1);
        return true;
    }

    Transform GetWorldActorTarget(Collider2D other)
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

        MonsterAI monster =
            other.GetComponentInParent<MonsterAI>();

        if (monster != null)
        {
            return monster.transform;
        }

        return null;
    }

    Transform GetPlayerTarget(Collider2D other)
    {
        CharacterStats characterStats =
            other.GetComponentInParent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.transform;
        }

        PlayerHealth player =
            other.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            return player.transform;
        }

        if (other.CompareTag("Player"))
        {
            return other.transform;
        }

        return null;
    }
}
