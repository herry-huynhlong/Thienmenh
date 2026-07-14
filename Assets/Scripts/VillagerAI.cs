using System.Collections.Generic;
using UnityEngine;

public enum VillagerAgeGroup
{
    Child,
    Adult,
    Elder
}

public enum VillagerJob
{
    None,
    Farmer,
    Trader,
    Worker,
    Guard,
    Healer,
    Fisher,
    Hunter,
    Alchemist,
    Blacksmith
}

[RequireComponent(typeof(NpcScheduleController))]
public partial class VillagerAI : MonoBehaviour, IDamageable, INpcActionStateOwner
{
    static readonly HashSet<VillagerAI> activeVillagers =
        new HashSet<VillagerAI>();

    public static IReadOnlyCollection<VillagerAI> ActiveVillagers =>
        activeVillagers;

    [Header("Entity Generation")]
    public bool generateFromEntityProfile = true;
    public EntityProfile entityProfile;
    NPCIdentity npcIdentity;

    [Header("Info")]
    public string villagerName = "Người dân";
    public VillagerAgeGroup ageGroup = VillagerAgeGroup.Adult;
    public VillagerJob job = VillagerJob.Farmer;
    public bool keepInspectorJob = true;

    [Header("Stats")]
    public int maxHP = 100;
    public int currentHP = 100;
    [InspectorName("Linh Thạch")]
    public int money = 20;
    public int spiritStone;
    public float moveSpeed = 1.6f;
    public CharacterStats characterStats;

    [Header("Cultivation")]
    public CultivationRealm realm = CultivationRealm.Mortal;
    [Range(1, 9)]
    public int realmStage = 1;
    public long cultivationExp;
    public int baseExpToNextRealm = 100;
    public int baseMaxHP = 100;
    public int baseAttack = 5;
    public int baseDefense = 2;
    public int attack = 5;
    public int defense = 2;
    public int lifespan = 80;
    public bool dieWhenLifespanEnds = true;
    public bool waitingForHeavenlyTribulation;

    [Header("Personality")]
    [Range(0, 100)]
    public int sociability = 50;
    [Range(0, 100)]
    public int greed = 30;
    [Range(0, 100)]
    public int diligence = 50;
    [Range(0, 100)]
    public int bravery = 30;

    [Header("Needs")]
    [Range(0, 100)]
    public float hunger;
    [Range(0, 100)]
    public float fatigue;
    [Range(0, 100)]
    public float fun;
    public float cultivatorResourceWorkChance = 0.65f;

    [Header("Places")]
    public Transform homePoint;
    public bool hideAtHome = true;
    public bool homeRoutineManagedExternally;
    public Transform workPoint;
    public Transform marketPoint;
    public Transform playPoint;

    [Header("Behavior")]
    public float thinkInterval = 2f;
    public float arriveDistance = 0.25f;
    public float wanderRadius = 3f;
    public float sharedTargetSpacingRadius = 0.45f;
    public float sharedTargetOccupancyRadius = 0.3f;
    public float sharedAnchorSpacingRadius = 0.7f;
    public LayerMask villagerLayers = ~0;
    public bool destroyOnDeath;
    public bool scatterWhenMissingPoints = true;
    public float missingPointScatterRadius = 3f;
    public float workDurationMin = 25f;
    public float workDurationMax = 60f;
    public float restDuration = 8f;
    public float eatDuration = 5f;
    public float playDuration = 8f;
    public float tradeDuration = 15f;
    [Range(0f, 1f)]
    public float roadPreferenceChance = 0.8f;
    public float lowHpRoadBypassPercent = 0.3f;
    public float separationRadius = 0.65f;
    public float separationStrength = 2.0f;
    public float crowdLookAheadDistance = 0.75f;
    public float crowdDetourDistance = 0.65f;
    public float crowdYieldDuration = 0.25f;
    public bool useKinematicNpcMovement = true;
    public bool ignoreNpcBodyCollisions = true;
    public bool strongNpcAvoidMortalWork = true;
    public float unstuckCheckDelay = 1.2f;
    public float unstuckMinMoveDistance = 0.03f;
    public float unstuckOffsetRadius = 0.7f;
    public float minWanderTargetDistance = 0.8f;
    public float movementAcceleration = 8f;
    public float movementDeceleration = 12f;
    public float animationIdleSpeed = 0.03f;
    public float idleAtHomeDuration = 6f;
    public LayerMask obstacleLayers = ~0;
    public float obstacleCheckDistance = 0.35f;
    public float targetClearRadius = 0.25f;
    public float navigationClearancePadding = 0.16f;
    public float obstacleScanDistance = 8f;
    public float obstacleScanStep = 0.35f;
    public int maxPickTargetAttempts = 16;
    public float blockedTargetRetryDelay = 0.8f;

    [Header("Camera Distance Throttle")]
    public bool useCameraDistanceThrottle = true;
    public float fullUpdateDistanceFromCamera = 14f;
    public float reducedUpdateInterval = 0.25f;

    [Header("Smart Obstacle Avoidance")]
    public bool useSmartPathfinding = true;
    public float pathCellSize = 0.7f;
    public float pathWaypointReachDistance = 0.18f;
    public float pathReplanTargetDistance = 0.6f;
    public float obstacleDetourLookAhead = 0.65f;
    public bool useLocalDetour = true;
    public bool requireClearLineForDirectMove = true;
    public float directMovePathDistance = 1.2f;
    public bool useSharedPathMemory = true;
    public float sharedPathMemoryCellSize = 2f;
    public bool compareRememberedPathWithNewPath;
    public float pathTurnPenalty = 0.25f;
    public float pathReplanCooldown = 2f;
    public int maxPathNodes = 500;
    public int maxPathSteps = 160;
    public float maxPathSearchDistance = 0f;
    public bool autonomousWorkEnabled = true;
    public bool autonomousResourceWorkEnabled = false;
    public bool autonomousDangerousWorkEnabled = false;
    public bool dailyTaskPlanEnabled = true;
    public float dailyTaskPlanStartupDelay = 2f;
    public int dailyTaskPlanMinTasks = 3;
    public int dailyTaskPlanMaxTasks = 5;
    public bool dailyRoutineEnabled = true;
    [Range(0f, 24f)] public float dailyCultivationMinHours = 4f;
    [Range(0f, 24f)] public float dailyCultivationMaxHours = 8f;
    [Range(0f, 24f)] public float earliestCultivationHour = 5f;
    [Range(0f, 24f)] public float latestCultivationStartHour = 20f;
    public float cultivationSessionMinGameHours = 1f;
    public float cultivationSessionMaxGameHours = 2f;
    public float workSessionMinGameHours = 1f;
    public float workSessionMaxGameHours = 3f;
    public float resourceSessionMinGameHours = 0.5f;
    public float resourceSessionMaxGameHours = 1.5f;
    public float tradeSessionMinGameHours = 0.5f;
    public float tradeSessionMaxGameHours = 1.5f;

    [Header("Map Bounds")]
    public bool keepInsideNpcMapArea = true;
    public bool allowCrossNpcMapAreas = true;
    public bool keepInsideCombinedNpcMapAreas;
    public float mapAreaEdgePadding = 0.15f;

    [Header("Economy")]
    public ItemInventory inventory;
    public StatItemData fishingProduct;
    public StatItemData huntingProduct;
    public int workProductMin = 1;
    public int workProductMax = 3;
    public int sellGoodsThreshold = 1;
    public float sellGoodsDuration = 8f;
    public float sellGoodsSearchRadius = 2.2f;
    public LayerMask traderLayers = ~0;
    public bool sellOnlyToTrader = true;

    [Header("Profession Progress")]
    public int professionLevel = 1;
    public int professionExp;
    public int baseProfessionExpToNextLevel = 10;
    public int professionExpGrowthPerLevel = 5;
    public int maxProfessionLevel = 20;
    public int professionExpPerWork = 1;
    public int productBonusEveryProfessionLevels = 3;

    public float ProfessionProgress01
    {
        get
        {
            return 0f;
        }
    }

    [Header("Runtime")]
    public string currentAction = "idle";
    public NpcActionId currentActionId = NpcActionId.Idle;
    public string currentActionKey = "idle";
    public Transform currentTarget;
    public string debugWorkTarget;
    public bool debugWorkLogs;
    [Header("Cultivation Effect")]
    public GameObject cultivationEffectPrefab;
    Transform treasureHuntTarget;
    StatItemData treasureHuntItem;
    bool waitingOutsideTreasureLightning;
    bool treasureWaitLowPowerSkirmish;
    bool hiddenAtHome;
    GameObject cultivationEffectInstance;

