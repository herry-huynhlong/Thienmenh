using System;
using UnityEngine;

public partial class SmartNpcAI
{
    public SmartAITask CurrentSmartTask => currentSmartTask;

    public bool HasEmergencySmartTask =>
        currentSmartTask != null &&
        currentSmartTask.priority >= SmartAITaskPriority.Emergency;

    public bool RequestEmergencyTask(
        SmartAITaskGoal goal,
        SmartAITaskPriority priority,
        bool canBeInterrupted,
        string reason)
    {
        DebugFlow(
            "TaskRequest",
            "Emergency request goal=" + goal +
            " priority=" + priority +
            " interrupt=" + canBeInterrupted +
            " reason=" + reason);
        return RequestSmartTask(
            goal,
            priority,
            canBeInterrupted,
            reason,
            isScheduleTask: false);
    }

    public bool RequestScheduledTask(
        SmartAITaskGoal goal,
        string reason)
    {
        DebugFlow(
            "TaskRequest",
            "Schedule request goal=" + goal +
            " reason=" + reason);
        return RequestSmartTask(
            goal,
            SmartAITaskPriority.Normal,
            true,
            reason,
            isScheduleTask: true);
    }

    public void ForceSetCurrentAction(string action, float durationSeconds = 0f)
    {
        SetActionImmediate(action, durationSeconds);
    }

    public void EnterBicanhSessionMode()
    {
        EnterRestrictedMapSessionMode();
    }

    public void EnterRestrictedMapSessionMode()
    {
        isBicanhParticipant = true;
        ExitFrontierDefenseMode();
        NpcMapNavigator.ClearNpcState(gameObject);
        NpcMapBehaviorPolicy.ClearForcedCombatZone(gameObject);

        NpcTaskProvider.ReleaseNpcFromProviderTasksForCombat(gameObject);
        ClearTaskProviderVisitState();
        ClearCultivationTravelState();
        ClearTravelTargetsAndStop();
        ClearHelpRequestState();
        StopMonsterRetreat();

        waitingOutsideTreasureLightning = false;
        hasTreasureWaitPosition = false;
        treasureWaitLowPowerSkirmish = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        hasHomeReturnTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        postTeleportRecoveryUntil = Time.time + 0.35f;
        actionTimer = 0f;
        thinkTimer = 0f;

        if (IsNormalWorldTravelAction(currentAction))
        {
            ReleaseMonsterReservation();
            currentMonsterTarget = null;
            ClearMonsterCombatState();
            SetActionImmediate(NpcText.Action("idle"));
        }
    }

    public void ExitBicanhSessionMode()
    {
        ExitRestrictedMapSessionMode();
    }

    public void ExitRestrictedMapSessionMode()
    {
        isBicanhParticipant = false;
    }

    public void EnforceBicanhCombatOnlyState()
    {
        EnforceMapBehaviorPolicyState();
    }

