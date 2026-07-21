using UnityEngine;

// Monster selection, pursuit and attack behavior is isolated from the core NPC lifecycle.
public partial class SmartNpcAI
{
    void SearchMonster()
    {
        NpcMapZone? allowedCombatZone =
            NpcMapBehaviorPolicy.GetAllowedCombatZone(gameObject);
        bool restrictToCombatZone =
            NpcMapBehaviorPolicy.ForcesCombatLoop(gameObject) &&
            allowedCombatZone.HasValue;
        bool actorInsideAllowedCombatZone =
            !restrictToCombatZone ||
            NpcMapBehaviorPolicy.IsActorInsideAllowedCombatZone(
                gameObject,
                allowedCombatZone);
        float awarenessRadius =
            GetMonsterAwarenessRadius(restrictToCombatZone);
        NpcLocationArea huntArea =
            NpcLocationArea.FindBestArea(
                gameObject,
                NpcScheduleActivity.Hunt,
                VillagerJob.None,
                NpcLocationPurpose.Hunt,
                allowedCombatZone,
                GetSmartDangerTier(),
                transform.position);

        if (restrictToCombatZone &&
            huntArea != null &&
            huntArea.zone != allowedCombatZone.Value)
        {
            huntArea = null;
        }

        bool hasHuntArea =
            huntArea != null;
        Vector3 huntAreaPosition =
            hasHuntArea
                ? huntArea.transform.position
                : spawnPosition;
        MonsterAI sharedCombatTarget =
            restrictToCombatZone
                ? NpcMapBehaviorPolicy.GetSharedCombatTarget(
                    allowedCombatZone.Value)
                : null;

        if (restrictToCombatZone &&
            !actorInsideAllowedCombatZone)
        {
            if (currentMonsterTarget != null)
            {
                ReleaseMonsterReservation(currentMonsterTarget);
                currentMonsterTarget = null;
                currentTarget = null;
                ClearMonsterCombatState();
            }

            DebugFlow(
                "Hunt",
                "Skip combat scan until reaching allowed zone " +
                allowedCombatZone.Value);
            return;
        }

        if (currentMonsterTarget != null &&
            !isCounterAttackingMonster &&
            (!ShouldSmartAutoHuntMonster(currentMonsterTarget) ||
            !CanUseMonsterTargetByMapPolicy(
                currentMonsterTarget,
                allowedCombatZone)))
        {
            ReleaseMonsterReservation(currentMonsterTarget);
            currentMonsterTarget = null;
            currentTarget = null;
            DebugFlow("Hunt", "Drop non-beast target");
        }

        if (restrictToCombatZone &&
            currentMonsterTarget != null)
        {
            if (sharedCombatTarget == null)
            {
                NpcMapBehaviorPolicy.SetSharedCombatTarget(
                    allowedCombatZone.Value,
                    currentMonsterTarget);
                sharedCombatTarget = currentMonsterTarget;
            }
            else if (currentMonsterTarget != sharedCombatTarget &&
                Vector2.Distance(
                    transform.position,
                    sharedCombatTarget.transform.position) <= awarenessRadius &&
                NpcMapBehaviorPolicy.CanUseMonsterTarget(
                    gameObject,
                    sharedCombatTarget))
            {
                ReleaseMonsterReservation(currentMonsterTarget);
                currentMonsterTarget = sharedCombatTarget;
                currentTarget = sharedCombatTarget.transform;
                TryIgnoreCombatTargetCollision(currentTarget);
                hasWanderTarget = false;
                currentAction =
                    NpcText.ActionFormat(
                        "huntMonsterNamed",
                        sharedCombatTarget.monsterName);
            }
        }

        // Neu dang co muc tieu song thi tiep tuc danh.
        if (currentMonsterTarget != null)
        {
            // Bo target neu quai da chet.
            if (currentMonsterTarget.currentHP <= 0)
            {
                if (restrictToCombatZone)
                {
                    NpcMapBehaviorPolicy.ClearSharedCombatTarget(
                        allowedCombatZone.Value,
                        currentMonsterTarget);
                }

                ReleaseMonsterReservation(currentMonsterTarget);
                currentMonsterTarget = null;
                currentTarget = null;
                ClearMonsterCombatState();
                DebugFlow("Hunt", "Current monster died");
                return;
            }

            bool targetStillInHuntArea =
                restrictToCombatZone
                    ? true
                    : hasHuntArea
                        ? IsPointInsideNpcLocationArea(
                            huntArea,
                            currentMonsterTarget.transform.position)
                        : Vector2.Distance(
                            spawnPosition,
                            currentMonsterTarget.transform.position) <=
                            maxRoamDistance;

            bool preserveCombatTarget =
                isCounterAttackingMonster ||
                MatchesSmartAction("attackMonsterNamed", true) ||
                MatchesSmartAction("attackMonster", true);

            if (!targetStillInHuntArea &&
                !preserveCombatTarget)
            {
                ReleaseMonsterReservation(currentMonsterTarget);
                currentMonsterTarget = null;
                currentTarget = null;
                ClearMonsterCombatState();
                DebugFlow("Hunt", "Monster left hunt area, drop target");
                return;
            }

            if (!targetStillInHuntArea &&
                preserveCombatTarget)
            {
                DebugFlow(
                    "Hunt",
                    "Keep combat target outside hunt area");
            }

            if (restrictToCombatZone &&
                !ShouldSharedCombatTeamFight(
                    currentMonsterTarget,
                    allowedCombatZone))
            {
                NpcMapBehaviorPolicy.ClearSharedCombatTarget(
                    allowedCombatZone.Value,
                    currentMonsterTarget);
                MonsterAI overpowerMonster = currentMonsterTarget;
                ReleaseMonsterReservation(currentMonsterTarget);
                currentMonsterTarget = null;
                currentTarget = null;
                ClearMonsterCombatState();
                TryAvoidCombatMapThreat(overpowerMonster);
                return;
            }

            if (isRetreatingFromMonster)
            {
                currentTarget = null;
                hasWanderTarget = true;
                wanderTarget = retreatTarget;
                currentAction = NpcText.Action("fleeMonsterArea");
                return;
            }

            if (!UsesSharedCombatTargeting() &&
                !TargetReservationSystem.Instance.IsReservedByOwner(
                    currentMonsterTarget.gameObject,
                    gameObject) &&
                !isCounterAttackingMonster)
            {
                ReleaseMonsterReservation(currentMonsterTarget);
                currentMonsterTarget = null;
                currentTarget = null;
                ClearMonsterCombatState();
                DebugFlow("Hunt", "Lost monster reservation");
                return;
            }

            if (CombatPowerUtility.ShouldRetreat(
                    gameObject,
                    currentMonsterTarget.gameObject))
            {
                RequestHelpForMonster(currentMonsterTarget);
                if (TryBeginMonsterRetreat(currentMonsterTarget))
                {
                    DebugFlow(
                        "Hunt",
                        "Retreat from stronger monster " +
                        DescribeMonsterMatchup(currentMonsterTarget));
                    return;
                }
            }

            if (CombatPowerUtility.ShouldRequestHelp(
                    gameObject,
                    currentMonsterTarget.gameObject))
            {
                RequestHelpForMonster(currentMonsterTarget);
            }

            currentTarget =
                currentMonsterTarget.transform;
            TryIgnoreCombatTargetCollision(currentTarget);

            // Tiep tuc tan cong muc tieu hien tai.
            TryAttackMonster();
            return;
        }

        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);

