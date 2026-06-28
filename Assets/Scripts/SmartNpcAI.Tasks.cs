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
                if (money >= 50)
                {
                    GoToTavernAndBuyPill();
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

    string GetCurrentScheduleActivityDebug()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        return schedule != null
            ? schedule.CurrentActivity.ToString()
            : "None";
    }
}
