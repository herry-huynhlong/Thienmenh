using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UiTextDatabase
{
    public UiTextCategory[] categories;
}

[Serializable]
public class UiTextCategory
{
    public string name;
    public UiTextEntry[] entries;
    public UiTextList[] lists;
}

[Serializable]
public class UiTextEntry
{
    public string key;
    public string value;
}

[Serializable]
public class UiTextList
{
    public string key;
    public string[] values;
}

public static class UiText
{
    const string ResourceName = "UiTextDatabase";

    static Dictionary<string, string> values;
    static Dictionary<string, string[]> lists;
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

    public static string[] Lines(string category, string key)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(category) ||
            string.IsNullOrEmpty(key))
        {
            return Array.Empty<string>();
        }

        string lookupKey = category + "." + key;
        if (lists != null &&
            lists.TryGetValue(lookupKey, out string[] lines) &&
            lines != null)
        {
            return lines;
        }

        string value = Get(category, key, "");
        return string.IsNullOrEmpty(value)
            ? Array.Empty<string>()
            : new[] { value };
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
            Debug.LogWarning(
                "Missing Resources/" +
                LocalizationSettings.GetLocalizedResourcePath(ResourceName) +
                ".json");
            return;
        }

        UiTextDatabase database =
            JsonUtility.FromJson<UiTextDatabase>(asset.text);
        if (database == null ||
            database.categories == null)
        {
            return;
        }

        foreach (UiTextCategory category in database.categories)
        {
            if (category == null ||
                string.IsNullOrEmpty(category.name))
            {
                continue;
            }

            if (category.entries != null)
            {
                foreach (UiTextEntry entry in category.entries)
                {
                    if (entry == null ||
                        string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    values[category.name + "." + entry.key] = entry.value;
                }
            }

            if (category.lists != null)
            {
                foreach (UiTextList list in category.lists)
                {
                    if (list == null ||
                        string.IsNullOrEmpty(list.key) ||
                        list.values == null)
                    {
                        continue;
                    }

                    lists[category.name + "." + list.key] = list.values;
                }
            }
        }
    }
}
