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
    [SerializeField] Image portraitImage;
    [SerializeField] TMP_Text detailNameText;
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
            ClearDetail();
            return;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = target.portrait;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        SetText(detailNameText, target.displayName);
        SetText(
            detailTitleText,
            string.IsNullOrWhiteSpace(target.title)
                ? UiText.Get("heavenNurture", "titleNone")
                : target.title);
        SetText(detailRealmText, target.realm);
        SetText(
            detailOriginText,
            string.IsNullOrWhiteSpace(target.origin)
                ? UiText.Get("heavenNurture", "originNone")
                : target.origin);
        SetText(
            detailAptitudeText,
            UiText.Format("heavenNurture", "aptitudeFormat", target.aptitude));
        SetText(
            detailFearText,
            UiText.Format("heavenNurture", "fearFormat", target.fear));
        SetText(detailFateText, target.fateState);
        SetText(
            detailDescriptionText,
            string.IsNullOrWhiteSpace(target.description)
                ? UiText.Get("heavenNurture", "descriptionNone")
                : target.description);

        SetButtonsInteractable(true);
    }

    public void ClearDetail()
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.enabled = false;
        }

        string emptyText = UiText.Get("heavenNurture", "detailEmpty");

        SetText(detailNameText, emptyText);
        SetText(detailTitleText, emptyText);
        SetText(detailRealmText, emptyText);
        SetText(detailOriginText, emptyText);
        SetText(detailAptitudeText, emptyText);
        SetText(detailFearText, emptyText);
        SetText(detailFateText, emptyText);
        SetText(
            detailDescriptionText,
            UiText.Get("heavenNurture", "descriptionNone"));

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
                itemPrefab = itemTransform.GetComponent<HeavenNurtureListItemUI>();
            }
        }

        if (portraitImage == null)
        {
            portraitImage = FindComponentInChildren<Image>(root, "PortraitSlot");
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

        for (int i = 0; i < jsonTargets.Length; i++)
        {
            HeavenNurtureTargetData jsonData = jsonTargets[i];
            if (jsonData == null || matchedJson.Contains(jsonData))
            {
                continue;
            }

            HeavenNurtureTargetData copy = CloneTargetData(jsonData);
            copy.fromJsonOnly = true;
            ApplyMissingFallbacks(copy);

            if (!IsDismissed(copy))
            {
                allTargets.Add(copy);
            }
        }

        if (allTargets.Count == 0)
        {
            HeavenNurtureTargetData sample =
                BuildSampleTarget();
            if (!IsDismissed(sample))
            {
                allTargets.Add(sample);
            }
        }
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
        data.fear = ResolveRuntimeFear(target);
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
            runtimeData != null
                ? CloneTargetData(runtimeData)
                : new HeavenNurtureTargetData();

        if (jsonData == null)
        {
            ApplyMissingFallbacks(merged);
            return merged;
        }

        if (!string.IsNullOrWhiteSpace(jsonData.id))
        {
            merged.id = jsonData.id;
        }

        if (!string.IsNullOrWhiteSpace(jsonData.displayName))
        {
            merged.displayName = jsonData.displayName;
        }

        if (!string.IsNullOrWhiteSpace(jsonData.realm))
        {
            merged.realm = jsonData.realm;
        }

        if (jsonData.aptitude > 0)
        {
            merged.aptitude = jsonData.aptitude;
        }

        if (jsonData.fear > 0)
        {
            merged.fear = jsonData.fear;
        }

        if (!string.IsNullOrWhiteSpace(jsonData.title))
        {
            merged.title = jsonData.title;
        }

        if (!string.IsNullOrWhiteSpace(jsonData.origin))
        {
            merged.origin = jsonData.origin;
        }

        if (!string.IsNullOrWhiteSpace(jsonData.fateState))
        {
            merged.fateState = jsonData.fateState;
        }

        if (!string.IsNullOrWhiteSpace(jsonData.description))
        {
            merged.description = jsonData.description;
        }

        merged.hasReceivedFate =
            jsonData.hasReceivedFate || merged.hasReceivedFate;
        merged.targetType = jsonData.targetType;

        if (jsonData.portrait != null)
        {
            merged.portrait = jsonData.portrait;
        }
        else if (!string.IsNullOrWhiteSpace(jsonData.portraitResource))
        {
            merged.portrait =
                Resources.Load<Sprite>(jsonData.portraitResource);
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
        data.fear = Mathf.Clamp(data.fear, 0, 100);

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

    HeavenNurtureTargetData BuildSampleTarget()
    {
        HeavenNurtureTargetData sample =
            new HeavenNurtureTargetData();
        sample.id = "nguyen-thanh";
        sample.displayName = UiText.Get("heavenNurture", "sampleName");
        sample.targetType = HeavenTargetType.Cultivator;
        sample.realm = UiText.Get("heavenNurture", "sampleRealm");
        sample.aptitude = 68;
        sample.fear = 43;
        sample.fateState = UiText.Get(
            "heavenNurture",
            "stateBlessed",
            "Duoc ban duyen");
        sample.title = UiText.Get("heavenNurture", "titleNone");
        sample.origin = UiText.Get("heavenNurture", "originCultivator");
        sample.description = UiText.Format(
            "heavenNurture",
            "descriptionFallbackFormat",
            sample.displayName,
            sample.realm);
        sample.fromJsonOnly = true;
        return sample;
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
            ClearDetail();
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

        SelectFirstVisibleItem();
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
            ClearDetail();
            return;
        }

        SelectTarget(visibleTargets[0], spawnedItems[0]);
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

        string explicitName = favorite.npcDisplayName;
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return NormalizeForLookup(explicitName);
        }

        return NormalizeForLookup(favorite.GetDisplayName());
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

    int ResolveRuntimeFear(GameObject target)
    {
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
