using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class InGameMenuController : MonoBehaviour
{
    const string TextCategory = "inGameMenu";
    const string VolumePrefKey = "Settings_MasterVolume";
    const string LastVolumePrefKey = "Settings_LastMasterVolume";

    [Header("Panels")]
    [SerializeField] GameObject menuPanel;
    [SerializeField] GameObject settingsPanel;
    [SerializeField] GameObject aboutPanel;
    [SerializeField] GameObject exitConfirmPanel;

    [Header("Open / Main")]
    [SerializeField] Button openMenuButton;
    [SerializeField] Button settingsButton;
    [FormerlySerializedAs("infoButton")]
    [SerializeField] Button saveButton;
    [SerializeField] Button infoButton;
    [FormerlySerializedAs("exitButton")]
    [SerializeField] Button exitButton;

    [Header("Settings")]
    [SerializeField] Button vietnameseButton;
    [SerializeField] Button chineseButton;
    [SerializeField] Button englishButton;
    [SerializeField] Button muteButton;
    [SerializeField] Button unmuteButton;
    [SerializeField] Button closeSettingsButton;

    [Header("About")]
    [SerializeField] Button closeAboutButton;

    [Header("Save Confirmation")]
    [SerializeField] Button confirmExitButton;
    [SerializeField] Button cancelExitButton;

    void Awake()
    {
        ResolveReferences();
        BindButtons();
        CloseAllPanels();
        RefreshLocalizedText();
    }

    void OnEnable()
    {
        LocalizationSettings.LanguageChanged += HandleLanguageChanged;
    }

    void OnDisable()
    {
        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
    }

    public void ToggleMenu()
    {
        ResolveReferences();

        if (menuPanel == null)
        {
            Debug.LogWarning(
                "InGameMenuController could not find InGameMenuPanel.",
                this);
            return;
        }

        if (menuPanel.activeSelf)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    public void OpenMenu()
    {
        ResolveReferences();
        SetActive(settingsPanel, false);
        SetActive(aboutPanel, false);
        SetActive(exitConfirmPanel, false);
        SetActive(menuPanel, true);
        RefreshLocalizedText();
    }

    public void CloseMenu()
    {
        SetActive(settingsPanel, false);
        SetActive(aboutPanel, false);
        SetActive(exitConfirmPanel, false);
        SetActive(menuPanel, false);
    }

    public void OpenSettings()
    {
        OpenChildPanel(settingsPanel, "SettingsPanel");
    }

    public void CloseSettings()
    {
        SetActive(settingsPanel, false);
    }

    public void OpenAbout()
    {
        OpenChildPanel(aboutPanel, "AboutPanel");
    }

    public void CloseAbout()
    {
        SetActive(aboutPanel, false);
    }

    public void OpenExitConfirmation()
    {
        OpenChildPanel(exitConfirmPanel, "ExitConfirmPanel");
    }

    public void CloseExitConfirmation()
    {
        SetActive(exitConfirmPanel, false);
    }

    public void SetLanguageVietnamese()
    {
        LocalizationSettings.SetLanguage("vi");
    }

    public void SetLanguageChinese()
    {
        LocalizationSettings.SetLanguage("zh");
    }

    public void SetLanguageEnglish()
    {
        LocalizationSettings.SetLanguage("en");
    }

    public void MuteAudio()
    {
        float currentVolume = AudioListener.volume;
        if (currentVolume > 0.001f)
        {
            PlayerPrefs.SetFloat(LastVolumePrefKey, currentVolume);
        }

        SetAndSaveVolume(0f);
    }

    public void UnmuteAudio()
    {
        float restoredVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(LastVolumePrefKey, 1f));
        if (restoredVolume <= 0.001f)
        {
            restoredVolume = 1f;
        }

        SetAndSaveVolume(restoredVolume);
    }

    public void ConfirmExit()
    {
        FullGameSaveController saveController =
            FullGameSaveController.EnsureInstance();
        if (saveController == null)
        {
            Debug.LogWarning(
                "InGameMenuController could not find the save controller.",
                this);
            return;
        }

        saveController.SaveFullGame(true);
        CloseMenu();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void RefreshLocalizedText()
    {
        ResolveReferences();

        SetNamedText(
            menuPanel,
            "TitleText",
            "panelTitle",
            "Bảng Điều Khiển");
        SetButtonText(settingsButton, "settingsButton", "Cài đặt");
        SetButtonText(saveButton, "saveButton", "Lưu game");
        SetButtonText(infoButton, "infoButton", "Giới thiệu");
        SetButtonText(exitButton, "exitButton", "Đóng Game");

        SetNamedText(
            settingsPanel,
            "SettingsTitle",
            "settingsTitle",
            "Cài đặt");
        SetNamedText(
            settingsPanel,
            "LangLabel",
            "languageLabel",
            "Ngôn ngữ");
        SetNamedText(
            settingsPanel,
            "AudioLabel",
            "audioLabel",
            "Âm thanh");
        SetButtonText(
            vietnameseButton,
            "languageVietnamese",
            "Tiếng Việt");
        SetButtonText(
            chineseButton,
            "languageChinese",
            "中文");
        SetButtonText(
            englishButton,
            "languageEnglish",
            "English");
        SetButtonText(muteButton, "muteButton", "Tắt âm");
        SetButtonText(unmuteButton, "unmuteButton", "Bật âm");
        SetButtonText(closeSettingsButton, "closeButton", "Đóng");

        SetNamedText(
            aboutPanel,
            "AboutTitle",
            "aboutTitle",
            "Giới thiệu");
        SetNamedText(
            aboutPanel,
            "BodyText",
            "aboutBody",
            "Thiên Mệnh Chi Tử - game tu tiên RPG");
        SetButtonText(closeAboutButton, "closeButton", "Đóng");

        SetNamedText(
            exitConfirmPanel,
            "ExitTitle",
            "exitTitle",
            "Xác nhận lưu");
        SetNamedText(
            exitConfirmPanel,
            "QuestionText",
            "exitQuestion",
            "Bạn có muốn lưu tiến trình ngay bây giờ không?");
        SetButtonText(confirmExitButton, "yesButton", "Có");
        SetButtonText(cancelExitButton, "noButton", "Không");
        RefreshBottomMenuTexts();
    }

    void ResolveReferences()
    {
        if (menuPanel == null)
        {
            Transform menu = FindSceneTransform("InGameMenuPanel");
            if (menu == null)
            {
                menu = FindBestLegacyMenuPanel();
            }

            menuPanel = menu != null ? menu.gameObject : null;
        }

        Transform menuRoot = menuPanel != null ? menuPanel.transform : null;

        settingsPanel = ResolvePanel(settingsPanel, menuRoot, "SettingsPanel");
        aboutPanel = ResolvePanel(aboutPanel, menuRoot, "AboutPanel");
        exitConfirmPanel = ResolvePanel(
            exitConfirmPanel,
            menuRoot,
            "ExitConfirmPanel");

        openMenuButton = ResolveSceneButton(openMenuButton, "Setting");
        settingsButton = ResolveButton(settingsButton, menuRoot, "BtnSetting");
        saveButton = ResolveButton(saveButton, menuRoot, "Luugame");
        infoButton = ResolveButton(infoButton, menuRoot, "BtnInfo");
        exitButton = ResolveButton(exitButton, menuRoot, "BtnCloseGame");

        Transform settingsRoot =
            settingsPanel != null ? settingsPanel.transform : null;
        vietnameseButton = ResolveButton(
            vietnameseButton,
            settingsRoot,
            "BtnLangVi");
        chineseButton = ResolveButton(
            chineseButton,
            settingsRoot,
            "BtnLangZh");
        englishButton = ResolveButton(
            englishButton,
            settingsRoot,
            "BtnLangEn");
        muteButton = ResolveButton(
            muteButton,
            settingsRoot,
            "BtnMuteAudio");
        unmuteButton = ResolveButton(
            unmuteButton,
            settingsRoot,
            "BtnUnmuteAudio");
        closeSettingsButton = ResolveButton(
            closeSettingsButton,
            settingsRoot,
            "CloseButton");

        Transform aboutRoot = aboutPanel != null ? aboutPanel.transform : null;
        closeAboutButton = ResolveButton(
            closeAboutButton,
            aboutRoot,
            "InfoCloseButton");

        Transform exitRoot =
            exitConfirmPanel != null ? exitConfirmPanel.transform : null;
        confirmExitButton = ResolveButton(
            confirmExitButton,
            exitRoot,
            "BtnYes");
        cancelExitButton = ResolveButton(
            cancelExitButton,
            exitRoot,
            "BtnNo");
    }

    void BindButtons()
    {
        BindButton(openMenuButton, ToggleMenu, nameof(ToggleMenu));
        BindButton(settingsButton, OpenSettings, nameof(OpenSettings));
        BindButton(
            saveButton,
            OpenExitConfirmation,
            nameof(OpenExitConfirmation));
        BindButton(infoButton, OpenAbout, nameof(OpenAbout));
        BindButton(exitButton, QuitGame, nameof(QuitGame));
        BindButton(
            vietnameseButton,
            SetLanguageVietnamese,
            nameof(SetLanguageVietnamese));
        BindButton(
            chineseButton,
            SetLanguageChinese,
            nameof(SetLanguageChinese));
        BindButton(
            englishButton,
            SetLanguageEnglish,
            nameof(SetLanguageEnglish));
        BindButton(muteButton, MuteAudio, nameof(MuteAudio));
        BindButton(unmuteButton, UnmuteAudio, nameof(UnmuteAudio));
        BindButton(
            closeSettingsButton,
            CloseSettings,
            nameof(CloseSettings));
        BindButton(closeAboutButton, CloseAbout, nameof(CloseAbout));
        BindButton(confirmExitButton, ConfirmExit, nameof(ConfirmExit));
        BindButton(
            cancelExitButton,
            CloseExitConfirmation,
            nameof(CloseExitConfirmation));
    }

    void BindButton(
        Button button,
        UnityAction action,
        string persistentMethodName)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        if (!HasPersistentListener(button, persistentMethodName))
        {
            button.onClick.AddListener(action);
        }
    }

    bool HasPersistentListener(Button button, string methodName)
    {
        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }

        return false;
    }

    void OpenChildPanel(GameObject panel, string panelName)
    {
        ResolveReferences();

        if (panel == null)
        {
            Debug.LogWarning(
                $"InGameMenuController could not find {panelName}.",
                this);
            return;
        }

        SetActive(menuPanel, true);
        SetActive(settingsPanel, panel == settingsPanel);
        SetActive(aboutPanel, panel == aboutPanel);
        SetActive(exitConfirmPanel, panel == exitConfirmPanel);
        RefreshLocalizedText();
    }

    void CloseAllPanels()
    {
        SetActive(settingsPanel, false);
        SetActive(aboutPanel, false);
        SetActive(exitConfirmPanel, false);
        SetActive(menuPanel, false);
    }

    void HandleLanguageChanged()
    {
        RefreshLocalizedText();
    }

    void SetAndSaveVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(VolumePrefKey, volume);
        PlayerPrefs.Save();
    }

    void SetNamedText(
        GameObject root,
        string objectName,
        string key,
        string fallback)
    {
        if (root == null)
        {
            return;
        }

        Transform target = FindChildRecursive(root.transform, objectName);
        if (target != null)
        {
            SetText(target.gameObject, UiText.Get(TextCategory, key, fallback));
        }
    }

    void SetButtonText(Button button, string key, string fallback)
    {
        if (button != null)
        {
            SetText(
                button.gameObject,
                UiText.Get(TextCategory, key, fallback));
        }
    }

    void RefreshBottomMenuTexts()
    {
        Transform bottomMenu = FindBottomMenuPanel();
        if (bottomMenu == null)
        {
            return;
        }

        SetBottomMenuText(bottomMenu, "Shop", "bottomShop", "Cửa Hàng");
        SetBottomMenuText(
            bottomMenu,
            "Balo",
            "bottomInventory",
            "Trữ Vật");
        SetBottomMenuText(
            bottomMenu,
            "Story",
            "bottomJournal",
            "Nhật Ký");
        SetBottomMenuText(bottomMenu, "map", "bottomMap", "Bản Đồ");
    }

    void SetBottomMenuText(
        Transform bottomMenu,
        string itemName,
        string key,
        string fallback)
    {
        Transform item = FindChildRecursive(bottomMenu, itemName);
        if (item != null)
        {
            SetText(item.gameObject, UiText.Get(TextCategory, key, fallback));
        }
    }

    Transform FindBottomMenuPanel()
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include);

        foreach (Transform candidate in transforms)
        {
            if (candidate == null ||
                candidate.gameObject.scene != gameObject.scene ||
                candidate.name != "MenuPanel")
            {
                continue;
            }

            if (FindChildRecursive(candidate, "Shop") != null &&
                FindChildRecursive(candidate, "Balo") != null &&
                FindChildRecursive(candidate, "Story") != null &&
                FindChildRecursive(candidate, "map") != null)
            {
                return candidate;
            }
        }

        return null;
    }

    void SetText(GameObject target, string value)
    {
        TMP_Text tmp = target.GetComponent<TMP_Text>();
        if (tmp == null)
        {
            tmp = target.GetComponentInChildren<TMP_Text>(true);
        }

        if (tmp != null)
        {
            tmp.text = value;
            return;
        }

        Text legacyText = target.GetComponent<Text>();
        if (legacyText == null)
        {
            legacyText = target.GetComponentInChildren<Text>(true);
        }

        if (legacyText != null)
        {
            legacyText.text = value;
        }
    }

    GameObject ResolvePanel(
        GameObject current,
        Transform root,
        string objectName)
    {
        if (current != null)
        {
            return current;
        }

        Transform found = FindChildRecursive(root, objectName);
        return found != null ? found.gameObject : null;
    }

    Button ResolveButton(Button current, Transform root, string objectName)
    {
        if (current != null)
        {
            return current;
        }

        Transform found = FindChildRecursive(root, objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    Button ResolveSceneButton(Button current, string objectName)
    {
        if (current != null)
        {
            return current;
        }

        Transform found = FindSceneTransform(objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    Transform FindBestLegacyMenuPanel()
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include);
        Transform fallback = null;

        foreach (Transform candidate in transforms)
        {
            if (candidate == null ||
                candidate.gameObject.scene != gameObject.scene ||
                candidate.name != "MainMenuPanel")
            {
                continue;
            }

            fallback ??= candidate;
            if (FindChildRecursive(candidate, "AboutPanel") != null)
            {
                return candidate;
            }
        }

        return fallback;
    }

    Transform FindSceneTransform(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include);

        foreach (Transform candidate in transforms)
        {
            if (candidate != null &&
                candidate.gameObject.scene == gameObject.scene &&
                candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == objectName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
