using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RuntimeStatusTextDatabase
{
    public RuntimeStatusExactEntry[] exactEntries;
    public RuntimeStatusPrefixEntry[] prefixEntries;
}

[Serializable]
public class RuntimeStatusExactEntry
{
    public string raw;
    public string value;
}

[Serializable]
public class RuntimeStatusPrefixEntry
{
    public string rawPrefix;
    public string rawSuffix;
    public string value;
}

public static class RuntimeStatusText
{
    const string ResourceName = "RuntimeStatusTextDatabase";

    static Dictionary<string, string> exactMap;
    static RuntimeStatusPrefixEntry[] prefixEntries;
    static string loadedLanguageCode;

    public static string Translate(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        EnsureLoaded();

        string trimmedAction = action.Trim();

        if (exactMap != null &&
            exactMap.TryGetValue(trimmedAction, out string exactValue))
        {
            return NpcText.CleanDisplayText(exactValue);
        }

        if (prefixEntries != null)
        {
            for (int i = 0; i < prefixEntries.Length; i++)
            {
                RuntimeStatusPrefixEntry entry = prefixEntries[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.rawPrefix) ||
                    string.IsNullOrWhiteSpace(entry.value) ||
                    !trimmedAction.StartsWith(
                        entry.rawPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string argument =
                    trimmedAction.Substring(entry.rawPrefix.Length);

                if (!string.IsNullOrWhiteSpace(entry.rawSuffix))
                {
                    if (!argument.EndsWith(
                            entry.rawSuffix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    argument =
                        argument.Substring(
                            0,
                            argument.Length - entry.rawSuffix.Length);
                }

                argument = argument.Trim();

                return string.IsNullOrEmpty(argument)
                    ? NpcText.CleanDisplayText(entry.value)
                    : NpcText.Format(entry.value, argument);
            }
        }

        return action;
    }

    static void EnsureLoaded()
    {
        string currentLanguageCode =
            LocalizationSettings.CurrentLanguageCode;
        if (exactMap != null &&
            string.Equals(
                loadedLanguageCode,
                currentLanguageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        exactMap =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        prefixEntries = Array.Empty<RuntimeStatusPrefixEntry>();
        loadedLanguageCode = currentLanguageCode;

        TextAsset asset =
            LocalizationSettings.LoadTextAsset(ResourceName);
        if (asset == null)
        {
            return;
        }

        RuntimeStatusTextDatabase database =
            JsonUtility.FromJson<RuntimeStatusTextDatabase>(asset.text);
        if (database == null)
        {
            return;
        }

        if (database.exactEntries != null)
        {
            for (int i = 0; i < database.exactEntries.Length; i++)
            {
                RuntimeStatusExactEntry entry =
                    database.exactEntries[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.raw))
                {
                    continue;
                }

                exactMap[entry.raw.Trim()] =
                    entry.value ?? string.Empty;
            }
        }

        if (database.prefixEntries != null)
        {
            prefixEntries = database.prefixEntries;
        }
    }
}