        MonsterAI sharedTarget =
            restrictToCombatZone
                ? FindSharedCombatMapTarget(allowedCombatZone.Value)
                : null;
        MonsterAI bestTarget = null;
        float closestDistance =
            Mathf.Infinity;
        MonsterAI overpowerThreatNearby = null;

        foreach (MonsterAI monster in monsters)
        {
            // Bo qua quai da chet.
            if (!ShouldSmartAutoHuntMonster(monster) ||
                monster.currentHP <= 0)
            {
                continue;
            }

            if (!CanUseMonsterTargetByMapPolicy(
                    monster,
                    allowedCombatZone))
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    transform.position,
                    monster.transform.position);
            if (distance > awarenessRadius)
            {
                continue;
            }

            if (CombatPowerUtility.ShouldRetreat(
                    gameObject,
                    monster.gameObject))
            {
                RequestHelpForMonster(monster);
                if (TryBeginMonsterRetreat(monster))
                {
                    DebugFlow(
                        "Hunt",
                        "Retreat before selecting stronger monster " +
                        DescribeMonsterMatchup(monster));
                    return;
                }
            }

            if (CombatPowerUtility.ShouldRequestHelp(
                    gameObject,
                    monster.gameObject))
            {
                RequestHelpForMonster(monster);
            }

