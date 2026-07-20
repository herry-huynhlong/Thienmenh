using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class VillagerFarmJob : MonoBehaviour
{
    enum FarmTaskKind
    {
        None,
        Plant,
        Harvest,
        Care,
        Water,
        PestControl
    }

    [Header("Farm Job")]
    public bool enabledFarmJob = true;
    public FarmFieldManager fieldManager;
    public bool autoFindFieldManager = true;
    public bool prioritizeHarvest = true;

    [Header("Timing")]
    [Min(0.5f)] public float plantDurationSeconds = 5f;
    [Min(0.5f)] public float harvestDurationSeconds = 10f;
    [Min(0.5f)] public float reservationRefreshSeconds = 6f;
    [Min(0f)] public float arriveDistancePadding = 0.1f;
    [Min(0.5f)] public float standbyActionRefreshSeconds = 3f;
    [Min(0.5f)] public float standbyRadius = 1.8f;
    [Min(0.1f)] public float supportActionDurationFallbackGameHours = 1f;

    [Header("Debug")]
    public NpcJobState currentState = NpcJobState.Idle;
    public bool debugLogs;

    VillagerAI villager;
    ItemInventory inventory;
    FarmPlot currentPlot;
    FarmTaskKind currentTaskKind;
    float workTimer;
    int lastDisplayedSeconds = -1;
    float nextStandbyActionAt;
    string standbyAction;
    Vector3 standbyTarget;
    bool hasStandbyTarget;

    public FarmPlot CurrentPlot => currentPlot;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
        inventory = GetComponent<ItemInventory>();
    }

    void Update()
    {
        if (!enabledFarmJob)
        {
            return;
        }

        if (!HasActiveTask())
        {
            return;
        }

        if (!CanRunJob())
        {
            bool shouldReturnHome =
                villager != null &&
                (villager.ShouldGoHomeForRest() ||
                villager.IsReturningHome);
            CancelCurrentTask();

            if (shouldReturnHome)
            {
                villager.GoHomeToRest();
            }

            return;
        }

        ProcessActiveTask(Time.deltaTime);
    }

    public bool TryRun()
    {
        if (!CanRunJob())
        {
            return false;
        }

        EnsureReferences();

        if (HasActiveTask())
        {
            return true;
        }

        if (TryReserveNextPlot(out FarmPlot reservedPlot, out FarmTaskKind taskKind))
        {
            currentPlot = reservedPlot;
            currentTaskKind = taskKind;
            currentState = NpcJobState.Moving;
            standbyAction = null;
            nextStandbyActionAt = 0f;
            lastDisplayedSeconds = -1;

            villager.ForceJobMoveTo(
                currentPlot.transform.position,
                BuildMoveActionText(taskKind));

            LogDebug(
                "Reserve",
                "task=" + taskKind +
                " plot=" + currentPlot.name);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(standbyAction))
        {
            RefreshStandbyAction();
            return true;
        }

        BeginStandbyCycle();
        return true;
    }

    void BeginStandbyCycle()
    {
        currentPlot = null;
        currentTaskKind = FarmTaskKind.None;
        currentState = NpcJobState.Idle;
        workTimer = 0f;
        lastDisplayedSeconds = -1;
        RefreshStandbyAction(true);
        LogDebug("Standby", "reason=noAvailableFarmTask");
    }

    void ProcessActiveTask(float deltaTime)
    {
        if (currentPlot == null)
        {
            ResetTaskState();
            return;
        }

        if (!RefreshReservation())
        {
            LogDebug("Release", "reason=reservationLost");
            CancelCurrentTask();
            return;
        }

        switch (currentState)
        {
            case NpcJobState.Moving:
                ProcessMoveToPlot();
                break;

            case NpcJobState.Working:
                ProcessWork(deltaTime);
                break;

            default:
                ResetTaskState();
                break;
        }
    }

    void ProcessMoveToPlot()
    {
        if (!IsPlotStillValidForTask())
        {
            LogDebug("Move", "reason=plotNoLongerValid");
            CancelCurrentTask();
            return;
        }

        float distance =
            Vector2.Distance(
                transform.position,
                currentPlot.transform.position);

        if (distance > villager.arriveDistance + arriveDistancePadding)
        {
            villager.ForceJobMoveTo(
                currentPlot.transform.position,
                BuildMoveActionText());
            return;
        }

        villager.StopMoving();
        currentState = NpcJobState.Working;
        workTimer = ResolveTaskDurationSeconds();
        lastDisplayedSeconds = -1;
        UpdateWorkActionText();

        LogDebug(
            "Arrive",
            "task=" + currentTaskKind +
            " timer=" + workTimer.ToString("0.00"));
    }

    void ProcessWork(float deltaTime)
    {
        if (!IsPlotStillValidForTask())
        {
            LogDebug("Work", "reason=plotNoLongerValid");
            CancelCurrentTask();
            return;
        }

        villager.StopMoving();
        workTimer -= deltaTime;
        UpdateWorkActionText();

        if (workTimer > 0f)
        {
            return;
        }

        if (currentTaskKind == FarmTaskKind.Plant)
        {
            bool planted =
                currentPlot.BeginPlanting(gameObject);
            if (planted)
            {
                villager.GainProfessionExpForJob(
                    VillagerJob.Farmer);
                villager.SetActionImmediate("Da trong lua", 0.35f);
                LogDebug("Plant", "result=success");
            }
            else
            {
                LogDebug("Plant", "result=failed");
            }

            CancelCurrentTask(!planted);
            return;
        }

        if (currentTaskKind == FarmTaskKind.Care ||
            currentTaskKind == FarmTaskKind.Water ||
            currentTaskKind == FarmTaskKind.PestControl)
        {
            float reducedHours;
            bool supported =
                currentPlot.ApplySupportAction(
                    ToSupportAction(currentTaskKind),
                    gameObject,
                    out reducedHours);
            if (supported)
            {
                villager.GainProfessionExpForJob(
                    VillagerJob.Farmer);
                villager.SetActionImmediate(
                    BuildSupportCompleteAction(
                        currentTaskKind,
                        reducedHours),
                    0.5f);
                LogDebug(
                    "Support",
                    "task=" + currentTaskKind +
                    " reducedHours=" + reducedHours.ToString("0.00"));
            }
            else
            {
                LogDebug(
                    "Support",
                    "task=" + currentTaskKind +
                    " result=failed");
            }

            CancelCurrentTask(!supported);
            return;
        }

        EnsureInventory();
        int harvestedAmount;
        bool harvested =
            currentPlot.HarvestToActor(gameObject, out harvestedAmount);

        if (harvested)
        {
            string itemName =
                currentPlot.harvestItem != null
                    ? currentPlot.harvestItem.itemName
                    : "nong san";
            int bonusAmount = 0;
            if (currentPlot.harvestItem != null &&
                inventory != null)
            {
                bonusAmount =
                    villager.GetProfessionBonusOutputForJob(
                        VillagerJob.Farmer);

                if (bonusAmount > 0)
                {
                    inventory.AddItem(
                        currentPlot.harvestItem,
                        bonusAmount);
                    harvestedAmount += bonusAmount;
                }
            }

            villager.GainProfessionExpForJob(
                VillagerJob.Farmer);
            villager.SetActionImmediate(
                "Thu hoach " + itemName + " x" + harvestedAmount,
                0.5f);
            LogDebug(
                "Harvest",
                "result=success amount=" + harvestedAmount);
        }
        else
        {
            LogDebug("Harvest", "result=failed");
        }

        CancelCurrentTask(!harvested);
    }

    bool TryReserveNextPlot(
        out FarmPlot reservedPlot,
        out FarmTaskKind taskKind)
    {
        reservedPlot = null;
        taskKind = FarmTaskKind.None;

        if (fieldManager == null)
        {
            return false;
        }

        if (prioritizeHarvest &&
            fieldManager.TryReserveNearestPlotForHarvest(
                transform.position,
                gameObject,
                out reservedPlot))
        {
            taskKind = FarmTaskKind.Harvest;
            return true;
        }

        if (fieldManager.TryReserveNearestPlotForPlant(
                transform.position,
                gameObject,
                out reservedPlot))
        {
            taskKind = FarmTaskKind.Plant;
            return true;
        }

        if (TryReserveNextSupportPlot(
                out reservedPlot,
                out taskKind))
        {
            return true;
        }

        if (!prioritizeHarvest &&
            fieldManager.TryReserveNearestPlotForHarvest(
                transform.position,
                gameObject,
                out reservedPlot))
        {
            taskKind = FarmTaskKind.Harvest;
            return true;
        }

        return false;
    }

    bool CanRunJob()
    {
        return enabledFarmJob &&
            villager != null &&
            villager.enabled &&
            !villager.IsDead &&
            !villager.IsReturningHome &&
            villager.job == VillagerJob.Farmer &&
            !villager.ShouldGoHomeForRest();
    }

    bool HasActiveTask()
    {
        return currentPlot != null &&
            currentTaskKind != FarmTaskKind.None;
    }

    bool RefreshReservation()
    {
        if (currentPlot == null)
        {
            return false;
        }

        return currentPlot.RefreshReservation(
            gameObject,
            Mathf.Max(0.5f, reservationRefreshSeconds));
    }

    bool IsPlotStillValidForTask()
    {
        if (currentPlot == null)
        {
            return false;
        }

        switch (currentTaskKind)
        {
            case FarmTaskKind.Plant:
                return currentState == NpcJobState.Working
                    ? currentPlot.CanPlantNow
                    : currentPlot.CanPlantNow ||
                        currentPlot.ReservedBy == gameObject;

            case FarmTaskKind.Harvest:
                return currentState == NpcJobState.Working
                    ? currentPlot.CanHarvestNow
                    : currentPlot.CanHarvestNow ||
                        currentPlot.ReservedBy == gameObject;

            case FarmTaskKind.Care:
            case FarmTaskKind.Water:
            case FarmTaskKind.PestControl:
                FarmPlotSupportAction supportAction =
                    ToSupportAction(currentTaskKind);
                return currentState == NpcJobState.Working
                    ? currentPlot.CanPerformSupportAction(supportAction)
                    : currentPlot.CanPerformSupportAction(supportAction) ||
                        currentPlot.ReservedBy == gameObject;

            default:
                return false;
        }
    }

    void UpdateWorkActionText()
    {
        int seconds =
            Mathf.CeilToInt(Mathf.Max(0f, workTimer));

        if (seconds == lastDisplayedSeconds)
        {
            return;
        }

        lastDisplayedSeconds = seconds;

        string action =
            BuildWorkingActionText();

        villager.SetActionImmediate(
            action + " (" + seconds + "s)",
            Mathf.Max(0.1f, workTimer));
    }

    void EnsureReferences()
    {
        if (villager == null)
        {
            villager = GetComponent<VillagerAI>();
        }

        EnsureInventory();

        if (fieldManager == null &&
            autoFindFieldManager)
        {
            fieldManager =
                FindAnyObjectByType<FarmFieldManager>(
                    FindObjectsInactive.Exclude);
        }
    }

    void EnsureInventory()
    {
        if (inventory != null)
        {
            return;
        }

        inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
            inventory.shareRuntimeItems = false;
        }
    }

    void RefreshStandbyAction(bool forcePick = false)
    {
        if (villager == null)
        {
            return;
        }

        if (forcePick ||
            string.IsNullOrWhiteSpace(standbyAction) ||
            Time.time >= nextStandbyActionAt)
        {
            standbyAction = BuildStandbyAction();
            nextStandbyActionAt =
                Time.time + Mathf.Max(0.5f, standbyActionRefreshSeconds);
        }

        if (TryRunFarmStandby())
        {
            return;
        }

        villager.SetActionImmediate(
            standbyAction,
            Mathf.Max(0.5f, standbyActionRefreshSeconds));
    }

    string BuildStandbyAction()
    {
        switch (Random.Range(0, 4))
        {
            case 0:
                return "Cham soc Linh Me";
            case 1:
                return "Kiem tra ruong Linh Me";
            case 2:
                return "Theo doi Linh Me phat trien";
            default:
                return "Don co quanh ruong";
        }
    }

    bool TryRunFarmStandby()
    {
        Vector3 anchor = ResolveFarmStandbyAnchor();
        float radius = Mathf.Max(0.75f, standbyRadius);
        float minMoveDistance =
            Mathf.Max(
                villager.arriveDistance * 2f,
                0.5f);

        if (!hasStandbyTarget ||
            Vector2.Distance(standbyTarget, anchor) > radius + 0.1f ||
            Vector2.Distance(transform.position, standbyTarget) <=
                villager.arriveDistance + arriveDistancePadding)
        {
            if (!TryPickStandbyTarget(anchor, radius, minMoveDistance))
            {
                hasStandbyTarget = false;
                return false;
            }
        }

        if (Vector2.Distance(transform.position, standbyTarget) >
            villager.arriveDistance + arriveDistancePadding)
        {
            villager.ForceJobMoveTo(
                standbyTarget,
                standbyAction);
            return true;
        }

        villager.StopMoving();
        villager.SetActionImmediate(
            standbyAction,
            Mathf.Max(0.5f, standbyActionRefreshSeconds));
        return true;
    }

    Vector3 ResolveFarmStandbyAnchor()
    {
        EnsureReferences();

        if (fieldManager != null &&
            fieldManager.plots != null &&
            fieldManager.plots.Count > 0)
        {
            Vector3 nearestPlotPosition = Vector3.zero;
            float bestDistanceSqr = float.PositiveInfinity;
            bool foundPlot = false;

            for (int i = 0; i < fieldManager.plots.Count; i++)
            {
                FarmPlot plot = fieldManager.plots[i];
                if (plot == null)
                {
                    continue;
                }

                float distanceSqr =
                    (plot.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                nearestPlotPosition = plot.transform.position;
                foundPlot = true;
            }

            if (foundPlot)
            {
                return nearestPlotPosition;
            }
        }

        if (fieldManager != null)
        {
            return fieldManager.transform.position;
        }

        if (villager != null &&
            villager.workPoint != null)
        {
            return villager.workPoint.position;
        }

        return transform.position;
    }

    bool TryPickStandbyTarget(
        Vector3 anchor,
        float radius,
        float minMoveDistance)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 randomOffset =
                Random.insideUnitCircle * radius;
            Vector3 candidate =
                anchor + new Vector3(randomOffset.x, randomOffset.y, 0f);

            if (Vector2.Distance(transform.position, candidate) <
                minMoveDistance)
            {
                continue;
            }

            standbyTarget = candidate;
            hasStandbyTarget = true;
            return true;
        }

        standbyTarget = anchor;
        hasStandbyTarget = true;
        return true;
    }

    void CancelCurrentTask(bool releaseReservation = true)
    {
        if (releaseReservation &&
            currentPlot != null)
        {
            currentPlot.ReleaseReservation(gameObject);
        }

        ResetTaskState();
    }

    void ResetTaskState()
    {
        currentPlot = null;
        currentTaskKind = FarmTaskKind.None;
        currentState = NpcJobState.Idle;
        workTimer = 0f;
        lastDisplayedSeconds = -1;
        standbyAction = null;
        nextStandbyActionAt = 0f;
        standbyTarget = Vector3.zero;
        hasStandbyTarget = false;
    }

    bool TryReserveNextSupportPlot(
        out FarmPlot reservedPlot,
        out FarmTaskKind taskKind)
    {
        reservedPlot = null;
        taskKind = FarmTaskKind.None;

        if (fieldManager == null ||
            fieldManager.plots == null ||
            fieldManager.plots.Count == 0)
        {
            return false;
        }

        FarmTaskKind[] supportPriority =
        {
            FarmTaskKind.Care,
            FarmTaskKind.Water,
            FarmTaskKind.PestControl
        };

        int startIndex = Random.Range(0, supportPriority.Length);
        float bestDistanceSqr = float.PositiveInfinity;

        for (int offset = 0; offset < supportPriority.Length; offset++)
        {
            FarmTaskKind candidateTask =
                supportPriority[(startIndex + offset) % supportPriority.Length];
            FarmPlotSupportAction supportAction =
                ToSupportAction(candidateTask);

            for (int i = 0; i < fieldManager.plots.Count; i++)
            {
                FarmPlot plot = fieldManager.plots[i];
                if (plot == null ||
                    !plot.CanPerformSupportAction(supportAction) ||
                    plot.IsReservedByOther(gameObject))
                {
                    continue;
                }

                float distanceSqr =
                    (plot.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                if (!plot.TryReserveForSupport(
                        gameObject,
                        Mathf.Max(0.5f, reservationRefreshSeconds),
                        supportAction))
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                reservedPlot = plot;
                taskKind = candidateTask;
            }

            if (reservedPlot != null)
            {
                return true;
            }
        }

        return false;
    }

    float ResolveTaskDurationSeconds()
    {
        switch (currentTaskKind)
        {
            case FarmTaskKind.Plant:
                return plantDurationSeconds;
            case FarmTaskKind.Harvest:
                return harvestDurationSeconds;
            case FarmTaskKind.Care:
            case FarmTaskKind.Water:
            case FarmTaskKind.PestControl:
                return GameHoursToSeconds(
                    currentPlot != null
                        ? currentPlot.GetSupportActionDurationGameHours()
                        : supportActionDurationFallbackGameHours);
            default:
                return 1f;
        }
    }

    string BuildWorkingActionText()
    {
        switch (currentTaskKind)
        {
            case FarmTaskKind.Harvest:
                return "Dang thu hoach lua";
            case FarmTaskKind.Plant:
                return "Dang trong lua";
            case FarmTaskKind.Care:
                return "Dang cham soc Linh Me";
            case FarmTaskKind.Water:
                return "Dang tuoi nuoc Linh Me";
            case FarmTaskKind.PestControl:
                return "Dang bat sau Linh Me";
            default:
                return "Dang lam viec ruong";
        }
    }

    string BuildMoveActionText()
    {
        return BuildMoveActionText(currentTaskKind);
    }

    string BuildMoveActionText(FarmTaskKind taskKind)
    {
        switch (taskKind)
        {
            case FarmTaskKind.Harvest:
                return "Di thu hoach lua";
            case FarmTaskKind.Plant:
                return "Di trong lua";
            case FarmTaskKind.Care:
                return "Di cham soc Linh Me";
            case FarmTaskKind.Water:
                return "Di tuoi nuoc Linh Me";
            case FarmTaskKind.PestControl:
                return "Di bat sau Linh Me";
            default:
                return "Di lam ruong";
        }
    }

    string BuildSupportCompleteAction(
        FarmTaskKind taskKind,
        float reducedHours)
    {
        string prefix;
        switch (taskKind)
        {
            case FarmTaskKind.Care:
                prefix = "Cham soc Linh Me";
                break;
            case FarmTaskKind.Water:
                prefix = "Tuoi nuoc Linh Me";
                break;
            case FarmTaskKind.PestControl:
                prefix = "Bat sau Linh Me";
                break;
            default:
                prefix = "Cham soc Linh Me";
                break;
        }

        return prefix +
            " giam " +
            reducedHours.ToString("0.#") +
            " gio";
    }

    FarmPlotSupportAction ToSupportAction(FarmTaskKind taskKind)
    {
        switch (taskKind)
        {
            case FarmTaskKind.Care:
                return FarmPlotSupportAction.Care;
            case FarmTaskKind.Water:
                return FarmPlotSupportAction.Water;
            case FarmTaskKind.PestControl:
                return FarmPlotSupportAction.PestControl;
            default:
                return FarmPlotSupportAction.None;
        }
    }

    float GameHoursToSeconds(float gameHours)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float realSecondsPerGameDay =
            timeSystem != null
                ? Mathf.Max(1f, timeSystem.realSecondsPerGameDay)
                : 900f;

        return Mathf.Max(
            0.1f,
            gameHours * realSecondsPerGameDay / 24f);
    }

    void LogDebug(string stage, string detail)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log(
            "[VillagerFarmJob] " +
            gameObject.name +
            " stage=" + stage +
            " state=" + currentState +
            " detail=" + detail,
            this);
    }
}
