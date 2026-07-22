using System.Collections.Generic;
using UnityEngine;

public partial class NpcFixedBlacksmithController
{
    bool HandleNeedMaterials()
    {
        lastProgressWorldHour = GetCurrentWorldHour();
        EnsureDynamicForgeBatchPlan();

        if (autoPlanLowGradeBatches &&
            (!HasConfiguredMaterialRequirements() ||
            forgedItem == null))
        {
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithNeedMoneyToBuy"),
                2f);
            return true;
        }

        if (HasConfiguredMaterialRequirements() &&
            HasAllRequiredMaterials())
        {
            state = ForgeCycleState.Forging;
            forgedWorkHours = 0f;
            stateStartedAtRealtime = -1f;
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithMaterialsReady"),
                1f);
            LogDebug("NeedMaterials", "resumeForgingFromInventory=1");
            return true;
        }

        int minimumBudget =
            GetEstimatedMaterialBudget();

        if (minimumBudget > 0 &&
            NpcEconomy.GetNpcMoney(gameObject) < minimumBudget)
        {
            LogDebug(
                "NeedMaterials",
                "budgetShortfall money=" +
                NpcEconomy.GetNpcMoney(gameObject) +
                " required=" +
                minimumBudget);

            if (!HasConfiguredMaterialRequirements())
            {
                villager.SetActionImmediate(
                    NpcText.Action("fixedBlacksmithNeedMoneyToBuy"),
                    2f);
                return true;
            }
        }

        if (!TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker buyBroker))
        {
            villager.SetActionImmediate(
                NpcText.ActionFormat(
                    "fixedBlacksmithMissingBuyPoint",
                    GetZoneLabel(preferredTradeZone)),
                Mathf.Max(1f, villager.thinkInterval));
            LogDebug(
                "NeedMaterials",
                "missingBuyDestination preferredZone=" + preferredTradeZone);
            return true;
        }

        bool arrivedAtBuyTarget =
            HasArrivedAtTradeDestination(
                buyPosition,
                isBrokerTarget,
                buyBroker);

        LogDebug(
            "BuyRoute",
            DescribeTradeTarget(
                buyPosition,
                buyZone,
                isBrokerTarget,
                buyBroker,
                arrivedAtBuyTarget));

        if (!arrivedAtBuyTarget)
        {
            LogDebug(
                "BuyMove",
                "forceMove=1 " +
                DescribeTradeTarget(
                    buyPosition,
                    buyZone,
                    isBrokerTarget,
                    buyBroker,
                    false));
            MoveVillagerToTradeTarget(
                buyPosition,
                BuyAction,
                buyZone,
                isBrokerTarget);
            return true;
        }

        BeginBuyingMaterials();
        return true;
    }

    void BeginBuyingMaterials()
    {
        state = ForgeCycleState.BuyingMaterials;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate(
            NpcText.Action("fixedBlacksmithBuyingMaterials"),
            buyDurationSeconds);
        LogDebug("State", "BuyingMaterials");
    }

    bool HandleBuyingMaterials()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker buyBroker))
        {
            bool arrivedAtBuyTarget =
                HasArrivedAtTradeDestination(
                buyPosition,
                isBrokerTarget,
                buyBroker);

            LogDebug(
                "BuyingRoute",
                DescribeTradeTarget(
                    buyPosition,
                    buyZone,
                    isBrokerTarget,
                    buyBroker,
                    arrivedAtBuyTarget));

            if (!arrivedAtBuyTarget)
            {
                LogDebug(
                    "BuyingMove",
                    "forceMove=1 " +
                    DescribeTradeTarget(
                        buyPosition,
                        buyZone,
                        isBrokerTarget,
                        buyBroker,
                        false));
                MoveVillagerToTradeTarget(
                    buyPosition,
                    BuyAction,
                    buyZone,
                    isBrokerTarget);
                return true;
            }
        }

        if (!HasActionFinished(buyDurationSeconds))
        {
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithBuyingMaterials"),
                buyDurationSeconds);
            return true;
        }

        if (NpcEconomy.GetNpcMoney(gameObject) < materialCost)
        {
            if (!HasConfiguredMaterialRequirements())
            {
                state = ForgeCycleState.NeedMaterials;
                villager.SetActionImmediate(
                    NpcText.Action("fixedBlacksmithNotEnoughMoneyToBuy"),
                    2f);
                return true;
            }
        }

        string purchaseDetail = string.Empty;
        if (HasConfiguredMaterialRequirements())
        {
            if (!TryPurchaseConfiguredMaterials(out purchaseDetail))
            {
                state = ForgeCycleState.NeedMaterials;
                villager.SetActionImmediate(
                    NpcText.Action("fixedBlacksmithMaterialsPurchaseIncomplete"),
                    2f);
                LogDebug("BuyPending", purchaseDetail);
                return true;
            }
        }
        else
        {
            NpcEconomy.AddNpcMoney(gameObject, -materialCost);
            purchaseDetail =
                "fallbackMoney=" +
                NpcEconomy.GetNpcMoney(gameObject);
        }

        state = ForgeCycleState.Forging;
        forgedWorkHours = 0f;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        lastPurchaseDay = GetCurrentWorldDay();
        villager.SetActionImmediate(
            NpcText.Action("fixedBlacksmithBoughtMaterials"),
            1f);
        LogDebug(
            "BuyComplete",
            "money=" + NpcEconomy.GetNpcMoney(gameObject) +
            " materials=" + purchaseDetail);
        return true;
    }

    bool HandleForging()
    {
        Transform forgePoint = GetForgePoint();
        Vector3 forgePosition = forgePoint != null
            ? GetSafeForgePosition(forgePoint.position)
            : transform.position;
        float currentWorldHour = GetCurrentWorldHour();

        if (forgePoint != null &&
            !IsNear(forgePosition))
        {
            lastProgressWorldHour = currentWorldHour;
            villager.ForceJobMoveTo(
                forgePosition,
                ForgeAction,
                NpcMapNavigator.GetDestinationZone(forgePoint));
            return true;
        }

        if (lastProgressWorldHour < 0f)
        {
            lastProgressWorldHour = currentWorldHour;
        }

        float gainedWorkHours =
            CalculateWorkHoursBetween(lastProgressWorldHour, currentWorldHour);
        lastProgressWorldHour = currentWorldHour;

        if (gainedWorkHours > 0f)
        {
            forgedWorkHours =
                Mathf.Min(
                    RequiredWorkHours,
                    forgedWorkHours + gainedWorkHours);
        }

        if (forgedWorkHours >= RequiredWorkHours)
        {
            CompleteForging();
            return true;
        }

        villager.SetActionImmediate(
            NpcText.ActionFormat(
                "fixedBlacksmithForgingProgress",
                ForgeAction,
                Mathf.CeilToInt(forgedWorkHours),
                Mathf.CeilToInt(RequiredWorkHours)),
            Mathf.Max(1f, villager.thinkInterval));
        return true;
    }

    void CompleteForging()
    {
        string consumedDetail = "none";
        if (HasConfiguredMaterialRequirements() &&
            !TryConsumeConfiguredMaterials(out consumedDetail))
        {
            state = ForgeCycleState.NeedMaterials;
            forgedWorkHours = 0f;
            stateStartedAtRealtime = -1f;
            lastProgressWorldHour = GetCurrentWorldHour();
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithMissingMaterialsContinue"),
                2f);
            LogDebug("ForgeBlocked", consumedDetail);
            return;
        }

        if (inventory != null &&
            forgedItem != null)
        {
            int amount =
                villager != null
                    ? villager.GetProfessionOutputAmountForJob(
                        VillagerJob.Blacksmith)
                    : 1;
            inventory.AddItem(
                forgedItem,
                Mathf.Max(1, amount));
        }

        if (villager != null)
        {
            villager.GainProfessionExpForJob(
                VillagerJob.Blacksmith);
        }

        state = ForgeCycleState.ReadyToSell;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        villager.SetActionImmediate(
            NpcText.Action("fixedBlacksmithForgeComplete"),
            1f);
        LogDebug(
            "ForgeComplete",
            "cycles=" + completedCycles +
            " item=" + (forgedItem != null ? forgedItem.itemName : "null") +
            " consumed=" + consumedDetail);
    }

    bool HandleReadyToSell()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (!TryGetSellDestination(
                out Vector3 sellPosition,
                out NpcMapZone? sellZone,
                out bool isBrokerTarget,
                out NpcCounterBroker sellBroker))
        {
            villager.SetActionImmediate(
                NpcText.ActionFormat(
                    "fixedBlacksmithMissingSellPoint",
                    GetZoneLabel(preferredTradeZone)),
                Mathf.Max(1f, villager.thinkInterval));
            LogDebug(
                "ReadyToSell",
                "missingSellDestination preferredZone=" + preferredTradeZone);
            return true;
        }

        if (!HasArrivedAtTradeDestination(
                sellPosition,
                isBrokerTarget,
                sellBroker))
        {
            MoveVillagerToTradeTarget(
                sellPosition,
                SellAction,
                sellZone,
                isBrokerTarget);
            return true;
        }

        BeginSelling();
        return true;
    }

    void BeginSelling()
    {
        state = ForgeCycleState.Selling;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate(
            NpcText.Action("fixedBlacksmithSellingGoods"),
            sellDurationSeconds);
        LogDebug("State", "Selling");
    }

    bool HandleSelling()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (TryGetSellDestination(
                out Vector3 sellPosition,
                out NpcMapZone? sellZone,
                out bool isBrokerTarget,
                out NpcCounterBroker sellBroker) &&
            !HasArrivedAtTradeDestination(
                sellPosition,
                isBrokerTarget,
                sellBroker))
        {
            MoveVillagerToTradeTarget(
                sellPosition,
                SellAction,
                sellZone,
                isBrokerTarget);
            return true;
        }

        if (!HasActionFinished(sellDurationSeconds))
        {
            villager.SetActionImmediate(
                NpcText.Action("fixedBlacksmithSellingGoods"),
                sellDurationSeconds);
            return true;
        }

        int earnedMoney = salePrice;
        string sellDetail = "fallback";
        bool soldToShop = false;

        if (inventory != null &&
            forgedItem != null)
        {
            soldToShop =
                TrySellForgedItemToMarket(
                    forgedItem,
                    out earnedMoney,
                    out sellDetail);

            if (!soldToShop)
            {
                inventory.RemoveItem(forgedItem, 1);
                sellDetail =
                    "fallbackSalePrice=" +
                    salePrice;
            }
        }

        if (!soldToShop)
        {
            NpcEconomy.AddNpcMoney(gameObject, earnedMoney);
        }

        if (villager != null)
        {
            villager.GainProfessionExpForJob(
                VillagerJob.Blacksmith);
        }

        completedCycles++;
        state = ForgeCycleState.NeedMaterials;
        forgedWorkHours = 0f;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        lastSaleDay = GetCurrentWorldDay();
        villager.SetActionImmediate(WaitAction, 1f);
        LogDebug(
            "SellComplete",
            "money=" + NpcEconomy.GetNpcMoney(gameObject) +
            " cycles=" + completedCycles +
            " detail=" + sellDetail);
        return true;
    }

    bool HasActionFinished(float durationSeconds)
    {
        if (stateStartedAtRealtime < 0f)
        {
            return true;
        }

        return Time.time - stateStartedAtRealtime >= Mathf.Max(0.01f, durationSeconds);
    }

    Transform GetForgePoint()
    {
        if (forgePointOverride != null)
        {
            return forgePointOverride;
        }

        return villager != null
            ? villager.workPoint
            : null;
    }

    Vector3 GetSafeForgePosition(Vector3 preferredPosition)
    {
        if (IsForgePositionClear(preferredPosition))
        {
            return preferredPosition;
        }

        for (int radiusStep = 0; radiusStep < 10; radiusStep++)
        {
            float radius = 0.65f + radiusStep * 0.22f;
            for (int angleStep = 0; angleStep < 20; angleStep++)
            {
                float angle =
                    (angleStep / 20f) * Mathf.PI * 2f +
                    radiusStep * 0.19f;
                Vector3 candidate =
                    preferredPosition +
                    new Vector3(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle),
                        0f) * radius;
                candidate.z = preferredPosition.z;
                if (IsForgePositionClear(candidate))
                {
                    return candidate;
                }
            }
        }

        return preferredPosition;
    }

    bool IsForgePositionClear(Vector3 position)
    {
        float clearanceRadius =
            Mathf.Max(
                0.6f,
                GetApproachClearanceRadius() + 0.2f);
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(position, clearanceRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                !hit.enabled ||
                hit.isTrigger ||
                hit.transform.IsChildOf(transform) ||
                hit.GetComponentInParent<VillagerAI>() != null ||
                hit.GetComponentInParent<SmartNpcAI>() != null ||
                hit.GetComponentInParent<MonsterAI>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    Transform GetMarketPoint()
    {
        if (marketPointOverride != null)
        {
            return marketPointOverride;
        }

        if (villager != null &&
            villager.marketPoint != null)
        {
            return villager.marketPoint;
        }

        return GetForgePoint();
    }

    bool TryGetManualTradePoint(
        Transform pointOverride,
        string sourceLabel,
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        broker = null;

        if (pointOverride == null)
        {
            return false;
        }

        targetZone = ResolveZoneForTransform(pointOverride);

        NpcCounterBroker pointBroker =
            pointOverride.GetComponentInParent<NpcCounterBroker>();
        if (pointBroker == null)
        {
            pointBroker =
                FindBrokerForManualTradePoint(
                    pointOverride.position,
                    targetZone);
        }

        if (pointBroker != null &&
            pointBroker.customerPoint == pointOverride)
        {
            targetPosition = GetBrokerApproachPosition(pointBroker);
            targetZone =
                ResolveBrokerZone(pointBroker) ??
                targetZone;
            isBrokerTarget = pointBroker.receiveAllNpcRequests;
            broker = pointBroker;
            lastTradeDestinationSource =
                sourceLabel + ":brokerCustomerPoint";
            lastTradeDestinationDetail =
                "overridePoint=" + pointOverride.position +
                " overrideZone=" + GetZoneText(targetZone) +
                " broker=" + pointBroker.name +
                " customerPoint=" + pointBroker.customerPoint.position +
                " resolved=" + targetPosition +
                " approachDetail=" + lastBrokerApproachDetail;
            return true;
        }

        if (pointBroker != null &&
            TryGetClearCustomerZonePreferredPoint(
                pointBroker,
                pointOverride.position,
                out Vector3 resolvedPoint))
        {
            targetPosition = resolvedPoint;
            targetZone =
                ResolveBrokerZone(pointBroker) ??
                targetZone;
            broker = pointBroker;
            lastTradeDestinationSource =
                sourceLabel + ":customerZonePreferred";
            lastTradeDestinationDetail =
                "overridePoint=" + pointOverride.position +
                " overrideZone=" + GetZoneText(targetZone) +
                " broker=" + pointBroker.name +
                " preferred=" + pointOverride.position +
                " resolved=" + resolvedPoint +
                " customerPoint=" +
                (pointBroker.customerPoint != null
                    ? pointBroker.customerPoint.position.ToString()
                    : "none");
            return true;
        }

        targetPosition = pointOverride.position;
        broker = pointBroker;
        lastTradeDestinationSource = sourceLabel;
        lastTradeDestinationDetail =
            "overridePoint=" + pointOverride.position +
            " overrideZone=" + GetZoneText(targetZone) +
            " broker=" + (pointBroker != null ? pointBroker.name : "none") +
            " resolved=manualOverride";
        return true;
    }

    bool HasConfiguredMaterialRequirements()
    {
        return materialRequirements != null &&
            materialRequirements.Count > 0;
    }

    void EnsureDynamicForgeBatchPlan()
    {
        if (!autoPlanLowGradeBatches)
        {
            return;
        }

        if (HasConfiguredMaterialRequirements() &&
            forgedItem != null &&
            GetExpectedForgeSaleValue(forgedItem) >=
            GetEstimatedMaterialBudget() + Mathf.Max(0, craftingLaborFee))
        {
            return;
        }

        TryGenerateDynamicForgeBatchPlan();
    }

    bool TryGenerateDynamicForgeBatchPlan()
    {
        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop) ||
            shop == null ||
            shop.items == null ||
            shop.items.Count == 0)
        {
            return false;
        }

        NpcForgeAgent forgeAgent = GetComponent<NpcForgeAgent>();
        if (forgeAgent == null ||
            forgeAgent.forgeCatalogItems == null ||
            forgeAgent.forgeCatalogItems.Count == 0)
        {
            return false;
        }

        List<StatItemData> materialPool = new List<StatItemData>();
        for (int i = 0; i < shop.items.Count; i++)
        {
            ShopItemSlot slot = shop.items[i];
            if (slot == null ||
                slot.item == null ||
                slot.amount <= 0 ||
                !IsValidLowGradeForgeMaterial(slot.item))
            {
                continue;
            }

            if (!materialPool.Contains(slot.item))
            {
                materialPool.Add(slot.item);
            }
        }

        if (materialPool.Count < Mathf.Max(1, randomMaterialKindsMin))
        {
            return false;
        }

        ShuffleItems(materialPool);

        int plannedKinds =
            Mathf.Clamp(
                Random.Range(
                    Mathf.Max(1, randomMaterialKindsMin),
                    Mathf.Max(randomMaterialKindsMin, randomMaterialKindsMax) + 1),
                1,
                materialPool.Count);

        List<FixedBlacksmithMaterialRequirement> plannedRequirements =
            new List<FixedBlacksmithMaterialRequirement>();
        int totalCost = 0;

        for (int i = 0; i < plannedKinds; i++)
        {
            StatItemData item = materialPool[i];
            int unitPrice = shop.GetNpcBuyPrice(item, gameObject);
            if (unitPrice <= 0)
            {
                continue;
            }

            plannedRequirements.Add(
                new FixedBlacksmithMaterialRequirement
                {
                    item = item,
                    amount = 1
                });
            totalCost += unitPrice;
        }

        if (plannedRequirements.Count < Mathf.Max(1, randomMaterialKindsMin))
        {
            return false;
        }

        int minimumSaleValue =
            totalCost + Mathf.Max(0, craftingLaborFee);
        List<StatItemData> resultCandidates =
            new List<StatItemData>();

        for (int i = 0; i < forgeAgent.forgeCatalogItems.Count; i++)
        {
            StatItemData candidate =
                forgeAgent.forgeCatalogItems[i];
            if (candidate == null ||
                candidate.itemType != ItemType.PhapBao ||
                candidate.grade != ItemGrade.Ha ||
                !candidate.canBeSold)
            {
                continue;
            }

            if (GetExpectedForgeSaleValue(candidate) < minimumSaleValue)
            {
                continue;
            }

            resultCandidates.Add(candidate);
        }

        if (resultCandidates.Count == 0)
        {
            return false;
        }

        forgedItem =
            resultCandidates[
                Random.Range(0, resultCandidates.Count)];
        materialRequirements = plannedRequirements;
        materialCost = totalCost;
        salePrice = Mathf.Max(
            minimumSaleValue,
            GetExpectedForgeSaleValue(forgedItem));
        lastBatchMaterialBudget = totalCost;
        lastBatchMinimumSaleValue = minimumSaleValue;
        return true;
    }

    bool IsValidLowGradeForgeMaterial(StatItemData item)
    {
        if (item == null ||
            item.itemType != ItemType.VatLieu ||
            item.grade != ItemGrade.Ha ||
            !item.canBeSold)
        {
            return false;
        }

        switch (item.materialKind)
        {
            case MaterialKind.Ore:
            case MaterialKind.SpiritStone:
            case MaterialKind.CraftingPart:
            case MaterialKind.BeastPart:
            case MaterialKind.BeastCore:
                return true;
            default:
                return false;
        }
    }

    int GetExpectedForgeSaleValue(StatItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        SimpleItemShop shop;
        if (TryFindPreferredTradeShop(out shop) &&
            shop != null)
        {
            return Mathf.Max(
                1,
                shop.GetSellPrice(item));
        }

        return Mathf.Max(
            1,
            NpcEconomy.GetTradePrice(
                item,
                NpcTradeContext.MarketSell));
    }

    static void ShuffleItems<T>(List<T> items)
    {
        if (items == null)
        {
            return;
        }

        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T temp = items[i];
            items[i] = items[swapIndex];
            items[swapIndex] = temp;
        }
    }

    int GetEstimatedMaterialBudget()
    {
        if (!HasConfiguredMaterialRequirements())
        {
            return autoPlanLowGradeBatches
                ? 0
                : materialCost;
        }

        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop))
        {
            return materialCost;
        }

        int total = 0;
        foreach (FixedBlacksmithMaterialRequirement requirement in materialRequirements)
        {
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            int missing =
                GetMissingMaterialAmount(requirement);
            if (missing <= 0)
            {
                continue;
            }

            int unitPrice =
                shop.GetNpcBuyPrice(
                    requirement.item,
                    gameObject);

            if (unitPrice <= 0)
            {
                unitPrice = Mathf.Max(
                    1,
                    NpcEconomy.GetNpcBuyPrice(
                        requirement.item,
                        gameObject,
                        NpcTradeContext.MarketBuy));
            }

            total += unitPrice * missing;
        }

        return Mathf.Max(0, total);
    }

    int GetMissingMaterialAmount(
        FixedBlacksmithMaterialRequirement requirement)
    {
        if (requirement == null ||
            requirement.item == null ||
            requirement.amount <= 0 ||
            inventory == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            requirement.amount -
            inventory.GetAmount(requirement.item));
    }

    bool TryPurchaseConfiguredMaterials(out string detail)
    {
        detail = "noRequirements";

        if (!HasConfiguredMaterialRequirements())
        {
            return true;
        }

        if (inventory == null)
        {
            detail = "missingInventory";
            return false;
        }

        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop))
        {
            detail = "missingShop";
            return false;
        }

        List<string> purchases = new List<string>();

        foreach (FixedBlacksmithMaterialRequirement requirement in materialRequirements)
        {
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            int missing =
                GetMissingMaterialAmount(requirement);
            if (missing <= 0)
            {
                continue;
            }

            int itemIndex =
                shop.FindItemIndex(
                    requirement.item);

            if (itemIndex < 0)
            {
                detail =
                    "missingStock=" +
                    requirement.item.itemName;
                return false;
            }

            int boughtAmount;
            int totalPrice;
            if (!shop.BuyNpcItemToInventory(
                    itemIndex,
                    gameObject,
                    inventory,
                    missing,
                    out boughtAmount,
                    out totalPrice))
            {
                detail =
                    "buyFailed=" +
                    requirement.item.itemName;
                return false;
            }

            purchases.Add(
                requirement.item.itemName +
                "x" + boughtAmount +
                " price=" + totalPrice);
        }

        bool hasAllMaterials =
            HasAllRequiredMaterials();

        detail =
            purchases.Count > 0
                ? string.Join("; ", purchases)
                : "alreadyReady";

        return hasAllMaterials;
    }

    bool HasAllRequiredMaterials()
    {
        if (!HasConfiguredMaterialRequirements() ||
            inventory == null)
        {
            return false;
        }

        foreach (FixedBlacksmithMaterialRequirement requirement in materialRequirements)
        {
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            if (inventory.GetAmount(requirement.item) < requirement.amount)
            {
                return false;
            }
        }

        return true;
    }

    bool TryConsumeConfiguredMaterials(out string detail)
    {
        detail = "noRequirements";

        if (!HasConfiguredMaterialRequirements())
        {
            return true;
        }

        if (!HasAllRequiredMaterials())
        {
            detail = "missingOwnedMaterials";
            return false;
        }

        List<string> consumed = new List<string>();

        foreach (FixedBlacksmithMaterialRequirement requirement in materialRequirements)
        {
            if (requirement == null ||
                requirement.item == null ||
                requirement.amount <= 0)
            {
                continue;
            }

            if (!inventory.RemoveItem(
                    requirement.item,
                    requirement.amount))
            {
                detail =
                    "consumeFailed=" +
                    requirement.item.itemName;
                return false;
            }

            consumed.Add(
                requirement.item.itemName +
                "x" + requirement.amount);
        }

        detail = string.Join("; ", consumed);
        return true;
    }

}
