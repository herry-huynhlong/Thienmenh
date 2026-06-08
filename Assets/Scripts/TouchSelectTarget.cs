using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

    public float worldItemTextLeftPadding = 88f;

    [Header("Character Portrait")]
    public Vector2 characterPortraitSize =
        new Vector2(96f, 96f);

    public Vector2 characterPortraitOffset =
        new Vector2(16f, -42f);

    public float characterTextLeftPadding = 112f;

    public bool autoUseCharacterSprite;

    [Header("Panel Follow")]
    public Vector3 panelOffset =
        new Vector3(0, 2f, 0);

    public float panelScreenPadding = 12f;

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
                    FindObjectOfType<MobileCameraController>(true);
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
                FindObjectOfType<NpcInventoryPanelUI>(true);
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

        if (infoText != null &&
            currentTarget != null)
        {
            infoText.text =
                BuildTargetInfo(currentTarget);
        }

        UpdateInfoIcon(currentTarget);

        SetInfoContentVisible(true);
        SetInventoryContentVisible(false);

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.SetContentVisible(false);
        }
    }

    public void ShowInventoryTab()
    {
        if (IsNpcTarget(currentTarget) &&
            !HasHeavenDaoPower(HeavenDaoPower.ViewBasicNpcInfo))
        {
            showingInventory = false;

            SetInventoryContentVisible(false);
            SetInfoIconVisible(false);
            SetInfoContentVisible(true);

            if (infoText != null)
            {
                infoText.text = "Thiên Đạo chưa đủ Chưởng Khống.\nCần 5% để xem kho Tu sĩ.";
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

        if (infoButton == null)
        {
            infoButton =
                FindButtonByName(
                    infoPanel.transform,
                    "InfoButton",
                    "Thong Tin",
                    "Th\u00F4ng Tin");
        }

        if (infoText == null &&
            infoButton != null)
        {
            Transform buttonText =
                FindChildByName(infoButton.transform, "Th\u00F4ng Tin");

            if (buttonText == null)
            {
                buttonText =
                    FindChildByName(infoButton.transform, "Th\u00F4ng Tin");
            }

            if (buttonText == null)
            {
                buttonText =
                    FindChildByName(infoButton.transform, "Th\u00F4ng Tin");
            }

            if (buttonText != null)
            {
                infoText = buttonText.GetComponent<TMP_Text>();
            }

            if (infoText == null)
            {
                infoText =
                    infoButton.GetComponentInChildren<TMP_Text>(true);
            }
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
                FindChildByName(infoPanel.transform, "InfoIcon");

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

        EnsureInfoIcon();
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

        if (infoText != null)
        {
            infoText.gameObject.SetActive(visible);
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
                FindObjectsOfType<RectTransform>(true);

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
        if (target == null ||
            target.GetComponent<MonsterAI>() != null)
        {
            return false;
        }

        if (!HasHeavenDaoPower(HeavenDaoPower.ViewBasicNpcInfo))
        {
            return false;
        }

        return target.GetComponent<SmartNpcAI>() != null ||
            target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<ItemInventory>() != null;
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
            worldItemNameText.text = pickup.item.itemName;
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

        Sprite icon =
            isWorldItem
            ? GetPickupIcon(pickup)
            : GetCharacterIcon(target);

        if (infoIcon == null ||
            icon == null)
        {
            SetInfoIconVisible(false);
            return;
        }

        infoIcon.sprite = icon;
        ConfigureInfoIconLayout(isWorldItem);
        SetInfoIconVisible(true);
        ApplyInfoTextMargin(
            isWorldItem
            ? worldItemTextLeftPadding
            : characterTextLeftPadding);
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

    Sprite GetCharacterIcon(Transform target)
    {
        if (target == null)
        {
            return null;
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

        if (!autoUseCharacterSprite)
        {
            return null;
        }

        SpriteRenderer bestRenderer =
            null;

        float bestArea =
            -1f;

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

        return bestRenderer != null
            ? bestRenderer.sprite
            : null;
    }

    void ConfigureInfoIconLayout(bool isWorldItem)
    {
        if (infoIcon == null)
        {
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

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = target;
            }
        }

        return closestTarget;
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
            return characterStats.GetRealmText() +
                " " +
                characterStats.realmStage;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return NpcText.Realm(smartNpc.realm) + " " + smartNpc.realmStage;
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
            return NpcText.Realm(monster.entityProfile.stats.realm) +
                " " +
                monster.entityProfile.stats.realmStage;
        }

        return "Y\u00EAu Th\u00FA";
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
            return villager.currentAction;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.currentAction;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return NpcText.Label("damage") + ": " + monster.damage;
        }

        return "";
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

        if (IsNpcTarget(target) &&
            !HasHeavenDaoPower(HeavenDaoPower.ViewBasicNpcInfo))
        {
            return "Thiên Đạo chưa đủ Chưởng Khống.\nCần 5% để xem thông tin Tu sĩ.";
        }

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(NpcText.Label("name") + ": " + GetTargetName(target));
        builder.AppendLine(NpcText.Label("age") + ": " + GetTargetAge(target));
        builder.AppendLine(NpcText.Label("lifespan") + ": " + GetTargetLifespan(target));
        builder.AppendLine(NpcText.Label("job") + ": " + GetTargetJob(target));
        builder.AppendLine(NpcText.Label("realm") + ": " + GetTargetRealm(target));
        builder.AppendLine(NpcText.Label("health") + ": " + BuildHealthText(target));

        string manuals =
            BuildManualStudyText(target);

        if (!string.IsNullOrEmpty(manuals))
        {
            builder.AppendLine(NpcText.Label("manual") + ":");
            builder.Append(manuals);
        }

        builder.AppendLine(NpcText.Label("action") + ": " + GetTargetAction(target));

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

        builder.AppendLine(NpcText.Label("name") + ": " + monster.monsterName);
        builder.AppendLine(NpcText.Label("type") + ": " + NpcText.Get("entityTypes", "monster", "Yêu Thú"));
        builder.AppendLine(NpcText.Label("level") + ": " + Mathf.Max(1, monster.beastLevel));
        builder.AppendLine(NpcText.Label("realm") + ": " + GetTargetRealm(monster.transform));
        builder.AppendLine(NpcText.Label("health") + ": " + BuildHealthText(monster.transform));
        builder.AppendLine(NpcText.Label("loot") + ": " + GetMonsterLootText(monster));

        return builder.ToString().TrimEnd();
    }

    bool IsNpcTarget(Transform target)
    {
        return target != null &&
            (target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<SmartNpcAI>() != null ||
            target.GetComponent<NpcData>() != null);
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
            return "Kh\u00F4ng";
        }

        StatItemData loot =
            monster.GetDeathLoot();

        if (loot == null)
        {
            return "Ch\u01B0a g\u1EAFn";
        }

        int amount =
            Mathf.Max(1, monster.lootAmount);

        if (amount <= 1)
        {
            return loot.itemName;
        }

        return loot.itemName + " x" + amount;
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

        builder.AppendLine(NpcText.Label("name") + ": " + pickup.item.itemName);
        builder.Append(BuildWorldItemBodyInfo(pickup));

        return builder.ToString().TrimEnd();
    }

    string BuildWorldItemBodyInfo(WorldStatItemPickup pickup)
    {
        StatItemData item =
            pickup.item;

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(NpcText.Label("type") + ": " + GetItemTypeText(item.itemType));
        builder.AppendLine(NpcText.Label("grade") + ": " + GetItemGradeText(item.grade));
        builder.AppendLine(NpcText.Label("amount") + ": " + Mathf.Max(0, pickup.amount));

        if (!string.IsNullOrWhiteSpace(item.description))
        {
            builder.AppendLine();
            builder.AppendLine(item.description);
        }

        return builder.ToString().TrimEnd();
    }

    string GetItemTypeText(ItemType itemType)
    {
        return NpcText.ItemType(itemType);
    }

    string GetItemGradeText(ItemGrade grade)
    {
        return NpcText.ItemGrade(grade);
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
        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.GetLifespan().ToString();
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.GetLifespan().ToString();
        }

        return "-";
    }

    string GetTargetJob(Transform target)
    {
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
            ? villager.job.ToString()
            : "-";
    }

    string BuildHealthText(Transform target)
    {
        int maxHP =
            Mathf.Max(1, GetTargetMaxHP(target));

        int currentHP =
            Mathf.Clamp(GetTargetCurrentHP(target), 0, maxHP);

        return BuildBar(currentHP, maxHP, 12) +
            " " +
            currentHP +
            " / " +
            maxHP +
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
            builder.Append(stack.item.itemName);
            builder.Append(": ");
            builder.Append(GetManualMasteryText(stack.mastery));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    string GetManualMasteryText(CultivationManualMastery mastery)
    {
        switch (mastery)
        {
            case CultivationManualMastery.TieuThanh:
                return "Ti\u1EC3u Th\u00E0nh";
            case CultivationManualMastery.TrungThanh:
                return "Trung Th\u00E0nh";
            case CultivationManualMastery.DaiThanh:
                return "\u0110\u1EA1i Th\u00E0nh";
            default:
                return "Ch\u01B0a h\u1ECDc";
        }
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
            return "Tr\u1ED1ng";
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
            return "Tr\u1ED1ng";
        }

        return itemKinds + " lo\u1EA1i / " + totalAmount + " m\u00F3n";
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
            FindObjectsOfType<InventoryPanelUI>(true);

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
            FindObjectsOfType<InventoryItemButtonUI>(true);

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

            if (infoText != null)
            {
                infoText.text =
                    BuildTargetInfo(currentTarget);
            }
        }

        if (npcInventoryPanel != null)
        {
            // Inventory UI rebuilds item buttons, so do not refresh it every frame.
            // It refreshes when opened and when the selected inventory changes.
        }

        Vector3 screenPos =
            cam.WorldToScreenPoint(
                currentTarget.position +
                panelOffset);

        if (isWorldItem)
        {
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


