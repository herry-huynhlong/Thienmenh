using UnityEngine;

// Schedule slot state, directed-work compatibility, and world-time helpers.
public partial class SmartNpcAI
{
    void RefreshScheduledStateForCurrentFrame()
    {
        if (!NpcMapBehaviorPolicy.AllowsSchedule(gameObject))
        {
            if (currentSmartTask != null &&
                currentSmartTask.IsValid &&
                !IsMapCombatTaskGoal(currentSmartTask.goal))
            {
                ClearSmartTask();
            }

            if (scheduleSmartTask != null &&
                scheduleSmartTask.IsValid &&
                !IsMapCombatTaskGoal(scheduleSmartTask.goal))
            {
                ClearScheduledTask();
            }

            return;
        }

        NpcScheduleController schedule = GetComponent<NpcScheduleController>();
        if (schedule == null || !schedule.enforceSchedule)
        {
            return;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        if (slot == null)
        {
            return;
        }

        string key = BuildScheduleSlotKey(slot, schedule.CurrentActivity);
        if (currentScheduleSlotKey == key)
        {
            EnsureScheduledTaskForCurrentActivity(schedule.CurrentActivity);
            return;
        }

        DebugFlow(
            "ScheduleSlot",
            "Switch to " + schedule.CurrentActivity +
            " " + slot.startHour.ToString("0.##") +
            "-" + slot.endHour.ToString("0.##"));

        bool preserveActiveFlow =
            (currentSmartTask != null &&
            currentSmartTask.priority > SmartAITaskPriority.Normal) ||
            HasCombatSupportIntent() ||
            IsDirectedWorkCompatibleWithSchedule(schedule.CurrentActivity) ||
            waitingOutsideTreasureLightning ||
            treasureHuntTarget != null ||
            hasTreasureWaitPosition;

        currentScheduleSlotKey = key;
        if (runtimeTraceScheduleChanges)
        {
            TraceRuntime(
                "RefreshScheduledStateForCurrentFrame",
                "switch slot=" + slot.activity +
                " activity=" + schedule.CurrentActivity +
                " preserve=" + preserveActiveFlow +
                " key=" + key);
        }
        if (!preserveActiveFlow)
        {
            ClearTaskProviderVisitState();
            ClearTravelTargetsAndStop();
            currentMonsterTarget = null;
            treasureHuntTarget = null;
            treasureHuntItem = null;
            waitingOutsideTreasureLightning = false;
            hasCultivationTarget = false;
            hasHomeReturnTarget = false;
            actionTimer = 0f;
            currentAction = string.Empty;

            UpdateCultivationEffect(false);

            if (resourceGatherer != null)
            {
                resourceGatherer.CancelGatheringNow();
            }
        }
        else
        {
            DebugFlow("ScheduleSlot", "Preserve active travel flow");
        }

        SmartAITaskGoal scheduledGoal =
            MapScheduleActivityToSmartGoal(schedule.CurrentActivity);
        if (scheduledGoal == SmartAITaskGoal.None)
        {
            ClearScheduledTask();
        }
        else
        {
            RequestScheduledTask(
                scheduledGoal,
                "schedule " + schedule.CurrentActivity);
        }
        if (runtimeTraceScheduleChanges)
        {
            TraceRuntime(
                "RefreshScheduledStateForCurrentFrame",
                "request-goal=" + scheduledGoal);
        }
        DebugFlow(
            "ScheduleGoal",
            "hour=" + GetCurrentWorldHour().ToString("0.00") +
            " activity=" + schedule.CurrentActivity +
            " mappedGoal=" + scheduledGoal);

        if (schedule.CurrentActivity == NpcScheduleActivity.Gather ||
            schedule.CurrentActivity == NpcScheduleActivity.Hunt ||
            schedule.CurrentActivity == NpcScheduleActivity.FreeHuntAndGather)
        {
            schedule.ClearCurrentSlotActivityState(schedule.CurrentActivity);
        }
    }

    void EnsureScheduledTaskForCurrentActivity(
        NpcScheduleActivity activity)
    {
        SmartAITaskGoal scheduledGoal =
            MapScheduleActivityToSmartGoal(activity);
        if (scheduledGoal == SmartAITaskGoal.None)
        {
            return;
        }

        bool missingScheduleTask =
            scheduleSmartTask == null ||
            !scheduleSmartTask.IsValid ||
            scheduleSmartTask.goal != scheduledGoal;
        bool missingCurrentTask =
            currentSmartTask == null ||
            !currentSmartTask.IsValid;

        if (!missingScheduleTask &&
            !missingCurrentTask)
        {
            return;
        }

        RequestScheduledTask(
            scheduledGoal,
            "schedule " + activity);

        if (runtimeTraceScheduleChanges)
        {
            TraceRuntime(
                "EnsureScheduledTaskForCurrentActivity",
                "restored-goal=" + scheduledGoal +
                " activity=" + activity);
        }
    }

    string BuildScheduleSlotKey(NpcScheduleSlot slot, NpcScheduleActivity activity)
    {
        return NpcScheduleController.GetStableSlotKey(slot, activity);
    }

    void ClearTravelTargets(bool clearWanderTarget = true)
    {
        TraceRuntime(
            "ClearTravelTargets",
            "clearWanderTarget=" + clearWanderTarget +
            " beforeTarget=" + (currentTarget != null ? currentTarget.name : "null") +
            " beforeWander=" + hasWanderTarget +
            " wanderTarget=" + wanderTarget +
            " action=" + currentAction);
        currentTarget = null;
        if (clearWanderTarget)
        {
            hasWanderTarget = false;
        }
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        unstuckRecoveryAttempts = 0;
    }

    void StopNpcMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void ClearTravelTargetsAndStop(bool clearWanderTarget = true)
    {
        TraceRuntime(
            "ClearTravelTargetsAndStop",
            "clearWanderTarget=" + clearWanderTarget +
            " target=" + (currentTarget != null ? currentTarget.name : "null") +
            " wander=" + hasWanderTarget +
            " action=" + currentAction);
        ClearTravelTargets(clearWanderTarget);
        StopNpcMovement();
    }

    bool IsDirectedWorkCompatibleWithSchedule(
        NpcScheduleActivity activity)
    {
        switch (activity)
        {
            case NpcScheduleActivity.Cultivate:
                return hasCultivationTarget ||
                    currentAction == NpcText.Action("goCultivatePoint");

            case NpcScheduleActivity.DoMission:
            case NpcScheduleActivity.TakeTask:
                return IsMissionDirectedWorkActive();

            case NpcScheduleActivity.FreeHuntAndGather:
            case NpcScheduleActivity.Hunt:
            case NpcScheduleActivity.Gather:
                return IsHuntOrGatherDirectedWorkActive();

            case NpcScheduleActivity.TradeBuySell:
            case NpcScheduleActivity.BuyGoods:
            case NpcScheduleActivity.SellGoods:
                return IsTradeDirectedWorkActive();

            default:
                return false;
        }
    }

    bool IsMissionDirectedWorkActive()
    {
        return currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("visitedTaskProvider") ||
            currentAction == NpcText.Action("goWorkTask") ||
            currentAction == NpcText.Action("pickHuntEvidence") ||
            currentAction == NpcText.Action("pickItem") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true) ||
            currentMonsterTarget != null ||
            HasDirectedTravelContext() ||
            (resourceGatherer != null &&
            resourceGatherer.HasActiveGatheringFlow);
    }

