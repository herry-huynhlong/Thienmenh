using UnityEngine;

public partial class SmartNpcAI
{
    bool isRetreatingFromMonster;
    bool isCounterAttackingMonster;
    float retreatUntilTime;
    Vector3 retreatTarget;

    bool TryHandleCombatSupport()
    {
        if (isRetreatingFromMonster)
        {
            bool shouldKeepRetreating =
                UsesSharedCombatTargeting()
                    ? Time.time < retreatUntilTime
                    : ShouldRetreatFromCurrentMonster() &&
                        Time.time < retreatUntilTime;
            if (!shouldKeepRetreating)
            {
                StopMonsterRetreat();
            }
            else
            {
                currentTarget = null;
                float safeArrivalDistance =
                    Mathf.Max(
                        0.25f,
                        escapeTargetReachDistance,
                        targetClearRadius);
                if (Vector2.Distance(transform.position, retreatTarget) <=
                    safeArrivalDistance)
                {
                    // The NPC has reached safety. Keep the retreat lock until
                    // its timer expires, but do not advertise a movement state
                    // or repeatedly recreate an already completed target.
                    hasWanderTarget = false;
                    currentAction = NpcText.Action("rest");
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                    }
                }
                else
                {
                    currentAction = NpcText.Action("fleeMonsterArea");
                    hasWanderTarget = true;
                    wanderTarget = retreatTarget;
                }
                return true;
            }
        }

        if (UsesSharedCombatTargeting() &&
            CombatPowerUtility.GetCurrentHpRatio(gameObject) <= 0.35f &&
            TryBeginCombatMapRetreat("bicanh low hp retreat"))
        {
            return true;
        }

