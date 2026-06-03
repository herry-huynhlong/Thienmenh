using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcInventoryPanelUI : MonoBehaviour
{
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text infoText;
    public TMP_Text itemsText;
    public InventoryPanelUI itemGridPanel;
    public bool useItemGrid = true;
    public bool hideItemsTextWhenUsingGrid = true;
    public bool readOnly = true;
    public string emptyText = "Không có vật phẩm";
    public bool blockMapDrag;
    public float infoRefreshInterval = 0.5f;
    public bool autoCreateInfoText = true;
    public float autoInfoHeight = 64f;


    Transform currentNpc;
    ItemInventory currentInventory;
    bool gridDirty = true;
    float refreshTimer;
    bool createdInfoText;


    void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        AutoFindItemGridPanel();
        SanitizeCopiedInventoryGrid();
        ConfigureRaycasts();
        Hide();
    }

    public void Show(Transform npc)
    {
        if (npc == null)
        {
            Hide();
            return;
        }

        ItemInventory inventory =
            npc.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory = npc.gameObject.AddComponent<ItemInventory>();
        }

        inventory.UsePrivateNpcRuntimeItems(false);

        if (currentNpc != npc ||
            currentInventory != inventory)
        {
            Unsubscribe();
            currentNpc = npc;
            currentInventory = inventory;
            currentInventory.OnChanged += OnInventoryChanged;
            gridDirty = true;
            BindItemGrid();
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (currentNpc == null ||
            currentInventory == null)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.text =
                GetNpcName(currentNpc) + " - Kho đồ";
        }

        EnsureInfoText();

        if (infoText != null)
        {
            infoText.text = createdInfoText
                ? BuildCompactInfoText(currentNpc, currentInventory)
                : BuildInfoText(currentNpc, currentInventory);
            infoText.gameObject.SetActive(true);
        }

        if (itemsText != null)
        {
            itemsText.gameObject.SetActive(
                !ShouldUseItemGrid() ||
                !hideItemsTextWhenUsingGrid);

            if (itemsText.gameObject.activeSelf)
            {
                itemsText.text =
                    BuildItemsText(currentInventory);
            }
        }

        if (ShouldUseItemGrid())
        {
            BindItemGrid();
            if (gridDirty)
            {
                itemGridPanel.Refresh(true);
                gridDirty = false;
            }

            ReserveGridTopSpace();
        }
    }

    public void Hide()
    {
        Unsubscribe();
        currentNpc = null;
        currentInventory = null;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (itemGridPanel != null &&
            itemGridPanel.panelRoot != panelRoot)
        {
            if (itemGridPanel.panelRoot != null)
            {
                SetCanvasGroupVisible(
                    itemGridPanel.panelRoot,
                    false);
            }
        }
    }

    public void HideContentOnly()
    {
        SetContentVisible(false);
    }

    public void SetContentVisible(bool visible)
    {
        SetTextVisible(titleText, visible);
        SetTextVisible(infoText, visible);

        if (itemsText != null)
        {
            bool showText =
                visible &&
                (!ShouldUseItemGrid() ||
                !hideItemsTextWhenUsingGrid);

            itemsText.gameObject.SetActive(showText);
        }

        if (ShouldUseItemGrid())
        {
            BindItemGrid();

            if (visible)
            {
                if (itemGridPanel.panelRoot != null)
                {
                    SetCanvasGroupVisible(
                        itemGridPanel.panelRoot,
                        true);
                }

                if (gridDirty)
                {
                    itemGridPanel.Refresh(true);
                    gridDirty = false;
                }

                ReserveGridTopSpace();
            }
            else if (itemGridPanel.panelRoot != panelRoot)
            {
                if (itemGridPanel.panelRoot != null)
                {
                    SetCanvasGroupVisible(
                        itemGridPanel.panelRoot,
                        false);
                }
            }
        }
    }


    void EnsureInfoText()
    {
        if (infoText != null ||
            !autoCreateInfoText ||
            panelRoot == null)
        {
            return;
        }

        GameObject textObject =
            new GameObject("NpcInventoryValueText", typeof(RectTransform));

        textObject.transform.SetParent(panelRoot.transform, false);
        textObject.transform.SetAsFirstSibling();

        RectTransform rect =
            textObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -12f);
        rect.sizeDelta = new Vector2(-28f, autoInfoHeight);

        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();

        text.fontSize = 20f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 20f;
        text.alignment = TextAlignmentOptions.TopRight;
        text.color = new Color(1f, 0.92f, 0.55f, 1f);
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;

        infoText = text;
        createdInfoText = true;
    }

    void ReserveGridTopSpace()
    {
        if (!createdInfoText ||
            itemGridPanel == null ||
            itemGridPanel.itemGridParent == null)
        {
            return;
        }

        RectTransform gridRect =
            itemGridPanel.itemGridParent as RectTransform;

        if (gridRect == null)
        {
            return;
        }

        gridRect.anchoredPosition =
            new Vector2(
                gridRect.anchoredPosition.x,
                -autoInfoHeight);
    }

    void SanitizeCopiedInventoryGrid()
    {
        if (itemGridPanel == null)
        {
            return;
        }

        itemGridPanel.readOnly = readOnly;
        itemGridPanel.closeOnStart = false;
        itemGridPanel.alwaysVisible = false;
        itemGridPanel.bringToFrontOnOpen = false;
        itemGridPanel.closeWhenClickOutside = false;

        GameObject gridRoot = itemGridPanel.panelRoot != null
            ? itemGridPanel.panelRoot
            : itemGridPanel.gameObject;

        ShopPanelUI[] shopPanels =
            gridRoot.GetComponentsInChildren<ShopPanelUI>(true);

        foreach (ShopPanelUI shopPanel in shopPanels)
        {
            if (shopPanel != null)
            {
                shopPanel.enabled = false;
            }
        }

        PlayerWallet[] wallets =
            gridRoot.GetComponentsInChildren<PlayerWallet>(true);

        foreach (PlayerWallet wallet in wallets)
        {
            if (wallet != null)
            {
                wallet.enabled = false;
            }
        }
    }
    void SetCanvasGroupVisible(
        GameObject target,
        bool visible)
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

    void SetTextVisible(TMP_Text text, bool visible)
    {
        if (text != null)
        {
            text.gameObject.SetActive(visible);
        }
    }

    void AutoFindItemGridPanel()
    {
        if (itemGridPanel != null)
        {
            return;
        }

        if (panelRoot != null)
        {
            itemGridPanel =
                panelRoot.GetComponent<InventoryPanelUI>();

            if (itemGridPanel != null)
            {
                return;
            }

            itemGridPanel =
                panelRoot.GetComponentInChildren<InventoryPanelUI>(true);

            if (itemGridPanel != null)
            {
                return;
            }
        }

        itemGridPanel =
            GetComponentInChildren<InventoryPanelUI>(true);
    }

    bool ShouldUseItemGrid()
    {
        AutoFindItemGridPanel();
        return useItemGrid &&
            itemGridPanel != null &&
            currentInventory != null;
    }

    void BindItemGrid()
    {
        if (!ShouldUseItemGrid())
        {
            return;
        }

        itemGridPanel.inventory = currentInventory;
        itemGridPanel.closeOnStart = false;
        itemGridPanel.readOnly = readOnly;
        itemGridPanel.bringToFrontOnOpen = false;
        itemGridPanel.alwaysVisible = false;

        if (itemGridPanel.panelRoot != null)
        {
            if (!itemGridPanel.panelRoot.activeSelf)
            {
                itemGridPanel.panelRoot.SetActive(true);
            }
        }
    }

    void Update()
    {
        if (panelRoot == null ||
            !panelRoot.activeInHierarchy ||
            currentNpc == null)
        {
            return;
        }

        refreshTimer += Time.deltaTime;
        if (refreshTimer < infoRefreshInterval)
        {
            return;
        }

        refreshTimer = 0f;
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnInventoryChanged()
    {
        gridDirty = true;
        Refresh();
    }

    void Unsubscribe()
    {
        if (currentInventory != null)
        {
            currentInventory.OnChanged -= OnInventoryChanged;
        }
    }

    string BuildCompactInfoText(
        Transform npc,
        ItemInventory inventory)
    {
        int inventoryValue =
            GetInventoryValue(inventory);
        int totalAssets =
            inventoryValue;

        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            totalAssets +=
                Mathf.Max(0, villager.money) +
                Mathf.Max(0, villager.spiritStone);
        }
        else
        {
            SmartNpcAI smartNpc =
                npc.GetComponent<SmartNpcAI>();

            if (smartNpc != null)
            {
                totalAssets +=
                    Mathf.Max(0, smartNpc.money) +
                    Mathf.Max(0, smartNpc.spiritStone);
            }
        }

        return "Balo: " +
            NpcEconomy.FormatCurrency(inventoryValue) +
            " | T\u1ed5ng: " +
            NpcEconomy.FormatCurrency(totalAssets);
    }

    string BuildInfoText(
        Transform npc,
        ItemInventory inventory)
    {
        StringBuilder builder =
            new StringBuilder();

        int money = 0;
        int spiritStone = 0;
        bool hasWallet = false;

        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            money = villager.money;
            spiritStone = villager.spiritStone;
            hasWallet = true;
        }
        else
        {
            SmartNpcAI smartNpc =
                npc.GetComponent<SmartNpcAI>();

            if (smartNpc != null)
            {
                money = smartNpc.money;
                spiritStone = smartNpc.spiritStone;
                hasWallet = true;
            }
        }

        int itemKindCount =
            GetItemKindCount(inventory);
        int itemTotalCount =
            GetItemTotalCount(inventory);
        int inventoryValue =
            GetInventoryValue(inventory);

        if (hasWallet)
        {
            builder.AppendLine("Ti\u1ec1n: " + money);
            builder.AppendLine("Linh th\u1ea1ch: " + spiritStone + " LT");
        }

        builder.AppendLine(
            "Gi\u00e1 tr\u1ecb balo: " +
            NpcEconomy.FormatCurrency(inventoryValue));

        if (hasWallet)
        {
            builder.AppendLine(
                "T\u1ed5ng t\u00e0i s\u1ea3n: " +
                NpcEconomy.FormatCurrency(
                    Mathf.Max(0, spiritStone) +
                    Mathf.Max(0, money) +
                    inventoryValue));
        }

        builder.Append("S\u1ed1 lo\u1ea1i h\u00e0ng: ");
        builder.Append(itemKindCount);
        builder.Append(" / T\u1ed5ng m\u00f3n: ");
        builder.Append(itemTotalCount);

        return builder.ToString();
    }

    int GetItemTotalCount(ItemInventory inventory)
    {
        if (inventory == null)
        {
            return 0;
        }

        int count = 0;

        foreach (ItemStack stack in inventory.items)
        {
            if (stack != null &&
                stack.item != null &&
                stack.amount > 0)
            {
                count += stack.amount;
            }
        }

        return count;
    }

    int GetInventoryValue(ItemInventory inventory)
    {
        if (inventory == null)
        {
            return 0;
        }

        int total = 0;

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            total +=
                NpcEconomy.GetItemValue(stack.item) *
                stack.amount;
        }

        return Mathf.Max(0, total);
    }

    int GetItemKindCount(ItemInventory inventory)
    {
        if (inventory == null)
        {
            return 0;
        }

        int count = 0;

        foreach (ItemStack stack in inventory.items)
        {
            if (stack != null &&
                stack.item != null &&
                stack.amount > 0)
            {
                count++;
            }
        }

        return count;
    }

    string BuildItemsText(ItemInventory inventory)
    {
        if (inventory == null ||
            inventory.items.Count == 0)
        {
            return emptyText;
        }

        StringBuilder builder =
            new StringBuilder();

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            builder.Append(stack.item.itemName);
            builder.Append(" x");
            builder.Append(stack.amount);

            builder.Append(" - ");
            builder.Append(NpcEconomy.FormatPrice(stack.item));

            builder.AppendLine();
        }

        string result =
            builder.ToString().TrimEnd();

        return string.IsNullOrEmpty(result)
            ? emptyText
            : result;
    }

    string GetNpcName(Transform npc)
    {
        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        NpcData npcData =
            npc.GetComponent<NpcData>();

        if (npcData != null)
        {
            return npcData.npcName;
        }

        return npc.name;
    }

    void ConfigureRaycasts()
    {
        Graphic[] graphics =
            GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
            {
                continue;
            }

            if (graphic.GetComponentInParent<Button>() != null)
            {
                continue;
            }

            graphic.raycastTarget = blockMapDrag;
        }

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.interactable = true;
        }
    }
}





