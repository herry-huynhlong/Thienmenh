using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemTextDatabase
{
    public ItemTextCategory[] categories;
}

[Serializable]
public class ItemTextCategory
{
    public string name;
    public ItemTextEntry[] entries;
}

[Serializable]
public class ItemTextEntry
{
    public string key;
    public string value;
}

public static class ItemText
{
    const string ResourceName = "ItemTextDatabase";

    static Dictionary<string, string> values;
    static string loadedLanguageCode;

    public static string Get(string category, string key, string fallback = "")
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(category) ||
            string.IsNullOrEmpty(key))
        {
            return fallback;
        }

        string lookupKey = category + "." + key;
        if (values != null &&
            values.TryGetValue(lookupKey, out string value))
        {
            return NpcText.CleanDisplayText(value);
        }

        return NpcText.CleanDisplayText(
            string.IsNullOrEmpty(fallback)
                ? key
                : fallback);
    }

    public static string Format(
        string category,
        string key,
        params object[] args)
    {
        return NpcText.Format(Get(category, key, key), args);
    }

    public static string Type(ItemType itemType)
    {
        return Get("itemTypes", itemType.ToString(), itemType.ToString());
    }

    public static string Name(StatItemData item)
    {
        if (item == null)
        {
            return "";
        }

        string fallback = !string.IsNullOrEmpty(item.itemName)
            ? item.itemName
            : item.name;

        return Get("itemNames", item.name, fallback);
    }

    public static string Description(StatItemData item)
    {
        if (item == null)
        {
            return "";
        }

        return Get("itemDescriptions", item.name, item.description);
    }

    public static string Grade(ItemGrade grade)
    {
        return Get("itemGrades", grade.ToString(), grade.ToString());
    }

    public static string GradeLong(ItemGrade grade)
    {
        return Get("itemGradeLong", grade.ToString(), Grade(grade));
    }

    public static string Target(ItemTargetType targetType)
    {
        if (targetType == ItemTargetType.All)
        {
            return Get("itemTargets", "All", targetType.ToString());
        }

        if (targetType == ItemTargetType.None)
        {
            return Get("itemTargets", "None", targetType.ToString());
        }

        List<string> parts = new List<string>();
        AddTargetPart(parts, targetType, ItemTargetType.Player);
        AddTargetPart(parts, targetType, ItemTargetType.Npc);
        AddTargetPart(parts, targetType, ItemTargetType.Monster);

        return parts.Count > 0
            ? string.Join(", ", parts)
            : targetType.ToString();
    }

    public static string UseStyle(ItemUseStyle style)
    {
        return Get("itemUseStyles", style.ToString(), style.ToString());
    }

    public static string RawUsePolicy(RawUsePolicy policy)
    {
        return Get("rawUsePolicies", policy.ToString(), policy.ToString());
    }

    public static string NpcIntent(NpcItemIntent intent)
    {
        return Get("npcItemIntents", intent.ToString(), intent.ToString());
    }

    public static string Mastery(CultivationManualMastery mastery)
    {
        return Get("manualMastery", mastery.ToString(), mastery.ToString());
    }

    public static string HeavenGiftName(StatItemData item)
    {
        if (item == null)
        {
            return Get("heavenGiftNames", "Default", "Bảo Vật");
        }

        string gradeText = GradeLong(item.grade);
        string key = item.itemType.ToString();
        string template = Get(
            "heavenGiftNames",
            key,
            Get("heavenGiftNames", "Default", "{0} Bảo Vật"));

        return NpcText.Format(template, gradeText);
    }

    static void AddTargetPart(
        List<string> parts,
        ItemTargetType current,
        ItemTargetType flag)
    {
        if ((current & flag) == flag)
        {
            parts.Add(Get("itemTargets", flag.ToString(), flag.ToString()));
        }
    }

    static void EnsureLoaded()
    {
        string currentLanguageCode =
            LocalizationSettings.CurrentLanguageCode;
        if (values != null &&
            string.Equals(
                loadedLanguageCode,
                currentLanguageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        values = new Dictionary<string, string>();
        loadedLanguageCode = currentLanguageCode;

        TextAsset asset =
            LocalizationSettings.LoadTextAsset(ResourceName);
        if (asset == null)
        {
            Debug.LogWarning(
                "Missing Resources/" +
                LocalizationSettings.GetLocalizedResourcePath(ResourceName) +
                ".json");
            return;
        }

        ItemTextDatabase database =
            JsonUtility.FromJson<ItemTextDatabase>(asset.text);
        if (database == null ||
            database.categories == null)
        {
            return;
        }

        foreach (ItemTextCategory category in database.categories)
        {
            if (category == null ||
                string.IsNullOrEmpty(category.name) ||
                category.entries == null)
            {
                continue;
            }

            foreach (ItemTextEntry entry in category.entries)
            {
                if (entry == null ||
                    string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                values[category.name + "." + entry.key] = entry.value;
            }
        }
    }
}