            if (sharedTarget != null &&
                monster != sharedTarget)
            {
                continue;
            }

            if (!UsesSharedCombatTargeting() &&
                TargetReservationSystem.Instance.IsReservedByOther(
                    monster.gameObject,
                    gameObject))
            {
                continue;
            }

            bool monsterInHuntArea =
                restrictToCombatZone
                    ? true
                    : hasHuntArea
                        ? IsPointInsideNpcLocationArea(
                            huntArea,
                            monster.transform.position)
                        : Vector2.Distance(
                            spawnPosition,
                            monster.transform.position) <=
                            maxRoamDistance;

            if (!monsterInHuntArea)
            {
                continue;
            }

            if (!ShouldSharedCombatTeamFight(
                    monster,
                    allowedCombatZone))
            {
                if (restrictToCombatZone &&
                    distance <= Mathf.Max(attackRange + 3f, 5f))
                {
                    overpowerThreatNearby = monster;
                }

                continue;
            }

            // Chon muc tieu gan nhat.
            if (distance < closestDistance)
            {
                closestDistance =
                    distance;
                bestTarget =
                    monster;
            }
        }

        // Tim duoc quai phu hop.
        if (bestTarget != null)
        {
            if (TryReserveMonsterTarget(
                    bestTarget,
                    Mathf.Max(4f, attackCooldown * 4f)))
            {
                if (restrictToCombatZone)
                {
                    NpcMapBehaviorPolicy.SetSharedCombatTarget(
                        allowedCombatZone.Value,
                        bestTarget);
                }

                currentTarget =
                    bestTarget.transform;
                TryIgnoreCombatTargetCollision(currentTarget);
                currentAction =
                    NpcText.ActionFormat(
                        "huntMonsterNamed",
                        bestTarget.monsterName);
                hasWanderTarget = false;
                DebugFlow("Hunt", "Target " + bestTarget.monsterName);
                return;
            }

            DebugFlow("Hunt", "Monster already reserved by other npc");
        }

        if (restrictToCombatZone &&
            overpowerThreatNearby != null &&
            TryAvoidCombatMapThreat(overpowerThreatNearby))
        {
            return;
        }

        if (restrictToCombatZone &&
            TryStartCombatMapLootPickup(allowedCombatZone.Value))
        {
            return;
        }

        if (hasHuntArea)
        {
            Vector3 roamPoint = huntArea.GetRandomPoint(gameObject);
            currentTarget = null;
            wanderTarget = roamPoint;
            hasWanderTarget = true;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            currentAction = NpcText.Action("goHunt");
            DebugFlow("Hunt", "Patrol hunt area");
        }
        else if (restrictToCombatZone)
        {
            NpcMapArea combatArea =
                NpcMapArea.FindNearestAreaInZone(
                    allowedCombatZone.Value,
                    transform.position);
            Vector3 roamPoint =
                combatArea != null
                    ? combatArea.GetMovementCenter(transform.position)
                    : spawnPosition;
            currentTarget = null;
            wanderTarget = roamPoint;
            hasWanderTarget = true;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            currentAction = NpcText.Action("goHunt");
            DebugFlow("Hunt", "Patrol combat map");
        }
        else
        {
            DebugFlow("Hunt", "No valid monster");
            LogHuntStall("no-valid-monster");
        }
    }

    public NpcDangerTier GetSmartDangerTier()
    {
        switch (realm)
        {
            case CultivationRealm.Mortal:
            case CultivationRealm.QiRefining:
                return NpcDangerTier.Low;

            case CultivationRealm.Foundation:
            case CultivationRealm.GoldenCore:
                return NpcDangerTier.Medium;

            case CultivationRealm.NascentSoul:
            case CultivationRealm.SoulFormation:
            case CultivationRealm.Tribulation:
                return NpcDangerTier.High;

            default:
                return NpcDangerTier.Low;
        }
    }

    bool TryGetSmartResourceArea(out Vector3 resourcePosition)
    {
        resourcePosition = transform.position;

        NpcDangerTier dangerTier = GetSmartDangerTier();
        NpcLocationArea area =
            NpcLocationArea.FindBestArea(
                gameObject,
                NpcScheduleActivity.Gather,
                VillagerJob.None,
                NpcLocationPurpose.Resource,
                NpcMapZone.MaThuSonMach,
                dangerTier,
                transform.position);

        if (area == null)
        {
            area =
                NpcLocationArea.FindBestArea(
                    gameObject,
                    NpcScheduleActivity.Gather,
                    VillagerJob.None,
                    NpcLocationPurpose.Resource,
                    NpcMapZone.MaThuSonMach,
                    null,
                    transform.position);
        }

        if (area == null)
        {
            area =
                NpcLocationArea.FindBestArea(
                    gameObject,
                    NpcScheduleActivity.Gather,
                    VillagerJob.None,
                    NpcLocationPurpose.Resource,
                    null,
                    dangerTier,
                    transform.position);
        }

        if (area == null)
        {
            area =
                NpcLocationArea.FindBestArea(
                    gameObject,
                    NpcScheduleActivity.Gather,
                    VillagerJob.None,
                    NpcLocationPurpose.Resource,
                    null,
                    transform.position);
        }

        if (area == null)
        {
            return false;
        }

        resourcePosition = area.GetRandomPoint(gameObject);
        return true;
    }

