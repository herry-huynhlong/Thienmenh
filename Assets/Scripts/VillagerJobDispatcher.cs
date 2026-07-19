using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class VillagerJobDispatcher : MonoBehaviour
{
    VillagerAI villager;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
    }

    public bool TryHandleAdultThink()
    {
        if (villager == null)
        {
            return false;
        }

        if (villager.IsReturningHome)
        {
            return false;
        }

        if (villager.ShouldGoHomeForRest())
        {
            villager.GoHomeToRest();
            return true;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(villager.gameObject);
        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == NpcScheduleActivity.ReturnHome)
        {
            villager.GoHomeToRest();
            return true;
        }

        switch (villager.job)
        {
            case VillagerJob.Trader:
                return TryRunMarketRole();

            case VillagerJob.Farmer:
                return TryRunFarmerJob();

            case VillagerJob.Fisher:
                return TryRunFishingJob();

            case VillagerJob.Hunter:
                return TryRunHunterJob();

            default:
                return false;
        }
    }

    public bool TryHandleWork()
    {
        if (villager == null)
        {
            return false;
        }

        if (villager.IsReturningHome)
        {
            return false;
        }

        if (villager.ShouldGoHomeForRest())
        {
            villager.GoHomeToRest();
            return true;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(villager.gameObject);
        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == NpcScheduleActivity.ReturnHome)
        {
            villager.GoHomeToRest();
            return true;
        }

        switch (villager.job)
        {
            case VillagerJob.Farmer:
                return TryRunFarmerJob();

            case VillagerJob.Fisher:
                return TryRunFishingJob();

            case VillagerJob.Hunter:
                return TryRunHunterJob();

            case VillagerJob.Trader:
                return false;

            default:
                return false;
        }
    }

    bool TryRunFarmerJob()
    {
        NpcItemCollector collector = villager.GetComponent<NpcItemCollector>();
        if (collector == null)
        {
            collector = villager.gameObject.AddComponent<NpcItemCollector>();
        }

        collector.canPickupItems = true;

        NpcResourceGatherer gatherer =
            villager.GetComponent<NpcResourceGatherer>();
        if (gatherer == null)
        {
            gatherer = villager.gameObject.AddComponent<NpcResourceGatherer>();
        }

        gatherer.canGather = true;
        gatherer.useVillagerPreferredZone = true;

        VillagerFarmJob farmJob = villager.GetComponent<VillagerFarmJob>();
        if (farmJob == null)
        {
            farmJob = villager.gameObject.AddComponent<VillagerFarmJob>();
        }

        if (farmJob.TryRun())
        {
            return true;
        }

        HarvestJob harvestJob = villager.GetComponent<HarvestJob>();
        if (harvestJob == null)
        {
            harvestJob = villager.gameObject.AddComponent<HarvestJob>();
        }

        return harvestJob.TryRun();
    }

    bool TryRunFishingJob()
    {
        VillagerFishingJob fishingJob =
            villager.GetComponent<VillagerFishingJob>();
        if (fishingJob == null)
        {
            fishingJob =
                villager.gameObject.AddComponent<VillagerFishingJob>();
        }

        return fishingJob.TryRun();
    }


    bool TryRunHunterJob()
    {
        NpcItemCollector collector = villager.GetComponent<NpcItemCollector>();
        if (collector == null)
        {
            collector = villager.gameObject.AddComponent<NpcItemCollector>();
        }

        collector.canPickupItems = true;

        NpcResourceGatherer gatherer =
            villager.GetComponent<NpcResourceGatherer>();
        if (gatherer != null)
        {
            gatherer.canGather = false;
        }

        HarvestJob harvestJob = villager.GetComponent<HarvestJob>();
        if (harvestJob != null)
        {
            harvestJob.enabledHarvestJob = false;
        }

        HunterJob hunterJob = villager.GetComponent<HunterJob>();
        if (hunterJob == null)
        {
            hunterJob = villager.gameObject.AddComponent<HunterJob>();
        }

        return hunterJob.TryRun();
    }

    bool TryRunMarketRole()
    {
        VillagerMarketRole marketRole =
            villager.GetComponent<VillagerMarketRole>();
        if (marketRole == null)
        {
            marketRole =
                villager.gameObject.AddComponent<VillagerMarketRole>();
        }

        return marketRole.TryHandleAdultThink();
    }
}
