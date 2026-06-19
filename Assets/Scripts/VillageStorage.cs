using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ItemInventory))]
public class VillageStorage : MonoBehaviour
{
    static VillageStorage instance;

    [Header("Storage")]
    public bool becomeGlobalStorage = true;
    public ItemInventory storageInventory;

    public static VillageStorage Instance => instance;

    void Awake()
    {
        if (storageInventory == null)
        {
            storageInventory = GetComponent<ItemInventory>();
        }

        if (storageInventory == null)
        {
            storageInventory = gameObject.AddComponent<ItemInventory>();
        }

        storageInventory.shareRuntimeItems = false;

        if (becomeGlobalStorage)
        {
            instance = this;
        }
    }

    void OnEnable()
    {
        if (becomeGlobalStorage)
        {
            instance = this;
        }
    }

    void OnDisable()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static VillageStorage FindNearest(Vector3 position)
    {
        VillageStorage best = null;
        float bestDistance = float.PositiveInfinity;

        VillageStorage[] storages =
            FindObjectsByType<VillageStorage>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (VillageStorage storage in storages)
        {
            if (storage == null)
            {
                continue;
            }

            float distance = Vector2.Distance(position, storage.transform.position);
            if (distance < bestDistance)
            {
                best = storage;
                bestDistance = distance;
            }
        }

        return best;
    }

    public bool Store(StatItemData item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return false;
        }

        storageInventory.AddItem(item, amount);
        return true;
    }

    public bool Store(ItemInventory inventory)
    {
        if (inventory == null)
        {
            return false;
        }

        bool storedAny = false;
        List<ItemStack> snapshot = new List<ItemStack>(inventory.items);
        foreach (ItemStack stack in snapshot)
        {
            if (stack == null || stack.item == null || stack.amount <= 0)
            {
                continue;
            }

            storageInventory.AddItem(stack.item, stack.amount);
            inventory.RemoveItem(stack.item, stack.amount);
            storedAny = true;
        }

        return storedAny;
    }

    public int GetAmount(StatItemData item)
    {
        return storageInventory != null && item != null
            ? storageInventory.GetAmount(item)
            : 0;
    }
}
