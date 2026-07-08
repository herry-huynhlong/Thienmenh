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
public partial class VillagerAI : MonoBehaviour, IDamageable
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
        bool suppressBaseTimeRestRules =
            fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.SuppressBaseTimeRestRules;

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentSlot != null)
        {
            NpcScheduleActivity activity =
                schedule.CurrentActivity;
            if (activity == NpcScheduleActivity.ReturnHome ||
                activity == NpcScheduleActivity.Sleep)
            {
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
        currentAction = action;
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

        if (characterStats != null)
        {
            characterStats.generatedEntityKind = EntityKind.Commoner;
            characterStats.generateFromEntityProfile = true;
            characterStats.entityProfile = entityProfile;
            characterStats.ApplyEntityProfile();
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
                entityProfile.identity.age = safeSpawnAge;
                if (npcIdentity != null)
                {
                    npcIdentity.age = entityProfile.identity.age;
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
        int worldYearOffset =
            WorldTimeSystem.Instance != null
                ? Mathf.Max(0, WorldTimeSystem.Instance.currentYear - 1)
                : 0;

        return Mathf.Max(1, targetLifespan - 1 - worldYearOffset);
    }

    void Update()
    {
        SyncCultivationEffect();

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

        RefreshScheduledStateForCurrentFrame();

        if (isReturningHome &&
            IsAtHomePosition(GetHomePosition()))
        {
            CompleteHomeArrival();
        }

        if (ShouldForceReturnHomeForCurrentSchedule())
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
            StopMoving();
            UpdateVisualAnimation();
            return;
        }

        if (Time.time < crowdYieldUntil)
        {
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
            StopMoving();
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
        maxHP = characterStats.finalHP;
        currentHP = characterStats.currentHP;
        attack = characterStats.attack;
        defense = characterStats.defense;
        moveSpeed = characterStats.moveSpeed;
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

        entityProfile.identity.age = Mathf.Max(0, npcIdentity.age);
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
        npcIdentity.age = Mathf.Max(0, entityProfile.identity.age);
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
            return Mathf.Max(0, npcIdentity.age);
        }

        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            return Mathf.Max(0, entityProfile.identity.age);
        }

        if (npcIdentity != null)
        {
            return Mathf.Max(0, npcIdentity.age);
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

        if (IsAtHomePosition(homePosition))
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
        if (characterStats != null)
        {
            characterStats.currentHP =
                Mathf.Min(
                    characterStats.finalHP,
                    characterStats.currentHP + 10);
            SyncFromCharacterStats();
        }
        else
        {
            currentHP = Mathf.Min(maxHP, currentHP + 10);
        }

        actionTimer = restDuration;
        currentAction = NpcText.Action("rest");
        ResetDailyTargets();
        isReturningHome = false;

        if (hideAtHome)
        {
            ForceHiddenAtHome(true);
        }
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

        if (IsAtHomePosition(homePosition))
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

        if (!debugWorkLogs &&
            (fixedBlacksmith == null ||
            !fixedBlacksmith.enabled ||
            !fixedBlacksmith.debugLogs))
        {
            return;
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

    void Wander(string action)
    {
        if (!hasWanderTarget ||
            Vector2.Distance(transform.position, wanderTarget) <
            arriveDistance ||
            !IsMoveTargetFeasible(wanderTarget))
        {
            if (!TryPickWanderTarget(out wanderTarget))
            {
                ClearMovementTargets();
                currentAction = NpcText.Action("watchRoad");
                StopMoving();
                return;
            }

            hasWanderTarget = true;
        }

        currentTarget = null;
        hasDirectMoveTarget = false;
        currentAction = action;
        MoveToPosition(wanderTarget);
    }

    void IdleOrGoHome(string wanderAction)
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            currentAction = wanderAction;
            return;
        }

        if (ShouldReturnHomeForIdle())
        {
            GoHomeIdle(NpcText.Action("stayNearHome"));
            return;
        }

        Wander(wanderAction);
    }

    bool ShouldReturnHomeForIdle()
    {
        if (IsInDungeonCombatSession())
        {
            return false;
        }

        if (homePoint == null)
        {
            return false;
        }

        return !IsAtHomePosition(GetHomePosition());
    }

    bool IsInDungeonCombatSession()
    {
        return BicanhSessionManager.IsDungeonParticipant(gameObject);
    }

    NpcMapZone? GetHomeZone()
    {
        if (homePoint == null)
        {
            return null;
        }

        NpcMapZone? destinationZone = NpcMapNavigator.GetDestinationZone(homePoint);
        if (destinationZone.HasValue)
        {
            return destinationZone;
        }

        NpcMapArea area = NpcMapArea.FindArea(homePoint.position);
        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(homePoint.position);
        }

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    void SetTarget(Transform target, string action)
    {
        if (target == null)
        {
            currentTarget = null;
            hasWanderTarget = false;
            hasDirectMoveTarget = false;
            currentAction = action;
        SetDirectMoveTarget(GetHomePosition());
            return;
        }

        hasWanderTarget = false;
        hasDirectMoveTarget = false;
        currentTarget = target;
        currentAction = action;
    }
    public void ForceJobMoveTo(
        Vector3 target,
        string action,
        NpcMapZone? targetZone = null)
    {
        if (IsDead)
        {
            return;
        }

        ClearTreasureHunt();
        actionTimer = 0f;
        currentTarget = null;

        if (!string.IsNullOrEmpty(action))
        {
            currentAction = action;
        }

        LogJobRouteDebug(
            "ForceJobMoveTo",
            "target=" + target);
        LogJobRouteDebug(
            "ForceJobZone",
            "current=" +
            (GetCurrentMapZone().HasValue
                ? GetCurrentMapZone().Value.ToString()
                : "None") +
            " target=" +
            (targetZone.HasValue
                ? targetZone.Value.ToString()
                : "None"));

        SetDirectMoveTarget(target, false, targetZone);
        MoveUsingRoad(target, targetZone);
    }

    public void ForceGatherTarget(
        Transform target,
        StatItemData item)
    {
        if (target == null || IsDead)
        {
            return;
        }

        ClearTreasureHunt();
        actionTimer = 0f;

        string itemName = item != null
            ? ItemText.Name(item)
            : "tài nguyên";

        string action;
        switch (job)
        {
            case VillagerJob.Farmer:
                action = "Đi thu hoạch " + itemName;
                break;
            case VillagerJob.Fisher:
                action = "Đi câu " + itemName;
                break;
            case VillagerJob.Hunter:
                action = "Đi thu thịt";
                break;
            default:
                action = item != null
                    ? NpcText.ActionFormat("goGatherNamed", itemName)
                    : NpcText.Action("gatherVillageResource");
                break;
        }

        if (currentTarget == target &&
            currentAction == action)
        {
            return;
        }

        SetTarget(target, action);
    }

    bool HasArrived()
    {
        if (currentTarget == null)
        {
            return false;
        }

        return Vector2.Distance(
            transform.position,
            GetApproachPosition(currentTarget)) <= arriveDistance;
    }

    void MoveToCurrentTarget()
    {
        if (currentTarget == null)
        {
            if (hasDirectMoveTarget)
            {
                Vector3 activeDirectTarget = directMoveTarget;
                bool usingTeleportRoute = false;
                string routeAction = string.Empty;

                if (directMoveTargetZone.HasValue)
                {
                    activeDirectTarget =
                        NpcMapNavigator.GetNextMoveTarget(
                            gameObject,
                            directMoveTarget,
                            directMoveTargetZone,
                            out usingTeleportRoute,
                            out routeAction);
                }

                if (Vector2.Distance(transform.position, activeDirectTarget) <= arriveDistance)
                {
                    if (usingTeleportRoute &&
                        TryForceTeleportRouteUseNearDirectTarget(
                            activeDirectTarget))
                    {
                        return;
                    }

                    hasDirectMoveTarget = false;
                    directMoveTargetZone = null;
                    StopMoving();
                    return;
                }

                MoveUsingRoad(directMoveTarget, directMoveTargetZone);
                return;
            }

            if (hasWanderTarget)
            {
                MoveToPosition(wanderTarget);
            }
            else
            {
                StopMoving();
            }

            return;
        }

        MoveUsingRoad(
            GetApproachPosition(currentTarget),
            GetTargetZone(currentTarget));
    }

    bool TryForceTeleportRouteUseNearDirectTarget(Vector3 activeDirectTarget)
    {
        if (directMoveTargetZone == null)
        {
            return false;
        }

        NpcMapZone? currentZone = GetCurrentMapZone();
        if (!currentZone.HasValue ||
            currentZone.Value == directMoveTargetZone.Value)
        {
            return false;
        }

        for (int i = 0; i < NpcTeleportGate.Gates.Count; i++)
        {
            NpcTeleportGate gate = NpcTeleportGate.Gates[i];
            if (gate == null ||
                !gate.TryGetTeleportRouteForZone(
                    currentZone.Value,
                    out _,
                    out _,
                    out _))
            {
                continue;
            }

            Vector3 gateApproach =
                gate.GetApproachPosition(transform.position);
              float distanceToGateApproach =
                  Vector2.Distance(
                      transform.position,
                      gateApproach);
              float distanceToDirectTarget =
                  Vector2.Distance(
                      gateApproach,
                      activeDirectTarget);

            if (distanceToDirectTarget > 0.25f ||
                distanceToGateApproach > Mathf.Max(
                    arriveDistance,
                    gate.npcAutoUseRadius))
            {
                continue;
            }

              LogJobRouteDebug(
                  "ForceGateUse",
                  "gate=" + gate.name +
                  " gateApproach=" + gateApproach +
                  " directMoveTarget=" + activeDirectTarget +
                  " distToGate=" + distanceToGateApproach.ToString("0.00"));

            if (gate.TryForceNpcUse(gameObject))
            {
                return true;
            }
        }

        return false;
    }
    NpcMapZone? GetTargetZone(Transform target)
    {
        NpcMapZone? destinationZone = NpcMapNavigator.GetDestinationZone(target);
        if (destinationZone.HasValue)
        {
            return destinationZone;
        }

        NpcMapArea area = target != null
            ? NpcMapArea.FindArea(target.position)
            : null;

        if (area != null)
        {
            return area.zone;
        }

        NpcMapArea nearestArea = target != null
            ? NpcMapArea.FindNearestArea(target.position)
            : null;

        return nearestArea != null
            ? nearestArea.zone
            : (NpcMapZone?)null;
    }

    Vector3 GetWorkPointPosition(VillagerJob targetJob)
    {
        NpcScheduleActivity requestedActivity =
            GetCurrentScheduleActivityForWorkTarget(targetJob);

        NpcMapZone? preferredZone = null;
        if (targetJob == VillagerJob.Fisher ||
            targetJob == VillagerJob.Hunter)
        {
            preferredZone = GetPreferredResourceGatherZone();
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                requestedActivity,
                targetJob,
                GetWorkLocationPurpose(targetJob),
                preferredZone,
                null,
                transform.position,
                out Vector3 registryWorkPoint,
                out NpcMapZone? registryWorkZone))
        {
            if (targetJob == VillagerJob.Fisher &&
                registryWorkZone.HasValue &&
                registryWorkZone.Value != NpcMapZone.Lang)
            {
                currentWorkTargetZone = NpcMapZone.Lang;
                return Vector3.zero;
            }

            currentWorkTargetZone = registryWorkZone;
            return registryWorkPoint;
        }

        if (targetJob == VillagerJob.Fisher)
        {
            if (workPoint == null)
            {
                return Vector3.zero;
            }

            currentWorkTargetZone =
                NpcMapNavigator.GetDestinationZone(workPoint);
            if (currentWorkTargetZone.HasValue &&
                currentWorkTargetZone.Value != NpcMapZone.Lang)
            {
                return Vector3.zero;
            }
        }

        if (workPoint == null)
        {
            return Vector3.zero;
        }

        currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);

        NpcWorkArea area = workPoint.GetComponent<NpcWorkArea>();
        if (area != null)
        {
            return area.job == targetJob
                ? area.GetRandomPoint(gameObject)
                : Vector3.zero;
        }

        return GetDistributedPointAround(workPoint.position, workPoint);
    }

    NpcScheduleActivity GetCurrentScheduleActivityForWorkTarget(
        VillagerJob targetJob)
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule != null && schedule.enforceSchedule)
        {
            NpcScheduleActivity activity = schedule.CurrentActivity;
            if (activity == NpcScheduleActivity.Hunt ||
                activity == NpcScheduleActivity.Gather ||
                activity == NpcScheduleActivity.Work)
            {
                return activity;
            }
        }

        return targetJob == VillagerJob.Hunter
            ? NpcScheduleActivity.Hunt
            : NpcScheduleActivity.Work;
    }

    NpcLocationPurpose GetWorkLocationPurpose(VillagerJob targetJob)
    {
        switch (targetJob)
        {
            case VillagerJob.Fisher:
                return NpcLocationPurpose.Fishing;
            case VillagerJob.Hunter:
                return NpcLocationPurpose.Hunt;
            default:
                return NpcLocationPurpose.Work;
        }
    }

    Vector3 GetDistributedPointAround(
        Vector3 center,
        Transform anchor,
        int slotCount = 12)
    {
        if (anchor == null)
        {
            return center;
        }

        int safeSlotCount = Mathf.Max(6, slotCount);
        int startSlotIndex = Mathf.Abs(
            gameObject.GetInstanceID() ^
            anchor.gameObject.GetInstanceID()) % safeSlotCount;
        float radius =
            Mathf.Max(
                arriveDistance,
                sharedAnchorSpacingRadius,
                sharedTargetOccupancyRadius * 2f);
        Vector3 fallback = center;

        for (int i = 0; i < safeSlotCount; i++)
        {
            int slotIndex = (startSlotIndex + i) % safeSlotCount;
            float angle = (Mathf.PI * 2f * slotIndex) / safeSlotCount;
            Vector3 candidate = ClampToCurrentMapArea(
                center +
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f) * radius);

            if (!IsSharedTargetOccupied(candidate))
            {
                return candidate;
            }

            fallback = candidate;
        }

        return fallback;
    }
    void MoveUsingRoad(Vector3 target, NpcMapZone? targetZone = null)
    {
        if (currentTarget == null &&
            !hasDirectMoveTarget &&
            !hasWanderTarget)
        {
            SetDirectMoveTarget(target, false, targetZone);
        }

        NpcMapZone? previousMovementTargetZone = movementTargetZone;
        NpcMapZone? routeZone = targetZone;

        try
        {
            bool usingTeleportRoute;
            string routeAction;
            Vector3 requestedTarget = target;
            target = NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                target,
                targetZone,
                out usingTeleportRoute,
                out routeAction);

            LogJobRouteDebug(
                "MoveUsingRoad",
                "requested=" + requestedTarget +
                " resolved=" + target);
            LogJobRouteDebug(
                "MoveUsingRoadRoute",
                "targetZone=" +
                (targetZone.HasValue
                    ? targetZone.Value.ToString()
                    : "None") +
                " useGate=" + usingTeleportRoute);
            LogJobRouteDebug(
                "MoveUsingRoadAction",
                "routeAction=" +
                (string.IsNullOrEmpty(routeAction)
                    ? "None"
                    : routeAction));

            if (usingTeleportRoute)
            {
                routeZone = GetCurrentMapZone() ?? targetZone;
            }

            movementTargetZone = routeZone;

            if (usingTeleportRoute &&
                !string.IsNullOrEmpty(routeAction) &&
                CanRouteActionReplaceCurrentAction())
            {
                currentAction = routeAction;
                actionTimer = 0f;
            }

            if (WorldTilemapManager.Instance == null)
            {
                MoveToPosition(target, routeZone);
                return;
            }

            if (ShouldBypassRoad())
            {
                hasRoadPreference = false;
                MoveToPosition(target, routeZone);
                return;
            }

            if (!hasRoadPreference ||
                Vector2.Distance(roadPreferenceTarget, target) > 0.5f)
            {
                roadPreferenceTarget = target;
                prefersRoadForCurrentRoute =
                    ShouldForceRoadForCurrentAction() ||
                    Random.value < roadPreferenceChance;
                hasRoadPreference = true;
            }

            if (!prefersRoadForCurrentRoute)
            {
                MoveToPosition(target, routeZone);
                return;
            }

                Vector3 roadWaypoint;
                bool hasRoadRoute =
                    WorldTilemapManager.Instance.TryGetRoadWaypointToTarget(
                        transform.position,
                        target,
                        routeZone,
                        IsReachableRoadTile,
                        out roadWaypoint);

            if (!hasRoadRoute)
            {
                MoveToPosition(target, routeZone);
                return;
            }

            float roadDistance =
                Vector2.Distance(
                    transform.position,
                    roadWaypoint);

            if (roadDistance > pathWaypointReachDistance)
            {
                MoveToPosition(roadWaypoint, routeZone);

                if (CanRouteActionReplaceCurrentAction())
                {
                    currentAction = NpcText.Action("walkingRoad");
                }

                return;
            }

            MoveToPosition(target, routeZone);

            if (Vector2.Distance(transform.position, target) < 0.4f)
            {
                hasRoadPreference = false;
            }
        }
        finally
        {
            movementTargetZone = previousMovementTargetZone;
        }
    }

    bool CanRouteActionReplaceCurrentAction()
    {
        if (string.IsNullOrEmpty(currentAction))
        {
            return true;
        }

        string teleportPrefix =
            NpcText.Action("teleportGateTo").Replace("{0}", "");
        return currentAction == NpcText.Action("idle") ||
            currentAction == NpcText.Action("walkingRoad") ||
            IsTravelIntentAction(currentAction) ||
            currentAction.StartsWith(teleportPrefix) ||
            currentAction.StartsWith("Đi cổng dịch chuyển");
    }


    bool TryForgePurchaseAtMarket()
    {
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
                currentForgeTradeTarget = null;
                currentForgeTradeItem = null;
                return false;
            }

            Transform forgePoint =
                currentForgeTradeTarget.forgeStandPoint != null
                    ? currentForgeTradeTarget.forgeStandPoint
                    : currentForgeTradeTarget.transform;

            if (forgePoint == null)
            {
                currentForgeTradeTarget = null;
                currentForgeTradeItem = null;
                return false;
            }

            currentTradeTarget = forgePoint.position;
            currentTradeTargetZone = null;
            hasTradeTarget = true;
        }

        if (currentForgeTradeTarget == null ||
            currentForgeTradeItem == null)
        {
            ClearForgeTradeTarget();
            return false;
        }

        MoveUsingRoad(currentTradeTarget, null);
        currentAction = NpcText.Action("tradeSeek");

        if (Vector2.Distance(transform.position, currentTradeTarget) >
            Mathf.Max(0.5f, arriveDistance))
        {
            return true;
        }

        ClearMovementTargets();
        StopMoving();
        hasTradeTarget = false;
        currentTradeTargetZone = null;

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

        if (ordered)
        {
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        0.2f,
                        0.6f));
            currentAction = NpcText.Action("idle");
            return true;
        }

        currentAction = GetScheduledTradeIdleAction();
        return false;
    }

    void ClearForgeTradeTarget()
    {
        currentForgeTradeTarget = null;
        currentForgeTradeItem = null;
        hasTradeTarget = false;
    }

    void SyncCultivationEffect()
    {
        if (IsDead || hiddenAtHome)
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
                Instantiate(cultivationEffectPrefab, transform);
            cultivationEffectInstance.name = cultivationEffectPrefab.name;
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

    bool IsReachableRoadTile(Vector3 road)
    {
        return IsMoveTargetFeasible(road) &&
            HasClearLineTo(road);
    }

    bool ShouldForceRoadForCurrentAction()
    {
        return currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goHomeCultivate") ||
            currentAction == NpcText.Action("goCultivatePoint") ||
            currentAction == NpcText.Action("goHunt") ||
            currentAction == NpcText.Action("goTavern") ||
            currentAction == NpcText.Action("buyPill") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("tradeSeek") ||
            currentAction == NpcText.Action("moveToTask") ||
            currentAction == NpcText.Action("receiveTask");
    }

    bool ShouldBypassRoad()
    {
        float hpPercent =
            maxHP <= 0
            ? 1f
            : (float)currentHP / maxHP;

        if (hpPercent <= lowHpRoadBypassPercent)
        {
            return true;
        }

        return currentAction.Contains(NpcText.Action("panicBurned"));
    }

    void MoveToPosition(Vector3 position, NpcMapZone? targetZone = null)
    {
        NpcMapZone? previousMovementTargetZone = movementTargetZone;
        movementTargetZone = targetZone;

        try
        {
            LogJobRouteDebug(
                "MoveToPosition",
                "actorPos=" + transform.position +
                " stepTarget=" + position +
                " zone=" +
                (targetZone.HasValue
                    ? targetZone.Value.ToString()
                    : "None"));

            position = ClampToCurrentMapArea(position);
            Vector3 finalTarget = position;

            if (hasObstacleAvoidTarget)
            {
                if (Time.time >= obstacleAvoidUntil ||
                    Vector2.Distance(transform.position, obstacleAvoidTarget) <=
                    arriveDistance ||
                    !IsMoveTargetFeasible(obstacleAvoidTarget))
                {
                    hasObstacleAvoidTarget = false;
                }
                else
                {
                    position = obstacleAvoidTarget;
                    finalTarget = obstacleAvoidTarget;
                }
            }

            if (!IsMoveTargetFeasible(position))
            {
                Vector3 fallback;
                if (TryFindClearPointNear(position, out fallback))
                {
                    position = fallback;
                    finalTarget = position;
                }
                else
                {
                    LogJobRouteDebug(
                        "MoveBlocked",
                        "requested=" + position +
                        " final=" + finalTarget);
                    HandleBlockedMovement(position, finalTarget);
                    return;
                }
            }

            Vector2 toPosition = position - transform.position;
            if (toPosition.magnitude <= arriveDistance)
            {
                ClearActivePath();
                StopMoving();
                return;
            }

            bool usingPathWaypoint =
                TryGetSmartPathWaypoint(finalTarget, out Vector3 pathWaypoint);

            if (usingPathWaypoint)
            {
                position = pathWaypoint;
                toPosition = position - transform.position;

                if (toPosition.magnitude <= pathWaypointReachDistance)
                {
                    AdvanceActivePathWaypoint();
                    return;
                }
            }
            else if (ShouldRequirePathForDirectMove(finalTarget) &&
                TryBuildSmartPath(finalTarget) &&
                TryGetSmartPathWaypoint(finalTarget, out pathWaypoint))
            {
                position = pathWaypoint;
                toPosition = position - transform.position;
                usingPathWaypoint = true;
            }
            Vector2 direction = toPosition.normalized;

            if (!usingPathWaypoint && IsMovementBlocked(direction))
            {
                if (TryBuildSmartPath(finalTarget) &&
                    TryGetSmartPathWaypoint(finalTarget, out pathWaypoint))
                {
                    position = pathWaypoint;
                    toPosition = position - transform.position;
                    direction = toPosition.normalized;
                }
                else if (TryCommitObstacleScanTarget(direction, finalTarget))
                {
                    return;
                }
                else if (useLocalDetour &&
                    TryChooseDetourDirection(
                    direction,
                    finalTarget,
                    out Vector2 detourDirection))
                {
                    if (TryCommitObstacleAvoidTarget(detourDirection))
                    {
                        return;
                    }

                    direction = detourDirection;
                }
                else
                {
                    ClearActivePath();
                    if (TryCommitObstacleScanTarget(direction, finalTarget))
                    {
                        return;
                    }

                    if (TrySetObstacleAvoidTarget(direction, finalTarget))
                    {
                        return;
                    }

                    HandleBlockedMovement(position, finalTarget);
                    return;
                }
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                StopMoving();
                return;
            }

            if (!TryResolveCrowdAhead(direction, finalTarget, out direction))
            {
                return;
            }

            if (IsMovementBlocked(direction))
            {
                if (useLocalDetour &&
                    TryChooseDetourDirection(
                    direction,
                    finalTarget,
                    out Vector2 detourDirection))
                {
                    if (TryCommitObstacleAvoidTarget(detourDirection))
                    {
                        return;
                    }

                    direction = detourDirection;
                }
                else
                {
                    if (TryCommitObstacleScanTarget(direction, finalTarget))
                    {
                        return;
                    }

                    ClearActivePath();
                    if (TrySetObstacleAvoidTarget(direction, finalTarget))
                    {
                        return;
                    }

                    HandleBlockedMovement(position, finalTarget);
                    return;
                }
            }

            Vector2 separation = GetSeparationDirection();

            if (separation.sqrMagnitude > 0.0001f)
            {
                direction =
                    (direction + separation * separationStrength)
                    .normalized;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                StopMoving();
                return;
            }

            if (IsMovementBlocked(direction))
            {
                if (useLocalDetour &&
                    TryChooseDetourDirection(
                    direction,
                    finalTarget,
                    out Vector2 finalDetourDirection))
                {
                    direction = finalDetourDirection;
                }
                else
                {
                    if (TryCommitObstacleScanTarget(direction, finalTarget))
                    {
                        return;
                    }

                    ClearActivePath();
                    if (TrySetObstacleAvoidTarget(direction, finalTarget))
                    {
                        return;
                    }

                    HandleBlockedMovement(position, finalTarget);
                    return;
                }
            }

            blockedMoveTimer = 0f;

            if (rb != null)
            {
                desiredVelocity = direction * moveSpeed;
            }
            else
            {
                transform.position =
                    Vector3.MoveTowards(
                        transform.position,
                        position,
                        moveSpeed * Time.deltaTime);
            }
        }
        finally
        {
            movementTargetZone = previousMovementTargetZone;
        }
    }
    public void StopMoving()
    {
        if (rb != null)
        {
            desiredVelocity = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }
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
        StopMoving();
    }

    void ClearMovementTargets()
    {
        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = false;
        hasObstacleAvoidTarget = false;
        movementTargetZone = null;
        directMoveTargetZone = null;
        ClearActivePath();
    }

    bool IsBusyActionActive()
    {
        return actionTimer > 0f &&
            !IsMovementAction(currentAction);
    }

    bool IsMovementAction(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        return action == NpcText.Action("goFarmWork") ||
            action == NpcText.Action("goWork") ||
            action == NpcText.Action("goPatrol") ||
            action == NpcText.Action("goHeal") ||
            action == NpcText.Action("goFish") ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("goMarketTrade") ||
            action == NpcText.Action("bringGoodsToCounter") ||
            action == NpcText.Action("goHomeRest") ||
            action == NpcText.Action("eatAtShop") ||
            action == NpcText.Action("goPlay") ||
            action == NpcText.Action("walkingRoad") ||
            action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("goTaskProviderDaily") ||
            action == NpcText.Action("goHomeCultivate") ||
            action == NpcText.Action("goCultivatePoint") ||
            action.StartsWith(NpcText.Action("goGatherNamed")
                .Replace("{0}", "")) ||
            action.StartsWith(NpcText.Action("teleportGateTo")
                .Replace("{0}", ""));
    }
    void SetDirectMoveTarget(
        Vector3 position,
        bool preserveCurrentTarget = false,
        NpcMapZone? targetZone = null)
    {
        if (!preserveCurrentTarget)
        {
            currentTarget = null;
            hasObstacleAvoidTarget = false;
        }

        hasWanderTarget = false;
        hasDirectMoveTarget = true;
        directMoveTargetZone = targetZone;

        NpcMapZone? currentZone = GetCurrentMapZone();
        bool crossZoneTarget =
            targetZone.HasValue &&
            currentZone.HasValue &&
            targetZone.Value != currentZone.Value;

        Vector3 resolvedTarget;
        if (crossZoneTarget)
        {
            // Đừng clamp target ở map khác vào rìa map hiện tại.
            // Cứ giữ target thật, MoveUsingRoad sẽ tự đổi bước đầu thành cổng dịch chuyển.
            resolvedTarget = position;
        }
        else
        {
            Vector3 clamped = ClampToCurrentMapArea(position);
            Vector3 clearTarget;
            resolvedTarget = TryFindClearPointNear(clamped, out clearTarget)
                ? clearTarget
                : clamped;
        }

        if (Vector2.Distance(directMoveTarget, resolvedTarget) >
            pathReplanTargetDistance)
        {
            ClearActivePath();
        }

        directMoveTarget = resolvedTarget;
    }

    NpcMapZone? GetCurrentMapZone()
    {
        RefreshCurrentMapArea();
        return currentMapArea != null
            ? currentMapArea.zone
            : (NpcMapZone?)null;
    }

    void RefreshCurrentMapArea(bool allowNearest = false)
    {
        if (!keepInsideNpcMapArea)
        {
            currentMapArea = null;
            return;
        }

        NpcMapArea area = NpcMapArea.FindArea(transform.position);

        if (area == null &&
            NpcMapNavigator.TryGetKnownNpcZone(
                gameObject,
                out NpcMapZone knownZone))
        {
            area = NpcMapArea.FindNearestAreaInZone(
                knownZone,
                transform.position);
        }

        if (area == null)
        {
            return;
        }

        if (allowCrossNpcMapAreas ||
            allowNearest ||
            currentMapArea == null ||
            area == currentMapArea)
        {
            currentMapArea = area;
        }
    }

    void ClampInsideCurrentMapArea()
    {
        if (!keepInsideNpcMapArea)
        {
            return;
        }

        if (allowCrossNpcMapAreas)
        {
            return;
        }

        if (currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return;
        }

        Vector3 clamped = ClampToCurrentMapArea(transform.position);
        if (Vector2.Distance(clamped, transform.position) <= 0.001f)
        {
            return;
        }

        if (rb != null)
        {
            rb.position = clamped;
            rb.linearVelocity = Vector2.zero;
            desiredVelocity = Vector2.zero;
        }

        transform.position = new Vector3(
            clamped.x,
            clamped.y,
            transform.position.z);
    }

    Vector3 ClampToCurrentMapArea(Vector3 position)
    {
        if (!keepInsideNpcMapArea)
        {
            return position;
        }

        if (allowCrossNpcMapAreas)
        {
            return position;
        }

        if (currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return position;
        }

        if (IsTeleportEntryTargetForCurrentArea(position))
        {
            return position;
        }

        if (movementTargetZone.HasValue &&
            movementTargetZone.Value == currentMapArea.zone)
        {
            NpcMapArea areaAtPosition = NpcMapArea.FindArea(position);
            if (areaAtPosition != null &&
                areaAtPosition.zone == currentMapArea.zone)
            {
                return position;
            }

            NpcMapArea nearestSameZone =
                NpcMapArea.FindNearestAreaInZone(
                    currentMapArea.zone,
                    position);

            if (nearestSameZone != null &&
                nearestSameZone.areaBounds != null)
            {
                Vector3 nearestPoint = nearestSameZone.ClosestPoint(position);
                if (Vector2.Distance(nearestPoint, position) <=
                    Mathf.Max(0.05f, mapAreaEdgePadding + targetClearRadius))
                {
                    return nearestPoint;
                }
            }
        }

        Collider2D boundsCollider = currentMapArea.areaBounds;
        Vector2 point = position;
        Vector2 closest = boundsCollider.ClosestPoint(point);
        if (Vector2.Distance(closest, point) <= 0.02f)
        {
            return position;
        }

        Bounds bounds = boundsCollider.bounds;
        float padding = Mathf.Max(0f, mapAreaEdgePadding);
        Vector2 candidate = new Vector2(
            Mathf.Clamp(point.x, bounds.min.x + padding, bounds.max.x - padding),
            Mathf.Clamp(point.y, bounds.min.y + padding, bounds.max.y - padding));

        if (IsInsideCurrentMapArea(candidate))
        {
            return new Vector3(candidate.x, candidate.y, position.z);
        }

        closest = boundsCollider.ClosestPoint(candidate);
        Vector2 inward = (Vector2)bounds.center - closest;
        if (inward.sqrMagnitude > 0.0001f)
        {
            closest += inward.normalized * padding;
        }

        return new Vector3(closest.x, closest.y, position.z);
    }

    bool IsInsideCurrentMapArea(Vector2 position)
    {
        if (allowCrossNpcMapAreas ||
            currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return true;
        }

        if (IsTeleportEntryTargetForCurrentArea(position))
        {
            return true;
        }

        if (movementTargetZone.HasValue &&
            movementTargetZone.Value == currentMapArea.zone)
        {
            NpcMapArea areaAtPosition = NpcMapArea.FindArea(position);
            if (areaAtPosition != null &&
                areaAtPosition.zone == currentMapArea.zone)
            {
                return true;
            }

            NpcMapArea nearestSameZone =
                NpcMapArea.FindNearestAreaInZone(
                    currentMapArea.zone,
                    position);

            if (nearestSameZone != null &&
                nearestSameZone.DistanceTo(position) <=
                Mathf.Max(0.05f, mapAreaEdgePadding + targetClearRadius))
            {
                return true;
            }
        }

        Vector2 closest = currentMapArea.areaBounds.ClosestPoint(position);
        return Vector2.Distance(closest, position) <= 0.02f;
    }

    bool IsTeleportEntryTargetForCurrentArea(Vector3 position)
    {
        if (currentMapArea == null ||
            !movementTargetZone.HasValue ||
            movementTargetZone.Value == currentMapArea.zone)
        {
            return false;
        }

        float entryTolerance =
            Mathf.Max(0.35f, targetClearRadius * 2f);

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                gate.fromZone != currentMapArea.zone ||
                gate.toZone != movementTargetZone.Value)
            {
                continue;
            }

            if (Vector2.Distance(position, gate.EntryPosition) <= entryTolerance)
            {
                return true;
            }
        }

        return false;
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;
        bool shouldResumeHomeReturn =
            isReturningHome ||
            currentAction == NpcText.Action("goHomeRest");
        bool hadActiveMoveTarget =
            currentTarget != null ||
            hasDirectMoveTarget ||
            hasWanderTarget;

        Vector3 referencePosition = gate != null
            ? gate.ExitPosition
            : transform.position;

        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindArea(referencePosition);
        }

        NpcMapZone? resolvedZone =
            area != null
                ? area.zone
                : NpcMapNavigator.ResolveActorZone(gameObject);

        if (gate != null)
        {
            if (resolvedZone.HasValue)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedZone.Value);
            }
            else
            {
                NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
                resolvedZone = gate.toZone;
            }

            area = NpcMapNavigator.ResolveMapAreaAfterTeleport(
                gameObject,
                resolvedZone.Value,
                referencePosition);
        }
        else if (area != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
        }

        currentMapArea = area;
        desiredVelocity = Vector2.zero;
        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = false;
        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        treasureWaitLowPowerSkirmish = false;
        hasRoadPreference = false;
        prefersRoadForCurrentRoute = false;
        hasObstacleAvoidTarget = false;
        movementTargetZone = null;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        blockedMoveTimer = 0f;
        crowdBlockedTimer = 0f;
        stuckMoveTimer = 0f;
        thinkTimer = 0f;
        actionTimer = 0f;
        currentAction = NpcText.Action("idle");
        ClearActivePath();
        UpdateCultivationEffect(false);

        LogJobRouteDebug(
            "OnNpcMapTeleported",
            "gate=" + (gate != null ? gate.name : "null") +
            " resolvedZone=" +
            (resolvedZone.HasValue
                ? resolvedZone.Value.ToString()
                : "None") +
            " actorPos=" + transform.position);

        if (shouldResumeHomeReturn && !hiddenAtHome)
        {
            Vector3 homePosition = GetHomePosition();
            Vector3 travelTarget = homePosition;
            TryResolveHomeTravelTarget(ref travelTarget);

            isReturningHome = true;
            currentAction = NpcText.Action("goHomeRest");
            SetDirectMoveTarget(travelTarget, false, GetHomeZone());
            MoveUsingRoad(travelTarget, GetHomeZone());

            if (IsAtHomePosition(homePosition))
            {
                CompleteHomeArrival();
            }
        }
        else if (!hadActiveMoveTarget && gate != null)
        {
            Vector2 awayFromGate =
                ((Vector2)gate.ExitPosition - (Vector2)gate.EntryPosition);

            if (awayFromGate.sqrMagnitude <= 0.0001f)
            {
                awayFromGate = Vector2.up;
            }

            Vector3 nudgeTarget =
                gate.ExitPosition +
                (Vector3)(awayFromGate.normalized *
                Mathf.Max(0.75f, targetClearRadius * 3f));

            if (TryFindClearPointNear(nudgeTarget, out Vector3 clearPoint))
            {
                SetDirectMoveTarget(clearPoint);
            }
        }

        ClampInsideCurrentMapArea();
    }

    void UpdateUnstuck()
    {
        if (!hasDirectMoveTarget && currentTarget == null && !hasWanderTarget)
        {
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            return;
        }

        Vector2 escapeDirection =
            desiredVelocity.sqrMagnitude > 0.0001f
            ? desiredVelocity.normalized
            : GetDirectionToActiveMoveTarget();

        if (escapeDirection.sqrMagnitude <= 0.0001f)
        {
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            return;
        }

        float moved = Vector2.Distance(transform.position, lastUnstuckPosition);
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

        Vector3 escapeTarget;
        if (!TryPickObstacleEscapeTarget(escapeDirection, out escapeTarget) &&
            (!ignoreNpcBodyCollisions &&
            !TryPickCrowdEscapeTarget(out escapeTarget)))
        {
            Vector2 offset =
                Random.insideUnitCircle.normalized *
                Mathf.Max(0.1f, unstuckOffsetRadius);
            escapeTarget =
                ClampToCurrentMapArea(transform.position + (Vector3)offset);
        }

        Vector2 toEscapeTarget =
            (Vector2)escapeTarget - (Vector2)transform.position;

        ClearActivePath();
        hasRoadPreference = false;
        obstacleAvoidTarget = escapeTarget;
        obstacleAvoidUntil = Time.time + Mathf.Max(0.8f, unstuckCheckDelay);
        hasObstacleAvoidTarget = true;
        desiredVelocity =
            toEscapeTarget.sqrMagnitude > 0.0001f
            ? toEscapeTarget.normalized * moveSpeed
            : escapeDirection * moveSpeed;
        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;
    }

    void ApplyNpcOverlapSeparation()
    {
        if (ignoreNpcBodyCollisions ||
            rb == null ||
            separationRadius <= 0f)
        {
            return;
        }

        Vector2 separation = GetSeparationDirection();
        if (separation.sqrMagnitude <= 0.0001f ||
            IsMovementBlocked(separation))
        {
            return;
        }

        Vector2 separationVelocity =
            separation.normalized * moveSpeed * 0.65f;

        desiredVelocity =
            desiredVelocity.sqrMagnitude > 0.0001f
            ? (desiredVelocity + separationVelocity).normalized * moveSpeed
            : separationVelocity;
    }
    void ApplySmoothVelocity()
    {
        if (rb == null)
        {
            return;
        }

        float rate =
            desiredVelocity.sqrMagnitude > rb.linearVelocity.sqrMagnitude
            ? movementAcceleration
            : movementDeceleration;

        rb.linearVelocity =
            Vector2.MoveTowards(
                rb.linearVelocity,
                desiredVelocity,
                rate * Time.fixedDeltaTime);
    }

    void ConfigureRigidbody()
    {
        if (rb == null)
        {
            return;
        }

        rb.bodyType = useKinematicNpcMovement
            ? RigidbodyType2D.Kinematic
            : RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void UpdateVisualAnimation()
    {
        if (visualAnimation == null)
        {
            return;
        }

        Vector2 animationVelocity =
            rb != null
            ? rb.linearVelocity
            : desiredVelocity;

        bool isIdle =
            animationVelocity.sqrMagnitude <=
            animationIdleSpeed * animationIdleSpeed;

        Vector2 direction =
            isIdle
            ? Vector2.zero
            : animationVelocity.normalized;

        if (visualAnimation.debugVisualLogs)
        {
            Debug.Log(
                "[VillagerAI] Visual input object=" +
                gameObject.name +
                " velocity=" + animationVelocity +
                " speed=" + animationVelocity.magnitude.ToString("F3") +
                " idleThreshold=" + animationIdleSpeed.ToString("F3") +
                " isIdle=" + isIdle +
                " desiredVelocity=" + desiredVelocity +
                " action=" + currentAction);
        }

        visualAnimation.UpdateNPCAnimation(direction, isIdle, currentAction);
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

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        foreach (Collider2D own in ownColliders)
        {
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

        Vector3 escapePoint = transform.position +
            (Vector3)(normal.normalized * Mathf.Max(unstuckOffsetRadius, targetClearRadius * 2f));

        Vector3 clearPoint;
        if (!TryFindClearPointNear(escapePoint, out clearPoint))
        {
            return;
        }

        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = true;
        directMoveTarget = clearPoint;
        desiredVelocity =
            normal.normalized *
            moveSpeed *
            0.75f;
        blockedMoveTimer = 0f;
    }

    void ResolveInitialObstacleOverlap()
    {
        if (!IsPositionBlocked(transform.position) &&
            !HasBlockingColliderOverlap())
        {
            return;
        }

        if (!TryFindClearPointNear(transform.position, out Vector3 clearPoint))
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
    }

    bool HasBlockingColliderOverlap()
    {
        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        Collider2D[] hits = new Collider2D[32];

        foreach (Collider2D own in ownColliders)
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

    bool TryPickWanderTarget(out Vector3 target)
    {
        Vector3 center = currentMapArea != null && currentMapArea.areaBounds != null
            ? currentMapArea.areaBounds.bounds.center
            : spawnPosition;

        for (int i = 0; i < maxPickTargetAttempts; i++)
        {
            Vector2 random = Random.insideUnitCircle * Mathf.Max(0.1f, wanderRadius);
            Vector3 candidate = ClampToCurrentMapArea(center + new Vector3(random.x, random.y, 0f));

            if (Vector2.Distance(transform.position, candidate) >=
                Mathf.Max(arriveDistance * 2f, minWanderTargetDistance) &&
                IsMoveTargetFeasible(candidate) &&
                HasClearLineTo(candidate))
            {
                target = candidate;
                return true;
            }
        }

        return TryFindClearPointNear(transform.position, out target);
    }

    bool TryFindClearPointNear(Vector3 preferred, out Vector3 result)
    {
        preferred = ClampToCurrentMapArea(preferred);
        if (IsMoveTargetFeasible(preferred))
        {
            result = preferred;
            return true;
        }

        float baseRadius = Mathf.Max(targetClearRadius * 2f, 0.25f);
        int angleSteps = Mathf.Max(8, maxPickTargetAttempts);
        for (int radiusStep = 0; radiusStep < 6; radiusStep++)
        {
            float radius = baseRadius + radiusStep * 0.2f;
            for (int angleStep = 0; angleStep < angleSteps; angleStep++)
            {
                float angle =
                    (angleStep / (float)angleSteps) * Mathf.PI * 2f +
                    radiusStep * 0.17f;
                Vector2 offset =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    radius;
                Vector3 candidate =
                    ClampToCurrentMapArea(
                        preferred + new Vector3(offset.x, offset.y, 0f));

                if (IsMoveTargetFeasible(candidate))
                {
                    result = candidate;
                    return true;
                }
            }
        }

        result = transform.position;
        return IsMoveTargetFeasible(result);
    }

    bool IsMoveTargetFeasible(Vector3 position)
    {
        if (!IsInsideCurrentMapArea(position))
        {
            return false;
        }

        return !IsPositionBlocked(position);
    }

    bool HasClearLineTo(Vector3 target)
    {
        return HasClearLineTo(
            target,
            transform.position);
    }

    bool HasClearLineTo(
        Vector3 target,
        Vector3 originPosition)
    {
        Vector2 origin = originPosition;
        Vector2 delta = (Vector2)target - origin;
        float distance = delta.magnitude;
        if (distance <= targetClearRadius)
        {
            return true;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
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

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            transform.position,
            Mathf.Max(0.01f, GetBodyClearRadius()),
            direction.normalized,
            GetObstacleLookAheadDistance());

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                return true;
            }
        }

        return false;
    }

    bool TryChooseDetourDirection(
        Vector2 desiredDirection,
        Vector3 finalTarget,
        out Vector2 detourDirection)
    {
        detourDirection = Vector2.zero;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired =
            desiredDirection.normalized;

        Vector2 toTarget =
            ((Vector2)finalTarget - (Vector2)transform.position);

        Vector2 targetDirection =
            toTarget.sqrMagnitude > 0.0001f
            ? toTarget.normalized
            : desired;

        float lookAhead =
            GetObstacleLookAheadDistance();

        float bestScore =
            float.NegativeInfinity;

        bool found =
            false;

        for (int i = 0; i < DetourAngles.Length; i++)
        {
            float angle =
                DetourAngles[i];

            if (TryScoreDetourDirection(
                    RotateDirection(desired, angle),
                    desired,
                    targetDirection,
                    lookAhead,
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

            if (TryScoreDetourDirection(
                    RotateDirection(desired, -angle),
                    desired,
                    targetDirection,
                    lookAhead,
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

    bool TrySetObstacleAvoidTarget(
        Vector2 blockedDirection,
        Vector3 finalTarget)
    {
        Vector2 desired =
            blockedDirection.sqrMagnitude > 0.0001f
            ? blockedDirection.normalized
            : ((Vector2)finalTarget - (Vector2)transform.position).normalized;

        if (desired.sqrMagnitude <= 0.0001f)
        {
            desired = Vector2.up;
        }

        Vector2 side =
            new Vector2(-desired.y, desired.x);

        float baseDistance =
            Mathf.Max(
                unstuckOffsetRadius,
                obstacleDetourLookAhead,
                targetClearRadius * 3f);

        Vector2[] directions =
        {
            side,
            -side,
            (side - desired * 0.35f).normalized,
            (-side - desired * 0.35f).normalized,
            -desired
        };

        for (int radiusStep = 0; radiusStep < 3; radiusStep++)
        {
            float distance =
                baseDistance + radiusStep * targetClearRadius * 2f;

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 candidateDirection =
                    directions[i];

                if (candidateDirection.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                Vector3 candidate =
                    ClampToCurrentMapArea(
                        transform.position +
                        (Vector3)(candidateDirection.normalized * distance));

                if (!IsMoveTargetFeasible(candidate) ||
                    !HasClearLineTo(candidate))
                {
                    continue;
                }

                Vector2 toCandidate =
                    (Vector2)candidate - (Vector2)transform.position;

                if (toCandidate.sqrMagnitude <= 0.0001f ||
                    IsMovementBlocked(toCandidate.normalized))
                {
                    continue;
                }

                obstacleAvoidTarget = candidate;
                obstacleAvoidUntil = Time.time + 1.2f;
                hasObstacleAvoidTarget = true;
                blockedMoveTimer = 0f;
                desiredVelocity =
                    toCandidate.normalized *
                    moveSpeed;
                return true;
            }
        }

        return false;
    }

    bool TryCommitObstacleAvoidTarget(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = direction.normalized;
        float distance = Mathf.Max(
            unstuckOffsetRadius,
            obstacleDetourLookAhead,
            targetClearRadius * 3f);

        Vector3 candidate =
            ClampToCurrentMapArea(
                transform.position +
                (Vector3)(desired * distance));

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
        desiredVelocity = desired * moveSpeed;
        return true;
    }

    void OnDisable()
    {
        activeVillagers.Remove(this);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        activeVillagers.Remove(this);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
    }

    bool TryCommitObstacleScanTarget(
        Vector2 desiredDirection,
        Vector3 finalTarget)
    {
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

        float scanDistance = Mathf.Max(
            obstacleCheckDistance * 2f,
            obstacleScanDistance,
            obstacleDetourLookAhead * 2f);
        float scanStep = Mathf.Max(0.1f, obstacleScanStep);
        float startDistance = Mathf.Max(
            targetClearRadius * 2f,
            obstacleCheckDistance * 0.75f);
        Vector2 side = new Vector2(-desired.y, desired.x);
        Vector2 sideOffset =
            side * Mathf.Max(targetClearRadius * 1.5f, 0.3f);

        bool sawBlocked = false;

        for (float distance = startDistance;
            distance <= scanDistance;
            distance += scanStep)
        {
            Vector2 forwardPoint =
                (Vector2)transform.position + desired * distance;

            bool forwardBlocked =
                !IsInsideCurrentMapArea(forwardPoint) ||
                IsPositionBlocked(forwardPoint) ||
                !HasClearLineTo(forwardPoint);

            if (forwardBlocked)
            {
                sawBlocked = true;
            }

            if (!sawBlocked)
            {
                continue;
            }

            Vector2[] candidates =
            {
                forwardPoint,
                forwardPoint + sideOffset,
                forwardPoint - sideOffset
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 candidate = ClampToCurrentMapArea(candidates[i]);

                if (!IsInsideCurrentMapArea(candidate) ||
                    IsPositionBlocked(candidate) ||
                    !HasClearLineTo(candidate))
                {
                    continue;
                }

                Vector2 toCandidate =
                    candidate - (Vector2)transform.position;

                if (toCandidate.sqrMagnitude <= 0.0001f ||
                    Vector2.Dot(toCandidate.normalized, targetDirection) < -0.05f)
                {
                    continue;
                }

                obstacleAvoidTarget = candidate;
                obstacleAvoidUntil = Time.time + 1.6f;
                hasObstacleAvoidTarget = true;
                blockedMoveTimer = 0f;
                desiredVelocity = toCandidate.normalized * moveSpeed;
                return true;
            }
        }

        return false;
    }

      bool TryResolveCrowdAhead(
          Vector2 desiredDirection,
          Vector3 finalTarget,
          out Vector2 resolvedDirection)
      {
          resolvedDirection = desiredDirection;

          if (ignoreNpcBodyCollisions)
          {
              return true;
          }

          if (Time.time < crowdDirectionCommitUntil &&
              crowdCommittedDirection.sqrMagnitude > 0.0001f)
          {
              resolvedDirection = crowdCommittedDirection.normalized;
              return true;
          }

          if (desiredDirection.sqrMagnitude <= 0.0001f ||
              crowdLookAheadDistance <= 0f)
        {
            return true;
        }

        Collider2D other;
        if (!TryFindNpcAhead(desiredDirection, out other))
        {
            crowdBlockedTimer = 0f;
            return true;
        }

        crowdBlockedTimer += Time.fixedDeltaTime;
        if (crowdBlockedTimer >= unstuckCheckDelay)
        {
            if (TryPickCrowdEscapeTarget(out Vector3 escapeTarget))
            {
                crowdBlockedTimer = 0f;
                ClearActivePath();
                SetDirectMoveTarget(escapeTarget, true);
                StopMoving();
                return false;
            }

            crowdBlockedTimer = 0f;
        }

          if (TryForceCrowdStepAside(
                  desiredDirection,
                  finalTarget,
                  other,
                  out resolvedDirection))
          {
              crowdBlockedTimer = 0f;
              crowdCommittedDirection = resolvedDirection;
              crowdDirectionCommitUntil = Time.time + 0.35f;
              return true;
          }

          if (ShouldYieldToNpc(other))
          {
              crowdYieldUntil =
                  Time.time +
                  Mathf.Max(0.05f, crowdYieldDuration) *
                  Random.Range(0.75f, 1.35f);
              crowdCommittedDirection = desiredDirection;
              crowdDirectionCommitUntil =
                  Time.time + Mathf.Max(0.1f, crowdYieldDuration * 0.5f);
              StopMoving();
              return false;
          }

          if (TryChooseCrowdDetourDirection(
                  desiredDirection,
                  finalTarget,
                  other,
                  out resolvedDirection))
          {
              crowdBlockedTimer = 0f;
              crowdCommittedDirection = resolvedDirection;
              crowdDirectionCommitUntil = Time.time + 0.35f;
              return true;
          }

          crowdYieldUntil =
              Time.time +
              Mathf.Max(0.05f, crowdYieldDuration) *
              Random.Range(0.75f, 1.35f);
          crowdCommittedDirection = desiredDirection;
          crowdDirectionCommitUntil =
              Time.time + Mathf.Max(0.1f, crowdYieldDuration * 0.5f);
          StopMoving();
          return false;
      }

    bool TryFindNpcAhead(
        Vector2 direction,
        out Collider2D npcCollider)
    {
        npcCollider = null;

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                transform.position,
                Mathf.Max(0.01f, GetBodyClearRadius()),
                direction.normalized,
                Mathf.Max(separationRadius, crowdLookAheadDistance),
                villagerLayers);

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

    bool TryChooseCrowdDetourDirection(
        Vector2 desiredDirection,
        Vector3 finalTarget,
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
            Mathf.Max(crowdDetourDistance, separationRadius, targetClearRadius * 2f);

        for (int i = 0; i < 2; i++)
        {
            Vector2 candidateSide =
                i == 0 ? side : -side;

            Vector3 candidate =
                ClampToCurrentMapArea(
                    transform.position +
                    (Vector3)((candidateSide + desired * 0.35f).normalized * distance));

            if (!IsMoveTargetFeasible(candidate) ||
                !HasClearLineTo(candidate))
            {
                continue;
            }

            Vector2 toCandidate =
                (Vector2)candidate - (Vector2)transform.position;

            if (toCandidate.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            detourDirection = toCandidate.normalized;
            return true;
        }

        return false;
    }

    bool TryPickCrowdEscapeTarget(out Vector3 target)
    {
        if (ignoreNpcBodyCollisions)
        {
            target = transform.position;
            return false;
        }

        target = transform.position;

        Vector2 baseDirection = desiredVelocity.sqrMagnitude > 0.0001f
            ? desiredVelocity.normalized
            : Vector2.zero;

        if (baseDirection.sqrMagnitude <= 0.0001f)
        {
            Vector3 targetPosition =
                hasDirectMoveTarget
                ? directMoveTarget
                : hasWanderTarget
                    ? wanderTarget
                    : currentTarget != null
                        ? currentTarget.position
                        : transform.position;

            baseDirection =
                ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        }

        if (baseDirection.sqrMagnitude <= 0.0001f)
        {
            baseDirection = Vector2.up;
        }

        Vector2 side =
            new Vector2(-baseDirection.y, baseDirection.x);

        float distance =
            Mathf.Max(crowdDetourDistance, unstuckOffsetRadius, targetClearRadius * 2f);

        for (int i = 0; i < 4; i++)
        {
            Vector2 candidateDirection =
                i == 0 ? side :
                i == 1 ? -side :
                i == 2 ? (side + baseDirection).normalized :
                (-side + baseDirection).normalized;

            Vector3 candidate =
                ClampToCurrentMapArea(
                    transform.position +
                    (Vector3)(candidateDirection * distance));

            if (IsMoveTargetFeasible(candidate) &&
                HasClearLineTo(candidate))
            {
                target = candidate;
                return true;
            }
        }

        return false;
    }

    bool TryForceCrowdStepAside(
        Vector2 desiredDirection,
        Vector3 finalTarget,
        Collider2D other,
        out Vector2 detourDirection)
    {
        if (ignoreNpcBodyCollisions)
        {
            detourDirection = desiredDirection;
            return false;
        }

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

        Vector2 side = new Vector2(-desired.y, desired.x);
        if (ShouldUseRightSide(other))
        {
            side = -side;
        }

        float distance = Mathf.Max(
            crowdDetourDistance,
            separationRadius,
            targetClearRadius * 2.5f);

        Vector2[] candidates =
        {
            side,
            -side,
            side + desired * 0.25f,
            -side + desired * 0.25f
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2 candidate = candidates[i];
            if (candidate.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            candidate.Normalize();
            Vector2 nextPoint =
                (Vector2)transform.position + candidate * distance;

              if (!IsInsideCurrentMapArea(nextPoint) ||
                  IsPositionBlocked(nextPoint) ||
                  !HasClearLineTo(nextPoint))
              {
                  continue;
              }

              float progress = Vector2.Dot(candidate, targetDirection);
              if (progress < -0.05f)
              {
                  continue;
              }

              detourDirection = candidate;
              crowdCommittedDirection = detourDirection;
              crowdDirectionCommitUntil = Time.time + 0.35f;
              return true;
          }

        return false;
    }

    Vector2 GetDirectionToActiveMoveTarget()
    {
        Vector3 targetPosition =
            hasObstacleAvoidTarget
            ? obstacleAvoidTarget
            : hasDirectMoveTarget
                ? directMoveTarget
                : hasWanderTarget
                    ? wanderTarget
                    : currentTarget != null
                        ? currentTarget.position
                        : transform.position;

        Vector2 direction =
            (Vector2)targetPosition - (Vector2)transform.position;

        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.zero;
    }

    bool TryPickObstacleEscapeTarget(
        Vector2 blockedDirection,
        out Vector3 target)
    {
        target = transform.position;

        Vector2 forward =
            blockedDirection.sqrMagnitude > 0.0001f
            ? blockedDirection.normalized
            : GetDirectionToActiveMoveTarget();

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Random.insideUnitCircle.normalized;
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector2.up;
        }

        Vector2 side = new Vector2(-forward.y, forward.x);
        float baseDistance =
            Mathf.Max(unstuckOffsetRadius, targetClearRadius * 3f);

        Vector2[] directions =
        {
            side,
            -side,
            (side - forward * 0.5f).normalized,
            (-side - forward * 0.5f).normalized,
            -forward,
            (side + forward * 0.25f).normalized,
            (-side + forward * 0.25f).normalized
        };

        float bestScore = float.NegativeInfinity;
        Vector3 bestTarget = transform.position;
        bool found = false;

        for (int radiusStep = 0; radiusStep < 4; radiusStep++)
        {
            float distance =
                baseDistance + radiusStep * Mathf.Max(targetClearRadius * 2f, 0.35f);

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 direction = directions[i];
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                direction.Normalize();
                Vector3 candidate =
                    ClampToCurrentMapArea(
                        transform.position +
                        (Vector3)(direction * distance));

                if (!IsMoveTargetFeasible(candidate) ||
                    !HasClearLineTo(candidate) ||
                    IsMovementBlocked(direction))
                {
                    continue;
                }

                float score =
                    GetClearDistance(direction, GetObstacleLookAheadDistance()) +
                    Mathf.Max(-0.25f, Vector2.Dot(direction, -forward)) *
                    baseDistance;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                    found = true;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        target = bestTarget;
        return true;
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

    Transform GetNpcRoot(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.transform;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        NpcMapMover2D mover =
            hit.GetComponentInParent<NpcMapMover2D>();
        return mover != null ? mover.transform : null;
    }

    bool TryScoreDetourDirection(
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
            (Vector3)(candidate * Mathf.Max(targetClearRadius * 2f, lookAhead * 0.65f));

        if (!IsInsideCurrentMapArea(nextPoint) ||
            IsPositionBlocked(nextPoint))
        {
            return false;
        }

        float clearDistance =
            GetClearDistance(candidate, lookAhead);

        if (clearDistance < targetClearRadius * 2f)
        {
            return false;
        }

        float progressScore =
            Mathf.Max(-0.5f, Vector2.Dot(candidate, targetDirection));

        float smoothScore =
            Mathf.Max(-0.5f, Vector2.Dot(candidate, desired));

        score =
            clearDistance / Mathf.Max(0.01f, lookAhead) * 3f +
            progressScore * 2f +
            smoothScore;

        return true;
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

        float best =
            maxDistance;

        foreach (RaycastHit2D hit in hits)
        {
            if (IsBlockingObstacle(hit.collider))
            {
                best =
                    Mathf.Min(best, hit.distance);
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

    Vector2 RotateDirection(
        Vector2 direction,
        float degrees)
    {
        float radians =
            degrees * Mathf.Deg2Rad;

        float sin =
            Mathf.Sin(radians);

        float cos =
            Mathf.Cos(radians);

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

    bool IsPositionBlocked(Vector3 position)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
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

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        foreach (Collider2D own in ownColliders)
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

    bool IsSelfCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
        {
            return true;
        }

        if (ownColliders == null)
        {
            return false;
        }

        foreach (Collider2D own in ownColliders)
        {
            if (own == hit)
            {
                return true;
            }
        }

        return false;
    }

    void HandleBlockedMovement(Vector3 blockedTarget, Vector3 finalTarget)
    {
        if (TryCommitObstacleScanTarget(
                (Vector2)finalTarget - (Vector2)transform.position,
                finalTarget))
        {
            return;
        }

        Vector2 escapeDirection =
            (Vector2)finalTarget - (Vector2)transform.position;

        if (escapeDirection.sqrMagnitude > 0.0001f &&
            TryChooseDetourDirection(
                escapeDirection,
                finalTarget,
                out Vector2 detourDirection) &&
            TryCommitObstacleAvoidTarget(detourDirection))
        {
            return;
        }

        blockedMoveTimer += Time.fixedDeltaTime;
        StopMoving();
        ClearActivePath();

        if (blockedMoveTimer < blockedTargetRetryDelay)
        {
            return;
        }

        blockedMoveTimer = 0f;

        if (hasWanderTarget)
        {
            hasWanderTarget = false;
            return;
        }

        if (hasDirectMoveTarget)
        {
            Vector3 fallback;
            Vector3 escapeSeed =
                GetBlockedEscapeSeed(blockedTarget);

            if (TryFindClearPointNear(escapeSeed, out fallback) &&
                Vector2.Distance(fallback, transform.position) >
                arriveDistance)
            {
                directMoveTarget = fallback;
            }
            else
            {
                hasDirectMoveTarget = false;
            }
        }

        if (!hasWanderTarget &&
            !hasDirectMoveTarget &&
            currentTarget == null)
        {
            actionTimer = Mathf.Max(actionTimer, thinkInterval);

            if (IsActionLocked ||
                IsRoutineTravelOrCultivationAction(currentAction))
            {
                return;
            }

            currentAction = NpcText.Action("idle");
        }
    }

    Vector3 GetBlockedEscapeSeed(Vector3 blockedTarget)
    {
        Vector2 away =
            (Vector2)(transform.position - blockedTarget);

        if (away.sqrMagnitude <= 0.0001f)
        {
            away =
                UnityEngine.Random.insideUnitCircle;
        }

        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.up;
        }

        away.Normalize();

        return ClampToCurrentMapArea(
            transform.position +
            (Vector3)(away * Mathf.Max(unstuckOffsetRadius, targetClearRadius * 3f)));
    }

    bool ShouldRequirePathForDirectMove(Vector3 finalTarget)
    {
        if (!requireClearLineForDirectMove ||
            !useSmartPathfinding)
        {
            return false;
        }

        if (Vector2.Distance(transform.position, finalTarget) <=
            directMovePathDistance)
        {
            return false;
        }

        return !HasClearLineTo(finalTarget);
    }

    bool TryGetSmartPathWaypoint(
        Vector3 finalTarget,
        out Vector3 waypoint)
    {
        waypoint = finalTarget;

        if (!useSmartPathfinding)
        {
            ClearActivePath();
            return false;
        }

        if (activePath.Count > 0 &&
            Vector2.Distance(activePathTarget, finalTarget) >
            pathReplanTargetDistance)
        {
            ClearActivePath();
        }

        if (HasClearLineTo(finalTarget))
        {
            ClearActivePath();
            return false;
        }

        if (activePath.Count == 0 &&
            !TryBuildSmartPath(finalTarget))
        {
            return false;
        }

        SkipVisiblePathWaypoints();

        if (activePathIndex < 0 ||
            activePathIndex >= activePath.Count)
        {
            ClearActivePath();
            return false;
        }

        waypoint = activePath[activePathIndex];
        return true;
    }

    bool TryBuildSmartPath(Vector3 finalTarget)
    {
        if (!useSmartPathfinding ||
            Time.time < nextSmartPathAllowedTime)
        {
            return false;
        }

        nextSmartPathAllowedTime =
            Time.time + Mathf.Max(2f, pathReplanCooldown);

        NpcPerformanceOverlay.RecordPathRequest();
        float pathStartTime =
            Time.realtimeSinceStartup;
        int visited = 0;

        ClearActivePath();

        if (!useSmartPathfinding ||
            pathCellSize <= 0.05f ||
            !IsInsideCurrentMapArea(finalTarget))
        {
            RecordSmartPathResult(false, visited, pathStartTime);
            return false;
        }

        Vector3 start =
            ClampToCurrentMapArea(transform.position);

        finalTarget =
            ClampToCurrentMapArea(finalTarget);

        if (!IsInsidePathSearchDistance(start, finalTarget))
        {
            RecordSmartPathResult(false, visited, pathStartTime);
            return false;
        }

        Vector2Int startCell =
            WorldToPathCell(start);

        Vector2Int targetCell =
            WorldToPathCell(finalTarget);

        if (!IsPathCellWalkable(startCell) &&
            !TryFindNearestWalkableCell(startCell, out startCell))
        {
            RecordSmartPathResult(false, visited, pathStartTime);
            return false;
        }

        if (!IsPathCellWalkable(targetCell) &&
            !TryFindNearestWalkableCell(targetCell, out targetCell))
        {
            RecordSmartPathResult(false, visited, pathStartTime);
            return false;
        }

        start =
            PathCellToWorld(startCell);

        finalTarget =
            PathCellToWorld(targetCell);

        bool hasRememberedPath =
            TryGetRememberedPathCandidate(start, finalTarget);

        if (hasRememberedPath &&
            !compareRememberedPathWithNewPath)
        {
            NpcPerformanceOverlay.RecordPathCacheHit();
            RecordSmartPathResult(true, visited, pathStartTime);
            return ApplyRememberedPath(start, finalTarget);
        }

        Dictionary<Vector2Int, PathNode> nodes =
            new Dictionary<Vector2Int, PathNode>();

        List<PathNode> open =
            new List<PathNode>();

        HashSet<Vector2Int> closed =
            new HashSet<Vector2Int>();

        PathNode startNode =
            new PathNode(startCell, null, 0, GetPathHeuristic(startCell, targetCell));

        nodes[startCell] = startNode;
        open.Add(startNode);

        while (open.Count > 0 && visited < maxPathNodes)
        {
            PathNode current =
                PopLowestCostNode(open);

            if (current.cell == targetCell)
            {
                BuildPathCandidate(
                    current,
                    finalTarget,
                    computedPathBuffer);

                if (computedPathBuffer.Count == 0)
                {
                    if (hasRememberedPath)
                    {
                        NpcPerformanceOverlay.RecordPathCacheHit();
                    }

                    RecordSmartPathResult(hasRememberedPath, visited, pathStartTime);
                    return hasRememberedPath &&
                        ApplyRememberedPath(start, finalTarget);
                }

                if (hasRememberedPath &&
                    IsRememberedPathBetter(
                        start,
                        finalTarget,
                        computedPathBuffer))
                {
                    NpcPerformanceOverlay.RecordPathCacheHit();
                    RecordSmartPathResult(true, visited, pathStartTime);
                    return ApplyRememberedPath(start, finalTarget);
                }

                ApplyPathCandidate(
                    computedPathBuffer,
                    finalTarget);

                RememberActivePath(start, finalTarget);
                RecordSmartPathResult(activePath.Count > 0, visited, pathStartTime);
                return activePath.Count > 0;
            }

            closed.Add(current.cell);
            visited++;

            for (int i = 0; i < PathNeighborOffsets.Length; i++)
            {
                Vector2Int offset =
                    PathNeighborOffsets[i];

                Vector2Int nextCell =
                    current.cell + offset;

                if (closed.Contains(nextCell) ||
                    !IsPathStepWalkable(current.cell, nextCell, offset))
                {
                    continue;
                }

                int stepCost =
                    offset.x != 0 && offset.y != 0
                    ? 14
                    : 10;

                int newCost =
                    current.gCost + stepCost;

                if (nodes.TryGetValue(nextCell, out PathNode nextNode))
                {
                    if (newCost >= nextNode.gCost)
                    {
                        continue;
                    }

                    nextNode.parent = current;
                    nextNode.gCost = newCost;
                    nextNode.hCost =
                        GetPathHeuristic(nextCell, targetCell);
                }
                else
                {
                    nextNode =
                        new PathNode(
                            nextCell,
                            current,
                            newCost,
                            GetPathHeuristic(nextCell, targetCell));

                    nodes[nextCell] = nextNode;
                    open.Add(nextNode);
                }
            }
        }

        if (hasRememberedPath)
        {
            NpcPerformanceOverlay.RecordPathCacheHit();
        }

        RecordSmartPathResult(hasRememberedPath, visited, pathStartTime);

        return hasRememberedPath &&
            ApplyRememberedPath(start, finalTarget);
    }

    float GetElapsedPathMs(float pathStartTime)
    {
        return (Time.realtimeSinceStartup - pathStartTime) * 1000f;
    }

    void RecordSmartPathResult(
        bool success,
        int visited,
        float pathStartTime)
    {
        NpcPerformanceOverlay.RecordPathResult(
            success,
            visited,
            GetElapsedPathMs(pathStartTime));

        float baseDelay = Mathf.Max(2f, pathReplanCooldown);
        if (success)
        {
            consecutiveSmartPathFailures = 0;
            nextSmartPathAllowedTime = Time.time + baseDelay;
            return;
        }

        consecutiveSmartPathFailures =
            Mathf.Min(consecutiveSmartPathFailures + 1, 4);

        float failDelay =
            Mathf.Min(
                Mathf.Max(8f, baseDelay),
                baseDelay * (1f + consecutiveSmartPathFailures));

        nextSmartPathAllowedTime = Time.time + failDelay;
    }

    void BuildPathCandidate(
        PathNode endNode,
        Vector3 finalTarget,
        List<Vector3> output)
    {
        output.Clear();

        List<Vector3> reversed =
            new List<Vector3>();

        PathNode current =
            endNode;

        int steps = 0;

        while (current != null && steps < maxPathSteps)
        {
            reversed.Add(PathCellToWorld(current.cell));
            current = current.parent;
            steps++;
        }

        if (current != null)
        {
            return;
        }

        for (int i = reversed.Count - 1; i >= 0; i--)
        {
            Vector3 point =
                ClampToCurrentMapArea(reversed[i]);

            if (Vector2.Distance(point, transform.position) <=
                pathWaypointReachDistance)
            {
                continue;
            }

            output.Add(point);
        }

        if (output.Count == 0 ||
            Vector2.Distance(output[output.Count - 1], finalTarget) >
            pathWaypointReachDistance)
        {
            output.Add(finalTarget);
        }

        SimplifyPathCandidate(output);
    }

    void ApplyPathCandidate(
        List<Vector3> source,
        Vector3 finalTarget)
    {
        activePath.Clear();
        activePath.AddRange(source);
        activePathTarget = finalTarget;
        activePathIndex = 0;
    }

    bool TryGetRememberedPathCandidate(
        Vector3 start,
        Vector3 finalTarget)
    {
        if (!useSharedPathMemory)
        {
            return false;
        }

        if (!NpcPathMemorySystem.TryGetPath(
                GetPathMemoryMapKey(),
                start,
                finalTarget,
                sharedPathMemoryCellSize,
                IsMoveTargetFeasible,
                HasClearLineTo,
                rememberedPathBuffer))
        {
            return false;
        }

        TrimPathStartForCurrentPosition(rememberedPathBuffer);
        return rememberedPathBuffer.Count > 0;
    }

    bool ApplyRememberedPath(
        Vector3 start,
        Vector3 finalTarget)
    {
        if (rememberedPathBuffer.Count == 0)
        {
            return false;
        }

        ApplyPathCandidate(
            rememberedPathBuffer,
            finalTarget);
        return true;
    }

    bool IsRememberedPathBetter(
        Vector3 start,
        Vector3 finalTarget,
        List<Vector3> computedPath)
    {
        float rememberedScore =
            GetPathCandidateScore(
                start,
                finalTarget,
                rememberedPathBuffer);

        float computedScore =
            GetPathCandidateScore(
                start,
                finalTarget,
                computedPath);

        return rememberedScore <= computedScore;
    }

    float GetPathCandidateScore(
        Vector3 start,
        Vector3 finalTarget,
        List<Vector3> path)
    {
        if (path == null ||
            path.Count == 0)
        {
            return float.PositiveInfinity;
        }

        float score = 0f;
        Vector3 previous = start;
        Vector2 previousDirection = Vector2.zero;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 point = path[i];
            Vector2 segment = point - previous;
            float length = segment.magnitude;

            score += length;

            if (length > 0.001f)
            {
                Vector2 direction = segment / length;
                if (previousDirection.sqrMagnitude > 0.0001f)
                {
                    score +=
                        (1f - Mathf.Clamp01(
                            Vector2.Dot(previousDirection, direction))) *
                        pathTurnPenalty;
                }

                previousDirection = direction;
            }

            previous = point;
        }

        score += Vector2.Distance(previous, finalTarget);
        return score;
    }

    void TrimPathStartForCurrentPosition(List<Vector3> path)
    {
        if (path == null)
        {
            return;
        }

        for (int i = path.Count - 1; i >= 0; i--)
        {
            path[i] =
                ClampToCurrentMapArea(path[i]);

            if (Vector2.Distance(path[i], transform.position) <=
                pathWaypointReachDistance)
            {
                path.RemoveAt(i);
            }
        }
    }

    void RememberActivePath(
        Vector3 start,
        Vector3 finalTarget)
    {
        if (!useSharedPathMemory ||
            activePath.Count == 0)
        {
            return;
        }

        NpcPathMemorySystem.RememberPath(
            GetPathMemoryMapKey(),
            start,
            finalTarget,
            sharedPathMemoryCellSize,
            activePath);
    }

    string GetPathMemoryMapKey()
    {
        RefreshCurrentMapArea();

        if (currentMapArea != null)
        {
            return gameObject.scene.name + ":" +
                currentMapArea.zone + ":" +
                currentMapArea.name;
        }

        return gameObject.scene.name;
    }

    void SimplifyPathCandidate(List<Vector3> path)
    {
        if (path == null ||
            path.Count <= 2)
        {
            return;
        }

        List<Vector3> simplified =
            new List<Vector3>();

        int index = 0;

        while (index < path.Count)
        {
            int next = index + 1;

            for (int i = path.Count - 1; i > index; i--)
            {
                if (HasClearLineTo(path[i], path[index]))
                {
                    next = i;
                    break;
                }
            }

            simplified.Add(path[index]);
            index = next;
        }

        path.Clear();
        path.AddRange(simplified);
    }

    void SkipVisiblePathWaypoints()
    {
        while (activePathIndex < activePath.Count - 1 &&
            HasClearLineTo(activePath[activePathIndex + 1], activePath[activePathIndex]))
        {
            activePathIndex++;
        }
    }

    void AdvanceActivePathWaypoint()
    {
        activePathIndex++;

        if (activePathIndex >= activePath.Count)
        {
            ClearActivePath();
        }
    }

    void ClearActivePath()
    {
        activePath.Clear();
        activePathIndex = 0;
        activePathTarget = Vector3.zero;
    }

    bool TryFindNearestWalkableCell(
        Vector2Int origin,
        out Vector2Int result)
    {
        int maxRadius =
            Mathf.CeilToInt(
                Mathf.Max(targetClearRadius * 3f, pathCellSize) /
                Mathf.Max(0.05f, pathCellSize)) + 3;

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius &&
                        Mathf.Abs(y) != radius)
                    {
                        continue;
                    }

                    Vector2Int candidate =
                        origin + new Vector2Int(x, y);

                    if (IsPathCellWalkable(candidate))
                    {
                        result = candidate;
                        return true;
                    }
                }
            }
        }

        result = origin;
        return false;
    }

    bool IsInsidePathSearchDistance(
        Vector3 start,
        Vector3 target)
    {
        float searchDistance =
            GetEffectivePathSearchDistance();

        return searchDistance <= 0f ||
            Vector2.Distance(start, target) <= searchDistance;
    }

    float GetEffectivePathSearchDistance()
    {
        if (maxPathSearchDistance > 0f)
        {
            return maxPathSearchDistance;
        }

        if (currentMapArea != null &&
            currentMapArea.areaBounds != null)
        {
            Bounds bounds =
                currentMapArea.areaBounds.bounds;

            return Mathf.Max(
                bounds.size.x,
                bounds.size.y) +
                pathCellSize * 4f;
        }

        return 0f;
    }

    bool IsPathStepWalkable(
        Vector2Int from,
        Vector2Int to,
        Vector2Int offset)
    {
        if (!IsPathCellWalkable(to))
        {
            return false;
        }

        if (offset.x == 0 || offset.y == 0)
        {
            return true;
        }

        return IsPathCellWalkable(from + new Vector2Int(offset.x, 0)) &&
            IsPathCellWalkable(from + new Vector2Int(0, offset.y));
    }

    bool IsPathCellWalkable(Vector2Int cell)
    {
        Vector3 world =
            PathCellToWorld(cell);

        return IsMoveTargetFeasible(world);
    }

    Vector2Int WorldToPathCell(Vector3 position)
    {
        float size =
            Mathf.Max(0.05f, pathCellSize);

        return new Vector2Int(
            Mathf.RoundToInt(position.x / size),
            Mathf.RoundToInt(position.y / size));
    }

    Vector3 PathCellToWorld(Vector2Int cell)
    {
        float size =
            Mathf.Max(0.05f, pathCellSize);

        return new Vector3(
            cell.x * size,
            cell.y * size,
            transform.position.z);
    }

    PathNode PopLowestCostNode(List<PathNode> open)
    {
        int bestIndex = 0;
        PathNode best = open[0];

        for (int i = 1; i < open.Count; i++)
        {
            PathNode candidate = open[i];

            if (candidate.FCost < best.FCost ||
                candidate.FCost == best.FCost &&
                candidate.hCost < best.hCost)
            {
                best = candidate;
                bestIndex = i;
            }
        }

        open.RemoveAt(bestIndex);
        return best;
    }

    int GetPathHeuristic(
        Vector2Int from,
        Vector2Int to)
    {
        int dx =
            Mathf.Abs(from.x - to.x);

        int dy =
            Mathf.Abs(from.y - to.y);

        return 10 * (dx + dy) - 6 * Mathf.Min(dx, dy);
    }

    static readonly Vector2Int[] PathNeighborOffsets =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    class PathNode
    {
        public readonly Vector2Int cell;
        public PathNode parent;
        public int gCost;
        public int hCost;

        public int FCost
        {
            get
            {
                return gCost + hCost;
            }
        }

        public PathNode(
            Vector2Int cell,
            PathNode parent,
            int gCost,
            int hCost)
        {
            this.cell = cell;
            this.parent = parent;
            this.gCost = gCost;
            this.hCost = hCost;
        }
    }

    Vector2 GetSeparationDirection()
    {
        if (ignoreNpcBodyCollisions || separationRadius <= 0f)
        {
            return Vector2.zero;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                separationRadius,
                villagerLayers);

        Vector2 push = Vector2.zero;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<VillagerAI>() == null &&
                hit.GetComponentInParent<SmartNpcAI>() == null &&
                hit.GetComponentInParent<NpcMapMover2D>() == null)
            {
                continue;
            }

            Vector2 away =
                (Vector2)transform.position -
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
        int baseAge = 0;

        if (npcIdentity != null &&
            HasMeaningfulNpcIdentityData())
        {
            baseAge = npcIdentity.age;
        }
        else if (entityProfile != null &&
            entityProfile.identity != null)
        {
            baseAge = entityProfile.identity.age;
        }
        else
        {
            switch (ageGroup)
            {
                case VillagerAgeGroup.Child:
                    baseAge = 12;
                    break;
                case VillagerAgeGroup.Elder:
                    baseAge = 70;
                    break;
                default:
                    baseAge = 30;
                    break;
            }
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
        if (characterStats != null)
        {
            characterStats.TakeDamage(damage);
            SyncFromCharacterStats();

            InterruptGatheringForCombat();

            if (IsDead)
            {
                Die();
            }
            else if (bravery < 50)
            {
                currentAction = NpcText.Action("panicBurned");
                currentTarget = homePoint;
            }

            return;
        }

        if (IsDead)
        {
            return;
        }

        int finalDamage =
            CombatStatCalculator.CalculateFinalDamageInt(
                damage,
                defense);
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
                damage);
        }

        if (currentHP <= 0)
        {
            Die();
        }
        else if (bravery < 50)
        {
            currentAction = NpcText.Action("panicBurned");
            currentTarget = homePoint;
        }
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
        currentHP = 0;
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

