using System.Collections.Generic;
using System;
using UnityEngine;

[System.Serializable]
public class ItemStack
{
    public StatItemData item;
    public int amount = 1;
    public int durability;
    public int maxDurability;
    public CultivationManualMastery mastery =
        CultivationManualMastery.None;
    public bool applied;
}

public class ItemInventory : MonoBehaviour
{
    static readonly Dictionary<string, List<ItemStack>> sharedItemsByKey =
        new Dictionary<string, List<ItemStack>>();

    public bool shareRuntimeItems = true;
    public bool keepInspectorItemsWhenLoadingSave = true;
    public string runtimeKey = "";

    public List<ItemStack> items =
        new List<ItemStack>();

    public event Action OnChanged;

    void Awake()
    {
        List<ItemStack> inspectorItems =
            CloneItems(items);

        RegisterItems();

        if (!shareRuntimeItems)
        {
            return;
        }

        string key =
            GetRuntimeKey();

        if (sharedItemsByKey.TryGetValue(
                key,
                out List<ItemStack> sharedItems))
        {
            CopyItems(sharedItems, items);
            if (keepInspectorItemsWhenLoadingSave)
            {
                MergeItems(inspectorItems, items);
                CopyItems(items, sharedItems);
            }
            return;
        }

        sharedItems =
            new List<ItemStack>();

        CopyItems(items, sharedItems);
        sharedItemsByKey[key] = sharedItems;
    }

    void Start()
    {
        if (!GameSaveSystem.HasSave)
        {
            return;
        }

        string key =
            GetRuntimeKey();

        if (!GameSaveSystem.TryLoadInventory(
                key,
                items))
        {
            return;
        }

        if (shareRuntimeItems &&
            keepInspectorItemsWhenLoadingSave &&
            sharedItemsByKey.TryGetValue(
                key,
                out List<ItemStack> sharedItems))
        {
            MergeItems(sharedItems, items);
        }

        SaveRuntimeItems();
        NotifyChanged();
    }

    public void UsePrivateRuntimeItems(
        string key,
        bool clearCurrentItems)
    {
        runtimeKey = key;
        shareRuntimeItems = false;

        if (clearCurrentItems)
        {
            items.Clear();
            SaveRuntimeItems();
            NotifyChanged();
            return;
        }

        if (GameSaveSystem.HasSave)
        {
            GameSaveSystem.TryLoadInventory(
                GetRuntimeKey(),
                items);
        }

        SaveRuntimeItems();
        NotifyChanged();
    }

    public void UsePrivateNpcRuntimeItems(bool clearCurrentItems = false)
    {
        UsePrivateRuntimeItems(
            BuildNpcRuntimeKey(),
            clearCurrentItems);
    }

    string BuildNpcRuntimeKey()
    {
        SpawnedWorldActor actor =
            GetComponent<SpawnedWorldActor>();

        if (actor != null &&
            !string.IsNullOrEmpty(actor.persistentId))
        {
            return "WorldActor_" + actor.persistentId;
        }

        Vector3 position = transform.position;
        return "Npc_" + gameObject.scene.name + "_" +
            gameObject.name + "_" +
            Mathf.RoundToInt(position.x * 100f) + "_" +
            Mathf.RoundToInt(position.y * 100f);
    }

    public void AddItem(StatItemData item, int amount = 1)
    {
        if (item == null ||
            amount <= 0)
        {
            return;
        }

        if (item.UsesDurability())
        {
            for (int i = 0; i < amount; i++)
            {
                items.Add(CreateStack(item, 1));
            }

            SaveRuntimeItems();
            NotifyChanged();
            return;
        }

        ItemStack stack =
            items.Find(entry => entry.item == item);

        if (stack != null)
        {
            stack.amount += amount;
            SaveRuntimeItems();
            NotifyChanged();
            return;
        }

        items.Add(
            CreateStack(item, amount));

        SaveRuntimeItems();
        NotifyChanged();
    }

