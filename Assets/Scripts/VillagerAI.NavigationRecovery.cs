using System.Collections.Generic;
using UnityEngine;

// Unstuck handling, local obstacle scans, collision filtering and crowd avoidance.
public partial class VillagerAI
{
    void UpdateUnstuck()
    {
        if (!hasDirectMoveTarget && currentTarget == null && !hasWanderTarget)
        {
            stuckMoveTimer = 0f;
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
            lastUnstuckPosition = transform.position;
        }

        if (stuckMoveTimer < unstuckCheckDelay)
        {
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

    void ApplyNpcOverlapSeparation()
    {
        if (ignoreNpcBodyCollisions ||
            rb == null ||
            separationRadius <= 0f)
        {
            return;
        }

        Vector2 separation = GetSeparationDirection();
        if (separation.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 separationDirection = separation.normalized;
        if (IsMovementBlocked(separationDirection))
        {
            Vector3 separationTarget =
                transform.position +
                (Vector3)(
                    separationDirection *
                    Mathf.Max(
                        separationRadius,
                        targetClearRadius * 1.5f,
                        0.45f));
            if (!TryChooseDetourDirection(
                    separationDirection,
                    separationTarget,
                    out Vector2 detourDirection))
            {
                return;
            }

            separationDirection = detourDirection.normalized;
        }

        Vector2 separationVelocity =
            separationDirection * moveSpeed * 0.85f;

        desiredVelocity =
            desiredVelocity.sqrMagnitude > 0.0001f
            ? (desiredVelocity + separationVelocity).normalized * moveSpeed
            : separationVelocity;
    }
    void ApplySmoothVelocity()
    {
        if (rb == null)
        {
            return;
        }

        float rate =
            desiredVelocity.sqrMagnitude > rb.linearVelocity.sqrMagnitude
            ? movementAcceleration
            : movementDeceleration;

        rb.linearVelocity =
            Vector2.MoveTowards(
                rb.linearVelocity,
                desiredVelocity,
                rate * Time.fixedDeltaTime);
    }

    void ConfigureRigidbody()
    {
        if (rb == null)
        {
            return;
        }

        rb.bodyType = useKinematicNpcMovement
            ? RigidbodyType2D.Kinematic
            : RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void UpdateVisualAnimation()
    {
        if (visualAnimation == null)
        {
            return;
        }

        Vector2 animationVelocity =
            rb != null
            ? rb.linearVelocity
            : desiredVelocity;

        bool isIdle =
            animationVelocity.sqrMagnitude <=
            animationIdleSpeed * animationIdleSpeed;

        Vector2 direction =
            isIdle
            ? Vector2.zero
            : animationVelocity.normalized;

        if (visualAnimation.debugVisualLogs)
        {
            Debug.Log(
                "[VillagerAI] Visual input object=" +
                gameObject.name +
                " velocity=" + animationVelocity +
                " speed=" + animationVelocity.magnitude.ToString("F3") +
                " idleThreshold=" + animationIdleSpeed.ToString("F3") +
                " isIdle=" + isIdle +
                " desiredVelocity=" + desiredVelocity +
                " action=" + currentAction);
        }

        visualAnimation.UpdateNPCAnimation(direction, isIdle, currentAction);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryIgnoreNpcCollision(collision.collider);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryIgnoreNpcCollision(collision.collider);

        if (collision.collider != null &&
            IsBlockingObstacle(collision.collider))
        {
            TryEscapeObstacleCollision(collision);
        }
    }
    void TryIgnoreNpcCollision(Collider2D other)
    {
        if (!ignoreNpcBodyCollisions || other == null || other.isTrigger)
        {
            return;
        }

        if (other.GetComponentInParent<VillagerAI>() == null &&
            other.GetComponentInParent<SmartNpcAI>() == null &&
            other.GetComponentInParent<NpcMapMover2D>() == null)
        {
            return;
        }

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        foreach (Collider2D own in ownColliders)
        {
            if (own != null &&
                !own.isTrigger &&
                own != other)
            {
                Physics2D.IgnoreCollision(own, other, true);
            }
        }
    }
    void TryEscapeObstacleCollision(Collision2D collision)
    {
        if (collision == null || collision.contactCount <= 0)
        {
            return;
        }

        if (ignoreNpcBodyCollisions &&
            collision.collider != null &&
            (collision.collider.GetComponentInParent<VillagerAI>() != null ||
             collision.collider.GetComponentInParent<SmartNpcAI>() != null ||
             collision.collider.GetComponentInParent<NpcMapMover2D>() != null))
        {
            return;
        }

        Vector2 normal = collision.GetContact(0).normal;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 escapePoint = transform.position +
            (Vector3)(normal.normalized * Mathf.Max(unstuckOffsetRadius, targetClearRadius * 2f));

        Vector3 clearPoint;
        if (!TryFindClearPointNear(escapePoint, out clearPoint))
        {
            return;
        }

        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = true;
        directMoveTarget = clearPoint;
        desiredVelocity =
            normal.normalized *
            moveSpeed *
            0.75f;
        blockedMoveTimer = 0f;
    }

    void ResolveInitialObstacleOverlap()
    {
        if (!IsPositionBlocked(transform.position) &&
            !HasBlockingColliderOverlap())
        {
            return;
        }

        if (!TryFindClearPointNear(transform.position, out Vector3 clearPoint))
        {
            return;
        }

        transform.position = clearPoint;
        spawnPosition = clearPoint;
        lastUnstuckPosition = clearPoint;

        if (rb != null)
        {
            rb.position = clearPoint;
            rb.linearVelocity = Vector2.zero;
        }
    }

    bool HasBlockingColliderOverlap()
    {
        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        Collider2D[] hits = new Collider2D[32];

        foreach (Collider2D own in ownColliders)
        {
            if (own == null || own.isTrigger || !own.enabled)
            {
                continue;
            }

            int count = Physics2D.OverlapCollider(own, filter, hits);
            for (int i = 0; i < count; i++)
            {
                if (IsBlockingObstacle(hits[i]))
                {
                    return true;
                }
            }
        }

        return false;
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

    void OnDisable()
    {
        activeVillagers.Remove(this);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        activeVillagers.Remove(this);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
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

      bool TryResolveCrowdAhead(
          Vector2 desiredDirection,
          Vector3 finalTarget,
          out Vector2 resolvedDirection)
      {
          resolvedDirection = desiredDirection;

          if (ignoreNpcBodyCollisions)
          {
              return true;
          }

          if (Time.time < crowdDirectionCommitUntil &&
              crowdCommittedDirection.sqrMagnitude > 0.0001f)
          {
              resolvedDirection = crowdCommittedDirection.normalized;
              return true;
          }

          if (desiredDirection.sqrMagnitude <= 0.0001f ||
              crowdLookAheadDistance <= 0f)
        {
            return true;
        }

        Collider2D other;
        if (!TryFindNpcAhead(desiredDirection, out other))
        {
            crowdBlockedTimer = 0f;
            return true;
        }

        crowdBlockedTimer += Time.fixedDeltaTime;
        if (crowdBlockedTimer >= unstuckCheckDelay)
        {
            if (TryPickCrowdEscapeTarget(out Vector3 escapeTarget))
            {
                crowdBlockedTimer = 0f;
                ClearActivePath();
                SetDirectMoveTarget(escapeTarget, true);
                StopMoving();
                return false;
            }

            crowdBlockedTimer = 0f;
        }

          if (TryForceCrowdStepAside(
                  desiredDirection,
                  finalTarget,
                  other,
                  out resolvedDirection))
          {
              crowdBlockedTimer = 0f;
              crowdCommittedDirection = resolvedDirection;
              crowdDirectionCommitUntil = Time.time + 0.35f;
              return true;
          }

          if (ShouldYieldToNpc(other))
          {
              crowdYieldUntil =
                  Time.time +
                  Mathf.Max(0.05f, crowdYieldDuration) *
                  Random.Range(0.75f, 1.35f);
              crowdCommittedDirection = desiredDirection;
              crowdDirectionCommitUntil =
                  Time.time + Mathf.Max(0.1f, crowdYieldDuration * 0.5f);
              StopMoving();
              return false;
          }

          if (TryChooseCrowdDetourDirection(
                  desiredDirection,
                  finalTarget,
                  other,
                  out resolvedDirection))
          {
              crowdBlockedTimer = 0f;
              crowdCommittedDirection = resolvedDirection;
              crowdDirectionCommitUntil = Time.time + 0.35f;
              return true;
          }

          crowdYieldUntil =
              Time.time +
              Mathf.Max(0.05f, crowdYieldDuration) *
              Random.Range(0.75f, 1.35f);
          crowdCommittedDirection = desiredDirection;
          crowdDirectionCommitUntil =
              Time.time + Mathf.Max(0.1f, crowdYieldDuration * 0.5f);
          StopMoving();
          return false;
      }

    bool TryFindNpcAhead(
        Vector2 direction,
        out Collider2D npcCollider)
    {
        npcCollider = null;

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                transform.position,
                Mathf.Max(0.01f, GetBodyClearRadius()),
                direction.normalized,
                Mathf.Max(separationRadius, crowdLookAheadDistance),
                villagerLayers);

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

    bool TryChooseCrowdDetourDirection(
        Vector2 desiredDirection,
        Vector3 finalTarget,
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
            Mathf.Max(crowdDetourDistance, separationRadius, targetClearRadius * 2f);

        for (int i = 0; i < 2; i++)
        {
            Vector2 candidateSide =
                i == 0 ? side : -side;

            Vector3 candidate =
                ClampToCurrentMapArea(
                    transform.position +
                    (Vector3)((candidateSide + desired * 0.35f).normalized * distance));

            if (!IsMoveTargetFeasible(candidate) ||
                !HasClearLineTo(candidate))
            {
                continue;
            }

            Vector2 toCandidate =
                (Vector2)candidate - (Vector2)transform.position;

            if (toCandidate.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            detourDirection = toCandidate.normalized;
            return true;
        }

        return false;
    }

    bool TryPickCrowdEscapeTarget(out Vector3 target)
    {
        if (ignoreNpcBodyCollisions)
        {
            target = transform.position;
            return false;
        }

        target = transform.position;

        Vector2 baseDirection = desiredVelocity.sqrMagnitude > 0.0001f
            ? desiredVelocity.normalized
            : Vector2.zero;

        if (baseDirection.sqrMagnitude <= 0.0001f)
        {
            Vector3 targetPosition =
                hasDirectMoveTarget
                ? directMoveTarget
                : hasWanderTarget
                    ? wanderTarget
                    : currentTarget != null
                        ? currentTarget.position
                        : transform.position;

            baseDirection =
                ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        }

        if (baseDirection.sqrMagnitude <= 0.0001f)
        {
            baseDirection = Vector2.up;
        }

        Vector2 side =
            new Vector2(-baseDirection.y, baseDirection.x);

        float distance =
            Mathf.Max(crowdDetourDistance, unstuckOffsetRadius, targetClearRadius * 2f);

        for (int i = 0; i < 4; i++)
        {
            Vector2 candidateDirection =
                i == 0 ? side :
                i == 1 ? -side :
                i == 2 ? (side + baseDirection).normalized :
                (-side + baseDirection).normalized;

            Vector3 candidate =
                ClampToCurrentMapArea(
                    transform.position +
                    (Vector3)(candidateDirection * distance));

            if (IsMoveTargetFeasible(candidate) &&
                HasClearLineTo(candidate))
            {
                target = candidate;
                return true;
            }
        }

        return false;
    }

    bool TryForceCrowdStepAside(
        Vector2 desiredDirection,
        Vector3 finalTarget,
        Collider2D other,
        out Vector2 detourDirection)
    {
        if (ignoreNpcBodyCollisions)
        {
            detourDirection = desiredDirection;
            return false;
        }

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

        Vector2 side = new Vector2(-desired.y, desired.x);
        if (ShouldUseRightSide(other))
        {
            side = -side;
        }

        float distance = Mathf.Max(
            crowdDetourDistance,
            separationRadius,
            targetClearRadius * 2.5f);

        Vector2[] candidates =
        {
            side,
            -side,
            side + desired * 0.25f,
            -side + desired * 0.25f
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2 candidate = candidates[i];
            if (candidate.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            candidate.Normalize();
            Vector2 nextPoint =
                (Vector2)transform.position + candidate * distance;

              if (!IsInsideCurrentMapArea(nextPoint) ||
                  IsPositionBlocked(nextPoint) ||
                  !HasClearLineTo(nextPoint))
              {
                  continue;
              }

              float progress = Vector2.Dot(candidate, targetDirection);
              if (progress < -0.05f)
              {
                  continue;
              }

              detourDirection = candidate;
              crowdCommittedDirection = detourDirection;
              crowdDirectionCommitUntil = Time.time + 0.35f;
              return true;
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

    Transform GetNpcRoot(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.transform;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        NpcMapMover2D mover =
            hit.GetComponentInParent<NpcMapMover2D>();
        return mover != null ? mover.transform : null;
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

    bool IsPositionBlocked(Vector3 position)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            GetBodyClearRadius());

        foreach (Collider2D hit in hits)
        {
            if (IsBlockingObstacle(hit))
            {
                LogMovementHaltDebug(
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

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        foreach (Collider2D own in ownColliders)
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

    bool IsBlockingObstacle(Collider2D hit)
    {
        if (hit == null || hit.isTrigger || IsSelfCollider(hit))
        {
            return false;
        }

        if (IsCounterCustomerZoneCollider(hit))
        {
            return false;
        }

        if (IsTaskProviderInteractionCollider(hit))
        {
            return false;
        }

        if (IsInteractionPointCollider(hit))
        {
            return false;
        }

        if (IsInteriorFocusCollider(hit))
        {
            return false;
        }

        return hit.GetComponentInParent<VillagerAI>() == null &&
            hit.GetComponentInParent<SmartNpcAI>() == null &&
            hit.GetComponentInParent<NpcMapMover2D>() == null;
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

    bool IsTaskProviderInteractionCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        NpcTaskProvider provider =
            hit.GetComponentInParent<NpcTaskProvider>();
        if (provider == null)
        {
            return false;
        }

        Transform providerPoint = provider.providerPoint;
        if (providerPoint != null &&
            (hit.transform == providerPoint ||
            hit.transform.IsChildOf(providerPoint)))
        {
            return true;
        }

        Transform providerStandPoint = provider.providerStandPoint;
        if (providerStandPoint != null &&
            (hit.transform == providerStandPoint ||
            hit.transform.IsChildOf(providerStandPoint)))
        {
            return true;
        }

        return false;
    }

    bool IsInteractionPointCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        NpcInteractionPoint interactionPoint =
            hit.GetComponentInParent<NpcInteractionPoint>();
        if (interactionPoint == null)
        {
            return false;
        }

        return hit.transform == interactionPoint.transform ||
            hit.transform.IsChildOf(interactionPoint.transform);
    }

    bool IsInteriorFocusCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        return hit.GetComponentInParent<InteriorCameraFocus>() != null;
    }

    bool IsSelfCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
        {
            return true;
        }

        if (ownColliders == null)
        {
            return false;
        }

        foreach (Collider2D own in ownColliders)
        {
            if (own == hit)
            {
                return true;
            }
        }

        return false;
    }

}
