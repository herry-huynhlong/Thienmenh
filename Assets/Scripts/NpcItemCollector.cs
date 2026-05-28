using System.Collections.Generic;
using UnityEngine;

public class NpcItemCollector : MonoBehaviour
{
    [Header("Inventory")]
    public ItemInventory inventory;
    public CharacterStats characterStats;

    [Header("Pickup")]
    public bool canPickupItems = true;
    public bool autoUsePickedItems = true;
    public LayerMask pickupLayers = ~0;
    public float pickupRadius = 0.45f;
    public float scanInterval = 0.5f;

    readonly List<StatItemData> equippedItems =
        new List<StatItemData>();

    float scanTimer;

    void Awake()
    {
        AutoFindReferences();
    }

    void Update()
    {
        if (!canPickupItems)
        {
            return;
        }

        scanTimer += Time.deltaTime;

        if (scanTimer < scanInterval)
        {
            return;
        }

        scanTimer = 0f;
        ScanForNearbyPickup();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canPickupItems)
        {
            return;
        }

        TryPickup(other.GetComponentInParent<WorldStatItemPickup>());
    }

    void ScanForNearbyPickup()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                pickupRadius,
                pickupLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (TryPickup(
                    hit.GetComponentInParent<WorldStatItemPickup>()))
            {
                return;
            }
        }
    }

    bool TryPickup(WorldStatItemPickup pickup)
    {
        AutoFindReferences();

        if (pickup == null ||
            !pickup.allowNpcPickup ||
            pickup.item == null ||
            inventory == null ||
            !pickup.TryTake(1))
        {
            return false;
        }

        ReceiveItem(
            pickup.item,
            ItemLifecycleEventType.Picked,
            autoUsePickedItems);

        return true;
    }

    public void ReceiveItem(
        StatItemData item,
        ItemLifecycleEventType source,
        bool considerUse)
    {
        AutoFindReferences();

        if (item == null ||
            inventory == null)
        {
            return;
        }

        inventory.AddItem(item, 1);
        ItemLifecycleSystem.Notify(source, item, gameObject);
        TreasureHeatSystem.NotifyNpcReceivedItem(gameObject, item);

        if (considerUse)
        {
            TryUseOwnedItem(item);
        }
    }

    public bool TryUseOwnedItem(StatItemData item)
    {
        if (item == null ||
            inventory == null ||
            !item.CanUseOn(gameObject))
        {
            return false;
        }

        if (item.itemType == ItemType.VatLieu &&
            item.canBeRefinedIntoPill)
        {
            return false;
        }

        int itemIndex =
            FindItemIndex(item);

        if (itemIndex < 0)
        {
            return false;
        }

        ItemStack stack =
            inventory.GetStack(itemIndex);

        if (!item.ConsumesWhenUsed() &&
            item.itemType != ItemType.CongPhap &&
            equippedItems.Contains(item))
        {
            return false;
        }

        if (item.itemType == ItemType.CongPhap)
        {
            float oldPower =
                GetMasteryPower(item, stack.mastery);

            AdvanceManualMastery(stack);

            float newPower =
                GetMasteryPower(item, stack.mastery);

            if (stack.applied &&
                oldPower > 0f)
            {
                item.ApplyTo(gameObject, -1, oldPower);
            }

            if (newPower > 0f &&
                item.ApplyTo(gameObject, 1, newPower))
            {
                if (!equippedItems.Contains(item))
                {
                    equippedItems.Add(item);
                }

                stack.applied = true;
            }

            inventory.MarkDirty();
            return true;
        }

        bool applied =
            !(item.ConsumesWhenUsed() &&
            !item.RollUseSuccess()) &&
            item.ApplyTo(gameObject);

        if (!applied &&
            !item.ConsumesWhenUsed())
        {
            return false;
        }

        if (item.ConsumesWhenUsed())
        {
            inventory.RemoveStackAt(itemIndex, 1);
            ItemLifecycleSystem.Notify(
                ItemLifecycleEventType.Used,
                item,
                gameObject);
            return true;
        }

        equippedItems.Add(item);
        stack.applied = true;
        inventory.LoseDurability(
            itemIndex,
            item.GetDurabilityLossPerUse());

        if (inventory.GetAmount(item) <= 0)
        {
            UnequipItem(item);
        }

        return true;
    }

    public bool TryTeachManualTo(
        NpcItemCollector target,
        StatItemData item)
    {
        if (target == null ||
            item == null ||
            item.itemType != ItemType.CongPhap ||
            !item.canBeTaught)
        {
            return false;
        }

        int itemIndex =
            FindItemIndex(item);

        if (itemIndex < 0)
        {
            return false;
        }

        ItemStack stack =
            inventory.GetStack(itemIndex);

        if (stack == null ||
            stack.mastery != CultivationManualMastery.DaiThanh)
        {
            return false;
        }

        target.ReceiveItem(
            item,
            ItemLifecycleEventType.Taught,
            true);

        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Taught,
            item,
            gameObject,
            target.gameObject);

        return true;
    }

    public bool UnequipForSale(StatItemData item)
    {
        return UnequipItem(item);
    }

    void AdvanceManualMastery(ItemStack stack)
    {
        if (stack == null ||
            stack.item == null ||
            stack.item.itemType != ItemType.CongPhap)
        {
            return;
        }

        switch (stack.mastery)
        {
            case CultivationManualMastery.None:
                stack.mastery = CultivationManualMastery.TieuThanh;
                return;
            case CultivationManualMastery.TieuThanh:
                stack.mastery = CultivationManualMastery.TrungThanh;
                return;
            case CultivationManualMastery.TrungThanh:
                stack.mastery = CultivationManualMastery.DaiThanh;
                return;
        }
    }

    float GetMasteryPower(
        StatItemData item,
        CultivationManualMastery mastery)
    {
        if (item == null)
        {
            return 0f;
        }

        switch (mastery)
        {
            case CultivationManualMastery.TieuThanh:
                return item.tieuThanhPower;
            case CultivationManualMastery.TrungThanh:
                return item.trungThanhPower;
            case CultivationManualMastery.DaiThanh:
                return item.daiThanhPower;
            default:
                return 0f;
        }
    }

    public bool UnequipItem(StatItemData item)
    {
        if (item == null ||
            !equippedItems.Remove(item))
        {
            return false;
        }

        item.RemoveFrom(gameObject);
        return true;
    }

    public bool RemoveOwnedItem(
        StatItemData item,
        int amount,
        ItemLifecycleEventType reason)
    {
        AutoFindReferences();

        if (item == null ||
            inventory == null)
        {
            return false;
        }

        UnequipItem(item);

        bool removed =
            inventory.RemoveItem(item, amount);

        if (removed)
        {
            ItemLifecycleSystem.Notify(reason, item, gameObject);
        }

        return removed;
    }

    int FindItemIndex(StatItemData item)
    {
        if (inventory == null ||
            item == null)
        {
            return -1;
        }

        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack =
                inventory.items[i];

            if (stack != null &&
                stack.item == item &&
                stack.amount > 0)
            {
                return i;
            }
        }

        return -1;
    }

    void AutoFindReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();

            if (inventory == null)
            {
                inventory = gameObject.AddComponent<ItemInventory>();
                inventory.shareRuntimeItems = false;
            }
        }

        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
