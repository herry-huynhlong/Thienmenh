using UnityEngine;

public class MonsterAI : MonoBehaviour, IDamageable
{
    [Header("===== ENTITY GENERATION =====")]
    public bool generateFromEntityProfile = true;
    public EntityProfile entityProfile;

    [Header("===== THÔNG TIN =====")]

    public string monsterName =
        "Yêu Thú";

    [Header("===== MÁU =====")]

    public int maxHP = 100;

    public int currentHP = 100;

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

    [Header("===== DI CHUYỂN =====")]

    public float moveSpeed = 2f;

    public float roamRadius = 3f;

    public float waitTime = 2f;

    public bool autoConfigureRigidbody = true;

    public bool fallbackTransformMove = true;

    [Header("===== RUNTIME DEBUG =====")]

    public string currentAction = "Idle";

    public Vector2 currentMoveVelocity;

    [Header("===== PLAYER =====")]

    public float detectRange = 6f;

    public float attackRange = 1.5f;

    Transform player;

    [Header("===== TẤN CÔNG =====")]

    public float attackCooldown = 2f;

    float attackTimer;

    bool isAttacking = false;

    [Header("===== FIREBALL =====")]

    public GameObject fireballPrefab;

    public Transform firePoint;

    [Header("===== ANIMATION =====")]

    public bool useAnimation = true;

    Animator animator;

    Rigidbody2D rb;

    Vector2 startPosition;

    Vector2 targetPosition;

    bool hasTarget = false;

    float waitTimer;

    bool isDead = false;

    Vector2 desiredVelocity;

    public bool IsDead => isDead;

    public Transform DamageTransform => transform;

    void Start()
    {
        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
        }

        currentHP =
            maxHP;

        animator =
            GetComponent<Animator>();

        rb =
            GetComponent<Rigidbody2D>();

        ConfigureRigidbody();

        startPosition =
            transform.position;

        waitTimer =
            waitTime;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player");

