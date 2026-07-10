using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(1250)]
public class WeatherControlPanelUI : MonoBehaviour
{
    const string TargetSceneName = "Lang";
    const string RootName = "WeatherUIRoot";
    const string ToggleButtonName = "WeatherToggleButton";
    const string OptionsPanelName = "WeatherOptionsPanel";
    const string RainButtonName = "BtnRain";
    const string SnowButtonName = "BtnSnow";
    const string ClearButtonName = "BtnClear";

    static WeatherControlPanelUI instance;

    Transform uiRoot;
    GameObject optionsPanel;
    Button toggleButton;
    Button rainButton;
    Button snowButton;
    Button clearButton;
    TMP_Text toggleLabel;
    TMP_Text rainLabel;
    TMP_Text snowLabel;
    TMP_Text clearLabel;
    bool panelVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (instance != null)
        {
            instance.ScheduleRefresh();
            return;
        }

        GameObject root = new GameObject("WeatherControlPanelUI");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<WeatherControlPanelUI>();
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
        BindUi();
        ApplyLocalizedTexts();
        ApplyPanelState(false);
    }

    void BindUi()
    {
        uiRoot = FindRootInLoadedScenes(RootName);
        optionsPanel = null;
        toggleButton = null;
        rainButton = null;
        snowButton = null;
        clearButton = null;
        toggleLabel = null;
        rainLabel = null;
        snowLabel = null;
        clearLabel = null;

        if (uiRoot == null ||
            uiRoot.gameObject.scene.name != TargetSceneName)
        {
            return;
        }

        optionsPanel = FindChild(uiRoot, OptionsPanelName)?.gameObject;
        toggleButton = FindChild(uiRoot, ToggleButtonName)?.GetComponent<Button>();
        rainButton = FindChild(uiRoot, RainButtonName)?.GetComponent<Button>();
        snowButton = FindChild(uiRoot, SnowButtonName)?.GetComponent<Button>();
        clearButton = FindChild(uiRoot, ClearButtonName)?.GetComponent<Button>();

        toggleLabel = FindButtonLabel(toggleButton);
        rainLabel = FindButtonLabel(rainButton);
        snowLabel = FindButtonLabel(snowButton);
        clearLabel = FindButtonLabel(clearButton);

        BindButton(toggleButton, ToggleOptionsPanel);
        BindButton(rainButton, () => ApplyWeather(WorldWeather.Rain));
        BindButton(snowButton, () => ApplyWeather(WorldWeather.Snow));
        BindButton(clearButton, () => ApplyWeather(WorldWeather.Clear));
    }

    void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    void ToggleOptionsPanel()
    {
        ApplyPanelState(!panelVisible);
    }

    void ApplyPanelState(bool visible)
    {
        panelVisible = visible;

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(panelVisible);
        }
    }

    void ApplyWeather(WorldWeather weather)
    {
        WeatherSystem weatherSystem = WeatherSystem.Instance;
        if (weatherSystem != null)
        {
            weatherSystem.SetManualWeather(weather);
        }

        ApplyPanelState(false);
    }

    void ApplyLocalizedTexts()
    {
        SetText(
            toggleLabel,
            UiText.Get("weatherUi", "toggleTitle", "Weather"));
        SetText(
            rainLabel,
            UiText.Get("weatherUi", "rain", "Heavy Rain"));
        SetText(
            snowLabel,
            UiText.Get("weatherUi", "snow", "Snow"));
        SetText(
            clearLabel,
            UiText.Get("weatherUi", "clear", "Clear Sky"));
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    static TMP_Text FindButtonLabel(Button button)
    {
        if (button == null)
        {
            return null;
        }

        return button.GetComponentInChildren<TMP_Text>(true);
    }

    static Transform FindRootInLoadedScenes(string objectName)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindChildRecursive(roots[i].transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    static Transform FindChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        return FindChildRecursive(root, childName);
    }

    static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
