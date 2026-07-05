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
public partial class SmartNpcAI : MonoBehaviour, IDamageable
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
    public float sharedTargetSpacingRadius = 0.55f;
    public float sharedTargetOccupancyRadius = 0.3f;

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
    [Range(0f, 24f)] public float taskProviderStartHour = 6f;
    [Range(0f, 24f)] public float taskProviderEndHour = 17f;
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

    [Header("Dia diem")]
    public Transform homePoint;
    public Transform cultivationPoint;

    public Transform tavernPoint;

    public Transform forestPoint;

    public Transform farmPoint;

    [Header("Trang thai hien tai")]
    public string currentAction = "";
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

    public bool IsDead =>
        isDead ||
        (characterStats != null ?
        characterStats.IsDead :
        currentHP <= 0);

    public Transform DamageTransform => transform;
    public bool IsRecoveringFromDamage =>
        Time.time < damageRecoveryUntil;

    void DebugFlow(string stage, string detail)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!ShouldLogDebugFlow())
        {
            return;
        }

        string key =
            stage + "|" +
            detail + "|" +
            currentAction + "|" +
            (currentTarget != null
                ? currentTarget.name
                : hasWanderTarget
                    ? "wander"
                    : "none");

        if (lastDebugFlowKey == key &&
            Time.time - lastDebugFlowTime < 0.75f)
        {
            return;
        }

        lastDebugFlowKey = key;
        lastDebugFlowTime = Time.time;

        float hour =
            WorldTimeSystem.Instance != null
                ? WorldTimeSystem.Instance.CurrentHour
                : -1f;

        Debug.LogWarning(
            "[SmartNpcAI] " + gameObject.name +
            " stage=" + stage +
            " detail=" + detail +
            " action=" + currentAction +
            " task=" + DescribeTask(currentSmartTask) +
            " scheduleTask=" + DescribeTask(scheduleSmartTask) +
            " target=" + (currentTarget != null ? currentTarget.name : "null") +
            " wander=" + hasWanderTarget +
            " homeReturn=" + hasHomeReturnTarget +
            " timer=" + actionTimer.ToString("0.00") +
            " hp=" + currentHP + "/" + maxHP +
            " hour=" + hour.ToString("0.00"));
#endif
    }

    bool ShouldLogDebugFlow()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return debugFlowLogs ||
            IsTrackedDebugNpc();
#else
        return false;
#endif
    }

    bool ShouldTraceRuntime()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!runtimeTraceEnabled &&
            !debugFlowLogs)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(runtimeTraceTarget))
        {
            return true;
        }

        string filter = runtimeTraceTarget.Trim();
        return ContainsIgnoreCase(gameObject.name, filter) ||
            ContainsIgnoreCase(npcName, filter);
#else
        return false;
#endif
    }

    bool ShouldLogStateTransition(
        ref string lastSignature,
        ref float lastLoggedTime,
        string signature,
        float repeatIntervalSeconds)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!ShouldLogDebugFlow())
        {
            return false;
        }

        if (string.IsNullOrEmpty(signature))
        {
            signature = "<empty>";
        }

        if (!string.Equals(
                lastSignature,
                signature,
                System.StringComparison.Ordinal))
        {
            lastSignature = signature;
            lastLoggedTime = Time.time;
            return true;
        }

        if (Time.time - lastLoggedTime >=
            Mathf.Max(0.1f, repeatIntervalSeconds))
        {
            lastLoggedTime = Time.time;
            return true;
        }
#endif

        return false;
    }

    static string QuantizeDebugVector(Vector2 value)
    {
        return Mathf.RoundToInt(value.x * 10f) + "," +
            Mathf.RoundToInt(value.y * 10f);
    }

    void TraceRuntime(
        string function,
        string detail,
        bool force = false)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!force &&
            !ShouldTraceRuntime())
        {
            return;
        }

        runtimeTraceSequence++;

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        NpcScheduleSlot slot =
            schedule != null ? schedule.CurrentSlot : null;
        float hour = GetCurrentWorldHour();
        string slotText =
            slot != null
                ? slot.activity + " " +
                    slot.startHour.ToString("0.##") + "-" +
                    slot.endHour.ToString("0.##")
                : "none";
        string scheduleText =
            schedule != null
                ? schedule.CurrentActivity.ToString()
                : "none";

        Debug.LogWarning(
            "[SmartNpcTrace] seq=" + runtimeTraceSequence +
            " frame=" + Time.frameCount +
            " fn=" + function +
            " detail=" + detail +
            " day=" + (WorldTimeSystem.Instance != null
                ? WorldTimeSystem.Instance.CurrentDay.ToString()
                : "null") +
            " hour=" + hour.ToString("0.00") +
            " slot=" + slotText +
            " schedule=" + scheduleText +
            " action=" + currentAction +
            " target=" + (currentTarget != null ? currentTarget.name : "null") +
            " wander=" + hasWanderTarget +
            " homeReturn=" + hasHomeReturnTarget +
            " monster=" + (currentMonsterTarget != null ? currentMonsterTarget.monsterName : "null") +
            " task=" + DescribeTask(currentSmartTask) +
            " scheduleTask=" + DescribeTask(scheduleSmartTask) +
            " thinkTimer=" + thinkTimer.ToString("0.00") +
            " actionTimer=" + actionTimer.ToString("0.00"));
#endif
    }

    void TraceBranch(
        string function,
        string branch,
        bool result)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        TraceRuntime(function, branch + " => " + (result ? "true" : "false"));
