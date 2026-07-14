using UnityEngine;

// Conversation holds, unstuck recovery, obstacle avoidance and crowd separation.
public partial class SmartNpcAI
{
    public void StopForConversation()
    {
        StopForConversation(2f);
    }

    public void StopForConversation(float duration)
    {
        movementPausedUntil = Mathf.Max(
            movementPausedUntil,
            Time.time + Mathf.Max(0.2f, duration));

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

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
        int sideSign = (GetInstanceID() & 1) == 0 ? 1 : -1;

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
                    transform.position +
                    (Vector3)(escapeDirection * distance);

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
        return !IsPositionBlocked(position);
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
            transform.position +
            (Vector3)(desired * distance);

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
                    preferred + new Vector3(offset.x, offset.y, 0f);

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

        return transform.position +
            (Vector3)(away * Mathf.Max(unstuckOffsetRadius, targetClearRadius * 3f));
    }

    void HandleBlockedMovement(Vector3 blockedTarget, Vector3 finalTarget)
    {
        blockedMoveTimer += Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.zero;

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
                NpcCounterBroker broker = NpcCounterBroker.Active;
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
            NpcCounterBroker broker = NpcCounterBroker.Active;
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

    bool IsBlockingObstacle(Collider2D hit)
    {
        if (hit == null || hit.isTrigger || IsSelfCollider(hit))
        {
            return false;
        }

        if (IsCurrentTargetCollider(hit) ||
            IsCurrentMonsterCollider(hit) ||
            IsCounterCustomerZoneCollider(hit))
        {
            return false;
        }

        return hit.GetComponentInParent<VillagerAI>() == null &&
            hit.GetComponentInParent<SmartNpcAI>() == null &&
            hit.GetComponentInParent<NpcMapMover2D>() == null &&
            hit.GetComponentInParent<MonsterAI>() == null;
    }

    string DescribeObstacle(Collider2D hit)
    {
        if (hit == null)
        {
            return "null";
        }

        Bounds bounds = hit.bounds;
        return hit.name +
            " layer=" + hit.gameObject.layer +
            " trigger=" + (hit.isTrigger ? 1 : 0) +
            " pos=" + hit.transform.position +
            " center=" + bounds.center +
            " size=" + bounds.size +
            " parent=" +
            (hit.transform.parent != null
                ? hit.transform.parent.name
                : "none");
    }

    bool IsCounterCustomerZoneCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        NpcCounterBroker broker =
            hit.GetComponentInParent<NpcCounterBroker>();
        if (broker == null ||
            broker.customerPoint == null)
        {
            return false;
        }

        Collider2D customerZone =
            broker.GetCustomerZoneCollider();
        if (customerZone == null)
        {
            return false;
        }

        return hit == customerZone;
    }

    bool IsCurrentTargetCollider(Collider2D hit)
    {
        if (hit == null || currentTarget == null)
        {
            return false;
        }

        return hit.transform == currentTarget ||
            hit.transform.IsChildOf(currentTarget);
    }

