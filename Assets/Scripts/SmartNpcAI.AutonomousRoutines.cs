using UnityEngine;

// Trade, pill-buying, social actions, and autonomous gather/sell routines.
public partial class SmartNpcAI
{
    bool GoToTavernAndBuyPill()
    {
        if (HasAvailablePills())
        {
            ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
            nextNeedPotionRetryTime = 0f;
            currentAction = NpcText.Action("idle");
            actionTimer = Mathf.Max(0.05f, thinkDelay * 0.25f);
            return true;
        }

        if (IsNeedPotionRetryCoolingDown())
        {
            return false;
        }

        RequestEmergencyTask(
            SmartAITaskGoal.NeedPotion,
            SmartAITaskPriority.Important,
            false,
            "buy pill");

        NpcCounterBroker broker =
            NpcCounterBroker.FindBestBrokerForNpc(gameObject);
        if (broker != null &&
            broker.receiveAllNpcRequests)
        {
            Transform brokerTarget =
                broker.customerPoint != null
                ? broker.customerPoint
                : broker.transform;

            if (brokerTarget == null)
            {
                ClearTravelTargetsAndStop();
                ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
                currentAction = NpcText.Action("calm");
                return false;
            }

            currentAction = NpcText.Action("goVanBaoLauBroker");
            ClearTravelTargets();
            Vector3 brokerApproach =
                ResolveBrokerApproachPosition(broker);
            currentTarget = null;
            wanderTarget = brokerApproach;
            hasWanderTarget = true;

            if (TryHandleBrokerPillPurchaseIfReady(
                    broker,
                    brokerApproach))
            {
                pill = Mathf.Max(0, CountOwnedPillItems());
                return true;
            }

            return true;
        }

        return TryGoToTavernAndBuyKnownPill(false);
    }

    bool GoToTavernAndBuyHealingPill()
    {
        if (HasAvailableRecoveryPills())
        {
            return TryConsumePillForLowHpRecovery();
        }

        if (IsNeedPotionRetryCoolingDown())
        {
            return false;
        }

        return TryGoToTavernAndBuyKnownPill(true);
    }

    bool TryGoToTavernAndBuyKnownPill(
        bool recoveryPurchase)
    {
        string traceLabel =
            recoveryPurchase
                ? "GoToTavernAndBuyHealingPill"
                : "GoToTavernAndBuyPill";

        Transform buyTarget = tavernPoint;
        Vector3 buyPosition =
            buyTarget != null
                ? buyTarget.position
                : Vector3.zero;

        if (buyTarget == null &&
            !TryResolveTradeFallbackPosition(
                NpcScheduleActivity.BuyGoods,
                NpcLocationPurpose.BuyGoods,
                out buyPosition))
        {
            ClearTravelTargetsAndStop();
            if (!recoveryPurchase)
            {
                ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
            }

            currentAction = NpcText.Action("idle");
            TraceRuntime(
                traceLabel,
                "no-destination");
            return false;
        }

        currentAction = NpcText.Action("goTavern");
        ClearTravelTargets();
        currentTarget = buyTarget;
        wanderTarget = buyPosition;
        hasWanderTarget = buyTarget == null;

        Vector3 approachPosition =
            buyTarget != null
                ? GetApproachPosition(buyTarget)
                : buyPosition;
        float distance =
            Vector2.Distance(
                transform.position,
                approachPosition);

        if (ShouldLogDebugFlow())
        {
            DebugFlow(
                "Trade",
                (recoveryPurchase
                    ? "NeedRecoveryPill target="
                    : "NeedPotion target=") +
                (buyTarget != null ? buyTarget.name : "wander") +
                " buyPos=" +
                buyPosition +
                " approach=" +
                approachPosition +
                " distance=" +
                distance.ToString("0.00") +
                " action=" +
                currentAction);
        }

        if (distance < 1.5f)
        {
            currentAction = NpcText.Action("buyPill");

            StatItemData purchasedItem =
                recoveryPurchase
                    ? FindPreferredRecoveryPillForPurchase()
                    : FindPreferredCultivationPillForPurchase();
            if (purchasedItem == null ||
                !TryReceivePurchasedPill(
                    purchasedItem,
                    recoveryPurchase))
            {
                DeferNeedPotionRetry(
                    recoveryPurchase
                        ? NpcText.Action("calm")
                        : NpcText.Action("checkedVanBaoLau"));
                return false;
            }

            money -= 50;
            pill = Mathf.Max(0, CountOwnedPillItems());
            if (!recoveryPurchase)
            {
                ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
                nextNeedPotionRetryTime = 0f;
            }

            ClearTravelTargetsAndStop();
            stuckMoveTimer = 0f;
            blockedMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        tradeSessionMinGameHours,
                        tradeSessionMaxGameHours));

