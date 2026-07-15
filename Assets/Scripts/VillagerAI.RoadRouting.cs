using UnityEngine;

public partial class VillagerAI
{
    bool TryForceTeleportRouteUseNearDirectTarget(Vector3 activeDirectTarget)
    {
        if (directMoveTargetZone == null)
        {
            return false;
        }

        NpcMapZone? currentZone = GetCurrentMapZone();
        if (!currentZone.HasValue ||
            currentZone.Value == directMoveTargetZone.Value)
        {
            return false;
        }

        for (int i = 0; i < NpcTeleportGate.Gates.Count; i++)
        {
            NpcTeleportGate gate = NpcTeleportGate.Gates[i];
            if (gate == null ||
                !gate.TryGetTeleportRouteForZone(
                    currentZone.Value,
                    out _,
                    out _,
                    out _))
            {
                continue;
            }

            Vector3 gateApproach =
                gate.GetApproachPositionForZone(currentZone.Value);
            float distanceToGateApproach =
                Vector2.Distance(
                    transform.position,
                    gateApproach);
            float distanceToDirectTarget =
                Vector2.Distance(
                    gateApproach,
                    activeDirectTarget);

            if (distanceToDirectTarget > 0.25f ||
                distanceToGateApproach > Mathf.Max(
                    arriveDistance,
                    gate.npcAutoUseRadius))
            {
                continue;
            }

            LogJobRouteDebug(
                "ForceGateUse",
                "gate=" + gate.name +
                " gateApproach=" + gateApproach +
                " directMoveTarget=" + activeDirectTarget +
                " distToGate=" + distanceToGateApproach.ToString("0.00"));

            if (gate.TryForceNpcUse(gameObject))
            {
                return true;
            }
        }

        return false;
    }

