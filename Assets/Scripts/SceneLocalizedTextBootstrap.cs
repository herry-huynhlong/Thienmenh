using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1200)]
public class SceneLocalizedTextBootstrap : MonoBehaviour
{
    struct SceneTextEntry
    {
        public string sceneName;
        public string objectName;
        public string directParentName;
        public string category;
        public string key;
        public string fallback;
    }

    static SceneLocalizedTextBootstrap instance;

    static readonly SceneTextEntry[] MainMenuEntries =
    {
        CreateEntry(
            "MainMenu",
            "SettingsPanel",
            "sceneUi",
            "settingsLanguage",
            "Ngon ngu"),
        CreateEntry(
            "MainMenu",
            "AudioLabel",
            "mainMenu",
            "settingsVolume",
            "Am thanh"),
        CreateEntry(
            "MainMenu",
            "TitleText",
            "sceneUi",
            "gameTitle",
            "Phuc Hung Thien Dao")
    };

    static readonly SceneTextEntry[] PersistentSceneEntries =
    {
        CreateEntry(
            "PersistentScene",
            "LTText",
            "sceneUi",
            "spiritStoneAmountDefault",
            "0 LT"),
        CreateEntry(
            "PersistentScene",
            "danhsach",
            "sceneUi",
            "listTitle",
            "Danh sach"),
        CreateEntry(
            "PersistentScene",
            "SoLuong",
            "sceneUi",
            "quantity",
            "So luong"),
        CreateEntry(
            "PersistentScene",
            "Gia",
            "sceneUi",
            "price",
            "Gia"),
        CreateEntry(
            "PersistentScene",
            "tinhtrang",
            "sceneUi",
            "status",
            "Tinh trang"),
        CreateEntry(
            "PersistentScene",
            "Xem Video",
            "sceneUi",
            "watchAd",
            "Xem Video"),
        CreateEntry(
            "PersistentScene",
            "Text (TMP)",
            "ShopPanel",
            "sceneUi",
            "heavenDaoShopTitle",
            "Thiên Đạo Lâu"),
        CreateEntry(
            "PersistentScene",
            "Gói Cơ Bản",
            "Button_Pack1",
            "sceneUi",
            "heavenDaoPackBasic",
            "Gói Cơ Bản"),
        CreateEntry(
            "PersistentScene",
            "Gói Nâng Cao",
            "Button_Pack2",
            "sceneUi",
            "heavenDaoPackAdvanced",
            "Gói Nâng Cao"),
        CreateEntry(
            "PersistentScene",
            "Gói Cao Cấp",
            "Button_Pack3",
            "sceneUi",
            "heavenDaoPackPremium",
            "Gói Cao Cấp")
    };

    static readonly SceneTextEntry[] LangSceneEntries =
    {
        CreateEntry(
            "Lang",
            "TitleText",
            "sceneUi",
            "heavenDaoTitle",
            "THIEN DAO CHUONG KHONG"),
        CreateEntry(
            "Lang",
            "Text_RecentTitle",
            "sceneUi",
            "recentTitle",
            "Gan Day"),
        CreateEntry(
            "Lang",
            "Xem Video",
            "sceneUi",
            "watchAd",
            "Xem Video"),
        CreateEntry(
            "Lang",
            "Text_FooterHint",
            "heavenDao",
            "footerHint",
            "")
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (instance != null)
        {
            instance.ScheduleRefresh();
            return;
        }

        GameObject bootstrapObject =
            new GameObject("SceneLocalizedTextBootstrap");
        DontDestroyOnLoad(bootstrapObject);
        instance =
            bootstrapObject.AddComponent<SceneLocalizedTextBootstrap>();
    }

    static SceneTextEntry CreateEntry(
        string sceneName,
        string objectName,
        string category,
        string key,
        string fallback)
    {
        return CreateEntry(
            sceneName,
            objectName,
            string.Empty,
            category,
            key,
            fallback);
    }