            if (recoveryPurchase)
            {
                TryConsumePillForLowHpRecovery();
            }

            Debug.Log(
                NpcText.Format(
                    NpcText.Get("logs", "buyPill"),
                    npcName) +
                " item=" +
                purchasedItem.itemName);
        }

        return true;
    }

    bool TryReceivePurchasedPill(
        StatItemData item,
        bool immediateUse)
    {
        if (item == null)
        {
            return false;
        }

        ItemInventory inventory = GetNpcItemInventory();
        if (inventory == null)
        {
            return false;
        }

        NpcItemCollector collector =
            GetComponent<NpcItemCollector>();
        if (collector != null)
        {
            collector.ReceiveItem(
                item,
                ItemLifecycleEventType.Picked,
                immediateUse);
            return true;
        }

        inventory.AddItem(item, 1);
        if (immediateUse &&
            item.IsNpcLowHpRecoveryPill())
        {
            TryConsumeAvailableRecoveryPill(out _);
        }

        return true;
    }

    StatItemData FindPreferredRecoveryPillForPurchase()
    {
        StatItemData[] loadedItems =
            Resources.FindObjectsOfTypeAll<StatItemData>();
        StatItemData best = null;
        float bestStrength = float.MaxValue;

        foreach (StatItemData item in loadedItems)
        {
            if (item == null ||
                !item.IsNpcLowHpRecoveryPill())
            {
                continue;
            }

            float strength =
                Mathf.Max(
                    1f,
                    item.hpBonus);
            if (best == null ||
                strength < bestStrength)
            {
                best = item;
                bestStrength = strength;
            }
        }

        return best;
    }

    StatItemData FindPreferredCultivationPillForPurchase()
    {
        StatItemData[] loadedItems =
            Resources.FindObjectsOfTypeAll<StatItemData>();
        StatItemData best = null;
        int bestPriority = int.MaxValue;
        float bestStrength = float.MaxValue;

        foreach (StatItemData item in loadedItems)
        {
            if (item == null ||
                !item.IsNpcCultivationReservePill())
            {
                continue;
            }

            int priority =
                GetCultivationPurchasePriority(item);
            float strength =
                GetCultivationPurchaseStrength(item);
            if (best == null ||
                priority < bestPriority ||
                (priority == bestPriority &&
                strength < bestStrength))
            {
                best = item;
                bestPriority = priority;
                bestStrength = strength;
            }
        }

        return best;
    }

    int GetCultivationPurchasePriority(
        StatItemData item)
    {
        if (item == null)
        {
            return int.MaxValue;
        }

        switch (item.pillKind)
        {
            case PillKind.Cultivation:
                return 0;
            case PillKind.Breakthrough:
                return 1;
            default:
                return 2;
        }
    }

    float GetCultivationPurchaseStrength(
        StatItemData item)
    {
        if (item == null)
        {
            return float.MaxValue;
        }

        switch (item.pillKind)
        {
            case PillKind.Cultivation:
                return Mathf.Max(1f, item.cultivationBonus);
            case PillKind.Breakthrough:
                return Mathf.Max(
                    1f,
                    item.cultivationBonus +
                    (item.breakthroughRealm
                        ? 100000f
                        : 0f));
            default:
                return Mathf.Max(1f, item.GetNpcUseScore());
        }
    }

    bool IsNeedPotionRetryCoolingDown()
    {
        return Time.time < nextNeedPotionRetryTime;
    }

    void DeferNeedPotionRetry(string action)
    {
        nextNeedPotionRetryTime =
            Time.time +
            Mathf.Max(
                thinkDelay * 2f,
                GameHoursToSeconds(0.5f));
        ClearTravelTargetsAndStop();
        ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
        actionTimer = Mathf.Max(thinkDelay, GameHoursToSeconds(0.15f));
        currentAction = action;
    }

    bool TryResolveTradeFallbackPosition(
        NpcScheduleActivity activity,
        NpcLocationPurpose purpose,
        out Vector3 position)
    {
        if (NpcLocationArea.TryGetPosition(
                gameObject,
                activity,
                VillagerJob.None,
                purpose,
                transform.position,
                out position,
                out _))
        {
            return true;
        }

        NpcLocationArea fallbackArea =
            NpcLocationArea.FindBestArea(
                gameObject,
                activity,
                VillagerJob.None,
                NpcLocationPurpose.Any,
                null,
                GetSmartDangerTier(),
                transform.position);

        if (fallbackArea != null)
        {
            position = fallbackArea.GetRandomPoint(gameObject);
            return true;
        }

        position = transform.position;
        return false;
    }

    bool TryStartTradePresenceRoutine()
    {
        if (!canTrade)
        {
            return false;
        }

        if (!TryResolveTradeFallbackPosition(
                NpcScheduleActivity.TradeBuySell,
                NpcLocationPurpose.Market,
                out Vector3 marketPosition) &&
            !TryResolveTradeFallbackPosition(
                NpcScheduleActivity.TradeBuySell,
                NpcLocationPurpose.Any,
                out marketPosition))
        {
            DebugFlow("Trade", "No fallback market area");
            return false;
        }

        float arriveDistance =
            Mathf.Max(
                targetClearRadius * 2f,
                0.45f);

        if (Vector2.Distance(transform.position, marketPosition) <=
            arriveDistance)
        {
            ClearTravelTargetsAndStop();
            actionTimer = Mathf.Max(
                actionTimer,
                GameHoursToSeconds(0.25f));
            currentAction = NpcText.Action("tradeSeek");
            DebugFlow("Trade", "Waiting inside market area");
            return true;
        }

        ClearTravelTargets();
        currentTarget = null;
        wanderTarget = marketPosition;
        hasWanderTarget = true;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        currentAction = NpcText.Action("goMarketTrade");
        DebugFlow("Trade", "Moving to market fallback area");
        return true;
    }

    bool IsPointInsideNpcLocationArea(
        NpcLocationArea area,
        Vector3 point)
    {
        if (area == null)
        {
            return false;
        }

        Collider2D bounds =
            area.areaBounds != null
                ? area.areaBounds
                : area.GetComponent<Collider2D>();

        if (bounds != null)
        {
            return bounds.OverlapPoint(point);
        }

        Vector2 half =
            area.fallbackSize * 0.5f;
        Vector3 center =
            area.transform.position;

        return point.x >= center.x - half.x &&
            point.x <= center.x + half.x &&
            point.y >= center.y - half.y &&
            point.y <= center.y + half.y;
    }

    void MakeFriend()
    {
        currentAction = NpcText.Action("makeFriend");

        Debug.Log(NpcText.Format(NpcText.Get("logs", "makeFriend"), npcName));
    }

    void TryCreateSect()
    {
        if (realm >=
            CultivationRealm.SoulFormation)
        {
            currentAction = NpcText.Action("createSect");

            Debug.Log(NpcText.Format(NpcText.Get("logs", "createSect"), npcName));
        }
    }

    bool TryStartResourceGatheringRoutine()
    {
        if (!canGather ||
            !canCompeteResource)
        {
            return false;
        }

        if (resourceGatherer == null)
        {
            resourceGatherer = GetComponent<NpcResourceGatherer>();
        }

        if (resourceGatherer != null)
        {
            resourceGatherer.canGather = true;
            resourceGatherer.limitHarvestsPerScheduleSlot = false;
            resourceGatherer.maxHarvestsPerScheduleSlot = 0;
        }

        if (resourceGatherer != null &&
            resourceGatherer.HasActiveGatheringFlow)
        {
            DebugFlow("Gather", "Gatherer already has active flow");
            return true;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        bool gatherCompleted =
            schedule != null &&
            schedule.enforceSchedule &&
            schedule.HasCompletedCurrentSlotActivity(
                NpcScheduleActivity.Gather);
        if (gatherCompleted)
        {
            if (schedule != null &&
                resourceGatherer != null &&
                !resourceGatherer.limitHarvestsPerScheduleSlot)
            {
                schedule.ClearCurrentSlotActivityState(
                    NpcScheduleActivity.Gather);
            }
            else
            {
                DebugFlow("Gather", "Schedule slot already completed");
                return false;
            }
        }

        bool gatherStarted =
            schedule != null &&
            schedule.enforceSchedule &&
            schedule.HasStartedCurrentSlotActivity(
                NpcScheduleActivity.Gather);
        if (gatherStarted)
        {
            bool hasActiveGatherFlow =
                hasWanderTarget ||
                currentTarget != null ||
                currentAction == NpcText.Action("gatherResource") ||
                currentAction == NpcText.Action("pickItem");

            if (hasActiveGatherFlow)
            {
                DebugFlow("Gather", "Continuing active gather flow");
                return true;
            }

            DebugFlow("Gather", "Started flag stale, rebuilding gather flow");
        }

        if (resourceGatherer == null)
        {
            DebugFlow("Gather", "Missing resource gatherer");
            return false;
        }

        if (resourceGatherer.TryStartGatheringNow())
        {
            DebugFlow("Gather", "Start gathering immediately");
            return true;
        }

        bool hasAutonomousGatherCandidate =
            resourceGatherer != null &&
            resourceGatherer.HasAutonomousGatherCandidate(GetSmartDangerTier());

        if (!hasAutonomousGatherCandidate)
        {
            DebugFlow("Gather", "No autonomous gather target, move to resource area");
        }

        if (TryGetSmartResourceArea(out Vector3 resourcePosition))
        {
            ClearTravelTargets();
            wanderTarget = resourcePosition;
            hasWanderTarget = true;
            currentAction = NpcText.Action("gatherResource");
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    NpcScheduleActivity.Gather);
            }
            DebugFlow("Gather", hasAutonomousGatherCandidate
                ? "Move to gather area"
                : "Move to resource area and wait");
            return true;
        }

        DebugFlow("Gather", "No gather target found");
        return false;
    }

    bool TryStartSellGoodsRoutine()
    {
        if (!canTrade ||
            !canSellGoods)
        {
            return false;
        }

        if (tradeAgent == null)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
        }

        if (tradeAgent == null ||
            tradeAgent.inventory == null ||
            !HasSellableGoods())
        {
            DebugFlow("Sell", "No trade agent or goods");
            return false;
        }

        NpcCounterBroker broker =
            NpcCounterBroker.FindBestBrokerForNpc(gameObject);
        if (broker == null)
        {
            if (TryResolveTradeFallbackPosition(
                NpcScheduleActivity.SellGoods,
                NpcLocationPurpose.SellGoods,
                out Vector3 fallbackSellPosition))
            {
                ClearTravelTargets();
                currentTarget = null;
                wanderTarget = fallbackSellPosition;
                hasWanderTarget = true;
                hasEscapeTarget = false;
                hasObstacleAvoidTarget = false;
                currentAction = NpcText.Action("goMarketTrade");
                DebugFlow("Sell", "No active broker; moving to fallback area");
                return true;
            }

            DebugFlow("Sell", "No active broker");
            return false;
        }

        Transform sellTarget =
            broker.customerPoint != null
            ? broker.customerPoint
            : broker.transform;

        if (sellTarget == null)
        {
            DebugFlow("Sell", "Broker has no sell target");
            return false;
        }

        ClearTravelTargets();
        currentTarget = sellTarget;
        currentAction = NpcText.Action("tradeSeek");

        if (Vector2.Distance(transform.position, sellTarget.position) >
            Mathf.Max(0.5f, broker.CustomerServiceRadius))
        {
            DebugFlow("Sell", "Moving to broker");
            return true;
        }

        if (NpcCounterBroker.TryTradeWithActiveBroker(tradeAgent))
        {
            ClearTravelTargetsAndStop();
            actionTimer = GameHoursToSeconds(Random.Range(0.2f, 0.8f));
            currentAction = NpcText.Action("tradeSeek");
            DebugFlow("Sell", "Trade completed");
        }
        else
        {
            DebugFlow("Sell", "Reached broker but trade failed");
        }

        return true;
    }

    bool HasSellableGoods()
    {
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            return false;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.ShouldNpcPreferSell())
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