    Rigidbody2D rb;
    NPCVisualAnimation visualAnimation;
    SpawnedWorldActor spawnedWorldActor;
    Vector3 spawnPosition;
    Vector3 wanderTarget;
    Vector3 directMoveTarget;
    NpcMapZone? directMoveTargetZone;
    bool directMoveTargetUsesRoad = true;
    float lastMovementHaltLogTime = -999f;
    string lastMovementHaltLogKey = string.Empty;
    float thinkTimer;
    float actionTimer;
    int routinePlanDay = int.MinValue;
    float routineCultivationStartHour;
    float routineCultivationEndHour;
    float movementPausedUntil;
    float crowdYieldUntil;
    float crowdDirectionCommitUntil;
    Vector2 crowdCommittedDirection;
    Vector3 obstacleAvoidTarget;
    float obstacleAvoidUntil;
    bool hasObstacleAvoidTarget;
    bool isReturningHome;
    NpcMapZone? movementTargetZone;
    bool hasWanderTarget;
    bool hasDirectMoveTarget;
    bool hasRoadPreference;
    bool prefersRoadForCurrentRoute;
    Vector3 roadPreferenceTarget;
    Vector2 desiredVelocity;
    Vector3 lastUnstuckPosition;
    float stuckMoveTimer;
    float blockedMoveTimer;
    float crowdBlockedTimer;
    float nextReducedMovementUpdateTime;
    int lastHomeTravelFrame = -1;
    float lastHomeTravelIssueTime = float.NegativeInfinity;
    float lastGoHomeLogTime = float.NegativeInfinity;
    float lastReturnHomeTriggerLogTime = float.NegativeInfinity;
    float lastWorkTargetLogTime = float.NegativeInfinity;
    float lastWorkMoveLogTime = float.NegativeInfinity;
    float lastWorkArrivalLogTime = float.NegativeInfinity;
    float lastWorkOccupancyLogTime = float.NegativeInfinity;
    Collider2D[] ownColliders;
    Renderer[] ownRenderers;
    NpcMapArea currentMapArea;
    readonly List<Vector3> activePath =
        new List<Vector3>();
    readonly List<Vector3> rememberedPathBuffer =
        new List<Vector3>();
    readonly List<Vector3> computedPathBuffer =
        new List<Vector3>();
    Vector3 activePathTarget;
    int activePathIndex;
    float nextSmartPathAllowedTime;
    int consecutiveSmartPathFailures;
    int lastPlanResetDay = -1;
    int lastDailyTaskPlanDay = -1;
    int dailyTaskPlanIndex;
    Vector3 currentWorkTarget;
    NpcMapZone? currentWorkTargetZone;
    string currentWorkTargetKey;
    string currentScheduleSlotKey;
    Vector3 currentTradeTarget;
    NpcMapZone? currentTradeTargetZone;
    Vector3 currentBuyTarget;
    NpcMapZone? currentBuyTargetZone;
    NpcForgeAgent currentForgeTradeTarget;
    StatItemData currentForgeTradeItem;
    Vector3 currentEatTarget;
    Vector3 currentSellTarget;
    NpcMapZone? currentSellTargetZone;
    NpcMapZone? resolvedTraderLocationZone;
    NpcMapZone? resolvedBuyLocationZone;
    NpcMapZone? resolvedSellLocationZone;

    bool hasWorkTarget;
    bool hasTradeTarget;
    bool hasBuyTarget;
    bool hasEatTarget;
    bool hasSellTarget;
    readonly List<NpcTaskOffer> dailyTaskPlan =
        new List<NpcTaskOffer>();
    readonly List<DailyTaskNeed> dailyTaskNeeds =
        new List<DailyTaskNeed>();

    class DailyTaskNeed
    {
        public StatItemData item;
        public int amount;
    }

    public bool IsDead =>
        characterStats != null ?
        characterStats.IsDead :
        currentHP <= 0;

    public bool IsActionLocked =>
        IsBusyActionActive();

    public bool IsHiddenAtHome => hiddenAtHome;

    public bool IsReturningHome => isReturningHome;

    public Transform DamageTransform => transform;

    public bool ShouldGoHomeForRest()
    {
        if (homePoint == null)
        {
            ResolveMissingHomePoint();
        }

        if (isReturningHome &&
            !IsAtHomePosition(GetHomePosition()))
        {
            return false;
        }

        if (hiddenAtHome ||
            IsInDungeonCombatSession())
        {
            return false;
        }

        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        bool suppressBaseTimeRestRules =
            (fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.SuppressBaseTimeRestRules) ||
            (fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.SuppressBaseTimeRestRules);

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentSlot != null)
        {
            NpcScheduleActivity activity =
                schedule.CurrentActivity;
            bool shouldHoldBlacksmithTradeRoute =
                fixedBlacksmith != null &&
                fixedBlacksmith.ShouldKeepTradeRouteActive();
            bool shouldHoldAlchemistTradeRoute =
                fixedAlchemist != null &&
                fixedAlchemist.ShouldKeepTradeRouteActive();
            if (activity == NpcScheduleActivity.ReturnHome ||
                activity == NpcScheduleActivity.Sleep)
            {
                if (shouldHoldBlacksmithTradeRoute)
                {
                    return false;
                }

                if (shouldHoldAlchemistTradeRoute)
                {
                    return false;
                }

                return !IsAtHomePosition(GetHomePosition());
            }

            if (suppressBaseTimeRestRules)
            {
                return false;
            }
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null &&
            !suppressBaseTimeRestRules &&
            (timeSystem.CurrentPhase == WorldTimePhase.Noon ||
            (timeSystem.CurrentHour >= 11f &&
                timeSystem.CurrentHour < 13f) ||
            timeSystem.CurrentPhase == WorldTimePhase.Night))
        {
            return !IsAtHomePosition(GetHomePosition());
        }

        return false;
    }

    bool ShouldLeaveHiddenHomeNow()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        return activity != NpcScheduleActivity.Sleep &&
            activity != NpcScheduleActivity.ReturnHome;
    }

    public void AddMoney(int amount)
    {
        spiritStone = Mathf.Max(0, spiritStone + amount);
        if (entityProfile != null)
        {
            entityProfile.stats.spiritStone = spiritStone;
        }
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

    void Awake()
    {
        ignoreNpcBodyCollisions = true;
        currentAction = NpcText.Action("idle");
        rb = GetComponent<Rigidbody2D>();
        ownColliders = GetComponentsInChildren<Collider2D>();
        ownRenderers = GetComponentsInChildren<Renderer>(true);
        spawnedWorldActor = GetComponent<SpawnedWorldActor>();
        NpcCollisionRegistry.Register(this, ownColliders);
        lastUnstuckPosition = transform.position;
        CacheNpcIdentity();
        visualAnimation = NPCVisualAnimation.EnsureOn(gameObject);
        characterStats = GetComponent<CharacterStats>();
        inventory = inventory != null
            ? inventory
            : GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        inventory.UsePrivateNpcRuntimeItems(false);

        ApplyRuntimePathPerformanceLimits();

        spawnPosition = transform.position;
        RefreshCurrentMapArea(false);
        ClampInsideCurrentMapArea();
        ResolveMissingHomePoint();

        ConfigureRigidbody();

        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
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
            characterStats.generatedEntityKind = EntityKind.Commoner;
            characterStats.generateFromEntityProfile = generateFromEntityProfile;
            characterStats.entityProfile = entityProfile;

            if (generateFromEntityProfile && entityProfile != null)
            {
                characterStats.ApplyEntityProfile();
            }
            else
            {
                characterStats.realm = CultivationRealm.Mortal;
                characterStats.realmStage = 1;
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
            baseMaxHP = Mathf.Max(1, maxHP);
            baseAttack = Mathf.Max(1, attack);
            baseDefense = Mathf.Max(0, defense);
            ApplyRealmPower();
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        }

        SyncNpcIdentityData();
        EnsureScheduleController();
        ResolveInitialObstacleOverlap();
    }

    void Start()
    {
        EnsureLifecycle();
    }

    void EnsureLifecycle()
    {
        CacheNpcIdentity();
        NPCLifecycle lifecycle =
            GetComponent<NPCLifecycle>() ??
            GetComponentInParent<NPCLifecycle>(true) ??
            GetComponentInChildren<NPCLifecycle>(true);
        if (lifecycle == null)
        {
            lifecycle = gameObject.AddComponent<NPCLifecycle>();
        }

        lifecycle.identity = npcIdentity;
        lifecycle.entityProfile = entityProfile;
        lifecycle.RefreshAgeNow(true);
    }

    void OnEnable()
    {
        activeVillagers.Add(this);

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null && smartNpc.enabled)
        {
            smartNpc.enabled = false;
        }
    }

        void EnsureScheduleController()
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();

        if (schedule == null)
        {
            schedule = gameObject.AddComponent<NpcScheduleController>();
        }

        schedule.lifePath = NpcLifePath.Commoner;
        schedule.canCultivate = false;

        if (schedule.autoBuildDefaultSchedule)
        {
            schedule.RebuildDefaultSchedule();
        }
    }

    public void ForceHiddenAtHome(bool hidden)
    {
        hiddenAtHome = hidden;
        if (hidden)
        {
            isReturningHome = false;
        }
        if (spawnedWorldActor != null)
        {
            spawnedWorldActor.isHiddenAtHome = hidden;
        }

        if (!hidden && !enabled)
        {
            // Re-enable the base AI so the villager can leave home again.
            enabled = true;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = !hidden;
        }

        if (ownRenderers == null || ownRenderers.Length == 0)
        {
            ownRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < ownRenderers.Length; i++)
        {
            if (ownRenderers[i] != null)
            {
                ownRenderers[i].enabled = !hidden;
            }
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] != null)
            {
                ownColliders[i].enabled = !hidden;
            }
        }

        if (hidden)
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("rest");
            actionTimer = Mathf.Max(actionTimer, restDuration);
            UpdateCultivationEffect(false);
        }
        else
        {
            currentAction = NpcText.Action("idle");
            thinkTimer = 0f;
            actionTimer = 0f;
        }
    }

    void ApplyRuntimePathPerformanceLimits()
    {
        if (!useSmartPathfinding)
        {
            return;
        }

        if (Application.isMobilePlatform)
        {
            useCameraDistanceThrottle = true;
            fullUpdateDistanceFromCamera =
                Mathf.Min(fullUpdateDistanceFromCamera, 10f);
            reducedUpdateInterval =
                Mathf.Max(reducedUpdateInterval, 0.45f);

            pathCellSize = Mathf.Max(pathCellSize, 1f);
            pathReplanCooldown = Mathf.Max(pathReplanCooldown, 3f);
            maxPathNodes = Mathf.Clamp(maxPathNodes, 48, 220);
            maxPathSteps = Mathf.Clamp(maxPathSteps, 24, 96);
            sharedPathMemoryCellSize =
                Mathf.Max(sharedPathMemoryCellSize, pathCellSize * 4f);
        }
        else
        {
            pathCellSize = Mathf.Max(pathCellSize, 0.7f);
            pathReplanCooldown = Mathf.Max(pathReplanCooldown, 2f);
            maxPathNodes = Mathf.Clamp(maxPathNodes, 64, 500);
            maxPathSteps = Mathf.Clamp(maxPathSteps, 32, 160);
            sharedPathMemoryCellSize =
                Mathf.Max(sharedPathMemoryCellSize, pathCellSize * 3f);
        }

        compareRememberedPathWithNewPath = false;
        useLocalDetour = true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }

        if (cultivationEffectPrefab == null)
        {
            cultivationEffectPrefab =
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Effects/CultivationEffect.prefab");
        }
    }
