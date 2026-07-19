using UnityEngine;

// Wander target selection, obstacle probing, and local detour scoring.
public partial class VillagerAI
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

    void UpdateUnstuck()
    {
        if (!hasDirectMoveTarget && currentTarget == null && !hasWanderTarget)
        {
            stuckMoveTimer = 0f;
            wanderUnstuckRecoveryAttempts = 0;
            lastUnstuckPosition = transform.position;
            return;
        }

        Vector2 escapeDirection =
            desiredVelocity.sqrMagnitude > 0.0001f
            ? desiredVelocity.normalized
            : GetDirectionToActiveMoveTarget();

        if (escapeDirection.sqrMagnitude <= 0.0001f)
        {
            stuckMoveTimer = 0f;
            wanderUnstuckRecoveryAttempts = 0;
            lastUnstuckPosition = transform.position;
            return;
        }

        float moved = Vector2.Distance(transform.position, lastUnstuckPosition);
        if (moved <= unstuckMinMoveDistance)
        {
            stuckMoveTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckMoveTimer = 0f;
            wanderUnstuckRecoveryAttempts = 0;
            lastUnstuckPosition = transform.position;
        }

        if (stuckMoveTimer < unstuckCheckDelay)
        {
            return;
        }

        if (hasWanderTarget &&
            currentTarget == null &&
            !hasDirectMoveTarget &&
            ++wanderUnstuckRecoveryAttempts >=
                Mathf.Max(1, maxWanderUnstuckRecoveriesBeforeReset))
        {
            hasWanderTarget = false;
            hasObstacleAvoidTarget = false;
            ClearActivePath();
            StopMoving();
            currentAction = NpcText.Action("idle");
            stuckMoveTimer = 0f;
            wanderUnstuckRecoveryAttempts = 0;
            lastUnstuckPosition = transform.position;
            return;
        }

        Vector3 escapeTarget;
        if (!TryPickObstacleEscapeTarget(escapeDirection, out escapeTarget) &&
            (!ignoreNpcBodyCollisions &&
            !TryPickCrowdEscapeTarget(out escapeTarget)))
        {
            Vector2 offset =
                Random.insideUnitCircle.normalized *
                Mathf.Max(0.1f, unstuckOffsetRadius);
            escapeTarget =
                ClampToCurrentMapArea(transform.position + (Vector3)offset);
        }

        Vector2 toEscapeTarget =
            (Vector2)escapeTarget - (Vector2)transform.position;

        ClearActivePath();
        hasRoadPreference = false;
        obstacleAvoidTarget = escapeTarget;
        obstacleAvoidUntil = Time.time + Mathf.Max(0.8f, unstuckCheckDelay);
        hasObstacleAvoidTarget = true;
        desiredVelocity =
            toEscapeTarget.sqrMagnitude > 0.0001f
            ? toEscapeTarget.normalized * moveSpeed
            : escapeDirection * moveSpeed;
        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;
    }

    bool TryPickWanderTarget(out Vector3 target)
    {
        Vector3 center = currentMapArea != null && currentMapArea.areaBounds != null
            ? currentMapArea.areaBounds.bounds.center
            : spawnPosition;

        for (int i = 0; i < maxPickTargetAttempts; i++)
        {
            Vector2 random = Random.insideUnitCircle * Mathf.Max(0.1f, wanderRadius);
            Vector3 candidate = ClampToCurrentMapArea(center + new Vector3(random.x, random.y, 0f));

            if (Vector2.Distance(transform.position, candidate) >=
                Mathf.Max(arriveDistance * 2f, minWanderTargetDistance) &&
                IsMoveTargetFeasible(candidate) &&
                HasClearLineTo(candidate))
            {
                target = candidate;
                return true;
            }
        }

        if (TryFindClearPointNear(transform.position, out target) &&
            Vector2.Distance(transform.position, target) >=
                Mathf.Max(arriveDistance * 2f, minWanderTargetDistance) &&
            HasClearLineTo(target))
        {
            return true;
        }

        target = transform.position;
        return false;
    }

    bool TryFindClearPointNear(Vector3 preferred, out Vector3 result)
    {
        preferred = ClampToCurrentMapArea(preferred);
        if (IsMoveTargetFeasible(preferred))
        {
            result = preferred;
            return true;
        }

        float baseRadius = Mathf.Max(targetClearRadius * 2f, 0.25f);
        int angleSteps = Mathf.Max(8, maxPickTargetAttempts);
        for (int radiusStep = 0; radiusStep < 6; radiusStep++)
        {
            float radius = baseRadius + radiusStep * 0.2f;
            for (int angleStep = 0; angleStep < angleSteps; angleStep++)
            {
                float angle =
                    (angleStep / (float)angleSteps) * Mathf.PI * 2f +
                    radiusStep * 0.17f;
                Vector2 offset =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    radius;
                Vector3 candidate =
                    ClampToCurrentMapArea(
                        preferred + new Vector3(offset.x, offset.y, 0f));

                if (IsMoveTargetFeasible(candidate))
                {
                    result = candidate;
                    return true;
                }
            }
        }

        result = transform.position;
        return IsMoveTargetFeasible(result);
    }

    bool ShouldRejectFallbackTarget(
        Vector3 requestedTarget,
        Vector3 fallbackTarget)
    {
        float requestedDistance =
            Vector2.Distance(
                transform.position,
                requestedTarget);
        if (requestedDistance <= arriveDistance)
        {
            return false;
        }

        float fallbackDistance =
            Vector2.Distance(
                transform.position,
                fallbackTarget);
        return fallbackDistance <= arriveDistance;
    }

    bool IsMoveTargetFeasible(Vector3 position)
    {
        if (!IsInsideCurrentMapArea(position))
        {
            return false;
        }

        return !IsPositionBlocked(position);
    }

    bool HasClearLineTo(Vector3 target)
    {
        return HasClearLineTo(
            target,
            transform.position);
    }

    bool HasClearLineTo(
        Vector3 target,
        Vector3 originPosition)
    {
        Vector2 origin = originPosition;
        Vector2 delta = (Vector2)target - origin;
        float distance = delta.magnitude;
        if (distance <= targetClearRadius)
        {
            return true;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
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

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            transform.position,
            Mathf.Max(0.01f, GetBodyClearRadius()),
            direction.normalized,
            GetObstacleLookAheadDistance());

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                return true;
            }
        }

        return false;
    }

    bool TryChooseDetourDirection(
        Vector2 desiredDirection,
        Vector3 finalTarget,
        out Vector2 detourDirection)
    {
        detourDirection = Vector2.zero;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired =
            desiredDirection.normalized;

        Vector2 toTarget =
            ((Vector2)finalTarget - (Vector2)transform.position);

        Vector2 targetDirection =
            toTarget.sqrMagnitude > 0.0001f
            ? toTarget.normalized
            : desired;

        float lookAhead =
            GetObstacleLookAheadDistance();

        float bestScore =
            float.NegativeInfinity;

        bool found =
            false;

        for (int i = 0; i < DetourAngles.Length; i++)
        {
            float angle =
                DetourAngles[i];

            if (TryScoreDetourDirection(
                    RotateDirection(desired, angle),
                    desired,
                    targetDirection,
                    lookAhead,
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

            if (TryScoreDetourDirection(
                    RotateDirection(desired, -angle),
                    desired,
                    targetDirection,
                    lookAhead,
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

    bool TrySetObstacleAvoidTarget(
        Vector2 blockedDirection,
        Vector3 finalTarget)
    {
        Vector2 desired =
            blockedDirection.sqrMagnitude > 0.0001f
            ? blockedDirection.normalized
            : ((Vector2)finalTarget - (Vector2)transform.position).normalized;

        if (desired.sqrMagnitude <= 0.0001f)
        {
            desired = Vector2.up;
        }

        Vector2 side =
            new Vector2(-desired.y, desired.x);

        float baseDistance =
            Mathf.Max(
                unstuckOffsetRadius,
                obstacleDetourLookAhead,
                targetClearRadius * 3f);

        Vector2[] directions =
        {
            side,
            -side,
            (side - desired * 0.35f).normalized,
            (-side - desired * 0.35f).normalized,
            -desired
        };

        for (int radiusStep = 0; radiusStep < 3; radiusStep++)
        {
            float distance =
                baseDistance + radiusStep * targetClearRadius * 2f;

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 candidateDirection =
                    directions[i];

                if (candidateDirection.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                Vector3 candidate =
                    ClampToCurrentMapArea(
                        transform.position +
                        (Vector3)(candidateDirection.normalized * distance));

                if (!IsMoveTargetFeasible(candidate) ||
                    !HasClearLineTo(candidate))
                {
                    continue;
                }

                Vector2 toCandidate =
                    (Vector2)candidate - (Vector2)transform.position;

                if (toCandidate.sqrMagnitude <= 0.0001f ||
                    IsMovementBlocked(toCandidate.normalized))
                {
                    continue;
                }

                obstacleAvoidTarget = candidate;
                obstacleAvoidUntil = Time.time + 1.2f;
                hasObstacleAvoidTarget = true;
                blockedMoveTimer = 0f;
                desiredVelocity =
                    toCandidate.normalized *
                    moveSpeed;
                return true;
            }
        }

        return false;
    }

    bool TryCommitObstacleAvoidTarget(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = direction.normalized;
        float distance = Mathf.Max(
            unstuckOffsetRadius,
            obstacleDetourLookAhead,
            targetClearRadius * 3f);

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
        desiredVelocity = desired * moveSpeed;
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
            ((Vector2)finalTarget - (Vector2)transform.position);
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
            obstacleScanDistance,
            obstacleDetourLookAhead * 2f);
        float scanStep = Mathf.Max(0.1f, obstacleScanStep);
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
                Vector2 candidate = ClampToCurrentMapArea(candidates[i]);

                if (!IsInsideCurrentMapArea(candidate) ||
                    IsPositionBlocked(candidate) ||
                    !HasClearLineTo(candidate))
                {
                    continue;
                }

                Vector2 toCandidate =
                    candidate - (Vector2)transform.position;

                if (toCandidate.sqrMagnitude <= 0.0001f ||
                    Vector2.Dot(toCandidate.normalized, targetDirection) < -0.05f)
                {
                    continue;
                }

                obstacleAvoidTarget = candidate;
                obstacleAvoidUntil = Time.time + 1.6f;
                hasObstacleAvoidTarget = true;
                blockedMoveTimer = 0f;
                desiredVelocity = toCandidate.normalized * moveSpeed;
                return true;
            }
        }

        return false;
    }

    Vector2 GetDirectionToActiveMoveTarget()
    {
        Vector3 targetPosition =
            hasObstacleAvoidTarget
            ? obstacleAvoidTarget
            : hasDirectMoveTarget
                ? directMoveTarget
                : hasWanderTarget
                    ? wanderTarget
                    : currentTarget != null
                        ? currentTarget.position
                        : transform.position;

        Vector2 direction =
            (Vector2)targetPosition - (Vector2)transform.position;

        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.zero;
    }

    bool TryPickObstacleEscapeTarget(
        Vector2 blockedDirection,
        out Vector3 target)
    {
        target = transform.position;

        Vector2 forward =
            blockedDirection.sqrMagnitude > 0.0001f
            ? blockedDirection.normalized
            : GetDirectionToActiveMoveTarget();

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Random.insideUnitCircle.normalized;
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector2.up;
        }

        Vector2 side = new Vector2(-forward.y, forward.x);
        float baseDistance =
            Mathf.Max(unstuckOffsetRadius, targetClearRadius * 3f);

        Vector2[] directions =
        {
            side,
            -side,
            (side - forward * 0.5f).normalized,
            (-side - forward * 0.5f).normalized,
            -forward,
            (side + forward * 0.25f).normalized,
            (-side + forward * 0.25f).normalized
        };

        float bestScore = float.NegativeInfinity;
        Vector3 bestTarget = transform.position;
        bool found = false;

        for (int radiusStep = 0; radiusStep < 4; radiusStep++)
        {
            float distance =
                baseDistance + radiusStep * Mathf.Max(targetClearRadius * 2f, 0.35f);

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 direction = directions[i];
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                direction.Normalize();
                Vector3 candidate =
                    ClampToCurrentMapArea(
                        transform.position +
                        (Vector3)(direction * distance));

                if (!IsMoveTargetFeasible(candidate) ||
                    !HasClearLineTo(candidate) ||
                    IsMovementBlocked(direction))
                {
                    continue;
                }

                float score =
                    GetClearDistance(direction, GetObstacleLookAheadDistance()) +
                    Mathf.Max(-0.25f, Vector2.Dot(direction, -forward)) *
                    baseDistance;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                    found = true;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        target = bestTarget;
        return true;
    }

    bool TryScoreDetourDirection(
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
            (Vector3)(candidate * Mathf.Max(targetClearRadius * 2f, lookAhead * 0.65f));

        if (!IsInsideCurrentMapArea(nextPoint) ||
            IsPositionBlocked(nextPoint))
        {
            return false;
        }

        float clearDistance =
            GetClearDistance(candidate, lookAhead);

        if (clearDistance < targetClearRadius * 2f)
        {
            return false;
        }

        float progressScore =
            Mathf.Max(-0.5f, Vector2.Dot(candidate, targetDirection));

        float smoothScore =
            Mathf.Max(-0.5f, Vector2.Dot(candidate, desired));

        score =
            clearDistance / Mathf.Max(0.01f, lookAhead) * 3f +
            progressScore * 2f +
            smoothScore;

        return true;
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

        float best =
            maxDistance;

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                best =
                    Mathf.Min(best, hit.distance);
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

    Vector2 RotateDirection(
        Vector2 direction,
        float degrees)
    {
        float radians =
            degrees * Mathf.Deg2Rad;

        float sin =
            Mathf.Sin(radians);

        float cos =
            Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }
}
