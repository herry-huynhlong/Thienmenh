using System.Collections.Generic;
using UnityEngine;

public partial class VillagerAI
{
    bool TryProcessDailyTaskPlan()
    {
        return false;
    }

    bool CanRunDailyTaskPlan()
    {
        return false;
    }

    void EnsureDailyTaskPlan()
    {
        int currentDay = GetCurrentWorldDay();
        if (lastDailyTaskPlanDay == currentDay)
        {
            return;
        }

        lastDailyTaskPlanDay = currentDay;
        dailyTaskPlanIndex = 0;
        dailyTaskPlan.Clear();
        dailyTaskNeeds.Clear();

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider == null)
        {
            return;
        }

        List<NpcTaskOffer> offers =
            provider.PickDailyOffersFor(
                gameObject,
                Mathf.Max(1, dailyTaskPlanMinTasks),
                Mathf.Max(dailyTaskPlanMinTasks, dailyTaskPlanMaxTasks));

        dailyTaskPlan.AddRange(offers);

        for (int i = 0; i < dailyTaskPlan.Count; i++)
        {
            NpcTaskOffer offer =
                dailyTaskPlan[i];

            if (offer == null ||
                offer.taskType == NpcTaskType.GatherResource)
            {
                continue;
            }

            StatItemData item =
                provider.GetPlannedRequiredItem(offer);

            int amount =
                provider.GetPlannedRequiredAmount(offer);

            if (item != null &&
                amount > 0)
            {
                AddDailyTaskNeed(item, amount);
            }
        }
    }

    void AddDailyTaskNeed(StatItemData item, int amount)
    {
        foreach (DailyTaskNeed need in dailyTaskNeeds)
        {
            if (need.item == item)
            {
                need.amount += amount;
                return;
            }
        }

        dailyTaskNeeds.Add(
            new DailyTaskNeed
            {
                item = item,
                amount = amount
            });
    }

    bool TryBuyDailyTaskNeeds()
    {
        DailyTaskNeed missingNeed =
            GetFirstMissingDailyTaskNeed();

        if (missingNeed == null)
        {
            return false;
        }

        NpcCounterBroker broker =
            NpcCounterBroker.Active;

        if (broker == null)
        {
            currentAction = NpcText.Action("missingTaskItems");
            return false;
        }

        Vector3 brokerPosition =
            broker.GetCustomerPositionFor(gameObject);

        int missingAmount =
            GetMissingDailyTaskItemAmount(missingNeed);

        if (missingAmount <= 0)
        {
            return false;
        }

        int requiredMoney =
            GetDailyTaskNeedBuyCost(
                broker,
                missingNeed.item,
                missingAmount);

        string missingItemName = ItemText.Name(missingNeed.item);
        currentAction =
            NpcText.ActionFormat("requestBuyTaskItem", missingItemName);

        if (requiredMoney > 0 &&
            NpcEconomy.GetNpcMoney(gameObject) < requiredMoney)
        {
            currentAction = NpcText.Action("notEnoughSpiritStoneWorkTask");
            return false;
        }

        currentAction =
            NpcText.ActionFormat("goStoreBuyItem", missingItemName);

        if (!IsInsideBrokerServiceArea(broker))
        {
            MoveUsingRoad(
                brokerPosition,
                GetTargetZone(broker.transform) ??
                NpcMapZone.Lang);
            return true;
        }

        NpcTradeAgent tradeAgent =
            GetComponent<NpcTradeAgent>();

        if (tradeAgent == null)
        {
            tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
        }

        if (broker.TrySellSpecificItemTo(
                tradeAgent,
                missingNeed.item,
                missingAmount,
                false))
        {
            currentAction =
                NpcText.ActionFormat("boughtTaskItem", missingItemName);

            if (GetMissingDailyTaskItemAmount(missingNeed) <= 0)
            {
                dailyTaskNeeds.Remove(missingNeed);
            }

            return GetFirstMissingDailyTaskNeed() != null;
        }

        dailyTaskNeeds.Remove(missingNeed);
        currentAction =
            NpcText.ActionFormat("storeMissingItem", missingItemName);
        return GetFirstMissingDailyTaskNeed() != null;
    }

    int GetDailyTaskNeedBuyCost(
        NpcCounterBroker broker,
        StatItemData item,
        int amount)
    {
        if (broker == null ||
            item == null ||
            amount <= 0)
        {
            return 0;
        }

        int unitPrice =
            NpcEconomy.GetNpcBuyPrice(
                item,
                gameObject,
                broker.sellToNpcContext);

        return Mathf.Max(0, unitPrice) * amount;
    }

    DailyTaskNeed GetFirstMissingDailyTaskNeed()
    {
        foreach (DailyTaskNeed need in dailyTaskNeeds)
        {
            if (GetMissingDailyTaskItemAmount(need) > 0)
            {
                return need;
            }
        }

        return null;
    }

    int GetMissingDailyTaskItemAmount(DailyTaskNeed need)
    {
        if (need == null ||
            need.item == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            need.amount - GetInventoryItemAmount(need.item));
    }

    int GetInventoryItemAmount(StatItemData item)
    {
        ItemInventory itemInventory =
            inventory != null
            ? inventory
            : GetComponent<ItemInventory>();

        int amount = itemInventory != null
            ? itemInventory.GetAmount(item)
            : 0;

        NpcTradeAgent tradeAgent =
            GetComponent<NpcTradeAgent>();

        if (tradeAgent != null &&
            tradeAgent.inventory != null &&
            tradeAgent.inventory != itemInventory)
        {
            amount += tradeAgent.inventory.GetAmount(item);
        }

        NpcItemCollector collector =
            GetComponent<NpcItemCollector>();

        if (collector != null &&
            collector.inventory != null &&
            collector.inventory != itemInventory &&
            (tradeAgent == null ||
            collector.inventory != tradeAgent.inventory))
        {
            amount += collector.inventory.GetAmount(item);
        }

        return amount;
    }

    bool TryStartNextDailyTask()
    {
        if (dailyTaskPlanIndex >= dailyTaskPlan.Count)
        {
            return false;
        }

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider == null)
        {
            return false;
        }

        Vector3 providerPosition =
            provider.GetProviderPositionFor(gameObject);

        NpcMapZone? providerZone =
            GetTargetZone(provider.transform);

        currentAction = NpcText.Action("goTaskProviderDaily");

        if (!IsNearTaskProvider(provider))
        {
            MoveUsingRoad(
                providerPosition,
                providerZone.HasValue ? providerZone : NpcMapZone.Lang);
            return true;
        }

        NpcTaskOffer offer =
            dailyTaskPlan[dailyTaskPlanIndex];

        if (provider.TryStartPlannedTask(gameObject, offer))
        {
            dailyTaskPlanIndex++;
            return true;
        }

        dailyTaskPlanIndex++;
        actionTimer = Mathf.Max(1f, thinkInterval);
        currentAction = NpcText.Action("skipUnavailableTask");
        return true;
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem =
            WorldTimeSystem.Instance;

        return timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.Max(1, lastDailyTaskPlanDay + 1);
    }
}
