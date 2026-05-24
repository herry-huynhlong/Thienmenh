using System.Collections.Generic;
using System;
using UnityEngine;

[System.Serializable]
public class ItemStack
{
    public StatItemData item;
    public int amount = 1;
}

public class ItemInventory : MonoBehaviour
{
    static readonly Dictionary<string, List<ItemStack>> sharedItemsByKey =
        new Dictionary<string, List<ItemStack>>();

    public bool shareRuntimeItems = true;
    public string runtimeKey = "";

    public List<ItemStack> items =
        new List<ItemStack>();

    public event Action OnChanged;

    void Awake()
    {
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
            return;
        }

        sharedItems =
            new List<ItemStack>();

        CopyItems(items, sharedItems);
        sharedItemsByKey[key] = sharedItems;
    }

    void Start()
    {
        if (!shareRuntimeItems ||
            !GameSaveSystem.HasSave)
        {
            return;
        }

        if (GameSaveSystem.TryLoadInventory(
                GetRuntimeKey(),
                items))
        {
            SaveSharedItems();
            NotifyChanged();
        }
    }

    public void AddItem(StatItemData item, int amount = 1)
    {
        if (item == null ||
            amount <= 0)
        {
            return;
        }

        ItemStack stack =
            items.Find(entry => entry.item == item);

        if (stack != null)
        {
            stack.amount += amount;
            SaveSharedItems();
            NotifyChanged();
            return;
        }

        items.Add(
            new ItemStack
            {
                item = item,
                amount = amount
            });

        SaveSharedItems();
        NotifyChanged();
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

        if (!stack.item.ApplyTo(target))
        {
            return false;
        }

        if (stack.item.consumeOnUse)
        {
            stack.amount -= 1;

            if (stack.amount <= 0)
            {
                items.RemoveAt(itemIndex);
            }
        }

        SaveSharedItems();
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

    void NotifyChanged()
    {
        OnChanged?.Invoke();
    }

    void SaveSharedItems()
    {
        if (!shareRuntimeItems)
        {
            return;
        }

        RegisterItems();

        string key =
            GetRuntimeKey();

        if (!sharedItemsByKey.TryGetValue(
                key,
                out List<ItemStack> sharedItems))
        {
            sharedItems =
                new List<ItemStack>();

            sharedItemsByKey[key] = sharedItems;
        }

        CopyItems(items, sharedItems);
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
                new ItemStack
                {
                    item = stack.item,
                    amount = stack.amount
                });
        }
    }
}
