using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NpcGeneratedNameDatabase
{
    public NpcGeneratedNameList[] lists;
}

[Serializable]
public class NpcGeneratedNameList
{
    public string key;
    public string[] values;
}

public static class NpcGeneratedNameText
{
    const string ResourceName = "NpcGeneratedNameDatabase";
    const string CanonicalLanguage = "en";

    static readonly Dictionary<string, Dictionary<string, string[]>> cache =
        new Dictionary<string, Dictionary<string, string[]>>(
            StringComparer.OrdinalIgnoreCase);

    public static string[] GetCanonicalList(string key)
    {
        return GetList(CanonicalLanguage, key);
    }

    public static string[] GetList(
        string languageCode,
        string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Array.Empty<string>();
        }

        Dictionary<string, string[]> lists =
            LoadDatabaseForLanguage(languageCode);
        if (lists.TryGetValue(key, out string[] values) &&
            values != null)
        {
            return values;
        }

        return Array.Empty<string>();
    }

    static Dictionary<string, string[]> LoadDatabaseForLanguage(
        string languageCode)
    {
        string normalizedLanguage =
            NormalizeLanguageCode(languageCode);
        if (cache.TryGetValue(normalizedLanguage, out Dictionary<string, string[]> loaded))
        {
            return loaded;
        }

        Dictionary<string, string[]> database =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        string resourcePath =
            "Localization/" +
            normalizedLanguage +
            "/" +
            ResourceName;
        TextAsset asset =
            Resources.Load<TextAsset>(resourcePath);
        if (asset == null &&
            !string.Equals(
                normalizedLanguage,
                CanonicalLanguage,
                StringComparison.OrdinalIgnoreCase))
        {
            resourcePath =
                "Localization/" +
                CanonicalLanguage +
                "/" +
                ResourceName;
            asset = Resources.Load<TextAsset>(resourcePath);
        }

        if (asset == null)
        {
            cache[normalizedLanguage] = database;
            return database;
        }

        NpcGeneratedNameDatabase parsed =
            JsonUtility.FromJson<NpcGeneratedNameDatabase>(asset.text);
        if (parsed == null ||
            parsed.lists == null)
        {
            cache[normalizedLanguage] = database;
            return database;
        }

        for (int i = 0; i < parsed.lists.Length; i++)
        {
            NpcGeneratedNameList list = parsed.lists[i];
            if (list == null ||
                string.IsNullOrWhiteSpace(list.key) ||
                list.values == null)
            {
                continue;
            }

            database[list.key] = list.values;
        }

        cache[normalizedLanguage] = database;
        return database;
    }

    static string NormalizeLanguageCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return CanonicalLanguage;
        }

        return languageCode.Trim().ToLowerInvariant();
    }
}
