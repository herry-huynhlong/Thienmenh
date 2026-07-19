using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class VillagerMarketRole : MonoBehaviour
{
    VillagerAI villager;
    NpcTradeAgent tradeAgent;
    ItemInventory inventory;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
        EnsureMarketTraderSetup();
    }

    public bool TryHandleAdultThink()
    {
        if (villager == null)
        {
            return false;
        }

        if (villager.IsDead ||
            villager.IsReturningHome ||
            villager.ShouldGoHomeForRest())
        {
            return false;
        }

        return RunMarketStallDuty();
    }

    public bool TryRunScheduledActivity(NpcScheduleActivity activity)
    {
        if (villager == null)
        {
            return false;
        }

        switch (activity)
        {
            case NpcScheduleActivity.Work:
            case NpcScheduleActivity.TradeBuySell:
            case NpcScheduleActivity.BuyGoods:
            case NpcScheduleActivity.SellGoods:
                RunMarketStallDuty();
                return true;

            case NpcScheduleActivity.TakeTask:
            case NpcScheduleActivity.DoMission:
                villager.RunMarketRoleTaskProviderVisit();
                return true;
        }

        return false;
    }

    bool RunMarketStallDuty()
    {
        EnsureMarketTraderSetup();

        Vector3 stallTarget = ResolveMarketStallPosition();
        NpcMapZone? stallZone = ResolveMarketStallZone();

        if (Vector2.Distance(villager.transform.position, stallTarget) >
            Mathf.Max(villager.arriveDistance, 0.45f))
        {
            villager.ForceJobMoveTo(
                stallTarget,
                NpcText.Action("goMarketTrade"),
                stallZone);
            return true;
        }

        villager.StopMoving();
        villager.SetActionImmediate(
            NpcText.Action("trading"),
            0.35f);
        return true;
    }

    void EnsureMarketTraderSetup()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
            if (inventory == null)
            {
                inventory = gameObject.AddComponent<ItemInventory>();
            }
        }

        if (tradeAgent == null)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
            if (tradeAgent == null)
            {
                tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
            }
        }

        tradeAgent.inventory = inventory;
        tradeAgent.isMarketTrader = true;
        tradeAgent.buyProduceFromVillagers = true;
        tradeAgent.buyUsefulItemsFromMarketTrader = false;
    }

    Vector3 ResolveMarketStallPosition()
    {
        if (villager.marketPoint != null)
        {
            return villager.marketPoint.position;
        }

        if (villager.workPoint != null)
        {
            return villager.workPoint.position;
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.Work,
                VillagerJob.Trader,
                NpcLocationPurpose.Market,
                transform.position,
                out Vector3 registryMarket,
                out _))
        {
            return registryMarket;
        }

        return transform.position;
    }

    NpcMapZone? ResolveMarketStallZone()
    {
        if (villager.marketPoint != null)
        {
            NpcMapZone? marketZone =
                NpcMapNavigator.GetDestinationZone(
                    villager.marketPoint);
            if (marketZone.HasValue)
            {
                return marketZone;
            }
        }

        if (villager.workPoint != null)
        {
            NpcMapZone? workZone =
                NpcMapNavigator.GetDestinationZone(
                    villager.workPoint);
            if (workZone.HasValue)
            {
                return workZone;
            }
        }

        return NpcMapNavigator.ResolveActorZone(gameObject);
    }
}
