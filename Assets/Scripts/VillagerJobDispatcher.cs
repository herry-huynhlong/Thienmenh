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
                villager.ThinkTrader();
                return true;

            case VillagerJob.Farmer:
            case VillagerJob.Fisher:
                return TryRunHarvestJob();

            case VillagerJob.Hunter:
                return TryRunHunterJob();

            case VillagerJob.Guard:
                return TryRunGuardJob();

            case VillagerJob.Healer:
                return TryRunHealerJob();

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
            case VillagerJob.Fisher:
                return TryRunHarvestJob();

            case VillagerJob.Hunter:
                return TryRunHunterJob();

            case VillagerJob.Trader:
                return false;

            case VillagerJob.Guard:
                return TryRunGuardJob();

            case VillagerJob.Healer:
                return TryRunHealerJob();

            default:
                return false;
        }
    }

    bool TryRunHarvestJob()
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

        HarvestJob harvestJob = villager.GetComponent<HarvestJob>();
        if (harvestJob == null)
        {
            harvestJob = villager.gameObject.AddComponent<HarvestJob>();
        }

        return harvestJob.TryRun();
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

    bool TryRunGuardJob()
    {
        GuardJob guardJob = villager.GetComponent<GuardJob>();
        if (guardJob == null)
        {
            guardJob = villager.gameObject.AddComponent<GuardJob>();
        }

        return guardJob.TryRun();
    }

    bool TryRunHealerJob()
    {
        HealerJob healerJob = villager.GetComponent<HealerJob>();
        if (healerJob == null)
        {
            healerJob = villager.gameObject.AddComponent<HealerJob>();
        }

        return healerJob.TryRun();
    }
}
