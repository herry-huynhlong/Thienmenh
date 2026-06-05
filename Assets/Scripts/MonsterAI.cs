using System.Collections;
using UnityEngine;

public class MonsterAI : MonoBehaviour, IDamageable
{
    [Header("===== ENTITY GENERATION =====")]
    public bool generateFromEntityProfile = true;
    public EntityProfile entityProfile;

    [Header("===== THONG TIN =====")]
    public string monsterName = "Yeu Thu";

    [Header("===== MAU =====")]
    public int maxHP = 100;
    public int currentHP = 100;

    [Header("===== CAP BAC =====")]
    [Min(1)] public int beastLevel = 1;

    [Header("===== DAMAGE =====")]
    public int damage = 10;
    public int defense = 0;
    public int effectResistance = 0;

    [Header("===== BAN NANG YEU THU =====")]
    [Range(0, 100)] public float beastInstinct = 60f;
    [Range(0, 100)] public float aggression = 50f;
    [Range(0, 100)] public float fear = 20f;
    [Range(0, 100)] public float hunger = 40f;
    [Range(0, 100)] public float territorial = 50f;
    [Range(0, 100)] public float bloodlust = 20f;
    [Range(0, 100)] public float survivalInstinct = 50f;

    [Header("===== DI CHUYEN =====")]
    public float moveSpeed = 2f;
    public float roamRadius = 3f;
    public float waitTime = 2f;
    public bool autoConfigureRigidbody = true;
    public bool fallbackTransformMove = true;

    [Header("===== LANH DIA =====")]
    public bool guardTerritory = true;
    public float territoryRadius = 5f;
    public float returnHomeDistance = 7f;
    public LayerMask intruderLayers = ~0;
    public bool attackPlayer = true;
    public bool attackVillagers = true;
    public bool attackSmartNpcs = true;
    public bool attackOtherMonsters;

    [Header("===== RUNTIME DEBUG =====")]
    public string currentAction = "Idle";
    public Vector2 currentMoveVelocity;

    [Header("===== PERFORMANCE =====")]
    public bool usePerformanceThrottle = true;
    [Min(0.02f)] public float thinkInterval = 0.2f;
    [Min(0.05f)] public float detectInterval = 0.35f;
    [Range(0f, 0.5f)] public float performanceJitter = 0.12f;

    [Header("===== PHAT HIEN =====")]
    public float detectRange = 6f;
    public float attackRange = 1.5f;
    public float forgetTargetRange = 8f;

    [Header("===== TAN CONG =====")]
    public float attackCooldown = 2f;
    public float attackDamageDelay = 0.35f;
    public float attackEndDelay = 1f;
    public bool directDamageOnAttack = true;

    [Header("===== HOI SINH =====")]
    public bool respawnAfterDeath = true;
    [Min(1)] public int respawnAfterDays = 1;
    public float corpseVisibleSeconds = 2f;

    [Header("===== ROI VAT PHAM =====")]
    public bool dropLootOnDeath = true;
    [Range(0f, 1f)] public float lootDropChance = 1f;
    public int lootAmount = 1;
    public StatItemData[] lootByBeastLevel;
    public bool allowPlayerLootPickup = true;
    public bool allowNpcLootPickup = true;
    public float lootDropOffsetRadius = 0.2f;

    [Header("===== FIREBALL =====")]
    public GameObject fireballPrefab;
    public Transform firePoint;

    [Header("===== ANIMATION =====")]
    public bool useAnimation = true;

    Animator animator;
    MonsterDirectionalAnimator directionalAnimator;
    Rigidbody2D rb;
    Transform currentTarget;
    IDamageable currentTargetDamageable;
    Vector2 startPosition;
    Vector2 targetPosition;
    bool hasTarget = false;
    float waitTimer;
    float attackTimer;
    bool isAttacking;
    bool isDead;
    bool isRespawning;
    Renderer[] cachedRenderers;
    Collider2D[] cachedColliders;
    Vector2 desiredVelocity;
    float nextThinkTime;
    float nextDetectTime;

    public bool IsDead => isDead;
    public Transform DamageTransform => transform;

    void Start()
    {
        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
        }

