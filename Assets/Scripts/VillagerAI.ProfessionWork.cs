using UnityEngine;

public partial class VillagerAI
{
    bool NeedsFood()
    {
        return true;
    }

    float GetHungerRate()
    {
        return 0.35f;
    }

    bool IsForgeWorker()
    {
        return job == VillagerJob.Blacksmith &&
            (GetComponent<NpcFixedBlacksmithController>() != null ||
            GetComponent<NpcForgeAgent>() != null);
    }

    bool IsAlchemyWorker()
    {
        return job == VillagerJob.Alchemist &&
            GetComponent<NpcAlchemyAgent>() != null;
    }

    void GoAlchemyWorkOrTrade()
    {
        if (!autonomousWorkEnabled)
        {
            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        NpcAlchemyAgent alchemyAgent = GetComponent<NpcAlchemyAgent>();
        if (alchemyAgent == null)
        {
            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        if (alchemyAgent.TrySellFinishedGoods() ||
            alchemyAgent.TryStartAnyAlchemy())
        {
            return;
        }

        if (alchemyAgent.autoBuyMaterialsFromMarketTraders &&
            alchemyAgent.NeedsMoreMaterials())
        {
            if (autonomousResourceWorkEnabled)
            {
                GoResourceWork();
                return;
            }

            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        if (workPoint != null)
        {
            currentAction = NpcText.Action("goAlchemy");
            SetDirectMoveTarget(workPoint.position);
            MoveUsingRoad(
                workPoint.position,
                NpcMapNavigator.GetDestinationZone(workPoint));
            return;
        }

        Wander(NpcText.Action("alchemy"));
    }

    void GoForgeWorkOrTrade()
    {
        if (!autonomousWorkEnabled)
        {
            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        if (fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.TryRunWorkCycle())
        {
            return;
        }

        NpcForgeAgent forgeAgent = GetComponent<NpcForgeAgent>();
        if (forgeAgent == null)
        {
            GoWork();
            return;
        }

        if (forgeAgent.autoBuyMaterialsFromMarketTraders &&
            forgeAgent.NeedsMoreMaterials())
        {
            if (autonomousResourceWorkEnabled)
            {
                GoResourceWork();
                return;
            }

            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        GoWork();
    }
}
