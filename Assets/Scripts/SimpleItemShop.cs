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
    static readonly Dictionary<string, Dictionary<string, int>> stockByShopKey =
        new Dictionary<string, Dictionary<string, int>>();

    [Header("Auto Load")]
    public bool autoLoadItemsFromAssets = true;
    public bool shareRuntimeStock = true;
    public string runtimeStockKey = "";
    public string[] itemFolders =
    {
        "Assets/Item"
    };
    public int defaultStockAmount = 99;

    [Header("NPC Seller")]
    public bool sellFromNpcInventory;
    public bool allowPlayerBuyFromNpcInventory;
    public ItemInventory sellerInventory;
    public GameObject sellerObject;
    public bool refreshNpcInventoryBeforeOpen = true;

    public List<ShopItemSlot> items =
        new List<ShopItemSlot>();

    public int ItemCount => items.Count;

    void Awake()
    {
        RefreshFromSellerInventory();
        RegisterItems();

        if (!sellFromNpcInventory)
        {
            LoadRuntimeStock();
        }
    }

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
        RefreshFromSellerInventory();

        if (buyerInventory == null ||
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
            !NpcEconomy.CanTradeNormally(slot.item) ||
            !CanBuyerPay(
                buyerWallet,
                NpcEconomy.GetTradePrice(
                    slot.item,
                    NpcTradeContext.MarketBuy)))
        {
            return false;
        }

        int price =
            NpcEconomy.GetTradePrice(
                slot.item,
                NpcTradeContext.MarketBuy);

        if (sellFromNpcInventory &&
            sellerInventory != null)
        {
            if (!allowPlayerBuyFromNpcInventory)
            {
                return false;
            }

            NpcItemCollector collector =
                sellerObject != null
                ? sellerObject.GetComponent<NpcItemCollector>()
                : sellerInventory.GetComponent<NpcItemCollector>();

            if (collector != null)
            {
                if (!collector.RemoveOwnedItem(
                        slot.item,
                        1,
                        ItemLifecycleEventType.Sold))
                {
                    return false;
                }
            }
            else if (!sellerInventory.RemoveItem(slot.item, 1))
            {
                return false;
            }

            if (buyerWallet != null)
            {
                buyerWallet.Pay(price);
            }

            AddMoneyToSeller(price);
            RefreshFromSellerInventory();
        }
        else
        {
            if (buyerWallet != null)
            {
                buyerWallet.Pay(price);
            }

            slot.amount -= 1;
            SaveRuntimeStock();
        }

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
            !NpcEconomy.CanTradeNormally(slot.item))
        {
            return false;
        }

        int price =
            NpcEconomy.GetNpcBuyPrice(
                slot.item,
                buyer.gameObject,
                NpcTradeContext.MarketBuy);

        if (buyer.money < price)
        {
            return false;
        }

        buyer.money -= price;
        slot.amount -= 1;
        SaveRuntimeStock();
        buyerInventory.AddItem(slot.item, 1);

        return true;
    }

    bool CanBuyerPay(PlayerWallet buyerWallet, int price)
    {
        if (price <= 0)
        {
            return true;
        }

        if (buyerWallet == null)
        {
            Debug.LogWarning(
                "SimpleItemShop missing PlayerWallet. Cannot buy item.");

            return false;
        }

        return buyerWallet.CanPay(price);
    }

    public void RefreshFromSellerInventory()
    {
        if (!sellFromNpcInventory ||
            sellerInventory == null)
        {
            return;
        }

        items.Clear();

        foreach (ItemStack stack in sellerInventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            items.Add(
                new ShopItemSlot
                {
                    item = stack.item,
                    amount = stack.amount
                });
        }

        RegisterItems();
    }

    void AddMoneyToSeller(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GameObject target =
            sellerObject != null
            ? sellerObject
            : sellerInventory != null
                ? sellerInventory.gameObject
                : null;

        if (target == null)
        {
            return;
        }

        NpcEconomy.AddNpcMoney(target, amount);
    }

    void LoadRuntimeStock()
    {
        if (!shareRuntimeStock)
        {
            return;
        }

        if (GameSaveSystem.HasSave &&
            GameSaveSystem.TryLoadShopStock(
                GetRuntimeStockKey(),
                items))
        {
            SaveRuntimeStock();
            return;
        }

        string key =
            GetRuntimeStockKey();

        if (!stockByShopKey.TryGetValue(
                key,
                out Dictionary<string, int> stock))
        {
            SaveRuntimeStock();
            return;
        }

        foreach (ShopItemSlot slot in items)
        {
            if (slot == null ||
                slot.item == null)
            {
                continue;
            }

            string itemKey =
                GetItemKey(slot.item);

            if (stock.TryGetValue(itemKey, out int amount))
            {
                slot.amount = amount;
            }
        }
    }

    void SaveRuntimeStock()
    {
        if (!shareRuntimeStock)
        {
            return;
        }

        RegisterItems();

        string key =
            GetRuntimeStockKey();

        Dictionary<string, int> stock =
            new Dictionary<string, int>();

        foreach (ShopItemSlot slot in items)
        {
            if (slot == null ||
                slot.item == null)
            {
                continue;
            }

            stock[GetItemKey(slot.item)] =
                slot.amount;
        }

        stockByShopKey[key] = stock;
        GameSaveSystem.SaveShopStock(key, items);
    }

    string GetRuntimeStockKey()
    {
        if (!string.IsNullOrEmpty(runtimeStockKey))
        {
            return runtimeStockKey;
        }

        return gameObject.name;
    }

    string GetItemKey(StatItemData item)
    {
        return GameSaveSystem.GetItemKey(item);
    }

    void RegisterItems()
    {
        foreach (ShopItemSlot slot in items)
        {
            if (slot == null ||
                slot.item == null)
            {
                continue;
            }

            GameSaveSystem.RegisterItem(slot.item);
        }
    }

    public static void ClearRuntimeStockCache()
    {
        stockByShopKey.Clear();
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
