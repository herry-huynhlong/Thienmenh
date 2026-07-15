using UnityEngine;

public partial class VillagerAI
{
    void TryScheduledTaskOrWait()
    {
        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider == null)
        {
            GoHomeIdle(GetScheduledTradeIdleAction());
            return;
        }

        Vector3 providerPosition =
            provider.GetProviderPositionFor(gameObject);
        NpcMapZone? providerZone = GetTargetZone(provider.transform);

        currentAction = NpcText.Action("goTaskProviderDaily");

        if (Vector2.Distance(transform.position, providerPosition) > arriveDistance)
        {
            MoveUsingRoad(
                providerPosition,
                providerZone);
            return;
        }

        ClearMovementTargets();
        StopMoving();

        if (!provider.TryHandleVisitor(gameObject))
        {
            actionTimer = Mathf.Max(thinkInterval, 2f);
            currentAction = NpcText.Action("visitedTaskProvider");
        }
    }

    bool TryHandleTraderImmediateNeeds()
    {
        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return true;
        }

        if (TryProcessDailyTaskPlan())
        {
            return true;
        }

        return false;
    }

    void TryTradeOrTaskOrIdle()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (job == VillagerJob.Trader && ShouldVisitCounterBroker())
        {
            GoTrade();
            return;
        }

        NpcTaskProvider provider = NpcTaskProvider.FindNearestProvider(transform.position);
        if (provider != null &&
            provider.TryHandleVisitor(gameObject))
        {
            return;
        }

        Wander(GetScheduledTradeIdleAction());
    }

    void GoTrade()
    {
        if (job != VillagerJob.Trader)
        {
            return;
        }

        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (!hasTradeTarget)
        {
            currentTradeTarget = GetTraderWorkPosition();
            currentTradeTargetZone = GetTraderWorkTargetZone();

            hasTradeTarget = true;
        }

        MoveUsingRoad(currentTradeTarget, currentTradeTargetZone);
        currentAction = NpcText.Action("goMarketTrade");

        NpcCounterBroker activeBroker = NpcCounterBroker.Active;
        bool arrivedForTrade = activeBroker != null && activeBroker.receiveAllNpcRequests
            ? IsInsideBrokerServiceArea(activeBroker)
            : IsAtPosition(currentTradeTarget);

        if (arrivedForTrade)
        {
            ClearMovementTargets();
            StopMoving();
            hasTradeTarget = false;
            currentTradeTargetZone = null;

            if (TryTradeAtCounterOrTakeTask())
            {
                return;
            }

            actionTimer = Mathf.Max(1f, thinkInterval);
            currentAction = GetScheduledTradeIdleAction();
        }
    }

    string GetScheduledTradeIdleAction()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule != null && schedule.enforceSchedule)
        {
            switch (schedule.CurrentActivity)
            {
                case NpcScheduleActivity.BuyGoods:
                    return NpcText.Action("goMarketTrade");
                case NpcScheduleActivity.SellGoods:
                    return NpcText.Action("waitTraderBuyGoods");
                case NpcScheduleActivity.TakeTask:
                    return NpcText.Action("visitedTaskProvider");
            }
        }

        return NpcText.Action("noTrade");
    }

    bool TryTradeAtCounterOrTakeTask()
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        bool canTradeNow = NpcScheduleController.AllowsTrade(gameObject);
        bool canTakeTaskNow = NpcScheduleController.AllowsTask(gameObject);
        bool traded = false;
        NpcCounterBroker broker = NpcCounterBroker.Active;

        if (canTradeNow &&
            broker != null &&
            broker.receiveAllNpcRequests &&
            IsInsideBrokerServiceArea(broker))
        {
            NpcTradeAgent tradeAgent = GetComponent<NpcTradeAgent>();
            if (tradeAgent != null && broker.CanTradeWithNpc(tradeAgent))
            {
                traded = broker.TryTradeWithNpc(tradeAgent);
            }
        }

        if (canTradeNow && !traded && HasSellableGoods())
        {
            traded = TrySellGoodsToTrader();
        }

        if (traded)
        {
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        tradeSessionMinGameHours,
                        tradeSessionMaxGameHours));
            currentAction = NpcText.Action("trading");
            return true;
        }

        if (!canTakeTaskNow)
        {
            return false;
        }

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider != null &&
            provider.TryHandleVisitor(gameObject))
        {
            return true;
        }

        return false;
    }

    void GoSellGoods()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (job == VillagerJob.Fisher &&
            !HasSellableGoods())
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (!hasSellTarget)
        {
            resolvedSellLocationZone = null;
            currentSellTarget = GetSellGoodsTarget();
            currentSellTargetZone = GetSellGoodsTargetZone();
            hasSellTarget = true;
        }

        MoveUsingRoad(currentSellTarget, currentSellTargetZone);
        currentAction = NpcText.Action("bringGoodsToCounter");

        NpcCounterBroker activeBroker = NpcCounterBroker.Active;
        bool arrivedToSell = activeBroker != null && activeBroker.receiveAllNpcRequests
            ? IsInsideBrokerServiceArea(activeBroker)
            : IsAtPosition(currentSellTarget);

        if (!arrivedToSell)
        {
            return;
        }

        ClearMovementTargets();
        StopMoving();

        if (TrySellGoodsToTrader() ||
            (!sellOnlyToTrader && TrySellGoodsToMarketTrader()))
        {
            hasSellTarget = false;
            currentSellTarget = Vector3.zero;
            currentSellTargetZone = null;
            resolvedSellLocationZone = null;
            actionTimer = sellGoodsDuration;
            currentAction = NpcText.Action("soldGoods");
            return;
        }

        actionTimer = sellGoodsDuration;
        currentAction = NpcText.Action("waitTraderBuyGoods");
    }

    void GoBuyGoods()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (!hasBuyTarget)
        {
            resolvedBuyLocationZone = null;
            currentBuyTarget = GetBuyGoodsTarget();
            currentBuyTargetZone = GetBuyGoodsTargetZone();
            hasBuyTarget = true;
        }

        MoveUsingRoad(currentBuyTarget, currentBuyTargetZone);
        currentAction = NpcText.Action("goBuyGoods");

        NpcCounterBroker activeBroker = NpcCounterBroker.Active;
        bool arrivedToBuy = activeBroker != null && activeBroker.receiveAllNpcRequests
            ? IsInsideBrokerServiceArea(activeBroker)
            : IsAtPosition(currentBuyTarget);

        if (!arrivedToBuy)
        {
            return;
        }

        ClearMovementTargets();
        StopMoving();

        if (TryBuyGoodsFromTrader())
        {
            hasBuyTarget = false;
            currentBuyTarget = Vector3.zero;
            currentBuyTargetZone = null;
            resolvedBuyLocationZone = null;
            actionTimer = tradeDuration;
            currentAction = NpcText.Action("boughtGoods");
            return;
        }

        actionTimer = tradeDuration;
        currentAction = NpcText.Action("waitTraderBuyGoods");
    }

    Vector3 GetMarketPosition(Vector3 fallback)
    {
        WorldTilemapManager worldTilemap =
            WorldTilemapManager.Instance;

        if (worldTilemap == null)
        {
            return fallback;
        }

        Vector3 market =
            worldTilemap.GetMarketTile();

        return market != Vector3.zero
            ? market
            : fallback;
    }

    Vector3 GetTraderWorkPosition()
    {
        if (job != VillagerJob.Trader)
        {
            return GetFallbackActivityPosition();
        }

        Transform nearbyMarketTrader = FindNearbyMarketTrader();
        if (nearbyMarketTrader != null)
        {
            resolvedTraderLocationZone =
                NpcMapNavigator.GetDestinationZone(nearbyMarketTrader);
            return nearbyMarketTrader.position;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null && broker.receiveAllNpcRequests && ShouldVisitCounterBroker())
        {
            return broker.GetCustomerPositionFor(gameObject);
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.BuyGoods,
                job,
                NpcLocationPurpose.Market,
                transform.position,
                out Vector3 registryMarket,
                out resolvedTraderLocationZone))
        {
            return registryMarket;
        }

        if (marketPoint != null)
        {
            resolvedTraderLocationZone =
                NpcMapNavigator.GetDestinationZone(marketPoint);
            return marketPoint.position;
        }

        if (workPoint != null)
        {
            resolvedTraderLocationZone =
                NpcMapNavigator.GetDestinationZone(workPoint);
            return workPoint.position;
        }

        resolvedTraderLocationZone = null;
        return GetMarketPosition(GetFallbackActivityPosition());
    }

    NpcMapZone? GetTraderWorkTargetZone()
    {
        if (job != VillagerJob.Trader)
        {
            return null;
        }

        Transform nearbyMarketTrader = FindNearbyMarketTrader();
        if (nearbyMarketTrader != null)
        {
            NpcMapZone? traderZone =
                NpcMapNavigator.GetDestinationZone(nearbyMarketTrader);
            if (traderZone.HasValue)
            {
                return traderZone;
            }
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null && broker.receiveAllNpcRequests && ShouldVisitCounterBroker())
        {
            NpcMapZone? brokerZone = GetTargetZone(broker.transform);
            return brokerZone.HasValue ? brokerZone : NpcMapZone.Lang;
        }

        if (resolvedTraderLocationZone.HasValue)
        {
            return resolvedTraderLocationZone;
        }

        NpcMapZone? marketZone = NpcMapNavigator.GetDestinationZone(marketPoint);
        if (marketZone.HasValue)
        {
            return marketZone;
        }

        NpcMapZone? workZone = NpcMapNavigator.GetDestinationZone(workPoint);
        return workZone.HasValue ? workZone : NpcMapZone.Lang;
    }

    Vector3 GetSellGoodsTarget()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyMyGoods(broker))
        {
            resolvedSellLocationZone =
                GetTargetZone(broker.transform);
            return broker.GetCustomerPositionFor(gameObject);
        }

        Transform nearbyMarketTrader = FindNearbyMarketTrader();
        if (nearbyMarketTrader != null)
        {
            resolvedSellLocationZone =
                NpcMapNavigator.GetDestinationZone(nearbyMarketTrader);
            return nearbyMarketTrader.position;
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.SellGoods,
                job,
                NpcLocationPurpose.SellGoods,
                transform.position,
                out Vector3 registrySell,
                out resolvedSellLocationZone))
        {
            return registrySell;
        }

        resolvedSellLocationZone = null;
        return GetMarketPosition(
            marketPoint != null
            ? marketPoint.position
            : GetFallbackActivityPosition());
    }

    Vector3 GetBuyGoodsTarget()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyUsefulGoods(broker))
        {
            resolvedBuyLocationZone =
                GetTargetZone(broker.transform);
            return broker.GetCustomerPositionFor(gameObject);
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.BuyGoods,
                job,
                NpcLocationPurpose.BuyGoods,
                transform.position,
                out Vector3 registryBuy,
                out resolvedBuyLocationZone))
        {
            return registryBuy;
        }

        resolvedBuyLocationZone = null;
        return GetMarketPosition(
            marketPoint != null
            ? marketPoint.position
            : GetFallbackActivityPosition());
    }

    NpcMapZone? GetSellGoodsTargetZone()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyMyGoods(broker))
        {
            NpcMapZone? brokerZone = GetTargetZone(broker.transform);

            return brokerZone.HasValue
                ? brokerZone
                : NpcMapZone.Lang;
        }

        Transform nearbyMarketTrader = FindNearbyMarketTrader();
        if (nearbyMarketTrader != null)
        {
            NpcMapZone? traderZone =
                NpcMapNavigator.GetDestinationZone(nearbyMarketTrader);
            if (traderZone.HasValue)
            {
                return traderZone;
            }
        }

        if (resolvedSellLocationZone.HasValue)
        {
            return resolvedSellLocationZone;
        }

        return NpcMapNavigator.GetDestinationZone(marketPoint);
    }

    NpcMapZone? GetBuyGoodsTargetZone()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyUsefulGoods(broker))
        {
            NpcMapZone? brokerZone = GetTargetZone(broker.transform);
            return brokerZone.HasValue
                ? brokerZone
                : NpcMapZone.Lang;
        }

        if (resolvedBuyLocationZone.HasValue)
        {
            return resolvedBuyLocationZone;
        }

        return NpcMapNavigator.GetDestinationZone(marketPoint);
    }

    bool BrokerCanBuyMyGoods(NpcCounterBroker broker)
    {
        return broker != null &&
            inventory != null &&
            broker.CanBuyProduceFrom(this, inventory);
    }

    bool BrokerCanSellUsefulGoods(NpcCounterBroker broker)
    {
        return broker != null &&
            inventory != null &&
            broker.CanSellUsefulItemTo(this);
    }

    bool BrokerCanBuyUsefulGoods(NpcCounterBroker broker)
    {
        return BrokerCanSellUsefulGoods(broker);
    }

    Transform FindNearbyMarketTrader()
    {
        if (job != VillagerJob.Trader)
        {
            return null;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                sellGoodsSearchRadius,
                traderLayers);

        Transform best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            NpcTradeAgent trader =
                hit.GetComponentInParent<NpcTradeAgent>();

            if (trader == null ||
                !trader.IsMarketTrader)
            {
                continue;
            }

            float distance = Vector2.Distance(
                transform.position,
                trader.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = trader.transform;
            }
        }

        return best;
    }

    bool ShouldVisitCounterBroker()
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null || !broker.receiveAllNpcRequests)
        {
            return false;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            return false;
        }

        if (HasSellableGoods() && BrokerCanBuyMyGoods(broker))
        {
            return true;
        }

        return CanAffordUsefulCounterPurchase(broker);
    }

    bool CanAffordUsefulCounterPurchase(NpcCounterBroker broker)
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        if (broker == null || broker.inventory == null)
        {
            return false;
        }

        NpcTradeAgent tradeAgent = GetComponent<NpcTradeAgent>();
        if (tradeAgent == null || !tradeAgent.CanUseCounterTrade())
        {
            return false;
        }

        foreach (ItemStack stack in broker.inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.CanUseOn(gameObject))
            {
                continue;
            }

            int price = NpcEconomy.GetNpcBuyPrice(
                stack.item,
                gameObject,
                NpcTradeContext.CounterBrokerBuy);

            if (tradeAgent.GetBuyScore(stack.item, price) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    bool HasCounterTradeOpportunity()
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null || !broker.receiveAllNpcRequests)
        {
            return false;
        }

        NpcTradeAgent tradeAgent = GetComponent<NpcTradeAgent>();
        return tradeAgent != null && broker.CanTradeWithNpc(tradeAgent);
    }

    bool IsInsideBrokerServiceArea(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return false;
        }

        return Vector2.Distance(
            transform.position,
            broker.GetCustomerPositionFor(gameObject)) <=
            Mathf.Max(arriveDistance, broker.customerArriveDistance);
    }

    bool IsNearTaskProvider(NpcTaskProvider provider)
    {
        if (provider == null)
        {
            return false;
        }

        float interactionDistance =
            Mathf.Max(
                arriveDistance,
                provider.providerTalkDistance);

        return Vector2.Distance(
            transform.position,
            provider.GetProviderPositionFor(gameObject)) <= interactionDistance;
    }
}
