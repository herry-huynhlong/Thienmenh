using UnityEngine;

public partial class SmartNpcAI
{
    void RefreshScheduledStateForCurrentFrame()
    {
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

    bool TryRunScheduledActivity()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        bool isCultivatorSchedule = IsCultivatorSchedule();

        if (runtimeTraceEveryThink)
        {
            TraceRuntime("TryRunScheduledActivity", "enter");
        }

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            TraceBranch("TryRunScheduledActivity", "schedule-disabled", false);
            return false;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        NpcScheduleActivity activity = schedule.CurrentActivity;

        if (slot == null)
        {
            DebugFlow("ScheduleMissing", "No current slot");
            TraceBranch("TryRunScheduledActivity", "no-current-slot", true);
            WaitForScheduledActivity(NpcScheduleActivity.Idle);
            return true;
        }

        DebugFlow("ScheduleRun", "Activity " + activity);

        if (HasEmergencySmartTask &&
            currentSmartTask.goal != MapScheduleActivityToSmartGoal(activity))
        {
            DebugFlow(
                "ScheduleRun",
                "Emergency task active, keep current override " +
                currentSmartTask.goal);
            TraceBranch("TryRunScheduledActivity", "emergency-task-override", true);
            return true;
        }

        if (canLive &&
            slot.allowHungerInterrupt &&
            NeedsFood() &&
            hunger >= 90f)
        {
            TraceBranch("TryRunScheduledActivity", "hunger-interrupt", true);
            Eat();
            return true;
        }

        if (canLive &&
            !IgnoresMortalNeeds() &&
            slot.allowFatigueInterrupt &&
            fatigue >= 90f)
        {
            TraceBranch("TryRunScheduledActivity", "fatigue-interrupt-sleep", true);
            Sleep();
            return true;
        }

        if (HasActiveHuntTravelIntent())
        {
            if (isRetreatingFromMonster)
            {
                TraceBranch("TryRunScheduledActivity", "hunt-retreat-wait", true);
                WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                return true;
            }

            if (canFight && currentMonsterTarget != null)
            {
                TraceBranch("TryRunScheduledActivity", "hunt-travel-search", true);
                SearchMonster();
            }

            TraceBranch("TryRunScheduledActivity", "hunt-travel-preserve", true);
            return true;
        }

        switch (activity)
        {
            case NpcScheduleActivity.Cultivate:
                if (canCultivate)
                {
                    if (schedule.HasCompletedCurrentSlotActivity(
                            NpcScheduleActivity.Cultivate))
                    {
                        TraceBranch("TryRunScheduledActivity", "cultivate-completed", true);
                        ClearCompletedCultivationAction();
                        return true;
                    }

                    if (schedule.HasStartedCurrentSlotActivity(
                            NpcScheduleActivity.Cultivate))
                    {
                        if (currentAction == NpcText.Action("goCultivatePoint"))
                        {
                            if (currentTarget != null ||
                                hasWanderTarget)
                            {
                                TraceBranch("TryRunScheduledActivity", "cultivate-travel-continue", true);
                                return true;
                            }

                            TraceBranch("TryRunScheduledActivity", "cultivate-travel-arrived", true);
                            CultivateNaturally();
                            return true;
                        }

                        if (actionTimer > 0f)
                        {
                            TraceBranch("TryRunScheduledActivity", "cultivate-actionTimer", true);
                            return true;
                        }

                        TraceBranch("TryRunScheduledActivity", "cultivate-start", true);
                        CultivateNaturally();
                        return true;
                    }

                    TraceBranch("TryRunScheduledActivity", "cultivate-start-fresh", true);
                    CultivateNaturally();
                    if (currentAction == NpcText.Action("goCultivatePoint") ||
                        currentAction == NpcText.Action("cultivate") ||
                        currentAction == NpcText.Action("cultivateAbsorbQi"))
                    {
                        schedule.MarkCurrentSlotActivityStarted(
                            NpcScheduleActivity.Cultivate);
                    }
                }
                else
                {
                    TraceBranch("TryRunScheduledActivity", "cultivate-fallback-idle", true);
                    StartIdleWander();
                }
                return true;

            case NpcScheduleActivity.DoMission:
                if (schedule.HasCompletedCurrentSlotActivity(
                        NpcScheduleActivity.DoMission))
                {
                    TraceBranch("TryRunScheduledActivity", "mission-completed", true);
                    ClearCompletedMissionAction();
                    return true;
                }

                DebugFlow(
                    "DoMission",
                    "Begin quota=" + GetDailyTaskQuotaDebugText() +
                    " canVisit=" + CanVisitTaskProviderToday());
                if (schedule.HasStartedCurrentSlotActivity(
                        NpcScheduleActivity.DoMission))
                {
                    if (currentAction == NpcText.Action("visitedTaskProvider") ||
                        IsDirectedWorkCompatibleWithSchedule(
                            NpcScheduleActivity.DoMission))
                    {
                        TraceBranch(
                            "TryRunScheduledActivity",
                            "mission-travel-continue",
                            true);
                        return true;
                    }
                }

                if (!HasDailyTaskQuotaRemaining())
                {
                    TraceBranch("TryRunScheduledActivity", "mission-quota-finished", true);
                    DebugFlow(
                        "DoMission",
                        "Blocked by quota quota=" + GetDailyTaskQuotaDebugText());
                    WaitForScheduledActivity(NpcScheduleActivity.DoMission);
                    return true;
                }

                if (!CanVisitTaskProviderToday())
                {
                    DebugFlow(
                        "DoMission",
                        "Provider unavailable, fallback non-mission window=" +
                        IsTaskProviderWindow() +
                        " quota=" + GetDailyTaskQuotaDebugText());
                    WaitForScheduledActivity(NpcScheduleActivity.DoMission);
                    return true;
                }

                bool didVisitTaskProvider = TryVisitTaskProvider();
                DebugFlow(
                    "DoMission",
                    "TryVisitTaskProvider result=" + didVisitTaskProvider +
                    " quota=" + GetDailyTaskQuotaDebugText());
                if (!didVisitTaskProvider)
                {
                    TraceBranch("TryRunScheduledActivity", "mission-wait-provider", true);
                    WaitForScheduledActivity(NpcScheduleActivity.DoMission);
                }
                else
                {
                    if (currentAction == NpcText.Action("visitedTaskProvider") ||
                        IsDirectedWorkCompatibleWithSchedule(
                            NpcScheduleActivity.DoMission))
                    {
                        schedule.MarkCurrentSlotActivityStarted(
                            NpcScheduleActivity.DoMission);
                    }

                    TraceBranch("TryRunScheduledActivity", "mission-visit-provider", true);
                }
                return true;

            case NpcScheduleActivity.FreeHuntAndGather:
                if (schedule.HasStartedCurrentSlotActivity(
                        NpcScheduleActivity.FreeHuntAndGather))
                {
                    if (IsDirectedWorkCompatibleWithSchedule(
                            NpcScheduleActivity.FreeHuntAndGather))
                    {
                        TraceBranch(
                            "TryRunScheduledActivity",
                            "free-hunt-continue",
                            true);
                        return true;
                    }
                }

                if (canFight && canCompeteResource)
                {
                    TraceBranch("TryRunScheduledActivity", "free-hunt-search", true);
                    SearchMonster();
                    schedule.MarkCurrentSlotActivityStarted(
                        NpcScheduleActivity.FreeHuntAndGather);
                    return true;
                }

                if (canGather && canCompeteResource)
                {
                    if (!TryStartResourceGatheringRoutine())
                    {
                        TraceBranch("TryRunScheduledActivity", "free-gather-wait", true);
                        WaitForScheduledActivity(NpcScheduleActivity.FreeHuntAndGather);
                    }
                    else
                    {
                        TraceBranch("TryRunScheduledActivity", "free-gather-start", true);
                        schedule.MarkCurrentSlotActivityStarted(
                            NpcScheduleActivity.FreeHuntAndGather);
                    }

                    return true;
                }

                TraceBranch("TryRunScheduledActivity", "free-hunt-gather-wait", true);
                WaitForScheduledActivity(NpcScheduleActivity.FreeHuntAndGather);
                return true;

            case NpcScheduleActivity.TradeBuySell:
                if (schedule.HasStartedCurrentSlotActivity(
                        NpcScheduleActivity.TradeBuySell))
                {
                    if (IsDirectedWorkCompatibleWithSchedule(
                            NpcScheduleActivity.TradeBuySell))
                    {
                        TraceBranch(
                            "TryRunScheduledActivity",
                            "trade-continue",
                            true);
                        return true;
                    }
                }

                if (!canTrade)
                {
                    TraceBranch("TryRunScheduledActivity", "trade-blocked", true);
                    WaitForScheduledActivity(NpcScheduleActivity.TradeBuySell);
                    return true;
                }

                if (HasSellableGoods() &&
                    TryStartSellGoodsRoutine())
                {
                    TraceBranch("TryRunScheduledActivity", "trade-sell-routine", true);
                    schedule.MarkCurrentSlotActivityStarted(
                        NpcScheduleActivity.TradeBuySell);
                    return true;
                }

                if (!HasAvailablePills() &&
                    money >= 50)
                {
                    TraceBranch("TryRunScheduledActivity", "trade-buy-pill", true);
                    if (!GoToTavernAndBuyPill())
                    {
                        if (HasSellableGoods() &&
                            TryStartSellGoodsRoutine())
                        {
                            TraceBranch("TryRunScheduledActivity", "trade-buy-pill-fallback-sell", true);
                            schedule.MarkCurrentSlotActivityStarted(
                                NpcScheduleActivity.TradeBuySell);
                        }
                        else
                        {
                            if (TryStartTradePresenceRoutine())
                            {
                                TraceBranch("TryRunScheduledActivity", "trade-buy-pill-fallback-market", true);
                                schedule.MarkCurrentSlotActivityStarted(
                                    NpcScheduleActivity.TradeBuySell);
                            }
                            else
                            {
                                TraceBranch("TryRunScheduledActivity", "trade-buy-pill-wait", true);
                                WaitForScheduledActivity(NpcScheduleActivity.TradeBuySell);
                            }
                        }
                    }
                    else
                    {
                        schedule.MarkCurrentSlotActivityStarted(
                            NpcScheduleActivity.TradeBuySell);
                    }
                    return true;
                }

                if (TryStartTradePresenceRoutine())
                {
                    TraceBranch("TryRunScheduledActivity", "trade-fallback-market", true);
                    schedule.MarkCurrentSlotActivityStarted(
                        NpcScheduleActivity.TradeBuySell);
                    return true;
                }

                TraceBranch("TryRunScheduledActivity", "trade-wait", true);
                WaitForScheduledActivity(NpcScheduleActivity.TradeBuySell);
                return true;

            case NpcScheduleActivity.BuyGoods:
                if (canTrade &&
                    !HasAvailablePills() &&
                    money >= 50)
                {
                    TraceBranch("TryRunScheduledActivity", "buy-goods", true);
                    if (!GoToTavernAndBuyPill())
                    {
                        TraceBranch("TryRunScheduledActivity", "buy-goods-wait", true);
                        WaitForScheduledActivity(NpcScheduleActivity.BuyGoods);
                    }
                }
                else
                {
                    TraceBranch("TryRunScheduledActivity", "buy-goods-wait", true);
                    WaitForScheduledActivity(NpcScheduleActivity.BuyGoods);
                }
                return true;

            case NpcScheduleActivity.SellGoods:
                if (!TryStartSellGoodsRoutine())
                {
                    TraceBranch("TryRunScheduledActivity", "sell-goods-fallback-wander", true);
                    StartIdleWander();
                }
                else
                {
                    TraceBranch("TryRunScheduledActivity", "sell-goods-start", true);
                }
                return true;

            case NpcScheduleActivity.TakeTask:
                if (schedule.HasCompletedCurrentSlotActivity(
                        NpcScheduleActivity.TakeTask))
                {
                    TraceBranch("TryRunScheduledActivity", "take-task-completed", true);
                    ClearCompletedMissionAction();
                    return true;
                }

                if (!HasDailyTaskQuotaRemaining())
                {
                    TraceBranch("TryRunScheduledActivity", "take-task-quota-finished", false);
                    return false;
                }

                if (!CanVisitTaskProviderToday())
                {
                    DebugFlow(
                        "TakeTask",
                        "Provider unavailable, fallback non-task window=" +
                        IsTaskProviderWindow() +
                        " quota=" + GetDailyTaskQuotaDebugText());
                    WaitForScheduledActivity(NpcScheduleActivity.TakeTask);
                    return true;
                }

                if (!TryVisitTaskProvider())
                {
                    TraceBranch("TryRunScheduledActivity", "take-task-wait-provider", true);
                    WaitForScheduledActivity(NpcScheduleActivity.TakeTask);
                }
                else
                {
                    TraceBranch("TryRunScheduledActivity", "take-task-visit-provider", true);
                }
                return true;

            case NpcScheduleActivity.Gather:
                if (!canCompeteResource)
                {
                    TraceBranch("TryRunScheduledActivity", "gather-blocked", true);
                    WaitForScheduledActivity(NpcScheduleActivity.Gather);
                    return true;
                }

                if (!TryStartResourceGatheringRoutine())
                {
                    TraceBranch("TryRunScheduledActivity", "gather-wait", true);
                    WaitForScheduledActivity(NpcScheduleActivity.Gather);
                }
                else
                {
                    TraceBranch("TryRunScheduledActivity", "gather-start", true);
                }
                return true;

            case NpcScheduleActivity.Hunt:
                if (!canFight || !canCompeteResource)
                {
                    TraceBranch("TryRunScheduledActivity", "hunt-blocked", true);
                    WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                    return true;
                }

                if (canFight)
                {
                    TraceBranch("TryRunScheduledActivity", "hunt-search", true);
                    SearchMonster();
                    bool huntFlowActive =
                        currentMonsterTarget != null ||
                        currentAction == NpcText.Action("goHunt") ||
                        MatchesSmartAction("huntMonsterNamed", true) ||
                        MatchesSmartAction("attackMonsterNamed", true) ||
                        currentTarget != null ||
                        hasWanderTarget;

                    if (!huntFlowActive)
                    {
                        TraceBranch("TryRunScheduledActivity", "hunt-wait", true);
                        WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                    }
                }
                else
                {
                    TraceBranch("TryRunScheduledActivity", "hunt-wait-no-fight", true);
                    WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                }
                return true;

            case NpcScheduleActivity.Eat:
                TraceBranch("TryRunScheduledActivity", "eat", true);
                Eat();
                return true;

            case NpcScheduleActivity.Sleep:
                if (IgnoresMortalNeeds())
                {
                    if (isCultivatorSchedule &&
                        canCultivate)
                    {
                        TraceBranch("TryRunScheduledActivity", "sleep-cultivate", true);
                        CultivateNaturally();
                    }
                    else
                    {
                        TraceBranch("TryRunScheduledActivity", "sleep-wait-ignored-needs", true);
                        WaitForScheduledActivity(NpcScheduleActivity.Sleep);
                    }
                }
                else
                {
                    TraceBranch("TryRunScheduledActivity", "sleep", true);
                    Sleep();
                }
                return true;

            default:
                TraceBranch("TryRunScheduledActivity", "default-wait-" + activity, true);
                WaitForScheduledActivity(activity);
                return true;
        }
    }

