using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryPanelUI : MonoBehaviour
{
    enum InventoryCategoryFilter
    {
        All,
        TrangBi,
        DanDuoc,
        PhapBao,
        CongPhap,
        VatLieu
    }

    [Header("Data")]
    public ItemInventory inventory;
    public bool autoFindReferences = true;

    [Header("Panel")]
    public GameObject panelRoot;
    public bool closeOnStart = true;
    public bool alwaysVisible = false;
    public bool bringToFrontOnOpen = true;
    public bool closeWhenClickOutside = true;
    public bool readOnly;
    public StatItemData selectedItem;

    public ItemInventory currentNpcInventory;

    public ItemInventory playerInventory;
    public PlayerWallet playerWallet;

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
    public int itemColumns = 8;

    [Header("Detail")]
    public GameObject detailPanel;
    public Image detailFrameImage;
    public Image detailIcon;
    public TMP_Text detailNameText;
    public TMP_Text detailAmountText;
    public TMP_Text detailAmountLabelText;
    public TMP_Text detailTypeText;
    public TMP_Text detailGradeText;
    public TMP_Text detailTargetsText;
    public TMP_Text detailPriceText;
    public TMP_Text detailPriceLabelText;
    public TMP_Text detailOwnerText;
    public TMP_Text detailOwnerLabelText;
    public TMP_Text detailDescriptionTitleText;
    public TMP_Text detailDescriptionText;
    public TMP_Text detailStatsText;
    public Button useButton;
    public Button giveToSelectedNpcButton;
    public Button heavenGiftButton;

    [Header("Footer")]
    public TMP_Text footerUsedSlotText;
    public TMP_Text footerLinhThachText;
    Transform rightPanelTransform;
    Transform previewPanelTransform;
    Image previewBackgroundImage;
    Image previewGradeFrameImage;
    Image previewItemIcon;
    Image previewBigIcon;
    Image previewMagicCircle;
    Vector3 previewMagicCircleBaseScale = Vector3.one;
    Vector3 previewMagicCircleBaseEuler = Vector3.zero;
    Color previewMagicCircleBaseColor = Color.white;
    bool previewMagicCircleBaseCaptured;
    float previewMagicCirclePulseSeed;
    TMP_Text attackValueText;
    TMP_Text defenseValueText;
    TMP_Text hpValueText;
    TMP_Text speedValueText;
    Button tabAllButton;
    Button tabTrangBiButton;
    Button tabDanDuocButton;
    Button tabPhapBaoButton;
    Button tabCongPhapButton;
    Button tabVatLieuButton;
    InventoryCategoryFilter currentCategoryFilter =
        InventoryCategoryFilter.All;

    readonly List<InventoryItemButtonUI> spawnedButtons =
        new List<InventoryItemButtonUI>();

    int selectedItemIndex = -1;
    ItemInventory subscribedInventory;
    bool walletEventsBound;
    RectTransform templateRect;
    Vector2 templateAnchorMin;
    Vector2 templateAnchorMax;
    Vector2 templatePivot;
    Vector2 templateAnchoredPosition;
    Vector2 templateSizeDelta;
    Vector3 templateLocalScale;
    Quaternion templateLocalRotation;
    bool hasStarted;
    bool disabledBecauseAttachedToBottomMenu;

    bool IsAccidentalBottomMenuAttachment()
    {
        if (!HasAncestorNamed(transform, "menupanel"))
        {
            return false;
        }

        string key = transform.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        return key == "balo" ||
            key == "inventory" ||
            key == "bag";
    }
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
        if (IsAccidentalBottomMenuAttachment())
        {
            disabledBecauseAttachedToBottomMenu = true;
            enabled = false;
            return;
        }

        AutoFindMissingReferences();
        BindActionButtons();
        BindCategoryTabButtons();
        BindWalletEvents();
        previewMagicCirclePulseSeed =
            Mathf.Abs(
                UnityObjectIdUtility.GetRuntimeId(this) * 0.193f);

        CacheTemplateTransform();
    }

    void OnEnable()
    {
        if (disabledBecauseAttachedToBottomMenu)
        {
            return;
        }

        BindInventoryEvents();
        BindWalletEvents();

        if (!hasStarted && closeOnStart && !alwaysVisible)
        {
            return;
        }

        Refresh();
    }

    void OnDisable()
    {
        UnbindInventoryEvents();
        UnbindWalletEvents();
    }

    void Start()
    {
        if (disabledBecauseAttachedToBottomMenu)
        {
            return;
        }

        hasStarted = true;
        AutoFindMissingReferences();
        BindCategoryTabButtons();

        if (ShouldStartClosed())
        {
            Close();
            return;
        }

        Refresh();
    }

    void Update()
    {
        if (disabledBecauseAttachedToBottomMenu)
        {
            return;
        }

        if (IsPanelVisible())
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandlePointerDown(Input.mousePosition);
            }

            if (Input.touchCount > 0 &&
                Input.GetTouch(0).phase == TouchPhase.Began)
            {
                HandlePointerDown(Input.GetTouch(0).position);
            }
        }

        RefreshPreviewMagicCircleRarityVisuals();
    }

    void HandlePointerDown(Vector2 screenPosition)
    {
        Camera eventCamera =
            GetEventCamera();

        if (IsScreenPositionInsideInventoryUi(
                screenPosition,
                eventCamera))
        {
            TrySelectItemAtScreenPosition(screenPosition, eventCamera);
            return;
        }

        if (ShouldCloseFromOutsidePointer(screenPosition, eventCamera))
        {
            Close();
            return;
        }

        TrySelectItemAtScreenPosition(screenPosition, eventCamera);
    }

    public void Open()
    {
        AutoFindMissingReferences();
        BindCategoryTabButtons();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);

            CanvasGroup canvasGroup =
                panelRoot.GetComponent<CanvasGroup>();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (bringToFrontOnOpen ||
                IsPlayerInventoryPanel())
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
        Refresh(false);
    }

    public void Refresh(bool preserveSelection)
    {
        int previousIndex = selectedItemIndex;
        StatItemData previousItem = selectedItem;

        if (!preserveSelection)
        {
            selectedItemIndex = -1;
            selectedItem = null;
            ClearDetail();
            RebuildItemGrid();
            RefreshFooter();
            return;
        }

        RebuildItemGrid();
        RefreshFooter();

        int restoredIndex = FindRestorableItemIndex(previousIndex, previousItem);
        if (restoredIndex >= 0)
        {
            SelectItem(restoredIndex);
            return;
        }

        selectedItemIndex = -1;
        selectedItem = null;
        ClearDetail();
    }

    public void SetInventory(
        ItemInventory newInventory,
        bool refreshNow = true)
    {
        if (inventory == newInventory &&
            subscribedInventory == newInventory)
        {
            if (refreshNow)
            {
                Refresh();
            }

            return;
        }

        UnbindInventoryEvents();
        inventory = newInventory;
        subscribedInventory = null;

        if (isActiveAndEnabled)
        {
            BindInventoryEvents();
        }

        if (refreshNow)
        {
            Refresh();
        }
    }

    void RefreshFooter()
    {
        RefreshFooterUsedSlotText();
        RefreshFooterLinhThachText();
    }

    void RefreshFooterUsedSlotText()
    {
        if (footerUsedSlotText == null)
        {
            return;
        }

        footerUsedSlotText.text =
            "\u00D4 \u0111\u00E3 d\u00F9ng " +
            GetUsedFooterCount();
    }

    void RefreshFooterLinhThachText()
    {
        if (footerLinhThachText == null)
        {
            return;
        }

        if (playerWallet == null)
        {
            playerWallet =
                GetComponent<PlayerWallet>() ??
                FindAnyObjectByType<PlayerWallet>(
                    FindObjectsInactive.Include);
        }

        if (playerWallet == null)
        {
            return;
        }

        footerLinhThachText.text =
            "Linh Th\u1EA1ch : " +
            NpcEconomy.FormatCompactAmount(
                playerWallet.LinhThach);
    }

    int GetUsedFooterCount()
    {
        if (inventory == null ||
            inventory.items == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack = inventory.items[i];
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            count += stack.amount;
        }

        return count;
    }

    int FindRestorableItemIndex(int previousIndex, StatItemData previousItem)
    {
        if (inventory == null ||
            previousItem == null)
        {
            return -1;
        }

        ItemStack previousStack =
            inventory.GetStack(previousIndex);

        if (previousStack != null &&
            previousStack.item == previousItem &&
            previousStack.amount > 0 &&
            MatchesCurrentFilter(previousItem))
        {
            return previousIndex;
        }

        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack = inventory.items[i];
            if (stack != null &&
                stack.item == previousItem &&
                stack.amount > 0 &&
                MatchesCurrentFilter(stack.item))
            {
                return i;
            }
        }

        return -1;
    }

    public void SelectItem(int itemIndex)
    {
        AutoFindMissingReferences();
        BindActionButtons();

        selectedItemIndex = itemIndex;
        selectedItem = null;

        ItemStack stack =
            inventory.GetStack(itemIndex);

        if (stack == null ||
            stack.item == null)
        {
            ClearDetail();
            return;
        }

        selectedItem = stack.item;
        bool usesRightPanelLayout =
            UsesRightPanelLayout();
        bool usesNpcInventoryDetailLayout =
            UsesNpcInventoryDetailLayout();

        if (detailPanel != null)
        {
            detailPanel.SetActive(true);
        }

        ApplyDetailGradeFrame(stack.item);

        if (detailIcon != null)
        {
            detailIcon.sprite = stack.item.icon;
            detailIcon.enabled = stack.item.icon != null;
        }

        if (detailNameText != null)
        {
            detailNameText.text = ItemText.Name(stack.item);
        }

        if (detailAmountText != null)
        {
            detailAmountText.text = usesNpcInventoryDetailLayout
                ? stack.amount.ToString()
                : ItemText.Format("detail", "amountFormat", stack.amount);
        }

        if (detailTypeText != null)
        {
            detailTypeText.text =
                (usesRightPanelLayout &&
                IsDescendantOf(detailTypeText.transform, rightPanelTransform)) ||
                usesNpcInventoryDetailLayout
                ? ItemText.Type(stack.item.itemType)
                : ItemText.Format(
                    "detail",
                    "typeFormat",
                    ItemText.Type(stack.item.itemType));
        }

        if (detailGradeText != null)
        {
            detailGradeText.text =
                (usesRightPanelLayout &&
                IsDescendantOf(detailGradeText.transform, rightPanelTransform)) ||
                usesNpcInventoryDetailLayout
                ? ItemText.Grade(stack.item.grade)
                : ItemText.Format(
                    "detail",
                    "gradeFormat",
                    ItemText.Grade(stack.item.grade));
            ShopPanelUI.ApplyGradeTextStyle(
                detailGradeText,
                stack.item.grade,
                1f);
        }

        if (detailTargetsText != null)
        {
            detailTargetsText.text =
                ItemText.Format(
                    "detail",
                    "targetsFormat",
                    ItemText.Target(stack.item.validTargets));
        }

        if (detailPriceText != null)
        {
            string formattedPrice =
                NpcEconomy.FormatPrice(stack.item);

            detailPriceText.text = usesNpcInventoryDetailLayout
                ? formattedPrice
                : ItemText.Format(
                    "detail",
                    "priceFormat",
                    formattedPrice);
        }

        if (detailOwnerText != null)
        {
            detailOwnerText.text =
                GetInventoryOwnerName();
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = ItemText.Description(stack.item);
        }

        if (detailStatsText != null)
        {
            detailStatsText.text =
                BuildStatsText(stack);
        }

        ApplyPreviewIcon(stack.item);
        ApplyPreviewGradeFrame(stack.item);
        ApplyAttributeValues(stack.item);

        if (useButton != null)
        {
            useButton.interactable = !readOnly;
        }

        if (giveToSelectedNpcButton != null)
        {
            giveToSelectedNpcButton.interactable = !readOnly;
        }

        if (heavenGiftButton != null)
        {
            heavenGiftButton.interactable = !readOnly;
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

    bool ShouldStartClosed()
    {
        if (alwaysVisible)
        {
            return false;
        }

        if (closeOnStart)
        {
            return true;
        }

        return IsPlayerInventoryPanel();
    }

    bool IsPlayerInventoryPanel()
    {
        if (readOnly)
        {
            return false;
        }

        if (GetComponent<PlayerWallet>() != null)
        {
            return true;
        }

        if (panelRoot != null &&
            panelRoot.GetComponent<PlayerWallet>() != null)
        {
            return true;
        }

        string key =
            (panelRoot != null
                ? panelRoot.name
                : name)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();

        return key == "balopanel" ||
            key == "balo";
    }

    void TrySelectItemAtScreenPosition(Vector2 screenPosition)
    {
        Camera eventCamera =
            GetEventCamera();

        TrySelectItemAtScreenPosition(screenPosition, eventCamera);
    }

    void TrySelectItemAtScreenPosition(
        Vector2 screenPosition,
        Camera eventCamera)
    {
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
            !IsScreenPositionInsideAnyPanelButton(
                screenPosition,
                eventCamera) &&
            !IsScreenPositionInsideDetailPanel(
                screenPosition,
                eventCamera))
        {
            selectedItemIndex = -1;
            ClearDetail();
        }
    }

    bool IsScreenPositionInsideAnyPanelButton(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (panelRoot == null)
        {
            return false;
        }

        Button[] buttons =
            panelRoot.GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button == null ||
                !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            RectTransform buttonRect =
                button.GetComponent<RectTransform>();

            if (buttonRect == null)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    buttonRect,
                    screenPosition,
                    eventCamera))
            {
                return true;
            }
        }

        return false;
    }

    bool IsScreenPositionInsideInventoryUi(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (IsScreenPositionInsideRect(
                panelRoot != null
                ? panelRoot.transform as RectTransform
                : null,
                screenPosition,
                eventCamera))
        {
            return true;
        }

        if (IsScreenPositionInsideRect(
                itemGridParent as RectTransform,
                screenPosition,
                eventCamera))
        {
            return true;
        }

        if (IsScreenPositionInsideDetailPanel(
                screenPosition,
                eventCamera))
        {
            return true;
        }

        if (IsScreenPositionInsideAnySpawnedItem(
                screenPosition,
                eventCamera))
        {
            return true;
        }

        return IsScreenPositionInsideAnyPanelButton(
            screenPosition,
            eventCamera);
    }

    bool IsScreenPositionInsideAnySpawnedItem(
        Vector2 screenPosition,
        Camera eventCamera)
    {
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

            if (IsScreenPositionInsideRect(
                    rect,
                    screenPosition,
                    eventCamera))
            {
                return true;
            }
        }

        return false;
    }

    bool IsScreenPositionInsideRect(
        RectTransform rect,
        Vector2 screenPosition,
        Camera eventCamera)
    {
        return rect != null &&
            rect.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(
                rect,
                screenPosition,
                eventCamera);
    }

    bool ShouldCloseFromOutsidePointer(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (!closeWhenClickOutside ||
            panelRoot == null)
        {
            return false;
        }

        if (IsScreenPositionInsideBottomMenuButton(screenPosition, eventCamera))
        {
            return false;
        }

        RectTransform panelRect =
            panelRoot.GetComponent<RectTransform>();

        if (panelRect == null)
        {
            return false;
        }

        return !RectTransformUtility.RectangleContainsScreenPoint(
            panelRect,
            screenPosition,
            eventCamera);
    }

    bool IsScreenPositionInsideBottomMenuButton(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        Button[] buttons =
            FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            RectTransform rect = button != null ? button.transform as RectTransform : null;
            if (button == null ||
                !button.gameObject.activeInHierarchy ||
                rect == null ||
                !HasAncestorNamed(button.transform, "menupanel"))
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    rect,
                    screenPosition,
                    eventCamera))
            {
                return true;
            }
        }

        return false;
    }

    bool HasAncestorNamed(Transform current, string normalizedName)
    {
        while (current != null)
        {
            string key = current.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (key == normalizedName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
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
            Debug.Log(ItemText.Get("messages", "noValidTarget"));
            return;
        }

        inventory.UseItemOn(
            selectedItemIndex,
            target);
    }

    public void GiveSelectedItemToSelectedNpc()
    {
        if (!HasHeavenDaoPower(HeavenDaoPower.DirectGiftItem))
        {
            Debug.Log(ItemText.Get("messages", "needHeavenControlGift"));
            return;
        }

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
            Debug.Log(ItemText.Get("messages", "noSelectedCultivator"));
            return;
        }

        ItemInventory targetInventory =
            selectedTarget.GetComponent<ItemInventory>();

        if (targetInventory == null)
        {
            targetInventory =
                selectedTarget.gameObject.AddComponent<ItemInventory>();
        }

        targetInventory.UsePrivateNpcRuntimeItems(false);

        currentNpcInventory = targetInventory;
        playerInventory = inventory;

        StatItemData item = stack.item;

        if (!inventory.RemoveItem(item, 1))
        {
            return;
        }

        ItemEffectSpawner.PlayPickupEffect(item, selectedTarget);
        targetInventory.AddItem(item, 1);
        NpcFavoriteManager.EnsureInstance()?.AddFavorite(selectedTarget.gameObject);
        selectedItem = null;
        Refresh();
    }

    public void GiveSelectedItemToNpc()
    {
        GiveSelectedItemToSelectedNpc();
    }

    public void BeginHeavenGiftPlacement()
    {
        if (!HasHeavenDaoPower(HeavenDaoPower.DropOpportunityArea))
        {
            Debug.Log(ItemText.Get("messages", "needHeavenControlDrop"));
            return;
        }

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

        HeavenGiftPlacementController.BeginGift(inventory, stack.item);
        selectedItem = null;
        Close();
    }

    bool CanReceiveItem(Transform target)
    {
        return target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<SmartNpcAI>() != null ||
            target.GetComponent<MonsterAI>() != null;
    }

    bool HasHeavenDaoPower(HeavenDaoPower power)
    {
        return HeavenDaoSystem.Instance != null &&
            HeavenDaoSystem.Instance.HasPower(power);
    }

    void RebuildItemGrid()
    {
        ResolveItemButtonTemplate();

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
                stack.amount <= 0 ||
                !MatchesCurrentFilter(stack.item))
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

    bool MatchesCurrentFilter(
        StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        switch (currentCategoryFilter)
        {
            case InventoryCategoryFilter.TrangBi:
                return IsEquipmentItem(item);
            case InventoryCategoryFilter.DanDuoc:
                return item.itemType == ItemType.DanDuoc;
            case InventoryCategoryFilter.PhapBao:
                return item.itemType == ItemType.PhapBao;
            case InventoryCategoryFilter.CongPhap:
                return item.itemType == ItemType.CongPhap;
            case InventoryCategoryFilter.VatLieu:
                return item.itemType == ItemType.VatLieu;
            default:
                return true;
        }
    }

    bool IsEquipmentItem(
        StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        return item.GetResolvedUseStyle() ==
                ItemUseStyle.DurableEquipment ||
            item.GetResolvedEquipmentSlot() !=
                EquipmentSlot.None;
    }

    void ConfigureItemGrid()
    {
        if (itemGridParent == null)
        {
            return;
        }

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

        if (HasConfiguredCellSize(grid))
        {
            itemCellSize = grid.cellSize;
        }
        else
        {
            grid.cellSize = itemCellSize;
        }

        if (HasConfiguredSpacing(grid))
        {
            itemSpacing = grid.spacing;
        }
        else
        {
            grid.spacing = itemSpacing;
        }

        if (HasConfiguredPadding(grid))
        {
            itemGridPadding =
                new Vector2(
                    grid.padding.left,
                    grid.padding.top);
        }
        else
        {
            grid.padding =
                new RectOffset(
                    Mathf.RoundToInt(itemGridPadding.x),
                    Mathf.RoundToInt(itemGridPadding.x),
                    Mathf.RoundToInt(itemGridPadding.y),
                    Mathf.RoundToInt(itemGridPadding.y));
        }

        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;

        if (grid.constraint !=
                GridLayoutGroup.Constraint.Flexible &&
            grid.constraintCount > 0)
        {
            itemColumns =
                Mathf.Max(1, grid.constraintCount);
        }
        else
        {
            grid.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount =
                Mathf.Max(1, itemColumns);
        }

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

        Vector2 effectiveCellSize =
            GetEffectiveItemCellSize();

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.sizeDelta = effectiveCellSize;
        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            effectiveCellSize.x);

        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            effectiveCellSize.y);
    }

    Vector2 GetEffectiveItemCellSize()
    {
        if (itemGridParent != null)
        {
            GridLayoutGroup grid =
                itemGridParent.GetComponent<GridLayoutGroup>();

            if (HasConfiguredCellSize(grid))
            {
                return grid.cellSize;
            }
        }

        return itemCellSize;
    }

    static bool HasConfiguredCellSize(
        GridLayoutGroup grid)
    {
        return grid != null &&
            grid.cellSize.x > 0f &&
            grid.cellSize.y > 0f;
    }

    static bool HasConfiguredSpacing(
        GridLayoutGroup grid)
    {
        return grid != null &&
            grid.spacing != Vector2.zero;
    }

    static bool HasConfiguredPadding(
        GridLayoutGroup grid)
    {
        return grid != null &&
            (grid.padding.left != 0 ||
                grid.padding.right != 0 ||
                grid.padding.top != 0 ||
                grid.padding.bottom != 0);
    }

    void CacheTemplateTransform()
    {
        ResolveItemButtonTemplate();

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
        selectedItem = null;

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
        SetText(detailOwnerText, "");
        SetText(detailDescriptionText, "");
        SetText(detailStatsText, "");
        SetImageVisible(
            detailFrameImage,
            false);
        ClearPreviewIcon(previewItemIcon);
        ClearPreviewIcon(previewBigIcon);
        ResetPreviewMagicCircleVisual();
        SetImageVisible(
            previewMagicCircle,
            false);
        SetImageVisible(
            previewGradeFrameImage,
            false);
        SetAttributeValue(
            attackValueText,
            null);
        SetAttributeValue(
            defenseValueText,
            null);
        SetAttributeValue(
            hpValueText,
            null);
        SetAttributeValue(
            speedValueText,
            null);

        if (useButton != null)
        {
            useButton.interactable = false;
        }

        if (giveToSelectedNpcButton != null)
        {
            giveToSelectedNpcButton.interactable = false;
        }

        if (heavenGiftButton != null)
        {
            heavenGiftButton.interactable = false;
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

        if (playerWallet == null)
        {
            playerWallet =
                GetComponent<PlayerWallet>() ??
                FindAnyObjectByType<PlayerWallet>(
                    FindObjectsInactive.Include);
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

        ResolveItemButtonTemplate();
        BindFooterReferences();

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

            if (detailFrameImage == null)
            {
                detailFrameImage =
                    FindImageByName(detailTransform, "IconBG");
            }

            if (detailIcon == null)
            {
                detailIcon =
                    FindImageByName(detailTransform, "IconItem");
            }

            if (detailNameText == null)
            {
                detailNameText =
                    FindTextByName(detailTransform, "Name") ??
                    FindTextByName(detailTransform, "DetailNameText");
            }

            if (detailAmountText == null)
            {
                detailAmountText =
                    FindTextByName(detailTransform, "DetailAmountText");
            }

            if (detailAmountLabelText == null)
            {
                detailAmountLabelText =
                    FindTextByName(detailTransform, "AmountLabel");
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

            if (detailPriceLabelText == null)
            {
                detailPriceLabelText =
                    FindTextByName(detailTransform, "PriceLabel");
            }

            if (detailOwnerText == null)
            {
                detailOwnerText =
                    FindTextByName(detailTransform, "DetailOwnerText");
            }

            if (detailOwnerLabelText == null)
            {
                detailOwnerLabelText =
                    FindTextByName(detailTransform, "OwnerLabel");
            }

            if (detailDescriptionTitleText == null)
            {
                detailDescriptionTitleText =
                    FindTextByName(detailTransform, "DescriptionTitle");
            }

            if (detailDescriptionText == null)
            {
                detailDescriptionText =
                    FindTextByName(detailTransform, "DescriptionText") ??
                    FindTextByName(detailTransform, "DetailDescriptionText");
            }

            ApplyNpcInventoryDetailStaticTexts();

            if (detailStatsText == null)
            {
                detailStatsText =
                    FindTextByName(detailTransform, "DetailStatsText");
            }
        }

        BindRightPanelReferences(
            panelRoot != null
                ? panelRoot.transform
                : transform);

        if (tabAllButton == null)
        {
            tabAllButton =
                FindButtonByName(transform, "Tab_All");
        }

        if (tabTrangBiButton == null)
        {
            tabTrangBiButton =
                FindButtonByName(transform, "Tab_TrangBi");
        }

        if (tabDanDuocButton == null)
        {
            tabDanDuocButton =
                FindButtonByName(transform, "Tab_DanDuoc");
        }

        if (tabPhapBaoButton == null)
        {
            tabPhapBaoButton =
                FindButtonByName(transform, "Tab_PhapBao");
        }

        if (tabCongPhapButton == null)
        {
            tabCongPhapButton =
                FindButtonByName(transform, "Tab_CongPhap");
        }

        if (tabVatLieuButton == null)
        {
            tabVatLieuButton =
                FindButtonByName(transform, "Tab_VatLieu");
        }

        if (giveToSelectedNpcButton == null)
        {
            giveToSelectedNpcButton =
                FindButtonByName(transform, "phat_cho_Player");
        }

        if (giveToSelectedNpcButton == null)
        {
            giveToSelectedNpcButton =
                FindButtonByName(transform, "Cho_NPC");
        }

        if (giveToSelectedNpcButton == null)
        {
            giveToSelectedNpcButton =
                FindButtonByName(transform, "Cho NPC");
        }

        if (giveToSelectedNpcButton == null)
        {
            giveToSelectedNpcButton =
                FindButtonByName(transform, "ChoNPC");
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

        if (heavenGiftButton == null)
        {
            heavenGiftButton =
                FindButtonByName(transform, "Ban_Tang");
        }

        if (heavenGiftButton == null)
        {
            heavenGiftButton =
                FindButtonByName(transform, "Ban Tang");
        }

        if (heavenGiftButton == null)
        {
            heavenGiftButton =
                FindButtonByName(transform, "BanTang");
        }
    }

    void BindActionButtons()
    {
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

        if (heavenGiftButton != null)
        {
            heavenGiftButton.onClick.RemoveAllListeners();
            heavenGiftButton.onClick.AddListener(BeginHeavenGiftPlacement);
        }
    }

    void BindFooterReferences()
    {
        Transform searchRoot =
            panelRoot != null
                ? panelRoot.transform
                : transform;

        Transform footer =
            FindChildByName(searchRoot, "Footer");

        if (footer == null)
        {
            return;
        }

        if (footerUsedSlotText == null)
        {
            footerUsedSlotText =
                FindTextByName(footer, "SlotCount");
        }

        if (footerLinhThachText == null)
        {
            footerLinhThachText =
                FindTextByName(footer, "LinhThach");
        }
    }

    void BindCategoryTabButtons()
    {
        BindCategoryButton(
            tabAllButton,
            ShowAllItems);
        BindCategoryButton(
            tabTrangBiButton,
            ShowTrangBiItems);
        BindCategoryButton(
            tabDanDuocButton,
            ShowDanDuocItems);
        BindCategoryButton(
            tabPhapBaoButton,
            ShowPhapBaoItems);
        BindCategoryButton(
            tabCongPhapButton,
            ShowCongPhapItems);
        BindCategoryButton(
            tabVatLieuButton,
            ShowVatLieuItems);
    }

    void BindCategoryButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null ||
            action == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void ShowAllItems()
    {
        SetCategoryFilter(
            InventoryCategoryFilter.All);
    }

    public void ShowTrangBiItems()
    {
        SetCategoryFilter(
            InventoryCategoryFilter.TrangBi);
    }

    public void ShowDanDuocItems()
    {
        SetCategoryFilter(
            InventoryCategoryFilter.DanDuoc);
    }

    public void ShowPhapBaoItems()
    {
        SetCategoryFilter(
            InventoryCategoryFilter.PhapBao);
    }

    public void ShowCongPhapItems()
    {
        SetCategoryFilter(
            InventoryCategoryFilter.CongPhap);
    }

    public void ShowVatLieuItems()
    {
        SetCategoryFilter(
            InventoryCategoryFilter.VatLieu);
    }

    void SetCategoryFilter(
        InventoryCategoryFilter filter)
    {
        currentCategoryFilter = filter;
        selectedItemIndex = -1;
        selectedItem = null;
        ClearDetail();
        RebuildItemGrid();
    }

    void ResolveItemButtonTemplate()
    {
        if (itemGridParent == null ||
            itemButtonPrefab == null)
        {
            return;
        }

        if (itemButtonPrefab.transform != itemGridParent)
        {
            return;
        }

        InventoryItemButtonUI[] candidates =
            itemGridParent.GetComponentsInChildren<InventoryItemButtonUI>(true);

        foreach (InventoryItemButtonUI candidate in candidates)
        {
            if (candidate == null ||
                candidate.transform == itemGridParent)
            {
                continue;
            }

            itemButtonPrefab = candidate;
            return;
        }

        Debug.LogError(
            "InventoryPanelUI itemButtonPrefab must be a child template, not the itemGridParent itself.",
            this);

        itemButtonPrefab = null;
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
        if (parent == null)
        {
            return null;
        }

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
        if (parent == null)
        {
            return null;
        }

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

    bool UsesNpcInventoryDetailLayout()
    {
        return detailPanel != null &&
            (HasAncestorNamed(detailPanel.transform, "targetinfopanel") ||
            HasAncestorNamed(detailPanel.transform, "npcinventorygrid")) &&
            FindChildByName(detailPanel.transform, "TagRow") != null &&
            FindChildByName(detailPanel.transform, "InfoRows") != null &&
            FindChildByName(detailPanel.transform, "DescriptionBox") != null;
    }

    void ApplyNpcInventoryDetailStaticTexts()
    {
        if (!UsesNpcInventoryDetailLayout())
        {
            return;
        }

        SetText(
            detailAmountLabelText,
            ItemText.Get("detail", "amountLabel", "S\u1ED1 l\u01B0\u1EE3ng"));
        SetText(
            detailPriceLabelText,
            ItemText.Get("detail", "priceLabel", "Gi\u00E1 tr\u1ECB"));
        SetText(
            detailOwnerLabelText,
            ItemText.Get("detail", "ownerLabel", "Ch\u1EE7 s\u1EDF h\u1EEFu"));
        SetText(
            detailDescriptionTitleText,
            ItemText.Get("detail", "descriptionTitle", "M\u00F4 t\u1EA3"));
    }

    string GetInventoryOwnerName()
    {
        if (inventory == null)
        {
            return "";
        }

        Transform owner =
            inventory.transform;

        if (owner == null)
        {
            return "";
        }

        VillagerAI villager =
            owner.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc =
            owner.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        NpcData npcData =
            owner.GetComponent<NpcData>();

        if (npcData != null)
        {
            return npcData.npcName;
        }

        return owner.name;
    }

    string BuildStatsText(ItemStack stack)
    {
        StringBuilder builder =
            new StringBuilder();

        if (stack == null ||
            stack.item == null)
        {
            return "";
        }

        StatItemData item =
            stack.item;

        if (item.hpBonus != 0)
        {
            builder.AppendLine(
                ItemText.Format("stats", "hpBonus", item.hpBonus));
        }

        if (item.cultivationBonus != 0)
        {
            builder.AppendLine(
                ItemText.Format(
                    "stats",
                    "cultivationBonus",
                    item.cultivationBonus));
        }

        if (item.breakthroughRealm)
        {
            builder.AppendLine(
                ItemText.Get("stats", "breakthroughRealm"));
        }

        if (item.damageBonus != 0)
        {
            builder.AppendLine(
                ItemText.Format("stats", "damageBonus", item.damageBonus));
        }

        if (item.damageBonusPercent != 0)
        {
            builder.AppendLine("Tăng công: +" + item.damageBonusPercent + "%");
        }

        if (item.armorBonus != 0)
        {
            builder.AppendLine(
                ItemText.Format("stats", "armorBonus", item.armorBonus));
        }

        if (item.armorBonusPercent != 0)
        {
            builder.AppendLine("Tăng thủ: +" + item.armorBonusPercent + "%");
        }

        if (item.maxHpBonusPercent != 0)
        {
            builder.AppendLine("Tăng máu: +" + item.maxHpBonusPercent + "%");
        }

        if (item.effectResistanceBonus != 0)
        {
            builder.AppendLine(
                ItemText.Format(
                    "stats",
                    "effectResistanceBonus",
                    item.effectResistanceBonus));
        }

        if (item.isTemporary)
        {
            builder.AppendLine(
                ItemText.Format(
                    "stats",
                    "duration",
                    item.durationScaledSeconds));
        }

        if (item.ConsumesWhenUsed() &&
            item.useSuccessChance < 0.999f)
        {
            builder.AppendLine(
                ItemText.Format(
                    "stats",
                    "successChance",
                    Mathf.RoundToInt(item.useSuccessChance * 100f)));
        }

        if (item.UsesDurability())
        {
            builder.AppendLine(
                ItemText.Format(
                    "stats",
                    "durability",
                    stack.durability,
                    stack.maxDurability));
        }

        if (item.itemType == ItemType.CongPhap)
        {
            builder.AppendLine(
                ItemText.Format(
                    "stats",
                    "mastery",
                    ItemText.Mastery(stack.mastery)));
        }

        AppendUseConversionText(builder, item);

        return builder.ToString();
    }

    void AppendUseConversionText(
        StringBuilder builder,
        StatItemData item)
    {
        builder.AppendLine(
            ItemText.Format(
                "stats",
                "useStyle",
                ItemText.UseStyle(item.GetResolvedUseStyle())));
        builder.AppendLine(
            ItemText.Format(
                "stats",
                "rawUse",
                ItemText.RawUsePolicy(item.rawUsePolicy)));

        if (item.itemType == ItemType.VatLieu)
        {
            builder.AppendLine(
                ItemText.Format(
                    "stats",
                    "rawEfficiency",
                    Mathf.RoundToInt(item.rawUseEfficiency * 100f)));
        }

        if (item.rawToxicityDamage > 0)
        {
            builder.AppendLine(
                ItemText.Format(
                    "stats",
                    "toxicity",
                    item.rawToxicityDamage));
        }

        if (item.canBeRefinedIntoPill)
        {
            builder.AppendLine(ItemText.Get("stats", "canRefinePill"));
        }

        if (item.canBeForgedIntoArtifact)
        {
            builder.AppendLine(ItemText.Get("stats", "canForgeArtifact"));
        }

        if (item.itemType == ItemType.CongPhap &&
            item.canBeStudied)
        {
            builder.AppendLine(ItemText.Get("stats", "canStudy"));
        }

        if (!item.canBeSold)
        {
            builder.AppendLine(ItemText.Get("stats", "cannotSell"));
        }

        builder.AppendLine(
            ItemText.Format(
                "stats",
                "npcIntent",
                ItemText.NpcIntent(item.npcIntent)));
    }

    void BindRightPanelReferences(
        Transform searchRoot)
    {
        rightPanelTransform =
            FindRightPanelTransform(searchRoot);

        if (rightPanelTransform == null)
        {
            previewPanelTransform = null;
            previewBackgroundImage = null;
            previewGradeFrameImage = null;
            previewItemIcon = null;
            previewBigIcon = null;
            previewMagicCircle = null;
            attackValueText = null;
            defenseValueText = null;
            hpValueText = null;
            speedValueText = null;
            return;
        }

        Transform infoPanel =
            FindDirectOrNestedChild(
                rightPanelTransform,
                "InfoPanel");
        previewPanelTransform =
            FindDirectOrNestedChild(
                rightPanelTransform,
                "PreviewPanel");
        previewBackgroundImage =
            FindImageByName(
                previewPanelTransform,
                "PreviewBG");

        if (detailPanel == null ||
            !IsDescendantOf(
                detailPanel.transform,
                rightPanelTransform))
        {
            detailPanel =
                infoPanel != null
                    ? infoPanel.gameObject
                    : detailPanel;
        }

        previewItemIcon =
            FindImageByName(
                previewPanelTransform,
                "ItemIcon");
        previewBigIcon =
            FindImageByName(
                previewPanelTransform,
                "ItemBigIcon");
        previewMagicCircle =
            FindImageByName(
                previewPanelTransform,
                "MagicCircle");
        CapturePreviewMagicCircleBaseState();
        previewGradeFrameImage =
            EnsurePreviewGradeFrameImage();

        if (detailIcon == null ||
            !IsDescendantOf(
                detailIcon.transform,
                rightPanelTransform))
        {
            detailIcon =
                previewItemIcon != null
                    ? previewItemIcon
                    : previewBigIcon;
        }

        if (detailAmountText != null &&
            !IsDescendantOf(
                detailAmountText.transform,
                rightPanelTransform))
        {
            detailAmountText = null;
        }

        if (detailTargetsText != null &&
            !IsDescendantOf(
                detailTargetsText.transform,
                rightPanelTransform))
        {
            detailTargetsText = null;
        }

        if (detailPriceText != null &&
            !IsDescendantOf(
                detailPriceText.transform,
                rightPanelTransform))
        {
            detailPriceText = null;
        }

        if (detailStatsText != null &&
            !IsDescendantOf(
                detailStatsText.transform,
                rightPanelTransform))
        {
            detailStatsText = null;
        }

        if (detailNameText == null ||
            !IsDescendantOf(
                detailNameText.transform,
                rightPanelTransform))
        {
            detailNameText =
                FindTextByName(
                    infoPanel,
                    "ItemNameText");
        }

        Transform qualityBadge =
            FindDirectOrNestedChild(
                infoPanel,
                "QualityBadge");

        if (detailTypeText == null ||
            !IsDescendantOf(
                detailTypeText.transform,
                rightPanelTransform))
        {
            detailTypeText =
                FindRightPanelTypeText(
                    qualityBadge);
        }

        if (detailGradeText == null ||
            !IsDescendantOf(
                detailGradeText.transform,
                rightPanelTransform))
        {
            detailGradeText =
                FindTextByName(
                    qualityBadge,
                    "QualityText");
        }

        if (detailDescriptionText == null ||
            !IsDescendantOf(
                detailDescriptionText.transform,
                rightPanelTransform))
        {
            detailDescriptionText =
                FindRightPanelDescriptionText(
                    infoPanel);
        }

        attackValueText =
            FindAttributeValueText(
                infoPanel,
                "Attr_Attack");
        defenseValueText =
            FindAttributeValueText(
                infoPanel,
                "Attr_Defense");
        hpValueText =
            FindAttributeValueText(
                infoPanel,
                "Attr_HP");
        speedValueText =
            FindAttributeValueText(
                infoPanel,
                "Attr_Speed");
    }

    Transform FindRightPanelTransform(
        Transform searchRoot)
    {
        if (searchRoot == null)
        {
            return null;
        }

        Transform rightPanel =
            searchRoot.Find("ContentRoot/RightPanel");

        if (rightPanel != null)
        {
            return rightPanel;
        }

        return FindChildByName(
            searchRoot,
            "RightPanel");
    }

    Transform FindDirectOrNestedChild(
        Transform parent,
        string childName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child =
            parent.Find(childName);

        if (child != null)
        {
            return child;
        }

        return FindChildByName(
            parent,
            childName);
    }

    Image FindImageByName(
        Transform parent,
        string childName)
    {
        Transform child =
            FindDirectOrNestedChild(
                parent,
                childName);

        if (child == null)
        {
            return null;
        }

        return child.GetComponent<Image>();
    }

    TMP_Text FindAttributeValueText(
        Transform parent,
        string rowName)
    {
        Transform row =
            FindDirectOrNestedChild(
                parent,
                rowName);

        return FindTextByName(
            row,
            "ValueText");
    }

    TMP_Text FindRightPanelTypeText(
        Transform qualityBadge)
    {
        if (qualityBadge == null)
        {
            return null;
        }

        TMP_Text fallback = null;
        float bestY = float.MinValue;
        TMP_Text[] texts =
            qualityBadge.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null ||
                text.name != "TypeText")
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = text;
                bestY =
                    text.rectTransform.anchoredPosition.y;
            }

            string currentText =
                text.text != null
                    ? text.text.ToLowerInvariant()
                    : "";

            if (!currentText.Contains("thu") &&
                !currentText.Contains("tinh"))
            {
                float currentY =
                    text.rectTransform.anchoredPosition.y;

                if (currentY > bestY)
                {
                    fallback = text;
                    bestY = currentY;
                }
            }
        }

        return fallback;
    }

    TMP_Text FindRightPanelDescriptionText(
        Transform infoPanel)
    {
        if (infoPanel == null)
        {
            return null;
        }

        TMP_Text[] texts =
            infoPanel.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text bestMatch = null;
        float bestScore = float.MinValue;

        foreach (TMP_Text text in texts)
        {
            if (text == null ||
                text == detailNameText ||
                text == detailTypeText ||
                text == detailGradeText ||
                text == attackValueText ||
                text == defenseValueText ||
                text == hpValueText ||
                text == speedValueText)
            {
                continue;
            }

            string currentText =
                text.text != null
                    ? text.text.Trim().ToLowerInvariant()
                    : "";

            if (currentText == "lo\u1EA1i" ||
                currentText == "ph\u1EA9m ch\u1EA5t" ||
                currentText == "th\u00F4ng tin" ||
                currentText == "thu\u1ED9c t\u00EDnh" ||
                currentText == "t\u1EA5n c\u00F4ng" ||
                currentText == "defense" ||
                currentText == "hp" ||
                currentText == "speed")
            {
                continue;
            }

            RectTransform rect =
                text.rectTransform;
            float score =
                rect.rect.width * rect.rect.height;

            if (text.name.Contains("Description"))
            {
                score += 100000f;
            }

            if (currentText.Length > 20)
            {
                score += currentText.Length * 10f;
            }

            if (bestMatch == null ||
                score > bestScore)
            {
                bestMatch = text;
                bestScore = score;
            }
        }

        return bestMatch;
    }

    bool UsesRightPanelLayout()
    {
        return rightPanelTransform != null &&
            (previewPanelTransform != null ||
                attackValueText != null ||
                IsDescendantOf(
                    detailNameText != null
                        ? detailNameText.transform
                        : null,
                    rightPanelTransform));
    }

    bool IsDescendantOf(
        Transform child,
        Transform ancestor)
    {
        if (child == null ||
            ancestor == null)
        {
            return false;
        }

        Transform current = child;

        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    void ApplyPreviewIcon(
        StatItemData item)
    {
        CapturePreviewMagicCircleBaseState();

        Sprite icon =
            item != null
                ? item.icon
                : null;
        bool hasIcon = icon != null;

        SetImageSprite(
            previewItemIcon,
            icon);
        SetImageSprite(
            previewBigIcon,
            icon);
        SetImageVisible(
            previewMagicCircle,
            hasIcon &&
                previewMagicCircle != null &&
                previewMagicCircle.sprite != null);

        if (!hasIcon)
        {
            ResetPreviewMagicCircleVisual();
        }
    }

    void ApplyPreviewGradeFrame(
        StatItemData item)
    {
        if (previewGradeFrameImage == null)
        {
            return;
        }

        Sprite frame =
            item != null
                ? ItemGradeFrameLibrary.GetFrame(item.grade)
                : null;
        previewGradeFrameImage.sprite = frame;
        SetImageVisible(
            previewGradeFrameImage,
            frame != null);
        previewGradeFrameImage.color = Color.white;
        previewGradeFrameImage.raycastTarget = false;
        previewGradeFrameImage.preserveAspect = false;
    }

    void ApplyDetailGradeFrame(
        StatItemData item)
    {
        if (detailFrameImage == null)
        {
            return;
        }

        Sprite frame =
            item != null
                ? ItemGradeFrameLibrary.GetFrame(item.grade)
                : null;
        detailFrameImage.sprite = frame;
        SetImageVisible(
            detailFrameImage,
            frame != null);
        detailFrameImage.color = Color.white;
        detailFrameImage.raycastTarget = false;
        detailFrameImage.preserveAspect = false;
    }

    void ClearPreviewIcon(
        Image image)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = null;
        SetImageVisible(
            image,
            false);
    }

    void SetImageSprite(
        Image image,
        Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        SetImageVisible(
            image,
            sprite != null);
    }

    void SetImageVisible(
        Image image,
        bool visible)
    {
        if (image == null)
        {
            return;
        }

        image.enabled = visible;
        image.gameObject.SetActive(visible);
    }

    void CapturePreviewMagicCircleBaseState()
    {
        if (previewMagicCircle == null ||
            previewMagicCircleBaseCaptured)
        {
            return;
        }

        previewMagicCircleBaseScale =
            previewMagicCircle.transform.localScale;
        previewMagicCircleBaseEuler =
            previewMagicCircle.transform.localEulerAngles;
        previewMagicCircleBaseColor =
            previewMagicCircle.color;
        previewMagicCircle.raycastTarget = false;
        previewMagicCircleBaseCaptured = true;
    }

    void ResetPreviewMagicCircleVisual()
    {
        if (previewMagicCircle == null)
        {
            return;
        }

        CapturePreviewMagicCircleBaseState();
        previewMagicCircle.transform.localScale =
            previewMagicCircleBaseScale;
        previewMagicCircle.transform.localRotation =
            Quaternion.Euler(previewMagicCircleBaseEuler);
        previewMagicCircle.color =
            previewMagicCircleBaseColor;
    }

    void RefreshPreviewMagicCircleRarityVisuals()
    {
        if (previewMagicCircle == null ||
            !previewMagicCircle.isActiveAndEnabled ||
            previewMagicCircle.sprite == null ||
            selectedItem == null)
        {
            return;
        }

        CapturePreviewMagicCircleBaseState();

        ItemGrade grade = selectedItem.grade;
        float time =
            Time.unscaledTime + previewMagicCirclePulseSeed;
        float pulse =
            0.68f +
            0.32f * Mathf.Sin(time * 3.1f);
        float strength =
            GetPreviewMagicCircleStrength(grade);
        float spinSpeed =
            GetPreviewMagicCircleSpinSpeed(grade);
        float amplitude =
            GetPreviewMagicCircleRotationAmplitude(grade);
        Color tintedColor =
            GetPreviewMagicCircleColor(grade, pulse);
        float rotation =
            Mathf.Sin(time * spinSpeed) * amplitude;
        float scale =
            1f + strength * (0.06f + pulse * 0.08f);

        previewMagicCircle.color = tintedColor;
        previewMagicCircle.transform.localRotation =
            Quaternion.Euler(
                previewMagicCircleBaseEuler.x,
                previewMagicCircleBaseEuler.y,
                previewMagicCircleBaseEuler.z + rotation);
        previewMagicCircle.transform.localScale =
            new Vector3(
                previewMagicCircleBaseScale.x * scale,
                previewMagicCircleBaseScale.y * scale,
                previewMagicCircleBaseScale.z);
    }

    static float GetPreviewMagicCircleStrength(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return 0.14f;
            case ItemGrade.Trung:
                return 0.24f;
            case ItemGrade.Thuong:
                return 0.38f;
            case ItemGrade.Tien:
                return 0.54f;
            default:
                return 0.16f;
        }
    }

    static float GetPreviewMagicCircleRotationAmplitude(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return 5f;
            case ItemGrade.Trung:
                return 9f;
            case ItemGrade.Thuong:
                return 15f;
            case ItemGrade.Tien:
                return 22f;
            default:
                return 6f;
        }
    }

    static float GetPreviewMagicCircleSpinSpeed(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return 0.95f;
            case ItemGrade.Trung:
                return 1.22f;
            case ItemGrade.Thuong:
                return 1.58f;
            case ItemGrade.Tien:
                return 1.96f;
            default:
                return 1f;
        }
    }

    static Color GetPreviewMagicCircleColor(
        ItemGrade grade,
        float pulse)
    {
        Color baseColor =
            ShopPanelUI.GetGradeBaseColor(grade);
        Color accentColor =
            ShopPanelUI.GetGradeAccentColor(grade);
        Color mixed =
            Color.Lerp(
                baseColor,
                accentColor,
                0.54f + Mathf.Clamp01(pulse) * 0.18f);

        Color.RGBToHSV(
            mixed,
            out float hue,
            out float saturation,
            out float value);

        saturation = Mathf.Clamp01(saturation + 0.12f);
        value = Mathf.Clamp01(value + 0.22f);

        Color result =
            Color.HSVToRGB(hue, saturation, value);
        result.a = 0.46f + Mathf.Clamp01(pulse) * 0.26f;
        return result;
    }

    Image EnsurePreviewGradeFrameImage()
    {
        if (previewPanelTransform == null)
        {
            return null;
        }

        Image existing =
            FindImageByName(
                previewPanelTransform,
                "PreviewGradeFrame");

        if (existing != null)
        {
            return existing;
        }

        if (previewBackgroundImage == null)
        {
            return null;
        }

        GameObject frameObject =
            new GameObject(
                "PreviewGradeFrame",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
        frameObject.transform.SetParent(
            previewPanelTransform,
            false);

        RectTransform sourceRect =
            previewBackgroundImage.rectTransform;
        RectTransform frameRect =
            frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = sourceRect.anchorMin;
        frameRect.anchorMax = sourceRect.anchorMax;
        frameRect.pivot = sourceRect.pivot;
        frameRect.anchoredPosition = sourceRect.anchoredPosition;
        frameRect.sizeDelta = sourceRect.sizeDelta;
        frameRect.localScale = Vector3.one;
        frameRect.localRotation = Quaternion.identity;
        frameObject.transform.SetSiblingIndex(
            previewBackgroundImage.transform.GetSiblingIndex() + 1);

        Image frameImage =
            frameObject.GetComponent<Image>();
        frameImage.enabled = false;
        frameImage.raycastTarget = false;
        frameImage.preserveAspect = false;
        frameImage.color = Color.white;
        return frameImage;
    }

    void ApplyAttributeValues(
        StatItemData item)
    {
        SetAttributeText(
            attackValueText,
            item != null
                ? ResolvePrimaryAttributeText(
                    item.damageBonus,
                    item.damageBonusPercent,
                    "ATK")
                : null);
        SetAttributeText(
            defenseValueText,
            item != null
                ? ResolvePrimaryAttributeText(
                    item.armorBonus,
                    item.armorBonusPercent,
                    "DEF")
                : null);
        SetAttributeText(
            hpValueText,
            item != null
                ? ResolvePrimaryAttributeText(
                    item.hpBonus,
                    item.maxHpBonusPercent,
                    "HP")
                : null);
        SetAttributeValue(
            speedValueText,
            null);
    }

    string ResolvePrimaryAttributeText(
        int flatValue,
        int percentValue,
        string percentLabel)
    {
        if (percentValue != 0)
        {
            return "+" + percentValue + "% " + percentLabel;
        }

        if (flatValue != 0)
        {
            return FormatSignedValue(flatValue);
        }

        return null;
    }

    void SetAttributeText(
        TMP_Text text,
        string value)
    {
        if (text == null)
        {
            return;
        }

        text.text =
            !string.IsNullOrEmpty(value)
                ? value
                : "-";
    }

    void SetAttributeValue(
        TMP_Text text,
        int? value)
    {
        if (text == null)
        {
            return;
        }

        text.text =
            value.HasValue
                ? FormatSignedValue(value.Value)
                : "-";
    }

    string FormatSignedValue(int value)
    {
        if (value > 0)
        {
            return "+" + value;
        }

        return value.ToString();
    }

    void BindInventoryEvents()
    {
        if (inventory == null)
        {
            return;
        }

        if (subscribedInventory == inventory)
        {
            return;
        }

        inventory.OnChanged += Refresh;
        subscribedInventory = inventory;
    }

    void BindWalletEvents()
    {
        if (walletEventsBound)
        {
            return;
        }

        PlayerWallet.OnAnyWalletChanged += HandleWalletChanged;
        walletEventsBound = true;
    }

    void UnbindWalletEvents()
    {
        if (!walletEventsBound)
        {
            return;
        }

        PlayerWallet.OnAnyWalletChanged -= HandleWalletChanged;
        walletEventsBound = false;
    }

    void HandleWalletChanged(int amount)
    {
        RefreshFooterLinhThachText();
    }

    void UnbindInventoryEvents()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnChanged -= Refresh;
            subscribedInventory = null;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        AutoFindMissingReferences();
        CacheTemplateTransform();
    }
#endif
}