    public void EnforceMapBehaviorPolicyState()
    {
        if (NpcMapBehaviorPolicy.AllowsNormalWorldTravel(gameObject) &&
            NpcMapBehaviorPolicy.AllowsSchedule(gameObject) &&
            !isBicanhParticipant)
        {
            return;
        }

        if (currentMonsterTarget != null &&
            !NpcMapBehaviorPolicy.CanUseMonsterTarget(
                gameObject,
                currentMonsterTarget))
        {
            ReleaseMonsterReservation(currentMonsterTarget);
            currentMonsterTarget = null;
            ClearMonsterCombatState();
            ClearHelpRequestState();
        }

        bool hasCombatPatrolIntent =
            NpcMapBehaviorPolicy.ForcesCombatLoop(gameObject) &&
            (currentAction == NpcText.Action("goHunt") ||
            IsHuntDisplayAction(currentAction)) &&
            (hasWanderTarget || currentTarget != null);
        bool hasCombatAvoidIntent =
            NpcMapBehaviorPolicy.ForcesCombatLoop(gameObject) &&
            currentAction == NpcText.Action("fleeMonsterArea") &&
            (hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget);

        bool hasCombatIntent =
            currentMonsterTarget != null ||
            hasCombatPatrolIntent ||
            hasCombatAvoidIntent ||
            HasCombatSupportIntent() ||
            (currentSmartTask != null &&
            currentSmartTask.IsValid &&
            IsMapCombatTaskGoal(currentSmartTask.goal)) ||
            (scheduleSmartTask != null &&
            scheduleSmartTask.IsValid &&
            IsMapCombatTaskGoal(scheduleSmartTask.goal));

        if (!hasCombatIntent)
        {
            ClearSmartTask();
            ClearScheduledTask();
        }

        bool hasRestrictedMapContext =
            IsNormalWorldTravelAction(currentAction) ||
            currentTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget ||
            currentMonsterTarget != null ||
            (currentSmartTask != null && currentSmartTask.IsValid) ||
            (scheduleSmartTask != null && scheduleSmartTask.IsValid);

        if (!hasCombatIntent &&
            !hasRestrictedMapContext)
        {
            return;
        }

        if (!hasCombatIntent ||
            IsNormalWorldTravelAction(currentAction))
        {
            bool clearedNormalWorldAction =
                IsNormalWorldTravelAction(currentAction);
            ReleaseMonsterReservation();
            ClearTaskProviderVisitState();
            ClearCultivationTravelState();
            ClearTravelTargetsAndStop();
            ClearHelpRequestState();
            StopMonsterRetreat();
            waitingOutsideTreasureLightning = false;
            hasTreasureWaitPosition = false;
            treasureWaitLowPowerSkirmish = false;
            treasureHuntTarget = null;
            treasureHuntItem = null;
            hasHomeReturnTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            movementPausedUntil = 0f;
            crowdYieldUntil = 0f;
            actionTimer = 0f;
            if (!hasCombatIntent)
            {
                currentMonsterTarget = null;
                ClearMonsterCombatState();
            }

            if (!hasCombatIntent ||
                clearedNormalWorldAction)
            {
                currentAction = NpcText.Action("idle");
            }
        }
    }

    public void ClearSmartTask()
    {
        SmartAITask clearedTask =
            currentSmartTask != null
                ? currentSmartTask.Clone()
                : null;
        currentSmartTask = new SmartAITask();
        if (clearedTask != null &&
            clearedTask.IsValid)
        {
            DebugFlow(
                "TaskComplete",
                "Cleared " + DescribeTask(clearedTask) +
                " returnSchedule=" + GetCurrentScheduleActivityDebug());
        }
    }

    public void ClearSmartTaskIfGoal(SmartAITaskGoal goal)
    {
        if (currentSmartTask == null ||
            currentSmartTask.goal != goal)
        {
            return;
        }

        ClearSmartTask();
    }

    public void ClearScheduledTask()
    {
        EnsureSmartTaskState();

        SmartAITask previousScheduledTask =
            scheduleSmartTask != null
                ? scheduleSmartTask.Clone()
                : null;
        bool shouldClearCurrentTask =
            previousScheduledTask != null &&
            previousScheduledTask.IsValid &&
            IsSameTask(currentSmartTask, previousScheduledTask);

        scheduleSmartTask = new SmartAITask();
        DebugFlow(
            "TaskComplete",
            "Cleared scheduled task " +
            DescribeTask(previousScheduledTask));

        if (shouldClearCurrentTask)
        {
            ClearSmartTask();
        }
    }

    public void ClearEmergencyTaskIfMatches(SmartAITaskGoal goal)
    {
        if (currentSmartTask == null ||
            currentSmartTask.goal != goal)
        {
            return;
        }

        if (currentSmartTask.priority >= SmartAITaskPriority.Emergency)
        {
            ClearSmartTask();
        }
    }

    bool IsLowHpRecoveryTaskActive()
    {
        return currentSmartTask != null &&
            currentSmartTask.IsValid &&
            currentSmartTask.goal == SmartAITaskGoal.LowHpRecovery;
    }

    int GetLowHpRecoveryClearThreshold()
    {
        return Mathf.Max(
            1,
            Mathf.RoundToInt(AuthoritativeMaxHP * 0.7f));
    }

    bool ShouldStartLowHpRecovery()
    {
        return currentHP > 0 &&
            currentHP <= GetLowHpRecoveryClearThreshold();
    }

