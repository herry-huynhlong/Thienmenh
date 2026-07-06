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
    static string loadedLanguageCode;

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
            string[] cleaned = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                cleaned[i] = CleanDisplayText(lines[i]);
            }

            return cleaned;
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

        string repaired = TryRepairMojibake(value);
        System.Text.StringBuilder builder = null;

        for (int i = 0; i < repaired.Length; i++)
        {
            char c = repaired[i];
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
                builder = new System.Text.StringBuilder(repaired.Length);
                builder.Append(repaired, 0, i);
            }
        }

        return builder == null ? repaired : builder.ToString();
    }

    static string TryRepairMojibake(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            !LooksLikeMojibake(value))
        {
            return value;
        }

        string best = value;
        int bestScore = GetDisplayQualityScore(value);
        string current = value;

        for (int i = 0; i < 3; i++)
        {
            string candidate = DecodeLatin1Utf8(current);
            if (string.IsNullOrEmpty(candidate) ||
                string.Equals(candidate, current, StringComparison.Ordinal))
            {
                break;
            }

            int candidateScore = GetDisplayQualityScore(candidate);
            if (candidateScore <= bestScore)
            {
                break;
            }

            best = candidate;
            bestScore = candidateScore;
            current = candidate;
        }

        return best;
    }

    static string DecodeLatin1Utf8(string value)
    {
        try
        {
            byte[] bytes =
                System.Text.Encoding.GetEncoding("ISO-8859-1")
                    .GetBytes(value);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return value;
        }
    }

    static bool LooksLikeMojibake(string value)
    {
        return value.IndexOf('Ã') >= 0 ||
            value.IndexOf('Â') >= 0 ||
            value.IndexOf('Ä') >= 0 ||
            value.IndexOf('Æ') >= 0 ||
            value.IndexOf('á') >= 0 ||
            value.IndexOf('º') >= 0 ||
            value.IndexOf('»') >= 0 ||
            (CountSuspiciousMojibakeChars(value) >= 2 &&
                !ContainsCjk(value));
    }

    static int GetDisplayQualityScore(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        int score = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (IsReadableVietnameseChar(c))
            {
                score += 3;
                continue;
            }

            if (IsCjkChar(c))
            {
                score += 4;
                continue;
            }

            if (IsLatinSupplementChar(c))
            {
                score -= 3;
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                score += 2;
                continue;
            }

            if (char.IsWhiteSpace(c) ||
                char.IsPunctuation(c) ||
                char.IsSymbol(c))
            {
                score += 1;
                continue;
            }

            score -= 4;
        }

        score -= CountSuspiciousSequences(value) * 8;
        return score;
    }

    static bool IsReadableVietnameseChar(char c)
    {
        return c == 'đ' ||
            c == 'Đ' ||
            "ăâêôơưáàảãạắằẳẵặấầẩẫậéèẻẽẹếềểễệóòỏõọốồổỗộớờởỡợúùủũụứừửữựíìỉĩịýỳỷỹỵ".IndexOf(c) >= 0 ||
            "ĂÂÊÔƠƯÁÀẢÃẠẮẰẲẴẶẤẦẨẪẬÉÈẺẼẸẾỀỂỄỆÓÒỎÕỌỐỒỔỖỘỚỜỞỠỢÚÙỦŨỤỨỪỬỮỰÍÌỈĨỊÝỲỶỸỴ".IndexOf(c) >= 0;
    }

    static int CountSuspiciousSequences(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (IsSuspiciousMojibakeChar(value[i]))
            {
                count++;
            }
        }

        return count;
    }

    static int CountSuspiciousMojibakeChars(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (IsSuspiciousMojibakeChar(value[i]))
            {
                count++;
            }
        }

        return count;
    }

    static bool ContainsCjk(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (IsCjkChar(value[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsCjkChar(char c)
    {
        return (c >= 0x3400 && c <= 0x4DBF) ||
            (c >= 0x4E00 && c <= 0x9FFF) ||
            (c >= 0xF900 && c <= 0xFAFF);
    }

    static bool IsLatinSupplementChar(char c)
    {
        return c >= 0x00C0 &&
            c <= 0x00FF &&
            !IsReadableVietnameseChar(c);
    }

    static bool IsSuspiciousMojibakeChar(char c)
    {
        switch (c)
        {
            case 'Ã':
            case 'Â':
            case 'Ä':
            case 'Æ':
            case 'º':
            case '»':
            case 'å':
            case 'æ':
            case 'ç':
            case 'è':
            case 'é':
            case 'ê':
            case 'ë':
            case 'ì':
            case 'í':
            case 'î':
            case 'ï':
            case 'ð':
            case 'ñ':
            case 'ò':
            case 'ó':
            case 'ô':
            case 'õ':
            case 'ö':
            case 'ù':
            case 'ú':
            case 'û':
            case 'ü':
                return true;
            default:
                return false;
        }
    }
}
