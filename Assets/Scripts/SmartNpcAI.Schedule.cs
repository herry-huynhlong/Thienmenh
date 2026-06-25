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

        currentScheduleSlotKey = key;
        currentTarget = null;
        currentMonsterTarget = null;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        waitingOutsideTreasureLightning = false;
        hasWanderTarget = false;
        hasCultivationTarget = false;
        hasEscapeTarget = false;
        hasHomeReturnTarget = false;
        hasObstacleAvoidTarget = false;
        actionTimer = 0f;
        currentAction = string.Empty;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        UpdateCultivationEffect(false);

        if (resourceGatherer != null)
        {
            resourceGatherer.CancelGatheringNow();
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

    bool TryRunScheduledActivity()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();

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
            Sleep();
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
                                hasWanderTarget ||
                                hasHomeReturnTarget)
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
                if (!TryVisitTaskProvider())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.TakeTask);
                }
                return true;

            case NpcScheduleActivity.Gather:
                if (!TryStartResourceGatheringRoutine())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Gather);
                }
                return true;

            case NpcScheduleActivity.Hunt:
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
                if (IgnoresMortalNeeds())
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

        currentTarget = null;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasHomeReturnTarget = false;
        hasObstacleAvoidTarget = false;
        actionTimer = Mathf.Max(actionTimer, GameHoursToSeconds(0.15f));

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        UpdateCultivationEffect(false);
        currentAction = "waitSchedule" + activity;
        DebugFlow("ScheduleWait", "Waiting " + activity);
    }

    void StartIdleWander()
    {
        if (canCultivate)
        {
            CultivateNaturally();
            return;
        }

        actionTimer =
            Mathf.Max(
                actionTimer,
                GameHoursToSeconds(Random.Range(0.4f, 1.2f)));
        currentAction = "";
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void ConfigureAutonomousWorkSystems()
    {
        if (canGather)
        {
            if (GetComponent<NpcItemCollector>() == null)
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
        if (dailyTaskVisitEnabled &&
            Random.value < dailyTaskVisitChance &&
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
                if (canCultivate)
                {
                    CultivateNaturally();
                }
                else if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    currentAction = "";
                }

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
            bravery >= 45 &&
            Random.value < 0.25f)
        {
            SearchMonster();
            if (currentMonsterTarget == null)
            {
                if (canCultivate)
                {
                    CultivateNaturally();
                }
                else if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    currentAction = "";
                }

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

        if (canCultivate)
        {
            CultivateNaturally();
            return true;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        currentAction = "";
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
        currentTarget = null;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    bool TryVisitTaskProvider()
    {
        if (!IsTaskVisitStaggerReady())
        {
            return false;
        }

        int day = GetCurrentWorldDay();
        if (lastTaskProviderVisitDay == day)
        {
            return false;
        }

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider == null)
        {
            DebugFlow("TaskProvider", "No provider found");
            return false;
        }

        Vector3 providerPosition =
            provider.GetProviderPositionFor(gameObject);

        currentAction = NpcText.Action("goTaskProviderDaily");

        if (Vector2.Distance(transform.position, providerPosition) > 1.5f)
        {
            currentTarget = provider.transform;
            hasWanderTarget = false;
            actionTimer = 0f;
            DebugFlow("TaskProvider", "Moving to provider");
            return true;
        }

        currentTarget = null;
        hasWanderTarget = false;
        lastTaskProviderVisitDay = day;

        if (provider.TryHandleVisitor(gameObject))
        {
            DebugFlow("TaskProvider", "Handled by provider");
            return true;
        }

        actionTimer = Mathf.Max(thinkDelay, GameHoursToSeconds(0.15f));
        currentAction = NpcText.Action("visitedTaskProvider");
        DebugFlow("TaskProvider", "Visited but no task handled");
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
