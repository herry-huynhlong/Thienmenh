using System;
using UnityEngine;

public static class LocalizationSettings
{
    const string LanguagePrefKey = "Localization_CurrentLanguage";
    const string DefaultLanguageCode = "vi";

    static bool initialized;
    static string currentLanguageCode;

    public static event Action LanguageChanged;

    public static string CurrentLanguageCode
    {
        get
        {
            EnsureInitialized();
            return currentLanguageCode;
        }
    }

    public static void SetLanguage(string languageCode)
    {
        EnsureInitialized();

        string normalized = NormalizeLanguageCode(languageCode);
        if (string.Equals(
                currentLanguageCode,
                normalized,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentLanguageCode = normalized;
        PlayerPrefs.SetString(LanguagePrefKey, currentLanguageCode);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public static TextAsset LoadTextAsset(string resourceName)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        TextAsset asset =
            Resources.Load<TextAsset>(
                BuildLocalizedResourcePath(currentLanguageCode, resourceName));
        if (asset != null)
        {
            return asset;
        }

        if (!string.Equals(
                currentLanguageCode,
                DefaultLanguageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            asset =
                Resources.Load<TextAsset>(
                    BuildLocalizedResourcePath(DefaultLanguageCode, resourceName));
            if (asset != null)
            {
                return asset;
            }
        }

        return Resources.Load<TextAsset>(resourceName);
    }

    public static string GetLocalizedResourcePath(string resourceName)
    {
        EnsureInitialized();
        return BuildLocalizedResourcePath(currentLanguageCode, resourceName);
    }

    static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        currentLanguageCode = NormalizeLanguageCode(
            PlayerPrefs.GetString(
                LanguagePrefKey,
                DefaultLanguageCode));
    }

    static string NormalizeLanguageCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return DefaultLanguageCode;
        }

        return languageCode.Trim().ToLowerInvariant();
    }

    static string BuildLocalizedResourcePath(
        string languageCode,
        string resourceName)
    {
        return "Localization/" + NormalizeLanguageCode(languageCode) + "/" + resourceName;
    }
}
