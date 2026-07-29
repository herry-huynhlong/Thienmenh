using UnityEngine;

public partial class NpcTaskProvider
{
    const string LinhRicePlantTaskId = "linh_rice_plant";
    const string LinhRiceCareTaskId = "linh_rice_care";
    const string LinhRiceHarvestTaskId = "linh_rice_harvest";

    bool IsLinhRiceVillageTask(NpcTaskOffer offer)
    {
        if (offer == null ||
            offer.taskType != NpcTaskType.GatherResource)
        {
            return false;
        }

        return IsLinhRiceVillageTaskId(offer.customTaskId);
    }

    bool IsLinhRiceVillageTask(RunningNpcTask task)
    {
        return task != null &&
            IsLinhRiceVillageTask(task.offer);
    }

    bool IsLinhRicePlantTask(NpcTaskOffer offer)
    {
        return IsTaskId(offer, LinhRicePlantTaskId);
    }

    bool IsLinhRiceCareTask(NpcTaskOffer offer)
    {
        return IsTaskId(offer, LinhRiceCareTaskId);
    }

    bool IsLinhRiceHarvestTask(NpcTaskOffer offer)
    {
        return IsTaskId(offer, LinhRiceHarvestTaskId);
    }

    bool IsLinhRicePlantTask(RunningNpcTask task)
    {
        return task != null &&
            IsLinhRicePlantTask(task.offer);
    }

    bool IsLinhRiceCareTask(RunningNpcTask task)
    {
        return task != null &&
            IsLinhRiceCareTask(task.offer);
    }

    bool IsLinhRiceHarvestTask(RunningNpcTask task)
    {
        return task != null &&
            IsLinhRiceHarvestTask(task.offer);
    }

    bool IsLinhRiceVillageTaskId(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return false;
        }

