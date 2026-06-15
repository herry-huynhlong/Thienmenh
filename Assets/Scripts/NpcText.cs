using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NpcTextDatabase
{
    public NpcTextCategory[] categories;
}

[Serializable]
public class NpcTextCategory
{
    public string name;
    public NpcTextEntry[] entries;
    public NpcTextList[] lists;
}

[Serializable]
public class NpcTextEntry
{
    public string key;
    public string value;
}

[Serializable]
public class NpcTextList
{
    public string key;
    public string[] values;
}

public static class NpcText
{
    const string ResourceName = "NpcTextDatabase";

    static Dictionary<string, string> values;
    static Dictionary<string, string[]> lists;

    public static string Get(string category, string key, string fallback = "")
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key))
        {
            return fallback;
        }

        string lookupKey = category + "." + key;
        if (values != null && values.TryGetValue(lookupKey, out string value))
        {
            return CleanDisplayText(value);
        }

        return CleanDisplayText(string.IsNullOrEmpty(fallback) ? key : fallback);
    }

    public static string Label(string key)
    {
        return Get("labels", key, key);
    }

    public static string Action(string key)
    {
        return Get("actions", key, key);
    }

    public static string ActionFormat(string key, params object[] args)
    {
        return Format(Action(key), args);
    }

    public static string Dialogue(string key, string fallback = "")
    {
        return Get("dialogue", key, fallback);
    }

    public static string DialogueFormat(string key, params object[] args)
    {
        return Format(Dialogue(key, key), args);
    }

    public static string DialogueLine(string key, string fallback = "")
    {
        return RandomLine("dialogue", key, fallback);
    }

    public static string RandomLine(string category, string key, string fallback = "")
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key))
        {
            return fallback;
        }

        string lookupKey = category + "." + key;
        if (lists != null &&
            lists.TryGetValue(lookupKey, out string[] lines) &&
            lines != null &&
            lines.Length > 0)
        {
            return CleanDisplayText(lines[UnityEngine.Random.Range(0, lines.Length)]);
        }

        return Get(category, key, fallback);
    }

    public static string[] Lines(string category, string key)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key))
        {
            return new string[0];
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
            ? new string[0]
            : new[] { value };
    }

    public static string Format(string template, params object[] args)
    {
        if (string.IsNullOrEmpty(template))
        {
            return "";
        }

        if (args == null || args.Length == 0)
        {
            return template;
        }

        try
        {
            return CleanDisplayText(string.Format(template, args));
        }
        catch (FormatException)
        {
            return CleanDisplayText(template);
        }
    }

    public static string Realm(CultivationRealm realm)
    {
        return Get("realms", realm.ToString(), realm.ToString());
    }

    public static string RealmWithStage(CultivationRealm realm, int stage)
    {
        string realmName = Realm(realm);
        int safeStage = Mathf.Max(1, stage);

        if (realm == CultivationRealm.Tribulation)
        {
            return realmName;
        }

        return Format(Label("realmStageFormat"), realmName, safeStage);
    }

    public static string ItemType(ItemType itemType)
    {
        return ItemText.Type(itemType);
    }

    public static string ItemGrade(ItemGrade grade)
    {
        return ItemText.Grade(grade);
    }

    public static string HealthStatus(int currentHP, int maxHP)
    {
        if (currentHP <= 0)
        {
            return Get("health", "dead", "Dead");
        }

        float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
        if (ratio <= 0.25f)
        {
            return Get("health", "critical", "Critical");
        }

        if (ratio < 1f)
        {
            return Get("health", "injured", "Injured");
        }

        return Get("health", "healthy", "Healthy");
    }

    static void EnsureLoaded()
    {
        if (values != null && lists != null)
        {
            return;
        }

        values = new Dictionary<string, string>();
        lists = new Dictionary<string, string[]>();

        TextAsset asset = Resources.Load<TextAsset>(ResourceName);
        if (asset == null)
        {
            Debug.LogWarning("Missing Resources/" + ResourceName + ".json");
            return;
        }

        NpcTextDatabase database = JsonUtility.FromJson<NpcTextDatabase>(asset.text);
        if (database == null || database.categories == null)
        {
            return;
        }

        foreach (NpcTextCategory category in database.categories)
        {
            if (category == null || string.IsNullOrEmpty(category.name))
            {
                continue;
            }

            if (category.entries != null)
            {
                foreach (NpcTextEntry entry in category.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    values[category.name + "." + entry.key] = entry.value;
                }
            }

            if (category.lists != null)
            {
                foreach (NpcTextList list in category.lists)
                {
                    if (list == null || string.IsNullOrEmpty(list.key) || list.values == null)
                    {
                        continue;
                    }

                    lists[category.name + "." + list.key] = list.values;
                }
            }
        }
    }

    public static string CleanDisplayText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        System.Text.StringBuilder builder = null;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool isControl = (c < 32 && c != '\n' && c != '\r' && c != '\t') ||
                (c >= 0x80 && c <= 0x9F);

            if (!isControl)
            {
                if (builder != null)
                {
                    builder.Append(c);
                }

                continue;
            }

            if (builder == null)
            {
                builder = new System.Text.StringBuilder(value.Length);
                builder.Append(value, 0, i);
            }
        }

        return builder == null ? value : builder.ToString();
    }
}
