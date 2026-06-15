using System.Collections;
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

    [Header("Manual Study")]
    public bool autoStudyManuals = true;
    public float manualStudyInterval = 120f;

    readonly List<StatItemData> equippedItems =
        new List<StatItemData>();

    float scanTimer;
    float manualStudyTimer;
    bool restoredAppliedItems;
    bool privateInventoryInitialized;

    void Awake()
    {
        AutoFindReferences();
    }

    IEnumerator Start()
    {
        yield return null;
        AutoFindReferences();
        RestoreAppliedItems();
    }

    void Update()
    {
        if (canPickupItems)
        {
            scanTimer += Time.deltaTime;

            if (scanTimer >= scanInterval)
            {
                scanTimer = 0f;
                ScanForNearbyPickup();
            }
        }

        TickManualStudy();
        AccumulateManualUseTime();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryPickupFromCollider(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryPickupFromCollider(other);
    }

    void TryPickupFromCollider(Collider2D other)
    {
        if (!canPickupItems || other == null)
        {
            return;
        }

        TryPickup(other.GetComponentInParent<WorldStatItemPickup>());
    }

    void ScanForNearbyPickup()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            pickupRadius,
            pickupLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (TryPickup(hit.GetComponentInParent<WorldStatItemPickup>()))
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
            pickup.requireNpcHarvestAction ||
            pickup.item == null ||
            inventory == null ||
            pickup.IsReservedByOther(gameObject))
        {
            return false;
        }

        StatItemData pickedItem = pickup.item;

        if (!pickup.TryTake(1))
        {
            return false;
        }

        ReceiveItem(
            pickedItem,
            ItemLifecycleEventType.Picked,
            false);

        return true;
    }

    public void ReceiveItem(
        StatItemData item,
        ItemLifecycleEventType source,
        bool considerUse)
    {
        AutoFindReferences();

        if (item == null || inventory == null)
        {
            return;
        }

        inventory.AddItem(item, 1);
        ItemLifecycleSystem.Notify(source, item, gameObject);
        TreasureHeatSystem.NotifyNpcReceivedItem(gameObject, item);

        if (considerUse ||
            ShouldNpcDecideItemUse(item))
        {
            TryUseOwnedItem(item);
        }
    }

    bool ShouldNpcDecideItemUse(StatItemData item)
    {
        if (item == null ||
            inventory == null)
        {
            return false;
        }

        if (!item.CanUseOn(gameObject) ||
            !item.ShouldNpcUseDirectly())
        {
            return false;
        }

        NpcDecisionBrain brain =
            GetComponent<NpcDecisionBrain>();

        if (brain != null &&
            brain.enabledDecisionBrain)
        {
            if (brain.currentDecision == NpcDecisionKind.Rest ||
                brain.currentDecision == NpcDecisionKind.Work ||
                brain.currentDecision == NpcDecisionKind.GatherResource)
            {
                return false;
            }
        }

        if (item.itemType == ItemType.CongPhap)
        {
            return CanStudyManualNow(item);
        }

        if (item.itemType == ItemType.PhapBao)
        {
            return IsItemBetterThanCurrentEquipment(item);
        }

        return item.CanUseOn(gameObject);
    }

    bool CanStudyManualNow(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        ItemStack stack = FindItemStack(item);
        if (stack == null ||
            stack.broken ||
            stack.mastery == CultivationManualMastery.DaiThanh)
        {
            return false;
        }

        return true;
    }

    bool IsItemBetterThanCurrentEquipment(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        EquipmentSlot slot = item.GetResolvedEquipmentSlot();
        if (slot != EquipmentSlot.Weapon &&
            slot != EquipmentSlot.Armor)
        {
            return false;
        }

        ItemStack current = FindAppliedEquipment(slot);
        if (current == null ||
            current.item == null)
        {
            return true;
        }

        return GetEquipmentScore(item) > GetEquipmentScore(current.item);
    }

    void RestoreAppliedItems()
    {
        if (restoredAppliedItems || inventory == null)
        {
            return;
        }

        restoredAppliedItems = true;
        equippedItems.Clear();

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null || stack.item == null || !stack.applied)
            {
                continue;
            }

            if (stack.item.itemType == ItemType.CongPhap)
            {
                RestoreManual(stack);
                continue;
            }

            if (!stack.item.ConsumesWhenUsed())
            {
                RestoreEquipment(stack);
            }
        }

        inventory.MarkDirty();
    }

    void RestoreManual(ItemStack stack)
    {
        if (stack.item.IsManualBroken(stack))
        {
            stack.applied = false;
            stack.broken = true;
            return;
        }

        float power = GetMasteryPower(stack.item, stack.mastery);
        if (power > 0f &&
            !equippedItems.Contains(stack.item) &&
            stack.item.ApplyTo(gameObject, 1, power))
        {
            equippedItems.Add(stack.item);
        }
    }

    void RestoreEquipment(ItemStack stack)
    {
        EquipmentSlot slot = stack.item.GetResolvedEquipmentSlot();
        if (slot != EquipmentSlot.Weapon && slot != EquipmentSlot.Armor)
        {
            stack.applied = false;
            return;
        }

        ItemStack current = FindAppliedEquipment(slot);
        if (current != null && current != stack)
        {
            if (GetEquipmentScore(stack.item) <= GetEquipmentScore(current.item))
            {
                stack.applied = false;
                return;
            }

            UnequipStack(current);
        }

        if (!equippedItems.Contains(stack.item) &&
            stack.item.ApplyTo(gameObject))
        {
            equippedItems.Add(stack.item);
            stack.applied = true;
        }
    }

    void TickManualStudy()
    {
        if (!autoStudyManuals || inventory == null)
        {
            return;
        }

        manualStudyTimer += Time.deltaTime;
        if (manualStudyTimer < Mathf.Max(1f, manualStudyInterval))
        {
            return;
        }

        manualStudyTimer = 0f;
        TryStudyOwnedManual();
    }

    void AccumulateManualUseTime()
    {
        if (inventory == null)
        {
            return;
        }

        float years = GetGameYearsDelta();
        if (years <= 0f)
        {
            return;
        }

        bool changed = false;
        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.item.itemType != ItemType.CongPhap ||
                !stack.applied ||
                stack.broken)
            {
                continue;
            }

            stack.manualUseYears += years;
            GameSaveSystem.AddManualUseYears(stack.item, years);
            changed = true;

            if (stack.item.IsManualBroken(stack))
            {
                stack.broken = true;
                UnequipStack(stack);
            }
        }

        if (changed)
        {
            inventory.MarkDirty();
        }
    }

    float GetGameYearsDelta()
    {
        WorldTimeSystem time = WorldTimeSystem.Instance;
        if (time == null || time.realSecondsPerGameDay <= 0f)
        {
            return 0f;
        }

        const float daysPerGameYear = 360f;
        return Time.deltaTime / time.realSecondsPerGameDay / daysPerGameYear;
    }

    bool TryStudyOwnedManual()
    {
        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.item.itemType != ItemType.CongPhap ||
                stack.mastery == CultivationManualMastery.DaiThanh ||
                stack.broken ||
                stack.item.IsManualBroken(stack) ||
                !stack.item.ShouldNpcUseDirectly())
            {
                continue;
            }

            return TryUseOwnedItem(stack.item);
        }

        return false;
    }

    public bool TryUseOwnedItem(StatItemData item)
    {
        if (item == null || inventory == null || !item.CanUseOn(gameObject))
        {
            return false;
        }

        if (!item.ShouldNpcUseDirectly())
        {
            return false;
        }

        int itemIndex = FindItemIndex(item);
        if (itemIndex < 0)
        {
            return false;
        }

        ItemStack stack = inventory.GetStack(itemIndex);
        if (stack == null || stack.broken)
        {
            return false;
        }

        if (item.itemType == ItemType.CongPhap)
        {
            return TryStudyManual(stack);
        }

        if (!item.ConsumesWhenUsed() && item.itemType == ItemType.PhapBao)
        {
            return TryEquipItem(itemIndex, stack);
        }

        bool applied =
            !(item.ConsumesWhenUsed() && !item.RollUseSuccess()) &&
            item.ApplyTo(gameObject);

        if (!applied && !item.ConsumesWhenUsed())
        {
            return false;
        }

        if (item.ConsumesWhenUsed())
        {
            inventory.RemoveStackAt(itemIndex, 1);
            ItemLifecycleSystem.Notify(ItemLifecycleEventType.Used, item, gameObject);
            return true;
        }

        stack.applied = true;
        inventory.MarkDirty();
        return true;
    }

    ItemStack FindItemStack(StatItemData item)
    {
        if (inventory == null || item == null)
        {
            return null;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack != null &&
                stack.item == item)
            {
                return stack;
            }
        }

        return null;
    }

    bool TryStudyManual(ItemStack stack)
    {
        StatItemData item = stack.item;
        float oldPower = GetMasteryPower(item, stack.mastery);
        AdvanceManualMastery(stack);
        float newPower = GetMasteryPower(item, stack.mastery);

        if (stack.applied && oldPower > 0f)
        {
            item.ApplyTo(gameObject, -1, oldPower);
        }

        if (newPower > 0f && item.ApplyTo(gameObject, 1, newPower))
        {
            if (!equippedItems.Contains(item))
            {
                equippedItems.Add(item);
            }

            ItemEffectSpawner.PlayUseEffect(item, transform);

            stack.applied = true;
            inventory.MarkDirty();
            return true;
        }

        inventory.MarkDirty();
        return false;
    }

    bool TryEquipItem(int itemIndex, ItemStack stack)
    {
        StatItemData item = stack.item;
        EquipmentSlot slot = item.GetResolvedEquipmentSlot();
        if (slot != EquipmentSlot.Weapon && slot != EquipmentSlot.Armor)
        {
            return false;
        }

        ItemStack current = FindAppliedEquipment(slot);
        if (current != null)
        {
            if (current == stack ||
                GetEquipmentScore(item) <= GetEquipmentScore(current.item))
            {
                return false;
            }

            UnequipStack(current);
        }

        if (!item.ApplyTo(gameObject))
        {
            return false;
        }

        ItemEffectSpawner.PlayUseEffect(item, transform);

        if (!equippedItems.Contains(item))
        {
            equippedItems.Add(item);
        }

        stack.applied = true;
        inventory.LoseDurability(itemIndex, item.GetDurabilityLossPerUse());

        if (inventory.GetAmount(item) <= 0)
        {
            UnequipItem(item);
        }

        inventory.MarkDirty();
        return true;
    }

    ItemStack FindAppliedEquipment(EquipmentSlot slot)
    {
        if (inventory == null)
        {
            return null;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                !stack.applied ||
                stack.item.itemType != ItemType.PhapBao)
            {
                continue;
            }

            if (stack.item.GetResolvedEquipmentSlot() == slot)
            {
                return stack;
            }
        }

        return null;
    }

    float GetEquipmentScore(StatItemData item)
    {
        return item != null ? item.GetEquipmentUseScore() : 0f;
    }

    void UnequipStack(ItemStack stack)
    {
        if (stack == null || stack.item == null || !stack.applied)
        {
            return;
        }

        if (stack.item.itemType == ItemType.CongPhap)
        {
            float power = GetMasteryPower(stack.item, stack.mastery);
            if (power > 0f)
            {
                stack.item.ApplyTo(gameObject, -1, power);
            }
        }
        else
        {
            stack.item.RemoveFrom(gameObject);
        }

        stack.applied = false;
        equippedItems.Remove(stack.item);
    }

    public bool TryTeachManualTo(NpcItemCollector target, StatItemData item)
    {
        if (target == null || item == null || item.itemType != ItemType.CongPhap || !item.canBeTaught)
        {
            return false;
        }

        int itemIndex = FindItemIndex(item);
        if (itemIndex < 0)
        {
            return false;
        }

        ItemStack stack = inventory.GetStack(itemIndex);
        if (stack == null || stack.mastery != CultivationManualMastery.DaiThanh || stack.broken)
        {
            return false;
        }

        target.ReceiveItem(item, ItemLifecycleEventType.Taught, true);
        ItemLifecycleSystem.Notify(ItemLifecycleEventType.Taught, item, gameObject, target.gameObject);
        return true;
    }

    public bool UnequipForSale(StatItemData item)
    {
        return UnequipItem(item);
    }

    void AdvanceManualMastery(ItemStack stack)
    {
        if (stack == null || stack.item == null || stack.item.itemType != ItemType.CongPhap)
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

    float GetMasteryPower(StatItemData item, CultivationManualMastery mastery)
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
        if (item == null || inventory == null)
        {
            return false;
        }

        bool removed = false;
        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null || stack.item != item || !stack.applied)
            {
                continue;
            }

            UnequipStack(stack);
            removed = true;
        }

        return removed;
    }

    public bool RemoveOwnedItem(StatItemData item, int amount, ItemLifecycleEventType reason)
    {
        AutoFindReferences();

        if (item == null || inventory == null)
        {
            return false;
        }

        UnequipItem(item);
        bool removed = inventory.RemoveItem(item, amount);

        if (removed)
        {
            ItemLifecycleSystem.Notify(reason, item, gameObject);
        }

        return removed;
    }

    int FindItemIndex(StatItemData item)
    {
        if (inventory == null || item == null)
        {
            return -1;
        }

        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack = inventory.items[i];
            if (stack != null && stack.item == item && stack.amount > 0)
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
            }
        }

        if (inventory != null && !privateInventoryInitialized)
        {
            inventory.UsePrivateNpcRuntimeItems(false);
            privateInventoryInitialized = true;
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
