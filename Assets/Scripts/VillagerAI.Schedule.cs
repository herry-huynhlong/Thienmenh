using UnityEngine;

public partial class VillagerAI
{
    bool TryRunScheduledActivity()
    {
        if (ageGroup == VillagerAgeGroup.Child)
        {
            return false;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        NpcScheduleActivity activity = schedule.CurrentActivity;
        ResetScheduledStateIfSlotChanged(schedule, slot, activity);

        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        bool shouldHoldBlacksmithTradeRoute =
            fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.ShouldKeepTradeRouteActive();
        bool shouldHoldAlchemistTradeRoute =
            fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.ShouldKeepTradeRouteActive();

        if (slot == null)
        {
            return false;
        }

        if (slot.allowFatigueInterrupt && fatigue >= 85f)
        {
            GoHomeToRest();
            return true;
        }

        switch (activity)
        {
            case NpcScheduleActivity.Sleep:
            case NpcScheduleActivity.Eat:
            case NpcScheduleActivity.ReturnHome:
                if (shouldHoldBlacksmithTradeRoute &&
                    IsForgeWorker())
                {
                    GoForgeWorkOrTrade();
                    return true;
                }

                if (shouldHoldAlchemistTradeRoute &&
                    IsAlchemyWorker())
                {
                    GoAlchemyWorkOrTrade();
                    return true;
                }

                GoHomeToRest();
                return true;

            case NpcScheduleActivity.BuyGoods:
            case NpcScheduleActivity.SellGoods:
            case NpcScheduleActivity.TradeBuySell:
                if (IsForgeWorker())
                {
                    GoForgeWorkOrTrade();
                }
                else if (IsAlchemyWorker())
                {
                    GoAlchemyWorkOrTrade();
                }
                else if (job == VillagerJob.Trader)
                {
                    GoTrade();
                }
                else if (activity == NpcScheduleActivity.BuyGoods)
                {
                    GoBuyGoods();
                }
                else if (activity == NpcScheduleActivity.SellGoods)
                {
                    GoSellGoods();
                }
                else
                {
                    GoTrade();
                }
                return true;

            case NpcScheduleActivity.Work:
                if (IsAlchemyWorker())
                {
                    GoAlchemyWorkOrTrade();
                }
                else if (IsForgeWorker())
                {
                    GoForgeWorkOrTrade();
                }
                else if (job == VillagerJob.Trader)
                {
                    GoTrade();
                }
                else if (!autonomousWorkEnabled)
                {
                    Wander(NpcText.Action("wanderVillage"));
                }
                else
                {
                    GoWork();
                }
                return true;

            default:
                Wander(NpcText.Action("wanderVillage"));
                return true;
        }
    }

    void RefreshScheduledStateForCurrentFrame()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        if (slot == null)
        {
            return;
        }

        ResetScheduledStateIfSlotChanged(
            schedule,
            slot,
            schedule.CurrentActivity);
    }

    void ResetScheduledStateIfSlotChanged(
        NpcScheduleController schedule,
        NpcScheduleSlot slot,
        NpcScheduleActivity activity)
    {
        string key = BuildScheduleSlotKey(slot, activity);
        if (currentScheduleSlotKey == key)
        {
            return;
        }

        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        if (fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.ShouldKeepTradeRouteActive())
        {
            currentScheduleSlotKey = key;
            return;
        }

        if (fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.ShouldKeepTradeRouteActive())
        {
            currentScheduleSlotKey = key;
            return;
        }

        currentScheduleSlotKey = key;
        ClearMovementTargets();
        StopMoving();
        ClearTreasureHunt();
        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        actionTimer = 0f;
        currentAction = string.Empty;

        hasWorkTarget = false;
        currentWorkTarget = Vector3.zero;
        currentWorkTargetZone = null;
        currentWorkTargetKey = string.Empty;
        hasTradeTarget = false;
        currentTradeTarget = Vector3.zero;
        currentTradeTargetZone = null;
        hasEatTarget = false;
        currentEatTarget = Vector3.zero;
        hasBuyTarget = false;
        currentBuyTarget = Vector3.zero;
        currentBuyTargetZone = null;
        hasSellTarget = false;
        currentSellTarget = Vector3.zero;
        currentSellTargetZone = null;
        resolvedTraderLocationZone = null;
        resolvedBuyLocationZone = null;
        resolvedSellLocationZone = null;

        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer != null)
        {
            gatherer.CancelGatheringNow();
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null)
        {
            harvestJob.CancelHarvestNow();
        }

        HunterJob hunterJob = GetComponent<HunterJob>();
        if (hunterJob != null)
        {
            hunterJob.CancelHunterNow();
        }
    }

    bool ShouldForceReturnHomeForCurrentSchedule()
    {
        return ShouldGoHomeForRest();
    }

    bool ShouldForceReturnHomeFromSchedule()
    {
        return ShouldForceReturnHomeForCurrentSchedule();
    }

    string BuildScheduleSlotKey(
        NpcScheduleSlot slot,
        NpcScheduleActivity activity)
    {
        return NpcScheduleController.GetStableSlotKey(slot, activity);
    }

    bool HasEnforcedSchedule()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentSlot != null;
    }

    bool IsTaskWindowActive()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == NpcScheduleActivity.TakeTask;
    }
}