    void WaitForScheduledActivity(NpcScheduleActivity activity)
    {
        if (activity == NpcScheduleActivity.Cultivate &&
            canCultivate)
        {
            CultivateNaturally();
            return;
        }

        bool preserveDirectedWork =
            activity != NpcScheduleActivity.Idle &&
            IsDirectedWorkCompatibleWithSchedule(activity);

        if (!preserveDirectedWork)
        {
            ClearTravelTargetsAndStop();
        }

        actionTimer = Mathf.Max(
            actionTimer,
            GameHoursToSeconds(
                activity == NpcScheduleActivity.Idle
                    ? 5f / 60f
                    : 0.15f));

        UpdateCultivationEffect(false);
        if (!preserveDirectedWork)
        {
            currentAction = "waitSchedule" + activity;
        }
        if (ShouldTraceRuntime())
        {
            TraceRuntime(
                "WaitForScheduledActivity",
                "activity=" + activity +
                " preserve=" + preserveDirectedWork);
        }
        DebugFlow(
            "ScheduleWait",
            (preserveDirectedWork ? "Preserve target " : "Waiting ") +
            activity);
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
            HasDirectedTravelContext() ||
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

        bool shouldResetForSchedule =
            activity == NpcScheduleActivity.Idle ||
            activity == NpcScheduleActivity.Work ||
            activity == NpcScheduleActivity.Cultivate ||
            activity == NpcScheduleActivity.TradeBuySell ||
            activity == NpcScheduleActivity.BuyGoods ||
            activity == NpcScheduleActivity.SellGoods ||
            activity == NpcScheduleActivity.Eat ||
            activity == NpcScheduleActivity.Sleep ||
            activity == NpcScheduleActivity.ReturnHome ||
            activity == NpcScheduleActivity.Alchemy ||
            activity == NpcScheduleActivity.Forge;

        if (!shouldResetForSchedule &&
            !hasStaleDirectedActionOnly)
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

    void StartIdleWander()
    {
        actionTimer =
            Mathf.Max(
                actionTimer,
                GameHoursToSeconds(Random.Range(0.4f, 1.2f)));
        currentAction = NpcText.Action("idle");

        if (TryPickIdleWanderTarget(out Vector3 target))
        {
            ClearTravelTargets();
            wanderTarget = target;
            hasWanderTarget = true;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            TraceRuntime(
                "StartIdleWander",
                "picked-target=" + target);
            return;
        }

        ClearTravelTargetsAndStop();
        TraceRuntime("StartIdleWander", "no-target");
    }

    void ConfigureAutonomousWorkSystems()
    {
        NpcItemCollector itemCollector = GetComponent<NpcItemCollector>();
        if (itemCollector != null)
        {
            itemCollector.canPickupItems =
                canGather && canCompeteResource;
        }

        if (canGather && canCompeteResource)
        {
            if (itemCollector == null)
            {
                gameObject.AddComponent<NpcItemCollector>();
            }

            resourceGatherer = GetComponent<NpcResourceGatherer>();
            if (resourceGatherer == null)
            {
                resourceGatherer = gameObject.AddComponent<NpcResourceGatherer>();
            }

            resourceGatherer.canGather = true;
        }
        else
        {
            resourceGatherer = GetComponent<NpcResourceGatherer>();
            if (resourceGatherer != null)
            {
                resourceGatherer.canGather = false;
            }
        }

        if (canTrade || canSellGoods)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
            if (tradeAgent == null)
            {
                tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
            }

            if (tradeAgent != null)
            {
                tradeAgent.inventory = GetComponent<ItemInventory>();
            }
        }
    }

