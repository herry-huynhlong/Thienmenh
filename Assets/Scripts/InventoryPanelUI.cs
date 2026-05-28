using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryPanelUI : MonoBehaviour
{
    [Header("Data")]
    public ItemInventory inventory;
    public bool autoFindReferences = true;

    [Header("Panel")]
    public GameObject panelRoot;
    public bool closeOnStart = true;
    public bool alwaysVisible = false;
    public bool bringToFrontOnOpen = true;
    public bool readOnly;

    [Header("Items")]
    public Transform itemGridParent;
    public InventoryItemButtonUI itemButtonPrefab;
    public bool autoConfigureGrid = true;
    public bool keepTemplateButtonVisible = false;
    public bool preserveTemplateButtonPosition = false;
    public Vector2 itemCellSize =
        new Vector2(96f, 120f);
    public Vector2 itemSpacing =
        new Vector2(8f, 8f);
    public Vector2 itemGridPadding =
        new Vector2(16f, 18f);
    public int itemColumns = 4;

    [Header("Detail")]
    public GameObject detailPanel;
    public Image detailIcon;
    public TMP_Text detailNameText;
    public TMP_Text detailAmountText;
    public TMP_Text detailTypeText;
    public TMP_Text detailGradeText;
    public TMP_Text detailTargetsText;
    public TMP_Text detailPriceText;
    public TMP_Text detailDescriptionText;
    public TMP_Text detailStatsText;
    public Button useButton;
    public Button giveToSelectedNpcButton;

    readonly List<InventoryItemButtonUI> spawnedButtons =
        new List<InventoryItemButtonUI>();

    int selectedItemIndex = -1;
    RectTransform templateRect;
    Vector2 templateAnchorMin;
    Vector2 templateAnchorMax;
    Vector2 templatePivot;
    Vector2 templateAnchoredPosition;
    Vector2 templateSizeDelta;
    Vector3 templateLocalScale;
    Quaternion templateLocalRotation;

    public bool IsOpen
    {
        get
        {
            return panelRoot != null &&
                panelRoot.activeInHierarchy;
        }
    }

    void Awake()
    {
        AutoFindMissingReferences();

        if (useButton != null)
        {
            useButton.onClick.RemoveAllListeners();
            useButton.onClick.AddListener(UseSelectedItem);
        }

        if (giveToSelectedNpcButton != null)
        {
            giveToSelectedNpcButton.onClick.RemoveAllListeners();
            giveToSelectedNpcButton.onClick.AddListener(GiveSelectedItemToSelectedNpc);
        }

        CacheTemplateTransform();
    }

    void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnChanged += Refresh;
        }

        Refresh();
    }

    void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= Refresh;
        }
    }

    void Start()
    {
        AutoFindMissingReferences();

        if (closeOnStart)
        {
            Close();
        }
    }

    void Update()
    {
        if (IsPanelVisible())
        {
            if (Input.GetMouseButtonDown(0))
            {
                TrySelectItemAtScreenPosition(Input.mousePosition);
            }

            if (Input.touchCount > 0 &&
                Input.GetTouch(0).phase == TouchPhase.Began)
            {
                TrySelectItemAtScreenPosition(
                    Input.GetTouch(0).position);
            }
        }

    }

    public void Open()
    {
        AutoFindMissingReferences();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);

            if (bringToFrontOnOpen)
            {
                panelRoot.transform.SetAsLastSibling();
            }
        }

        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void Toggle()
    {
        AutoFindMissingReferences();

        if (panelRoot == null)
        {
            return;
        }

        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Refresh()
    {
        selectedItemIndex = -1;
        ClearDetail();
        RebuildItemGrid();
    }

    public void SelectItem(int itemIndex)
    {
        selectedItemIndex = itemIndex;

        ItemStack stack =
            inventory.GetStack(itemIndex);

        if (stack == null ||
            stack.item == null)
        {
            ClearDetail();
            return;
        }

        if (detailPanel != null)
        {
            detailPanel.SetActive(true);
        }

        if (detailIcon != null)
        {
            detailIcon.sprite = stack.item.icon;
            detailIcon.enabled = stack.item.icon != null;
        }

        if (detailNameText != null)
        {
            detailNameText.text = stack.item.itemName;
        }

        if (detailAmountText != null)
        {
            detailAmountText.text = "x" + stack.amount;
        }

        if (detailTypeText != null)
        {
            detailTypeText.text = GetTypeText(stack.item.itemType);
        }

        if (detailGradeText != null)
        {
            detailGradeText.text = GetGradeText(stack.item.grade);
        }

        if (detailTargetsText != null)
        {
            detailTargetsText.text = GetTargetText(stack.item.validTargets);
        }

        if (detailPriceText != null)
        {
            detailPriceText.text = stack.item.price + " LT";
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = stack.item.description;
        }

        if (detailStatsText != null)
        {
            detailStatsText.text = BuildStatsText(stack.item);
        }

        if (useButton != null)
        {
            useButton.interactable = !readOnly;
        }

        if (giveToSelectedNpcButton != null)
        {
            giveToSelectedNpcButton.interactable = !readOnly;
        }
    }

    public void SelectItemButton(int itemIndex)
    {
        if (itemIndex < 0)
        {
            selectedItemIndex = -1;
            ClearDetail();
            return;
        }

        SelectItem(itemIndex);
    }

    bool IsPanelVisible()
    {
        if (panelRoot == null)
        {
            return gameObject.activeInHierarchy;
        }

        return panelRoot.activeInHierarchy;
    }

    void TrySelectItemAtScreenPosition(Vector2 screenPosition)
    {
        Camera eventCamera =
            GetEventCamera();

        for (int i = spawnedButtons.Count - 1; i >= 0; i--)
        {
            InventoryItemButtonUI button =
                spawnedButtons[i];

            if (button == null ||
                !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            RectTransform rect =
                button.GetComponent<RectTransform>();

            if (rect == null)
            {
                continue;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    rect,
                    screenPosition,
                    eventCamera))
            {
                continue;
            }

            SelectItemButton(button.ItemIndex);
            return;
        }

        if (selectedItemIndex >= 0 &&
            !IsScreenPositionInsideDetailPanel(
                screenPosition,
                eventCamera))
        {
            selectedItemIndex = -1;
            ClearDetail();
        }
    }

    bool IsScreenPositionInsideDetailPanel(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (detailPanel == null ||
            !detailPanel.activeInHierarchy)
        {
            return false;
        }

        RectTransform detailRect =
            detailPanel.GetComponent<RectTransform>();

        if (detailRect == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            detailRect,
            screenPosition,
            eventCamera);
    }

    Camera GetEventCamera()
    {
        Canvas canvas =
            GetComponentInParent<Canvas>();

        if (canvas == null ||
            canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (canvas.worldCamera != null)
        {
            return canvas.worldCamera;
        }

        return Camera.main;
    }

    public void UseSelectedItem()
    {
        if (readOnly)
        {
            return;
        }

        if (inventory == null ||
            selectedItemIndex < 0)
        {
            return;
        }

        ItemStack stack =
            inventory.GetStack(selectedItemIndex);

        if (stack == null ||
            stack.item == null)
        {
            return;
        }

        Transform selectedTarget =
            TouchSelectTarget.CurrentTarget;

        GameObject target =
            selectedTarget != null
            ? selectedTarget.gameObject
            : null;

        if (target == null &&
            stack.item.CanUseOn(inventory.gameObject))
        {
            target = inventory.gameObject;
        }

        if (target == null)
        {
            Debug.Log("Chua chon muc tieu phu hop de dung vat pham.");
            return;
        }

        inventory.UseItemOn(
            selectedItemIndex,
            target);
    }

    public void GiveSelectedItemToSelectedNpc()
    {
        if (readOnly ||
            inventory == null ||
            selectedItemIndex < 0)
        {
            return;
        }

        ItemStack stack =
            inventory.GetStack(selectedItemIndex);

        if (stack == null ||
            stack.item == null ||
            stack.amount <= 0)
        {
            return;
        }

        Transform selectedTarget =
            TouchSelectTarget.CurrentTarget;

        if (selectedTarget == null ||
            !CanReceiveItem(selectedTarget))
        {
            Debug.Log("Chua chon NPC de phat vat pham.");
            return;
        }

        ItemInventory targetInventory =
            selectedTarget.GetComponent<ItemInventory>();

        if (targetInventory == null)
        {
            targetInventory =
                selectedTarget.gameObject.AddComponent<ItemInventory>();
        }

        StatItemData item = stack.item;

        if (!inventory.RemoveItem(item, 1))
        {
            return;
        }

        targetInventory.AddItem(item, 1);
        Refresh();
    }

    bool CanReceiveItem(Transform target)
    {
        return target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<SmartNpcAI>() != null;
    }

    void RebuildItemGrid()
    {
        if (inventory == null ||
            itemGridParent == null ||
            itemButtonPrefab == null)
        {
            return;
        }

        if (autoConfigureGrid)
        {
            ConfigureItemGrid();
        }

        spawnedButtons.Clear();

        for (int i = itemGridParent.childCount - 1; i >= 0; i--)
        {
            Transform child =
                itemGridParent.GetChild(i);

            if (child == itemButtonPrefab.transform)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        itemButtonPrefab.gameObject.SetActive(false);

        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack =
                inventory.items[i];

            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            InventoryItemButtonUI button =
                Instantiate(
                    itemButtonPrefab,
                    itemGridParent);

            button.gameObject.SetActive(true);
            ResetItemButtonTransform(button, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                button.GetComponent<RectTransform>());

            button.Setup(this, i, stack);
            spawnedButtons.Add(button);
        }
    }

    void ConfigureItemGrid()
    {
        RectTransform contentRect =
            itemGridParent as RectTransform;

        if (contentRect != null)
        {
            contentRect.anchorMin =
                new Vector2(0f, 1f);

            contentRect.anchorMax =
                new Vector2(1f, 1f);

            contentRect.pivot =
                new Vector2(0f, 1f);

            contentRect.anchoredPosition =
                Vector2.zero;
        }

        GridLayoutGroup grid =
            itemGridParent.GetComponent<GridLayoutGroup>();

        if (grid == null)
        {
            grid =
                itemGridParent.gameObject.AddComponent<GridLayoutGroup>();
        }

        grid.cellSize = itemCellSize;
        grid.spacing = itemSpacing;
        grid.padding =
            new RectOffset(
                Mathf.RoundToInt(itemGridPadding.x),
                Mathf.RoundToInt(itemGridPadding.x),
                Mathf.RoundToInt(itemGridPadding.y),
                Mathf.RoundToInt(itemGridPadding.y));
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount =
            Mathf.Max(1, itemColumns);

        ContentSizeFitter fitter =
            itemGridParent.GetComponent<ContentSizeFitter>();

        if (fitter == null)
        {
            fitter =
                itemGridParent.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit =
            ContentSizeFitter.FitMode.Unconstrained;

        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            itemGridParent as RectTransform);
    }

    void ResetItemButtonTransform(
        InventoryItemButtonUI button,
        bool restoreTemplatePosition)
    {
        RectTransform rect =
            button.GetComponent<RectTransform>();

        if (rect == null)
        {
            return;
        }

        if (restoreTemplatePosition)
        {
            RestoreTemplateTransform(rect);
            return;
        }

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.sizeDelta = itemCellSize;
        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            itemCellSize.x);

        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            itemCellSize.y);
    }

    void CacheTemplateTransform()
    {
        if (itemButtonPrefab == null)
        {
            return;
        }

        templateRect =
            itemButtonPrefab.GetComponent<RectTransform>();

        if (templateRect == null)
        {
            return;
        }

        templateAnchorMin =
            templateRect.anchorMin;

        templateAnchorMax =
            templateRect.anchorMax;

        templatePivot =
            templateRect.pivot;

        templateAnchoredPosition =
            templateRect.anchoredPosition;

        templateSizeDelta =
            templateRect.sizeDelta;

        templateLocalScale =
            templateRect.localScale;

        templateLocalRotation =
            templateRect.localRotation;
    }

    void RestoreTemplateTransform(RectTransform rect)
    {
        if (templateRect == null)
        {
            CacheTemplateTransform();
        }

        rect.anchorMin = templateAnchorMin;
        rect.anchorMax = templateAnchorMax;
        rect.pivot = templatePivot;
        rect.anchoredPosition = templateAnchoredPosition;
        rect.sizeDelta = templateSizeDelta;
        rect.localScale = templateLocalScale;
        rect.localRotation = templateLocalRotation;
    }

    void ClearDetail()
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }

        SetText(detailNameText, "");
        SetText(detailAmountText, "");
        SetText(detailTypeText, "");
        SetText(detailGradeText, "");
        SetText(detailTargetsText, "");
        SetText(detailPriceText, "");
        SetText(detailDescriptionText, "");
        SetText(detailStatsText, "");

        if (useButton != null)
        {
            useButton.interactable = false;
        }

        if (giveToSelectedNpcButton != null)
        {
            giveToSelectedNpcButton.interactable = false;
        }
    }

    void AutoFindMissingReferences()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (panelRoot == gameObject)
        {
            Transform childBaloPanel =
                FindChildByName(transform, "BaloPanel");

            if (childBaloPanel != null)
            {
                panelRoot = childBaloPanel.gameObject;
            }
        }

        if (panelRoot == null)
        {
            if (name == "Balo" ||
                name == "Inventory" ||
                name == "InventoryPanel")
            {
                Transform baloPanel =
                    FindChildByName(transform, "BaloPanel");

                if (baloPanel != null)
                {
                    panelRoot = baloPanel.gameObject;
                }
            }

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }
        }

        if (inventory == null)
        {
            inventory =
                GetComponent<ItemInventory>();
        }

        if (itemGridParent == null)
        {
            Transform content =
                transform.Find("ItemScrollView/Viewport/Content");

            if (content == null)
            {
                content = FindChildByName(transform, "Content");
            }

            itemGridParent = content;
        }

        if (itemButtonPrefab == null &&
            itemGridParent != null)
        {
            itemButtonPrefab =
                itemGridParent
                    .GetComponentInChildren<InventoryItemButtonUI>(true);
        }

        if (detailPanel == null)
        {
            Transform detail =
                transform.Find("DetailPanel");

            if (detail == null)
            {
                detail = FindChildByName(transform, "DetailPanel");
            }

            if (detail != null)
            {
                detailPanel = detail.gameObject;
            }
        }

        if (detailPanel != null)
        {
            Transform detailTransform =
                detailPanel.transform;

            if (detailNameText == null)
            {
                detailNameText =
                    FindTextByName(detailTransform, "DetailNameText");
            }

            if (detailTypeText == null)
            {
                detailTypeText =
                    FindTextByName(detailTransform, "DetailTypeText");
            }

            if (detailGradeText == null)
            {
                detailGradeText =
                    FindTextByName(detailTransform, "DetailGradeText");
            }

            if (detailTargetsText == null)
            {
                detailTargetsText =
                    FindTextByName(detailTransform, "DetailTargetsText");
            }

            if (detailPriceText == null)
            {
                detailPriceText =
                    FindTextByName(detailTransform, "DetailPriceText");
            }

            if (detailDescriptionText == null)
            {
                detailDescriptionText =
                    FindTextByName(detailTransform, "DetailDescriptionText");
            }

            if (detailStatsText == null)
            {
                detailStatsText =
                    FindTextByName(detailTransform, "DetailStatsText");
            }
        }

        if (giveToSelectedNpcButton == null)
        {
            giveToSelectedNpcButton =
                FindButtonByName(transform, "phat_cho_Player");
        }

        if (giveToSelectedNpcButton == null)
        {
            giveToSelectedNpcButton =
                FindButtonByName(transform, "Phat_cho_player");
        }

        if (giveToSelectedNpcButton == null)
        {
            giveToSelectedNpcButton =
                FindButtonByName(transform, "phat_cho_player");
        }
    }

    Button FindButtonByName(
        Transform parent,
        string childName)
    {
        Transform child =
            FindChildByName(parent, childName);

        if (child == null)
        {
            return null;
        }

        Button button =
            child.GetComponent<Button>();

        if (button != null)
        {
            return button;
        }

        return child.GetComponentInChildren<Button>(true);
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

    TMP_Text FindTextByName(
        Transform parent,
        string childName)
    {
        Transform child =
            FindChildByName(parent, childName);

        if (child == null)
        {
            return null;
        }

        return child.GetComponent<TMP_Text>();
    }

    void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    string BuildStatsText(StatItemData item)
    {
        StringBuilder builder =
            new StringBuilder();

        if (item.hpBonus != 0)
        {
            builder.AppendLine("Mau +" + item.hpBonus);
        }

        if (item.cultivationBonus != 0)
        {
            builder.AppendLine("Tu vi +" + item.cultivationBonus);
        }

        if (item.breakthroughRealm)
        {
            builder.AppendLine("Dot pha canh gioi");
        }

        if (item.damageBonus != 0)
        {
            builder.AppendLine("Dame +" + item.damageBonus);
        }

        if (item.armorBonus != 0)
        {
            builder.AppendLine("Giap +" + item.armorBonus);
        }

        if (item.effectResistanceBonus != 0)
        {
            builder.AppendLine("Khang hieu ung +" + item.effectResistanceBonus);
        }

        if (item.isTemporary)
        {
            builder.AppendLine("Thoi gian: " + item.duration + "s");
        }

        return builder.ToString();
    }

    string GetTypeText(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.DanDuoc:
                return "Dan Duoc";
            case ItemType.PhapBao:
                return "Phap Bao";
            case ItemType.CongPhap:
                return "Cong Phap";
            case ItemType.VatLieu:
                return "Vat Lieu";
            default:
                return itemType.ToString();
        }
    }

    string GetGradeText(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return "Ha";
            case ItemGrade.Trung:
                return "Trung";
            case ItemGrade.Thuong:
                return "Thuong";
            case ItemGrade.Tien:
                return "Tien";
            default:
                return grade.ToString();
        }
    }

    string GetTargetText(ItemTargetType targetType)
    {
        if (targetType == ItemTargetType.All)
        {
            return "Tat Ca";
        }

        return targetType.ToString();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        AutoFindMissingReferences();
        CacheTemplateTransform();
    }
#endif
}
