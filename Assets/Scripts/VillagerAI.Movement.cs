using UnityEngine;

// Core target movement, wander behavior, and movement-state lifecycle.
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

    bool TryWanderNearAnchor(
        Vector3 anchor,
        float radius,
        string action)
    {
        radius = Mathf.Max(0.35f, radius);

        if (!hasWanderTarget ||
            Vector2.Distance(transform.position, wanderTarget) <
                arriveDistance ||
            Vector2.Distance(wanderTarget, anchor) > radius + 0.1f ||
            !IsMoveTargetFeasible(wanderTarget))
        {
            if (!TryPickWanderTargetAround(anchor, radius, out wanderTarget))
            {
                return false;
            }

            hasWanderTarget = true;
        }

        currentTarget = null;
        hasDirectMoveTarget = false;
        currentAction = action;
        MoveToPosition(wanderTarget);
        return true;
    }

    bool TryPickWanderTargetAround(
        Vector3 anchor,
        float radius,
        out Vector3 target)
    {
        Vector3 center = ClampToCurrentMapArea(anchor);

        for (int i = 0; i < maxPickTargetAttempts; i++)
        {
            Vector2 random =
                Random.insideUnitCircle * Mathf.Max(0.1f, radius);
            Vector3 candidate =
                ClampToCurrentMapArea(
                    center + new Vector3(random.x, random.y, 0f));

            if (Vector2.Distance(transform.position, candidate) <
                Mathf.Max(arriveDistance * 2f, minWanderTargetDistance))
            {
                continue;
            }

            if (!IsMoveTargetFeasible(candidate) ||
                !HasClearLineTo(candidate))
            {
                continue;
            }

            target = candidate;
            return true;
        }

        if (TryFindClearPointNear(center, out target) &&
            Vector2.Distance(transform.position, target) >=
                Mathf.Max(arriveDistance * 2f, minWanderTargetDistance) &&
            HasClearLineTo(target))
        {
            return true;
        }

        target = transform.position;
        return false;
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
            : "tÃ i nguyÃªn";

        string action;
        switch (job)
        {
            case VillagerJob.Farmer:
                action = "Äi thu hoáº¡ch " + itemName;
                break;
            case VillagerJob.Fisher:
                action = "Äi cÃ¢u " + itemName;
                break;
            case VillagerJob.Hunter:
                action = "Äi thu thá»‹t";
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
            UnityObjectIdUtility.GetRuntimeId(gameObject) ^
            UnityObjectIdUtility.GetRuntimeId(anchor.gameObject)) % safeSlotCount;
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
        wanderUnstuckRecoveryAttempts = 0;
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
}