    bool IsHuntOrGatherDirectedWorkActive()
    {
        return currentAction == NpcText.Action("goHunt") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("pickItem") ||
            currentAction == NpcText.Action("pickHuntEvidence") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true) ||
            HasActiveHuntTravelIntent() ||
            (resourceGatherer != null &&
            resourceGatherer.HasActiveGatheringFlow);
    }

    bool IsTradeDirectedWorkActive()
    {
        return currentAction == NpcText.Action("tradeSeek") ||
            currentAction == NpcText.Action("goTavern") ||
            currentAction == NpcText.Action("goMarketTrade") ||
            currentAction == NpcText.Action("goVanBaoLauBroker") ||
            currentAction == NpcText.Action("goVanBaoLauTask") ||
            currentAction == NpcText.Action("buyPill") ||
            currentAction == NpcText.Action("checkedVanBaoLau");
    }

    bool TryAbortIncompatibleHuntFlowForSchedule()
    {
        if (!NpcMapBehaviorPolicy.AllowsSchedule(gameObject))
        {
            return false;
        }

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        if (activity == NpcScheduleActivity.DoMission ||
            activity == NpcScheduleActivity.TakeTask ||
            activity == NpcScheduleActivity.FreeHuntAndGather ||
            activity == NpcScheduleActivity.Hunt ||
            activity == NpcScheduleActivity.Gather)
        {
            return false;
        }

        if (currentMonsterTarget != null ||
            isRetreatingFromMonster ||
            HasCombatSupportIntent() ||
            !IsHuntDisplayAction(currentAction))
        {
            return false;
        }

        ResetDirectedWorkStateForSchedule(activity);

        if (ShouldTraceRuntime())
        {
            TraceRuntime(
                "TryAbortIncompatibleHuntFlowForSchedule",
                "activity=" + activity +
                " action=" + currentAction);
        }

        return true;
    }

    bool TryClearStaleScheduledDirectedState()
    {
        if (!NpcMapBehaviorPolicy.AllowsSchedule(gameObject))
        {
            return false;
        }

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        bool hasGatherFlow =
            resourceGatherer != null &&
            resourceGatherer.HasActiveGatheringFlow;
        bool hasCombatOrSupportIntent =
            currentMonsterTarget != null ||
            HasCombatSupportIntent();
        bool hasDirectedContext = HasDirectedTravelContext();
        bool hasStaleDirectedActionOnly =
            IsDirectedScheduleAction(currentAction) &&
            !hasGatherFlow &&
            !hasCombatOrSupportIntent &&
            !hasDirectedContext;

        if (!hasStaleDirectedActionOnly &&
            IsDirectedWorkCompatibleWithSchedule(activity))
        {
            return false;
        }

        if (!hasStaleDirectedActionOnly)
        {
            return false;
        }

        ResetDirectedWorkStateForSchedule(activity);
        return true;
    }

    void ResetDirectedWorkStateForSchedule(NpcScheduleActivity activity)
    {
        ReleaseMonsterReservation();
        ClearActiveHuntFlow();
        ClearHelpRequestState();
        StopMonsterRetreat();
        ClearTaskProviderVisitState();
        ClearScheduledTask();
        currentMonsterTarget = null;

        if (resourceGatherer != null)
        {
            resourceGatherer.CancelGatheringNow();
        }

        hasHomeReturnTarget = false;
        ClearCultivationTravelState();
        ClearTravelTargetsAndStop();
        actionTimer = 0f;
        currentAction = string.Empty;

        if (ShouldTraceRuntime())
        {
            TraceRuntime(
                "ResetDirectedWorkStateForSchedule",
                "activity=" + activity);
        }
    }

    bool IsDirectedScheduleAction(string action)
    {
        return !string.IsNullOrWhiteSpace(action) &&
            (IsTravelIntentAction(action) ||
            IsGatherDisplayAction(action) ||
            IsHuntDisplayAction(action) ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("checkedVanBaoLau"));
    }

    bool HasDirectedTravelContext()
    {
        return currentTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget ||
            (HasLockedDirectedTarget() &&
            (currentAction == NpcText.Action("walkingRoad") ||
            IsTeleportRouteAction(currentAction)));
    }

    bool IsCultivatorSchedule()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        return schedule != null &&
            schedule.lifePath == NpcLifePath.Cultivator;
    }

    SmartAITaskGoal MapScheduleActivityToSmartGoal(
        NpcScheduleActivity activity)
    {
        switch (activity)
        {
            case NpcScheduleActivity.Cultivate:
                return SmartAITaskGoal.Cultivate;
            case NpcScheduleActivity.DoMission:
            case NpcScheduleActivity.TakeTask:
                return SmartAITaskGoal.DoMission;
            case NpcScheduleActivity.FreeHuntAndGather:
            case NpcScheduleActivity.Hunt:
            case NpcScheduleActivity.Gather:
                return SmartAITaskGoal.FreeHuntAndGather;
            case NpcScheduleActivity.TradeBuySell:
            case NpcScheduleActivity.BuyGoods:
            case NpcScheduleActivity.SellGoods:
                return SmartAITaskGoal.TradeBuySell;
            default:
                return SmartAITaskGoal.None;
        }
    }

    void ClearTaskProviderVisitState()
    {
        cachedTaskProviderTarget = null;
    }

    bool TryGetTaskProviderScheduleActivity(
        NpcScheduleController schedule,
        out NpcScheduleActivity activity)
    {
        activity = NpcScheduleActivity.Idle;

        if (schedule == null ||
            !schedule.enforceSchedule ||
            schedule.CurrentSlot == null)
        {
            return false;
        }

        if (schedule.CurrentActivity == NpcScheduleActivity.DoMission ||
            schedule.CurrentActivity == NpcScheduleActivity.TakeTask)
        {
            activity = schedule.CurrentActivity;
            return true;
        }

        return false;
    }

    bool IsScheduledCultivationTime()
    {
        EnsureDailyRoutinePlan();
        float hour = GetCurrentWorldHour();

        if (routineCultivationStartHour <= routineCultivationEndHour)
        {
            return hour >= routineCultivationStartHour &&
                hour < routineCultivationEndHour;
        }

        return hour >= routineCultivationStartHour ||
            hour < routineCultivationEndHour;
    }

    void EnsureDailyRoutinePlan()
    {
        int day = GetCurrentWorldDay();
        if (routinePlanDay == day)
        {
            return;
        }

        routinePlanDay = day;

        float minHours =
            Mathf.Clamp(dailyCultivationMinHours, 0f, 24f);
        float maxHours =
            Mathf.Clamp(
                Mathf.Max(dailyCultivationMaxHours, minHours),
                minHours,
                24f);
        float duration = Random.Range(minHours, maxHours);
        float latestStart =
            Mathf.Clamp(latestCultivationStartHour, 0f, 23.9f);
        float earliestStart =
            Mathf.Clamp(earliestCultivationHour, 0f, 23.9f);

        if (latestStart < earliestStart)
        {
            latestStart = earliestStart;
        }

        routineCultivationStartHour =
            Random.Range(earliestStart, latestStart);
        routineCultivationEndHour =
            Mathf.Repeat(routineCultivationStartHour + duration, 24f);
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.FloorToInt(Time.time / 900f) + 1;
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            return timeSystem.CurrentHour;
        }

        return Mathf.Repeat(Time.time * 24f / 900f, 24f);
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

    float GetRemainingScheduledCultivationSeconds()
    {
        if (!dailyRoutineEnabled)
        {
            return float.PositiveInfinity;
        }

        EnsureDailyRoutinePlan();
        float hour = GetCurrentWorldHour();
        float remainingHours =
            routineCultivationEndHour >= hour
            ? routineCultivationEndHour - hour
            : 24f - hour + routineCultivationEndHour;

        return GameHoursToSeconds(Mathf.Max(0.1f, remainingHours));
    }
}
