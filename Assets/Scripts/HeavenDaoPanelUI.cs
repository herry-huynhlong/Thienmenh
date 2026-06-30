using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HeavenDaoPanelUI : MonoBehaviour
{
    static bool installedSceneHook;

    [Header("Panel")]
    public GameObject panel;
    public Button openButton;
    public Button closeButton;

    [Header("Overview")]
    public TMP_Text originValueText;
    public Image originFillImage;
    public RectTransform originFillRect;
    public TMP_Text controlText;
    public TMP_Text karmaText;

    [Header("Powers")]
    public TMP_Text[] powerRows;
    public TMP_Text nextUnlockText;

    [Header("Recent")]
    public TMP_Text[] recentRows;
    public TMP_Text footerHintText;

    HeavenDaoSystem system;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallForLoadedScene()
    {
        TryInstallOnButton();

        if (!installedSceneHook)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            installedSceneHook = true;
        }
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallOnButton();
    }

    static void TryInstallOnButton()
    {
        GameObject buttonObject = GameObject.Find("Btn_BanNguyen");
        if (buttonObject == null ||
            buttonObject.GetComponent<HeavenDaoPanelUI>() != null)
        {
            return;
        }

        buttonObject.AddComponent<HeavenDaoPanelUI>();
    }

    void Awake()
    {
        AutoBind();
        BindButtons();
    }

    void OnEnable()
    {
        system = HeavenDaoSystem.Instance;
        if (system != null)
        {
            system.OnChanged += Refresh;
        }

        LocalizationSettings.LanguageChanged += HandleLanguageChanged;

        Refresh();
    }

    void OnDisable()
    {
        if (system != null)
        {
            system.OnChanged -= Refresh;
        }

        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
    }

    public void TogglePanel()
    {
        SetPanelVisible(panel == null || !panel.activeSelf);
    }

    public void OpenPanel()
    {
        SetPanelVisible(true);
    }

    public void ClosePanel()
    {
        SetPanelVisible(false);
    }

    void SetPanelVisible(bool visible)
    {
        if (panel != null)
        {
            panel.SetActive(visible);
        }

        if (visible)
        {
            Refresh();
        }
    }

    void Refresh()
    {
        if (system == null)
        {
            system = HeavenDaoSystem.Instance;
        }

        if (system == null)
        {
            return;
        }

        bool devBypassActive =
            system.bypassUnlockRequirements &&
            !system.HasForcedLocks;

        int nextRequired = Mathf.Max(1, system.GetNextRequiredOrigin());
        float fill = devBypassActive
            ? 1f
            : Mathf.Clamp01(system.origin / (float)nextRequired);

        if (originValueText != null)
        {
            originValueText.text = system.origin + "/" + nextRequired;
        }

        SetProgressFill(fill);

        ApplyOverviewTexts();
        RefreshPowers();
        RefreshRecentLogs();
    }

    void RefreshPowers()
    {
        if (system == null || powerRows == null)
        {
            return;
        }

        for (int i = 0; i < powerRows.Length; i++)
        {
            TMP_Text row = powerRows[i];
            if (row == null)
            {
                continue;
            }

            row.richText = true;

            if (i >= system.unlocks.Length || system.unlocks[i] == null)
            {
                row.text = "";
                continue;
            }

            HeavenDaoUnlock unlock = system.unlocks[i];
            bool unlocked = system.HasPower(unlock.power);
            row.text =
                (unlocked
                    ? "<color=#39E66D>V</color> "
                    : "<color=#FF4D4D>X</color> ") +
                unlock.displayName;
            row.color = unlocked
                ? new Color(0.9f, 1f, 0.9f)
                : new Color(0.72f, 0.72f, 0.72f);
        }

        HeavenDaoUnlock next = system.GetNextUnlock();
        if (nextUnlockText != null)
        {
        }
        ApplyNextUnlockText(next);
    }

    void RefreshRecentLogs()
    {
        if (system == null || recentRows == null)
        {
            return;
        }

        int visibleCount = Mathf.Min(recentRows.Length, 5);
        for (int i = 0; i < recentRows.Length; i++)
        {
            TMP_Text row = recentRows[i];
            if (row == null)
            {
                continue;
            }

            row.text = i < visibleCount && i < system.RecentLogs.Count
                ? system.RecentLogs[i]
                : "";
        }

        ApplyFooterHintText();
    }

    void SetProgressFill(float fill)
    {
        if (originFillImage != null)
        {
            if (originFillImage.type == Image.Type.Filled)
            {
                originFillImage.fillAmount = fill;
            }
            else if (originFillRect == null)
            {
                originFillRect = originFillImage.rectTransform;
            }
        }

        if (originFillRect != null)
        {
            Vector2 anchorMax = originFillRect.anchorMax;
            anchorMax.x = fill;
            originFillRect.anchorMax = anchorMax;
        }
    }

    void ApplyOverviewTexts()
    {
        if (system == null)
        {
            return;
        }

        if (controlText != null)
        {
            controlText.text = UiText.Format(
                "heavenDao",
                "controlFormat",
                system.ControlPercent);
        }

        if (karmaText != null)
        {
            karmaText.text = UiText.Format(
                "heavenDao",
                "karmaFormat",
                system.karma);
        }
    }

    void ApplyNextUnlockText(HeavenDaoUnlock next)
    {
        if (nextUnlockText == null)
        {
            return;
        }

        if (system != null &&
            system.bypassUnlockRequirements &&
            !system.HasForcedLocks)
        {
            nextUnlockText.text =
                UiText.Get(
                    "heavenDao",
                    "devUnlockAll");
            return;
        }

        nextUnlockText.text = next != null
            ? UiText.Format(
                "heavenDao",
                "nextUnlockFormat",
                next.controlPercent,
                next.displayName)
            : UiText.Get(
                "heavenDao",
                "allUnlocked");
    }

    void ApplyFooterHintText()
    {
        if (footerHintText == null)
        {
            return;
        }

        footerHintText.text =
            UiText.Get(
                "heavenDao",
                "footerHint");
    }

    void HandleLanguageChanged()
    {
        Refresh();
    }

    void AutoBind()
    {
        if (panel == null)
        {
            panel = FindObject("Panel_ThienDaoControl");
        }

        if (openButton == null)
        {
            openButton = FindButton("Btn_BanNguyen");
        }

        if (closeButton == null)
        {
            closeButton = FindButton("CloseButton");
        }

        originValueText = originValueText != null
            ? originValueText
            : FindText("Text_BanNguyenValue");
        controlText = controlText != null
            ? controlText
            : FindText("Text_ChuongKhong");
        karmaText = karmaText != null
            ? karmaText
            : FindText("Text_NhanQua");
        nextUnlockText = nextUnlockText != null
            ? nextUnlockText
            : FindText("Text_NextUnlock");
        footerHintText = footerHintText != null
            ? footerHintText
            : FindText("Text_FooterHint");

        if (originFillRect == null)
        {
            GameObject fillObject = FindObject("ProgressBar_Fill");
            if (fillObject != null)
            {
                originFillRect = fillObject.GetComponent<RectTransform>();
                originFillImage = fillObject.GetComponent<Image>();
            }
        }

        if (powerRows == null || powerRows.Length == 0)
        {
            powerRows = new TMP_Text[]
            {
                FindText("PowerRow_01"),
                FindText("PowerRow_02"),
                FindText("PowerRow_03"),
                FindText("PowerRow_04"),
                FindText("PowerRow_05"),
                FindText("PowerRow_06")
            };
        }

        if (recentRows == null || recentRows.Length == 0)
        {
            recentRows = new TMP_Text[]
            {
                FindText("LogRow_01"),
                FindText("LogRow_02"),
                FindText("LogRow_03"),
                FindText("LogRow_04"),
                FindText("LogRow_05")
            };
        }
    }

    void BindButtons()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(TogglePanel);
            openButton.onClick.AddListener(TogglePanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    TMP_Text FindText(string objectName)
    {
        GameObject target = FindObject(objectName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    Button FindButton(string objectName)
    {
        GameObject target = FindObject(objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    GameObject FindObject(string objectName)
    {
        Transform root = panel != null
            ? panel.transform.root
            : transform.root;

        Transform found = FindChildRecursive(root, objectName);
        if (found != null)
        {
            return found.gameObject;
        }

        GameObject global = GameObject.Find(objectName);
        if (global != null)
        {
            return global;
        }

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null ||
                candidate.name != objectName ||
                !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            return candidate.gameObject;
        }

        return null;
    }

    Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
