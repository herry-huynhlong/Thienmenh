using UnityEngine;

// Npc overlap separation, crowd yielding, and step-aside behavior.
public partial class VillagerAI
{
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
}
