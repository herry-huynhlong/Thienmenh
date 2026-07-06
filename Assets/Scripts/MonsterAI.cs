using UnityEngine;

public enum HuntTargetType
{
    Any,
    Beast,
    Animal
}

public partial class MonsterAI : MonoBehaviour, IDamageable
{
    [Header("===== ENTITY GENERATION =====")]
    public bool generateFromEntityProfile = true;
    public EntityProfile entityProfile;

    [Header("===== THONG TIN =====")]
    public string monsterName = "";

    [Header("===== MAU =====")]
    public int maxHP = 100;
    public int currentHP = 100;

    [Header("===== CAP BAC =====")]
    [Min(1)] public int beastLevel = 1;
    public HuntTargetType huntTargetType = HuntTargetType.Beast;

    [Header("===== TU LUYEN YEU THU =====")]
    public bool autoStatsFromRealm = true;
    public bool syncBeastLevelFromRealm = true;
    public CultivationRealm realm = CultivationRealm.QiRefining;
    [Range(1, 9)] public int realmStage = 1;
    public long cultivationExp;
    public int baseExpToNextRealm = 100;
    public bool waitingForHeavenlyTribulation;
    public int baseMaxHP = 150;
    public int baseDamage = 15;
    public int baseDefense = 8;
    public int baseEffectResistance;
    public float baseMoveSpeed = 2f;
    [Min(0f)] public float naturalCultivationExpPerSecond = 0.35f;
    [Range(0f, 1f)] public float hungryCultivationEfficiency = 0.45f;
    [Range(0f, 1f)] public float npcDevourExpMultiplier = 0.35f;
    [Min(0)] public int minNpcDevourExp = 20;
    public bool healAfterDevouringNpc = true;

    [Header("===== DAMAGE =====")]
    public int damage = 15;
    public int defense = 8;
    public int effectResistance = 0;

    [Header("===== BAN NANG YEU THU =====")]
    [Range(0, 100)] public float beastInstinct = 60f;
    [Range(0, 100)] public float aggression = 50f;
    [Range(0, 100)] public float fear = 20f;
    [Range(0, 100)] public float hunger = 40f;
    [Range(0f, 1f)] public float retreatChanceWhenSuppressed = 0.2f;
    [Min(0.1f)] public float retreatDuration = 1.25f;
    [Min(0.1f)] public float retreatRetryDelay = 0.75f;
    [Range(0, 100)] public float territorial = 50f;
    [Range(0, 100)] public float bloodlust = 20f;
    [Range(0, 100)] public float survivalInstinct = 50f;

    [Header("===== DI CHUYEN =====")]
    public float moveSpeed = 2f;
    public float roamRadius = 3f;
    public float waitTime = 2f;
    public bool autoConfigureRigidbody = true;
    public bool fallbackTransformMove = true;
    [Min(0.2f)] public float unstuckCheckDelay = 1.1f;
    [Min(0.01f)] public float unstuckMinMoveDistance = 0.03f;
    [Min(0.5f)] public float unstuckRepathRadius = 1.5f;
    [Min(1)] public int maxPatrolRecoveriesBeforeReset = 2;

    [Header("===== CAMERA DISTANCE THROTTLE =====")]
    public bool useCameraDistanceThrottle = true;
    public float fullUpdateDistanceFromCamera = 14f;
    public float reducedFixedUpdateInterval = 0.25f;

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
    public bool debugFlowLogs;
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
    int attackSequence;
    bool isDead;
    bool isRespawning;
    Renderer[] cachedRenderers;
    Collider2D[] cachedColliders;
    Vector2 desiredVelocity;
    bool isRetreating;
    float retreatUntilTime;
    float nextRetreatRollTime;
    float nextThinkTime;
    float nextDetectTime;
    float nextReducedFixedUpdateTime;
    Transform treasureHuntTarget;
    StatItemData treasureHuntItem;
    bool waitingOutsideTreasureLightning;
    Vector3 treasureWaitPosition;
    Vector2 lastUnstuckPosition;
    float stuckMoveTimer;
    int patrolRecoveryAttempts;

    float naturalCultivationRemainder;

    static readonly string[] TrackedDebugMonsterNames =
    {
        "yeuthu",
        "cap23",
        "cap235"
    };

    public bool IsDead => isDead;
    public Transform DamageTransform => transform;
    public Transform CurrentCombatTarget => currentTarget;
    public bool IsAttackActive => isAttacking;
    public int AttackSequence => attackSequence;
    public bool UsesDirectAttackDamage => directDamageOnAttack;

    void DebugFlow(string stage, string detail)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!debugFlowLogs &&
            !IsTrackedDebugMonster())
        {
            return;
        }

        string targetName =
            currentTarget != null
                ? currentTarget.name
                : "null";
        string key =
            stage + "|" +
            detail + "|" +
            currentAction + "|" +
            targetName;

        if (lastDebugKey == key &&
            Time.time - lastDebugTime < 0.75f)
        {
            return;
        }

        lastDebugKey = key;
        lastDebugTime = Time.time;

        Debug.LogWarning(
            "[MonsterAI] " + gameObject.name +
            " stage=" + stage +
            " detail=" + detail +
            " action=" + currentAction +
            " target=" + targetName +
            " hp=" + currentHP + "/" + maxHP +
            " vel=" + currentMoveVelocity.ToString("F2"));
