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
                        currentWorkTarget = worldTilemap != null
                            ? worldTilemap.GetFishingTile(this)
                            : Vector3.zero;
                        currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);
                    }
                    if (currentWorkTarget == Vector3.zero)
                    {
                        currentWorkTarget = worldTilemap != null
                            ? worldTilemap.GetFarmTile()
                            : Vector3.zero;
                        currentWorkTargetZone = NpcMapZone.Lang;
                        currentAction = NpcText.Action("noFishingSpotFarmFallback");
                    }
                    break;

                case VillagerJob.Hunter:
                    currentWorkTarget = GetWorkPointPosition(VillagerJob.Hunter);
                    if (currentWorkTarget == Vector3.zero)
                    {
                        currentWorkTarget = worldTilemap != null
                            ? worldTilemap.GetHuntingTile()
                            : Vector3.zero;
                    }
                    currentWorkTargetZone = currentWorkTargetZone.HasValue
                        ? currentWorkTargetZone
                        : NpcMapZone.MaThuSonMach;
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
                    ? GetFallbackPositionInZone(NpcMapZone.MaThuSonMach)
                    : workPoint != null
                        ? workPoint.position
                        : GetFallbackActivityPosition();
                currentWorkTargetZone = job == VillagerJob.Hunter
                    ? NpcMapZone.MaThuSonMach
                    : NpcMapNavigator.GetDestinationZone(workPoint);
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
}