        string normalized = taskId.Trim();
        return normalized == LinhRicePlantTaskId ||
            normalized == LinhRiceCareTaskId ||
            normalized == LinhRiceHarvestTaskId;
    }

    bool IsTaskId(NpcTaskOffer offer, string taskId)
    {
        return offer != null &&
            string.Equals(
                offer.customTaskId,
                taskId,
                System.StringComparison.OrdinalIgnoreCase);
    }

    string GetLinhRiceVillageTaskName(NpcTaskOffer offer)
    {
        if (IsLinhRicePlantTask(offer))
        {
            return "Trồng Linh Mễ";
        }

        if (IsLinhRiceCareTask(offer))
        {
            return "Chăm sóc Linh Mễ";
        }

        if (IsLinhRiceHarvestTask(offer))
        {
            return "Thu hoạch Linh Mễ";
        }

        return string.Empty;
    }

    string GetLinhRiceVillageTaskTypeLabel(NpcTaskOffer offer)
    {
        if (IsLinhRicePlantTask(offer))
        {
            return "Trồng";
        }

        if (IsLinhRiceCareTask(offer))
        {
            return "Chăm sóc";
        }

        if (IsLinhRiceHarvestTask(offer))
        {
            return "Thu hoạch";
        }

        return string.Empty;
    }

    string GetLinhRiceVillageObjectiveLabel(NpcTaskOffer offer)
    {
        if (IsLinhRicePlantTask(offer))
        {
            return "Trồng";
        }

        if (IsLinhRiceCareTask(offer))
        {
            return "Chăm sóc";
        }

        if (IsLinhRiceHarvestTask(offer))
        {
            return "Linh Mễ";
        }

        return string.Empty;
    }

    string GetLinhRiceVillageStatusItemLabel(NpcTaskOffer offer)
    {
        return TaskDisplay("linhRice");
    }

    string GetLinhRiceVillageWorkActionKey(NpcTaskOffer offer)
    {
        if (IsLinhRicePlantTask(offer))
        {
            return "plantingItem";
        }

        if (IsLinhRiceCareTask(offer))
        {
            return "caringItem";
        }

        return "harvestingItem";
    }

    Vector3 GetLinhRiceVillageWorkPosition()
    {
        if (linhRiceFieldPoint != null)
        {
            return linhRiceFieldPoint.position;
        }

        FarmFieldManager fieldManager = ResolveFarmFieldManager();
        if (fieldManager != null &&
            fieldManager.plots != null)
        {
            for (int i = 0; i < fieldManager.plots.Count; i++)
            {
                FarmPlot plot = fieldManager.plots[i];
                if (plot != null)
                {
                    return plot.transform.position;
                }
            }
        }

        return gatherPoint != null
            ? gatherPoint.position
            : GetFallbackWorkPosition();
    }

    FarmFieldManager ResolveFarmFieldManager()
    {
        FarmFieldManager fieldManager =
            linhRiceFieldPoint != null
                ? linhRiceFieldPoint.GetComponentInParent<FarmFieldManager>()
                : null;
        if (fieldManager != null)
        {
            if (fieldManager.autoRefreshPlotCache &&
                (fieldManager.plots == null || fieldManager.plots.Count == 0))
            {
                fieldManager.RefreshPlotCache();
            }

            return fieldManager;
        }

        fieldManager =
            FindFirstObjectByType<FarmFieldManager>(
                FindObjectsInactive.Exclude);
        if (fieldManager != null &&
            fieldManager.autoRefreshPlotCache &&
            (fieldManager.plots == null || fieldManager.plots.Count == 0))
        {
            fieldManager.RefreshPlotCache();
        }

        return fieldManager;
    }

    bool HasAvailableLinhRiceVillageTask(
        NpcTaskOffer offer,
        GameObject npc = null)
    {
        FarmFieldManager fieldManager = ResolveFarmFieldManager();
        if (fieldManager == null)
        {
            return false;
        }

        if (IsLinhRicePlantTask(offer))
        {
            return fieldManager.FindNearestPlotForPlant(
                GetLinhRiceVillageSearchOrigin(npc),
                npc) != null;
        }

        if (IsLinhRiceHarvestTask(offer))
        {
            return fieldManager.FindNearestPlotForHarvest(
                GetLinhRiceVillageSearchOrigin(npc),
                npc) != null;
        }

        if (!IsLinhRiceCareTask(offer) ||
            fieldManager.plots == null)
        {
            return false;
        }

        for (int i = 0; i < fieldManager.plots.Count; i++)
        {
            FarmPlot plot = fieldManager.plots[i];
            if (plot == null ||
                plot.IsReservedByOther(npc) ||
                !plot.CanPerformSupportAction(
                    FarmPlotSupportAction.Care))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    Vector3 GetLinhRiceVillageSearchOrigin(GameObject npc)
    {
        if (npc != null)
        {
            return npc.transform.position;
        }

        if (linhRiceFieldPoint != null)
        {
            return linhRiceFieldPoint.position;
        }

        return transform.position;
    }

    bool TryAssignFarmPlotForTask(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null ||
            !IsLinhRiceVillageTask(task.offer))
        {
            return false;
        }

        if (IsAssignedFarmPlotStillValid(task))
        {
            RefreshFarmPlotReservation(task);
            task.workPosition = task.farmPlot.transform.position;
            return true;
        }

        ReleaseFarmPlotReservation(task);

        FarmFieldManager fieldManager = ResolveFarmFieldManager();
        if (fieldManager == null)
        {
            task.workPosition = GetLinhRiceVillageWorkPosition();
            return false;
        }

        if (IsLinhRicePlantTask(task))
        {
            if (fieldManager.TryReserveNearestPlotForPlant(
                    GetLinhRiceVillageSearchOrigin(task.npc),
                    task.npc,
                    out FarmPlot plantPlot))
            {
                task.farmPlot = plantPlot;
            }
        }
        else if (IsLinhRiceHarvestTask(task))
        {
            if (fieldManager.TryReserveNearestPlotForHarvest(
                    GetLinhRiceVillageSearchOrigin(task.npc),
                    task.npc,
                    out FarmPlot harvestPlot))
            {
                task.farmPlot = harvestPlot;
            }
        }
        else if (IsLinhRiceCareTask(task) &&
            TryReserveNearestSupportPlot(
                fieldManager,
                task.npc,
                FarmPlotSupportAction.Care,
                out FarmPlot supportPlot))
        {
            task.farmPlot = supportPlot;
        }

        if (task.farmPlot == null)
        {
            task.workPosition = GetLinhRiceVillageWorkPosition();
            return false;
        }

        task.workPosition = task.farmPlot.transform.position;
        return true;
    }

    bool TryReserveNearestSupportPlot(
        FarmFieldManager fieldManager,
        GameObject requester,
        FarmPlotSupportAction action,
        out FarmPlot reservedPlot)
    {
        reservedPlot = null;
        if (fieldManager == null ||
            requester == null ||
            fieldManager.plots == null)
        {
            return false;
        }

        FarmPlot bestPlot = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < fieldManager.plots.Count; i++)
        {
            FarmPlot plot = fieldManager.plots[i];
            if (plot == null ||
                plot.IsReservedByOther(requester) ||
                !plot.CanPerformSupportAction(action))
            {
                continue;
            }

            float distance =
                (plot.transform.position - requester.transform.position)
                .sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPlot = plot;
            }
        }

        if (bestPlot == null ||
            !bestPlot.TryReserveForSupport(
                requester,
                GetFarmPlotReservationSeconds(),
                action))
        {
            return false;
        }

        reservedPlot = bestPlot;
        return true;
    }

    bool IsAssignedFarmPlotStillValid(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.farmPlot == null)
        {
            return false;
        }

        if (task.farmPlot.IsReservedByOther(task.npc))
        {
            return false;
        }

        if (IsLinhRicePlantTask(task))
        {
            return task.farmPlot.CanPlantNow;
        }

        if (IsLinhRiceCareTask(task))
        {
            return task.farmPlot.CanPerformSupportAction(
                FarmPlotSupportAction.Care);
        }

        if (IsLinhRiceHarvestTask(task))
        {
            return task.farmPlot.CanHarvestNow;
        }

        return false;
    }

    float GetFarmPlotReservationSeconds()
    {
        FarmFieldManager fieldManager = ResolveFarmFieldManager();
        if (fieldManager != null)
        {
            return Mathf.Max(
                0.25f,
                fieldManager.defaultReservationSeconds);
        }

        return 8f;
    }

    void ReleaseFarmPlotReservation(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        if (task.farmPlot != null)
        {
            task.farmPlot.ReleaseReservation(task.npc);
        }

        task.farmPlot = null;
    }

    void RefreshFarmPlotReservation(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.farmPlot == null)
        {
            return;
        }

        float durationSeconds = GetFarmPlotReservationSeconds();
        if (task.farmPlot.RefreshReservation(
                task.npc,
                durationSeconds))
        {
            return;
        }

        if (IsLinhRicePlantTask(task))
        {
            task.farmPlot.TryReserveForPlant(
                task.npc,
                durationSeconds);
            return;
        }

        if (IsLinhRiceHarvestTask(task))
        {
            task.farmPlot.TryReserveForHarvest(
                task.npc,
                durationSeconds);
            return;
        }

        if (IsLinhRiceCareTask(task))
        {
            task.farmPlot.TryReserveForSupport(
                task.npc,
                durationSeconds,
                FarmPlotSupportAction.Care);
        }
    }

    void UpdateLinhRiceVillageTravel(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null)
        {
            return;
        }

        if (HasGatherObjectiveComplete(task))
        {
            ReleaseFarmPlotReservation(task);
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        bool hasPlot = TryAssignFarmPlotForTask(task);
        task.workPosition = hasPlot
            ? task.farmPlot.transform.position
            : GetLinhRiceVillageWorkPosition();
        MoveNpcToWork(task, task.workPosition);

        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat(
                hasPlot
                    ? "goGatherItem"
                    : "searchGatherItem",
                GetTaskRequiredItemName(task),
                BuildGatherProgressText(task)));

        if (UpdateTaskTravelWatchdog(
                task,
                task.workPosition,
                Mathf.Max(arriveDistance, gatherInteractDistance),
                GetWorkZone(task.offer),
                hasPlot
                    ? "LinhRiceVillageTravel"
                    : "LinhRiceVillageSearch"))
        {
            return;
        }

        if (!hasPlot)
        {
            if (Vector2.Distance(
                    task.npc.transform.position,
                    task.workPosition) <= Mathf.Max(
                        arriveDistance,
                        gatherInteractDistance))
            {
                DisarmTaskTravelWatchdog(task);
                NpcRoleUtility.StopForConversation(task.npc, 0.25f);
            }

            return;
        }

        if (Vector2.Distance(
                task.npc.transform.position,
                task.workPosition) <= Mathf.Max(
                    arriveDistance,
                    gatherInteractDistance))
        {
            task.stage = TavernTaskStage.Working;
            task.remainingTime = GetGatherWorkDuration(task);
            DisarmTaskTravelWatchdog(task);
        }
    }

    void UpdateLinhRiceVillageWork(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null)
        {
            return;
        }

        DisarmTaskTravelWatchdog(task);

        if (HasGatherObjectiveComplete(task))
        {
            ReleaseFarmPlotReservation(task);
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        if (!IsAssignedFarmPlotStillValid(task))
        {
            ReleaseFarmPlotReservation(task);
            task.stage = TavernTaskStage.GoingToWork;
            return;
        }

        RefreshFarmPlotReservation(task);
        NpcRoleUtility.StopForConversation(task.npc, 0.35f);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat(
                GetLinhRiceVillageWorkActionKey(task.offer),
                GetLinhRiceVillageStatusItemLabel(task.offer),
                BuildGatherProgressText(task)));

        task.remainingTime -= Time.deltaTime;
        if (task.remainingTime > 0f)
        {
            return;
        }

        bool succeeded = false;

        if (IsLinhRicePlantTask(task))
        {
            succeeded = task.farmPlot.BeginPlanting(task.npc);
            if (succeeded)
            {
                task.collectedAmount++;
            }
        }
        else if (IsLinhRiceCareTask(task))
        {
            succeeded = task.farmPlot.ApplySupportAction(
                FarmPlotSupportAction.Care,
                task.npc,
                out _);
            if (succeeded)
            {
                task.collectedAmount++;
            }
        }
        else if (IsLinhRiceHarvestTask(task))
        {
            succeeded = task.farmPlot.HarvestToActor(
                task.npc,
                out _);
        }

        ReleaseFarmPlotReservation(task);
        task.stage = succeeded && HasGatherObjectiveComplete(task)
            ? TavernTaskStage.ReturningToTurnIn
            : TavernTaskStage.GoingToWork;
    }
}
