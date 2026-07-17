using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SpiritQiPanelUI : MonoBehaviour
{
    const string PanelName = "SpiritQiPanel";
    const string UnlimitedValue = "--";

    static bool installedSceneHook;

    [Header("Panel")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Button closeButton;
    [SerializeField] Button upgradeButton;
    [SerializeField] Button addButton;

    [Header("Overview")]
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text labelText;
    [SerializeField] TMP_Text currentQiText;
    [SerializeField] TMP_Text maxQiText;
    [SerializeField] TMP_Text cultivationMultiplierText;

    [Header("Upgrade")]
    [SerializeField] TMP_Text upgradeTitleText;
    [SerializeField] TMP_Text upgradeDescriptionText;
    [SerializeField] TMP_Text currentLevelText;
    [SerializeField] TMP_Text nextLevelText;
    [SerializeField] TMP_Text arrowText;
    [SerializeField] TMP_Text costLabelText;
    [SerializeField] TMP_Text costValueText;
    [SerializeField] TMP_Text upgradeButtonText;

    [Header("Resource")]
    [SerializeField] TMP_Text resourceLabelText;
    [SerializeField] TMP_Text resourceValueText;

    HeavenDaoSystem heavenDaoSystem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallForLoadedScene()
    {
        TryInstall();

        if (installedSceneHook)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        installedSceneHook = true;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall();
    }

    static void TryInstall()
    {
        GameObject panelObject = FindPanelObject();
        if (panelObject == null)
        {
            return;
        }

        if (panelObject.GetComponent<SpiritQiPanelUI>() == null)
        {
            panelObject.AddComponent<SpiritQiPanelUI>();
        }

        Transform qiIconTransform = FindDescendant(panelObject.transform, "QiIcon");
        if (qiIconTransform != null &&
            qiIconTransform.GetComponent<SpiritQiIconAnimator>() == null)
        {
            qiIconTransform.gameObject.AddComponent<SpiritQiIconAnimator>();
        }
    }

    void Awake()
    {
        AutoBind();
        BindButtons();
    }

    void OnEnable()
    {
        heavenDaoSystem = HeavenDaoSystem.Instance;
        SubscribeEvents();
        RefreshAll();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    void SubscribeEvents()
    {
        if (heavenDaoSystem != null)
        {
            heavenDaoSystem.OnChanged -= HandleHeavenDaoChanged;
            heavenDaoSystem.OnChanged += HandleHeavenDaoChanged;
            heavenDaoSystem.WorldSpiritQiChanged -= HandleWorldSpiritQiChanged;
            heavenDaoSystem.WorldSpiritQiChanged += HandleWorldSpiritQiChanged;
        }

        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
        LocalizationSettings.LanguageChanged += HandleLanguageChanged;
    }

    void UnsubscribeEvents()
    {
        if (heavenDaoSystem != null)
        {
            heavenDaoSystem.OnChanged -= HandleHeavenDaoChanged;
            heavenDaoSystem.WorldSpiritQiChanged -= HandleWorldSpiritQiChanged;
        }

        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
    }

    void HandleHeavenDaoChanged()
    {
        RefreshAll();
    }

    void HandleWorldSpiritQiChanged(int currentQi)
    {
        RefreshAll();
    }

    void HandleLanguageChanged()
    {
        RefreshAll();
    }

    void RefreshAll()
    {
        heavenDaoSystem = HeavenDaoSystem.Instance;
        RefreshStaticTexts();
        RefreshDynamicValues();
        RefreshUpgradeState();
    }

    void RefreshStaticTexts()
    {
        SetText(titleText, UiText.Get("spiritQiPanel", "title", "Spirit Qi"));
        SetText(labelText, UiText.Get("spiritQiPanel", "qiLabel", "World Spirit Qi"));
        SetText(
            upgradeTitleText,
            UiText.Get("spiritQiPanel", "upgradeTitle", "Upgrade Spirit Qi"));
        SetText(
            upgradeDescriptionText,
            UiText.Get(
                "spiritQiPanel",
                "upgradeDescription",
                "Increase world spirit qi by 50 using Origin."));
        SetText(costLabelText, UiText.Get("spiritQiPanel", "costLabel", "Cost"));
        SetText(
            upgradeButtonText,
            UiText.Get("spiritQiPanel", "upgradeButton", "Upgrade"));
        SetText(
            resourceLabelText,
            UiText.Get("spiritQiPanel", "resourceLabel", "Origin"));
        SetText(
            arrowText,
            UiText.Get("spiritQiPanel", "upgradeArrow", "->"));
    }

    void RefreshDynamicValues()
    {
        int currentQi =
            heavenDaoSystem != null
                ? heavenDaoSystem.CurrentWorldSpiritQi
                : HeavenDaoSystem.WorldSpiritQiBaseValue;
        int nextQi = currentQi + HeavenDaoSystem.WorldSpiritQiUpgradeStep;
        int currentOrigin =
            heavenDaoSystem != null
                ? heavenDaoSystem.CurrentOrigin
                : 0;
        float multiplier =
            heavenDaoSystem != null
                ? heavenDaoSystem.GetWorldSpiritQiMultiplier()
                : 1f;

        SetText(currentQiText, currentQi.ToString());
        SetText(maxQiText, UnlimitedValue);
        SetText(currentLevelText, currentQi.ToString());
        SetText(nextLevelText, nextQi.ToString());
        SetText(costValueText, HeavenDaoSystem.WorldSpiritQiUpgradeCost.ToString());
        SetText(resourceValueText, currentOrigin.ToString());

        string multiplierLabel =
            UiText.Get(
                "spiritQiPanel",
                "cultivationMultiplierLabel",
                "Cultivation Multiplier");
        SetText(
            cultivationMultiplierText,
            string.Format("{0}: x{1:0.0#}", multiplierLabel, multiplier));
    }

    void RefreshUpgradeState()
    {
        if (upgradeButton == null)
        {
            return;
        }

        bool hasSystem = heavenDaoSystem != null;
        bool unlocked =
            hasSystem &&
            heavenDaoSystem.HasPower(HeavenDaoPower.GatherAreaSpiritualQi);
        bool enoughOrigin =
            hasSystem &&
            heavenDaoSystem.CurrentOrigin >= HeavenDaoSystem.WorldSpiritQiUpgradeCost;

        upgradeButton.interactable = unlocked && enoughOrigin;
    }

    void BindButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(HandleUpgradeClicked);
        }

        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.interactable = false;
        }
    }

    void HandleUpgradeClicked()
    {
        if (heavenDaoSystem == null)
        {
            return;
        }

        if (!heavenDaoSystem.HasPower(HeavenDaoPower.GatherAreaSpiritualQi))
        {
            if (WorldScreenNotificationHub.Instance != null)
            {
                WorldScreenNotificationHub.ShowNormal(
                    UiText.Get(
                        "spiritQiPanel",
                        "upgradeLockedMessage",
                        "Unlock Regional Spirit Gathering first."));
            }

            RefreshUpgradeState();
            return;
        }

        if (!heavenDaoSystem.TryUpgradeWorldSpiritQi())
        {
            if (WorldScreenNotificationHub.Instance != null)
            {
                WorldScreenNotificationHub.ShowNormal(
                    UiText.Get(
                        "spiritQiPanel",
                        "notEnoughOriginMessage",
                        "Not enough Origin."));
            }

            RefreshUpgradeState();
            return;
        }

        RefreshAll();
    }

    public void OpenPanel()
    {
        SetPanelVisible(true);
    }

    public void ClosePanel()
    {
        SetPanelVisible(false);
    }

    public void TogglePanel()
    {
        GameObject target = panelRoot != null ? panelRoot : gameObject;
        SetPanelVisible(target == null || !target.activeSelf);
    }

    void SetPanelVisible(bool visible)
    {
        GameObject target = panelRoot != null ? panelRoot : gameObject;
        if (target == null)
        {
            return;
        }

        target.SetActive(visible);
        if (visible)
        {
            RefreshAll();
        }
    }

    void AutoBind()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        closeButton = closeButton != null
            ? closeButton
            : FindButton("Header/CloseButton");
        upgradeButton = upgradeButton != null
            ? upgradeButton
            : FindButton("UpgradePanel/UpgradeButton");
        addButton = addButton != null
            ? addButton
            : FindButton("ResourcePanel/AddButton");

        titleText = titleText != null
            ? titleText
            : FindText("Header/TitleText");
        labelText = labelText != null
            ? labelText
            : FindText("QiOverviewPanel/QiMainInfo/LabelText");
        currentQiText = currentQiText != null
            ? currentQiText
            : FindText("QiOverviewPanel/QiMainInfo/QiValueRow/CurrentQiText");
        maxQiText = maxQiText != null
            ? maxQiText
            : FindText("QiOverviewPanel/QiMainInfo/QiValueRow/MaxQiText");
        cultivationMultiplierText = cultivationMultiplierText != null
            ? cultivationMultiplierText
            : FindText("QiOverviewPanel/QiMainInfo/CultivationMultiplierText");

        upgradeTitleText = upgradeTitleText != null
            ? upgradeTitleText
            : FindText("UpgradePanel/UpgradeInfo/UpgradeTitleText");
        upgradeDescriptionText = upgradeDescriptionText != null
            ? upgradeDescriptionText
            : FindText("UpgradePanel/UpgradeInfo/UpgradeDescriptionText");
        currentLevelText = currentLevelText != null
            ? currentLevelText
            : FindText("UpgradePanel/UpgradeInfo/UpgradeValueRow/CurrentLevelText");
        nextLevelText = nextLevelText != null
            ? nextLevelText
            : FindText("UpgradePanel/UpgradeInfo/UpgradeValueRow/NextLevelText");
        arrowText = arrowText != null
            ? arrowText
            : FindText("UpgradePanel/UpgradeInfo/UpgradeValueRow/ArrowText");
        costLabelText = costLabelText != null
            ? costLabelText
            : FindText("UpgradePanel/CostPanel/CostLabelText");
        costValueText = costValueText != null
            ? costValueText
            : FindText("UpgradePanel/CostPanel/CostValueText");
        upgradeButtonText = upgradeButtonText != null
            ? upgradeButtonText
            : FindButtonLabel("UpgradePanel/UpgradeButton");

        resourceLabelText = resourceLabelText != null
            ? resourceLabelText
            : FindText("ResourcePanel/ResourceLabelText");
        resourceValueText = resourceValueText != null
            ? resourceValueText
            : FindText("ResourcePanel/ResourceValueText");
    }

    Button FindButton(string path)
    {
        Transform target = FindTransform(path);
        return target != null ? target.GetComponent<Button>() : null;
    }

    TMP_Text FindText(string path)
    {
        Transform target = FindTransform(path);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    TMP_Text FindButtonLabel(string buttonPath)
    {
        Transform buttonTransform = FindTransform(buttonPath);
        if (buttonTransform == null)
        {
            return null;
        }

        Transform explicitText = buttonTransform.Find("ButtonText");
        if (explicitText != null)
        {
            TMP_Text explicitLabel = explicitText.GetComponent<TMP_Text>();
            if (explicitLabel != null)
            {
                return explicitLabel;
            }
        }

        return buttonTransform.GetComponentInChildren<TMP_Text>(true);
    }

    Transform FindTransform(string path)
    {
        if (panelRoot == null)
        {
            return null;
        }

        Transform root = panelRoot.transform;
        Transform direct = root.Find(path);
        if (direct != null)
        {
            return direct;
        }

        string[] parts = path.Split('/');
        if (parts.Length == 0)
        {
            return null;
        }

        return FindDescendant(root, parts[parts.Length - 1]);
    }

    static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform candidate = descendants[i];
            if (candidate != null && candidate.name == name)
            {
                return candidate;
            }
        }

        return null;
    }

    static GameObject FindPanelObject()
    {
        GameObject activeObject = GameObject.Find(PanelName);
        if (activeObject != null)
        {
            return activeObject;
        }

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null ||
                candidate.name != PanelName ||
                candidate.hideFlags != HideFlags.None ||
                !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            return candidate.gameObject;
        }

        return null;
    }

    static void SetText(TMP_Text textComponent, string value)
    {
        if (textComponent != null)
        {
            textComponent.text = value;
        }
    }
}
