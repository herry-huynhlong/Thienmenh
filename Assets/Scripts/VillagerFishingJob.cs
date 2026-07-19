using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class VillagerFishingJob : MonoBehaviour
{
    VillagerAI villager;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
    }

    public bool TryRun()
    {
        if (!CanRunJob())
        {
            return false;
        }

        NpcItemCollector collector = GetComponent<NpcItemCollector>();
        if (collector == null)
        {
            collector = gameObject.AddComponent<NpcItemCollector>();
        }

        collector.canPickupItems = true;

        NpcResourceGatherer gatherer =
            GetComponent<NpcResourceGatherer>();
        if (gatherer == null)
        {
            gatherer = gameObject.AddComponent<NpcResourceGatherer>();
        }

        gatherer.canGather = true;
        gatherer.useVillagerPreferredZone = true;

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob == null)
        {
            harvestJob = gameObject.AddComponent<HarvestJob>();
        }

        harvestJob.enabledHarvestJob = true;
        return harvestJob.TryRun();
    }

    bool CanRunJob()
    {
        return villager != null &&
            villager.enabled &&
            !villager.IsDead &&
            !villager.IsReturningHome &&
            villager.job == VillagerJob.Fisher &&
            !villager.ShouldGoHomeForRest();
    }
}
