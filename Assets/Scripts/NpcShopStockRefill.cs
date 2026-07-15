using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NpcShopStockRefill : MonoBehaviour
{
    public SimpleItemShop sourceShop;
    public SimpleItemShop npcShop;
    public ItemInventory sellerInventory;

    [Header("Catalog Fallback")]
    public bool autoLoadCatalogFromAssets = true;
    public string[] itemFolders =
    {
        "Assets/Item"
    };
    public List<StatItemData> catalogItems = new List<StatItemData>();
    public bool mirrorConfiguredNpcShopItemsToSellerInventory;

    [Header("Stock")]
    public int lowGradeKinds = 20;
    public int middleGradeKinds = 10;
    public int upperGradeKinds = 1;
    public int amountPerKind = 1;
    public int haGradeStockAmount = 100;
    public int trungGradeStockAmount = 50;
    public int thuongGradeStockAmount = 10;
    public bool refillOnStart = true;
    public bool replaceStockOnRefill = true;
    public bool excludeImmortalGrade = true;
    public float npcShopPriceMultiplier = 1.5f;

    bool refilling;
    bool stockReady;
    int lastCatalogSignature;

    void Awake()
    {
        AutoBind();
    }

    void Start()
    {
        if (refillOnStart)
        {
            EnsureStock();
            StartCoroutine(EnsureMirroredStockAfterStartup());
        }
    }

    IEnumerator EnsureMirroredStockAfterStartup()
    {
        yield return null;

        if (!refillOnStart)
        {
            yield break;
        }

        AutoBind();
        if (!ShouldMirrorConfiguredNpcShopItems())
        {
            yield break;
        }

        stockReady = false;
        EnsureStock();
    }

    [ContextMenu("Ensure NPC Shop Stock")]
    public void EnsureStock()
    {
        if (refilling)
        {
            return;
        }

        refilling = true;
        AutoBind();

        if (sellerInventory == null || npcShop == null)
        {
            refilling = false;
            return;
        }

        EnsureCatalogItems();
        CaptureCatalogFromNpcShopItemsIfNeeded();

        bool shouldMirrorConfiguredItems =
            ShouldMirrorConfiguredNpcShopItems();

        if (sourceShop == null &&
            catalogItems.Count == 0 &&
            !shouldMirrorConfiguredItems)
        {
            refilling = false;
            return;
        }

        int catalogSignature = GetCatalogSignature();
        if (stockReady &&
            catalogSignature == lastCatalogSignature &&
            HasSellerStock())
        {
            ConfigureNpcShop();
            refilling = false;
            return;
        }

        if (shouldMirrorConfiguredItems)
        {
            RefillFromConfiguredNpcShopItems();

            ConfigureNpcShop();
            stockReady = true;
            lastCatalogSignature = catalogSignature;
            npcShop.RefreshFromSellerInventory();
            refilling = false;
            return;
        }

        List<StatItemData> lowGrade = new List<StatItemData>();
        List<StatItemData> middleGrade = new List<StatItemData>();
        List<StatItemData> upperGrade = new List<StatItemData>();

        CollectCatalogItems(lowGrade, middleGrade, upperGrade);

        if (replaceStockOnRefill)
        {
            sellerInventory.items.Clear();
        }

        AddRandomKinds(lowGrade, lowGradeKinds);
        AddRandomKinds(middleGrade, middleGradeKinds);
        AddRandomKinds(upperGrade, upperGradeKinds);

        sellerInventory.MarkDirty();

        ConfigureNpcShop();
        stockReady = true;
        lastCatalogSignature = catalogSignature;
        npcShop.RefreshFromSellerInventory();
        refilling = false;
    }

    bool HasSellerStock()
    {
        if (sellerInventory == null)
        {
            return false;
        }

        foreach (ItemStack stack in sellerInventory.items)
        {
            if (stack != null &&
                stack.item != null &&
                stack.amount > 0)
            {
                return true;
            }
        }

        return false;
    }
    void AutoBind()
    {
        if (npcShop == null)
        {
            npcShop = GetComponent<SimpleItemShop>();
        }

        if (sellerInventory == null)
        {
            sellerInventory = GetComponent<ItemInventory>();
        }

        if (sellerInventory == null)
        {
            sellerInventory = gameObject.AddComponent<ItemInventory>();
        }

        if (sourceShop == null)
        {
            SimpleItemShop[] shops =
                FindObjectsByType<SimpleItemShop>(FindObjectsInactive.Include);

            foreach (SimpleItemShop shop in shops)
            {
                if (shop != null && shop != npcShop && !shop.sellFromNpcInventory)
                {
                    sourceShop = shop;
                    break;
                }
            }
        }
    }

    void ConfigureNpcShop()
    {
        npcShop.sellFromNpcInventory = true;
        npcShop.allowPlayerBuyFromNpcInventory = true;
        npcShop.sellerInventory = sellerInventory;
        npcShop.sellerObject = gameObject;
        npcShop.refreshNpcInventoryBeforeOpen = true;
        npcShop.priceMultiplier = Mathf.Max(0f, npcShopPriceMultiplier);
    }
    void CaptureCatalogFromNpcShopItemsIfNeeded()
    {
        if (sourceShop != null ||
            catalogItems.Count > 0 ||
            npcShop == null ||
            npcShop.items == null)
        {
            return;
        }

        HashSet<StatItemData> seen = new HashSet<StatItemData>();

        foreach (ShopItemSlot slot in npcShop.items)
        {
            StatItemData item = slot != null ? slot.item : null;
            if (item == null || seen.Contains(item))
            {
                continue;
            }

            if (excludeImmortalGrade && item.grade == ItemGrade.Tien)
            {
                continue;
            }

            seen.Add(item);
            catalogItems.Add(item);
        }
    }

    bool ShouldMirrorConfiguredNpcShopItems()
    {
        if (!mirrorConfiguredNpcShopItemsToSellerInventory ||
            sourceShop != null ||
            npcShop == null ||
            npcShop.items == null)
        {
            return false;
        }

        for (int i = 0; i < npcShop.items.Count; i++)
        {
            ShopItemSlot slot = npcShop.items[i];
            if (slot == null ||
                slot.item == null ||
                slot.amount <= 0)
            {
                continue;
            }

            if (excludeImmortalGrade &&
                slot.item.grade == ItemGrade.Tien)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    void RefillFromConfiguredNpcShopItems()
    {
        if (sellerInventory == null ||
            npcShop == null ||
            npcShop.items == null)
        {
            return;
        }

        if (replaceStockOnRefill)
        {
            sellerInventory.items.Clear();
        }

        foreach (ShopItemSlot slot in npcShop.items)
        {
            if (slot == null ||
                slot.item == null ||
                slot.amount <= 0)
            {
                continue;
            }

            if (excludeImmortalGrade &&
                slot.item.grade == ItemGrade.Tien)
            {
                continue;
            }

            sellerInventory.AddItem(
                slot.item,
                GetConfiguredStockAmountForGrade(
                    slot.item,
                    slot.amount));
        }

        sellerInventory.MarkDirty();
    }

    int GetConfiguredStockAmountForGrade(
        StatItemData item,
        int fallbackAmount)
    {
        if (item == null)
        {
            return Mathf.Max(1, fallbackAmount);
        }

        switch (item.grade)
        {
            case ItemGrade.Ha:
                return Mathf.Max(1, haGradeStockAmount);

            case ItemGrade.Trung:
                return Mathf.Max(1, trungGradeStockAmount);

            case ItemGrade.Thuong:
                return Mathf.Max(1, thuongGradeStockAmount);

            default:
                return Mathf.Max(1, fallbackAmount);
        }
    }

    int GetCatalogSignature()
    {
        unchecked
        {
            int hash = 17;

            if (ShouldMirrorConfiguredNpcShopItems())
            {
                foreach (ShopItemSlot slot in npcShop.items)
                {
                    StatItemData item =
                        slot != null
                            ? slot.item
                            : null;
                    hash = AddItemToSignature(hash, item);
                    hash = hash * 31 + Mathf.Max(0, slot != null ? slot.amount : 0);
                }
            }
            else if (sourceShop != null)
            {
                foreach (ShopItemSlot slot in sourceShop.items)
                {
                    hash = AddItemToSignature(hash, slot != null ? slot.item : null);
                }
            }
            else
            {
                foreach (StatItemData item in catalogItems)
                {
                    hash = AddItemToSignature(hash, item);
                }
            }

            return hash;
        }
    }

    int AddItemToSignature(int hash, StatItemData item)
    {
        if (item == null)
        {
            return hash;
        }

        if (excludeImmortalGrade && item.grade == ItemGrade.Tien)
        {
            return hash;
        }

        hash = hash * 31 + item.GetInstanceID();
        hash = hash * 31 + (int)item.grade;
        return hash;
    }

    void CollectCatalogItems(
        List<StatItemData> lowGrade,
        List<StatItemData> middleGrade,
        List<StatItemData> upperGrade)
    {
        HashSet<StatItemData> seen = new HashSet<StatItemData>();

        if (sourceShop != null)
        {
            foreach (ShopItemSlot slot in sourceShop.items)
            {
                AddCatalogItem(
                    slot != null ? slot.item : null,
                    seen,
                    lowGrade,
                    middleGrade,
                    upperGrade);
            }

            return;
        }

        foreach (StatItemData item in catalogItems)
        {
            AddCatalogItem(item, seen, lowGrade, middleGrade, upperGrade);
        }
    }

    void AddCatalogItem(
        StatItemData item,
        HashSet<StatItemData> seen,
        List<StatItemData> lowGrade,
        List<StatItemData> middleGrade,
        List<StatItemData> upperGrade)
    {
        if (item == null || seen.Contains(item))
        {
            return;
        }

        seen.Add(item);

        if (excludeImmortalGrade && item.grade == ItemGrade.Tien)
        {
            return;
        }

        switch (item.grade)
        {
            case ItemGrade.Ha:
                lowGrade.Add(item);
                break;

            case ItemGrade.Trung:
                middleGrade.Add(item);
                break;

            case ItemGrade.Thuong:
                upperGrade.Add(item);
                break;
        }
    }

    void EnsureCatalogItems()
    {
#if UNITY_EDITOR
        if (autoLoadCatalogFromAssets &&
            sourceShop == null &&
            catalogItems.Count == 0)
        {
            RefreshCatalogFromAssets();
        }
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (autoLoadCatalogFromAssets)
        {
            RefreshCatalogFromAssets();
        }
    }

    [ContextMenu("Refresh Catalog From Assets")]
    public void RefreshCatalogFromAssets()
    {
        if (itemFolders == null || itemFolders.Length == 0)
        {
            return;
        }

        string[] guids =
            AssetDatabase.FindAssets(
                "t:StatItemData",
                itemFolders);

        HashSet<StatItemData> seen = new HashSet<StatItemData>();
        List<StatItemData> loadedItems = new List<StatItemData>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StatItemData item =
                AssetDatabase.LoadAssetAtPath<StatItemData>(path);

            if (item == null || seen.Contains(item))
            {
                continue;
            }

            if (excludeImmortalGrade && item.grade == ItemGrade.Tien)
            {
                continue;
            }

            seen.Add(item);
            loadedItems.Add(item);
        }

        loadedItems.Sort(
            (a, b) =>
            {
                int gradeCompare = a.grade.CompareTo(b.grade);
                if (gradeCompare != 0)
                {
                    return gradeCompare;
                }

                int typeCompare = a.itemType.CompareTo(b.itemType);
                if (typeCompare != 0)
                {
                    return typeCompare;
                }

                return string.Compare(
                    a.itemName,
                    b.itemName,
                    System.StringComparison.Ordinal);
            });

        catalogItems = loadedItems;
        EditorUtility.SetDirty(this);
    }
#endif

    void AddRandomKinds(List<StatItemData> candidates, int targetKinds)
    {
        if (candidates == null || candidates.Count == 0 || targetKinds <= 0)
        {
            return;
        }

        Shuffle(candidates);

        int count = Mathf.Min(targetKinds, candidates.Count);
        int amount = Mathf.Max(1, amountPerKind);

        for (int i = 0; i < count; i++)
        {
            sellerInventory.AddItem(candidates[i], amount);
        }
    }

    void Shuffle(List<StatItemData> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            StatItemData temp = items[i];
            items[i] = items[swapIndex];
            items[swapIndex] = temp;
        }
    }
}
