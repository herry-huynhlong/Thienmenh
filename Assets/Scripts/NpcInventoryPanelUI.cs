using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcInventoryPanelUI : MonoBehaviour
{
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text infoText;

    [Header("NPC Wallet")]
    public TMP_Text npcLinhThachText;
    public bool showNpcLinhThach = true;

    public TMP_Text itemsText;
    public InventoryPanelUI itemGridPanel;
    public bool useItemGrid = true;
    public bool hideItemsTextWhenUsingGrid = true;
    public bool readOnly = true;
    public string emptyText = "";
    public bool blockMapDrag;
    public float infoRefreshInterval = 0.5f;
    public bool autoCreateInfoText = false;
    public float autoInfoHeight = 64f;

    [Header("NPC Shop Inventory")]
    public bool showNearbyShopInventory = false;
    public bool preferActiveCounterBroker = true;
    public float shopInventorySearchRadius = 4f;

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

        emptyText = GetLocalizedInventoryText(
            "emptyText",
            "");

        AutoFindNpcWalletText();
        AutoFindItemGridPanel();
        SanitizeCopiedInventoryGrid();
        NormalizeNpcWalletText();
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

        if (false &&
            IsNpcTarget(npc) &&
            !HasHeavenDaoPower(HeavenDaoPower.ViewBasicNpcInfo))
        {
            HideContentOnly();
            return;
        }

        ItemInventory inventory =
            ResolveInventorySource(npc);

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

        NormalizeNpcWalletText();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);

            panelRoot.SendMessage(
                "SetCurrentNpc",
                npc.gameObject,
                SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            SendMessage(
                "SetCurrentNpc",
                npc.gameObject,
                SendMessageOptions.DontRequireReceiver);
        }

        Refresh();
    }

    ItemInventory ResolveInventorySource(Transform npc)
    {
        if (npc == null)
        {
            return null;
        }

        if (TryResolveLinkedShopInventory(npc, out ItemInventory shopInventory))
        {
            return shopInventory;
        }

        ItemInventory inventory =
            npc.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory = npc.gameObject.AddComponent<ItemInventory>();
        }

        inventory.UsePrivateNpcRuntimeItems(false);
        return inventory;
    }

    bool TryResolveLinkedShopInventory(
        Transform npc,
        out ItemInventory inventory)
    {
        inventory = null;

        if (npc == null ||
            !showNearbyShopInventory)
        {
            return false;
        }

        NpcShopStockRefill refill =
            ResolveShopStockRefill(npc);
        if (refill != null &&
            IsRefillLinkedToNpc(refill, npc))
        {
            refill.EnsureStock();
            inventory = GetRefillInventory(refill);
            if (inventory != null)
            {
                return true;
            }
        }

        SimpleItemShop shop =
            ResolveLinkedShop(npc);
        if (shop != null)
        {
            if (shop.refreshNpcInventoryBeforeOpen)
            {
                shop.RefreshFromSellerInventory();
            }

            inventory =
                shop.sellerInventory != null
                    ? shop.sellerInventory
                    : npc.GetComponent<ItemInventory>();

            if (inventory != null)
            {
                return true;
            }
        }

        return false;
    }

    NpcShopStockRefill ResolveShopStockRefill(Transform npc)
    {
        if (npc == null)
        {
            return null;
        }

        NpcShopStockRefill direct =
            npc.GetComponent<NpcShopStockRefill>();

        if (direct != null)
        {
            return direct;
        }

        if (!showNearbyShopInventory)
        {
            return null;
        }

        if (preferActiveCounterBroker &&
            TryGetBrokerStockRefill(
                npc,
                NpcCounterBroker.Active,
                out NpcShopStockRefill activeRefill))
        {
            return activeRefill;
        }

        NpcShopStockRefill[] refills =
            FindObjectsByType<NpcShopStockRefill>(FindObjectsInactive.Include);

        NpcShopStockRefill best = null;
        float bestDistance = float.MaxValue;
        float maxDistance = Mathf.Max(0.1f, shopInventorySearchRadius);

        foreach (NpcShopStockRefill refill in refills)
        {
            if (refill == null)
            {
                continue;
            }

            float distance = Vector2.Distance(
                npc.position,
                refill.transform.position);

            if (distance > maxDistance || distance >= bestDistance)
            {
                continue;
            }

            best = refill;
            bestDistance = distance;
        }

        return best;
    }

    SimpleItemShop ResolveLinkedShop(Transform npc)
    {
        if (npc == null)
        {
            return null;
        }

        SimpleItemShop directShop =
            npc.GetComponent<SimpleItemShop>();
        if (IsShopLinkedToNpc(directShop, npc))
        {
            return directShop;
        }

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(FindObjectsInactive.Include);

        float bestDistance = float.MaxValue;
        float maxDistance = Mathf.Max(0.1f, shopInventorySearchRadius);
        SimpleItemShop best = null;

        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (!IsShopLinkedToNpc(candidate, npc))
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    npc.position,
                    candidate.transform.position);

            if (distance > maxDistance ||
                distance >= bestDistance)
            {
                continue;
            }

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    bool IsRefillLinkedToNpc(
        NpcShopStockRefill refill,
        Transform npc)
    {
        if (refill == null ||
            npc == null)
        {
            return false;
        }

        if (refill.transform == npc)
        {
            return true;
        }

        if (refill.sellerInventory != null &&
            refill.sellerInventory.transform == npc)
        {
            return true;
        }

        return IsShopLinkedToNpc(refill.npcShop, npc);
    }

    bool IsShopLinkedToNpc(
        SimpleItemShop shop,
        Transform npc)
    {
        if (shop == null ||
            npc == null)
        {
            return false;
        }

        if (shop.transform == npc)
        {
            return true;
        }

        if (shop.sellerObject == npc.gameObject)
        {
            return true;
        }

        return shop.sellerInventory != null &&
            shop.sellerInventory.transform == npc;
    }

    ItemInventory GetRefillInventory(NpcShopStockRefill refill)
    {
        if (refill == null)
        {
            return null;
        }

        if (refill.sellerInventory != null)
        {
            return refill.sellerInventory;
        }

        return refill.npcShop != null
            ? refill.npcShop.sellerInventory
            : null;
    }

    bool TryGetBrokerStockRefill(
        Transform npc,
        NpcCounterBroker broker,
        out NpcShopStockRefill refill)
    {
        refill = null;

        if (npc == null || broker == null)
        {
            return false;
        }

        refill = broker.GetComponent<NpcShopStockRefill>();
        if (refill == null)
        {
            return false;
        }

        float maxDistance = Mathf.Max(
            shopInventorySearchRadius,
            broker.CustomerServiceRadius + 0.5f);

        return Vector2.Distance(
            npc.position,
            broker.transform.position) <= maxDistance;
    }
    public void Refresh()
    {
        if (currentNpc == null ||
            currentInventory == null)
        {
            return;
        }

        NormalizeNpcWalletText();

        if (titleText != null)
        {
            titleText.text =
                UiText.Format(
                    "npcInventory",
                    "titleFormat",
                    GetNpcName(currentNpc));
        }

        EnsureInfoText();

        if (infoText != null)
        {
            infoText.text = createdInfoText
                ? BuildCompactInfoText(currentNpc, currentInventory)
                : BuildInfoText(currentNpc, currentInventory);

            infoText.gameObject.SetActive(true);
        }

        UpdateWalletText();

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

        ApplyLocalizedTexts();
    }

    public void Hide()
    {
        Unsubscribe();
        currentNpc = null;
        currentInventory = null;

        if (itemGridPanel != null)
        {
            itemGridPanel.currencyOwnerOverride = null;
        }

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

    public void SetContentVisible(bool visible)
    {
        SetTextVisible(titleText, visible);
        SetTextVisible(infoText, visible);
        SetTextVisible(npcLinhThachText, visible && showNpcLinhThach);

        if (visible)
        {
            UpdateWalletText();
        }

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

                itemGridPanel.Refresh(true);
                gridDirty = false;

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

    void UpdateWalletText()
    {
        if (!showNpcLinhThach ||
            npcLinhThachText == null ||
            currentNpc == null)
        {
            return;
        }

        NormalizeNpcWalletText();

        npcLinhThachText.text =
            NpcEconomy.FormatCurrency(
                NpcEconomy.GetNpcLinhThach(currentNpc.gameObject));

        npcLinhThachText.gameObject.SetActive(true);

        if (itemGridPanel != null &&
            itemGridPanel.footerLinhThachText != null)
        {
            itemGridPanel.footerLinhThachText.text =
                NpcEconomy.FormatCompactAmount(
                    NpcEconomy.GetNpcLinhThach(
                        currentNpc.gameObject));
        }
    }

    void EnsureInfoText()
    {
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

        PlayerWalletTextUI[] walletTexts =
            gridRoot.GetComponentsInChildren<PlayerWalletTextUI>(true);

        foreach (PlayerWalletTextUI walletText in walletTexts)
        {
            if (walletText != null)
            {
                walletText.enabled = false;
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

    void NormalizeNpcWalletText()
    {
        AutoFindNpcWalletText();

        if (npcLinhThachText == null)
        {
            return;
        }

        PlayerWalletTextUI conflictingWalletText =
            npcLinhThachText.GetComponent<PlayerWalletTextUI>();
        if (conflictingWalletText != null &&
            conflictingWalletText.enabled)
        {
            conflictingWalletText.enabled = false;
        }

        if (itemGridPanel != null &&
            itemGridPanel.footerLinhThachText != null)
        {
            PlayerWalletTextUI footerWalletText =
                itemGridPanel.footerLinhThachText.GetComponent<PlayerWalletTextUI>();
            if (footerWalletText != null &&
                footerWalletText.enabled)
            {
                footerWalletText.enabled = false;
            }
        }
    }

    void AutoFindNpcWalletText()
    {
        if (npcLinhThachText != null)
        {
            return;
        }

        Transform searchRoot = panelRoot != null
            ? panelRoot.transform
            : transform;

        npcLinhThachText =
            FindTextByName(searchRoot, "NpcLinhThachText");
    }

    void AutoFindItemGridPanel()
    {
        if (IsValidItemGridPanel(itemGridPanel))
        {
            DisableDuplicateGridControllers();
            return;
        }

        itemGridPanel =
            FindBestItemGridPanel();

        DisableDuplicateGridControllers();
    }

    InventoryPanelUI FindBestItemGridPanel()
    {
        InventoryPanelUI bestPanel = null;
        int bestScore = int.MinValue;

        if (panelRoot != null)
        {
            InventoryPanelUI[] candidates =
                panelRoot.GetComponentsInChildren<InventoryPanelUI>(true);

            foreach (InventoryPanelUI candidate in candidates)
            {
                int score =
                    ScoreItemGridPanelCandidate(candidate);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPanel = candidate;
                }
            }
        }

        if (bestPanel != null)
        {
            return bestPanel;
        }

        InventoryPanelUI[] fallbackCandidates =
            GetComponentsInChildren<InventoryPanelUI>(true);

        foreach (InventoryPanelUI candidate in fallbackCandidates)
        {
            int score =
                ScoreItemGridPanelCandidate(candidate);

            if (score > bestScore)
            {
                bestScore = score;
                bestPanel = candidate;
            }
        }

        return bestPanel;
    }

    int ScoreItemGridPanelCandidate(InventoryPanelUI candidate)
    {
        if (!IsValidItemGridPanel(candidate))
        {
            return int.MinValue;
        }

        int score = 0;

        if (candidate.panelRoot == candidate.gameObject)
        {
            score += 100;
        }

        if (candidate.gameObject != panelRoot)
        {
            score += 30;
        }

        if (candidate.inventory != null)
        {
            score += 20;
        }

        if (!candidate.closeWhenClickOutside)
        {
            score += 10;
        }

        if (candidate.transform.IsChildOf(transform))
        {
            score += 5;
        }

        return score;
    }

    bool IsValidItemGridPanel(InventoryPanelUI candidate)
    {
        return candidate != null &&
            candidate.itemGridParent != null &&
            candidate.itemButtonPrefab != null;
    }

    void DisableDuplicateGridControllers()
    {
        if (!IsValidItemGridPanel(itemGridPanel) ||
            panelRoot == null)
        {
            return;
        }

        InventoryPanelUI[] candidates =
            panelRoot.GetComponentsInChildren<InventoryPanelUI>(true);

        foreach (InventoryPanelUI candidate in candidates)
        {
            if (candidate == null ||
                candidate == itemGridPanel)
            {
                continue;
            }

            bool sameGridParent =
                candidate.itemGridParent ==
                itemGridPanel.itemGridParent;
            bool samePanelRoot =
                candidate.panelRoot ==
                itemGridPanel.panelRoot;
            bool sameDetailPanel =
                candidate.detailPanel ==
                itemGridPanel.detailPanel;

            if (!sameGridParent &&
                !samePanelRoot &&
                !sameDetailPanel)
            {
                continue;
            }

            candidate.enabled = false;
        }
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

        itemGridPanel.currencyOwnerOverride =
            currentNpc != null
                ? currentNpc.gameObject
                : null;
        itemGridPanel.SetInventory(currentInventory, false);
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

    Transform FindChildByName(
        Transform parent,
        string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == childName)
            {
                return child;
            }

            Transform nested =
                FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
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

        TMP_Text text =
            child.GetComponent<TMP_Text>();
        return text != null
            ? text
            : child.GetComponentInChildren<TMP_Text>(true);
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
            inventoryValue +
            NpcEconomy.GetNpcLinhThach(npc.gameObject);

        return UiText.Format(
            "npcInventory",
            "compactInfoFormat",
            NpcEconomy.FormatCurrency(inventoryValue),
            NpcEconomy.FormatCurrency(totalAssets));
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
            builder.AppendLine(
                UiText.Format(
                    "npcInventory",
                    "walletLine",
                    NpcEconomy.FormatCurrency(Mathf.Max(0, money))));

            builder.AppendLine(
                UiText.Format(
                    "npcInventory",
                    "spiritStoneLine",
                    Mathf.Max(0, spiritStone)));
        }

        builder.AppendLine(
            UiText.Format(
                "npcInventory",
                "inventoryValueLine",
                NpcEconomy.FormatCurrency(inventoryValue)));

        builder.AppendLine(
            UiText.Format(
                "npcInventory",
                "totalAssetsLine",
                NpcEconomy.FormatCurrency(
                    inventoryValue +
                    NpcEconomy.GetNpcLinhThach(npc.gameObject))));

        builder.Append(
            UiText.Format(
                "npcInventory",
                "itemCountsFormat",
                itemKindCount,
                itemTotalCount));

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

            builder.Append(ItemText.Name(stack.item));
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

    void ApplyLocalizedTexts()
    {
        if (currentNpc == null ||
            currentInventory == null)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.text = FormatLocalizedInventoryText(
                "titleFormat",
                "{0} - Kho do",
                GetNpcName(currentNpc));
        }

        if (infoText != null &&
            infoText.gameObject.activeSelf)
        {
            infoText.text = createdInfoText
                ? BuildLocalizedCompactInfoText(currentNpc, currentInventory)
                : BuildLocalizedInfoText(currentNpc, currentInventory);
        }

        if (itemsText != null &&
            itemsText.gameObject.activeSelf)
        {
            itemsText.text = BuildLocalizedItemsText(currentInventory);
        }
    }

    string BuildLocalizedCompactInfoText(
        Transform npc,
        ItemInventory inventory)
    {
        int inventoryValue =
            GetInventoryValue(inventory);
        int totalAssets =
            inventoryValue +
            NpcEconomy.GetNpcLinhThach(npc.gameObject);

        return FormatLocalizedInventoryText(
            "compactInfoFormat",
            "Balo: {0} | Tong: {1}",
            NpcEconomy.FormatCurrency(inventoryValue),
            NpcEconomy.FormatCurrency(totalAssets));
    }

    string BuildLocalizedInfoText(
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
            builder.AppendLine(FormatLocalizedInventoryText(
                "walletLine",
                "Linh thach: {0}",
                NpcEconomy.FormatCurrency(Mathf.Max(0, money))));

            builder.AppendLine(FormatLocalizedInventoryText(
                "spiritStoneLine",
                "Linh thach tu luyen: {0}",
                Mathf.Max(0, spiritStone)));
        }

        builder.AppendLine(FormatLocalizedInventoryText(
            "inventoryValueLine",
            "Gia tri balo: {0}",
            NpcEconomy.FormatCurrency(inventoryValue)));

        builder.AppendLine(FormatLocalizedInventoryText(
            "totalAssetsLine",
            "Tong tai san: {0}",
            NpcEconomy.FormatCurrency(
                inventoryValue +
                NpcEconomy.GetNpcLinhThach(npc.gameObject))));

        builder.Append(FormatLocalizedInventoryText(
            "itemCountsFormat",
            "So loai hang: {0} / Tong mon: {1}",
            itemKindCount,
            itemTotalCount));

        return builder.ToString();
    }

    string BuildLocalizedItemsText(ItemInventory inventory)
    {
        if (inventory == null ||
            inventory.items.Count == 0)
        {
            return GetLocalizedInventoryText(
                "emptyText",
                emptyText);
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

            builder.AppendLine(FormatLocalizedInventoryText(
                "itemLineFormat",
                "{0} x{1} - {2}",
                ItemText.Name(stack.item),
                stack.amount,
                NpcEconomy.FormatPrice(stack.item)));
        }

        string result =
            builder.ToString().TrimEnd();

        return string.IsNullOrEmpty(result)
            ? GetLocalizedInventoryText(
                "emptyText",
                emptyText)
            : result;
    }

    string GetLocalizedInventoryText(
        string key,
        string fallback)
    {
        return UiText.Get("npcInventory", key, fallback);
    }

    string FormatLocalizedInventoryText(
        string key,
        string fallback,
        params object[] args)
    {
        return NpcText.Format(
            GetLocalizedInventoryText(key, fallback),
            args);
    }

    string GetNpcName(Transform npc)
    {
        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return NpcGeneratedIdentityProfiles.ToDisplayName(
                villager.villagerName);
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return NpcGeneratedIdentityProfiles.ToDisplayName(
                smartNpc.npcName);
        }

        NpcData npcData =
            npc.GetComponent<NpcData>();

        if (npcData != null)
        {
            return NpcGeneratedIdentityProfiles.ToDisplayName(
                npcData.npcName);
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

        CanvasGroup group =
            GetComponent<CanvasGroup>();

        if (group != null)
        {
            group.interactable = true;
        }
    }
}