void TryAttackMonster()
{
    if (currentMonsterTarget == null)
    {
        return;
    }

    if (isRetreatingFromMonster)
    {
        return;
    }

    // Bo target neu quai da chet.
    if (currentMonsterTarget.currentHP <= 0)
    {
        NpcMapZone? allowedCombatZone =
            NpcMapBehaviorPolicy.GetAllowedCombatZone(gameObject);
        if (UsesSharedCombatTargeting() &&
            allowedCombatZone.HasValue)
        {
            NpcMapBehaviorPolicy.ClearSharedCombatTarget(
                allowedCombatZone.Value,
                currentMonsterTarget);
        }

        ReleaseMonsterReservation(currentMonsterTarget);
        currentMonsterTarget = null;

        currentTarget = null;
        ClearMonsterCombatState();

        return;
    }

    if (!UsesSharedCombatTargeting() &&
        !TargetReservationSystem.Instance.IsReservedByOwner(
            currentMonsterTarget.gameObject,
            gameObject) &&
        !isCounterAttackingMonster)
    {
        ReleaseMonsterReservation(currentMonsterTarget);
        currentMonsterTarget = null;
        currentTarget = null;
        ClearMonsterCombatState();
        return;
    }

    if (CombatPowerUtility.ShouldRetreat(
            gameObject,
            currentMonsterTarget.gameObject))
    {
        RequestHelpForMonster(currentMonsterTarget);
        if (TryBeginMonsterRetreat(currentMonsterTarget))
        {
            return;
        }
    }

    TryIgnoreCombatTargetCollision(
        currentMonsterTarget.transform);

    float distance =
        GetCombatSurfaceDistance(
            currentMonsterTarget.transform);

    float engageRange =
        Mathf.Max(
            attackRange + 0.35f,
            0.45f);

    // Chua toi tam danh.
    if (distance > engageRange)
    {
        if (ShouldAbandonUnreachableMonster(currentMonsterTarget))
        {
            MonsterAI stalledMonster = currentMonsterTarget;
            ReleaseMonsterReservation(currentMonsterTarget);
            currentMonsterTarget = null;
            currentTarget = null;
            ClearMonsterCombatState();
            ClearTravelTargetsAndStop();
            currentAction = NpcText.Action("idle");
            actionTimer = 0f;
            DebugFlow(
                "Combat",
                "Abandon unreachable monster " +
                (stalledMonster != null
                    ? stalledMonster.monsterName
                    : "null"));
            return;
        }

        currentAction = NpcText.ActionFormat(
            "huntMonsterNamed",
            currentMonsterTarget.monsterName);
        return;
    }

    ResetMonsterProgressWatch(currentMonsterTarget);

    NpcRoleUtility.SetCombatAttackAction(
        gameObject,
        currentMonsterTarget.gameObject);
    actionTimer = Mathf.Max(actionTimer, 0.6f);
    if (rb != null)
    {
        rb.linearVelocity = Vector2.zero;
    }

    if (visualAnimation != null)
    {
        visualAnimation.SetFacingTarget(
            currentMonsterTarget.transform.position);
    }

    if (ShouldLogDebugFlow())
    {
        DebugFlow(
            "Combat",
            "Attack intent target=" +
            currentMonsterTarget.monsterName +
            " distance=" + distance.ToString("0.00") +
            " attackTimer=" + attackTimer.ToString("0.00") +
            " cooldown=" + attackCooldown.ToString("0.00") +
            " facingTarget=" + (visualAnimation != null));
    }

    // Hoi chieu tan cong.
    if (attackTimer < attackCooldown)
    {
        return;
    }

    if (visualAnimation != null)
    {
        visualAnimation.ReplayActionAnimation(currentAction);
    }

    if (ShouldLogDebugFlow())
    {
        DebugFlow(
            "Combat",
            "Replay attack action=" + currentAction +
            " target=" + currentMonsterTarget.monsterName);
    }

    // reset cooldown
    attackTimer = 0;

    actionTimer = Mathf.Max(actionTimer, 0.45f);
    DamageContext context = DamageContext.Attack(
        attack,
        gameObject,
        this,
        DamageSourceCategory.Npc,
        DamageType.Physical,
        NpcText.Dialogue("combatMonsterReason"),
        currentMonsterTarget.transform.position,
        true);
    DamageResult result = DamageSystem.Apply(
        currentMonsterTarget,
        context);
    int attackDamage = result.finalDamage;

    Debug.Log(NpcText.Format(NpcText.Get("logs", "attackMonster"), npcName, currentMonsterTarget.monsterName, attackDamage));
}

