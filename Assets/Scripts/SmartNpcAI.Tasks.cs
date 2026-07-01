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
            DebugFlow(
                "TaskOverride",
                "Accepted " + DescribeTask(nextTask) +
                " previous=None");
            return true;
        }

        if (currentSmartTask.priority < priority ||
            currentSmartTask.canBeInterrupted)
        {
            SmartAITask previousTask = currentSmartTask.Clone();
            currentSmartTask = nextTask;
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

        switch (currentSmartTask.goal)
        {
            case SmartAITaskGoal.LowHpRecovery:
                if (currentHP > Mathf.Max(1, maxHP / 2))
                {
                    ClearEmergencyTaskIfMatches(SmartAITaskGoal.LowHpRecovery);
                    return false;
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
            case SmartAITaskGoal.SupportAlly:
            case SmartAITaskGoal.Pursued:
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

    public string GetPlayerActionText()
    {
        if (IsDead)
        {
            return NpcText.Action("dead");
        }

        string action = NormalizeDisplayAction(currentAction);
        if (!string.IsNullOrWhiteSpace(action))
        {
            return action;
        }

        string taskAction =
            NormalizeDisplayAction(
                GetTaskActionTextForDisplay(currentSmartTask));
        if (!string.IsNullOrWhiteSpace(taskAction))
        {
            return taskAction;
        }

        taskAction =
            NormalizeDisplayAction(
                GetTaskActionTextForDisplay(scheduleSmartTask));
        if (!string.IsNullOrWhiteSpace(taskAction))
        {
            return taskAction;
        }

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        if (schedule != null &&
            schedule.enforceSchedule)
        {
            string scheduleAction =
                NormalizeDisplayAction(
                    GetScheduleActionTextForDisplay(
                        schedule.CurrentActivity));
            if (!string.IsNullOrWhiteSpace(scheduleAction))
            {
                return scheduleAction;
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

        return NpcText.Action("idle");
    }

    string NormalizeDisplayAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        if (IsIdleLikeDisplayAction(action))
        {
            return "";
        }

        if (IsTravelIntentAction(action) &&
            !HasActiveTravelContext())
        {
            return "";
        }

        if (IsGatherDisplayAction(action) &&
            !HasActiveGatherContext())
        {
            return "";
        }

        if (IsHuntDisplayAction(action) &&
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
            action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("checkedVanBaoLau") ||
            action == NpcText.Action("calm") ||
            action.StartsWith("waitSchedule", StringComparison.OrdinalIgnoreCase);
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

        switch (task.goal)
        {
            case SmartAITaskGoal.Cultivate:
                return NpcText.Action("cultivate");

            case SmartAITaskGoal.DoMission:
            case SmartAITaskGoal.SupportAlly:
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
                return NpcText.Action("buyPill");

            case SmartAITaskGoal.CriticalBreakthrough:
                return NpcText.Action("cultivateAbsorbQi");

            default:
                return "";
        }
    }

    string GetScheduleActionTextForDisplay(NpcScheduleActivity activity)
    {
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
