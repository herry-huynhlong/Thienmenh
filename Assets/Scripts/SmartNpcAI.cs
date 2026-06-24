using UnityEngine;

public enum CultivationRealm
{
    Mortal,
    QiRefining,
    Foundation,
    GoldenCore,
    NascentSoul,
    SoulFormation,
    Tribulation
}

public enum PhysiqueType
{
    MortalBody,
    FiveElementBody,
    ChaosBody
}

[RequireComponent(typeof(NpcScheduleController))]
public class SmartNpcAI : MonoBehaviour, IDamageable
{
    [Header("Entity Generation")]
    public bool generateFromEntityProfile = true;
    public EntityProfile entityProfile;

    [Header("Thong tin Tu si")]
    public string npcName = "Tu sĩ";

    [Header("Bat / Tat chuc nang")]
    public bool canLive = true;
    public bool canCultivate = true;
    public bool canFight = true;
    public bool canTrade = true;
    public bool canGather = true;
    public bool canSellGoods = true;
    public bool canMakeFriends = true;
    public bool canKillOthers = true;
    public bool canCompeteResource = true;
    public bool canCreateSect = true;
    public bool autonomousActivitiesEnabled = false;

    [Header("Canh gioi")]
    public CultivationRealm realm = CultivationRealm.Mortal;

    [Range(1, 9)]
    public int realmStage = 1;

    [Header("Thien phu")]
    [Range(1, 100)]
    public int comprehension = 10;

    public PhysiqueType physique = PhysiqueType.MortalBody;

    [Header("Chi so")]
    public int maxHP = 100;

    public int currentHP = 100;

    public int attack = 10;

    public int defense = 5;

    public int baseMaxHP = 100;
    public int baseAttack = 10;
    public int baseDefense = 5;

    public int effectResistance = 0;
    public CharacterStats characterStats;
    public int lifespan = 80;
    public bool dieWhenLifespanEnds = true;

    [Header("Tu luyen")]
    public long cultivation = 0;

    public long breakthroughNeed = 100;

    public bool readyForHeavenlyTribulation = false;
    public bool waitingForHeavenlyTribulation;

    [Header("Tai san")]
    [InspectorName("Linh Thach")]
    public int money = 100;

    public int spiritStone = 0;

    public int pill = 0;

    [Header("Tinh cach")]
    [Range(0, 100)]
    public int bravery = 50;

    [Range(0, 100)]
    public int greed = 50;

    [Range(0, 100)]
    public int kindness = 50;

    [Header("Nhu cau song")]
    [Range(0, 100)]
    public float hunger = 0;

    [Range(0, 100)]
    public float fatigue = 0;

    [Header("Di chuyen")]
    public float moveSpeed = 2f;
    public bool useKinematicNpcMovement = true;
    public LayerMask crowdLayers = ~0;
    public LayerMask obstacleLayers = ~0;
    public float separationRadius = 0.55f;
    public float separationStrength = 1.5f;
    public float crowdLookAheadDistance = 0.7f;
    public float crowdDetourDistance = 0.6f;
    public float crowdYieldDuration = 0.22f;
    public float unstuckCheckDelay = 1.1f;
    public float unstuckMinMoveDistance = 0.03f;
    public float unstuckOffsetRadius = 0.9f;
    public float escapeTargetReachDistance = 0.18f;
    public float obstacleCheckDistance = 0.35f;
    public float obstacleDetourLookAhead = 0.65f;
    public float targetClearRadius = 0.25f;
    public float navigationClearancePadding = 0.16f;
    public float blockedTargetRetryDelay = 0.8f;
    public bool useObstacleAvoidance = true;
    public bool ignoreNpcBodyCollisions = true;

    public Transform currentTarget;
    Transform treasureHuntTarget;
    StatItemData treasureHuntItem;
    bool waitingOutsideTreasureLightning;
    bool treasureWaitLowPowerSkirmish;
    Vector3 treasureWaitPosition;
    bool hasTreasureWaitPosition;

    private Rigidbody2D rb;
    NPCVisualAnimation visualAnimation;
    Collider2D[] selfColliders;
    Vector3 lastUnstuckPosition;
    Vector3 escapeTarget;
    Vector3 obstacleAvoidTarget;
    float obstacleAvoidUntil;
    float stuckMoveTimer;
    float blockedMoveTimer;
    bool hasEscapeTarget;
    bool hasObstacleAvoidTarget;

    [Header("Chien dau")]
    public float attackRange = 1.5f;

    public float attackCooldown = 1f;

    public float deathDestroyDelay = 2f;

    private float attackTimer = 0;

    bool isDead;

    private MonsterAI currentMonsterTarget;
    float movementPausedUntil;
    float crowdYieldUntil;
    float postTeleportRecoveryUntil;

    [Header("Skill")]
    public GameObject fireballPrefab;

    public Transform firePoint;

    [Header("Khoang cach hoat dong")]
    public float maxRoamDistance = 10f;
    public float idleWanderRadius = 3.5f;
    public float idleWanderMinDistance = 1.2f;
    public float idleWanderArriveDistance = 0.45f;
    public int idleWanderPickAttempts = 12;
    public bool refreshGeneratedProfileOnStart = true;

    [Header("Daily Routine")]
    public bool dailyRoutineEnabled = true;
    [Range(0f, 24f)] public float dailyCultivationMinHours = 4f;
    [Range(0f, 24f)] public float dailyCultivationMaxHours = 8f;
    [Range(0f, 24f)] public float earliestCultivationHour = 5f;
    [Range(0f, 24f)] public float latestCultivationStartHour = 20f;
    public float cultivationSessionMinGameHours = 1f;
    public float cultivationSessionMaxGameHours = 2f;
    public float tradeSessionMinGameHours = 0.5f;
    public float tradeSessionMaxGameHours = 1.5f;
    public float socialSessionMinGameHours = 0.5f;
    public float socialSessionMaxGameHours = 1.5f;
    public bool dailyTaskVisitEnabled = true;
    [Range(0f, 1f)] public float dailyTaskVisitChance = 1f;
    public bool staggerDailyTaskVisits = true;
    [Range(0f, 4f)] public float dailyTaskVisitStaggerHours = 2f;

    private Vector3 spawnPosition;
    private Vector3 wanderTarget;
    private bool hasWanderTarget;
    private Vector3 cultivationTarget;
    private bool hasCultivationTarget;
    private GameObject cultivationEffectInstance;
    NpcResourceGatherer resourceGatherer;
    string currentScheduleSlotKey;
    NpcTradeAgent tradeAgent;
    NpcForgeAgent currentForgeTradeTarget;
    StatItemData currentForgeTradeItem;

    [Header("Dia diem")]
    public Transform homePoint;
    public Transform cultivationPoint;

    public Transform tavernPoint;

    public Transform forestPoint;

    public Transform farmPoint;

    [Header("Trang thai hien tai")]
    public string currentAction = "";

    [Header("Hieu ung tu luyen")]
    public GameObject cultivationEffectPrefab;

    private float thinkTimer = 0;

    public float thinkDelay = 2f;
    float actionTimer;
    int routinePlanDay = int.MinValue;
    float routineCultivationStartHour;
    float routineCultivationEndHour;
    float taskVisitAnchorHour = -1f;
    float taskVisitDelayHours;
    int lastTaskProviderVisitDay = int.MinValue;
    Vector3 homeReturnTarget;
    bool hasHomeReturnTarget;
    bool reportedVillagerBrainConflict;

    public bool IsDead =>
        isDead ||
        (characterStats != null ?
        characterStats.IsDead :
        currentHP <= 0);

    public Transform DamageTransform => transform;

    void Awake()
    {
        EnforceVillagerPrimaryBrain();
    }

    public void SetActionImmediate(string action)
    {
        SetActionImmediate(action, 0f);
    }

    public void SetActionImmediate(string action, float durationSeconds)
    {
        if (string.IsNullOrEmpty(action))
        {
            return;
        }

        actionTimer = Mathf.Max(actionTimer, Mathf.Max(0f, durationSeconds));
        currentAction = action;
    }

    void OnEnable()
    {
        EnforceVillagerPrimaryBrain();
    }

    void EnforceVillagerPrimaryBrain()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager == null || !villager.enabled || !enabled)
        {
            return;
        }

        if (reportedVillagerBrainConflict)
        {
            enabled = false;
            return;
        }

        reportedVillagerBrainConflict = true;
        Debug.LogWarning(
            "[NPC] " + gameObject.name +
            " has both SmartNpcAI and VillagerAI. SmartNpcAI will stay passive to avoid conflicting NPC logic.");
        enabled = false;
    }

    void Start()
    {
        ignoreNpcBodyCollisions = true;
        currentAction = "";
        visualAnimation = NPCVisualAnimation.EnsureOn(gameObject);
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        inventory.UsePrivateNpcRuntimeItems(false);
        ConfigureAutonomousWorkSystems();

        rb = GetComponent<Rigidbody2D>();
        if (rb != null && useKinematicNpcMovement)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        selfColliders = GetComponentsInChildren<Collider2D>();
        NpcCollisionRegistry.Register(this, selfColliders);

        spawnPosition = transform.position;
        lastUnstuckPosition = transform.position;
        InitializeTaskVisitStagger();

        characterStats = GetComponent<CharacterStats>();

        bool appliedProfile = false;
        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
            if (refreshGeneratedProfileOnStart &&
                entityProfile != null)
            {
                entityProfile.kind = EntityKind.Cultivator;
                entityProfile.ReloadGeneratedProfile();
                ApplyEntityProfile();
            }
            appliedProfile = entityProfile != null;
        }

        if (characterStats != null)
        {
            characterStats.generatedEntityKind = EntityKind.Cultivator;
            characterStats.generateFromEntityProfile = true;
            characterStats.entityProfile = entityProfile;
            characterStats.ApplyEntityProfile();
            SyncFromCharacterStats();
        }
        else
        {
            ApplyRealmPower(!appliedProfile);
        }

        EnsureScheduleController();
        SyncCultivationEffect();
        ResolveInitialObstacleOverlap();
    }

    void EnsureScheduleController()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();

        if (schedule == null)
        {
            schedule = gameObject.AddComponent<NpcScheduleController>();
        }

        schedule.lifePath = NpcLifePath.Cultivator;
        schedule.canCultivate = canCultivate;

        if (schedule.autoBuildDefaultSchedule)
        {
            schedule.RebuildDefaultSchedule();
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (cultivationEffectPrefab != null)
        {
            return;
        }

        cultivationEffectPrefab =
            UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Effects/CultivationEffect.prefab");
    }
