using UnityEngine;

// Central movement update, combat hold behavior, and action matching helpers.
public partial class SmartNpcAI
{
    void UpdateMovement()
    {
        if ((Time.time < movementPausedUntil ||
            Time.time < crowdYieldUntil) &&
            !hasEscapeTarget)
        {
            DebugFlow(
                "Move",
                "Paused movement pausedUntil=" +
                movementPausedUntil.ToString("0.00") +
                " crowdUntil=" +
                crowdYieldUntil.ToString("0.00"));

            if (!TryApplyNpcOverlapSeparation() && rb != null)
            {
                StopMovingSmooth();
            }
            return;
        }

        TryRestoreStaleTravelIntent();

        if (currentMonsterTarget == null)
        {
            // Frontier-defense travel is an emergency override and should
            // stay latched even if route recovery temporarily clears targets.
            TryContinueFrontierDefenseTravel();
        }

        if (ShouldHoldCombatPosition())
        {
            if (visualAnimation != null &&
                currentMonsterTarget != null)
            {
                visualAnimation.SetFacingTarget(
                    currentMonsterTarget.transform.position);
            }

            if (rb != null)
            {
                StopMovingSmooth();
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            blockedMoveTimer = 0f;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            DebugFlow(
                "Move",
                "Hold combat position target=" +
                (currentMonsterTarget != null
                    ? currentMonsterTarget.monsterName
                    : "null"));
            return;
        }

        if (currentTarget == null &&
            currentMonsterTarget != null &&
            currentMonsterTarget.currentHP > 0 &&
            !isRetreatingFromMonster &&
            (currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true)))
        {
            currentTarget = currentMonsterTarget.transform;
            hasWanderTarget = false;
            TryIgnoreCombatTargetCollision(currentTarget);
        }

        bool movingToTreasureWait =
            waitingOutsideTreasureLightning &&
            hasTreasureWaitPosition;
        bool holdPositionWithoutTarget =
            currentTarget == null &&
            !movingToTreasureWait &&
            Time.time >= postTeleportRecoveryUntil &&
            IsStationaryAction(currentAction) &&
            !hasWanderTarget &&
            !hasObstacleAvoidTarget &&
            !hasEscapeTarget;

        if (currentTarget == null &&
            !movingToTreasureWait &&
            holdPositionWithoutTarget)
        {
            string holdDebugSignature =
                "HoldWithoutTarget|" +
                currentAction + "|" +
                (currentSmartTask != null && currentSmartTask.IsValid
                    ? currentSmartTask.goal.ToString()
                    : "None");
            if (ShouldLogStateTransition(
                    ref lastMoveHoldDebugSignature,
                    ref lastMoveHoldDebugTime,
                    holdDebugSignature,
                    3.5f))
            {
                DebugFlow(
                    "Move",
                    "Hold without target action=" + currentAction);
            }

            LogHuntStall("hold-without-target");

            bool suppressStationarySeparation =
                currentAction == NpcText.Action("buyPill") ||
                currentAction == NpcText.Action("checkedVanBaoLau");

            if (!suppressStationarySeparation &&
                TryApplyNpcOverlapSeparation())
            {
                return;
            }

            if (rb != null)
            {
                StopMovingSmooth();
            }
            hasEscapeTarget = false;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;

            return;
        }

        Vector3 desiredTarget;

        if (hasEscapeTarget)
        {
            desiredTarget = escapeTarget;
        }
        else if (movingToTreasureWait)
        {
            desiredTarget = treasureWaitPosition;
        }
        else if (currentTarget != null)
        {
            desiredTarget = GetApproachPosition(currentTarget);
            hasWanderTarget = false;

            if (currentTarget.GetComponentInParent<NpcTaskProvider>() != null ||
                currentAction == NpcText.Action("goTaskProviderDaily") ||
                currentAction == NpcText.Action("visitedTaskProvider"))
            {
                DebugFlow(
                    "Move",
                    "Approach provider target=" +
                    currentTarget.name +
                    " desired=" +
                    desiredTarget +
                    " pos=" +
                    transform.position +
                    " dist=" +
                    Vector2.Distance(transform.position, desiredTarget).ToString("0.00"));
            }
        }
        else if (hasWanderTarget)
        {
            NpcCounterBroker broker =
                currentAction == NpcText.Action("goVanBaoLauBroker")
                    ? NpcCounterBroker.FindBestBrokerForNpc(gameObject)
                    : null;
            desiredTarget =
                broker != null
                ? ResolveBrokerApproachPosition(broker)
                : wanderTarget;
        }
        else
        {
            if (currentAction == NpcText.Action("goTaskProviderDaily"))
            {
                DebugFlow("Move", "Reached task provider route end");

                if (TryVisitTaskProvider())
                {
                    return;
                }

                currentAction = NpcText.Action("visitedTaskProvider");
                actionTimer = Mathf.Max(
                    actionTimer,
                    GameHoursToSeconds(0.2f));

                StopMovingSmooth(true);

                return;
            }

            if (IsTeleportRouteAction(currentAction))
            {
                if (TryRecoverTeleportRouteActionWithoutTarget())
                {
                    return;
                }

                if (!IsTeleportRouteAction(currentAction))
                {
                    return;
                }

                if (ShouldLogStateTransition(
                        ref lastMoveHoldDebugSignature,
                        ref lastMoveHoldDebugTime,
                        "TeleportRouteEnded|" + currentAction,
                        3.5f))
                {
                    DebugFlow(
                        "Move",
                        "Teleport route ended without follow target");
                }

                if (TryContinueFrontierDefenseTravel())
                {
                    return;
                }

                currentAction = NpcText.Action("idle");

                if (rb != null)
                {
                    StopMovingSmooth();
                }

                return;
            }

            if (currentAction == NpcText.Action("goHunt") &&
                currentMonsterTarget == null &&
                !hasWanderTarget &&
                !hasEscapeTarget &&
                !hasObstacleAvoidTarget)
            {
                LogHuntStall("goHunt-without-target-or-wander");

                if (TryContinueFrontierDefenseTravel())
                {
                    return;
                }

                currentAction = NpcText.Action("idle");
                StopMovingSmooth();

                return;
            }

            if (IsPreservedTravelAction(currentAction))
            {
                if (currentAction == NpcText.Action("fleeMonsterArea") &&
                    BeginLowHpRecoveryFromSafeState(
                        "retreat action lost travel target"))
                {
                    return;
                }

                if (ShouldLogStateTransition(
                        ref lastMoveHoldDebugSignature,
                        ref lastMoveHoldDebugTime,
                        "PreservedTravel|" + currentAction,
                        3.5f))
                {
                    DebugFlow("Move", "Preserved travel action waiting");
                }

                if (rb != null)
                {
                    StopMovingSmooth();
                }

                return;
            }

            StopMovingSmooth();

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;

            if (!IsStationaryAction(currentAction))
            {
                currentAction = NpcText.Action("idle");
            }

            return;
        }

        if (TryHandleBrokerArrivalFromMovement(desiredTarget))
        {
            return;
        }

        if (TryHandleGatherArrivalFromMovement(desiredTarget))
        {
            return;
        }

        if (hasObstacleAvoidTarget)
        {
            if (Time.time >= obstacleAvoidUntil ||
                Vector2.Distance(transform.position, obstacleAvoidTarget) <=
                escapeTargetReachDistance ||
                !IsMoveTargetFeasible(obstacleAvoidTarget))
            {
                hasObstacleAvoidTarget = false;
            }
            else
            {
                desiredTarget = obstacleAvoidTarget;
            }
        }

        if (hasEscapeTarget)
        {
            if (Vector2.Distance(transform.position, escapeTarget) <=
                escapeTargetReachDistance)
            {
                hasEscapeTarget = false;
            }
            else
            {
                desiredTarget = escapeTarget;
            }
        }

        NpcCounterBroker activeBroker =
            currentAction == NpcText.Action("goVanBaoLauBroker")
                ? NpcCounterBroker.FindBestBrokerForNpc(gameObject)
                : null;
        NpcMapZone? forcedTargetZone =
            ResolveBrokerTargetZone(activeBroker);

        bool usingTeleportRoute;
        string routeAction;
        NpcRouteStatus routeStatus;
        Vector3 moveTarget =
            NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                desiredTarget,
                forcedTargetZone,
                out usingTeleportRoute,
                out routeAction,
                out routeStatus);