    bool TryStartScheduledNonCultivationActivity()
    {
        if (runtimeTraceEveryThink)
        {
            TraceRuntime("TryStartScheduledNonCultivationActivity", "enter");
        }

        ClearCultivationTravelState();

        if (CanVisitTaskProviderToday() &&
            TryVisitTaskProvider())
        {
            TraceBranch("TryStartScheduledNonCultivationActivity", "visit-task-provider", true);
            return true;
        }

        if (TryStartSellGoodsRoutine())
        {
            TraceBranch("TryStartScheduledNonCultivationActivity", "sell-goods", true);
            return true;
        }

        if (TryStartResourceGatheringRoutine())
        {
            TraceBranch("TryStartScheduledNonCultivationActivity", "resource-gather", true);
            return true;
        }

        if (canTrade &&
            (!HasAvailablePills() || spiritStone <= 0) &&
            money >= 50 &&
            Random.value < 0.35f)
        {
            if (!GoToTavernAndBuyPill())
            {
                StartIdleWander();
                TraceBranch("TryStartScheduledNonCultivationActivity", "buy-pill-failed-wander", true);
            }

            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        tradeSessionMinGameHours,
                        tradeSessionMaxGameHours));
            TraceBranch("TryStartScheduledNonCultivationActivity", "buy-pill", true);
            return true;
        }

        if (canFight &&
            canCompeteResource &&
            bravery >= 45 &&
            Random.value < 0.25f)
        {
            SearchMonster();
            if (currentMonsterTarget == null)
            {
                ClearTravelTargetsAndStop();
                currentAction = NpcText.Action("idle");

                StartIdleWander();
                TraceBranch("TryStartScheduledNonCultivationActivity", "hunt-failed-wander", true);
                return true;
            }

            actionTimer =
                GameHoursToSeconds(Random.Range(0.5f, 1.5f));
            TraceBranch("TryStartScheduledNonCultivationActivity", "hunt", true);
            return true;
        }

        if (canMakeFriends &&
            kindness + greed < 140 &&
            Random.value < 0.25f)
        {
            MakeFriend();
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        socialSessionMinGameHours,
                        socialSessionMaxGameHours));
            TraceBranch("TryStartScheduledNonCultivationActivity", "make-friend", true);
            return true;
        }

        if (autonomousActivitiesEnabled &&
            canCreateSect)
        {
            TryCreateSect();
            TraceBranch("TryStartScheduledNonCultivationActivity", "create-sect", true);
            return true;
        }

        StartIdleWander();
        TraceBranch("TryStartScheduledNonCultivationActivity", "idle-wander", true);
        return true;
    }

    bool TryStartForgePurchaseRoutine()
    {
        if (!canTrade ||
            money <= 0)
        {
            return false;
        }

        if (currentForgeTradeTarget == null ||
            currentForgeTradeItem == null)
        {
            currentForgeTradeTarget =
                NpcForgeAgent.FindBestForgeForBuyer(
                    gameObject,
                    out currentForgeTradeItem);

            if (currentForgeTradeTarget == null ||
                currentForgeTradeItem == null)
            {
                ClearForgeTradeTarget();
                return false;
            }

            Transform forgePoint =
                currentForgeTradeTarget.forgeStandPoint != null
                    ? currentForgeTradeTarget.forgeStandPoint
                    : currentForgeTradeTarget.transform;

            if (forgePoint == null)
            {
                ClearForgeTradeTarget();
                return false;
            }

            currentTarget = forgePoint;
            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            currentAction = NpcText.Action("tradeSeek");
        }

        if (currentForgeTradeTarget == null ||
            currentForgeTradeItem == null ||
            currentTarget == null)
        {
            ClearForgeTradeTarget();
            return false;
        }

        if (Vector2.Distance(transform.position, currentTarget.position) >
            Mathf.Max(0.5f, targetClearRadius))
        {
            return true;
        }

        string buyerLine;
        string smithLine;
        bool ordered =
            currentForgeTradeTarget.TryRequestCustomOrder(
                gameObject,
                currentForgeTradeItem,
                1,
                out buyerLine,
                out smithLine);

        ClearForgeTradeTarget();

        if (!ordered)
        {
            return false;
        }

        actionTimer =
            GameHoursToSeconds(
                Random.Range(
                    tradeSessionMinGameHours,
                    tradeSessionMaxGameHours));
        currentAction = NpcText.Action("idle");
        return true;
    }

    void ClearForgeTradeTarget()
    {
        currentForgeTradeTarget = null;
        currentForgeTradeItem = null;
        ClearTravelTargetsAndStop();
    }

    void ClearTaskProviderVisitState()
    {
        cachedTaskProviderTarget = null;
    }

    bool IsTaskProviderTargetUsable(NpcTaskProvider provider)
    {
        return provider != null &&
            provider.isActiveAndEnabled &&
            provider.gameObject.activeInHierarchy &&
            provider.provideTasks &&
            provider.offers != null &&
            provider.offers.Length > 0;
    }

    NpcTaskProvider ResolveTaskProviderVisitTarget()
    {
        if (IsTaskProviderTargetUsable(cachedTaskProviderTarget) &&
            cachedTaskProviderTarget.HasAnyOfferForNpc(gameObject, false))
        {
            return cachedTaskProviderTarget;
        }

        cachedTaskProviderTarget = null;

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(
                gameObject,
                transform.position,
                false);
        if (!IsTaskProviderTargetUsable(provider))
        {
            return null;
        }

        cachedTaskProviderTarget = provider;
        return cachedTaskProviderTarget;
    }

    bool TryVisitTaskProvider()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        NpcScheduleActivity scheduledTaskActivity;
        bool hasScheduledTaskActivity =
            TryGetTaskProviderScheduleActivity(
                schedule,
                out scheduledTaskActivity);

        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentSlot != null &&
            !hasScheduledTaskActivity)
        {
            DebugFlow(
                "TaskProvider",
                "Skip visit outside task slot current=" +
                schedule.CurrentActivity);
            return false;
        }

        if (!CanVisitTaskProviderToday())
        {
            ClearTaskProviderVisitState();
            float currentHour = GetCurrentWorldHour();
            DebugFlow(
                "TaskProvider",
                "Visit blocked enabled=" + dailyTaskVisitEnabled +
                " quota=" + GetDailyTaskQuotaDebugText() +
                " window=" + IsTaskProviderWindow() +
                " hour=" + currentHour.ToString("0.00") +
                " start=" + taskProviderStartHour.ToString("0.00") +
                " end=" + taskProviderEndHour.ToString("0.00"));
            return false;
        }

        TraceRuntime(
            "TryVisitTaskProvider",
            "enter action=" + currentAction +
            " target=" + (currentTarget != null ? currentTarget.name : "null") +
            " wander=" + hasWanderTarget +
            " quota=" + GetDailyTaskQuotaDebugText() +
            " slot=" + (schedule != null && schedule.CurrentSlot != null
                ? schedule.CurrentActivity.ToString()
                : "none"));

        NpcTaskProvider provider =
            ResolveTaskProviderVisitTarget();

        if (provider == null)
        {
            DebugFlow(
                "TaskProvider",
                "No provider with valid offer found quota=" +
                GetDailyTaskQuotaDebugText());
            return false;
        }

        Vector3 providerPosition =
            provider.GetProviderPositionFor(gameObject);
        NpcMapArea providerArea = NpcMapArea.FindArea(providerPosition);
        NpcMapArea selfArea = NpcMapArea.FindArea(transform.position);
        NpcMapZone? providerStandZone =
            provider.providerStandPoint != null
                ? NpcMapNavigator.GetDestinationZone(
                    provider.providerStandPoint)
                : (NpcMapZone?)null;
        float providerArriveDistance =
            Mathf.Max(
                0.45f,
                targetClearRadius * 2f,
                provider.providerTalkDistance * 0.9f);
        float providerDistance =
            Vector2.Distance(transform.position, providerPosition);
        bool inProviderRange =
            provider.IsNpcInProviderInteractionRange(gameObject);
        bool allowBlockedCloseVisit =
            !inProviderRange &&
            provider.providerStandPoint == null &&
            providerDistance <=
            Mathf.Max(
                provider.GetProviderInteractionDistance(),
                providerArriveDistance) +
            Mathf.Max(0.12f, targetClearRadius * 0.5f);

        if (allowBlockedCloseVisit)
        {
            inProviderRange = true;
        }

        DebugFlow(
            "TaskProvider",
            "Eval provider=" +
            provider.name +
            " providerPos=" +
            providerPosition +
            " standPoint=" +
            (provider.providerStandPoint != null
                ? provider.providerStandPoint.position.ToString()
                : "null") +
            " inRange=" +
            inProviderRange +
            " closeVisit=" +
            allowBlockedCloseVisit +
            " arrive=" +
            providerArriveDistance.ToString("0.00") +
            " dist=" +
            providerDistance.ToString("0.00") +
            " pos=" +
            transform.position +
            " selfArea=" +
            (selfArea != null ? selfArea.name : "null") +
            " providerArea=" +
            (providerArea != null ? providerArea.name : "null") +
            " providerZone=" +
            (providerArea != null ? providerArea.zone.ToString() : "None") +
            " standZone=" +
            (providerStandZone.HasValue ? providerStandZone.Value.ToString() : "None") +
            " target=" +
            (currentTarget != null ? currentTarget.name : "null") +
            " wander=" +
            hasWanderTarget);

        currentAction = NpcText.Action("goTaskProviderDaily");

        if (!inProviderRange &&
            providerDistance > providerArriveDistance)
        {
            ClearTravelTargets();
            if (provider.providerStandPoint != null)
            {
                currentTarget = provider.providerStandPoint;
                hasWanderTarget = false;
            }
            else
            {
                currentTarget = null;
                wanderTarget = providerPosition;
                hasWanderTarget = true;
            }
            actionTimer = 0f;
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    hasScheduledTaskActivity
                        ? scheduledTaskActivity
                        : NpcScheduleActivity.DoMission);
            }
            DebugFlow(
                "TaskProvider",
                "Moving to provider " + provider.name +
                " stand=" + providerPosition +
                " mode=" +
                (provider.providerStandPoint != null
                    ? "TransformTarget"
                    : "WanderTarget") +
                " providerArea=" +
                (providerArea != null ? providerArea.name : "null") +
                " providerZone=" +
                (providerArea != null ? providerArea.zone.ToString() : "None") +
                " quota=" + GetDailyTaskQuotaDebugText());
            TraceRuntime(
                "TryVisitTaskProvider",
                "move provider=" + provider.name +
                " inRange=" + inProviderRange +
                " dist=" + providerDistance.ToString("0.00") +
                " arrive=" + providerArriveDistance.ToString("0.00") +
                " target=" + (currentTarget != null ? currentTarget.name : "null") +
                " wander=" + hasWanderTarget);
            return true;
        }

        if (!inProviderRange)
        {
            ClearTravelTargets();
            if (provider.providerStandPoint != null)
            {
                currentTarget = provider.providerStandPoint;
                hasWanderTarget = false;
            }
            else
            {
                currentTarget = null;
                wanderTarget = providerPosition;
                hasWanderTarget = true;
            }
            actionTimer = 0f;
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    hasScheduledTaskActivity
                        ? scheduledTaskActivity
                        : NpcScheduleActivity.DoMission);
            }
            DebugFlow(
                "TaskProvider",
                "Adjusting to exact provider stand " + provider.name +
                " stand=" + providerPosition +
                " mode=" +
                (provider.providerStandPoint != null
                    ? "TransformTarget"
                    : "WanderTarget") +
                " providerArea=" +
                (providerArea != null ? providerArea.name : "null") +
                " providerZone=" +
                (providerArea != null ? providerArea.zone.ToString() : "None") +
                " quota=" + GetDailyTaskQuotaDebugText());
            TraceRuntime(
                "TryVisitTaskProvider",
                "adjust provider=" + provider.name +
                " inRange=" + inProviderRange +
                " dist=" + providerDistance.ToString("0.00") +
                " arrive=" + providerArriveDistance.ToString("0.00") +
                " target=" + (currentTarget != null ? currentTarget.name : "null") +
                " wander=" + hasWanderTarget);
            return true;
        }

        ClearTravelTargets();

        if (provider.TryHandleVisitor(gameObject, false))
        {
            MarkDailyTaskAccepted();
            ClearTaskProviderVisitState();
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    hasScheduledTaskActivity
                        ? scheduledTaskActivity
                        : NpcScheduleActivity.DoMission);
            }
            ClearTravelTargetsAndStop();
            currentAction = NpcText.Action("visitedTaskProvider");
            actionTimer = GameHoursToSeconds(0.2f);
            DebugFlow(
                "TaskProvider",
                "Handled by provider " + provider.name +
                " quota=" + GetDailyTaskQuotaDebugText());
            TraceRuntime(
                "TryVisitTaskProvider",
                "handled provider=" + provider.name +
                " inRange=" + inProviderRange +
                " target=" + (currentTarget != null ? currentTarget.name : "null") +
                " wander=" + hasWanderTarget);
            return true;
        }

        ClearTravelTargetsAndStop();
        ClearTaskProviderVisitState();
        actionTimer = Mathf.Max(thinkDelay, GameHoursToSeconds(0.15f));
        currentAction = NpcText.Action("visitedTaskProvider");
        DebugFlow(
            "TaskProvider",
            "Visited but no task handled provider=" + provider.name +
            " providerPos=" +
            providerPosition +
            " quota=" + GetDailyTaskQuotaDebugText());
        TraceRuntime(
            "TryVisitTaskProvider",
            "visit-without-task provider=" + provider.name +
            " inRange=" + inProviderRange +
            " target=" + (currentTarget != null ? currentTarget.name : "null") +
            " wander=" + hasWanderTarget);
        return true;
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
