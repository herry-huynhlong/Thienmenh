using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class RuntimeWorldLogTextDatabase
{
    public RuntimeWorldLogExactEntry[] exactEntries;
    public RuntimeWorldLogRegexEntry[] regexEntries;
}

[Serializable]
public class RuntimeWorldLogExactEntry
{
    public string raw;
    public string value;
}

[Serializable]
public class RuntimeWorldLogRegexEntry
{
    public string rawPattern;
    public string value;
}

public static class RuntimeWorldLogText
{
    const string ResourceName = "RuntimeWorldLogTextDatabase";

    static Dictionary<string, string> exactMap;
    static RuntimeWorldLogRegexEntry[] regexEntries;
    static string loadedLanguageCode;

    public static string Translate(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "";
        }

        EnsureLoaded();

        string trimmedMessage = message.Trim();

        if (exactMap != null &&
            exactMap.TryGetValue(trimmedMessage, out string exactValue))
        {
            return NpcText.CleanDisplayText(exactValue);
        }

        if (regexEntries != null)
        {
            for (int i = 0; i < regexEntries.Length; i++)
            {
                RuntimeWorldLogRegexEntry entry = regexEntries[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.rawPattern) ||
                    string.IsNullOrWhiteSpace(entry.value))
                {
                    continue;
                }

                Match match =
                    Regex.Match(
                        trimmedMessage,
                        entry.rawPattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    continue;
                }

                if (match.Groups.Count <= 1)
                {
                    return NpcText.CleanDisplayText(entry.value);
                }

                string[] args =
                    new string[match.Groups.Count - 1];

                for (int groupIndex = 1;
                    groupIndex < match.Groups.Count;
                    groupIndex++)
                {
                    args[groupIndex - 1] =
                        match.Groups[groupIndex].Value.Trim();
                }

                return NpcText.Format(entry.value, args);
            }
        }

        return message;
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
        regexEntries = Array.Empty<RuntimeWorldLogRegexEntry>();
        loadedLanguageCode = currentLanguageCode;

        TextAsset asset =
            LocalizationSettings.LoadTextAsset(ResourceName);
        if (asset == null)
        {
            return;
        }

        RuntimeWorldLogTextDatabase database =
            JsonUtility.FromJson<RuntimeWorldLogTextDatabase>(asset.text);
        if (database == null)
        {
            return;
        }

        if (database.exactEntries != null)
        {
            for (int i = 0; i < database.exactEntries.Length; i++)
            {
                RuntimeWorldLogExactEntry entry =
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

        if (database.regexEntries != null)
        {
            regexEntries = database.regexEntries;
        }
    }
}