bool ShouldAbandonUnreachableMonster(MonsterAI monster)
{
    if (monster == null)
    {
        ResetMonsterProgressWatch(null);
        return false;
    }

    if (progressMonsterTarget != monster)
    {
        ResetMonsterProgressWatch(monster);
        return false;
    }

    float moved =
        Vector2.Distance(
            transform.position,
            lastMonsterProgressPosition);
    if (moved > Mathf.Max(unreachableMonsterMoveEpsilon, unstuckMinMoveDistance * 2f))
    {
        ResetMonsterProgressWatch(monster);
        return false;
    }

    return Time.time - monsterProgressTime >=
        Mathf.Max(unreachableMonsterSeconds, unstuckCheckDelay * 3f);
}

void ResetMonsterProgressWatch(MonsterAI monster)
{
    progressMonsterTarget = monster;
    lastMonsterProgressPosition = transform.position;
    monsterProgressTime = Time.time;
}

public void ShootFireball()
{
    if (fireballPrefab == null ||
        firePoint == null ||
        currentMonsterTarget == null)
    {
        return;
    }

    GameObject fireball =
        Instantiate(
            fireballPrefab,
            firePoint.position,
            Quaternion.identity);

    Vector2 direction =
        currentMonsterTarget.transform.position -
        firePoint.position;

    Fireball fb =
        fireball.GetComponent<Fireball>();

    if (fb != null)
    {
        fb.SetOwner(gameObject);
        fb.damage = attack;
        fb.SetDirection(direction);
    }
}

    bool ShouldFightMonster(
        MonsterAI monster)
    {
        if (!ShouldSmartAutoHuntMonster(monster))
        {
            return false;
        }

        // Bo qua quai da chet.
        if (monster.currentHP <= 0)
        {
            return false;
        }

        if (CombatPowerUtility.ShouldRetreat(gameObject, monster.gameObject))
        {
            return false;
        }

        if (CombatPowerUtility.ShouldRequestHelp(gameObject, monster.gameObject))
        {
            RequestHelpForMonster(monster);
            return true;
        }

        return true;
    }

    bool ShouldSmartAutoHuntMonster(MonsterAI monster)
    {
        return monster != null &&
            monster.huntTargetType == HuntTargetType.Beast;
    }

}
