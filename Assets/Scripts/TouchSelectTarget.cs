using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System;

public class TouchSelectTarget : MonoBehaviour
{
    public static Transform CurrentTarget { get; private set; }

    [Header("Camera")]
    public MobileCameraController cameraController;

    [Header("UI")]
    public GameObject infoPanel;

    public GameObject infoContentRoot;

    public GameObject inventoryContentRoot;

    public TMP_Text infoText;

    public Image infoIcon;

    public NpcInventoryPanelUI npcInventoryPanel;

    public Button infoButton;

    public Button inventoryButton;

    [Header("Target Header")]
    public TMP_Text nameText;

    public TMP_Text realmText;

    public Image realmIcon;

    public Image npcIcon;

    public TMP_Text hpText;

    public Image hpFillImage;

    public TMP_Text expText;

    [Header("Target Detail Rows")]
    public TMP_Text damageValueText;

    public TMP_Text defenseValueText;

    public TMP_Text lifespanValueText;

    public TMP_Text jobValueText;

    public TMP_Text maritalStatusText;

    public TMP_Text statusText;

    public Transform equipmentListRoot;

    public GameObject equipmentRowTemplate;

    public Transform skillListRoot;

    public GameObject skillRowTemplate;

    [Header("World Item Panel")]
    public GameObject worldItemInfoPanel;

    public TMP_Text worldItemNameText;

    public TMP_Text worldItemInfoText;

    public Image worldItemPanelIcon;

    [Header("World Item Info")]
    public Vector2 worldItemIconSize =
        new Vector2(72f, 72f);

    public Vector2 worldItemIconOffset =
        new Vector2(16f, -16f);

    public float worldItemTextLeftPadding = 24f;

    [Header("Character Portrait")]
    public Vector2 characterPortraitSize =
        new Vector2(96f, 96f);

    public Vector2 characterPortraitOffset =
        new Vector2(16f, -42f);

    public float characterTextLeftPadding = 0f;

    public bool autoUseCharacterSprite;

    [Header("Panel Follow")]
    public Vector3 panelOffset =
        new Vector3(0, 2f, 0);

    public float panelScreenPadding = 12f;

    [Tooltip("If enabled, the target panel stays centered on screen instead of following the NPC position.")]
    public bool forcePanelToScreenCenter;

    [Tooltip("Extra manual offset applied when centering the panel.")]
    public Vector2 centeredPanelOffset;

    [Header("Tap")]
    public float tapThreshold = 10f;

    Camera cam;

    RectTransform panelRect;

    RectTransform worldItemPanelRect;

    Transform currentTarget;

    Vector3 pointerDownPosition;

    bool pointerStartedOverUI;

    bool pointerMoved;

    bool showingInventory;

    Vector4 originalInfoTextMargin;

    bool hasOriginalInfoTextMargin;

    bool splitTargetHeaderLayout;

    Vector3 originalInfoIconScale = Vector3.one;

    bool hasOriginalInfoIconScale;

    static Sprite phamNhanRealmIcon;
    static Sprite luyenKhiRealmIcon;
    static Sprite trucCoRealmIcon;
    static Sprite kimDanRealmIcon;
    static Sprite hoaThanRealmIcon;
    static Sprite doKiepRealmIcon;
    static Sprite npcPortraitDefaultIcon;
    static Sprite monsterPortraitDefaultIcon;

    static bool realmIconsLoaded;
    static bool portraitIconsLoaded;

    readonly List<GameObject> spawnedEquipmentRows =
        new List<GameObject>();

    readonly List<GameObject> spawnedSkillRows =
        new List<GameObject>();

    void Start()
    {
        cam = Camera.main;

        if (cameraController == null &&
            cam != null)
        {
            cameraController =
                cam.GetComponent<MobileCameraController>();

            if (cameraController == null)
            {
                cameraController =
                    FindAnyObjectByType<MobileCameraController>(
                        FindObjectsInactive.Include);
            }

            if (cameraController == null)
            {
                cameraController =
                    cam.gameObject.AddComponent<MobileCameraController>();
            }
        }

        if (infoPanel != null)
        {
            panelRect =
                infoPanel.GetComponent<RectTransform>();
        }

        AutoFindWorldItemPanelReferences();

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        if (worldItemInfoPanel != null)
        {
            worldItemInfoPanel.SetActive(false);
        }

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.Hide();
        }

        if (npcInventoryPanel == null)
        {
            npcInventoryPanel =
                FindAnyObjectByType<NpcInventoryPanelUI>(
                    FindObjectsInactive.Include);
        }