        currentHP = maxHP;
        animator = GetComponent<Animator>();
        directionalAnimator = GetComponent<MonsterDirectionalAnimator>();
        rb = GetComponent<Rigidbody2D>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider2D>(true);
        ConfigureRigidbody();
        startPosition = transform.position;
        waitTimer = waitTime;
        nextThinkTime = Time.time + Random.Range(0f, GetThinkDelay());
        nextDetectTime = Time.time + Random.Range(0f, GetDetectDelay());

        if (territoryRadius <= 0f)
        {
            territoryRadius = Mathf.Max(roamRadius, detectRange);
        }

        forgetTargetRange = Mathf.Max(forgetTargetRange, detectRange + 1f);
    }

    void ConfigureRigidbody()
    {
        if (!autoConfigureRigidbody || rb == null)
        {
            return;
        }

        if (rb.bodyType == RigidbodyType2D.Static)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if ((rb.constraints & RigidbodyConstraints2D.FreezePositionX) != 0 ||
            (rb.constraints & RigidbodyConstraints2D.FreezePositionY) != 0)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void ApplyEntityProfile()
    {
        entityProfile = EntityGenerator.EnsureProfile(gameObject, EntityKind.Beast);
        if (entityProfile == null)
        {
            return;
        }

        if (entityProfile.kind != EntityKind.Beast)
        {
            EntityGenerator.FillProfile(entityProfile, EntityKind.Beast);
            entityProfile.lockGeneratedValues = true;
        }

        monsterName = entityProfile.identity.entityName;
        maxHP = entityProfile.stats.maxHP;
        currentHP = entityProfile.stats.currentHP;
        damage = entityProfile.stats.attack;
        defense = entityProfile.stats.defense;
        effectResistance = entityProfile.stats.effectResistance;
        moveSpeed = entityProfile.stats.moveSpeed;
        beastInstinct = Mathf.Clamp(45f + entityProfile.talent.combatMultiplier * 15f, 0f, 100f);
        aggression = Mathf.Clamp(entityProfile.personality.bravery + entityProfile.personality.hotTemper * 0.5f, 0f, 100f);
        fear = Mathf.Clamp(100f - entityProfile.personality.bravery, 0f, 100f);
        hunger = entityProfile.needs.hunger;
        territorial = Random.Range(35f, 95f);
        bloodlust = Mathf.Clamp(entityProfile.personality.hotTemper, 0f, 100f);
        survivalInstinct = Random.Range(35f, 100f);
    }

    void Update()
    {
        if (isDead || isRespawning)
        {
            return;
        }

        UpdateBeastNeeds();
        attackTimer -= Time.deltaTime;

        if (HasValidTarget())
        {
            float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
            float distanceFromHome = Vector2.Distance(startPosition, transform.position);

            if (distanceToTarget > forgetTargetRange ||
                (guardTerritory && distanceFromHome > returnHomeDistance && distanceToTarget > attackRange))
            {
                ClearCurrentTarget();
                ReturnToTerritory();
                return;
            }

            if (ShouldFleeFrom(currentTarget))
            {
                FleeFrom(currentTarget);
                return;
            }

            if (ShouldAttackTarget(currentTarget))
            {
                FollowTarget(distanceToTarget);
                return;
            }
        }

        if (usePerformanceThrottle && Time.time < nextThinkTime)
        {
            return;
        }

        nextThinkTime = Time.time + GetThinkDelay();
        NpcPerformanceOverlay.RecordMonsterThinkUpdate();

        if (!usePerformanceThrottle || Time.time >= nextDetectTime)
        {
            nextDetectTime = Time.time + GetDetectDelay();
            AcquireIntruderTarget();
            if (HasValidTarget())
            {
                return;
            }
        }

        if (guardTerritory && Vector2.Distance(transform.position, startPosition) > returnHomeDistance)
        {
            ReturnToTerritory();
            return;
        }

        Patrol();
    }

    void FixedUpdate()
    {
        NpcPerformanceOverlay.RecordMonsterFixedUpdate();

        if (rb == null)
        {
            return;
        }

        if (isDead || isRespawning || isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            currentMoveVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = desiredVelocity;
        currentMoveVelocity = rb.linearVelocity;

        if (fallbackTransformMove &&
            desiredVelocity.sqrMagnitude > 0.0001f &&
            rb.bodyType != RigidbodyType2D.Dynamic)
        {
            transform.position += (Vector3)(desiredVelocity * Time.fixedDeltaTime);
        }
    }

    float GetThinkDelay()
    {
        if (!usePerformanceThrottle)
        {
            return 0f;
        }

        return Mathf.Max(0.02f, thinkInterval + Random.Range(0f, performanceJitter));
    }

    float GetDetectDelay()
    {
        if (!usePerformanceThrottle)
        {
            return 0f;
        }

        return Mathf.Max(0.05f, detectInterval + Random.Range(0f, performanceJitter));
    }

    void UpdateBeastNeeds()
    {
        hunger = Mathf.Clamp(hunger + Time.deltaTime * 0.4f, 0f, 100f);

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null)
        {
            aggression = Mathf.Clamp(aggression + weather.BeastAggressionBonus() * Time.deltaTime * 0.01f, 0f, 100f);
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null && timeSystem.IsDangerousNight())
        {
            bloodlust = Mathf.Clamp(bloodlust + Time.deltaTime * 0.2f, 0f, 100f);
        }

        if (entityProfile != null)
        {
            entityProfile.needs.hunger = hunger;
            entityProfile.emotion.fear = fear;
        }
    }

    void AcquireIntruderTarget()
    {
        ClearCurrentTarget();
        NpcPerformanceOverlay.RecordMonsterDetectScan();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRange, intruderLayers);
        Transform bestTarget = null;
        IDamageable bestDamageable = null;
        float bestScore = float.PositiveInfinity;

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead || damageable.DamageTransform == null)
            {
                continue;
            }

            Transform candidate = damageable.DamageTransform;
            if (!CanAttackIntruder(candidate.gameObject))
            {
                continue;
            }

            float distanceFromHome = Vector2.Distance(startPosition, candidate.position);
            if (guardTerritory && distanceFromHome > territoryRadius)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, candidate.position);
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
        }
    }

    bool CanAttackIntruder(GameObject candidate)
    {
        if (candidate == null || candidate == gameObject)
        {
            return false;
        }

        if (candidate.GetComponentInParent<PlayerHealth>() != null || candidate.CompareTag("Player"))
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

        if (candidate.CompareTag("Player") || candidate.GetComponentInParent<PlayerHealth>() != null)
        {
            return 1f;
        }

        return 0f;
    }

    bool HasValidTarget()
    {
        if (currentTarget == null || currentTargetDamageable == null)
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
    }

    bool ShouldAttackTarget(Transform target)
    {
        float reason = hunger * 0.45f +
            aggression * 0.3f +
            bloodlust * 0.2f +
            territorial * 0.15f;

        if (guardTerritory && target != null &&
            Vector2.Distance(startPosition, target.position) <= territoryRadius)
        {
            reason += territorial * 0.35f;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null && timeSystem.IsDangerousNight())
        {
            reason += 15f;
        }

        return reason >= 45f;
    }

    bool ShouldFleeFrom(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        int targetPower = EstimatePower(target.gameObject, damageable);
        int selfPower = Mathf.Max(1, GetRealmPower());
        bool clearlyWeaker = targetPower > selfPower * 2;
        bool almostDead = currentHP < maxHP * 0.25f;

        return (clearlyWeaker && fear + survivalInstinct > 80f) ||
            (almostDead && survivalInstinct > 45f);
    }

    int EstimatePower(GameObject target, IDamageable damageable)
    {
        CharacterStats stats = target.GetComponentInParent<CharacterStats>();
        if (stats != null)
        {
            return stats.attack + stats.defense + stats.finalHP / 10;
        }

        VillagerAI villager = target.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.attack + villager.defense + villager.maxHP / 10;
        }

        SmartNpcAI smartNpc = target.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.attack + smartNpc.defense + smartNpc.maxHP / 10;
        }

        MonsterAI monster = target.GetComponentInParent<MonsterAI>();
        if (monster != null)
        {
            return monster.GetRealmPower();
        }

        return damageable != null ? 50 : 1;
    }

    void FleeFrom(Transform threat)
    {
        if (threat == null)
        {
            return;
        }

        Vector2 direction = ((Vector2)transform.position - (Vector2)threat.position).normalized;
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
        if (isAttacking || !HasValidTarget())
        {
            return;
        }

        Vector2 direction = currentTarget.position - transform.position;
        FaceDirection(direction);

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

    void Attack()
    {
        attackTimer = attackCooldown;
        isAttacking = true;

        if (useAnimation)
        {
            if (directionalAnimator != null)
            {
                Vector2 attackDirection = currentTarget != null
                    ? (Vector2)(currentTarget.position - transform.position)
                    : Vector2.zero;
                directionalAnimator.PlayAttack(attackDirection);
            }
            else if (animator != null)
            {
                animator.SetTrigger("attack");
            }
        }

        if (directDamageOnAttack)
        {
            Invoke(nameof(ApplyAttackDamage), Mathf.Max(0f, attackDamageDelay));
        }

        Invoke(nameof(EndAttack), Mathf.Max(attackDamageDelay, attackEndDelay));
    }

    void ApplyAttackDamage()
    {
        if (!isAttacking || !HasValidTarget())
        {
            return;
        }

        float distance = Vector2.Distance(transform.position, currentTarget.position);
        if (distance > attackRange + 0.25f)
        {
            return;
        }

        currentTargetDamageable.TakeDamage(damage);
    }

    void EndAttack()
    {
        isAttacking = false;
    }

    public void ShootFireball()
    {
        if (fireballPrefab == null || firePoint == null || !HasValidTarget())
        {
            return;
        }

        GameObject fireball = Instantiate(fireballPrefab, firePoint.position, Quaternion.identity);
        Vector2 direction = currentTarget.position - firePoint.position;
        Fireball fb = fireball.GetComponent<Fireball>();

        if (fb != null)
        {
            fb.SetOwner(gameObject);
            fb.damage = damage;
            fb.SetDirection(direction);
        }
    }

    void ChooseNewPoint()
    {
        Vector2 randomPoint = Random.insideUnitCircle * roamRadius;
        targetPosition = startPosition + randomPoint;
        hasTarget = true;
        currentAction = "Chon diem tuan tra";
    }

    void FaceDirection(Vector2 direction)
    {
        if (directionalAnimator != null)
        {
            directionalAnimator.SetMoveDirection(direction);
            return;
        }

        if (direction.x < -0.01f)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (direction.x > 0.01f)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
    }

    void SetMovingAnimation(bool isMoving)
    {
        if (directionalAnimator != null)
        {
            directionalAnimator.SetMoving(isMoving);
            return;
        }

        if (animator != null && useAnimation)
        {
            animator.SetBool("isMoving", isMoving);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead || isRespawning)
        {
            return;
        }

        int finalDamage = Mathf.Max(1, damageAmount - defense);
        currentHP -= finalDamage;

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = Mathf.Max(0, currentHP);
            entityProfile.Remember("attacker", "was_attacked", -finalDamage);
        }

        if (animator != null && useAnimation && directionalAnimator == null)
        {
            animator.SetTrigger("hurt");
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        desiredVelocity = Vector2.zero;
        ClearCurrentTarget();

        CancelInvoke(nameof(ApplyAttackDamage));
        CancelInvoke(nameof(EndAttack));

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (useAnimation)
        {
            if (directionalAnimator != null)
            {
                directionalAnimator.PlayDeath();
            }
            else if (animator != null)
            {
                animator.SetBool("isDead", true);
            }
        }

        DropDeathLoot();

        if (respawnAfterDeath)
        {
            StartCoroutine(RespawnRoutine());
        }
        else
        {
            Destroy(gameObject, Mathf.Max(0f, corpseVisibleSeconds));
        }
    }

    IEnumerator RespawnRoutine()
    {
        isRespawning = true;
        yield return new WaitForSeconds(Mathf.Max(0f, corpseVisibleSeconds));

        SetMonsterVisible(false);
        SetMonsterColliders(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        float targetWorldHour = GetAbsoluteWorldHour() + Mathf.Max(1, respawnAfterDays) * 24f;
        while (GetAbsoluteWorldHour() < targetWorldHour)
        {
            yield return null;
        }

        RespawnAtHome();
    }

    void RespawnAtHome()
    {
        transform.position = startPosition;
        currentHP = maxHP;
        isDead = false;
        isAttacking = false;
        isRespawning = false;
        desiredVelocity = Vector2.zero;
        waitTimer = waitTime;
        nextThinkTime = Time.time + Random.Range(0f, GetThinkDelay());
        nextDetectTime = Time.time + Random.Range(0f, GetDetectDelay());
        hasTarget = false;
        ClearCurrentTarget();
        currentAction = "Hoi sinh trong lanh dia";

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = currentHP;
        }

        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        SetMonsterVisible(true);
        SetMonsterColliders(true);
        if (directionalAnimator != null)
        {
            directionalAnimator.PlayRespawn();
        }
        SetMovingAnimation(false);
    }

    float GetAbsoluteWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return Time.time / 3600f;
        }

        int year = Mathf.Max(1, timeSystem.currentYear);
        int month = Mathf.Max(1, timeSystem.currentMonth);
        int day = Mathf.Max(1, timeSystem.currentDay);
        int absoluteDay = (year - 1) * 360 + (month - 1) * 30 + (day - 1);
        return absoluteDay * 24f + Mathf.Max(0f, timeSystem.currentHour);
    }

    void SetMonsterVisible(bool visible)
    {
        if (cachedRenderers == null)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        foreach (Renderer targetRenderer in cachedRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = visible;
            }
        }
    }

    void SetMonsterColliders(bool enabledValue)
    {
        if (cachedColliders == null)
        {
            cachedColliders = GetComponentsInChildren<Collider2D>(true);
        }

        foreach (Collider2D targetCollider in cachedColliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = enabledValue;
            }
        }
    }
    void DropDeathLoot()
    {
        if (!dropLootOnDeath || lootDropChance <= 0f || Random.value > lootDropChance)
        {
            return;
        }

        StatItemData loot = GetDeathLoot();
        if (loot == null)
        {
            return;
        }

        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, lootDropOffsetRadius);
        Vector3 dropPosition = transform.position + (Vector3)offset;
        GameObject lootObject = new GameObject(loot.itemName + " Pickup");
        lootObject.transform.position = dropPosition;

        WorldStatItemPickup pickup = lootObject.AddComponent<WorldStatItemPickup>();
        pickup.item = loot;
        pickup.amount = Mathf.Max(1, lootAmount);
        pickup.allowPlayerPickup = allowPlayerLootPickup;
        pickup.allowNpcPickup = allowNpcLootPickup;
        pickup.destroyWhenEmpty = true;

        CircleCollider2D collider = lootObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.25f;

        if (loot.icon != null)
        {
            SpriteRenderer renderer = lootObject.AddComponent<SpriteRenderer>();
            renderer.sprite = loot.icon;
            renderer.sortingOrder = 20;
        }

        GameSaveSystem.RegisterItem(loot);
    }

    public StatItemData GetDeathLoot()
    {
        if (lootByBeastLevel == null || lootByBeastLevel.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(beastLevel - 1, 0, lootByBeastLevel.Length - 1);
        return lootByBeastLevel[index];
    }
    public int GetRealmPower()
    {
        return Mathf.Max(1, damage + defense + maxHP / 10);
    }

    public void ApplyItem(StatItemData item)
    {
        ApplyItem(item, 1);
    }

    public void ApplyItem(StatItemData item, int direction)
    {
        ApplyItem(item, direction, 1f);
    }

    public void ApplyItem(StatItemData item, int direction, float powerMultiplier)
    {
        if (item == null)
        {
            return;
        }

        foreach (StatModifier modifier in item.GetAllModifiers(powerMultiplier))
        {
            ApplyModifier(modifier, direction);
        }

        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
    }

    void ApplyModifier(StatModifier modifier, int direction)
    {
        if (modifier == null)
        {
            return;
        }

        int intValue = modifier.intValue * direction;
        float floatValue = modifier.floatValue * direction;

        switch (modifier.statType)
        {
            case StatType.MaxHP:
                maxHP += intValue;
                currentHP += intValue;
                break;
            case StatType.CurrentHP:
                currentHP += intValue;
                break;
            case StatType.Damage:
            case StatType.Attack:
                damage += intValue;
                break;
            case StatType.Defense:
                defense += intValue;
                break;
            case StatType.EffectResistance:
                effectResistance += intValue;
                break;
            case StatType.MoveSpeed:
                moveSpeed += floatValue;
                break;
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? startPosition : (Vector2)transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, roamRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, territoryRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, returnHomeDistance);
    }
}