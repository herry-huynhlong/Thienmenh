using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class NpcGeneratedIdentityProfiles
{
    public const int CurrentGeneratedIdentityVersion = 2;

    const string VillagerSurnameKey = "villagerSurnames";
    const string VillagerMaleMiddleKey = "villagerMaleMiddles";
    const string VillagerFemaleMiddleKey = "villagerFemaleMiddles";
    const string VillagerMaleTeenGivenKey = "villagerMaleTeenGivens";
    const string VillagerMaleAdultGivenKey = "villagerMaleAdultGivens";
    const string VillagerMaleElderGivenKey = "villagerMaleElderGivens";
    const string VillagerFemaleTeenGivenKey = "villagerFemaleTeenGivens";
    const string VillagerFemaleAdultGivenKey = "villagerFemaleAdultGivens";
    const string VillagerFemaleElderGivenKey = "villagerFemaleElderGivens";
    const string SmartSurnameKey = "smartSurnames";
    const string SmartMaleMiddleKey = "smartMaleMiddles";
    const string SmartFemaleMiddleKey = "smartFemaleMiddles";
    const string SmartMaleGivenKey = "smartMaleGivens";
    const string SmartFemaleGivenKey = "smartFemaleGivens";

    static readonly string[] AllListKeys =
    {
        VillagerSurnameKey,
        VillagerMaleMiddleKey,
        VillagerFemaleMiddleKey,
        VillagerMaleTeenGivenKey,
        VillagerMaleAdultGivenKey,
        VillagerMaleElderGivenKey,
        VillagerFemaleTeenGivenKey,
        VillagerFemaleAdultGivenKey,
        VillagerFemaleElderGivenKey,
        SmartSurnameKey,
        SmartMaleMiddleKey,
        SmartFemaleMiddleKey,
        SmartMaleGivenKey,
        SmartFemaleGivenKey
    };

    enum GeneratedAgeGroup
    {
        Teen,
        Adult,
        Elder
    }

    struct NameParts
    {
        public string surname;
        public string middle;
        public string given;
    }

    static string displayLanguageCode;
    static Dictionary<string, string> localizedComponentMap;
    static List<string[]> sortedCanonicalComponents;

    public static void ApplyCommonerIdentity(EntityIdentity identity)
    {
        if (identity == null)
        {
            return;
        }

        EnsureGender(identity);
        GeneratedAgeGroup ageGroup = WeightedVillagerAgeGroup();
        identity.age = GenerateVillagerAge(ageGroup);
        NpcAgeUtility.SetCurrentAge(identity, identity.age);
        identity.entityName = GenerateUniqueCommonerName(
            identity.gender,
            ageGroup);
    }

    public static void MigrateExistingCommonerIdentity(EntityProfile profile)
    {
        if (profile == null ||
            profile.identity == null)
        {
            return;
        }

        EntityIdentity identity = profile.identity;
        EnsureGender(identity);
        GeneratedAgeGroup ageGroup = WeightedVillagerAgeGroup();
        identity.age = GenerateVillagerAge(ageGroup);
        NpcAgeUtility.SetCurrentAge(identity, identity.age);
        identity.entityName = GenerateUniqueCommonerName(
            identity.gender,
            ageGroup,
            identity.entityName);
        profile.generatedIdentityVersion =
            CurrentGeneratedIdentityVersion;
    }

    public static void MigrateExistingSmartIdentity(EntityProfile profile)
    {
        if (profile == null ||
            profile.identity == null)
        {
            return;
        }

        EntityIdentity identity = profile.identity;
        EnsureGender(identity);

        identity.age = Mathf.Max(14, identity.age);
        NpcAgeUtility.SetCurrentAge(identity, identity.age);
        identity.entityName = GenerateUniqueSmartName(
            identity.gender,
            identity.entityName);
        profile.generatedIdentityVersion =
            CurrentGeneratedIdentityVersion;
    }

    public static string PickSmartName(EntityGender gender)
    {
        return GenerateUniqueSmartName(gender);
    }

    public static string GenerateChildName(
        EntityGender gender,
        NPCIdentity fatherIdentity,
        NPCIdentity motherIdentity)
    {
        GeneratedAgeGroup ageGroup = GeneratedAgeGroup.Teen;
        return GenerateUniqueCommonerName(
            gender,
            ageGroup,
            null,
            fatherIdentity,
            motherIdentity);
    }

    public static bool NeedsMigration(EntityProfile profile)
    {
        return profile != null &&
            profile.generatedIdentityVersion <
            CurrentGeneratedIdentityVersion;
    }

    public static string ToDisplayName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return rawName;
        }

        EnsureDisplayLocalization();

        if (localizedComponentMap == null ||
            localizedComponentMap.Count == 0)
        {
            return rawName;
        }

        if (!TryParseCanonicalComponents(rawName, out List<string> parts) ||
            parts == null ||
            parts.Count == 0)
        {
            return rawName;
        }

        StringBuilder builder = new StringBuilder(rawName.Length + 8);
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            if (localizedComponentMap.TryGetValue(parts[i], out string localized) &&
                !string.IsNullOrWhiteSpace(localized))
            {
                builder.Append(localized);
            }
            else
            {
                builder.Append(parts[i]);
            }
        }

        return builder.ToString();
    }

    static void EnsureGender(EntityIdentity identity)
    {
        if (identity == null ||
            identity.gender == EntityGender.Male ||
            identity.gender == EntityGender.Female)
        {
            return;
        }

        identity.gender =
            UnityEngine.Random.value < 0.5f
                ? EntityGender.Male
                : EntityGender.Female;
    }

    static GeneratedAgeGroup WeightedVillagerAgeGroup()
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.60f)
        {
            return GeneratedAgeGroup.Teen;
        }

        if (roll < 0.90f)
        {
            return GeneratedAgeGroup.Adult;
        }

        return GeneratedAgeGroup.Elder;
    }

    static int GenerateVillagerAge(GeneratedAgeGroup ageGroup)
    {
        switch (ageGroup)
        {
            case GeneratedAgeGroup.Teen:
                return UnityEngine.Random.Range(13, 20);

            case GeneratedAgeGroup.Adult:
                return UnityEngine.Random.Range(20, 50);

            case GeneratedAgeGroup.Elder:
                return UnityEngine.Random.Range(50, 86);

            default:
                return UnityEngine.Random.Range(20, 50);
        }
    }

    static string GenerateUniqueCommonerName(
        EntityGender gender,
        GeneratedAgeGroup ageGroup,
        string existingName = null,
        NPCIdentity fatherIdentity = null,
        NPCIdentity motherIdentity = null)
    {
        string excludeKey = NormalizeNameKey(existingName);
        HashSet<string> usedNames = CollectUsedNames(excludeKey);

        for (int i = 0; i < 96; i++)
        {
            string candidate =
                ComposeRandomName(
                    VillagerSurnameKey,
                    GetVillagerMiddleKey(gender),
                    GetVillagerGivenKey(gender, ageGroup));
            if (TryAcceptCandidate(candidate, usedNames, out string accepted))
            {
                return accepted;
            }
        }

        string familyCandidate =
            ComposeFamilyDerivedName(
                fatherIdentity,
                motherIdentity,
                GetVillagerMiddleKey(gender),
                GetVillagerGivenKey(gender, ageGroup));
        if (TryAcceptCandidate(familyCandidate, usedNames, out string familyAccepted))
        {
            return familyAccepted;
        }

        return ComposeFallbackName(
            VillagerSurnameKey,
            GetVillagerMiddleKey(gender),
            GetVillagerGivenKey(gender, ageGroup),
            usedNames);
    }

    static string GenerateUniqueSmartName(
        EntityGender gender,
        string existingName = null)
    {
        string excludeKey = NormalizeNameKey(existingName);
        HashSet<string> usedNames = CollectUsedNames(excludeKey);

        for (int i = 0; i < 96; i++)
        {
            string candidate =
                ComposeRandomName(
                    SmartSurnameKey,
                    GetSmartMiddleKey(gender),
                    GetSmartGivenKey(gender));
            if (TryAcceptCandidate(candidate, usedNames, out string accepted))
            {
                return accepted;
            }
        }

        return ComposeFallbackName(
            SmartSurnameKey,
            GetSmartMiddleKey(gender),
            GetSmartGivenKey(gender),
            usedNames);
    }

    static string GetVillagerMiddleKey(EntityGender gender)
    {
        return gender == EntityGender.Female
            ? VillagerFemaleMiddleKey
            : VillagerMaleMiddleKey;
    }

    static string GetVillagerGivenKey(
        EntityGender gender,
        GeneratedAgeGroup ageGroup)
    {
        bool female = gender == EntityGender.Female;

        switch (ageGroup)
        {
            case GeneratedAgeGroup.Teen:
                return female
                    ? VillagerFemaleTeenGivenKey
                    : VillagerMaleTeenGivenKey;

            case GeneratedAgeGroup.Elder:
                return female
                    ? VillagerFemaleElderGivenKey
                    : VillagerMaleElderGivenKey;

            default:
                return female
                    ? VillagerFemaleAdultGivenKey
                    : VillagerMaleAdultGivenKey;
        }
    }

    static string GetSmartMiddleKey(EntityGender gender)
    {
        return gender == EntityGender.Female
            ? SmartFemaleMiddleKey
            : SmartMaleMiddleKey;
    }

    static string GetSmartGivenKey(EntityGender gender)
    {
        return gender == EntityGender.Female
            ? SmartFemaleGivenKey
            : SmartMaleGivenKey;
    }

    static string ComposeRandomName(
        string surnameKey,
        string middleKey,
        string givenKey)
    {
        string surname = PickRandomComponent(surnameKey);
        string middle = PickRandomComponent(middleKey);
        string given = PickRandomComponent(givenKey);
        return ComposeName(surname, middle, given);
    }

    static string ComposeFamilyDerivedName(
        NPCIdentity fatherIdentity,
        NPCIdentity motherIdentity,
        string middleKey,
        string givenKey)
    {
        NameParts fatherParts = ParseNameParts(
            fatherIdentity != null ? fatherIdentity.npcName : string.Empty);
        NameParts motherParts = ParseNameParts(
            motherIdentity != null ? motherIdentity.npcName : string.Empty);

        string surname =
            !string.IsNullOrWhiteSpace(fatherParts.surname)
                ? fatherParts.surname
                : !string.IsNullOrWhiteSpace(motherParts.surname)
                    ? motherParts.surname
                    : PickRandomComponent(VillagerSurnameKey);
        string middle =
            !string.IsNullOrWhiteSpace(fatherParts.middle)
                ? fatherParts.middle
                : PickRandomComponent(middleKey);
        string given =
            !string.IsNullOrWhiteSpace(motherParts.given)
                ? motherParts.given
                : PickRandomComponent(givenKey);

        return ComposeName(surname, middle, given);
    }

    static string ComposeFallbackName(
        string surnameKey,
        string middleKey,
        string givenKey,
        HashSet<string> usedNames)
    {
        string baseName =
            ComposeRandomName(
                surnameKey,
                middleKey,
                givenKey);
        if (!TryAcceptCandidate(baseName, usedNames, out string accepted))
        {
            accepted = baseName;
        }

        string key = NormalizeNameKey(accepted);
        if (!string.IsNullOrEmpty(key) &&
            !usedNames.Contains(key))
        {
            return accepted;
        }

        for (int i = 2; i <= 999; i++)
        {
            string numbered = accepted + " " + i;
            string numberedKey = NormalizeNameKey(numbered);
            if (string.IsNullOrEmpty(numberedKey) ||
                usedNames.Contains(numberedKey))
            {
                continue;
            }

            return numbered;
        }

        return accepted;
    }

    static bool TryAcceptCandidate(
        string candidate,
        HashSet<string> usedNames,
        out string accepted)
    {
        accepted = candidate != null
            ? CollapseWhitespace(candidate)
            : string.Empty;
        string normalized = NormalizeNameKey(accepted);
        if (string.IsNullOrEmpty(normalized) ||
            (usedNames != null && usedNames.Contains(normalized)))
        {
            return false;
        }

        return true;
    }

    static HashSet<string> CollectUsedNames(string excludeKey = null)
    {
        HashSet<string> usedNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        NPCIdentity[] identities =
            UnityEngine.Object.FindObjectsByType<NPCIdentity>(
                FindObjectsInactive.Include);
        for (int i = 0; i < identities.Length; i++)
        {
            AddUsedName(usedNames, identities[i] != null
                ? identities[i].npcName
                : string.Empty, excludeKey);
        }

        EntityProfile[] profiles =
            UnityEngine.Object.FindObjectsByType<EntityProfile>(
                FindObjectsInactive.Include);
        for (int i = 0; i < profiles.Length; i++)
        {
            AddUsedName(
                usedNames,
                profiles[i] != null && profiles[i].identity != null
                    ? profiles[i].identity.entityName
                    : string.Empty,
                excludeKey);
        }

        return usedNames;
    }

    static void AddUsedName(
        HashSet<string> usedNames,
        string candidate,
        string excludeKey)
    {
        if (usedNames == null)
        {
            return;
        }

        string normalized = NormalizeNameKey(candidate);
        if (string.IsNullOrEmpty(normalized) ||
            string.Equals(
                normalized,
                excludeKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        usedNames.Add(normalized);
    }

    static string NormalizeNameKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized =
            value.Normalize(NormalizationForm.FormD);
        StringBuilder builder =
            new StringBuilder(normalized.Length);
        bool previousWasSpace = false;

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            if (c == 'đ' || c == 'Đ')
            {
                builder.Append('d');
                previousWasSpace = false;
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
            previousWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    static string CollapseWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string[] parts =
            value.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    static string ComposeName(
        string surname,
        string middle,
        string given)
    {
        List<string> parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(surname))
        {
            parts.Add(CollapseWhitespace(surname));
        }

        if (!string.IsNullOrWhiteSpace(middle))
        {
            parts.Add(CollapseWhitespace(middle));
        }

        if (!string.IsNullOrWhiteSpace(given))
        {
            parts.Add(CollapseWhitespace(given));
        }

        return string.Join(" ", parts);
    }

    static NameParts ParseNameParts(string rawName)
    {
        if (!TryParseCanonicalComponents(rawName, out List<string> parts) ||
            parts == null ||
            parts.Count == 0)
        {
            return default;
        }

        if (parts.Count == 1)
        {
            return new NameParts
            {
                given = parts[0]
            };
        }

        if (parts.Count == 2)
        {
            return new NameParts
            {
                surname = parts[0],
                given = parts[1]
            };
        }

        return new NameParts
        {
            surname = parts[0],
            middle = parts[1],
            given = parts[parts.Count - 1]
        };
    }

    static bool TryParseCanonicalComponents(
        string rawName,
        out List<string> parts)
    {
        parts = null;
        string collapsed = CollapseWhitespace(rawName);
        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return false;
        }

        EnsureCanonicalComponentLookup();
        if (sortedCanonicalComponents == null ||
            sortedCanonicalComponents.Count == 0)
        {
            return false;
        }

        string[] tokens =
            collapsed.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
        List<string> resolved = new List<string>(3);
        int index = 0;

        while (index < tokens.Length)
        {
            string[] match = null;
            for (int i = 0; i < sortedCanonicalComponents.Count; i++)
            {
                string[] candidate = sortedCanonicalComponents[i];
                if (!MatchesTokens(tokens, index, candidate))
                {
                    continue;
                }

                match = candidate;
                break;
            }

            if (match == null)
            {
                if (char.IsDigit(tokens[index][0]))
                {
                    resolved.Add(tokens[index]);
                    index++;
                    continue;
                }

                return false;
            }

            resolved.Add(string.Join(" ", match));
            index += match.Length;
        }

        parts = resolved;
        return resolved.Count > 0;
    }

    static bool MatchesTokens(
        string[] source,
        int startIndex,
        string[] candidate)
    {
        if (source == null ||
            candidate == null ||
            startIndex < 0 ||
            startIndex + candidate.Length > source.Length)
        {
            return false;
        }

        for (int i = 0; i < candidate.Length; i++)
        {
            if (!string.Equals(
                    source[startIndex + i],
                    candidate[i],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    static void EnsureCanonicalComponentLookup()
    {
        if (sortedCanonicalComponents != null)
        {
            return;
        }

        HashSet<string> seen =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string[]> components = new List<string[]>();

        for (int i = 0; i < AllListKeys.Length; i++)
        {
            string[] values =
                NpcGeneratedNameText.GetCanonicalList(AllListKeys[i]);
            for (int j = 0; j < values.Length; j++)
            {
                string value = CollapseWhitespace(values[j]);
                if (string.IsNullOrWhiteSpace(value) ||
                    !seen.Add(value))
                {
                    continue;
                }

                components.Add(
                    value.Split(
                        new[] { ' ' },
                        StringSplitOptions.RemoveEmptyEntries));
            }
        }

        components.Sort((left, right) =>
        {
            int tokenCompare = right.Length.CompareTo(left.Length);
            return tokenCompare != 0
                ? tokenCompare
                : string.Compare(
                    string.Join(" ", left),
                    string.Join(" ", right),
                    StringComparison.OrdinalIgnoreCase);
        });

        sortedCanonicalComponents = components;
    }

    static void EnsureDisplayLocalization()
    {
        string currentLanguage =
            LocalizationSettings.CurrentLanguageCode;
        if (localizedComponentMap != null &&
            string.Equals(
                displayLanguageCode,
                currentLanguage,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        displayLanguageCode = currentLanguage;
        localizedComponentMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < AllListKeys.Length; i++)
        {
            string key = AllListKeys[i];
            string[] canonical =
                NpcGeneratedNameText.GetCanonicalList(key);
            string[] localized =
                NpcGeneratedNameText.GetList(currentLanguage, key);
            int count = Mathf.Min(canonical.Length, localized.Length);

            for (int index = 0; index < count; index++)
            {
                string canonicalValue =
                    CollapseWhitespace(canonical[index]);
                string localizedValue =
                    CollapseWhitespace(localized[index]);
                if (string.IsNullOrWhiteSpace(canonicalValue) ||
                    string.IsNullOrWhiteSpace(localizedValue))
                {
                    continue;
                }

                localizedComponentMap[canonicalValue] = localizedValue;
            }
        }
    }

    static string PickRandomComponent(string key)
    {
        string[] values =
            NpcGeneratedNameText.GetCanonicalList(key);
        if (values == null ||
            values.Length == 0)
        {
            return string.Empty;
        }

        return values[UnityEngine.Random.Range(0, values.Length)];
    }
}
