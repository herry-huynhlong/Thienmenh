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
            (GetComponent<NpcFixedAlchemistController>() != null ||
            GetComponent<NpcAlchemyAgent>() != null);
    }

    void GoAlchemyWorkOrTrade()
    {
        if (!autonomousWorkEnabled)
        {
            GoToAlchemyWorkPointOrWait();
            return;
        }

        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        if (fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.TryRunWorkCycle())
        {
            return;
        }

        NpcAlchemyAgent alchemyAgent = GetComponent<NpcAlchemyAgent>();
        if (alchemyAgent == null)
        {
            GoToAlchemyWorkPointOrWait();
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

            GoToAlchemyWorkPointOrWait();
            return;
        }

        GoToAlchemyWorkPointOrWait();
    }

    void GoToAlchemyWorkPointOrWait()
    {
        if (workPoint != null)
        {
            currentAction = NpcText.Action("goAlchemy");
            SetDirectMoveTarget(workPoint.position);
            MoveUsingRoad(
                workPoint.position,
                NpcMapNavigator.GetDestinationZone(workPoint));
            return;
        }

        ClearMovementTargets();
        StopMoving();
        currentAction = NpcText.Action("alchemy");
        actionTimer = Mathf.Max(actionTimer, restDuration);
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
