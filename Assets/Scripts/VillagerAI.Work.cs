using UnityEngine;

public partial class VillagerAI
{
    void GoWork()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
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
                return;
            }
        }

        if (job == VillagerJob.Farmer ||
            job == VillagerJob.Fisher)
        {
            HarvestJob harvestJob = EnsureHarvestJob();
            if (harvestJob != null &&
                harvestJob.TryRun())
            {
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
        }

        float distance =
            Vector2.Distance(
                transform.position,
                currentWorkTarget);

        if (distance >= 0.5f)
        {
            currentAction = GetWorkAction();
            SetDirectMoveTarget(
                currentWorkTarget,
                false,
                currentWorkTargetZone);
            MoveUsingRoad(currentWorkTarget, currentWorkTargetZone);
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

        if (produced)
        {
            AddProfessionExp(professionExpPerWork);
        }
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