    public bool RemoveItem(StatItemData item, int amount = 1)
    {
        if (item == null ||
            amount <= 0)
        {
            return false;
        }

        if (item.UsesDurability())
        {
            if (GetAmount(item) < amount)
            {
                return false;
            }

            int remaining =
                amount;

            for (int i = items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (items[i] == null ||
                    items[i].item != item)
                {
                    continue;
                }

                int take =
                    Mathf.Min(items[i].amount, remaining);

                items[i].amount -= take;
                remaining -= take;

                if (items[i].amount <= 0)
                {
                    items.RemoveAt(i);
                }
            }

            SaveRuntimeItems();
            NotifyChanged();
            return true;
        }

        ItemStack stack =
            items.Find(entry => entry.item == item);

        if (stack == null ||
            stack.amount < amount)
        {
            return false;
        }

        stack.amount -= amount;

        if (stack.amount <= 0)
        {
            items.Remove(stack);
        }

        SaveRuntimeItems();
        NotifyChanged();
        return true;
    }

    public int GetAmount(StatItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        int total = 0;

        foreach (ItemStack entry in items)
        {
            if (entry != null &&
                entry.item == item)
            {
                total += entry.amount;
            }
        }

        return total;
    }

    public bool UseItemOn(int itemIndex, GameObject target)
    {
        if (target == null ||
            itemIndex < 0 ||
            itemIndex >= items.Count)
        {
            return false;
        }

        ItemStack stack =
            items[itemIndex];

        if (stack == null ||
            stack.item == null ||
            stack.amount <= 0)
        {
            return false;
        }

        if (stack.item.itemType == ItemType.CongPhap)
        {
            float oldPower =
                GetMasteryPower(stack.item, stack.mastery);

            AdvanceManualMastery(stack);

            float newPower =
                GetMasteryPower(stack.item, stack.mastery);

            if (stack.applied &&
                oldPower > 0f)
            {
                stack.item.ApplyTo(target, -1, oldPower);
            }

            if (newPower > 0f)
            {
                if (!stack.item.ApplyTo(target, 1, newPower))
                {
                    return false;
                }

                stack.applied = true;
            }

            SaveRuntimeItems();
            NotifyChanged();
            return true;
        }

        if (!stack.item.ConsumesWhenUsed() &&
            stack.applied)
        {
            return false;
        }

        bool applied =
            !(stack.item.ConsumesWhenUsed() &&
            !stack.item.RollUseSuccess()) &&
            stack.item.ApplyTo(target);

        if (!applied &&
            !stack.item.ConsumesWhenUsed())
        {
            return false;
        }

        if (stack.item.ConsumesWhenUsed())
        {
            RemoveStackAt(itemIndex, 1);
            ItemLifecycleSystem.Notify(
                ItemLifecycleEventType.Used,
                stack.item,
                target);
        }
        else if (stack.item.UsesDurability())
        {
            StatItemData durableItem =
                stack.item;

            stack.applied = true;
            LoseDurability(itemIndex, stack.item.GetDurabilityLossPerUse());

            if (itemIndex >= items.Count ||
                items[itemIndex] != stack)
            {
                durableItem.RemoveFrom(target);
            }
        }
        else
        {
            stack.applied = true;
        }

        SaveRuntimeItems();
        NotifyChanged();
        return true;
    }

    public bool LoseDurability(
        int itemIndex,
        int amount)
    {
        if (itemIndex < 0 ||
            itemIndex >= items.Count ||
            amount <= 0)
        {
            return false;
        }

        ItemStack stack =
            items[itemIndex];

        if (stack == null ||
            stack.item == null ||
            !stack.item.UsesDurability())
        {
            return false;
        }

        EnsureStackRuntimeFields(stack);
        stack.durability -= amount;

        if (stack.durability <= 0 &&
            stack.item.breaksAtZero)
        {
            StatItemData brokenItem =
                stack.item;

            items.RemoveAt(itemIndex);
            ItemLifecycleSystem.Notify(
                ItemLifecycleEventType.Broken,
                brokenItem,
                gameObject);
        }

        SaveRuntimeItems();
        NotifyChanged();
        return true;
    }