#endif
    }

    string DescribeMonsterMatchup(MonsterAI monster)
    {
        if (monster == null)
        {
            return "matchup=null";
        }

        return "monster=" + monster.monsterName +
            " monsterHp=" + monster.currentHP + "/" + monster.maxHP +
            " " + CombatPowerUtility.DescribeNpcVsMonster(
                gameObject,
                monster.gameObject);
    }

    bool IsTrackedDebugNpc()
    {
        return IsTrackedDebugName(gameObject.name) ||
            IsTrackedDebugName(npcName);
    }

    static bool IsTrackedDebugName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string normalized = NormalizeDebugName(value);
        for (int i = 0; i < TrackedDebugNpcNames.Length; i++)
        {
            if (normalized == TrackedDebugNpcNames[i])
            {
                return true;
            }
        }

        return false;
    }

    static string NormalizeDebugName(string value)
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

        if (runtimeTraceEveryUpdate &&
            ShouldTraceRuntime())
        {
            TraceRuntime("Update", "enter");
        }

        RefreshScheduledStateForCurrentFrame();

        bool busyWithProvider =
            NpcTaskProvider.IsNpcBusyWithAnyProvider(gameObject);
        if (busyWithProvider &&
            !IsRecoveringFromDamage &&
            !HasEmergencySmartTask &&
            currentMonsterTarget == null &&
            !MatchesSmartAction("attackMonsterNamed", true) &&
            !MatchesSmartAction("attackMonster", true))
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (runtimeTraceEveryUpdate)
            {
                TraceRuntime("Update", "busy-by-provider");
            }

            return;
        }

        thinkTimer += Time.deltaTime;
        actionTimer -= Time.deltaTime;

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

        bool holdStationaryAction =
            actionTimer > 0f &&
            (IsStationaryAction(currentAction) ||
            ShouldHoldCombatPosition()) &&
            !HasLockedDirectedTarget() &&
            !hasEscapeTarget &&
            !hasObstacleAvoidTarget;

        if (holdStationaryAction)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            stuckMoveTimer = 0f;
            blockedMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
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

        SyncVisualAnimationDebugFlags();

        Vector2 animationVelocity =
            rb != null
            ? rb.linearVelocity
            : Vector2.zero;

        bool isIdle = animationVelocity.sqrMagnitude <= 0.0025f;
        Vector2 direction = isIdle ? Vector2.zero : animationVelocity.normalized;
        string forcedCombatAnimationAction =
            ResolveForcedCombatAnimationAction();
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
            }
        }
        else if (currentMonsterTarget != null &&
            ShouldHoldCombatPosition())
        {
            Vector2 targetDirection =
                currentMonsterTarget.transform.position -
                transform.position;
            if (targetDirection.sqrMagnitude > 0.0001f)
            {
                direction = targetDirection.normalized;
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
                " idle=" + isIdle +
                " vel=" + animationVelocity +
                " visual=" + (visualAnimation != null) +
                " animAction=" + animationAction);
        }

        visualAnimation.UpdateNPCAnimation(direction, isIdle, animationAction);
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

        if (IsMonsterCombatAnimationAction(currentAction))
        {
            return currentAction;
        }

        if (currentMonsterTarget != null &&
            ShouldHoldCombatPosition())
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

    string ResolvePostTeleportTravelAction()
    {
        if (currentMonsterTarget != null)
        {
            return NpcText.Action("goHunt");
        }

        if (ShouldResumeCultivationTravel())
        {
            return NpcText.Action("goCultivatePoint");
        }

        if (currentSmartTask != null &&
            currentSmartTask.IsValid)
        {
            string taskAction =
                GetRuntimeActionForSmartTask(currentSmartTask);
            if (!string.IsNullOrWhiteSpace(taskAction) &&
                !IsTeleportRouteAction(taskAction))
            {
                return taskAction;
            }
        }

        if (scheduleSmartTask != null &&
            scheduleSmartTask.IsValid)
        {
            string scheduleTaskAction =
                GetRuntimeActionForSmartTask(scheduleSmartTask);
            if (!string.IsNullOrWhiteSpace(scheduleTaskAction) &&
                !IsTeleportRouteAction(scheduleTaskAction))
            {
                return scheduleTaskAction;
            }
        }

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        if (schedule != null &&
            schedule.enforceSchedule)
        {
            string scheduleAction =
                GetScheduleActionTextForDisplay(
                    schedule.CurrentActivity);
            if (!string.IsNullOrWhiteSpace(scheduleAction) &&
                !IsTeleportRouteAction(scheduleAction))
            {
                return scheduleAction;
            }
        }

        if (currentTarget != null ||
            hasWanderTarget)
        {
            return NpcText.Action("walkingRoad");
        }

        return NpcText.Action("idle");
    }

    bool TryRebuildPostTeleportTravelIntent(string restoredAction)
    {
        if (string.IsNullOrEmpty(restoredAction))
        {
            return false;
        }

        if (restoredAction == NpcText.Action("goCultivatePoint"))
        {
            if (currentTarget != null)
            {
                return false;
            }

            if (hasWanderTarget)
            {
                if (TryRefreshPendingCultivationTravelTarget())
                {
                    DebugFlow(
                        "MoveRoute",
                        "Refreshed cultivate travel after teleport restore");
                    return true;
                }

                return false;
            }

            ClearTravelTargetsAndStop();
            hasCultivationTarget = false;

            if (TryGoToCultivationPoint())
            {
                DebugFlow(
                    "MoveRoute",
                    "Rebuilt cultivate travel after teleport restore");
                return true;
            }

            CultivateNaturally();
            if (currentAction == NpcText.Action("cultivate") ||
                currentAction == NpcText.Action("cultivateAbsorbQi"))
            {
                DebugFlow(
                    "MoveRoute",
                    "Resumed cultivate action after teleport restore");
                return true;
            }

            return false;
        }

        if (restoredAction == NpcText.Action("goTaskProviderDaily"))
        {
            if (currentTarget != null ||
                hasWanderTarget)
            {
                return false;
            }

            ClearTravelTargetsAndStop();

            if (TryVisitTaskProvider())
            {
                DebugFlow(
                    "MoveRoute",
                    "Rebuilt task-provider travel after teleport restore");
                return true;
            }

            return false;
        }

        return false;
    }

    string GetRuntimeActionForSmartTask(SmartAITask task)
    {
        if (task == null ||
            !task.IsValid)
        {
            return string.Empty;
        }

        if (task.goal == SmartAITaskGoal.NeedPotion)
        {
            NpcCounterBroker broker = NpcCounterBroker.Active;
            return broker != null &&
                broker.receiveAllNpcRequests
                ? NpcText.Action("goVanBaoLauBroker")
                : NpcText.Action("goTavern");
        }

        if (task.goal == SmartAITaskGoal.Cultivate &&
            ShouldResumeCultivationTravel())
        {
            return NpcText.Action("goCultivatePoint");
        }

        return GetTaskActionTextForDisplay(task);
    }

    ItemInventory GetNpcItemInventory()
    {
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null &&
            tradeAgent != null)
        {
            inventory = tradeAgent.inventory;
        }

        return inventory;
    }

    bool IsCountedPillItem(StatItemData item)
    {
        return item != null &&
            item.itemType == ItemType.DanDuoc &&
            item.CanUseOn(gameObject);
    }

    int CountOwnedPillItems()
    {
        ItemInventory inventory = GetNpcItemInventory();
        if (inventory == null ||
            inventory.items == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack = inventory.items[i];
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !IsCountedPillItem(stack.item))
            {
                continue;
            }

            total += stack.amount;
        }

        return total;
    }

    bool HasAvailablePills()
    {
        if (pill > 0)
        {
            return true;
        }

        int ownedPills = CountOwnedPillItems();
        if (ownedPills > 0)
        {
            pill = Mathf.Max(pill, ownedPills);
            return true;
        }

        return false;
    }

    bool TryConsumeAvailablePill()
    {
        if (!HasAvailablePills())
        {
            return false;
        }

        ItemInventory inventory = GetNpcItemInventory();
        if (inventory != null &&
            inventory.items != null)
        {
            for (int i = 0; i < inventory.items.Count; i++)
            {
                ItemStack stack = inventory.items[i];
                if (stack == null ||
                    stack.item == null ||
                    stack.amount <= 0 ||
                    !IsCountedPillItem(stack.item))
                {
                    continue;
                }

                inventory.RemoveItem(stack.item, 1);
                break;
            }
        }

        pill = Mathf.Max(0, pill - 1);
        return true;
    }

    bool ShouldResumeCultivationTravel()
    {
        if (!canCultivate ||
            IsInDungeonCombatSession())
        {
            return false;
        }

        bool hasCultivateIntent =
            (currentSmartTask != null &&
            currentSmartTask.IsValid &&
            currentSmartTask.goal == SmartAITaskGoal.Cultivate) ||
            (scheduleSmartTask != null &&
            scheduleSmartTask.IsValid &&
            scheduleSmartTask.goal == SmartAITaskGoal.Cultivate);

        if (!hasCultivateIntent)
        {
            NpcScheduleController schedule =
                GetComponent<NpcScheduleController>();
            hasCultivateIntent =
                schedule != null &&
                schedule.enforceSchedule &&
                schedule.CurrentActivity == NpcScheduleActivity.Cultivate;
        }

        if (!hasCultivateIntent)
        {
            return false;
        }

        return IsCultivationTravelPending();
    }

    bool IsCultivationTravelPending()
    {
        float cultivationArriveDistance =
            GetCultivationArriveDistance();
        if (!TryResolveCultivationTravelDestination(
                out _,
                out Vector3 targetPosition))
        {
            return false;
        }

        return Vector2.Distance(transform.position, targetPosition) >
            cultivationArriveDistance;
    }

    bool EnsureTradeAgentReady()
    {
        if (tradeAgent == null)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
        }

        if (tradeAgent == null)
        {
            tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
        }

        if (tradeAgent == null)
        {
            return false;
        }

        if (tradeAgent.inventory == null)
        {
            tradeAgent.inventory = GetComponent<ItemInventory>();
        }

        if (tradeAgent.inventory == null)
        {
            tradeAgent.inventory = gameObject.AddComponent<ItemInventory>();
        }

        return tradeAgent.inventory != null;
    }

    bool TryHandleBrokerPillPurchaseIfReady(
        NpcCounterBroker broker,
        Vector3 approachPosition,
        float extraDistance = 0f)
    {
        if (broker == null ||
            !broker.receiveAllNpcRequests)
        {
            return false;
        }

        float allowedDistance =
            Mathf.Max(0.5f, broker.CustomerServiceRadius) +
            Mathf.Max(0f, extraDistance);
        bool atBroker =
            broker.IsCustomerAtCounter(gameObject) ||
            Vector2.Distance(transform.position, approachPosition) <=
            allowedDistance;

        if (!atBroker)
        {
            return false;
        }

        if (!EnsureTradeAgentReady())
        {
            return false;
        }

        int pillCountBefore = CountOwnedPillItems();
        bool hadPillBefore = HasAvailablePills();

        if (broker.TryTradeWithNpc(tradeAgent))
        {
            int pillCountAfter = CountOwnedPillItems();
            if (pillCountAfter > pillCountBefore)
            {
                pill += pillCountAfter - pillCountBefore;
            }

            ClearTravelTargetsAndStop();
            if (HasAvailablePills() || hadPillBefore)
            {
                ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
                nextNeedPotionRetryTime = 0f;
                actionTimer = Mathf.Max(0.05f, thinkDelay * 0.25f);
                currentAction = NpcText.Action("idle");
            }
            else
            {
                DeferNeedPotionRetry(
                    NpcText.Action("checkedVanBaoLau"));
            }
            return true;
        }

        DeferNeedPotionRetry(
            NpcText.Action("checkedVanBaoLau"));
        return true;
    }

    Vector3 ResolveBrokerApproachPosition(NpcCounterBroker broker)
    {
        if (currentAction == NpcText.Action("goVanBaoLauBroker") &&
            hasWanderTarget)
        {
            return wanderTarget;
        }

        return broker != null
            ? broker.GetCustomerPositionFor(gameObject)
            : transform.position;
    }

    NpcMapZone? ResolveBrokerTargetZone(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return null;
        }

        Transform brokerTarget =
            broker.customerPoint != null
                ? broker.customerPoint
                : broker.transform;

        NpcMapZone? targetZone =
            NpcMapNavigator.GetDestinationZone(brokerTarget);
        if (targetZone.HasValue)
        {
            return targetZone;
        }

        NpcMapArea brokerArea =
            NpcMapArea.FindArea(broker.CustomerPosition);
        if (brokerArea != null)
        {
            return brokerArea.zone;
        }

        return NpcMapNavigator.ResolveActorZone(broker.gameObject);
    }

    bool TryHandleBrokerArrivalFromMovement(Vector3 desiredTarget)
    {
        if (currentAction != NpcText.Action("goVanBaoLauBroker"))
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null ||
            !broker.receiveAllNpcRequests)
        {
            return false;
        }

        Vector3 brokerApproach =
            ResolveBrokerApproachPosition(broker);
        float approachDistance =
            Vector2.Distance(
                transform.position,
                brokerApproach);
        float desiredDistance =
            Vector2.Distance(
                transform.position,
                desiredTarget);
        float arriveDistance =
            Mathf.Max(
                escapeTargetReachDistance,
                broker.CustomerServiceRadius);

        if (approachDistance > arriveDistance &&
            desiredDistance > arriveDistance)
        {
            return false;
        }

        if (TryHandleBrokerPillPurchaseIfReady(
                broker,
                brokerApproach,
                0.15f))
        {
            DebugFlow(
                "Trade",
                "Handled broker arrival approachDist=" +
                approachDistance.ToString("0.00") +
                " desiredDist=" +
                desiredDistance.ToString("0.00") +
                " arrive=" +
                arriveDistance.ToString("0.00"));
            return true;
        }

        return false;
    }

    void UpdateMovement()
    {
        if ((Time.time < movementPausedUntil ||
            Time.time < crowdYieldUntil) &&
            !hasEscapeTarget)
        {
            DebugFlow(
                "Move",
                "Paused movement pausedUntil=" +
                movementPausedUntil.ToString("0.00") +
                " crowdUntil=" +
                crowdYieldUntil.ToString("0.00"));

            if (!TryApplyNpcOverlapSeparation() && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        if (ShouldHoldCombatPosition())
        {
            if (visualAnimation != null &&
                currentMonsterTarget != null)
            {
                visualAnimation.SetFacingTarget(
                    currentMonsterTarget.transform.position);
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            blockedMoveTimer = 0f;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            DebugFlow(
                "Move",
                "Hold combat position target=" +
                (currentMonsterTarget != null
                    ? currentMonsterTarget.monsterName
                    : "null"));
            return;
        }

        bool movingToTreasureWait =
            waitingOutsideTreasureLightning &&
            hasTreasureWaitPosition;
        bool holdPositionWithoutTarget =
            currentTarget == null &&
            !movingToTreasureWait &&
            Time.time >= postTeleportRecoveryUntil &&
            IsStationaryAction(currentAction) &&
            !hasWanderTarget &&
            !hasObstacleAvoidTarget &&
            !hasEscapeTarget;

        if (currentTarget == null &&
            !movingToTreasureWait &&
            holdPositionWithoutTarget)
        {
            string holdDebugSignature =
                "HoldWithoutTarget|" +
                currentAction + "|" +
                (currentSmartTask != null && currentSmartTask.IsValid
                    ? currentSmartTask.goal.ToString()
                    : "None");
            if (ShouldLogStateTransition(
                    ref lastMoveHoldDebugSignature,
                    ref lastMoveHoldDebugTime,
                    holdDebugSignature,
                    3.5f))
            {
                DebugFlow(
                    "Move",
                    "Hold without target action=" + currentAction);
            }

            bool suppressStationarySeparation =
                currentAction == NpcText.Action("buyPill") ||
                currentAction == NpcText.Action("checkedVanBaoLau");

            if (!suppressStationarySeparation &&
                TryApplyNpcOverlapSeparation())
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

        if (hasEscapeTarget)
        {
            desiredTarget = escapeTarget;
        }
        else if (movingToTreasureWait)
        {
            desiredTarget = treasureWaitPosition;
        }
        else if (currentTarget != null)
        {
            desiredTarget = GetApproachPosition(currentTarget);
            hasWanderTarget = false;

            if (currentTarget.GetComponentInParent<NpcTaskProvider>() != null ||
                currentAction == NpcText.Action("goTaskProviderDaily") ||
                currentAction == NpcText.Action("visitedTaskProvider"))
            {
                DebugFlow(
                    "Move",
                    "Approach provider target=" +
                    currentTarget.name +
                    " desired=" +
                    desiredTarget +
                    " pos=" +
                    transform.position +
                    " dist=" +
                    Vector2.Distance(transform.position, desiredTarget).ToString("0.00"));
            }
        }
        else if (hasWanderTarget)
        {
            desiredTarget = wanderTarget;
        }
        else
        {
            if (currentAction == NpcText.Action("goTaskProviderDaily"))
            {
                DebugFlow("Move", "Reached task provider route end");

                if (TryVisitTaskProvider())
                {
                    return;
                }

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

            if (IsTeleportRouteAction(currentAction))
            {
                if (ShouldLogStateTransition(
                        ref lastMoveHoldDebugSignature,
                        ref lastMoveHoldDebugTime,
                        "TeleportRouteEnded|" + currentAction,
                        3.5f))
                {
                    DebugFlow(
                        "Move",
                        "Teleport route ended without follow target");
                }

                currentAction = NpcText.Action("idle");

                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (IsPreservedTravelAction(currentAction))
            {
                if (ShouldLogStateTransition(
                        ref lastMoveHoldDebugSignature,
                        ref lastMoveHoldDebugTime,
                        "PreservedTravel|" + currentAction,
                        3.5f))
                {
                    DebugFlow("Move", "Preserved travel action waiting");
                }

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
                currentAction = NpcText.Action("idle");
            }

            return;
        }

        if (TryHandleBrokerArrivalFromMovement(desiredTarget))
        {
            return;
        }

        if (TryHandleGatherArrivalFromMovement(desiredTarget))
        {
            return;
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

        NpcCounterBroker activeBroker =
            currentAction == NpcText.Action("goVanBaoLauBroker")
                ? NpcCounterBroker.Active
                : null;
        NpcMapZone? forcedTargetZone =
            ResolveBrokerTargetZone(activeBroker);

        bool usingTeleportRoute;
        string routeAction;
        Vector3 moveTarget =
            NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                desiredTarget,
                forcedTargetZone,
                out usingTeleportRoute,
                out routeAction);

        NpcMapArea currentArea = NpcMapArea.FindArea(transform.position);
        NpcMapArea desiredTargetArea = NpcMapArea.FindArea(desiredTarget);
        NpcMapZone? currentZone = NpcMapNavigator.ResolveActorZone(gameObject);
        NpcMapZone? desiredZone = forcedTargetZone ??
            (currentTarget != null
                ? NpcMapNavigator.GetDestinationZone(currentTarget)
                : (NpcMapZone?)null);
        if (!desiredZone.HasValue && desiredTargetArea != null)
        {
            desiredZone = desiredTargetArea.zone;
        }

        if (usingTeleportRoute)
        {
            string routeDebugSignature =
                "TeleportRoute|" +
                routeAction + "|" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                "|" +
                (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                "|" +
                (currentTarget != null ? currentTarget.name : "null") +
                "|" + hasWanderTarget;
            if (ShouldTraceRuntime() &&
                ShouldLogStateTransition(
                    ref lastRouteDebugSignature,
                    ref lastRouteDebugTime,
                    routeDebugSignature,
                    2.5f))
            {
                DebugFlow(
                    "MoveRoute",
                    "Teleport route action=" +
                    routeAction +
                    " currentZone=" +
                    (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                    " currentArea=" +
                    (currentArea != null ? currentArea.name : "null") +
                    " desiredZone=" +
                    (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                    " desiredArea=" +
                    (desiredTargetArea != null ? desiredTargetArea.name : "null") +
                    " moveTarget=" +
                    moveTarget +
                    " desiredTarget=" +
                    desiredTarget +
                    " target=" +
                    (currentTarget != null ? currentTarget.name : "null") +
                    " wander=" +
                    hasWanderTarget);
            }
        }
        else if (IsTeleportRouteAction(currentAction))
        {
            string restoredAction =
                ResolvePostTeleportTravelAction();

            if (TryRebuildPostTeleportTravelIntent(restoredAction))
            {
                return;
            }

            string routeRestoreSignature =
                "TeleportRestore|" +
                currentAction + "|" +
                restoredAction + "|" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                "|" +
                (desiredZone.HasValue ? desiredZone.Value.ToString() : "None");
            if (ShouldTraceRuntime() &&
                ShouldLogStateTransition(
                    ref lastRouteDebugSignature,
                    ref lastRouteDebugTime,
                    routeRestoreSignature,
                    2.5f))
            {
                DebugFlow(
                    "MoveRoute",
                    "Teleport action without route currentAction=" +
                    currentAction +
                    " restoredAction=" +
                    restoredAction +
                    " currentZone=" +
                    (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                    " currentArea=" +
                    (currentArea != null ? currentArea.name : "null") +
                    " desiredZone=" +
                    (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                    " desiredArea=" +
                    (desiredTargetArea != null ? desiredTargetArea.name : "null") +
                    " moveTarget=" +
                    moveTarget +
                    " desiredTarget=" +
                    desiredTarget +
                    " target=" +
                    (currentTarget != null ? currentTarget.name : "null") +
                    " wander=" +
                    hasWanderTarget);
            }

            currentAction = restoredAction;
        }

        if (usingTeleportRoute &&
            !string.IsNullOrEmpty(routeAction))
        {
            currentAction = routeAction;
        }

        if (usingTeleportRoute &&
            TryForceTeleportRouteProgress(moveTarget, currentZone))
        {
            return;
        }

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
            currentAction == NpcText.Action("goCultivatePoint");

        bool isAutonomousWorkRoute =
            currentAction == NpcText.Action("tradeSeek") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goTavern") ||
            currentAction == NpcText.Action("buyPill") ||
            currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);

        bool hasActiveDirectedTarget =
            currentTarget != null ||
            currentMonsterTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget;

        float distanceFromSpawn =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (!movingToTreasureWait &&
            treasureHuntTarget == null &&
            !usingTeleportRoute &&
            targetInSpawnArea &&
            !isCultivationTravelRoute &&
            !hasActiveDirectedTarget &&
            !isAutonomousWorkRoute &&
            distanceFromSpawn > maxRoamDistance)
        {
            DebugFlow(
                "Move",
                "Stopped by roam limit distance=" +
                distanceFromSpawn.ToString("0.00") +
                " max=" +
                maxRoamDistance.ToString("0.00"));

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            hasHomeReturnTarget = false;

            if (!IsStationaryAction(currentAction))
            {
                currentAction = NpcText.Action("idle");
            }

            return;
        }

        if (currentAction == NpcText.Action("goCultivatePoint") &&
            Vector2.Distance(transform.position, desiredTarget) <=
            GetCultivationArriveDistance())
        {
            if (TryRefreshPendingCultivationTravelTarget())
            {
                DebugFlow(
                    "Move",
                    "Refreshed cultivate route after intermediate target");
                return;
            }

            DebugFlow("Move", "Reached cultivate point");

            currentTarget = null;
            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            SyncCultivationEffect();
            DebugFlow("Move", "Arrived at cultivate point, start cultivate immediately");

            CultivateNaturally();
            return;
        }

        if (currentTarget == null &&
            hasWanderTarget &&
            Vector2.Distance(transform.position, desiredTarget) <= escapeTargetReachDistance)
        {
            DebugFlow("Move", "Reached wander target");

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
                if (ShouldBypassCrowdBlockForTravelAction())
                {
                    direction = ApplyCrowdAvoidance(direction);
                    rb.linearVelocity = direction * moveSpeed;
                    UpdateUnstuck(direction);
                    return;
                }

                DebugFlow("Move", "Crowd ahead blocked");

                if (currentTarget != null ||
                    hasWanderTarget ||
                    HasLockedDirectedTarget())
                {
                    if (currentTarget != null &&
                        currentTarget.GetComponentInParent<NpcTaskProvider>() != null)
                    {
                        DebugFlow(
                            "Move",
                            "Crowd blocked near provider target=" +
                            currentTarget.name +
                            " desired=" +
                            desiredTarget +
                            " moveTarget=" +
                            moveTarget +
                            " pos=" +
                            transform.position);
                    }

                    HandleBlockedMovement(moveTarget, desiredTarget);
                }
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
            if (ShouldBypassCrowdBlockForTravelAction())
            {
                direction = ApplyCrowdAvoidance(direction);
                rb.linearVelocity = direction * moveSpeed;
                UpdateUnstuck(direction);
                return;
            }

            DebugFlow("Move", "Crowd ahead blocked with obstacle avoidance");

            if (currentTarget != null ||
                hasWanderTarget ||
                HasLockedDirectedTarget())
            {
                if (currentTarget != null &&
                    currentTarget.GetComponentInParent<NpcTaskProvider>() != null)
                {
                    DebugFlow(
                        "Move",
                        "Crowd blocked with avoidance near provider target=" +
                        currentTarget.name +
                        " desired=" +
                        desiredTarget +
                        " moveTarget=" +
                        moveTarget +
                        " pos=" +
                        transform.position);
                }

                HandleBlockedMovement(moveTarget, desiredTarget);
            }
            return;
        }

        direction = ApplyCrowdAvoidance(direction);

        rb.linearVelocity =
            direction * moveSpeed;

        UpdateUnstuck(direction);
    }

    bool ShouldBypassCrowdBlockForTravelAction()
    {
        return IsTeleportRouteAction(currentAction) ||
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goVanBaoLauBroker") ||
            currentAction == NpcText.Action("goVanBaoLauTask");
    }

    bool TryForceTeleportRouteProgress(
        Vector3 moveTarget,
        NpcMapZone? currentZone)
    {
        if (!currentZone.HasValue)
        {
            return false;
        }

        float forceDistance =
            Mathf.Max(
                1.05f,
                targetClearRadius * 4f,
                moveSpeed * 0.5f);

        if (Vector2.Distance(transform.position, moveTarget) > forceDistance)
        {
            return false;
        }

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                !gate.TryGetTeleportRouteForZone(
                    currentZone.Value,
                    out _,
                    out _,
                    out _) ||
                Vector2.Distance(
                    gate.GetApproachPosition(transform.position),
                    moveTarget) > 0.2f)
            {
                continue;
            }

            if (!gate.TryForceNpcUse(gameObject))
            {
                return false;
            }

            blockedMoveTimer = 0f;
            stuckMoveTimer = 0f;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            DebugFlow(
                "MoveRoute",
                "Forced teleport near gate moveTarget=" +
                moveTarget +
                " gate=" +
                gate.name +
                " currentZone=" +
                currentZone.Value);
            return true;
        }

        return false;
    }

    bool TryHandleGatherArrivalFromMovement(
        Vector3 desiredTarget,
        bool blockedRecovery = false)
    {
        if (currentAction != NpcText.Action("gatherResource") ||
            currentTarget == null)
        {
            return false;
        }

        if (resourceGatherer == null)
        {
            resourceGatherer = GetComponent<NpcResourceGatherer>();
        }

        if (resourceGatherer == null ||
            !resourceGatherer.enabled)
        {
            return false;
        }

        float extraDistance = blockedRecovery
            ? Mathf.Max(targetClearRadius, escapeTargetReachDistance, 0.18f)
            : 0.02f;
        if (!resourceGatherer.TryHandleSmartNpcBlockedArrival(extraDistance))
        {
            return false;
        }

        DebugFlow(
            "Gather",
            (blockedRecovery
                ? "Handled blocked gather arrival"
                : "Handled gather arrival") +
            " target=" +
            currentTarget.name +
            " desired=" +
            desiredTarget +
            " dist=" +
            Vector2.Distance(
                transform.position,
                currentTarget.position).ToString("0.00"));
        return true;
    }

    bool ShouldSuspendAutonomousDamageResponse()
    {
        return NpcTaskProvider.IsNpcBusyWithAnyProvider(gameObject);
    }

    bool TryRecoverBlockedTeleportRoute(
        Vector3 blockedTarget,
        NpcMapZone? currentZone,
        NpcMapArea currentArea)
    {
        if (!currentZone.HasValue ||
            !IsTeleportRouteAction(currentAction))
        {
            return false;
        }

        float recoveryDistance =
            Mathf.Max(
                16f,
                moveSpeed * 8f);

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                !gate.TryGetTeleportRouteForZone(
                    currentZone.Value,
                    out _,
                    out _,
                    out _) ||
                Vector2.Distance(
                    gate.GetApproachPosition(transform.position),
                    blockedTarget) > 0.35f)
            {
                continue;
            }

            NpcMapArea gateArea =
                NpcMapArea.FindArea(gate.GetApproachPosition(transform.position));
            if (currentArea != null &&
                gateArea != null &&
                gateArea != currentArea)
            {
                continue;
            }

            if (Vector2.Distance(
                    transform.position,
                    gate.GetApproachPosition(transform.position)) > recoveryDistance)
            {
                continue;
            }

            if (!gate.TryForceNpcUse(gameObject))
            {
                continue;
            }

            blockedMoveTimer = 0f;
            stuckMoveTimer = 0f;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            DebugFlow(
                "Move",
                "Recovered blocked teleport route via " +
                gate.name +
                " target=" +
                blockedTarget);
            return true;
        }

        return false;
    }

    bool ShouldHoldCombatPosition()
    {
        if (currentMonsterTarget == null ||
            isRetreatingFromMonster ||
            IsDead)
        {
            return false;
        }

        if (currentMonsterTarget.currentHP <= 0)
        {
            return false;
        }

        if (currentTarget != null &&
            currentTarget != currentMonsterTarget.transform)
        {
            return false;
        }

        // When the NPC has already committed to an attack state, freeze in
        // place so the fight reads as a stand-and-swing interaction instead of
        // orbiting around the monster collider.
        if (MatchesSmartAction("attackMonsterNamed", true) ||
            MatchesSmartAction("attackMonster", true) ||
            MatchesSmartAction("attack", true))
        {
            return true;
        }

        float distance =
            GetCombatSurfaceDistance(
                currentMonsterTarget.transform);

        float holdRange =
            Mathf.Max(
                attackRange + 0.55f,
                0.45f);

        return distance <= holdRange;
    }

    bool IsMonsterCombatApproachActive()
    {
        if (currentMonsterTarget == null ||
            currentTarget == null ||
            currentTarget != currentMonsterTarget.transform ||
            isRetreatingFromMonster ||
            IsDead ||
            currentMonsterTarget.currentHP <= 0)
        {
            return false;
        }

        return MatchesSmartAction("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);
    }

    bool MatchesSmartAction(string key, bool allowPrefix = false)
    {
        if (string.IsNullOrEmpty(currentAction) ||
            string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.Equals(
                currentAction,
                key,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string pattern = NpcText.Action(key);
        if (!string.IsNullOrEmpty(pattern) &&
            string.Equals(
                currentAction,
                pattern,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowPrefix)
        {
            return false;
        }

        return MatchesActionKey(
            currentAction,
            key,
            allowPrefix);
    }

    bool MatchesActionKey(
        string action,
        string key,
        bool allowPrefix = false)
    {
        if (string.IsNullOrEmpty(action) ||
            string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.Equals(
                action,
                key,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string pattern = NpcText.Action(key);
        if (!string.IsNullOrEmpty(pattern) &&
            string.Equals(
                action,
                pattern,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowPrefix)
        {
            return false;
        }

        return ActionMatchesPrefix(action, key) ||
            ActionMatchesPrefix(action, pattern);
    }

    static bool ActionMatchesPrefix(string action, string pattern)
    {
        if (string.IsNullOrEmpty(action) ||
            string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        int placeholderIndex = pattern.IndexOf('{');
        if (placeholderIndex < 0)
        {
            return action.StartsWith(
                pattern,
                System.StringComparison.OrdinalIgnoreCase);
        }

        string prefix = pattern.Substring(0, placeholderIndex).TrimEnd();
        return !string.IsNullOrEmpty(prefix) &&
            action.StartsWith(
                prefix,
                System.StringComparison.OrdinalIgnoreCase);
    }

    void OnDisable()
    {
        UpdateCultivationEffect(false);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        UpdateCultivationEffect(false);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        NpcCollisionRegistry.Unregister(this);
        NpcMapNavigator.ClearNpcState(gameObject);
    }

    void OnNpcMapTeleported()
    {
        OnNpcMapTeleported(null);
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        DebugFlow("Teleport", "Teleported gate=" +
            (gateObject != null ? gateObject.name : "null") +
            " source=" +
            (gateObject != null ? "gate" : "fallback") +
            " pos=" +
            transform.position +
            " area=" +
            (NpcMapArea.FindArea(transform.position) != null
                ? NpcMapArea.FindArea(transform.position).name
                : "null"));

        bool preserveTravelState =
            currentMonsterTarget != null ||
            waitingOutsideTreasureLightning ||
            hasTreasureWaitPosition ||
            treasureHuntTarget != null ||
            treasureHuntItem != null ||
            hasHomeReturnTarget;

        spawnPosition = transform.position;       // Đặt lại điểm gốc di chuyển tại map mới
        lastUnstuckPosition = transform.position; // Reset vị trí chống kẹt
        if (!preserveTravelState)
        {
            hasWanderTarget = false;
            currentTarget = null;
            currentMonsterTarget = null;
            ClearMonsterCombatState();
            waitingOutsideTreasureLightning = false;
            hasTreasureWaitPosition = false;
            treasureWaitLowPowerSkirmish = false;
            treasureHuntTarget = null;
            treasureHuntItem = null;
            hasHomeReturnTarget = false;
        }

        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        postTeleportRecoveryUntil = Time.time + 0.35f;
        stuckMoveTimer = 0f;
        blockedMoveTimer = 0f;
        actionTimer = 0f;
        thinkTimer = 0f;
        if (!preserveTravelState)
        {
            currentAction = NpcText.Action("idle");
        }

        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;
        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindArea(gate != null
                ? gate.ExitPosition
                : transform.position);
        }
        NpcMapZone? resolvedZone =
            area != null
                ? area.zone
                : NpcMapNavigator.ResolveActorZone(gameObject);

        if (gate != null)
        {
            if (resolvedZone.HasValue)
            {
                NpcMapNavigator.LockNpcZone(gameObject, resolvedZone.Value, 3f);
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedZone.Value);
            }
            else
            {
                NpcMapNavigator.LockNpcZone(gameObject, gate.toZone, 3f);
                NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
                resolvedZone = gate.toZone;
            }

            NpcMapArea resolvedArea =
                NpcMapNavigator.ResolveMapAreaAfterTeleport(
                    gameObject,
                    resolvedZone.Value,
                    transform.position);

            if (resolvedArea != null &&
                resolvedZone.HasValue &&
                resolvedArea.zone == resolvedZone.Value)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedArea.zone);
            }
        }
        else
        {
            NpcMapArea fallbackArea = NpcMapArea.FindArea(transform.position);
            if (fallbackArea != null)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, fallbackArea.zone);
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
            other.GetComponentInParent<NpcMapMover2D>() == null &&
            other.GetComponentInParent<MonsterAI>() == null)
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
            IsCurrentMonsterCollider(collision.collider))
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
        actionTimer = 0f;

        if (currentTarget == target &&
            currentAction == NpcText.Action("gatherResource"))
        {
            return;
        }

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
        homeReturnTarget = transform.position;
        hasHomeReturnTarget = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
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
            action == NpcText.Action("injured") ||
            action == NpcText.Action("dead") ||
            action == NpcText.Action("oldAgeDeath") ||
            action == NpcText.Action("outerSkirmishNamed") ||
            action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("checkedVanBaoLau") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("waitLightningNamed");
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
                hasEscapeTarget
                ? escapeTarget
                : currentTarget != null
                ? GetApproachPosition(currentTarget)
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

        NpcMapArea currentArea = NpcMapArea.FindArea(transform.position);
        NpcMapArea blockedArea = NpcMapArea.FindArea(blockedTarget);
        NpcMapArea finalArea = NpcMapArea.FindArea(finalTarget);
        NpcMapZone? currentZone = NpcMapNavigator.ResolveActorZone(gameObject);
        NpcMapZone? targetZone =
            currentTarget != null
                ? NpcMapNavigator.GetDestinationZone(currentTarget)
                : (NpcMapZone?)null;
        if (!targetZone.HasValue && finalArea != null)
        {
            targetZone = finalArea.zone;
        }

        bool shouldLogBlockedMovement =
            blockedMoveTimer >= blockedTargetRetryDelay ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget;
        if (shouldLogBlockedMovement)
        {
            DebugFlow(
                "Move",
                "Blocked movement blocked=" +
                blockedTarget +
                " final=" +
                finalTarget +
                " timer=" +
                blockedMoveTimer.ToString("0.00") +
                " escape=" +
                hasEscapeTarget +
                " obstacle=" +
                hasObstacleAvoidTarget +
                " currentZone=" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                " currentArea=" +
                (currentArea != null ? currentArea.name : "null") +
                " blockedArea=" +
                (blockedArea != null ? blockedArea.name : "null") +
                " finalArea=" +
                (finalArea != null ? finalArea.name : "null") +
                " targetZone=" +
                (targetZone.HasValue ? targetZone.Value.ToString() : "None") +
                " target=" +
                (currentTarget != null ? currentTarget.name : "null") +
                " wander=" +
                hasWanderTarget +
                " wanderTarget=" +
                wanderTarget);
            TraceRuntime(
                "HandleBlockedMovement",
                "blockedTarget=" + blockedTarget +
                " finalTarget=" + finalTarget +
                " timer=" + blockedMoveTimer.ToString("0.00") +
                " action=" + currentAction +
                " target=" + (currentTarget != null ? currentTarget.name : "null") +
                " wander=" + hasWanderTarget +
                " wanderTarget=" + wanderTarget);
        }

        if (blockedMoveTimer < blockedTargetRetryDelay)
        {
            if (currentAction == NpcText.Action("goVanBaoLauBroker"))
            {
                NpcCounterBroker broker = NpcCounterBroker.Active;
                Vector3 brokerApproach =
                    broker != null
                        ? ResolveBrokerApproachPosition(broker)
                        : finalTarget;
                if (TryHandleBrokerPillPurchaseIfReady(
                        broker,
                        brokerApproach,
                        0.2f))
                {
                    DebugFlow(
                        "Move",
                        "Blocked near broker, handled trade immediately");
                    blockedMoveTimer = 0f;
                    return;
                }
            }

            if (currentAction == NpcText.Action("goTaskProviderDaily") &&
                TryVisitTaskProvider())
            {
                DebugFlow(
                    "Move",
                    "Blocked near task provider, handled visit immediately");
                TraceRuntime(
                    "HandleBlockedMovement",
                    "handled-near-provider action=" + currentAction +
                    " target=" + (currentTarget != null ? currentTarget.name : "null") +
                    " wander=" + hasWanderTarget);
                blockedMoveTimer = 0f;
            }

            return;
        }

        blockedMoveTimer = 0f;

        if (TryRecoverBlockedTeleportRoute(
                blockedTarget,
                currentZone,
                currentArea))
        {
            return;
        }

        if (currentAction == NpcText.Action("goVanBaoLauBroker"))
        {
            NpcCounterBroker broker = NpcCounterBroker.Active;
            Vector3 brokerApproach =
                broker != null
                    ? ResolveBrokerApproachPosition(broker)
                    : finalTarget;
            if (TryHandleBrokerPillPurchaseIfReady(
                    broker,
                    brokerApproach,
                    0.35f))
            {
                DebugFlow(
                    "Move",
                    "Blocked retry near broker, handled trade immediately");
                return;
            }
        }

        if (currentAction == NpcText.Action("goTaskProviderDaily") &&
            TryVisitTaskProvider())
        {
            DebugFlow(
                "Move",
                "Blocked retry near task provider, handled visit immediately");
            TraceRuntime(
                "HandleBlockedMovement",
                "retry-handled-provider action=" + currentAction +
                " target=" + (currentTarget != null ? currentTarget.name : "null") +
                " wander=" + hasWanderTarget);
            return;
        }

        if (TryHandleGatherArrivalFromMovement(finalTarget, true))
        {
            return;
        }

        if (currentAction == NpcText.Action("gatherResource") &&
            currentTarget != null &&
            TryFindClearPointNear(finalTarget, out Vector3 gatherApproach, false) &&
            Vector2.Distance(transform.position, gatherApproach) >
                escapeTargetReachDistance)
        {
            obstacleAvoidTarget = gatherApproach;
            obstacleAvoidUntil = Time.time + 1.1f;
            hasObstacleAvoidTarget = true;
            hasEscapeTarget = false;
            DebugFlow(
                "Move",
                "Picked gather approach point=" +
                gatherApproach +
                " final=" +
                finalTarget +
                " target=" +
                currentTarget.name);
            return;
        }

        Vector2 escapeDirection =
            (Vector2)finalTarget - (Vector2)transform.position;

        if (TryChooseObstacleDetourDirection(
                escapeDirection,
                finalTarget,
                out Vector2 detourDirection) &&
            TryCommitObstacleAvoidTarget(detourDirection))
        {
            DebugFlow(
                "Move",
                "Committed obstacle detour toward=" +
                detourDirection +
                " final=" +
                finalTarget);
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
            DebugFlow(
                "Move",
                "Picked escape point=" +
                clear +
                " seed=" +
                escapeSeed +
                " final=" +
                finalTarget);
        }
        else
        {
            DebugFlow(
                "Move",
                "Failed to find escape point seed=" +
                escapeSeed +
                " final=" +
                finalTarget);
        }
    }

    bool IsBlockingObstacle(Collider2D hit)
    {
        if (hit == null || hit.isTrigger || IsSelfCollider(hit))
        {
            return false;
        }

        if (IsCurrentTargetCollider(hit) ||
            IsCurrentMonsterCollider(hit))
        {
            return false;
        }

        return hit.GetComponentInParent<VillagerAI>() == null &&
            hit.GetComponentInParent<SmartNpcAI>() == null &&
            hit.GetComponentInParent<NpcMapMover2D>() == null &&
            hit.GetComponentInParent<MonsterAI>() == null;
    }

    bool IsCurrentTargetCollider(Collider2D hit)
    {
        if (hit == null || currentTarget == null)
        {
            return false;
        }

        return hit.transform == currentTarget ||
            hit.transform.IsChildOf(currentTarget);
    }

    bool IsCurrentMonsterCollider(Collider2D hit)
    {
        if (hit == null || currentMonsterTarget == null)
        {
            return false;
        }

        Transform monsterTarget =
            currentMonsterTarget.transform;
        return hit.transform == monsterTarget ||
            hit.transform.IsChildOf(monsterTarget);
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

        if (ShouldBypassCrowdAvoidanceForMonsterCombat())
        {
            return true;
        }

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
        if (ShouldBypassCrowdAvoidanceForMonsterCombat())
        {
            return direction;
        }

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
        if (ShouldBypassCrowdAvoidanceForMonsterCombat())
        {
            return false;
        }

        if (IsTeleportRouteAction(currentAction) ||
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goCultivatePoint") ||
            currentAction == NpcText.Action("goVanBaoLauBroker") ||
            currentAction == NpcText.Action("goVanBaoLauTask"))
        {
            return false;
        }

        if (ignoreNpcBodyCollisions ||
            rb == null ||
            separationRadius <= 0f)
        {
            return false;
        }

        Vector2 separation = GetNpcSeparationDirection();
        if (separation.sqrMagnitude > 0.0001f &&
            !IsMovementBlocked(separation))
        {
            rb.linearVelocity =
                separation.normalized * moveSpeed * 0.65f;
            return true;
        }

        if (TryPickUnstuckEscapeTarget(separation, out escapeTarget))
        {
            hasEscapeTarget = true;
            hasObstacleAvoidTarget = false;
            blockedMoveTimer = 0f;
            DebugFlow(
                "Escape",
                "Commit overlap escape target=" +
                escapeTarget +
                " separation=" +
                separation);
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return true;
        }

        return false;
    }

    Vector3 GetApproachPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        NpcInteractionPoint interactionPoint =
            target.GetComponent<NpcInteractionPoint>();
        if (interactionPoint != null)
        {
            Vector3 standPosition =
                interactionPoint.GetStandPositionFor(gameObject);
            standPosition.z = transform.position.z;
            return standPosition;
        }

        NpcCounterBroker counterBroker =
            target.GetComponentInParent<NpcCounterBroker>();
        if (counterBroker != null)
        {
            Vector3 brokerPosition =
                counterBroker.GetCustomerPositionFor(gameObject);
            brokerPosition.z = transform.position.z;
            return brokerPosition;
        }

        Vector3 targetPosition = target.position;
        if (currentMonsterTarget != null &&
            target == currentMonsterTarget.transform)
        {
            return GetMonsterCombatApproachPosition(target);
        }

        if (!ShouldUseSharedTargetSpacing(target))
        {
            return targetPosition;
        }

        int slotCount = 8;
        int slotIndex = Mathf.Abs(
            gameObject.GetInstanceID() ^
            target.gameObject.GetInstanceID()) % slotCount;
        float spacingRadius = Mathf.Max(
            targetClearRadius * 3f,
            sharedTargetSpacingRadius,
            0.85f);

        return FindOpenSharedTargetSlot(
            targetPosition,
            slotCount,
            slotIndex,
            spacingRadius);
    }

    Vector3 FindOpenSharedTargetSlot(
        Vector3 targetPosition,
        int slotCount,
        int startSlotIndex,
        float spacingRadius)
    {
        Vector3 fallback = targetPosition;

        for (int i = 0; i < slotCount; i++)
        {
            int slotIndex = (startSlotIndex + i) % slotCount;
            float angle = (Mathf.PI * 2f * slotIndex) / slotCount;
            Vector3 candidate =
                targetPosition +
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f) * spacingRadius;

            if (!IsSharedTargetOccupied(candidate))
            {
                return candidate;
            }

            fallback = candidate;
        }

        return fallback;
    }

    Vector3 GetMonsterCombatApproachPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>(true);
        }

        Vector2 fromPosition = transform.position;
        Vector2 away = fromPosition - (Vector2)target.position;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.right;
        }

        float desiredGap =
            Mathf.Max(
                0.28f,
                targetClearRadius * 2f,
                attackRange * 0.3f);

        Vector3 approach =
            target.position +
            (Vector3)(away.normalized * desiredGap);
        approach.z = transform.position.z;
        return approach;
    }

    bool ShouldUseSharedTargetSpacing(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (currentMonsterTarget != null &&
            target == currentMonsterTarget.transform)
        {
            return false;
        }

        if (target.GetComponentInParent<NpcTaskProvider>() != null ||
            target.GetComponentInParent<NpcCounterBroker>() != null)
        {
            return false;
        }

        return target.GetComponentInParent<MonsterAI>() != null;
    }

    bool IsSharedTargetOccupied(Vector3 targetPosition)
    {
        float radius = Mathf.Max(0.18f, sharedTargetOccupancyRadius);
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                targetPosition,
                radius,
                crowdLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || IsSelfCollider(hit))
            {
                continue;
            }

            VillagerAI otherVillager =
                hit.GetComponentInParent<VillagerAI>();
            if (otherVillager != null &&
                otherVillager.gameObject != gameObject &&
                !otherVillager.IsDead)
            {
                return true;
            }

            SmartNpcAI otherCultivator =
                hit.GetComponentInParent<SmartNpcAI>();
            if (otherCultivator != null &&
                otherCultivator.gameObject != gameObject &&
                !otherCultivator.IsDead)
            {
                return true;
            }
        }

        return false;
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
        ThinkBrain();
    }

    void ThinkBrain()
    {
        ThinkBrainCore();
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

        if (TryConsumeAvailablePill())
        {
            int gain =
                Mathf.RoundToInt(
                    50f *
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

        ClearTravelTargetsAndStop();

        int gain =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    50f *
                    GetCultivationMultiplier()));

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
            currentAction = NpcText.Action("idle");
            SyncCultivationEffect();
        }

        StopNpcMovement();
    }

    void ClearCompletedMissionAction()
    {
        if (actionTimer > 0f)
        {
            return;
        }

        ClearTravelTargetsAndStop();

        if (!IsStationaryAction(currentAction))
        {
            currentAction = NpcText.Action("idle");
        }
    }

    void ClearCultivationTravelState()
    {
        bool wasCultivatingAction =
            currentAction == NpcText.Action("goCultivatePoint") ||
            currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi");

        if (IsCultivationTarget(currentTarget))
        {
            currentTarget = null;
        }

        hasCultivationTarget = false;

        if (wasCultivatingAction)
        {
            hasWanderTarget = false;
            if (currentAction != NpcText.Action("idle"))
            {
                currentAction = NpcText.Action("idle");
            }
        }

        UpdateCultivationEffect(false);
    }

    bool TryClearStaleCultivationTravelState()
    {
        if (HasCultivationIntent())
        {
            return false;
        }

        if (!hasCultivationTarget &&
            !IsCultivationTarget(currentTarget) &&
            currentAction != NpcText.Action("goCultivatePoint") &&
            currentAction != NpcText.Action("cultivate") &&
            currentAction != NpcText.Action("cultivateAbsorbQi"))
        {
            return false;
        }

        ClearCultivationTravelState();
        return true;
    }

    bool HasCultivationIntent()
    {
        if (currentSmartTask != null &&
            currentSmartTask.IsValid &&
            currentSmartTask.goal == SmartAITaskGoal.Cultivate)
        {
            return true;
        }

        if (scheduleSmartTask != null &&
            scheduleSmartTask.IsValid &&
            scheduleSmartTask.goal == SmartAITaskGoal.Cultivate)
        {
            return true;
        }

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == NpcScheduleActivity.Cultivate;
    }

    bool IsCultivationTarget(Transform target)
    {
        if (target == null ||
            cultivationPoint == null)
        {
            return false;
        }

        return target == cultivationPoint ||
            target.IsChildOf(cultivationPoint) ||
            cultivationPoint.IsChildOf(target);
    }

    bool TryGoToCultivationPoint()
    {
        if (IsInDungeonCombatSession())
        {
            return false;
        }

        float cultivationArriveDistance =
            GetCultivationArriveDistance();

        if (currentAction == NpcText.Action("goCultivatePoint") &&
            hasWanderTarget)
        {
            if (Vector2.Distance(transform.position, wanderTarget) <=
                cultivationArriveDistance)
            {
                if (TryRefreshPendingCultivationTravelTarget())
                {
                    return true;
                }

                return false;
            }

            ClearTravelTargets(false);
            return true;
        }

        if (!TryResolveCultivationTravelDestination(
                out Transform targetPoint,
                out Vector3 cultivationPosition))
        {
            return false;
        }

        if (Vector2.Distance(transform.position, cultivationPosition) <=
            cultivationArriveDistance)
        {
            return false;
        }

        ClearTravelTargets();
        currentTarget = targetPoint;
        wanderTarget = cultivationPosition;
        hasWanderTarget = targetPoint == null;
        cultivationTarget = cultivationPosition;
        hasCultivationTarget = true;
        currentAction = NpcText.Action("goCultivatePoint");
        TraceRuntime(
            "TryGoToCultivationPoint",
            "set targetPoint=" +
            (targetPoint != null ? targetPoint.name : "null") +
            " cultivationPosition=" +
            cultivationPosition +
            " hasWander=" +
            hasWanderTarget);
        return true;
    }

    float GetCultivationArriveDistance()
    {
        return Mathf.Max(
            escapeTargetReachDistance,
            targetClearRadius * 2f);
    }

    bool TryResolveCultivationTravelDestination(
        out Transform targetPoint,
        out Vector3 cultivationPosition)
    {
        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.Cultivate,
                VillagerJob.None,
                NpcLocationPurpose.Cultivation,
                transform.position,
                out cultivationPosition,
                out _))
        {
            targetPoint = null;
            hasCultivationTarget = false;
            DebugFlow(
                "Cultivate",
                "Use configured cultivation area cultivationPosition=" +
                cultivationPosition +
                " currentPos=" +
                transform.position);
            TraceRuntime(
                "TryResolveCultivationTravelDestination",
                "configured cultivationPosition=" +
                cultivationPosition);
            return true;
        }

        targetPoint = cultivationPoint;
        cultivationPosition =
            targetPoint != null
                ? targetPoint.position
                : Vector3.zero;

        if (targetPoint != null)
        {
            NpcMapZone? destinationZone =
                NpcMapNavigator.GetDestinationZone(targetPoint);
            NpcMapArea targetArea =
                NpcMapArea.FindArea(targetPoint.position);
            if (destinationZone.HasValue &&
                (targetArea == null ||
                targetArea.zone != destinationZone.Value))
            {
                if (NpcLocationArea.TryGetPosition(
                        gameObject,
                        NpcScheduleActivity.Cultivate,
                        VillagerJob.None,
                        NpcLocationPurpose.Cultivation,
                        transform.position,
                        out cultivationPosition,
                        out _))
                {
                    targetPoint = null;
                    DebugFlow(
                        "Cultivate",
                        "Fallback to configured cultivation area targetPoint=null destinationZone=" +
                        destinationZone.Value +
                        " cultivationPosition=" +
                        cultivationPosition +
                        " currentPos=" +
                        transform.position);
                    TraceRuntime(
                        "TryResolveCultivationTravelDestination",
                        "fallback-configured destinationZone=" +
                        destinationZone.Value +
                        " cultivationPosition=" +
                        cultivationPosition);
                    return true;
                }

                NpcMapArea destinationArea =
                    NpcMapArea.FindNearestAreaInZone(
                        destinationZone.Value,
                        targetPoint.position);
                if (destinationArea != null)
                {
                    cultivationPosition =
                        destinationArea.ClosestPoint(targetPoint.position);
                    targetPoint = null;
                    DebugFlow(
                        "Cultivate",
                        "Fallback to nearest area destinationZone=" +
                        destinationZone.Value +
                        " area=" +
                        destinationArea.name +
                        " cultivationPosition=" +
                        cultivationPosition +
                        " currentPos=" +
                        transform.position);
                    TraceRuntime(
                        "TryResolveCultivationTravelDestination",
                        "fallback-nearest destinationZone=" +
                        destinationZone.Value +
                        " area=" +
                        destinationArea.name +
                        " cultivationPosition=" +
                        cultivationPosition);
                    return true;
                }
            }

            DebugFlow(
                "Cultivate",
                "Use direct cultivation target targetPoint=" +
                targetPoint.name +
                " targetZone=" +
                (destinationZone.HasValue
                    ? destinationZone.Value.ToString()
                    : "None") +
                " cultivationPosition=" +
                cultivationPosition);
            return true;
        }

        bool foundCultivationPosition =
            NpcLocationArea.TryGetPosition(
            gameObject,
            NpcScheduleActivity.Cultivate,
            VillagerJob.None,
            NpcLocationPurpose.Cultivation,
            transform.position,
            out cultivationPosition,
            out _);

        DebugFlow(
            "Cultivate",
            foundCultivationPosition
                ? "Use fallback cultivation area cultivationPosition=" +
                    cultivationPosition +
                    " currentPos=" +
                    transform.position
                : "No cultivation destination found currentPos=" +
                    transform.position);

        return foundCultivationPosition;
    }

    bool TryRefreshPendingCultivationTravelTarget()
    {
        float cultivationArriveDistance =
            GetCultivationArriveDistance();

        // Area-based cultivation destinations already use a concrete
        // cached point in `cultivationTarget`. Re-rolling a random point
        // here makes the NPC keep walking around inside the same area
        // instead of starting cultivation when it has effectively arrived.
        if (currentTarget == null &&
            hasWanderTarget &&
            hasCultivationTarget &&
            Vector2.Distance(wanderTarget, cultivationTarget) <=
                cultivationArriveDistance)
        {
            return false;
        }

        if (!TryResolveCultivationTravelDestination(
                out Transform targetPoint,
                out Vector3 cultivationPosition))
        {
            return false;
        }

        if (Vector2.Distance(transform.position, cultivationPosition) <=
            cultivationArriveDistance)
        {
            return false;
        }

        bool alreadyUsingTarget =
            currentTarget == targetPoint &&
            targetPoint != null;
        if (!alreadyUsingTarget &&
            targetPoint == null &&
            hasWanderTarget &&
            Vector2.Distance(wanderTarget, cultivationPosition) <=
                cultivationArriveDistance)
        {
            alreadyUsingTarget = true;
        }

        if (alreadyUsingTarget)
        {
            return false;
        }

        ClearTravelTargets();
        currentTarget = targetPoint;
        wanderTarget = cultivationPosition;
        hasWanderTarget = targetPoint == null;
        cultivationTarget = cultivationPosition;
        hasCultivationTarget = true;
        currentAction = NpcText.Action("goCultivatePoint");
        TraceRuntime(
            "TryRefreshPendingCultivationTravelTarget",
            "refresh targetPoint=" +
            (targetPoint != null ? targetPoint.name : "null") +
            " cultivationPosition=" +
            cultivationPosition +
            " hasWander=" +
            hasWanderTarget);
        return true;
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

        if (action == NpcText.Action("injured") &&
            IsRecoveringFromDamage)
        {
            return true;
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
            action == NpcText.Action("injured") ||
            action == NpcText.Action("waitTribulation") ||
            action == NpcText.Action("breakthrough") ||
            action == NpcText.Action("cultivate") ||
            action == NpcText.Action("cultivateAbsorbQi");
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

    public float GetCultivationMultiplier()
    {
        float multiplier =
            CultivationProgression.GetCultivationRealmMultiplier(
                realm,
                realmStage);

        if (physique ==
            PhysiqueType.MortalBody)
        {
            multiplier *= 1f;
        }
        else if (physique ==
            PhysiqueType.FiveElementBody)
        {
            multiplier *= 10f;
        }
        else if (physique ==
            PhysiqueType.ChaosBody)
        {
            multiplier *= 100f;
        }

        multiplier +=
            Mathf.Max(1f, comprehension / 10f);

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

    bool GoToTavernAndBuyPill()
    {
        if (HasAvailablePills())
        {
            ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
            nextNeedPotionRetryTime = 0f;
            currentAction = NpcText.Action("idle");
            actionTimer = Mathf.Max(0.05f, thinkDelay * 0.25f);
            return true;
        }

        if (IsNeedPotionRetryCoolingDown())
        {
            return false;
        }

        RequestEmergencyTask(
            SmartAITaskGoal.NeedPotion,
            SmartAITaskPriority.Important,
            false,
            "buy pill");

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests)
        {
            Transform brokerTarget =
                broker.customerPoint != null
                ? broker.customerPoint
                : broker.transform;

            if (brokerTarget == null)
            {
                ClearTravelTargetsAndStop();
                ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
                currentAction = NpcText.Action("calm");
                return false;
            }

            currentAction = NpcText.Action("goVanBaoLauBroker");
            ClearTravelTargets();
            Vector3 brokerApproach =
                ResolveBrokerApproachPosition(broker);
            currentTarget = null;
            wanderTarget = brokerApproach;
            hasWanderTarget = true;

            if (TryHandleBrokerPillPurchaseIfReady(
                    broker,
                    brokerApproach))
            {
                return true;
            }

            return true;
        }

        Transform buyTarget = tavernPoint;
        Vector3 buyPosition =
            buyTarget != null
                ? buyTarget.position
                : Vector3.zero;

        if (buyTarget == null &&
            !TryResolveTradeFallbackPosition(
                NpcScheduleActivity.BuyGoods,
                NpcLocationPurpose.BuyGoods,
                out buyPosition))
        {
            ClearTravelTargetsAndStop();
            ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
            currentAction = NpcText.Action("idle");
            TraceRuntime(
                "GoToTavernAndBuyPill",
                "no-destination");
            return false;
        }

        currentAction = NpcText.Action("goTavern");
        ClearTravelTargets();
        currentTarget = buyTarget;
        wanderTarget = buyPosition;
        hasWanderTarget = buyTarget == null;

        Vector3 approachPosition =
            buyTarget != null
                ? GetApproachPosition(buyTarget)
                : buyPosition;
        float distance =
            Vector2.Distance(
                transform.position,
                approachPosition);

        if (ShouldLogDebugFlow())
        {
            DebugFlow(
                "Trade",
                "NeedPotion target=" +
                (buyTarget != null ? buyTarget.name : "wander") +
                " buyPos=" +
                buyPosition +
                " approach=" +
                approachPosition +
                " distance=" +
                distance.ToString("0.00") +
                " action=" +
                currentAction);
        }

        if (distance < 1.5f)
        {
            currentAction = NpcText.Action("buyPill");

            money -= 50;

            pill += 1;
            ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
            nextNeedPotionRetryTime = 0f;

            ClearTravelTargetsAndStop();
            stuckMoveTimer = 0f;
            blockedMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        tradeSessionMinGameHours,
                        tradeSessionMaxGameHours));

            Debug.Log(NpcText.Format(NpcText.Get("logs", "buyPill"), npcName));
        }

        return true;
    }

    bool IsNeedPotionRetryCoolingDown()
    {
        return Time.time < nextNeedPotionRetryTime;
    }

    void DeferNeedPotionRetry(string action)
    {
        nextNeedPotionRetryTime =
            Time.time +
            Mathf.Max(
                thinkDelay * 2f,
                GameHoursToSeconds(0.5f));
        ClearTravelTargetsAndStop();
        ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
        actionTimer = Mathf.Max(thinkDelay, GameHoursToSeconds(0.15f));
        currentAction = action;
    }

    bool TryResolveTradeFallbackPosition(
        NpcScheduleActivity activity,
        NpcLocationPurpose purpose,
        out Vector3 position)
    {
        if (NpcLocationArea.TryGetPosition(
                gameObject,
                activity,
                VillagerJob.None,
                purpose,
                transform.position,
                out position,
                out _))
        {
            return true;
        }

        NpcLocationArea fallbackArea =
            NpcLocationArea.FindBestArea(
                gameObject,
                activity,
                VillagerJob.None,
                NpcLocationPurpose.Any,
                null,
                GetSmartDangerTier(),
                transform.position);

        if (fallbackArea != null)
        {
            position = fallbackArea.GetRandomPoint(gameObject);
            return true;
        }

        position = transform.position;
        return false;
    }

    bool TryStartTradePresenceRoutine()
    {
        if (!canTrade)
        {
            return false;
        }

        if (!TryResolveTradeFallbackPosition(
                NpcScheduleActivity.TradeBuySell,
                NpcLocationPurpose.Market,
                out Vector3 marketPosition) &&
            !TryResolveTradeFallbackPosition(
                NpcScheduleActivity.TradeBuySell,
                NpcLocationPurpose.Any,
                out marketPosition))
        {
            DebugFlow("Trade", "No fallback market area");
            return false;
        }

        float arriveDistance =
            Mathf.Max(
                targetClearRadius * 2f,
                0.45f);

        if (Vector2.Distance(transform.position, marketPosition) <=
            arriveDistance)
        {
            ClearTravelTargetsAndStop();
            actionTimer = Mathf.Max(
                actionTimer,
                GameHoursToSeconds(0.25f));
            currentAction = NpcText.Action("tradeSeek");
            DebugFlow("Trade", "Waiting inside market area");
            return true;
        }

        ClearTravelTargets();
        currentTarget = null;
        wanderTarget = marketPosition;
        hasWanderTarget = true;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        currentAction = NpcText.Action("goMarketTrade");
        DebugFlow("Trade", "Moving to market fallback area");
        return true;
    }

    bool IsPointInsideNpcLocationArea(
        NpcLocationArea area,
        Vector3 point)
    {
        if (area == null)
        {
            return false;
        }

        Collider2D bounds =
            area.areaBounds != null
                ? area.areaBounds
                : area.GetComponent<Collider2D>();

        if (bounds != null)
        {
            return bounds.OverlapPoint(point);
        }

        Vector2 half =
            area.fallbackSize * 0.5f;
        Vector3 center =
            area.transform.position;

        return point.x >= center.x - half.x &&
            point.x <= center.x + half.x &&
            point.y >= center.y - half.y &&
            point.y <= center.y + half.y;
    }

    void SearchMonster()
    {
        NpcLocationArea huntArea =
            NpcLocationArea.FindBestArea(
                gameObject,
                NpcScheduleActivity.Hunt,
                VillagerJob.None,
                NpcLocationPurpose.Hunt,
                null,
                GetSmartDangerTier(),
                transform.position);

        bool hasHuntArea =
            huntArea != null;
        Vector3 huntAreaPosition =
            hasHuntArea
                ? huntArea.transform.position
                : spawnPosition;

        if (currentMonsterTarget != null &&
            !isCounterAttackingMonster &&
            !ShouldSmartAutoHuntMonster(currentMonsterTarget))
        {
            ReleaseMonsterReservation(currentMonsterTarget);
            currentMonsterTarget = null;
            currentTarget = null;
            DebugFlow("Hunt", "Drop non-beast target");
        }

        // Neu dang co muc tieu song thi tiep tuc danh.
        if (currentMonsterTarget != null)
        {
            // Bo target neu quai da chet.
            if (currentMonsterTarget.currentHP <= 0)
            {
                ReleaseMonsterReservation(currentMonsterTarget);
                currentMonsterTarget = null;
                currentTarget = null;
                ClearMonsterCombatState();
                DebugFlow("Hunt", "Current monster died");
                return;
            }

            bool targetStillInHuntArea =
                hasHuntArea
                    ? IsPointInsideNpcLocationArea(
                        huntArea,
                        currentMonsterTarget.transform.position)
                    : Vector2.Distance(
                        spawnPosition,
                        currentMonsterTarget.transform.position) <=
                        maxRoamDistance;

            bool preserveCombatTarget =
                isCounterAttackingMonster ||
                currentHelpRequest != null ||
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

            if (isRetreatingFromMonster)
            {
                currentTarget = null;
                hasWanderTarget = true;
                wanderTarget = retreatTarget;
                currentAction = NpcText.Action("fleeMonsterArea");
                return;
            }

            if (!TargetReservationSystem.Instance.IsReservedByOwner(
                    currentMonsterTarget.gameObject,
                    gameObject) &&
                currentHelpRequest == null &&
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

        MonsterAI bestTarget = null;
        float closestDistance =
            Mathf.Infinity;

        foreach (MonsterAI monster in monsters)
        {
            // Bo qua quai da chet.
            if (!ShouldSmartAutoHuntMonster(monster) ||
                monster.currentHP <= 0)
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

            if (!ShouldFightMonster(monster))
            {
                continue;
            }

            if (TargetReservationSystem.Instance.IsReservedByOther(
                    monster.gameObject,
                    gameObject))
            {
                continue;
            }

            bool monsterInHuntArea =
                hasHuntArea
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
            if (TryReserveMonsterTarget(
                    bestTarget,
                    Mathf.Max(4f, attackCooldown * 4f)))
            {
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
        else
        {
            DebugFlow("Hunt", "No valid monster");
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
        ReleaseMonsterReservation(currentMonsterTarget);
        currentMonsterTarget = null;

        currentTarget = null;
        ClearMonsterCombatState();

        return;
    }

    if (!TargetReservationSystem.Instance.IsReservedByOwner(
            currentMonsterTarget.gameObject,
            gameObject) &&
        currentHelpRequest == null &&
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
        return;
    }

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
        if (!canGather ||
            !canCompeteResource)
        {
            return false;
        }

        if (resourceGatherer == null)
        {
            resourceGatherer = GetComponent<NpcResourceGatherer>();
        }

        if (resourceGatherer != null)
        {
            resourceGatherer.canGather = true;
            resourceGatherer.limitHarvestsPerScheduleSlot = false;
            resourceGatherer.maxHarvestsPerScheduleSlot = 0;
        }

        if (resourceGatherer != null &&
            resourceGatherer.HasActiveGatheringFlow)
        {
            DebugFlow("Gather", "Gatherer already has active flow");
            return true;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        bool gatherCompleted =
            schedule != null &&
            schedule.enforceSchedule &&
            schedule.HasCompletedCurrentSlotActivity(
                NpcScheduleActivity.Gather);
        if (gatherCompleted)
        {
            if (schedule != null &&
                resourceGatherer != null &&
                !resourceGatherer.limitHarvestsPerScheduleSlot)
            {
                schedule.ClearCurrentSlotActivityState(
                    NpcScheduleActivity.Gather);
            }
            else
            {
                DebugFlow("Gather", "Schedule slot already completed");
                return false;
            }
        }

        bool gatherStarted =
            schedule != null &&
            schedule.enforceSchedule &&
            schedule.HasStartedCurrentSlotActivity(
                NpcScheduleActivity.Gather);
        if (gatherStarted)
        {
            bool hasActiveGatherFlow =
                hasWanderTarget ||
                currentTarget != null ||
                currentAction == NpcText.Action("gatherResource") ||
                currentAction == NpcText.Action("pickItem");

            if (hasActiveGatherFlow)
            {
                DebugFlow("Gather", "Continuing active gather flow");
                return true;
            }

            DebugFlow("Gather", "Started flag stale, rebuilding gather flow");
        }

        if (resourceGatherer == null)
        {
            DebugFlow("Gather", "Missing resource gatherer");
            return false;
        }

        if (resourceGatherer.TryStartGatheringNow())
        {
            DebugFlow("Gather", "Start gathering immediately");
            return true;
        }

        bool hasAutonomousGatherCandidate =
            resourceGatherer != null &&
            resourceGatherer.HasAutonomousGatherCandidate(GetSmartDangerTier());

        if (!hasAutonomousGatherCandidate)
        {
            DebugFlow("Gather", "No autonomous gather target, move to resource area");
        }

        if (TryGetSmartResourceArea(out Vector3 resourcePosition))
        {
            ClearTravelTargets();
            wanderTarget = resourcePosition;
            hasWanderTarget = true;
            currentAction = NpcText.Action("gatherResource");
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    NpcScheduleActivity.Gather);
            }
            DebugFlow("Gather", hasAutonomousGatherCandidate
                ? "Move to gather area"
                : "Move to resource area and wait");
            return true;
        }

        DebugFlow("Gather", "No gather target found");
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
            DebugFlow("Sell", "No trade agent or goods");
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null)
        {
            if (TryResolveTradeFallbackPosition(
                NpcScheduleActivity.SellGoods,
                NpcLocationPurpose.SellGoods,
                out Vector3 fallbackSellPosition))
            {
                ClearTravelTargets();
                currentTarget = null;
                wanderTarget = fallbackSellPosition;
                hasWanderTarget = true;
                hasEscapeTarget = false;
                hasObstacleAvoidTarget = false;
                currentAction = NpcText.Action("goMarketTrade");
                DebugFlow("Sell", "No active broker; moving to fallback area");
                return true;
            }

            DebugFlow("Sell", "No active broker");
            return false;
        }

        Transform sellTarget =
            broker.customerPoint != null
            ? broker.customerPoint
            : broker.transform;

        if (sellTarget == null)
        {
            DebugFlow("Sell", "Broker has no sell target");
            return false;
        }

        ClearTravelTargets();
        currentTarget = sellTarget;
        currentAction = NpcText.Action("tradeSeek");

        if (Vector2.Distance(transform.position, sellTarget.position) >
            Mathf.Max(0.5f, broker.CustomerServiceRadius))
        {
            DebugFlow("Sell", "Moving to broker");
            return true;
        }

        if (NpcCounterBroker.TryTradeWithActiveBroker(tradeAgent))
        {
            ClearTravelTargetsAndStop();
            actionTimer = GameHoursToSeconds(Random.Range(0.2f, 0.8f));
            currentAction = NpcText.Action("tradeSeek");
            DebugFlow("Sell", "Trade completed");
        }
        else
        {
            DebugFlow("Sell", "Reached broker but trade failed");
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
        TakeDamage(damage, null);
    }

    public void TakeDamage(int damage, GameObject attackerObject)
    {
        if (IsDead)
        {
            return;
        }

        if (characterStats != null)
        {
            characterStats.TakeDamage(damage);
            SyncFromCharacterStats();

            if (!characterStats.IsDead)
            {
                InterruptGatheringForCombat();
                if (!ShouldSuspendAutonomousDamageResponse())
                {
                    if (!enabled)
                    {
                        enabled = true;
                    }

                    TryCounterAttackFromDamage(attackerObject, damage);
                }
            }

            if (characterStats.IsDead)
            {
                Die();
            }

            return;
        }

        int finalDamage =
            CombatStatCalculator.CalculateFinalDamageInt(
                damage,
                defense);

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

                TryCounterAttackFromDamage(attackerObject, damage);

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
                damage);
        }

        Debug.Log(NpcText.Format(NpcText.Get("logs", "takeDamage"), npcName, finalDamage));

        if (currentHP <= 0)
        {
            Die();
        }
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
        currentAction = NpcText.Action("injured");
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

        MonsterAI bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterAI monster = monsters[i];
            if (monster == null ||
                monster.currentHP <= 0 ||
                !monster.attackSmartNpcs ||
                !ShouldSmartAutoHuntMonster(monster))
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

            if (!ShouldFightMonster(monster))
            {
                continue;
            }

            if (TargetReservationSystem.Instance.IsReservedByOther(
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

        double power =
            CombatStatCalculator.GetRealmMultiplier(
                Mathf.Max(0, (int)realm),
                Mathf.Clamp(realmStage, 1, CultivationProgression.MaxStage) - 1);

        maxHP =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(baseMaxHP * power));

        currentHP =
            fillHP
                ? maxHP
                : Mathf.Clamp(
                    CombatStatCalculator.ClampToInt(maxHP * hpPercent),
                    0,
                    maxHP);

        attack =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(baseAttack * power));

        defense =
            Mathf.Max(
                0,
                CombatStatCalculator.ClampToInt(baseDefense * power));

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




