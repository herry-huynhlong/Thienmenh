using UnityEngine;

public partial class MonsterAI
{
    void FollowTreasureHuntTarget()
    {
        if (waitingOutsideTreasureLightning)
        {
            Vector2 waitDirection = treasureWaitPosition - transform.position;
            float waitDistance = waitDirection.magnitude;
            FaceDirection(waitDirection);

            if (waitDistance > Mathf.Max(0.25f, attackRange * 0.5f))
            {
                desiredVelocity = waitDirection.normalized * moveSpeed;
                currentAction = "Cho thien loi tan " +
                    (treasureHuntItem != null
                        ? ItemText.Name(treasureHuntItem)
                        : "bao vat");
                SetMovingAnimation(true);
                return;
            }

            desiredVelocity = Vector2.zero;
            currentAction = "Ran minh ngoai vung set";
            SetMovingAnimation(false);
            return;
        }

        if (treasureHuntTarget == null)
        {
            ClearTreasureHunt();
            return;
        }

        Vector2 direction = treasureHuntTarget.position - transform.position;
        float distance = direction.magnitude;
        FaceDirection(direction);

        if (distance > Mathf.Max(0.25f, attackRange * 0.5f))
        {
            desiredVelocity = direction.normalized * moveSpeed;
            currentAction = "Phat cuong tranh doat " +
                (treasureHuntItem != null
                    ? ItemText.Name(treasureHuntItem)
                    : "bao vat");
            SetMovingAnimation(true);
            return;
        }

        desiredVelocity = Vector2.zero;
        currentAction = "Canh giu bao vat";
        SetMovingAnimation(false);
    }

    public void ForceTreasureWait(
        Vector3 origin,
        float safeRadius,
        StatItemData item,
        bool lowPowerSkirmish)
    {
        if (item == null ||
            IsDead)
        {
            return;
        }

        waitingOutsideTreasureLightning = true;
        treasureHuntTarget = null;
        treasureHuntItem = item;
        ClearCurrentTarget();

        Vector2 away = transform.position - origin;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = Random.insideUnitCircle.normalized;
        }

