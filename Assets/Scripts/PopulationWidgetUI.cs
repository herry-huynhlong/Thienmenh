using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PopulationWidgetUI : MonoBehaviour
{
    [SerializeField] TMP_Text totalText;
    [SerializeField] TMP_Text commonerCountText;
    [SerializeField] TMP_Text cultivatorCountText;
    [SerializeField] TMP_Text monsterCountText;
    [SerializeField] GameObject detailPanel;
    [SerializeField] Button toggleButton;
    [SerializeField] RectTransform arrowIcon;
    [SerializeField] string totalPrefix = "Sinh linh: ";
    [SerializeField] float refreshInterval = 0.5f;
    [SerializeField] bool startExpanded;

    float nextRefreshTime;
    bool isExpanded;

    void Awake()
    {
        AutoResolveReferences();
        BindToggleButton();
        SetExpanded(startExpanded, true);
        RefreshCounts();
    }

    void OnEnable()
    {
        AutoResolveReferences();
        BindToggleButton();
        nextRefreshTime = 0f;
        RefreshCounts();
    }

    void OnDestroy()
    {
        UnbindToggleButton();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime =
            Time.unscaledTime +
            Mathf.Max(0.1f, refreshInterval);

        RefreshCounts();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        refreshInterval = Mathf.Max(0.1f, refreshInterval);

        if (!Application.isPlaying)
        {
            AutoResolveReferences();
        }
    }
#endif

    public void ToggleDetailPanel()
    {
        SetExpanded(!isExpanded);
    }

    public void ShowDetailPanel()
    {
        SetExpanded(true);
    }

    public void HideDetailPanel()
    {
        SetExpanded(false);
    }

    void RefreshCounts()
    {
        AutoResolveReferences();

        int commonerCount = CountCommoners();
        int cultivatorCount = CountCultivators();
        int monsterCount = CountMonsters();
        int totalCount =
            commonerCount +
            cultivatorCount +
            monsterCount;

        SetText(
            totalText,
            UiText.Format("population", "totalFormat", totalCount));
        SetText(commonerCountText, commonerCount.ToString());
        SetText(cultivatorCountText, cultivatorCount.ToString());
        SetText(monsterCountText, monsterCount.ToString());
    }

    int CountCommoners()
    {
        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(
                FindObjectsInactive.Include);

        int count = 0;

        foreach (VillagerAI villager in villagers)
        {
            if (villager == null ||
                !IsCountableEntity(villager) ||
                villager.IsDead ||
                villager.GetComponent<SmartNpcAI>() != null)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    int CountCultivators()
    {
        SmartNpcAI[] smartNpcs =
            FindObjectsByType<SmartNpcAI>(
                FindObjectsInactive.Include);

        int count = 0;

        foreach (SmartNpcAI smartNpc in smartNpcs)
        {
            if (smartNpc == null ||
                !IsCountableEntity(smartNpc) ||
                smartNpc.IsDead)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    int CountMonsters()
    {
        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(
                FindObjectsInactive.Include);

        int count = 0;

        foreach (MonsterAI monster in monsters)
        {
            if (monster == null ||
                !IsCountableEntity(monster) ||
                monster.IsDead)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    bool IsCountableEntity(Component entity)
    {
        if (entity == null ||
            !entity.gameObject.activeInHierarchy)
        {
            return false;
        }

        Scene scene = entity.gameObject.scene;
        return scene.IsValid() &&
            scene.isLoaded;
    }

    void BindToggleButton()
    {
        if (toggleButton == null)
        {
            return;
        }

        toggleButton.onClick.RemoveListener(ToggleDetailPanel);
        toggleButton.onClick.AddListener(ToggleDetailPanel);
    }

    void UnbindToggleButton()
    {
        if (toggleButton == null)
        {
            return;
        }

        toggleButton.onClick.RemoveListener(ToggleDetailPanel);
    }

    void SetExpanded(bool expanded, bool force = false)
    {
        if (!force &&
            isExpanded == expanded)
        {
            return;
        }

        isExpanded = expanded;

        if (detailPanel != null)
        {
            detailPanel.SetActive(expanded);
        }

        if (arrowIcon != null)
        {
            arrowIcon.localEulerAngles =
                expanded
                    ? Vector3.zero
                    : new Vector3(0f, 0f, -90f);
        }
    }

    void AutoResolveReferences()
    {
        if (totalText == null)
        {
            totalText = FindText(transform, "Text_Total");
        }

        if (commonerCountText == null)
        {
            commonerCountText =
                FindRowCountText("DanLang");
        }

        if (cultivatorCountText == null)
        {
            cultivatorCountText =
                FindRowCountText("TuSi");
        }

        if (monsterCountText == null)
        {
            monsterCountText =
                FindRowCountText("YeuThu");
        }

        if (detailPanel == null)
        {
            Transform detailTransform =
                FindChildRecursive(transform, "DetailPanel");
            detailPanel = detailTransform != null
                ? detailTransform.gameObject
                : null;
        }

        if (toggleButton == null)
        {
            toggleButton =
                GetComponentInChildren<Button>(true);
        }

        if (arrowIcon == null)
        {
            Transform arrowTransform =
                FindChildRecursive(transform, "Icon_Arrow");
            arrowIcon = arrowTransform as RectTransform;
        }
    }

    TMP_Text FindRowCountText(string rowName)
    {
        Transform row =
            FindChildRecursive(transform, rowName);
        if (row == null)
        {
            return null;
        }

        Transform countTransform =
            FindChildRecursive(row, "Text_Count");

        return countTransform != null
            ? countTransform.GetComponent<TMP_Text>()
            : null;
    }

    TMP_Text FindText(Transform root, string objectName)
    {
        Transform target =
            FindChildRecursive(root, objectName);
        return target != null
            ? target.GetComponent<TMP_Text>()
            : null;
    }

    Transform FindChildRecursive(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found =
                FindChildRecursive(child, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    void SetText(TMP_Text text, string value)
    {
        if (text == null)
        {
            return;
        }

        if (text.text == value)
        {
            return;
        }

        text.text = value;
    }
}