    void MoveUsingRoad(Vector3 target, NpcMapZone? targetZone = null)
    {
        if (currentTarget == null &&
            !hasDirectMoveTarget &&
            !hasWanderTarget)
        {
            SetDirectMoveTarget(target, false, targetZone);
        }

        NpcMapZone? previousMovementTargetZone = movementTargetZone;
        NpcMapZone? routeZone = targetZone;

        try
        {
            bool usingTeleportRoute;
            string routeAction;
            NpcRouteStatus routeStatus;
            Vector3 requestedTarget = target;
            target = NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                target,
                targetZone,
                out usingTeleportRoute,
                out routeAction,
                out routeStatus);

            LogJobRouteDebug(
                "MoveUsingRoad",
                "requested=" + requestedTarget +
                " resolved=" + target);
            LogJobRouteDebug(
                "MoveUsingRoadRoute",
                "targetZone=" +
                (targetZone.HasValue
                    ? targetZone.Value.ToString()
                    : "None") +
                " useGate=" + usingTeleportRoute);
            LogJobRouteDebug(
                "MoveUsingRoadAction",
                "routeAction=" +
                (string.IsNullOrEmpty(routeAction)
                    ? "None"
                    : routeAction));

            if (routeStatus == NpcRouteStatus.NoGate ||
                routeStatus == NpcRouteStatus.InvalidGate)
            {
                if (!string.IsNullOrEmpty(routeAction) &&
                    CanRouteActionReplaceCurrentAction())
                {
                    currentAction = routeAction;
                    actionTimer = 0f;
                }

                StopMoving();
                return;
            }

            if (usingTeleportRoute)
            {
                routeZone = GetCurrentMapZone() ?? targetZone;
            }

            movementTargetZone = routeZone;

            if (usingTeleportRoute &&
                !string.IsNullOrEmpty(routeAction) &&
                CanRouteActionReplaceCurrentAction())
            {
                currentAction = routeAction;
                actionTimer = 0f;
            }

            if (WorldTilemapManager.Instance == null)
            {
                MoveToPosition(target, routeZone);
                return;
            }

            if (ShouldBypassRoad())
            {
                hasRoadPreference = false;
                MoveToPosition(target, routeZone);
                return;
            }

            if (!hasRoadPreference ||
                Vector2.Distance(roadPreferenceTarget, target) > 0.5f)
            {
                roadPreferenceTarget = target;
                prefersRoadForCurrentRoute =
                    ShouldForceRoadForCurrentAction() ||
                    Random.value < roadPreferenceChance;
                hasRoadPreference = true;
            }

            if (!prefersRoadForCurrentRoute)
            {
                MoveToPosition(target, routeZone);
                return;
            }

            Vector3 roadWaypoint;
            bool hasRoadRoute =
                WorldTilemapManager.Instance.TryGetRoadWaypointToTarget(
                    transform.position,
                    target,
                    routeZone,
                    IsReachableRoadTile,
                    out roadWaypoint);

            if (!hasRoadRoute)
            {
                MoveToPosition(target, routeZone);
                return;
            }

            float roadDistance =
                Vector2.Distance(
                    transform.position,
                    roadWaypoint);

            if (roadDistance > pathWaypointReachDistance)
            {
                MoveToPosition(roadWaypoint, routeZone);

                if (CanRouteActionReplaceCurrentAction())
                {
                    currentAction = NpcText.Action("walkingRoad");
                }

                return;
            }

            MoveToPosition(target, routeZone);

            if (Vector2.Distance(transform.position, target) < 0.4f)
            {
                hasRoadPreference = false;
            }
        }
        finally
        {
            movementTargetZone = previousMovementTargetZone;
        }
    }

    bool CanRouteActionReplaceCurrentAction()
    {
        if (string.IsNullOrEmpty(currentAction))
        {
            return true;
        }

        string teleportPrefix =
            NpcText.Action("teleportGateTo").Replace("{0}", "");
        return currentAction == NpcText.Action("idle") ||
            currentAction == NpcText.Action("walkingRoad") ||
            IsTravelIntentAction(currentAction) ||
            currentAction.StartsWith(teleportPrefix) ||
            currentAction.StartsWith("Äi cá»•ng dá»‹ch chuyá»ƒn");
    }

    bool TryForgePurchaseAtMarket()
    {
        if (currentForgeTradeTarget == null ||
            currentForgeTradeItem == null)
        {
            currentForgeTradeTarget =
                NpcForgeAgent.FindBestForgeForBuyer(
                    gameObject,
                    out currentForgeTradeItem);

            if (currentForgeTradeTarget == null ||
                currentForgeTradeItem == null)
            {
                currentForgeTradeTarget = null;
                currentForgeTradeItem = null;
                return false;
            }

            Transform forgePoint =
                currentForgeTradeTarget.forgeStandPoint != null
                    ? currentForgeTradeTarget.forgeStandPoint
                    : currentForgeTradeTarget.transform;

            if (forgePoint == null)
            {
                currentForgeTradeTarget = null;
                currentForgeTradeItem = null;
                return false;
            }

            currentTradeTarget = forgePoint.position;
            currentTradeTargetZone = null;
            hasTradeTarget = true;
        }

        if (currentForgeTradeTarget == null ||
            currentForgeTradeItem == null)
        {
            ClearForgeTradeTarget();
            return false;
        }

        MoveUsingRoad(currentTradeTarget, null);
        currentAction = NpcText.Action("tradeSeek");

        if (Vector2.Distance(transform.position, currentTradeTarget) >
            Mathf.Max(0.5f, arriveDistance))
        {
            return true;
        }

        ClearMovementTargets();
        StopMoving();
        hasTradeTarget = false;
        currentTradeTargetZone = null;

        string buyerLine;
        string smithLine;
        bool ordered =
            currentForgeTradeTarget.TryRequestCustomOrder(
                gameObject,
                currentForgeTradeItem,
                1,
                out buyerLine,
                out smithLine);

        ClearForgeTradeTarget();

        if (ordered)
        {
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        0.2f,
                        0.6f));
            currentAction = NpcText.Action("idle");
            return true;
        }

        currentAction = GetScheduledTradeIdleAction();
        return false;
    }

    void ClearForgeTradeTarget()
    {
        currentForgeTradeTarget = null;
        currentForgeTradeItem = null;
        hasTradeTarget = false;
    }

    void SyncCultivationEffect()
    {
        if (IsDead || hiddenAtHome)
        {
            UpdateCultivationEffect(false);
            return;
        }

        bool shouldShow =
            currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi");

        UpdateCultivationEffect(shouldShow);
    }

    void UpdateCultivationEffect(bool shouldShow)
    {
        if (!shouldShow)
        {
            if (cultivationEffectInstance != null)
            {
                cultivationEffectInstance.SetActive(false);
            }

            return;
        }

        if (cultivationEffectInstance == null)
        {
            if (cultivationEffectPrefab == null)
            {
                TryAutoAssignCultivationEffectPrefab();
            }

            if (cultivationEffectPrefab == null)
            {
                return;
            }

            cultivationEffectInstance =
                Instantiate(cultivationEffectPrefab, transform);
            cultivationEffectInstance.name = cultivationEffectPrefab.name;
        }

        Transform effectTransform = cultivationEffectInstance.transform;
        effectTransform.SetParent(transform, false);
        effectTransform.localPosition = Vector3.zero;
        effectTransform.localRotation = Quaternion.identity;

        if (!cultivationEffectInstance.activeSelf)
        {
            cultivationEffectInstance.SetActive(true);
        }
    }

    void TryAutoAssignCultivationEffectPrefab()
    {
#if UNITY_EDITOR
        if (cultivationEffectPrefab != null)
        {
            return;
        }

        cultivationEffectPrefab =
            UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Effects/CultivationEffect.prefab");
#endif
    }

    bool IsReachableRoadTile(Vector3 road)
    {
        return IsMoveTargetFeasible(road) &&
            HasClearLineTo(road);
    }

    bool ShouldForceRoadForCurrentAction()
    {
        return currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goHomeCultivate") ||
            currentAction == NpcText.Action("goCultivatePoint") ||
            currentAction == NpcText.Action("goHunt") ||
            currentAction == NpcText.Action("goTavern") ||
            currentAction == NpcText.Action("buyPill") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("tradeSeek") ||
            currentAction == NpcText.Action("moveToTask") ||
            currentAction == NpcText.Action("receiveTask");
    }

    bool ShouldBypassRoad()
    {
        float hpPercent =
            maxHP <= 0
            ? 1f
            : (float)currentHP / maxHP;

        if (hpPercent <= lowHpRoadBypassPercent)
        {
            return true;
        }

        return currentAction.Contains(NpcText.Action("panicBurned"));
    }

    void MoveToPosition(Vector3 position, NpcMapZone? targetZone = null)
    {
        NpcMapZone? previousMovementTargetZone = movementTargetZone;
        movementTargetZone = targetZone;

        try
        {
            LogJobRouteDebug(
                "MoveToPosition",
                "actorPos=" + transform.position +
                " stepTarget=" + position +
                " zone=" +
                (targetZone.HasValue
                    ? targetZone.Value.ToString()
                    : "None"));

            position = ClampToCurrentMapArea(position);
            Vector3 finalTarget = position;

            if (hasObstacleAvoidTarget)
            {
                if (Time.time >= obstacleAvoidUntil ||
                    Vector2.Distance(transform.position, obstacleAvoidTarget) <=
                    arriveDistance ||
                    !IsMoveTargetFeasible(obstacleAvoidTarget))
                {
                    hasObstacleAvoidTarget = false;
                }
                else
                {
                    position = obstacleAvoidTarget;
                    finalTarget = obstacleAvoidTarget;
                }
            }

            if (!IsMoveTargetFeasible(position))
            {
                Vector3 requestedPosition = position;
                Vector3 fallback;
                if (TryFindClearPointNear(position, out fallback) &&
                    !ShouldRejectFallbackTarget(
                        requestedPosition,
                        fallback))
                {
                    position = fallback;
                    finalTarget = position;
                }
                else
                {
                    LogMovementHaltDebug(
                        "MoveFallbackRejected",
                        "requested=" + requestedPosition +
                        " fallback=" + fallback +
                        " requestedDist=" +
                        Vector2.Distance(
                            transform.position,
                            requestedPosition).ToString("0.00") +
                        " fallbackDist=" +
                        Vector2.Distance(
                            transform.position,
                            fallback).ToString("0.00"));
                    LogJobRouteDebug(
                        "MoveBlocked",
                        "requested=" + requestedPosition +
                        " final=" + finalTarget);
                    HandleBlockedMovement(
                        requestedPosition,
                        finalTarget);
                    return;
                }
            }

            Vector2 toPosition = position - transform.position;
            if (toPosition.magnitude <= arriveDistance)
            {
                LogMovementHaltDebug(
                    "MoveStopNearTarget",
                    "actorPos=" + transform.position +
                    " stepTarget=" + position +
                    " finalTarget=" + finalTarget +
                    " dist=" +
                    toPosition.magnitude.ToString("0.00") +
                    " arriveDistance=" +
                    arriveDistance.ToString("0.00"));
                ClearActivePath();
                StopMoving();
                return;
            }

            bool usingPathWaypoint =
                TryGetSmartPathWaypoint(finalTarget, out Vector3 pathWaypoint);

            if (usingPathWaypoint)
            {
                position = pathWaypoint;
                toPosition = position - transform.position;

                if (toPosition.magnitude <= pathWaypointReachDistance)
                {
                    AdvanceActivePathWaypoint();
                    return;
                }
            }
            else if (ShouldRequirePathForDirectMove(finalTarget) &&
                TryBuildSmartPath(finalTarget) &&
                TryGetSmartPathWaypoint(finalTarget, out pathWaypoint))
            {
                position = pathWaypoint;
                toPosition = position - transform.position;
                usingPathWaypoint = true;
            }
            Vector2 direction = toPosition.normalized;

            if (!usingPathWaypoint && IsMovementBlocked(direction))
            {
                if (TryBuildSmartPath(finalTarget) &&
                    TryGetSmartPathWaypoint(finalTarget, out pathWaypoint))
                {
                    position = pathWaypoint;
                    toPosition = position - transform.position;
                    direction = toPosition.normalized;
                }
                else if (TryCommitObstacleScanTarget(direction, finalTarget))
                {
                    return;
                }
                else if (useLocalDetour &&
                    TryChooseDetourDirection(
                    direction,
                    finalTarget,
                    out Vector2 detourDirection))
                {
                    if (TryCommitObstacleAvoidTarget(detourDirection))
                    {
                        return;
                    }

                    direction = detourDirection;
                }
                else
                {
                    ClearActivePath();
                    if (TryCommitObstacleScanTarget(direction, finalTarget))
                    {
                        return;
                    }

                    if (TrySetObstacleAvoidTarget(direction, finalTarget))
                    {
                        return;
                    }

                    HandleBlockedMovement(position, finalTarget);
                    return;
                }
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                StopMoving();
                return;
            }

            if (!TryResolveCrowdAhead(direction, finalTarget, out direction))
            {
                return;
            }

            if (IsMovementBlocked(direction))
            {
                if (useLocalDetour &&
                    TryChooseDetourDirection(
                    direction,
                    finalTarget,
                    out Vector2 detourDirection))
                {
                    if (TryCommitObstacleAvoidTarget(detourDirection))
                    {
                        return;
                    }

                    direction = detourDirection;
                }
                else
                {
                    if (TryCommitObstacleScanTarget(direction, finalTarget))
                    {
                        return;
                    }

                    ClearActivePath();
                    if (TrySetObstacleAvoidTarget(direction, finalTarget))
                    {
                        return;
                    }

                    HandleBlockedMovement(position, finalTarget);
                    return;
                }
            }

            Vector2 separation = GetSeparationDirection();

            if (separation.sqrMagnitude > 0.0001f)
            {
                direction =
                    (direction + separation * separationStrength)
                    .normalized;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                StopMoving();
                return;
            }

            if (IsMovementBlocked(direction))
            {
                if (useLocalDetour &&
                    TryChooseDetourDirection(
                    direction,
                    finalTarget,
                    out Vector2 finalDetourDirection))
                {
                    direction = finalDetourDirection;
                }
                else
                {
                    if (TryCommitObstacleScanTarget(direction, finalTarget))
                    {
                        return;
                    }

                    ClearActivePath();
                    if (TrySetObstacleAvoidTarget(direction, finalTarget))
                    {
                        return;
                    }

                    HandleBlockedMovement(position, finalTarget);
                    return;
                }
            }

            blockedMoveTimer = 0f;

            if (rb != null)
            {
                desiredVelocity = direction * moveSpeed;
            }
            else
            {
                transform.position =
                    Vector3.MoveTowards(
                        transform.position,
                        position,
                        moveSpeed * Time.deltaTime);
            }
        }
        finally
        {
            movementTargetZone = previousMovementTargetZone;
        }
    }
}