#endif

    void ApplyEntityProfile()
    {
        entityProfile =
            EntityGenerator.EnsureProfile(
                gameObject,
                EntityKind.Commoner);

        if (entityProfile == null)
        {
            return;
        }

        if (entityProfile.kind != EntityKind.Commoner)
        {
            EntityGenerator.FillProfile(entityProfile, EntityKind.Commoner);
            entityProfile.lockGeneratedValues = true;
        }

        CacheNpcIdentity();
        bool preferNpcIdentityData = ShouldPreferNpcIdentityData();
        SyncNpcIdentityData();

        if (!keepInspectorJob &&
            job == VillagerJob.None)
        {
            job = GetGeneratedJob(entityProfile.personality);
        }
        realm = CultivationRealm.Mortal;
        realmStage = 1;
        cultivationExp = 0;
        lifespan = 80;
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
        if (!preferNpcIdentityData &&
            entityProfile.identity != null &&
            currentHP > 0)
        {
            int safeSpawnAge = GetSafeSpawnAge(lifespan);
            if (entityProfile.identity.age > safeSpawnAge)
            {
                NpcAgeUtility.SetCurrentAge(
                    entityProfile.identity,
                    safeSpawnAge);
                if (npcIdentity != null)
                {
                    npcIdentity.SetCurrentAge(entityProfile.identity.age);
                }
            }
        }
        ageGroup = GetAgeGroup(GetCurrentVillagerAge());
        attack = entityProfile.stats.attack;
        defense = entityProfile.stats.defense;
        moveSpeed = entityProfile.stats.moveSpeed;
        money = entityProfile.stats.money;
        spiritStone = entityProfile.stats.spiritStone;
        sociability = entityProfile.personality.sociability;
        greed = entityProfile.personality.greed;
        diligence = entityProfile.personality.diligence;
        bravery = entityProfile.personality.bravery;
        hunger = entityProfile.needs.hunger;
        fatigue = entityProfile.needs.fatigue;
        fun = Mathf.Clamp(100f - entityProfile.needs.socialNeed, 0f, 100f);
    }

    [ContextMenu("Reload Villager Identity")]
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

        entityProfile.kind = EntityKind.Commoner;
        entityProfile.ReloadGeneratedProfile();
        ApplyEntityProfile();

        if (characterStats != null)
        {
            characterStats.entityProfile = entityProfile;
            characterStats.ApplyEntityProfile();
            SyncFromCharacterStats();
        }
    }

    VillagerAgeGroup GetAgeGroup(int age)
    {
        if (age < 18)
        {
            return VillagerAgeGroup.Child;
        }

        if (age > 60)
        {
            return VillagerAgeGroup.Elder;
        }

        return VillagerAgeGroup.Adult;
    }

    VillagerJob GetGeneratedJob(EntityPersonality source)
    {
        if (source == null)
        {
            return VillagerJob.Farmer;
        }

        if (source.bravery > 70)
        {
            return VillagerJob.Guard;
        }

        if (source.greed > 70 || source.sociability > 75)
        {
            return VillagerJob.Trader;
        }

        if (source.kindness > 75)
        {
            return VillagerJob.Healer;
        }

        if (source.diligence < 30)
        {
            return VillagerJob.None;
        }

        float roll = Random.value;
        if (roll < 0.34f)
        {
            return VillagerJob.Farmer;
        }

        return roll < 0.67f ? VillagerJob.Fisher : VillagerJob.Hunter;
    }

    int GetSafeSpawnAge(int targetLifespan)
    {
        return Mathf.Max(1, targetLifespan - 1);
    }

    void Update()
    {
        SyncCultivationEffect();

        if (HeavenlyTribulationSystem.IsTargetLocked(gameObject))
        {
            StopMoving();
            SetCurrentActionState(NpcActionState.FromKey("waitTribulation"));
            return;
        }

        if (hiddenAtHome)
        {
            if (ShouldLeaveHiddenHomeNow())
            {
                ForceHiddenAtHome(false);
            }
            else
            {
                return;
            }
        }

        SyncFromCharacterStats();

        if (IsDead)
        {
            StopMoving();
            return;
        }

        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        bool useDedicatedBlacksmithRoutine =
            fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.UseDedicatedRoutine;
        bool useDedicatedAlchemistRoutine =
            fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.UseDedicatedRoutine;

        if (!useDedicatedBlacksmithRoutine &&
            !useDedicatedAlchemistRoutine)
        {
            RefreshScheduledStateForCurrentFrame();
        }

        if (isReturningHome &&
            IsAtHomePosition(GetHomePosition()))
        {
            CompleteHomeArrival();
        }

        if (!useDedicatedBlacksmithRoutine &&
            !useDedicatedAlchemistRoutine &&
            ShouldForceReturnHomeForCurrentSchedule())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.time - lastReturnHomeTriggerLogTime >= 1f)
            {
                Debug.LogWarning(
                    "[VillagerAI] ReturnHome trigger -> " +
                    gameObject.name +
                    " action=" + currentAction +
                    " job=" + job +
                    " hour=" + (WorldTimeSystem.Instance != null
                        ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                        : "null"));
                lastReturnHomeTriggerLogTime = Time.time;
            }
#endif
            GoHomeToRest();
            return;
        }

        if (NpcTaskProvider.IsNpcBusyWithAnyProvider(gameObject))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[VillagerAI] Busy by provider -> " +
                gameObject.name +
                " action=" + currentAction +
                " job=" + job +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
