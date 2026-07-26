using UnityEngine;

// Unstuck recovery, obstacle probing, and local detour scoring.
public partial class SmartNpcAI
{
    static readonly float[] DetourAngles =
    {
        0f,
        20f,
        35f,
        50f,
        70f,
        90f,
        120f,
        150f
    };

    void UpdateUnstuck(Vector2 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            stuckMoveTimer = 0f;
            unstuckRecoveryAttempts = 0;
            lastUnstuckPosition = transform.position;
            return;
        }

        float moved = Vector2.Distance(
            transform.position,
            lastUnstuckPosition);

        if (moved <= unstuckMinMoveDistance)
        {
            stuckMoveTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckMoveTimer = 0f;
            unstuckRecoveryAttempts = 0;
            lastUnstuckPosition = transform.position;
        }

        if (stuckMoveTimer < unstuckCheckDelay)
        {
            return;
        }

        unstuckRecoveryAttempts++;
        if (unstuckRecoveryAttempts >=
            Mathf.Max(1, maxUnstuckRecoveriesBeforeAbort))
        {
            RecoverFromRepeatedMovementStall(moveDirection);
            return;
        }

        if (TryPickUnstuckEscapeTarget(moveDirection, out escapeTarget))
        {
            hasEscapeTarget = true;
            hasObstacleAvoidTarget = false;
            blockedMoveTimer = 0f;
        }
        else
        {
            Vector3 finalTarget =
                hasEscapeTarget
                ? escapeTarget
                : currentTarget != null
                ? GetApproachPosition(currentTarget)
                : hasWanderTarget
                    ? wanderTarget
                    : transform.position + (Vector3)moveDirection;

            hasEscapeTarget = false;
            HandleBlockedMovement(
                transform.position + (Vector3)moveDirection,
                finalTarget);
        }

        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;
    }

    void RecoverFromRepeatedMovementStall(Vector2 moveDirection)
    {
        Transform stalledTarget = currentTarget;
        Vector3 finalTarget =
            stalledTarget != null
                ? GetApproachPosition(stalledTarget)
                : hasWanderTarget
                    ? wanderTarget
                    : transform.position + (Vector3)moveDirection;
        NpcMapZone? currentZone =
            NpcMapNavigator.ResolveActorZone(gameObject);
        NpcMapArea currentArea =
            NpcMapArea.FindArea(transform.position);
        Vector3 routeTarget = NpcMapNavigator.GetNextMoveTarget(
            gameObject,
            finalTarget,
            out bool usingTeleportRoute,
            out _,
            out _);

        unstuckRecoveryAttempts = 0;
        stuckMoveTimer = 0f;
        blockedMoveTimer = 0f;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;

        if (usingTeleportRoute &&
            TryRecoverBlockedTeleportRoute(
                routeTarget,
                currentZone,
                currentArea))
        {
            return;
        }

        if (currentAction == NpcText.Action("gatherResource"))
        {
            if (resourceGatherer == null)
            {
                resourceGatherer = GetComponent<NpcResourceGatherer>();
            }

            if (resourceGatherer != null &&
                resourceGatherer.TryRecoverSmartNpcStalledGather(
                    stalledTarget))
            {
                DebugFlow(
                    "Unstuck",
                    "Retargeted stalled resource gathering");
                return;
            }
        }

        ClearTravelTargetsAndStop();
        currentAction = NpcText.Action("idle");
        if (IsRoutineBlockingTask(currentSmartTask))
        {
            ClearSmartTask();
        }

        if (IsRoutineBlockingTask(scheduleSmartTask))
        {
            ClearScheduledTask();
        }

        if (resourceGatherer != null)
        {
            resourceGatherer.CancelGatheringNow();
        }

        actionTimer = 0f;
        thinkTimer = thinkDelay;
        lastUnstuckPosition = transform.position;
        DebugFlow(
            "Unstuck",
            "Aborted repeated movement stall target=" +
            (stalledTarget != null ? stalledTarget.name : "null"));
    }

    bool TryPickUnstuckEscapeTarget(
        Vector2 moveDirection,
        out Vector3 target)
    {
        Vector2 direction = moveDirection.sqrMagnitude > 0.0001f
            ? moveDirection.normalized
            : Random.insideUnitCircle.normalized;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.up;
        }

        Vector2 side = new Vector2(-direction.y, direction.x);
        int sideSign =
            (UnityObjectIdUtility.GetRuntimeId(this) & 1) == 0 ? 1 : -1;

        Vector2[] directions =
        {
            side * sideSign,
            -side * sideSign,
            (side * sideSign - direction * 0.5f).normalized,
            (-side * sideSign - direction * 0.5f).normalized,
            -direction,
            (side * sideSign + direction * 0.25f).normalized,
            (-side * sideSign + direction * 0.25f).normalized
        };

        float baseDistance =
            Mathf.Max(0.35f, unstuckOffsetRadius, targetClearRadius * 3f);
        float bestScore = float.NegativeInfinity;
        Vector3 best = transform.position;
        bool found = false;

        for (int radiusStep = 0; radiusStep < 4; radiusStep++)
        {
            float distance =
                baseDistance + radiusStep * Mathf.Max(targetClearRadius * 2f, 0.35f);

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 escapeDirection = directions[i];
                if (escapeDirection.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                escapeDirection.Normalize();
                Vector3 candidate =
                    ClampToCurrentMapArea(
                        transform.position +
                        (Vector3)(escapeDirection * distance));

                candidate.z = transform.position.z;

                if (!IsMoveTargetFeasible(candidate) ||
                    !HasClearLineTo(candidate) ||
                    IsMovementBlocked(escapeDirection))
                {
                    continue;
                }

                float score =
                    GetClearDistance(escapeDirection, GetObstacleLookAheadDistance()) +
                    Mathf.Max(-0.25f, Vector2.Dot(escapeDirection, -direction)) *
                    baseDistance;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }
        }

        target = best;
        return found;
    }

    bool IsMoveTargetFeasible(Vector3 position)
    {
        return IsInsideCurrentMapArea(position) &&
            !IsPositionBlocked(position);
    }

    bool IsPositionBlocked(Vector3 position)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                GetBodyClearRadius());

        foreach (Collider2D hit in hits)
        {
            if (IsBlockingObstacle(hit))
            {
                DebugFlow(
                    "PositionBlocked",
                    "probe=" + position +
                    " radius=" + GetBodyClearRadius().ToString("0.00") +
                    " obstacle=" + DescribeObstacle(hit));
                return true;
            }
        }

        return false;
    }

    float GetBodyClearRadius()
    {
        float radius = Mathf.Max(0.01f, targetClearRadius + navigationClearancePadding);

        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>();
        }

        foreach (Collider2D own in selfColliders)
        {
            if (own == null || own.isTrigger)
            {
                continue;
            }

            Bounds bounds = own.bounds;
            radius =
                Mathf.Max(
                    radius,
                    bounds.extents.x,
                    bounds.extents.y);
        }

        return radius;
    }

    bool HasClearLineTo(Vector3 target)
    {
        Vector2 origin = transform.position;
        Vector2 delta = (Vector2)target - origin;
        float distance = delta.magnitude;

        if (distance <= targetClearRadius)
        {
            return true;
        }

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                origin,
                Mathf.Max(0.01f, GetBodyClearRadius()),
                delta.normalized,
                distance);

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                return false;
            }
        }

        return true;
    }

    bool IsMovementBlocked(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                transform.position,
                Mathf.Max(0.01f, GetBodyClearRadius()),
                direction.normalized,
                Mathf.Max(obstacleCheckDistance, obstacleDetourLookAhead));

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                return true;
            }
        }

        return false;
    }

    bool TryChooseObstacleDetourDirection(
        Vector2 desiredDirection,
        Vector3 finalTarget,
        out Vector2 detourDirection)
    {
        detourDirection = desiredDirection;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = desiredDirection.normalized;
        Vector2 targetDirection =
            ((Vector2)finalTarget - (Vector2)transform.position);
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            targetDirection = desired;
        }
        else
        {
            targetDirection.Normalize();
        }

        float lookAhead = GetObstacleLookAheadDistance();
        float detourStep = Mathf.Max(
            targetClearRadius * 3f,
            obstacleDetourLookAhead * 2f,
            moveSpeed * 0.75f);
        float bestScore = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < DetourAngles.Length; i++)
        {
            float angle = DetourAngles[i];

            if (TryScoreObstacleDetourDirection(
                    RotateDirection(desired, angle),
                    desired,
                    targetDirection,
                    Mathf.Max(lookAhead, detourStep),
                    out float score) &&
                score > bestScore)
            {
                bestScore = score;
                detourDirection = RotateDirection(desired, angle);
                found = true;
            }

            if (Mathf.Approximately(angle, 0f))
            {
                continue;
            }

            if (TryScoreObstacleDetourDirection(
                    RotateDirection(desired, -angle),
                    desired,
                    targetDirection,
                    Mathf.Max(lookAhead, detourStep),
                    out score) &&
                score > bestScore)
            {
                bestScore = score;
                detourDirection = RotateDirection(desired, -angle);
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        detourDirection.Normalize();
        return true;
    }

    bool TryCommitObstacleAvoidTarget(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = direction.normalized;
        float distance = Mathf.Max(
            unstuckOffsetRadius * 1.5f,
            obstacleDetourLookAhead * 1.5f,
            targetClearRadius * 4f,
            moveSpeed * 0.5f);

        Vector3 candidate =
            ClampToCurrentMapArea(
                transform.position +
                (Vector3)(desired * distance));

        Vector3 clearPoint;
        if (!TryFindClearPointNear(candidate, out clearPoint))
        {
            clearPoint = candidate;
        }

        if (!IsMoveTargetFeasible(clearPoint) ||
            !HasClearLineTo(clearPoint))
        {
            return false;
        }

        obstacleAvoidTarget = clearPoint;
        obstacleAvoidUntil = Time.time + 1.1f;
        hasObstacleAvoidTarget = true;
        blockedMoveTimer = 0f;
        return true;
    }

    bool TryCommitObstacleScanTarget(
        Vector2 desiredDirection,
        Vector3 finalTarget)
    {
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = desiredDirection.normalized;
        Vector2 targetDirection =
            (Vector2)finalTarget - (Vector2)transform.position;

        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            targetDirection = desired;
        }
        else
        {
            targetDirection.Normalize();
        }

        float scanDistance = Mathf.Max(
            obstacleCheckDistance * 2f,
            GetObstacleLookAheadDistance() * 2f);
        float scanStep = Mathf.Max(0.12f, targetClearRadius);
        float startDistance = Mathf.Max(
            targetClearRadius * 2f,
            obstacleCheckDistance * 0.75f);
        Vector2 side = new Vector2(-desired.y, desired.x);
        Vector2 sideOffset =
            side * Mathf.Max(targetClearRadius * 1.5f, 0.3f);

        bool sawBlocked = false;

        for (float distance = startDistance;
             distance <= scanDistance;
             distance += scanStep)
        {
            Vector2 forwardPoint =
                (Vector2)transform.position + desired * distance;

            bool forwardBlocked =
                !IsInsideCurrentMapArea(forwardPoint) ||
                IsPositionBlocked(forwardPoint) ||
                !HasClearLineTo(forwardPoint);

            if (forwardBlocked)
            {
                sawBlocked = true;
            }

            if (!sawBlocked)
            {
                continue;
            }

            Vector2[] candidates =
            {
                forwardPoint,
                forwardPoint + sideOffset,
                forwardPoint - sideOffset
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 candidate =
                    ClampToCurrentMapArea(candidates[i]);

                if (!IsInsideCurrentMapArea(candidate) ||
                    IsPositionBlocked(candidate) ||
                    !HasClearLineTo(candidate))
                {
                    continue;
                }

                Vector2 toCandidate =
                    candidate - (Vector2)transform.position;

                if (toCandidate.sqrMagnitude <= 0.0001f ||
                    Vector2.Dot(
                        toCandidate.normalized,
                        targetDirection) < -0.05f)
                {
                    continue;
                }

                obstacleAvoidTarget =
                    new Vector3(
                        candidate.x,
                        candidate.y,
                        transform.position.z);
                obstacleAvoidUntil = Time.time + 1.15f;
                hasObstacleAvoidTarget = true;
                hasEscapeTarget = false;
                blockedMoveTimer = 0f;
                return true;
            }
        }

        return false;
    }

    bool TryScoreObstacleDetourDirection(
        Vector2 candidate,
        Vector2 desired,
        Vector2 targetDirection,
        float lookAhead,
        out float score)
    {
        score = 0f;

        if (candidate.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        candidate.Normalize();

        Vector3 nextPoint =
            transform.position +
            (Vector3)(candidate * Mathf.Max(targetClearRadius * 3f, lookAhead * 0.85f));

        if (!IsMoveTargetFeasible(nextPoint) ||
            !HasClearLineTo(nextPoint))
        {
            return false;
        }

        float clearDistance = GetClearDistance(candidate, lookAhead);
        if (clearDistance < targetClearRadius * 2f)
        {
            return false;
        }

        float progressScore = Mathf.Max(-0.5f, Vector2.Dot(candidate, targetDirection));
        if (progressScore < -0.05f)
        {
            return false;
        }

        float smoothScore = Mathf.Max(-0.5f, Vector2.Dot(candidate, desired));

        score =
            clearDistance / Mathf.Max(0.01f, lookAhead) * 3f +
            progressScore * 2f +
            smoothScore;

        return true;
    }

    bool TryFindClearPointNear(
        Vector3 preferred,
        out Vector3 result,
        bool requireClearLine = true)
    {
        preferred = ClampToCurrentMapArea(preferred);
        preferred.z = transform.position.z;

        if (IsMoveTargetFeasible(preferred))
        {
            result = preferred;
            return true;
        }

        float baseRadius = Mathf.Max(targetClearRadius * 2f, 0.25f);
        for (int radiusStep = 0; radiusStep < 6; radiusStep++)
        {
            float radius = baseRadius + radiusStep * 0.2f;
            for (int angleStep = 0; angleStep < 16; angleStep++)
            {
                float angle =
                    (angleStep / 16f) * Mathf.PI * 2f +
                    radiusStep * 0.17f;
                Vector2 offset =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    radius;
                Vector3 candidate =
                    ClampToCurrentMapArea(
                        preferred + new Vector3(offset.x, offset.y, 0f));

                if (IsMoveTargetFeasible(candidate) &&
                    (!requireClearLine || HasClearLineTo(candidate)))
                {
                    result = candidate;
                    return true;
                }
            }
        }

        result = transform.position;
        return false;
    }

    Vector3 GetBlockedEscapeSeed(Vector3 blockedTarget, Vector3 finalTarget)
    {
        Vector2 away = (Vector2)(transform.position - blockedTarget);
        Vector2 towardFinal = (Vector2)finalTarget - (Vector2)transform.position;

        if (towardFinal.sqrMagnitude > 0.0001f)
        {
            towardFinal.Normalize();
            away += towardFinal * 0.45f;
        }

        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Random.insideUnitCircle;
        }

        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.up;
        }

        away.Normalize();

        return ClampToCurrentMapArea(
            transform.position +
            (Vector3)(away * Mathf.Max(unstuckOffsetRadius, targetClearRadius * 3f)));
    }

    void HandleBlockedMovement(Vector3 blockedTarget, Vector3 finalTarget)
    {
        blockedMoveTimer += Time.fixedDeltaTime;
        StopMovingSmooth();

        NpcMapArea currentArea = NpcMapArea.FindArea(transform.position);
        NpcMapArea blockedArea = NpcMapArea.FindArea(blockedTarget);
        NpcMapArea finalArea = NpcMapArea.FindArea(finalTarget);
        NpcMapZone? currentZone = NpcMapNavigator.ResolveActorZone(gameObject);
        NpcMapZone? targetZone =
            currentTarget != null
                ? NpcMapNavigator.GetDestinationZone(currentTarget)
                : (NpcMapZone?)null;
        if (!targetZone.HasValue && finalArea != null)
        {
            targetZone = finalArea.zone;
        }

        bool shouldLogBlockedMovement =
            blockedMoveTimer >= blockedTargetRetryDelay ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget;
        if (shouldLogBlockedMovement)
        {
            DebugFlow(
                "Move",
                "Blocked movement blocked=" +
                blockedTarget +
                " final=" +
                finalTarget +
                " timer=" +
                blockedMoveTimer.ToString("0.00") +
                " escape=" +
                hasEscapeTarget +
                " obstacle=" +
                hasObstacleAvoidTarget +
                " currentZone=" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                " currentArea=" +
                (currentArea != null ? currentArea.name : "null") +
                " blockedArea=" +
                (blockedArea != null ? blockedArea.name : "null") +
                " finalArea=" +
                (finalArea != null ? finalArea.name : "null") +
                " targetZone=" +
                (targetZone.HasValue ? targetZone.Value.ToString() : "None") +
                " target=" +
                (currentTarget != null ? currentTarget.name : "null") +
                " wander=" +
                hasWanderTarget +
                " wanderTarget=" +
                wanderTarget);
            TraceRuntime(
                "HandleBlockedMovement",
                "blockedTarget=" + blockedTarget +
                " finalTarget=" + finalTarget +
                " timer=" + blockedMoveTimer.ToString("0.00") +
                " action=" + currentAction +
                " target=" + (currentTarget != null ? currentTarget.name : "null") +
                " wander=" + hasWanderTarget +
                " wanderTarget=" + wanderTarget);
        }

        if (blockedMoveTimer < blockedTargetRetryDelay)
        {
            if (currentAction == NpcText.Action("goVanBaoLauBroker"))
            {
                NpcCounterBroker broker =
                    NpcCounterBroker.FindBestBrokerForNpc(gameObject);
                Vector3 brokerApproach =
                    broker != null
                        ? ResolveBrokerApproachPosition(broker)
                        : finalTarget;
                if (TryHandleBrokerPillPurchaseIfReady(
                        broker,
                        brokerApproach,
                        0.2f))
                {
                    DebugFlow(
                        "Move",
                        "Blocked near broker, handled trade immediately");
                    blockedMoveTimer = 0f;
                    return;
                }
            }

            if (currentAction == NpcText.Action("goTaskProviderDaily") &&
                TryVisitTaskProvider())
            {
                DebugFlow(
                    "Move",
                    "Blocked near task provider, handled visit immediately");
                TraceRuntime(
                    "HandleBlockedMovement",
                    "handled-near-provider action=" + currentAction +
                    " target=" + (currentTarget != null ? currentTarget.name : "null") +
                    " wander=" + hasWanderTarget);
                blockedMoveTimer = 0f;
            }

            return;
        }

        blockedMoveTimer = 0f;
        unstuckRecoveryAttempts++;
        if (unstuckRecoveryAttempts >=
            Mathf.Max(1, maxUnstuckRecoveriesBeforeAbort))
        {
            Vector2 recoveryDirection =
                (Vector2)finalTarget - (Vector2)transform.position;
            RecoverFromRepeatedMovementStall(recoveryDirection);
            return;
        }

        if (TryRecoverBlockedTeleportRoute(
                blockedTarget,
                currentZone,
                currentArea))
        {
            return;
        }

        if (currentAction == NpcText.Action("goVanBaoLauBroker"))
        {
            NpcCounterBroker broker =
                NpcCounterBroker.FindBestBrokerForNpc(gameObject);
            Vector3 brokerApproach =
                broker != null
                    ? ResolveBrokerApproachPosition(broker)
                    : finalTarget;
            if (TryHandleBrokerPillPurchaseIfReady(
                    broker,
                    brokerApproach,
                    0.35f))
            {
                DebugFlow(
                    "Move",
                    "Blocked retry near broker, handled trade immediately");
                return;
            }
        }

        if (currentAction == NpcText.Action("goTaskProviderDaily") &&
            TryVisitTaskProvider())
        {
            DebugFlow(
                "Move",
                "Blocked retry near task provider, handled visit immediately");
            TraceRuntime(
                "HandleBlockedMovement",
                "retry-handled-provider action=" + currentAction +
                " target=" + (currentTarget != null ? currentTarget.name : "null") +
                " wander=" + hasWanderTarget);
            return;
        }

        if (TryHandleGatherArrivalFromMovement(finalTarget, true))
        {
            return;
        }

        if (currentAction == NpcText.Action("gatherResource") &&
            currentTarget != null &&
            TryFindClearPointNear(finalTarget, out Vector3 gatherApproach, false) &&
            Vector2.Distance(transform.position, gatherApproach) >
                escapeTargetReachDistance)
        {
            obstacleAvoidTarget = gatherApproach;
            obstacleAvoidUntil = Time.time + 1.1f;
            hasObstacleAvoidTarget = true;
            hasEscapeTarget = false;
            DebugFlow(
                "Move",
                "Picked gather approach point=" +
                gatherApproach +
                " final=" +
                finalTarget +
                " target=" +
                currentTarget.name);
            return;
        }

        Vector2 escapeDirection =
            (Vector2)finalTarget - (Vector2)transform.position;

        if (TryChooseObstacleDetourDirection(
                escapeDirection,
                finalTarget,
                out Vector2 detourDirection) &&
            TryCommitObstacleAvoidTarget(detourDirection))
        {
            DebugFlow(
                "Move",
                "Committed obstacle detour toward=" +
                detourDirection +
                " final=" +
                finalTarget);
            return;
        }

        Vector3 escapeSeed =
            GetBlockedEscapeSeed(blockedTarget, finalTarget);

        if (TryFindClearPointNear(escapeSeed, out Vector3 clear))
        {
            obstacleAvoidTarget = clear;
            obstacleAvoidUntil = Time.time + 1f;
            hasObstacleAvoidTarget = true;
            hasEscapeTarget = false;
            DebugFlow(
                "Move",
                "Picked escape point=" +
                clear +
                " seed=" +
                escapeSeed +
                " final=" +
                finalTarget);
        }
        else
        {
            DebugFlow(
                "Move",
                "Failed to find escape point seed=" +
                escapeSeed +
                " final=" +
                finalTarget);
        }
    }

    float GetClearDistance(
        Vector2 direction,
        float maxDistance)
    {
        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                transform.position,
                Mathf.Max(0.01f, GetBodyClearRadius()),
                direction.normalized,
                maxDistance);

        float best = maxDistance;

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                best = Mathf.Min(best, hit.distance);
            }
        }

        return best;
    }

    float GetObstacleLookAheadDistance()
    {
        float speedLookAhead =
            Mathf.Max(0f, moveSpeed) * 0.25f + targetClearRadius * 2f;

        return Mathf.Max(
            obstacleCheckDistance,
            obstacleDetourLookAhead,
            targetClearRadius * 3f,
            speedLookAhead);
    }

    Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }
}
