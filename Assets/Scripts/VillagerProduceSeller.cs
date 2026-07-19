using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class VillagerProduceSeller : MonoBehaviour
{
    VillagerAI villager;
    VillagerDailyTradePlan tradePlan;
    NpcTradeAgent activeTrader;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
        tradePlan = GetComponent<VillagerDailyTradePlan>();
    }

    public bool TryRun()
    {
        if (!CanRunJob() ||
            !villager.HasProducedGoodsForSale() ||
            villager.inventory == null)
        {
            activeTrader = null;
            return false;
        }

        NpcTradeAgent trader = ResolveActiveTrader();
        if (trader == null)
        {
            activeTrader = null;
            return false;
        }

        Vector3 standTarget = ResolveTraderStandPosition(trader);
        NpcMapZone? traderZone = ResolveTraderZone(trader);
        float distance = Vector2.Distance(
            transform.position,
            standTarget);
        float arriveDistance = ResolveTraderArriveDistance(trader);

        if (distance > arriveDistance)
        {
            villager.ForceJobMoveTo(
                standTarget,
                NpcText.Action("bringGoodsToCounter"),
                traderZone);
            return true;
        }

        villager.StopMoving();
        if (TrySellToTrader(trader))
        {
            villager.SetActionImmediate(
                NpcText.Action("soldGoods"),
                0.5f);
            activeTrader = null;
            return true;
        }

        villager.SetActionImmediate(
            NpcText.Action("waitTraderBuyGoods"),
            0.5f);
        return true;
    }

    bool CanRunJob()
    {
        if (tradePlan == null)
        {
            tradePlan = GetComponent<VillagerDailyTradePlan>();
        }

        if (tradePlan != null &&
            !tradePlan.ShouldParticipateToday())
        {
            return false;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity != NpcScheduleActivity.TradeBuySell &&
            schedule.CurrentActivity != NpcScheduleActivity.SellGoods)
        {
            return false;
        }

        return villager != null &&
            villager.enabled &&
            !villager.IsDead &&
            !villager.IsReturningHome &&
            villager.job != VillagerJob.Trader &&
            !villager.ShouldGoHomeForRest();
    }

    NpcTradeAgent ResolveActiveTrader()
    {
        if (IsTraderUsable(activeTrader))
        {
            return activeTrader;
        }

        activeTrader = FindNearestMarketTrader();
        return activeTrader;
    }

    bool IsTraderUsable(NpcTradeAgent trader)
    {
        return trader != null &&
            trader.IsMarketTrader &&
            trader.gameObject != gameObject &&
            !NpcRoleUtility.IsDead(trader.gameObject) &&
            !NpcRoleUtility.IsInCombat(trader.gameObject);
    }

    NpcTradeAgent FindNearestMarketTrader()
    {
        NpcTradeAgent[] traders =
            FindObjectsByType<NpcTradeAgent>(
                FindObjectsInactive.Exclude);

        NpcTradeAgent best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < traders.Length; i++)
        {
            NpcTradeAgent trader = traders[i];
            if (trader == null ||
                !IsTraderUsable(trader))
            {
                continue;
            }

            float distance = Vector2.Distance(
                transform.position,
                trader.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = trader;
            }
        }

        return best;
    }

    Vector3 ResolveTraderStandPosition(NpcTradeAgent trader)
    {
        if (trader == null)
        {
            return transform.position;
        }

        NpcCounterBroker broker = FindTraderCounterBroker(trader);
        if (broker != null &&
            broker.receiveAllNpcRequests)
        {
            return broker.GetCustomerPositionFor(gameObject);
        }

        VillagerAI traderVillager =
            trader.GetComponent<VillagerAI>();
        if (traderVillager != null &&
            traderVillager.marketPoint != null)
        {
            NpcInteractionPoint interactionPoint =
                traderVillager.marketPoint.GetComponent<NpcInteractionPoint>();
            if (interactionPoint != null)
            {
                return interactionPoint.GetStandPositionFor(gameObject);
            }

            return traderVillager.marketPoint.position;
        }

        return trader.transform.position;
    }

    NpcMapZone? ResolveTraderZone(NpcTradeAgent trader)
    {
        if (trader == null)
        {
            return null;
        }

        NpcCounterBroker broker = FindTraderCounterBroker(trader);
        if (broker != null &&
            broker.customerPoint != null)
        {
            NpcMapZone? brokerZone =
                NpcMapNavigator.GetDestinationZone(
                    broker.customerPoint);
            if (brokerZone.HasValue)
            {
                return brokerZone;
            }
        }

        VillagerAI traderVillager =
            trader.GetComponent<VillagerAI>();
        if (traderVillager != null &&
            traderVillager.marketPoint != null)
        {
            NpcMapZone? marketZone =
                NpcMapNavigator.GetDestinationZone(
                    traderVillager.marketPoint);
            if (marketZone.HasValue)
            {
                return marketZone;
            }
        }

        return NpcMapNavigator.GetDestinationZone(trader.transform);
    }

    float ResolveTraderArriveDistance(NpcTradeAgent trader)
    {
        float arriveDistance =
            Mathf.Max(
                villager != null ? villager.arriveDistance : 0.45f,
                0.65f);
        NpcCounterBroker broker = FindTraderCounterBroker(trader);
        if (broker != null &&
            broker.receiveAllNpcRequests)
        {
            return Mathf.Max(
                arriveDistance,
                broker.customerArriveDistance,
                broker.CustomerServiceRadius * 0.6f);
        }

        return arriveDistance;
    }

    bool TrySellToTrader(NpcTradeAgent trader)
    {
        if (trader == null)
        {
            return false;
        }

        NpcCounterBroker broker = FindTraderCounterBroker(trader);
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            broker.buyGoodsFromNpcs &&
            broker.IsCustomerAtCounter(gameObject))
        {
            return broker.TryBuyProduceFrom(villager, villager.inventory);
        }

        return trader.TryBuyProduceFrom(villager, villager.inventory);
    }

    NpcCounterBroker FindTraderCounterBroker(NpcTradeAgent trader)
    {
        if (trader == null)
        {
            return null;
        }

        NpcCounterBroker broker =
            trader.GetComponent<NpcCounterBroker>();
        if (broker != null)
        {
            return broker;
        }

        broker = trader.GetComponentInParent<NpcCounterBroker>();
        if (broker != null)
        {
            return broker;
        }

        broker = trader.GetComponentInChildren<NpcCounterBroker>(true);
        if (broker != null)
        {
            return broker;
        }

        return NpcCounterBroker.Active != null &&
            NpcCounterBroker.Active.receiveAllNpcRequests
                ? NpcCounterBroker.Active
                : null;
    }
}