#endif

    void ApplyEntityProfile()
    {
        entityProfile =
            EntityGenerator.EnsureProfile(
                gameObject,
                EntityKind.Cultivator);

        if (entityProfile == null)
        {
            return;
        }

        if (entityProfile.kind != EntityKind.Cultivator)
        {
            EntityGenerator.FillProfile(entityProfile, EntityKind.Cultivator);
            entityProfile.lockGeneratedValues = true;
        }

        npcName = entityProfile.identity.entityName;
        realm = entityProfile.stats.realm;
        lifespan = GetLifespanForRealm(realm);
        realmStage = entityProfile.stats.realmStage;
        comprehension = entityProfile.talent.comprehension;
        physique = ToPhysique(entityProfile.talent.grade);
        float realmPower =
            CultivationProgression.GetStatPower(
                realm,
                realmStage,
                EntityKind.Cultivator);
        baseMaxHP = Mathf.Max(1, Mathf.RoundToInt(entityProfile.stats.maxHP / realmPower));
        baseAttack = Mathf.Max(1, Mathf.RoundToInt(entityProfile.stats.attack / realmPower));
        baseDefense = Mathf.Max(0, Mathf.RoundToInt(entityProfile.stats.defense / realmPower));
        maxHP = entityProfile.stats.maxHP;
        currentHP =
            Mathf.Clamp(entityProfile.stats.currentHP, 0, maxHP);
        attack = entityProfile.stats.attack;
        defense = entityProfile.stats.defense;
        effectResistance = entityProfile.stats.effectResistance;
        moveSpeed = entityProfile.stats.moveSpeed;
        cultivation = entityProfile.stats.cultivationExp;
        money = entityProfile.stats.money;
        spiritStone = entityProfile.stats.spiritStone;
        bravery = entityProfile.personality.bravery;
        greed = entityProfile.personality.greed;
        kindness = entityProfile.personality.kindness;
        hunger = entityProfile.needs.hunger;
        fatigue = entityProfile.needs.fatigue;
    }

    [ContextMenu("Reload Smart NPC Identity")]
    public void ReloadGeneratedProfile()
    {
        if (entityProfile == null)
        {
            entityProfile = GetComponent<EntityProfile>();

            if (entityProfile == null)
            {
                entityProfile = gameObject.AddComponent<EntityProfile>();
            }
        }

        entityProfile.kind = EntityKind.Cultivator;
        entityProfile.ReloadGeneratedProfile();
        ApplyEntityProfile();

        if (characterStats != null)
        {
            characterStats.entityProfile = entityProfile;
            characterStats.ApplyEntityProfile();
            SyncFromCharacterStats();
        }
    }

    PhysiqueType ToPhysique(TalentGrade grade)
    {
        switch (grade)
        {
            case TalentGrade.ChildOfHeaven:
            case TalentGrade.SaintBody:
                return PhysiqueType.ChaosBody;
            case TalentGrade.FireSpiritRoot:
            case TalentGrade.SwordHeart:
            case TalentGrade.SpiritRoot:
                return PhysiqueType.FiveElementBody;
            default:
                return PhysiqueType.MortalBody;
        }
    }

    void Update()
    {
        if (HasActiveVillagerBrain())
        {
            return;
        }

        if (isDead)
        {
            return;
        }

        SyncFromCharacterStats();

        if (IsDead)
        {
            Die();
            return;
        }

        RefreshScheduledStateForCurrentFrame();

        if (NpcTaskProvider.IsNpcBusyWithAnyProvider(gameObject))
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        thinkTimer += Time.deltaTime;
        actionTimer -= Time.deltaTime;

        attackTimer += Time.deltaTime;

        if (waitingOutsideTreasureLightning)
        {
            UpdateTreasureWaitAction();
            thinkTimer = 0f;
            if (canLive)
            {
                UpdateNeeds();
            }
            return;
        }

        if (treasureHuntTarget != null)
        {
            RefreshTreasureHuntAction();
            thinkTimer = 0f;
            if (canLive)
            {
                UpdateNeeds();
            }
            return;
        }

        if (actionTimer > 0f)
        {
            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        if (currentMonsterTarget != null ||
            currentAction == NpcText.Action("huntMonsterNamed") ||
            currentAction == NpcText.Action("attackMonsterNamed"))
        {
            SearchMonster();
            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        if (HasLockedDirectedTarget())
        {
            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        if (thinkTimer >= thinkDelay)
        {
            thinkTimer = 0;

            Think();
        }

        if (canLive)
        {
            UpdateNeeds();
        }
    }

    void FixedUpdate()
    {
        if (HasActiveVillagerBrain())
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        if (IsDead)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            UpdateVisualAnimation();
            return;
        }

        UpdateMovement();
        UpdateVisualAnimation();
    }

    void LateUpdate()
    {
        if (HasActiveVillagerBrain())
        {
            return;
        }

        SyncCultivationEffect();
    }

    bool HasActiveVillagerBrain()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        return villager != null && villager.enabled;
    }

    void UpdateVisualAnimation()
    {
        if (visualAnimation == null)
        {
            visualAnimation = NPCVisualAnimation.EnsureOn(gameObject);
            if (visualAnimation == null)
            {
                return;
            }
        }

        Vector2 animationVelocity =
            rb != null
            ? rb.linearVelocity
            : Vector2.zero;

        bool isIdle = animationVelocity.sqrMagnitude <= 0.0001f;
        Vector2 direction = isIdle ? Vector2.zero : animationVelocity.normalized;

        visualAnimation.UpdateNPCAnimation(direction, isIdle, currentAction);
    }

    void UpdateMovement()
    {
        if (Time.time < movementPausedUntil ||
            Time.time < crowdYieldUntil)
        {
            if (!TryApplyNpcOverlapSeparation() && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        bool movingToTreasureWait =
            waitingOutsideTreasureLightning &&
            hasTreasureWaitPosition;
        bool movingHomeToCultivate =
            hasHomeReturnTarget &&
            canCultivate;

        bool holdPositionWithoutTarget =
            currentTarget == null &&
            !movingToTreasureWait &&
            !movingHomeToCultivate &&
            Time.time >= postTeleportRecoveryUntil &&
            IsStationaryAction(currentAction);

        if (currentTarget == null &&
            !movingToTreasureWait &&
            holdPositionWithoutTarget)
        {
            if (TryApplyNpcOverlapSeparation())
            {
                return;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            hasEscapeTarget = false;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;

            return;
        }

        Vector3 desiredTarget;

        if (movingToTreasureWait)
        {
            desiredTarget = treasureWaitPosition;
        }
        else if (movingHomeToCultivate)
        {
            desiredTarget = homeReturnTarget;
            hasWanderTarget = false;
        }
        else if (currentTarget != null)
        {
            desiredTarget = currentTarget.position;
            hasWanderTarget = false;
        }
        else if (hasWanderTarget)
        {
            desiredTarget = wanderTarget;
        }
        else
        {
            if (currentAction == NpcText.Action("goTaskProviderDaily"))
            {
                currentAction = NpcText.Action("visitedTaskProvider");
                actionTimer = Mathf.Max(
                    actionTimer,
                    GameHoursToSeconds(0.2f));

                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (IsPreservedTravelAction(currentAction))
            {
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;

            if (!IsStationaryAction(currentAction))
            {
                currentAction = "";
            }

            return;
        }

        if (movingHomeToCultivate)
        {
            float homeDistance =
                Vector2.Distance(
                    transform.position,
                    homeReturnTarget);

            if (homeDistance <=
                Mathf.Max(escapeTargetReachDistance, targetClearRadius * 2f))
            {
                hasHomeReturnTarget = false;
                currentTarget = null;
                hasEscapeTarget = false;
                hasObstacleAvoidTarget = false;

                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                CultivateNaturally();
                return;
            }
        }

        if (hasObstacleAvoidTarget)
        {
            if (Time.time >= obstacleAvoidUntil ||
                Vector2.Distance(transform.position, obstacleAvoidTarget) <=
                escapeTargetReachDistance ||
                !IsMoveTargetFeasible(obstacleAvoidTarget))
            {
                hasObstacleAvoidTarget = false;
            }
            else
            {
                desiredTarget = obstacleAvoidTarget;
            }
        }

        if (hasEscapeTarget)
        {
            if (Vector2.Distance(transform.position, escapeTarget) <=
                escapeTargetReachDistance)
            {
                hasEscapeTarget = false;
            }
            else
            {
                desiredTarget = escapeTarget;
            }
        }

        bool usingTeleportRoute;
        string routeAction;
        Vector3 moveTarget =
            NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                desiredTarget,
                out usingTeleportRoute,
                out routeAction);

        NpcMapArea spawnArea =
            NpcMapArea.FindArea(spawnPosition);
        NpcMapArea targetArea =
            NpcMapArea.FindArea(desiredTarget);
        bool targetInSpawnArea =
            spawnArea == null ||
            targetArea == null ||
            spawnArea.zone == targetArea.zone;

        bool isCultivationTravelRoute =
            currentTarget == cultivationPoint ||
            currentAction == NpcText.Action("goCultivatePoint") ||
            currentAction == NpcText.Action("goHomeCultivate");

        bool isAutonomousWorkRoute =
            currentAction == NpcText.Action("tradeSeek") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goTavern") ||
            currentAction == NpcText.Action("buyPill") ||
            currentAction == NpcText.Action("goHunt") ||
            currentAction == NpcText.Action("huntMonsterNamed") ||
            currentAction == NpcText.Action("attackMonsterNamed");

        float distanceFromSpawn =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (!movingToTreasureWait &&
            treasureHuntTarget == null &&
            !usingTeleportRoute &&
            targetInSpawnArea &&
            !isCultivationTravelRoute &&
            !isAutonomousWorkRoute &&
            distanceFromSpawn > maxRoamDistance)
        {
            ReturnToSpawn();

            return;
        }

        if (currentAction == NpcText.Action("goCultivatePoint") &&
            Vector2.Distance(transform.position, desiredTarget) <=
            Mathf.Max(escapeTargetReachDistance, targetClearRadius * 2f))
        {
            currentTarget = null;
            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            CultivateNaturally();
            return;
        }

        if (currentTarget == null &&
            hasWanderTarget &&
            Vector2.Distance(transform.position, desiredTarget) <= escapeTargetReachDistance)
        {
            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        Vector2 direction =
            (moveTarget -
            transform.position).normalized;

        if (!useObstacleAvoidance)
        {
            if (!TryResolveCrowdAhead(direction, out direction))
            {
                return;
            }

            direction = ApplyCrowdAvoidance(direction);
            rb.linearVelocity = direction * moveSpeed;
            UpdateUnstuck(direction);
            return;
        }

        if (IsMovementBlocked(direction))
        {
            if (TryChooseObstacleDetourDirection(direction, desiredTarget, out Vector2 detourDirection))
            {
                if (TryCommitObstacleAvoidTarget(detourDirection))
                {
                    blockedMoveTimer = 0f;
                    return;
                }

                HandleBlockedMovement(moveTarget, desiredTarget);
                return;
            }
            else
            {
                HandleBlockedMovement(moveTarget, desiredTarget);
                return;
            }
        }

        if (!TryResolveCrowdAhead(direction, out direction))
        {
            return;
        }

        direction = ApplyCrowdAvoidance(direction);

        rb.linearVelocity =
            direction * moveSpeed;

        UpdateUnstuck(direction);
    }

    void OnDisable()
    {
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
    }

    void OnNpcMapTeleported()
    {
        OnNpcMapTeleported(null);
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        spawnPosition = transform.position;       // Đặt lại điểm gốc di chuyển tại map mới
        lastUnstuckPosition = transform.position; // Reset vị trí chống kẹt
        hasWanderTarget = false;                  // Hủy điểm muốn đi dạo cũ ở Map A
        currentTarget = null;                     // Hủy mục tiêu đuổi theo cũ ở Map A
        currentMonsterTarget = null;
        waitingOutsideTreasureLightning = false;
        hasTreasureWaitPosition = false;
        treasureWaitLowPowerSkirmish = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        postTeleportRecoveryUntil = Time.time + 5f;
        stuckMoveTimer = 0f;
        blockedMoveTimer = 0f;
        actionTimer = 0f;
        thinkTimer = 0f;
        currentAction = "";

        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;

        if (gate != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
            NpcMapArea resolvedArea =
                NpcMapNavigator.ResolveMapAreaAfterTeleport(
                    gameObject,
                    gate.toZone,
                    transform.position);

            if (resolvedArea != null)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedArea.zone);
            }
        }
        else
        {
            NpcMapArea area = NpcMapArea.FindArea(transform.position);
            if (area != null)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
            }
        }

        if (TryFindClearPointNear(transform.position, out Vector3 clearPoint, false))
        {
            transform.position = clearPoint;
            spawnPosition = clearPoint;
            lastUnstuckPosition = clearPoint;

            if (rb != null)
            {
                rb.position = clearPoint;
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        UpdateCultivationEffect(false);
    }

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
            other.GetComponentInParent<NpcMapMover2D>() == null)
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

    void TryEscapeObstacleCollision(Collision2D collision)
    {
        if (collision == null || collision.contactCount <= 0)
        {
            return;
        }

        if (ignoreNpcBodyCollisions &&
            collision.collider != null &&
            (collision.collider.GetComponentInParent<VillagerAI>() != null ||
             collision.collider.GetComponentInParent<SmartNpcAI>() != null ||
             collision.collider.GetComponentInParent<NpcMapMover2D>() != null))
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

        if (rb != null)
        {
            rb.linearVelocity =
                normal.normalized * moveSpeed * 0.75f;
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
            rb.linearVelocity = Vector2.zero;
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

        Vector2 away = transform.position - origin;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = Random.insideUnitCircle.normalized;
        }

        treasureWaitPosition =
            origin +
            (Vector3)away.normalized * Mathf.Max(0.5f, safeRadius);
        hasTreasureWaitPosition = true;
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
        currentAction = NpcText.ActionFormat("treasureHuntNamed", ItemText.Name(item));
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
        if (currentTarget != null && currentAction.Contains(NpcText.Action("treasureHunt")))
        {
            currentTarget = null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
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
        homeReturnTarget = spawnPosition;
        hasHomeReturnTarget = true;

        Vector2 direction =
            (spawnPosition -
            transform.position).normalized;

        if (useObstacleAvoidance &&
            IsMovementBlocked(direction))
        {
            if (TryChooseObstacleDetourDirection(direction, spawnPosition, out Vector2 spawnDetour))
            {
                if (TryCommitObstacleAvoidTarget(spawnDetour))
                {
                    return;
                }

                HandleBlockedMovement(spawnPosition, spawnPosition);
                return;
            }
            else
            {
                HandleBlockedMovement(spawnPosition, spawnPosition);
                return;
            }
        }

        if (!TryResolveCrowdAhead(direction, out direction))
        {
            return;
        }

        direction = ApplyCrowdAvoidance(direction);

        rb.linearVelocity =
            direction * moveSpeed;

        currentAction = canCultivate
            ? NpcText.Action("goHomeCultivate")
            : NpcText.Action("idle");

        float distance =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (distance < 1f)
        {
            rb.linearVelocity = Vector2.zero;

            currentTarget = null;
            hasHomeReturnTarget = false;

            if (canCultivate)
            {
                CultivateNaturally();
            }
        }
    }

    bool TryPickIdleWanderTarget(out Vector3 target)
    {
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
            action == NpcText.Action("rest") ||
            action == NpcText.Action("cultivate") ||
            action == NpcText.Action("cultivateAbsorbQi") ||
            action == NpcText.Action("waitTribulation") ||
            action == NpcText.Action("breakthrough") ||
            action == NpcText.Action("injured") ||
            action == NpcText.Action("dead") ||
            action == NpcText.Action("oldAgeDeath") ||
            action == NpcText.Action("outerSkirmishNamed") ||
            action == NpcText.Action("waitLightningNamed");
    }

    bool HasLockedDirectedTarget()
    {
        if (currentTarget == null &&
            !(hasHomeReturnTarget &&
                currentAction == NpcText.Action("goHomeCultivate")))
        {
            return false;
        }

        return currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("visitedTaskProvider") ||
            currentAction == NpcText.Action("goHomeCultivate") ||
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
            currentAction == NpcText.Action("outerSkirmishNamed");
    }

    public void StopForConversation()
    {
        StopForConversation(2f);
    }

    public void StopForConversation(float duration)
    {
        movementPausedUntil = Mathf.Max(
            movementPausedUntil,
            Time.time + Mathf.Max(0.2f, duration));

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void UpdateUnstuck(Vector2 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            return;
        }

        float moved = Vector2.Distance(
            transform.position,
            lastUnstuckPosition);

        if (moved <= unstuckMinMoveDistance)
        {
            stuckMoveTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
        }

        if (stuckMoveTimer < unstuckCheckDelay)
        {
            return;
        }

        if (TryPickUnstuckEscapeTarget(moveDirection, out escapeTarget))
        {
            hasEscapeTarget = true;
            hasObstacleAvoidTarget = false;
            blockedMoveTimer = 0f;
        }
        else
        {
            Vector3 finalTarget =
                currentTarget != null
                ? currentTarget.position
                : hasWanderTarget
                    ? wanderTarget
                    : transform.position + (Vector3)moveDirection;

            hasEscapeTarget = false;
            HandleBlockedMovement(
                transform.position + (Vector3)moveDirection,
                finalTarget);
        }

        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;
    }

    bool TryPickUnstuckEscapeTarget(
        Vector2 moveDirection,
        out Vector3 target)
    {
        Vector2 direction = moveDirection.sqrMagnitude > 0.0001f
            ? moveDirection.normalized
            : Random.insideUnitCircle.normalized;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.up;
        }

        Vector2 side = new Vector2(-direction.y, direction.x);
        int sideSign = (GetInstanceID() & 1) == 0 ? 1 : -1;

        Vector2[] directions =
        {
            side * sideSign,
            -side * sideSign,
            (side * sideSign - direction * 0.5f).normalized,
            (-side * sideSign - direction * 0.5f).normalized,
            -direction,
            (side * sideSign + direction * 0.25f).normalized,
            (-side * sideSign + direction * 0.25f).normalized
        };

        float baseDistance =
            Mathf.Max(0.35f, unstuckOffsetRadius, targetClearRadius * 3f);
        float bestScore = float.NegativeInfinity;
        Vector3 best = transform.position;
        bool found = false;

        for (int radiusStep = 0; radiusStep < 4; radiusStep++)
        {
            float distance =
                baseDistance + radiusStep * Mathf.Max(targetClearRadius * 2f, 0.35f);

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 escapeDirection = directions[i];
                if (escapeDirection.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                escapeDirection.Normalize();
                Vector3 candidate =
                    transform.position +
                    (Vector3)(escapeDirection * distance);

                candidate.z = transform.position.z;

                if (!IsMoveTargetFeasible(candidate) ||
                    !HasClearLineTo(candidate) ||
                    IsMovementBlocked(escapeDirection))
                {
                    continue;
                }

                float score =
                    GetClearDistance(escapeDirection, GetObstacleLookAheadDistance()) +
                    Mathf.Max(-0.25f, Vector2.Dot(escapeDirection, -direction)) *
                    baseDistance;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }
        }

        target = best;
        return found;
    }

    bool IsMoveTargetFeasible(Vector3 position)
    {
        return !IsPositionBlocked(position);
    }

    bool IsPositionBlocked(Vector3 position)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                GetBodyClearRadius());

        foreach (Collider2D hit in hits)
        {
            if (IsBlockingObstacle(hit))
            {
                return true;
            }
        }

        return false;
    }

    float GetBodyClearRadius()
    {
        float radius = Mathf.Max(0.01f, targetClearRadius + navigationClearancePadding);

        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>();
        }

        foreach (Collider2D own in selfColliders)
        {
            if (own == null || own.isTrigger)
            {
                continue;
            }

            Bounds bounds = own.bounds;
            radius =
                Mathf.Max(
                    radius,
                    bounds.extents.x,
                    bounds.extents.y);
        }

        return radius;
    }

    bool HasClearLineTo(Vector3 target)
    {
        Vector2 origin = transform.position;
        Vector2 delta = (Vector2)target - origin;
        float distance = delta.magnitude;

        if (distance <= targetClearRadius)
        {
            return true;
        }

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                origin,
                Mathf.Max(0.01f, GetBodyClearRadius()),
                delta.normalized,
                distance);

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                return false;
            }
        }

        return true;
    }

    bool IsMovementBlocked(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                transform.position,
                Mathf.Max(0.01f, GetBodyClearRadius()),
                direction.normalized,
                Mathf.Max(obstacleCheckDistance, obstacleDetourLookAhead));

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                return true;
            }
        }

        return false;
    }

    bool TryChooseObstacleDetourDirection(
        Vector2 desiredDirection,
        Vector3 finalTarget,
        out Vector2 detourDirection)
    {
        detourDirection = desiredDirection;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = desiredDirection.normalized;
        Vector2 targetDirection =
            ((Vector2)finalTarget - (Vector2)transform.position);
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            targetDirection = desired;
        }
        else
        {
            targetDirection.Normalize();
        }

        float lookAhead = GetObstacleLookAheadDistance();
        float detourStep = Mathf.Max(
            targetClearRadius * 3f,
            obstacleDetourLookAhead * 2f,
            moveSpeed * 0.75f);
        float bestScore = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < DetourAngles.Length; i++)
        {
            float angle = DetourAngles[i];

            if (TryScoreObstacleDetourDirection(
                    RotateDirection(desired, angle),
                    desired,
                    targetDirection,
                    Mathf.Max(lookAhead, detourStep),
                    out float score) &&
                score > bestScore)
            {
                bestScore = score;
                detourDirection = RotateDirection(desired, angle);
                found = true;
            }

            if (Mathf.Approximately(angle, 0f))
            {
                continue;
            }

            if (TryScoreObstacleDetourDirection(
                    RotateDirection(desired, -angle),
                    desired,
                    targetDirection,
                    Mathf.Max(lookAhead, detourStep),
                    out score) &&
                score > bestScore)
            {
                bestScore = score;
                detourDirection = RotateDirection(desired, -angle);
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        detourDirection.Normalize();
        return true;
    }

    bool TryCommitObstacleAvoidTarget(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = direction.normalized;
        float distance = Mathf.Max(
            unstuckOffsetRadius * 1.5f,
            obstacleDetourLookAhead * 1.5f,
            targetClearRadius * 4f,
            moveSpeed * 0.5f);

        Vector3 candidate =
            transform.position +
            (Vector3)(desired * distance);

        Vector3 clearPoint;
        if (!TryFindClearPointNear(candidate, out clearPoint))
        {
            clearPoint = candidate;
        }

        if (!IsMoveTargetFeasible(clearPoint) ||
            !HasClearLineTo(clearPoint))
        {
            return false;
        }

        obstacleAvoidTarget = clearPoint;
        obstacleAvoidUntil = Time.time + 1.1f;
        hasObstacleAvoidTarget = true;
        blockedMoveTimer = 0f;
        return true;
    }

    bool TryScoreObstacleDetourDirection(
        Vector2 candidate,
        Vector2 desired,
        Vector2 targetDirection,
        float lookAhead,
        out float score)
    {
        score = 0f;

        if (candidate.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        candidate.Normalize();

        Vector3 nextPoint =
            transform.position +
            (Vector3)(candidate * Mathf.Max(targetClearRadius * 3f, lookAhead * 0.85f));

        if (!IsMoveTargetFeasible(nextPoint) ||
            !HasClearLineTo(nextPoint))
        {
            return false;
        }

        float clearDistance = GetClearDistance(candidate, lookAhead);
        if (clearDistance < targetClearRadius * 2f)
        {
            return false;
        }

        float progressScore = Mathf.Max(-0.5f, Vector2.Dot(candidate, targetDirection));
        if (progressScore < -0.05f)
        {
            return false;
        }

        float smoothScore = Mathf.Max(-0.5f, Vector2.Dot(candidate, desired));

        score =
            clearDistance / Mathf.Max(0.01f, lookAhead) * 3f +
            progressScore * 2f +
            smoothScore;

        return true;
    }

    bool TryFindClearPointNear(
        Vector3 preferred,
        out Vector3 result,
        bool requireClearLine = true)
    {
        preferred.z = transform.position.z;

        if (IsMoveTargetFeasible(preferred))
        {
            result = preferred;
            return true;
        }

        float baseRadius = Mathf.Max(targetClearRadius * 2f, 0.25f);
        for (int radiusStep = 0; radiusStep < 6; radiusStep++)
        {
            float radius = baseRadius + radiusStep * 0.2f;
            for (int angleStep = 0; angleStep < 16; angleStep++)
            {
                float angle =
                    (angleStep / 16f) * Mathf.PI * 2f +
                    radiusStep * 0.17f;
                Vector2 offset =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    radius;
                Vector3 candidate =
                    preferred + new Vector3(offset.x, offset.y, 0f);

                if (IsMoveTargetFeasible(candidate) &&
                    (!requireClearLine || HasClearLineTo(candidate)))
                {
                    result = candidate;
                    return true;
                }
            }
        }

        result = transform.position;
        return false;
    }

    Vector3 GetBlockedEscapeSeed(Vector3 blockedTarget, Vector3 finalTarget)
    {
        Vector2 away = (Vector2)(transform.position - blockedTarget);
        Vector2 towardFinal = (Vector2)finalTarget - (Vector2)transform.position;

        if (towardFinal.sqrMagnitude > 0.0001f)
        {
            towardFinal.Normalize();
            away += towardFinal * 0.45f;
        }

        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Random.insideUnitCircle;
        }

        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.up;
        }

        away.Normalize();

        return transform.position +
            (Vector3)(away * Mathf.Max(unstuckOffsetRadius, targetClearRadius * 3f));
    }

    void HandleBlockedMovement(Vector3 blockedTarget, Vector3 finalTarget)
    {
        blockedMoveTimer += Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.zero;

        if (blockedMoveTimer < blockedTargetRetryDelay)
        {
            return;
        }

        blockedMoveTimer = 0f;

        Vector2 escapeDirection =
            (Vector2)finalTarget - (Vector2)transform.position;

        if (TryChooseObstacleDetourDirection(
                escapeDirection,
                finalTarget,
                out Vector2 detourDirection) &&
            TryCommitObstacleAvoidTarget(detourDirection))
        {
            return;
        }

        Vector3 escapeSeed =
            GetBlockedEscapeSeed(blockedTarget, finalTarget);

        if (TryFindClearPointNear(escapeSeed, out Vector3 clear))
        {
            obstacleAvoidTarget = clear;
            obstacleAvoidUntil = Time.time + 1f;
            hasObstacleAvoidTarget = true;
            hasEscapeTarget = false;
        }
    }

    bool IsBlockingObstacle(Collider2D hit)
    {
        if (hit == null || hit.isTrigger || IsSelfCollider(hit))
        {
            return false;
        }

        return hit.GetComponentInParent<VillagerAI>() == null &&
            hit.GetComponentInParent<SmartNpcAI>() == null &&
            hit.GetComponentInParent<NpcMapMover2D>() == null;
    }

    float GetClearDistance(
        Vector2 direction,
        float maxDistance)
    {
        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                transform.position,
                Mathf.Max(0.01f, GetBodyClearRadius()),
                direction.normalized,
                maxDistance);

        float best = maxDistance;

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                best = Mathf.Min(best, hit.distance);
            }
        }

        return best;
    }

    float GetObstacleLookAheadDistance()
    {
        float speedLookAhead =
            Mathf.Max(0f, moveSpeed) * 0.25f + targetClearRadius * 2f;

        return Mathf.Max(
            obstacleCheckDistance,
            obstacleDetourLookAhead,
            targetClearRadius * 3f,
            speedLookAhead);
    }

    Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    static readonly float[] DetourAngles =
    {
        0f,
        20f,
        35f,
        50f,
        70f,
        90f,
        120f,
        150f
    };

    bool TryResolveCrowdAhead(
        Vector2 desiredDirection,
        out Vector2 resolvedDirection)
    {
        resolvedDirection = desiredDirection;

        if (ignoreNpcBodyCollisions)
        {
            return true;
        }

        if (desiredDirection.sqrMagnitude <= 0.0001f ||
            crowdLookAheadDistance <= 0f ||
            rb == null)
        {
            return true;
        }

        Collider2D other;
        if (!TryFindNpcAhead(desiredDirection, out other))
        {
            return true;
        }

        if (ShouldYieldToNpc(other))
        {
            crowdYieldUntil =
                Time.time +
                Mathf.Max(0.05f, crowdYieldDuration) *
                Random.Range(0.75f, 1.35f);
            rb.linearVelocity = Vector2.zero;
            return false;
        }

        if (TryChooseCrowdDetourDirection(
                desiredDirection,
                other,
                out resolvedDirection))
        {
            return true;
        }

        crowdYieldUntil =
            Time.time +
            Mathf.Max(0.05f, crowdYieldDuration) *
            Random.Range(0.75f, 1.35f);
        rb.linearVelocity = Vector2.zero;
        return false;
    }

    bool TryFindNpcAhead(
        Vector2 direction,
        out Collider2D npcCollider)
    {
        npcCollider = null;

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                rb.position,
                Mathf.Max(0.01f, separationRadius * 0.45f),
                direction.normalized,
                Mathf.Max(separationRadius, crowdLookAheadDistance),
                crowdLayers);

        float nearestDistance =
            float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D collider = hit.collider;
            if (collider == null ||
                IsSelfCollider(collider) ||
                !IsNpcCollider(collider))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                npcCollider = collider;
            }
        }

        return npcCollider != null;
    }

    Vector2 ApplyCrowdAvoidance(Vector2 direction)
    {
        if (ignoreNpcBodyCollisions ||
            separationRadius <= 0f ||
            rb == null)
        {
            return direction;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                rb.position,
                separationRadius,
                crowdLayers);

        Vector2 push = Vector2.zero;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                IsSelfCollider(hit) ||
                !IsNpcCollider(hit))
            {
                continue;
            }

            Vector2 away =
                rb.position -
                (Vector2)hit.transform.position;

            float distance =
                Mathf.Max(away.magnitude, 0.01f);

            push += away.normalized / distance;
        }

        if (push.sqrMagnitude <= 0.0001f)
        {
            return direction;
        }

        return (direction + push.normalized * separationStrength).normalized;
    }

    bool TryApplyNpcOverlapSeparation()
    {
        if (ignoreNpcBodyCollisions ||
            rb == null ||
            separationRadius <= 0f)
        {
            return false;
        }

        Vector2 separation = GetNpcSeparationDirection();
        if (separation.sqrMagnitude <= 0.0001f ||
            IsMovementBlocked(separation))
        {
            return false;
        }

        rb.linearVelocity =
            separation.normalized * moveSpeed * 0.65f;
        return true;
    }

    Vector2 GetNpcSeparationDirection()
    {
        if (ignoreNpcBodyCollisions)
        {
            return Vector2.zero;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                rb.position,
                separationRadius,
                crowdLayers);

        Vector2 push = Vector2.zero;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                IsSelfCollider(hit) ||
                !IsNpcCollider(hit))
            {
                continue;
            }

            Vector2 away =
                rb.position -
                (Vector2)hit.transform.position;

            if (away.sqrMagnitude <= 0.0001f)
            {
                Transform root = GetNpcRoot(hit);
                int otherId =
                    root != null
                    ? root.gameObject.GetInstanceID()
                    : hit.gameObject.GetInstanceID();
                away = ((GetInstanceID() ^ otherId) & 1) == 0
                    ? Vector2.right
                    : Vector2.left;
            }

            float distance =
                Mathf.Max(away.magnitude, 0.01f);

            push += away.normalized / distance;
        }

        return push.normalized;
    }

    bool TryChooseCrowdDetourDirection(
        Vector2 desiredDirection,
        Collider2D other,
        out Vector2 detourDirection)
    {
        detourDirection = desiredDirection;

        Vector2 desired =
            desiredDirection.normalized;

        Vector2 side =
            new Vector2(-desired.y, desired.x);

        if (ShouldUseRightSide(other))
        {
            side = -side;
        }

        float distance =
            Mathf.Max(crowdDetourDistance, separationRadius);

        for (int i = 0; i < 2; i++)
        {
            Vector2 candidateSide =
                i == 0 ? side : -side;

            Vector2 candidateDirection =
                (candidateSide + desired * 0.35f).normalized;

            if (candidateDirection.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            Vector2 candidate =
                rb.position + candidateDirection * distance;

            Collider2D hit =
                Physics2D.OverlapCircle(
                    candidate,
                    Mathf.Max(0.01f, separationRadius * 0.45f),
                    crowdLayers);

            if (hit != null &&
                !IsSelfCollider(hit) &&
                IsNpcCollider(hit))
            {
                continue;
            }

            detourDirection = candidateDirection;
            return true;
        }

        return false;
    }

    bool ShouldYieldToNpc(Collider2D other)
    {
        Transform otherRoot = GetNpcRoot(other);
        if (otherRoot == null)
        {
            return false;
        }

        return GetInstanceID() > otherRoot.gameObject.GetInstanceID();
    }

    bool ShouldUseRightSide(Collider2D other)
    {
        Transform otherRoot = GetNpcRoot(other);
        int otherId = otherRoot != null
            ? otherRoot.gameObject.GetInstanceID()
            : 0;

        return ((GetInstanceID() ^ otherId) & 1) == 0;
    }

    bool IsNpcCollider(Collider2D hit)
    {
        return GetNpcRoot(hit) != null;
    }

    bool IsSelfCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.transform == transform ||
            hit.transform.IsChildOf(transform))
        {
            return true;
        }

        if (selfColliders == null)
        {
            return false;
        }

        foreach (Collider2D own in selfColliders)
        {
            if (own == hit)
            {
                return true;
            }
        }

        return false;
    }

    Transform GetNpcRoot(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.transform;
        }

        NpcMapMover2D mover =
            hit.GetComponentInParent<NpcMapMover2D>();
        return mover != null ? mover.transform : null;
    }

    void SyncFromCharacterStats()
    {
        if (characterStats == null)
        {
            return;
        }

        realm = characterStats.realm;
        realmStage = characterStats.realmStage;
        cultivation = characterStats.cultivationExp;
        breakthroughNeed = characterStats.ExpToNextRealm();
        maxHP = characterStats.finalHP;
        currentHP = characterStats.currentHP;
        attack = characterStats.attack;
        defense = characterStats.defense;
        effectResistance = characterStats.effectResistance;
        moveSpeed = characterStats.moveSpeed;
    }

    void Think()
    {
        if (IsDead)
        {
            Die();
            return;
        }

        if (currentHP <= 0)
        {
            Die();

            return;
        }

        if (ShouldDieFromOldAge())
        {
            currentAction = NpcText.Action("oldAgeDeath");
            Die();
            return;
        }

        if (IsLockedRoutineAction(currentAction))
        {
            return;
        }

        if (TryRunScheduledActivity())
        {
            return;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            if (timeSystem.CurrentPhase == WorldTimePhase.Night &&
                !IgnoresMortalNeeds() &&
                bravery < 55 &&
                currentMonsterTarget == null)
            {
                Sleep();
                return;
            }

            if (autonomousActivitiesEnabled &&
                timeSystem.CurrentPhase == WorldTimePhase.Evening &&
                canMakeFriends &&
                kindness + greed < 130)
            {
                MakeFriend();
                return;
            }
        }

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null &&
            weather.CurrentWeather == WorldWeather.DenseSpiritualQi &&
            canCultivate)
        {
            Cultivate();
            return;
        }

        if (canLive &&
            NeedsFood() &&
            hunger >= 80)
        {
            Eat();

            return;
        }

        if (canLive &&
            !IgnoresMortalNeeds() &&
            fatigue >= 85)
        {
            Sleep();

            return;
        }

        if (dailyRoutineEnabled &&
            dailyTaskVisitEnabled &&
            Random.value < dailyTaskVisitChance &&
            TryVisitTaskProvider())
        {
            return;
        }

        if (dailyRoutineEnabled &&
            canCultivate &&
            IsScheduledCultivationTime())
        {
            CultivateNaturally();
            return;
        }

        if (canCultivate &&
            (pill > 0 || spiritStone > 0))
        {
            Cultivate();

            return;
        }

        if (dailyRoutineEnabled &&
            TryStartScheduledNonCultivationActivity())
        {
            return;
        }

        if (autonomousActivitiesEnabled &&
            canTrade &&
            money >= 50 &&
            pill <= 0)
        {
            GoToTavernAndBuyPill();

            return;
        }

        if (autonomousActivitiesEnabled &&
            canFight)
        {
            SearchMonster();

            return;
        }

        if (autonomousActivitiesEnabled &&
            canMakeFriends)
        {
            MakeFriend();

            return;
        }

        if (autonomousActivitiesEnabled &&
            canCreateSect)
        {
            TryCreateSect();

            return;
        }

        if (TryStartScheduledNonCultivationActivity())
        {
            return;
        }

        if (canCultivate)
        {
            CultivateNaturally();
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        currentAction = "";
    }

    void RefreshScheduledStateForCurrentFrame()
    {
        NpcScheduleController schedule = GetComponent<NpcScheduleController>();
        if (schedule == null || !schedule.enforceSchedule)
        {
            return;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        if (slot == null)
        {
            return;
        }

        string key = BuildScheduleSlotKey(slot, schedule.CurrentActivity);
        if (currentScheduleSlotKey == key)
        {
            return;
        }

        currentScheduleSlotKey = key;
        currentTarget = null;
        currentMonsterTarget = null;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        waitingOutsideTreasureLightning = false;
        hasWanderTarget = false;
        hasCultivationTarget = false;
        hasEscapeTarget = false;
        hasHomeReturnTarget = false;
        hasObstacleAvoidTarget = false;
        actionTimer = 0f;
        currentAction = string.Empty;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        UpdateCultivationEffect(false);

        if (resourceGatherer != null)
        {
            resourceGatherer.CancelGatheringNow();
        }
    }

    string BuildScheduleSlotKey(NpcScheduleSlot slot, NpcScheduleActivity activity)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        int day = timeSystem != null ? timeSystem.CurrentDay : 0;
        return day + ":" + activity + ":" +
            Mathf.RoundToInt(slot.startHour * 100f) + ":" +
            Mathf.RoundToInt(slot.endHour * 100f);
    }

    bool TryRunScheduledActivity()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        NpcScheduleActivity activity = schedule.CurrentActivity;

        if (slot == null)
        {
            return false;
        }

        if (canLive &&
            slot.allowHungerInterrupt &&
            NeedsFood() &&
            hunger >= 90f)
        {
            Eat();
            return true;
        }

        if (canLive &&
            !IgnoresMortalNeeds() &&
            slot.allowFatigueInterrupt &&
            fatigue >= 90f)
        {
            Sleep();
            return true;
        }

        switch (activity)
        {
            case NpcScheduleActivity.Cultivate:
                if (canCultivate)
                {
                    if (schedule.HasCompletedCurrentSlotActivity(
                            NpcScheduleActivity.Cultivate))
                    {
                        ClearCompletedCultivationAction();
                        return true;
                    }

                    if (schedule.HasStartedCurrentSlotActivity(
                            NpcScheduleActivity.Cultivate))
                    {
                        if (currentAction == NpcText.Action("goCultivatePoint"))
                        {
                            if (currentTarget != null ||
                                hasWanderTarget ||
                                hasHomeReturnTarget)
                            {
                                return true;
                            }

                            CultivateNaturally();
                            return true;
                        }

                        if (actionTimer > 0f)
                        {
                            return true;
                        }

                        CultivateNaturally();
                        return true;
                    }

                    CultivateNaturally();
                    if (currentAction == NpcText.Action("goCultivatePoint") ||
                        currentAction == NpcText.Action("cultivate") ||
                        currentAction == NpcText.Action("cultivateAbsorbQi"))
                    {
                        schedule.MarkCurrentSlotActivityStarted(
                            NpcScheduleActivity.Cultivate);
                    }
                }
                else
                {
                    StartIdleWander();
                }
                return true;

            case NpcScheduleActivity.BuyGoods:
                if (canTrade && money >= 50)
                {
                    GoToTavernAndBuyPill();
                }
                else
                {
                    WaitForScheduledActivity(NpcScheduleActivity.BuyGoods);
                }
                return true;

            case NpcScheduleActivity.SellGoods:
                if (!TryStartSellGoodsRoutine())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.SellGoods);
                }
                return true;

            case NpcScheduleActivity.TakeTask:
                if (!TryVisitTaskProvider())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.TakeTask);
                }
                return true;

            case NpcScheduleActivity.Gather:
                if (!TryStartResourceGatheringRoutine())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Gather);
                }
                return true;

            case NpcScheduleActivity.Hunt:
                if (canFight)
                {
                    SearchMonster();
                    if (currentMonsterTarget == null)
                    {
                        WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                    }
                }
                else
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Hunt);
                }
                return true;

            case NpcScheduleActivity.Eat:
                Eat();
                return true;

            case NpcScheduleActivity.Sleep:
                if (IgnoresMortalNeeds())
                {
                    WaitForScheduledActivity(NpcScheduleActivity.Sleep);
                }
                else
                {
                    Sleep();
                }
                return true;

            default:
                WaitForScheduledActivity(activity);
                return true;
        }
    }

    void WaitForScheduledActivity(NpcScheduleActivity activity)
    {
        if (activity == NpcScheduleActivity.Cultivate &&
            canCultivate)
        {
            CultivateNaturally();
            return;
        }

        currentTarget = null;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasHomeReturnTarget = false;
        hasObstacleAvoidTarget = false;
        actionTimer = Mathf.Max(actionTimer, GameHoursToSeconds(0.15f));

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        UpdateCultivationEffect(false);
        currentAction = "waitSchedule" + activity;
    }

    void StartIdleWander()
    {
        if (canCultivate)
        {
            CultivateNaturally();
            return;
        }

        actionTimer =
            Mathf.Max(
                actionTimer,
                GameHoursToSeconds(Random.Range(0.4f, 1.2f)));
        currentAction = "";
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void ConfigureAutonomousWorkSystems()
    {
        if (canGather)
        {
            if (GetComponent<NpcItemCollector>() == null)
            {
                gameObject.AddComponent<NpcItemCollector>();
            }

            resourceGatherer = GetComponent<NpcResourceGatherer>();
            if (resourceGatherer == null)
            {
                resourceGatherer = gameObject.AddComponent<NpcResourceGatherer>();
            }

            resourceGatherer.canGather = true;
        }

        if (canTrade || canSellGoods)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
            if (tradeAgent == null)
            {
                tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
            }

            if (tradeAgent != null)
            {
                tradeAgent.inventory = GetComponent<ItemInventory>();
            }
        }
    }

    bool TryStartScheduledNonCultivationActivity()
    {
        if (dailyTaskVisitEnabled &&
            Random.value < dailyTaskVisitChance &&
            TryVisitTaskProvider())
        {
            return true;
        }

        if (TryStartSellGoodsRoutine())
        {
            return true;
        }

        if (TryStartResourceGatheringRoutine())
        {
            return true;
        }

        if (canTrade &&
            (pill <= 0 || spiritStone <= 0) &&
            money >= 50 &&
            Random.value < 0.35f)
        {
            GoToTavernAndBuyPill();
            if (currentTarget == null &&
                currentAction != NpcText.Action("buyPill"))
            {
                if (canCultivate)
                {
                    CultivateNaturally();
                }
                else if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    currentAction = "";
                }

                return true;
            }

            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        tradeSessionMinGameHours,
                        tradeSessionMaxGameHours));
            return true;
        }

        if (canFight &&
            bravery >= 45 &&
            Random.value < 0.25f)
        {
            SearchMonster();
            if (currentMonsterTarget == null)
            {
                if (canCultivate)
                {
                    CultivateNaturally();
                }
                else if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    currentAction = "";
                }

                return true;
            }

            actionTimer =
                GameHoursToSeconds(Random.Range(0.5f, 1.5f));
            return true;
        }

        if (canMakeFriends &&
            kindness + greed < 140 &&
            Random.value < 0.25f)
        {
            MakeFriend();
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        socialSessionMinGameHours,
                        socialSessionMaxGameHours));
            return true;
        }

        if (autonomousActivitiesEnabled &&
            canCreateSect)
        {
            TryCreateSect();
            return true;
        }

        if (canCultivate)
        {
            CultivateNaturally();
            return true;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        currentAction = "";
        return true;
    }

    bool TryStartForgePurchaseRoutine()
    {
        if (!canTrade ||
            money <= 0)
        {
            return false;
        }

        if (currentForgeTradeTarget == null ||
            currentForgeTradeItem == null)
        {
            currentForgeTradeTarget =
                NpcForgeAgent.FindBestForgeForBuyer(
                    gameObject,
                    out currentForgeTradeItem);

            if (currentForgeTradeTarget == null ||
                currentForgeTradeItem == null)
            {
                ClearForgeTradeTarget();
                return false;
            }

            Transform forgePoint =
                currentForgeTradeTarget.forgeStandPoint != null
                    ? currentForgeTradeTarget.forgeStandPoint
                    : currentForgeTradeTarget.transform;

            if (forgePoint == null)
            {
                ClearForgeTradeTarget();
                return false;
            }

            currentTarget = forgePoint;
            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            currentAction = NpcText.Action("tradeSeek");
        }

        if (currentForgeTradeTarget == null ||
            currentForgeTradeItem == null ||
            currentTarget == null)
        {
            ClearForgeTradeTarget();
            return false;
        }

        if (Vector2.Distance(transform.position, currentTarget.position) >
            Mathf.Max(0.5f, targetClearRadius))
        {
            return true;
        }

        string buyerLine;
        string smithLine;
        bool ordered =
            currentForgeTradeTarget.TryRequestCustomOrder(
                gameObject,
                currentForgeTradeItem,
                1,
                out buyerLine,
                out smithLine);

        ClearForgeTradeTarget();

        if (!ordered)
        {
            return false;
        }

        actionTimer =
            GameHoursToSeconds(
                Random.Range(
                    tradeSessionMinGameHours,
                    tradeSessionMaxGameHours));
        currentAction = NpcText.Action("idle");
        return true;
    }

    void ClearForgeTradeTarget()
    {
        currentForgeTradeTarget = null;
        currentForgeTradeItem = null;
        currentTarget = null;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    bool TryVisitTaskProvider()
    {
        if (!IsTaskVisitStaggerReady())
        {
            return false;
        }

        int day = GetCurrentWorldDay();
        if (lastTaskProviderVisitDay == day)
        {
            return false;
        }

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider == null)
        {
            return false;
        }

        Vector3 providerPosition =
            provider.GetProviderPositionFor(gameObject);

        currentAction = NpcText.Action("goTaskProviderDaily");

        if (Vector2.Distance(transform.position, providerPosition) > 1.5f)
        {
            currentTarget = provider.transform;
            hasWanderTarget = false;
            actionTimer = 0f;
            return true;
        }

        currentTarget = null;
        hasWanderTarget = false;
        lastTaskProviderVisitDay = day;

        if (provider.TryHandleVisitor(gameObject))
        {
            return true;
        }

        actionTimer = Mathf.Max(thinkDelay, GameHoursToSeconds(0.15f));
        currentAction = NpcText.Action("visitedTaskProvider");
        return true;
    }

    bool IsScheduledCultivationTime()
    {
        EnsureDailyRoutinePlan();
        float hour = GetCurrentWorldHour();

        if (routineCultivationStartHour <= routineCultivationEndHour)
        {
            return hour >= routineCultivationStartHour &&
                hour < routineCultivationEndHour;
        }

        return hour >= routineCultivationStartHour ||
            hour < routineCultivationEndHour;
    }

    void EnsureDailyRoutinePlan()
    {
        int day = GetCurrentWorldDay();
        if (routinePlanDay == day)
        {
            return;
        }

        routinePlanDay = day;

        float minHours =
            Mathf.Clamp(dailyCultivationMinHours, 0f, 24f);
        float maxHours =
            Mathf.Clamp(
                Mathf.Max(dailyCultivationMaxHours, minHours),
                minHours,
                24f);
        float duration = Random.Range(minHours, maxHours);
        float latestStart =
            Mathf.Clamp(latestCultivationStartHour, 0f, 23.9f);
        float earliestStart =
            Mathf.Clamp(earliestCultivationHour, 0f, 23.9f);

        if (latestStart < earliestStart)
        {
            latestStart = earliestStart;
        }

        routineCultivationStartHour =
            Random.Range(earliestStart, latestStart);
        routineCultivationEndHour =
            Mathf.Repeat(routineCultivationStartHour + duration, 24f);
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.FloorToInt(Time.time / 900f) + 1;
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            return timeSystem.CurrentHour;
        }

        return Mathf.Repeat(Time.time * 24f / 900f, 24f);
    }

    float GameHoursToSeconds(float gameHours)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float secondsPerDay =
            timeSystem != null
            ? Mathf.Max(1f, timeSystem.realSecondsPerGameDay)
            : 900f;

        return Mathf.Max(0.5f, gameHours * secondsPerDay / 24f);
    }

    float GetRemainingScheduledCultivationSeconds()
    {
        if (!dailyRoutineEnabled)
        {
            return float.PositiveInfinity;
        }

        EnsureDailyRoutinePlan();
        float hour = GetCurrentWorldHour();
        float remainingHours =
            routineCultivationEndHour >= hour
            ? routineCultivationEndHour - hour
            : 24f - hour + routineCultivationEndHour;

        return GameHoursToSeconds(Mathf.Max(0.1f, remainingHours));
    }

    void UpdateNeeds()
    {
        if (IsDead)
        {
            return;
        }

        if (NeedsFood())
        {
            hunger += Time.deltaTime *
                (realm == CultivationRealm.QiRefining ? 0.015f : 0.05f);
        }
        else
        {
            hunger = 0f;
        }

        fatigue += Time.deltaTime * 0.04f;

        if (entityProfile != null)
        {
            entityProfile.needs.hunger = Mathf.Clamp(hunger, 0f, 100f);
            entityProfile.needs.fatigue = Mathf.Clamp(fatigue, 0f, 100f);
        }
    }

    void Eat()
    {
        hunger = 0;

        money -= 5;

        if (money < 0)
        {
            money = 0;
        }

        currentAction = NpcText.Action("eating");
        actionTimer = Mathf.Max(actionTimer, GameHoursToSeconds(0.5f));

        Debug.Log(NpcText.Format(NpcText.Get("logs", "eat"), npcName));
    }

    void Sleep()
    {
        currentAction = NpcText.Action("rest");
        actionTimer = Mathf.Max(actionTimer, GameHoursToSeconds(2f));

        fatigue = 0;

        if (characterStats != null)
        {
            characterStats.currentHP =
                Mathf.Min(
                    characterStats.finalHP,
                    characterStats.currentHP + 30);
            SyncFromCharacterStats();
        }
        else
        {
            currentHP += 30;

            if (currentHP > maxHP)
            {
                currentHP = maxHP;
            }
        }

        Debug.Log(NpcText.Format(NpcText.Get("logs", "sleep"), npcName));
    }

    void Cultivate()
    {
        if (IsDead || IsInDungeonCombatSession())
        {
            return;
        }

        currentAction = NpcText.Action("cultivate");
        float cultivateSeconds =
            GameHoursToSeconds(
                Random.Range(
                    cultivationSessionMinGameHours,
                    cultivationSessionMaxGameHours));
        if (dailyRoutineEnabled)
        {
            cultivateSeconds =
                Mathf.Min(
                    cultivateSeconds,
                    GetRemainingScheduledCultivationSeconds());
        }

        actionTimer = Mathf.Max(actionTimer, cultivateSeconds);

        if (pill > 0)
        {
            pill -= 1;

            int gain =
                Mathf.RoundToInt(
                    30 *
                    GetCultivationMultiplier());

            AddCultivationProgress(gain);

            Debug.Log(NpcText.Format(NpcText.Get("logs", "absorbPill"), npcName, gain));
        }
        else if (spiritStone > 0)
        {
            spiritStone -= 1;

            int gain =
                Mathf.RoundToInt(
                    CultivationProgression.GetSpiritStoneExp(
                        realm,
                        realmStage) *
                    GetCultivationMultiplier());

            AddCultivationProgress(gain);

            Debug.Log(NpcText.Format(NpcText.Get("logs", "absorbSpiritStone"), npcName, gain));
        }

    }

    void CultivateNaturally()
    {
        if (IsDead ||
            waitingForHeavenlyTribulation ||
            IsInDungeonCombatSession())
        {
            return;
        }

        if (actionTimer > 0f &&
            (currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi")))
        {
            return;
        }

        if (TryGoToCultivationPoint())
        {
            return;
        }

        currentTarget = null;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        int gain =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    (1f + (int)realm + realmStage * 0.2f) *
                    Mathf.Max(0.5f, comprehension / 50f) *
                    GetCultivationMultiplier() * 0.1f));

        AddCultivationProgress(gain);
        float cultivateSeconds =
            GameHoursToSeconds(
                Random.Range(
                    cultivationSessionMinGameHours,
                    cultivationSessionMaxGameHours));
        if (dailyRoutineEnabled)
        {
            cultivateSeconds =
                Mathf.Min(
                    cultivateSeconds,
                    GetRemainingScheduledCultivationSeconds());
        }

        actionTimer = Mathf.Max(actionTimer, cultivateSeconds);
        currentAction = NpcText.Action("cultivateAbsorbQi");

    }

    void ClearCompletedCultivationAction()
    {
        if (actionTimer > 0f)
        {
            return;
        }

        if (currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi"))
        {
            currentAction = "";
            SyncCultivationEffect();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    bool TryGoToCultivationPoint()
    {
        if (IsInDungeonCombatSession())
        {
            return false;
        }

        if (currentAction == NpcText.Action("goCultivatePoint") &&
            hasWanderTarget)
        {
            if (Vector2.Distance(transform.position, wanderTarget) <= escapeTargetReachDistance)
            {
                return false;
            }

            currentTarget = null;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            return true;
        }

        if (hasCultivationTarget &&
            IsMoveTargetFeasible(cultivationTarget))
        {
            if (Vector2.Distance(transform.position, cultivationTarget) <= escapeTargetReachDistance)
            {
                return false;
            }

            currentTarget = null;
            wanderTarget = cultivationTarget;
            hasWanderTarget = true;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            currentAction = NpcText.Action("goCultivatePoint");
            return true;
        }

        Transform targetPoint = cultivationPoint;
        Vector3 cultivationPosition =
            targetPoint != null
                ? targetPoint.position
                : Vector3.zero;

        if (targetPoint == null &&
            !NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.Cultivate,
                VillagerJob.None,
                NpcLocationPurpose.Cultivation,
                transform.position,
                out cultivationPosition,
                out _))
        {
            return false;
        }

        if (Vector2.Distance(transform.position, cultivationPosition) <= escapeTargetReachDistance)
        {
            return false;
        }

        currentTarget = targetPoint;
        wanderTarget = cultivationPosition;
        hasWanderTarget = targetPoint == null;
        cultivationTarget = cultivationPosition;
        hasCultivationTarget = true;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        currentAction = NpcText.Action("goCultivatePoint");
        return true;
    }

    void InitializeTaskVisitStagger()
    {
        taskVisitAnchorHour = GetCurrentWorldHour();

        if (!staggerDailyTaskVisits ||
            dailyTaskVisitStaggerHours <= 0f)
        {
            taskVisitDelayHours = 0f;
            return;
        }

        taskVisitDelayHours =
            Random.Range(0f, Mathf.Max(0.1f, dailyTaskVisitStaggerHours));
    }

    bool IsTaskVisitStaggerReady()
    {
        if (!staggerDailyTaskVisits ||
            taskVisitDelayHours <= 0f)
        {
            return true;
        }

        if (taskVisitAnchorHour < 0f)
        {
            InitializeTaskVisitStagger();
        }

        float elapsedHours = GetCurrentWorldHour() - taskVisitAnchorHour;
        if (elapsedHours < 0f)
        {
            elapsedHours += 24f;
        }

        return elapsedHours >= taskVisitDelayHours;
    }

    bool IsLockedRoutineAction(string action)
    {
        return action == NpcText.Action("goTaskProviderDaily") ||
            action == NpcText.Action("goHomeCultivate") ||
            action == NpcText.Action("goCultivatePoint") ||
            action == NpcText.Action("goVanBaoLauBroker") ||
            action == NpcText.Action("goVanBaoLauTask") ||
            action == NpcText.Action("checkedVanBaoLau") ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("goTavern") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("huntMonsterNamed") ||
            action == NpcText.Action("attackMonsterNamed") ||
            action == NpcText.Action("makeFriend") ||
            action == NpcText.Action("createSect") ||
            action == NpcText.Action("goMarketTrade") ||
            action == NpcText.Action("goWorkTask") ||
            action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("pickItem") ||
            action == NpcText.Action("pickHuntEvidence") ||
            action == NpcText.Action("fleeMonsterArea") ||
            action == NpcText.Action("guardSpiritHerbMonster") ||
            action == NpcText.Action("fightBlockingMonster") ||
            action == NpcText.Action("clearHarvestMonster") ||
            action == NpcText.Action("treasureHuntNamed") ||
            action == NpcText.Action("outerSkirmishNamed") ||
            action == NpcText.Action("rest") ||
            action == NpcText.Action("eating") ||
            action == NpcText.Action("injured") ||
            action == NpcText.Action("waitTribulation") ||
            action == NpcText.Action("breakthrough") ||
            action == NpcText.Action("oldAgeDeath");
    }

    bool IsPreservedTravelAction(string action)
    {
        return action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("goHomeCultivate") ||
            action == NpcText.Action("goVanBaoLauBroker") ||
            action == NpcText.Action("goVanBaoLauTask") ||
            action == NpcText.Action("checkedVanBaoLau") ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("goTavern") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("huntMonsterNamed") ||
            action == NpcText.Action("attackMonsterNamed") ||
            action == NpcText.Action("makeFriend") ||
            action == NpcText.Action("createSect") ||
            action == NpcText.Action("goMarketTrade") ||
            action == NpcText.Action("goWorkTask") ||
            action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("pickItem") ||
            action == NpcText.Action("pickHuntEvidence") ||
            action == NpcText.Action("fleeMonsterArea") ||
            action == NpcText.Action("guardSpiritHerbMonster") ||
            action == NpcText.Action("fightBlockingMonster") ||
            action == NpcText.Action("clearHarvestMonster") ||
            action == NpcText.Action("treasureHuntNamed") ||
            action == NpcText.Action("outerSkirmishNamed");
    }

    bool IsInDungeonCombatSession()
    {
        return BicanhSessionManager.IsDungeonParticipant(gameObject);
    }

    bool IgnoresMortalNeeds()
    {
        return realm >= CultivationRealm.Foundation;
    }

    bool NeedsFood()
    {
        return realm < CultivationRealm.Foundation;
    }

    void AddCultivationProgress(int amount)
    {
        if (IsDead || waitingForHeavenlyTribulation)
        {
            return;
        }

        if (characterStats != null)
        {
            characterStats.AddCultivationExp(amount);
            SyncFromCharacterStats();
            return;
        }

        cultivation += amount;

        while (!waitingForHeavenlyTribulation &&
            cultivation >= breakthroughNeed &&
            realm != CultivationRealm.Tribulation)
        {
            cultivation -= breakthroughNeed;
            Breakthrough();
        }
    }

    float GetCultivationMultiplier()
    {
        float multiplier = 1f;

        if (physique ==
            PhysiqueType.MortalBody)
        {
            multiplier = 1f;
        }
        else if (physique ==
            PhysiqueType.FiveElementBody)
        {
            multiplier = 10f;
        }
        else if (physique ==
            PhysiqueType.ChaosBody)
        {
            multiplier = 100f;
        }

        multiplier +=
            comprehension * 0.05f;

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null)
        {
            multiplier *= weather.CultivationMultiplier();
        }

        return multiplier;
    }

    void Breakthrough()
    {
        if (waitingForHeavenlyTribulation)
        {
            return;
        }

        if (characterStats != null)
        {
            characterStats.Breakthrough();
            SyncFromCharacterStats();
            currentAction = characterStats.waitingForHeavenlyTribulation
                ? NpcText.Action("waitTribulation")
                : NpcText.Action("breakthrough");
            return;
        }

        if (realm ==
            CultivationRealm.Tribulation)
        {
            readyForHeavenlyTribulation = true;

            currentAction = NpcText.Action("waitTribulation");

            Debug.Log(NpcText.Format(NpcText.Get("logs", "tribulationReady"), npcName));

            return;
        }

        cultivation = 0;

        if (realm == CultivationRealm.Mortal &&
            realmStage >= CultivationProgression.MaxStage)
        {
            realmStage = 1;
            realm = CultivationRealm.QiRefining;
            ApplyRealmPower(true);
            lifespan = GetLifespanForRealm(realm);
            currentAction = NpcText.Action("breakthrough");
            return;
        }

        if (CultivationProgression.RequiresHeavenlyTribulation(
                realm,
                realmStage))
        {
            CultivationRealm targetRealm =
                CultivationProgression.GetNextRealm(realm);

            waitingForHeavenlyTribulation = true;
            currentAction = NpcText.Action("waitTribulation");
            HeavenlyTribulationSystem.Request(
                gameObject,
                npcName,
                targetRealm,
                () => CompleteMajorBreakthrough(targetRealm));
            return;
        }

        realmStage += 1;

        ApplyRealmPower(true);
        lifespan = GetLifespanForRealm(realm);

        currentAction = NpcText.Action("breakthrough");

        Debug.Log(NpcText.Format(NpcText.Get("logs", "breakthrough"), npcName, GetRealmName(), realmStage));
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
        ApplyRealmPower(true);
        lifespan = GetLifespanForRealm(realm);
        currentAction = NpcText.Action("breakthrough");
        Debug.Log(NpcText.Format(NpcText.Get("logs", "breakthrough"), npcName, GetRealmName(), realmStage));
    }

    void GoToTavernAndBuyPill()
    {
        Transform buyTarget = tavernPoint;
        Vector3 buyPosition =
            buyTarget != null
                ? buyTarget.position
                : Vector3.zero;

        if (buyTarget == null &&
            !NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.BuyGoods,
                VillagerJob.None,
                NpcLocationPurpose.Market,
                transform.position,
                out buyPosition,
                out _))
        {
            currentTarget = null;
            currentAction = NpcText.Action("calm");
            return;
        }

        currentAction = NpcText.Action("goTavern");
        currentTarget = buyTarget;
        wanderTarget = buyPosition;
        hasWanderTarget = buyTarget == null;

        float distance =
            Vector2.Distance(
                transform.position,
                buyPosition);

        if (distance < 1.5f)
        {
            currentAction = NpcText.Action("buyPill");

            money -= 50;

            pill += 1;

            Debug.Log(NpcText.Format(NpcText.Get("logs", "buyPill"), npcName));
        }
    }

    void SearchMonster()
{
    // Neu dang co muc tieu song thi tiep tuc danh.

    if (currentMonsterTarget != null)
    {
        // Bo target neu quai da chet.
        if (currentMonsterTarget.currentHP <= 0)
        {
            currentMonsterTarget = null;

            currentTarget = null;

            return;
        }

        float currentDistance =
            Vector2.Distance(
                transform.position,
                currentMonsterTarget.transform.position);

        // Bo target neu quai ra qua xa.
        if (currentDistance > maxRoamDistance)
        {
            currentMonsterTarget = null;

            currentTarget = null;

            return;
        }

        // Tiep tuc tan cong muc tieu hien tai.
        TryAttackMonster();

        return;
    }

    MonsterAI[] monsters =
        FindObjectsOfType<MonsterAI>();

    MonsterAI bestTarget = null;

    float closestDistance =
        Mathf.Infinity;

    bool hasHuntArea =
        NpcLocationArea.TryGetPosition(
            gameObject,
            NpcScheduleActivity.Hunt,
            VillagerJob.None,
            NpcLocationPurpose.Hunt,
            transform.position,
            out Vector3 huntAreaPosition,
            out _);

    Vector3 huntOrigin =
        hasHuntArea
            ? huntAreaPosition
            : spawnPosition;

    foreach (MonsterAI monster in monsters)
    {
        // Bo qua quai da chet.
        if (monster.currentHP <= 0)
        {
            continue;
        }

        // Kiem tra co nen danh quai nay khong.
        if (!ShouldFightMonster(monster))
        {
            continue;
        }

        // Chi danh quai trong lanh dia cua NPC.
        float distanceFromSpawn =
            Vector2.Distance(
                huntOrigin,
                monster.transform.position);

        if (distanceFromSpawn >
            maxRoamDistance)
        {
            continue;
        }

        // Tinh khoang cach toi quai.
        float distance =
            Vector2.Distance(
                transform.position,
                monster.transform.position);

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
        currentMonsterTarget =
            bestTarget;

        currentTarget =
            bestTarget.transform;

        currentAction = NpcText.ActionFormat("huntMonsterNamed", bestTarget.monsterName);
        hasWanderTarget = false;
        return;
    }

    if (hasHuntArea &&
        Vector2.Distance(transform.position, huntAreaPosition) > escapeTargetReachDistance)
    {
        currentTarget = null;
        wanderTarget = huntAreaPosition;
        hasWanderTarget = true;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        currentAction = NpcText.Action("goHunt");
    }
}

