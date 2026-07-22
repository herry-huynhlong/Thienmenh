using UnityEngine;

// Directed gather/treasure actions and their collision/presentation support.
public partial class SmartNpcAI
{
    void SyncCultivationEffect()
    {
        if (IsDead)
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
                Instantiate(cultivationEffectPrefab);
            cultivationEffectInstance.name = cultivationEffectPrefab.name;
            cultivationEffectInstance.SetActive(false);
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
            other.GetComponentInParent<NpcMapMover2D>() == null &&
            other.GetComponentInParent<MonsterAI>() == null &&
            !IsSoftNpcTrafficCollider(other))
        {
            return;
        }

        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>();
        }

        for (int i = 0; i < selfColliders.Length; i++)
        {
            Collider2D own = selfColliders[i];
            if (own != null &&
                !own.isTrigger &&
                own != other)
            {
                Physics2D.IgnoreCollision(own, other, true);
            }
        }
    }

    void TryIgnoreCombatTargetCollision(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>();
        }

        Collider2D[] targetColliders =
            target.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < selfColliders.Length; i++)
        {
            Collider2D own = selfColliders[i];
            if (own == null || own.isTrigger)
            {
                continue;
            }

            for (int j = 0; j < targetColliders.Length; j++)
            {
                Collider2D other = targetColliders[j];
                if (other == null || other.isTrigger || own == other)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(own, other, true);
            }
        }
    }

    float GetCombatSurfaceDistance(Transform target)
    {
        if (target == null)
        {
            return float.PositiveInfinity;
        }

        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>(true);
        }

        Collider2D[] targetColliders =
            target.GetComponentsInChildren<Collider2D>(true);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < selfColliders.Length; i++)
        {
            Collider2D own = selfColliders[i];
            if (own == null || own.isTrigger || !own.enabled)
            {
                continue;
            }

            for (int j = 0; j < targetColliders.Length; j++)
            {
                Collider2D other = targetColliders[j];
                if (other == null || other.isTrigger || !other.enabled)
                {
                    continue;
                }

                ColliderDistance2D distanceInfo =
                    own.Distance(other);
                float gap =
                    distanceInfo.isOverlapped
                        ? 0f
                        : Mathf.Max(0f, distanceInfo.distance);
                bestDistance = Mathf.Min(bestDistance, gap);
            }
        }

        if (float.IsPositiveInfinity(bestDistance))
        {
            return Vector2.Distance(transform.position, target.position);
        }

        return bestDistance;
    }

    void TryEscapeObstacleCollision(Collision2D collision)
    {
        if (collision == null || collision.contactCount <= 0)
        {
            return;
        }

        if (IsCurrentTargetCollider(collision.collider) ||
            IsCurrentMonsterCollider(collision.collider) ||
            IsSoftNpcTrafficCollider(collision.collider))
        {
            return;
        }

        if (ignoreNpcBodyCollisions &&
            collision.collider != null &&
            (collision.collider.GetComponentInParent<VillagerAI>() != null ||
             collision.collider.GetComponentInParent<SmartNpcAI>() != null ||
             collision.collider.GetComponentInParent<NpcMapMover2D>() != null ||
             collision.collider.GetComponentInParent<MonsterAI>() != null))
        {
            return;
        }

        Vector2 normal = collision.GetContact(0).normal;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 escapeSeed =
            transform.position +
            (Vector3)(normal.normalized *
            Mathf.Max(unstuckOffsetRadius, targetClearRadius * 3f));

        if (!TryFindClearPointNear(escapeSeed, out Vector3 clearPoint))
        {
            return;
        }

        escapeTarget = clearPoint;
        hasEscapeTarget = true;
        hasObstacleAvoidTarget = false;
        blockedMoveTimer = 0f;

        DebugFlow(
            "Escape",
            "Commit collision escape collider=" +
            (collision.collider != null
                ? collision.collider.name
                : "null") +
            " point=" +
            clearPoint);

        if (rb != null)
        {
            SetDesiredVelocity(
                normal.normalized * moveSpeed * 0.75f);
        }
    }

    void ResolveInitialObstacleOverlap()
    {
        if (!IsPositionBlocked(transform.position) &&
            !HasBlockingColliderOverlap())
        {
            return;
        }

        if (!TryFindClearPointNear(transform.position, out Vector3 clearPoint, false))
        {
            return;
        }

        transform.position = clearPoint;
        spawnPosition = clearPoint;
        lastUnstuckPosition = clearPoint;

        if (rb != null)
        {
            rb.position = clearPoint;
            StopMovingSmooth(true);
        }

        Physics2D.SyncTransforms();
    }

    bool HasBlockingColliderOverlap()
    {
        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>();
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        Collider2D[] hits = new Collider2D[32];

        foreach (Collider2D own in selfColliders)
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


    public void ForceTreasureWait(
        Vector3 origin,
        float safeRadius,
        StatItemData item,
        bool lowPowerSkirmish)
    {
        if (item == null || IsDead)
        {
            return;
        }

        waitingOutsideTreasureLightning = true;
        treasureHuntTarget = null;
        treasureHuntItem = item;
        treasureWaitLowPowerSkirmish = lowPowerSkirmish;
        currentTarget = null;
        TraceRuntime(
            "ForceTreasureWait",
            "origin=" + origin +
            " safeRadius=" + safeRadius.ToString("0.00") +
            " item=" + (item != null ? ItemText.Name(item) : "null") +
            " lowPowerSkirmish=" + lowPowerSkirmish);

        Vector2 away = transform.position - origin;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = Random.insideUnitCircle.normalized;
        }

        treasureWaitPosition =
            origin +
            (Vector3)away.normalized * Mathf.Max(0.5f, safeRadius);
        hasTreasureWaitPosition = true;
        RequestEmergencyTask(
            SmartAITaskGoal.Treasure,
            SmartAITaskPriority.Emergency,
            false,
            lowPowerSkirmish ? "treasure skirmish" : "treasure wait");
        currentAction = NpcText.Action("goHunt");
    }
    public void ForceTreasureHunt(
        Transform target,
        StatItemData item)
    {
        if (target == null ||
            item == null ||
            IsDead)
        {
            return;
        }

        waitingOutsideTreasureLightning = false;
        hasTreasureWaitPosition = false;
        treasureWaitLowPowerSkirmish = false;
        treasureHuntTarget = target;
        treasureHuntItem = item;
        currentTarget = target;
        TraceRuntime(
            "ForceTreasureHunt",
            "target=" + (target != null ? target.name : "null") +
            " item=" + (item != null ? ItemText.Name(item) : "null"));
        RequestEmergencyTask(
            SmartAITaskGoal.Treasure,
            SmartAITaskPriority.Emergency,
            false,
            "treasure hunt");
        currentAction = NpcText.ActionFormat("treasureHuntNamed", ItemText.Name(item));
    }

    public void ForceGatherTarget(
        Transform target,
        StatItemData item)
    {
        if (target == null ||
            IsDead)
        {
            return;
        }

        // Never let a stale gather heartbeat steal control back from an
        // already-active combat intent.
        SmartAITask task = currentSmartTask;
        bool hasCombatTask =
            task != null &&
            task.IsValid &&
            (task.goal == SmartAITaskGoal.Combat ||
            task.goal == SmartAITaskGoal.Pursued);
        bool hasCombatIntent =
            currentMonsterTarget != null ||
            HasCombatSupportIntent() ||
            hasCombatTask ||
            currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);
        if (hasCombatIntent)
        {
            return;
        }

        // NpcResourceGatherer refreshes the same reservation every Update.
        // Treat that as a heartbeat, not a new order; otherwise it clears the
        // detour and recovery counters before the anti-stuck logic can finish.
        if (currentTarget == target &&
            (currentAction == NpcText.Action("gatherResource") ||
             IsTeleportRouteAction(currentAction)))
        {
            return;
        }

        waitingOutsideTreasureLightning = false;
        hasTreasureWaitPosition = false;
        treasureWaitLowPowerSkirmish = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        currentMonsterTarget = null;
        ClearMonsterCombatState();
        hasCultivationTarget = false;
        hasHomeReturnTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        unstuckRecoveryAttempts = 0;
        actionTimer = 0f;

        currentTarget = target;
        hasWanderTarget = false;
        currentAction = NpcText.Action("gatherResource");
        TraceRuntime(
            "ForceGatherTarget",
            "target=" + target.name +
            " item=" + (item != null ? ItemText.Name(item) : "null"));
        DebugFlow(
            "Gather",
            "Force gather target " +
            (item != null ? ItemText.Name(item) : target.name));
    }

    public void ReleaseGatherTarget(Transform target)
    {
        if (target == null || currentTarget != target)
        {
            return;
        }

        currentTarget = null;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        stuckMoveTimer = 0f;
        blockedMoveTimer = 0f;
        unstuckRecoveryAttempts = 0;

        if (currentAction == NpcText.Action("gatherResource"))
        {
            currentAction = NpcText.Action("idle");
        }

        if (rb != null)
        {
            StopMovingSmooth();
        }
    }

    public void ClearTreasureHunt()
    {
        if (treasureHuntTarget == null && treasureHuntItem == null)
        {
            return;
        }

        waitingOutsideTreasureLightning = false;
        hasTreasureWaitPosition = false;
        treasureWaitLowPowerSkirmish = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        ClearEmergencyTaskIfMatches(SmartAITaskGoal.Treasure);
        if (currentTarget != null && currentAction.Contains(NpcText.Action("treasureHunt")))
        {
            currentTarget = null;
        }

        if (rb != null)
        {
            StopMovingSmooth();
        }

        currentAction = NpcText.Action("calm");
    }

    void RefreshTreasureHuntAction()
    {
        if (waitingOutsideTreasureLightning)
        {
            return;
        }

        if (treasureHuntTarget == null || treasureHuntItem == null)
        {
            ClearTreasureHunt();
            return;
        }

        currentTarget = treasureHuntTarget;
        currentAction = NpcText.ActionFormat(
            "treasureHuntNamed",
            ItemText.Name(treasureHuntItem));
    }

    void UpdateTreasureWaitAction()
    {
        if (!waitingOutsideTreasureLightning ||
            !hasTreasureWaitPosition ||
            treasureHuntItem == null)
        {
            return;
        }

        string itemName = ItemText.Name(treasureHuntItem);
        if (Vector2.Distance(transform.position, treasureWaitPosition) <= escapeTargetReachDistance)
        {
            currentAction = treasureWaitLowPowerSkirmish
                ? NpcText.ActionFormat("outerSkirmishNamed", itemName)
                : NpcText.ActionFormat("waitLightningNamed", itemName);
            return;
        }

        currentAction = NpcText.Action("goHunt");
    }

    void ReturnToSpawn()
    {
        homeReturnTarget = transform.position;
        hasHomeReturnTarget = false;

        if (rb != null)
        {
            StopMovingSmooth();
        }

        currentTarget = null;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        currentAction = NpcText.Action("idle");
        TraceRuntime(
            "ReturnToSpawn",
            "homeReturnTarget=" + homeReturnTarget +
            " action=idle");
    }

    bool TryPickIdleWanderTarget(out Vector3 target)
    {
        Vector3 anchor = spawnPosition;
        if (homePoint != null)
        {
            anchor = homePoint.position;
        }
        else if (cultivationPoint != null)
        {
            anchor = cultivationPoint.position;
        }

        if (TryPickIdleWanderTargetFromCenter(
                transform.position,
                anchor,
                out target))
        {
            return true;
        }

        if (homePoint != null &&
            TryPickIdleWanderTargetFromCenter(
                homePoint.position,
                homePoint.position,
                out target))
        {
            return true;
        }

        if (cultivationPoint != null &&
            TryPickIdleWanderTargetFromCenter(
                cultivationPoint.position,
                anchor,
                out target))
        {
            return true;
        }

        target = transform.position;
        return false;
    }

    bool TryPickIdleWanderTargetFromCenter(
        Vector3 center,
        Vector3 anchor,
        out Vector3 target)
    {
        bool foundFeasibleOnly = false;
        Vector3 feasibleOnlyTarget = transform.position;

        for (int i = 0; i < Mathf.Max(1, idleWanderPickAttempts); i++)
        {
            Vector2 offset =
                Random.insideUnitCircle *
                Mathf.Max(0.1f, idleWanderRadius);

            Vector3 candidate =
                center +
                new Vector3(offset.x, offset.y, 0f);

            if (Vector2.Distance(transform.position, candidate) <
                Mathf.Max(idleWanderArriveDistance * 2f, idleWanderMinDistance))
            {
                continue;
            }

            if (Vector2.Distance(anchor, candidate) >
                Mathf.Max(maxRoamDistance, idleWanderRadius))
            {
                continue;
            }

            if (!IsMoveTargetFeasible(candidate) ||
                !HasClearLineTo(candidate))
            {
                if (!foundFeasibleOnly &&
                    IsMoveTargetFeasible(candidate))
                {
                    feasibleOnlyTarget = candidate;
                    foundFeasibleOnly = true;
                }

                continue;
            }

            target = candidate;
            return true;
        }

        if (foundFeasibleOnly)
        {
            target = feasibleOnlyTarget;
            return true;
        }

        target = transform.position;
        return false;
    }

    bool IsStationaryAction(string action)
    {
        return action == NpcText.Action("eating") ||
            action == NpcText.Action("idle") ||
            action == NpcText.Action("rest") ||
            action == NpcText.Action("restNearHome") ||
            action == NpcText.Action("restVillageNoon") ||
            action == NpcText.Action("stayNearHome") ||
            action == NpcText.Action("cultivate") ||
            action == NpcText.Action("cultivateAbsorbQi") ||
            action == NpcText.Action("waitTribulation") ||
            IsMonsterCombatAnimationAction(action) ||
            ContainsIgnoreCase(action, "waitSchedule") ||
            action == NpcText.Action("breakthrough") ||
            IsBlockingInjuredAction(action) ||
            action == NpcText.Action("dead") ||
            action == NpcText.Action("oldAgeDeath") ||
            action == NpcText.Action("outerSkirmishNamed") ||
            action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("checkedVanBaoLau") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("waitLightningNamed");
    }

    bool IsBlockingInjuredAction(string action)
    {
        return action == NpcText.Action("injured") &&
            (IsRecoveringFromDamage ||
             IsLowHpRecoveryTaskActive());
    }

    static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source) &&
            !string.IsNullOrEmpty(value) &&
            source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool HasLockedDirectedTarget()
    {
        bool hasDirectedTravelTarget =
            currentTarget != null ||
            hasWanderTarget;

        if (!hasDirectedTravelTarget)
        {
            return false;
        }

        return currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("visitedTaskProvider") ||
            currentAction == NpcText.Action("tradeSeek") ||
            currentAction == NpcText.Action("goTavern") ||
            currentAction == NpcText.Action("buyPill") ||
            currentAction == NpcText.Action("goHunt") ||
            currentAction == NpcText.Action("goCultivatePoint") ||
            currentAction == NpcText.Action("goMarketTrade") ||
            currentAction == NpcText.Action("goVanBaoLauBroker") ||
            currentAction == NpcText.Action("goWorkTask") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("pickItem") ||
            currentAction == NpcText.Action("pickHuntEvidence") ||
            currentAction == NpcText.Action("fleeMonsterArea") ||
            currentAction == NpcText.Action("guardSpiritHerbMonster") ||
            currentAction == NpcText.Action("fightBlockingMonster") ||
            currentAction == NpcText.Action("clearHarvestMonster") ||
            currentAction == NpcText.Action("treasureHuntNamed") ||
            currentAction == NpcText.Action("outerSkirmishNamed") ||
            currentAction == NpcText.Action("walkingRoad") ||
            IsTeleportRouteAction(currentAction);
    }

}