#endif
            StopMoving();
            return;
        }

        UpdateNeeds();

        thinkTimer += Time.deltaTime;
        actionTimer -= Time.deltaTime;

        if (waitingOutsideTreasureLightning)
        {
            UpdateTreasureWaitAction();
            return;
        }

        if (treasureHuntTarget != null)
        {
            RefreshTreasureHuntAction();
            return;
        }

        if (IsBusyActionActive())
        {
            StopMoving();
            UpdateVisualAnimation();
            return;
        }

        if (thinkTimer >= thinkInterval)
        {
            thinkTimer = 0f;
            Think();
        }
    }

    void FixedUpdate()
    {
        if (HeavenlyTribulationSystem.IsTargetLocked(gameObject))
        {
            StopMoving();
            // Harvesting and service interactions can keep two villagers
            // stationary for several seconds. Continue gentle body separation
            // during the busy action so they do not render on top of each
            // other while preserving the interaction itself.
            ApplyNpcOverlapSeparation();
            ApplySmoothVelocity();
            UpdateVisualAnimation();
            return;
        }

        if (hiddenAtHome)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        SyncFromCharacterStats();

        if (IsDead)
        {
            StopMoving();
            UpdateVisualAnimation();
            return;
        }

        if (Time.time < movementPausedUntil)
        {
            LogMovementHaltDebug(
                "MoveHold",
                "reason=movementPaused now=" +
                Time.time.ToString("0.00"));
            StopMoving();
            UpdateVisualAnimation();
            return;
        }

        if (Time.time < crowdYieldUntil)
        {
            LogMovementHaltDebug(
                "MoveHold",
                "reason=crowdYield now=" +
                Time.time.ToString("0.00"));
            StopMoving();
            UpdateVisualAnimation();
            return;
        }

        if (treasureHuntTarget != null || waitingOutsideTreasureLightning)
        {
            NpcPerformanceOverlay.RecordNpcFixedUpdate();
            ClampInsideCurrentMapArea();
            RefreshCurrentMapArea();
            MoveToCurrentTarget();
            UpdateUnstuck();
            ApplyNpcOverlapSeparation();
            ApplySmoothVelocity();
            ClampInsideCurrentMapArea();
            UpdateVisualAnimation();
            return;
        }

        if (IsBusyActionActive())
        {
            LogMovementHaltDebug(
                "MoveHold",
                "reason=busyAction actionTimer=" +
                actionTimer.ToString("0.00"));
            StopMoving();
            // Service counters and resource nodes are shared destinations.
            // Keep the interaction active without pinning two NPC bodies at
            // the exact same coordinate for the whole timed action.
            ApplyNpcOverlapSeparation();
            ApplySmoothVelocity();
            UpdateVisualAnimation();
            return;
        }

        if (ShouldUseReducedMovementUpdate())
        {
            ApplySmoothVelocity();
            UpdateVisualAnimation();
            return;
        }

        NpcPerformanceOverlay.RecordNpcFixedUpdate();

        ClampInsideCurrentMapArea();
        RefreshCurrentMapArea();
        MoveToCurrentTarget();
        UpdateUnstuck();
        ApplyNpcOverlapSeparation();
        ApplySmoothVelocity();
        ClampInsideCurrentMapArea();
        UpdateVisualAnimation();
    }

    bool ShouldUseReducedMovementUpdate()
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

        if (Time.time >= nextReducedMovementUpdateTime)
        {
            nextReducedMovementUpdateTime =
                Time.time + Mathf.Max(Time.fixedDeltaTime, reducedUpdateInterval);
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

        realm = CultivationRealm.Mortal;
        realmStage = 1;
        cultivationExp = 0;
        waitingForHeavenlyTribulation =
            characterStats.waitingForHeavenlyTribulation;
        maxHP = characterStats.finalHP;
        currentHP = characterStats.currentHP;
        attack = characterStats.attack;
        defense = characterStats.defense;
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
    }

    public void SetCurrentHealth(int value)
    {
        EnsureCharacterStatsHealthSource();
        characterStats.SetCurrentHP(value);
        SyncFromCharacterStats();
    }

    public int Heal(int amount)
    {
        EnsureCharacterStatsHealthSource();
        int healed = characterStats.Heal(amount);
        SyncFromCharacterStats();
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

        characterStats.generatedEntityKind = EntityKind.Commoner;
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

    public void SyncNpcIdentityData()
    {
        CacheNpcIdentity();

        if (npcIdentity == null)
        {
            return;
        }

        if (entityProfile == null)
        {
            if (generateFromEntityProfile)
            {
                ApplyEntityProfile();
            }
            else
            {
                entityProfile = GetComponent<EntityProfile>();
            }
        }

        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            if (ShouldPreferNpcIdentityData())
            {
                ApplyNpcIdentityToEntityProfile();
            }
            else
            {
                ApplyEntityProfileToNpcIdentity();
            }
        }

        if (!string.IsNullOrWhiteSpace(npcIdentity.npcName))
        {
            villagerName = npcIdentity.npcName;
        }
        else if (entityProfile != null &&
            entityProfile.identity != null &&
            !string.IsNullOrWhiteSpace(entityProfile.identity.entityName))
        {
            villagerName = entityProfile.identity.entityName;
        }

        ageGroup = GetAgeGroup(GetCurrentVillagerAge());
    }

    void CacheNpcIdentity()
    {
        if (npcIdentity == null)
        {
            npcIdentity =
                GetComponent<NPCIdentity>() ??
                GetComponentInParent<NPCIdentity>(true) ??
                GetComponentInChildren<NPCIdentity>(true);

            if (npcIdentity == null && Application.isPlaying)
            {
                npcIdentity = gameObject.AddComponent<NPCIdentity>();
            }
        }
    }

    bool HasMeaningfulNpcIdentityData()
    {
        if (npcIdentity == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(npcIdentity.npcName) ||
            npcIdentity.age > 0 ||
            npcIdentity.hasBirthAbsoluteDay ||
            npcIdentity.lifeStage != LifeStage.Youth ||
            !string.IsNullOrWhiteSpace(npcIdentity.homeId) ||
            !string.IsNullOrWhiteSpace(npcIdentity.fatherId) ||
            !string.IsNullOrWhiteSpace(npcIdentity.motherId) ||
            !string.IsNullOrWhiteSpace(npcIdentity.spouseId);
    }

    bool ShouldPreferNpcIdentityData()
    {
        return HasMeaningfulNpcIdentityData() ||
            entityProfile == null ||
            entityProfile.identity == null ||
            string.IsNullOrWhiteSpace(entityProfile.identity.entityName);
    }

    void ApplyNpcIdentityToEntityProfile()
    {
        if (npcIdentity == null ||
            entityProfile == null ||
            entityProfile.identity == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(npcIdentity.npcName))
        {
            entityProfile.identity.entityName = npcIdentity.npcName;
        }
        else if (string.IsNullOrWhiteSpace(npcIdentity.npcName))
        {
            npcIdentity.npcName = entityProfile.identity.entityName;
        }

        int currentAge = npcIdentity.GetCurrentAge();
        entityProfile.identity.age = currentAge;
        entityProfile.identity.birthAbsoluteDay =
            npcIdentity.birthAbsoluteDay;
        entityProfile.identity.hasBirthAbsoluteDay =
            npcIdentity.hasBirthAbsoluteDay;
        entityProfile.identity.gender =
            npcIdentity.gender == Gender.Female
                ? EntityGender.Female
                : EntityGender.Male;

        villagerName = entityProfile.identity.entityName;
    }

    void ApplyEntityProfileToNpcIdentity()
    {
        if (npcIdentity == null ||
            entityProfile == null ||
            entityProfile.identity == null)
        {
            return;
        }

        npcIdentity.npcName = entityProfile.identity.entityName;
        int currentAge =
            NpcAgeUtility.GetCurrentAge(entityProfile.identity);
        npcIdentity.age = currentAge;
        npcIdentity.birthAbsoluteDay =
            entityProfile.identity.birthAbsoluteDay;
        npcIdentity.hasBirthAbsoluteDay =
            entityProfile.identity.hasBirthAbsoluteDay;
        npcIdentity.gender =
            entityProfile.identity.gender == EntityGender.Female
                ? Gender.Female
                : Gender.Male;
    }

    int GetCurrentVillagerAge()
    {
        if (npcIdentity != null &&
            HasMeaningfulNpcIdentityData())
        {
            return npcIdentity.GetCurrentAge();
        }

        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            return NpcAgeUtility.GetCurrentAge(entityProfile.identity);
        }

        if (npcIdentity != null)
        {
            return npcIdentity.GetCurrentAge();
        }

        return 0;
    }

    void UpdateNeeds()
    {
        if (NeedsFood())
        {
            hunger = Mathf.Clamp(
                hunger + Time.deltaTime * GetHungerRate(),
                0f,
                100f);
        }
        else
        {
            hunger = 0f;
        }

        fatigue = Mathf.Clamp(
            fatigue + Time.deltaTime * 0.25f,
            0f,
            100f);

        fun = Mathf.Clamp(
            fun - Time.deltaTime * 0.2f,
            0f,
            100f);

        if (entityProfile != null)
        {
            entityProfile.needs.hunger = hunger;
            entityProfile.needs.fatigue = fatigue;
            entityProfile.needs.socialNeed = Mathf.Clamp(100f - fun, 0f, 100f);
        }
    }

    public void GoHomeToRest()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            isReturningHome = false;
            return;
        }

        if (ShouldContinueExistingHomeReturn())
        {
            return;
        }

        if (Time.frameCount == lastHomeTravelFrame &&
            currentAction == NpcText.Action("goHomeRest") &&
            (currentTarget != null ||
            hasDirectMoveTarget ||
            hasWanderTarget ||
            hasObstacleAvoidTarget))
        {
            return;
        }

        if (!enabled)
        {
            // Another system may have paused the base AI; restore it so home travel runs.
            enabled = true;
        }

        if (homePoint == null)
        {
            ResolveMissingHomePoint();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Time.time - lastGoHomeLogTime >= 1f)
        {
            Vector3 debugHomePosition = GetHomePosition();
            float distanceToHome =
                Vector2.Distance(transform.position, debugHomePosition);

            Debug.LogWarning(
                "[VillagerAI] GoHomeToRest -> " +
                gameObject.name +
                " action=" + currentAction +
                " job=" + job +
                " homePoint=" + (homePoint != null ? homePoint.name : "null") +
                " hiddenAtHome=" + hiddenAtHome +
                " atHome=" + IsAtHomePosition(debugHomePosition) +
                " distanceToHome=" + distanceToHome.ToString("0.00") +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
            lastGoHomeLogTime = Time.time;
        }
#endif

        CancelScheduledWorkState();
        isReturningHome = true;
        lastHomeTravelFrame = Time.frameCount;
        lastHomeTravelIssueTime = Time.time;
        actionTimer = 0f;

        Vector3 homePosition = GetHomePosition();
        Vector3 travelTarget = homePosition;
        TryResolveHomeTravelTarget(ref travelTarget);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Vector2.Distance(homePosition, travelTarget) > 0.01f)
        {
            Debug.LogWarning(
                "[VillagerAI] Home fallback target -> " +
                gameObject.name +
                " home=" + homePosition +
                " fallback=" + travelTarget +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
        }
#endif
        SetDirectMoveTarget(travelTarget, false, GetHomeZone());
        MoveUsingRoad(travelTarget, GetHomeZone());
        currentAction = NpcText.Action("goHomeRest");

        if (IsAtHomePosition(homePosition) ||
            IsAtResolvedHomeTravelPosition(homePosition, travelTarget))
        {
            CompleteHomeArrival();
        }
    }

    bool ShouldContinueExistingHomeReturn()
    {
        if (!isReturningHome ||
            IsAtHomePosition(GetHomePosition()))
        {
            return false;
        }

        float retryDelay =
            Mathf.Max(
                0.75f,
                unstuckCheckDelay);
        if (Time.time - lastHomeTravelIssueTime < retryDelay)
        {
            return true;
        }

        return currentTarget != null ||
            hasDirectMoveTarget ||
            hasWanderTarget ||
            hasObstacleAvoidTarget;
    }

    void CompleteHomeArrival()
    {
        ClearMovementTargets();
        StopMoving();
        fatigue = 0f;
        Heal(10);

        actionTimer = restDuration;
        currentAction = NpcText.Action("rest");
        ResetDailyTargets();
        isReturningHome = false;

        if (hideAtHome)
        {
            ForceHiddenAtHome(true);
        }
    }

    bool IsAtResolvedHomeTravelPosition(
        Vector3 homePosition,
        Vector3 travelTarget)
    {
        return Vector2.Distance(homePosition, travelTarget) > 0.01f &&
            IsAtHomePosition(travelTarget);
    }

    void CancelScheduledWorkState()
    {
        ResetDailyTargets();
        ClearMovementTargets();

        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer != null)
        {
            gatherer.CancelGatheringNow();
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null)
        {
            harvestJob.CancelHarvestNow();
        }

        HunterJob hunterJob = GetComponent<HunterJob>();
        if (hunterJob != null)
        {
            hunterJob.CancelHunterNow();
        }
    }

    void GoEat()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (IsAtHomePosition(GetHomePosition()))
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        GoHomeToRest();
    }

    void EatWhereTraderIs()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (IsAtHomePosition(GetHomePosition()))
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        GoHomeToRest();
    }

    void GatherAndPlay()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (playPoint == null)
        {
            GoHomeIdle(NpcText.Action("restNearHome"));
            return;
        }

        SetTarget(
            playPoint,
            NpcText.Action("goPlay"));

        if (HasArrived())
        {
            ClearMovementTargets();
            StopMoving();
            fun = 100f;
            actionTimer = playDuration;
            currentAction = NpcText.Action("playWithFriends");
        }
    }
    NpcResourceGatherer EnsureWorkGatherer()
    {
        NpcItemCollector collector = GetComponent<NpcItemCollector>();
        if (collector == null)
        {
            collector = gameObject.AddComponent<NpcItemCollector>();
        }

        collector.canPickupItems = true;
        collector.pickupRadius = collector.pickupRadius > 0f
            ? Mathf.Min(collector.pickupRadius, 0.22f)
            : 0.22f;

        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer == null)
        {
            gatherer = gameObject.AddComponent<NpcResourceGatherer>();
        }

        gatherer.canGather = true;
        gatherer.useVillagerPreferredZone = true;
        gatherer.arriveDistance = gatherer.arriveDistance > 0f
            ? Mathf.Min(gatherer.arriveDistance, 0.16f)
            : 0.16f;
        return gatherer;
    }

    HarvestJob EnsureHarvestJob()
    {
        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob == null)
        {
            harvestJob = gameObject.AddComponent<HarvestJob>();
        }

        if (harvestJob.fishingProduct == null)
        {
            harvestJob.fishingProduct = fishingProduct;
        }

        if (harvestJob.huntingProduct == null)
        {
            harvestJob.huntingProduct = huntingProduct;
        }

        return harvestJob;
    }

    VillagerJobDispatcher EnsureJobDispatcher()
    {
        VillagerJobDispatcher dispatcher = GetComponent<VillagerJobDispatcher>();
        if (dispatcher == null)
        {
            dispatcher = gameObject.AddComponent<VillagerJobDispatcher>();
        }

        return dispatcher;
    }

    Vector3 GetFallbackPositionInZone(NpcMapZone zone)
    {
        NpcMapArea area = NpcMapArea.FindNearestAreaInZone(
            zone,
            transform.position);

        if (area != null)
        {
            return area.ClosestPoint(transform.position);
        }

        return GetFallbackActivityPosition();
    }

    string GetCurrentWorkTargetKey()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        NpcScheduleSlot slot = schedule != null
            ? schedule.CurrentSlot
            : null;

        string zoneKey = string.Empty;
        if (job == VillagerJob.Fisher ||
            job == VillagerJob.Hunter)
        {
            zoneKey = ":" + GetPreferredResourceGatherZone();
        }

        return job + ":" +
            (slot != null ? slot.activity.ToString() : "none") + ":" +
            (slot != null ? slot.startHour.ToString("0.##") : "x") + ":" +
            (slot != null ? slot.endHour.ToString("0.##") : "x") +
            zoneKey;
    }

    void GoTrade()
    {
        if (job != VillagerJob.Trader)
        {
            return;
        }

        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (!hasTradeTarget)
        {
            currentTradeTarget = GetTraderWorkPosition();
            currentTradeTargetZone = GetTraderWorkTargetZone();

            hasTradeTarget = true;
        }

        MoveUsingRoad(currentTradeTarget, currentTradeTargetZone);
        currentAction = NpcText.Action("goMarketTrade");

        NpcCounterBroker activeBroker = NpcCounterBroker.Active;
        bool arrivedForTrade = activeBroker != null && activeBroker.receiveAllNpcRequests
            ? IsInsideBrokerServiceArea(activeBroker)
            : IsAtPosition(currentTradeTarget);

        if (arrivedForTrade)
        {
            ClearMovementTargets();
            StopMoving();
            hasTradeTarget = false;
            currentTradeTargetZone = null;

            if (TryTradeAtCounterOrTakeTask())
            {
                return;
            }

            actionTimer = Mathf.Max(1f, thinkInterval);
            currentAction = GetScheduledTradeIdleAction();
        }
    }

    string GetScheduledTradeIdleAction()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule != null && schedule.enforceSchedule)
        {
            switch (schedule.CurrentActivity)
            {
                case NpcScheduleActivity.BuyGoods:
                    return NpcText.Action("goMarketTrade");
                case NpcScheduleActivity.SellGoods:
                    return NpcText.Action("waitTraderBuyGoods");
                case NpcScheduleActivity.TakeTask:
                    return NpcText.Action("visitedTaskProvider");
            }
        }

        return NpcText.Action("noTrade");
    }

    bool TryTradeAtCounterOrTakeTask()
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        bool canTradeNow = NpcScheduleController.AllowsTrade(gameObject);
        bool canTakeTaskNow = NpcScheduleController.AllowsTask(gameObject);
        bool traded = false;
        NpcCounterBroker broker = NpcCounterBroker.Active;

        if (canTradeNow &&
            broker != null &&
            broker.receiveAllNpcRequests &&
            IsInsideBrokerServiceArea(broker))
        {
            NpcTradeAgent tradeAgent = GetComponent<NpcTradeAgent>();
            if (tradeAgent != null && broker.CanTradeWithNpc(tradeAgent))
            {
                traded = broker.TryTradeWithNpc(tradeAgent);
            }
        }

        if (canTradeNow && !traded && HasSellableGoods())
        {
            traded = TrySellGoodsToTrader();
        }

        if (traded)
        {
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        tradeSessionMinGameHours,
                        tradeSessionMaxGameHours));
            currentAction = NpcText.Action("trading");
            return true;
        }

        if (!canTakeTaskNow)
        {
            return false;
        }

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider != null &&
            provider.TryHandleVisitor(gameObject))
        {
            return true;
        }

        return false;
    }
    void GoSellGoods()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (job == VillagerJob.Fisher &&
            !HasSellableGoods())
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (!hasSellTarget)
        {
            resolvedSellLocationZone = null;
            currentSellTarget = GetSellGoodsTarget();
            currentSellTargetZone = GetSellGoodsTargetZone();
            hasSellTarget = true;
        }

        MoveUsingRoad(currentSellTarget, currentSellTargetZone);
        currentAction = NpcText.Action("bringGoodsToCounter");

        NpcCounterBroker activeBroker = NpcCounterBroker.Active;
        bool arrivedToSell = activeBroker != null && activeBroker.receiveAllNpcRequests
            ? IsInsideBrokerServiceArea(activeBroker)
            : IsAtPosition(currentSellTarget);

        if (!arrivedToSell)
        {
            return;
        }

        ClearMovementTargets();
        StopMoving();

        if (TrySellGoodsToTrader() ||
            (!sellOnlyToTrader && TrySellGoodsToMarketTrader()))
        {
            hasSellTarget = false;
            currentSellTarget = Vector3.zero;
            currentSellTargetZone = null;
            resolvedSellLocationZone = null;
            actionTimer = sellGoodsDuration;
            currentAction = NpcText.Action("soldGoods");
            return;
        }

        actionTimer = sellGoodsDuration;
        currentAction = NpcText.Action("waitTraderBuyGoods");
    }

    void GoBuyGoods()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            GoHomeIdle(NpcText.Action("idle"));
            return;
        }

        if (!hasBuyTarget)
        {
            resolvedBuyLocationZone = null;
            currentBuyTarget = GetBuyGoodsTarget();
            currentBuyTargetZone = GetBuyGoodsTargetZone();
            hasBuyTarget = true;
        }

        MoveUsingRoad(currentBuyTarget, currentBuyTargetZone);
        currentAction = NpcText.Action("goBuyGoods");

        NpcCounterBroker activeBroker = NpcCounterBroker.Active;
        bool arrivedToBuy = activeBroker != null && activeBroker.receiveAllNpcRequests
            ? IsInsideBrokerServiceArea(activeBroker)
            : IsAtPosition(currentBuyTarget);

        if (!arrivedToBuy)
        {
            return;
        }

        ClearMovementTargets();
        StopMoving();

        if (TryBuyGoodsFromTrader())
        {
            hasBuyTarget = false;
            currentBuyTarget = Vector3.zero;
            currentBuyTargetZone = null;
            resolvedBuyLocationZone = null;
            actionTimer = tradeDuration;
            currentAction = NpcText.Action("boughtGoods");
            return;
        }

        actionTimer = tradeDuration;
        currentAction = NpcText.Action("waitTraderBuyGoods");
    }

    void GoHomeIdle(string action)
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            currentAction = action;
            return;
        }

        if (homePoint == null)
        {
            Wander(action);
            return;
        }

        Vector3 homePosition = GetHomePosition();
        Vector3 travelTarget = homePosition;
        TryResolveHomeTravelTarget(ref travelTarget);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Vector2.Distance(homePosition, travelTarget) > 0.01f)
        {
            Debug.LogWarning(
                "[VillagerAI] Home idle fallback target -> " +
                gameObject.name +
                " home=" + homePosition +
                " fallback=" + travelTarget +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
        }
#endif
        SetDirectMoveTarget(travelTarget, false, GetHomeZone());
        MoveUsingRoad(travelTarget, GetHomeZone());
        currentAction = action;

        if (IsAtHomePosition(homePosition) ||
            IsAtResolvedHomeTravelPosition(homePosition, travelTarget))
        {
            ClearMovementTargets();
            StopMoving();
            actionTimer = idleAtHomeDuration;
            if (hideAtHome &&
                IsCurrentScheduleActivity(NpcScheduleActivity.ReturnHome))
            {
                ForceHiddenAtHome(true);
            }
        }
    }

    Vector3 GetHomePosition()
    {
        return homePoint != null
            ? homePoint.position
            : spawnPosition;
    }

    bool TryResolveHomeTravelTarget(ref Vector3 homePosition)
    {
        if (IsMoveTargetFeasible(homePosition))
        {
            return true;
        }

        if (TryFindClearPointNear(homePosition, out Vector3 clearPoint) &&
            IsMoveTargetFeasible(clearPoint))
        {
            homePosition = clearPoint;
            return true;
        }

        NpcMapZone? homeZone = GetHomeZone();
        if (homeZone.HasValue)
        {
            Vector3 zoneFallback = GetFallbackPositionInZone(homeZone.Value);
            if (IsMoveTargetFeasible(zoneFallback))
            {
                homePosition = zoneFallback;
                return true;
            }
        }

        return false;
    }

    void ResolveMissingHomePoint()
    {
        if (homePoint != null)
        {
            return;
        }

        Transform[] candidates =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include);

        NpcMapZone? actorZone = GetCurrentMapZone();
        Transform bestCandidate = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];
            if (candidate == null ||
                candidate == transform ||
                candidate.IsChildOf(transform))
            {
                continue;
            }

            if (!IsLikelyHomePointName(candidate.name))
            {
                continue;
            }

            float score = Vector2.Distance(spawnPosition, candidate.position);
            if (actorZone.HasValue)
            {
                NpcMapZone? candidateZone =
                    NpcMapNavigator.GetDestinationZone(candidate);
                if (candidateZone.HasValue &&
                    candidateZone.Value != actorZone.Value)
                {
                    score += 1000f;
                }
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate != null)
        {
            homePoint = bestCandidate;
        }
    }

    static bool IsLikelyHomePointName(string candidateName)
    {
        if (string.IsNullOrEmpty(candidateName))
        {
            return false;
        }

        string normalized = candidateName.Trim().ToLowerInvariant();
        return normalized.StartsWith("home") ||
            normalized.Contains("homepoint");
    }

    Vector3 GetFallbackActivityPosition()
    {
        if (!scatterWhenMissingPoints)
        {
            return GetHomePosition();
        }

        Vector2 randomOffset =
            Random.insideUnitCircle *
            Mathf.Max(0.5f, missingPointScatterRadius);

        return spawnPosition +
            new Vector3(randomOffset.x, randomOffset.y, 0f);
    }

    Vector3 GetMarketPosition(Vector3 fallback)
    {
        WorldTilemapManager worldTilemap =
            WorldTilemapManager.Instance;

        if (worldTilemap == null)
        {
            return fallback;
        }

        Vector3 market =
            worldTilemap.GetMarketTile();

        return market != Vector3.zero
            ? market
            : fallback;
    }

    Vector3 GetTraderWorkPosition()
    {
        if (job != VillagerJob.Trader)
        {
            return GetFallbackActivityPosition();
        }

        Transform nearbyMarketTrader = FindNearbyMarketTrader();
        if (nearbyMarketTrader != null)
        {
            resolvedTraderLocationZone =
                NpcMapNavigator.GetDestinationZone(nearbyMarketTrader);
            return nearbyMarketTrader.position;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null && broker.receiveAllNpcRequests && ShouldVisitCounterBroker())
        {
            return broker.GetCustomerPositionFor(gameObject);
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.BuyGoods,
                job,
                NpcLocationPurpose.Market,
                transform.position,
                out Vector3 registryMarket,
                out resolvedTraderLocationZone))
        {
            return registryMarket;
        }

        if (marketPoint != null)
        {
            resolvedTraderLocationZone =
                NpcMapNavigator.GetDestinationZone(marketPoint);
            return marketPoint.position;
        }

        if (workPoint != null)
        {
            resolvedTraderLocationZone =
                NpcMapNavigator.GetDestinationZone(workPoint);
            return workPoint.position;
        }

        resolvedTraderLocationZone = null;
        return GetMarketPosition(GetFallbackActivityPosition());
    }

    NpcMapZone? GetTraderWorkTargetZone()
    {
        if (job != VillagerJob.Trader)
        {
            return null;
        }

        Transform nearbyMarketTrader = FindNearbyMarketTrader();
        if (nearbyMarketTrader != null)
        {
            NpcMapZone? traderZone =
                NpcMapNavigator.GetDestinationZone(nearbyMarketTrader);
            if (traderZone.HasValue)
            {
                return traderZone;
            }
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null && broker.receiveAllNpcRequests && ShouldVisitCounterBroker())
        {
            NpcMapZone? brokerZone = GetTargetZone(broker.transform);
            return brokerZone.HasValue ? brokerZone : NpcMapZone.Lang;
        }

        if (resolvedTraderLocationZone.HasValue)
        {
            return resolvedTraderLocationZone;
        }

        NpcMapZone? marketZone = NpcMapNavigator.GetDestinationZone(marketPoint);
        if (marketZone.HasValue)
        {
            return marketZone;
        }

        NpcMapZone? workZone = NpcMapNavigator.GetDestinationZone(workPoint);
        return workZone.HasValue ? workZone : NpcMapZone.Lang;
    }
    Vector3 GetSellGoodsTarget()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyMyGoods(broker))
        {
            resolvedSellLocationZone =
                GetTargetZone(broker.transform);
            return broker.GetCustomerPositionFor(gameObject);
        }

        Transform nearbyMarketTrader = FindNearbyMarketTrader();
        if (nearbyMarketTrader != null)
        {
            resolvedSellLocationZone =
                NpcMapNavigator.GetDestinationZone(nearbyMarketTrader);
            return nearbyMarketTrader.position;
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.SellGoods,
                job,
                NpcLocationPurpose.SellGoods,
                transform.position,
                out Vector3 registrySell,
                out resolvedSellLocationZone))
        {
            return registrySell;
        }

        resolvedSellLocationZone = null;
        return GetMarketPosition(
            marketPoint != null
            ? marketPoint.position
            : GetFallbackActivityPosition());
    }

    Vector3 GetBuyGoodsTarget()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyUsefulGoods(broker))
        {
            resolvedBuyLocationZone =
                GetTargetZone(broker.transform);
            return broker.GetCustomerPositionFor(gameObject);
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.BuyGoods,
                job,
                NpcLocationPurpose.BuyGoods,
                transform.position,
                out Vector3 registryBuy,
                out resolvedBuyLocationZone))
        {
            return registryBuy;
        }

        resolvedBuyLocationZone = null;
        return GetMarketPosition(
            marketPoint != null
            ? marketPoint.position
            : GetFallbackActivityPosition());
    }

    NpcMapZone? GetSellGoodsTargetZone()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyMyGoods(broker))
        {
            NpcMapZone? brokerZone = GetTargetZone(broker.transform);

            return brokerZone.HasValue
                ? brokerZone
                : NpcMapZone.Lang;
        }

        Transform nearbyMarketTrader = FindNearbyMarketTrader();
        if (nearbyMarketTrader != null)
        {
            NpcMapZone? traderZone =
                NpcMapNavigator.GetDestinationZone(nearbyMarketTrader);
            if (traderZone.HasValue)
            {
                return traderZone;
            }
        }

        if (resolvedSellLocationZone.HasValue)
        {
            return resolvedSellLocationZone;
        }

        return NpcMapNavigator.GetDestinationZone(marketPoint);
    }

    NpcMapZone? GetBuyGoodsTargetZone()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyUsefulGoods(broker))
        {
            NpcMapZone? brokerZone = GetTargetZone(broker.transform);
            return brokerZone.HasValue
                ? brokerZone
                : NpcMapZone.Lang;
        }

        if (resolvedBuyLocationZone.HasValue)
        {
            return resolvedBuyLocationZone;
        }

        return NpcMapNavigator.GetDestinationZone(marketPoint);
    }
    bool BrokerCanBuyMyGoods(NpcCounterBroker broker)
    {
        return broker != null &&
            inventory != null &&
            broker.CanBuyProduceFrom(this, inventory);
    }

    bool BrokerCanSellUsefulGoods(NpcCounterBroker broker)
    {
        return broker != null &&
            inventory != null &&
            broker.CanSellUsefulItemTo(this);
    }

    bool BrokerCanBuyUsefulGoods(NpcCounterBroker broker)
    {
        return BrokerCanSellUsefulGoods(broker);
    }

    Transform FindNearbyMarketTrader()
    {
        if (job != VillagerJob.Trader)
        {
            return null;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                sellGoodsSearchRadius,
                traderLayers);

        Transform best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            NpcTradeAgent trader =
                hit.GetComponentInParent<NpcTradeAgent>();

            if (trader == null ||
                !trader.IsMarketTrader)
            {
                continue;
            }

            float distance = Vector2.Distance(
                transform.position,
                trader.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = trader.transform;
            }
        }

        return best;
    }


    bool ShouldVisitCounterBroker()
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null || !broker.receiveAllNpcRequests)
        {
            return false;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            return false;
        }

        if (HasSellableGoods() && BrokerCanBuyMyGoods(broker))
        {
            return true;
        }

        return CanAffordUsefulCounterPurchase(broker);
    }

    bool CanAffordUsefulCounterPurchase(NpcCounterBroker broker)
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        if (broker == null || broker.inventory == null)
        {
            return false;
        }

        NpcTradeAgent tradeAgent = GetComponent<NpcTradeAgent>();
        if (tradeAgent == null || !tradeAgent.CanUseCounterTrade())
        {
            return false;
        }

        foreach (ItemStack stack in broker.inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.CanUseOn(gameObject))
            {
                continue;
            }

            int price = NpcEconomy.GetNpcBuyPrice(
                stack.item,
                gameObject,
                NpcTradeContext.CounterBrokerBuy);

            if (tradeAgent.GetBuyScore(stack.item, price) > 0f)
            {
                return true;
            }
        }

        return false;
    }
    bool HasCounterTradeOpportunity()
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null || !broker.receiveAllNpcRequests)
        {
            return false;
        }

        NpcTradeAgent tradeAgent = GetComponent<NpcTradeAgent>();
        return tradeAgent != null && broker.CanTradeWithNpc(tradeAgent);
    }

    bool IsInsideBrokerServiceArea(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return false;
        }

        return Vector2.Distance(
            transform.position,
            broker.GetCustomerPositionFor(gameObject)) <=
            Mathf.Max(arriveDistance, broker.customerArriveDistance);
    }

    bool IsNearTaskProvider(NpcTaskProvider provider)
    {
        if (provider == null)
        {
            return false;
        }

        float interactionDistance =
            Mathf.Max(
                arriveDistance,
                provider.providerTalkDistance);

        return Vector2.Distance(
            transform.position,
            provider.GetProviderPositionFor(gameObject)) <= interactionDistance;
    }

    bool IsAtPosition(Vector3 position)
    {
        return Vector2.Distance(
            transform.position,
            position) <= arriveDistance;
    }

    bool IsAtHomePosition(Vector3 homePosition)
    {
        return Vector2.Distance(
            transform.position,
            homePosition) <= Mathf.Max(arriveDistance, 0.75f);
    }

    Vector3 GetApproachPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        Vector3 targetPosition = target.position;
        if (!ShouldUseSharedTargetSpacing(target))
        {
            return targetPosition;
        }

        int slotCount = 8;
        int slotIndex = Mathf.Abs(
            gameObject.GetInstanceID() ^
            target.gameObject.GetInstanceID()) % slotCount;
        float spacingRadius = Mathf.Max(
            arriveDistance * 3f,
            sharedTargetSpacingRadius,
            0.85f);

        Vector3 spacedPosition =
            FindOpenSharedTargetSlot(targetPosition, slotCount, slotIndex, spacingRadius);

        if (target.GetComponent<NpcTaskProvider>() != null ||
            target.GetComponent<NpcCounterBroker>() != null)
        {
            return spacedPosition;
        }

        if (!IsSharedTargetOccupied(targetPosition))
        {
            return targetPosition;
        }

        return spacedPosition;
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
            Vector3 candidate = ClampToCurrentMapArea(
                targetPosition +
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f) * spacingRadius);

            if (!IsSharedTargetOccupied(candidate))
            {
                return candidate;
            }

            fallback = candidate;
        }

        return fallback;
    }

    bool ShouldUseSharedTargetSpacing(Transform target)
    {
        return target == workPoint ||
            target == marketPoint ||
            target == playPoint ||
            target == homePoint ||
            target.GetComponent<NpcTaskProvider>() != null ||
            target.GetComponent<NpcCounterBroker>() != null;
    }

    bool IsSharedTargetOccupied(Vector3 targetPosition)
    {
        float radius = Mathf.Max(0.18f, sharedTargetOccupancyRadius);
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                targetPosition,
                radius,
                villagerLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            VillagerAI otherVillager =
                hit.GetComponentInParent<VillagerAI>();
            if (otherVillager != null &&
                otherVillager != this &&
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

    string DescribeSharedTargetOccupants(Vector3 targetPosition)
    {
        float radius = Mathf.Max(0.18f, sharedTargetOccupancyRadius);
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                targetPosition,
                radius,
                villagerLayers);

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            VillagerAI otherVillager =
                hit.GetComponentInParent<VillagerAI>();
            if (otherVillager != null &&
                otherVillager != this &&
                !otherVillager.IsDead)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(otherVillager.gameObject.name);
                builder.Append("[");
                builder.Append(otherVillager.job);
                builder.Append("]");
                continue;
            }

            SmartNpcAI otherCultivator =
                hit.GetComponentInParent<SmartNpcAI>();
            if (otherCultivator != null &&
                otherCultivator.gameObject != gameObject &&
                !otherCultivator.IsDead)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(otherCultivator.gameObject.name);
                builder.Append("[Smart]");
            }
        }

        return builder.Length > 0
            ? builder.ToString()
            : "none";
    }

    void LogWorkDebug(
        string stage,
        string detail,
        ref float lastLogTime,
        float intervalSeconds = 1f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!debugWorkLogs)
        {
            return;
        }

        if (Time.time - lastLogTime < Mathf.Max(0.05f, intervalSeconds))
        {
            return;
        }

        lastLogTime = Time.time;
        Debug.LogWarning(
            "[VillagerAI] Work " +
            stage +
            " -> " +
            gameObject.name +
            " action=" +
            currentAction +
            " job=" +
            job +
            " target=" +
            currentWorkTarget +
            " zone=" +
            (currentWorkTargetZone.HasValue
                ? currentWorkTargetZone.Value.ToString()
                : "none") +
            " detail=" +
            detail +
            " hour=" +
            (WorldTimeSystem.Instance != null
                  ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                  : "null"));