        treasureWaitPosition =
            origin +
            (Vector3)away.normalized * Mathf.Max(0.5f, safeRadius);
        string itemName = ItemText.Name(item);
        currentAction = lowPowerSkirmish
            ? "Hon chien vong ngoai " + itemName
            : "Doi thien loi tan " + itemName;
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
        treasureHuntTarget = target;
        treasureHuntItem = item;
        ClearCurrentTarget();
        currentAction = "Phat cuong tranh doat " + ItemText.Name(item);
    }

    public void ClearTreasureHunt()
    {
        if (treasureHuntTarget == null &&
            treasureHuntItem == null)
        {
            return;
        }

        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        desiredVelocity = Vector2.zero;
        currentAction = "Binh tinh tro lai";
        SetMovingAnimation(false);
    }

    void Attack()
    {
        attackTimer = attackCooldown;
        isAttacking = true;
        attackSequence++;

        DebugFlow(
            "Combat",
            "Attack start target=" +
            (currentTarget != null ? currentTarget.name : "null") +
            " cooldown=" +
            attackCooldown.ToString("0.00") +
            " damageDelay=" +
            attackDamageDelay.ToString("0.00"));

        if (useAnimation)
        {
            if (directionalAnimator != null)
            {
                Vector2 attackDirection = currentTarget != null
                    ? (Vector2)(currentTarget.position - transform.position)
                    : Vector2.zero;
                DebugFlow(
                    "Combat",
                    "Attack animation via directionalAnimator=" +
                    directionalAnimator.GetType().Name +
                    " direction=" +
                    attackDirection.ToString("F2"));
                directionalAnimator.PlayAttack(attackDirection);
            }
            else if (animator != null)
            {
                DebugFlow(
                    "Combat",
                    "Attack animation via Animator trigger controller=" +
                    (animator.runtimeAnimatorController != null
                        ? animator.runtimeAnimatorController.name
                        : "null"));
                SetAnimatorTriggerIfExists("attack");
            }
            else
            {
                DebugFlow("Combat", "Attack animation missing animator components");
            }
        }
        else
        {
            DebugFlow("Combat", "Attack animation disabled useAnimation=false");
        }

        if (directDamageOnAttack)
        {
            Invoke(nameof(ApplyAttackDamage), Mathf.Max(0f, attackDamageDelay));
        }

        Invoke(nameof(EndAttack), Mathf.Max(attackDamageDelay, attackEndDelay));
    }

    void ApplyAttackDamage()
    {
        if (!isAttacking ||
            !HasValidTarget())
        {
            return;
        }

        float surfaceDistance =
            GetCombatSurfaceDistance(currentTarget);
        float centerDistance =
            Vector2.Distance(transform.position, currentTarget.position);
        if (surfaceDistance > attackRange + 0.25f)
        {
            DebugFlow(
                "Combat",
                "Attack damage skipped target=" +
                currentTarget.name +
                " surfaceDistance=" +
                surfaceDistance.ToString("0.00") +
                " centerDistance=" +
                centerDistance.ToString("0.00"));
            return;
        }

        if (float.IsPositiveInfinity(surfaceDistance))
        {
            DebugFlow(
                "Combat",
                "Attack damage skipped target=" +
                currentTarget.name +
                " invalidSurfaceDistance centerDistance=" +
                centerDistance.ToString("0.00"));
            return;
        }

        Transform damagedTarget = currentTarget;
        IDamageable damagedTargetDamageable = currentTargetDamageable;
        int finalDamage =
            NpcCombatTechniqueSystem.ModifyOutgoingDamage(
                gameObject,
                damagedTarget != null ? damagedTarget.gameObject : null,
                damage);

        if (damagedTarget != null)
        {
            NpcSocialEventBus.PublishHostility(
                gameObject,
                damagedTarget.gameObject,
                Mathf.Clamp(finalDamage, 1, 100),
                damagedTarget.position,
                NpcText.Dialogue("combatBeastReason"));
        }

        DebugFlow(
            "Combat",
            "Attack damage target=" +
            (damagedTarget != null ? damagedTarget.name : "null") +
            " finalDamage=" +
            finalDamage +
            " surfaceDistance=" +
            surfaceDistance.ToString("0.00") +
            " centerDistance=" +
            centerDistance.ToString("0.00"));

        SmartNpcAI smartNpc =
            damagedTargetDamageable as SmartNpcAI;
        if (smartNpc != null)
        {
            smartNpc.TakeDamage(finalDamage, gameObject);
        }
        else
        {
            damagedTargetDamageable.TakeDamage(finalDamage);
        }

        if (damagedTargetDamageable.IsDead)
        {
            DevourDefeatedNpc(damagedTarget);
            ClearCurrentTarget();
        }
    }

    void EndAttack()
    {
        isAttacking = false;
        DebugFlow("Combat", "Attack end");
    }

    public void ShootFireball()
    {
        if (fireballPrefab == null ||
            firePoint == null ||
            !HasValidTarget())
        {
            return;
        }

        GameObject fireball =
            Instantiate(fireballPrefab, firePoint.position, Quaternion.identity);
        Vector2 direction = currentTarget.position - firePoint.position;
        Fireball fb = fireball.GetComponent<Fireball>();

        if (fb != null)
        {
            fb.SetOwner(gameObject);
            fb.damage = damage;
            fb.SetDirection(direction);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        TakeDamage(damageAmount, null);
    }

    public void TakeDamage(int damageAmount, GameObject attackerObject)
    {
        if (isDead ||
            isRespawning)
        {
            return;
        }

        lastDamageSource = attackerObject;
        lastSmartNpcAttacker =
            attackerObject != null
                ? attackerObject.GetComponentInParent<SmartNpcAI>()
                : null;

        int finalDamage =
            CombatStatCalculator.CalculateFinalDamageInt(
                damageAmount,
                defense);
        currentHP = Mathf.Clamp(currentHP - finalDamage, 0, maxHP);

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = currentHP;
            entityProfile.Remember("attacker", "was_attacked", -finalDamage);
        }

        if (currentHP > 0)
        {
            NpcCombatTechniqueSystem.ReactToDamageTaken(
                gameObject,
                damageAmount);
        }

        if (animator != null &&
            useAnimation &&
            directionalAnimator == null)
        {
            SetAnimatorTriggerIfExists("hurt");
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }
}