    static SceneTextEntry CreateEntry(
        string sceneName,
        string objectName,
        string directParentName,
        string category,
        string key,
        string fallback)
    {
        return new SceneTextEntry
        {
            sceneName = sceneName,
            objectName = objectName,
            directParentName = directParentName,
            category = category,
            key = key,
            fallback = fallback
        };
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        LocalizationSettings.LanguageChanged += HandleLanguageChanged;
        ScheduleRefresh();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleRefresh();
    }

    void HandleLanguageChanged()
    {
        ScheduleRefresh();
    }

    void ScheduleRefresh()
    {
        if (isActiveAndEnabled)
        {
            StartCoroutine(RefreshAfterUiAwake());
        }
    }

    IEnumerator RefreshAfterUiAwake()
    {
        yield return null;
        yield return null;
        ApplyLocalizedTexts();
    }

    void ApplyLocalizedTexts()
    {
        ApplyMainMenuTexts();
        ApplyPersistentSceneTexts();
        ApplyLangSceneTexts();
    }

    void ApplyMainMenuTexts()
    {
        if (!IsSceneLoaded("MainMenu"))
        {
            return;
        }

        ApplyEntries(MainMenuEntries);
        SetTextByName(
            "MainMenu",
            "Item Label",
            GetCurrentLanguageDisplayName());
        SetTextByName(
            "MainMenu",
            "Label",
            GetCurrentLanguageDisplayName());
    }

    void ApplyPersistentSceneTexts()
    {
        if (!IsSceneLoaded("PersistentScene"))
        {
            return;
        }

        ApplyEntries(PersistentSceneEntries);
        SetTextByName(
            "PersistentScene",
            "AmountText",
            UiText.Get("sceneUi", "amountZero", "x0"));
        SetTextByName(
            "PersistentScene",
            "WorldClockText",
            GetWorldClockPreviewText());
        SetTextByName(
            "PersistentScene",
            "DetailNameText",
            "");
    }

    void ApplyLangSceneTexts()
    {
        if (!IsSceneLoaded("Lang"))
        {
            return;
        }

        ApplyEntries(LangSceneEntries);
        SetTextByName("Lang", "RewardText", "");
        SetTextByName("Lang", "RankText", "");
        SetTextByName("Lang", "RequireText ", "");
        SetTextByName("Lang", "TileText", "");
        SetTextByName("Lang", "ItemNameText", "");
        SetTextByName("Lang", "ItemsText", "");
        SetTextByName("Lang", "ItemInfoText", "");
    }

    string GetCurrentLanguageDisplayName()
    {
        switch (LocalizationSettings.CurrentLanguageCode)
        {
            case "zh":
                return UiText.Get("mainMenu", "languageChinese", "中文");
            case "en":
                return UiText.Get("mainMenu", "languageEnglish", "English");
            default:
                return UiText.Get("mainMenu", "languageVietnamese", "Tiếng Việt");
        }
    }

    string GetWorldClockPreviewText()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            return timeSystem.GetClockText();
        }

        return UiText.Get("worldClock", "defaultWhenUnavailable");
    }

    bool IsSceneLoaded(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() &&
            scene.isLoaded;
    }

    void ApplyEntries(SceneTextEntry[] entries)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            SceneTextEntry entry = entries[i];
            SetTextByName(
                entry.sceneName,
                entry.objectName,
                entry.directParentName,
                UiText.Get(
                    entry.category,
                    entry.key,
                    entry.fallback));
        }
    }

    void SetTextByName(
        string sceneName,
        string objectName,
        string value)
    {
        SetTextByName(
            sceneName,
            objectName,
            string.Empty,
            value);
    }

    void SetTextByName(
        string sceneName,
        string objectName,
        string directParentName,
        string value)
    {
        if (string.IsNullOrWhiteSpace(sceneName) ||
            string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        TMP_Text[] texts =
            FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include);

        foreach (TMP_Text text in texts)
        {
            if (text == null ||
                text.gameObject.scene.name != sceneName ||
                text.gameObject.name != objectName)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(directParentName))
            {
                Transform parent = text.transform.parent;
                if (parent == null ||
                    parent.name != directParentName)
                {
                    continue;
                }
            }

            text.text = value ?? string.Empty;
        }
    }
}
