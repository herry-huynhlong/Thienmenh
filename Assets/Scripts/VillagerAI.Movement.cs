using System.Collections.Generic;
using UnityEngine;

// Directed travel, road routing, teleport continuation and movement lifecycle.
public partial class VillagerAI
{
    void Wander(string action)
    {
        if (!hasWanderTarget ||
            Vector2.Distance(transform.position, wanderTarget) <
            arriveDistance ||
            !IsMoveTargetFeasible(wanderTarget))
        {
            if (!TryPickWanderTarget(out wanderTarget))
            {
                ClearMovementTargets();
                currentAction = NpcText.Action("watchRoad");
                StopMoving();
                return;
            }

            hasWanderTarget = true;
        }

        currentTarget = null;
        hasDirectMoveTarget = false;
        currentAction = action;
        MoveToPosition(wanderTarget);
    }

    void IdleOrGoHome(string wanderAction)
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            currentAction = wanderAction;
            return;
        }

        if (ShouldReturnHomeForIdle())
        {
            GoHomeIdle(NpcText.Action("stayNearHome"));
            return;
        }

        Wander(wanderAction);
    }

    bool ShouldReturnHomeForIdle()
    {
        if (IsInDungeonCombatSession())
        {
            return false;
        }

        if (homePoint == null)
        {
            return false;
        }

        return !IsAtHomePosition(GetHomePosition());
    }

    bool IsInDungeonCombatSession()
    {
        return BicanhSessionManager.IsDungeonParticipant(gameObject);
    }

    NpcMapZone? GetHomeZone()
    {
        if (homePoint == null)
        {
            return null;
        }

        NpcMapZone? destinationZone = NpcMapNavigator.GetDestinationZone(homePoint);
        if (destinationZone.HasValue)
        {
            return destinationZone;
        }

        NpcMapArea area = NpcMapArea.FindArea(homePoint.position);
        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(homePoint.position);
        }

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    void SetTarget(Transform target, string action)
    {
        if (target == null)
        {
            currentTarget = null;
            hasWanderTarget = false;
            hasDirectMoveTarget = false;
            currentAction = action;
        SetDirectMoveTarget(GetHomePosition());
            return;
        }

        hasWanderTarget = false;
        hasDirectMoveTarget = false;
        currentTarget = target;
        currentAction = action;
    }
    public void ForceJobMoveTo(
        Vector3 target,
        string action,
        NpcMapZone? targetZone = null,
        bool useRoad = true)
    {
        if (IsDead)
        {
            return;
        }

        ClearTreasureHunt();
        actionTimer = 0f;
        currentTarget = null;

        if (!string.IsNullOrEmpty(action))
        {
            currentAction = action;
        }

        LogJobRouteDebug(
            "ForceJobMoveTo",
            "target=" + target +
            " useRoad=" + (useRoad ? 1 : 0));
        LogJobRouteDebug(
            "ForceJobZone",
            "current=" +
            (GetCurrentMapZone().HasValue
                ? GetCurrentMapZone().Value.ToString()
                : "None") +
            " target=" +
            (targetZone.HasValue
                ? targetZone.Value.ToString()
                : "None"));

        SetDirectMoveTarget(target, false, targetZone);
        directMoveTargetUsesRoad = useRoad;
        if (useRoad)
        {
            MoveUsingRoad(target, targetZone);
            return;
        }

        MoveToPosition(target, targetZone);
    }

    public void ForceGatherTarget(
        Transform target,
        StatItemData item)
    {
        if (target == null || IsDead)
        {
            return;
        }

        ClearTreasureHunt();
        actionTimer = 0f;

        string itemName = item != null
            ? ItemText.Name(item)
            : "tài nguyên";

        string action;
        switch (job)
        {
            case VillagerJob.Farmer:
                action = "Đi thu hoạch " + itemName;
                break;
            case VillagerJob.Fisher:
                action = "Đi câu " + itemName;
                break;
            case VillagerJob.Hunter:
                action = "Đi thu thịt";
                break;
            default:
                action = item != null
                    ? NpcText.ActionFormat("goGatherNamed", itemName)
                    : NpcText.Action("gatherVillageResource");
                break;
        }

        if (currentTarget == target &&
            currentAction == action)
        {
            return;
        }

        SetTarget(target, action);
    }

    bool HasArrived()
    {
        if (currentTarget == null)
        {
            return false;
        }

        return Vector2.Distance(
            transform.position,
            GetApproachPosition(currentTarget)) <= arriveDistance;
    }

    void MoveToCurrentTarget()
    {
        if (currentTarget == null)
        {
            if (hasDirectMoveTarget)
            {
                Vector3 activeDirectTarget = directMoveTarget;
                bool usingTeleportRoute = false;
                string routeAction = string.Empty;
                NpcRouteStatus routeStatus = NpcRouteStatus.Direct;

                if (directMoveTargetZone.HasValue)
                {
                    activeDirectTarget =
                        NpcMapNavigator.GetNextMoveTarget(
                            gameObject,
                            directMoveTarget,
                            directMoveTargetZone,
                            out usingTeleportRoute,
                            out routeAction,
                            out routeStatus);
                }

                if (routeStatus == NpcRouteStatus.NoGate ||
                    routeStatus == NpcRouteStatus.InvalidGate)
                {
                    if (!string.IsNullOrEmpty(routeAction))
                    {
                        currentAction = routeAction;
                    }

                    StopMoving();
                    LogMovementHaltDebug(
                        "DirectTargetNoRoute",
                        "status=" + routeStatus +
                        " actorPos=" + transform.position +
                        " directMoveTarget=" + directMoveTarget +
                        " zone=" +
                        (directMoveTargetZone.HasValue
                            ? directMoveTargetZone.Value.ToString()
                            : "None"));
                    return;
                }

                if (Vector2.Distance(transform.position, activeDirectTarget) <= arriveDistance)
                {
                    LogMovementHaltDebug(
                        "DirectTargetReached",
                        "actorPos=" + transform.position +
                        " activeDirectTarget=" +
                        activeDirectTarget +
                        " storedDirectTarget=" +
                        directMoveTarget +
                        " dist=" +
                        Vector2.Distance(
                            transform.position,
                            activeDirectTarget).ToString("0.00") +
                        " arriveDistance=" +
                        arriveDistance.ToString("0.00") +
                        " usingTeleportRoute=" +
                        (usingTeleportRoute ? 1 : 0));
                    if (usingTeleportRoute &&
                        TryForceTeleportRouteUseNearDirectTarget(
                            activeDirectTarget))
                    {
                        return;
                    }

                    hasDirectMoveTarget = false;
                    directMoveTargetZone = null;
                    directMoveTargetUsesRoad = true;
                    StopMoving();
                    return;
                }

                if (directMoveTargetUsesRoad)
                {
                    MoveUsingRoad(directMoveTarget, directMoveTargetZone);
                }
                else
                {
                    NpcMapZone? currentZone = GetCurrentMapZone();
                    if (directMoveTargetZone.HasValue &&
                        (!currentZone.HasValue ||
                        currentZone.Value != directMoveTargetZone.Value))
                    {
                        MoveUsingRoad(directMoveTarget, directMoveTargetZone);
                        return;
                    }

                    MoveToPosition(directMoveTarget, directMoveTargetZone);
                }
                return;
            }

            if (hasWanderTarget)
            {
                MoveToPosition(wanderTarget);
            }
            else
            {
                StopMoving();
            }

            return;
        }

        MoveUsingRoad(
            GetApproachPosition(currentTarget),
            GetTargetZone(currentTarget));
    }

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
    NpcMapZone? GetTargetZone(Transform target)
    {
        NpcMapZone? destinationZone = NpcMapNavigator.GetDestinationZone(target);
        if (destinationZone.HasValue)
        {
            return destinationZone;
        }

        NpcMapArea area = target != null
            ? NpcMapArea.FindArea(target.position)
            : null;

        if (area != null)
        {
            return area.zone;
        }

        NpcMapArea nearestArea = target != null
            ? NpcMapArea.FindNearestArea(target.position)
            : null;

        return nearestArea != null
            ? nearestArea.zone
            : (NpcMapZone?)null;
    }

    Vector3 GetWorkPointPosition(VillagerJob targetJob)
    {
        NpcScheduleActivity requestedActivity =
            GetCurrentScheduleActivityForWorkTarget(targetJob);

        NpcMapZone? preferredZone = null;
        if (targetJob == VillagerJob.Fisher ||
            targetJob == VillagerJob.Hunter)
        {
            preferredZone = GetPreferredResourceGatherZone();
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                requestedActivity,
                targetJob,
                GetWorkLocationPurpose(targetJob),
                preferredZone,
                null,
                transform.position,
                out Vector3 registryWorkPoint,
                out NpcMapZone? registryWorkZone))
        {
            if (targetJob == VillagerJob.Fisher &&
                registryWorkZone.HasValue &&
                registryWorkZone.Value != NpcMapZone.Lang)
            {
                currentWorkTargetZone = NpcMapZone.Lang;
                return Vector3.zero;
            }

            currentWorkTargetZone = registryWorkZone;
            return registryWorkPoint;
        }

        if (targetJob == VillagerJob.Fisher)
        {
            if (workPoint == null)
            {
                return Vector3.zero;
            }

            currentWorkTargetZone =
                NpcMapNavigator.GetDestinationZone(workPoint);
            if (currentWorkTargetZone.HasValue &&
                currentWorkTargetZone.Value != NpcMapZone.Lang)
            {
                return Vector3.zero;
            }
        }

        if (workPoint == null)
        {
            return Vector3.zero;
        }

        currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);

        NpcWorkArea area = workPoint.GetComponent<NpcWorkArea>();
        if (area != null)
        {
            return area.job == targetJob
                ? area.GetRandomPoint(gameObject)
                : Vector3.zero;
        }

        return GetDistributedPointAround(workPoint.position, workPoint);
    }

    NpcScheduleActivity GetCurrentScheduleActivityForWorkTarget(
        VillagerJob targetJob)
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule != null && schedule.enforceSchedule)
        {
            NpcScheduleActivity activity = schedule.CurrentActivity;
            if (activity == NpcScheduleActivity.Hunt ||
                activity == NpcScheduleActivity.Gather ||
                activity == NpcScheduleActivity.Work)
            {
                return activity;
            }
        }

        return targetJob == VillagerJob.Hunter
            ? NpcScheduleActivity.Hunt
            : NpcScheduleActivity.Work;
    }

    NpcLocationPurpose GetWorkLocationPurpose(VillagerJob targetJob)
    {
        switch (targetJob)
        {
            case VillagerJob.Fisher:
                return NpcLocationPurpose.Fishing;
            case VillagerJob.Hunter:
                return NpcLocationPurpose.Hunt;
            default:
                return NpcLocationPurpose.Work;
        }
    }

    Vector3 GetDistributedPointAround(
        Vector3 center,
        Transform anchor,
        int slotCount = 12)
    {
        if (anchor == null)
        {
            return center;
        }

        int safeSlotCount = Mathf.Max(6, slotCount);
        int startSlotIndex = Mathf.Abs(
            gameObject.GetInstanceID() ^
            anchor.gameObject.GetInstanceID()) % safeSlotCount;
        float radius =
            Mathf.Max(
                arriveDistance,
                sharedAnchorSpacingRadius,
                sharedTargetOccupancyRadius * 2f);
        Vector3 fallback = center;

        for (int i = 0; i < safeSlotCount; i++)
        {
            int slotIndex = (startSlotIndex + i) % safeSlotCount;
            float angle = (Mathf.PI * 2f * slotIndex) / safeSlotCount;
            Vector3 candidate = ClampToCurrentMapArea(
                center +
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f) * radius);

            if (!IsSharedTargetOccupied(candidate))
            {
                return candidate;
            }

            fallback = candidate;
        }

        return fallback;
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
            currentAction.StartsWith("Đi cổng dịch chuyển");
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
    public void StopMoving()
    {
        if (rb != null)
        {
            desiredVelocity = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void StopForConversation()
    {
        StopForConversation(2f);
    }

    public void StopForConversation(float duration)
    {
        movementPausedUntil = Mathf.Max(
            movementPausedUntil,
            Time.time + Mathf.Max(0.2f, duration));
        StopMoving();
    }

    void ClearMovementTargets()
    {
        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = false;
        hasObstacleAvoidTarget = false;
        movementTargetZone = null;
        directMoveTargetZone = null;
        directMoveTargetUsesRoad = true;
        ClearActivePath();
    }

    bool IsBusyActionActive()
    {
        return actionTimer > 0f &&
            !IsMovementAction(currentAction);
    }

    bool IsMovementAction(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        return action == NpcText.Action("goFarmWork") ||
            action == NpcText.Action("goWork") ||
            action == NpcText.Action("goPatrol") ||
            action == NpcText.Action("goHeal") ||
            action == NpcText.Action("goFish") ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("goMarketTrade") ||
            action == NpcText.Action("bringGoodsToCounter") ||
            action == NpcText.Action("goHomeRest") ||
            action == NpcText.Action("eatAtShop") ||
            action == NpcText.Action("goPlay") ||
            action == NpcText.Action("walkingRoad") ||
            action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("goTaskProviderDaily") ||
            action == NpcText.Action("goHomeCultivate") ||
            action == NpcText.Action("goCultivatePoint") ||
            action.StartsWith(NpcText.Action("goGatherNamed")
                .Replace("{0}", "")) ||
            action.StartsWith(NpcText.Action("teleportGateTo")
                .Replace("{0}", ""));
    }
    void SetDirectMoveTarget(
        Vector3 position,
        bool preserveCurrentTarget = false,
        NpcMapZone? targetZone = null)
    {
        if (!preserveCurrentTarget)
        {
            currentTarget = null;
            hasObstacleAvoidTarget = false;
        }

        hasWanderTarget = false;
        hasDirectMoveTarget = true;
        directMoveTargetZone = targetZone;
        directMoveTargetUsesRoad = true;

        NpcMapZone? currentZone = GetCurrentMapZone();
        bool crossZoneTarget =
            targetZone.HasValue &&
            currentZone.HasValue &&
            targetZone.Value != currentZone.Value;

        Vector3 resolvedTarget;
        if (crossZoneTarget)
        {
            // Đừng clamp target ở map khác vào rìa map hiện tại.
            // Cứ giữ target thật, MoveUsingRoad sẽ tự đổi bước đầu thành cổng dịch chuyển.
            resolvedTarget = position;
        }
        else
        {
            Vector3 clamped = ClampToCurrentMapArea(position);
            Vector3 clearTarget;
            bool foundClearTarget =
                TryFindClearPointNear(clamped, out clearTarget);
            if (foundClearTarget &&
                ShouldRejectFallbackTarget(clamped, clearTarget))
            {
                LogMovementHaltDebug(
                    "DirectTargetFallbackRejected",
                    "requested=" + position +
                    " clamped=" + clamped +
                    " fallback=" + clearTarget +
                    " requestedDist=" +
                    Vector2.Distance(
                        transform.position,
                        clamped).ToString("0.00") +
                    " fallbackDist=" +
                    Vector2.Distance(
                        transform.position,
                        clearTarget).ToString("0.00"));
                resolvedTarget = clamped;
            }
            else
            {
                resolvedTarget = foundClearTarget
                    ? clearTarget
                    : clamped;
            }
        }

        if (Vector2.Distance(directMoveTarget, resolvedTarget) >
            pathReplanTargetDistance)
        {
            ClearActivePath();
        }

        directMoveTarget = resolvedTarget;
    }

    NpcMapZone? GetCurrentMapZone()
    {
        RefreshCurrentMapArea();
        return currentMapArea != null
            ? currentMapArea.zone
            : (NpcMapZone?)null;
    }

    void RefreshCurrentMapArea(bool allowNearest = false)
    {
        if (!keepInsideNpcMapArea)
        {
            currentMapArea = null;
            return;
        }

        NpcMapArea area = NpcMapArea.FindArea(transform.position);

        if (area == null &&
            NpcMapNavigator.TryGetKnownNpcZone(
                gameObject,
                out NpcMapZone knownZone))
        {
            area = NpcMapArea.FindNearestAreaInZone(
                knownZone,
                transform.position);
        }

        if (area == null)
        {
            return;
        }

        if (allowCrossNpcMapAreas ||
            allowNearest ||
            currentMapArea == null ||
            area == currentMapArea)
        {
            currentMapArea = area;
        }
    }

    void ClampInsideCurrentMapArea()
    {
        if (!keepInsideNpcMapArea)
        {
            return;
        }

        if (allowCrossNpcMapAreas)
        {
            return;
        }

        if (currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return;
        }

        Vector3 clamped = ClampToCurrentMapArea(transform.position);
        if (Vector2.Distance(clamped, transform.position) <= 0.001f)
        {
            return;
        }

        if (rb != null)
        {
            rb.position = clamped;
            rb.linearVelocity = Vector2.zero;
            desiredVelocity = Vector2.zero;
        }

        transform.position = new Vector3(
            clamped.x,
            clamped.y,
            transform.position.z);
    }

    Vector3 ClampToCurrentMapArea(Vector3 position)
    {
        if (!keepInsideNpcMapArea)
        {
            return position;
        }

        if (allowCrossNpcMapAreas)
        {
            return position;
        }

        if (currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return position;
        }

        if (IsTeleportEntryTargetForCurrentArea(position))
        {
            return position;
        }

        if (movementTargetZone.HasValue &&
            movementTargetZone.Value == currentMapArea.zone)
        {
            NpcMapArea areaAtPosition = NpcMapArea.FindArea(position);
            if (areaAtPosition != null &&
                areaAtPosition.zone == currentMapArea.zone)
            {
                return position;
            }

            NpcMapArea nearestSameZone =
                NpcMapArea.FindNearestAreaInZone(
                    currentMapArea.zone,
                    position);

            if (nearestSameZone != null &&
                nearestSameZone.areaBounds != null)
            {
                Vector3 nearestPoint = nearestSameZone.ClosestPoint(position);
                if (Vector2.Distance(nearestPoint, position) <=
                    Mathf.Max(0.05f, mapAreaEdgePadding + targetClearRadius))
                {
                    return nearestPoint;
                }
            }
        }

        Collider2D boundsCollider = currentMapArea.areaBounds;
        Vector2 point = position;
        Vector2 closest = boundsCollider.ClosestPoint(point);
        if (Vector2.Distance(closest, point) <= 0.02f)
        {
            return position;
        }

        Bounds bounds = boundsCollider.bounds;
        float padding = Mathf.Max(0f, mapAreaEdgePadding);
        Vector2 candidate = new Vector2(
            Mathf.Clamp(point.x, bounds.min.x + padding, bounds.max.x - padding),
            Mathf.Clamp(point.y, bounds.min.y + padding, bounds.max.y - padding));

        if (IsInsideCurrentMapArea(candidate))
        {
            return new Vector3(candidate.x, candidate.y, position.z);
        }

        closest = boundsCollider.ClosestPoint(candidate);
        Vector2 inward = (Vector2)bounds.center - closest;
        if (inward.sqrMagnitude > 0.0001f)
        {
            closest += inward.normalized * padding;
        }

        return new Vector3(closest.x, closest.y, position.z);
    }

    bool IsInsideCurrentMapArea(Vector2 position)
    {
        if (allowCrossNpcMapAreas ||
            currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return true;
        }

        if (IsTeleportEntryTargetForCurrentArea(position))
        {
            return true;
        }

        if (movementTargetZone.HasValue &&
            movementTargetZone.Value == currentMapArea.zone)
        {
            NpcMapArea areaAtPosition = NpcMapArea.FindArea(position);
            if (areaAtPosition != null &&
                areaAtPosition.zone == currentMapArea.zone)
            {
                return true;
            }

            NpcMapArea nearestSameZone =
                NpcMapArea.FindNearestAreaInZone(
                    currentMapArea.zone,
                    position);

            if (nearestSameZone != null &&
                nearestSameZone.DistanceTo(position) <=
                Mathf.Max(0.05f, mapAreaEdgePadding + targetClearRadius))
            {
                return true;
            }
        }

        Vector2 closest = currentMapArea.areaBounds.ClosestPoint(position);
        return Vector2.Distance(closest, position) <= 0.02f;
    }

    bool IsTeleportEntryTargetForCurrentArea(Vector3 position)
    {
        if (currentMapArea == null ||
            !movementTargetZone.HasValue ||
            movementTargetZone.Value == currentMapArea.zone)
        {
            return false;
        }

        float entryTolerance =
            Mathf.Max(0.35f, targetClearRadius * 2f);

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                gate.fromZone != currentMapArea.zone ||
                gate.toZone != movementTargetZone.Value)
            {
                continue;
            }

            if (Vector2.Distance(position, gate.EntryPosition) <= entryTolerance)
            {
                return true;
            }
        }

        return false;
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;
        bool shouldResumeHomeReturn =
            isReturningHome ||
            currentAction == NpcText.Action("goHomeRest");
        bool hadActiveMoveTarget =
            currentTarget != null ||
            hasDirectMoveTarget ||
            hasWanderTarget;

        Vector3 referencePosition = gate != null
            ? gate.ExitPosition
            : transform.position;

        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindArea(referencePosition);
        }

        NpcMapZone? resolvedZone =
            area != null
                ? area.zone
                : NpcMapNavigator.ResolveActorZone(gameObject);

        if (gate != null)
        {
            if (resolvedZone.HasValue)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedZone.Value);
            }
            else
            {
                NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
                resolvedZone = gate.toZone;
            }

            area = NpcMapNavigator.ResolveMapAreaAfterTeleport(
                gameObject,
                resolvedZone.Value,
                referencePosition);
        }
        else if (area != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
        }

        currentMapArea = area;
        desiredVelocity = Vector2.zero;
        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = false;
        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        treasureWaitLowPowerSkirmish = false;
        hasRoadPreference = false;
        prefersRoadForCurrentRoute = false;
        hasObstacleAvoidTarget = false;
        movementTargetZone = null;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        blockedMoveTimer = 0f;
        crowdBlockedTimer = 0f;
        stuckMoveTimer = 0f;
        thinkTimer = 0f;
        actionTimer = 0f;
        currentAction = NpcText.Action("idle");
        ClearActivePath();
        UpdateCultivationEffect(false);

        LogJobRouteDebug(
            "OnNpcMapTeleported",
            "gate=" + (gate != null ? gate.name : "null") +
            " resolvedZone=" +
            (resolvedZone.HasValue
                ? resolvedZone.Value.ToString()
                : "None") +
            " actorPos=" + transform.position);

        if (shouldResumeHomeReturn && !hiddenAtHome)
        {
            Vector3 homePosition = GetHomePosition();
            Vector3 travelTarget = homePosition;
            TryResolveHomeTravelTarget(ref travelTarget);

            isReturningHome = true;
            currentAction = NpcText.Action("goHomeRest");
            SetDirectMoveTarget(travelTarget, false, GetHomeZone());
            MoveUsingRoad(travelTarget, GetHomeZone());

            if (IsAtHomePosition(homePosition) ||
                IsAtResolvedHomeTravelPosition(homePosition, travelTarget))
            {
                CompleteHomeArrival();
            }
        }
        else if (!hadActiveMoveTarget && gate != null)
        {
            Vector2 awayFromGate =
                ((Vector2)gate.ExitPosition - (Vector2)gate.EntryPosition);

            if (awayFromGate.sqrMagnitude <= 0.0001f)
            {
                awayFromGate = Vector2.up;
            }

            Vector3 nudgeTarget =
                gate.ExitPosition +
                (Vector3)(awayFromGate.normalized *
                Mathf.Max(0.75f, targetClearRadius * 3f));

            if (TryFindClearPointNear(nudgeTarget, out Vector3 clearPoint))
            {
                SetDirectMoveTarget(clearPoint);
            }
        }

        ClampInsideCurrentMapArea();
    }

}