    public bool RemoveStackAt(
        int itemIndex,
        int amount = 1)
    {
        if (itemIndex < 0 ||
            itemIndex >= items.Count ||
            amount <= 0)
        {
            return false;
        }

        ItemStack stack =
            items[itemIndex];

        if (stack == null ||
            stack.amount < amount)
        {
            return false;
        }

        stack.amount -= amount;

        if (stack.amount <= 0)
        {
            items.RemoveAt(itemIndex);
        }

        SaveRuntimeItems();
        NotifyChanged();
        return true;
    }

    public ItemStack GetStack(int itemIndex)
    {
        if (itemIndex < 0 ||
            itemIndex >= items.Count)
        {
            return null;
        }

        return items[itemIndex];
    }

    public void MarkDirty()
    {
        SaveRuntimeItems();
        NotifyChanged();
    }

    void NotifyChanged()
    {
        OnChanged?.Invoke();
    }

    void SaveRuntimeItems()
    {
        RegisterItems();

        string key =
            GetRuntimeKey();

        if (shareRuntimeItems)
        {
            if (!sharedItemsByKey.TryGetValue(
                    key,
                    out List<ItemStack> sharedItems))
            {
                sharedItems =
                    new List<ItemStack>();

                sharedItemsByKey[key] = sharedItems;
            }

            CopyItems(items, sharedItems);
        }

        GameSaveSystem.SaveInventory(key, items);
    }

    void RegisterItems()
    {
        foreach (ItemStack stack in items)
        {
            if (stack == null ||
                stack.item == null)
            {
                continue;
            }

            GameSaveSystem.RegisterItem(stack.item);
        }
    }

    public static void ClearRuntimeCache()
    {
        sharedItemsByKey.Clear();
    }

    string GetRuntimeKey()
    {
        if (!string.IsNullOrEmpty(runtimeKey))
        {
            return runtimeKey;
        }

        return gameObject.name;
    }

    void CopyItems(
        List<ItemStack> source,
        List<ItemStack> destination)
    {
        destination.Clear();

        foreach (ItemStack stack in source)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            destination.Add(
                CloneStack(stack));
        }
    }

    List<ItemStack> CloneItems(List<ItemStack> source)
    {
        List<ItemStack> result =
            new List<ItemStack>();

        CopyItems(source, result);
        return result;
    }

    void MergeItems(
        List<ItemStack> source,
        List<ItemStack> destination)
    {
        foreach (ItemStack stack in source)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            ItemStack existing =
                stack.item != null &&
                stack.item.UsesDurability()
                ? null
                : destination.Find(entry => entry.item == stack.item);

            if (existing != null)
            {
                existing.amount =
                    Mathf.Max(existing.amount, stack.amount);
                EnsureStackRuntimeFields(existing);
                continue;
            }

            destination.Add(
                CloneStack(stack));
        }
    }

    ItemStack CreateStack(
        StatItemData item,
        int amount)
    {
        ItemStack stack =
            new ItemStack
            {
                item = item,
                amount = amount
            };

        EnsureStackRuntimeFields(stack);
        return stack;
    }

    ItemStack CloneStack(ItemStack source)
    {
        ItemStack clone =
            new ItemStack
            {
                item = source.item,
                amount = source.amount,
                durability = source.durability,
                maxDurability = source.maxDurability,
                mastery = source.mastery,
                applied = source.applied
            };

        EnsureStackRuntimeFields(clone);
        return clone;
    }

    void EnsureStackRuntimeFields(ItemStack stack)
    {
        if (stack == null ||
            stack.item == null)
        {
            return;
        }

        if (!stack.item.UsesDurability())
        {
            stack.maxDurability = 0;
            stack.durability = 0;
            return;
        }

        int maxDurability =
            stack.item.GetMaxDurability();

        if (stack.maxDurability <= 0)
        {
            stack.maxDurability = maxDurability;
        }

        if (stack.durability <= 0)
        {
            stack.durability = stack.maxDurability;
        }
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
}




