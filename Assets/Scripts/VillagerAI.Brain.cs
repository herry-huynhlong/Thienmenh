using UnityEngine;

public partial class VillagerAI
{
    void Think()
    {
        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        bool useDedicatedBlacksmithRoutine =
            fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.UseDedicatedRoutine;
        bool useDedicatedAlchemistRoutine =
            fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.UseDedicatedRoutine;

        if (WorldTimeSystem.Instance != null)
        {
            if (WorldTimeSystem.Instance.CurrentDay != lastPlanResetDay)
            {
                lastPlanResetDay = WorldTimeSystem.Instance.CurrentDay;
                ResetDailyTargets();
            }
        }

        if (homeRoutineManagedExternally &&
            !HasEnforcedSchedule() &&
            (WorldTimeSystem.Instance == null ||
            WorldTimeSystem.Instance.CurrentPhase == WorldTimePhase.Night ||
            fatigue >= 85f))
        {
            return;
        }

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        if (ShouldDieFromOldAge())
        {
            currentAction = NpcText.Action("oldAgeDeath");
            Die();
            return;
        }

        if (actionTimer > 0f &&
            NpcRoleUtility.IsInCombat(gameObject))
        {
            return;
        }

        if (useDedicatedBlacksmithRoutine &&
            fixedBlacksmith.TryRunDedicatedRoutine())
        {
            return;
        }

        if (useDedicatedAlchemistRoutine &&
            fixedAlchemist.TryRunDedicatedRoutine())
        {
            return;
        }

        if (ShouldForceReturnHomeFromSchedule())
        {
            GoHomeToRest();
            return;
        }

        if (TryRunScheduledActivity())
        {
            return;
        }

        if (actionTimer > 0f)
        {
            return;
        }

        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return;
        }

        if (ageGroup == VillagerAgeGroup.Child)
        {
            ThinkChild();
            return;
        }

