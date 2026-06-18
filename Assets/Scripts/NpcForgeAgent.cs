using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ForgeIngredient
{
    public StatItemData item;
    public MaterialKind materialKind = MaterialKind.None;
    public bool allowAnyMaterialFallback = true;
    [Min(1)] public int amount = 1;
}

[System.Serializable]
public class ForgeRecipe
{
    public string recipeName = "Forge Recipe";
    public List<ForgeIngredient> ingredients = new List<ForgeIngredient>();
    [Min(1)] public int genericMaterialUnits = 3;
    public EquipmentSlot outputSlot = EquipmentSlot.Weapon;
    public StatItemData preferredResult;
    [Min(1)] public int resultAmount = 1;
    public bool allowCustomOrders = true;
    [Min(0f)] public float defaultLeadTimeGameDays = 3f;
    [Range(0f, 1f)] public float depositFraction = 0.3f;
}

[System.Serializable]
public class ForgeCustomerOrder
{
    public GameObject customer;
    public StatItemData requestedItem;
    [Min(1)] public int amount = 1;
    [Min(0)] public int depositPaid;
    [Min(0)] public int totalPrice;
    [Min(0f)] public float readyAtWorldHour;
    public bool notifiedReady;
    public bool fulfilled;
}

public class NpcForgeAgent : MonoBehaviour
{
    [Header("Inventory")]
    public ItemInventory inventory;
    public NpcTradeAgent tradeAgent;

    [Header("Forge")]
    public bool autoForge = true;
    public bool preferCultivateWhenIdle = true;
    public float checkInterval = 1f;
    [Min(0f)] public float forgeDurationMinGameHours = 2f;
    [Min(0f)] public float forgeDurationMaxGameHours = 3f;
    [Min(1)] public int genericMaterialUnits = 3;
    public EquipmentSlot genericOutputSlot = EquipmentSlot.Weapon;
    public bool allowGenericArmorFallback = true;

    [Header("Work Spot")]
    public Transform forgeStandPoint;
    public Transform forgeFacingPoint;
    public Vector2 defaultFacingDirection = Vector2.down;
    public bool snapToForgeStandPoint = true;
    public bool keepAtForgeStandPoint = true;
    public bool lockFacingToForgePoint = true;

    [Header("Materials")]
    public bool autoBuyMaterialsFromMarketTraders = true;
    public bool acceptAllForgeMaterials = true;
    public bool acceptDanDuocAsForgeMaterial = true;
    public bool acceptVatLieuAsForgeMaterial = true;
    public bool acceptPhapBaoAsForgeMaterial = false;
    public bool acceptThucPhamAsForgeMaterial = false;
    public bool excludeImmortalGrade = true;

    [Header("Selling")]
    public bool autoSellFinishedGoods = true;
    public bool sellToNearbyNpcBuyers = true;
    public bool sellToVanBaoLau = true;
    public bool preferDirectSaleWhenPriceHigher = true;
    public float sellSearchRadius = 2.25f;
    public LayerMask npcLayers = ~0;

    [Header("Catalog Fallback")]
    public bool autoLoadForgeCatalogFromAssets = true;
    public string[] itemFolders =
    {
        "Assets/Item/PhapBao"
    };
    public List<StatItemData> forgeCatalogItems = new List<StatItemData>();

    [Header("Recipes")]
    public List<ForgeRecipe> recipes = new List<ForgeRecipe>();

    [Header("Custom Orders")]
    public bool autoAcceptCustomOrders = false;
    [Range(0f, 1f)] public float customOrderDepositFraction = 0.3f;
    [Min(1f)] public float customOrderDefaultLeadTimeGameDays = 3f;
    public bool autoBuildDefaultRecipesFromCatalog = true;
    public List<ForgeCustomerOrder> pendingOrders = new List<ForgeCustomerOrder>();

    float checkTimer;
    bool forging;
    float forgeTimer;
    StatItemData pendingResult;
    int pendingResultAmount;
    bool privateInventoryInitialized;
    string currentAction = "";
    NPCVisualAnimation visualAnimation;
    bool workSpotInitialized;

    void Awake()
    {
        EnsureReferences();
        EnsureScheduleController();
        EnsureVisualAnimation();
        SnapToWorkSpotIfNeeded();
        ApplyForgeFacing();
    }

    void EnsureScheduleController()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();

        if (schedule == null)
        {
            schedule = gameObject.AddComponent<NpcScheduleController>();
        }

        schedule.lifePath = NpcLifePath.Cultivator;
        schedule.canCultivate = true;

