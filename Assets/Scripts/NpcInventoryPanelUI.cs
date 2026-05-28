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
    public string emptyText = "Khong co vat pham";
    public bool blockMapDrag;

    Transform currentNpc;
    ItemInventory currentInventory;
    bool gridDirty = true;

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
                GetNpcName(currentNpc) + " - Kho do";
        }

        if (infoText != null)
        {
            infoText.text =
                BuildInfoText(currentNpc, currentInventory);
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
                itemGridPanel.Refresh();
                gridDirty = false;
            }
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
                    itemGridPanel.Refresh();
                    gridDirty = false;
                }
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

    string BuildInfoText(
        Transform npc,
        ItemInventory inventory)
    {
        StringBuilder builder =
            new StringBuilder();

        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            builder.AppendLine("Tien: " + villager.money + " LT");
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            builder.AppendLine("Tien: " + smartNpc.money + " LT");
        }

        builder.Append("So loai hang: ");
        builder.Append(GetItemKindCount(inventory));

        return builder.ToString();
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
