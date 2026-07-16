using UnityEngine;

public partial class NpcTaskProvider
{
    void PrepareFrontierWatchDuty(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null)
        {
            return;
        }

        FrontierWatchDutyAgent dutyAgent =
            GetOrCreateFrontierWatchDutyAgent(task);
        if (dutyAgent == null)
        {
            return;
        }

        dutyAgent.BeginDuty(task);
        task.workPosition = dutyAgent.CurrentTarget;
    }

    FrontierWatchDutyAgent GetOrCreateFrontierWatchDutyAgent(
        RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null)
        {
            return null;
        }

        if (task.frontierDutyAgent != null)
        {
            return task.frontierDutyAgent;
        }

        FrontierWatchDutyAgent existing =
            task.npc.GetComponent<FrontierWatchDutyAgent>();
        if (existing == null)
        {
            existing =
                task.npc.AddComponent<FrontierWatchDutyAgent>();
        }

        task.frontierDutyAgent = existing;
        return task.frontierDutyAgent;
    }

    void CleanupFrontierWatchDuty(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        FrontierWatchDutyAgent dutyAgent =
            task.frontierDutyAgent != null
                ? task.frontierDutyAgent
                : task.npc != null
                    ? task.npc.GetComponent<FrontierWatchDutyAgent>()
                    : null;
        if (dutyAgent == null)
        {
            return;
        }

        dutyAgent.StopDuty();

        if (Application.isPlaying)
        {
            Destroy(dutyAgent);
        }
        else
        {
            DestroyImmediate(dutyAgent);
        }

        task.frontierDutyAgent = null;
    }
}
