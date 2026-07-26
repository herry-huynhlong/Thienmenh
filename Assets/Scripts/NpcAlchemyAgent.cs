using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class AlchemyIngredient
{
    public StatItemData item;
    public MaterialKind materialKind = MaterialKind.None;
    public bool allowAnyMaterialFallback = true;
    [Min(1)] public int amount = 1;
}

[System.Serializable]
public class AlchemyRecipe
{
    public string recipeName = "Alchemy Recipe";
    public List<AlchemyIngredient> ingredients = new List<AlchemyIngredient>();
    [Min(1)] public int genericMaterialUnits = 3;
    public StatItemData pill;
    [Min(1)] public int pillAmount = 1;
}

public class NpcAlchemyAgent : MonoBehaviour
{
    [Header("Inventory")]
    public ItemInventory inventory;
    public NpcTradeAgent tradeAgent;

    [Header("Alchemy")]
    public bool autoAlchemy = true;
    public bool preferCultivateWhenIdle = true;
    public float checkInterval = 1f;
    [Min(0f)] public float refineDurationMinGameHours = 2f;
    [Min(0f)] public float refineDurationMaxGameHours = 3f;
    [Min(1)] public int genericMaterialUnits = 3;

    [Header("Work Spot")]
    public Transform alchemyStandPoint;
    public Transform alchemyFacingPoint;
    public Vector2 defaultFacingDirection = Vector2.down;
    public bool snapToAlchemyStandPoint = true;
    public bool keepAtAlchemyStandPoint = true;
    public bool lockFacingToAlchemyPoint = true;

    [Header("Materials")]
    public bool autoBuyMaterialsFromMarketTraders = true;
    public bool acceptAllAlchemyMaterials = true;
    public bool acceptVatLieuAsAlchemyMaterial = true;
    public bool acceptDanDuocAsAlchemyMaterial = false;
    public bool acceptPhapBaoAsAlchemyMaterial = false;
    public bool acceptThucPhamAsAlchemyMaterial = false;
    public bool excludeImmortalGrade = true;

    [Header("Selling")]
    public bool autoSellFinishedGoods = true;
    public bool sellToNearbyNpcBuyers = true;
    public bool sellToVanBaoLau = true;
    public bool preferDirectSaleWhenPriceHigher = true;
    public float sellSearchRadius = 2.25f;
    public LayerMask npcLayers = ~0;

    [Header("Catalog Fallback")]
    public bool autoLoadAlchemyCatalogFromAssets = true;
    public string[] itemFolders =
    {
        "Assets/Item/DanDuoc"
    };
    public List<StatItemData> alchemyCatalogItems = new List<StatItemData>();

    [Header("Recipes")]
    public bool autoBuildDefaultRecipesFromCatalog = true;
    public List<AlchemyRecipe> recipes = new List<AlchemyRecipe>();

    float checkTimer;
    bool refining;
    float refineTimer;
    float currentRefineDurationGameHours;
    StatItemData pendingResult;
    int pendingResultAmount;
    bool privateInventoryInitialized;
    string currentAction = "";
    NPCVisualAnimation visualAnimation;
    bool workSpotInitialized;
    bool movingToBroker;

