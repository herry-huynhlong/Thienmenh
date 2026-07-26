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
public partial class SmartNpcAI : MonoBehaviour, IDamageable, INpcActionStateOwner
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
    [Header("Map Session")]
    [Tooltip("Legacy runtime flag used by BicanhSessionManager; behavior rules are resolved through NpcMapBehaviorPolicy.")]
    public bool isBicanhParticipant;

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
    public float bonusMaxHPPercent;
    public float bonusAttackPercent;
    public float bonusDefensePercent;

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
    [Min(1)] public int maxUnstuckRecoveriesBeforeAbort = 3;
    public float escapeTargetReachDistance = 0.18f;
    public float obstacleCheckDistance = 0.35f;
    public float obstacleDetourLookAhead = 0.65f;
    public float targetClearRadius = 0.25f;
    public float navigationClearancePadding = 0.16f;
    public float blockedTargetRetryDelay = 0.8f;
    public bool useObstacleAvoidance = true;
    public bool ignoreNpcBodyCollisions = true;
    public float sharedTargetSpacingRadius = 0.55f;
    public float sharedTargetOccupancyRadius = 0.3f;
    public float movementAcceleration = 8f;
    public float movementDeceleration = 12f;
    public float animationIdleSpeed = 0.03f;

    public Transform currentTarget;
    Transform treasureHuntTarget;
    StatItemData treasureHuntItem;
    bool waitingOutsideTreasureLightning;
    bool treasureWaitLowPowerSkirmish;
    Vector3 treasureWaitPosition;
    bool hasTreasureWaitPosition;

    private Rigidbody2D rb;
    NPCVisualAnimation visualAnimation;
    Vector2 desiredVelocity;
    Collider2D[] selfColliders;
    Vector3 lastUnstuckPosition;
    Vector3 escapeTarget;
    Vector3 obstacleAvoidTarget;
    Vector3 cultivationTravelProgressPosition;
    float obstacleAvoidUntil;
    float stuckMoveTimer;
    float blockedMoveTimer;
    float cultivationTravelProgressTime;
    int unstuckRecoveryAttempts;
    bool hasEscapeTarget;
    bool hasObstacleAvoidTarget;

    [Header("Chien dau")]
    public float attackRange = 1.5f;

    public float attackCooldown = 1f;

    public float unreachableMonsterSeconds = 4f;
    public float unreachableMonsterMoveEpsilon = 0.08f;

    public float deathDestroyDelay = 2f;

    private float attackTimer = 0;

    bool isDead;

    private MonsterAI currentMonsterTarget;
    MonsterAI progressMonsterTarget;
    Vector3 lastMonsterProgressPosition;
    float monsterProgressTime;
    float movementPausedUntil;
    float crowdYieldUntil;
    float crowdDirectionCommitUntil;
    Vector2 crowdCommittedDirection;
    float crowdBlockedTimer;
    float postTeleportRecoveryUntil;
    float damageRecoveryUntil;

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
    [Range(0f, 24f)] public float earliestCultivationHour = 19f;
    [Range(0f, 24f)] public float latestCultivationStartHour = 22f;
    [Header("Daily Tasks")]
    public bool dailyTaskVisitEnabled = true;
    [Range(1, 20)] public int dailyTaskMinCount = 5;
    [Range(1, 20)] public int dailyTaskMaxCount = 7;
    [Range(0f, 24f)] public float taskProviderStartHour = 0f;
    [Range(0f, 24f)] public float taskProviderEndHour = 24f;
    public float cultivationSessionMinGameHours = 1f;
    public float cultivationSessionMaxGameHours = 2f;
    public float tradeSessionMinGameHours = 0.5f;
    public float tradeSessionMaxGameHours = 1.5f;
    public float socialSessionMinGameHours = 0.5f;
    public float socialSessionMaxGameHours = 1.5f;

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
    NpcTaskProvider cachedTaskProviderTarget;

    [Header("Dia diem")]
    public Transform homePoint;
    public Transform cultivationPoint;

    public Transform tavernPoint;

    public Transform forestPoint;

    public Transform farmPoint;

    [Header("Trang thai hien tai")]
    public string currentAction = "";
    public NpcActionId currentActionId = NpcActionId.Unknown;
    public string currentActionKey = "";
    [SerializeField] SmartAITask currentSmartTask = new SmartAITask();
    [SerializeField] SmartAITask scheduleSmartTask = new SmartAITask();

    [Header("Hieu ung tu luyen")]
    public GameObject cultivationEffectPrefab;

    private float thinkTimer = 0;

    public float thinkDelay = 2f;
    float actionTimer;
    int routinePlanDay = int.MinValue;
    float routineCultivationStartHour;
    float routineCultivationEndHour;
    float nextNeedPotionRetryTime;
    int taskRoutineDay = int.MinValue;
    int taskRoutineTargetCount;
    int taskRoutineAcceptedCount;
    Vector3 homeReturnTarget;
    bool hasHomeReturnTarget;
    bool reportedVillagerBrainConflict;
    [Header("Debug")]
    public bool debugFlowLogs;
    [Tooltip("Trace runtime decisions for this NPC only.")]
    public bool runtimeTraceEnabled;
    [Tooltip("Exact name or substring to match for runtime trace. Leave empty to trace this NPC when enabled.")]
    public string runtimeTraceTarget = "";
    [Tooltip("Trace every Update call for the matched NPC.")]
    public bool runtimeTraceEveryUpdate;
    [Tooltip("Log specific reasons when this NPC reaches the forest/hunt flow and stops moving.")]
    public bool debugHuntStallLogs;
    [Min(0.25f)]
    public float huntStallLogIntervalSeconds = 2f;
    [Tooltip("Trace every ThinkBrainCore call for the matched NPC.")]
    public bool runtimeTraceEveryThink = true;
    [Tooltip("Trace schedule slot switches and schedule overrides.")]
    public bool runtimeTraceScheduleChanges = true;
    static readonly string[] TrackedDebugNpcNames =
    {
        "laoba1",
        "satthu1",
        "thusinh33"
    };
    string lastDebugFlowKey;
    float lastDebugFlowTime;
    int runtimeTraceSequence;
    string lastAnimDebugSignature;
    float lastAnimDebugTime;
    string lastRouteDebugSignature;
    float lastRouteDebugTime;
    string lastMoveHoldDebugSignature;
    float lastMoveHoldDebugTime;
    string lastHuntStallSignature;
    float lastHuntStallLogTime;

    public bool IsDead =>
        isDead ||
        (characterStats != null ?
        characterStats.IsDead :
        currentHP <= 0);

    public Transform DamageTransform => transform;
    public bool IsRecoveringFromDamage =>
        Time.time < damageRecoveryUntil;

    void Awake()
    {
        DisableBatchModeDebugOutput();
        EnforceVillagerPrimaryBrain();
        EnsureCharacterStatsHealthSource();
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
        SetCurrentActionState(NpcActionState.FromDisplayText(action));
    }

    public void PlayCombatTechniqueAnimation(Transform target, bool offensive)
    {
        if (visualAnimation == null)
        {
            visualAnimation = NPCVisualAnimation.EnsureOn(gameObject);
        }

        if (visualAnimation == null)
        {
            return;
        }

        if (target != null)
        {
            visualAnimation.SetFacingTarget(target.position);
        }

        string techniqueAction = NpcText.Action("cultivate");
        visualAnimation.ReplayActionAnimation(techniqueAction);
        actionTimer = Mathf.Max(actionTimer, offensive ? 0.3f : 0.2f);
    }

    public NpcActionState CurrentActionState
    {
        get
        {
            NpcActionState resolved =
                NpcActionState.FromDisplayText(currentAction);

            if (resolved.id == NpcActionId.Unknown &&
                !string.IsNullOrWhiteSpace(currentActionKey))
            {
                resolved = NpcActionState.FromKey(currentActionKey);
            }

            if (resolved.id == NpcActionId.Unknown &&
                currentActionId != NpcActionId.Unknown)
            {
                resolved.id = currentActionId;
            }

            if (!string.IsNullOrWhiteSpace(currentAction))
            {
                resolved.displayText = currentAction;
            }

            return resolved;
        }
    }

    public void SetCurrentActionState(NpcActionState state)
    {
        currentActionId = state.id;
        currentActionKey = state.key ?? "";
        currentAction = !string.IsNullOrWhiteSpace(state.displayText)
            ? state.displayText
            : NpcText.Action(currentActionKey);
    }

    void OnEnable()
    {
        DisableBatchModeDebugOutput();
        EnforceVillagerPrimaryBrain();
    }

    void DisableBatchModeDebugOutput()
    {
        if (!Application.isBatchMode)
        {
            return;
        }

        SuppressRuntimeDebugOutput = true;
        debugFlowLogs = false;
        runtimeTraceEnabled = false;
        runtimeTraceEveryUpdate = false;
        runtimeTraceEveryThink = false;
        runtimeTraceScheduleChanges = false;

        if (visualAnimation != null)
        {
            visualAnimation.debugVisualLogs = false;
        }
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
        currentAction = NpcText.Action("idle");
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
            rb.useFullKinematicContacts = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        else if (rb != null)
        {
            rb.useFullKinematicContacts = false;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        selfColliders = GetComponentsInChildren<Collider2D>();
        NpcCollisionRegistry.Register(this, selfColliders);

        spawnPosition = transform.position;
        lastUnstuckPosition = transform.position;
        InitializeDailyTaskRoutine();

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

        if (characterStats == null)
        {
            characterStats = gameObject.AddComponent<CharacterStats>();
        }

        if (entityProfile == null)
        {
            entityProfile = characterStats.entityProfile;
        }

        if (characterStats != null)
        {
            characterStats.generatedEntityKind = EntityKind.Cultivator;
            characterStats.generateFromEntityProfile = generateFromEntityProfile;
            characterStats.entityProfile = entityProfile;

            if (generateFromEntityProfile && entityProfile != null)
            {
                characterStats.ApplyEntityProfile();
            }
            else
            {
                characterStats.realm = realm;
                characterStats.realmStage = realmStage;
                characterStats.cultivationExp = cultivation;
                characterStats.baseMaxHP = Mathf.Max(1, baseMaxHP);
                characterStats.baseAttack = Mathf.Max(1, baseAttack);
                characterStats.baseDefense = Mathf.Max(0, baseDefense);
                characterStats.baseMoveSpeed = Mathf.Max(0f, moveSpeed);
                characterStats.RecalculateStats(false);
                characterStats.RestoreHealthState(maxHP, currentHP);
            }

            SyncFromCharacterStats();
        }
        else
        {
            ApplyRealmPower(!appliedProfile);
        }

        EnsureScheduleController();
        SyncCultivationEffect();
        ResolveInitialObstacleOverlap();
        EnsureLifecycle();
    }

    void EnsureLifecycle()
    {
        NPCIdentity identity =
            GetComponent<NPCIdentity>() ??
            GetComponentInParent<NPCIdentity>(true) ??
            GetComponentInChildren<NPCIdentity>(true);
        if (identity == null)
        {
            identity = gameObject.AddComponent<NPCIdentity>();
        }

        NPCLifecycle lifecycle =
            GetComponent<NPCLifecycle>() ??
            GetComponentInParent<NPCLifecycle>(true) ??
            GetComponentInChildren<NPCLifecycle>(true);
        if (lifecycle == null)
        {
            lifecycle = gameObject.AddComponent<NPCLifecycle>();
        }

        lifecycle.identity = identity;
        lifecycle.entityProfile = entityProfile;
        lifecycle.RefreshAgeNow(true);
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
            if (!Application.isPlaying)
            {
                ApplyRealmPower(false);
            }
            return;
        }

        cultivationEffectPrefab =
            UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Effects/CultivationEffect.prefab");

        if (!Application.isPlaying)
        {
            ApplyRealmPower(false);
        }
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
        else if (NpcGeneratedIdentityProfiles.NeedsMigration(
            entityProfile))
        {
            NpcGeneratedIdentityProfiles.MigrateExistingSmartIdentity(
                entityProfile);
        }
        else
        {
            EntityGenerator.NormalizeCultivatorCombatStats(entityProfile.stats);
        }

        npcName = entityProfile.identity.entityName;
        SyncNpcIdentityFromEntityProfile();
        realm = entityProfile.stats.realm;
        lifespan = GetLifespanForRealm(realm);
        realmStage = entityProfile.stats.realmStage;
        comprehension = entityProfile.talent.comprehension;
        physique = ToPhysique(entityProfile.talent.grade);
        double realmPower =
            CombatStatCalculator.GetRealmMultiplier(
                Mathf.Max(0, (int)realm),
                Mathf.Clamp(realmStage, 1, CultivationProgression.MaxStage) - 1);
        baseMaxHP =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(
                    entityProfile.stats.maxHP / realmPower));
        baseAttack =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(
                    entityProfile.stats.attack / realmPower));
        baseDefense =
            Mathf.Max(
                0,
                CombatStatCalculator.ClampToInt(
                    entityProfile.stats.defense / realmPower));
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

    void SyncNpcIdentityFromEntityProfile()
    {
        if (entityProfile == null || entityProfile.identity == null)
        {
            return;
        }

        NPCIdentity identity =
            GetComponent<NPCIdentity>() ??
            GetComponentInParent<NPCIdentity>(true) ??
            GetComponentInChildren<NPCIdentity>(true);
        if (identity == null && Application.isPlaying)
        {
            identity = gameObject.AddComponent<NPCIdentity>();
        }

        if (identity == null)
        {
            return;
        }

        bool preferNpcIdentity =
            identity.hasBirthAbsoluteDay ||
            identity.age > 0 ||
            !string.IsNullOrWhiteSpace(identity.npcName) ||
            !string.IsNullOrWhiteSpace(identity.fatherId) ||
            !string.IsNullOrWhiteSpace(identity.motherId);

        if (preferNpcIdentity)
        {
            int currentAge = identity.GetCurrentAge();
            if (!string.IsNullOrWhiteSpace(identity.npcName))
            {
                entityProfile.identity.entityName = identity.npcName;
            }

            entityProfile.identity.age = currentAge;
            entityProfile.identity.birthAbsoluteDay =
                identity.birthAbsoluteDay;
            entityProfile.identity.hasBirthAbsoluteDay = true;
            npcName = entityProfile.identity.entityName;
            return;
        }

        int profileAge =
            NpcAgeUtility.GetCurrentAge(entityProfile.identity);
        identity.npcName = entityProfile.identity.entityName;
        identity.age = profileAge;
        identity.birthAbsoluteDay =
            entityProfile.identity.birthAbsoluteDay;
        identity.hasBirthAbsoluteDay = true;
        identity.gender =
            entityProfile.identity.gender == EntityGender.Female
                ? Gender.Female
                : Gender.Male;
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

        if (HeavenlyTribulationSystem.IsTargetLocked(gameObject))
        {
            StopMovingSmooth();
            SetCurrentActionState(NpcActionState.FromKey("waitTribulation"));
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

        if (runtimeTraceEveryUpdate &&
            ShouldTraceRuntime())
        {
            TraceRuntime("Update", "enter");
        }

        RefreshScheduledStateForCurrentFrame();

        if (TryAbortRoutineBlockingFlowForSchedule())
        {
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "aborted-routine-blocking-flow");
            }
        }

        if (ShouldEnforceMapBehaviorPolicy())
        {
            EnforceMapBehaviorPolicyState();
        }

        bool busyWithProvider =
            NpcTaskProvider.IsNpcBusyWithAnyProvider(gameObject);
        if (busyWithProvider &&
            !IsRecoveringFromDamage &&
            !HasEmergencySmartTask &&
            currentMonsterTarget == null &&
            !MatchesSmartAction("attackMonsterNamed", true) &&
            !MatchesSmartAction("attackMonster", true))
        {
            StopMovingSmooth();
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "busy-by-provider");
            }

            LogHuntStall("busy-by-provider");

            return;
        }

        thinkTimer += Time.deltaTime;
        actionTimer -= Time.deltaTime;
        ClearStaleRecoveryAction();

        attackTimer += Time.deltaTime;

        if (TryAbortIncompatibleHuntFlowForSchedule())
        {
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "aborted-incompatible-hunt-flow");
            }

            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        // Retreat arrival/expiry must be evaluated before the generic
        // currentMonsterTarget branch below. Otherwise SearchMonster keeps
        // recreating the same flee target and the NPC can remain parked at
        // an already reached safe point indefinitely.
        if (isRetreatingFromMonster && TryHandleCombatSupport())
        {
            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        if (waitingOutsideTreasureLightning)
        {
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "treasure-wait");
            }

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
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "treasure-hunt");
            }

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
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "cooldown actionTimer=" + actionTimer.ToString("0.00"));
            }

            LogHuntStall("action-timer-cooldown");

            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        bool combatAttackActive =
            currentMonsterTarget != null &&
            (MatchesSmartAction("attackMonsterNamed", true) ||
            MatchesSmartAction("attackMonster", true) ||
            ShouldHoldCombatPosition());

        if (combatAttackActive)
        {
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "preserve-combat-attack");
            }

            TryAttackMonster();
            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        if (currentMonsterTarget != null ||
            MatchesSmartAction("huntMonsterNamed", true))
        {
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "preserve-hunt-travel");
            }

            SearchMonster();
            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        if (TryClearStaleCultivationTravelState())
        {
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "cleared-stale-cultivation-travel");
            }
        }

        if (TryClearStaleScheduledDirectedState())
        {
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "cleared-stale-scheduled-directed-state");
            }
        }

        if (HasLockedDirectedTarget())
        {
            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "locked-directed-target");
            }

            LogHuntStall("locked-directed-target");

            if (canLive)
            {
                UpdateNeeds();
            }

            return;
        }

        if (thinkTimer >= thinkDelay)
        {
            thinkTimer = 0;

            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "Think()");
            }

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
            StopMovingSmooth(true);
            return;
        }

        RefreshCurrentMapArea();
        ClampInsideCurrentMapArea();

        if (HeavenlyTribulationSystem.IsTargetLocked(gameObject))
        {
            StopMovingSmooth();
            ApplySmoothVelocity();
            ClampInsideCurrentMapArea();
            UpdateVisualAnimation();
            return;
        }

        if (IsDead)
        {
            StopMovingSmooth(true);
            ClampInsideCurrentMapArea();
            UpdateVisualAnimation();
            return;
        }

        bool holdStationaryAction =
            actionTimer > 0f &&
            ((IsStationaryAction(currentAction) &&
            !IsMonsterCombatApproachActive()) ||
            ShouldHoldCombatPosition()) &&
            !HasLockedDirectedTarget() &&
            !hasEscapeTarget &&
            !hasObstacleAvoidTarget;

        if (holdStationaryAction)
        {
            StopMovingSmooth();
            stuckMoveTimer = 0f;
            blockedMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            ApplySmoothVelocity();
            ClampInsideCurrentMapArea();
            UpdateVisualAnimation();
            return;
        }

        UpdateMovement();
        ApplySmoothVelocity();
        ClampInsideCurrentMapArea();
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

        SyncVisualAnimationDebugFlags();

        NpcVisualMotionState motionState =
            NpcVisualMotionResolver.Resolve(
                rb != null ? rb.linearVelocity : desiredVelocity,
                desiredVelocity,
                animationIdleSpeed,
                allowDesiredVelocityFallback: false);
        Vector2 animationVelocity =
            motionState.AnimationVelocity;
        bool isIdle = motionState.IsIdle;
        bool hasDesiredMotion =
            motionState.HasDesiredMotion;
        Vector2 velocityDirection =
            motionState.LocomotionDirection;
        Vector2 direction = velocityDirection;
        string visualDirectionSource =
            motionState.UsedDesiredVelocityFallback
                ? "desired-velocity"
                : isIdle
                ? "idle"
                : "velocity";

        if (isIdle &&
            !hasDesiredMotion &&
            IsStationaryAction(currentAction) &&
            direction.sqrMagnitude <= 0.0001f)
        {
            Vector2 idleLookDirection =
                ResolveIdleVisualFacingDirection();
            if (idleLookDirection.sqrMagnitude > 0.0001f)
            {
                direction = idleLookDirection;
                visualDirectionSource = "idle-look";
            }
        }

        bool holdCombatPosition =
            currentMonsterTarget != null &&
            ShouldHoldCombatPosition();
        string forcedCombatAnimationAction =
            (isIdle || holdCombatPosition)
                ? ResolveForcedCombatAnimationAction()
                : string.Empty;
        if (isIdle &&
            !string.IsNullOrWhiteSpace(forcedCombatAnimationAction) &&
            currentMonsterTarget != null)
        {
            Vector2 targetDirection =
                currentMonsterTarget.transform.position -
                transform.position;
            if (targetDirection.sqrMagnitude > 0.0001f)
            {
                direction = targetDirection.normalized;
                visualDirectionSource = "combat-target-idle";
            }
        }
        else if (holdCombatPosition)
        {
            Vector2 targetDirection =
                currentMonsterTarget.transform.position -
                transform.position;
            if (targetDirection.sqrMagnitude > 0.0001f)
            {
                direction = targetDirection.normalized;
                visualDirectionSource = "combat-target-hold";
            }
        }

        string animationAction =
            ResolveAnimationActionForCurrentState(
                isIdle,
                forcedCombatAnimationAction);

        string animDebugSignature =
            currentAction + "|" +
            animationAction + "|" +
            isIdle + "|" +
            QuantizeDebugVector(direction);
        if (ShouldTraceRuntime() &&
            ShouldLogStateTransition(
                ref lastAnimDebugSignature,
                ref lastAnimDebugTime,
                animDebugSignature,
                2.5f))
        {
            DebugFlow(
                "Anim",
                "Update visual dir=" +
                direction +
                " source=" + visualDirectionSource +
                " idle=" + isIdle +
                " vel=" + animationVelocity +
                " velDir=" + velocityDirection +
                " desiredVel=" + desiredVelocity +
                " pos=" + transform.position +
                " rbPos=" + (rb != null ? rb.position.ToString() : "no-rb") +
                " target=" +
                (currentTarget != null ? currentTarget.name : "null") +
                " wander=" + hasWanderTarget +
                " avoid=" + hasObstacleAvoidTarget +
                " escape=" + hasEscapeTarget +
                " pauseUntil=" + movementPausedUntil.ToString("0.00") +
                " crowdUntil=" + crowdYieldUntil.ToString("0.00") +
                " visual=" + (visualAnimation != null) +
                " animAction=" + animationAction);
        }

        visualAnimation.UpdateNPCAnimation(direction, isIdle, animationAction);
    }

    Vector2 ResolveIdleVisualFacingDirection()
    {
        if (currentTarget != null)
        {
            return NormalizeVisualDirection(
                GetApproachPosition(currentTarget) - transform.position);
        }

        if (IsTaskProviderTargetUsable(cachedTaskProviderTarget))
        {
            Vector3 providerPosition =
                cachedTaskProviderTarget.GetProviderPositionFor(gameObject);
            float providerDistance =
                Vector2.Distance(transform.position, providerPosition);
            float maxLookDistance =
                Mathf.Max(
                    3f,
                    cachedTaskProviderTarget.GetProviderInteractionDistance() +
                    1f);
            if (providerDistance <= maxLookDistance)
            {
                return NormalizeVisualDirection(
                    providerPosition - transform.position);
            }
        }

        return Vector2.zero;
    }

    Vector2 NormalizeVisualDirection(Vector3 delta)
    {
        Vector2 planarDelta = new Vector2(delta.x, delta.y);
        return planarDelta.sqrMagnitude > 0.0001f
            ? planarDelta.normalized
            : Vector2.zero;
    }

    void SyncVisualAnimationDebugFlags()
    {
        if (visualAnimation == null)
        {
            return;
        }

        visualAnimation.debugVisualLogs =
            debugFlowLogs ||
            runtimeTraceEnabled;
    }

    string ResolveAnimationActionForCurrentState(
        bool isIdle,
        string forcedCombatAnimationAction = null)
    {
        if (IsDead)
        {
            return string.IsNullOrWhiteSpace(currentAction)
                ? NpcText.Action("dead")
                : currentAction;
        }

        if (currentAction == NpcText.Action("dead") ||
            currentAction == NpcText.Action("oldAgeDeath"))
        {
            return currentAction;
        }

        forcedCombatAnimationAction =
            string.IsNullOrWhiteSpace(forcedCombatAnimationAction)
                ? ResolveForcedCombatAnimationAction()
                : forcedCombatAnimationAction;
        if (!string.IsNullOrWhiteSpace(forcedCombatAnimationAction))
        {
            return forcedCombatAnimationAction;
        }

        bool holdCombatPosition =
            currentMonsterTarget != null &&
            ShouldHoldCombatPosition();

        if (IsMonsterCombatAnimationAction(currentAction))
        {
            if (!isIdle &&
                !holdCombatPosition)
            {
                return string.Empty;
            }

            return currentAction;
        }

        if (holdCombatPosition)
        {
            string targetName =
                currentMonsterTarget != null
                    ? currentMonsterTarget.monsterName
                    : string.Empty;
            return string.IsNullOrWhiteSpace(targetName)
                ? NpcText.Action("attackMonsterNamed")
                : NpcText.ActionFormat("attackMonsterNamed", targetName);
        }

        if (!isIdle)
        {
            return string.Empty;
        }

        return currentAction;
    }

    string ResolveForcedCombatAnimationAction()
    {
        if (!ShouldForceCombatAttackAnimation())
        {
            return string.Empty;
        }

        if (MatchesSmartAction("attackMonsterNamed", true) ||
            MatchesSmartAction("attackMonster", true) ||
            MatchesSmartAction("attack", true))
        {
            return currentAction;
        }

        string targetName =
            currentMonsterTarget != null
                ? currentMonsterTarget.monsterName
                : string.Empty;

        return string.IsNullOrWhiteSpace(targetName)
            ? NpcText.Action("attackMonsterNamed")
            : NpcText.ActionFormat("attackMonsterNamed", targetName);
    }

    bool ShouldForceCombatAttackAnimation()
    {
        if (currentMonsterTarget == null ||
            isRetreatingFromMonster ||
            currentMonsterTarget.currentHP <= 0)
        {
            return false;
        }

        if (!IsMonsterCombatAnimationAction(currentAction))
        {
            return false;
        }

        float distance =
            GetCombatSurfaceDistance(
                currentMonsterTarget.transform);

        float forceRange =
            Mathf.Max(
                attackRange + 0.6f,
                0.55f);

        return distance <= forceRange;
    }

    bool IsMonsterCombatAnimationAction(string action)
    {
        return MatchesActionKey(action, "attackMonsterNamed", true) ||
            MatchesActionKey(action, "attackMonster", true) ||
            MatchesActionKey(action, "attack", true) ||
            MatchesActionKey(action, "huntMonsterNamed", true) ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("fightBlockingMonster") ||
            action == NpcText.Action("guardSpiritHerbMonster") ||
            action == NpcText.Action("clearHarvestMonster");
    }

    bool ShouldBypassCrowdAvoidanceForMonsterCombat()
    {
        if (currentMonsterTarget == null ||
            isRetreatingFromMonster ||
            currentMonsterTarget.currentHP <= 0)
        {
            return false;
        }

        if (!IsMonsterCombatApproachActive() &&
            !ShouldHoldCombatPosition() &&
            !MatchesSmartAction("attackMonsterNamed", true) &&
            !MatchesSmartAction("huntMonsterNamed", true) &&
            currentAction != NpcText.Action("goHunt"))
        {
            return false;
        }

        return true;
    }

    void Think()
    {
        ThinkBrain();
    }

    void ThinkBrain()
    {
        ThinkBrainCore();
    }

    void InitializeDailyTaskRoutine()
    {
        taskRoutineDay = int.MinValue;
        taskRoutineTargetCount = 0;
        taskRoutineAcceptedCount = 0;
        EnsureDailyTaskRoutine();
    }

    void EnsureDailyTaskRoutine()
    {
        int day = GetCurrentWorldDay();
        if (taskRoutineDay == day)
        {
            return;
        }

        taskRoutineDay = day;
        taskRoutineAcceptedCount = 0;

        int minCount = Mathf.Max(1, dailyTaskMinCount);
        int maxCount = Mathf.Max(minCount, dailyTaskMaxCount);
        taskRoutineTargetCount = Random.Range(minCount, maxCount + 1);
    }

    bool HasDailyTaskQuotaRemaining()
    {
        EnsureDailyTaskRoutine();
        return taskRoutineAcceptedCount < taskRoutineTargetCount;
    }

    string GetDailyTaskQuotaDebugText()
    {
        EnsureDailyTaskRoutine();
        return taskRoutineAcceptedCount + "/" + taskRoutineTargetCount;
    }

    bool IsTaskProviderWindow()
    {
        float hour = GetCurrentWorldHour();
        float start = Mathf.Repeat(taskProviderStartHour, 24f);
        float end = Mathf.Repeat(taskProviderEndHour, 24f);

        if (Mathf.Approximately(start, end))
        {
            return true;
        }

        if (start < end)
        {
            return hour >= start && hour < end;
        }

        return hour >= start || hour < end;
    }

    bool CanVisitTaskProviderToday()
    {
        return dailyTaskVisitEnabled &&
            HasDailyTaskQuotaRemaining() &&
            IsTaskProviderWindow();
    }

    bool HasActiveEnforcedScheduleSlot()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentSlot != null;
    }

    void MarkDailyTaskAccepted()
    {
        EnsureDailyTaskRoutine();
        taskRoutineAcceptedCount = Mathf.Min(
            taskRoutineTargetCount,
            taskRoutineAcceptedCount + 1);
    }

    bool IsLockedRoutineAction(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        if (HasLockedDirectedTarget())
        {
            return true;
        }

        if (action == NpcText.Action("oldAgeDeath"))
        {
            return true;
        }

        if (IsBlockingInjuredAction(action))
        {
            return true;
        }

        if (IsStaleRecoveryAction(action))
        {
            return false;
        }

        if (actionTimer <= 0f)
        {
            return false;
        }

        return action == NpcText.Action("checkedVanBaoLau") ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("makeFriend") ||
            action == NpcText.Action("createSect") ||
            action == NpcText.Action("rest") ||
            action == NpcText.Action("eating") ||
            action == NpcText.Action("waitTribulation") ||
            action == NpcText.Action("breakthrough") ||
            action == NpcText.Action("cultivate") ||
            action == NpcText.Action("cultivateAbsorbQi");
    }

    void ClearStaleRecoveryAction()
    {
        if (!IsStaleRecoveryAction(currentAction))
        {
            return;
        }

        if (currentActionKey == "injured")
        {
            currentActionKey = string.Empty;
        }

        if (currentActionId == NpcActionId.Injured)
        {
            currentActionId = NpcActionId.Unknown;
        }

        currentAction = string.Empty;
        actionTimer = 0f;
    }

    bool IsStaleRecoveryAction(string action)
    {
        if (action != NpcText.Action("injured"))
        {
            return false;
        }

        if (IsRecoveringFromDamage)
        {
            return false;
        }

        return !IsLowHpRecoveryTaskActive();
    }

    bool IsPreservedTravelAction(string action)
    {
        return action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("goVanBaoLauBroker") ||
            action == NpcText.Action("goVanBaoLauTask") ||
            action == NpcText.Action("checkedVanBaoLau") ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("goTavern") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("goHunt") ||
            MatchesActionKey(action, "huntMonsterNamed", true) ||
            MatchesActionKey(action, "attackMonsterNamed", true) ||
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
            action == NpcText.Action("walkingRoad") ||
            IsTeleportRouteAction(action);
    }

    bool IsTeleportRouteAction(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        string teleportPrefix =
            NpcText.Action("teleportGateTo").Replace("{0}", "");
        return action.StartsWith(teleportPrefix) ||
            action.StartsWith("Đi cổng dịch chuyển");
    }

    bool IsRestrictedMapSessionActive()
    {
        return NpcMapBehaviorPolicy.IsRestrictedSessionParticipant(gameObject);
    }

    bool ShouldEnforceMapBehaviorPolicy()
    {
        return IsRestrictedMapSessionActive() ||
            !NpcMapBehaviorPolicy.AllowsNormalWorldTravel(gameObject) ||
            !NpcMapBehaviorPolicy.AllowsSchedule(gameObject);
    }

    bool IgnoresMortalNeeds()
    {
        return realm >= CultivationRealm.Foundation;
    }

    bool NeedsFood()
    {
        return realm < CultivationRealm.Foundation;
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
        EnsureCharacterStatsHealthSource();

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

            case StatType.MaxHPPercent:
                bonusMaxHPPercent += floatValue;
                ApplyRealmPower();
                break;

            case StatType.CurrentHP:
                currentHP += intValue;
                break;

            case StatType.Attack:
            case StatType.Damage:
                attack += intValue;
                break;

            case StatType.AttackPercent:
                bonusAttackPercent += floatValue;
                ApplyRealmPower();
                break;

            case StatType.Defense:
                defense += intValue;
                break;

            case StatType.DefensePercent:
                bonusDefensePercent += floatValue;
                ApplyRealmPower();
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

        double power =
            CombatStatCalculator.GetRealmMultiplier(
                Mathf.Max(0, (int)realm),
                Mathf.Clamp(realmStage, 1, CultivationProgression.MaxStage) - 1);

        double scaledMaxHp =
            CombatStatCalculator.ClampToInt(baseMaxHP * power) *
            (1d + bonusMaxHPPercent);
        maxHP =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(scaledMaxHp));

        currentHP =
            fillHP
                ? maxHP
                : Mathf.Clamp(
                    CombatStatCalculator.ClampToInt(maxHP * hpPercent),
                    0,
                    maxHP);

        double scaledAttack =
            CombatStatCalculator.ClampToInt(baseAttack * power) *
            (1d + bonusAttackPercent);
        attack =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(scaledAttack));

        double scaledDefense =
            CombatStatCalculator.ClampToInt(baseDefense * power) *
            (1d + bonusDefensePercent);
        defense =
            Mathf.Max(
                0,
                CombatStatCalculator.ClampToInt(scaledDefense));

        breakthroughNeed =
            CultivationProgression.GetExpToNextLong(
                realm,
                realmStage,
                100);
    }

}




