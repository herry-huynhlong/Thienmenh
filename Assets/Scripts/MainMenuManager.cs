using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public string firstGameScene = "PersistentScene";

    [Header("New Game Intro")]
    public NewGameIntroPanel newGameIntroPanel;

    [Header("Settings")]
    public GameObject settingsPanel;
    public Slider masterVolumeSlider;
    public Toggle fullscreenToggle;
    public Dropdown targetFpsDropdown;
    public Button muteVolumeButton;
    public Button unmuteVolumeButton;
    public Button applySettingsButton;
    public Button closeSettingsButton;

    const string VolumePrefKey = "Settings_MasterVolume";
    const string LastVolumePrefKey = "Settings_LastMasterVolume";
    const string FullscreenPrefKey = "Settings_Fullscreen";
    const string TargetFpsPrefKey = "Settings_TargetFps";
    bool createdRuntimeSettingsPanel;

    void Awake()
    {
        EnsureSingleActiveEventSystem();
        EnsureCanvasRaycaster();
        // ApplySavedSettings();
        HookMainMenuButtonsByName();
        HookSettingsControls();
        HookSettingsButtonByName();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void NewGame()
    {
        Debug.Log("MainMenuManager: NewGame clicked. Showing intro.");

        if (newGameIntroPanel == null)
        {
            newGameIntroPanel =
                FindAnyObjectByType<NewGameIntroPanel>(
                    FindObjectsInactive.Include);
        }

        if (newGameIntroPanel != null)
        {
            newGameIntroPanel.ShowIntro();
            return;
        }

        Debug.LogWarning(
            "MainMenuManager: Khong tim thay NewGameIntroPanel, vao game luon.");

        StartNewGameNow();
    }

    public void StartNewGameNow()
    {
        Debug.Log("MainMenuManager: Starting new game. Clearing save.");

        float savedVolume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        int savedFullscreen =
            PlayerPrefs.GetInt(FullscreenPrefKey, Screen.fullScreen ? 1 : 0);
        int savedTargetFps = PlayerPrefs.GetInt(TargetFpsPrefKey, 60);

        GameSaveSystem.RequestNewGameStart();
        GameSaveSystem.ClearSave();

        PlayerPrefs.DeleteAll();

        PlayerPrefs.SetFloat(VolumePrefKey, savedVolume);
        PlayerPrefs.SetInt(FullscreenPrefKey, savedFullscreen);
        PlayerPrefs.SetInt(TargetFpsPrefKey, savedTargetFps);
        PlayerPrefs.Save();

        ItemInventory.ClearRuntimeCache();
        SimpleItemShop.ClearRuntimeStockCache();

        LoadFirstGameScene();
    }

    public void ContinueGame()
    {
        Debug.Log("MainMenuManager: ContinueGame clicked.");

        if (!GameSaveSystem.HasSave)
        {
            NewGame();
            return;
        }

        GameSaveSystem.RequestContinueGameStart();
        LoadFirstGameScene();
    }

    void LoadFirstGameScene()
    {
        if (string.IsNullOrEmpty(firstGameScene) ||
            !Application.CanStreamedLevelBeLoaded(firstGameScene))
        {
            Debug.LogWarning(
                $"Cannot load first game scene '{firstGameScene}'.");
            return;
        }

        SceneManager.LoadScene(firstGameScene);
    }

    void HookMainMenuButtonsByName()
    {
        Button[] buttons =
            FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            string key = GetButtonKey(button);

            if (IsNewGameButtonKey(key))
            {
                ReplaceButtonClick(button, NewGame);
                SetButtonLabel(
                    button,
                    UiText.Get("mainMenu", "newGame"));
                continue;
            }

            if (IsContinueButtonKey(key))
            {
                ReplaceButtonClick(button, ContinueGame);
                SetButtonLabel(
                    button,
                    UiText.Get("mainMenu", "continueGame"));
                continue;
            }

            if (IsSettingsButtonKey(key))
            {
                ReplaceButtonClick(button, OpenSettings);
                SetButtonLabel(
                    button,
                    UiText.Get("mainMenu", "settingsMenu"));
                continue;
            }

            if (IsAboutButtonKey(key))
            {
                ReplaceButtonClick(button, About);
                SetButtonLabel(
                    button,
                    UiText.Get("mainMenu", "aboutMenu"));
                continue;
            }

            if (IsExitButtonKey(key))
            {
                ReplaceButtonClick(button, QuitGame);
                SetButtonLabel(
                    button,
                    UiText.Get("mainMenu", "exitGame"));
            }
        }
    }

    void EnsureSingleActiveEventSystem()
    {
        EventSystem[] systems =
            FindObjectsByType<EventSystem>(FindObjectsInactive.Include);

        if (systems.Length == 0)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            return;
        }

        EventSystem keep = ChoosePreferredEventSystem(systems);

        foreach (EventSystem system in systems)
        {
            if (system == null || system == keep)
            {
                continue;
            }

            if (system.gameObject.activeSelf)
            {
                Debug.LogWarning(
                    $"MainMenuManager: disabling duplicate EventSystem '{system.name}'.");
                system.gameObject.SetActive(false);
            }
        }

        if (keep == null)
        {
            return;
        }

        if (!keep.gameObject.activeSelf)
        {
            keep.gameObject.SetActive(true);
        }

        keep.enabled = true;
        EventSystem.current = keep;

        BaseInputModule[] modules = keep.GetComponents<BaseInputModule>();

        foreach (BaseInputModule module in modules)
        {
            if (module != null)
            {
                module.enabled = true;
            }
        }
    }

    EventSystem ChoosePreferredEventSystem(EventSystem[] systems)
    {
        EventSystem firstActive = null;

        foreach (EventSystem system in systems)
        {
            if (system == null)
            {
                continue;
            }

            if (firstActive == null && system.gameObject.activeInHierarchy)
            {
                firstActive = system;
            }

            BaseInputModule[] modules = system.GetComponents<BaseInputModule>();

            foreach (BaseInputModule module in modules)
            {
                if (module == null)
                {
                    continue;
                }

                if (module.GetType().Name.Contains("InputSystemUIInputModule"))
                {
                    return system;
                }
            }
        }

        return firstActive != null ? firstActive : systems[0];
    }

    void EnsureCanvasRaycaster()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            return;
        }

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();

        if (raycaster == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            return;
        }

        raycaster.enabled = true;
    }

    void ReplaceButtonClick(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
    }

    string GetButtonKey(Button button)
    {
        if (button == null)
        {
            return "";
        }

        string label = GetButtonLabel(button);
        return NormalizeMenuKey(button.name + " " + label);
    }

    string GetButtonLabel(Button button)
    {
        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);

        if (tmpText != null)
        {
            return tmpText.text;
        }

        Text legacyText = button.GetComponentInChildren<Text>(true);
        return legacyText != null ? legacyText.text : "";
    }

    void SetButtonLabel(Button button, string value)
    {
        if (button == null || string.IsNullOrEmpty(value))
        {
            return;
        }

        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = value;
            return;
        }

        Text legacyText = button.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = value;
        }
    }

    bool IsNewGameButtonKey(string key)
    {
        return key.Contains("newgame") ||
            key.Contains("batdau") ||
            key.Contains("start") ||
            key.Contains("play") ||
            key.Contains("choimoi");
    }

    bool IsContinueButtonKey(string key)
    {
        return key.Contains("continue") ||
            key.Contains("contine") ||
            key.Contains("tieptuc") ||
            key.Contains("loadgame");
    }

    bool IsSettingsButtonKey(string key)
    {
        return key.Contains("settings") ||
            key.Contains("setting") ||
            key.Contains("options") ||
            key.Contains("caidat");
    }

    bool IsAboutButtonKey(string key)
    {
        return key.Contains("about") ||
            key.Contains("gioithieu") ||
            key.Contains("intro");
    }

    bool IsExitButtonKey(string key)
    {
        return key.Contains("exit") ||
            key.Contains("quit") ||
            key.Contains("thoat");
    }

    string NormalizeMenuKey(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c == '\u0111' || c == '\u0110')
            {
                builder.Append('d');
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    public void About()
    {
        Debug.Log(UiText.Get("mainMenu", "aboutLog"));
    }

    public void Settings()
    {
        OpenSettings();
    }

    public void OpenSettings()
    {
        EnsureSettingsPanel();
        SyncSettingsControls();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void ToggleSettings()
    {
        EnsureSettingsPanel();

        if (settingsPanel == null)
        {
            return;
        }

        bool nextActive = !settingsPanel.activeSelf;
        settingsPanel.SetActive(nextActive);

        if (nextActive)
        {
            SyncSettingsControls();
        }
    }

    public void ApplySettings()
    {
        float volume = masterVolumeSlider != null
            ? masterVolumeSlider.value
            : PlayerPrefs.GetFloat(VolumePrefKey, 1f);

        bool fullscreen = fullscreenToggle != null
            ? fullscreenToggle.isOn
            : PlayerPrefs.GetInt(
                FullscreenPrefKey,
                Screen.fullScreen ? 1 : 0) == 1;

        int fps = GetSelectedTargetFps();

        PlayerPrefs.SetFloat(VolumePrefKey, Mathf.Clamp01(volume));
        PlayerPrefs.SetInt(FullscreenPrefKey, fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(TargetFpsPrefKey, fps);
        PlayerPrefs.Save();

        ApplyRuntimeSettings(volume, fullscreen, fps);
    }

    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
    }

    public void MuteVolume()
    {
        float currentVolume = masterVolumeSlider != null
            ? masterVolumeSlider.value
            : AudioListener.volume;

        if (currentVolume > 0.001f)
        {
            PlayerPrefs.SetFloat(LastVolumePrefKey, currentVolume);
        }

        SetVolumeValue(0f);
        ApplySettings();
    }

    public void UnmuteVolume()
    {
        float restoreVolume =
            PlayerPrefs.GetFloat(LastVolumePrefKey, 1f);

        if (restoreVolume <= 0.001f)
        {
            restoreVolume = 1f;
        }

        SetVolumeValue(restoreVolume);
        ApplySettings();
    }

    public void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    void ApplySavedSettings()
    {
        float volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        bool fullscreen =
            PlayerPrefs.GetInt(
                FullscreenPrefKey,
                Screen.fullScreen ? 1 : 0) == 1;
        int fps = PlayerPrefs.GetInt(TargetFpsPrefKey, 60);

        ApplyRuntimeSettings(volume, fullscreen, fps);
    }

    void ApplyRuntimeSettings(float volume, bool fullscreen, int fps)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
        Screen.fullScreen = fullscreen;
        GamePerformanceSettings.Apply(fps, 1f / 30f, 0.08f, true);
    }

    void HookSettingsControls()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        if (applySettingsButton != null)
        {
            applySettingsButton.onClick.RemoveListener(ApplySettings);
            applySettingsButton.onClick.AddListener(ApplySettings);
        }

        if (muteVolumeButton != null)
        {
            muteVolumeButton.onClick.RemoveListener(MuteVolume);
            muteVolumeButton.onClick.AddListener(MuteVolume);
        }

        if (unmuteVolumeButton != null)
        {
            unmuteVolumeButton.onClick.RemoveListener(UnmuteVolume);
            unmuteVolumeButton.onClick.AddListener(UnmuteVolume);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveListener(CloseSettings);
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }
    }

    void HookSettingsButtonByName()
    {
        Button[] buttons =
            FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            if (button == null ||
                !IsSettingsButtonKey(GetButtonKey(button)))
            {
                continue;
            }

            button.onClick.RemoveListener(OpenSettings);
            button.onClick.AddListener(OpenSettings);
        }
    }

    bool IsSettingsButtonName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName))
        {
            return false;
        }

        string normalized =
            buttonName.Trim().ToLowerInvariant();

        return normalized == "settings" ||
            normalized == "setting" ||
            normalized == "options" ||
            normalized == "cai dat" ||
            normalized == "cài đặt";
    }

    void SyncSettingsControls()
    {
        if (masterVolumeSlider != null)
        {
            SetVolumeValue(
                PlayerPrefs.GetFloat(VolumePrefKey, AudioListener.volume));
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(
                PlayerPrefs.GetInt(
                    FullscreenPrefKey,
                    Screen.fullScreen ? 1 : 0) == 1);
        }

        if (targetFpsDropdown != null)
        {
            targetFpsDropdown.SetValueWithoutNotify(
                TargetFpsToDropdownIndex(
                    PlayerPrefs.GetInt(TargetFpsPrefKey, 60)));
        }
    }

    int GetSelectedTargetFps()
    {
        if (targetFpsDropdown == null)
        {
            return PlayerPrefs.GetInt(TargetFpsPrefKey, 60);
        }

        switch (targetFpsDropdown.value)
        {
            case 0:
                return 30;
            case 2:
                return 120;
            default:
                return 60;
        }
    }

    int TargetFpsToDropdownIndex(int fps)
    {
        if (fps <= 30)
        {
            return 0;
        }

        if (fps >= 120)
        {
            return 2;
        }

        return 1;
    }

    void SetVolumeValue(float volume)
    {
        volume = Mathf.Clamp01(volume);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(volume);
        }

        AudioListener.volume = volume;
    }

    void EnsureSettingsPanel()
    {
        if (settingsPanel != null)
        {
            return;
        }

        BuildRuntimeSettingsPanel();
    }

    void BuildRuntimeSettingsPanel()
    {
        if (createdRuntimeSettingsPanel)
        {
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning(
                "MainMenuManager: No Canvas found for settings panel.");
            return;
        }

        createdRuntimeSettingsPanel = true;

        settingsPanel = CreatePanel(canvas.transform);
        CreateLabel(
            settingsPanel.transform,
            UiText.Get("mainMenu", "settingsTitle"),
            new Vector2(0f, 150f),
            42);

        masterVolumeSlider =
            CreateSlider(
                settingsPanel.transform,
                UiText.Get("mainMenu", "settingsVolume"),
                new Vector2(0f, 70f));

        fullscreenToggle =
            CreateToggle(
                settingsPanel.transform,
                UiText.Get("mainMenu", "settingsFullscreen"),
                new Vector2(0f, 10f));

        targetFpsDropdown =
            CreateDropdown(
                settingsPanel.transform,
                UiText.Get("mainMenu", "settingsFps"),
                new Vector2(0f, -60f));

        muteVolumeButton =
            CreateButton(
                settingsPanel.transform,
                UiText.Get("mainMenu", "settingsMute"),
                new Vector2(-110f, -105f));

        unmuteVolumeButton =
            CreateButton(
                settingsPanel.transform,
                UiText.Get("mainMenu", "settingsUnmute"),
                new Vector2(110f, -105f));

        applySettingsButton =
            CreateButton(
                settingsPanel.transform,
                UiText.Get("mainMenu", "settingsSave"),
                new Vector2(-110f, -165f));

        closeSettingsButton =
            CreateButton(
                settingsPanel.transform,
                UiText.Get("mainMenu", "settingsClose"),
                new Vector2(110f, -165f));

        HookSettingsControls();
        settingsPanel.SetActive(false);
    }

    GameObject CreatePanel(Transform parent)
    {
        GameObject panel = new GameObject("SettingsPanel");
        panel.transform.SetParent(parent, false);

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.12f, 0.94f);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(560f, 420f);

        return panel;
    }

    Text CreateLabel(
        Transform parent,
        string text,
        Vector2 position,
        int fontSize)
    {
        GameObject labelObject = new GameObject(text);
        labelObject.transform.SetParent(parent, false);

        Text label = labelObject.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(420f, 48f);

        return label;
    }

    Slider CreateSlider(Transform parent, string label, Vector2 position)
    {
        CreateLabel(parent, label, position + new Vector2(-155f, 0f), 24);

        GameObject sliderObject = new GameObject(label + " Slider");
        sliderObject.transform.SetParent(parent, false);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        RectTransform rect = slider.GetComponent<RectTransform>();
        rect.anchoredPosition = position + new Vector2(95f, 0f);
        rect.sizeDelta = new Vector2(250f, 24f);

        GameObject background =
            CreateSliderImage(sliderObject.transform, "Background", Color.gray);
        GameObject fill =
            CreateSliderImage(sliderObject.transform, "Fill", new Color(0.28f, 0.68f, 1f));
        GameObject handle =
            CreateSliderImage(sliderObject.transform, "Handle", Color.white);

        slider.targetGraphic = handle.GetComponent<Image>();
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();

        background.GetComponent<RectTransform>().sizeDelta = new Vector2(250f, 10f);
        fill.GetComponent<RectTransform>().sizeDelta = new Vector2(250f, 10f);
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 22f);

        return slider;
    }

    GameObject CreateSliderImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = color;

        RectTransform rect = image.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        return imageObject;
    }

    Toggle CreateToggle(Transform parent, string label, Vector2 position)
    {
        GameObject toggleObject = new GameObject(label + " Toggle");
        toggleObject.transform.SetParent(parent, false);

        Toggle toggle = toggleObject.AddComponent<Toggle>();

        RectTransform rect = toggle.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 42f);

        GameObject background = new GameObject("Background");
        background.transform.SetParent(toggleObject.transform, false);

        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = Color.white;

        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchoredPosition = new Vector2(-155f, 0f);
        backgroundRect.sizeDelta = new Vector2(28f, 28f);

        GameObject checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(background.transform, false);

        Image checkmarkImage = checkmark.AddComponent<Image>();
        checkmarkImage.color = new Color(0.28f, 0.68f, 1f);

        RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
        checkmarkRect.anchoredPosition = Vector2.zero;
        checkmarkRect.sizeDelta = new Vector2(18f, 18f);

        Text text = CreateLabel(toggleObject.transform, label, new Vector2(30f, 0f), 24);
        text.alignment = TextAnchor.MiddleLeft;

        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;

        return toggle;
    }

    Dropdown CreateDropdown(Transform parent, string label, Vector2 position)
    {
        CreateLabel(parent, label, position + new Vector2(-155f, 0f), 24);

        GameObject dropdownObject = new GameObject(label + " Dropdown");
        dropdownObject.transform.SetParent(parent, false);

        Dropdown dropdown = dropdownObject.AddComponent<Dropdown>();
        dropdown.options.Clear();
        dropdown.options.Add(new Dropdown.OptionData("30"));
        dropdown.options.Add(new Dropdown.OptionData("60"));
        dropdown.options.Add(new Dropdown.OptionData("120"));

        Image image = dropdownObject.AddComponent<Image>();
        image.color = Color.white;
        dropdown.targetGraphic = image;

        Text caption = CreateLabel(dropdownObject.transform, "", Vector2.zero, 22);
        caption.color = Color.black;
        dropdown.captionText = caption;

        RectTransform rect = dropdown.GetComponent<RectTransform>();
        rect.anchoredPosition = position + new Vector2(95f, 0f);
        rect.sizeDelta = new Vector2(160f, 42f);

        return dropdown;
    }

    Button CreateButton(Transform parent, string label, Vector2 position)
    {
        GameObject buttonObject = new GameObject(label);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.25f, 0.32f, 0.44f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateLabel(buttonObject.transform, label, Vector2.zero, 24);
        text.color = Color.white;

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(160f, 54f);

        return button;
    }
}