        ThinkAdult();
    }

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
                GoHomeToRest();
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

    bool TryScheduledGather()
    {
        if (job == VillagerJob.Hunter)
        {
            HunterJob hunterJob = GetComponent<HunterJob>();
            if (hunterJob == null)
            {
                hunterJob = gameObject.AddComponent<HunterJob>();
            }

            if (hunterJob.TryRun())
            {
                return true;
            }
        }

        if (job == VillagerJob.Farmer ||
            job == VillagerJob.Fisher)
        {
            EnsureWorkGatherer();

            HarvestJob harvestJob = EnsureHarvestJob();
            if (harvestJob != null && harvestJob.TryRun())
            {
                return true;
            }
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        NpcResourceGatherer gatherer = EnsureWorkGatherer();
        if (gatherer != null &&
            gatherer.enabled &&
            gatherer.canGather)
        {
            if (gatherer.TryStartGatheringNow())
            {
                return true;
            }
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.Gather,
                job,
                NpcLocationPurpose.Resource,
                transform.position,
                out Vector3 resourcePosition,
                out NpcMapZone? resourceZone))
        {
            currentAction = NpcText.Action("gatherResource");
            MoveUsingRoad(resourcePosition, resourceZone);
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    NpcScheduleActivity.Gather);
            }
            return true;
        }

        return false;
    }

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

    void ThinkChild()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null &&
            (timeSystem.CurrentPhase == WorldTimePhase.Night ||
            timeSystem.CurrentPhase == WorldTimePhase.Dawn))
        {
            GoHomeToRest();
            return;
        }

        if (fun <= 70f)
        {
            GatherAndPlay();
            return;
        }

        GoHomeIdle(NpcText.Action("stayNearHome"));
    }

    void ThinkAdult()
    {
        if (ShouldGoHomeForRest())
        {
            GoHomeToRest();
            return;
        }

        if (!hiddenAtHome &&
            !isReturningHome &&
            IsAtHomePosition(GetHomePosition()) &&
            IsCurrentScheduleActivity(NpcScheduleActivity.Work))
        {
            actionTimer = 0f;
            currentAction = string.Empty;
        }

        if (TryHandleAdultImmediateNeeds())
        {
            return;
        }

        if (dailyRoutineEnabled &&
            IsCultivationCapableVillager() &&
            IsScheduledCultivationTime())
        {
            CultivateNaturally();
            return;
        }

        VillagerJobDispatcher jobDispatcher = EnsureJobDispatcher();
        if (jobDispatcher != null &&
            jobDispatcher.TryHandleAdultThink())
        {
            return;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            switch (timeSystem.CurrentPhase)
            {
                case WorldTimePhase.Dawn:
                    if (fatigue > 35f)
                    {
                        GoHomeToRest();
                        return;
                    }
                    GoWork();
                    return;

                case WorldTimePhase.Morning:
                    GoWork();
                    return;

                case WorldTimePhase.Noon:
                    GoHomeToRest();
                    return;

                case WorldTimePhase.Afternoon:
                    GoWork();
                    return;

                case WorldTimePhase.Evening:
                    if (playPoint != null)
                    {
                        GatherAndPlay();
                        return;
                    }

                    IdleOrGoHome(NpcText.Action("eveningWalkVillage"));
                    return;

                case WorldTimePhase.Night:
                    GoHomeToRest();
                    return;
            }
        }

        if (!autonomousWorkEnabled)
        {
            IdleOrGoHome(NpcText.Action("wanderVillage"));
            return;
        }

        if (ShouldDoMortalWork())
        {
            GoWork();
            return;
        }
    }

    bool ShouldDoMortalWork()
    {
        return true;
    }

    bool IsCurrentScheduleActivity(NpcScheduleActivity activity)
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == activity;
    }

    void GoResourceWork()
    {
        NpcMapZone preferredZone = GetPreferredResourceGatherZone();

        if (ShouldSeekForestResources())
        {
            GoToResourcePoint(
                WorldTilemapManager.Instance != null
                ? WorldTilemapManager.Instance.GetHuntingTile(preferredZone)
                : Vector3.zero,
                NpcText.Action("huntForestResource"),
                preferredZone);
            return;
        }

        GoToResourcePoint(
            workPoint != null
            ? workPoint.position
            : GetFallbackActivityPosition(),
            NpcText.Action("gatherVillageResource"),
            NpcMapZone.Lang);
    }

    bool ShouldSeekForestResources()
    {
        if (job == VillagerJob.Hunter)
        {
            return true;
        }

        return bravery >= 55;
    }

    public NpcMapZone GetPreferredResourceGatherZone()
    {
        if (job == VillagerJob.Fisher)
        {
            return NpcMapZone.Lang;
        }

        if (job == VillagerJob.Hunter)
        {
            HunterJob hunterJob = GetComponent<HunterJob>();
            if (hunterJob != null)
            {
                if (hunterJob.huntPoint != null)
                {
                    NpcMapZone? huntPointZone =
                        NpcMapNavigator.GetDestinationZone(
                            hunterJob.huntPoint);
                    if (huntPointZone.HasValue)
                    {
                        return huntPointZone.Value;
                    }
                }

                return hunterJob.huntZone;
            }
        }

        if (workPoint != null)
        {
            NpcMapZone? workZone =
                NpcMapNavigator.GetDestinationZone(workPoint);
            if (workZone.HasValue)
            {
                return workZone.Value;
            }
        }

        return ShouldSeekForestResources()
            ? NpcMapZone.MaThuSonMach
            : NpcMapZone.Lang;
    }

    void GoToResourcePoint(
        Vector3 target,
        string action,
        NpcMapZone? targetZone = null)
    {
        if (target == Vector3.zero)
        {
            target = GetFallbackActivityPosition();
        }

        MoveUsingRoad(target, targetZone);
        currentAction = action;

        if (IsAtPosition(target))
        {
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        resourceSessionMinGameHours,
                        resourceSessionMaxGameHours));
            currentAction = NpcText.Action("harvestResource");
        }
    }

    bool TryHandleAdultImmediateNeeds()
    {
        if (ShouldGoHomeForRest())
        {
            GoHomeToRest();
            return true;
        }

        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return true;
        }

        if (IsRoutineTravelOrCultivationAction(currentAction))
        {
            return true;
        }

        if (TryProcessDailyTaskPlan())
        {
            return true;
        }

        return false;
    }

    float GameHoursToSeconds(float gameHours)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float secondsPerDay =
            timeSystem != null
            ? Mathf.Max(1f, timeSystem.realSecondsPerGameDay)
            : 900f;

        return Mathf.Max(0.5f, gameHours * secondsPerDay / 24f);
    }

    void ResetDailyTargets()
    {
        if (WorldTilemapManager.Instance != null)
        {
            WorldTilemapManager.Instance.ReleaseFishingTile(this);
        }

        currentWorkTarget = Vector3.zero;
        currentWorkTargetZone = null;
        currentWorkTargetKey = string.Empty;
        hasWorkTarget = false;
        hasTradeTarget = false;
        currentTradeTarget = Vector3.zero;
        currentTradeTargetZone = null;
        hasBuyTarget = false;
        currentBuyTarget = Vector3.zero;
        currentBuyTargetZone = null;
        hasEatTarget = false;
        hasSellTarget = false;
        currentSellTarget = Vector3.zero;
        currentSellTargetZone = null;
        resolvedTraderLocationZone = null;
        resolvedBuyLocationZone = null;
        resolvedSellLocationZone = null;
        hasRoadPreference = false;
    }
}