    public bool IsRefining => refining;
    public float CurrentRefineTotalGameHours =>
        refining
            ? Mathf.Max(0f, currentRefineDurationGameHours)
            : 0f;
    public float CurrentRefineRemainingGameHours =>
        refining
            ? Mathf.Max(0f, SecondsToGameHours(refineTimer))
            : 0f;
    public float CurrentRefineElapsedGameHours =>
        refining
            ? Mathf.Max(
                0f,
                CurrentRefineTotalGameHours -
                CurrentRefineRemainingGameHours)
            : 0f;
    public int CurrentRefineDisplayedElapsedHours
    {
        get
        {
            if (!refining)
            {
                return 0;
            }

            int totalHours =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(CurrentRefineTotalGameHours));

            if (CurrentRefineRemainingGameHours <= 0.01f)
            {
                return totalHours;
            }

            return Mathf.Clamp(
                Mathf.FloorToInt(
                    CurrentRefineElapsedGameHours + 0.0001f),
                0,
                totalHours);
        }
    }
    public bool HasSellableFinishedGoodsAvailable =>
        HasSellableFinishedGoods();

    void Awake()
    {
        EnsureReferences();
        EnsureScheduleController();
        EnsureVisualAnimation();
        SnapToWorkSpotIfNeeded();
        ApplyAlchemyFacing();
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

        if (!refining)
        {
            SetIdleAction();
        }
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (autoLoadAlchemyCatalogFromAssets)
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

        if (UpdateMoveToBroker())
        {
            UpdateVisualAnimation();
            return;
        }

        if (UpdateRefining())
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
        bool canAlchemyNow =
            NpcScheduleController.AllowsAlchemy(gameObject);

        if (autoSellFinishedGoods &&
            canTradeNow &&
            TrySellFinishedGoods())
        {
            return;
        }

        if (autoAlchemy &&
            canAlchemyNow &&
            TryStartAnyAlchemy())
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

        if (!canTradeNow && !canAlchemyNow)
        {
            return;
        }

        SetIdleAction();
        UpdateVisualAnimation();
    }

    public bool TryStartAnyAlchemy()
    {
        EnsureReferences();
        EnsureCatalog();
        EnsureDefaultRecipes();

        if (refining || inventory == null)
        {
            return false;
        }

        if (recipes != null)
        {
            foreach (AlchemyRecipe recipe in recipes)
            {
                if (TryStartAlchemy(recipe))
                {
                    return true;
                }
            }
        }

        return TryStartGenericAlchemy();
    }

    public bool TryStartAlchemy(AlchemyRecipe recipe)
    {
        EnsureReferences();
        EnsureCatalog();
        EnsureDefaultRecipes();

        if (refining ||
            recipe == null ||
            inventory == null)
        {
            return false;
        }

        List<AlchemyCost> costs = new List<AlchemyCost>();
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
            recipe.pill != null
                ? recipe.pill.grade
                : ResolveAlchemyGrade(highestGrade);

        resultItem =
            ResolveAlchemyResult(
                recipe.pill,
                outputGrade);

        if (resultItem == null)
        {
            return false;
        }

        if (!ConsumeCosts(costs))
        {
            return false;
        }

        StartRefining(resultItem, Mathf.Max(1, recipe.pillAmount));
        return true;
    }

    public bool TryStartGenericAlchemy()
    {
        EnsureReferences();
        EnsureCatalog();
        EnsureDefaultRecipes();

        if (refining || inventory == null)
        {
            return false;
        }

        List<AlchemyCost> costs =
            BuildGenericCosts(
                Mathf.Max(1, genericMaterialUnits),
                out ItemGrade highestGrade);

        if (costs.Count == 0)
        {
            return false;
        }

        StatItemData resultItem =
            ResolveAlchemyResult(
                null,
                ResolveAlchemyGrade(highestGrade));

        if (resultItem == null)
        {
            return false;
        }

        if (!ConsumeCosts(costs))
        {
            return false;
        }

        StartRefining(resultItem, 1);
        return true;
    }

    public bool TryStartFixedAlchemy(
        StatItemData pillItem,
        int amount = 1)
    {
        EnsureReferences();

        if (refining ||
            inventory == null ||
            pillItem == null)
        {
            return false;
        }

        StartRefining(
            pillItem,
            Mathf.Max(1, amount));
        return true;
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

        if (sellToNearbyNpcBuyers && TrySellToNearbyBuyer())
        {
            return true;
        }

        if (sellToVanBaoLau && TrySellToBroker())
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

    bool UpdateRefining()
    {
        if (!refining)
        {
            return false;
        }

        refineTimer -= Time.deltaTime;
        SetRefiningAction();

        if (refineTimer > 0f)
        {
            return true;
        }

        FinishRefining();
        return true;
    }

    void StartRefining(StatItemData pillItem, int amount)
    {
        refining = true;
        currentRefineDurationGameHours =
            Random.Range(
                Mathf.Max(0.25f, refineDurationMinGameHours),
                Mathf.Max(
                    Mathf.Max(0.25f, refineDurationMinGameHours),
                    refineDurationMaxGameHours));
        refineTimer =
            GameHoursToSeconds(
                currentRefineDurationGameHours);
        pendingResult = pillItem;
        pendingResultAmount = Mathf.Max(1, amount);
        SetRefiningAction();
        UpdateVisualAnimation();
    }

    void FinishRefining()
    {
        refining = false;
        refineTimer = 0f;
        currentRefineDurationGameHours = 0f;

        if (inventory != null && pendingResult != null)
        {
            int resultAmount =
                pendingResultAmount;
            VillagerAI villager =
                GetComponent<VillagerAI>();

            if (villager != null)
            {
                resultAmount =
                    Mathf.Max(
                        resultAmount,
                        villager.GetProfessionOutputAmountForJob(
                            VillagerJob.Alchemist));
                villager.GainProfessionExpForJob(
                    VillagerJob.Alchemist);
            }

            inventory.AddItem(pendingResult, resultAmount);
            ItemLifecycleSystem.Notify(
                ItemLifecycleEventType.Refined,
                pendingResult,
                gameObject);
        }

        pendingResult = null;
        pendingResultAmount = 0;
        SetIdleAction();
        UpdateVisualAnimation();
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
            movingToBroker = false;
            return false;
        }

        if (broker.IsCustomerAtCounter(gameObject))
        {
            movingToBroker = false;
            bool traded = broker.TryTradeWithNpc(tradeAgent);
            if (traded)
            {
                GainAlchemyProfessionExp();
                SetIdleAction();
                UpdateVisualAnimation();
            }

            return traded;
        }

        movingToBroker = true;
        currentAction = NpcText.Action("goVanBaoLauBroker");
        NpcRoleUtility.SetAction(
            gameObject,
            currentAction);
        UpdateVisualAnimation();
        return true;
    }

    bool UpdateMoveToBroker()
    {
        if (!movingToBroker)
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null ||
            tradeAgent == null ||
            !broker.CanTradeWithNpc(tradeAgent))
        {
            movingToBroker = false;
            return false;
        }

        if (broker.IsCustomerAtCounter(gameObject))
        {
            movingToBroker = false;
            if (broker.TryTradeWithNpc(tradeAgent))
            {
                GainAlchemyProfessionExp();
                SetIdleAction();
            }

            return true;
        }

        currentAction = NpcText.Action("goVanBaoLauBroker");
        NpcRoleUtility.SetAction(gameObject, currentAction);
        NpcRoleUtility.MoveTowards(
            gameObject,
            broker.GetCustomerPositionFor(gameObject),
            2.25f);
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

        GainAlchemyProfessionExp();

        return true;
    }

    void GainAlchemyProfessionExp()
    {
        VillagerAI villager =
            GetComponent<VillagerAI>();

        if (villager != null)
        {
            villager.GainProfessionExpForJob(
                VillagerJob.Alchemist);
        }
    }

    bool CanSellFinishedItem(ItemStack stack)
    {
        return stack != null &&
            stack.item != null &&
            stack.amount > 0 &&
            !stack.applied &&
            stack.item.itemType == ItemType.DanDuoc &&
            stack.item.canBeSold &&
            NpcEconomy.CanTradeNormally(stack.item);
    }

    bool TryBuildRecipeCosts(
        AlchemyRecipe recipe,
        List<AlchemyCost> costs,
        ref ItemGrade highestGrade)
    {
        if (recipe == null ||
            recipe.ingredients == null ||
            recipe.ingredients.Count == 0)
        {
            return false;
        }

        foreach (AlchemyIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null ||
                ingredient.amount <= 0)
            {
                return false;
            }

            StatItemData resolvedItem =
                ResolveAlchemyIngredientItem(ingredient);

            if (resolvedItem == null)
            {
                return false;
            }

            if (inventory.GetAmount(resolvedItem) < ingredient.amount)
            {
                return false;
            }

            costs.Add(
                new AlchemyCost
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

    List<AlchemyCost> BuildGenericCosts(
        int materialUnits,
        out ItemGrade highestGrade)
    {
        highestGrade = ItemGrade.Ha;

        List<AlchemyCost> costs = new List<AlchemyCost>();
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
                !CanUseAsAlchemyMaterial(stack.item))
            {
                continue;
            }

            candidates.Add(stack);
        }

        candidates.Sort(CompareAlchemyMaterialStacks);

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
                new AlchemyCost
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

    int CompareAlchemyMaterialStacks(ItemStack a, ItemStack b)
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

    bool ConsumeCosts(List<AlchemyCost> costs)
    {
        if (inventory == null ||
            costs == null ||
            costs.Count == 0)
        {
            return false;
        }

        foreach (AlchemyCost cost in costs)
        {
            if (cost == null ||
                cost.item == null ||
                cost.amount <= 0 ||
                inventory.GetAmount(cost.item) < cost.amount)
            {
                return false;
            }
        }

        foreach (AlchemyCost cost in costs)
        {
            if (!inventory.RemoveItem(cost.item, cost.amount))
            {
                return false;
            }
        }

        return true;
    }

    StatItemData ResolveAlchemyIngredientItem(AlchemyIngredient ingredient)
    {
        if (ingredient == null || inventory == null)
        {
            return null;
        }

        if (ingredient.item != null)
        {
            return CanUseAsAlchemyMaterial(ingredient.item)
                ? ingredient.item
                : null;
        }

        StatItemData fallback = null;
        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.applied ||
                !CanUseAsAlchemyMaterial(stack.item))
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
                    fallback = stack.item;
                }

                continue;
            }

            return stack.item;
        }

        return fallback;
    }

    StatItemData ResolveAlchemyResult(
        StatItemData preferredPill,
        ItemGrade outputGrade)
    {
        StatItemData result = null;

        if (preferredPill != null &&
            preferredPill.itemType == ItemType.DanDuoc &&
            preferredPill.grade == outputGrade)
        {
            return preferredPill;
        }

        result = FindCatalogItem(outputGrade);
        if (result != null)
        {
            return result;
        }

        result = FindCatalogItem(GetLowerGrade(outputGrade));
        if (result != null)
        {
            return result;
        }

        result = FindCatalogItem(GetHigherGrade(outputGrade));
        if (result != null)
        {
            return result;
        }

        return FindAnyAlchemyItem();
    }

    StatItemData FindCatalogItem(ItemGrade grade)
    {
        List<StatItemData> candidates = new List<StatItemData>();

        foreach (StatItemData item in alchemyCatalogItems)
        {
            if (item == null ||
                item.itemType != ItemType.DanDuoc ||
                (excludeImmortalGrade && item.grade == ItemGrade.Tien))
            {
                continue;
            }

            if (item.grade != grade)
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

    StatItemData FindAnyAlchemyItem()
    {
        List<StatItemData> candidates = new List<StatItemData>();

        foreach (StatItemData item in alchemyCatalogItems)
        {
            if (item == null ||
                item.itemType != ItemType.DanDuoc ||
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

        ApplyAlchemyFacing();
        visualAnimation.UpdateNPCAnimation(
            Vector2.zero,
            true,
            currentAction);
    }

    void SnapToWorkSpotIfNeeded()
    {
        if (!snapToAlchemyStandPoint ||
            alchemyStandPoint == null)
        {
            return;
        }

        if (!keepAtAlchemyStandPoint &&
            workSpotInitialized)
        {
            return;
        }

        Vector3 target = alchemyStandPoint.position;
        if (transform.position != target)
        {
            transform.position = new Vector3(
                target.x,
                target.y,
                transform.position.z);
        }

        workSpotInitialized = true;
    }

    void ApplyAlchemyFacing()
    {
        if (visualAnimation == null)
        {
            return;
        }

        if (lockFacingToAlchemyPoint &&
            alchemyFacingPoint != null)
        {
            visualAnimation.SetFacingTarget(alchemyFacingPoint.position);
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
        if (autoLoadAlchemyCatalogFromAssets &&
            alchemyCatalogItems.Count == 0)
        {
            RefreshCatalogFromAssets();
        }
#endif
        EnsureDefaultRecipes();
    }

    void EnsureDefaultRecipes()
    {
        if (!autoBuildDefaultRecipesFromCatalog ||
            alchemyCatalogItems == null ||
            alchemyCatalogItems.Count == 0)
        {
            return;
        }

        if (recipes == null)
        {
            recipes = new List<AlchemyRecipe>();
        }

        BuildDefaultRecipesFromCatalog();
    }

    void BuildDefaultRecipesFromCatalog()
    {
        HashSet<StatItemData> seen = new HashSet<StatItemData>();

        if (recipes != null)
        {
            foreach (AlchemyRecipe recipe in recipes)
            {
                if (recipe != null &&
                    recipe.pill != null)
                {
                    seen.Add(recipe.pill);
                }
            }
        }

        foreach (StatItemData item in alchemyCatalogItems)
        {
            if (item == null ||
                seen.Contains(item) ||
                item.itemType != ItemType.DanDuoc ||
                (excludeImmortalGrade && item.grade == ItemGrade.Tien))
            {
                continue;
            }

            seen.Add(item);
            recipes.Add(BuildDefaultRecipeForItem(item));
        }
    }

    AlchemyRecipe BuildDefaultRecipeForItem(StatItemData item)
    {
        AlchemyRecipe recipe = new AlchemyRecipe();
        recipe.recipeName = item.itemName + " Recipe";
        recipe.pill = item;
        recipe.pillAmount = 1;
        recipe.genericMaterialUnits = Mathf.Max(1, 2 + (int)item.grade);
        recipe.ingredients = new List<AlchemyIngredient>();

        int gradeIndex = Mathf.Max(0, (int)item.grade);
        int baseUnits = Mathf.Max(2, 2 + gradeIndex * 2);

        switch (item.pillKind)
        {
            case PillKind.Breakthrough:
                AddRecipeIngredient(recipe, MaterialKind.Herb, baseUnits + 2);
                AddRecipeIngredient(recipe, MaterialKind.SpiritStone, Mathf.Max(1, 1 + gradeIndex));
                AddRecipeIngredient(recipe, MaterialKind.BeastCore, Mathf.Max(1, 1 + gradeIndex / 2));
                break;
            case PillKind.Cultivation:
                AddRecipeIngredient(recipe, MaterialKind.Herb, baseUnits + 1);
                AddRecipeIngredient(recipe, MaterialKind.SpiritStone, Mathf.Max(1, 1 + gradeIndex / 2));
                AddRecipeIngredient(recipe, MaterialKind.BeastPart, Mathf.Max(1, 1 + gradeIndex / 2));
                break;
            case PillKind.Heal:
                AddRecipeIngredient(recipe, MaterialKind.Herb, baseUnits);
                AddRecipeIngredient(recipe, MaterialKind.SpiritStone, Mathf.Max(1, 1 + gradeIndex / 2));
                break;
            case PillKind.PermanentAttack:
            case PillKind.PermanentDefense:
            case PillKind.PermanentMaxHP:
                AddRecipeIngredient(recipe, MaterialKind.Herb, baseUnits + 1);
                AddRecipeIngredient(recipe, MaterialKind.BeastCore, Mathf.Max(1, 1 + gradeIndex / 2));
                AddRecipeIngredient(recipe, MaterialKind.SpiritStone, Mathf.Max(1, 1 + gradeIndex / 2));
                break;
            default:
                AddRecipeIngredient(recipe, MaterialKind.Herb, baseUnits);
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
        AlchemyRecipe recipe,
        MaterialKind materialKind,
        int amount)
    {
        if (recipe == null ||
            amount <= 0)
        {
            return;
        }

        recipe.ingredients.Add(
            new AlchemyIngredient
            {
                item = null,
                materialKind = materialKind,
                allowAnyMaterialFallback = true,
                amount = amount
            });
    }

    bool CanUseAsAlchemyMaterial(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (acceptAllAlchemyMaterials)
        {
            return item.itemType == ItemType.VatLieu ||
                item.canBeRefinedIntoPill;
        }

        if (item.itemType == ItemType.VatLieu && acceptVatLieuAsAlchemyMaterial)
        {
            return true;
        }

        if (item.itemType == ItemType.DanDuoc && acceptDanDuocAsAlchemyMaterial)
        {
            return true;
        }

        if (item.itemType == ItemType.PhapBao && acceptPhapBaoAsAlchemyMaterial)
        {
            return true;
        }

        if (item.itemType == ItemType.ThucPham && acceptThucPhamAsAlchemyMaterial)
        {
            return true;
        }

        return false;
    }

    public bool NeedsMoreMaterials()
    {
        if (inventory == null)
        {
            return false;
        }

        EnsureDefaultRecipes();

        if (recipes == null || recipes.Count == 0)
        {
            int totalMaterialUnits = 0;

            foreach (ItemStack stack in inventory.items)
            {
                if (stack == null ||
                    stack.item == null ||
                    stack.amount <= 0 ||
                    stack.applied ||
                    !CanUseAsAlchemyMaterial(stack.item))
                {
                    continue;
                }

                totalMaterialUnits += stack.amount;
            }

            return totalMaterialUnits < Mathf.Max(1, genericMaterialUnits);
        }

        foreach (AlchemyRecipe recipe in recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            List<AlchemyCost> costs = new List<AlchemyCost>();
            ItemGrade highestGrade = ItemGrade.Ha;
            if (TryBuildRecipeCosts(recipe, costs, ref highestGrade))
            {
                return false;
            }
        }

        return true;
    }

    ItemGrade ResolveAlchemyGrade(ItemGrade inputGrade)
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

    void SetRefiningAction()
    {
        movingToBroker = false;
        string refineAction =
            NpcText.Action("fixedAlchemistRefining");
        int totalHours =
            Mathf.Max(
                1,
                Mathf.CeilToInt(CurrentRefineTotalGameHours));
        int elapsedHours =
            Mathf.Clamp(
                CurrentRefineDisplayedElapsedHours,
                0,
                totalHours);
        currentAction =
            refining &&
            CurrentRefineTotalGameHours > 0f
                ? NpcText.ActionFormat(
                    "fixedBlacksmithForgingProgress",
                    refineAction,
                    elapsedHours,
                    totalHours)
                : refineAction;
        NpcRoleUtility.SetAction(
            gameObject,
            currentAction);
    }

    void SetIdleAction()
    {
        movingToBroker = false;
        currentAction =
            preferCultivateWhenIdle
                ? NpcText.Action("cultivate")
                : NpcText.Action("idle");

        NpcRoleUtility.SetAction(
            gameObject,
            currentAction);
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh Alchemy Catalog From Assets")]
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
                item.itemType != ItemType.DanDuoc)
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

                return string.Compare(
                    a.itemName,
                    b.itemName,
                    System.StringComparison.Ordinal);
            });

        alchemyCatalogItems = loadedItems;
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

    float SecondsToGameHours(float seconds)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float realSecondsPerGameDay =
            timeSystem != null
                ? Mathf.Max(1f, timeSystem.realSecondsPerGameDay)
                : 900f;

        return Mathf.Max(0f, seconds) *
            24f /
            realSecondsPerGameDay;
    }

    void UpdateRefineAnimation()
    {
        UpdateVisualAnimation();
    }

    class AlchemyCost
    {
        public StatItemData item;
        public int amount;
    }
}
