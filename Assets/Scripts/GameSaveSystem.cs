using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class SavedItemStack
{
    public string itemKey;
    public int amount;
    public int durability;
    public int maxDurability;
    public int mastery;
    public float manualUseYears;
    public bool broken;
    public bool applied;
}

[Serializable]
public class SavedInventoryData
{
    public List<SavedItemStack> items =
        new List<SavedItemStack>();
}

[Serializable]
public class SavedShopStock
{
    public string itemKey;
    public int amount;
}

[Serializable]
public class SavedShopData
{
    public List<SavedShopStock> stocks =
        new List<SavedShopStock>();
}

public static class GameSaveSystem
{
    const string SavePrefix = "ThienMenh.Save.";
    const string HasSaveKey = SavePrefix + "HasSave";
    const string SceneKey = SavePrefix + "CurrentScene";
    const string InventoryPrefix = SavePrefix + "Inventory.";
    const string ShopPrefix = SavePrefix + "Shop.";
    const string DynamicKeysKey = SavePrefix + "DynamicKeys";
    const string WorldTimeYearKey = SavePrefix + "WorldTime.Year";
    const string WorldTimeMonthKey = SavePrefix + "WorldTime.Month";
    const string WorldTimeDayKey = SavePrefix + "WorldTime.Day";
    const string WorldTimeHourKey = SavePrefix + "WorldTime.Hour";
    const string ManualUsePrefix = SavePrefix + "ManualUse.";
    const string LastSceneKey = "LastScene";
    const string MapReturnSceneKey = "MapReturnScene";
    const string NewGameIntroFlagKey = "ThienMenh_NewGame";

    static readonly Dictionary<string, StatItemData> itemByKey =
        new Dictionary<string, StatItemData>();

    static bool savedGameLoadRequested;
    static bool pendingPlayerPrefsCommit;

    public static bool SavedGameLoadRequested =>
        savedGameLoadRequested;

    public static bool ShouldLoadSavedGame =>
        savedGameLoadRequested && HasSave;

    public static bool HasSave =>
        PlayerPrefs.GetInt(HasSaveKey, 0) == 1;

    public static void RequestNewGameStart()
    {
        savedGameLoadRequested = false;
    }

    public static void RequestContinueGameStart()
    {
        savedGameLoadRequested = HasSave;
    }

    public static void MarkSaveExists()
    {
        PlayerPrefs.SetInt(HasSaveKey, 1);
        QueuePendingCommit();
    }

    public static void QueuePendingCommit()
    {
        pendingPlayerPrefsCommit = true;
    }

    public static void FlushPendingCommit()
    {
        if (!pendingPlayerPrefsCommit)
        {
            return;
        }

        PlayerPrefs.Save();
        pendingPlayerPrefsCommit = false;
    }

    public static void ClearSave()
    {
        savedGameLoadRequested = false;
        itemByKey.Clear();
        ItemInventory.ClearRuntimeCache();
        SimpleItemShop.ClearRuntimeStockCache();
        PlayerWallet.ClearSave();
        PlayerWallet.ResetRuntime();
        HeavenDaoSystem.ClearSavedState();
        FullGameSaveController.DeletePersistedSaveFiles();

        List<string> keysToDelete =
            new List<string>();

        foreach (string key in PlayerPrefsKeys())
        {
            if (key.StartsWith(SavePrefix))
            {
                keysToDelete.Add(key);
            }
        }

        AddDynamicSaveKeys(keysToDelete);
        AddNonPrefixedGameplayKeys(keysToDelete);

        foreach (string key in keysToDelete)
        {
            PlayerPrefs.DeleteKey(key);
        }

        pendingPlayerPrefsCommit = false;
        PlayerPrefs.Save();
    }

    public static void RegisterItem(StatItemData item)
    {
        if (item == null)
        {
            return;
        }

        RegisterItemKey(GetItemKey(item), item, true);
        RegisterItemKey(item.name, item, false);
        RegisterItemKey(item.itemName, item, false);
    }