#endif
    }

    string lastDebugKey;
    float lastDebugTime;

    bool IsTrackedDebugMonster()
    {
        return IsTrackedDebugMonsterName(gameObject.name) ||
            IsTrackedDebugMonsterName(monsterName);
    }

    static bool IsTrackedDebugMonsterName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string normalized = NormalizeDebugMonsterName(value);
        for (int i = 0; i < TrackedDebugMonsterNames.Length; i++)
        {
            if (normalized == TrackedDebugMonsterNames[i])
            {
                return true;
            }
        }

        return false;
    }

    static string NormalizeDebugMonsterName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    void Start()
    {
        bool appliedProfile = false;
        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
            appliedProfile = entityProfile != null;
        }

        RecalculateRealmStats(!appliedProfile);
        animator = GetComponent<Animator>();
        directionalAnimator = GetComponent<MonsterDirectionalAnimator>();
        rb = GetComponent<Rigidbody2D>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider2D>(true);
        NpcCollisionRegistry.Register(this, cachedColliders);
        ConfigureRigidbody();

        if (Application.isMobilePlatform)
        {
            useCameraDistanceThrottle = true;
            fullUpdateDistanceFromCamera =
                Mathf.Min(fullUpdateDistanceFromCamera, 10f);
            reducedFixedUpdateInterval =
                Mathf.Max(reducedFixedUpdateInterval, 0.45f);
            usePerformanceThrottle = true;
            thinkInterval = Mathf.Max(thinkInterval, 0.35f);
            detectInterval = Mathf.Max(detectInterval, 0.6f);
            detectRange = Mathf.Min(detectRange, 5f);
        }

        startPosition = transform.position;
        lastUnstuckPosition = transform.position;
        waitTimer = waitTime;
        nextThinkTime = Time.time + Random.Range(0f, GetThinkDelay());
        nextDetectTime = Time.time + Random.Range(0f, GetDetectDelay());

        if (territoryRadius <= 0f)
        {
            territoryRadius = Mathf.Max(roamRadius, detectRange);
        }

        forgetTargetRange = Mathf.Max(forgetTargetRange, detectRange + 1f);
    }

    void OnDisable()
    {
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        NpcCollisionRegistry.Unregister(this);
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

    void Update()
    {
        if (isDead || isRespawning)
        {
            return;
        }

        UpdateBeastNeeds();
        attackTimer -= Time.deltaTime;

        if (treasureHuntTarget != null || waitingOutsideTreasureLightning)
        {
            FollowTreasureHuntTarget();
            return;
        }

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

            if (isRetreating)
            {
                if (!ShouldContinueRetreating(currentTarget))
                {
                    StopRetreating();
                }
                else
                {
                    FleeFrom(currentTarget);
                    return;
                }
            }
            else if (TryBeginRetreat(currentTarget))
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

        if (ShouldUseReducedFixedUpdate())
        {
            currentMoveVelocity = rb.linearVelocity;
            return;
        }

        NpcPerformanceOverlay.RecordMonsterFixedUpdate();
        UpdateMovementRecovery();

        rb.linearVelocity = desiredVelocity;
        currentMoveVelocity = rb.linearVelocity;

        if (fallbackTransformMove &&
            desiredVelocity.sqrMagnitude > 0.0001f &&
            rb.bodyType != RigidbodyType2D.Dynamic)
        {
            transform.position += (Vector3)(desiredVelocity * Time.fixedDeltaTime);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryIgnoreCombatBodyCollision(collision != null ? collision.collider : null);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryIgnoreCombatBodyCollision(collision != null ? collision.collider : null);
    }

    void TryIgnoreCombatBodyCollision(Collider2D other)
    {
        if (other == null || other.isTrigger)
        {
            return;
        }

        if (other.GetComponentInParent<VillagerAI>() == null &&
            other.GetComponentInParent<SmartNpcAI>() == null)
        {
            return;
        }

        DebugFlow(
            "Collision",
            "Ignore body collision other=" +
            other.name +
            " otherRoot=" +
            other.transform.root.name);

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider2D own = cachedColliders[i];
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

        DebugFlow(
            "Collision",
            "Ignore target collision target=" +
            target.name +
            " surfaceDistance=" +
            GetCombatSurfaceDistance(target).ToString("0.00"));

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider2D>(true);
        }

        Collider2D[] targetColliders =
            target.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider2D own = cachedColliders[i];
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

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider2D>(true);
        }

        Collider2D[] targetColliders =
            target.GetComponentsInChildren<Collider2D>(true);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider2D own = cachedColliders[i];
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

    bool ShouldUseReducedFixedUpdate()
    {
        if (!useCameraDistanceThrottle ||
            fullUpdateDistanceFromCamera <= 0f)
        {
            return false;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        float maxDistance = fullUpdateDistanceFromCamera;
        if (((Vector2)transform.position - (Vector2)camera.transform.position).sqrMagnitude <=
            maxDistance * maxDistance)
        {
            return false;
        }

        if (Time.time >= nextReducedFixedUpdateTime)
        {
            nextReducedFixedUpdateTime =
                Time.time + Mathf.Max(Time.fixedDeltaTime, reducedFixedUpdateInterval);
            return false;
        }

        return true;
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

}