void TryAttackMonster()
{
    if (currentMonsterTarget == null)
    {
        return;
    }

    // Bo target neu quai da chet.
    if (currentMonsterTarget.currentHP <= 0)
    {
        currentMonsterTarget = null;

        currentTarget = null;

        return;
    }

    float distance =
        Vector2.Distance(
            transform.position,
            currentMonsterTarget.transform.position);

    // Chua toi tam danh.
    if (distance > attackRange)
    {
        return;
    }

    // Hoi chieu tan cong.
    if (attackTimer < attackCooldown)
    {
        return;
    }

    // reset cooldown
    attackTimer = 0;

    int attackDamage =
        NpcCombatTechniqueSystem.ModifyOutgoingDamage(
            gameObject,
            currentMonsterTarget.gameObject,
            attack);

    // Gay damage.
    NpcSocialEventBus.PublishHostility(
        gameObject,
        currentMonsterTarget.gameObject,
        Mathf.Clamp(attackDamage, 1, 100),
        currentMonsterTarget.transform.position,
        NpcText.Dialogue("combatMonsterReason"));

    currentAction = NpcText.ActionFormat("attackMonsterNamed", currentMonsterTarget.monsterName);
    actionTimer = Mathf.Max(actionTimer, 0.45f);
    currentMonsterTarget.TakeDamage(attackDamage);

    Debug.Log(NpcText.Format(NpcText.Get("logs", "attackMonster"), npcName, currentMonsterTarget.monsterName, attackDamage));
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
    // Bo qua quai da chet.
    if (monster.currentHP <= 0)
    {
        return false;
    }

    int myPower =
        Mathf.Max(
            1,
            Mathf.RoundToInt(
                CultivationProgression.GetStatPower(
                    realm,
                    realmStage,
                    EntityKind.Cultivator)));

    int monsterPower =
        Mathf.Max(
            1,
            Mathf.RoundToInt(
                CultivationProgression.GetStatPower(
                    monster.realm,
                    monster.realmStage,
                    EntityKind.Beast)));

    int difference =
        monsterPower - myPower;

    float monsterHpPercent =
        (float)monster.currentHP /
        monster.maxHP;

    float myHpPercent =
        (float)currentHP / maxHP;

    // Mau thap thi chay.
    if (myHpPercent <= 0.3f)
    {
        currentAction = NpcText.Action("injured");

        return false;
    }

    // Quai manh hon nhieu.
    if (difference >= 2)
    {
        // Chi danh neu quai gan chet.
        if (monsterHpPercent <= 0.3f)
        {
            return true;
        }

        return false;
    }

    return true;
}
    void MakeFriend()
    {
        currentAction = NpcText.Action("makeFriend");

        Debug.Log(NpcText.Format(NpcText.Get("logs", "makeFriend"), npcName));
    }

    void TryCreateSect()
    {
        if (realm >=
            CultivationRealm.SoulFormation)
        {
            currentAction = NpcText.Action("createSect");

            Debug.Log(NpcText.Format(NpcText.Get("logs", "createSect"), npcName));
        }
    }

    bool TryStartResourceGatheringRoutine()
    {
        if (!canGather)
        {
            return false;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule != null &&
            schedule.enforceSchedule &&
            (schedule.HasStartedCurrentSlotActivity(NpcScheduleActivity.Gather) ||
            schedule.HasCompletedCurrentSlotActivity(NpcScheduleActivity.Gather)))
        {
            return true;
        }

        if (resourceGatherer == null)
        {
            resourceGatherer = GetComponent<NpcResourceGatherer>();
        }

        if (resourceGatherer == null)
        {
            return false;
        }

        resourceGatherer.canGather = true;
        if (resourceGatherer.TryStartGatheringNow())
        {
            return true;
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.Gather,
                VillagerJob.None,
                NpcLocationPurpose.Resource,
                transform.position,
                out Vector3 resourcePosition,
                out _))
        {
            currentTarget = null;
            wanderTarget = resourcePosition;
            hasWanderTarget = true;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            currentAction = NpcText.Action("gatherResource");
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    NpcScheduleActivity.Gather);
            }
            return true;
        }

        return false;
    }

    bool TryStartSellGoodsRoutine()
    {
        if (!canTrade ||
            !canSellGoods)
        {
            return false;
        }

        if (tradeAgent == null)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
        }

        if (tradeAgent == null ||
            tradeAgent.inventory == null ||
            !HasSellableGoods())
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null)
        {
            return false;
        }

        Transform sellTarget =
            broker.customerPoint != null
            ? broker.customerPoint
            : broker.transform;

        if (sellTarget == null)
        {
            return false;
        }

        currentTarget = sellTarget;
        hasWanderTarget = false;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        currentAction = NpcText.Action("tradeSeek");

        if (Vector2.Distance(transform.position, sellTarget.position) >
            Mathf.Max(0.5f, broker.CustomerServiceRadius))
        {
            return true;
        }

        if (NpcCounterBroker.TryTradeWithActiveBroker(tradeAgent))
        {
            actionTimer = GameHoursToSeconds(Random.Range(0.2f, 0.8f));
            currentAction = NpcText.Action("tradeSeek");
        }

        return true;
    }

    bool HasSellableGoods()
    {
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            return false;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.ShouldNpcPreferSell())
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead)
        {
            return;
        }

        if (characterStats != null)
        {
            characterStats.TakeDamage(damage);
            SyncFromCharacterStats();

            if (characterStats.IsDead)
            {
                Die();
            }

            return;
        }

        int finalDamage =
            damage - defense;

        if (finalDamage < 1)
        {
            finalDamage = 1;
        }

        currentHP = Mathf.Clamp(currentHP - finalDamage, 0, maxHP);

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = currentHP;
        }

        if (currentHP > 0)
        {
            NpcCombatTechniqueSystem.ReactToDamageTaken(
                gameObject,
                damage);
        }

        Debug.Log(NpcText.Format(NpcText.Get("logs", "takeDamage"), npcName, finalDamage));

        if (currentHP <= 0)
        {
            Die();
        }
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
        if (characterStats != null)
        {
            characterStats.ApplyItem(item, direction, powerMultiplier);
            SyncFromCharacterStats();
            return;
        }

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

            case StatType.Attack:
            case StatType.Damage:
                attack += intValue;
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

            case StatType.Cultivation:
                if (intValue > 0)
                {
                    AddCultivationProgress(intValue);
                }
                else
                {
                    cultivation =
                        System.Math.Max(0L, cultivation + intValue);
                }
                break;

            case StatType.Breakthrough:
                if (direction > 0)
                {
                    Breakthrough();
                }
                break;

            case StatType.Money:
                money += intValue;
                break;

            case StatType.SpiritStone:
                spiritStone += intValue;
                break;

            case StatType.Pill:
                pill += intValue;
                break;
        }
    }

    void ApplyRealmPower(bool fillHP = false)
    {
        int oldMaxHP = Mathf.Max(1, maxHP);
        float hpPercent = Mathf.Clamp01(currentHP / (float)oldMaxHP);

        float power =
            CultivationProgression.GetStatPower(
                realm,
                realmStage,
                EntityKind.Cultivator);

        maxHP =
            Mathf.Max(1, Mathf.RoundToInt(baseMaxHP * power));

        currentHP =
            fillHP
                ? maxHP
                : Mathf.Clamp(
                    Mathf.RoundToInt(maxHP * hpPercent),
                    0,
                    maxHP);

        attack =
            Mathf.Max(1, Mathf.RoundToInt(baseAttack * power));

        defense =
            Mathf.Max(0, Mathf.RoundToInt(baseDefense * power));

        breakthroughNeed =
            CultivationProgression.GetExpToNextLong(
                realm,
                realmStage,
                100);
    }

    string GetRealmName()
    {
        return NpcText.Realm(realm);
    }

    public int GetAge()
    {
        int baseAge = 0;

        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            baseAge = entityProfile.identity.age;
        }

        if (WorldTimeSystem.Instance != null)
        {
            baseAge += Mathf.Max(0, WorldTimeSystem.Instance.currentYear - 1);
        }

        return baseAge;
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
        currentHP = 0;
        bool preserveInDungeon = BicanhSessionManager.ShouldPreserveDungeonDeath(gameObject);

        if (characterStats != null)
        {
            characterStats.currentHP = 0;
        }

        currentTarget = null;
        currentMonsterTarget = null;
        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        hasTreasureWaitPosition = false;
        hasWanderTarget = false;
        hasObstacleAvoidTarget = false;
        thinkTimer = 0f;
        actionTimer = 0f;
        attackTimer = 0f;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        postTeleportRecoveryUntil = 0f;
        stuckMoveTimer = 0f;
        blockedMoveTimer = 0f;
        currentAction = NpcText.Action("dead");
        UpdateCultivationEffect(false);
        UpdateVisualAnimation();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

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