    bool IsCurrentMonsterCollider(Collider2D hit)
    {
        if (hit == null || currentMonsterTarget == null)
        {
            return false;
        }

        Transform monsterTarget =
            currentMonsterTarget.transform;
        return hit.transform == monsterTarget ||
            hit.transform.IsChildOf(monsterTarget);
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

    bool TryResolveCrowdAhead(
        Vector2 desiredDirection,
        out Vector2 resolvedDirection)
    {
        resolvedDirection = desiredDirection;

        if (ShouldBypassCrowdAvoidanceForMonsterCombat())
        {
            return true;
        }

        if (ignoreNpcBodyCollisions)
        {
            return true;
        }

        if (desiredDirection.sqrMagnitude <= 0.0001f ||
            crowdLookAheadDistance <= 0f ||
            rb == null)
        {
            return true;
        }

        Collider2D other;
        if (!TryFindNpcAhead(desiredDirection, out other))
        {
            return true;
        }

        if (ShouldYieldToNpc(other))
        {
            crowdYieldUntil =
                Time.time +
                Mathf.Max(0.05f, crowdYieldDuration) *
                Random.Range(0.75f, 1.35f);
            rb.linearVelocity = Vector2.zero;
            return false;
        }

        if (TryChooseCrowdDetourDirection(
                desiredDirection,
                other,
                out resolvedDirection))
        {
            return true;
        }

        crowdYieldUntil =
            Time.time +
            Mathf.Max(0.05f, crowdYieldDuration) *
            Random.Range(0.75f, 1.35f);
        rb.linearVelocity = Vector2.zero;
        return false;
    }

    bool TryFindNpcAhead(
        Vector2 direction,
        out Collider2D npcCollider)
    {
        npcCollider = null;

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                rb.position,
                Mathf.Max(0.01f, separationRadius * 0.45f),
                direction.normalized,
                Mathf.Max(separationRadius, crowdLookAheadDistance),
                crowdLayers);

        float nearestDistance =
            float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D collider = hit.collider;
            if (collider == null ||
                IsSelfCollider(collider) ||
                !IsNpcCollider(collider))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                npcCollider = collider;
            }
        }

        return npcCollider != null;
    }

    Vector2 ApplyCrowdAvoidance(Vector2 direction)
    {
        if (ShouldBypassCrowdAvoidanceForMonsterCombat())
        {
            return direction;
        }

        if (ignoreNpcBodyCollisions ||
            separationRadius <= 0f ||
            rb == null)
        {
            return direction;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                rb.position,
                separationRadius,
                crowdLayers);

        Vector2 push = Vector2.zero;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                IsSelfCollider(hit) ||
                !IsNpcCollider(hit))
            {
                continue;
            }

            Vector2 away =
                rb.position -
                (Vector2)hit.transform.position;

            float distance =
                Mathf.Max(away.magnitude, 0.01f);

            push += away.normalized / distance;
        }

        if (push.sqrMagnitude <= 0.0001f)
        {
            return direction;
        }

        return (direction + push.normalized * separationStrength).normalized;
    }

    bool TryApplyNpcOverlapSeparation()
    {
        if (ShouldBypassCrowdAvoidanceForMonsterCombat())
        {
            return false;
        }

        if (IsTeleportRouteAction(currentAction) ||
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goCultivatePoint") ||
            currentAction == NpcText.Action("goVanBaoLauBroker") ||
            currentAction == NpcText.Action("goVanBaoLauTask"))
        {
            return false;
        }

        if (ignoreNpcBodyCollisions ||
            rb == null ||
            separationRadius <= 0f)
        {
            return false;
        }

        Vector2 separation = GetNpcSeparationDirection();
        if (separation.sqrMagnitude > 0.0001f &&
            !IsMovementBlocked(separation))
        {
            rb.linearVelocity =
                separation.normalized * moveSpeed * 0.65f;
            return true;
        }

        if (TryPickUnstuckEscapeTarget(separation, out escapeTarget))
        {
            hasEscapeTarget = true;
            hasObstacleAvoidTarget = false;
            blockedMoveTimer = 0f;
            DebugFlow(
                "Escape",
                "Commit overlap escape target=" +
                escapeTarget +
                " separation=" +
                separation);
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return true;
        }

        return false;
    }

    Vector3 GetApproachPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        NpcInteractionPoint interactionPoint =
            target.GetComponent<NpcInteractionPoint>();
        if (interactionPoint != null)
        {
            Vector3 standPosition =
                interactionPoint.GetStandPositionFor(gameObject);
            standPosition.z = transform.position.z;
            return standPosition;
        }

        NpcCounterBroker counterBroker =
            target.GetComponentInParent<NpcCounterBroker>();
        if (counterBroker != null)
        {
            Vector3 brokerPosition =
                counterBroker.GetCustomerPositionFor(gameObject);
            brokerPosition.z = transform.position.z;
            return brokerPosition;
        }

        Vector3 targetPosition = target.position;
        if (currentMonsterTarget != null &&
            target == currentMonsterTarget.transform)
        {
            return GetMonsterCombatApproachPosition(target);
        }

        if (!ShouldUseSharedTargetSpacing(target))
        {
            return targetPosition;
        }

        int slotCount = 8;
        int slotIndex = Mathf.Abs(
            gameObject.GetInstanceID() ^
            target.gameObject.GetInstanceID()) % slotCount;
        float spacingRadius = Mathf.Max(
            targetClearRadius * 3f,
            sharedTargetSpacingRadius,
            0.85f);

        return FindOpenSharedTargetSlot(
            targetPosition,
            slotCount,
            slotIndex,
            spacingRadius);
    }

    Vector3 FindOpenSharedTargetSlot(
        Vector3 targetPosition,
        int slotCount,
        int startSlotIndex,
        float spacingRadius)
    {
        Vector3 fallback = targetPosition;

        for (int i = 0; i < slotCount; i++)
        {
            int slotIndex = (startSlotIndex + i) % slotCount;
            float angle = (Mathf.PI * 2f * slotIndex) / slotCount;
            Vector3 candidate =
                targetPosition +
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f) * spacingRadius;

            if (!IsSharedTargetOccupied(candidate))
            {
                return candidate;
            }

            fallback = candidate;
        }

        return fallback;
    }

    Vector3 GetMonsterCombatApproachPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>(true);
        }

        Vector2 fromPosition = transform.position;
        Vector2 away = fromPosition - (Vector2)target.position;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.right;
        }

        float desiredGap =
            Mathf.Max(
                0.28f,
                targetClearRadius * 2f,
                attackRange * 0.3f);

        Vector3 approach =
            target.position +
            (Vector3)(away.normalized * desiredGap);
        approach.z = transform.position.z;
        return approach;
    }

    bool ShouldUseSharedTargetSpacing(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (currentMonsterTarget != null &&
            target == currentMonsterTarget.transform)
        {
            return false;
        }

        if (target.GetComponentInParent<NpcTaskProvider>() != null ||
            target.GetComponentInParent<NpcCounterBroker>() != null)
        {
            return false;
        }

        return target.GetComponentInParent<MonsterAI>() != null;
    }

    bool IsSharedTargetOccupied(Vector3 targetPosition)
    {
        float radius = Mathf.Max(0.18f, sharedTargetOccupancyRadius);
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                targetPosition,
                radius,
                crowdLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || IsSelfCollider(hit))
            {
                continue;
            }

            VillagerAI otherVillager =
                hit.GetComponentInParent<VillagerAI>();
            if (otherVillager != null &&
                otherVillager.gameObject != gameObject &&
                !otherVillager.IsDead)
            {
                return true;
            }

            SmartNpcAI otherCultivator =
                hit.GetComponentInParent<SmartNpcAI>();
            if (otherCultivator != null &&
                otherCultivator.gameObject != gameObject &&
                !otherCultivator.IsDead)
            {
                return true;
            }
        }

        return false;
    }

    Vector2 GetNpcSeparationDirection()
    {
        if (ignoreNpcBodyCollisions)
        {
            return Vector2.zero;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                rb.position,
                separationRadius,
                crowdLayers);

        Vector2 push = Vector2.zero;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                IsSelfCollider(hit) ||
                !IsNpcCollider(hit))
            {
                continue;
            }

            Vector2 away =
                rb.position -
                (Vector2)hit.transform.position;

            if (away.sqrMagnitude <= 0.0001f)
            {
                Transform root = GetNpcRoot(hit);
                int otherId =
                    root != null
                    ? root.gameObject.GetInstanceID()
                    : hit.gameObject.GetInstanceID();
                away = ((GetInstanceID() ^ otherId) & 1) == 0
                    ? Vector2.right
                    : Vector2.left;
            }

            float distance =
                Mathf.Max(away.magnitude, 0.01f);

            push += away.normalized / distance;
        }

        return push.normalized;
    }

    bool TryChooseCrowdDetourDirection(
        Vector2 desiredDirection,
        Collider2D other,
        out Vector2 detourDirection)
    {
        detourDirection = desiredDirection;

        Vector2 desired =
            desiredDirection.normalized;

        Vector2 side =
            new Vector2(-desired.y, desired.x);

        if (ShouldUseRightSide(other))
        {
            side = -side;
        }

        float distance =
            Mathf.Max(crowdDetourDistance, separationRadius);

        for (int i = 0; i < 2; i++)
        {
            Vector2 candidateSide =
                i == 0 ? side : -side;

            Vector2 candidateDirection =
                (candidateSide + desired * 0.35f).normalized;

            if (candidateDirection.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            Vector2 candidate =
                rb.position + candidateDirection * distance;

            Collider2D hit =
                Physics2D.OverlapCircle(
                    candidate,
                    Mathf.Max(0.01f, separationRadius * 0.45f),
                    crowdLayers);

            if (hit != null &&
                !IsSelfCollider(hit) &&
                IsNpcCollider(hit))
            {
                continue;
            }

            detourDirection = candidateDirection;
            return true;
        }

        return false;
    }

    bool ShouldYieldToNpc(Collider2D other)
    {
        Transform otherRoot = GetNpcRoot(other);
        if (otherRoot == null)
        {
            return false;
        }

        return GetInstanceID() > otherRoot.gameObject.GetInstanceID();
    }

    bool ShouldUseRightSide(Collider2D other)
    {
        Transform otherRoot = GetNpcRoot(other);
        int otherId = otherRoot != null
            ? otherRoot.gameObject.GetInstanceID()
            : 0;

        return ((GetInstanceID() ^ otherId) & 1) == 0;
    }

    bool IsNpcCollider(Collider2D hit)
    {
        return GetNpcRoot(hit) != null;
    }

    bool IsSelfCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.transform == transform ||
            hit.transform.IsChildOf(transform))
        {
            return true;
        }

        if (selfColliders == null)
        {
            return false;
        }

        foreach (Collider2D own in selfColliders)
        {
            if (own == hit)
            {
                return true;
            }
        }

        return false;
    }

    Transform GetNpcRoot(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.transform;
        }

        NpcMapMover2D mover =
            hit.GetComponentInParent<NpcMapMover2D>();
        return mover != null ? mover.transform : null;
    }

}
