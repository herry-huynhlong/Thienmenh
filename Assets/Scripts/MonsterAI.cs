using System.Collections;
using UnityEngine;

public enum HuntTargetType
{
    Any,
    Beast,
    Animal
}

public class MonsterAI : MonoBehaviour, IDamageable
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
    public int baseMaxHP = 80;
    public int baseDamage = 8;
    public int baseDefense = 3;
    public int baseEffectResistance;
    public float baseMoveSpeed = 2f;
    [Min(0f)] public float naturalCultivationExpPerSecond = 0.35f;
    [Range(0f, 1f)] public float hungryCultivationEfficiency = 0.45f;
    [Range(0f, 1f)] public float npcDevourExpMultiplier = 0.35f;
    [Min(0)] public int minNpcDevourExp = 20;
    public bool healAfterDevouringNpc = true;

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
    float nextReducedFixedUpdateTime;
    Transform treasureHuntTarget;
    StatItemData treasureHuntItem;
    bool waitingOutsideTreasureLightning;
    Vector3 treasureWaitPosition;
    bool hasTreasureWaitPosition;

    float naturalCultivationRemainder;

    public bool IsDead => isDead;
    public Transform DamageTransform => transform;

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

        float realmPower =
            CultivationProgression.GetStatPower(
                entityProfile.stats.realm,
                entityProfile.stats.realmStage,
                EntityKind.Beast);
        baseMaxHP =
            Mathf.Max(1, Mathf.RoundToInt(entityProfile.stats.maxHP / realmPower));
        baseDamage =
            Mathf.Max(1, Mathf.RoundToInt(entityProfile.stats.attack / realmPower));
        baseDefense =
            Mathf.Max(0, Mathf.RoundToInt(entityProfile.stats.defense / realmPower));
        baseEffectResistance = Mathf.Max(0, entityProfile.stats.effectResistance);
        baseMoveSpeed = Mathf.Max(0.1f, entityProfile.stats.moveSpeed);
        maxHP = Mathf.Max(1, entityProfile.stats.maxHP);
        currentHP =
            Mathf.Clamp(
                entityProfile.stats.currentHP,
                0,
                maxHP);

        if (!autoStatsFromRealm)
        {
            realm = entityProfile.stats.realm;
            realmStage = entityProfile.stats.realmStage;
            cultivationExp = Mathf.Max(0, entityProfile.stats.cultivationExp);
        }
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

        rb.linearVelocity = desiredVelocity;
        currentMoveVelocity = rb.linearVelocity;

        if (fallbackTransformMove &&
            desiredVelocity.sqrMagnitude > 0.0001f &&
            rb.bodyType != RigidbodyType2D.Dynamic)
        {
            transform.position += (Vector3)(desiredVelocity * Time.fixedDeltaTime);
        }
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

    void UpdateBeastNeeds()
    {
        hunger = Mathf.Clamp(hunger + Time.deltaTime * 0.4f, 0f, 100f);
        AbsorbWorldSpiritualEnergy();

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
            SyncEntityProfileStats();
        }
    }

    void AbsorbWorldSpiritualEnergy()
    {
        if (naturalCultivationExpPerSecond <= 0f ||
            realm == CultivationRealm.Tribulation)
        {
            return;
        }

        float hungerRatio = Mathf.Clamp01(hunger / 100f);
        float hungerEfficiency =
            Mathf.Lerp(1f, hungryCultivationEfficiency, hungerRatio);

        float realmEfficiency =
            Mathf.Clamp01(
                CultivationProgression.GetSpiritStoneEfficiency(realm));

        naturalCultivationRemainder +=
            naturalCultivationExpPerSecond *
            hungerEfficiency *
            Mathf.Max(0.05f, realmEfficiency) *
            Time.deltaTime;

        int wholeExp =
            Mathf.FloorToInt(naturalCultivationRemainder);

        if (wholeExp <= 0)
        {
            return;
        }

        naturalCultivationRemainder -= wholeExp;
        AddCultivationExp(wholeExp);
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

        if (NpcPetCompanion.BlocksMonsterAttacks(candidate))
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
        if (target != null &&
            NpcPetCompanion.BlocksMonsterAttacks(target.gameObject))
        {
            return false;
        }

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
        int selfPower =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    CultivationProgression.GetStatPower(
                        realm,
                        realmStage,
                        EntityKind.Beast)));
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
            return Mathf.Max(
                1,
                Mathf.RoundToInt(
                    CultivationProgression.GetStatPower(
                        monster.realm,
                        monster.realmStage,
                        EntityKind.Beast)));
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
                    (treasureHuntItem != null ? ItemText.Name(treasureHuntItem) : "bao vat");
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
                (treasureHuntItem != null ? ItemText.Name(treasureHuntItem) : "bao vat");
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
        if (item == null || IsDead)
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
        hasTreasureWaitPosition = true;
        string itemName = ItemText.Name(item);
        currentAction = lowPowerSkirmish
            ? "Hon chien vong ngoai " + itemName
            : "Doi thien loi tan " + itemName;
    }
    public void ForceTreasureHunt(
        Transform target,
        StatItemData item)
    {
        if (target == null || item == null || IsDead)
        {
            return;
        }

        waitingOutsideTreasureLightning = false;
        hasTreasureWaitPosition = false;
        treasureHuntTarget = target;
        treasureHuntItem = item;
        ClearCurrentTarget();
        currentAction = "Phat cuong tranh doat " + ItemText.Name(item);
    }

    public void ClearTreasureHunt()
    {
        if (treasureHuntTarget == null && treasureHuntItem == null)
        {
            return;
        }

        waitingOutsideTreasureLightning = false;
        hasTreasureWaitPosition = false;
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
                SetAnimatorTriggerIfExists("attack");
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

        Transform damagedTarget = currentTarget;
        IDamageable damagedTargetDamageable = currentTargetDamageable;

        if (damagedTarget != null)
        {
            NpcSocialEventBus.PublishHostility(
                gameObject,
                damagedTarget.gameObject,
                Mathf.Clamp(damage, 1, 100),
                damagedTarget.position,
                NpcText.Dialogue("combatBeastReason"));
        }

        damagedTargetDamageable.TakeDamage(damage);

        if (damagedTargetDamageable.IsDead)
        {
            DevourDefeatedNpc(damagedTarget);
            ClearCurrentTarget();
        }
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
            SetAnimatorBoolIfExists("isMoving", isMoving);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead || isRespawning)
        {
            return;
        }

        int finalDamage = Mathf.Max(1, damageAmount - defense);
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

        if (animator != null && useAnimation && directionalAnimator == null)
        {
            SetAnimatorTriggerIfExists("hurt");
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public long ExpToNextRealm()
    {
        return CultivationProgression.GetExpToNextLong(
            realm,
            realmStage,
            baseExpToNextRealm);
    }

    public void AddCultivationExp(int amount)
    {
        if (amount <= 0 ||
            waitingForHeavenlyTribulation ||
            realm == CultivationRealm.Tribulation)
        {
            return;
        }

        cultivationExp += amount;

        while (!waitingForHeavenlyTribulation &&
            cultivationExp >= ExpToNextRealm() &&
            realm != CultivationRealm.Tribulation)
        {
            cultivationExp -= ExpToNextRealm();
            Breakthrough();
        }
            SyncEntityProfileStats();
    }

    public void Breakthrough()
    {
        if (waitingForHeavenlyTribulation)
        {
            return;
        }

        if (realm == CultivationRealm.Tribulation)
        {
            cultivationExp = 0;
            return;
        }

        if (realm == CultivationRealm.Mortal &&
            realmStage >= CultivationProgression.MaxStage)
        {
            realmStage = 1;
            realm = CultivationRealm.QiRefining;
            RecalculateRealmStats(true);
            currentAction = "Dot pha " + GetRealmText();
            return;
        }

        if (CultivationProgression.RequiresHeavenlyTribulation(
                realm,
                realmStage))
        {
            CultivationRealm targetRealm =
                CultivationProgression.GetNextRealm(realm);

            waitingForHeavenlyTribulation = true;
            currentAction = "Cho thien kiep";
            HeavenlyTribulationSystem.Request(
                gameObject,
                monsterName,
                targetRealm,
                () => CompleteMajorBreakthrough(targetRealm));
            return;
        }

        realmStage += 1;

        RecalculateRealmStats(true);
        currentAction = "Dot pha " + GetRealmText();
    }

    void CompleteMajorBreakthrough(CultivationRealm targetRealm)
    {
        waitingForHeavenlyTribulation = false;
        if (IsDead)
        {
            return;
        }

        realmStage = 1;
        realm = targetRealm;
        RecalculateRealmStats(true);
        currentAction = "Dot pha " + GetRealmText();
    }

    public void RecalculateRealmStats(bool fillHP)
    {
        if (!autoStatsFromRealm)
        {
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            return;
        }

        int oldMaxHP = Mathf.Max(1, maxHP);
        float hpPercent = Mathf.Clamp01((float)currentHP / oldMaxHP);
        float power =
            CultivationProgression.GetStatPower(
                realm,
                realmStage,
                EntityKind.Beast);

        maxHP = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseMaxHP) * power));
        damage = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseDamage) * power));
        defense = Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, baseDefense) * power));
        effectResistance =
            Mathf.Max(
                0,
                baseEffectResistance +
                (int)realm * 2 +
                Mathf.Max(0, realmStage - 1) / 3);
        moveSpeed =
            Mathf.Max(
                0.1f,
                baseMoveSpeed +
                Mathf.Max(0, (int)realm) * 0.12f +
                Mathf.Max(0, realmStage - 1) * 0.02f);

        if (syncBeastLevelFromRealm)
        {
            beastLevel = GetBeastLevelForRealm();
        }

        currentHP = fillHP
            ? maxHP
            : Mathf.Clamp(
                Mathf.RoundToInt(maxHP * hpPercent),
                0,
                maxHP);
            SyncEntityProfileStats();
    }

    int GetBeastLevelForRealm()
    {
        switch (realm)
        {
            case CultivationRealm.Mortal:
            case CultivationRealm.QiRefining:
                return 1;
            case CultivationRealm.Foundation:
                return 2;
            case CultivationRealm.GoldenCore:
                return 3;
            default:
                return 4;
        }
    }

    void DevourDefeatedNpc(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (target.GetComponentInParent<PlayerHealth>() != null ||
            target.GetComponentInParent<MonsterAI>() != null)
        {
            return;
        }

        int exp = GetDevourExp(target);
        if (exp <= 0)
        {
            return;
        }

        AddCultivationExp(exp);
        hunger = Mathf.Clamp(hunger - 35f, 0f, 100f);
        bloodlust = Mathf.Clamp(bloodlust + 10f, 0f, 100f);

        if (healAfterDevouringNpc)
        {
            currentHP =
                Mathf.Clamp(
                    currentHP + Mathf.Max(1, maxHP / 5),
                    0,
                    maxHP);
        }

        if (entityProfile != null)
        {
            entityProfile.Remember(
                target.name,
                "devoured_npc",
                Mathf.Clamp(exp / 100, 1, 100));
        }

        currentAction = "An thit hap thu " + exp + " tu vi";
            SyncEntityProfileStats();
    }

    int GetDevourExp(Transform target)
    {
        CharacterStats stats = target.GetComponentInParent<CharacterStats>();
        if (stats != null)
        {
            return CalculateDevourExp(
                stats.realm,
                stats.realmStage,
                stats.attack + stats.defense + stats.finalHP / 10);
        }

        VillagerAI villager = target.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return CalculateDevourExp(
                villager.realm,
                villager.realmStage,
                villager.attack + villager.defense + villager.maxHP / 10);
        }

        SmartNpcAI smartNpc = target.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return CalculateDevourExp(
                smartNpc.realm,
                smartNpc.realmStage,
                smartNpc.attack + smartNpc.defense + smartNpc.maxHP / 10);
        }

        return minNpcDevourExp;
    }

    int CalculateDevourExp(
        CultivationRealm targetRealm,
        int targetStage,
        int targetPower)
    {
        long realmExp =
            CultivationProgression.GetExpToNextLong(
                targetRealm,
                targetStage,
                baseExpToNextRealm);

        if (realmExp == long.MaxValue)
        {
            realmExp = int.MaxValue;
        }

        long value =
            Mathf.Max(0, minNpcDevourExp) +
            Mathf.Max(0, targetPower) * 3L +
            (long)(realmExp * Mathf.Clamp01(npcDevourExpMultiplier));

        long minValue = Mathf.Max(0, minNpcDevourExp);
        value = System.Math.Max(minValue, value);
        value = System.Math.Min(int.MaxValue, value);
        return (int)value;
    }

    void SyncEntityProfileStats()
    {
        if (entityProfile == null)
        {
            return;
        }

        entityProfile.stats.realm = realm;
        entityProfile.stats.realmStage =
            Mathf.Clamp(
                realmStage,
                1,
                CultivationProgression.MaxStage);
        entityProfile.stats.maxHP = maxHP;
        entityProfile.stats.currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        entityProfile.stats.attack = damage;
        entityProfile.stats.defense = defense;
        entityProfile.stats.effectResistance = effectResistance;
        entityProfile.stats.moveSpeed = moveSpeed;
        entityProfile.stats.cultivationExp =
            Mathf.Clamp(
                cultivationExp > int.MaxValue ? int.MaxValue : (int)cultivationExp,
                0,
                int.MaxValue);
    }


    void Die()
    {
        isDead = true;
        isAttacking = false;
        hasTarget = false;
        desiredVelocity = Vector2.zero;
        waitTimer = waitTime;
        nextThinkTime = Time.time + Random.Range(0f, GetThinkDelay());
        nextDetectTime = Time.time + Random.Range(0f, GetDetectDelay());
        nextReducedFixedUpdateTime = Time.time;
        ClearCurrentTarget();
        NpcSocialEventBus.PublishMonsterDefeated(this);
        bool preserveInDungeon = BicanhSessionManager.ShouldPreserveDungeonDeath(gameObject);

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
                SetAnimatorBoolIfExists("isDead", true);
            }
        }

        DropDeathLoot();

        if (preserveInDungeon)
        {
            return;
        }

        NpcInventoryDropper.DropAll(gameObject);

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

        if (direction > 0)
        {
            HeavenlyTribulationSystem.MarkPillProtectionIfEligible(
                gameObject,
                item);
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
                baseMaxHP += intValue;
                RecalculateRealmStats(false);
                currentHP += intValue;
                break;
            case StatType.CurrentHP:
                currentHP += intValue;
                break;
            case StatType.Damage:
            case StatType.Attack:
                baseDamage += intValue;
                RecalculateRealmStats(false);
                break;
            case StatType.Defense:
                baseDefense += intValue;
                RecalculateRealmStats(false);
                break;
            case StatType.EffectResistance:
                baseEffectResistance += intValue;
                RecalculateRealmStats(false);
                break;
            case StatType.MoveSpeed:
                baseMoveSpeed += floatValue;
                RecalculateRealmStats(false);
                break;
            case StatType.Cultivation:
                if (direction > 0)
                {
                    AddCultivationExp(modifier.intValue);
                }
                else
                {
                    cultivationExp =
                        System.Math.Max(
                            0L,
                            cultivationExp - modifier.intValue);
                }
                break;
            case StatType.Breakthrough:
                if (direction > 0)
                {
                    Breakthrough();
                }
                break;
        }
    }

    public string GetRealmText()
    {
        switch (realm)
        {
            case CultivationRealm.Mortal:
                return "Pham Nhan";
            case CultivationRealm.QiRefining:
                return "Luyen Khi";
            case CultivationRealm.Foundation:
                return "Truc Co";
            case CultivationRealm.GoldenCore:
                return "Kim Dan";
            case CultivationRealm.NascentSoul:
                return "Nguyen Anh";
            case CultivationRealm.SoulFormation:
                return "Hoa Than";
            case CultivationRealm.Tribulation:
                return "Do Kiep";
            default:
                return realm.ToString();
        }
    }

    bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == parameterType &&
                parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    void SetAnimatorBoolIfExists(string parameterName, bool value)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    void SetAnimatorTriggerIfExists(string parameterName)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameterName);
        }
    }

    void OnValidate()
    {
        realmStage =
            Mathf.Clamp(
                realmStage,
                1,
                CultivationProgression.MaxStage);
        baseMaxHP = Mathf.Max(1, baseMaxHP);
        baseDamage = Mathf.Max(1, baseDamage);
        baseDefense = Mathf.Max(0, baseDefense);
        baseEffectResistance = Mathf.Max(0, baseEffectResistance);
        baseMoveSpeed = Mathf.Max(0.1f, baseMoveSpeed);
        baseExpToNextRealm = Mathf.Max(1, baseExpToNextRealm);

        if (!Application.isPlaying)
        {
            RecalculateRealmStats(false);
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
