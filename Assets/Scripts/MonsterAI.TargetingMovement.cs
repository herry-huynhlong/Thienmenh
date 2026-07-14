using UnityEngine;

public partial class MonsterAI
{
    void AcquireIntruderTarget()
    {
        ClearCurrentTarget();
        NpcPerformanceOverlay.RecordMonsterDetectScan();

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                detectRange,
                intruderLayers);
        Transform bestTarget = null;
        IDamageable bestDamageable = null;
        float bestScore = float.PositiveInfinity;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IDamageable damageable =
                hit.GetComponentInParent<IDamageable>();
            if (damageable == null ||
                damageable.IsDead ||
                damageable.DamageTransform == null)
            {
                continue;
            }

            Transform candidate = damageable.DamageTransform;
            if (!CanAttackIntruder(candidate.gameObject))
            {
                continue;
            }

            float distanceFromHome =
                Vector2.Distance(startPosition, candidate.position);
            if (guardTerritory &&
                distanceFromHome > territoryRadius)
            {
                continue;
            }

            float distance =
                Vector2.Distance(transform.position, candidate.position);
            float score = distance - GetIntruderPriority(candidate.gameObject);
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
                bestDamageable = damageable;
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            currentTargetDamageable = bestDamageable;
            hasTarget = false;
            currentAction = NpcText.Action("detectIntruder");
            DebugFlow(
                "Target",
                "Acquire target=" +
                bestTarget.name +
                " distance=" +
                Vector2.Distance(
                    transform.position,
                    bestTarget.position).ToString("0.00"));
            TryIgnoreCombatTargetCollision(currentTarget);
        }
    }

    bool CanAttackIntruder(GameObject candidate)
    {
        if (candidate == null ||
            candidate == gameObject)
        {
            return false;
        }

        if (NpcPetCompanion.BlocksMonsterAttacks(candidate))
        {
            return false;
        }

        if (candidate.GetComponentInParent<PlayerHealth>() != null ||
            candidate.CompareTag("Player"))
        {
            return attackPlayer;
        }

        if (candidate.GetComponentInParent<VillagerAI>() != null)
        {
            return attackVillagers;
        }

        if (candidate.GetComponentInParent<SmartNpcAI>() != null)
        {
            return attackSmartNpcs;
        }

        if (candidate.GetComponentInParent<MonsterAI>() != null)
        {
            return attackOtherMonsters;
        }

        return false;
    }

    float GetIntruderPriority(GameObject candidate)
    {
        if (candidate == null)
        {
            return 0f;
        }

        if (candidate.CompareTag("Player") ||
            candidate.GetComponentInParent<PlayerHealth>() != null)
        {
            return 1f;
        }

        return 0f;
    }

    bool HasValidTarget()
    {
        if (currentTarget == null ||
            currentTargetDamageable == null)
        {
            return false;
        }

        if (currentTargetDamageable.IsDead)
        {
            ClearCurrentTarget();
            return false;
        }

        return true;
    }

    void ClearCurrentTarget()
    {
        currentTarget = null;
        currentTargetDamageable = null;
        StopRetreating();
        isAttacking = false;
        desiredVelocity = Vector2.zero;
        CancelInvoke(nameof(ApplyAttackDamage));
        CancelInvoke(nameof(EndAttack));

        if (IsCombatStateAction(currentAction))
        {
            currentAction = NpcText.Action("restTerritory");
            SetMovingAnimation(false);
        }
    }

    bool IsCombatStateAction(string action)
    {
        return action == NpcText.Action("detectIntruder") ||
            action == NpcText.Action("chaseIntruder") ||
            action == NpcText.Action("attackIntruder") ||
            action == NpcText.Action("flee");
    }

    bool ShouldAttackTarget(Transform target)
    {
        if (target != null &&
            NpcPetCompanion.BlocksMonsterAttacks(target.gameObject))
        {
            return false;
        }

        float reason = hunger * 0.45f +
            aggression * 0.3f +
            bloodlust * 0.2f +
            territorial * 0.15f;

        if (guardTerritory &&
            target != null &&
            Vector2.Distance(startPosition, target.position) <= territoryRadius)
        {
            reason += territorial * 0.35f;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null &&
            timeSystem.IsDangerousNight())
        {
            reason += 15f;
        }

        return reason >= 45f;
    }

    bool ShouldFleeFrom(Transform target)
    {
        return TryBeginRetreat(target);
    }

    bool TryBeginRetreat(Transform target)
    {
        if (target == null ||
            Time.time < nextRetreatRollTime)
        {
            return false;
        }

        if (!HasSevereRealmSuppression(target) ||
            !IsInsideOwnTerritory(target))
        {
            return false;
        }

        nextRetreatRollTime = Time.time + retreatRetryDelay;

        if (Random.value > retreatChanceWhenSuppressed)
        {
            return false;
        }

        isRetreating = true;
        retreatUntilTime = Time.time + retreatDuration;
        currentAction = NpcText.Action("retreat");
        return true;
    }

    bool ShouldContinueRetreating(Transform target)
    {
        return isRetreating &&
            target != null &&
            Time.time < retreatUntilTime &&
            HasSevereRealmSuppression(target) &&
            IsInsideOwnTerritory(target);
    }

    void StopRetreating()
    {
        isRetreating = false;
        retreatUntilTime = 0f;
    }

    bool HasSevereRealmSuppression(Transform target)
    {
        int targetPower = GetTargetRealmPower(target);
        if (targetPower <= 0)
        {
            return false;
        }

        int selfPower = GetSelfRealmPower();
        return targetPower >= selfPower + 2;
    }

    bool IsInsideOwnTerritory(Transform target)
    {
        if (!guardTerritory ||
            target == null)
        {
            return false;
        }

        return Vector2.Distance(startPosition, target.position) <= territoryRadius;
    }

    int GetSelfRealmPower()
    {
        return Mathf.Max(
            1,
            CultivationProgression.GetRealmPower(realm, realmStage));
    }

    int GetTargetRealmPower(Transform target)
    {
        if (target == null)
        {
            return 0;
        }

        CharacterStats stats = target.GetComponentInParent<CharacterStats>();
        if (stats != null)
        {
            return Mathf.Max(
                1,
                CultivationProgression.GetRealmPower(
                    stats.realm,
                    stats.realmStage));
        }

        VillagerAI villager = target.GetComponentInParent<VillagerAI>();
        if (villager != null &&
            villager.enabled)
        {
            return Mathf.Max(
                1,
                CultivationProgression.GetRealmPower(
                    CultivationRealm.Mortal,
                    1));
        }

        SmartNpcAI smartNpc = target.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return Mathf.Max(
                1,
                CultivationProgression.GetRealmPower(
                    smartNpc.realm,
                    smartNpc.realmStage));
        }

        MonsterAI monster = target.GetComponentInParent<MonsterAI>();
        if (monster != null)
        {
            return Mathf.Max(
                1,
                CultivationProgression.GetRealmPower(
                    monster.realm,
                    monster.realmStage));
        }

        return 0;
    }

    void FleeFrom(Transform threat)
    {
        if (threat == null)
        {
            return;
        }

        Vector2 direction =
            ((Vector2)transform.position - (Vector2)threat.position).normalized;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Random.insideUnitCircle.normalized;
        }

        desiredVelocity = direction * moveSpeed * 1.25f;
        currentAction = NpcText.Action("flee");
        SetMovingAnimation(true);
        FaceDirection(direction);
    }

    void ReturnToTerritory()
    {
        Vector2 direction = startPosition - (Vector2)transform.position;
        float distance = direction.magnitude;
        if (distance <= 0.15f)
        {
            desiredVelocity = Vector2.zero;
            currentAction = NpcText.Action("restTerritory");
            SetMovingAnimation(false);
            return;
        }

        desiredVelocity = direction.normalized * moveSpeed;
        currentAction = NpcText.Action("returnTerritory");
        SetMovingAnimation(true);
        FaceDirection(direction);
    }

    void Patrol()
    {
        if (!hasTarget)
        {
            desiredVelocity = Vector2.zero;
            currentAction = NpcText.Action("restTerritory");
            patrolRecoveryAttempts = 0;
            waitTimer -= Time.deltaTime;
            SetMovingAnimation(false);

            if (waitTimer <= 0)
            {
                ChooseNewPoint();
            }

            return;
        }

        Vector2 direction = targetPosition - (Vector2)transform.position;
        float distance = direction.magnitude;

        if (distance < 0.1f)
        {
            hasTarget = false;
            waitTimer = waitTime;
            desiredVelocity = Vector2.zero;
            currentAction = NpcText.Action("restTerritory");
            patrolRecoveryAttempts = 0;
            SetMovingAnimation(false);
            return;
        }

        direction = direction.normalized;
        desiredVelocity = direction * moveSpeed;
        currentAction = NpcText.Action("patrolTerritory");
        SetMovingAnimation(true);
        FaceDirection(direction);
    }

    bool TryFinishPatrolMovement()
    {
        if (!hasTarget ||
            HasValidTarget() ||
            currentAction != NpcText.Action("patrolTerritory"))
        {
            return false;
        }

        Vector2 remaining = targetPosition - rb.position;
        float arrivalDistance =
            Mathf.Max(
                0.1f,
                desiredVelocity.magnitude * Time.fixedDeltaTime * 1.25f);
        if (remaining.magnitude > arrivalDistance)
        {
            return false;
        }

        // Arrival is checked in FixedUpdate so a throttled Update cannot let
        // the rigidbody cross a small patrol point and reverse forever.
        rb.position = targetPosition;
        rb.linearVelocity = Vector2.zero;
        desiredVelocity = Vector2.zero;
        hasTarget = false;
        waitTimer = waitTime;
        currentAction = NpcText.Action("restTerritory");
        patrolRecoveryAttempts = 0;
        stuckMoveTimer = 0f;
        lastUnstuckPosition = targetPosition;
        SetMovingAnimation(false);
        return true;
    }

    void FollowTarget(float distance)
    {
        if (isAttacking ||
            !HasValidTarget())
        {
            return;
        }

        TryIgnoreCombatTargetCollision(currentTarget);
        distance = GetCombatSurfaceDistance(currentTarget);

        Vector2 direction = currentTarget.position - transform.position;
        FaceDirection(direction);

        DebugFlow(
            "Combat",
            "Follow target=" +
            currentTarget.name +
            " surfaceDistance=" +
            distance.ToString("0.00") +
            " attackRange=" +
            attackRange.ToString("0.00") +
            " attacking=" +
            isAttacking);

        if (distance > attackRange)
        {
            desiredVelocity = direction.normalized * moveSpeed;
            currentAction = NpcText.Action("chaseIntruder");
            SetMovingAnimation(true);
            return;
        }

        desiredVelocity = Vector2.zero;
        currentAction = NpcText.Action("attackIntruder");
        SetMovingAnimation(false);

        if (attackTimer <= 0f)
        {
            Attack();
        }
    }

    void UpdateMovementRecovery()
    {
        if (desiredVelocity.sqrMagnitude <= 0.0001f)
        {
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            if (!hasTarget)
            {
                patrolRecoveryAttempts = 0;
            }
            return;
        }

        float moved =
            Vector2.Distance(
                transform.position,
                lastUnstuckPosition);
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

        DebugFlow(
            "Unstuck",
            "Recover from stall action=" + currentAction +
            " hasTarget=" + hasTarget +
            " combat=" + (currentTarget != null));

        if (HasValidTarget())
        {
            patrolRecoveryAttempts = 0;
            ClearCurrentTarget();
        }
        else
        {
            patrolRecoveryAttempts += 1;
            if (patrolRecoveryAttempts >=
                Mathf.Max(1, maxPatrolRecoveriesBeforeReset))
            {
                ResetPatrolToAnchor();
                return;
            }
        }

        ChooseNewPoint(
            Mathf.Max(0.75f, unstuckRepathRadius),
            preserveRecoveryAttempts: true);
        desiredVelocity = Vector2.zero;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        SetMovingAnimation(false);
        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;
    }

    void ResetPatrolToAnchor()
    {
        Vector2 anchorPosition = startPosition;
        hasTarget = false;
        waitTimer = Mathf.Max(0.35f, waitTime * 0.5f);
        desiredVelocity = Vector2.zero;
        currentAction = NpcText.Action("restTerritory");
        patrolRecoveryAttempts = 0;
        stuckMoveTimer = 0f;
        lastUnstuckPosition = anchorPosition;

        if (rb != null)
        {
            rb.position = anchorPosition;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            transform.position = anchorPosition;
        }

        SetMovingAnimation(false);
        DebugFlow(
            "Unstuck",
            "Reset patrol anchor pos=" + anchorPosition);
    }

    void ChooseNewPoint()
    {
        ChooseNewPoint(
            Mathf.Max(
                0.75f,
                Mathf.Min(
                    Mathf.Max(0.75f, roamRadius * 0.2f),
                    Mathf.Max(0.75f, territoryRadius * 0.2f))),
            preserveRecoveryAttempts: false);
    }

    void ChooseNewPoint(
        float minDistanceFromCurrentPosition,
        bool preserveRecoveryAttempts = false)
    {
        float patrolRadius =
            Mathf.Max(
                roamRadius,
                Mathf.Min(
                    territoryRadius > 0f ? territoryRadius : roamRadius,
                    unstuckRepathRadius));
        Vector2 bestPoint = startPosition;
        float bestDistance = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < 12; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * patrolRadius;
            Vector2 candidate = startPosition + randomPoint;
            float candidateDistance =
                Vector2.Distance(
                    transform.position,
                    candidate);
            if (candidateDistance <
                minDistanceFromCurrentPosition)
            {
                continue;
            }

            if (!IsPatrolPointReachable(candidate))
            {
                continue;
            }

            if (candidateDistance <= bestDistance)
            {
                continue;
            }

            bestPoint = candidate;
            bestDistance = candidateDistance;
            found = true;
        }

        if (!found)
        {
            hasTarget = false;
            waitTimer = Mathf.Max(0.35f, waitTime * 0.5f);
            desiredVelocity = Vector2.zero;
            currentAction = NpcText.Action("restTerritory");
            if (!preserveRecoveryAttempts)
            {
                patrolRecoveryAttempts = 0;
            }
            SetMovingAnimation(false);
            return;
        }

        targetPosition = bestPoint;
        hasTarget = true;
        waitTimer = 0f;
        if (!preserveRecoveryAttempts)
        {
            patrolRecoveryAttempts = 0;
        }
        Vector2 direction = targetPosition - (Vector2)transform.position;
        desiredVelocity = direction.normalized * moveSpeed;
        currentAction = NpcText.Action("patrolTerritory");
        SetMovingAnimation(true);
        FaceDirection(direction);
    }

    bool IsPatrolPointReachable(Vector2 candidate)
    {
        float collisionRadius = GetPatrolCollisionRadius();
        if (!IsPatrolSpaceClear(candidate, collisionRadius))
        {
            return false;
        }

        Vector2 delta = candidate - (Vector2)transform.position;
        float distance = delta.magnitude;
        if (distance <= 0.05f)
        {
            return true;
        }

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                transform.position,
                collisionRadius,
                delta / distance,
                distance);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;
            if (IsPatrolBlockingCollider(collider))
            {
                return false;
            }
        }

        return true;
    }

    float GetPatrolCollisionRadius()
    {
        if (cachedColliders == null ||
            cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider2D>(true);
        }

        float radius = 0.12f;

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider2D collider = cachedColliders[i];
            if (collider == null ||
                collider.isTrigger ||
                !collider.enabled)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            radius = Mathf.Max(
                radius,
                Mathf.Max(
                    bounds.extents.x,
                    bounds.extents.y));
        }

        return radius;
    }

    bool IsPatrolSpaceClear(Vector2 candidate, float collisionRadius)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                candidate,
                collisionRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (IsPatrolBlockingCollider(hits[i]))
            {
                return false;
            }
        }

        return true;
    }

    bool IsPatrolBlockingCollider(Collider2D collider)
    {
        if (collider == null ||
            collider.isTrigger)
        {
            return false;
        }

        if (collider.transform == transform ||
            collider.transform.IsChildOf(transform))
        {
            return false;
        }

        if (collider.attachedRigidbody != null &&
            collider.attachedRigidbody.gameObject == gameObject)
        {
            return false;
        }

        if (collider.GetComponentInParent<MonsterAI>() != null ||
            collider.GetComponentInParent<VillagerAI>() != null ||
            collider.GetComponentInParent<SmartNpcAI>() != null ||
            collider.GetComponentInParent<PlayerHealth>() != null)
        {
            return false;
        }

        return true;
    }
}