    public static StatItemData FindItem(string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey))
        {
            return null;
        }

        itemByKey.TryGetValue(itemKey.Trim(), out StatItemData item);
        return item;
    }

    public static string GetItemKey(StatItemData item)
    {
        if (item == null)
        {
            return "";
        }

        return item.ItemId;
    }

    static void RegisterItemKey(
        string itemKey,
        StatItemData item,
        bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(itemKey) || item == null)
        {
            return;
        }

        string normalizedKey = itemKey.Trim();
        if (overwrite || !itemByKey.ContainsKey(normalizedKey))
        {
            itemByKey[normalizedKey] = item;
        }
    }

    public static float GetManualUseYears(StatItemData item)
    {
        if (item == null)
        {
            return 0f;
        }

        float years = PlayerPrefs.GetFloat(
            ManualUsePrefix + GetItemKey(item),
            0f);

        if (!string.IsNullOrEmpty(item.name))
        {
            years = Mathf.Max(
                years,
                PlayerPrefs.GetFloat(ManualUsePrefix + item.name, 0f));
        }

        if (!string.IsNullOrEmpty(item.itemName))
        {
            years = Mathf.Max(
                years,
                PlayerPrefs.GetFloat(ManualUsePrefix + item.itemName, 0f));
        }

        return years;
    }

    public static void AddManualUseYears(StatItemData item, float years)
    {
        if (item == null || years <= 0f)
        {
            return;
        }

        string key = ManualUsePrefix + GetItemKey(item);
        PlayerPrefs.SetFloat(
            key,
            Mathf.Max(0f, PlayerPrefs.GetFloat(key, 0f) + years));
        RegisterDynamicSaveKey(key);
        MarkSaveExists();
    }
    public static void SaveCurrentScene(string sceneName = "")
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName =
                SceneManager.GetActiveScene().name;
        }

        PlayerPrefs.SetString(SceneKey, sceneName);
        MarkSaveExists();
        QueuePendingCommit();
    }

    public static string LoadCurrentScene(string fallbackScene)
    {
        return PlayerPrefs.GetString(
            SceneKey,
            fallbackScene);
    }
    public static void RegisterDynamicSaveKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        string existing = PlayerPrefs.GetString(DynamicKeysKey, "");
        string wrapped = "|" + key + "|";
        if (("|" + existing + "|").Contains(wrapped))
        {
            return;
        }

        PlayerPrefs.SetString(
            DynamicKeysKey,
            string.IsNullOrEmpty(existing) ? key : existing + "|" + key);
    }

    static void AddDynamicSaveKeys(List<string> keysToDelete)
    {
        string existing = PlayerPrefs.GetString(DynamicKeysKey, "");
        if (string.IsNullOrEmpty(existing))
        {
            return;
        }

        string[] keys = existing.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string key in keys)
        {
            if (!string.IsNullOrEmpty(key) && !keysToDelete.Contains(key))
            {
                keysToDelete.Add(key);
            }
        }
    }

    static void AddNonPrefixedGameplayKeys(List<string> keysToDelete)
    {
        AddKeyIfMissing(keysToDelete, LastSceneKey);
        AddKeyIfMissing(keysToDelete, MapReturnSceneKey);
        AddKeyIfMissing(keysToDelete, NewGameIntroFlagKey);
    }

    static void AddKeyIfMissing(
        List<string> keysToDelete,
        string key)
    {
        if (string.IsNullOrEmpty(key) ||
            keysToDelete.Contains(key))
        {
            return;
        }

        keysToDelete.Add(key);
    }

    public static void SaveWorldTime(int year, int month, int day, float hour)
    {
        PlayerPrefs.SetInt(WorldTimeYearKey, Mathf.Max(1, year));
        PlayerPrefs.SetInt(WorldTimeMonthKey, Mathf.Max(1, month));
        PlayerPrefs.SetInt(WorldTimeDayKey, Mathf.Max(1, day));
        PlayerPrefs.SetFloat(WorldTimeHourKey, Mathf.Clamp(hour, 0f, 23.999f));
        MarkSaveExists();
        QueuePendingCommit();
    }

    public static bool TryLoadWorldTime(
        out int year,
        out int month,
        out int day,
        out float hour)
    {
        year = 1;
        month = 1;
        day = 1;
        hour = 6f;

        if (!HasSave || !PlayerPrefs.HasKey(WorldTimeDayKey))
        {
            return false;
        }

        year = Mathf.Max(1, PlayerPrefs.GetInt(WorldTimeYearKey, year));
        month = Mathf.Max(1, PlayerPrefs.GetInt(WorldTimeMonthKey, month));
        day = Mathf.Max(1, PlayerPrefs.GetInt(WorldTimeDayKey, day));
        hour = Mathf.Clamp(PlayerPrefs.GetFloat(WorldTimeHourKey, hour), 0f, 23.999f);
        return true;
    }

    public static void SaveInventory(
        string inventoryKey,
        List<ItemStack> items)
    {
        if (string.IsNullOrEmpty(inventoryKey) ||
            items == null)
        {
            return;
        }

        string saveKey =
            InventoryPrefix + inventoryKey;

        RegisterDynamicSaveKey(saveKey);

        SavedInventoryData data =
            new SavedInventoryData();

        foreach (ItemStack stack in items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            RegisterItem(stack.item);

            data.items.Add(
                new SavedItemStack
                {
                    itemKey = GetItemKey(stack.item),
                    amount = stack.amount,
                    durability = stack.durability,
                    maxDurability = stack.maxDurability,
                    mastery = (int)stack.mastery,
                    manualUseYears = stack.manualUseYears,
                    broken = stack.broken,
                    applied = stack.applied
                });
        }

        PlayerPrefs.SetString(
            saveKey,
            JsonUtility.ToJson(data));

        MarkSaveExists();
        QueuePendingCommit();
    }

    public static bool TryLoadInventory(
        string inventoryKey,
        List<ItemStack> target)
    {
        if (string.IsNullOrEmpty(inventoryKey) ||
            target == null)
        {
            return false;
        }

        string key =
            InventoryPrefix + inventoryKey;

        if (!PlayerPrefs.HasKey(key))
        {
            return false;
        }

        RegisterDynamicSaveKey(key);

        if (!TryDeserializeJson(
                key,
                PlayerPrefs.GetString(key),
                out SavedInventoryData data) ||
            data == null ||
            data.items == null)
        {
            return false;
        }

        List<ItemStack> loadedItems =
            new List<ItemStack>(data.items.Count);
        List<string> unresolvedItemKeys =
            null;

        foreach (SavedItemStack savedStack in data.items)
        {
            if (savedStack == null ||
                savedStack.amount <= 0)
            {
                continue;
            }

            StatItemData item =
                FindItem(savedStack.itemKey);

            if (item == null)
            {
                if (!string.IsNullOrWhiteSpace(savedStack.itemKey))
                {
                    if (unresolvedItemKeys == null)
                    {
                        unresolvedItemKeys =
                            new List<string>();
                    }

                    unresolvedItemKeys.Add(savedStack.itemKey);
                }

                continue;
            }

            loadedItems.Add(
                new ItemStack
                {
                    item = item,
                    amount = savedStack.amount,
                    durability = savedStack.durability,
                    maxDurability = savedStack.maxDurability,
                    mastery =
                        (CultivationManualMastery)savedStack.mastery,
                    manualUseYears = savedStack.manualUseYears,
                    broken = savedStack.broken,
                    applied = savedStack.applied
                });
        }

        if (unresolvedItemKeys != null &&
            unresolvedItemKeys.Count > 0)
        {
            Debug.LogWarning(
                "GameSaveSystem skipped applying inventory '" +
                inventoryKey +
                "' because some item IDs could not be resolved: " +
                string.Join(", ", unresolvedItemKeys));
            return false;
        }

        target.Clear();
        target.AddRange(loadedItems);
        return true;
    }

    public static void SaveShopStock(
        string shopKey,
        List<ShopItemSlot> items)
    {
        if (string.IsNullOrEmpty(shopKey) ||
            items == null)
        {
            return;
        }

        string saveKey =
            ShopPrefix + shopKey;

        RegisterDynamicSaveKey(saveKey);

        SavedShopData data =
            new SavedShopData();

        foreach (ShopItemSlot slot in items)
        {
            if (slot == null ||
                slot.item == null)
            {
                continue;
            }

            RegisterItem(slot.item);

            data.stocks.Add(
                new SavedShopStock
                {
                    itemKey = GetItemKey(slot.item),
                    amount = slot.amount
                });
        }

        PlayerPrefs.SetString(
            saveKey,
            JsonUtility.ToJson(data));

        MarkSaveExists();
        QueuePendingCommit();
    }

    public static bool TryLoadShopStock(
        string shopKey,
        List<ShopItemSlot> items)
    {
        if (string.IsNullOrEmpty(shopKey) ||
            items == null)
        {
            return false;
        }

        string key =
            ShopPrefix + shopKey;

        if (!PlayerPrefs.HasKey(key))
        {
            return false;
        }

        RegisterDynamicSaveKey(key);

        if (!TryDeserializeJson(
                key,
                PlayerPrefs.GetString(key),
                out SavedShopData data) ||
            data == null ||
            data.stocks == null)
        {
            return false;
        }

        Dictionary<string, int> stockByItem =
            new Dictionary<string, int>();

        foreach (SavedShopStock stock in data.stocks)
        {
            stockByItem[stock.itemKey] =
                stock.amount;
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

            if (stockByItem.TryGetValue(
                    itemKey,
                    out int amount))
            {
                slot.amount = amount;
            }
        }

        return true;
    }

    static bool TryDeserializeJson<T>(
        string saveKey,
        string serialized,
        out T data)
    {
        data = default(T);

        if (string.IsNullOrWhiteSpace(serialized))
        {
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<T>(serialized);
            return data != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "GameSaveSystem failed to parse save key '" +
                saveKey +
                "': " +
                exception.Message);
            return false;
        }
    }

    static IEnumerable<string> PlayerPrefsKeys()
    {
        // Unity does not expose key enumeration. Keep deletion targeted to
        // the keys this project writes.
        yield return HasSaveKey;
        yield return SceneKey;
        yield return InventoryPrefix + "BaloPanel";
        yield return InventoryPrefix + "Balo";
        yield return ShopPrefix + "ShopPanel";
        yield return ShopPrefix + "Shop";
        yield return ShopPrefix + "CuaHang";
        yield return DynamicKeysKey;
        yield return WorldTimeYearKey;
        yield return WorldTimeMonthKey;
        yield return WorldTimeDayKey;
        yield return WorldTimeHourKey;
    }
}
