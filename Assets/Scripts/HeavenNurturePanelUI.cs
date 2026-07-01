using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HeavenNurturePanelUI : MonoBehaviour
{
    const string TargetDatabaseResourceName = "HeavenNurtureTargetDatabase";

    static bool installedSceneHook;

    [Header("Root")]
    [SerializeField] GameObject heavenPanel;
    [SerializeField] Button toggleButton;
    [SerializeField] Button closeButton;

    [Header("Static Labels")]
    [SerializeField] TMP_Text panelTitleText;
    [SerializeField] TMP_Text listTitleText;
    [SerializeField] TMP_Text listHeaderText;
    [SerializeField] TMP_Text countText;
    [SerializeField] TMP_Text nameLabelText;
    [SerializeField] TMP_Text titleLabelText;
    [SerializeField] TMP_Text realmLabelText;
    [SerializeField] TMP_Text originLabelText;
    [SerializeField] TMP_Text aptitudeLabelText;
    [SerializeField] TMP_Text fearLabelText;
    [SerializeField] TMP_Text fateLabelText;
    [SerializeField] TMP_Text descriptionLabelText;

    [Header("Filter")]
    [SerializeField] Button allButton;
    [SerializeField] Button cultivatorButton;
    [SerializeField] Button monsterButton;
    [SerializeField] TMP_InputField searchInput;

    [Header("List")]
    [SerializeField] Transform contentRoot;
    [SerializeField] HeavenNurtureListItemUI itemPrefab;

    [Header("Detail")]
    [SerializeField] GameObject rightDetailPanel;
    [SerializeField] Image portraitImage;
    [SerializeField] TMP_Text detailNameText;
    [SerializeField] TMP_Text detailTypeText;
    [SerializeField] TMP_Text detailTitleText;
    [SerializeField] TMP_Text detailRealmText;
    [SerializeField] TMP_Text detailOriginText;
    [SerializeField] TMP_Text detailAptitudeText;
    [SerializeField] TMP_Text detailFearText;
    [SerializeField] TMP_Text detailFateText;
    [SerializeField] TMP_Text detailDescriptionText;

    [Header("Actions")]
    [SerializeField] Button giveFateButton;
    [SerializeField] Button removeButton;

    [Header("Options")]
    [SerializeField] bool hideOnStart = true;

    readonly List<HeavenNurtureTargetData> allTargets =
        new List<HeavenNurtureTargetData>();
    readonly List<HeavenNurtureTargetData> visibleTargets =
        new List<HeavenNurtureTargetData>();
    readonly List<HeavenNurtureListItemUI> spawnedItems =
        new List<HeavenNurtureListItemUI>();
    readonly HashSet<string> dismissedJsonOnlyTargetIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    HeavenTargetType? currentFilter;
    HeavenNurtureTargetData selectedTarget;
    HeavenNurtureListItemUI selectedItem;
    NpcFavoriteManager favoriteManager;
    HeavenNurtureTargetData[] jsonTargets = Array.Empty<HeavenNurtureTargetData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallForLoadedScene()
    {
        TryInstallOnPanel();

        if (!installedSceneHook)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            installedSceneHook = true;
        }
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallOnPanel();
    }

    static void TryInstallOnPanel()
    {
        GameObject panelObject = FindSceneObject("HeavenNurturePanel");
        if (panelObject == null)
        {
            return;
        }

        if (panelObject.GetComponent<HeavenNurturePanelUI>() == null)
        {
            panelObject.AddComponent<HeavenNurturePanelUI>();
        }
    }

    void Awake()
    {
        AutoResolveReferences();
        favoriteManager = NpcFavoriteManager.EnsureInstance();
        BindButtons();
        BindSearchInput();
        ApplyStaticTexts();
        ClearDetail();

        if (itemPrefab != null && itemPrefab.gameObject.activeSelf)
        {
            itemPrefab.gameObject.SetActive(false);
        }

        if (hideOnStart && heavenPanel != null)
        {
            heavenPanel.SetActive(false);
        }
    }

    void OnEnable()
    {
        AutoResolveReferences();
        favoriteManager = NpcFavoriteManager.EnsureInstance();
        if (favoriteManager != null)
        {
            favoriteManager.OnFavoritesChanged += HandleFavoritesChanged;
        }

        LocalizationSettings.LanguageChanged += HandleLanguageChanged;
        LoadJsonTargets();
        ApplyStaticTexts();

        if (heavenPanel == null || heavenPanel.activeInHierarchy)
        {
            RefreshList();
        }
    }

    void OnDisable()
    {
        if (favoriteManager != null)
        {
            favoriteManager.OnFavoritesChanged -= HandleFavoritesChanged;
        }

        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
    }

    public void TogglePanel()
    {
        if (heavenPanel == null)
        {
            return;
        }

        if (heavenPanel.activeSelf)
        {
            HidePanel();
        }
        else
        {
            ShowPanel();
        }
    }

    public void ShowPanel()
    {
        if (heavenPanel != null)
        {
            heavenPanel.SetActive(true);
        }

        RefreshList();
    }

    public void HidePanel()
    {
        if (heavenPanel != null)
        {
            heavenPanel.SetActive(false);
        }
    }

    public void RefreshList()
    {
        LoadJsonTargets();
        BuildTargetList();
        RebuildVisibleTargets();
        RebuildItemViews();
        RefreshCountText();
        RefreshSelectionAfterListChanged();
    }

    public void SetFilter(HeavenTargetType? filter)
    {
        currentFilter = filter;
        RebuildVisibleTargets();
        RebuildItemViews();
        RefreshCountText();
        RefreshSelectionAfterListChanged();
    }

    public void OnSearchChanged(string value)
    {
        RebuildVisibleTargets();
        RebuildItemViews();
        RefreshCountText();
        RefreshSelectionAfterListChanged();
    }

    public void SelectTarget(
        HeavenNurtureTargetData target,
        HeavenNurtureListItemUI item)
    {
        selectedTarget = target;
        selectedItem = item;

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            HeavenNurtureListItemUI row = spawnedItems[i];
            if (row != null)
            {
                row.SetSelected(row == selectedItem);
            }
        }

        UpdateDetail(target);
    }

    public void UpdateDetail(HeavenNurtureTargetData target)
    {
        if (target == null)
        {
            SetRightDetailVisible(true);
            ClearDetail(false);
            return;
        }

        SetRightDetailVisible(true);

        if (portraitImage != null)
        {
            portraitImage.sprite = target.portrait;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        SetText(
            detailNameText,
            UiText.Get("heavenNurture", "labelName") + " " +
            target.displayName);
        SetText(
            detailTitleText,
            UiText.Get("heavenNurture", "labelTitle") + " " +
            (string.IsNullOrWhiteSpace(target.title)
                ? UiText.Get("heavenNurture", "titleNone")
                : target.title));
        SetText(
            detailRealmText,
            UiText.Get("heavenNurture", "labelRealm") + " " + target.realm);
        SetText(
            detailOriginText,
            UiText.Get("heavenNurture", "labelOrigin") + " " +
            (string.IsNullOrWhiteSpace(target.origin)
                ? UiText.Get("heavenNurture", "originNone")
                : target.origin));
        SetText(
            detailAptitudeText,
            UiText.Get("heavenNurture", "labelAptitude") + ": " +
            UiText.Format("heavenNurture", "aptitudeFormat", target.aptitude));
        SetText(
            detailFearText,
            UiText.Get("heavenNurture", "labelFear") + ": " +
            target.fear.ToString(CultureInfo.InvariantCulture));
        SetText(
            detailFateText,
            UiText.Get("heavenNurture", "labelFate") + ": " +
            target.fateState);
        SetText(
            detailDescriptionText,
            UiText.Get("heavenNurture", "labelDescription") + ": " +
            (string.IsNullOrWhiteSpace(target.description)
                ? UiText.Get("heavenNurture", "descriptionNone")
                : target.description));
        SetText(detailTypeText, string.Empty);

        SetButtonsInteractable(true);
    }

    public void ClearDetail()
    {
        ClearDetail(false);
    }

    void ClearDetail(bool showEmptyState)
    {
        if (showEmptyState)
        {
            SetRightDetailVisible(true);
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.enabled = false;
        }

        string emptyText = UiText.Get("heavenNurture", "detailEmpty");
        string emptyStateTitle = UiText.Get(
            "heavenNurture",
            "emptyStateTitle",
            "Chưa có thiên mệnh chi tử");
        string emptyStateDescription = UiText.Get(
            "heavenNurture",
            "emptyStateDescription",
            "Hãy ban cơ duyên cho một sinh linh phù hợp để xuất hiện tại đây.");

        SetText(
            detailNameText,
            showEmptyState
                ? emptyStateTitle
                : emptyText);
        SetText(detailTypeText, emptyText);
        SetText(detailTitleText, emptyText);
        SetText(detailRealmText, emptyText);
        SetText(detailOriginText, emptyText);
        SetText(detailAptitudeText, emptyText);
        SetText(detailFearText, emptyText);
        SetText(detailFateText, emptyText);
        SetText(
            detailDescriptionText,
            showEmptyState
                ? emptyStateDescription
                : UiText.Get("heavenNurture", "descriptionNone"));

        SetButtonsInteractable(false);
    }

    public void GiveFateToSelectedTarget()
    {
        if (selectedTarget == null)
        {
            return;
        }

        selectedTarget.hasReceivedFate = true;
        selectedTarget.fateState = UiText.Get(
            "heavenNurture",
            "stateReceived",
            "Da nhan");
        selectedTarget.fear = Mathf.Clamp(
            selectedTarget.fear + 5,
            0,
            100);

        TryApplyRuntimeFateEffects(selectedTarget);
        RefreshList();
        RestoreSelection(selectedTarget.id, selectedTarget.displayName);
    }

    public void RemoveSelectedTarget()
    {
        if (selectedTarget == null)
        {
            return;
        }

        if (selectedTarget.favoriteComponent != null &&
            favoriteManager != null)
        {
            favoriteManager.RemoveFavorite(selectedTarget.favoriteComponent);
        }
        else if (!string.IsNullOrEmpty(selectedTarget.id))
        {
            dismissedJsonOnlyTargetIds.Add(
                NormalizeForLookup(selectedTarget.id));
            RefreshList();
        }
        else
        {
            dismissedJsonOnlyTargetIds.Add(
                NormalizeForLookup(selectedTarget.displayName));
            RefreshList();
        }
    }

    void BindButtons()
    {
        AutoResolveReferences();
        BindButton(closeButton, HidePanel);
        BindButton(allButton, () => SetFilter(null));
        BindButton(cultivatorButton, () => SetFilter(HeavenTargetType.Cultivator));
        BindButton(monsterButton, () => SetFilter(HeavenTargetType.Monster));
        BindButton(giveFateButton, GiveFateToSelectedTarget);
        BindButton(removeButton, RemoveSelectedTarget);
    }

    void BindSearchInput()
    {
        if (searchInput == null)
        {
            return;
        }

        searchInput.onValueChanged.RemoveListener(OnSearchChanged);
        searchInput.onValueChanged.AddListener(OnSearchChanged);
    }

    void AutoResolveReferences()
    {
        if (heavenPanel == null)
        {
            GameObject panelObject = FindSceneObject("HeavenNurturePanel");
            if (panelObject != null)
            {
                heavenPanel = panelObject;
            }
        }

        Transform root = heavenPanel != null
            ? heavenPanel.transform.root
            : transform.root;

        if (toggleButton == null)
        {
            toggleButton = FindButton(root, "HeavenToggleButton");
        }

        if (closeButton == null)
        {
            closeButton = FindButton(root, "CloseButton");
        }

        if (allButton == null)
        {
            allButton = FindButtonByNormalizedName(root, "tat ca");
        }

        if (cultivatorButton == null)
        {
            cultivatorButton = FindButtonByNormalizedName(root, "tu si");
        }

        if (monsterButton == null)
        {
            monsterButton = FindButtonByNormalizedName(root, "yeu thu");
        }

        if (searchInput == null)
        {
            searchInput = FindComponentInChildren<TMP_InputField>(
                root,
                "SearchBox");
        }

        if (contentRoot == null)
        {
            Transform scrollView = FindChildRecursive(root, "NPCScrollView");
            Transform viewport = FindChildRecursive(scrollView, "Viewport");
            Transform content = FindChildRecursive(viewport, "Content");
            if (content != null)
            {
                contentRoot = content;
            }
        }

        if (itemPrefab == null)
        {
            Transform itemTransform = FindChildRecursive(root, "NPCListItem");
            if (itemTransform != null)
            {
                itemPrefab =
                    itemTransform.GetComponent<HeavenNurtureListItemUI>();

                if (itemPrefab == null)
                {
                    itemPrefab =
                        itemTransform.gameObject.AddComponent<HeavenNurtureListItemUI>();
                }
            }
        }

        Transform panelRoot = heavenPanel != null
            ? heavenPanel.transform
            : root;

        if (panelTitleText == null)
        {
            panelTitleText =
                FindComponentInChildren<TMP_Text>(panelRoot, "Header");
        }

        if (listTitleText == null)
        {
            listTitleText =
                FindComponentInChildren<TMP_Text>(panelRoot, "ListTitle");
        }

        if (listHeaderText == null)
        {
            listHeaderText =
                FindComponentInChildren<TMP_Text>(panelRoot, "danhsach");
        }

        if (rightDetailPanel == null)
        {
            Transform detailPanelTransform =
                FindChildRecursive(panelRoot, "RightDetailPanel");
            if (detailPanelTransform != null)
            {
                rightDetailPanel = detailPanelTransform.gameObject;
            }
        }

        if (portraitImage == null)
        {
            portraitImage = FindComponentInChildren<Image>(root, "PortraitSlot");
        }

        Transform detailRoot =
            rightDetailPanel != null
                ? rightDetailPanel.transform
                : panelRoot;

        if (detailNameText == null)
        {
            detailNameText =
                FindDetailText(detailRoot, "Tên:");
        }

        if (detailTitleText == null)
        {
            detailTitleText =
                FindDetailText(detailRoot, "Danh Hiệu:");
        }

        if (detailTypeText == null)
        {
            detailTypeText =
                FindComponentInChildren<TMP_Text>(
                    detailRoot,
                    "DetailTypeText");
        }

        if (detailRealmText == null)
        {
            detailRealmText =
                FindDetailText(detailRoot, "Cảnh Giới:");
        }

        if (detailOriginText == null)
        {
            detailOriginText =
                FindDetailText(detailRoot, "Xuất Thân:");
        }

        if (detailAptitudeText == null)
        {
            detailAptitudeText =
                FindDetailText(detailRoot, "Tư Chất");
        }

        if (detailFearText == null)
        {
            detailFearText =
                FindDetailText(detailRoot, "Kính Sợ");
        }

        if (detailFateText == null)
        {
            detailFateText =
                FindDetailText(detailRoot, "Cơ Duyên");
        }

        if (detailDescriptionText == null)
        {
            detailDescriptionText =
                FindDetailText(detailRoot, "Mô tả/Ghi chú");
        }

        if (giveFateButton == null)
        {
            giveFateButton = FindButton(root, "Ban Co Duyen");
        }

        if (removeButton == null)
        {
            removeButton = FindButtonByNormalizedName(root, "xoa");
        }
    }

    static GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject direct = GameObject.Find(objectName);
        if (direct != null)
        {
            return direct;
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

    static Button FindButton(Transform root, string objectName)
    {
        Transform found = FindChildRecursive(root, objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    static Button FindButtonByNormalizedName(
        Transform root,
        string normalizedName)
    {
        if (root == null || string.IsNullOrEmpty(normalizedName))
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (NormalizeForLookup(button.name) == normalizedName)
            {
                return button;
            }
        }

        return null;
    }

    static T FindComponentInChildren<T>(
        Transform root,
        string objectName)
        where T : Component
    {
        Transform found = FindChildRecursive(root, objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    static TMP_Text FindDetailText(Transform root, string expectedText)
    {
        if (root == null || string.IsNullOrWhiteSpace(expectedText))
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        string expectedRaw = expectedText.Trim();
        string expectedKey = NormalizeForLookup(expectedText);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (string.Equals(
                    text.text != null ? text.text.Trim() : string.Empty,
                    expectedRaw,
                    StringComparison.Ordinal))
            {
                return text;
            }
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (string.Equals(
                    text.gameObject.name != null
                        ? text.gameObject.name.Trim()
                        : string.Empty,
                    expectedRaw,
                    StringComparison.Ordinal))
            {
                return text;
            }
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            string currentKey = NormalizeForLookup(text.text);
            string nameKey = NormalizeForLookup(text.gameObject.name);
            if (currentKey == expectedKey || nameKey == expectedKey)
            {
                return text;
            }
        }

        return null;
    }

    static Transform FindChildRecursive(
        Transform root,
        string objectName)
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
            Transform child = root.GetChild(i);
            Transform found = FindChildRecursive(child, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    void HandleFavoritesChanged()
    {
        if (heavenPanel == null || heavenPanel.activeInHierarchy)
        {
            RefreshList();
        }
    }

    void HandleLanguageChanged()
    {
        jsonTargets = Array.Empty<HeavenNurtureTargetData>();
        ApplyStaticTexts();
        RefreshList();
    }

    void LoadJsonTargets()
    {
        if (jsonTargets != null && jsonTargets.Length > 0)
        {
            return;
        }

        jsonTargets = Array.Empty<HeavenNurtureTargetData>();

        TextAsset asset =
            LocalizationSettings.LoadTextAsset(TargetDatabaseResourceName);
        if (asset == null)
        {
            return;
        }

        HeavenNurtureTargetDatabase database =
            JsonUtility.FromJson<HeavenNurtureTargetDatabase>(asset.text);
        if (database == null || database.targets == null)
        {
            return;
        }

        jsonTargets = database.targets;
        for (int i = 0; i < jsonTargets.Length; i++)
        {
            HeavenNurtureTargetData data = jsonTargets[i];
            if (data == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(data.portraitResource))
            {
                data.portrait =
                    Resources.Load<Sprite>(data.portraitResource);
            }
        }
    }

    void BuildTargetList()
    {
        allTargets.Clear();

        Dictionary<string, HeavenNurtureTargetData> jsonById =
            new Dictionary<string, HeavenNurtureTargetData>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HeavenNurtureTargetData> jsonByName =
            new Dictionary<string, HeavenNurtureTargetData>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < jsonTargets.Length; i++)
        {
            HeavenNurtureTargetData data = jsonTargets[i];
            if (data == null)
            {
                continue;
            }

            string idKey = NormalizeForLookup(data.id);
            if (!string.IsNullOrEmpty(idKey) && !jsonById.ContainsKey(idKey))
            {
                jsonById.Add(idKey, data);
            }

            string nameKey = NormalizeForLookup(data.displayName);
            if (!string.IsNullOrEmpty(nameKey) && !jsonByName.ContainsKey(nameKey))
            {
                jsonByName.Add(nameKey, data);
            }
        }

        HashSet<HeavenNurtureTargetData> matchedJson =
            new HashSet<HeavenNurtureTargetData>();

        favoriteManager = NpcFavoriteManager.EnsureInstance();
        if (favoriteManager != null)
        {
            favoriteManager.PruneInvalidFavorites();
            IReadOnlyList<NpcFavorite> favorites = favoriteManager.Favorites;
            for (int i = 0; i < favorites.Count; i++)
            {
                NpcFavorite favorite = favorites[i];
                if (favorite == null)
                {
                    continue;
                }

                HeavenNurtureTargetData runtimeData =
                    BuildRuntimeTargetData(favorite);
                HeavenNurtureTargetData jsonData =
                    FindJsonOverride(runtimeData, jsonById, jsonByName);

                HeavenNurtureTargetData merged =
                    MergeRuntimeAndJson(runtimeData, jsonData);
                merged.favoriteComponent = favorite;
                merged.runtimeObject = favorite.gameObject;
                merged.fromJsonOnly = false;

                if (jsonData != null)
                {
                    matchedJson.Add(jsonData);
                }

                if (!IsDismissed(merged))
                {
                    allTargets.Add(merged);
                }
            }
        }

        // Runtime favorites are the source of truth for the list.
        // JSON only overrides metadata for matched runtime entries.
    }

    HeavenNurtureTargetData BuildRuntimeTargetData(NpcFavorite favorite)
    {
        GameObject target =
            favorite != null ? favorite.gameObject : null;
        HeavenNurtureTargetData data =
            new HeavenNurtureTargetData();

        data.favoriteComponent = favorite;
        data.runtimeObject = target;
        data.id = BuildRuntimeTargetId(favorite);
        data.displayName = ResolveRuntimeDisplayName(favorite);
        data.targetType = ResolveRuntimeTargetType(target);
        data.realm = ResolveRuntimeRealmText(favorite);
        data.aptitude = ResolveRuntimeAptitude(target);
        data.fear = ResolveRuntimeFear(favorite, target);
        data.title = ResolveRuntimeTitle(target);
        data.origin = ResolveRuntimeOrigin(target);
        data.hasReceivedFate = false;
        data.fateState = UiText.Get(
            "heavenNurture",
            "stateWatching",
            "Dang chu y");
        data.description = ResolveRuntimeDescription(data);
        data.portrait = ResolveRuntimePortrait(target);

        return data;
    }

    HeavenNurtureTargetData FindJsonOverride(
        HeavenNurtureTargetData runtimeData,
        Dictionary<string, HeavenNurtureTargetData> jsonById,
        Dictionary<string, HeavenNurtureTargetData> jsonByName)
    {
        if (runtimeData == null)
        {
            return null;
        }

        string idKey = NormalizeForLookup(runtimeData.id);
        if (!string.IsNullOrEmpty(idKey) &&
            jsonById.TryGetValue(idKey, out HeavenNurtureTargetData byId))
        {
            return byId;
        }

        string nameKey = NormalizeForLookup(runtimeData.displayName);
        if (!string.IsNullOrEmpty(nameKey) &&
            jsonByName.TryGetValue(nameKey, out HeavenNurtureTargetData byName))
        {
            return byName;
        }

        return null;
    }

    HeavenNurtureTargetData MergeRuntimeAndJson(
        HeavenNurtureTargetData runtimeData,
        HeavenNurtureTargetData jsonData)
    {
        HeavenNurtureTargetData merged =
            jsonData != null
                ? CloneTargetData(jsonData)
                : runtimeData != null
                    ? CloneTargetData(runtimeData)
                    : new HeavenNurtureTargetData();

        if (jsonData == null)
        {
            ApplyMissingFallbacks(merged);
            return merged;
        }

        if (runtimeData != null)
        {
            merged.favoriteComponent = runtimeData.favoriteComponent;
            merged.runtimeObject = runtimeData.runtimeObject;
        }

        merged.hasReceivedFate =
            jsonData.hasReceivedFate || merged.hasReceivedFate;

        if (jsonData.portrait != null)
        {
            merged.portrait = jsonData.portrait;
        }
        else if (!string.IsNullOrWhiteSpace(jsonData.portraitResource))
        {
            merged.portrait =
                Resources.Load<Sprite>(jsonData.portraitResource);
        }
        else if (runtimeData != null)
        {
            merged.portrait = runtimeData.portrait;
        }

        ApplyMissingFallbacks(merged);
        return merged;
    }

    void ApplyMissingFallbacks(HeavenNurtureTargetData data)
    {
        if (data == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(data.id))
        {
            data.id = NormalizeForLookup(data.displayName);
        }

        if (string.IsNullOrWhiteSpace(data.displayName))
        {
            data.displayName =
                UiText.Get("heavenNurture", "unknownName");
        }

        if (string.IsNullOrWhiteSpace(data.realm))
        {
            data.realm =
                UiText.Get("heavenNurture", "unknownRealm");
        }

        data.aptitude = Mathf.Clamp(data.aptitude, 0, 100);
        data.fear = Mathf.Max(0, data.fear);

        if (string.IsNullOrWhiteSpace(data.title))
        {
            data.title = UiText.Get("heavenNurture", "titleNone");
        }

        if (string.IsNullOrWhiteSpace(data.origin))
        {
            data.origin = data.targetType == HeavenTargetType.Monster
                ? UiText.Get("heavenNurture", "originMonster")
                : UiText.Get("heavenNurture", "originCultivator");
        }

        if (string.IsNullOrWhiteSpace(data.fateState))
        {
            data.fateState = data.hasReceivedFate
                ? UiText.Get("heavenNurture", "stateReceived")
                : UiText.Get("heavenNurture", "stateWatching");
        }

        if (string.IsNullOrWhiteSpace(data.description))
        {
            data.description = UiText.Format(
                "heavenNurture",
                "descriptionFallbackFormat",
                data.displayName,
                data.realm);
        }
    }

    void RebuildVisibleTargets()
    {
        visibleTargets.Clear();

        string query = searchInput != null ? searchInput.text : string.Empty;
        string normalizedQuery = NormalizeForLookup(query);

        for (int i = 0; i < allTargets.Count; i++)
        {
            HeavenNurtureTargetData data = allTargets[i];
            if (data == null)
            {
                continue;
            }

            if (currentFilter.HasValue &&
                data.targetType != currentFilter.Value)
            {
                continue;
            }

            if (!MatchesSearch(data, normalizedQuery))
            {
                continue;
            }

            visibleTargets.Add(data);
        }
    }

    void RebuildItemViews()
    {
        ClearSpawnedItems();
        HideTemplateItem();

        if (contentRoot == null || itemPrefab == null)
        {
            return;
        }

        for (int i = 0; i < visibleTargets.Count; i++)
        {
            HeavenNurtureTargetData data = visibleTargets[i];
            HeavenNurtureListItemUI item =
                Instantiate(itemPrefab, contentRoot);

            item.gameObject.SetActive(true);
            item.Setup(data, SelectTarget);
            spawnedItems.Add(item);
        }
    }

    void RefreshCountText()
    {
        if (countText == null)
        {
            return;
        }

        countText.text = UiText.Format(
            "heavenNurture",
            "countFormat",
            visibleTargets.Count,
            allTargets.Count);
    }

    void RefreshSelectionAfterListChanged()
    {
        if (visibleTargets.Count == 0)
        {
            selectedTarget = null;
            selectedItem = null;
            ClearDetail(true);
            return;
        }

        if (selectedTarget != null)
        {
            RestoreSelection(selectedTarget.id, selectedTarget.displayName);
            if (selectedTarget != null)
            {
                return;
            }
        }

        selectedTarget = null;
        selectedItem = null;
        SetRightDetailVisible(false);
        ClearDetail(false);
    }

    void RestoreSelection(string id, string displayName)
    {
        string idKey = NormalizeForLookup(id);
        string nameKey = NormalizeForLookup(displayName);

        for (int i = 0; i < visibleTargets.Count; i++)
        {
            HeavenNurtureTargetData data = visibleTargets[i];
            bool idMatch =
                !string.IsNullOrEmpty(idKey) &&
                NormalizeForLookup(data.id) == idKey;
            bool nameMatch =
                !string.IsNullOrEmpty(nameKey) &&
                NormalizeForLookup(data.displayName) == nameKey;

            if (!idMatch && !nameMatch)
            {
                continue;
            }

            HeavenNurtureListItemUI item =
                i < spawnedItems.Count ? spawnedItems[i] : null;
            if (item != null)
            {
                SelectTarget(data, item);
                return;
            }
        }

        selectedTarget = null;
        selectedItem = null;
        SelectFirstVisibleItem();
    }

    void SelectFirstVisibleItem()
    {
        if (visibleTargets.Count == 0 || spawnedItems.Count == 0)
        {
            selectedTarget = null;
            selectedItem = null;
            ClearDetail(true);
            return;
        }

        selectedTarget = null;
        selectedItem = null;
        SetRightDetailVisible(false);
        ClearDetail(false);
    }

    void ApplyStaticTexts()
    {
        SetText(
            panelTitleText,
            UiText.Get("heavenNurture", "panelTitle"));
        SetText(
            listTitleText,
            UiText.Get("heavenNurture", "listTitle"));
        SetText(
            listHeaderText,
            UiText.Get("heavenNurture", "listHeader"));
        SetText(
            nameLabelText,
            UiText.Get("heavenNurture", "labelName"));
        SetText(
            titleLabelText,
            UiText.Get("heavenNurture", "labelTitle"));
        SetText(
            realmLabelText,
            UiText.Get("heavenNurture", "labelRealm"));
        SetText(
            originLabelText,
            UiText.Get("heavenNurture", "labelOrigin"));
        SetText(
            aptitudeLabelText,
            UiText.Get("heavenNurture", "labelAptitude"));
        SetText(
            fearLabelText,
            UiText.Get("heavenNurture", "labelFear"));
        SetText(
            fateLabelText,
            UiText.Get("heavenNurture", "labelFate"));
        SetText(
            descriptionLabelText,
            UiText.Get("heavenNurture", "labelDescription"));

        SetButtonText(
            allButton,
            UiText.Get("heavenNurture", "filterAll"));
        SetButtonText(
            cultivatorButton,
            UiText.Get("heavenNurture", "filterCultivator"));
        SetButtonText(
            monsterButton,
            UiText.Get("heavenNurture", "filterMonster"));
        SetButtonText(
            giveFateButton,
            UiText.Get("heavenNurture", "buttonGiveFate"));
        SetButtonText(
            removeButton,
            UiText.Get("heavenNurture", "buttonRemove"));

        if (searchInput != null &&
            searchInput.placeholder is TMP_Text placeholder)
        {
            placeholder.text = UiText.Get(
                "heavenNurture",
                "searchPlaceholder",
                "Tim ten / canh gioi...");
        }
    }

    string BuildDetailedStatsText(HeavenNurtureTargetData target)
    {
        if (target == null)
        {
            return UiText.Get("heavenNurture", "descriptionNone");
        }

        string title = string.IsNullOrWhiteSpace(target.title)
            ? UiText.Get("heavenNurture", "titleNone")
            : target.title;
        string realm = string.IsNullOrWhiteSpace(target.realm)
            ? UiText.Get("heavenNurture", "unknownRealm")
            : target.realm;
        string origin = string.IsNullOrWhiteSpace(target.origin)
            ? UiText.Get("heavenNurture", "originNone")
            : target.origin;
        string aptitude = UiText.Format(
            "heavenNurture",
            "aptitudeFormat",
            target.aptitude);
        string fear = target.fear.ToString(CultureInfo.InvariantCulture);
        string fate = string.IsNullOrWhiteSpace(target.fateState)
            ? UiText.Get("heavenNurture", "detailEmpty")
            : target.fateState;
        string description = string.IsNullOrWhiteSpace(target.description)
            ? UiText.Get("heavenNurture", "descriptionNone")
            : target.description;

        StringBuilder builder = new StringBuilder(256);
        builder.AppendLine(UiText.Get("heavenNurture", "labelName") + " " + target.displayName);
        builder.AppendLine(UiText.Get("heavenNurture", "labelTitle") + " " + title);
        builder.AppendLine(UiText.Get("heavenNurture", "labelRealm") + " " + realm);
        builder.AppendLine(UiText.Get("heavenNurture", "labelOrigin") + " " + origin);
        builder.AppendLine(UiText.Get("heavenNurture", "labelAptitude") + ": " + aptitude);
        builder.AppendLine(UiText.Get("heavenNurture", "labelFear") + ": " + fear);
        builder.AppendLine(UiText.Get("heavenNurture", "labelFate") + ": " + fate);
        builder.AppendLine(UiText.Get("heavenNurture", "labelDescription") + ":");
        builder.Append(description);
        return builder.ToString().TrimEnd();
    }

    string GetTargetTypeText(HeavenNurtureTargetData target)
    {
        if (target == null)
        {
            return UiText.Get("heavenNurture", "detailEmpty");
        }

        return target.targetType == HeavenTargetType.Monster
            ? UiText.Get("heavenNurture", "filterMonster")
            : UiText.Get("heavenNurture", "filterCultivator");
    }

    void TryApplyRuntimeFateEffects(HeavenNurtureTargetData target)
    {
        if (target == null || target.runtimeObject == null)
        {
            return;
        }

        EntityProfile profile =
            target.runtimeObject.GetComponent<EntityProfile>();
        if (profile != null)
        {
            profile.emotion.fear =
                Mathf.Clamp(profile.emotion.fear + 5f, 0f, 100f);
        }

        MonsterAI monster =
            target.runtimeObject.GetComponent<MonsterAI>();
        if (monster != null)
        {
            monster.fear = Mathf.Clamp(monster.fear + 5f, 0f, 100f);
        }
    }

    HeavenTargetType ResolveRuntimeTargetType(GameObject target)
    {
        return target != null &&
            target.GetComponent<MonsterAI>() != null
            ? HeavenTargetType.Monster
            : HeavenTargetType.Cultivator;
    }

    string BuildRuntimeTargetId(NpcFavorite favorite)
    {
        if (favorite == null)
        {
            return string.Empty;
        }

        string npcId = ResolveRuntimeNpcId(favorite.gameObject);
        if (!string.IsNullOrWhiteSpace(npcId))
        {
            return NormalizeForLookup(npcId);
        }

        string explicitName = favorite.npcDisplayName;
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return NormalizeForLookup(explicitName);
        }

        return NormalizeForLookup(favorite.GetDisplayName());
    }

    string ResolveRuntimeNpcId(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        NPCIdentity identity =
            target.GetComponent<NPCIdentity>() ??
            target.GetComponentInParent<NPCIdentity>(true) ??
            target.GetComponentInChildren<NPCIdentity>(true);

        if (identity == null)
        {
            return string.Empty;
        }

        return identity.npcId;
    }

    string ResolveRuntimeDisplayName(NpcFavorite favorite)
    {
        if (favorite == null)
        {
            return UiText.Get("heavenNurture", "unknownName");
        }

        return favorite.GetDisplayName();
    }

    string ResolveRuntimeRealmText(NpcFavorite favorite)
    {
        if (favorite == null)
        {
            return UiText.Get("heavenNurture", "unknownRealm");
        }

        GameObject target = favorite.gameObject;
        if (target == null)
        {
            return favorite.GetRealmText();
        }

        CharacterStats stats = target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return stats.GetRealmText();
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return monster.GetRealmText();
        }

        SmartNpcAI smartNpc = target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return NpcText.RealmWithStage(
                smartNpc.realm,
                smartNpc.realmStage);
        }

        VillagerAI villager = target.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.GetRealmText();
        }

        return favorite.GetRealmText();
    }

    int ResolveRuntimeAptitude(GameObject target)
    {
        if (target == null)
        {
            return 0;
        }

        SmartNpcAI smartNpc = target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return Mathf.Clamp(smartNpc.comprehension, 0, 100);
        }

        EntityProfile profile = target.GetComponent<EntityProfile>();
        if (profile != null)
        {
            return Mathf.Clamp(profile.talent.comprehension, 0, 100);
        }

        return 0;
    }

    int ResolveRuntimeFear(NpcFavorite favorite, GameObject target)
    {
        if (favorite != null)
        {
            return favorite.GetHeavenFavorFear();
        }

        if (target == null)
        {
            return 0;
        }

        EntityProfile profile = target.GetComponent<EntityProfile>();
        if (profile != null)
        {
            return Mathf.Clamp(
                Mathf.RoundToInt(profile.emotion.fear),
                0,
                100);
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return Mathf.Clamp(
                Mathf.RoundToInt(monster.fear),
                0,
                100);
        }

        return 0;
    }

    string ResolveRuntimeTitle(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        EntityProfile profile = target.GetComponent<EntityProfile>();
        if (profile == null)
        {
            return string.Empty;
        }

        switch (profile.talent.grade)
        {
            case TalentGrade.ChildOfHeaven:
                return UiText.Get("heavenNurture", "titleChildOfHeaven");
            case TalentGrade.SaintBody:
                return UiText.Get("heavenNurture", "titleSaintBody");
            case TalentGrade.SwordHeart:
                return UiText.Get("heavenNurture", "titleSwordHeart");
            default:
                return string.Empty;
        }
    }

    string ResolveRuntimeOrigin(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        if (target.GetComponent<MonsterAI>() != null)
        {
            return UiText.Get("heavenNurture", "originMonster");
        }

        if (NpcRoleUtility.IsCommoner(target))
        {
            return UiText.Get("heavenNurture", "originCommoner");
        }

        return UiText.Get("heavenNurture", "originCultivator");
    }

    string ResolveRuntimeDescription(HeavenNurtureTargetData data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        return UiText.Format(
            "heavenNurture",
            "descriptionFallbackFormat",
            data.displayName,
            data.realm);
    }

    Sprite ResolveRuntimePortrait(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        SpriteRenderer spriteRenderer =
            target.GetComponentInChildren<SpriteRenderer>();
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    HeavenNurtureTargetData CloneTargetData(HeavenNurtureTargetData source)
    {
        HeavenNurtureTargetData clone =
            new HeavenNurtureTargetData();

        if (source == null)
        {
            return clone;
        }

        clone.id = source.id;
        clone.displayName = source.displayName;
        clone.portraitResource = source.portraitResource;
        clone.targetType = source.targetType;
        clone.realm = source.realm;
        clone.aptitude = source.aptitude;
        clone.fear = source.fear;
        clone.title = source.title;
        clone.origin = source.origin;
        clone.fateState = source.fateState;
        clone.description = source.description;
        clone.hasReceivedFate = source.hasReceivedFate;
        clone.portrait = source.portrait;
        clone.runtimeObject = source.runtimeObject;
        clone.favoriteComponent = source.favoriteComponent;
        clone.fromJsonOnly = source.fromJsonOnly;
        return clone;
    }

    bool MatchesSearch(
        HeavenNurtureTargetData data,
        string normalizedQuery)
    {
        if (data == null || string.IsNullOrEmpty(normalizedQuery))
        {
            return true;
        }

        string combined =
            NormalizeForLookup(data.displayName) + " " +
            NormalizeForLookup(data.realm) + " " +
            NormalizeForLookup(data.fateState) + " " +
            NormalizeForLookup(data.title);

        return combined.Contains(normalizedQuery);
    }

    bool IsDismissed(HeavenNurtureTargetData data)
    {
        if (data == null)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(data.id) &&
            dismissedJsonOnlyTargetIds.Contains(
                NormalizeForLookup(data.id)))
        {
            return true;
        }

        string fallbackKey = NormalizeForLookup(data.displayName);
        return !string.IsNullOrEmpty(fallbackKey) &&
            dismissedJsonOnlyTargetIds.Contains(fallbackKey);
    }

    void ClearSpawnedItems()
    {
        if (contentRoot != null)
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                HeavenNurtureListItemUI item =
                    child.GetComponent<HeavenNurtureListItemUI>();
                if (item == null)
                {
                    continue;
                }

                if (itemPrefab != null &&
                    child == itemPrefab.transform)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            HeavenNurtureListItemUI item = spawnedItems[i];
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }

        spawnedItems.Clear();
    }

    void HideTemplateItem()
    {
        if (itemPrefab == null)
        {
            return;
        }

        if (itemPrefab.gameObject.activeSelf)
        {
            itemPrefab.gameObject.SetActive(false);
        }
    }

    void SetRightDetailVisible(bool visible)
    {
        if (rightDetailPanel == null)
        {
            return;
        }

        if (!rightDetailPanel.activeSelf)
        {
            rightDetailPanel.SetActive(true);
        }
    }

    void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    void SetButtonText(Button button, string value)
    {
        if (button == null || string.IsNullOrEmpty(value))
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = value;
        }
    }

    void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    void SetButtonsInteractable(bool value)
    {
        if (giveFateButton != null)
        {
            giveFateButton.interactable = value;
        }

        if (removeButton != null)
        {
            removeButton.interactable = value;
        }
    }

    static string NormalizeForLookup(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized =
            value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder =
            new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(c);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c == '\u0111')
            {
                builder.Append('d');
                continue;
            }

            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
