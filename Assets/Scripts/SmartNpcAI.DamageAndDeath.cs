using UnityEngine;

// Damage intake, retaliation behavior, lifespan checks, and death handling.
public partial class SmartNpcAI
{
    public void TakeDamage(int damage)
    {
        DamageSystem.Apply(this, DamageContext.Legacy(damage));
    }

    public void TakeDamage(int damage, GameObject attackerObject)
    {
        DamageSystem.Apply(
            this,
            DamageContext.Legacy(damage, attackerObject));
    }

    public DamageResult ReceiveDamage(DamageContext context)
    {
        if (IsDead)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.TargetAlreadyDead);
        }

        EnsureCharacterStatsHealthSource();

        if (characterStats != null)
        {
            DamageResult result = characterStats.ReceiveDamage(context);
            SyncFromCharacterStats();

            if (result.wasApplied && !characterStats.IsDead)
            {
                InterruptGatheringForCombat();
                if (currentHP <= Mathf.Max(1, maxHP / 3))
                {
                    RequestEmergencyTask(
                        SmartAITaskGoal.LowHpRecovery,
                        SmartAITaskPriority.Emergency,
                        true,
                        "low hp");
                }

                if (!ShouldSuspendAutonomousDamageResponse())
                {
                    if (!enabled)
                    {
                        enabled = true;
                    }

                    TryCounterAttackFromDamage(
                        context.attacker,
                        result.finalDamage);
                }
            }

            if (characterStats.IsDead)
            {
                Die();
            }

            result.receiver = this;
            result.target = gameObject;
            return result;
        }

        int healthBefore = currentHP;
        int finalDamage =
            DamageSystem.CalculateFinalDamage(
                context,
                defense);

        if (finalDamage <= 0)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.InvalidAmount);
        }

        currentHP = Mathf.Clamp(currentHP - finalDamage, 0, maxHP);

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = currentHP;
        }

        if (currentHP > 0)
        {
            InterruptGatheringForCombat();
            if (!ShouldSuspendAutonomousDamageResponse())
            {
                if (!enabled)
                {
                    enabled = true;
                }

                TryCounterAttackFromDamage(
                    context.attacker,
                    finalDamage);

                if (currentHP <= Mathf.Max(1, maxHP / 3))
                {
                    RequestEmergencyTask(
                        SmartAITaskGoal.LowHpRecovery,
                        SmartAITaskPriority.Emergency,
                        true,
                        "low hp");
                }
            }

            NpcCombatTechniqueSystem.ReactToDamageTaken(
                gameObject,
                finalDamage);
        }

        Debug.Log(NpcText.Format(NpcText.Get("logs", "takeDamage"), npcName, finalDamage));

        if (currentHP <= 0)
        {
            Die();
        }

        return DamageResult.Applied(
            context,
            this,
            gameObject,
            finalDamage,
            healthBefore,
            currentHP);
    }

    void InterruptGatheringForCombat()
    {
        const float gatherRecoverySeconds = 1.5f;

        damageRecoveryUntil = Mathf.Max(
            damageRecoveryUntil,
            Time.time + gatherRecoverySeconds);

        if (resourceGatherer == null)
        {
            resourceGatherer = GetComponent<NpcResourceGatherer>();
        }

        if (resourceGatherer != null)
        {
            resourceGatherer.SuppressGatheringForSeconds(
                gatherRecoverySeconds);
        }

        ClearTravelTargets();
        StopNpcMovement();
        actionTimer = 0f;

        if (IsLowHpRecoveryTaskActive())
        {
            currentAction = NpcText.Action("injured");
            return;
        }

        if (currentMonsterTarget != null &&
            currentMonsterTarget.currentHP > 0)
        {
            string targetName =
                string.IsNullOrWhiteSpace(currentMonsterTarget.monsterName)
                    ? string.Empty
                    : currentMonsterTarget.monsterName;
            currentAction =
                string.IsNullOrWhiteSpace(targetName)
                    ? NpcText.Action("attackMonsterNamed")
                    : NpcText.ActionFormat(
                        "attackMonsterNamed",
                        targetName);
            return;
        }

        currentAction = NpcText.Action("idle");
    }

    void TryReactToNearbyAttackingMonster()
    {
        if (IsDead ||
            isRetreatingFromMonster)
        {
            return;
        }

        if (currentMonsterTarget != null &&
            currentMonsterTarget.currentHP > 0)
        {
            return;
        }

        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);
        NpcMapZone? allowedCombatZone =
            NpcMapBehaviorPolicy.GetAllowedCombatZone(gameObject);

        MonsterAI bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterAI monster = monsters[i];
            if (monster == null ||
                monster.currentHP <= 0 ||
                !monster.attackSmartNpcs ||
                !ShouldSmartAutoHuntMonster(monster) ||
                !CanUseMonsterTargetByMapPolicy(
                    monster,
                    allowedCombatZone))
            {
                continue;
            }

            float distance =
                GetCombatSurfaceDistance(monster.transform);
            if (distance > Mathf.Max(attackRange + 1f, 2.5f))
            {
                continue;
            }

            if (CombatPowerUtility.ShouldRetreat(
                    gameObject,
                    monster.gameObject))
            {
                RequestHelpForMonster(monster);
                TryBeginMonsterRetreat(monster);
                return;
            }

            if (!ShouldSharedCombatTeamFight(
                    monster,
                    allowedCombatZone))
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

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = monster;
            }
        }

        if (bestTarget == null)
        {
            return;
        }

        if (!TryReserveMonsterTarget(
                bestTarget,
                Mathf.Max(4f, attackCooldown * 4f)))
        {
            return;
        }

        currentMonsterTarget = bestTarget;
        currentTarget = bestTarget.transform;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        actionTimer = 0f;
        attackTimer = Mathf.Max(attackTimer, attackCooldown);
        currentAction =
            NpcText.ActionFormat(
                "attackMonsterNamed",
                bestTarget.monsterName);
        TryIgnoreCombatTargetCollision(currentTarget);
        DebugFlow(
            "Combat",
            "Interrupted gather to attack monster " +
            bestTarget.monsterName);
    }

    bool CanUseMonsterTargetByMapPolicy(
        MonsterAI monster,
        NpcMapZone? allowedCombatZone)
    {
        if (!NpcMapBehaviorPolicy.ForcesCombatLoop(gameObject))
        {
            return true;
        }

        if (!allowedCombatZone.HasValue)
        {
            return false;
        }

        if (monster == null)
        {
            return false;
        }

        if (!NpcMapBehaviorPolicy.IsActorInsideAllowedCombatZone(
                gameObject,
                allowedCombatZone))
        {
            return false;
        }

        NpcMapZone? monsterZone =
            NpcMapNavigator.ResolveActorZone(monster.gameObject);
        return monsterZone.HasValue &&
            monsterZone.Value == allowedCombatZone.Value;
    }

    bool UsesSharedCombatTargeting()
    {
        return NpcMapBehaviorPolicy.ForcesCombatLoop(gameObject);
    }

    bool TryStartCombatMapLootPickup(NpcMapZone allowedCombatZone)
    {
        WorldStatItemPickup[] pickups =
            FindObjectsByType<WorldStatItemPickup>(
                FindObjectsInactive.Exclude);
        WorldStatItemPickup bestPickup = null;
        float bestDistance = Mathf.Infinity;

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (pickup == null ||
                pickup.item == null ||
                pickup.amount <= 0 ||
                !pickup.allowNpcPickup ||
                pickup.RequiresNpcHarvestAction() ||
                pickup.IsReservedByOther(gameObject))
            {
                continue;
            }

            NpcMapArea pickupArea =
                NpcMapArea.FindArea(pickup.transform.position);
            if (pickupArea == null ||
                pickupArea.zone != allowedCombatZone)
            {
                continue;
            }

            float distance =
                Vector2.Distance(transform.position, pickup.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPickup = pickup;
            }
        }

        if (bestPickup == null ||
            !bestPickup.TryReserve(gameObject, 4f))
        {
            return false;
        }

        currentTarget = bestPickup.transform;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        currentAction = NpcText.Action("goHunt");
        DebugFlow(
            "Loot",
            "Bicanh loot pickup " + ItemText.Name(bestPickup.item));
        return true;
    }

    float GetSharedCombatTeamPower(NpcMapZone allowedCombatZone)
    {
        SmartNpcAI[] smartNpcs =
            FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude);
        float power = 0f;

        foreach (SmartNpcAI ally in smartNpcs)
        {
            if (ally == null ||
                ally.IsDead ||
                CombatPowerUtility.GetCurrentHpRatio(ally.gameObject) <= 0.05f)
            {
                continue;
            }

            NpcMapZone? allyZone =
                NpcMapNavigator.ResolveActorZone(ally.gameObject);
            if (!allyZone.HasValue ||
                allyZone.Value != allowedCombatZone)
            {
                continue;
            }

            power += CombatPowerUtility.GetPower(ally.gameObject);
        }

        return Mathf.Max(1f, power);
    }

    bool ShouldSharedCombatTeamFight(
        MonsterAI monster,
        NpcMapZone? allowedCombatZone)
    {
        if (!UsesSharedCombatTargeting() ||
            !allowedCombatZone.HasValue ||
            monster == null)
        {
            return ShouldFightMonster(monster);
        }

        float teamPower =
            GetSharedCombatTeamPower(allowedCombatZone.Value);
        return CombatPowerUtility.ShouldTeamFight(
            teamPower,
            monster.gameObject,
            0.8f);
    }

    MonsterAI FindSharedCombatMapTarget(NpcMapZone allowedCombatZone)
    {
        float awarenessRadius =
            GetMonsterAwarenessRadius(true);
        MonsterAI policyTarget =
            NpcMapBehaviorPolicy.GetSharedCombatTarget(allowedCombatZone);
        if (policyTarget != null &&
            Vector2.Distance(
                transform.position,
                policyTarget.transform.position) <= awarenessRadius &&
            NpcMapBehaviorPolicy.CanUseMonsterTarget(
                gameObject,
                policyTarget))
        {
            return policyTarget;
        }

        SmartNpcAI[] smartNpcs =
            FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude);
        MonsterAI bestExistingTarget = null;
        float bestExistingDistance = Mathf.Infinity;

        foreach (SmartNpcAI ally in smartNpcs)
        {
            if (ally == null ||
                ally == this ||
                ally.currentMonsterTarget == null ||
                ally.currentMonsterTarget.currentHP <= 0 ||
                !NpcMapBehaviorPolicy.CanUseMonsterTarget(
                    gameObject,
                    ally.currentMonsterTarget))
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    transform.position,
                    ally.currentMonsterTarget.transform.position);
            if (distance <= awarenessRadius &&
                distance < bestExistingDistance)
            {
                bestExistingDistance = distance;
                bestExistingTarget = ally.currentMonsterTarget;
            }
        }

        if (bestExistingTarget != null)
        {
            NpcMapBehaviorPolicy.SetSharedCombatTarget(
                allowedCombatZone,
                bestExistingTarget);
            return bestExistingTarget;
        }

        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);
        MonsterAI bestTarget = null;
        float bestDistance = Mathf.Infinity;

        foreach (MonsterAI monster in monsters)
        {
            if (!ShouldSmartAutoHuntMonster(monster) ||
                monster.currentHP <= 0 ||
                !CanUseMonsterTargetByMapPolicy(
                    monster,
                    allowedCombatZone) ||
                !ShouldSharedCombatTeamFight(
                    monster,
                    allowedCombatZone))
            {
                continue;
            }

            float distance =
                Vector2.Distance(transform.position, monster.transform.position);
            if (distance <= awarenessRadius &&
                distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = monster;
            }
        }

        if (bestTarget != null)
        {
            NpcMapBehaviorPolicy.SetSharedCombatTarget(
                allowedCombatZone,
                bestTarget);
        }

        return bestTarget;
    }

    float GetMonsterAwarenessRadius(bool restrictToCombatZone)
    {
        float radius =
            Mathf.Max(
                attackRange + 3f,
                targetClearRadius * 6f,
                8f);
        if (restrictToCombatZone)
        {
            radius = Mathf.Max(radius, 10f);
        }

        return radius;
    }

    string GetRealmName()
    {
        return NpcText.Realm(realm);
    }

    public int GetAge()
    {
        NPCIdentity identity =
            GetComponent<NPCIdentity>() ??
            GetComponentInParent<NPCIdentity>(true) ??
            GetComponentInChildren<NPCIdentity>(true);

        bool hasNpcIdentityAge =
            identity != null &&
            (identity.hasBirthAbsoluteDay ||
             identity.age > 0 ||
             !string.IsNullOrWhiteSpace(identity.npcName) ||
             !string.IsNullOrWhiteSpace(identity.fatherId) ||
             !string.IsNullOrWhiteSpace(identity.motherId));

        if (hasNpcIdentityAge)
        {
            int currentAge = identity.GetCurrentAge();
            if (entityProfile != null &&
                entityProfile.identity != null)
            {
                entityProfile.identity.age = currentAge;
                entityProfile.identity.birthAbsoluteDay =
                    identity.birthAbsoluteDay;
                entityProfile.identity.hasBirthAbsoluteDay = true;
            }

            return currentAge;
        }

        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            int currentAge =
                NpcAgeUtility.GetCurrentAge(entityProfile.identity);
            if (identity != null)
            {
                identity.age = currentAge;
                identity.birthAbsoluteDay =
                    entityProfile.identity.birthAbsoluteDay;
                identity.hasBirthAbsoluteDay = true;
            }

            return currentAge;
        }

        return identity != null ? identity.GetCurrentAge() : 0;
    }

    public int GetLifespan()
    {
        return lifespan > 0
            ? lifespan
            : GetLifespanForRealm(realm);
    }

    bool ShouldDieFromOldAge()
    {
        return dieWhenLifespanEnds &&
            GetAge() > 0 &&
            GetAge() >= GetLifespan();
    }

    int GetLifespanForRealm(CultivationRealm targetRealm)
    {
        switch (targetRealm)
        {
            case CultivationRealm.QiRefining:
                return 120;
            case CultivationRealm.Foundation:
                return 220;
            case CultivationRealm.GoldenCore:
                return 500;
            case CultivationRealm.NascentSoul:
                return 1200;
            case CultivationRealm.SoulFormation:
                return 3000;
            case CultivationRealm.Tribulation:
                return 10000;
            default:
                return 80;
        }
    }

    void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        SetCurrentHealth(0);
        waitingForHeavenlyTribulation = false;
        bool preserveInDungeon = BicanhSessionManager.ShouldPreserveDungeonDeath(gameObject);

        characterStats.waitingForHeavenlyTribulation = false;

        ClearTravelTargets();
        currentMonsterTarget = null;
        ClearMonsterCombatState();
        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        hasTreasureWaitPosition = false;
        thinkTimer = 0f;
        actionTimer = 0f;
        attackTimer = 0f;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        postTeleportRecoveryUntil = 0f;
        stuckMoveTimer = 0f;
        blockedMoveTimer = 0f;
        currentAction = NpcText.Action("dead");

        if (visualAnimation != null &&
            rb != null &&
            rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            visualAnimation.SetFacingDirection(rb.linearVelocity);
        }

        UpdateCultivationEffect(false);
        UpdateVisualAnimation();

        StopNpcMovement();

        Collider2D collider2d =
            GetComponent<Collider2D>();

        if (collider2d != null)
        {
            collider2d.enabled = false;
        }

        Debug.Log(NpcText.Format(NpcText.Get("logs", "dead"), npcName));

        if (preserveInDungeon)
        {
            return;
        }

        NpcInventoryDropper.DropAll(gameObject);

        Destroy(gameObject, deathDestroyDelay);
    }
}
