using UnityEngine;

public partial class VillagerAI
{
    public bool TryRunWorkStandby(
        string action,
        float radius = 1.4f)
    {
        if (IsDead)
        {
            return false;
        }

        string resolvedAction =
            string.IsNullOrWhiteSpace(action)
                ? NpcText.Action("working")
                : action;
        Vector3 anchor = ResolveWorkStandbyAnchor();
        float resolvedRadius =
            Mathf.Max(
                radius,
                arriveDistance * 2.5f,
                0.75f);

        if (TryWanderNearAnchor(
                anchor,
                resolvedRadius,
                resolvedAction))
        {
            return true;
        }

        ClearMovementTargets();
        StopMoving();
        currentAction = resolvedAction;
        return true;
    }

    Vector3 ResolveWorkStandbyAnchor()
    {
        if (hasWorkTarget &&
            currentWorkTarget != Vector3.zero)
        {
            return currentWorkTarget;
        }

        if (hasDirectMoveTarget)
        {
            return directMoveTarget;
        }

        if (currentTarget != null)
        {
            return GetApproachPosition(currentTarget);
        }

        if (workPoint != null)
        {
            return workPoint.position;
        }

        return transform.position;
    }

    void GoWork()
    {
        if (IsInDungeonCombatSession())
        {
            LogWorkDebug(
                "Skip",
                "reason=dungeonCombat",
                ref lastWorkMoveLogTime,
                0.25f);
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (ShouldGoHomeForRest())
        {
            LogWorkDebug(
                "Skip",
                "reason=goHomeForRest",
                ref lastWorkMoveLogTime,
                0.25f);
            GoHomeToRest();
            return;
        }

        NpcResourceGatherer gatherer = EnsureWorkGatherer();
        if (job != VillagerJob.Farmer &&
            job != VillagerJob.Fisher &&
            job != VillagerJob.Hunter &&
            gatherer != null &&
            gatherer.enabled &&
            gatherer.canGather)
        {
            if (gatherer.TryStartGatheringNow())
            {
                LogWorkDebug(
                    "Delegate",
                    "reason=resourceGatherer",
                    ref lastWorkMoveLogTime,
                    0.25f);
                return;
            }
        }

        if (job == VillagerJob.Hunter)
        {
            HunterJob hunterJob = GetComponent<HunterJob>();
            if (hunterJob == null)
            {
                hunterJob = gameObject.AddComponent<HunterJob>();
            }

            if (hunterJob.TryRun())
            {
                LogWorkDebug(
                    "Delegate",
                    "reason=hunterJob",
                    ref lastWorkMoveLogTime,
                    0.25f);
                return;
            }
        }

        if (job == VillagerJob.Farmer)
        {
            VillagerFarmJob farmJob =
                GetComponent<VillagerFarmJob>();
            if (farmJob == null)
            {
                farmJob = gameObject.AddComponent<VillagerFarmJob>();
            }

            if (farmJob.TryRun())
            {
                LogWorkDebug(
                    "Delegate",
                    "reason=villagerFarmJob",
                    ref lastWorkMoveLogTime,
                    0.25f);
                return;
            }

            HarvestJob harvestJob = EnsureHarvestJob();
            if (harvestJob != null &&
                harvestJob.TryRun())
            {
                LogWorkDebug(
                    "Delegate",
                    "reason=harvestJob",
                    ref lastWorkMoveLogTime,
                    0.25f);
                return;
            }
        }

        if (job == VillagerJob.Fisher)
        {
            VillagerFishingJob fishingJob =
                GetComponent<VillagerFishingJob>();
            if (fishingJob == null)
            {
                fishingJob = gameObject.AddComponent<VillagerFishingJob>();
            }

            if (fishingJob.TryRun())
            {
                LogWorkDebug(
                    "Delegate",
                    "reason=villagerFishingJob",
                    ref lastWorkMoveLogTime,
                    0.25f);
                return;
            }
        }

        string desiredWorkTargetKey = GetCurrentWorkTargetKey();
        NpcMapZone preferredResourceZone = GetPreferredResourceGatherZone();
        if (currentWorkTargetKey != desiredWorkTargetKey)
        {
            hasWorkTarget = false;
            currentWorkTargetZone = null;
            currentWorkTargetKey = desiredWorkTargetKey;
            LogWorkDebug(
                "Retarget",
                "reason=keyChanged newKey=" + desiredWorkTargetKey,
                ref lastWorkTargetLogTime);
        }

        if (!hasWorkTarget)
        {
            WorldTilemapManager worldTilemap =
                WorldTilemapManager.Instance;

            currentWorkTarget = Vector3.zero;
            currentWorkTargetZone = null;

            switch (job)
            {
                case VillagerJob.Fisher:
                    currentWorkTarget = GetWorkPointPosition(VillagerJob.Fisher);
                    if (currentWorkTarget == Vector3.zero)
                    {
                        currentWorkTarget = GetNextFishingPatrolPoint();
                        currentWorkTargetZone =
                            GetZoneForPosition(currentWorkTarget) ??
                            NpcMapNavigator.GetDestinationZone(workPoint);
                    }
                    if (currentWorkTarget == Vector3.zero)
                    {
                        currentWorkTarget = GetFallbackPositionInZone(
                            preferredResourceZone);
                        currentWorkTargetZone =
                            GetZoneForPosition(currentWorkTarget) ??
                            preferredResourceZone;
                        currentAction = NpcText.Action("noFishingSpotFarmFallback");
                    }
                    break;

                case VillagerJob.Hunter:
                    currentWorkTarget = GetWorkPointPosition(VillagerJob.Hunter);
                    if (currentWorkTarget == Vector3.zero)
                    {
                        currentWorkTarget = worldTilemap != null
                            ? worldTilemap.GetHuntingTile(preferredResourceZone)
                            : Vector3.zero;
                    }
                    currentWorkTargetZone =
                        currentWorkTargetZone.HasValue
                        ? currentWorkTargetZone
                        : (GetZoneForPosition(currentWorkTarget) ??
                            preferredResourceZone);
                    break;

                default:
                    if (workPoint != null)
                    {
                        currentWorkTarget = GetWorkPointPosition(job);
                        currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);
                    }
                    break;
            }

            if (currentWorkTarget == Vector3.zero)
            {
                currentWorkTarget = job == VillagerJob.Hunter
                    ? GetFallbackPositionInZone(preferredResourceZone)
                    : workPoint != null
                        ? workPoint.position
                        : GetFallbackActivityPosition();
                currentWorkTargetZone = job == VillagerJob.Hunter
                    ? GetZoneForPosition(currentWorkTarget) ??
                        preferredResourceZone
                    : NpcMapNavigator.GetDestinationZone(workPoint);
            }

            if (!currentWorkTargetZone.HasValue &&
                currentWorkTarget != Vector3.zero)
            {
                currentWorkTargetZone = GetZoneForPosition(currentWorkTarget);
            }

            hasWorkTarget = true;
            debugWorkTarget =
                job + " -> " + currentWorkTarget +
                " zone=" + (currentWorkTargetZone.HasValue
                    ? currentWorkTargetZone.Value.ToString()
                    : "none") +
                " purpose=" + GetWorkLocationPurpose(job);
            LogWorkDebug(
                "Target",
                "key=" + currentWorkTargetKey +
                " workPoint=" + (workPoint != null ? workPoint.name : "null") +
                " debug=" + debugWorkTarget,
                ref lastWorkTargetLogTime,
                0.25f);
        }

        float distance =
            Vector2.Distance(
                transform.position,
                currentWorkTarget);
        bool targetOccupied =
            IsSharedTargetOccupied(currentWorkTarget);

        if (distance >= 0.5f)
        {
            if (targetOccupied)
            {
                LogWorkDebug(
                    "Occupied",
                    "distance=" + distance.ToString("0.00") +
                    " occupants=" + DescribeSharedTargetOccupants(currentWorkTarget),
                    ref lastWorkOccupancyLogTime,
                    0.75f);
            }

            currentAction = GetWorkAction();
            SetDirectMoveTarget(
                currentWorkTarget,
                false,
                currentWorkTargetZone);
            MoveUsingRoad(currentWorkTarget, currentWorkTargetZone);
            LogWorkDebug(
                "Move",
                "distance=" + distance.ToString("0.00") +
                " direct=" + hasDirectMoveTarget +
                " currentTarget=" + (currentTarget != null
                    ? currentTarget.name
                    : "null") +
                " occupied=" + targetOccupied,
                ref lastWorkMoveLogTime,
                0.75f);
            return;
        }

        ClearMovementTargets();
        StopMoving();

        currentAction = GetWorkingAction();
        bool produced = AddWorkProduct();
        fatigue = Mathf.Clamp(fatigue + 8f, 0f, 100f);
        actionTimer = produced
            ? GameHoursToSeconds(
                Random.Range(
                    workSessionMinGameHours,
                    workSessionMaxGameHours))
            : Mathf.Max(thinkInterval, 2f);
        LogWorkDebug(
            "Arrive",
            "distance=" + distance.ToString("0.00") +
            " produced=" + produced +
            " actionTimer=" + actionTimer.ToString("0.00") +
            " occupied=" + targetOccupied,
            ref lastWorkArrivalLogTime,
            0.5f);

    }