#endif
    }

    void LogJobRouteDebug(string stage, string detail)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        bool hasFixedTradeDebug =
            fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.debugLogs;
        bool hasFixedAlchemyDebug =
            fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.debugLogs;

        if (!debugWorkLogs &&
            !hasFixedTradeDebug &&
            !hasFixedAlchemyDebug)
        {
            return;
        }

        if (hasFixedTradeDebug)
        {
            detail +=
                " tradeSource=" +
                fixedBlacksmith.DebugTradeDestinationSource +
                " tradeShop=" +
                fixedBlacksmith.DebugTradeShopName +
                " materialReqCount=" +
                fixedBlacksmith.DebugMaterialRequirementCount +
                " hasMaterialReq=" +
                (fixedBlacksmith.DebugHasConfiguredMaterialRequirements
                    ? 1
                    : 0);
        }
        else if (hasFixedAlchemyDebug)
        {
            detail +=
                " tradeSource=" +
                fixedAlchemist.DebugTradeDestinationSource +
                " tradeShop=" +
                fixedAlchemist.DebugTradeShopName +
                " materialReqCount=" +
                fixedAlchemist.DebugMaterialRequirementCount +
                " hasMaterialReq=" +
                (fixedAlchemist.DebugHasConfiguredMaterialRequirements
                    ? 1
                    : 0);
        }

        Debug.LogWarning(
            "[VillagerAI] " +
            stage +
            " -> " +
            gameObject.name +
            " action=" +
            currentAction +
            " job=" +
            job +
            " detail=" +
            detail +
            " hour=" +
            (WorldTimeSystem.Instance != null
                ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                : "null"));