        if (schedule.autoBuildDefaultSchedule)
        {
            schedule.RebuildDefaultSchedule();
        }
    }

    void Start()
    {
        EnsureReferences();
        EnsureVisualAnimation();
        EnsureCatalog();
        EnsureDefaultRecipes();
        if (!forging)
        {
            SetIdleAction();
        }
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (autoLoadForgeCatalogFromAssets)
        {
            RefreshCatalogFromAssets();
        }
#endif
    }

    void Update()
    {
        EnsureReferences();

        if (NpcRoleUtility.IsDead(gameObject))
        {
            return;
        }

        SnapToWorkSpotIfNeeded();

        if (UpdateForging())
        {
            UpdateVisualAnimation();
            return;
        }

        checkTimer += Time.deltaTime;
        if (checkTimer < Mathf.Max(0.1f, checkInterval))
        {
            return;
        }

        checkTimer = 0f;

        bool canTradeNow =
            NpcScheduleController.AllowsTrade(gameObject);
        bool canForgeNow =
            NpcScheduleController.AllowsForge(gameObject);

        if (autoSellFinishedGoods &&
            canTradeNow &&
            TrySellFinishedGoods())
        {
            return;
        }

        if (autoForge &&
            canForgeNow &&
            TryStartAnyForge())
        {
            return;
        }

        if (autoBuyMaterialsFromMarketTraders &&
            canTradeNow &&
            NeedsMoreMaterials())
        {
            currentAction = NpcText.Action("goMarketTrade");
            UpdateVisualAnimation();
            NpcRoleUtility.SetAction(
                gameObject,
                currentAction);
            return;
        }

        if (!canTradeNow && !canForgeNow)
        {
            return;
        }

        SetIdleAction();
        UpdateVisualAnimation();
    }

    public bool TryStartAnyForge()
    {
        EnsureReferences();
        EnsureCatalog();
        EnsureDefaultRecipes();

        if (forging || inventory == null)
        {
            return false;
        }

        if (recipes != null)
        {
            foreach (ForgeRecipe recipe in recipes)
            {
                if (TryStartForge(recipe))
                {
                    return true;
                }
            }
        }

        return TryStartGenericForge();
    }

    public bool TryStartForge(ForgeRecipe recipe)
    {
        EnsureReferences();
        EnsureCatalog();

        if (forging ||
            recipe == null ||
            inventory == null)
        {
            return false;
        }

        List<ForgeCost> costs = new List<ForgeCost>();
        ItemGrade highestGrade = ItemGrade.Ha;
        StatItemData resultItem = null;

        if (recipe.ingredients == null ||
            recipe.ingredients.Count == 0)
        {
            costs =
                BuildGenericCosts(
                    Mathf.Max(1, recipe.genericMaterialUnits),
                    out highestGrade);

            if (costs.Count == 0)
            {
                return false;
            }
        }
        else if (!TryBuildRecipeCosts(recipe, costs, ref highestGrade))
        {
            return false;
        }

        ItemGrade outputGrade =
            recipe.preferredResult != null
                ? recipe.preferredResult.grade
                : ResolveForgeGrade(highestGrade);

        resultItem =
            ResolveForgeResult(
                recipe.preferredResult,
                recipe.outputSlot,
                outputGrade);

        if (resultItem == null)
        {
            return false;
        }

        if (!ConsumeCosts(costs))
        {
            return false;
        }

        StartForging(resultItem, Mathf.Max(1, recipe.resultAmount));
        return true;
    }

    public bool TryStartGenericForge()
    {
        EnsureReferences();
        EnsureCatalog();
        EnsureDefaultRecipes();

        if (forging || inventory == null)
        {
            return false;
        }

        List<ForgeCost> costs =
            BuildGenericCosts(
                Mathf.Max(1, genericMaterialUnits),
                out ItemGrade highestGrade);

        if (costs.Count == 0)
        {
            return false;
        }

        StatItemData resultItem =
            ResolveForgeResult(
                null,
                genericOutputSlot,
                ResolveForgeGrade(highestGrade));

        if (resultItem == null &&
            allowGenericArmorFallback &&
            genericOutputSlot != EquipmentSlot.Armor)
        {
            resultItem =
                ResolveForgeResult(
                    null,
                    EquipmentSlot.Armor,
                    ResolveForgeGrade(highestGrade));
        }

        if (resultItem == null)
        {
            return false;
        }

        if (!ConsumeCosts(costs))
        {
            return false;
        }

        StartForging(resultItem, 1);
        return true;
    }

    ForgeRecipe FindRecipeForItem(StatItemData item)
    {
        if (item == null)
        {
            return null;
        }

        if (recipes != null)
        {
            foreach (ForgeRecipe recipe in recipes)
            {
                if (recipe != null &&
                    recipe.preferredResult == item)
                {
                    return recipe;
                }
            }
        }

        return null;
    }

    bool UpdateForging()
    {
        if (!forging)
        {
            return false;
        }

        forgeTimer -= Time.deltaTime;
        SetForgingAction();

        if (forgeTimer > 0f)
        {
            return true;
        }

        FinishForging();
        return true;
    }

    void StartForging(StatItemData resultItem, int amount)
    {
        forging = true;
        forgeTimer = GameHoursToSeconds(
            Random.Range(
                Mathf.Min(forgeDurationMinGameHours, forgeDurationMaxGameHours),
                Mathf.Max(forgeDurationMinGameHours, forgeDurationMaxGameHours)));

        pendingResult = resultItem;
        pendingResultAmount = Mathf.Max(1, amount);
        SetForgingAction();
        UpdateVisualAnimation();
    }

    void FinishForging()
    {
        forging = false;
        forgeTimer = 0f;

        if (inventory != null && pendingResult != null)
        {
            inventory.AddItem(pendingResult, pendingResultAmount);
            ItemLifecycleSystem.Notify(
                ItemLifecycleEventType.Forged,
                pendingResult,
                gameObject);
        }

        pendingResult = null;
        pendingResultAmount = 0;
        SetIdleAction();
        UpdateVisualAnimation();
    }

    public bool TrySellFinishedGoods()
    {
        EnsureReferences();

        if (inventory == null || tradeAgent == null)
        {
            return false;
        }

        if (!HasSellableFinishedGoods())
        {
            return false;
        }

        if (sellToNearbyNpcBuyers &&
            TrySellToNearbyBuyer())
        {
            return true;
        }

        if (sellToVanBaoLau &&
            TrySellToBroker())
        {
            return true;
        }

        return false;
    }

    bool HasSellableFinishedGoods()
    {
        if (inventory == null)
        {
            return false;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (CanSellFinishedItem(stack))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryRequestCustomOrder(
        GameObject customer,
        StatItemData requestedItem,
        int amount,
        out string customerLine,
        out string smithLine)
    {
        EnsureReferences();
        EnsureCatalog();

        customerLine = "";
        smithLine = "";

        if (!autoAcceptCustomOrders)
        {
            customerLine =
                NpcText.Dialogue(
                    "forgeOrderDisabledBuyer",
                    "Tam thoi khong nhan don");
            smithLine =
                NpcText.Dialogue(
                    "forgeOrderDisabledSmith",
                    "Ta chi nhan rèn thong thuong.");
            ShowForgeDialogue(customer, customerLine, smithLine);
            return false;
        }

        if (!autoAcceptCustomOrders ||
            customer == null ||
            requestedItem == null ||
            amount <= 0)
        {
            customerLine =
                NpcText.Dialogue(
                    "forgeOrderRejectInvalidBuyer",
                    "Don hang khong hop le");
            smithLine =
                NpcText.Dialogue(
                    "forgeOrderRejectInvalidSmith",
                    "Khong nhan don nay.");
            ShowForgeDialogue(customer, customerLine, smithLine);
            return false;
        }

        if (requestedItem.itemType != ItemType.PhapBao ||
            (excludeImmortalGrade && requestedItem.grade == ItemGrade.Tien))
        {
            customerLine =
                NpcText.Dialogue(
                    "forgeOrderRejectTypeBuyer",
                    "Chi nhan don do phap bao");
            smithLine =
                NpcText.Dialogue(
                    "forgeOrderRejectTypeSmith",
                    "Chi nhan do phap bao.");
            ShowForgeDialogue(customer, customerLine, smithLine);
            return false;
        }

        if (HasPendingCustomOrder(customer, requestedItem))
        {
            customerLine =
                NpcText.Dialogue(
                    "forgeOrderDuplicateBuyer",
                    "Da co don nay roi");
            smithLine =
                NpcText.Dialogue(
                    "forgeOrderDuplicateSmith",
                    "Don nay dang lam.");
            ShowForgeDialogue(customer, customerLine, smithLine);
            currentAction = NpcText.Action("cultivate");
            UpdateVisualAnimation();
            return true;
        }

        StatItemData craftable = ResolveForgeResult(
            requestedItem,
            requestedItem.GetResolvedEquipmentSlot(),
            requestedItem.grade);

        if (craftable == null ||
            craftable.itemType != ItemType.PhapBao)
        {
            customerLine =
                NpcText.Dialogue(
                    "forgeOrderRejectRecipeBuyer",
                    "Chua co cong thuc phu hop");
            smithLine =
                NpcText.Dialogue(
                    "forgeOrderRejectRecipeSmith",
                    "Mon nay chua lam duoc.");
            ShowForgeDialogue(customer, customerLine, smithLine);
            return false;
        }

        int totalPrice =
            Mathf.Max(
                1,
                NpcEconomy.GetNpcBuyPrice(
                    requestedItem,
                    customer,
                    NpcTradeContext.NpcToNpc)) *
            amount;
        int deposit =
            Mathf.Max(
                1,
                Mathf.RoundToInt(totalPrice * Mathf.Clamp01(customOrderDepositFraction)));

        if (NpcEconomy.GetNpcMoney(customer) < deposit)
        {
            customerLine =
                NpcText.Dialogue(
                    "forgeOrderRejectDepositBuyer",
                    "Khong du tien coc");
            smithLine =
                NpcText.Dialogue(
                    "forgeOrderRejectDepositSmith",
                    "Khong du tien coc.");
            ShowForgeDialogue(customer, customerLine, smithLine);
            return false;
        }

        NpcEconomy.AddNpcMoney(customer, -deposit);
        tradeAgent.AddMoney(deposit);

        ForgeCustomerOrder order = new ForgeCustomerOrder
        {
            customer = customer,
            requestedItem = requestedItem,
            amount = amount,
            depositPaid = deposit,
            totalPrice = totalPrice,
            readyAtWorldHour = GetCurrentWorldHour() +
                Mathf.Max(1f, customOrderDefaultLeadTimeGameDays) * 24f,
            notifiedReady = false,
            fulfilled = false
        };

        pendingOrders.Add(order);
        customerLine =
            NpcText.DialogueFormat(
                "forgeOrderAcceptBuyer",
                ItemText.Name(requestedItem),
                NpcEconomy.FormatCurrency(deposit));
        smithLine =
            NpcText.DialogueFormat(
                "forgeOrderAcceptSmith",
                Mathf.RoundToInt(
                    Mathf.Max(1f, customOrderDefaultLeadTimeGameDays)));
        ShowForgeDialogue(customer, customerLine, smithLine);
        currentAction = NpcText.Action("cultivate");
        UpdateVisualAnimation();
        return true;
    }

    bool HasPendingCustomOrder(
        GameObject customer,
        StatItemData requestedItem)
    {
        if (customer == null ||
            requestedItem == null)
        {
            return false;
        }

        NpcForgeAgent[] agents =
            FindObjectsByType<NpcForgeAgent>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (NpcForgeAgent agent in agents)
        {
            if (agent == null ||
                agent.pendingOrders == null ||
                agent.pendingOrders.Count == 0)
            {
                continue;
            }

            foreach (ForgeCustomerOrder order in agent.pendingOrders)
            {
                if (order == null ||
                    order.fulfilled ||
                    order.customer != customer ||
                    order.requestedItem != requestedItem)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    public bool CanAcceptDesiredForgeOrder(StatItemData desiredItem)
    {
        if (!autoAcceptCustomOrders ||
            desiredItem == null ||
            desiredItem.itemType != ItemType.PhapBao ||
            (excludeImmortalGrade && desiredItem.grade == ItemGrade.Tien))
        {
            return false;
        }

        EnsureDefaultRecipes();
        return FindRecipeForItem(desiredItem) != null;
    }

    bool ShowForgeDialogue(
        GameObject customer,
        string customerLine,
        string smithLine)
    {
        bool showed = false;

        if (customer != null)
        {
            NpcOverheadDialogueUI customerOverhead =
                customer.GetComponent<NpcOverheadDialogueUI>();

            if (customerOverhead == null)
            {
                customerOverhead =
                    customer.AddComponent<NpcOverheadDialogueUI>();
            }

            customerOverhead.ShowLine(customerLine, 3f, 2);
            showed = true;
        }

        NpcOverheadDialogueUI smithOverhead =
            GetComponent<NpcOverheadDialogueUI>();

        if (smithOverhead == null)
        {
            smithOverhead = gameObject.AddComponent<NpcOverheadDialogueUI>();
        }

        smithOverhead.ShowLine(smithLine, 3f, 2);
        return showed;
    }

    public StatItemData SuggestCustomOrderItemForBuyer(GameObject buyer)
    {
        EnsureCatalog();

        if (buyer == null ||
            forgeCatalogItems == null ||
            forgeCatalogItems.Count == 0)
        {
            return null;
        }

        if (HasAnyPendingCustomOrder(buyer))
        {
            return null;
        }

        ItemInventory buyerInventory = buyer.GetComponent<ItemInventory>();
        EquipmentSlot preferredSlot =
            DeterminePreferredOrderSlot(buyer, buyerInventory);

        if (preferredSlot == EquipmentSlot.None)
        {
            return null;
        }

        ItemGrade preferredGrade =
            DeterminePreferredOrderGrade(buyer);

        StatItemData candidate =
            PickAffordableCatalogItem(
                buyer,
                buyerInventory,
                preferredSlot,
                preferredGrade);

        if (candidate != null)
        {
            return candidate;
        }

        EquipmentSlot alternateSlot;
        EquipmentSlot tertiarySlot;

        switch (preferredSlot)
        {
            case EquipmentSlot.Weapon:
                alternateSlot = EquipmentSlot.Armor;
                tertiarySlot = EquipmentSlot.Accessory;
                break;
            case EquipmentSlot.Armor:
                alternateSlot = EquipmentSlot.Weapon;
                tertiarySlot = EquipmentSlot.Accessory;
                break;
            case EquipmentSlot.Accessory:
                alternateSlot = EquipmentSlot.Armor;
                tertiarySlot = EquipmentSlot.Weapon;
                break;
            default:
                alternateSlot = EquipmentSlot.Weapon;
                tertiarySlot = EquipmentSlot.Armor;
                break;
        }

        candidate =
            PickAffordableCatalogItem(
                buyer,
                buyerInventory,
                alternateSlot,
                preferredGrade);

        if (candidate != null)
        {
            return candidate;
        }

        candidate =
            PickAffordableCatalogItem(
                buyer,
                buyerInventory,
                tertiarySlot,
                preferredGrade);

        return candidate;
    }

    public static bool HasAnyPendingCustomOrder(GameObject customer)
    {
        if (customer == null)
        {
            return false;
        }

        NpcForgeAgent[] agents =
            FindObjectsByType<NpcForgeAgent>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (NpcForgeAgent agent in agents)
        {
            if (agent == null ||
                agent.pendingOrders == null ||
                agent.pendingOrders.Count == 0)
            {
                continue;
            }

            foreach (ForgeCustomerOrder order in agent.pendingOrders)
            {
                if (order == null ||
                    order.fulfilled ||
                    order.customer != customer)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    bool IsCombatBuyer(GameObject buyer)
    {
        if (buyer == null)
        {
            return false;
        }

        SmartNpcAI smartNpc = buyer.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.canFight;
        }

        VillagerAI villager = buyer.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.job == VillagerJob.Guard ||
                villager.job == VillagerJob.Hunter;
        }

        return false;
    }

    public static NpcForgeAgent FindBestForgeForBuyer(
        GameObject buyer,
        out StatItemData desiredItem)
    {
        desiredItem = null;
        if (buyer == null)
        {
            return null;
        }

        NpcForgeAgent[] agents =
            FindObjectsByType<NpcForgeAgent>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        NpcForgeAgent best = null;
        float bestDistance = float.MaxValue;

        foreach (NpcForgeAgent agent in agents)
        {
            if (agent == null ||
                NpcRoleUtility.IsDead(agent.gameObject))
            {
                continue;
            }

            StatItemData candidate =
                agent.SuggestCustomOrderItemForBuyer(buyer);

            if (candidate == null)
            {
                continue;
            }

            Vector3 forgePosition =
                agent.forgeStandPoint != null
                    ? agent.forgeStandPoint.position
                    : agent.transform.position;

            float distance =
                Vector2.Distance(
                    buyer.transform.position,
                    forgePosition);

            if (best == null || distance < bestDistance)
            {
                best = agent;
                bestDistance = distance;
                desiredItem = candidate;
            }
        }

        return best;
    }

    EquipmentSlot DeterminePreferredOrderSlot(
        GameObject buyer,
        ItemInventory buyerInventory)
    {
        bool combatBuyer = IsCombatBuyer(buyer);

        float weaponScore =
            GetBestOwnedEquipmentScore(
                buyerInventory,
                EquipmentSlot.Weapon);
        float armorScore =
            GetBestOwnedEquipmentScore(
                buyerInventory,
                EquipmentSlot.Armor);
        float accessoryScore =
            GetBestOwnedEquipmentScore(
                buyerInventory,
                EquipmentSlot.Accessory);

        if (combatBuyer)
        {
            weaponScore *= 0.75f;
            armorScore *= 0.95f;
            accessoryScore *= 1.1f;
        }
        else
        {
            weaponScore *= 1.15f;
            armorScore *= 0.8f;
            accessoryScore *= 0.95f;
        }

        EquipmentSlot preferred = EquipmentSlot.Weapon;
        float bestScore = weaponScore;

        if (armorScore < bestScore)
        {
            preferred = EquipmentSlot.Armor;
            bestScore = armorScore;
        }

        if (accessoryScore < bestScore)
        {
            preferred = EquipmentSlot.Accessory;
        }

        if (!HasAnyForgeableItemForSlot(preferred))
        {
            if (preferred != EquipmentSlot.Weapon &&
                HasAnyForgeableItemForSlot(EquipmentSlot.Weapon))
            {
                return EquipmentSlot.Weapon;
            }

            if (preferred != EquipmentSlot.Armor &&
                HasAnyForgeableItemForSlot(EquipmentSlot.Armor))
            {
                return EquipmentSlot.Armor;
            }

            if (preferred != EquipmentSlot.Accessory &&
                HasAnyForgeableItemForSlot(EquipmentSlot.Accessory))
            {
                return EquipmentSlot.Accessory;
            }
        }

        return preferred;
    }

    ItemGrade DeterminePreferredOrderGrade(GameObject buyer)
    {
        int money = NpcEconomy.GetNpcMoney(buyer);

        if (money < 120)
        {
            return ItemGrade.Ha;
        }

        if (money < 350)
        {
            return ItemGrade.Trung;
        }

        if (money < 900)
        {
            return ItemGrade.Thuong;
        }

        return excludeImmortalGrade
            ? ItemGrade.Thuong
            : ItemGrade.Tien;
    }

    StatItemData PickAffordableCatalogItem(
        GameObject buyer,
        ItemInventory buyerInventory,
        EquipmentSlot desiredSlot,
        ItemGrade preferredGrade)
    {
        if (buyer == null ||
            forgeCatalogItems == null ||
            forgeCatalogItems.Count == 0)
        {
            return null;
        }

        int buyerMoney = NpcEconomy.GetNpcMoney(buyer);
        float currentBestScore =
            GetBestOwnedEquipmentScore(
                buyerInventory,
                desiredSlot);

        StatItemData best = null;
        int bestRank = int.MaxValue;
        float bestScore = float.MinValue;
        int bestPrice = int.MaxValue;

        foreach (StatItemData item in forgeCatalogItems)
        {
            if (item == null ||
                item.itemType != ItemType.PhapBao ||
                (excludeImmortalGrade && item.grade == ItemGrade.Tien) ||
                item.GetResolvedEquipmentSlot() != desiredSlot)
            {
                continue;
            }

            float equipmentScore = item.GetEquipmentUseScore();
            if (equipmentScore <= currentBestScore + 0.01f)
            {
                continue;
            }

            int price =
                NpcEconomy.GetNpcBuyPrice(
                    item,
                    buyer,
                    NpcTradeContext.NpcToNpc);

            int deposit =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        price *
                        Mathf.Clamp01(customOrderDepositFraction)));

            if (buyerMoney < deposit)
            {
                continue;
            }

            int rank =
                GetGradeSelectionRank(item.grade, preferredGrade);

            if (best == null ||
                rank < bestRank ||
                (rank == bestRank &&
                (equipmentScore > bestScore ||
                (Mathf.Approximately(equipmentScore, bestScore) &&
                price < bestPrice))))
            {
                best = item;
                bestRank = rank;
                bestScore = equipmentScore;
                bestPrice = price;
            }
        }

        return best;
    }

    float GetBestOwnedEquipmentScore(
        ItemInventory buyerInventory,
        EquipmentSlot slot)
    {
        if (buyerInventory == null ||
            buyerInventory.items == null)
        {
            return 0f;
        }

        float best = 0f;
        bool found = false;

        foreach (ItemStack stack in buyerInventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.PhapBao ||
                stack.item.GetResolvedEquipmentSlot() != slot)
            {
                continue;
            }

            float score = stack.item.GetEquipmentUseScore();
            if (!found || score > best)
            {
                best = score;
                found = true;
            }
        }

        return found ? best : 0f;
    }

    int GetGradeSelectionRank(
        ItemGrade grade,
        ItemGrade preferredGrade)
    {
        int diff = grade - preferredGrade;
        if (diff == 0)
        {
            return 0;
        }

        if (diff < 0)
        {
            return 1 + Mathf.Abs(diff);
        }

        return 10 + diff;
    }

    bool HasAnyForgeableItemForSlot(EquipmentSlot slot)
    {
        if (forgeCatalogItems == null)
        {
            return false;
        }

        foreach (StatItemData item in forgeCatalogItems)
        {
            if (item == null ||
                item.itemType != ItemType.PhapBao ||
                (excludeImmortalGrade && item.grade == ItemGrade.Tien))
            {
                continue;
            }

            if (item.GetResolvedEquipmentSlot() == slot)
            {
                return true;
            }
        }

        return false;
    }

    public static NpcForgeAgent FindNearestForgeForItem(
        GameObject requester,
        StatItemData desiredItem)
    {
        if (desiredItem == null)
        {
            return null;
        }

        NpcForgeAgent[] agents =
            FindObjectsByType<NpcForgeAgent>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        NpcForgeAgent best = null;
        float bestDistance = float.MaxValue;

        foreach (NpcForgeAgent agent in agents)
        {
            if (agent == null ||
                NpcRoleUtility.IsDead(agent.gameObject) ||
                !agent.CanAcceptDesiredForgeOrder(desiredItem))
            {
                continue;
            }

            Vector3 forgePosition =
                agent.forgeStandPoint != null
                    ? agent.forgeStandPoint.position
                    : agent.transform.position;

            float distance =
                requester != null
                    ? Vector2.Distance(requester.transform.position, forgePosition)
                    : 0f;

            if (best == null || distance < bestDistance)
            {
                best = agent;
                bestDistance = distance;
            }
        }

        return best;
    }

    bool UpdateCustomOrders()
    {
        if (pendingOrders == null || pendingOrders.Count == 0)
        {
            return false;
        }

        bool changed = false;
        float now = GetCurrentWorldHour();

        for (int i = pendingOrders.Count - 1; i >= 0; i--)
        {
            ForgeCustomerOrder order = pendingOrders[i];
            if (order == null ||
                order.requestedItem == null ||
                order.amount <= 0)
            {
                pendingOrders.RemoveAt(i);
                changed = true;
                continue;
            }

            if (order.fulfilled)
            {
                pendingOrders.RemoveAt(i);
                changed = true;
                continue;
            }

            if (order.readyAtWorldHour > now)
            {
                continue;
            }

            if (inventory != null &&
                inventory.GetAmount(order.requestedItem) >= order.amount)
            {
                order.notifiedReady = true;
            }
        }

        return changed;
    }

    bool TryFulfillCustomOrder()
    {
        if (pendingOrders == null ||
            pendingOrders.Count == 0 ||
            inventory == null)
        {
            return false;
        }

        float now = GetCurrentWorldHour();

        for (int i = 0; i < pendingOrders.Count; i++)
        {
            ForgeCustomerOrder order = pendingOrders[i];
            if (order == null ||
                order.fulfilled ||
                order.requestedItem == null ||
                order.amount <= 0)
            {
                continue;
            }

            if (order.readyAtWorldHour > now)
            {
                continue;
            }

            if (inventory.GetAmount(order.requestedItem) < order.amount)
            {
                continue;
            }

            if (order.customer == null)
            {
                continue;
            }

            int remainingPrice =
                Mathf.Max(0, order.totalPrice - order.depositPaid);

            if (remainingPrice > 0 &&
                NpcEconomy.GetNpcMoney(order.customer) < remainingPrice)
            {
                continue;
            }

            if (!tradeAgent.RemoveOwnedItem(
                    order.requestedItem,
                    order.amount,
                    ItemLifecycleEventType.Sold))
            {
                continue;
            }

            if (remainingPrice > 0)
            {
                NpcEconomy.AddNpcMoney(order.customer, -remainingPrice);
                tradeAgent.AddMoney(remainingPrice);
            }

            for (int j = 0; j < order.amount; j++)
            {
                NpcTradeAgent customerTradeAgent =
                    order.customer.GetComponent<NpcTradeAgent>();
                if (customerTradeAgent != null)
                {
                    customerTradeAgent.ReceiveBoughtItem(order.requestedItem);
                }
                else
                {
                    ItemInventory customerInventory =
                        order.customer.GetComponent<ItemInventory>();
                    if (customerInventory != null)
                    {
                        ItemEffectSpawner.PlayPickupEffect(
                            order.requestedItem,
                            order.customer.transform);
                        customerInventory.AddItem(order.requestedItem, 1);
                    }
                }
            }

            NpcSocialEventBus.PublishTradeCompleted(
                gameObject,
                order.customer,
                order.requestedItem,
                order.totalPrice);

            ShowForgeDialogue(
                order.customer,
                NpcText.Dialogue(
                    "forgeOrderReadyBuyer",
                    "Hang cua ban den roi."),
                NpcText.Dialogue(
                    "forgeOrderReadySmith",
                    "Nhan hang di."));
            order.fulfilled = true;
            pendingOrders.RemoveAt(i);
            currentAction = NpcText.Action("cultivate");
            UpdateVisualAnimation();
            return true;
        }

        return false;
    }

    bool TrySellToNearbyBuyer()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                Mathf.Max(0.1f, sellSearchRadius),
                npcLayers);

        NpcTradeAgent bestBuyer = null;
        StatItemData bestItem = null;
        int bestPrice = 0;
        int bestBrokerPrice = 0;

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            NpcTradeAgent buyer =
                hit.GetComponentInParent<NpcTradeAgent>();

            if (buyer == null ||
                buyer == tradeAgent ||
                buyer.inventory == null ||
                NpcRoleUtility.IsDead(buyer.gameObject) ||
                NpcRoleUtility.IsInCombat(buyer.gameObject))
            {
                continue;
            }

            foreach (ItemStack stack in inventory.items)
            {
                if (!CanSellFinishedItem(stack))
                {
                    continue;
                }

                int directPrice =
                    NpcEconomy.GetNpcBuyPrice(
                        stack.item,
                        buyer.gameObject,
                        NpcTradeContext.NpcToNpc);

                if (directPrice <= 0 ||
                    buyer.GetMoney() < directPrice ||
                    buyer.GetBuyScore(stack.item, directPrice) <= 0f)
                {
                    continue;
                }

                int brokerPrice =
                    GetBrokerReferencePrice(stack.item);

                if (preferDirectSaleWhenPriceHigher &&
                    directPrice <= brokerPrice)
                {
                    continue;
                }

                if (directPrice > bestPrice ||
                    (directPrice == bestPrice &&
                    brokerPrice > bestBrokerPrice))
                {
                    bestBuyer = buyer;
                    bestItem = stack.item;
                    bestPrice = directPrice;
                    bestBrokerPrice = brokerPrice;
                }
            }
        }

        if (bestBuyer == null || bestItem == null)
        {
            return false;
        }

        return SellItemToBuyer(bestBuyer, bestItem, 1, bestPrice);
    }

    bool TrySellToBroker()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null ||
            tradeAgent == null ||
            !broker.CanTradeWithNpc(tradeAgent))
        {
            return false;
        }

        if (broker.IsCustomerAtCounter(gameObject))
        {
            bool traded = broker.TryTradeWithNpc(tradeAgent);
            if (traded)
            {
                SetIdleAction();
                UpdateVisualAnimation();
            }

            return traded;
        }

        NpcRoleUtility.SetAction(
            gameObject,
            NpcText.Action("goVanBaoLauBroker"));
        UpdateVisualAnimation();
        return true;
    }

    bool SellItemToBuyer(
        NpcTradeAgent buyer,
        StatItemData item,
        int amount,
        int price)
    {
        if (buyer == null ||
            item == null ||
            amount <= 0 ||
            tradeAgent == null ||
            inventory == null)
        {
            return false;
        }

        int available = inventory.GetAmount(item);
        if (available < amount ||
            buyer.GetMoney() < price)
        {
            return false;
        }

        if (!tradeAgent.RemoveOwnedItem(
                item,
                amount,
                ItemLifecycleEventType.Sold))
        {
            return false;
        }

        buyer.AddMoney(-price);
        tradeAgent.AddMoney(price);

        for (int i = 0; i < amount; i++)
        {
            buyer.ReceiveBoughtItem(item);
        }

        NpcSocialEventBus.PublishTradeCompleted(
            gameObject,
            buyer.gameObject,
            item,
            price);

        return true;
    }

    bool CanSellFinishedItem(ItemStack stack)
    {
        return stack != null &&
            stack.item != null &&
            stack.amount > 0 &&
            !stack.applied &&
            stack.item.itemType == ItemType.PhapBao &&
            stack.item.canBeSold &&
            NpcEconomy.CanTradeNormally(stack.item) &&
            !IsReservedForCustomOrder(stack.item);
    }

    bool IsReservedForCustomOrder(StatItemData item)
    {
        if (item == null ||
            pendingOrders == null ||
            pendingOrders.Count == 0)
        {
            return false;
        }

        foreach (ForgeCustomerOrder order in pendingOrders)
        {
            if (order == null ||
                order.fulfilled ||
                order.requestedItem != item ||
                order.amount <= 0)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    bool TryBuildRecipeCosts(
        ForgeRecipe recipe,
        List<ForgeCost> costs,
        ref ItemGrade highestGrade)
    {
        if (recipe == null)
        {
            return false;
        }

        if (recipe.ingredients == null ||
            recipe.ingredients.Count == 0)
        {
            return false;
        }

        foreach (ForgeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null ||
                ingredient.amount <= 0)
            {
                return false;
            }

            StatItemData resolvedItem =
                ResolveForgeIngredientItem(ingredient);

            if (resolvedItem == null)
            {
                return false;
            }

            int available = inventory.GetAmount(resolvedItem);
            if (available < ingredient.amount)
            {
                return false;
            }

            costs.Add(
                new ForgeCost
                {
                    item = resolvedItem,
                    amount = ingredient.amount
                });

            if (resolvedItem.grade > highestGrade)
            {
                highestGrade = resolvedItem.grade;
            }
        }

        return costs.Count > 0;
    }

    StatItemData ResolveForgeIngredientItem(ForgeIngredient ingredient)
    {
        if (ingredient == null ||
            inventory == null)
        {
            return null;
        }

        if (ingredient.item != null)
        {
            return CanUseAsForgeMaterial(ingredient.item)
                ? ingredient.item
                : null;
        }

        ItemStack fallback = null;
        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.applied ||
                !CanUseAsForgeMaterial(stack.item))
            {
                continue;
            }

            if (ingredient.materialKind != MaterialKind.None &&
                stack.item.materialKind != ingredient.materialKind)
            {
                if (!ingredient.allowAnyMaterialFallback)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = stack;
                }

                continue;
            }

            return stack.item;
        }

        return fallback != null ? fallback.item : null;
    }

    List<ForgeCost> BuildGenericCosts(
        int materialUnits,
        out ItemGrade highestGrade)
    {
        highestGrade = ItemGrade.Ha;
        List<ForgeCost> costs = new List<ForgeCost>();

        if (inventory == null ||
            materialUnits <= 0)
        {
            return costs;
        }

        List<ItemStack> candidates = new List<ItemStack>();
        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.applied ||
                !CanUseAsForgeMaterial(stack.item))
            {
                continue;
            }

            candidates.Add(stack);
        }

        candidates.Sort(CompareForgeMaterialStacks);

        int remaining = materialUnits;
        foreach (ItemStack stack in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            int take = Mathf.Min(stack.amount, remaining);
            if (take <= 0)
            {
                continue;
            }

            costs.Add(
                new ForgeCost
                {
                    item = stack.item,
                    amount = take
                });

            if (stack.item.grade > highestGrade)
            {
                highestGrade = stack.item.grade;
            }

            remaining -= take;
        }

        if (remaining > 0)
        {
            costs.Clear();
        }

        return costs;
    }

    int CompareForgeMaterialStacks(ItemStack a, ItemStack b)
    {
        if (a == null || a.item == null)
        {
            return 1;
        }

        if (b == null || b.item == null)
        {
            return -1;
        }

        int gradeCompare = b.item.grade.CompareTo(a.item.grade);
        if (gradeCompare != 0)
        {
            return gradeCompare;
        }

        int valueCompare =
            NpcEconomy.GetItemValue(b.item).CompareTo(
                NpcEconomy.GetItemValue(a.item));

        if (valueCompare != 0)
        {
            return valueCompare;
        }

        return string.Compare(
            a.item.itemName,
            b.item.itemName,
            System.StringComparison.Ordinal);
    }

    bool ConsumeCosts(List<ForgeCost> costs)
    {
        if (inventory == null ||
            costs == null ||
            costs.Count == 0)
        {
            return false;
        }

        foreach (ForgeCost cost in costs)
        {
            if (cost == null ||
                cost.item == null ||
                cost.amount <= 0 ||
                inventory.GetAmount(cost.item) < cost.amount)
            {
                return false;
            }
        }

        foreach (ForgeCost cost in costs)
        {
            if (!inventory.RemoveItem(cost.item, cost.amount))
            {
                return false;
            }
        }

        return true;
    }

    StatItemData ResolveForgeResult(
        StatItemData preferredResult,
        EquipmentSlot desiredSlot,
        ItemGrade outputGrade)
    {
        StatItemData result = null;

        if (preferredResult != null &&
            preferredResult.itemType == ItemType.PhapBao &&
            (desiredSlot == EquipmentSlot.None ||
            preferredResult.GetResolvedEquipmentSlot() == desiredSlot) &&
            preferredResult.grade == outputGrade)
        {
            return preferredResult;
        }

        result = FindCatalogItem(desiredSlot, outputGrade);

        if (result != null)
        {
            return result;
        }

        result = FindCatalogItem(desiredSlot, GetLowerGrade(outputGrade));
        if (result != null)
        {
            return result;
        }

        result = FindCatalogItem(desiredSlot, GetHigherGrade(outputGrade));
        if (result != null)
        {
            return result;
        }

        if (desiredSlot != EquipmentSlot.None)
        {
            result = FindCatalogItem(EquipmentSlot.None, outputGrade);
        }

        if (result != null)
        {
            return result;
        }

        return FindAnyForgeableItem();
    }

    StatItemData FindCatalogItem(
        EquipmentSlot desiredSlot,
        ItemGrade grade)
    {
        List<StatItemData> candidates = new List<StatItemData>();

        foreach (StatItemData item in forgeCatalogItems)
        {
            if (item == null ||
                item.itemType != ItemType.PhapBao ||
                (excludeImmortalGrade && item.grade == ItemGrade.Tien))
            {
                continue;
            }

            if (item.grade != grade)
            {
                continue;
            }

            if (desiredSlot != EquipmentSlot.None &&
                item.GetResolvedEquipmentSlot() != desiredSlot)
            {
                continue;
            }

            candidates.Add(item);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    StatItemData FindAnyForgeableItem()
    {
        List<StatItemData> candidates = new List<StatItemData>();

        foreach (StatItemData item in forgeCatalogItems)
        {
            if (item == null ||
                item.itemType != ItemType.PhapBao ||
                (excludeImmortalGrade && item.grade == ItemGrade.Tien))
            {
                continue;
            }

            candidates.Add(item);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    bool CanUseAsForgeMaterial(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (acceptAllForgeMaterials)
        {
            return item.itemType == ItemType.VatLieu ||
                item.canBeForgedIntoArtifact;
        }

        if (item.itemType == ItemType.VatLieu && acceptVatLieuAsForgeMaterial)
        {
            return true;
        }

        if (item.itemType == ItemType.DanDuoc && acceptDanDuocAsForgeMaterial)
        {
            return true;
        }

        if (item.itemType == ItemType.PhapBao && acceptPhapBaoAsForgeMaterial)
        {
            return true;
        }

        if (item.itemType == ItemType.ThucPham && acceptThucPhamAsForgeMaterial)
        {
            return true;
        }

        return item.canBeForgedIntoArtifact;
    }

    public bool NeedsMoreMaterials()
    {
        if (inventory == null)
        {
            return true;
        }

        int totalMaterialUnits = 0;

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.applied ||
                !CanUseAsForgeMaterial(stack.item))
            {
                continue;
            }

            totalMaterialUnits += stack.amount;
        }

        return totalMaterialUnits < Mathf.Max(1, genericMaterialUnits);
    }

    ItemGrade ResolveForgeGrade(ItemGrade inputGrade)
    {
        switch (inputGrade)
        {
            case ItemGrade.Trung:
                return Random.value <= 0.4f
                    ? ItemGrade.Trung
                    : ItemGrade.Ha;

            case ItemGrade.Thuong:
                return Random.value <= 0.2f
                    ? ItemGrade.Thuong
                    : ItemGrade.Trung;

            case ItemGrade.Tien:
                return ItemGrade.Tien;

            default:
                return ItemGrade.Ha;
        }
    }

    ItemGrade GetLowerGrade(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Tien:
                return ItemGrade.Thuong;
            case ItemGrade.Thuong:
                return ItemGrade.Trung;
            case ItemGrade.Trung:
                return ItemGrade.Ha;
            default:
                return ItemGrade.Ha;
        }
    }

    ItemGrade GetHigherGrade(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return ItemGrade.Trung;
            case ItemGrade.Trung:
                return ItemGrade.Thuong;
            case ItemGrade.Thuong:
                return ItemGrade.Tien;
            default:
                return ItemGrade.Tien;
        }
    }

    int GetBrokerReferencePrice(StatItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        return NpcEconomy.GetTradePrice(
            item,
            NpcTradeContext.CounterBrokerBuy);
    }

    void SetForgingAction()
    {
        currentAction = NpcText.Action("attack");
        NpcRoleUtility.SetAction(
            gameObject,
            currentAction);
    }

    void SetIdleAction()
    {
        currentAction =
            preferCultivateWhenIdle
                ? NpcText.Action("cultivate")
                : NpcText.Action("idle");

        NpcRoleUtility.SetAction(
            gameObject,
            currentAction);
    }

    void EnsureReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }

        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        if (tradeAgent == null)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
        }

        if (tradeAgent == null)
        {
            tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
        }

        tradeAgent.inventory = inventory;
        tradeAgent.buyUsefulItemsFromMarketTrader = autoBuyMaterialsFromMarketTraders;

        if (inventory != null &&
            !privateInventoryInitialized)
        {
            inventory.UsePrivateNpcRuntimeItems(false);
            privateInventoryInitialized = true;
        }
    }

    void EnsureVisualAnimation()
    {
        if (visualAnimation == null)
        {
            visualAnimation = NPCVisualAnimation.EnsureOn(gameObject);
        }
    }

    void UpdateVisualAnimation()
    {
        if (visualAnimation == null)
        {
            return;
        }

        ApplyForgeFacing();
        visualAnimation.UpdateNPCAnimation(
            Vector2.zero,
            true,
            currentAction);
    }

    void SnapToWorkSpotIfNeeded()
    {
        if (!snapToForgeStandPoint ||
            forgeStandPoint == null)
        {
            return;
        }

        if (!keepAtForgeStandPoint &&
            workSpotInitialized)
        {
            return;
        }

        Vector3 target = forgeStandPoint.position;
        if (transform.position != target)
        {
            transform.position = new Vector3(
                target.x,
                target.y,
                transform.position.z);
        }

        workSpotInitialized = true;
    }

    void ApplyForgeFacing()
    {
        if (visualAnimation == null)
        {
            return;
        }

        if (lockFacingToForgePoint &&
            forgeFacingPoint != null)
        {
            visualAnimation.SetFacingTarget(forgeFacingPoint.position);
            return;
        }

        if (defaultFacingDirection.sqrMagnitude > 0.0001f)
        {
            visualAnimation.SetFacingDirection(defaultFacingDirection);
        }
    }

    void EnsureCatalog()
    {
#if UNITY_EDITOR
        if (autoLoadForgeCatalogFromAssets &&
            forgeCatalogItems.Count == 0)
        {
            RefreshCatalogFromAssets();
        }
#endif
        EnsureDefaultRecipes();
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentWorldHour
            : Time.time / 60f;
    }

    void EnsureDefaultRecipes()
    {
        if (!autoBuildDefaultRecipesFromCatalog ||
            forgeCatalogItems == null ||
            forgeCatalogItems.Count == 0)
        {
            return;
        }

        if (recipes == null)
        {
            recipes = new List<ForgeRecipe>();
        }

        BuildDefaultRecipesFromCatalog();
    }

    void BuildDefaultRecipesFromCatalog()
    {
        HashSet<StatItemData> seen = new HashSet<StatItemData>();

        if (recipes != null)
        {
            foreach (ForgeRecipe recipe in recipes)
            {
                if (recipe != null &&
                    recipe.preferredResult != null)
                {
                    seen.Add(recipe.preferredResult);
                }
            }
        }

        foreach (StatItemData item in forgeCatalogItems)
        {
            if (item == null ||
                seen.Contains(item) ||
                item.itemType != ItemType.PhapBao ||
                (excludeImmortalGrade && item.grade == ItemGrade.Tien))
            {
                continue;
            }

            seen.Add(item);
            recipes.Add(BuildDefaultRecipeForItem(item));
        }
    }

    ForgeRecipe BuildDefaultRecipeForItem(StatItemData item)
    {
        ForgeRecipe recipe = new ForgeRecipe();
        recipe.recipeName = item.itemName + " Recipe";
        recipe.preferredResult = item;
        recipe.outputSlot = item.GetResolvedEquipmentSlot();
        recipe.resultAmount = 1;
        recipe.defaultLeadTimeGameDays = Mathf.Max(1f, 2f + (int)item.grade);
        recipe.depositFraction = 0.3f;
        recipe.allowCustomOrders = true;
        recipe.ingredients = new List<ForgeIngredient>();

        int gradeIndex = Mathf.Max(0, (int)item.grade);
        int baseUnits = Mathf.Max(2, 2 + gradeIndex * 2);

        switch (recipe.outputSlot)
        {
            case EquipmentSlot.Armor:
                AddRecipeIngredient(recipe, MaterialKind.BeastPart, baseUnits);
                AddRecipeIngredient(recipe, MaterialKind.Herb, Mathf.Max(1, 1 + gradeIndex / 2));
                AddRecipeIngredient(recipe, MaterialKind.SpiritStone, Mathf.Max(1, 1 + gradeIndex / 2));
                break;
            case EquipmentSlot.Accessory:
                AddRecipeIngredient(recipe, MaterialKind.CraftingPart, baseUnits);
                AddRecipeIngredient(recipe, MaterialKind.SpiritStone, Mathf.Max(1, 1 + gradeIndex / 2));
                AddRecipeIngredient(recipe, MaterialKind.Herb, Mathf.Max(1, 1 + gradeIndex / 2));
                break;
            default:
                AddRecipeIngredient(recipe, MaterialKind.Ore, baseUnits);
                AddRecipeIngredient(recipe, MaterialKind.BeastCore, Mathf.Max(1, 1 + gradeIndex / 2));
                AddRecipeIngredient(recipe, MaterialKind.SpiritStone, Mathf.Max(1, 1 + gradeIndex / 2));
                break;
        }

        if (recipe.ingredients.Count == 0)
        {
            recipe.genericMaterialUnits = Mathf.Max(1, baseUnits);
        }

        return recipe;
    }

    void AddRecipeIngredient(
        ForgeRecipe recipe,
        MaterialKind materialKind,
        int amount)
    {
        if (recipe == null ||
            amount <= 0)
        {
            return;
        }

        recipe.ingredients.Add(
            new ForgeIngredient
            {
                item = null,
                materialKind = materialKind,
                allowAnyMaterialFallback = true,
                amount = amount
            });
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh Forge Catalog From Assets")]
    public void RefreshCatalogFromAssets()
    {
        if (itemFolders == null ||
            itemFolders.Length == 0)
        {
            return;
        }

        string[] guids =
            AssetDatabase.FindAssets(
                "t:StatItemData",
                itemFolders);

        HashSet<StatItemData> seen = new HashSet<StatItemData>();
        List<StatItemData> loadedItems = new List<StatItemData>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StatItemData item =
                AssetDatabase.LoadAssetAtPath<StatItemData>(path);

            if (item == null ||
                seen.Contains(item) ||
                item.itemType != ItemType.PhapBao)
            {
                continue;
            }

            if (excludeImmortalGrade &&
                item.grade == ItemGrade.Tien)
            {
                continue;
            }

            seen.Add(item);
            loadedItems.Add(item);
        }

        loadedItems.Sort(
            (a, b) =>
            {
                int gradeCompare = a.grade.CompareTo(b.grade);
                if (gradeCompare != 0)
                {
                    return gradeCompare;
                }

                int slotCompare =
                    a.GetResolvedEquipmentSlot().CompareTo(
                        b.GetResolvedEquipmentSlot());
                if (slotCompare != 0)
                {
                    return slotCompare;
                }

                return string.Compare(
                    a.itemName,
                    b.itemName,
                    System.StringComparison.Ordinal);
            });

        forgeCatalogItems = loadedItems;
        EditorUtility.SetDirty(this);
        EnsureDefaultRecipes();
    }
#endif

    float GameHoursToSeconds(float gameHours)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float realSecondsPerGameDay =
            timeSystem != null
                ? Mathf.Max(1f, timeSystem.realSecondsPerGameDay)
                : 900f;

        return Mathf.Max(0.1f, gameHours) *
            realSecondsPerGameDay /
            24f;
    }

    class ForgeCost
    {
        public StatItemData item;
        public int amount;
    }
}