    NpcMapZone? GetZoneForPosition(Vector3 position)
    {
        NpcMapArea area = NpcMapArea.FindArea(position);
        if (area != null)
        {
            return area.zone;
        }

        NpcMapArea nearestArea = NpcMapArea.FindNearestArea(position);
        return nearestArea != null
            ? nearestArea.zone
            : (NpcMapZone?)null;
    }

    Vector3 GetNextFishingPatrolPoint()
    {
        WorldTilemapManager worldTilemap =
            WorldTilemapManager.Instance;
        NpcMapZone preferredZone = GetPreferredResourceGatherZone();

        if (worldTilemap != null)
        {
            Vector3 fishingTile =
                worldTilemap.GetFishingTile(
                    this,
                    preferredZone);

            if (fishingTile != Vector3.zero)
            {
                return fishingTile;
            }
        }

        NpcMapArea area = NpcMapArea.FindAreaByZone(preferredZone);
        if (area != null && area.areaBounds != null)
        {
            Bounds bounds = area.areaBounds.bounds;
            Vector3 seed = transform.position + new Vector3(
                Random.Range(-4f, 4f),
                Random.Range(-4f, 4f),
                0f);

            return new Vector3(
                Mathf.Clamp(seed.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(seed.y, bounds.min.y, bounds.max.y),
                transform.position.z);
        }

        return Vector3.zero;
    }
}