    bool BeginLowHpRecoveryFromSafeState(string reason)
    {
        if (!ShouldStartLowHpRecovery())
        {
            return false;
        }

        StopMonsterRetreat();

        bool accepted =
            RequestEmergencyTask(
                SmartAITaskGoal.LowHpRecovery,
                SmartAITaskPriority.Emergency,
                false,
                reason);
        if (!accepted)
        {
            return false;
        }

        if (currentAction != NpcText.Action("goTavern") &&
            currentAction != NpcText.Action("buyPill") &&
            currentAction != NpcText.Action("goCultivatePoint") &&
            currentAction != NpcText.Action("cultivate") &&
            currentAction != NpcText.Action("cultivateAbsorbQi"))
        {
            currentAction = NpcText.Action("injured");
        }

        actionTimer = Mathf.Max(
            actionTimer,
            Mathf.Min(0.35f, thinkDelay));

        DebugFlow(
            "Recovery",
            "Promoted safe state to low hp recovery hp=" +
            currentHP +
            "/" +
            AuthoritativeMaxHP +
            " reason=" +
            reason);
        return true;
    }

    void PrepareForLowHpRecovery()
    {
        bool hadCombatFlow =
            currentMonsterTarget != null ||
            isRetreatingFromMonster ||
            currentAction == NpcText.Action("fleeMonsterArea") ||
            currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);

        ReleaseMonsterReservation(currentMonsterTarget);
        currentMonsterTarget = null;
        ClearMonsterCombatState();
        ClearHelpRequestState();
        StopMonsterRetreat();

        if (!hadCombatFlow)
        {
            return;
        }

        currentTarget = null;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        StopNpcMovement();