        return false;
    }

    bool ShouldRetreatFromCurrentMonster()
    {
        return currentMonsterTarget != null &&
            CombatPowerUtility.ShouldRetreat(
                gameObject,
                currentMonsterTarget.gameObject);
    }

    void StopMonsterRetreat()
    {
        isRetreatingFromMonster = false;
        retreatUntilTime = 0f;
        retreatTarget = Vector3.zero;
        hasWanderTarget = false;
        ClearEmergencyTaskIfMatches(SmartAITaskGoal.Pursued);
        if (currentAction == NpcText.Action("fleeMonsterArea"))
        {
            currentAction = string.Empty;
        }
    }

    void RequestHelpForMonster(MonsterAI monster)
    {
    }

    bool TryBeginMonsterRetreat(MonsterAI monster)
    {
        if (monster == null ||
            !CombatPowerUtility.ShouldRetreat(
                gameObject,
                monster.gameObject))
        {
            return false;
        }

        BeginMonsterRetreat(monster);
        return true;
    }

    bool TryBeginCombatMapRetreat(string reason)
    {
        if (!BicanhSessionManager.TryGetDungeonRetreatPoint(
                gameObject,
                out Vector3 safePoint))
        {
            return false;
        }

        ReleaseMonsterReservation(currentMonsterTarget);
        currentMonsterTarget = null;
        currentTarget = null;
        ClearMonsterCombatState();
        ClearHelpRequestState();

        isRetreatingFromMonster = true;
        retreatUntilTime = Time.time + 12f;
        retreatTarget = safePoint;
        hasWanderTarget = true;
        wanderTarget = retreatTarget;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        RequestEmergencyTask(
            SmartAITaskGoal.Pursued,
            SmartAITaskPriority.Emergency,
            false,
            reason);
        currentAction = NpcText.Action("fleeMonsterArea");
        StopNpcMovement();
        DebugFlow(
            "Retreat",
            "Bicanh retreat reason=" + reason +
            " target=" + retreatTarget);
        return true;
    }

    bool TryAvoidCombatMapThreat(MonsterAI monster)
    {
        if (monster == null)
        {
            return false;
        }

        ReleaseMonsterReservation(currentMonsterTarget);
        currentMonsterTarget = null;
        currentTarget = null;
        ClearMonsterCombatState();
        ClearHelpRequestState();

        Vector3 safePoint =
            GetCombatMapAvoidPoint(monster.transform.position);
        hasWanderTarget = true;
        wanderTarget = safePoint;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = true;
        obstacleAvoidTarget = safePoint;
        hasHomeReturnTarget = false;
        actionTimer = Mathf.Max(actionTimer, 1.25f);
        currentAction = NpcText.Action("fleeMonsterArea");
        DebugFlow(
            "Retreat",
            "Bicanh avoid threat target=" +
            monster.monsterName +
            " avoidPoint=" +
            safePoint);
        return true;
    }

    Vector3 GetCombatMapAvoidPoint(Vector3 threatPosition)
    {
        Vector3 fallback = transform.position;
        NpcMapZone? zone = NpcMapNavigator.ResolveActorZone(gameObject);
        NpcMapArea area = zone.HasValue
            ? NpcMapArea.FindNearestAreaInZone(zone.Value, transform.position)
            : NpcMapArea.FindArea(transform.position);

        Vector2 away =
            (Vector2)transform.position - (Vector2)threatPosition;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = Random.insideUnitCircle;
        }

        float avoidDistance = Mathf.Max(attackRange + 2.5f, 4f);
        Vector3 candidate =
            transform.position + (Vector3)(away.normalized * avoidDistance);

        if (area != null)
        {
            candidate = area.ClosestPoint(candidate);
        }

        if (IsMoveTargetFeasible(candidate))
        {
            return candidate;
        }

        if (area != null)
        {
            Vector3 center = area.GetMovementCenter(transform.position);
            if (IsMoveTargetFeasible(center))
            {
                return center;
            }
        }

        return fallback;
    }

    bool TryReserveMonsterTarget(
        MonsterAI monster,
        float durationSeconds)
    {
        if (monster == null)
        {
            return false;
        }

        if (UsesSharedCombatTargeting())
        {
            currentMonsterTarget = monster;
            return true;
        }

        TargetReservationSystem reservationSystem =
            TargetReservationSystem.Instance;
        if (reservationSystem == null)
        {
            currentMonsterTarget = monster;
            return true;
        }

        bool reserved = reservationSystem.TryReserve(
            monster.gameObject,
            gameObject,
            durationSeconds,
            "Combat");

        if (reserved)
        {
            currentMonsterTarget = monster;
        }

        return reserved;
    }

    void ReleaseMonsterReservation(MonsterAI monster = null)
    {
        MonsterAI target = monster != null
            ? monster
            : currentMonsterTarget;

        if (target == null)
        {
            return;
        }

        TargetReservationSystem reservationSystem =
            TargetReservationSystem.Instance;
        if (reservationSystem != null)
        {
            reservationSystem.Release(target.gameObject, gameObject);
        }
    }

    void BeginMonsterRetreat(MonsterAI monster)
    {
        if (!TryEnterMonsterRetreat(monster, false))
        {
            return;
        }
    }

    void ForceBeginMonsterRetreatForDebug(MonsterAI monster)
    {
        TryEnterMonsterRetreat(monster, true);
    }

    bool TryEnterMonsterRetreat(MonsterAI monster, bool force)
    {
        if (monster == null)
        {
            return false;
        }

        if (!force &&
            !CombatPowerUtility.ShouldRetreat(
                gameObject,
                monster.gameObject))
        {
            DebugFlow(
                "Hunt",
                "Ignored retreat request " +
                DescribeMonsterMatchup(monster));
            return false;
        }

        isRetreatingFromMonster = true;
        retreatUntilTime = Time.time + 2.5f;
        retreatTarget = GetRetreatPoint(monster.transform.position);
        currentTarget = null;
        hasWanderTarget = true;
        wanderTarget = retreatTarget;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        RequestEmergencyTask(
            SmartAITaskGoal.Pursued,
            SmartAITaskPriority.Emergency,
            false,
            "monster retreat");
        currentAction = NpcText.Action("fleeMonsterArea");
        StopNpcMovement();
        DebugFlow(
            "Retreat",
            "Begin retreat target=" +
            monster.monsterName +
            " retreatTarget=" +
            retreatTarget +
            " matchup=" +
            DescribeMonsterMatchup(monster));
        return true;
    }

    Vector3 GetRetreatPoint(Vector3 threatPosition)
    {
        if (UsesSharedCombatTargeting() &&
            BicanhSessionManager.TryGetDungeonRetreatPoint(
                gameObject,
                out Vector3 dungeonRetreatPoint))
        {
            return dungeonRetreatPoint;
        }

        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        NpcTeleportGate escapeGate = FindNearestTeleportGate(area);
        if (escapeGate != null)
        {
            return escapeGate.EntryPosition;
        }

        Vector3 center = transform.position;
        if (area != null &&
            area.areaBounds != null)
        {
            center = area.areaBounds.bounds.center;
        }

        Vector2 away =
            (Vector2)transform.position - (Vector2)threatPosition;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = Random.insideUnitCircle;
        }

        Vector2 toCenter = (Vector2)center - (Vector2)transform.position;
        if (toCenter.sqrMagnitude <= 0.01f)
        {
            toCenter = Vector2.zero;
        }

        Vector2 mixedDirection = away.normalized * 0.75f;
        if (toCenter.sqrMagnitude > 0.01f)
        {
            mixedDirection += toCenter.normalized * 0.55f;
        }

        if (mixedDirection.sqrMagnitude <= 0.01f)
        {
            mixedDirection = away.normalized;
        }

        float retreatDistance =
            Mathf.Max(4f, Mathf.Max(1f, idleWanderRadius) * 1.5f);
        Vector3 point =
            transform.position + (Vector3)mixedDirection.normalized * retreatDistance;

        if (area != null &&
            area.areaBounds != null)
        {
            Bounds bounds = area.areaBounds.bounds;
            float padding = Mathf.Max(
                1.75f,
                Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.22f);
            float minX = bounds.min.x + padding;
            float maxX = bounds.max.x - padding;
            float minY = bounds.min.y + padding;
            float maxY = bounds.max.y - padding;

            if (minX >= maxX || minY >= maxY)
            {
                point = bounds.center;
            }
            else
            {
                point.x = Mathf.Clamp(point.x, minX, maxX);
                point.y = Mathf.Clamp(point.y, minY, maxY);
            }
        }

        return point;
    }

    NpcTeleportGate FindNearestTeleportGate(NpcMapArea area)
    {
        if (area == null)
        {
            return null;
        }

        NpcTeleportGate bestGate = null;
        float bestDistance = float.PositiveInfinity;

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                !gate.TryGetOtherZone(area.zone, out _))
            {
                continue;
            }

            Vector3 gatePosition = gate.EntryPosition;
            NpcMapArea gateArea = NpcMapArea.FindArea(gatePosition);
            if (gateArea == null ||
                gateArea.zone != area.zone)
            {
                continue;
            }

            float distance =
                Vector2.Distance(transform.position, gatePosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestGate = gate;
            }
        }

        return bestGate;
    }

    void ClearHelpRequestState()
    {
    }

    void ClearMonsterCombatState()
    {
        isCounterAttackingMonster = false;
        ResetMonsterProgressWatch(null);
    }

    void TryCounterAttackFromDamage(
        GameObject attackerObject,
        int incomingDamage)
    {
        if (attackerObject == null ||
            IsDead)
        {
            return;
        }

        MonsterAI monster =
            attackerObject.GetComponentInParent<MonsterAI>();
        if (monster == null ||
            monster.IsDead)
        {
            return;
        }

        if (!NpcMapBehaviorPolicy.CanUseMonsterTarget(
                gameObject,
                monster))
        {
            return;
        }

        if (currentMonsterTarget != null &&
            currentMonsterTarget != monster)
        {
            ReleaseMonsterReservation(currentMonsterTarget);
        }

        NpcTaskProvider.ReleaseNpcFromProviderTasksForCombat(gameObject);
        ClearHelpRequestState();
        StopMonsterRetreat();
        isCounterAttackingMonster = true;

        TryReserveMonsterTarget(
            monster,
            Mathf.Max(4f, attackCooldown * 4f));

        currentMonsterTarget = monster;
        currentTarget = monster.transform;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        actionTimer = 0f;
        attackTimer = Mathf.Max(attackTimer, attackCooldown);
        currentAction = NpcText.ActionFormat(
            "attackMonsterNamed",
            monster.monsterName);

        TryIgnoreCombatTargetCollision(currentTarget);
        StopNpcMovement();

        DebugFlow(
            "Combat",
            "Counterattack after damage attacker=" +
            monster.monsterName +
            " damage=" +
            incomingDamage);
    }

    bool HasCombatSupportIntent()
    {
        return isRetreatingFromMonster ||
            isCounterAttackingMonster;
    }
}
