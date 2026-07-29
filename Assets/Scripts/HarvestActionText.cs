using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HarvestActionTextDatabase
{
    public HarvestActionTextCategory[] categories;
}

[Serializable]
public class HarvestActionTextCategory
{
    public string name;
    public HarvestActionTextEntry[] entries;
    public HarvestActionTextList[] lists;
}

[Serializable]
public class HarvestActionTextEntry
{
    public string key;
    public string value;
}

[Serializable]
public class HarvestActionTextList
{
    public string key;
    public string[] values;
}

public static class HarvestActionText
{
    const string ResourceName = "HarvestActionTextDatabase";

    static Dictionary<string, string> values;
    static Dictionary<string, string[]> lists;
    static string loadedLanguageCode;

    public static string Get(
        string category,
        string key,
        string fallback = "")
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
        string fallback,
        params object[] args)
    {
        return NpcText.Format(
            Get(category, key, fallback),
            args);
    }

    public static string RandomFormat(
        string category,
        string key,
        string fallback,
        string[] fallbackValues,
        params object[] args)
    {
        string template =
            RandomLine(
                category,
                key,
                fallback,
                fallbackValues);

        return NpcText.Format(template, args);
    }

    public static string RandomLine(
        string category,
        string key,
        string fallback,
        string[] fallbackValues)
    {
        EnsureLoaded();

        if (!string.IsNullOrEmpty(category) &&
            !string.IsNullOrEmpty(key))
        {
            string lookupKey = category + "." + key;
            if (lists != null &&
                lists.TryGetValue(lookupKey, out string[] localizedValues) &&
                localizedValues != null &&
                localizedValues.Length > 0)
            {
                return NpcText.CleanDisplayText(
                    localizedValues[
                        UnityEngine.Random.Range(
                            0,
                            localizedValues.Length)]);
            }
        }

        if (fallbackValues != null &&
            fallbackValues.Length > 0)
        {
            return NpcText.CleanDisplayText(
                fallbackValues[
                    UnityEngine.Random.Range(
                        0,
                        fallbackValues.Length)]);
        }

        return NpcText.CleanDisplayText(fallback);
    }

    static void EnsureLoaded()
    {
        string currentLanguageCode =
            LocalizationSettings.CurrentLanguageCode;
        if (values != null &&
            lists != null &&
            string.Equals(
                loadedLanguageCode,
                currentLanguageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        values = new Dictionary<string, string>();
        lists = new Dictionary<string, string[]>();
        loadedLanguageCode = currentLanguageCode;

        TextAsset asset =
            LocalizationSettings.LoadTextAsset(ResourceName);
        if (asset == null)
        {
            return;
        }

        HarvestActionTextDatabase database =
            JsonUtility.FromJson<HarvestActionTextDatabase>(asset.text);
        if (database == null ||
            database.categories == null)
        {
            return;
        }

        foreach (HarvestActionTextCategory category in database.categories)
        {
            if (category == null ||
                string.IsNullOrEmpty(category.name))
            {
                continue;
            }

            if (category.entries != null)
            {
                foreach (HarvestActionTextEntry entry in category.entries)
                {
                    if (entry == null ||
                        string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    values[category.name + "." + entry.key] =
                        entry.value ?? string.Empty;
                }
            }

            if (category.lists != null)
            {
                foreach (HarvestActionTextList list in category.lists)
                {
                    if (list == null ||
                        string.IsNullOrEmpty(list.key))
                    {
                        continue;
                    }

                    lists[category.name + "." + list.key] =
                        list.values ?? Array.Empty<string>();
                }
            }
        }
    }
}
