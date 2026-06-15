using UnityEngine;

public class NpcEscortTaskForceStarter : MonoBehaviour
{
    [Header("Targets")]
    public GameObject targetNpc;
    public NpcTaskProvider provider;

    [Header("Test")]
    public bool autoFindNearestProvider = true;
    public bool autoFindNearestNpc = true;
    public bool temporarilyBoostEligibility = true;
    public bool restoreEligibilityAfterStart = true;

    [ContextMenu("Force Start Escort Task")]
    public void ForceStartEscortTask()
    {
        GameObject npc = ResolveTargetNpc();
        if (npc == null)
        {
            Debug.LogWarning("[EscortTest] Missing target NPC.");
            return;
        }

        NpcTaskProvider taskProvider = provider;
        if (taskProvider == null && autoFindNearestProvider)
        {
            taskProvider = NpcTaskProvider.FindNearestProvider(npc.transform.position);
        }

        if (taskProvider == null)
        {
            Debug.LogWarning("[EscortTest] No NpcTaskProvider found.");
            return;
        }

        NpcTaskOffer escortOffer = FindEscortOffer(taskProvider);
        if (escortOffer == null)
        {
            Debug.LogWarning("[EscortTest] Provider has no escort offer.");
            return;
        }

        bool originalRequireWillingness = taskProvider.requireNpcTaskWillingness;
        NpcEligibilitySnapshot snapshot = default;
        bool capturedSnapshot = false;

        try
        {
            if (temporarilyBoostEligibility)
            {
                snapshot = CaptureEligibility(npc);
                capturedSnapshot = true;
                ApplyEligibilityBoost(npc);
            }

            taskProvider.requireNpcTaskWillingness = false;

            bool started =
                taskProvider.TryStartPlannedTask(npc, escortOffer);

            Debug.Log(
                started
                    ? "[EscortTest] Escort task started for " + npc.name + " using offer " + escortOffer.taskName
                    : "[EscortTest] Escort task failed for " + npc.name + " using offer " + escortOffer.taskName);
        }
        finally
        {
            taskProvider.requireNpcTaskWillingness = originalRequireWillingness;

            if (capturedSnapshot && restoreEligibilityAfterStart)
            {
                RestoreEligibility(npc, snapshot);
            }
        }
    }

    GameObject ResolveTargetNpc()
    {
        if (targetNpc != null)
        {
            return targetNpc;
        }

        if (gameObject.GetComponent<SmartNpcAI>() != null ||
            gameObject.GetComponent<VillagerAI>() != null)
        {
            return gameObject;
        }

        if (!autoFindNearestNpc)
        {
            return null;
        }

        GameObject best = null;
        float bestDistance = float.PositiveInfinity;

        SmartNpcAI[] smartNpcs =
            FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude);
        foreach (SmartNpcAI npc in smartNpcs)
        {
            if (npc == null)
            {
                continue;
            }

            float distance =
                Vector2.Distance(transform.position, npc.transform.position);
            if (distance < bestDistance)
            {
                best = npc.gameObject;
                bestDistance = distance;
            }
        }

        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude);
        foreach (VillagerAI npc in villagers)
        {
            if (npc == null)
            {
                continue;
            }

            float distance =
                Vector2.Distance(transform.position, npc.transform.position);
            if (distance < bestDistance)
            {
                best = npc.gameObject;
                bestDistance = distance;
            }
        }

        return best;
    }

    NpcTaskOffer FindEscortOffer(NpcTaskProvider taskProvider)
    {
        if (taskProvider == null || taskProvider.offers == null)
        {
            return null;
        }

        foreach (NpcTaskOffer offer in taskProvider.offers)
        {
            if (offer != null &&
                offer.taskType == NpcTaskType.Escort)
            {
                return offer;
            }
        }

        return null;
    }

    struct NpcEligibilitySnapshot
    {
        public bool hasSmartNpc;
        public bool smartCanFight;
        public int smartBravery;
        public float smartFatigue;
        public float smartHunger;

        public bool hasVillager;
        public int villagerBravery;
        public float villagerFatigue;
        public float villagerHunger;
    }

    NpcEligibilitySnapshot CaptureEligibility(GameObject npc)
    {
        NpcEligibilitySnapshot snapshot = default;

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            snapshot.hasSmartNpc = true;
            snapshot.smartCanFight = smartNpc.canFight;
            snapshot.smartBravery = smartNpc.bravery;
            snapshot.smartFatigue = smartNpc.fatigue;
            snapshot.smartHunger = smartNpc.hunger;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            snapshot.hasVillager = true;
            snapshot.villagerBravery = villager.bravery;
            snapshot.villagerFatigue = villager.fatigue;
            snapshot.villagerHunger = villager.hunger;
        }

        return snapshot;
    }

    void ApplyEligibilityBoost(GameObject npc)
    {
        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.canFight = true;
            smartNpc.bravery = Mathf.Max(smartNpc.bravery, 100);
            smartNpc.fatigue = 0f;
            smartNpc.hunger = 0f;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.bravery = Mathf.Max(villager.bravery, 100);
            villager.fatigue = 0f;
            villager.hunger = 0f;
        }
    }

    void RestoreEligibility(GameObject npc, NpcEligibilitySnapshot snapshot)
    {
        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null && snapshot.hasSmartNpc)
        {
            smartNpc.canFight = snapshot.smartCanFight;
            smartNpc.bravery = snapshot.smartBravery;
            smartNpc.fatigue = snapshot.smartFatigue;
            smartNpc.hunger = snapshot.smartHunger;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null && snapshot.hasVillager)
        {
            villager.bravery = snapshot.villagerBravery;
            villager.fatigue = snapshot.villagerFatigue;
            villager.hunger = snapshot.villagerHunger;
        }
    }
}
