using System;
using System.Text;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanelUI : MonoBehaviour
{
    [Header("Data")]
    public SimpleItemShop shop;
    public PlayerWallet playerWallet;
    public ItemInventory playerInventory;
    public InventoryPanelUI inventoryPanelUI;

    [Header("Panel")]
    public GameObject panelRoot;
    public TMP_Text shopTitleText;
    public string shopTitle = "";
    public bool closeWhenClickOutside = true;

    [Header("Shopkeeper")]
    public TMP_Text npcSpeechText;
    public RectTransform npcSpeechBubbleRect;
    public Vector2 speechBubblePadding =
        new Vector2(56f, 34f);
    public Vector2 speechBubbleMinSize =
        new Vector2(260f, 95f);
    public float speechBubbleMaxWidth = 430f;

    [Header("Category Labels")]
    public TMP_Text vatLieuCategoryText;
    public TMP_Text congPhapCategoryText;
    public TMP_Text danDuocCategoryText;
    public TMP_Text phapBaoCategoryText;

    [Header("Items")]
    public Transform itemGridParent;
    public ShopItemButtonUI itemButtonPrefab;
    public bool autoConfigureGrid = true;
    public Vector2 itemCellSize =
        new Vector2(130f, 170f);
    public Vector2 itemSpacing =
        new Vector2(12f, 12f);
    public int itemColumns = 3;

    [Header("Detail")]
    public GameObject detailPanel;
    public Image detailIcon;
    public TMP_Text detailNameText;
    public TMP_Text detailTypeText;
    public TMP_Text detailGradeText;
    public TMP_Text detailTargetsText;
    public TMP_Text detailPriceText;
    public TMP_Text detailDescriptionText;
    public TMP_Text detailStatsText;
    public GameObject buyPanel;
    public Button buyButton;
    public TMP_Text buyButtonText;
    public TMP_Text moneyText;
    public Vector2 detailOffset =
        new Vector2(24f, 0f);
    public Vector2 buyOffset =
        new Vector2(0f, -32f);
    public bool autoLayoutBuyPanel = false;
    public Vector2 buyPanelSize =
        new Vector2(150f, 130f);
    public bool moveDetailBesideSelectedItem = true;
    public bool moveBuyPanelBelowSelectedItem = false;

    ItemType currentType = ItemType.DanDuoc;
    int selectedItemIndex = -1;
    readonly List<ShopItemButtonUI> spawnedButtons =
        new List<ShopItemButtonUI>();
    Canvas rootCanvas;
    bool disabledBecauseAttachedToInventoryPanel;
    float detailGradePulseSeed;
    Vector2 speechBubbleBaseSize;
    bool hasSpeechBubbleBaseSize;
    bool IsAccidentalInventoryPanelAttachment()
    {
        InventoryPanelUI inventoryPanel = GetComponent<InventoryPanelUI>();
        if (inventoryPanel == null)
        {
            return false;
        }

        return shop == null &&
            itemGridParent == null &&
            itemButtonPrefab == null &&
            shopTitleText == null &&
            buyPanel == null &&
            buyButton == null;
    }
    void Awake()
    {
        if (IsAccidentalInventoryPanelAttachment())
        {
            disabledBecauseAttachedToInventoryPanel = true;
            enabled = false;
            return;
        }

        AutoFindMissingReferences();

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuySelectedItem);
        }

        rootCanvas =
            GetComponentInParent<Canvas>();
        detailGradePulseSeed =
            Mathf.Abs(
                UnityObjectIdUtility.GetRuntimeId(this) * 0.137f);

        ClearDetail();
    }

    void Update()
    {
        if (disabledBecauseAttachedToInventoryPanel)
        {
            return;
        }

        if (panelRoot != null &&
            !panelRoot.activeSelf)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandlePointerDown(Input.mousePosition);
        }

        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
        {
            HandlePointerDown(Input.GetTouch(0).position);
        }

        RefreshAnimatedGradeDetail();
    }

    void HandlePointerDown(Vector2 screenPosition)
    {
        Camera eventCamera =
            GetEventCamera();

        if (ShouldCloseFromOutsidePointer(screenPosition, eventCamera))
        {
            Close();
            return;
        }

        // Khi đang bấm lên DetailPanel / BuyPanel thì không tự chọn item phía sau.
        // Nút BuyButton vẫn nhận OnClick bình thường thông qua Button component.
        if (IsPointerInsideDetailOrBuyPanel(screenPosition, eventCamera))
        {
            return;
        }

        TrySelectItemAtScreenPosition(screenPosition, eventCamera);
    }

    void Start()
    {
        if (disabledBecauseAttachedToInventoryPanel)
        {
            return;
        }

        Close();
    }

    public void Open()
    {
        AutoFindMissingReferences();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshShopSource();
        SyncCategoryLabels();
        ShowDanDuoc();
        ClearDetail();
        ApplyShopkeeperSpeechList(
            "speechGreetingLines",
            "\u0110\u1ea1o h\u1eefu mu\u1ed1n t\u00ecm b\u1ea3o v\u1eadt g\u00ec h\u00f4m nay?");
    }

    public void Close()
    {
        ResetItemGridScrollPosition();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void Toggle()
    {
        if (panelRoot == null)
        {
            return;
        }

        if (panelRoot.activeSelf)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void ShowDanDuoc()
    {
        ShowCategory(
            ItemType.DanDuoc,
            ResolveCategoryTitle(
                ItemType.DanDuoc,
                UiText.Get("shop", "titleDanDuoc")));
    }

    public void ShowPhapBao()
    {
        ShowCategory(
            ItemType.PhapBao,
            ResolveCategoryTitle(
                ItemType.PhapBao,
                UiText.Get("shop", "titlePhapBao")));
    }

    public void ShowCongPhap()
    {
        ShowCategory(
            ItemType.CongPhap,
            ResolveCategoryTitle(
                ItemType.CongPhap,
                UiText.Get("shop", "titleCongPhap")));
    }

    public void ShowVatLieu()
    {
        ShowCategory(
            ItemType.VatLieu,
            ResolveCategoryTitle(
                ItemType.VatLieu,
                UiText.Get("shop", "titleVatLieu")));
    }

    public void ShowCategory(
        ItemType itemType,
        string title)
    {
        currentType = itemType;
        shopTitle = ResolveCategoryTitle(itemType, title);
        selectedItemIndex = -1;

        RefreshShopSource();

        if (shopTitleText != null)
        {
            shopTitleText.text = shopTitle;
        }

        SyncCategoryLabels();
        RefreshMoney();
        ClearDetail();
        RebuildItemGrid(true);
        ApplyShopkeeperCategorySpeech(itemType);
    }

    public void SelectItem(int itemIndex)
    {
        AutoFindMissingReferences();

        Debug.Log("Select shop item index: " + itemIndex);

        selectedItemIndex = itemIndex;

        ShopItemSlot slot =
            shop.GetSlot(itemIndex);

        if (slot == null ||
            slot.item == null)
        {
            ClearDetail();
            return;
        }

        if (detailPanel != null)
        {
            detailPanel.SetActive(true);
        }
        else if (!TemplateUsesInlineBuyButton())
        {
            Debug.LogWarning("ShopPanelUI missing DetailPanel reference.");
        }

        if (buyPanel != null)
        {
            buyPanel.SetActive(true);
            RemoveBuyPanelAutoLayoutIfDisabled();
            ConfigureBuyPanelLayout();
            EnsureBuyPanelVisible();
        }
        else if (!TemplateUsesInlineBuyButton())
        {
            Debug.LogWarning("ShopPanelUI missing BuyPanel reference.");
        }

        if (detailIcon != null)
        {
            detailIcon.sprite = slot.item.icon;
            detailIcon.enabled = slot.item.icon != null;
        }

        if (detailNameText != null)
        {
            detailNameText.text = ItemText.Name(slot.item);
        }

        if (detailTypeText != null)
        {
            detailTypeText.text =
                ItemText.Format(
                    "detail",
                    "typeFormat",
                    ItemText.Type(slot.item.itemType));
        }

        if (detailGradeText != null)
        {
            detailGradeText.text =
                ItemText.Format(
                    "detail",
                    "gradeFormat",
                    ItemText.Grade(slot.item.grade));
            ApplyGradeTextStyle(
                detailGradeText,
                slot.item.grade,
                1f);
        }

        if (detailTargetsText != null)
        {
            detailTargetsText.text =
                ItemText.Format(
                    "detail",
                    "targetsFormat",
                    ItemText.Target(slot.item.validTargets));
        }

        if (detailPriceText != null)
        {
            detailPriceText.text =
                ItemText.Format(
                    "detail",
                    "priceFormat",
                    FormatDisplayPrice(slot.item));
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = ItemText.Description(slot.item);
        }

        if (detailStatsText != null)
        {
            detailStatsText.text = BuildStatsText(slot.item);
        }

        BringDetailPanelsToFront();
        PositionDetailAndBuyPanel(itemIndex);
        EnsureBuyPanelVisible();
        RefreshBuyButton();
        ApplyLocalizedBuyButtonText();
        ApplyShopkeeperInspectSpeech(slot.item);
    }

    public void BuySelectedItem()
    {
        BuyItem(selectedItemIndex);
    }

    public void BuyItem(int itemIndex)
    {
        AutoFindMissingReferences();

        if (itemIndex < 0 ||
            shop == null ||
            playerInventory == null)
        {
            Debug.LogWarning("ShopPanelUI missing shop or player inventory.");
            return;
        }

        ShopItemSlot selectedSlot =
            shop.GetSlot(itemIndex);
        StatItemData purchasedItem =
            selectedSlot != null
                ? selectedSlot.item
                : null;

        bool bought =
            shop.BuyToInventory(
                itemIndex,
                playerWallet,
                playerInventory);

        if (!bought)
        {
            selectedItemIndex = itemIndex;
            RefreshBuyButton();
            ApplyLocalizedBuyButtonText();
            RefreshSpawnedButtonStates();
            return;
        }

        selectedItemIndex = itemIndex;
        RefreshMoney();
        RefreshInventoryPanel();
        RebuildItemGrid(false);
        SelectItem(selectedItemIndex);

        if (purchasedItem != null)
        {
            ApplyShopkeeperPurchaseSpeech(purchasedItem);
        }
    }


    public int GetDisplayPrice(StatItemData item)
    {
        return shop != null
            ? shop.GetBuyPrice(item)
            : NpcEconomy.GetTradePrice(item, NpcTradeContext.MarketBuy);
    }

    public string FormatDisplayPrice(StatItemData item)
    {
        return NpcEconomy.FormatCurrency(GetDisplayPrice(item));
    }

    public NpcTradeContext GetBuyContext()
    {
        return shop != null
            ? shop.GetBuyContext()
            : NpcTradeContext.MarketBuy;
    }

    string GetLocalizedCategoryTitle(
        ItemType itemType,
        string fallbackTitle)
    {
        switch (itemType)
        {
            case ItemType.DanDuoc:
                return UiText.Get("shop", "titleDanDuoc", fallbackTitle);
            case ItemType.PhapBao:
                return UiText.Get("shop", "titlePhapBao", fallbackTitle);
            case ItemType.CongPhap:
                return UiText.Get("shop", "titleCongPhap", fallbackTitle);
            case ItemType.VatLieu:
                return UiText.Get("shop", "titleVatLieu", fallbackTitle);
            case ItemType.ThucPham:
                return UiText.Get("shop", "titleThucPham", fallbackTitle);
            default:
                return fallbackTitle;
        }
    }

    string ResolveCategoryTitle(
        ItemType itemType,
        string fallbackTitle)
    {
        string fixedTitle =
            GetFixedCategoryTitle(itemType);
        return !string.IsNullOrEmpty(fixedTitle)
            ? fixedTitle
            : GetLocalizedCategoryTitle(itemType, fallbackTitle);
    }

    string GetFixedCategoryTitle(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.VatLieu:
                return "V\u1eadt Li\u1ec7u";
            case ItemType.CongPhap:
                return "C\u00f4ng Ph\u00e1p";
            case ItemType.DanDuoc:
                return "\u0110an D\u01b0\u1ee3c";
            case ItemType.PhapBao:
                return "Ph\u00e1p B\u1ea3o";
            default:
                return string.Empty;
        }
    }

    void ApplyLocalizedBuyButtonText()
    {
        if (buyButtonText == null ||
            shop == null)
        {
            return;
        }

        ShopItemSlot slot =
            shop.GetSlot(selectedItemIndex);

        bool canTrade =
            slot != null &&
            slot.item != null &&
            NpcEconomy.CanTradeNormally(slot.item);

        bool canBuy =
            canTrade &&
            slot.amount > 0 &&
            CanPay(GetDisplayPrice(slot.item));

        buyButtonText.text = canBuy
            ? UiText.Get("shop", "buyButtonBuy")
            : canTrade
                ? UiText.Get("shop", "buyButtonNotEnough")
                : UiText.Get("shop", "buyButtonUnavailable");
    }

    void RefreshInventoryPanel()
    {
        if (inventoryPanelUI != null &&
            inventoryPanelUI.IsOpen)
        {
            inventoryPanelUI.Refresh();
        }
    }

    public bool CanAffordDisplayedPrice(StatItemData item)
    {
        return item != null &&
            CanPay(GetDisplayPrice(item));
    }

    public void TryBuyItemFromInlineButton(int itemIndex)
    {
        SelectItem(itemIndex);
        BuyItem(itemIndex);
    }

    void RefreshShopSource()
    {
        if (shop != null &&
            shop.refreshNpcInventoryBeforeOpen)
        {
            shop.RefreshFromSellerInventory();
        }
    }

    void AutoFindMissingReferences()
    {
        InventoryToggleButton inventoryToggle =
            FindAnyObjectByType<InventoryToggleButton>(
                FindObjectsInactive.Include);

        if (inventoryToggle != null &&
            inventoryToggle.inventoryPanel != null)
        {
            inventoryPanelUI =
                inventoryToggle.inventoryPanel;
        }

        if (inventoryPanelUI == null)
        {
            inventoryPanelUI =
                FindAnyObjectByType<InventoryPanelUI>(
                    FindObjectsInactive.Include);
        }

        if (inventoryPanelUI != null &&
            inventoryPanelUI.inventory != null)
        {
            playerInventory =
                inventoryPanelUI.inventory;
        }

        if (playerInventory == null)
        {
            playerInventory =
                FindAnyObjectByType<ItemInventory>(
                    FindObjectsInactive.Include);
        }

        if (playerWallet == null)
        {
            playerWallet =
                FindAnyObjectByType<PlayerWallet>(
                    FindObjectsInactive.Include);
        }

        if (playerWallet == null &&
            inventoryPanelUI != null)
        {
            playerWallet =
                inventoryPanelUI.GetComponent<PlayerWallet>();

            if (playerWallet == null)
            {
                playerWallet =
                    inventoryPanelUI.gameObject.AddComponent<PlayerWallet>();
            }
        }

        if (itemGridParent == null)
        {
            Transform foundGridParent =
                FindChildByName(transform, "Content");
            if (foundGridParent != null)
            {
                itemGridParent = foundGridParent;
            }
        }

        if (itemButtonPrefab == null &&
            itemGridParent != null)
        {
            itemButtonPrefab =
                itemGridParent.GetComponentInChildren<ShopItemButtonUI>(true);
        }

        if (shopTitleText == null)
        {
            shopTitleText =
                FindTextByName(transform, "ShopTitleText") ??
                FindTextByName(transform, "TitleText");
        }

        if (npcSpeechText == null)
        {
            npcSpeechText =
                FindTextByName(transform, "SpeechText") ??
                FindTextByName(transform, "NpcSpeechText");
        }

        if (npcSpeechBubbleRect == null)
        {
            Transform foundSpeechBubble =
                FindChildByName(transform, "SpeechBubble");
            if (foundSpeechBubble != null)
            {
                npcSpeechBubbleRect =
                    foundSpeechBubble.GetComponent<RectTransform>();
            }
        }

        CacheSpeechBubbleBaseSize();

        if (vatLieuCategoryText == null)
        {
            vatLieuCategoryText = FindCategoryLabelText("VatLieu");
        }

        if (congPhapCategoryText == null)
        {
            congPhapCategoryText = FindCategoryLabelText("Congphap");
        }

        if (danDuocCategoryText == null)
        {
            danDuocCategoryText = FindCategoryLabelText("DanDuoc");
        }

        if (phapBaoCategoryText == null)
        {
            phapBaoCategoryText = FindCategoryLabelText("Phapbao");
        }

        if (detailPanel == null)
        {
            Transform foundDetailPanel =
                FindChildByName(transform, "DetailPanel");

            if (foundDetailPanel != null)
            {
                detailPanel = foundDetailPanel.gameObject;
            }
        }

        if (detailPanel != null)
        {
            Transform detailTransform = detailPanel.transform;

            if (detailIcon == null)
            {
                Transform foundDetailIcon =
                    FindChildByName(detailTransform, "DetailIcon");

                if (foundDetailIcon != null)
                {
                    detailIcon =
                        foundDetailIcon.GetComponent<Image>();
                }
            }

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

        if (buyPanel != null &&
            !buyPanel.transform.IsChildOf(transform))
        {
            buyPanel = null;
            buyButton = null;
            buyButtonText = null;
        }

        if (buyPanel == null)
        {
            Transform foundBuyPanel =
                FindChildByName(
                    transform,
                    "BuyPanel");

            if (foundBuyPanel != null)
            {
                buyPanel = foundBuyPanel.gameObject;
            }
        }

        if (buyPanel == null)
        {
            Transform foundBuyPanel =
                FindChildByName(
                    GetComponentInParent<Canvas>() != null
                    ? GetComponentInParent<Canvas>().transform
                    : transform,
                    "ShopBuyPanel");

            if (foundBuyPanel != null)
            {
                buyPanel = foundBuyPanel.gameObject;
            }
        }

        if (buyPanel == null &&
            !TemplateUsesInlineBuyButton())
        {
            CreateShopBuyPanel();
        }

        if (buyPanel != null)
        {
            if (buyButton == null)
            {
                Transform foundBuyButton =
                    FindChildByName(
                        buyPanel.transform,
                        "BuyButton");

                if (foundBuyButton != null)
                {
                    buyButton =
                        foundBuyButton.GetComponent<Button>();
                }
            }

            if (buyButtonText == null &&
                buyButton != null)
            {
                buyButtonText =
                    buyButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (moneyText == null)
            {
                Transform foundMoneyText =
                    FindChildByName(
                        buyPanel.transform,
                        "MoneyText");

                if (foundMoneyText != null)
                {
                    moneyText =
                        foundMoneyText.GetComponent<TMP_Text>();
                }
            }
        }
    }

    void CreateShopBuyPanel()
    {
        GameObject panelObject =
            new GameObject(
                "ShopBuyPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        panelObject.transform.SetParent(transform, false);

        RectTransform rect =
            panelObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = buyPanelSize;

        Image image =
            panelObject.GetComponent<Image>();

        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        buyPanel = panelObject;
        buyPanel.SetActive(false);
        EnsureBuyButtonExists();
    }

    Transform FindChildByName(
        Transform parent,
        string childName)
    {
        if (parent == null ||
            string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        string normalizedName =
            NormalizeNodeName(childName);

        foreach (Transform child in parent)
        {
            if (string.Equals(
                    child.name,
                    childName,
                    StringComparison.OrdinalIgnoreCase) ||
                NormalizeNodeName(child.name) == normalizedName)
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

    static string NormalizeNodeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder =
            new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    bool TemplateUsesInlineBuyButton()
    {
        if (itemButtonPrefab == null)
        {
            return false;
        }

        return FindChildByName(
                itemButtonPrefab.transform,
                "Button Mua") != null ||
            FindChildByName(
                itemButtonPrefab.transform,
                "BuyButton") != null;
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

    TMP_Text FindCategoryLabelText(string nodeName)
    {
        Transform node =
            FindChildByName(transform, nodeName);
        if (node == null)
        {
            return null;
        }

        TMP_Text direct =
            node.GetComponent<TMP_Text>();
        if (direct != null)
        {
            return direct;
        }

        return node.GetComponentInChildren<TMP_Text>(true);
    }

    void RebuildItemGrid(bool resetScrollPosition = true)
    {
        RefreshShopSource();

        if (itemGridParent == null ||
            itemButtonPrefab == null ||
            shop == null)
        {
            return;
        }

        RectTransform contentRect =
            itemGridParent as RectTransform;
        Vector2 preservedContentPosition =
            contentRect != null
                ? contentRect.anchoredPosition
                : Vector2.zero;

        if (autoConfigureGrid)
        {
            ConfigureItemGrid(resetScrollPosition);
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

        for (int i = 0; i < shop.ItemCount; i++)
        {
            ShopItemSlot slot =
                shop.GetSlot(i);

            if (slot == null ||
                slot.item == null ||
                slot.amount <= 0 ||
                slot.item.itemType != currentType)
            {
                continue;
            }

            ShopItemButtonUI button =
                Instantiate(
                    itemButtonPrefab,
                    itemGridParent);

            button.gameObject.SetActive(true);
            ResetItemButtonTransform(button);
            button.Setup(this, i, slot);
            spawnedButtons.Add(button);
        }

        Canvas.ForceUpdateCanvases();

        if (resetScrollPosition)
        {
            ResetItemGridScrollPosition();
            return;
        }

        if (contentRect != null)
        {
            contentRect.anchoredPosition =
                preservedContentPosition;
        }
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
        // Chặn click xuyên qua bảng chi tiết hoặc nút mua.
        if (IsPointerInsideDetailOrBuyPanel(screenPosition, eventCamera))
        {
            return;
        }

        foreach (ShopItemButtonUI button in spawnedButtons)
        {
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

            SelectItem(button.ItemIndex);
            return;
        }

        if (selectedItemIndex >= 0 &&
            !IsPointerInsideDetailOrBuyPanel(
                screenPosition,
                eventCamera))
        {
            selectedItemIndex = -1;
            ClearDetail();
        }
    }

    Camera GetEventCamera()
    {
        if (rootCanvas == null ||
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (rootCanvas.worldCamera != null)
        {
            return rootCanvas.worldCamera;
        }

        return Camera.main;
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

        if (IsPointerInsideBottomMenuButton(screenPosition, eventCamera))
        {
            return false;
        }

        return !IsPointerInsideShopUi(screenPosition, eventCamera);
    }

    bool IsPointerInsideBottomMenuButton(
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

    bool IsPointerInsideShopUi(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (IsPointerInsidePanel(
                panelRoot,
                screenPosition,
                eventCamera) ||
            IsPointerInsidePanel(
                detailPanel,
                screenPosition,
                eventCamera) ||
            IsPointerInsidePanel(
                buyPanel,
                screenPosition,
                eventCamera))
        {
            return true;
        }

        if (itemGridParent is RectTransform itemGridRect &&
            itemGridParent.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(
                itemGridRect,
                screenPosition,
                eventCamera))
        {
            return true;
        }

        return IsPointerInsideChildGraphic(
                transform,
                screenPosition,
                eventCamera) ||
            (panelRoot != null &&
                IsPointerInsideChildGraphic(
                    panelRoot.transform,
                    screenPosition,
                    eventCamera));
    }

    bool IsPointerInsideChildGraphic(
        Transform root,
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (root == null ||
            !root.gameObject.activeInHierarchy)
        {
            return false;
        }

        Graphic[] graphics =
            root.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null ||
                !graphic.gameObject.activeInHierarchy ||
                !graphic.raycastTarget)
            {
                continue;
            }

            RectTransform rect =
                graphic.rectTransform;

            if (rect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    rect,
                    screenPosition,
                    eventCamera))
            {
                return true;
            }
        }

        return false;
    }

    bool IsPointerInsideDetailOrBuyPanel(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        return IsPointerInsidePanel(
                detailPanel,
                screenPosition,
                eventCamera) ||
            IsPointerInsidePanel(
                buyPanel,
                screenPosition,
                eventCamera);
    }

    bool IsBuyPanelInsideDetailPanel()
    {
        return buyPanel != null &&
            detailPanel != null &&
            buyPanel.transform.IsChildOf(detailPanel.transform);
    }

    bool IsPointerInsidePanel(
        GameObject panel,
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (panel == null ||
            !panel.activeInHierarchy)
        {
            return false;
        }

        RectTransform rect =
            panel.GetComponent<RectTransform>();

        if (rect == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            screenPosition,
            eventCamera);
    }

    void BringDetailPanelsToFront()
    {
        if (detailPanel != null)
        {
            detailPanel.transform.SetAsLastSibling();
        }

        if (buyPanel != null)
        {
            buyPanel.transform.SetAsLastSibling();
        }
    }

    void EnsureBuyPanelVisible()
    {
        if (buyPanel == null)
        {
            return;
        }

        buyPanel.SetActive(true);
        buyPanel.transform.SetAsLastSibling();

        RectTransform buyRect =
            buyPanel.GetComponent<RectTransform>();

        if (buyRect != null)
        {
            buyRect.localScale = Vector3.one;

            if (buyRect.sizeDelta.x <= 1f ||
                buyRect.sizeDelta.y <= 1f)
            {
                buyRect.sizeDelta = buyPanelSize;
            }
        }

        CanvasGroup group =
            buyPanel.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = buyPanel.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        foreach (Transform child in buyPanel.transform)
        {
            child.gameObject.SetActive(true);
        }

        EnsureBuyButtonExists();
    }

    void EnsureBuyButtonExists()
    {
        if (buyPanel == null)
        {
            return;
        }

        if (buyButton == null)
        {
            Transform foundBuyButton =
                FindChildByName(
                    buyPanel.transform,
                    "BuyButton");

            if (foundBuyButton != null)
            {
                buyButton =
                    foundBuyButton.GetComponent<Button>();
            }
        }

        if (buyButton == null)
        {
            GameObject buttonObject =
                new GameObject(
                    "BuyButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));

            buttonObject.transform.SetParent(
                buyPanel.transform,
                false);

            RectTransform buttonRect =
                buttonObject.GetComponent<RectTransform>();

            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(90f, 36f);

            Image image =
                buttonObject.GetComponent<Image>();

            image.color = new Color(1f, 1f, 1f, 0.85f);

            buyButton =
                buttonObject.GetComponent<Button>();

            GameObject textObject =
                new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));

            textObject.transform.SetParent(
                buttonObject.transform,
                false);

            RectTransform textRect =
                textObject.GetComponent<RectTransform>();

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            buyButtonText =
                textObject.GetComponent<TextMeshProUGUI>();

            buyButtonText.alignment =
                TextAlignmentOptions.Center;

            buyButtonText.fontSize = 18f;
            buyButtonText.color = Color.black;
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(BuySelectedItem);
        buyButton.gameObject.SetActive(true);

        if (buyButtonText == null)
        {
            buyButtonText =
                buyButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    void PositionDetailAndBuyPanel(int itemIndex)
    {
        ShopItemButtonUI selectedButton =
            spawnedButtons.Find(
                button =>
                    button != null &&
                    button.ItemIndex == itemIndex);

        if (selectedButton == null)
        {
            return;
        }

        RectTransform itemRect =
            selectedButton.GetComponent<RectTransform>();

        if (itemRect == null)
        {
            return;
        }

        if (moveDetailBesideSelectedItem &&
            detailPanel != null)
        {
            RectTransform detailRect =
                detailPanel.GetComponent<RectTransform>();

            if (detailRect != null)
            {
                MovePanelBesideItem(
                    detailRect,
                    itemRect,
                    detailOffset);
            }
        }

        if (moveBuyPanelBelowSelectedItem &&
            buyPanel != null &&
            !IsBuyPanelInsideDetailPanel())
        {
            RectTransform buyRect =
                buyPanel.GetComponent<RectTransform>();

            if (buyRect != null)
            {
                MovePanelBelowItem(
                    buyRect,
                    itemRect,
                    buyOffset);
            }
        }
    }

    void MovePanelBesideItem(
        RectTransform panelRect,
        RectTransform itemRect,
        Vector2 offset)
    {
        RectTransform parent =
            panelRect.parent as RectTransform;

        if (parent == null)
        {
            return;
        }

        Bounds itemBounds =
            RectTransformUtility.CalculateRelativeRectTransformBounds(
                parent,
                itemRect);

        panelRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        panelRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        panelRect.pivot =
            new Vector2(0f, 0.5f);

        Vector2 localPos =
            new Vector2(
                itemBounds.max.x,
                itemBounds.center.y) +
            offset;

        panelRect.anchoredPosition =
            ClampLocalPointToParent(
                parent,
                panelRect,
                localPos);
    }

    void MovePanelBelowItem(
        RectTransform panelRect,
        RectTransform itemRect,
        Vector2 offset)
    {
        RectTransform parent =
            panelRect.parent as RectTransform;

        if (parent == null)
        {
            return;
        }

        Bounds itemBounds =
            RectTransformUtility.CalculateRelativeRectTransformBounds(
                parent,
                itemRect);

        panelRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        panelRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        panelRect.pivot =
            new Vector2(0.5f, 1f);

        panelRect.sizeDelta =
            buyPanelSize;

        Vector2 localPos =
            new Vector2(
                itemBounds.center.x,
                itemBounds.min.y) +
            offset;

        panelRect.anchoredPosition =
            ClampLocalPointToParent(
                parent,
                panelRect,
                localPos);
    }

    Vector2 ClampLocalPointToParent(
        RectTransform parent,
        RectTransform panelRect,
        Vector2 localPoint)
    {
        Rect parentRect =
            parent.rect;

        Vector2 size =
            panelRect.rect.size;

        if (size.x <= 0f ||
            size.y <= 0f)
        {
            size = panelRect.sizeDelta;
        }

        float minX =
            parentRect.xMin +
            size.x * panelRect.pivot.x;

        float maxX =
            parentRect.xMax -
            size.x * (1f - panelRect.pivot.x);

        float minY =
            parentRect.yMin +
            size.y * panelRect.pivot.y;

        float maxY =
            parentRect.yMax -
            size.y * (1f - panelRect.pivot.y);

        if (minX <= maxX)
        {
            localPoint.x =
                Mathf.Clamp(
                    localPoint.x,
                    minX,
                    maxX);
        }

        if (minY <= maxY)
        {
            localPoint.y =
                Mathf.Clamp(
                    localPoint.y,
                    minY,
                    maxY);
        }

        return localPoint -
            parentRect.center;
    }

    void ConfigureBuyPanelLayout()
    {
        if (!autoLayoutBuyPanel ||
            buyPanel == null)
        {
            return;
        }

        RectTransform buyRect =
            buyPanel.GetComponent<RectTransform>();

        if (buyRect != null)
        {
            buyRect.sizeDelta =
                buyPanelSize;
        }

        if (!autoLayoutBuyPanel)
        {
            return;
        }

        VerticalLayoutGroup layout =
            buyPanel.GetComponent<VerticalLayoutGroup>();

        if (layout == null)
        {
            layout =
                buyPanel.AddComponent<VerticalLayoutGroup>();
        }

        layout.padding =
            new RectOffset(4, 4, 4, 4);

        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            buyPanel.GetComponent<ContentSizeFitter>();

        if (fitter == null)
        {
            fitter =
                buyPanel.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit =
            ContentSizeFitter.FitMode.Unconstrained;

        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        ConfigureBuyChild("MinusButton", 32f);
        ConfigureBuyChild("PlusButton", 32f);
        ConfigureBuyChild("BuyButton", 40f);
        ConfigureBuyChild("QuantityText", 24f);
        ConfigureBuyChild("MoneyText", 24f);
    }

    void RemoveBuyPanelAutoLayoutIfDisabled()
    {
        if (autoLayoutBuyPanel ||
            buyPanel == null)
        {
            return;
        }

        VerticalLayoutGroup layout =
            buyPanel.GetComponent<VerticalLayoutGroup>();

        if (layout != null)
        {
            Destroy(layout);
        }

        ContentSizeFitter fitter =
            buyPanel.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            Destroy(fitter);
        }
    }

    void ConfigureBuyChild(
        string childName,
        float preferredHeight)
    {
        Transform child =
            buyPanel.transform.Find(childName);

        if (child == null)
        {
            return;
        }

        LayoutElement element =
            child.GetComponent<LayoutElement>();

        if (element == null)
        {
            element =
                child.gameObject.AddComponent<LayoutElement>();
        }

        element.preferredHeight =
            preferredHeight;
    }

    void ConfigureItemGrid(bool resetScrollPosition)
    {
        RectTransform contentRect =
            itemGridParent as RectTransform;

        if (contentRect == null)
        {
            return;
        }

        contentRect.anchorMin =
            new Vector2(0f, 1f);

        contentRect.anchorMax =
            new Vector2(1f, 1f);

        contentRect.pivot =
            new Vector2(0f, 1f);

        if (resetScrollPosition)
        {
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

        if (grid.cellSize.x <= 0f ||
            grid.cellSize.y <= 0f)
        {
            grid.cellSize = itemCellSize;
        }

        if (grid.spacing == Vector2.zero)
        {
            grid.spacing = itemSpacing;
        }

        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;

        if (grid.constraint ==
                GridLayoutGroup.Constraint.Flexible ||
            grid.constraintCount <= 0)
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
    }

    void ResetItemGridScrollPosition()
    {
        RectTransform contentRect =
            itemGridParent as RectTransform;

        if (contentRect == null)
        {
            return;
        }

        contentRect.anchoredPosition =
            Vector2.zero;

        ScrollRect scrollRect =
            itemGridParent.GetComponentInParent<ScrollRect>();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void ResetItemButtonTransform(ShopItemButtonUI button)
    {
        RectTransform rect =
            button.GetComponent<RectTransform>();

        if (rect == null)
        {
            return;
        }

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition3D = Vector3.zero;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        LayoutElement element =
            button.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.preferredWidth = -1f;
            element.preferredHeight = -1f;
            element.minWidth = -1f;
            element.minHeight = -1f;
            element.flexibleWidth = -1f;
            element.flexibleHeight = -1f;
        }
    }

    void ClearDetail()
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }

        if (buyPanel != null)
        {
            buyPanel.SetActive(false);
        }

        if (buyButton != null)
        {
            buyButton.interactable = false;
        }
    }

    public void RefreshMoney()
    {
        if (moneyText != null &&
            playerWallet != null)
        {
            moneyText.text =
                NpcEconomy.FormatCurrency(playerWallet.LinhThach);
        }

        PlayerWalletTextUI[] walletTexts =
            FindObjectsByType<PlayerWalletTextUI>(
                FindObjectsInactive.Include);

        foreach (PlayerWalletTextUI walletText in walletTexts)
        {
            if (walletText != null)
            {
                walletText.Refresh();
            }
        }

        RefreshSpawnedButtonStates();
    }

    void RefreshBuyButton()
    {
        if (buyButton == null)
        {
            return;
        }

        ShopItemSlot slot =
            shop.GetSlot(selectedItemIndex);

        bool canTrade =
            slot != null &&
            slot.item != null &&
            NpcEconomy.CanTradeNormally(slot.item);

        bool canBuy =
            canTrade &&
            slot.amount > 0 &&
            CanPay(GetDisplayPrice(slot.item));

        buyButton.interactable = canBuy;

        if (buyButtonText != null)
        {
            buyButtonText.text =
                canBuy
                ? UiText.Get("shop", "buyButtonBuy")
                : canTrade
                    ? UiText.Get("shop", "buyButtonNotEnough")
                    : UiText.Get("shop", "buyButtonUnavailable");
        }
    }

    bool CanPay(int price)
    {
        if (price <= 0)
        {
            return true;
        }

        return playerWallet != null &&
            playerWallet.CanPay(price);
    }

    void RefreshSpawnedButtonStates()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            ShopItemButtonUI button = spawnedButtons[i];
            if (button != null)
            {
                button.RefreshAffordability();
            }
        }
    }

    void RefreshAnimatedGradeDetail()
    {
        if (detailGradeText == null ||
            detailPanel == null ||
            !detailPanel.activeInHierarchy ||
            selectedItemIndex < 0 ||
            shop == null)
        {
            return;
        }

        ShopItemSlot slot =
            shop.GetSlot(selectedItemIndex);
        if (slot == null ||
            slot.item == null)
        {
            return;
        }

        float pulse =
            0.72f +
            0.28f * Mathf.Sin(
                Time.unscaledTime * 2.4f +
                detailGradePulseSeed);
        ApplyGradeTextStyle(
            detailGradeText,
            slot.item.grade,
            pulse);
    }

    void SyncCategoryLabels()
    {
        ApplyCategoryLabel(
            vatLieuCategoryText,
            ItemType.VatLieu,
            currentType == ItemType.VatLieu,
            "VatLieu");
        ApplyCategoryLabel(
            congPhapCategoryText,
            ItemType.CongPhap,
            currentType == ItemType.CongPhap,
            "Congphap");
        ApplyCategoryLabel(
            danDuocCategoryText,
            ItemType.DanDuoc,
            currentType == ItemType.DanDuoc,
            "DanDuoc");
        ApplyCategoryLabel(
            phapBaoCategoryText,
            ItemType.PhapBao,
            currentType == ItemType.PhapBao,
            "Phapbao");
    }

    void ApplyCategoryLabel(
        TMP_Text label,
        ItemType itemType,
        bool selected,
        string fallback)
    {
        if (label == null)
        {
            return;
        }

        label.text =
            ResolveCategoryTitle(
                itemType,
                fallback);
        label.fontStyle =
            selected
                ? FontStyles.Bold
                : FontStyles.Normal;
        label.color =
            selected
                ? new Color(0.20f, 0.14f, 0.08f, 1f)
                : new Color(0.26f, 0.24f, 0.22f, 0.94f);
    }

    void ApplyShopkeeperCategorySpeech(ItemType itemType)
    {
        string key = "speechGreetingLines";
        switch (itemType)
        {
            case ItemType.DanDuoc:
                key = "speechDanDuocLines";
                break;
            case ItemType.PhapBao:
                key = "speechPhapBaoLines";
                break;
            case ItemType.CongPhap:
                key = "speechCongPhapLines";
                break;
            case ItemType.VatLieu:
                key = "speechVatLieuLines";
                break;
            case ItemType.ThucPham:
                key = "speechThucPhamLines";
                break;
        }

        ApplyShopkeeperSpeechList(
            key,
            UiText.Get("shop", "speechCategoryFallback", ""));
    }

    void ApplyShopkeeperInspectSpeech(StatItemData item)
    {
        if (item == null)
        {
            return;
        }

        ApplyShopkeeperSpeechList(
            GetInspectSpeechKey(item),
            UiText.Get("shop", "speechInspectFallback", ""),
            ItemText.Name(item),
            ItemText.GradeLong(item.grade));
    }

    string GetInspectSpeechKey(StatItemData item)
    {
        if (item == null)
        {
            return "speechInspectFallbackLines";
        }

        switch (item.itemType)
        {
            case ItemType.VatLieu:
                return GetVatLieuInspectSpeechKey(item);
            case ItemType.CongPhap:
                return GetCongPhapInspectSpeechKey(item);
            case ItemType.DanDuoc:
                return GetDanDuocInspectSpeechKey(item);
            case ItemType.PhapBao:
                return GetPhapBaoInspectSpeechKey(item);
            case ItemType.ThucPham:
                return GetThucPhamInspectSpeechKey(item);
            default:
                return "speechInspectFallbackLines";
        }
    }

    string GetVatLieuInspectSpeechKey(StatItemData item)
    {
        if (item.materialKind == MaterialKind.Herb)
        {
            return "speechInspectVatLieuHerbLines";
        }

        if (item.materialKind == MaterialKind.Ore)
        {
            return "speechInspectVatLieuOreLines";
        }

        if (item.materialKind == MaterialKind.SpiritStone)
        {
            return "speechInspectVatLieuSpiritStoneLines";
        }

        if (item.materialKind == MaterialKind.CraftingPart)
        {
            return "speechInspectVatLieuCraftingPartLines";
        }

        return "speechInspectVatLieuGeneralLines";
    }

    string GetCongPhapInspectSpeechKey(StatItemData item)
    {
        if (item.manualKind == ManualKind.Attack)
        {
            return "speechInspectCongPhapAttackLines";
        }

        if (item.manualKind == ManualKind.Defense)
        {
            return "speechInspectCongPhapDefenseLines";
        }

        if (item.manualKind == ManualKind.Movement)
        {
            return "speechInspectCongPhapMovementLines";
        }

        if (item.manualKind == ManualKind.Cultivation)
        {
            return "speechInspectCongPhapCultivationLines";
        }

        return "speechInspectCongPhapGeneralLines";
    }

    string GetDanDuocInspectSpeechKey(StatItemData item)
    {
        if (item.breakthroughRealm)
        {
            return "speechInspectDanDuocBreakthroughLines";
        }

        if (item.cultivationBonus > 0)
        {
            return "speechInspectDanDuocCultivationLines";
        }

        if (item.hpBonus > 0)
        {
            return "speechInspectDanDuocHpLines";
        }

        return "speechInspectDanDuocGeneralLines";
    }

    string GetPhapBaoInspectSpeechKey(StatItemData item)
    {
        bool isProtective =
            item.armorBonus > 0 ||
            item.armorBonusPercent > 0 ||
            item.maxHpBonusPercent > 0 ||
            item.effectResistanceBonus > 0;
        bool isOffensive =
            (item.damageBonus > 0 ||
            item.damageBonusPercent > 0) &&
            Mathf.Max(item.damageBonus, item.damageBonusPercent) >=
                Mathf.Max(
                    item.armorBonus,
                    Mathf.Max(item.armorBonusPercent, item.effectResistanceBonus));

        if (isProtective)
        {
            if (item.effectResistanceBonus > item.armorBonus)
            {
                return "speechInspectPhapBaoResistanceLines";
            }

            return "speechInspectPhapBaoProtectLines";
        }

        if (isOffensive)
        {
            return "speechInspectPhapBaoOffenseLines";
        }

        return "speechInspectPhapBaoGeneralLines";
    }

    string GetThucPhamInspectSpeechKey(StatItemData item)
    {
        if (item.foodKind == FoodKind.Meal ||
            item.foodKind == FoodKind.Meat ||
            item.foodKind == FoodKind.Fish)
        {
            return "speechInspectThucPhamNourishLines";
        }

        if (item.foodKind == FoodKind.Grain)
        {
            return "speechInspectThucPhamGrainLines";
        }

        if (item.foodKind == FoodKind.SpiritFruit)
        {
            return "speechInspectThucPhamSpiritFruitLines";
        }

        return "speechInspectThucPhamGeneralLines";
    }

    void ApplyShopkeeperPurchaseSpeech(StatItemData item)
    {
        if (item == null)
        {
            return;
        }

        string fallback =
            C(
                UiText.Get("shop", "speechBuyFallback", ""));
        switch (item.grade)
        {
            case ItemGrade.Trung:
                fallback =
                    UiText.Get("shop", "speechBuyFallbackTrung", fallback);
                break;
            case ItemGrade.Thuong:
                fallback =
                    UiText.Get("shop", "speechBuyFallbackThuong", fallback);
                break;
            case ItemGrade.Tien:
                fallback =
                    UiText.Get("shop", "speechBuyFallbackTien", fallback);
                break;
        }

        ApplyShopkeeperSpeechList(
            "speechBuy" + item.grade + "Lines",
            fallback,
            ItemText.Name(item),
            ItemText.GradeLong(item.grade));
    }

    void ApplyShopkeeperSpeechList(
        string key,
        string fallback,
        params object[] args)
    {
        if (npcSpeechText == null)
        {
            return;
        }

        string[] lines =
            UiText.Lines("shop", key);
        string line = fallback;
        if (lines != null &&
            lines.Length > 0)
        {
            line = lines[
                Mathf.Abs(Time.frameCount + key.GetHashCode()) %
                lines.Length];
        }

        npcSpeechText.text =
            args != null && args.Length > 0
                ? NpcText.Format(line, args)
                : line;
        RefreshSpeechBubbleLayout();
    }

    void CacheSpeechBubbleBaseSize()
    {
        if (hasSpeechBubbleBaseSize ||
            npcSpeechBubbleRect == null)
        {
            return;
        }

        Vector2 rectSize =
            npcSpeechBubbleRect.rect.size;
        if (rectSize.x <= 0f ||
            rectSize.y <= 0f)
        {
            return;
        }

        speechBubbleBaseSize = rectSize;
        hasSpeechBubbleBaseSize = true;
    }

    void RefreshSpeechBubbleLayout()
    {
        if (npcSpeechText == null)
        {
            return;
        }

        if (npcSpeechBubbleRect == null)
        {
            Transform foundSpeechBubble =
                FindChildByName(transform, "SpeechBubble");
            if (foundSpeechBubble != null)
            {
                npcSpeechBubbleRect =
                    foundSpeechBubble.GetComponent<RectTransform>();
            }
        }

        if (npcSpeechBubbleRect == null)
        {
            return;
        }

        CacheSpeechBubbleBaseSize();

        RectTransform textRect =
            npcSpeechText.rectTransform;
        if (textRect == null)
        {
            return;
        }

        Vector2 baseSize =
            hasSpeechBubbleBaseSize
                ? speechBubbleBaseSize
                : speechBubbleMinSize;
        float minBubbleWidth =
            Mathf.Max(speechBubbleMinSize.x, baseSize.x * 0.76f);
        float minBubbleHeight =
            Mathf.Max(speechBubbleMinSize.y, baseSize.y);
        float maxBubbleWidth =
            Mathf.Max(minBubbleWidth, speechBubbleMaxWidth);
        float maxTextWidth =
            Mathf.Max(140f, maxBubbleWidth - speechBubblePadding.x);

        npcSpeechText.ForceMeshUpdate();
        Vector2 preferred =
            npcSpeechText.GetPreferredValues(
                npcSpeechText.text,
                maxTextWidth,
                0f);

        float bubbleWidth =
            Mathf.Clamp(
                preferred.x + speechBubblePadding.x,
                minBubbleWidth,
                maxBubbleWidth);
        float textWidth =
            Mathf.Max(120f, bubbleWidth - speechBubblePadding.x);

        preferred =
            npcSpeechText.GetPreferredValues(
                npcSpeechText.text,
                textWidth,
                0f);

        float bubbleHeight =
            Mathf.Max(
                minBubbleHeight,
                preferred.y + speechBubblePadding.y);

        textRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            textWidth);
        textRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            preferred.y + 4f);
        npcSpeechBubbleRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            bubbleWidth);
        npcSpeechBubbleRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            bubbleHeight);

        LayoutRebuilder.MarkLayoutForRebuild(textRect);
        LayoutRebuilder.MarkLayoutForRebuild(npcSpeechBubbleRect);
    }

    static string C(string value)
    {
        return NpcText.CleanDisplayText(value);
    }

    public static Color GetGradeBaseColor(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return new Color(0.70f, 0.83f, 0.96f, 1f);
            case ItemGrade.Trung:
                return new Color(0.54f, 0.96f, 0.67f, 1f);
            case ItemGrade.Thuong:
                return new Color(1.00f, 0.78f, 0.33f, 1f);
            case ItemGrade.Tien:
                return new Color(0.93f, 0.48f, 0.94f, 1f);
            default:
                return Color.white;
        }
    }

    public static Color GetGradeAccentColor(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return new Color(0.90f, 0.96f, 1f, 1f);
            case ItemGrade.Trung:
                return new Color(0.90f, 1f, 0.92f, 1f);
            case ItemGrade.Thuong:
                return new Color(1f, 0.94f, 0.62f, 1f);
            case ItemGrade.Tien:
                return new Color(1f, 0.82f, 0.98f, 1f);
            default:
                return Color.white;
        }
    }

    public static void ApplyGradeTextStyle(
        TMP_Text text,
        ItemGrade grade,
        float pulse)
    {
        if (text == null)
        {
            return;
        }

        Color baseColor =
            GetGradeBaseColor(grade);
        Color accentColor =
            GetGradeAccentColor(grade);
        float glowStrength =
            Mathf.Clamp01(pulse);

        text.enableVertexGradient = true;
        text.fontStyle |= FontStyles.Bold;
        text.color =
            Color.Lerp(
                baseColor,
                accentColor,
                0.18f + glowStrength * 0.38f);
        text.colorGradient =
            new VertexGradient(
                Color.Lerp(accentColor, Color.white, 0.15f),
                Color.Lerp(accentColor, Color.white, 0.10f),
                Color.Lerp(baseColor, Color.black, 0.08f),
                Color.Lerp(baseColor, Color.black, 0.12f));
        text.outlineWidth = 0.18f;
        text.outlineColor =
            Color.Lerp(
                new Color(0.15f, 0.09f, 0.03f, 0.9f),
                baseColor,
                0.25f);
    }

    string BuildStatsText(StatItemData item)
    {
        StringBuilder builder =
            new StringBuilder();

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
}