#endif
    }

    void LogMovementHaltDebug(string stage, string detail)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        bool hasFixedTradeDebug =
            fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.debugLogs;
        bool hasFixedAlchemyDebug =
            fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.debugLogs;

        if (!debugWorkLogs &&
            !hasFixedTradeDebug &&
            !hasFixedAlchemyDebug)
        {
            return;
        }

        string key = stage + "|" + detail;
        if (key == lastMovementHaltLogKey &&
            Time.time - lastMovementHaltLogTime < 0.35f)
        {
            return;
        }

        lastMovementHaltLogKey = key;
        lastMovementHaltLogTime = Time.time;

        LogJobRouteDebug(
            stage,
            detail +
            " vel=" +
            (rb != null
                ? rb.linearVelocity.ToString()
                : "noRb") +
            " desiredVel=" + desiredVelocity +
            " pausedUntil=" +
            movementPausedUntil.ToString("0.00") +
            " crowdYieldUntil=" +
            crowdYieldUntil.ToString("0.00") +
            " directTarget=" +
            (hasDirectMoveTarget
                ? directMoveTarget.ToString()
                : "none"));
#endif
    }

    bool AddWorkProduct()
    {
        StatItemData product = null;

        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();

            if (inventory == null)
            {
                inventory = gameObject.AddComponent<ItemInventory>();
                inventory.shareRuntimeItems = false;
            }
        }

        HarvestJob harvestJob = EnsureHarvestJob();
        switch (job)
        {
            case VillagerJob.Farmer:
                if (harvestJob != null)
                {
                    product = harvestJob.farmProduct;
                }
                break;

            case VillagerJob.Fisher:
                if (harvestJob != null)
                {
                    product = harvestJob.fishingProduct != null
                        ? harvestJob.fishingProduct
                        : fishingProduct;
                }
                break;

            case VillagerJob.Hunter:
                if (harvestJob != null)
                {
                    product = harvestJob.huntingProduct != null
                        ? harvestJob.huntingProduct
                        : huntingProduct;
                }
                break;
        }

        if (product == null)
        {
            money += GetWorkIncome();
            currentAction = GetWorkingAction();
            return true;
        }

        int amount =
            Random.Range(
                Mathf.Max(1, workProductMin),
                Mathf.Max(workProductMin, workProductMax) + 1) +
            GetProfessionProductBonus();

        inventory.AddItem(product, amount);
        currentAction = NpcText.ActionFormat("harvestItemAmount", product.itemName, amount);
        return true;
    }

    void EnsureWorkInventory()
    {
        if (inventory != null)
        {
            return;
        }

        inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
            inventory.shareRuntimeItems = false;
        }
    }

    int GetProfessionProductBonus()
    {
        int levelsPerBonus =
            Mathf.Max(1, productBonusEveryProfessionLevels);

        return Mathf.Max(0, professionLevel - 1) / levelsPerBonus;
    }

    void AddProfessionExp(int amount)
    {
        return;
    }

    int GetProfessionExpToNextLevel()
    {
        return 0;
    }

    bool HasSellableGoods()
    {
        if (inventory == null)
        {
            return false;
        }

        int amount = 0;

        foreach (ItemStack stack in inventory.items)
        {
            if (IsSellableStack(stack))
            {
                amount += stack.amount;
            }
        }

        int requiredAmount = Mathf.Max(1, sellGoodsThreshold);
        if (job == VillagerJob.Fisher)
        {
            requiredAmount = Mathf.Max(requiredAmount, 3);
        }

        return amount >= requiredAmount;
    }

    bool IsSellableStack(ItemStack stack)
    {
        if (stack == null ||
            stack.item == null ||
            stack.amount <= 0 ||
            !NpcEconomy.CanTradeNormally(stack.item))
        {
            return false;
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null &&
            harvestJob.IsProducedItem(stack.item))
        {
            return true;
        }

        HunterJob hunterJob = GetComponent<HunterJob>();
        if (hunterJob != null &&
            hunterJob.IsProducedItem(stack.item))
        {
            return true;
        }

        return stack.item.itemType == ItemType.VatLieu ||
            stack.item.itemType == ItemType.ThucPham;
    }

    bool TrySellGoodsToTrader()
    {
        if (inventory == null)
        {
            return false;
        }

        if (TrySellGoodsToNearbyMarketTrader())
        {
            return true;
        }

        if (NpcCounterBroker.Active != null &&
            NpcCounterBroker.Active.receiveAllNpcRequests &&
            NpcCounterBroker.Active.CanBuyProduceFrom(this, inventory) &&
            NpcCounterBroker.Active.TryBuyProduceFrom(this, inventory))
        {
            return true;
        }

        return job == VillagerJob.Trader &&
            TrySellGoodsToMarketTrader();
    }

    bool TrySellGoodsToNearbyMarketTrader()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                sellGoodsSearchRadius,
                traderLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            NpcTradeAgent trader =
                hit.GetComponentInParent<NpcTradeAgent>();

            if (trader == null ||
                !trader.IsMarketTrader)
            {
                continue;
            }

            if (trader.TryBuyProduceFrom(this, inventory))
            {
                return true;
            }
        }

        return false;
    }

    bool TryBuyGoodsFromTrader()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            IsInsideBrokerServiceArea(broker))
        {
            return broker.TrySellUsefulItemTo(this);
        }

        return false;
    }

    bool TrySellGoodsToMarketTrader()
    {
        if (job != VillagerJob.Trader)
        {
            return false;
        }

        if (inventory == null)
        {
            return false;
        }

        for (int i = inventory.items.Count - 1; i >= 0; i--)
        {
            ItemStack stack = inventory.items[i];
            if (!IsSellableStack(stack))
            {
                continue;
            }

            StatItemData item = stack.item;
            int amount = stack.amount;
            int price =
                NpcEconomy.GetTradePrice(
                    item,
                    NpcTradeContext.MarketSell);

            if (!inventory.RemoveItem(item, amount))
            {
                continue;
            }

            spiritStone += price * amount;
            if (entityProfile != null)
            {
                entityProfile.stats.spiritStone = spiritStone;
            }
            return true;
        }

        return false;
    }

    string GetWorkAction()
    {
        switch (job)
        {
            case VillagerJob.Fisher:
                return NpcText.Action("goFish");
            case VillagerJob.Hunter:
                return NpcText.Action("goHunt");
            case VillagerJob.Alchemist:
                return NpcText.Action("goAlchemy");
            case VillagerJob.Blacksmith:
                return NpcText.Action("goForge");
            case VillagerJob.Trader:
                return NpcText.Action("goMarketTrade");
            default:
                return NpcText.Action("goWork");
        }
    }

    string GetWorkingAction()
    {
        switch (job)
        {
            case VillagerJob.Fisher:
                return NpcText.Action("fishing");
            case VillagerJob.Hunter:
                return NpcText.Action("hunting");
            case VillagerJob.Alchemist:
                return NpcText.Action("alchemy");
            case VillagerJob.Blacksmith:
                return NpcText.Action("forging");
            case VillagerJob.Trader:
                return NpcText.Action("trading");
            default:
                return NpcText.Action("working");
        }
    }

    int GetWorkIncome()
    {
        switch (job)
        {
            case VillagerJob.Trader:
                return 3;
            case VillagerJob.Fisher:
            case VillagerJob.Hunter:
            case VillagerJob.Alchemist:
            case VillagerJob.Blacksmith:
                return 1;
            default:
                return 0;
        }
    }


    public string GetRealmText()
    {
        return NpcText.Realm(CultivationRealm.Mortal);
    }

    public int GetAge()
    {
        CacheNpcIdentity();

        if (npcIdentity != null ||
            (entityProfile != null && entityProfile.identity != null))
        {
            return GetCurrentVillagerAge();
        }

        int fallbackAge;
        switch (ageGroup)
        {
            case VillagerAgeGroup.Child:
                fallbackAge = 12;
                break;
            case VillagerAgeGroup.Elder:
                fallbackAge = 70;
                break;
            default:
                fallbackAge = 30;
                break;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return fallbackAge +
            (timeSystem != null
                ? Mathf.Max(0, timeSystem.currentYear - 1)
                : 0);
    }

    public int GetLifespan()
    {
        return lifespan > 0
            ? lifespan
            : 80;
    }

    bool ShouldDieFromOldAge()
    {
        return dieWhenLifespanEnds &&
            GetAge() > 0 &&
            GetAge() >= GetLifespan();
    }

    public void TakeDamage(int damage)
    {
        DamageSystem.Apply(this, DamageContext.Legacy(damage));
    }

    public DamageResult ReceiveDamage(DamageContext context)
    {
        EnsureCharacterStatsHealthSource();

        if (characterStats != null)
        {
            DamageResult result = characterStats.ReceiveDamage(context);
            SyncFromCharacterStats();

            if (result.wasApplied)
            {
                InterruptGatheringForCombat();
            }

            if (IsDead)
            {
                Die();
            }
            else if (bravery < 50)
            {
                currentAction = NpcText.Action("panicBurned");
                currentTarget = homePoint;
                NpcSpeechController.TryShowSpeech(gameObject, null, "flee_self");
            }

            result.receiver = this;
            result.target = gameObject;
            return result;
        }

        if (IsDead)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.TargetAlreadyDead);
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

        currentHP -= finalDamage;
        currentHP = Mathf.Clamp(
            currentHP,
            0,
            Mathf.Max(1, maxHP));

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = currentHP;
        }

        if (currentHP > 0)
        {
            InterruptGatheringForCombat();
            NpcCombatTechniqueSystem.ReactToDamageTaken(
                gameObject,
                finalDamage);
        }

        if (currentHP <= 0)
        {
            Die();
        }
        else if (bravery < 50)
        {
            currentAction = NpcText.Action("panicBurned");
            currentTarget = homePoint;
            NpcSpeechController.TryShowSpeech(gameObject, null, "flee_self");
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
        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer != null)
        {
            gatherer.CancelGatheringNow();
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null)
        {
            harvestJob.CancelHarvestNow();
        }

        ClearMovementTargets();
        StopMoving();
        actionTimer = 0f;
        currentAction = NpcText.Action("injured");
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

        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
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
        currentHP = fillHP
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

        if (entityProfile != null)
        {
            entityProfile.stats.maxHP = maxHP;
            entityProfile.stats.currentHP = currentHP;
            entityProfile.stats.attack = attack;
            entityProfile.stats.defense = defense;
        }

        SyncCultivationEffect();
    }

    void ApplyModifier(StatModifier modifier, int direction)
    {
        if (modifier == null)
        {
            return;
        }

        int intValue =
            modifier.intValue * direction;

        switch (modifier.statType)
        {
            case StatType.MaxHP:
                baseMaxHP += intValue;
                ApplyRealmPower();
                currentHP += intValue;
                break;

            case StatType.CurrentHP:
                currentHP += intValue;
                break;

            case StatType.Cultivation:
                break;

            case StatType.Attack:
            case StatType.Damage:
                baseAttack += intValue;
                ApplyRealmPower();
                break;

            case StatType.Defense:
                baseDefense += intValue;
                ApplyRealmPower();
                break;

            case StatType.Money:
                money += intValue;
                break;

            case StatType.SpiritStone:
                spiritStone += intValue;
                break;

            case StatType.MoveSpeed:
                moveSpeed += modifier.floatValue * direction;
                break;

            case StatType.Breakthrough:
                break;
        }
    }

    void Die()
    {
        SetCurrentHealth(0);
        currentAction = NpcText.Action("dead");
        StopMoving();
        ClearMovementTargets();
        UpdateCultivationEffect(false);
        UpdateVisualAnimation();
        VillagerRelationshipManager relationshipManager =
            VillagerRelationshipManager.Instance;
        if (relationshipManager != null)
        {
            relationshipManager.HandleVillagerDeath(this);
        }
        bool preserveInDungeon = BicanhSessionManager.ShouldPreserveDungeonDeath(gameObject);

        Collider2D collider2d =
            GetComponent<Collider2D>();

        if (collider2d != null)
        {
            collider2d.enabled = false;
        }

        if (preserveInDungeon)
        {
            return;
        }

        NpcInventoryDropper.DropAll(gameObject);

        Destroy(gameObject, 2f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);
    }
}