        if (currentAction == NpcText.Action("fleeMonsterArea") ||
            currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true))
        {
            currentAction = NpcText.Action("idle");
        }
    }

    bool TryConsumePillForLowHpRecovery()
    {
        int hpBefore = currentHP;
        if (!TryConsumeAvailableRecoveryPill(
                out StatItemData usedPill))
        {
            return false;
        }

        int healed =
            Mathf.Max(
                0,
                currentHP - hpBefore);
        actionTimer = Mathf.Max(
            actionTimer,
            GameHoursToSeconds(0.25f));
        currentAction = NpcText.Action("rest");
        DebugFlow(
            "Recovery",
            "Consumed recovery pill item=" +
            (usedPill != null
                ? usedPill.itemName
                : "unknown") +
            " heal=" +
            healed +
            " hp=" +
            currentHP +
            "/" +
            maxHP);
        return true;
    }

    bool TryCultivateForLowHpRecovery()
    {
        if (!canCultivate ||
            waitingForHeavenlyTribulation ||
            IsRestrictedMapSessionActive())
        {
            return false;
        }

        CultivateNaturally();

        if (currentAction == NpcText.Action("goCultivatePoint"))
        {
            return true;
        }

        if (currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi"))
        {
            int healAmount =
                Mathf.Max(
                    8,
                    Mathf.RoundToInt(AuthoritativeMaxHP * 0.14f));
            Heal(healAmount);
            actionTimer = Mathf.Max(
                actionTimer,
                GameHoursToSeconds(0.75f));
            DebugFlow(
                "Recovery",
                "Cultivation healing heal=" + healAmount +
                " hp=" + currentHP + "/" + maxHP);
            return true;
        }

        return false;
    }

    bool TryRecoverAtHomeForLowHp()
    {
        if (homePoint == null)
        {
            return false;
        }

        Vector3 homePosition = GetApproachPosition(homePoint);
        float arriveDistance =
            Mathf.Max(targetClearRadius * 2f, 0.45f);
        float distance =
            Vector2.Distance(transform.position, homePosition);

        if (distance > arriveDistance)
        {
            ClearTravelTargets();
            currentTarget = homePoint;
            hasWanderTarget = false;
            currentAction = NpcText.Action("goHomeRest");
            return true;
        }

        ClearTravelTargetsAndStop();

        if (currentHP <= Mathf.Max(1, AuthoritativeMaxHP / 4) ||
            fatigue >= 60f)
        {
            Sleep();
            return true;
        }

        int healAmount =
            Mathf.Max(
                10,
                Mathf.RoundToInt(AuthoritativeMaxHP * 0.18f));
        Heal(healAmount);
        fatigue = Mathf.Max(0f, fatigue - 20f);
        currentAction = NpcText.Action("restNearHome");
        actionTimer = Mathf.Max(
            actionTimer,
            GameHoursToSeconds(1f));
        DebugFlow(
            "Recovery",
            "Rest at home heal=" + healAmount +
            " hp=" + currentHP + "/" + maxHP);
        return true;
    }

    bool RequestSmartTask(
        SmartAITaskGoal goal,
        SmartAITaskPriority priority,
        bool canBeInterrupted,
        string reason,
        bool isScheduleTask)
    {
        if (goal == SmartAITaskGoal.None)
        {
            DebugFlow("TaskOverride", "Rejected empty goal request");
            return false;
        }

        EnsureSmartTaskState();

        SmartAITask nextTask = new SmartAITask
        {
            goal = goal,
            priority = priority,
            canBeInterrupted = canBeInterrupted,
            reason = string.IsNullOrWhiteSpace(reason)
                ? goal.ToString()
                : reason,
            createdTime = Time.time
        };

        if (currentSmartTask != null &&
            currentSmartTask.IsValid &&
            currentSmartTask.goal == nextTask.goal &&
            currentSmartTask.priority == nextTask.priority &&
            currentSmartTask.canBeInterrupted == nextTask.canBeInterrupted &&
            string.Equals(
                currentSmartTask.reason,
                nextTask.reason,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (isScheduleTask)
        {
            scheduleSmartTask = nextTask.Clone();

            if (currentSmartTask != null &&
                currentSmartTask.priority > SmartAITaskPriority.Normal &&
                currentSmartTask.goal != goal)
            {
                DebugFlow(
                    "TaskOverride",
                    "Rejected schedule " + DescribeTask(nextTask) +
                    " because active=" + DescribeTask(currentSmartTask));
                return false;
            }
        }

        if (currentSmartTask == null ||
            !currentSmartTask.IsValid)
        {
            currentSmartTask = nextTask;
            if (nextTask.goal == SmartAITaskGoal.LowHpRecovery)
            {
                PrepareForLowHpRecovery();
            }
            DebugFlow(
                "TaskOverride",
                "Accepted " + DescribeTask(nextTask) +
                " previous=None");
            return true;
        }

        bool canReplace =
            priority > currentSmartTask.priority ||
            (priority == currentSmartTask.priority &&
            currentSmartTask.canBeInterrupted);
        if (canReplace)
        {
            SmartAITask previousTask = currentSmartTask.Clone();
            currentSmartTask = nextTask;
            if (nextTask.goal == SmartAITaskGoal.LowHpRecovery)
            {
                PrepareForLowHpRecovery();
            }
            DebugFlow(
                "TaskOverride",
                "Accepted " + DescribeTask(nextTask) +
                " previous=" + DescribeTask(previousTask));
            return true;
        }

        DebugFlow(
            "TaskOverride",
            "Denied " + DescribeTask(nextTask) +
            " active=" + DescribeTask(currentSmartTask));
        return false;
    }

    bool TryHandleSmartTaskOverride()
    {
        EnsureSmartTaskState();

        if (currentSmartTask == null ||
            !currentSmartTask.IsValid)
        {
            return false;
        }

        if (!NpcMapBehaviorPolicy.AllowsSchedule(gameObject) &&
            !IsMapCombatTaskGoal(currentSmartTask.goal))
        {
            return false;
        }

        switch (currentSmartTask.goal)
        {
            case SmartAITaskGoal.LowHpRecovery:
                if (currentHP > GetLowHpRecoveryClearThreshold())
                {
                    ClearEmergencyTaskIfMatches(SmartAITaskGoal.LowHpRecovery);
                    return false;
                }

                PrepareForLowHpRecovery();

                if (HasAvailableRecoveryPills() &&
                    TryConsumePillForLowHpRecovery())
                {
                    return true;
                }

                if (canTrade &&
                    money >= 50 &&
                    GoToTavernAndBuyHealingPill())
                {
                    return true;
                }

                if (TryCultivateForLowHpRecovery())
                {
                    return true;
                }

                if (currentMonsterTarget != null &&
                    currentMonsterTarget.currentHP > 0)
                {
                    if (CombatPowerUtility.ShouldRetreat(
                            gameObject,
                            currentMonsterTarget.gameObject))
                    {
                        RequestHelpForMonster(currentMonsterTarget);
                        if (TryBeginMonsterRetreat(currentMonsterTarget))
                        {
                            return true;
                        }
                    }

                    SearchMonster();
                    return true;
                }

                if (TryRecoverAtHomeForLowHp())
                {
                    return true;
                }

                if (IsRecoveringFromDamage)
                {
                    currentAction = NpcText.Action("injured");
                    actionTimer = Mathf.Max(
                        actionTimer,
                        Mathf.Min(0.35f, thinkDelay));
                    return true;
                }

                Sleep();
                return true;

            case SmartAITaskGoal.NeedPotion:
                if (HasAvailablePills())
                {
                    ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
                    return false;
                }

                if (money >= 50)
                {
                    if (IsNeedPotionRetryCoolingDown())
                    {
                        return false;
                    }

                    if (!GoToTavernAndBuyPill())
                    {
                        StartIdleWander();
                    }
                }
                else
                {
                    ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
                }

                return true;

            case SmartAITaskGoal.CriticalBreakthrough:
                Cultivate();
                return true;

            case SmartAITaskGoal.Combat:
            case SmartAITaskGoal.Pursued:
                if (TryContinueFrontierDefenseTravel())
                {
                    return true;
                }

                if (TryHandleCombatSupport())
                {
                    return true;
                }

                if (canFight && canCompeteResource)
                {
                    SearchMonster();
                    return true;
                }

                return false;

            case SmartAITaskGoal.Treasure:
                if (waitingOutsideTreasureLightning ||
                    treasureHuntTarget != null ||
                    hasTreasureWaitPosition)
                {
                    return true;
                }

                ClearEmergencyTaskIfMatches(SmartAITaskGoal.Treasure);
                return false;

            default:
                return false;
        }
    }

    void EnsureSmartTaskState()
    {
        if (currentSmartTask == null)
        {
            currentSmartTask = new SmartAITask();
        }

        if (scheduleSmartTask == null)
        {
            scheduleSmartTask = new SmartAITask();
        }
    }

    string DescribeTask(SmartAITask task)
    {
        if (task == null ||
            !task.IsValid)
        {
            return "None";
        }

        return task.goal +
            "/" + task.priority +
            "/interrupt=" + task.canBeInterrupted +
            "/reason=" + task.reason;
    }

    bool IsSameTask(SmartAITask first, SmartAITask second)
    {
        if (first == null ||
            second == null)
        {
            return false;
        }

        return first.IsValid &&
            second.IsValid &&
            first.goal == second.goal &&
            first.priority == second.priority &&
            first.canBeInterrupted == second.canBeInterrupted &&
            string.Equals(
                first.reason,
                second.reason,
                StringComparison.OrdinalIgnoreCase);
    }

    public string GetPlayerActionText()
    {
        if (IsDead)
        {
            return NpcText.Action("dead");
        }

        if (waitingForHeavenlyTribulation ||
            readyForHeavenlyTribulation ||
            (characterStats != null &&
            characterStats.waitingForHeavenlyTribulation))
        {
            return NpcText.Action("waitTribulation");
        }

        string action =
            NormalizeDisplayAction(
                currentAction,
                !ShouldKeepCurrentActionWithoutTravelContext(currentAction));
        if (!string.IsNullOrWhiteSpace(action))
        {
            return RuntimeStatusText.Translate(action);
        }

        string taskAction =
            NormalizeDisplayAction(
                GetTaskActionTextForDisplay(currentSmartTask),
                false);
        if (!string.IsNullOrWhiteSpace(taskAction))
        {
            return RuntimeStatusText.Translate(taskAction);
        }

        taskAction =
            NormalizeDisplayAction(
                GetTaskActionTextForDisplay(scheduleSmartTask),
                false);
        if (!string.IsNullOrWhiteSpace(taskAction))
        {
            return RuntimeStatusText.Translate(taskAction);
        }

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        if (schedule != null &&
            schedule.enforceSchedule)
        {
            string scheduleAction =
                NormalizeDisplayAction(
                    GetScheduleActionTextForDisplay(
                        schedule.CurrentActivity),
                    false);
            if (!string.IsNullOrWhiteSpace(scheduleAction))
            {
                return RuntimeStatusText.Translate(scheduleAction);
            }
        }

        if (currentMonsterTarget != null ||
            isRetreatingFromMonster)
        {
            return NpcText.Action("goHunt");
        }

        if (hasWanderTarget)
        {
            return NpcText.Action("walkingRoad");
        }

        string routineAction = GetDailyRoutineActionTextForDisplay();
        if (!string.IsNullOrWhiteSpace(routineAction))
        {
            return routineAction;
        }

        return NpcText.Action("idle");
    }

    bool ShouldKeepCurrentActionWithoutTravelContext(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        if (action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("checkedVanBaoLau"))
        {
            return true;
        }

        return IsStationaryAction(action);
    }

    string GetDailyRoutineActionTextForDisplay()
    {
        if (!dailyRoutineEnabled ||
            !NpcMapBehaviorPolicy.AllowsSchedule(gameObject))
        {
            return "";
        }

        if (CanVisitTaskProviderToday())
        {
            return NpcText.Action("goTaskProviderDaily");
        }

        if (canCultivate &&
            IsScheduledCultivationTime())
        {
            return NpcText.Action("cultivate");
        }

        return "";
    }

    string NormalizeDisplayAction(
        string action,
        bool requireActiveContext = true)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        string waitScheduleAction;
        if (TryGetWaitScheduleDisplayAction(action, out waitScheduleAction))
        {
            return waitScheduleAction;
        }

        if (IsStaleRecoveryAction(action))
        {
            return "";
        }

        if (!NpcMapBehaviorPolicy.AllowsNormalWorldTravel(gameObject) &&
            IsNormalWorldTravelAction(action))
        {
            return "";
        }

        if (IsIdleLikeDisplayAction(action))
        {
            return "";
        }

        if (requireActiveContext &&
            IsTravelIntentAction(action) &&
            !HasActiveTravelContext())
        {
            return "";
        }

        if (requireActiveContext &&
            IsGatherDisplayAction(action) &&
            !HasActiveGatherContext())
        {
            return "";
        }

        if (requireActiveContext &&
            IsHuntDisplayAction(action) &&
            !HasActiveHuntContext())
        {
            return "";
        }

        return action;
    }

    bool IsIdleLikeDisplayAction(string action)
    {
        return action == NpcText.Action("idle") ||
            action == NpcText.Action("rest") ||
            action == NpcText.Action("restNearHome") ||
            action == NpcText.Action("restVillageNoon") ||
            action == NpcText.Action("stayNearHome") ||
            action == NpcText.Action("calm");
    }

    bool TryGetWaitScheduleDisplayAction(
        string action,
        out string displayAction)
    {
        displayAction = "";
        const string prefix = "waitSchedule";
        if (string.IsNullOrWhiteSpace(action) ||
            !action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string activityName = action.Substring(prefix.Length);
        NpcScheduleActivity activity;
        if (!Enum.TryParse(activityName, out activity))
        {
            return false;
        }

        displayAction = GetScheduleActionTextForDisplay(activity);
        return !string.IsNullOrWhiteSpace(displayAction);
    }

    bool IsTravelIntentAction(string action)
    {
        return action == NpcText.Action("goTaskProviderDaily") ||
            action == NpcText.Action("goVanBaoLauBroker") ||
            action == NpcText.Action("goVanBaoLauTask") ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("goTavern") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("goCultivatePoint") ||
            action == NpcText.Action("goMarketTrade") ||
            action == NpcText.Action("goWorkTask") ||
            action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("pickItem") ||
            action == NpcText.Action("pickHuntEvidence") ||
            action == NpcText.Action("fleeMonsterArea") ||
            action == NpcText.Action("guardSpiritHerbMonster") ||
            action == NpcText.Action("fightBlockingMonster") ||
            action == NpcText.Action("clearHarvestMonster") ||
            action == NpcText.Action("treasureHuntNamed") ||
            action == NpcText.Action("outerSkirmishNamed") ||
            action == NpcText.Action("walkingRoad") ||
            IsTeleportRouteAction(action);
    }

    bool IsGatherDisplayAction(string action)
    {
        return action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("pickItem") ||
            action == NpcText.Action("pickHuntEvidence");
    }

    bool IsHuntDisplayAction(string action)
    {
        return action == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true) ||
            action == NpcText.Action("fleeMonsterArea") ||
            action == NpcText.Action("guardSpiritHerbMonster") ||
            action == NpcText.Action("fightBlockingMonster") ||
            action == NpcText.Action("clearHarvestMonster") ||
            action == NpcText.Action("treasureHuntNamed") ||
            action == NpcText.Action("outerSkirmishNamed");
    }

    bool IsNormalWorldTravelAction(string action)
    {
        return NpcMapBehaviorPolicy.IsNormalWorldTravelAction(action);
    }

    bool IsMapCombatTaskGoal(SmartAITaskGoal goal)
    {
        return NpcMapBehaviorPolicy.IsAllowedCombatTask(goal);
    }

    bool HasActiveTravelContext()
    {
        return currentTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget ||
            currentMonsterTarget != null;
    }

    bool HasActiveGatherContext()
    {
        return resourceGatherer != null &&
            resourceGatherer.HasActiveGatheringFlow ||
            currentTarget != null ||
            hasWanderTarget;
    }

    bool HasActiveHuntContext()
    {
        return currentMonsterTarget != null ||
            currentTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget;
    }

    string GetTaskActionTextForDisplay(SmartAITask task)
    {
        if (task == null ||
            !task.IsValid)
        {
            return "";
        }

        if (!NpcMapBehaviorPolicy.AllowsSchedule(gameObject) &&
            !IsMapCombatTaskGoal(task.goal))
        {
            return "";
        }

        switch (task.goal)
        {
            case SmartAITaskGoal.Cultivate:
                return NpcText.Action("cultivate");

            case SmartAITaskGoal.DoMission:
                return NpcText.Action("goTaskProviderDaily");

            case SmartAITaskGoal.FreeHuntAndGather:
                return currentMonsterTarget != null ||
                    isRetreatingFromMonster
                    ? NpcText.Action("goHunt")
                    : NpcText.Action("gatherResource");

            case SmartAITaskGoal.TradeBuySell:
                return NpcText.Action("tradeSeek");

            case SmartAITaskGoal.Combat:
            case SmartAITaskGoal.Pursued:
                return NpcText.Action("goHunt");

            case SmartAITaskGoal.LowHpRecovery:
                return NpcText.Action("rest");

            case SmartAITaskGoal.HeavenlyGift:
                return NpcText.Action("idle");

            case SmartAITaskGoal.Treasure:
                return NpcText.Action("treasureHunt");

            case SmartAITaskGoal.NeedPotion:
            {
                NpcCounterBroker broker =
                    NpcCounterBroker.FindBestBrokerForNpc(gameObject);
                if (broker != null &&
                    broker.receiveAllNpcRequests)
                {
                    NpcMapZone? currentZone =
                        NpcMapNavigator.ResolveActorZone(gameObject);
                    NpcMapZone? brokerZone =
                        ResolveBrokerTargetZone(broker);

                    if (currentZone.HasValue &&
                        brokerZone.HasValue &&
                        currentZone.Value == brokerZone.Value)
                    {
                        return NpcText.Action("tradeSeek");
                    }

                    return NpcText.Action("goVanBaoLauBroker");
                }

                return NpcText.Action("buyPill");
            }

            case SmartAITaskGoal.CriticalBreakthrough:
                return NpcText.Action("cultivateAbsorbQi");

            default:
                return "";
        }
    }

    string GetScheduleActionTextForDisplay(NpcScheduleActivity activity)
    {
        if (!NpcMapBehaviorPolicy.AllowsSchedule(gameObject))
        {
            return "";
        }

        switch (activity)
        {
            case NpcScheduleActivity.Idle:
                return NpcText.Action("idle");
            case NpcScheduleActivity.Sleep:
                return NpcText.Action("sleep");
            case NpcScheduleActivity.Eat:
                return NpcText.Action("eating");
            case NpcScheduleActivity.Work:
                return NpcText.Action("working");
            case NpcScheduleActivity.SellGoods:
            case NpcScheduleActivity.BuyGoods:
            case NpcScheduleActivity.TradeBuySell:
                return NpcText.Action("tradeSeek");
            case NpcScheduleActivity.Gather:
                return NpcText.Action("gatherResource");
            case NpcScheduleActivity.Hunt:
                return NpcText.Action("goHunt");
            case NpcScheduleActivity.Cultivate:
                return NpcText.Action("cultivate");
            case NpcScheduleActivity.Alchemy:
            case NpcScheduleActivity.Forge:
                return NpcText.Action("workingTask");
            case NpcScheduleActivity.TakeTask:
            case NpcScheduleActivity.DoMission:
                return NpcText.Action("goTaskProviderDaily");
            case NpcScheduleActivity.ReturnHome:
                return NpcText.Action("goHomeRest");
            case NpcScheduleActivity.FreeHuntAndGather:
                return currentMonsterTarget != null
                    ? NpcText.Action("goHunt")
                    : NpcText.Action("gatherResource");
            default:
                return "";
        }
    }

    string GetCurrentScheduleActivityDebug()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        return schedule != null
            ? schedule.CurrentActivity.ToString()
            : "None";
    }
}
