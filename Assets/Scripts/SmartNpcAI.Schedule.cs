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
            HasLockedDirectedTarget() ||
            (currentSmartTask != null &&
            currentSmartTask.priority > SmartAITaskPriority.Normal) ||
            HasCombatSupportIntent() ||
            (NpcScheduleController.IsMatchingActivity(
                schedule.CurrentActivity,
                NpcScheduleActivity.Hunt) &&
            (currentMonsterTarget != null ||
            currentAction == NpcText.Action("goHunt") ||
            currentAction == NpcText.Action("huntMonsterNamed") ||
            currentAction == NpcText.Action("attackMonsterNamed"))) ||
            waitingOutsideTreasureLightning ||
            treasureHuntTarget != null ||
            hasTreasureWaitPosition ||
            IsTeleportRouteAction(currentAction);

        currentScheduleSlotKey = key;
        if (!preserveActiveFlow)
        {
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

        RequestScheduledTask(
            MapScheduleActivityToSmartGoal(schedule.CurrentActivity),
            "schedule " + schedule.CurrentActivity);
        DebugFlow(
            "ScheduleGoal",
            "hour=" + GetCurrentWorldHour().ToString("0.00") +
            " activity=" + schedule.CurrentActivity +
            " mappedGoal=" +
            MapScheduleActivityToSmartGoal(schedule.CurrentActivity));

        if (schedule.CurrentActivity == NpcScheduleActivity.Gather ||
            schedule.CurrentActivity == NpcScheduleActivity.Hunt ||
            schedule.CurrentActivity == NpcScheduleActivity.FreeHuntAndGather)
        {
            schedule.ClearCurrentSlotActivityState(schedule.CurrentActivity);
        }
    }

    string BuildScheduleSlotKey(NpcScheduleSlot slot, NpcScheduleActivity activity)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        int day = timeSystem != null ? timeSystem.CurrentDay : 0;
        return day + ":" + activity + ":" +
            Mathf.RoundToInt(slot.startHour * 100f) + ":" +
            Mathf.RoundToInt(slot.endHour * 100f);
    }

    void ClearTravelTargets(bool clearWanderTarget = true)
    {
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
        ClearTravelTargets(clearWanderTarget);
        StopNpcMovement();
    }

    bool TryRunScheduledActivity()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        bool isCultivatorSchedule = IsCultivatorSchedule();

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        NpcScheduleActivity activity = schedule.CurrentActivity;

        if (slot == null)
        {
            DebugFlow("ScheduleMissing", "No current slot");
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
            return true;
        }

        if (canLive &&
            slot.allowHungerInterrupt &&
            NeedsFood() &&
            hunger >= 90f)
        {
            Eat();
            return true;
        }

        if (canLive &&
            !IgnoresMortalNeeds() &&
            slot.allowFatigueInterrupt &&
            fatigue >= 90f)
        {
            if (activity == NpcScheduleActivity.Cultivate &&
                canCultivate)
            {
                CultivateNaturally();
            }

            else
            {
                WaitForScheduledActivity(activity);
            }

            return true;
        }

        if (HasActiveHuntTravelIntent())
        {
            if (isRetreatingFromMonster)
            {
                WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                return true;
            }

            if (canFight && currentMonsterTarget != null)
            {
                SearchMonster();
            }

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
                                return true;
                            }

                            CultivateNaturally();
                            return true;
                        }

                        if (actionTimer > 0f)
                        {
                            return true;
                        }

                        CultivateNaturally();
                        return true;
                    }

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
                    StartIdleWander();
                }
                return true;

            case NpcScheduleActivity.DoMission:
                DebugFlow(
                    "DoMission",
                    "Begin quota=" + GetDailyTaskQuotaDebugText() +
                    " canVisit=" + CanVisitTaskProviderToday());
                if (!HasDailyTaskQuotaRemaining())
                {
                    DebugFlow(
                        "DoMission",
                        "Blocked by quota quota=" + GetDailyTaskQuotaDebugText());
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
                    WaitForScheduledActivity(NpcScheduleActivity.DoMission);
                }
                return true;

            case NpcScheduleActivity.FreeHuntAndGather:
                if (canFight && canCompeteResource)
                {
                    SearchMonster();
                    return true;
                }

                if (canGather && canCompeteResource)
                {
                    if (!TryStartResourceGatheringRoutine())
                    {
                        WaitForScheduledActivity(NpcScheduleActivity.FreeHuntAndGather);
                    }

                    return true;
                }

                WaitForScheduledActivity(NpcScheduleActivity.FreeHuntAndGather);
                return true;

            case NpcScheduleActivity.TradeBuySell:
                if (!canTrade)
                {
                    WaitForScheduledActivity(NpcScheduleActivity.TradeBuySell);
                    return true;
                }

                if (HasSellableGoods() &&
                    TryStartSellGoodsRoutine())
                {
                    return true;
                }

                if (money >= 50)
                {
                    GoToTavernAndBuyPill();
                    return true;
                }

                WaitForScheduledActivity(NpcScheduleActivity.TradeBuySell);
                return true;

            case NpcScheduleActivity.BuyGoods:
                if (canTrade && money >= 50)
                {
                    GoToTavernAndBuyPill();
                }
                else
                {
                    WaitForScheduledActivity(NpcScheduleActivity.BuyGoods);
                }
                return true;

            case NpcScheduleActivity.SellGoods:
                if (!TryStartSellGoodsRoutine())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.SellGoods);
                }
                return true;

            case NpcScheduleActivity.TakeTask:
                if (!HasDailyTaskQuotaRemaining())
                {
                    return false;
                }

                if (!TryVisitTaskProvider())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.TakeTask);
                }
                return true;

            case NpcScheduleActivity.Gather:
                if (!canCompeteResource)
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Gather);
                    return true;
                }

                if (!TryStartResourceGatheringRoutine())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Gather);
                }
                return true;

            case NpcScheduleActivity.Hunt:
                if (!canFight || !canCompeteResource)
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                    return true;
                }

                if (canFight)
                {
                    SearchMonster();
                    bool huntFlowActive =
                        currentMonsterTarget != null ||
                        currentAction == NpcText.Action("goHunt") ||
                        currentAction == NpcText.Action("huntMonsterNamed") ||
                        currentAction == NpcText.Action("attackMonsterNamed") ||
                        currentTarget != null ||
                        hasWanderTarget;

                    if (!huntFlowActive)
                    {
                        WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                    }
                }
                else
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                }
                return true;

            case NpcScheduleActivity.Eat:
                Eat();
                return true;

            case NpcScheduleActivity.Sleep:
                if (isCultivatorSchedule &&
                    canCultivate)
                {
                    CultivateNaturally();
                }
                else if (IgnoresMortalNeeds())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Sleep);
                }
                else
                {
                    Sleep();
                }
                return true;

            default:
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

        ClearTravelTargetsAndStop();
        actionTimer = Mathf.Max(actionTimer, GameHoursToSeconds(0.15f));

        UpdateCultivationEffect(false);
        currentAction = "waitSchedule" + activity;
        DebugFlow("ScheduleWait", "Waiting " + activity);
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
        currentAction = "";
        StopNpcMovement();
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
        if (CanVisitTaskProviderToday() &&
            TryVisitTaskProvider())
        {
            return true;
        }

        if (TryStartSellGoodsRoutine())
        {
            return true;
        }

        if (TryStartResourceGatheringRoutine())
        {
            return true;
        }

        if (canTrade &&
            (pill <= 0 || spiritStone <= 0) &&
            money >= 50 &&
            Random.value < 0.35f)
        {
            GoToTavernAndBuyPill();
            if (currentTarget == null &&
                currentAction != NpcText.Action("buyPill"))
            {
                ClearTravelTargetsAndStop();
                currentAction = "";

                StartIdleWander();
                return true;
            }

            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        tradeSessionMinGameHours,
                        tradeSessionMaxGameHours));
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
                currentAction = "";

                StartIdleWander();
                return true;
            }

            actionTimer =
                GameHoursToSeconds(Random.Range(0.5f, 1.5f));
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
            return true;
        }

        if (autonomousActivitiesEnabled &&
            canCreateSect)
        {
            TryCreateSect();
            return true;
        }

        StartIdleWander();
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

    bool TryVisitTaskProvider()
    {
        if (!CanVisitTaskProviderToday())
        {
            DebugFlow(
                "TaskProvider",
                "Visit blocked enabled=" + dailyTaskVisitEnabled +
                " quota=" + GetDailyTaskQuotaDebugText() +
                " window=" + IsTaskProviderWindow());
            return false;
        }

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider == null)
        {
            DebugFlow(
                "TaskProvider",
                "No provider found quota=" + GetDailyTaskQuotaDebugText());
            return false;
        }

        Vector3 providerPosition =
            provider.GetProviderPositionFor(gameObject);
        float providerArriveDistance =
            Mathf.Max(
                0.45f,
                targetClearRadius * 2f,
                provider.providerTalkDistance * 0.9f);
        bool inProviderRange =
            provider.IsNpcInProviderInteractionRange(gameObject);

        currentAction = NpcText.Action("goTaskProviderDaily");

        if (!inProviderRange &&
            Vector2.Distance(transform.position, providerPosition) >
            providerArriveDistance)
        {
            ClearTravelTargets();
            currentTarget = null;
            wanderTarget = providerPosition;
            hasWanderTarget = true;
            actionTimer = 0f;
            DebugFlow(
                "TaskProvider",
                "Moving to provider " + provider.name +
                " stand=" + providerPosition +
                " quota=" + GetDailyTaskQuotaDebugText());
            return true;
        }

        if (!inProviderRange)
        {
            ClearTravelTargets();
            currentTarget = null;
            wanderTarget = providerPosition;
            hasWanderTarget = true;
            actionTimer = 0f;
            DebugFlow(
                "TaskProvider",
                "Adjusting to exact provider stand " + provider.name +
                " stand=" + providerPosition +
                " quota=" + GetDailyTaskQuotaDebugText());
            return true;
        }

        ClearTravelTargets();

        if (provider.TryHandleVisitor(gameObject))
        {
            MarkDailyTaskAccepted();
            DebugFlow(
                "TaskProvider",
                "Handled by provider " + provider.name +
                " quota=" + GetDailyTaskQuotaDebugText());
            return true;
        }

        actionTimer = Mathf.Max(thinkDelay, GameHoursToSeconds(0.15f));
        currentAction = NpcText.Action("visitedTaskProvider");
        DebugFlow(
            "TaskProvider",
            "Visited but no task handled provider=" + provider.name +
            " quota=" + GetDailyTaskQuotaDebugText());
        return true;
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
