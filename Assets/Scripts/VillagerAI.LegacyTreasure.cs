using UnityEngine;

public partial class VillagerAI
{
    public void ForceTreasureWait(
        Vector3 origin,
        float safeRadius,
        StatItemData item,
        bool lowPowerSkirmish)
    {
        return;
    }

    public void ForceTreasureHunt(
        Transform target,
        StatItemData item)
    {
        return;
    }

    public void ClearTreasureHunt()
    {
        if (treasureHuntTarget == null &&
            treasureHuntItem == null)
        {
            return;
        }

        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        treasureWaitLowPowerSkirmish = false;
        if (currentTarget != null &&
            currentAction.Contains(NpcText.Action("treasureHunt")))
        {
            ClearMovementTargets();
        }

        currentAction = NpcText.Action("calm");
    }

    void RefreshTreasureHuntAction()
    {
        if (waitingOutsideTreasureLightning)
        {
            return;
        }

        if (treasureHuntTarget == null ||
            treasureHuntItem == null)
        {
            ClearTreasureHunt();
            return;
        }

        SetTarget(
            treasureHuntTarget,
            NpcText.ActionFormat(
                "treasureHuntNamed",
                ItemText.Name(treasureHuntItem)));
    }

    void UpdateTreasureWaitAction()
    {
        if (!waitingOutsideTreasureLightning ||
            treasureHuntItem == null)
        {
            return;
        }

        Vector3 waitPosition = hasDirectMoveTarget
            ? directMoveTarget
            : transform.position;

        string itemName = ItemText.Name(treasureHuntItem);
        if (Vector2.Distance(transform.position, waitPosition) <= arriveDistance)
        {
            currentAction = treasureWaitLowPowerSkirmish
                ? NpcText.ActionFormat("outerSkirmishNamed", itemName)
                : NpcText.ActionFormat("waitLightningNamed", itemName);
            return;
        }

        currentAction = NpcText.Action("goHunt");
    }
}