        NpcMapArea currentArea = NpcMapArea.FindArea(transform.position);
        NpcMapArea desiredTargetArea = NpcMapArea.FindArea(desiredTarget);
        NpcMapZone? currentZone = NpcMapNavigator.ResolveActorZone(gameObject);
        NpcMapZone? desiredZone = forcedTargetZone ??
            (currentTarget != null
                ? NpcMapNavigator.GetDestinationZone(currentTarget)
                : (NpcMapZone?)null);
        if (!desiredZone.HasValue && desiredTargetArea != null)
        {
            desiredZone = desiredTargetArea.zone;
        }

        if (routeStatus == NpcRouteStatus.NoGate ||
            routeStatus == NpcRouteStatus.InvalidGate)
        {
            if (!string.IsNullOrEmpty(routeAction))
            {
                currentAction = routeAction;
            }

            StopNpcMovement();
            DebugFlow(
                "MoveRoute",
                "Blocked no route status=" +
                routeStatus +
                " currentZone=" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                " desiredZone=" +
                (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                " desiredTarget=" +
                desiredTarget +
                " target=" +
                (currentTarget != null ? currentTarget.name : "null"));
            return;
        }

        if (usingTeleportRoute)
        {
            string routeDebugSignature =
                "TeleportRoute|" +
                routeAction + "|" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                "|" +
                (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                "|" +
                (currentTarget != null ? currentTarget.name : "null") +
                "|" + hasWanderTarget;
            if (ShouldTraceRuntime() &&
                ShouldLogStateTransition(
                    ref lastRouteDebugSignature,
                    ref lastRouteDebugTime,
                    routeDebugSignature,
                    2.5f))
            {
                DebugFlow(
                    "MoveRoute",
                    "Teleport route action=" +
                    routeAction +
                    " currentZone=" +
                    (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                    " currentArea=" +
                    (currentArea != null ? currentArea.name : "null") +
                    " desiredZone=" +
                    (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                    " desiredArea=" +
                    (desiredTargetArea != null ? desiredTargetArea.name : "null") +
                    " moveTarget=" +
                    moveTarget +
                    " desiredTarget=" +
                    desiredTarget +
                    " target=" +
                    (currentTarget != null ? currentTarget.name : "null") +
                    " wander=" +
                    hasWanderTarget);
            }
        }
        else if (IsTeleportRouteAction(currentAction))
        {
            string restoredAction =
                ResolvePostTeleportTravelAction();

            if (TryRebuildPostTeleportTravelIntent(restoredAction))
            {
                return;
            }

            string routeRestoreSignature =
                "TeleportRestore|" +
                currentAction + "|" +
                restoredAction + "|" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                "|" +
                (desiredZone.HasValue ? desiredZone.Value.ToString() : "None");
            if (ShouldTraceRuntime() &&
                ShouldLogStateTransition(
                    ref lastRouteDebugSignature,
                    ref lastRouteDebugTime,
                    routeRestoreSignature,
                    2.5f))
            {
                DebugFlow(
                    "MoveRoute",
                    "Teleport action without route currentAction=" +
                    currentAction +
                    " restoredAction=" +
                    restoredAction +
                    " currentZone=" +
                    (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                    " currentArea=" +
                    (currentArea != null ? currentArea.name : "null") +
                    " desiredZone=" +
                    (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                    " desiredArea=" +
                    (desiredTargetArea != null ? desiredTargetArea.name : "null") +
                    " moveTarget=" +
                    moveTarget +
                    " desiredTarget=" +
                    desiredTarget +
                    " target=" +
                    (currentTarget != null ? currentTarget.name : "null") +
                    " wander=" +
                    hasWanderTarget);
            }

            currentAction = restoredAction;
        }

        if (usingTeleportRoute &&
            !string.IsNullOrEmpty(routeAction))
        {
            currentAction = routeAction;
        }

        if (usingTeleportRoute &&
            TryForceTeleportRouteProgress(moveTarget, currentZone))
        {
            return;
        }

        NpcMapArea spawnArea =
            NpcMapArea.FindArea(spawnPosition);
        NpcMapArea targetArea =
            NpcMapArea.FindArea(desiredTarget);
        bool targetInSpawnArea =
            spawnArea == null ||
            targetArea == null ||
            spawnArea.zone == targetArea.zone;

        bool isCultivationTravelRoute =
            currentTarget == cultivationPoint ||
            currentAction == NpcText.Action("goCultivatePoint");

        bool isAutonomousWorkRoute =
            currentAction == NpcText.Action("tradeSeek") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goTavern") ||
            currentAction == NpcText.Action("buyPill") ||
            currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);

        bool hasActiveDirectedTarget =
            currentTarget != null ||
            currentMonsterTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget;

        float distanceFromSpawn =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (!movingToTreasureWait &&
            treasureHuntTarget == null &&
            !usingTeleportRoute &&
            targetInSpawnArea &&
            !isCultivationTravelRoute &&
            !hasActiveDirectedTarget &&
            !isAutonomousWorkRoute &&
            distanceFromSpawn > maxRoamDistance)
        {
            DebugFlow(
                "Move",
                "Stopped by roam limit distance=" +
                distanceFromSpawn.ToString("0.00") +
                " max=" +
                maxRoamDistance.ToString("0.00"));

            if (rb != null)
            {
                StopMovingSmooth();
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            hasHomeReturnTarget = false;

            if (!IsStationaryAction(currentAction))
            {
                currentAction = NpcText.Action("idle");
            }

            return;
        }

        if (currentAction == NpcText.Action("goCultivatePoint") &&
            Vector2.Distance(transform.position, desiredTarget) <=
            GetCultivationArriveDistance())
        {
            if (TryRefreshPendingCultivationTravelTarget())
            {
                DebugFlow(
                    "Move",
                    "Refreshed cultivate route after intermediate target");
                return;
            }

            DebugFlow("Move", "Reached cultivate point");

            currentTarget = null;
            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            StopMovingSmooth(true);

            SyncCultivationEffect();
            DebugFlow("Move", "Arrived at cultivate point, start cultivate immediately");

            CultivateNaturally();
            return;
        }

        if (currentTarget == null &&
            hasWanderTarget &&
            Vector2.Distance(transform.position, desiredTarget) <= escapeTargetReachDistance)
        {
            DebugFlow("Move", "Reached wander target");

            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            StopMovingSmooth(true);

            if (currentAction == NpcText.Action("fleeMonsterArea") &&
                BeginLowHpRecoveryFromSafeState(
                    "reached flee wander target"))
            {
                return;
            }

            if (currentAction == NpcText.Action("goHunt") &&
                !IsInFrontierDefenseMode)
            {
                currentAction = NpcText.Action("idle");
            }

            return;
        }

        NpcMapZone? stepTargetZone =
            usingTeleportRoute
                ? (currentZone.HasValue
                    ? currentZone
                    : desiredZone)
                : desiredZone;

        MoveToPosition(
            moveTarget,
            stepTargetZone);
    }

    bool ShouldHoldCombatPosition()
    {
        if (currentMonsterTarget == null ||
            isRetreatingFromMonster ||
            IsDead)
        {
            return false;
        }

        if (currentMonsterTarget.currentHP <= 0)
        {
            return false;
        }

        if (currentTarget != null &&
            currentTarget != currentMonsterTarget.transform)
        {
            return false;
        }

        float distance =
            GetCombatSurfaceDistance(
                currentMonsterTarget.transform);

        float holdRange =
            Mathf.Max(
                attackRange + 0.35f,
                0.45f);

        return distance <= holdRange;
    }

    bool IsMonsterCombatApproachActive()
    {
        if (currentMonsterTarget == null ||
            currentTarget == null ||
            currentTarget != currentMonsterTarget.transform ||
            isRetreatingFromMonster ||
            IsDead ||
            currentMonsterTarget.currentHP <= 0)
        {
            return false;
        }

        return MatchesSmartAction("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);
    }

    bool MatchesSmartAction(string key, bool allowPrefix = false)
    {
        if (string.IsNullOrEmpty(currentAction) ||
            string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.Equals(
                currentAction,
                key,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string pattern = NpcText.Action(key);
        if (!string.IsNullOrEmpty(pattern) &&
            string.Equals(
                currentAction,
                pattern,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowPrefix)
        {
            return false;
        }

        return MatchesActionKey(
            currentAction,
            key,
            allowPrefix);
    }

    bool MatchesActionKey(
        string action,
        string key,
        bool allowPrefix = false)
    {
        if (string.IsNullOrEmpty(action) ||
            string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.Equals(
                action,
                key,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string pattern = NpcText.Action(key);
        if (!string.IsNullOrEmpty(pattern) &&
            string.Equals(
                action,
                pattern,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowPrefix)
        {
            return false;
        }

        return ActionMatchesPrefix(action, key) ||
            ActionMatchesPrefix(action, pattern);
    }

    static bool ActionMatchesPrefix(string action, string pattern)
    {
        if (string.IsNullOrEmpty(action) ||
            string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        int placeholderIndex = pattern.IndexOf('{');
        if (placeholderIndex < 0)
        {
            return action.StartsWith(
                pattern,
                System.StringComparison.OrdinalIgnoreCase);
        }

        string prefix = pattern.Substring(0, placeholderIndex).TrimEnd();
        return !string.IsNullOrEmpty(prefix) &&
            action.StartsWith(
                prefix,
                System.StringComparison.OrdinalIgnoreCase);
    }
}
