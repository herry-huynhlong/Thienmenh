using UnityEngine;

public partial class SmartNpcAI
{
    float frontierDefenseModeUntilTime;
    Vector3 frontierDefenseStagingPoint;
    bool hasFrontierDefenseStagingPoint;

    public bool IsInFrontierDefenseMode =>
        Time.time < frontierDefenseModeUntilTime &&
        NpcMapBehaviorPolicy.HasForcedCombatZone(
            gameObject,
            NpcMapZone.MaThuSonMach);

    public void EnterFrontierDefenseMode(
        Vector3 stagingPoint,
        float durationSeconds,
        string reason)
    {
        float resolvedDuration =
            Mathf.Max(5f, durationSeconds);
        frontierDefenseModeUntilTime =
            Time.time + resolvedDuration;

        NpcMapBehaviorPolicy.RegisterForcedCombatZone(
            gameObject,
            NpcMapZone.MaThuSonMach,
            resolvedDuration);
        NpcMapNavigator.LockNpcZone(
            gameObject,
            NpcMapZone.MaThuSonMach,
            resolvedDuration);

        NpcTaskProvider.ReleaseNpcFromProviderTasksForCombat(gameObject);
        ClearTaskProviderVisitState();
        ClearCultivationTravelState();
        ClearHelpRequestState();
        StopMonsterRetreat();
        ReleaseMonsterReservation();
        currentMonsterTarget = null;
        currentTarget = null;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        hasHomeReturnTarget = false;
        waitingOutsideTreasureLightning = false;
        hasTreasureWaitPosition = false;
        treasureWaitLowPowerSkirmish = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;

        RequestEmergencyTask(
            SmartAITaskGoal.Combat,
            SmartAITaskPriority.Emergency,
            false,
            reason);

        frontierDefenseStagingPoint = stagingPoint;
        hasFrontierDefenseStagingPoint = true;
        wanderTarget = stagingPoint;
        hasWanderTarget = true;
        currentAction = NpcText.Action("goHunt");
        StopNpcMovement();
    }

    public void ExitFrontierDefenseMode()
    {
        frontierDefenseModeUntilTime = 0f;
        hasFrontierDefenseStagingPoint = false;
        frontierDefenseStagingPoint = Vector3.zero;
        NpcMapBehaviorPolicy.ClearForcedCombatZone(gameObject);
        ClearSmartTaskIfGoal(SmartAITaskGoal.Combat);

        if (currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true))
        {
            currentAction = string.Empty;
        }

        if (currentMonsterTarget == null)
        {
            currentTarget = null;
        }
    }

    bool TryContinueFrontierDefenseTravel()
    {
        if (!IsInFrontierDefenseMode ||
            !hasFrontierDefenseStagingPoint)
        {
            return false;
        }

        float arriveDistance =
            Mathf.Max(
                escapeTargetReachDistance,
                targetClearRadius * 2f,
                0.65f);
        float distance =
            Vector2.Distance(
                transform.position,
                frontierDefenseStagingPoint);

        if (distance <= arriveDistance)
        {
            hasFrontierDefenseStagingPoint = false;

            if (hasWanderTarget &&
                Vector2.Distance(
                    wanderTarget,
                    frontierDefenseStagingPoint) <= arriveDistance)
            {
                hasWanderTarget = false;
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            return false;
        }

        currentTarget = null;
        wanderTarget = frontierDefenseStagingPoint;
        hasWanderTarget = true;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        hasHomeReturnTarget = false;
        currentAction = NpcText.Action("goHunt");
        return true;
    }

    bool HasPendingFrontierDefenseTravel()
    {
        if (!IsInFrontierDefenseMode ||
            !hasFrontierDefenseStagingPoint)
        {
            return false;
        }

        float arriveDistance =
            Mathf.Max(
                escapeTargetReachDistance,
                targetClearRadius * 2f,
                0.65f);
        return Vector2.Distance(
                transform.position,
                frontierDefenseStagingPoint) > arriveDistance;
    }
}