        BindTabButtons();
    }

    void OnEnable()
    {
        BindTabButtons();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            pointerDownPosition =
                Input.mousePosition;

            pointerStartedOverUI =
                IsPointerOverUI();

            pointerMoved = false;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 pointerDelta =
                Input.mousePosition -
                pointerDownPosition;

            if (!pointerStartedOverUI &&
                !pointerMoved &&
                pointerDelta.magnitude > tapThreshold)
            {
                pointerMoved = true;
                HidePanel();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            Vector3 pointerDelta =
                Input.mousePosition -
                pointerDownPosition;

            if (!pointerStartedOverUI &&
                !IsPointerOverUI() &&
                !pointerMoved &&
                pointerDelta.magnitude <= tapThreshold)
            {
                SelectTarget();
            }
        }

        UpdatePanelPosition();
    }

    void SelectTarget()
    {
        if (IsPointerOverUI())
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null)
        {
            Debug.Log("Kh\u00F4ng t\u00ECm th\u1EA5y Camera Main");
            return;
        }

        Vector2 worldPos =
            cam.ScreenToWorldPoint(
                Input.mousePosition);

        Transform selectedTarget =
            GetClosestSelectableTarget(
                Physics2D.OverlapPointAll(worldPos),
                worldPos);

        if (selectedTarget == null)
        {
            selectedTarget =
                GetClosestSelectableTarget(
                    Physics2D.OverlapCircleAll(
                        worldPos,
                        0.8f),
                    worldPos);
        }

        if (selectedTarget == null)
        {
            HidePanel();
            return;
        }

        currentTarget =
            selectedTarget;

        CurrentTarget =
            selectedTarget;

        if (cameraController != null)
        {
            cameraController.FollowImmediately(
                selectedTarget);
        }

        bool isWorldItem =
            IsWorldItemTarget(selectedTarget);

        if (infoPanel != null)
        {
            infoPanel.SetActive(!isWorldItem);
        }

        SetWorldItemPanelVisible(isWorldItem);

        if (isWorldItem)
        {
            ShowWorldItemInfo(selectedTarget);

            if (npcInventoryPanel != null)
            {
                npcInventoryPanel.Hide();
            }

            return;
        }

        if (npcInventoryPanel != null)
        {
            if (CanShowInventoryForTarget(selectedTarget))
            {
                npcInventoryPanel.Show(selectedTarget);
            }
            else
            {
                npcInventoryPanel.HideContentOnly();
            }
        }

        ShowInfoTab();
    }

    public void ShowInfoTab()
    {
        showingInventory = false;

        RefreshTargetInfo(currentTarget);

        SetInfoContentVisible(true);
        SetInventoryContentVisible(false);

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.SetContentVisible(false);
        }
    }

    public void ShowInventoryTab()
    {
        if (false &&
            IsNpcTarget(currentTarget) &&
            !HasHeavenDaoPower(HeavenDaoPower.ViewBasicNpcInfo))
        {
            showingInventory = false;

            SetInventoryContentVisible(false);
            SetInfoIconVisible(false);
            SetInfoContentVisible(true);

            string lockedMessage =
                NpcText.Get("dialogue", "heavenDaoBasicLocked");

            SetValueText(damageValueText, "-");
            SetValueText(defenseValueText, "-");
            SetValueText(lifespanValueText, "-");
            SetValueText(jobValueText, "-");
            SetValueText(statusText, lockedMessage);
            ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);
            ClearSpawnedRows(spawnedSkillRows, skillListRoot);

            if (infoText != null &&
                !HasTargetDetailPanelLayout())
            {
                infoText.text = lockedMessage;
            }

            if (npcInventoryPanel != null)
            {
                npcInventoryPanel.HideContentOnly();
            }

            return;
        }

        showingInventory = true;

        SetInfoContentVisible(false);
        SetInfoIconVisible(false);
        SetInventoryContentVisible(true);

        if (npcInventoryPanel != null &&
            currentTarget != null)
        {
            npcInventoryPanel.Show(currentTarget);
            npcInventoryPanel.SetContentVisible(true);
        }
    }

    void HideTabContents()
    {
        showingInventory = false;

        SetInfoContentVisible(false);
        SetInfoIconVisible(false);
        SetInventoryContentVisible(false);

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.SetContentVisible(false);
        }
    }

    void BindTabButtons()
    {
        AutoFindTabReferences();

        if (infoButton != null)
        {
            infoButton.onClick.RemoveListener(ShowInfoTab);

            if (!HasPersistentListener(infoButton, nameof(ShowInfoTab)))
            {
                infoButton.onClick.AddListener(ShowInfoTab);
            }

            infoButton.interactable = true;
        }

        if (inventoryButton != null)
        {
            inventoryButton.onClick.RemoveListener(ShowInventoryTab);

            if (!HasPersistentListener(inventoryButton, nameof(ShowInventoryTab)))
            {
                inventoryButton.onClick.AddListener(ShowInventoryTab);
            }

            inventoryButton.interactable = true;
        }
    }

    bool HasPersistentListener(Button button, string methodName)
    {
        if (button == null)
        {
            return false;
        }

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }

        return false;
    }

    void RefreshTargetInfo(Transform target)
    {
        if (target == null)
        {
            return;
        }

        AutoFindTabReferences();

        UpdateTargetHeader(target);
        UpdateInfoIcon(target);
        UpdateNpcIcon(target);

        bool hasDetailPanel =
            UpdateTargetDetailPanel(target);

        if (infoText != null)
        {
            infoText.gameObject.SetActive(!hasDetailPanel);
            infoText.text =
                hasDetailPanel
                ? ""
                : BuildTargetInfo(target);
        }
    }

    void AutoFindTabReferences()
    {
        AutoFindWorldItemPanelReferences();

        if (infoPanel == null)
        {
            return;
        }

        if (infoContentRoot == null)
        {
            Transform infoContent =
                FindChildByName(infoPanel.transform, "InfoContent");

            if (infoContent != null)
            {
                infoContentRoot = infoContent.gameObject;
            }
        }

        if (inventoryContentRoot == null)
        {
            Transform inventoryContent =
                FindChildByName(infoPanel.transform, "InventoryContent");

            if (inventoryContent != null)
            {
                inventoryContentRoot = inventoryContent.gameObject;
            }
        }

        if (infoText == null)
        {
            Transform infoContentText =
                infoContentRoot != null
                ? FindChildByName(infoContentRoot.transform, "InfoText")
                : null;

            if (infoContentText != null)
            {
                infoText = infoContentText.GetComponent<TMP_Text>();
            }
        }

        if (infoText != null)
        {
            infoText.raycastTarget = false;
        }

        if (infoButton == null)
        {
            infoButton =
                FindButtonByName(
                    infoPanel.transform,
                    "InfoButton",
                    "Thong Tin",
                    "Th\u00F4ng Tin");
        }

        if (nameText == null)
        {
            Transform nameTransform =
                infoContentRoot != null
                ? FindChildByName(infoContentRoot.transform, "NameText")
                : FindChildByName(infoPanel.transform, "NameText");

            if (nameTransform != null)
            {
                nameText = nameTransform.GetComponent<TMP_Text>();
            }
        }

        if (nameText != null)
        {
            nameText.raycastTarget = false;
        }

        if (realmText == null)
        {
            Transform realmTransform =
                infoContentRoot != null
                ? FindChildByName(infoContentRoot.transform, "RealmText")
                : FindChildByName(infoPanel.transform, "RealmText");

            if (realmTransform != null)
            {
                realmText = realmTransform.GetComponent<TMP_Text>();
            }
        }

        if (realmText != null)
        {
            realmText.raycastTarget = false;
        }

        Transform detailRoot =
            infoContentRoot != null
            ? infoContentRoot.transform
            : infoPanel.transform;

        Transform lowerInfoRoot =
            FindChildByName(
                detailRoot,
                "LowerInfoContent");

        Transform equipmentPanelRoot =
            lowerInfoRoot != null
            ? FindChildByName(
                lowerInfoRoot,
                "EquipmentPanel")
            : null;

        Transform skillPanelRoot =
            lowerInfoRoot != null
            ? FindChildByName(
                lowerInfoRoot,
                "SkillPanel")
            : null;

        if (realmIcon == null)
        {
            Transform realmIconTransform =
                FindChildByName(detailRoot, "canhgioi");

            if (realmIconTransform == null)
            {
                realmIconTransform =
                    FindChildByName(detailRoot, "RealmIcon");
            }

            if (realmIconTransform == null)
            {
                realmIconTransform =
                    FindChildByName(detailRoot, "RealmIconImage");
            }

            if (realmIconTransform != null)
            {
                realmIcon = realmIconTransform.GetComponent<Image>();
            }
        }

        if (realmIcon != null)
        {
            realmIcon.raycastTarget = false;
        }

        if (npcIcon == null)
        {
            Transform npcIconTransform =
                FindChildByName(detailRoot, "npcicon");

            if (npcIconTransform != null)
            {
                npcIcon = npcIconTransform.GetComponent<Image>();
            }
        }

        if (npcIcon != null)
        {
            npcIcon.raycastTarget = false;
        }

        if (hpText == null)
        {
            Transform hpTransform =
                FindChildByName(infoPanel.transform, "HpText");

            if (hpTransform != null)
            {
                hpText = hpTransform.GetComponent<TMP_Text>();
            }
        }

        if (hpText != null)
        {
            hpText.raycastTarget = false;
        }

        if (expText == null)
        {
            Transform expTransform =
                FindChildByName(detailRoot, "ExpText");

            if (expTransform == null)
            {
                expTransform =
                    FindChildByName(detailRoot, "ExperienceText");
            }

            if (expTransform == null)
            {
                expTransform =
                    FindChildByName(detailRoot, "ExpValueText");
            }

            if (expTransform == null)
            {
                expTransform =
                    FindChildByName(detailRoot, "CultivationExpText");
            }

            if (expTransform != null)
            {
                expText = expTransform.GetComponent<TMP_Text>();
            }
        }

        if (expText != null)
        {
            expText.raycastTarget = false;
        }

        if (inventoryButton == null)
        {
            inventoryButton =
                FindButtonByName(
                    infoPanel.transform,
                    "InventoryButton",
                    "Kho");
        }

        if (infoIcon == null)
        {
            Transform iconTransform =
                FindChildByName(infoPanel.transform, "FactionIcon");

            if (iconTransform == null)
            {
                iconTransform =
                    FindChildByName(infoPanel.transform, "InfoIcon");
            }

            if (iconTransform == null)
            {
                iconTransform =
                    FindChildByName(infoPanel.transform, "ItemIcon");
            }

            if (iconTransform == null)
            {
                iconTransform =
                    FindChildByName(infoPanel.transform, "IconImage");
            }

            if (iconTransform == null)
            {
                iconTransform =
                    FindChildByName(infoPanel.transform, "DetailIcon");
            }

            if (iconTransform != null)
            {
                infoIcon = iconTransform.GetComponent<Image>();
            }
        }

        if (hpFillImage == null)
        {
            Transform targetHealthBar =
                FindChildByName(infoPanel.transform, "TargetHealthBar");

            Transform fillTransform =
                targetHealthBar != null
                ? FindChildByName(targetHealthBar, "Fill")
                : FindChildByName(infoPanel.transform, "Fill");

            if (fillTransform != null)
            {
                hpFillImage = fillTransform.GetComponent<Image>();
            }
        }

        if (damageValueText == null)
        {
            damageValueText =
                FindRowValueText(
                    detailRoot,
                    UiText.Get("touchSelect", "damageRow"));
        }

        if (defenseValueText == null)
        {
            defenseValueText =
                FindRowValueText(
                    detailRoot,
                    UiText.Get("touchSelect", "defenseRow"));
        }

        if (lifespanValueText == null)
        {
            lifespanValueText =
                FindRowValueText(
                    detailRoot,
                    UiText.Get("touchSelect", "lifespanRow"));
        }

        if (jobValueText == null)
        {
            jobValueText =
                FindRowValueText(
                    detailRoot,
                    UiText.Get("touchSelect", "jobRow"));
        }

        if (maritalStatusText == null)
        {
            maritalStatusText =
                FindRowValueText(
                    detailRoot,
                    UiText.Get("touchSelect", "maritalStatusRow"));

            if (maritalStatusText == null)
            {
                maritalStatusText =
                    FindRowValueText(
                    detailRoot,
                    UiText.Get("touchSelect", "maritalStatusRowAlt"));
            }

            if (maritalStatusText == null)
            {
                Transform maritalStatusTransform =
                    FindChildByName(detailRoot, "tinhtrang");

                if (maritalStatusTransform != null)
                {
                    maritalStatusText =
                        maritalStatusTransform.GetComponent<TMP_Text>();
                }
            }

            if (maritalStatusText == null)
            {
                maritalStatusText =
                    FindTextByDisplayedText(
                        detailRoot,
                        "tinhtrang",
                        UiText.Get("touchSelect", "maritalStatusRow"),
                        UiText.Get("touchSelect", "maritalStatusRowAlt"));
            }
        }

        if (maritalStatusText != null)
        {
            maritalStatusText.raycastTarget = false;
        }

        if (statusText == null)
        {
            Transform statusTransform =
                FindChildByName(
                    detailRoot,
                    "trangthai");

            if (statusTransform == null)
            {
                statusTransform =
                    FindChildByName(
                        detailRoot,
                    UiText.Get("touchSelect", "statusRow"));
            }

            if (statusTransform == null)
            {
                statusTransform =
                    FindChildByName(
                        detailRoot,
                        UiText.Get("touchSelect", "statusRowAlt"));
            }

            if (statusTransform != null)
            {
                statusText = statusTransform.GetComponent<TMP_Text>();
            }

            if (statusText == null)
            {
                statusText =
                    FindTextByDisplayedText(
                        detailRoot,
                        UiText.Get("touchSelect", "statusRow"),
                        UiText.Get("touchSelect", "statusRowAlt"),
                        "trạng thái",
                        "trang thai",
                        "trangthai",
                        "status");
            }
        }

        if (statusText != null &&
            maritalStatusText == statusText)
        {
            maritalStatusText = null;
        }

        if (statusText != null)
        {
            statusText.raycastTarget = false;
        }

        if (equipmentListRoot == null)
        {
            Transform equipmentTransform =
                equipmentPanelRoot != null
                ? FindChildByName(
                    equipmentPanelRoot,
                    "EquipmentList")
                : FindChildByName(
                    detailRoot,
                    UiText.Get("touchSelect", "equipmentList"));

            if (equipmentTransform != null)
            {
                equipmentListRoot = equipmentTransform;
            }
        }

        if (equipmentRowTemplate == null &&
            equipmentListRoot != null &&
            equipmentListRoot.childCount > 0)
        {
            equipmentRowTemplate =
                equipmentListRoot.GetChild(0).gameObject;
        }

        if (skillListRoot == null)
        {
            Transform skillTransform =
                skillPanelRoot != null
                ? FindChildByName(
                    skillPanelRoot,
                    "SkillList")
                : FindChildByName(
                    detailRoot,
                    UiText.Get("touchSelect", "skillList"));

            if (skillTransform != null)
            {
                skillListRoot = skillTransform;
            }
        }

        if (skillRowTemplate == null &&
            skillListRoot != null &&
            skillListRoot.childCount > 0)
        {
            skillRowTemplate =
                skillListRoot.GetChild(0).gameObject;
        }

        EnsureInfoIcon();
        UpdateRealmIcon(currentTarget);
        splitTargetHeaderLayout = HasSplitTargetHeaderLayout();
    }

    bool UpdateTargetDetailPanel(Transform target)
    {
        if (target == null ||
            (!IsNpcTarget(target) && !IsMonsterTarget(target)) ||
            infoPanel == null)
        {
            ClearTargetDetailPanel();
            return false;
        }

        AutoFindTabReferences();

        string lockedMessage =
            NpcText.Get("dialogue", "heavenDaoBasicLocked");

        if (false && !HasHeavenDaoPower(HeavenDaoPower.ViewBasicNpcInfo))
        {
            SetValueText(damageValueText, "-");
            SetValueText(defenseValueText, "-");
            SetValueText(lifespanValueText, "-");
            SetValueText(jobValueText, "-");
            SetValueText(statusText, lockedMessage);
            ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);
            ClearSpawnedRows(spawnedSkillRows, skillListRoot);
            return true;
        }

        bool hasAnyDetail = false;

        hasAnyDetail |=
            SetValueText(
                damageValueText,
                FormatMaybeInt(GetTargetAttack(target)));

        hasAnyDetail |=
            SetValueText(
                defenseValueText,
                FormatMaybeInt(GetTargetDefense(target)));

        hasAnyDetail |=
            SetValueText(
                lifespanValueText,
                GetTargetLifespan(target));

        hasAnyDetail |=
            SetValueText(
                jobValueText,
                GetTargetJob(target));

        hasAnyDetail |=
            SetOptionalValueText(
                maritalStatusText,
                GetTargetMarriageStatus(target));

        hasAnyDetail |=
            SetValueText(
                statusText,
                UiText.Get("touchSelect", "statusRow") +
                FormatTargetActionText(target));

        hasAnyDetail |=
            RefreshEquipmentRows(target);

        hasAnyDetail |=
            RefreshSkillRows(target);

        return hasAnyDetail;
    }

    void ClearTargetDetailPanel()
    {
        SetValueText(damageValueText, "-");
        SetValueText(defenseValueText, "-");
        SetValueText(lifespanValueText, "-");
        SetValueText(jobValueText, "-");
        SetValueText(maritalStatusText, "-");
        SetValueText(statusText, "-");
        ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);
        ClearSpawnedRows(spawnedSkillRows, skillListRoot);
        SetSectionVisible(equipmentListRoot, false);
        SetSectionVisible(skillListRoot, false);
    }

    bool RefreshEquipmentRows(Transform target)
    {
        ItemInventory inventory =
            target != null
            ? target.GetComponent<ItemInventory>()
            : null;

        if (inventory == null ||
            equipmentListRoot == null)
        {
            ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);
            SetSectionVisible(equipmentListRoot, false);
            return false;
        }

        List<ItemStack> equipmentStacks =
            CollectEquipmentStacks(inventory);

        ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);

        if (equipmentStacks.Count <= 0)
        {
            SetSectionVisible(equipmentListRoot, false);
            return false;
        }

        SetSectionVisible(equipmentListRoot, true);

        GameObject template =
            GetRowTemplate(equipmentListRoot, equipmentRowTemplate);

        if (template == null)
        {
            return false;
        }

        for (int i = 0; i < equipmentStacks.Count; i++)
        {
            GameObject row =
                GetOrCreateRow(
                    equipmentListRoot,
                    template,
                    i,
                    spawnedEquipmentRows);

            if (row == null)
            {
                continue;
            }

            row.SetActive(true);
            BindEquipmentRow(row, equipmentStacks[i]);
        }

        return true;
    }

    bool RefreshSkillRows(Transform target)
    {
        ItemInventory inventory =
            target != null
            ? target.GetComponent<ItemInventory>()
            : null;

        if (inventory == null ||
            skillListRoot == null)
        {
            ClearSpawnedRows(spawnedSkillRows, skillListRoot);
            SetSectionVisible(skillListRoot, false);
            return false;
        }

        NpcHideCultivationInfo hideInfo =
            target.GetComponentInParent<NpcHideCultivationInfo>();

        if (hideInfo != null &&
            hideInfo.hideCultivationSkills)
        {
            ClearSpawnedRows(spawnedSkillRows, skillListRoot);
            SetSectionVisible(skillListRoot, false);
            return false;
        }

        List<ItemStack> skillStacks =
            CollectSkillStacks(inventory);

        ClearSpawnedRows(spawnedSkillRows, skillListRoot);

        if (skillStacks.Count <= 0)
        {
            SetSectionVisible(skillListRoot, false);
            return false;
        }

        SetSectionVisible(skillListRoot, true);

        GameObject template =
            GetRowTemplate(skillListRoot, skillRowTemplate);

        if (template == null)
        {
            return false;
        }

        for (int i = 0; i < skillStacks.Count; i++)
        {
            GameObject row =
                GetOrCreateRow(
                    skillListRoot,
                    template,
                    i,
                    spawnedSkillRows);

            if (row == null)
            {
                continue;
            }

            row.SetActive(true);
            BindSkillRow(row, skillStacks[i]);
        }

        return true;
    }

    void ClearSpawnedRows(
        List<GameObject> spawnedRows,
        Transform root)
    {
        if (spawnedRows != null)
        {
            spawnedRows.RemoveAll(item => item == null);
        }

        if (root == null)
        {
            return;
        }

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    void SetSectionVisible(
        Transform root,
        bool visible)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.SetActive(visible);
    }

    GameObject FindSectionContainer(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform current = root;
        while (current != null)
        {
            string key = NormalizeSectionName(current.name);
            if (key == "equipmentpanel" ||
                key == "skillpanel")
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return root.gameObject;
    }

    string NormalizeSectionName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }

    GameObject GetRowTemplate(
        Transform root,
        GameObject fallbackTemplate)
    {
        if (fallbackTemplate != null)
        {
            return fallbackTemplate;
        }

        if (root != null &&
            root.childCount > 0)
        {
            return root.GetChild(0).gameObject;
        }

        return null;
    }

    GameObject GetOrCreateRow(
        Transform root,
        GameObject template,
        int index,
        List<GameObject> spawnedRows)
    {
        if (root == null ||
            template == null ||
            index < 0)
        {
            return null;
        }

        if (index < root.childCount)
        {
            return root.GetChild(index).gameObject;
        }

        GameObject row =
            Instantiate(
                template,
                root,
                false);

        row.name = template.name;

        if (spawnedRows != null)
        {
            spawnedRows.Add(row);
        }

        return row;
    }

    List<ItemStack> CollectEquipmentStacks(ItemInventory inventory)
    {
        List<ItemStack> stacks =
            new List<ItemStack>();

        if (inventory == null)
        {
            return stacks;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.PhapBao ||
                !stack.applied)
            {
                continue;
            }

            EquipmentSlot slot =
                stack.item.GetResolvedEquipmentSlot();

            if (slot == EquipmentSlot.None)
            {
                continue;
            }

            stacks.Add(stack);
        }

        stacks.Sort(
            (left, right) =>
            {
                int leftApplied =
                    left != null && left.applied ? 0 : 1;
                int rightApplied =
                    right != null && right.applied ? 0 : 1;

                int compare =
                    leftApplied.CompareTo(rightApplied);
                if (compare != 0)
                {
                    return compare;
                }

                EquipmentSlot leftSlot =
                    left != null && left.item != null
                    ? left.item.GetResolvedEquipmentSlot()
                    : EquipmentSlot.None;

                EquipmentSlot rightSlot =
                    right != null && right.item != null
                    ? right.item.GetResolvedEquipmentSlot()
                    : EquipmentSlot.None;

                return leftSlot.CompareTo(rightSlot);
            });

        return stacks;
    }

    List<ItemStack> CollectSkillStacks(ItemInventory inventory)
    {
        List<ItemStack> stacks =
            new List<ItemStack>();

        if (inventory == null)
        {
            return stacks;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.CongPhap ||
                stack.broken ||
                stack.item.IsManualBroken(stack))
            {
                continue;
            }

            if (!stack.applied &&
                stack.mastery == CultivationManualMastery.None)
            {
                continue;
            }

            stacks.Add(stack);
        }

        stacks.Sort(
            (left, right) =>
            {
                float leftPower =
                    GetManualPower(left);
                float rightPower =
                    GetManualPower(right);

                int compare =
                    rightPower.CompareTo(leftPower);
                if (compare != 0)
                {
                    return compare;
                }

                string leftName =
                    left != null && left.item != null
                    ? ItemText.Name(left.item)
                    : "";
                string rightName =
                    right != null && right.item != null
                    ? ItemText.Name(right.item)
                    : "";
                return string.Compare(
                    leftName,
                    rightName,
                    StringComparison.Ordinal);
            });

        return stacks;
    }

    void BindEquipmentRow(
        GameObject row,
        ItemStack stack)
    {
        if (row == null ||
            stack == null ||
            stack.item == null)
        {
            return;
        }

        Image icon =
            FindRowImage(row, "Icon");
        if (icon == null)
        {
            icon =
                row.GetComponentInChildren<Image>(true);
        }

        if (icon != null)
        {
            Sprite itemIcon = stack.item.icon;
            icon.sprite = itemIcon;
            icon.enabled = itemIcon != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        TMP_Text nameText =
            FindRowText(row, "ItemNameText");
        if (nameText == null)
        {
            nameText = FindRowText(row, "NameText");
        }

        if (nameText != null)
        {
            nameText.text = ItemText.Name(stack.item);
            nameText.raycastTarget = false;
        }

        TMP_Text slotText =
            FindRowText(row, "SlotText");

        if (slotText != null)
        {
            slotText.text = GetEquipmentSlotLabel(stack.item);
            slotText.raycastTarget = false;
        }
    }

    void BindSkillRow(
        GameObject row,
        ItemStack stack)
    {
        if (row == null ||
            stack == null ||
            stack.item == null)
        {
            return;
        }

        Image icon =
            FindRowImage(row, "Icon");
        if (icon == null)
        {
            icon =
                row.GetComponentInChildren<Image>(true);
        }

        if (icon != null)
        {
            Sprite itemIcon = stack.item.icon;
            icon.sprite = itemIcon;
            icon.enabled = itemIcon != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        TMP_Text nameText =
            FindRowText(row, "SkillNameText");
        if (nameText == null)
        {
            nameText = FindRowText(row, "NameText");
        }

        if (nameText != null)
        {
            nameText.text = ItemText.Name(stack.item);
            nameText.raycastTarget = false;
        }

        TMP_Text percentText =
            FindRowText(row, "PercentText");

        if (percentText != null)
        {
            percentText.text =
                FormatPercentText(
                    GetManualPower(stack));
            percentText.raycastTarget = false;
        }

        Image fillImage =
            FindRowImage(row, "ProficiencyFill");
        if (fillImage == null)
        {
            Transform fillTransform =
                FindChildByName(row.transform, "ProficiencyFill");

            if (fillTransform != null)
            {
                fillImage = fillTransform.GetComponent<Image>();
            }
        }

        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillAmount =
                Mathf.Clamp01(GetManualPower(stack));
            fillImage.raycastTarget = false;
        }
    }

    TMP_Text FindRowText(
        GameObject row,
        string childName)
    {
        if (row == null ||
            string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform child =
            FindChildByName(row.transform, childName);

        if (child == null)
        {
            return null;
        }

        return child.GetComponent<TMP_Text>();
    }

    Image FindRowImage(
        GameObject row,
        string childName)
    {
        if (row == null ||
            string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform child =
            FindChildByName(row.transform, childName);

        if (child == null)
        {
            return null;
        }

        return child.GetComponent<Image>();
    }

    TMP_Text FindRowValueText(
        Transform root,
        string rowName)
    {
        if (root == null ||
            string.IsNullOrEmpty(rowName))
        {
            return null;
        }

        Transform row =
            FindChildByName(root, rowName);

        if (row == null)
        {
            return null;
        }

        Transform value =
            FindChildByName(row, "ValueText");

        if (value == null)
        {
            value = FindChildByName(row, "PercentText");
        }

        if (value == null)
        {
            value = FindChildByName(row, "SlotText");
        }

        if (value == null)
        {
            value = FindChildByName(row, "LabelText");
        }

        if (value == null)
        {
            return row.GetComponent<TMP_Text>();
        }

        return value.GetComponent<TMP_Text>();
    }

    TMP_Text FindTextByDisplayedText(
        Transform root,
        params string[] candidates)
    {
        if (root == null ||
            candidates == null ||
            candidates.Length <= 0)
        {
            return null;
        }

        TMP_Text[] texts =
            root.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            string current =
                NormalizeLookupText(text.text);
            string objectName =
                NormalizeLookupText(text.gameObject.name);

            for (int j = 0; j < candidates.Length; j++)
            {
                string candidate =
                    NormalizeLookupText(candidates[j]);

                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                if (current == candidate ||
                    objectName == candidate)
                {
                    return text;
                }
            }
        }

        return null;
    }

    string NormalizeLookupText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
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

            if (char.IsWhiteSpace(c) ||
                c == '_' ||
                c == ':')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    string GetEquipmentSlotLabel(StatItemData item)
    {
        if (item == null)
        {
            return UiText.Get("touchSelect", "placeholder");
        }

        EquipmentSlot slot =
            item.GetResolvedEquipmentSlot();

        switch (slot)
        {
            case EquipmentSlot.Weapon:
                return UiText.Get("touchSelect", "equipmentWeapon");
            case EquipmentSlot.Armor:
                return UiText.Get("touchSelect", "equipmentArmor");
            case EquipmentSlot.Accessory:
                return UiText.Get("touchSelect", "equipmentAccessory");
        }

        return item.itemType == ItemType.PhapBao
            ? UiText.Get("touchSelect", "equipmentAccessory")
            : UiText.Get("touchSelect", "placeholder");
    }

    float GetManualPower(ItemStack stack)
    {
        if (stack == null ||
            stack.item == null ||
            stack.item.itemType != ItemType.CongPhap)
        {
            return 0f;
        }

        switch (stack.mastery)
        {
            case CultivationManualMastery.TieuThanh:
                return stack.item.tieuThanhPower;
            case CultivationManualMastery.TrungThanh:
                return stack.item.trungThanhPower;
            case CultivationManualMastery.DaiThanh:
                return stack.item.daiThanhPower;
        }

        return 0f;
    }

    string FormatPercentText(float percentValue)
    {
        return Mathf.Clamp(Mathf.RoundToInt(percentValue * 100f), 0, 100) + "%";
    }

    string FormatTargetActionText(Transform target)
    {
        string action =
            GetDisplayAction(
                GetTargetAction(target));

        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        if (action.StartsWith("đang", StringComparison.OrdinalIgnoreCase) ||
            action.StartsWith("Đang", StringComparison.OrdinalIgnoreCase))
        {
            return action;
        }

        return UiText.Format("touchSelect", "actionFormat", action);
    }

    bool SetValueText(
        TMP_Text text,
        int value)
    {
        if (text == null)
        {
            return false;
        }

        SetValueText(text, value.ToString());
        return true;
    }

    bool SetValueText(
        TMP_Text text,
        string value)
    {
        if (text == null)
        {
            return false;
        }

        text.text =
            string.IsNullOrEmpty(value)
            ? "-"
            : value;
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        return true;
    }

    bool SetOptionalValueText(
        TMP_Text text,
        string value)
    {
        if (text == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            text.text = "";
            text.gameObject.SetActive(false);
            return false;
        }

        SetValueText(text, value);
        return true;
    }

    bool HasSplitTargetHeaderLayout()
    {
        return nameText != null ||
            realmText != null ||
            hpText != null ||
            hpFillImage != null;
    }

    bool HasTargetDetailPanelLayout()
    {
        return damageValueText != null ||
            defenseValueText != null ||
            lifespanValueText != null ||
            jobValueText != null ||
            statusText != null ||
            equipmentListRoot != null ||
            skillListRoot != null;
    }

    Button FindButtonByName(
        Transform parent,
        params string[] names)
    {
        foreach (string targetName in names)
        {
            Transform found =
                FindChildByName(parent, targetName);

            if (found == null)
            {
                continue;
            }

            Button button =
                found.GetComponent<Button>();

            if (button != null)
            {
                return button;
            }

            button =
                found.GetComponentInParent<Button>();

            if (button != null &&
                button.transform.IsChildOf(parent))
            {
                return button;
            }

            button =
                found.GetComponentInChildren<Button>(true);

            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    Transform FindChildByName(
        Transform parent,
        string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform found =
                FindChildByName(child, childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    void SetInfoContentVisible(bool visible)
    {
        if (infoContentRoot != null)
        {
            SetCanvasGroupVisible(infoContentRoot, visible);
        }

        if (infoPanel != null)
        {
            Image panelImage = infoPanel.GetComponent<Image>();

            if (panelImage != null)
            {
                panelImage.raycastTarget = false;
            }
        }

        if (infoText != null)
        {
            infoText.gameObject.SetActive(
                visible &&
                !HasTargetDetailPanelLayout());
        }

        if (!visible)
        {
            SetInfoIconVisible(false);
        }
    }

    void EnsureInfoIcon()
    {
        if (infoIcon != null ||
            infoPanel == null)
        {
            return;
        }

        Transform parent =
            infoContentRoot != null
            ? infoContentRoot.transform
            : infoPanel.transform;

        GameObject iconObject =
            new GameObject(
                "InfoIcon",
                typeof(RectTransform),
                typeof(Image));

        iconObject.transform.SetParent(parent, false);

        RectTransform iconRect =
            iconObject.GetComponent<RectTransform>();

        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = worldItemIconOffset;
        iconRect.sizeDelta = worldItemIconSize;

        infoIcon = iconObject.GetComponent<Image>();
        infoIcon.preserveAspect = true;
        infoIcon.raycastTarget = false;
        SetInfoIconVisible(false);
    }

    void AutoFindWorldItemPanelReferences()
    {
        if (worldItemInfoPanel == null)
        {
            RectTransform[] rects =
                FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include);

            foreach (RectTransform rect in rects)
            {
                if (rect != null &&
                    rect.name == "WorldItemInfoPanel")
                {
                    worldItemInfoPanel = rect.gameObject;
                    break;
                }
            }
        }

        if (worldItemInfoPanel == null)
        {
            return;
        }

        worldItemPanelRect =
            worldItemInfoPanel.GetComponent<RectTransform>();

        if (worldItemPanelIcon == null)
        {
            Transform iconTransform =
                FindChildByName(worldItemInfoPanel.transform, "ItemIcon");

            if (iconTransform == null)
            {
                iconTransform =
                    FindChildByName(worldItemInfoPanel.transform, "InfoIcon");
            }

            if (iconTransform == null)
            {
                iconTransform =
                    FindChildByName(worldItemInfoPanel.transform, "IconImage");
            }

            if (iconTransform != null)
            {
                worldItemPanelIcon = iconTransform.GetComponent<Image>();
            }
        }

        if (worldItemNameText == null)
        {
            Transform nameTransform =
                FindChildByName(worldItemInfoPanel.transform, "ItemNameText");

            if (nameTransform == null)
            {
                nameTransform =
                    FindChildByName(worldItemInfoPanel.transform, "NameText");
            }

            if (nameTransform == null)
            {
                nameTransform =
                    FindChildByName(worldItemInfoPanel.transform, "TitleText");
            }

            if (nameTransform != null)
            {
                worldItemNameText = nameTransform.GetComponent<TMP_Text>();
            }
        }

        if (worldItemInfoText == null)
        {
            Transform infoTransform =
                FindChildByName(worldItemInfoPanel.transform, "ItemInfoText");

            if (infoTransform == null)
            {
                infoTransform =
                    FindChildByName(worldItemInfoPanel.transform, "DescriptionText");
            }

            if (infoTransform == null)
            {
                infoTransform =
                    FindChildByName(worldItemInfoPanel.transform, "InfoText");
            }

            if (infoTransform != null)
            {
                worldItemInfoText = infoTransform.GetComponent<TMP_Text>();
            }
        }
    }

    void SetWorldItemPanelVisible(bool visible)
    {
        if (worldItemInfoPanel == null)
        {
            return;
        }

        worldItemInfoPanel.SetActive(visible);
    }

    bool IsWorldItemTarget(Transform target)
    {
        return target != null &&
            target.GetComponent<WorldStatItemPickup>() != null;
    }

    bool CanShowInventoryForTarget(Transform target)
    {
        return target != null;
    }

    void ShowWorldItemInfo(Transform target)
    {
        AutoFindWorldItemPanelReferences();

        WorldStatItemPickup pickup =
            target != null
            ? target.GetComponent<WorldStatItemPickup>()
            : null;

        if (pickup == null ||
            pickup.item == null)
        {
            SetWorldItemPanelVisible(false);
            return;
        }

        SetWorldItemPanelVisible(true);

        if (worldItemNameText != null)
        {
            worldItemNameText.text = ItemText.Name(pickup.item);
        }

        if (worldItemInfoText != null)
        {
            worldItemInfoText.text = BuildWorldItemBodyInfo(pickup);
        }

        if (worldItemPanelIcon != null)
        {
            Sprite icon = GetPickupIcon(pickup);
            worldItemPanelIcon.sprite = icon;
            worldItemPanelIcon.enabled = icon != null;
            worldItemPanelIcon.preserveAspect = true;
        }
    }
    void UpdateInfoIcon(Transform target)
    {
        EnsureInfoIcon();
        RestoreInfoTextMargin();

        WorldStatItemPickup pickup =
            target != null
            ? target.GetComponent<WorldStatItemPickup>()
            : null;

        bool isWorldItem =
            pickup != null;

        bool mirrored = false;

        Sprite icon =
            isWorldItem
            ? GetPickupIcon(pickup)
            : GetCharacterIcon(target, out mirrored);

        if (infoIcon == null ||
            icon == null)
        {
            SetInfoIconVisible(false);
            return;
        }

        infoIcon.sprite = icon;
        infoIcon.color = GetFactionTint(target);
        ConfigureInfoIconLayout(isWorldItem);
        ApplyFactionIconMirror(isWorldItem, mirrored);
        SetInfoIconVisible(true);

        if (!HasSplitTargetHeaderLayout())
        {
            ApplyInfoTextMargin(
                isWorldItem
                ? worldItemTextLeftPadding
                : characterTextLeftPadding);
        }
    }

    Sprite GetPickupIcon(WorldStatItemPickup pickup)
    {
        if (pickup == null ||
            pickup.item == null)
        {
            return null;
        }

        if (pickup.item.icon != null)
        {
            return pickup.item.icon;
        }

        SpriteRenderer spriteRenderer =
            pickup.GetComponentInChildren<SpriteRenderer>(true);

        return spriteRenderer != null
            ? spriteRenderer.sprite
            : null;
    }

    Sprite GetCharacterIcon(Transform target, out bool mirrored)
    {
        mirrored = false;

        if (target == null)
        {
            return null;
        }

        if (IsMonsterPortraitTarget(target))
        {
            SpriteRenderer monsterRenderer =
                FindBestCharacterRenderer(target, out mirrored);

            if (monsterRenderer != null &&
                monsterRenderer.sprite != null)
            {
                return monsterRenderer.sprite;
            }

            LoadPortraitIcons();
            return monsterPortraitDefaultIcon;
        }

        SpriteRenderer liveRenderer =
            FindBestCharacterRenderer(target, out mirrored);

        if (liveRenderer != null &&
            liveRenderer.sprite != null)
        {
            return liveRenderer.sprite;
        }

        NpcPortraitIcon portraitIcon =
            target.GetComponent<NpcPortraitIcon>();

        if (portraitIcon == null)
        {
            portraitIcon =
                target.GetComponentInChildren<NpcPortraitIcon>(true);
        }

        if (portraitIcon != null &&
            portraitIcon.icon != null)
        {
            return portraitIcon.icon;
        }

        Sprite defaultPortraitIcon =
            GetDefaultPortraitIcon(target);

        if (defaultPortraitIcon != null)
        {
            return defaultPortraitIcon;
        }

        bool forceSprite =
            target.GetComponent<MonsterAI>() != null ||
            target.GetComponent<BicanhBoneMonsterAI>() != null ||
            target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<SmartNpcAI>() != null ||
            target.GetComponent<CharacterStats>() != null ||
            target.GetComponent<PlayerHealth>() != null;

        if (!autoUseCharacterSprite &&
            !forceSprite)
        {
            return null;
        }

        SpriteRenderer fallbackRenderer =
            FindBestCharacterRenderer(target, out mirrored);

        return fallbackRenderer != null
            ? fallbackRenderer.sprite
            : null;
    }

    SpriteRenderer FindBestCharacterRenderer(
        Transform target,
        out bool mirrored)
    {
        mirrored = false;

        if (target == null)
        {
            return null;
        }

        SpriteRenderer bestRenderer = null;
        float bestArea = -1f;

        SpriteRenderer[] renderers =
            target.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null ||
                renderer.sprite == null ||
                !renderer.enabled)
            {
                continue;
            }

            float area =
                renderer.bounds.size.x *
                renderer.bounds.size.y;

            if (area > bestArea)
            {
                bestArea = area;
                bestRenderer = renderer;
            }
        }

        if (bestRenderer != null)
        {
            mirrored =
                bestRenderer.flipX ^
                (bestRenderer.transform.lossyScale.x < 0f);
        }

        return bestRenderer;
    }

    void ApplyFactionIconMirror(bool isWorldItem, bool mirrored)
    {
        if (infoIcon == null)
        {
            return;
        }

        RectTransform rect = infoIcon.rectTransform;
        if (rect == null)
        {
            return;
        }

        if (!hasOriginalInfoIconScale)
        {
            originalInfoIconScale = rect.localScale;
            hasOriginalInfoIconScale = true;
        }

        if (isWorldItem)
        {
            rect.localScale = originalInfoIconScale;
            return;
        }

        Vector3 scale = originalInfoIconScale;
        scale.x = Mathf.Abs(scale.x) * (mirrored ? -1f : 1f);
        rect.localScale = scale;
    }

    Sprite GetDefaultPortraitIcon(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        LoadPortraitIcons();

        if (IsMonsterPortraitTarget(target))
        {
            return monsterPortraitDefaultIcon;
        }

        if (IsNpcPortraitTarget(target))
        {
            return npcPortraitDefaultIcon;
        }

        return null;
    }

    bool IsMonsterPortraitTarget(Transform target)
    {
        return target.GetComponent<MonsterAI>() != null ||
            target.GetComponent<BicanhBoneMonsterAI>() != null ||
            HasTag(target, "Monster") ||
            target.name.IndexOf("monster", StringComparison.OrdinalIgnoreCase) >= 0 ||
            target.name.IndexOf("monter", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool IsNpcPortraitTarget(Transform target)
    {
        return target.GetComponent<SmartNpcAI>() != null ||
            target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<CharacterStats>() != null ||
            HasTag(target, "NPC") ||
            HasTag(target, "Npc") ||
            target.name.IndexOf("npc", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool HasTag(Transform target, string tagName)
    {
        if (target == null ||
            string.IsNullOrEmpty(tagName))
        {
            return false;
        }

        return target.gameObject.tag == tagName;
    }

    static void LoadPortraitIcons()
    {
        if (portraitIconsLoaded)
        {
            return;
        }

        npcPortraitDefaultIcon =
            Resources.Load<Sprite>("photo/icon/npc_monter/npcicon");
        monsterPortraitDefaultIcon =
            Resources.Load<Sprite>("photo/icon/npc_monter/monter");

        portraitIconsLoaded = true;
    }

    void ConfigureInfoIconLayout(bool isWorldItem)
    {
        if (infoIcon == null)
        {
            return;
        }

        if (HasSplitTargetHeaderLayout())
        {
            infoIcon.preserveAspect = true;
            infoIcon.raycastTarget = false;
            return;
        }

        RectTransform rect =
            infoIcon.GetComponent<RectTransform>();

        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition =
            isWorldItem
            ? worldItemIconOffset
            : characterPortraitOffset;
        rect.sizeDelta =
            isWorldItem
            ? worldItemIconSize
            : characterPortraitSize;

        infoIcon.preserveAspect = true;
        infoIcon.raycastTarget = false;
    }

    void EnsureNpcIcon()
    {
        if (npcIcon != null)
        {
            return;
        }

        if (infoContentRoot != null)
        {
            Transform npcIconTransform =
                FindChildByName(infoContentRoot.transform, "npcicon");

            if (npcIconTransform != null)
            {
                npcIcon = npcIconTransform.GetComponent<Image>();
            }
        }

        if (npcIcon == null &&
            infoPanel != null)
        {
            Transform npcIconTransform =
                FindChildByName(infoPanel.transform, "npcicon");

            if (npcIconTransform != null)
            {
                npcIcon = npcIconTransform.GetComponent<Image>();
            }
        }

        if (npcIcon != null)
        {
            npcIcon.raycastTarget = false;
        }
    }

    void SetInfoIconVisible(bool visible)
    {
        if (infoIcon != null)
        {
            infoIcon.gameObject.SetActive(visible);
            infoIcon.enabled = visible;
        }

        if (!visible)
        {
            RestoreInfoTextMargin();
        }
    }

    void ApplyInfoTextMargin(float leftPadding)
    {
        if (infoText == null)
        {
            return;
        }

        if (!hasOriginalInfoTextMargin)
        {
            originalInfoTextMargin = infoText.margin;
            hasOriginalInfoTextMargin = true;
        }

        Vector4 margin =
            originalInfoTextMargin;

        margin.x += leftPadding;
        infoText.margin = margin;
    }

    void RestoreInfoTextMargin()
    {
        if (infoText == null ||
            !hasOriginalInfoTextMargin)
        {
            return;
        }

        infoText.margin = originalInfoTextMargin;
    }

    void SetInventoryContentVisible(bool visible)
    {
        if (inventoryContentRoot != null)
        {
            SetCanvasGroupVisible(inventoryContentRoot, visible);
        }

        if (npcInventoryPanel != null &&
            npcInventoryPanel.panelRoot != null)
        {
            npcInventoryPanel.SetContentVisible(visible);
        }
    }

    void SetCanvasGroupVisible(GameObject target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        if (!target.activeSelf)
        {
            target.SetActive(true);
        }

        CanvasGroup group =
            target.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    Transform GetSelectableTarget(Collider2D hit)
    {
        WorldStatItemPickup pickup =
            hit.GetComponentInParent<WorldStatItemPickup>();

        if (pickup != null &&
            pickup.item != null &&
            pickup.amount > 0)
        {
            return pickup.transform;
        }

        Transform taggedNpc =
            GetTaggedNpcTarget(hit);

        if (taggedNpc != null)
        {
            return taggedNpc;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.transform;
        }

        MonsterAI monster =
            hit.GetComponentInParent<MonsterAI>();

        if (monster != null)
        {
            return monster.transform;
        }

        return null;
    }

    Transform GetTaggedNpcTarget(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        Transform taggedTarget = null;
        Transform current = hit.transform;

        while (current != null)
        {
            if (IsNpcTag(current))
            {
                taggedTarget = current;
                break;
            }

            current = current.parent;
        }

        if (taggedTarget == null)
        {
            return null;
        }

        SmartNpcAI smartNpc =
            taggedTarget.GetComponentInParent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        VillagerAI villager =
            taggedTarget.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.transform;
        }

        CharacterStats stats =
            taggedTarget.GetComponentInParent<CharacterStats>();

        if (stats != null)
        {
            return stats.transform;
        }

        ItemInventory inventory =
            taggedTarget.GetComponentInParent<ItemInventory>();

        if (inventory != null)
        {
            return inventory.transform;
        }

        return taggedTarget;
    }

    bool IsNpcTag(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        return string.Equals(
            target.gameObject.tag,
            "npc",
            StringComparison.OrdinalIgnoreCase);
    }

    Transform GetClosestSelectableTarget(
        Collider2D[] hits,
        Vector2 worldPos)
    {
        Transform closestTarget = null;
        int closestPriority = int.MaxValue;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            Transform target =
                GetSelectableTarget(hit);

            if (target == null)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    worldPos,
                    target.position);

            int priority = GetSelectableTargetPriority(target);

            if (priority < closestPriority ||
                (priority == closestPriority &&
                distance < closestDistance))
            {
                closestPriority = priority;
                closestDistance = distance;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    int GetSelectableTargetPriority(Transform target)
    {
        if (target == null)
        {
            return int.MaxValue;
        }

        if (IsNpcTarget(target) ||
            target.GetComponent<MonsterAI>() != null ||
            target.GetComponent<BicanhBoneMonsterAI>() != null)
        {
            return 0;
        }

        if (IsWorldItemTarget(target))
        {
            return 1;
        }

        return 2;
    }

    string GetTargetName(Transform target)
    {
        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.monsterName;
        }

        return target.name;
    }

    string GetTargetRealm(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return NpcText.RealmWithStage(
                characterStats.realm,
                characterStats.realmStage);
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return NpcText.RealmWithStage(smartNpc.realm, smartNpc.realmStage);
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.GetRealmText();
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null &&
            monster.entityProfile != null)
        {
            return NpcText.RealmWithStage(
                monster.entityProfile.stats.realm,
                monster.entityProfile.stats.realmStage);
        }

        return NpcText.Get("entityTypes", "monster");
    }

    void UpdateRealmIcon(Transform target)
    {
        if (realmIcon == null)
        {
            return;
        }

        Sprite sprite =
            GetRealmIconSprite(target);

        if (sprite == null)
        {
            realmIcon.enabled = false;
            realmIcon.gameObject.SetActive(false);
            return;
        }

        realmIcon.sprite = sprite;
        realmIcon.enabled = true;
        realmIcon.preserveAspect = true;
        realmIcon.raycastTarget = false;
        realmIcon.gameObject.SetActive(true);
    }

    Sprite GetRealmIconSprite(Transform target)
    {
        if (target == null)
        {
            return GetRealmIconSprite(CultivationRealm.Mortal);
        }

        if (TryGetTargetCultivationRealm(target, out CultivationRealm realm))
        {
            return GetRealmIconSprite(realm);
        }

        return GetRealmIconSprite(CultivationRealm.Mortal);
    }

    Sprite GetRealmIconSprite(CultivationRealm realm)
    {
        LoadRealmIcons();

        switch (realm)
        {
            case CultivationRealm.QiRefining:
                return luyenKhiRealmIcon ?? phamNhanRealmIcon;
            case CultivationRealm.Foundation:
                return trucCoRealmIcon ?? luyenKhiRealmIcon ?? phamNhanRealmIcon;
            case CultivationRealm.GoldenCore:
                return kimDanRealmIcon ?? trucCoRealmIcon ?? phamNhanRealmIcon;
            case CultivationRealm.NascentSoul:
                return hoaThanRealmIcon ?? kimDanRealmIcon ?? phamNhanRealmIcon;
            case CultivationRealm.SoulFormation:
            case CultivationRealm.Tribulation:
                return doKiepRealmIcon ?? hoaThanRealmIcon ?? phamNhanRealmIcon;
            case CultivationRealm.Mortal:
            default:
                return phamNhanRealmIcon ?? luyenKhiRealmIcon;
        }
    }

    void UpdateNpcIcon(Transform target)
    {
        EnsureNpcIcon();

        if (npcIcon == null || target == null)
        {
            return;
        }

        LoadPortraitIcons();

        Sprite icon =
            IsMonsterPortraitTarget(target)
            ? monsterPortraitDefaultIcon
            : npcPortraitDefaultIcon;

        npcIcon.sprite = icon;
        npcIcon.enabled = icon != null;
        npcIcon.preserveAspect = true;
        npcIcon.raycastTarget = false;
        npcIcon.gameObject.SetActive(icon != null);
    }

    void LoadRealmIcons()
    {
        if (realmIconsLoaded)
        {
            return;
        }

        realmIconsLoaded = true;
        phamNhanRealmIcon =
            Resources.Load<Sprite>("photo/icon/rank/phamnhan");
        luyenKhiRealmIcon =
            Resources.Load<Sprite>("photo/icon/rank/luyenkhi");
        trucCoRealmIcon =
            Resources.Load<Sprite>("photo/icon/rank/trucco");
        kimDanRealmIcon =
            Resources.Load<Sprite>("photo/icon/rank/kimdan");
        hoaThanRealmIcon =
            Resources.Load<Sprite>("photo/icon/rank/hoathan");
        doKiepRealmIcon =
            Resources.Load<Sprite>("photo/icon/rank/dokiep");
    }

    bool TryGetTargetCultivationRealm(
        Transform target,
        out CultivationRealm realm)
    {
        realm = CultivationRealm.Mortal;

        if (target == null)
        {
            return false;
        }

        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            realm = characterStats.realm;
            return true;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            realm = smartNpc.realm;
            return true;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            realm = villager.realm;
            return true;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null &&
            monster.entityProfile != null)
        {
            realm = monster.entityProfile.stats.realm;
            return true;
        }

        NpcData npcData =
            target.GetComponent<NpcData>();

        if (npcData != null)
        {
            return TryParseNpcDataRealm(npcData.realm, out realm);
        }

        return false;
    }

    bool TryParseNpcDataRealm(
        string realmText,
        out CultivationRealm realm)
    {
        realm = CultivationRealm.Mortal;

        if (string.IsNullOrWhiteSpace(realmText))
        {
            return false;
        }

        string normalized =
            realmText.Trim().ToLowerInvariant();

        if (normalized.Contains("mort"))
        {
            realm = CultivationRealm.Mortal;
            return true;
        }

        if (normalized.Contains("luyen") ||
            normalized.Contains("qi"))
        {
            realm = CultivationRealm.QiRefining;
            return true;
        }

        if (normalized.Contains("truc") ||
            normalized.Contains("found"))
        {
            realm = CultivationRealm.Foundation;
            return true;
        }

        if (normalized.Contains("kim"))
        {
            realm = CultivationRealm.GoldenCore;
            return true;
        }

        if (normalized.Contains("hoa"))
        {
            realm = CultivationRealm.NascentSoul;
            return true;
        }

        if (normalized.Contains("than") ||
            normalized.Contains("so")
        )
        {
            realm = CultivationRealm.SoulFormation;
            return true;
        }

        if (normalized.Contains("do") ||
            normalized.Contains("trib"))
        {
            realm = CultivationRealm.Tribulation;
            return true;
        }

        return false;
    }

    int GetTargetCurrentHP(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.currentHP;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.currentHP;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.currentHP;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.currentHP;
        }

        return 0;
    }

    int GetTargetMaxHP(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.finalHP;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.maxHP;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.maxHP;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.maxHP;
        }

        return 0;
    }

    string GetTargetAction(Transform target)
    {
        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.GetPlayerActionText();
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.GetPlayerActionText();
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.GetPlayerActionText();
        }

        NpcData npcData =
            target.GetComponent<NpcData>();

        if (npcData != null &&
            !string.IsNullOrWhiteSpace(npcData.currentAction))
        {
            return npcData.currentAction;
        }

        NpcMapMover2D mapMover =
            target.GetComponent<NpcMapMover2D>();

        if (mapMover != null &&
            !string.IsNullOrWhiteSpace(mapMover.currentAction))
        {
            return mapMover.currentAction;
        }

        return "";
    }

    string GetDisplayAction(string action)
    {
        if (string.IsNullOrEmpty(action) ||
            action == NpcText.Action("avoidObstacle"))
        {
            return "";
        }

        return action;
    }

    string GetCurrentNpcActionForDisplay(
        Transform target,
        string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        if (!IsTravelIntentAction(action))
        {
            return action;
        }

        if (!IsNpcMoving(target))
        {
            return NpcText.Action("waiting");
        }

        return action;
    }

    bool IsNpcMoving(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            return false;
        }

        return body.linearVelocity.sqrMagnitude > 0.0025f;
    }

    bool IsTravelIntentAction(string action)
    {
        return action == NpcText.Action("goMarketTrade") ||
            action == NpcText.Action("goTaskProviderDaily") ||
            action == NpcText.Action("goVanBaoLauBroker") ||
            action == NpcText.Action("goVanBaoLauTask") ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("goWork") ||
            action == NpcText.Action("goFarmWork") ||
            action == NpcText.Action("goPatrol") ||
            action == NpcText.Action("goHeal") ||
            action == NpcText.Action("goFish") ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("goTavern") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("goHomeRest") ||
            action == NpcText.Action("goHomeCultivate") ||
            action == NpcText.Action("goCultivatePoint") ||
            action == NpcText.Action("moveToTask") ||
            action == NpcText.Action("receiveTask") ||
            action == NpcText.Action("goPlay") ||
            action == NpcText.Action("walkingRoad");
    }

    void UpdateTargetHeader(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (nameText != null)
        {
            nameText.text = GetTargetName(target);
        }

        if (realmText != null)
        {
            realmText.text = GetTargetRealm(target);
        }

        UpdateRealmIcon(target);

        UpdateHealthDisplay(target);
        UpdateExpDisplay(target);
    }

    void UpdateHealthDisplay(Transform target)
    {
        int maxHP = Mathf.Max(1, GetTargetMaxHP(target));
        int currentHP = Mathf.Clamp(GetTargetCurrentHP(target), 0, maxHP);
        float fill = Mathf.Clamp01(currentHP / (float)maxHP);

        if (hpFillImage != null)
        {
            hpFillImage.type = Image.Type.Filled;
            hpFillImage.fillAmount = fill;
            hpFillImage.color = Color.Lerp(
                new Color(0.62f, 0.12f, 0.12f, 1f),
                new Color(0.18f, 0.72f, 0.24f, 1f),
                fill);
        }

        if (hpText != null)
        {
            hpText.text =
                FormatCompactNumber(currentHP) +
                " / " +
                FormatCompactNumber(maxHP);
        }
    }

    void UpdateExpDisplay(Transform target)
    {
        if (expText == null)
        {
            return;
        }

        if (!TryGetTargetCultivationExp(target, out long currentExp) ||
            !TryGetTargetCultivationNeed(target, out long needExp))
        {
            expText.text = "-";
            return;
        }

        currentExp = Math.Max(0L, currentExp);
        needExp = Math.Max(1L, needExp);

        if (TryGetTargetCultivationRatePer10Seconds(target, out long ratePer10Seconds) &&
            ratePer10Seconds > 0)
        {
            expText.text =
                FormatCompactNumber(currentExp) +
                " / " +
                FormatCompactNumber(needExp) +
                "  (+" +
                FormatCompactNumber(ratePer10Seconds) +
                "/10s)";
            return;
        }

        expText.text =
            FormatCompactNumber(currentExp) +
            " / " +
            FormatCompactNumber(needExp);
    }

    Color GetFactionTint(Transform target)
    {
        if (target == null)
        {
            return Color.white;
        }

        if (target.GetComponent<BicanhBoneMonsterAI>() != null)
        {
            return new Color(0.82f, 0.78f, 0.96f, 1f);
        }

        if (target.GetComponent<MonsterAI>() != null)
        {
            return new Color(1f, 0.72f, 0.42f, 1f);
        }

        if (target.CompareTag("Player") ||
            target.GetComponentInParent<PlayerHealth>() != null)
        {
            return new Color(0.55f, 0.88f, 1f, 1f);
        }

        if (target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<SmartNpcAI>() != null ||
            target.GetComponent<NpcData>() != null)
        {
            return new Color(0.45f, 1f, 0.62f, 1f);
        }

        return Color.white;
    }

    string FormatMaybeInt(int value)
    {
        return value < 0 ? "-" : NpcEconomy.FormatCompactAmount(value);
    }

    string FormatMaybeFloat(float value)
    {
        return value < 0f ? "-" : value.ToString("0.##");
    }

    string FormatCompactNumber(long value)
    {
        return NpcEconomy.FormatCompactAmount(value);
    }

    int GetTargetAttack(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.attack;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.attack;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.attack;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.damage;
        }

        return -1;
    }

    bool TryGetTargetCultivationExp(
        Transform target,
        out long cultivationExp)
    {
        cultivationExp = 0L;

        if (target == null)
        {
            return false;
        }

        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            cultivationExp = characterStats.cultivationExp;
            return true;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            cultivationExp = smartNpc.cultivation;
            return true;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            cultivationExp = villager.cultivationExp;
            return true;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            cultivationExp = monster.cultivationExp;
            return true;
        }

        return false;
    }

    bool TryGetTargetCultivationNeed(
        Transform target,
        out long cultivationNeed)
    {
        cultivationNeed = 0L;

        if (target == null)
        {
            return false;
        }

        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            cultivationNeed = characterStats.ExpToNextRealm();
            return true;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            cultivationNeed = smartNpc.breakthroughNeed;
            return true;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            cultivationNeed = villager.ExpToNextRealm();
            return true;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            cultivationNeed = monster.ExpToNextRealm();
            return true;
        }

        return false;
    }

    bool TryGetTargetCultivationRatePer10Seconds(
        Transform target,
        out long ratePer10Seconds)
    {
        ratePer10Seconds = 0L;

        if (target == null)
        {
            return false;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            float multiplier =
                Mathf.Max(0.1f, smartNpc.GetCultivationMultiplier());

            if (smartNpc.pill > 0)
            {
                ratePer10Seconds =
                    Mathf.RoundToInt(3000f * multiplier);
                return true;
            }

            if (smartNpc.spiritStone > 0)
            {
                ratePer10Seconds =
                    Mathf.RoundToInt(
                        CultivationProgression.GetSpiritStoneExp(
                            smartNpc.realm,
                            smartNpc.realmStage) *
                        10f *
                        multiplier);
                return true;
            }

            ratePer10Seconds =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        500f *
                        multiplier));
            return true;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            ratePer10Seconds =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        500f *
                        CultivationProgression.GetCultivationRealmMultiplier(
                            villager.realm,
                            villager.realmStage) *
                        Mathf.Max(0.5f, villager.diligence / 50f)));
            return true;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            ratePer10Seconds =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        monster.naturalCultivationExpPerSecond *
                        10f *
                        CultivationProgression.GetCultivationRealmMultiplier(
                            monster.realm,
                            monster.realmStage) *
                        Mathf.Lerp(
                            1f,
                            monster.hungryCultivationEfficiency,
                            Mathf.Clamp01(monster.hunger / 100f)) *
                        Mathf.Max(
                            0.05f,
                            CultivationProgression.GetSpiritStoneEfficiency(
                                monster.realm))));
            return true;
        }

        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            long need = characterStats.ExpToNextRealm();
            ratePer10Seconds =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(Mathf.Max(500f, need / 10f)));
            return true;
        }

        return false;
    }

    int GetTargetDefense(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.defense;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.defense;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.defense;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.defense;
        }

        return -1;
    }

    float GetTargetMoveSpeed(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.moveSpeed;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.moveSpeed;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.moveSpeed;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.moveSpeed;
        }

        return -1f;
    }

    string BuildTargetInfo(Transform target)
    {
        WorldStatItemPickup pickup =
            target.GetComponent<WorldStatItemPickup>();

        if (pickup != null &&
            pickup.item != null)
        {
            return BuildWorldItemInfo(pickup);
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return BuildMonsterInfo(monster);
        }

        if (false &&
            IsNpcTarget(target) &&
            !HasHeavenDaoPower(HeavenDaoPower.ViewBasicNpcInfo))
        {
            return NpcText.Get("dialogue", "heavenDaoBasicLocked");
        }

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(NpcText.Label("age") + ": " + GetTargetAge(target));
        builder.AppendLine(NpcText.Label("lifespan") + ": " + GetTargetLifespan(target));
        builder.AppendLine(NpcText.Label("job") + ": " + GetTargetJob(target));
        builder.AppendLine(NpcText.Label("maritalStatus") + ": " + GetTargetMarriageStatus(target));
        builder.AppendLine(NpcText.Label("attack") + ": " + FormatMaybeInt(GetTargetAttack(target)));
        builder.AppendLine(NpcText.Label("defense") + ": " + FormatMaybeInt(GetTargetDefense(target)));
        builder.AppendLine(NpcText.Label("speed") + ": " + FormatMaybeFloat(GetTargetMoveSpeed(target)));

        string manuals =
            BuildManualStudyText(target);

        if (!string.IsNullOrEmpty(manuals))
        {
            builder.AppendLine(NpcText.Label("manual") + ":");
            builder.Append(manuals);
        }

        string action = GetTargetAction(target);
        if (!string.IsNullOrEmpty(action))
        {
            builder.AppendLine(NpcText.Label("action") + ": " + action);
        }

        return builder.ToString().TrimEnd();
    }


    string BuildMonsterInfo(MonsterAI monster)
    {
        if (monster == null)
        {
            return "";
        }

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(NpcText.Label("type") + ": " + NpcText.Get("entityTypes", "monster"));
        builder.AppendLine(NpcText.Label("level") + ": " + Mathf.Max(1, monster.beastLevel));
        builder.AppendLine(NpcText.Label("damage") + ": " + FormatMaybeInt(GetTargetAttack(monster.transform)));
        builder.AppendLine(NpcText.Label("defense") + ": " + FormatMaybeInt(GetTargetDefense(monster.transform)));
        builder.AppendLine(NpcText.Label("speed") + ": " + FormatMaybeFloat(GetTargetMoveSpeed(monster.transform)));
        builder.AppendLine(NpcText.Label("loot") + ": " + GetMonsterLootText(monster));
        builder.AppendLine(NpcText.Label("action") + ": " + GetTargetAction(monster.transform));

        return builder.ToString().TrimEnd();
    }

    bool IsNpcTarget(Transform target)
    {
        return target != null &&
            (target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<SmartNpcAI>() != null ||
            target.GetComponent<NpcData>() != null);
    }

    bool IsMonsterTarget(Transform target)
    {
        return target != null &&
            (target.GetComponent<MonsterAI>() != null ||
            target.GetComponent<BicanhBoneMonsterAI>() != null ||
            string.Equals(
                target.gameObject.tag,
                "Monster",
                StringComparison.OrdinalIgnoreCase));
    }

    bool HasHeavenDaoPower(HeavenDaoPower power)
    {
        return HeavenDaoSystem.Instance != null &&
            HeavenDaoSystem.Instance.HasPower(power);
    }

    string GetMonsterLootText(MonsterAI monster)
    {
        if (monster == null ||
            !monster.dropLootOnDeath ||
            monster.lootDropChance <= 0f)
        {
            return UiText.Get("touchSelect", "monsterLootNone");
        }

        StatItemData loot =
            monster.GetDeathLoot();

        if (loot == null)
        {
            return UiText.Get("touchSelect", "monsterLootUnset");
        }

        int amount =
            Mathf.Max(1, monster.lootAmount);

        if (amount <= 1)
        {
            return ItemText.Name(loot);
        }

        return ItemText.Name(loot) + " x" + amount;
    }

    string BuildWorldItemInfo(WorldStatItemPickup pickup)
    {
        if (pickup == null ||
            pickup.item == null)
        {
            return "";
        }

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(NpcText.Label("name") + ": " + ItemText.Name(pickup.item));
        builder.Append(BuildWorldItemBodyInfo(pickup));

        return builder.ToString().TrimEnd();
    }

    string BuildWorldItemBodyInfo(WorldStatItemPickup pickup)
    {
        StatItemData item =
            pickup.item;

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            ItemText.Format(
                "detail",
                "typeFormat",
                GetItemTypeText(item.itemType)));
        builder.AppendLine(
            ItemText.Format(
                "detail",
                "gradeFormat",
                GetItemGradeText(item.grade)));
        builder.AppendLine(
            ItemText.Format(
                "detail",
                "amountFormat",
                Mathf.Max(0, pickup.amount)));

        string description = ItemText.Description(item);
        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.AppendLine();
            builder.AppendLine(description);
        }

        return builder.ToString().TrimEnd();
    }

    string GetItemTypeText(ItemType itemType)
    {
        return ItemText.Type(itemType);
    }

    string GetItemGradeText(ItemGrade grade)
    {
        return ItemText.Grade(grade);
    }

    string GetTargetAge(Transform target)
    {
        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null &&
            villager.GetAge() > 0)
        {
            return villager.GetAge().ToString();
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null &&
            smartNpc.GetAge() > 0)
        {
            return smartNpc.GetAge().ToString();
        }

        return "-";
    }

    string GetTargetLifespan(Transform target)
    {
        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return Mathf.Max(1, monster.beastLevel).ToString();
        }

        if (TryGetTargetLifespanValues(target, out int currentLifespan, out int maxLifespan))
        {
            return currentLifespan + " / " + maxLifespan;
        }

        return "-";
    }

    bool TryGetTargetLifespanValues(
        Transform target,
        out int currentLifespan,
        out int maxLifespan)
    {
        currentLifespan = 0;
        maxLifespan = 0;

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            maxLifespan = Mathf.Max(1, villager.GetLifespan());
            currentLifespan = Mathf.Max(0, villager.GetAge());
            return true;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            maxLifespan = Mathf.Max(1, smartNpc.GetLifespan());
            currentLifespan = Mathf.Max(0, smartNpc.GetAge());
            return true;
        }

        return false;
    }

    string GetTargetJob(Transform target)
    {
        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return NpcText.Get("entityTypes", "monster");
        }

        NpcSpecialProfession profession =
            target.GetComponent<NpcSpecialProfession>();

        if (profession != null &&
            !string.IsNullOrEmpty(profession.professionName))
        {
            return profession.professionName;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        return villager != null
            ? GetVillagerJobText(villager.job)
            : UiText.Get("touchSelect", "placeholder");
    }

    string GetVillagerJobText(VillagerJob job)
    {
        switch (job)
        {
            case VillagerJob.Farmer:
                return NpcText.Get("professions", "farmer");
            case VillagerJob.Trader:
                return NpcText.Get("professions", "trader");
            case VillagerJob.Worker:
                return NpcText.Get("professions", "worker");
            case VillagerJob.Guard:
                return NpcText.Get("professions", "guard");
            case VillagerJob.Healer:
                return NpcText.Get("professions", "healer");
            case VillagerJob.Fisher:
                return NpcText.Get("professions", "fisher");
            case VillagerJob.Hunter:
                return NpcText.Get("professions", "hunter");
            case VillagerJob.Alchemist:
                return NpcText.Get("professions", "alchemist");
            case VillagerJob.Blacksmith:
                return NpcText.Get("professions", "blacksmith");
            default:
                return NpcText.Get("professions", "none");
        }
    }

    string GetTargetMarriageStatus(Transform target)
    {
        if (target == null ||
            IsMonsterTarget(target))
        {
            return UiText.Get("touchSelect", "placeholder");
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc == null)
        {
            smartNpc =
                target.GetComponentInParent<SmartNpcAI>();
        }

        NPCIdentity identity =
            target.GetComponent<NPCIdentity>();

        if (identity == null)
        {
            identity =
                target.GetComponentInParent<NPCIdentity>();
        }

        if (identity != null)
        {
            if (smartNpc != null)
            {
                return string.IsNullOrWhiteSpace(identity.spouseId)
                    ? NpcText.Get("maritalStatus", "single")
                    : NpcText.Get("maritalStatus", "daoCompanion");
            }

            return string.IsNullOrWhiteSpace(identity.spouseId)
                ? NpcText.Get("maritalStatus", "single")
                : NpcText.Get("maritalStatus", "married");
        }

        VillagerRelationship relationship =
            target.GetComponent<VillagerRelationship>();

        if (relationship == null)
        {
            relationship =
                target.GetComponentInParent<VillagerRelationship>();
        }

        if (relationship != null)
        {
            return relationship.IsSingle()
                ? NpcText.Get("maritalStatus", "single")
                : NpcText.Get("maritalStatus", "married");
        }

        return NpcText.Get("maritalStatus", "single");
    }

    string BuildHealthText(Transform target)
    {
        int maxHP =
            Mathf.Max(1, GetTargetMaxHP(target));

        int currentHP =
            Mathf.Clamp(GetTargetCurrentHP(target), 0, maxHP);

        return BuildBar(currentHP, maxHP, 12) +
            " " +
            FormatCompactNumber(currentHP) +
            " / " +
            FormatCompactNumber(maxHP) +
            " (" +
            NpcText.HealthStatus(currentHP, maxHP) +
            ")";
    }

    string BuildBar(
        int current,
        int max,
        int width)
    {
        float percent =
            Mathf.Clamp01((float)current / Mathf.Max(1, max));

        int filled =
            Mathf.RoundToInt(width * percent);

        StringBuilder builder =
            new StringBuilder("[");

        for (int i = 0; i < width; i++)
        {
            builder.Append(i < filled ? "#" : "-");
        }

        builder.Append("]");
        return builder.ToString();
    }

    string BuildManualStudyText(Transform target)
    {
        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            return "";
        }

        NpcHideCultivationInfo hideInfo =
            target.GetComponentInParent<NpcHideCultivationInfo>();

        if (hideInfo != null &&
            hideInfo.hideCultivationSkills)
        {
            return "";
        }

        StringBuilder builder =
            new StringBuilder();

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.CongPhap)
            {
                continue;
            }

            builder.Append(" - ");
            builder.Append(ItemText.Name(stack.item));
            builder.Append(": ");
            builder.Append(GetManualMasteryText(stack.mastery));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    string GetManualMasteryText(CultivationManualMastery mastery)
    {
        return ItemText.Mastery(mastery);
    }

    int GetTargetMoney(Transform target)
    {
        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.money;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        return smartNpc != null
            ? smartNpc.money
            : 0;
    }

    string GetInventorySummary(Transform target)
    {
        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            return UiText.Get("touchSelect", "inventoryEmpty");
        }

        int itemKinds = 0;
        int totalAmount = 0;

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            itemKinds++;
            totalAmount += stack.amount;
        }

        if (itemKinds <= 0)
        {
            return UiText.Get("touchSelect", "inventoryEmpty");
        }

        return UiText.Format(
            "touchSelect",
            "inventorySummaryFormat",
            itemKinds,
            totalAmount);
    }

    void HidePanel()
    {
        Transform hiddenTarget = currentTarget;

        currentTarget = null;
        CurrentTarget = null;

        if (cameraController != null &&
            cameraController.followTarget == hiddenTarget)
        {
            cameraController.followTarget = null;
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        SetWorldItemPanelVisible(false);
        SetInfoIconVisible(false);

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.Hide();
        }
    }

    bool IsPointerOverUI()
    {
        Vector2 screenPosition =
            Input.touchCount > 0
            ? Input.GetTouch(0).position
            : (Vector2)Input.mousePosition;

        if (IsScreenPositionInsideKnownUi(screenPosition))
        {
            return true;
        }

        if (EventSystem.current != null)
        {
            if (Input.touchCount > 0)
            {
                return EventSystem.current.IsPointerOverGameObject(
                    Input.GetTouch(0).fingerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        return false;
    }

    bool IsScreenPositionInsideKnownUi(Vector2 screenPosition)
    {
        if (IsScreenPositionInsideRect(
                infoPanel != null
                ? infoPanel.transform as RectTransform
                : null,
                screenPosition) ||
            IsScreenPositionInsideRect(
                worldItemInfoPanel != null
                ? worldItemInfoPanel.transform as RectTransform
                : null,
                screenPosition))
        {
            return true;
        }

        InventoryPanelUI[] inventoryPanels =
            FindObjectsByType<InventoryPanelUI>(
                FindObjectsInactive.Include);

        foreach (InventoryPanelUI panel in inventoryPanels)
        {
            if (panel == null)
            {
                continue;
            }

            if (IsScreenPositionInsideRect(
                    panel.panelRoot != null
                    ? panel.panelRoot.transform as RectTransform
                    : null,
                    screenPosition) ||
                IsScreenPositionInsideRect(
                    panel.itemGridParent as RectTransform,
                    screenPosition) ||
                IsScreenPositionInsideRect(
                    panel.detailPanel != null
                    ? panel.detailPanel.transform as RectTransform
                    : null,
                    screenPosition))
            {
                return true;
            }
        }

        InventoryItemButtonUI[] itemButtons =
            FindObjectsByType<InventoryItemButtonUI>(
                FindObjectsInactive.Include);

        foreach (InventoryItemButtonUI button in itemButtons)
        {
            if (button == null ||
                !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (IsScreenPositionInsideRect(
                    button.transform as RectTransform,
                    screenPosition))
            {
                return true;
            }
        }

        return false;
    }

    bool IsScreenPositionInsideRect(
        RectTransform rect,
        Vector2 screenPosition)
    {
        if (rect == null ||
            !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas canvas =
            rect.GetComponentInParent<Canvas>();

        Camera eventCamera = null;

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera =
                canvas.worldCamera != null
                ? canvas.worldCamera
                : Camera.main;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            screenPosition,
            eventCamera);
    }

    void KeepCameraFollowingCurrentTarget()
    {
        if (cameraController == null ||
            currentTarget == null ||
            infoPanel == null ||
            !infoPanel.activeInHierarchy)
        {
            return;
        }

        if (cameraController.followTarget != currentTarget)
        {
            cameraController.followTarget = currentTarget;
        }
    }

    Vector3 ClampPanelToScreen(
        RectTransform rect,
        Vector3 screenPosition)
    {
        if (rect == null)
        {
            return screenPosition;
        }

        Vector2 rectSize = rect.rect.size;
        Vector3 scale = rect.lossyScale;
        float width = Mathf.Abs(rectSize.x * scale.x);
        float height = Mathf.Abs(rectSize.y * scale.y);
        Vector2 pivot = rect.pivot;
        float padding = Mathf.Max(0f, panelScreenPadding);

        float minX = padding + width * pivot.x;
        float maxX = Screen.width - padding - width * (1f - pivot.x);
        float minY = padding + height * pivot.y;
        float maxY = Screen.height - padding - height * (1f - pivot.y);

        if (maxX < minX)
        {
            screenPosition.x = Screen.width * 0.5f;
        }
        else
        {
            screenPosition.x = Mathf.Clamp(screenPosition.x, minX, maxX);
        }

        if (maxY < minY)
        {
            screenPosition.y = Screen.height * 0.5f;
        }
        else
        {
            screenPosition.y = Mathf.Clamp(screenPosition.y, minY, maxY);
        }

        return screenPosition;
    }

    void ClampNpcInventoryPanelToScreen()
    {
        if (npcInventoryPanel == null ||
            npcInventoryPanel.panelRoot == null ||
            !npcInventoryPanel.panelRoot.activeInHierarchy)
        {
            return;
        }

        RectTransform inventoryRect =
            npcInventoryPanel.panelRoot.transform as RectTransform;

        if (inventoryRect == null)
        {
            return;
        }

        inventoryRect.position =
            ClampPanelToScreen(inventoryRect, inventoryRect.position);
    }
    void UpdatePanelPosition()
    {
        if (currentTarget == null)
        {
            return;
        }

        bool isWorldItem = IsWorldItemTarget(currentTarget);

        if (!isWorldItem)
        {
            KeepCameraFollowingCurrentTarget();
        }

        if (isWorldItem)
        {
            if (worldItemPanelRect == null)
            {
                AutoFindWorldItemPanelReferences();
            }

            if (worldItemPanelRect == null)
            {
                return;
            }

            ShowWorldItemInfo(currentTarget);
        }
        else
        {
            if (panelRect == null)
            {
                return;
            }

            RefreshTargetInfo(currentTarget);
        }

        if (npcInventoryPanel != null)
        {
            // Inventory UI rebuilds item buttons, so do not refresh it every frame.
            // It refreshes when opened and when the selected inventory changes.
        }

        Vector3 screenPos =
            forcePanelToScreenCenter
            ? new Vector3(
                Screen.width * 0.5f + centeredPanelOffset.x,
                Screen.height * 0.5f + centeredPanelOffset.y,
                0f)
            : cam.WorldToScreenPoint(
                currentTarget.position +
                panelOffset);

        if (isWorldItem)
        {
            if (!forcePanelToScreenCenter)
            {
                screenPos =
                    new Vector3(
                        Screen.width * 0.5f,
                        Screen.height * 0.5f,
                        0f);
            }

            worldItemPanelRect.position =
                ClampPanelToScreen(worldItemPanelRect, screenPos);
        }
        else
        {
            panelRect.position =
                ClampPanelToScreen(panelRect, screenPos);

            ClampNpcInventoryPanelToScreen();
        }
    }
}


