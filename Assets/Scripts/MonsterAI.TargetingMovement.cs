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
            currentAction = "Phat hien ke xam pham";
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
        currentAction = "Luc lui";
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
        currentAction = "Bo chay";
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
            currentAction = "Nghi trong lanh dia";
            SetMovingAnimation(false);
            return;
        }

        desiredVelocity = direction.normalized * moveSpeed;
        currentAction = "Tro ve lanh dia";
        SetMovingAnimation(true);
        FaceDirection(direction);
    }

    void Patrol()
    {
        if (!hasTarget)
        {
            desiredVelocity = Vector2.zero;
            currentAction = "Nghi ngoi";
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
            currentAction = "Dung lai nghi";
            SetMovingAnimation(false);
            return;
        }

        direction = direction.normalized;
        desiredVelocity = direction * moveSpeed;
        currentAction = "Tuan tra lanh dia";
        SetMovingAnimation(true);
        FaceDirection(direction);
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
            currentAction = "Duoi ke xam pham";
            SetMovingAnimation(true);
            return;
        }

        desiredVelocity = Vector2.zero;
        currentAction = "Tan cong ke xam pham";
        SetMovingAnimation(false);

        if (attackTimer <= 0f)
        {
            Attack();
        }
    }

    void ChooseNewPoint()
    {
        Vector2 randomPoint = Random.insideUnitCircle * roamRadius;
        targetPosition = startPosition + randomPoint;
        hasTarget = true;
        currentAction = "Chon diem tuan tra";
    }
}