        if (playerObject != null)
        {
            player =
                playerObject.transform;
        }
    }

    void ConfigureRigidbody()
    {
        if (!autoConfigureRigidbody ||
            rb == null)
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
        entityProfile =
            EntityGenerator.EnsureProfile(
                gameObject,
                EntityKind.Beast);

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
        if (isDead)
        {
            return;
        }

        UpdateBeastNeeds();

        attackTimer -= Time.deltaTime;

        if (player != null)
        {
            float distanceToPlayer =
                Vector2.Distance(
                    transform.position,
                    player.position);

            if (distanceToPlayer <= detectRange)
            {
                if (ShouldFleeFrom(player))
                {
                    FleeFrom(player);
                    return;
                }

                if (!ShouldAttackTarget(player))
                {
                    Patrol();
                    return;
                }

                FollowPlayer(
                    distanceToPlayer);

                return;
            }
        }

        Patrol();
    }

    void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        if (isDead ||
            isAttacking)
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
            transform.position +=
                (Vector3)(desiredVelocity * Time.fixedDeltaTime);
        }
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

    bool ShouldAttackTarget(Transform target)
    {
        float reason =
            hunger * 0.45f +
            aggression * 0.3f +
            bloodlust * 0.2f +
            territorial * 0.15f;

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null && timeSystem.IsDangerousNight())
        {
            reason += 15f;
        }

        return reason >= 45f;
    }

    bool ShouldFleeFrom(Transform target)
    {
        IDamageable damageable =
            target.GetComponentInParent<IDamageable>();

        int targetPower = EstimatePower(target.gameObject, damageable);
        int selfPower = Mathf.Max(1, damage + defense + maxHP / 10);
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

        MonsterAI monster = target.GetComponentInParent<MonsterAI>();
        if (monster != null)
        {
            return monster.damage + monster.defense + monster.maxHP / 10;
        }

        return damageable != null ? 50 : 1;
    }

    void FleeFrom(Transform threat)
    {
        if (rb == null || threat == null)
        {
            return;
        }

        Vector2 direction =
            ((Vector2)transform.position - (Vector2)threat.position).normalized;

        desiredVelocity =
            direction *
            moveSpeed *
            1.25f;
        currentAction = "Flee";

        if (animator != null && useAnimation)
        {
            animator.SetBool("isMoving", true);
        }
    }

    void Patrol()
    {
        if (!hasTarget)
        {
            desiredVelocity = Vector2.zero;
            currentAction = "Waiting";

            waitTimer -= Time.deltaTime;

            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    false);
            }

            if (waitTimer <= 0)
            {
                ChooseNewPoint();
            }

            return;
        }

        Vector2 direction =
            targetPosition -
            (Vector2)transform.position;

        float distance =
            direction.magnitude;

        if (distance < 0.1f)
        {
            hasTarget = false;

            waitTimer =
                waitTime;

            desiredVelocity = Vector2.zero;
            currentAction = "Arrived";

            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    false);
            }

            return;
        }

        direction =
            direction.normalized;

        desiredVelocity =
            direction *
            moveSpeed;
        currentAction = "Patrol";

        if (animator != null &&
            useAnimation)
        {
            animator.SetBool(
                "isMoving",
                true);
        }

        FaceDirection(direction);
    }

    void FollowPlayer(float distance)
    {
        if (isAttacking)
        {
            return;
        }

        Vector2 direction =
            player.position -
            transform.position;

        FaceDirection(direction);

        if (distance > attackRange)
        {
            desiredVelocity =
                direction.normalized *
                moveSpeed;
            currentAction = "Chasing Player";

            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    true);
            }
        }
        else
        {
            desiredVelocity = Vector2.zero;
            currentAction = "Attack Range";

            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    false);
            }

            if (attackTimer <= 0)
            {
                Attack();
            }
        }
    }

    void Attack()
    {
        attackTimer =
            attackCooldown;

        isAttacking = true;

        if (animator != null &&
            useAnimation)
        {
            animator.SetTrigger(
                "attack");
        }

        Invoke(
            nameof(EndAttack),
            1f);
    }

    void EndAttack()
    {
        isAttacking = false;
    }

    public void ShootFireball()
    {
        if (fireballPrefab == null)
        {
            return;
        }

        if (firePoint == null)
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        GameObject fireball =
            Instantiate(
                fireballPrefab,
                firePoint.position,
                Quaternion.identity);

        Vector2 direction =
            player.position -
            firePoint.position;

        Fireball fb =
            fireball.GetComponent<Fireball>();

        if (fb != null)
        {
            fb.SetOwner(gameObject);
            fb.damage = damage;

            fb.SetDirection(
                direction);
        }
    }

    void ChooseNewPoint()
    {
        Vector2 randomPoint =
            Random.insideUnitCircle *
            roamRadius;

        targetPosition =
            startPosition +
            randomPoint;

        hasTarget = true;
        currentAction = "New Patrol Target";
    }

    void FaceDirection(Vector2 direction)
    {
        if (direction.x < 0)
        {
            transform.localScale =
                new Vector3(-1, 1, 1);
        }
        else if (direction.x > 0)
        {
            transform.localScale =
                new Vector3(1, 1, 1);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead)
        {
            return;
        }

        int finalDamage =
            damageAmount - defense;

        if (finalDamage < 1)
        {
            finalDamage = 1;
        }

        currentHP -= finalDamage;

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = Mathf.Max(0, currentHP);
            entityProfile.Remember("attacker", "was_attacked", -finalDamage);
        }

        if (animator != null &&
            useAnimation)
        {
            animator.SetTrigger(
                "hurt");
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

        if (rb != null)
        {
            rb.linearVelocity =
                Vector2.zero;
        }

        if (animator != null &&
            useAnimation)
        {
            animator.SetTrigger(
                "die");
        }

        Destroy(gameObject, 2f);
    }

    public int GetRealmPower()
    {
        return maxHP;
    }

    public void ApplyItem(StatItemData item)
    {
        ApplyItem(item, 1);
    }

    public void ApplyItem(StatItemData item, int direction)
    {
        ApplyItem(item, direction, 1f);
    }

    public void ApplyItem(
        StatItemData item,
        int direction,
        float powerMultiplier)
    {
        if (item == null)
        {
            return;
        }

        foreach (StatModifier modifier in item.GetAllModifiers(powerMultiplier))
        {
            ApplyModifier(modifier, direction);
        }

        currentHP =
            Mathf.Clamp(currentHP, 0, maxHP);
    }

    void ApplyModifier(StatModifier modifier, int direction)
    {
        if (modifier == null)
        {
            return;
        }

        int intValue =
            modifier.intValue * direction;

        float floatValue =
            modifier.floatValue * direction;

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
        Gizmos.color =
            Color.red;

        Gizmos.DrawWireSphere(
            Application.isPlaying
            ? startPosition
            : (Vector2)transform.position,
            roamRadius);

        Gizmos.color =
            Color.white;

        Gizmos.DrawWireSphere(
            transform.position,
            detectRange);
    }
}
