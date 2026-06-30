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
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true))) ||
            waitingOutsideTreasureLightning ||
            treasureHuntTarget != null ||
            hasTreasureWaitPosition ||
            IsTeleportRouteAction(currentAction);

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
        if (runtimeTraceScheduleChanges)
        {
            TraceRuntime(
                "RefreshScheduledStateForCurrentFrame",
                "request-goal=" +
                MapScheduleActivityToSmartGoal(schedule.CurrentActivity));
        }
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
            if (activity == NpcScheduleActivity.Cultivate &&
                canCultivate)
            {
                TraceBranch("TryRunScheduledActivity", "fatigue-interrupt-cultivate", true);
                CultivateNaturally();
            }

            else
            {
                TraceBranch("TryRunScheduledActivity", "fatigue-interrupt-wait", true);
                WaitForScheduledActivity(activity);
            }

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
                DebugFlow(
                    "DoMission",
                    "Begin quota=" + GetDailyTaskQuotaDebugText() +
                    " canVisit=" + CanVisitTaskProviderToday());
                if (!HasDailyTaskQuotaRemaining())
                {
                    TraceBranch("TryRunScheduledActivity", "mission-quota-finished", true);
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
                    TraceBranch("TryRunScheduledActivity", "mission-wait-provider", true);
                    WaitForScheduledActivity(NpcScheduleActivity.DoMission);
                }
                else
                {
                    TraceBranch("TryRunScheduledActivity", "mission-visit-provider", true);
                }
                return true;

            case NpcScheduleActivity.FreeHuntAndGather:
                if (canFight && canCompeteResource)
                {
                    TraceBranch("TryRunScheduledActivity", "free-hunt-search", true);
                    SearchMonster();
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
                    }

                    return true;
                }

                TraceBranch("TryRunScheduledActivity", "free-hunt-gather-wait", true);
                WaitForScheduledActivity(NpcScheduleActivity.FreeHuntAndGather);
                return true;

            case NpcScheduleActivity.TradeBuySell:
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
                    return true;
                }

                if (money >= 50)
                {
                    TraceBranch("TryRunScheduledActivity", "trade-buy-pill", true);
                    if (!GoToTavernAndBuyPill())
                    {
                        TraceBranch("TryRunScheduledActivity", "trade-buy-pill-fallback-wander", true);
                        StartIdleWander();
                    }
                    return true;
                }

                TraceBranch("TryRunScheduledActivity", "trade-wait", true);
                WaitForScheduledActivity(NpcScheduleActivity.TradeBuySell);
                return true;

            case NpcScheduleActivity.BuyGoods:
                if (canTrade && money >= 50)
                {
                    TraceBranch("TryRunScheduledActivity", "buy-goods", true);
                    if (!GoToTavernAndBuyPill())
                    {
                        TraceBranch("TryRunScheduledActivity", "buy-goods-fallback-wander", true);
                        StartIdleWander();
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
                if (!HasDailyTaskQuotaRemaining())
                {
                    TraceBranch("TryRunScheduledActivity", "take-task-quota-finished", false);
                    return false;
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
                if (isCultivatorSchedule &&
                    canCultivate)
                {
                    TraceBranch("TryRunScheduledActivity", "sleep-cultivate", true);
                    CultivateNaturally();
                }
                else if (IgnoresMortalNeeds())
                {
                    TraceBranch("TryRunScheduledActivity", "sleep-wait-ignored-needs", true);
                    WaitForScheduledActivity(NpcScheduleActivity.Sleep);
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

        ClearTravelTargetsAndStop();
        actionTimer = Mathf.Max(actionTimer, GameHoursToSeconds(0.15f));

        UpdateCultivationEffect(false);
        currentAction = "waitSchedule" + activity;
        if (ShouldTraceRuntime())
        {
            TraceRuntime("WaitForScheduledActivity", "activity=" + activity);
        }
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
            (pill <= 0 || spiritStone <= 0) &&
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
            NpcTaskProvider.FindNearestProvider(
                gameObject,
                transform.position);

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
        bool inProviderRange =
            provider.IsNpcInProviderInteractionRange(gameObject);

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
            " arrive=" +
            providerArriveDistance.ToString("0.00") +
            " dist=" +
            Vector2.Distance(transform.position, providerPosition).ToString("0.00") +
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
            Vector2.Distance(transform.position, providerPosition) >
            providerArriveDistance)
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
            return true;
        }

        ClearTravelTargets();

        if (provider.TryHandleVisitor(gameObject))
        {
            MarkDailyTaskAccepted();
            ClearTravelTargetsAndStop();
            currentAction = NpcText.Action("visitedTaskProvider");
            actionTimer = GameHoursToSeconds(0.2f);
            DebugFlow(
                "TaskProvider",
                "Handled by provider " + provider.name +
                " quota=" + GetDailyTaskQuotaDebugText());
            return true;
        }

        ClearTravelTargetsAndStop();
        actionTimer = Mathf.Max(thinkDelay, GameHoursToSeconds(0.15f));
        currentAction = NpcText.Action("visitedTaskProvider");
        DebugFlow(
            "TaskProvider",
            "Visited but no task handled provider=" + provider.name +
            " providerPos=" +
            providerPosition +
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
