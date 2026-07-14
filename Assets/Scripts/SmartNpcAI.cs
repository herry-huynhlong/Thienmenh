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
    int unstuckRecoveryAttempts;
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
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

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

        if (HeavenlyTribulationSystem.IsTargetLocked(gameObject))
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            UpdateVisualAnimation();
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
        waitingForHeavenlyTribulation =
            characterStats.waitingForHeavenlyTribulation;
        maxHP = characterStats.finalHP;
        currentHP = characterStats.currentHP;
        attack = characterStats.attack;
        defense = characterStats.defense;
        effectResistance = characterStats.effectResistance;
        moveSpeed = characterStats.moveSpeed;
    }

    public int AuthoritativeCurrentHP
    {
        get
        {
            characterStats = characterStats != null
                ? characterStats
                : GetComponent<CharacterStats>();
            return characterStats != null
                ? characterStats.CurrentHP
                : Mathf.Clamp(currentHP, 0, Mathf.Max(1, maxHP));
        }
    }

    public int AuthoritativeMaxHP
    {
        get
        {
            characterStats = characterStats != null
                ? characterStats
                : GetComponent<CharacterStats>();
            return characterStats != null
                ? characterStats.MaxHP
                : Mathf.Max(1, maxHP);
        }
    }

    public void RestoreHealthState(int restoredMaxHP, int restoredCurrentHP)
    {
        EnsureCharacterStatsHealthSource();
        characterStats.RestoreHealthState(restoredMaxHP, restoredCurrentHP);
        SyncFromCharacterStats();

        if (currentHP > 0)
        {
            isDead = false;
        }
    }

    public void SetCurrentHealth(int value)
    {
        EnsureCharacterStatsHealthSource();
        characterStats.SetCurrentHP(value);
        SyncFromCharacterStats();

        if (currentHP > 0)
        {
            isDead = false;
        }
    }

    public int Heal(int amount)
    {
        EnsureCharacterStatsHealthSource();
        int healed = characterStats.Heal(amount);
        SyncFromCharacterStats();

        if (currentHP > 0)
        {
            isDead = false;
        }

        return healed;
    }

    void EnsureCharacterStatsHealthSource()
    {
        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }

        if (characterStats == null)
        {
            characterStats = gameObject.AddComponent<CharacterStats>();
        }

        characterStats.generatedEntityKind = EntityKind.Cultivator;
        characterStats.generateFromEntityProfile = generateFromEntityProfile;
        if (entityProfile == null)
        {
            entityProfile = characterStats.entityProfile;
        }

        if (entityProfile != null)
        {
            characterStats.entityProfile = entityProfile;
        }
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
        if (IsDead || IsRestrictedMapSessionActive())
        {
            return;
        }

        currentAction = NpcText.Action("cultivate");
        NpcSpeechController.TryShowSpeech(gameObject, null, "cultivate_self");
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
            IsRestrictedMapSessionActive())
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

        if (TryGetCultivationZoneMismatch(
                out NpcMapZone preferredZone,
                out NpcMapZone currentZone))
        {
            ClearTravelTargetsAndStop();
            UpdateCultivationEffect(false);
            actionTimer = Mathf.Max(
                actionTimer,
                GameHoursToSeconds(5f / 60f));
            currentAction = "waitSchedule" + NpcScheduleActivity.Cultivate;
            DebugFlow(
                "Cultivate",
                "Blocked natural cultivate outside preferred zone currentZone=" +
                currentZone +
                " preferredZone=" +
                preferredZone);
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
        NpcSpeechController.TryShowSpeech(gameObject, null, "cultivate_self");

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
        if (IsRestrictedMapSessionActive())
        {
            return false;
        }

        float cultivationArriveDistance =
            GetCultivationArriveDistance();

        if (hasCultivationTarget &&
            Vector2.Distance(
                transform.position,
                cultivationTarget) <=
            cultivationArriveDistance)
        {
            return false;
        }

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
        NpcMapZone? preferredZone =
            ResolveCultivationPreferredZone();
        bool requirePreferredZone =
            TryGetCultivationZoneMismatch(
                out NpcMapZone mismatchedPreferredZone,
                out NpcMapZone currentZone);
        if (requirePreferredZone)
        {
            preferredZone = mismatchedPreferredZone;
        }

        if (TryGetCultivationAreaPosition(
                preferredZone,
                requirePreferredZone,
                out cultivationPosition,
                out NpcMapZone? configuredZone))
        {
            targetPoint = null;
            hasCultivationTarget = false;
            DebugFlow(
                "Cultivate",
                "Use configured cultivation area cultivationPosition=" +
                cultivationPosition +
                " cultivationZone=" +
                (configuredZone.HasValue
                    ? configuredZone.Value.ToString()
                    : "None") +
                " preferredZone=" +
                (preferredZone.HasValue
                    ? preferredZone.Value.ToString()
                    : "None") +
                " currentPos=" +
                transform.position);
            TraceRuntime(
                "TryResolveCultivationTravelDestination",
                "configured cultivationPosition=" +
                cultivationPosition +
                " zone=" +
                (configuredZone.HasValue
                    ? configuredZone.Value.ToString()
                    : "None") +
                " preferredZone=" +
                (preferredZone.HasValue
                    ? preferredZone.Value.ToString()
                    : "None"));
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
                if (TryGetCultivationAreaPosition(
                        destinationZone,
                        true,
                        out cultivationPosition,
                        out NpcMapZone? fallbackZone))
                {
                    targetPoint = null;
                    DebugFlow(
                        "Cultivate",
                        "Fallback to configured cultivation area targetPoint=null destinationZone=" +
                        destinationZone.Value +
                        " cultivationZone=" +
                        (fallbackZone.HasValue
                            ? fallbackZone.Value.ToString()
                            : "None") +
                        " cultivationPosition=" +
                        cultivationPosition +
                        " currentPos=" +
                        transform.position);
                    TraceRuntime(
                        "TryResolveCultivationTravelDestination",
                        "fallback-configured destinationZone=" +
                        destinationZone.Value +
                        " cultivationZone=" +
                        (fallbackZone.HasValue
                            ? fallbackZone.Value.ToString()
                            : "None") +
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
            TryGetCultivationAreaPosition(
                preferredZone,
                requirePreferredZone,
                out cultivationPosition,
                out NpcMapZone? fallbackCultivationZone);

        if (!foundCultivationPosition &&
            requirePreferredZone &&
            preferredZone.HasValue &&
            TryResolveCultivationZoneEntryPosition(
                preferredZone.Value,
                out cultivationPosition))
        {
            foundCultivationPosition = true;
            fallbackCultivationZone = preferredZone.Value;
            DebugFlow(
                "Cultivate",
                "Fallback to preferred zone entry preferredZone=" +
                preferredZone.Value +
                " currentZone=" +
                currentZone +
                " cultivationPosition=" +
                cultivationPosition +
                " currentPos=" +
                transform.position);
            TraceRuntime(
                "TryResolveCultivationTravelDestination",
                "fallback-zone-entry preferredZone=" +
                preferredZone.Value +
                " cultivationPosition=" +
                cultivationPosition);
        }

        DebugFlow(
            "Cultivate",
            foundCultivationPosition
                ? "Use fallback cultivation area cultivationPosition=" +
                    cultivationPosition +
                    " cultivationZone=" +
                    (fallbackCultivationZone.HasValue
                        ? fallbackCultivationZone.Value.ToString()
                        : "None") +
                    " preferredZone=" +
                    (preferredZone.HasValue
                        ? preferredZone.Value.ToString()
                        : "None") +
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
                () => CompleteMajorBreakthrough(targetRealm),
                passed =>
                {
                    if (!passed)
                    {
                        waitingForHeavenlyTribulation = false;
                    }
                });
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
        MonsterAI policyTarget =
            NpcMapBehaviorPolicy.GetSharedCombatTarget(allowedCombatZone);
        if (policyTarget != null &&
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
            if (distance < bestExistingDistance)
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
            if (distance < bestDistance)
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




