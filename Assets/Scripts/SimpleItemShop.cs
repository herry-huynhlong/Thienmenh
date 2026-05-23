using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ShopItemSlot
{
    public StatItemData item;
    public int amount = 1;
}

public class SimpleItemShop : MonoBehaviour
{
    [Header("Auto Load")]
    public bool autoLoadItemsFromAssets = true;
    public string[] itemFolders =
    {
        "Assets/Item"
    };
    public int defaultStockAmount = 99;

    public List<ShopItemSlot> items =
        new List<ShopItemSlot>();

    public int ItemCount => items.Count;

    public ShopItemSlot GetSlot(int itemIndex)
    {
        if (itemIndex < 0 ||
            itemIndex >= items.Count)
        {
            return null;
        }

        return items[itemIndex];
    }

    public bool BuyToInventory(
        int itemIndex,
        PlayerWallet buyerWallet,
        ItemInventory buyerInventory)
    {
        if (buyerWallet == null ||
            buyerInventory == null ||
            itemIndex < 0 ||
            itemIndex >= items.Count)
        {
            return false;
        }

        ShopItemSlot slot =
            items[itemIndex];

        if (slot == null ||
            slot.item == null ||
            slot.amount <= 0 ||
            !buyerWallet.CanPay(slot.item.price))
        {
            return false;
        }

        buyerWallet.Pay(slot.item.price);
        slot.amount -= 1;
        buyerInventory.AddItem(slot.item, 1);

        return true;
    }

    public bool BuyNpcItemToInventory(
        int itemIndex,
        SmartNpcAI buyer,
        ItemInventory buyerInventory)
    {
        if (buyer == null ||
            buyerInventory == null ||
            itemIndex < 0 ||
            itemIndex >= items.Count)
        {
            return false;
        }

        ShopItemSlot slot =
            items[itemIndex];

        if (slot == null ||
            slot.item == null ||
            slot.amount <= 0 ||
            buyer.money < slot.item.price)
        {
            return false;
        }

        buyer.money -= slot.item.price;
        slot.amount -= 1;
        buyerInventory.AddItem(slot.item, 1);

        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoLoadItemsFromAssets)
        {
            return;
        }

        RefreshItemsFromAssets();
    }

    [ContextMenu("Refresh Items From Assets")]
    public void RefreshItemsFromAssets()
    {
        if (itemFolders == null ||
            itemFolders.Length == 0)
        {
            return;
        }

        Dictionary<StatItemData, int> existingAmounts =
            new Dictionary<StatItemData, int>();

        foreach (ShopItemSlot slot in items)
        {
            if (slot == null ||
                slot.item == null)
            {
                continue;
            }

            existingAmounts[slot.item] = slot.amount;
        }

        string[] guids =
            AssetDatabase.FindAssets(
                "t:StatItemData",
                itemFolders);

        List<ShopItemSlot> loadedItems =
            new List<ShopItemSlot>();

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            StatItemData item =
                AssetDatabase.LoadAssetAtPath<StatItemData>(
                    path);

            if (item == null)
            {
                continue;
            }

            int amount =
                existingAmounts.TryGetValue(item, out int existingAmount)
                ? existingAmount
                : defaultStockAmount;

            loadedItems.Add(
                new ShopItemSlot
                {
                    item = item,
                    amount = amount
                });
        }

        loadedItems.Sort(
            (a, b) =>
            {
                int typeCompare =
                    a.item.itemType.CompareTo(b.item.itemType);

                if (typeCompare != 0)
                {
                    return typeCompare;
                }

                int gradeCompare =
                    a.item.grade.CompareTo(b.item.grade);

                if (gradeCompare != 0)
                {
                    return gradeCompare;
                }

                return string.Compare(
                    a.item.itemName,
                    b.item.itemName,
                    System.StringComparison.Ordinal);
            });

        items = loadedItems;

        EditorUtility.SetDirty(this);
    }
#endif
}
