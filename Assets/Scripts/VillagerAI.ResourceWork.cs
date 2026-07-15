using UnityEngine;

public partial class VillagerAI
{
    bool TryScheduledGather()
    {
        if (job == VillagerJob.Hunter)
        {
            HunterJob hunterJob = GetComponent<HunterJob>();
            if (hunterJob == null)
            {
                hunterJob = gameObject.AddComponent<HunterJob>();
            }

            if (hunterJob.TryRun())
            {
                return true;
            }
        }

        if (job == VillagerJob.Farmer ||
            job == VillagerJob.Fisher)
        {
            EnsureWorkGatherer();

            HarvestJob harvestJob = EnsureHarvestJob();
            if (harvestJob != null && harvestJob.TryRun())
            {
                return true;
            }
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        NpcResourceGatherer gatherer = EnsureWorkGatherer();
        if (gatherer != null &&
            gatherer.enabled &&
            gatherer.canGather)
        {
            if (gatherer.TryStartGatheringNow())
            {
                return true;
            }
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.Gather,
                job,
                NpcLocationPurpose.Resource,
                transform.position,
                out Vector3 resourcePosition,
                out NpcMapZone? resourceZone))
        {
            currentAction = NpcText.Action("gatherResource");
            MoveUsingRoad(resourcePosition, resourceZone);
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    NpcScheduleActivity.Gather);
            }
            return true;
        }

        return false;
    }

    void GoResourceWork()
    {
        NpcMapZone preferredZone = GetPreferredResourceGatherZone();

        if (ShouldSeekForestResources())
        {
            GoToResourcePoint(
                WorldTilemapManager.Instance != null
                ? WorldTilemapManager.Instance.GetHuntingTile(preferredZone)
                : Vector3.zero,
                NpcText.Action("huntForestResource"),
                preferredZone);
            return;
        }

        GoToResourcePoint(
            workPoint != null
            ? workPoint.position
            : GetFallbackActivityPosition(),
            NpcText.Action("gatherVillageResource"),
            NpcMapZone.Lang);
    }

    bool ShouldSeekForestResources()
    {
        if (job == VillagerJob.Hunter)
        {
            return true;
        }

        return bravery >= 55;
    }

    public NpcMapZone GetPreferredResourceGatherZone()
    {
        if (job == VillagerJob.Fisher)
        {
            return NpcMapZone.Lang;
        }

        if (job == VillagerJob.Hunter)
        {
            HunterJob hunterJob = GetComponent<HunterJob>();
            if (hunterJob != null)
            {
                if (hunterJob.huntPoint != null)
                {
                    NpcMapZone? huntPointZone =
                        NpcMapNavigator.GetDestinationZone(
                            hunterJob.huntPoint);
                    if (huntPointZone.HasValue)
                    {
                        return huntPointZone.Value;
                    }
                }

                return hunterJob.huntZone;
            }
        }

        if (workPoint != null)
        {
            NpcMapZone? workZone =
                NpcMapNavigator.GetDestinationZone(workPoint);
            if (workZone.HasValue)
            {
                return workZone.Value;
            }
        }

        return ShouldSeekForestResources()
            ? NpcMapZone.MaThuSonMach
            : NpcMapZone.Lang;
    }

    void GoToResourcePoint(
        Vector3 target,
        string action,
        NpcMapZone? targetZone = null)
    {
        if (target == Vector3.zero)
        {
            target = GetFallbackActivityPosition();
        }

        MoveUsingRoad(target, targetZone);
        currentAction = action;

        if (IsAtPosition(target))
        {
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        resourceSessionMinGameHours,
                        resourceSessionMaxGameHours));
            currentAction = NpcText.Action("harvestResource");
        }
    }

    bool TryHandleAdultImmediateNeeds()
    {
        if (ShouldGoHomeForRest())
        {
            GoHomeToRest();
            return true;
        }

        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return true;
        }

        if (IsRoutineTravelOrCultivationAction(currentAction))
        {
            return true;
        }

        if (TryProcessDailyTaskPlan())
        {
            return true;
        }

        return false;
    }

    float GameHoursToSeconds(float gameHours)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float secondsPerDay =
            timeSystem != null
            ? Mathf.Max(1f, timeSystem.realSecondsPerGameDay)
            : 900f;

        return Mathf.Max(0.5f, gameHours * secondsPerDay / 24f);
    }

    void ResetDailyTargets()
    {
        if (WorldTilemapManager.Instance != null)
        {
            WorldTilemapManager.Instance.ReleaseFishingTile(this);
        }

        currentWorkTarget = Vector3.zero;
        currentWorkTargetZone = null;
        currentWorkTargetKey = string.Empty;
        hasWorkTarget = false;
        hasTradeTarget = false;
        currentTradeTarget = Vector3.zero;
        currentTradeTargetZone = null;
        hasBuyTarget = false;
        currentBuyTarget = Vector3.zero;
        currentBuyTargetZone = null;
        hasEatTarget = false;
        hasSellTarget = false;
        currentSellTarget = Vector3.zero;
        currentSellTargetZone = null;
        resolvedTraderLocationZone = null;
        resolvedBuyLocationZone = null;
        resolvedSellLocationZone = null;
        hasRoadPreference = false;
    }
}
