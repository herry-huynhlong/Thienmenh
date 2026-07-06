using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class VillagerFarmJob : MonoBehaviour
{
    enum FarmTaskKind
    {
        None,
        Plant,
        Harvest
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

    [Header("Debug")]
    public NpcJobState currentState = NpcJobState.Idle;
    public bool debugLogs;

    VillagerAI villager;
    ItemInventory inventory;
    FarmPlot currentPlot;
    FarmTaskKind currentTaskKind;
    float workTimer;
    int lastDisplayedSeconds = -1;

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

        if (!TryReserveNextPlot(out FarmPlot reservedPlot, out FarmTaskKind taskKind))
        {
            currentState = NpcJobState.Idle;
            return false;
        }

        currentPlot = reservedPlot;
        currentTaskKind = taskKind;
        currentState = NpcJobState.Moving;
        lastDisplayedSeconds = -1;

        villager.ForceJobMoveTo(
            currentPlot.transform.position,
            taskKind == FarmTaskKind.Harvest
                ? "Đi thu hoạch lúa"
                : "Đi trồng lúa");

        LogDebug(
            "Reserve",
            "task=" + taskKind +
            " plot=" + currentPlot.name);
        return true;
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
                currentTaskKind == FarmTaskKind.Harvest
                    ? "Đi thu hoạch lúa"
                    : "Đi trồng lúa");
            return;
        }

        villager.StopMoving();
        currentState = NpcJobState.Working;
        workTimer =
            currentTaskKind == FarmTaskKind.Harvest
                ? harvestDurationSeconds
                : plantDurationSeconds;
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
                villager.SetActionImmediate("Đã trồng lúa", 0.35f);
                LogDebug("Plant", "result=success");
            }
            else
            {
                LogDebug("Plant", "result=failed");
            }

            CancelCurrentTask(!planted);
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
                    : "nông sản";
            villager.SetActionImmediate(
                "Thu hoạch " + itemName + " x" + harvestedAmount,
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
            currentTaskKind == FarmTaskKind.Harvest
                ? "Đang thu hoạch lúa"
                : "Đang trồng lúa";

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
