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
    Hunter
}

public enum VillagerMood
{
    Normal,
    Happy,
    Sad,
    Angry,
    Afraid,
    Tired
}

[RequireComponent(typeof(NpcScheduleController))]
public class VillagerAI : MonoBehaviour, IDamageable
{
    [Header("Entity Generation")]
    public bool generateFromEntityProfile = true;
    public EntityProfile entityProfile;

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
    public VillagerMood mood = VillagerMood.Normal;
    public bool strongNpcAvoidMortalWork = true;
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
    public float talkRadius = 1.2f;
    public float sharedTargetSpacingRadius = 0.45f;
    public float sharedTargetOccupancyRadius = 0.3f;
    public float sharedAnchorSpacingRadius = 0.7f;
    public LayerMask villagerLayers = ~0;
    public int acquaintanceTalkChanceBonus = 30;
    public bool requireKnownVillagerToTalk = true;
    public int minFriendshipToTalk = 3;
    public float socialScanInterval = 6f;
    public float conversationCooldown = 25f;
    public float conversationPauseDuration = 2f;
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
    public bool dailyVanBaoLauVisitEnabled = true;
    public bool staggerDailyVanBaoLauVisits = true;
    [Range(0f, 4f)] public float dailyVanBaoLauVisitStaggerHours = 2f;
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
    public float scheduledWorkHarvestSeconds = 8f;
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
    public StatItemData farmProduct;
    public StatItemData fishingProduct;
    public StatItemData huntingProduct;
    public StatItemData workerProduct;
    public int workProductMin = 1;
    public int workProductMax = 3;
    public bool farmerHarvestOnlyInMorning = true;
    public bool limitScheduledHarvestOncePerDay;
    public int farmerHarvestAmountPerDay = 1;
    public bool farmerPreferWorkPoint = true;
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
            int need = GetProfessionExpToNextLevel();
            return need <= 0
                ? 1f
                : Mathf.Clamp01(professionExp / (float)need);
        }
    }

    [Header("Runtime")]
    public string currentAction = "idle";
    public Transform currentTarget;
    public string lastWorkProductStatus;
    public string debugWorkTarget;
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
    float thinkTimer;
    float actionTimer;
    int routinePlanDay = int.MinValue;
    float routineCultivationStartHour;
    float routineCultivationEndHour;
    float nextSocialScanTime;
    float nextConversationAllowedTime;
      float movementPausedUntil;
      float crowdYieldUntil;
      float crowdDirectionCommitUntil;
      Vector2 crowdCommittedDirection;
      Vector3 obstacleAvoidTarget;
    float obstacleAvoidUntil;
    bool hasObstacleAvoidTarget;
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
    int lastProfessionHarvestDay = -1;
    bool scheduledWorkHarvestInProgress;
    StatItemData scheduledWorkHarvestProduct;
    int scheduledWorkHarvestAmount;
    int lastScheduledWorkHarvestSeconds = -1;
    int lastVanBaoLauVisitDay = -1;
    float vanBaoLauVisitAnchorHour = -1f;
    float vanBaoLauVisitDelayHours;
    int vanBaoLauVisitStep;

    Vector3 currentWorkTarget;
    NpcMapZone? currentWorkTargetZone;
    string currentWorkTargetKey;
    string currentScheduleSlotKey;
    Vector3 currentTradeTarget;
    NpcMapZone? currentTradeTargetZone;
    NpcForgeAgent currentForgeTradeTarget;
    StatItemData currentForgeTradeItem;
    Vector3 currentEatTarget;
    Vector3 currentSellTarget;
    NpcMapZone? currentSellTargetZone;
    NpcMapZone? resolvedTraderLocationZone;
    NpcMapZone? resolvedSellLocationZone;

    bool hasWorkTarget;
    bool hasTradeTarget;
    bool hasEatTarget;
    bool hasSellTarget;
    readonly System.Collections.Generic.HashSet<VillagerAI> acquaintances =
        new System.Collections.Generic.HashSet<VillagerAI>();
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

    public Transform DamageTransform => transform;

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
        InitializeVanBaoLauVisitStagger();
        RefreshCurrentMapArea(false);
        ClampInsideCurrentMapArea();

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

        EnsureScheduleController();
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

        if (realm > CultivationRealm.Mortal ||
            IsCultivationCapableVillager())
        {
            schedule.lifePath = NpcLifePath.SemiCultivator;
            schedule.canCultivate = true;
        }
        else
        {
            schedule.lifePath = NpcLifePath.Commoner;
        }

        if (schedule.autoBuildDefaultSchedule)
        {
            schedule.RebuildDefaultSchedule();
        }
    }

    public void ForceHiddenAtHome(bool hidden)
    {
        hiddenAtHome = hidden;
        if (spawnedWorldActor != null)
        {
            spawnedWorldActor.isHiddenAtHome = hidden;
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

        if (farmProduct == null)
        {
            farmProduct =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/ThucPham/Linh_Me.asset");
        }

        if (fishingProduct == null)
        {
            fishingProduct =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/NPCitem/Ca.asset");
        }

        if (huntingProduct == null)
        {
            huntingProduct =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/NPCitem/Thit.asset");
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

        villagerName = entityProfile.identity.entityName;
        ageGroup = GetAgeGroup(entityProfile.identity.age);
        if (!keepInspectorJob &&
            job == VillagerJob.None)
        {
            job = GetGeneratedJob(entityProfile.personality);
        }
        realm = entityProfile.stats.realm;
        lifespan = GetLifespanForRealm(realm);
        realmStage = entityProfile.stats.realmStage;
        cultivationExp = entityProfile.stats.cultivationExp;
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

        return Random.value < 0.5f ? VillagerJob.Farmer : VillagerJob.Worker;
    }

    void Update()
    {
        SyncCultivationEffect();

        if (hiddenAtHome)
        {
            return;
        }

        SyncFromCharacterStats();

        if (IsDead)
        {
            StopMoving();
            return;
        }

        RefreshScheduledStateForCurrentFrame();

        if (NpcTaskProvider.IsNpcBusyWithAnyProvider(gameObject))
        {
            StopMoving();
            return;
        }

        UpdateNeeds();
        UpdateMood();

        thinkTimer += Time.deltaTime;
        actionTimer -= Time.deltaTime;

        if (scheduledWorkHarvestInProgress)
        {
            RefreshScheduledHarvestAction();
        }

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

        TryTalkToPassingVillager();

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

        realm = characterStats.realm;
        realmStage = characterStats.realmStage;
        cultivationExp = characterStats.cultivationExp;
        baseExpToNextRealm = characterStats.baseExpToNextRealm;
        baseMaxHP = characterStats.baseMaxHP;
        baseAttack = characterStats.baseAttack;
        baseDefense = characterStats.baseDefense;
        maxHP = characterStats.finalHP;
        currentHP = characterStats.currentHP;
        attack = characterStats.attack;
        defense = characterStats.defense;
        moveSpeed = characterStats.moveSpeed;
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

    void UpdateMood()
    {
        if (currentHP < maxHP * 0.3f)
        {
            mood = VillagerMood.Afraid;
        }
        else if (fatigue >= 80f)
        {
            mood = VillagerMood.Tired;
        }
        else if (NeedsFood() &&
            hunger >= 80f)
        {
            mood = VillagerMood.Sad;
        }
        else if (fun >= 80f)
        {
            mood = VillagerMood.Happy;
        }
        else
        {
            mood = VillagerMood.Normal;
        }

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null && weather.MoodModifier() < -10f && mood == VillagerMood.Normal)
        {
            mood = VillagerMood.Sad;
        }

        if (entityProfile != null)
        {
            entityProfile.emotion.mood = ToEntityMood(mood);
        }
    }

    EntityMood ToEntityMood(VillagerMood source)
    {
        switch (source)
        {
            case VillagerMood.Happy:
                return EntityMood.Happy;
            case VillagerMood.Sad:
                return EntityMood.Sad;
            case VillagerMood.Angry:
                return EntityMood.Angry;
            case VillagerMood.Afraid:
                return EntityMood.Afraid;
            case VillagerMood.Tired:
                return EntityMood.Tired;
            default:
                return EntityMood.Calm;
        }
    }

    void Think()
    {
        if (WorldTimeSystem.Instance != null)
        {
            if (WorldTimeSystem.Instance.CurrentDay != lastPlanResetDay)
            {
                lastPlanResetDay = WorldTimeSystem.Instance.CurrentDay;
                ResetDailyTargets();
            }
        }

        if (homeRoutineManagedExternally &&
            !HasEnforcedSchedule() &&
            (WorldTimeSystem.Instance == null ||
            WorldTimeSystem.Instance.CurrentPhase == WorldTimePhase.Night ||
            fatigue >= 85f))
        {
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

        if (actionTimer > 0f &&
            NpcRoleUtility.IsInCombat(gameObject))
        {
            return;
        }

        if (TryRunScheduledActivity())
        {
            return;
        }

        if (actionTimer > 0f)
        {
            return;
        }

        if (ageGroup == VillagerAgeGroup.Adult &&
            job == VillagerJob.Trader)
        {
            ThinkTrader();
            return;
        }

        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return;
        }

        if (NeedsFood() &&
            hunger >= 75f)
        {
            GoEat();
            return;
        }

        if (ShouldSellGoodsNow())
        {
            GoSellGoods();
            return;
        }

        if (ageGroup == VillagerAgeGroup.Child)
        {
            ThinkChild();
            return;
        }

        ThinkAdult();
    }

    bool TryRunScheduledActivity()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        NpcScheduleActivity activity = schedule.CurrentActivity;
        ResetScheduledStateIfSlotChanged(schedule, slot, activity);

        if (slot == null)
        {
            return false;
        }

        if (slot.allowFatigueInterrupt && fatigue >= 85f)
        {
            GoHomeToRest();
            return true;
        }

        if (slot.allowHungerInterrupt &&
            NeedsFood() &&
            hunger >= 80f)
        {
            GoEat();
            return true;
        }

        switch (activity)
        {
            case NpcScheduleActivity.Sleep:
                GoHomeToRest();
                return true;

            case NpcScheduleActivity.Eat:
                if (NeedsFood())
                {
                    GoEat();
                }
                else
                {
                    GoHomeIdle(NpcText.Action("idle"));
                }
                return true;

            case NpcScheduleActivity.Work:
                GoWorkOrCultivatorActivity();
                return true;

            case NpcScheduleActivity.SellGoods:
                if (HasSellableGoods())
                {
                    GoSellGoods();
                }
                else
                {
                    GoTrade();
                }
                return true;

            case NpcScheduleActivity.BuyGoods:
                GoTrade();
                return true;

            case NpcScheduleActivity.Gather:
                if (!TryScheduledGather())
                {
                    GoWorkOrCultivatorActivity();
                }
                return true;

            case NpcScheduleActivity.Hunt:
                GoWork();
                return true;

            case NpcScheduleActivity.Cultivate:
                if (schedule.canCultivate ||
                    IsCultivationCapableVillager())
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
                        if (currentAction == NpcText.Action("goHomeCultivate") ||
                            currentAction == NpcText.Action("goCultivatePoint"))
                        {
                            if (currentTarget != null ||
                                hasDirectMoveTarget ||
                                hasWanderTarget)
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

                        schedule.MarkCurrentSlotActivityCompleted(
                            NpcScheduleActivity.Cultivate);
                        ClearCompletedCultivationAction();
                        return true;
                    }

                    CultivateNaturally();
                    if (currentAction == NpcText.Action("goHomeCultivate") ||
                        currentAction == NpcText.Action("goCultivatePoint") ||
                        currentAction == NpcText.Action("cultivate") ||
                        currentAction == NpcText.Action("cultivateAbsorbQi"))
                    {
                        schedule.MarkCurrentSlotActivityStarted(
                            NpcScheduleActivity.Cultivate);
                    }
                }
                else
                {
                    GoHomeIdle(NpcText.Action("idle"));
                }
                return true;

            case NpcScheduleActivity.TakeTask:
                TryScheduledTaskOrWait();
                return true;

            case NpcScheduleActivity.ReturnHome:
                GoHomeIdle(NpcText.Action("stayNearHome"));
                return true;

            default:
                GoHomeIdle(NpcText.Action("idle"));
                return true;
        }
    }

    void RefreshScheduledStateForCurrentFrame()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        if (slot == null)
        {
            return;
        }

        ResetScheduledStateIfSlotChanged(
            schedule,
            slot,
            schedule.CurrentActivity);
    }

    void ResetScheduledStateIfSlotChanged(
        NpcScheduleController schedule,
        NpcScheduleSlot slot,
        NpcScheduleActivity activity)
    {
        string key = BuildScheduleSlotKey(slot, activity);
        if (currentScheduleSlotKey == key)
        {
            return;
        }

        currentScheduleSlotKey = key;
        ClearMovementTargets();
        StopMoving();
        ClearTreasureHunt();
        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        actionTimer = 0f;
        currentAction = string.Empty;

        hasWorkTarget = false;
        currentWorkTarget = Vector3.zero;
        currentWorkTargetZone = null;
        currentWorkTargetKey = string.Empty;
        hasTradeTarget = false;
        currentTradeTarget = Vector3.zero;
        currentTradeTargetZone = null;
        hasEatTarget = false;
        currentEatTarget = Vector3.zero;
        hasSellTarget = false;
        currentSellTarget = Vector3.zero;
        currentSellTargetZone = null;

        scheduledWorkHarvestInProgress = false;
        scheduledWorkHarvestProduct = null;
        scheduledWorkHarvestAmount = 0;
        lastScheduledWorkHarvestSeconds = -1;

        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer != null)
        {
            gatherer.CancelGatheringNow();
        }
    }

    string BuildScheduleSlotKey(
        NpcScheduleSlot slot,
        NpcScheduleActivity activity)
    {
        if (slot == null)
        {
            return "none";
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        int day = timeSystem != null ? timeSystem.CurrentDay : 0;
        return day + ":" + activity + ":" +
            Mathf.RoundToInt(slot.startHour * 100f) + ":" +
            Mathf.RoundToInt(slot.endHour * 100f);
    }

    bool HasEnforcedSchedule()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentSlot != null;
    }

    bool TryScheduledGather()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        NpcResourceGatherer gatherer = EnsureWorkGatherer();
        if (gatherer != null &&
            gatherer.enabled &&
            gatherer.canGather)
        {
            if (job == VillagerJob.Farmer &&
                TryStartFarmerMapHarvest(gatherer))
            {
                return true;
            }

            if (job != VillagerJob.Farmer &&
                gatherer.TryStartGatheringNow())
            {
                return true;
            }
        }

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.Gather,
                job,
                NpcLocationPurpose.Resource,
                transform.position,
                out Vector3 resourcePosition,
                out NpcMapZone? resourceZone))
        {
            currentAction = NpcText.Action("gatherResource");
            MoveUsingRoad(resourcePosition, resourceZone);
            if (schedule != null)
            {
                schedule.MarkCurrentSlotActivityStarted(
                    NpcScheduleActivity.Gather);
            }
            return true;
        }

        return false;
    }

    void TryScheduledTaskOrWait()
    {
        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider == null)
        {
            GoHomeIdle(GetScheduledTradeIdleAction());
            return;
        }

        Vector3 providerPosition =
            provider.GetProviderPositionFor(gameObject);

        currentAction = NpcText.Action("goTaskProviderDaily");

        if (Vector2.Distance(transform.position, providerPosition) > arriveDistance)
        {
            MoveUsingRoad(
                providerPosition,
                NpcMapNavigator.GetDestinationZone(provider.transform));
            return;
        }

        ClearMovementTargets();
        StopMoving();

        if (!provider.TryHandleVisitor(gameObject))
        {
            actionTimer = Mathf.Max(thinkInterval, 2f);
            currentAction = NpcText.Action("visitedTaskProvider");
        }
    }

    void ThinkTrader()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;

        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return;
        }

        if (NeedsFood() &&
            hunger >= 75f)
        {
            EatWhereTraderIs();
            return;
        }

        if (TryProcessDailyTaskPlan())
        {
            return;
        }

        if (timeSystem == null)
        {
            TryTradeOrTaskOrIdle();
            return;
        }

        switch (timeSystem.CurrentPhase)
        {
            case WorldTimePhase.Evening:
            case WorldTimePhase.Night:
                GoHomeIdle(NpcText.Action("closeShopGoHome"));
                return;
            default:
                TryTradeOrTaskOrIdle();
                return;
        }
    }

    void TryTradeOrTaskOrIdle()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (ShouldVisitCounterBroker())
        {
            GoTrade();
            return;
        }

        NpcTaskProvider provider = NpcTaskProvider.FindNearestProvider(transform.position);
        if (provider != null &&
            provider.TryHandleVisitor(gameObject))
        {
            return;
        }

        GoHomeIdle(GetScheduledTradeIdleAction());
    }

    void ThinkChild()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null &&
            (timeSystem.CurrentPhase == WorldTimePhase.Night ||
            timeSystem.CurrentPhase == WorldTimePhase.Dawn))
        {
            GoHomeToRest();
            return;
        }

        if (NeedsFood() &&
            hunger >= 60f)
        {
            GoEat();
            return;
        }

        if (fun <= 70f)
        {
            GatherAndPlay();
            return;
        }

        GoHomeIdle(NpcText.Action("stayNearHome"));
    }

    void ThinkAdult()
    {
        if (NeedsFood() &&
            hunger >= 70f)
        {
            GoEat();
            return;
        }

        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return;
        }

        if (IsRoutineTravelOrCultivationAction(currentAction))
        {
            return;
        }

        if (TryProcessDailyTaskPlan())
        {
            return;
        }

        if (dailyRoutineEnabled &&
            IsCultivationCapableVillager() &&
            IsScheduledCultivationTime())
        {
            CultivateNaturally();
            return;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            switch (timeSystem.CurrentPhase)
            {
                case WorldTimePhase.Dawn:
                    if (job == VillagerJob.Trader)
                    {
                        TryTradeOrTaskOrIdle();
                        return;
                    }

                    if (fatigue > 35f)
                    {
                        GoHomeToRest();
                        return;
                    }
                    GoWorkOrCultivatorActivity();
                    return;

                case WorldTimePhase.Morning:
                    if (job == VillagerJob.Trader)
                    {
                        TryTradeOrTaskOrIdle();
                        return;
                    }

                    GoWorkOrCultivatorActivity();
                    return;

                case WorldTimePhase.Noon:
                    if (NeedsFood())
                    {
                        GoEat();
                    }
                    else if (ShouldDoDailyVanBaoLauCheck())
                    {
                        GoDailyVanBaoLauCheck();
                    }
                    else if (fun < 80f && playPoint != null)
                    {
                        GatherAndPlay();
                    }
                    else
                    {
                        IdleOrGoHome(NpcText.Action("restVillageNoon"));
                    }
                    return;

                case WorldTimePhase.Afternoon:
                    if (job == VillagerJob.Trader)
                    {
                        TryTradeOrTaskOrIdle();
                        return;
                    }

                    if (ShouldDoDailyVanBaoLauCheck())
                    {
                        GoDailyVanBaoLauCheck();
                        return;
                    }

                    GoWorkOrCultivatorActivity();
                    return;

                case WorldTimePhase.Evening:
                    if (job == VillagerJob.Trader)
                    {
                        TryTradeOrTaskOrIdle();
                        return;
                    }

                    if (NeedsFood() &&
                        hunger >= 45f)
                    {
                        GoEat();
                        return;
                    }

                    if (ShouldDoDailyVanBaoLauCheck())
                    {
                        GoDailyVanBaoLauCheck();
                        return;
                    }

                    if (playPoint != null)
                    {
                        GatherAndPlay();
                        return;
                    }

                    IdleOrGoHome(NpcText.Action("eveningWalkVillage"));
                    return;

                case WorldTimePhase.Night:
                    GoHomeToRest();
                    return;
            }
        }

        if (job == VillagerJob.Trader)
        {
            TryTradeOrTaskOrIdle();
            return;
        }

        if (!autonomousWorkEnabled)
        {
            IdleOrGoHome(NpcText.Action("wanderVillage"));
            return;
        }

        if (ShouldDoMortalWork())
        {
            GoWork();
            return;
        }

        DoCultivatorActivity();
    }

    bool ShouldTalk()
    {
        int chance = sociability;

        if (mood == VillagerMood.Happy)
        {
            chance += 20;
        }
        else if (mood == VillagerMood.Sad ||
            mood == VillagerMood.Tired ||
            mood == VillagerMood.Afraid)
        {
            chance -= 25;
        }

        return Random.Range(0, 100) < Mathf.Clamp(chance, 0, 100);
    }

    bool NeedsFood()
    {
        return realm < CultivationRealm.Foundation;
    }

    float GetHungerRate()
    {
        return realm == CultivationRealm.QiRefining
            ? 0.08f
            : 0.35f;
    }

    bool IsForgeWorker()
    {
        return job == VillagerJob.Worker &&
            GetComponent<NpcForgeAgent>() != null;
    }

    void GoForgeWorkOrTrade()
    {
        if (!autonomousWorkEnabled)
        {
            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        NpcForgeAgent forgeAgent = GetComponent<NpcForgeAgent>();
        if (forgeAgent == null)
        {
            GoWork();
            return;
        }

        if (forgeAgent.autoBuyMaterialsFromMarketTraders &&
            forgeAgent.NeedsMoreMaterials())
        {
            if (NpcEconomy.GetNpcMoney(gameObject) <= 0)
            {
                if (ShouldDoDailyVanBaoLauCheck())
                {
                    GoDailyVanBaoLauCheck();
                    return;
                }

                if (autonomousResourceWorkEnabled)
                {
                    GoResourceWork();
                    return;
                }

                Wander(NpcText.Action("wanderVillage"));
                return;
            }

            if (marketPoint != null)
            {
                GoTrade();
                return;
            }

            if (autonomousResourceWorkEnabled)
            {
                GoResourceWork();
                return;
            }

            GoDailyVanBaoLauCheck();
            return;
        }

        GoWork();
    }

    bool ShouldDoMortalWork()
    {
        if (!strongNpcAvoidMortalWork)
        {
            return true;
        }

        return realm < CultivationRealm.Foundation;
    }

    void GoWorkOrCultivatorActivity()
    {
        if (IsCurrentScheduleActivity(NpcScheduleActivity.Work))
        {
            GoWork();
            return;
        }

        if (IsForgeWorker())
        {
            GoForgeWorkOrTrade();
            return;
        }


        if (!autonomousWorkEnabled)
        {
            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        if (ShouldDoMortalWork())
        {
            GoWork();
            return;
        }

        if (dailyRoutineEnabled &&
            IsCultivationCapableVillager())
        {
            if (IsScheduledCultivationTime())
            {
                CultivateNaturally();
                return;
            }

            if (ShouldDoDailyVanBaoLauCheck())
            {
                GoDailyVanBaoLauCheck();
                return;
            }

            if (autonomousResourceWorkEnabled ||
                Random.value < cultivatorResourceWorkChance)
            {
                GoResourceWork();
                return;
            }

            if (job == VillagerJob.Trader)
            {
                TryTradeOrTaskOrIdle();
                return;
            }

            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        DoCultivatorActivity();
    }

    bool IsCurrentScheduleActivity(NpcScheduleActivity activity)
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == activity;
    }

    void DoCultivatorActivity()
    {
        if (autonomousResourceWorkEnabled &&
            realm >= CultivationRealm.Foundation &&
            Random.value < cultivatorResourceWorkChance)
        {
            GoResourceWork();
            return;
        }

        CultivateNaturally();
    }

    void GoResourceWork()
    {
        if (job == VillagerJob.Farmer)
        {
            GoWork();
            return;
        }

        if (ShouldSeekForestResources())
        {
            GoToResourcePoint(
                WorldTilemapManager.Instance != null
                ? WorldTilemapManager.Instance.GetHuntingTile()
                : Vector3.zero,
                NpcText.Action("huntForestResource"),
                NpcMapZone.MaThuSonMach);
            return;
        }

        GoToResourcePoint(
            workPoint != null
            ? workPoint.position
            : GetFallbackActivityPosition(),
            NpcText.Action("gatherVillageResource"),
            NpcMapZone.Lang);
    }

    bool ShouldSeekForestResources()
    {
        if (job == VillagerJob.Hunter ||
            job == VillagerJob.Guard)
        {
            return true;
        }

        if (job == VillagerJob.Worker && autonomousDangerousWorkEnabled)
        {
            return true;
        }

        return bravery >= 55 && realm >= CultivationRealm.Foundation;
    }

    public NpcMapZone GetPreferredResourceGatherZone()
    {
        if (job == VillagerJob.Farmer)
        {
            return NpcMapZone.Lang;
        }

        return ShouldSeekForestResources()
            ? NpcMapZone.MaThuSonMach
            : NpcMapZone.Lang;
    }
    void GoToResourcePoint(
        Vector3 target,
        string action,
        NpcMapZone? targetZone = null)
    {
        if (target == Vector3.zero)
        {
            target = GetFallbackActivityPosition();
        }

        MoveUsingRoad(target, targetZone);
        currentAction = action;

        if (IsAtPosition(target))
        {
            AddCultivationExp(
                Mathf.Max(1, 2 + (int)realm + realmStage));
            actionTimer =
                GameHoursToSeconds(
                    Random.Range(
                        resourceSessionMinGameHours,
                        resourceSessionMaxGameHours));
            currentAction = NpcText.Action("harvestResource");
        }
    }

    void CultivateNaturally()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (actionTimer > 0f &&
            (currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi")))
        {
            return;
        }

        if (TryGoHomeForCultivation())
        {
            return;
        }

        int gain =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    (1 + (int)realm + realmStage * 0.2f) *
                    Mathf.Max(0.5f, diligence / 50f)));

        AddCultivationExp(gain);
        ClearMovementTargets();
        StopMoving();
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

        actionTimer = Mathf.Max(thinkInterval, cultivateSeconds);
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
            UpdateCultivationEffect(false);
        }

        StopMoving();
    }

    bool IsCultivationCapableVillager()
    {
        return realm >= CultivationRealm.QiRefining ||
            !ShouldDoMortalWork();
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
        int day = GetRoutineWorldDay();
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
        float earliestStart =
            Mathf.Clamp(earliestCultivationHour, 0f, 23.9f);
        float latestStart =
            Mathf.Clamp(latestCultivationStartHour, 0f, 23.9f);

        if (latestStart < earliestStart)
        {
            latestStart = earliestStart;
        }

        routineCultivationStartHour =
            Random.Range(earliestStart, latestStart);
        routineCultivationEndHour =
            Mathf.Repeat(routineCultivationStartHour + duration, 24f);
    }

    int GetRoutineWorldDay()
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

    void InitializeVanBaoLauVisitStagger()
    {
        vanBaoLauVisitAnchorHour = GetCurrentWorldHour();

        if (!dailyVanBaoLauVisitEnabled ||
            !staggerDailyVanBaoLauVisits ||
            dailyVanBaoLauVisitStaggerHours <= 0f)
        {
            vanBaoLauVisitDelayHours = 0f;
            return;
        }

        vanBaoLauVisitDelayHours =
            Random.Range(
                0f,
                Mathf.Max(0.1f, dailyVanBaoLauVisitStaggerHours));
    }

    bool IsVanBaoLauVisitStaggerReady()
    {
        if (!dailyVanBaoLauVisitEnabled ||
            !staggerDailyVanBaoLauVisits ||
            vanBaoLauVisitDelayHours <= 0f)
        {
            return true;
        }

        if (vanBaoLauVisitAnchorHour < 0f)
        {
            InitializeVanBaoLauVisitStagger();
        }

        float elapsedHours = GetCurrentWorldHour() - vanBaoLauVisitAnchorHour;
        if (elapsedHours < 0f)
        {
            elapsedHours += 24f;
        }

        return elapsedHours >= vanBaoLauVisitDelayHours;
    }

    bool IsRoutineTravelOrCultivationAction(string action)
    {
        return action == NpcText.Action("goTaskProviderDaily") ||
            action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("goVanBaoLauBroker") ||
            action == NpcText.Action("goVanBaoLauTask") ||
            action == NpcText.Action("checkedVanBaoLau") ||
            action == NpcText.Action("goHomeCultivate") ||
            action == NpcText.Action("goCultivatePoint");
    }

    bool TryGoHomeForCultivation()
    {
        if (IsInDungeonCombatSession())
        {
            return false;
        }

        if (homeRoutineManagedExternally &&
            !HasEnforcedSchedule())
        {
            return false;
        }

        Vector3 homePosition = GetHomePosition();
        if (IsAtPosition(homePosition))
        {
            return false;
        }

        MoveUsingRoad(homePosition);
        currentAction = NpcText.Action("goHomeCultivate");
        return true;
    }

    void ResetDailyTargets()
    {
        if (WorldTilemapManager.Instance != null)
        {
            WorldTilemapManager.Instance.ReleaseFishingTile(this);
        }

        hasWorkTarget = false;
        hasTradeTarget = false;
        currentTradeTargetZone = null;
        hasEatTarget = false;
        hasSellTarget = false;
        currentSellTargetZone = null;
        hasRoadPreference = false;
    }

    bool TryProcessDailyTaskPlan()
    {
        if (HasEnforcedSchedule() &&
            !IsCurrentScheduleActivity(NpcScheduleActivity.TakeTask))
        {
            return false;
        }

        if (!dailyTaskPlanEnabled ||
            ageGroup != VillagerAgeGroup.Adult ||
            fatigue >= 85f ||
            IsBusyActionActive() ||
            Time.timeSinceLevelLoad < dailyTaskPlanStartupDelay)
        {
            return false;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null &&
            timeSystem.CurrentPhase == WorldTimePhase.Night &&
            !IsCultivationCapableVillager())
        {
            return false;
        }

        EnsureDailyTaskPlan();

        if (dailyTaskPlan.Count == 0 ||
            dailyTaskPlanIndex >= dailyTaskPlan.Count)
        {
            return false;
        }

        if (TryBuyDailyTaskNeeds())
        {
            return true;
        }

        return TryStartNextDailyTask();
    }

    void EnsureDailyTaskPlan()
    {
        int currentDay = GetCurrentWorldDay();
        if (lastDailyTaskPlanDay == currentDay)
        {
            return;
        }

        lastDailyTaskPlanDay = currentDay;
        dailyTaskPlanIndex = 0;
        dailyTaskPlan.Clear();
        dailyTaskNeeds.Clear();

        NpcTaskProvider provider =
            NpcTaskProvider.FindNearestProvider(transform.position);

        if (provider == null)
        {
            return;
        }

        List<NpcTaskOffer> offers =
            provider.PickDailyOffersFor(
                gameObject,
                Mathf.Max(1, dailyTaskPlanMinTasks),
                Mathf.Max(dailyTaskPlanMinTasks, dailyTaskPlanMaxTasks));

        dailyTaskPlan.AddRange(offers);

        for (int i = 0; i < dailyTaskPlan.Count; i++)
        {
            NpcTaskOffer offer =
                dailyTaskPlan[i];

            if (offer == null ||
                offer.taskType == NpcTaskType.GatherResource)
            {
                continue;
            }

            StatItemData item =
                provider.GetPlannedRequiredItem(offer);

            int amount =
                provider.GetPlannedRequiredAmount(offer);

            if (item != null &&
                amount > 0)
            {
                AddDailyTaskNeed(item, amount);
            }
        }
    }

    void AddDailyTaskNeed(StatItemData item, int amount)
    {
        foreach (DailyTaskNeed need in dailyTaskNeeds)
        {
            if (need.item == item)
            {
                need.amount += amount;
                return;
            }
        }

        dailyTaskNeeds.Add(
            new DailyTaskNeed
            {
                item = item,
                amount = amount
            });
    }

    bool TryBuyDailyTaskNeeds()
    {
        DailyTaskNeed missingNeed =
            GetFirstMissingDailyTaskNeed();

        if (missingNeed == null)
        {
            return false;
        }

        NpcCounterBroker broker =
            NpcCounterBroker.Active;

        if (broker == null)
        {
            currentAction = NpcText.Action("missingTaskItems");
            return false;
        }

        Vector3 brokerPosition =
            broker.GetCustomerPositionFor(gameObject);

        int missingAmount =
            GetMissingDailyTaskItemAmount(missingNeed);

        if (missingAmount <= 0)
        {
            return false;
        }

        int requiredMoney =
            GetDailyTaskNeedBuyCost(
                broker,
                missingNeed.item,
                missingAmount);

        string missingItemName = ItemText.Name(missingNeed.item);
        currentAction = NpcText.ActionFormat("requestBuyTaskItem", missingItemName);

        if (requiredMoney > 0 &&
            NpcEconomy.GetNpcMoney(gameObject) < requiredMoney)
        {
            currentAction = NpcText.Action("notEnoughSpiritStoneWorkTask");
            return false;
        }

        currentAction = NpcText.ActionFormat("goStoreBuyItem", missingItemName);

        if (!IsInsideBrokerServiceArea(broker))
        {
            MoveUsingRoad(
                brokerPosition,
                NpcMapNavigator.GetDestinationZone(broker.transform) ??
                NpcMapZone.VanBaoLau);
            return true;
        }

        NpcTradeAgent tradeAgent =
            GetComponent<NpcTradeAgent>();

        if (tradeAgent == null)
        {
            tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
        }

        if (broker.TrySellSpecificItemTo(
                tradeAgent,
                missingNeed.item,
                missingAmount,
                false))
        {
            currentAction = NpcText.ActionFormat("boughtTaskItem", missingItemName);

            if (GetMissingDailyTaskItemAmount(missingNeed) <= 0)
            {
                dailyTaskNeeds.Remove(missingNeed);
            }

            return GetFirstMissingDailyTaskNeed() != null;
        }

        dailyTaskNeeds.Remove(missingNeed);
        currentAction = NpcText.ActionFormat("storeMissingItem", missingItemName);
        return GetFirstMissingDailyTaskNeed() != null;
    }

    int GetDailyTaskNeedBuyCost(
        NpcCounterBroker broker,
        StatItemData item,
        int amount)
    {
        if (broker == null ||
            item == null ||
            amount <= 0)
        {
            return 0;
        }

        int unitPrice =
            NpcEconomy.GetNpcBuyPrice(
                item,
                gameObject,
                broker.sellToNpcContext);

        return Mathf.Max(0, unitPrice) * amount;
    }

    DailyTaskNeed GetFirstMissingDailyTaskNeed()
    {
        foreach (DailyTaskNeed need in dailyTaskNeeds)
        {
            if (GetMissingDailyTaskItemAmount(need) > 0)
            {
                return need;
            }
        }

        return null;
    }

    int GetMissingDailyTaskItemAmount(DailyTaskNeed need)
    {
        if (need == null ||
            need.item == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            need.amount - GetInventoryItemAmount(need.item));
    }

    int GetInventoryItemAmount(StatItemData item)
    {
        ItemInventory itemInventory =
            inventory != null
            ? inventory
            : GetComponent<ItemInventory>();

        int amount = itemInventory != null
            ? itemInventory.GetAmount(item)
            : 0;

        NpcTradeAgent tradeAgent =
            GetComponent<NpcTradeAgent>();

        if (tradeAgent != null &&
            tradeAgent.inventory != null &&
            tradeAgent.inventory != itemInventory)
        {
            amount += tradeAgent.inventory.GetAmount(item);
        }

        NpcItemCollector collector =
            GetComponent<NpcItemCollector>();

        if (collector != null &&
            collector.inventory != null &&
            collector.inventory != itemInventory &&
            (tradeAgent == null ||
            collector.inventory != tradeAgent.inventory))
        {
            amount += collector.inventory.GetAmount(item);
        }

        return amount;
    }

    bool TryStartNextDailyTask()
    {
        if (dailyTaskPlanIndex >= dailyTaskPlan.Count)
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

        NpcMapZone? providerZone =
            NpcMapNavigator.GetDestinationZone(provider.transform);

        currentAction = NpcText.Action("goTaskProviderDaily");

        if (!IsNearTaskProvider(provider))
        {
            MoveUsingRoad(
                providerPosition,
                providerZone.HasValue ? providerZone : NpcMapZone.VanBaoLau);
            return true;
        }

        NpcTaskOffer offer =
            dailyTaskPlan[dailyTaskPlanIndex];

        if (provider.TryStartPlannedTask(gameObject, offer))
        {
            dailyTaskPlanIndex++;
            return true;
        }

        dailyTaskPlanIndex++;
        actionTimer = Mathf.Max(1f, thinkInterval);
        currentAction = NpcText.Action("skipUnavailableTask");
        return true;
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem =
            WorldTimeSystem.Instance;

        return timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.Max(1, lastDailyTaskPlanDay + 1);
    }


    bool ShouldDoDailyVanBaoLauCheck()
    {
        if (HasEnforcedSchedule())
        {
            return false;
        }

        if (!dailyVanBaoLauVisitEnabled ||
            ageGroup != VillagerAgeGroup.Adult ||
            fatigue >= 85f)
        {
            return false;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null ||
            lastVanBaoLauVisitDay == timeSystem.CurrentDay)
        {
            return false;
        }

        if (!IsVanBaoLauVisitStaggerReady())
        {
            return false;
        }

        if (timeSystem.CurrentPhase != WorldTimePhase.Noon &&
            timeSystem.CurrentPhase != WorldTimePhase.Afternoon &&
            timeSystem.CurrentPhase != WorldTimePhase.Evening)
        {
            return false;
        }

        if (ShouldVisitCounterBroker())
        {
            return true;
        }

        return NpcTaskProvider.FindNearestProvider(transform.position) != null;
    }

    void GoDailyVanBaoLauCheck()
    {
        if (vanBaoLauVisitStep <= 0)
        {
            NpcCounterBroker broker = NpcCounterBroker.Active;
            if (broker != null && ShouldVisitCounterBroker())
            {
                Vector3 brokerPosition = broker.GetCustomerPositionFor(gameObject);
                NpcMapZone? brokerZone = NpcMapNavigator.GetDestinationZone(broker.transform);
                currentAction = NpcText.Action("goVanBaoLauBroker");

                if (!IsInsideBrokerServiceArea(broker))
                {
                    MoveUsingRoad(
                        brokerPosition,
                        brokerZone.HasValue ? brokerZone : NpcMapZone.VanBaoLau);
                    return;
                }

                ClearMovementTargets();
                StopMoving();

                if (TryTradeAtCounterOrTakeTask())
                {
                    MarkDailyVanBaoLauVisited();
                    return;
                }

                vanBaoLauVisitStep = 1;
                actionTimer = Mathf.Max(1f, thinkInterval);
                currentAction = GetScheduledTradeIdleAction();
                return;
            }

            vanBaoLauVisitStep = 1;
        }

        NpcTaskProvider provider = NpcTaskProvider.FindNearestProvider(transform.position);
        if (provider != null)
        {
            Vector3 providerPosition = provider.GetProviderPositionFor(gameObject);
            NpcMapZone? providerZone = NpcMapNavigator.GetDestinationZone(provider.transform);
            if (IsNearTaskProvider(provider))
            {
                MarkDailyVanBaoLauVisited();
                if (provider.TryHandleVisitor(gameObject))
                {
                    return;
                }

                ClearMovementTargets();
                StopMoving();
                actionTimer = Mathf.Max(1f, thinkInterval);
                currentAction = NpcText.Action("visitedTaskProvider");
                return;
            }

            MoveUsingRoad(
                providerPosition,
                providerZone.HasValue ? providerZone : NpcMapZone.VanBaoLau);
            currentAction = NpcText.Action("goVanBaoLauTask");

            if (!IsNearTaskProvider(provider))
            {
                MoveUsingRoad(
                    providerPosition,
                    providerZone.HasValue ? providerZone : NpcMapZone.VanBaoLau);
                return;
            }

            MarkDailyVanBaoLauVisited();
            if (provider.TryHandleVisitor(gameObject))
            {
                return;
            }
        }

        MarkDailyVanBaoLauVisited();
        ClearMovementTargets();
        StopMoving();
        actionTimer = Mathf.Max(1f, thinkInterval);
        currentAction = NpcText.Action("checkedVanBaoLau");
    }

    void MarkDailyVanBaoLauVisited()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        lastVanBaoLauVisitDay = timeSystem != null
            ? timeSystem.CurrentDay
            : lastVanBaoLauVisitDay;
        vanBaoLauVisitStep = 0;
    }

    bool ShouldSellGoodsNow()
    {
        if (HasEnforcedSchedule())
        {
            return false;
        }

        if (job == VillagerJob.Trader ||
            !HasSellableGoods())
        {
            return false;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return true;
        }

        return timeSystem.CurrentPhase == WorldTimePhase.Noon ||
            timeSystem.CurrentPhase == WorldTimePhase.Afternoon ||
            timeSystem.CurrentPhase == WorldTimePhase.Evening;
    }

    bool CanSocializeNow()
    {
        if (!NpcScheduleController.AllowsSocial(gameObject))
        {
            return false;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return false;
        }

        if (timeSystem.CurrentPhase != WorldTimePhase.Evening &&
            timeSystem.CurrentPhase != WorldTimePhase.Noon)
        {
            return false;
        }

        return currentAction.Contains(NpcText.Action("trading")) ||
            currentAction.Contains(NpcText.Action("playWithFriends")) ||
            currentAction.Contains(NpcText.Action("goHomeRest")) ||
            currentAction.Contains(NpcText.Action("restNearHome")) ||
            currentAction.Contains(NpcText.Action("stayNearHome"));
    }

    void TryTalkToPassingVillager()
    {
        if (Time.time < nextSocialScanTime ||
            Time.time < nextConversationAllowedTime ||
            actionTimer > 0f ||
            IsDead ||
            !CanSocializeNow())
        {
            return;
        }

        nextSocialScanTime = Time.time + Mathf.Max(1f, socialScanInterval);

        VillagerAI other =
            FindNearbyVillager();

        if (other == null ||
            other.actionTimer > 0f ||
            Time.time < other.nextConversationAllowedTime ||
            !CanTalkWith(other) ||
            !other.CanTalkWith(this))
        {
            return;
        }

        int chance = sociability;

        if (acquaintances.Contains(other))
        {
            chance += acquaintanceTalkChanceBonus;
        }

        if (other.mood == VillagerMood.Happy)
        {
            chance += 10;
        }

        if (Random.Range(0, 100) > Mathf.Clamp(chance, 0, 100))
        {
            return;
        }

        StartConversation(other);
    }

    public void GoHomeToRest()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        Vector3 homePosition = GetHomePosition();
        MoveUsingRoad(homePosition);
        currentAction = NpcText.Action("goHomeRest");

        if (IsAtPosition(homePosition))
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

            if (hideAtHome)
            {
                ForceHiddenAtHome(true);
            }
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

        if (!hasEatTarget)
        {
            currentEatTarget =
                GetMarketPosition(
                    marketPoint != null
                    ? marketPoint.position
                    : GetFallbackActivityPosition());

            hasEatTarget = true;
        }

        MoveUsingRoad(currentEatTarget);
        currentAction = NpcText.Action("eat");

        if (IsAtPosition(currentEatTarget))
        {
            ClearMovementTargets();
            StopMoving();
            hasEatTarget = false;
            hunger = 0f;
            money = Mathf.Max(0, money - 1);
            actionTimer = eatDuration;
            currentAction = NpcText.Action("eating");
        }
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

        StopMoving();
        ClearMovementTargets();
        hunger = 0f;
        money = Mathf.Max(0, money - 1);
        actionTimer = eatDuration;
        currentAction = NpcText.Action("eatAtShop");
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
            TalkToNearbyVillager();
        }
    }
    void GoWork()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            return;
        }

        if (job == VillagerJob.Trader)
        {
            TryTradeOrTaskOrIdle();
            return;
        }

        NpcResourceGatherer gatherer = EnsureWorkGatherer();
        if (gatherer != null &&
            gatherer.enabled &&
            gatherer.canGather)
        {
            if (job == VillagerJob.Farmer &&
                TryStartFarmerMapHarvest(gatherer))
            {
                return;
            }

            if (job != VillagerJob.Farmer &&
                gatherer.TryStartGatheringNow())
            {
                return;
            }
        }

        string desiredWorkTargetKey = GetCurrentWorkTargetKey();
        if (currentWorkTargetKey != desiredWorkTargetKey)
        {
            hasWorkTarget = false;
            currentWorkTargetZone = null;
            currentWorkTargetKey = desiredWorkTargetKey;
        }

        if (!hasWorkTarget)
        {
            WorldTilemapManager worldTilemap =
                WorldTilemapManager.Instance;

            currentWorkTarget = Vector3.zero;
            currentWorkTargetZone = null;

            switch (job)
            {
                case VillagerJob.Farmer:
                    currentWorkTarget = GetWorkPointPosition(VillagerJob.Farmer);
                    if (currentWorkTarget != Vector3.zero)
                    {
                        currentWorkTargetZone = currentWorkTargetZone.HasValue
                            ? currentWorkTargetZone
                            : NpcMapZone.Lang;
                    }
                    else
                    {
                        currentWorkTarget = worldTilemap != null
                            ? worldTilemap.GetFarmTile()
                            : Vector3.zero;
                        currentWorkTargetZone = NpcMapZone.Lang;
                    }
                    break;

                case VillagerJob.Fisher:
                    currentWorkTarget = GetWorkPointPosition(VillagerJob.Fisher);
                    if (currentWorkTarget == Vector3.zero)
                    {
                        currentWorkTarget = worldTilemap != null
                            ? worldTilemap.GetFishingTile(this)
                            : Vector3.zero;
                        currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);
                    }
                    if (currentWorkTarget == Vector3.zero)
                    {
                        currentWorkTarget = worldTilemap != null
                            ? worldTilemap.GetFarmTile()
                            : Vector3.zero;
                        currentWorkTargetZone = NpcMapZone.Lang;
                        currentAction = NpcText.Action("noFishingSpotFarmFallback");
                    }
                    break;

                case VillagerJob.Hunter:
                    currentWorkTarget = GetWorkPointPosition(VillagerJob.Hunter);
                    if (currentWorkTarget == Vector3.zero)
                    {
                        currentWorkTarget = worldTilemap != null
                            ? worldTilemap.GetHuntingTile()
                            : Vector3.zero;
                    }
                    currentWorkTargetZone = currentWorkTargetZone.HasValue
                        ? currentWorkTargetZone
                        : NpcMapZone.MaThuSonMach;
                    break;

                default:
                    if (workPoint != null)
                    {
                        currentWorkTarget = GetWorkPointPosition(job);
                        currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);
                    }
                    break;
            }

            if (currentWorkTarget == Vector3.zero)
            {
                currentWorkTarget = job == VillagerJob.Hunter
                    ? GetFallbackPositionInZone(NpcMapZone.MaThuSonMach)
                    : workPoint != null
                        ? workPoint.position
                        : GetFallbackActivityPosition();
                currentWorkTargetZone = job == VillagerJob.Hunter
                    ? NpcMapZone.MaThuSonMach
                    : NpcMapNavigator.GetDestinationZone(workPoint);
            }

            hasWorkTarget = true;
            debugWorkTarget =
                job + " -> " + currentWorkTarget +
                " zone=" + (currentWorkTargetZone.HasValue
                    ? currentWorkTargetZone.Value.ToString()
                    : "none") +
                " purpose=" + GetWorkLocationPurpose(job);
        }

        float distance =
            Vector2.Distance(
                transform.position,
                currentWorkTarget);

        if (distance >= 0.5f)
        {
            currentAction = GetWorkAction();
            SetDirectMoveTarget(currentWorkTarget);
            MoveUsingRoad(currentWorkTarget, currentWorkTargetZone);
            return;
        }

        ClearMovementTargets();
        StopMoving();

        if (IsScheduledHarvestJob())
        {
            if (job == VillagerJob.Farmer)
            {
                SetFarmerWaitingForMapHarvest();
                return;
            }

            RunScheduledHarvestWork();
            return;
        }

        currentAction = GetWorkingAction();
        bool produced = AddWorkProduct();
        fatigue = Mathf.Clamp(fatigue + 8f, 0f, 100f);
        actionTimer = produced
            ? GameHoursToSeconds(
                Random.Range(
                    workSessionMinGameHours,
                    workSessionMaxGameHours))
            : Mathf.Max(thinkInterval, 2f);

        if (produced)
        {
            AddProfessionExp(professionExpPerWork);
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

        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer == null)
        {
            gatherer = gameObject.AddComponent<NpcResourceGatherer>();
        }

        gatherer.canGather = true;
        gatherer.useVillagerPreferredZone = true;
        return gatherer;
    }

    StatItemData ResolveFarmProductForHarvest()
    {
        if (IsFarmHarvestItem(farmProduct))
        {
            return farmProduct;
        }

#if UNITY_EDITOR
        StatItemData linhRice =
            UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                "Assets/Item/ThucPham/Linh_Me.asset");
        if (IsFarmHarvestItem(linhRice))
        {
            farmProduct = linhRice;
            return farmProduct;
        }
#endif

        foreach (WorldResourceField field in WorldResourceField.Fields)
        {
            if (field == null || field.items == null)
            {
                continue;
            }

            foreach (ResourceFieldItemEntry entry in field.items)
            {
                if (entry != null && IsFarmHarvestItem(entry.item))
                {
                    farmProduct = entry.item;
                    return farmProduct;
                }
            }
        }

        WorldStatItemPickup[] pickups =
            FindObjectsByType<WorldStatItemPickup>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (pickup != null && IsFarmHarvestItem(pickup.item))
            {
                farmProduct = pickup.item;
                return farmProduct;
            }
        }

        return null;
    }

    bool TryStartFarmerMapHarvest(NpcResourceGatherer gatherer)
    {
        if (gatherer == null ||
            !gatherer.enabled ||
            !gatherer.canGather)
        {
            return false;
        }

        StatItemData harvestItem = ResolveFarmProductForHarvest();
        return harvestItem != null &&
            gatherer.TryStartGatheringItemNow(harvestItem);
    }

    void SetFarmerWaitingForMapHarvest()
    {
        StatItemData harvestItem = ResolveFarmProductForHarvest();
        string itemName = harvestItem != null
            ? ItemText.Name(harvestItem)
            : "Linh Me";

        currentAction = "Khong tim thay " + itemName + " de thu hoach";
        actionTimer = Mathf.Max(thinkInterval, 1f);
    }

    bool IsFarmHarvestItem(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        string itemName = item.itemName != null
            ? item.itemName.ToLowerInvariant()
            : string.Empty;
        string assetName = item.name != null
            ? item.name.ToLowerInvariant()
            : string.Empty;

        return item.ItemId == "46fa9c5a1da91f041b6da40762359694" ||
            itemName.Contains("linh m") ||
            itemName.Contains("linh g") ||
            itemName.Contains("lua") ||
            assetName.Contains("linh_m") ||
            assetName.Contains("linhme") ||
            assetName.Contains("lua");
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

        return job + ":" +
            (slot != null ? slot.activity.ToString() : "none") + ":" +
            (slot != null ? slot.startHour.ToString("0.##") : "x") + ":" +
            (slot != null ? slot.endHour.ToString("0.##") : "x");
    }

    void GoTrade()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
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

        if (!hasSellTarget)
        {
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
            (!sellOnlyToTrader && SellGoodsToMarket()))
        {
            hasSellTarget = false;
            currentSellTargetZone = null;
            actionTimer = sellGoodsDuration;
            currentAction = NpcText.Action("soldGoods");
            return;
        }

        actionTimer = sellGoodsDuration;
        currentAction = NpcText.Action("waitTraderBuyGoods");
    }

    void TalkToNearbyVillager()
    {
        VillagerAI other =
            FindNearbyVillager();

        if (other == null)
        {
            return;
        }

        currentTarget = other.transform;
        StartConversation(other);
    }

    void StartConversation(VillagerAI other)
    {
        if (other == null ||
            other.IsDead ||
            !CanTalkWith(other) ||
            !other.CanTalkWith(this) ||
            !NpcScheduleController.AllowsSocial(gameObject) ||
            !NpcScheduleController.AllowsSocial(other.gameObject) ||
            Time.time < nextConversationAllowedTime ||
            Time.time < other.nextConversationAllowedTime)
        {
            return;
        }

        StopForConversation(conversationPauseDuration);
        other.StopForConversation(other.conversationPauseDuration);

        acquaintances.Add(other);
        other.acquaintances.Add(this);

        ApplySocialMemory(other);

        currentAction =
            GetConversationAction(other);

        other.currentAction =
            other.GetConversationAction(this);

        float pauseDuration = Mathf.Max(0.5f, conversationPauseDuration);
        actionTimer = pauseDuration;
        other.actionTimer = Mathf.Max(0.5f, other.conversationPauseDuration);
        nextConversationAllowedTime = Time.time + Mathf.Max(pauseDuration, conversationCooldown);
        other.nextConversationAllowedTime = Time.time + Mathf.Max(other.actionTimer, other.conversationCooldown);
    }

    bool CanTalkWith(VillagerAI other)
    {
        if (other == null ||
            other == this ||
            other.IsDead)
        {
            return false;
        }

        if (!NpcScheduleController.AllowsSocial(gameObject) ||
            !NpcScheduleController.AllowsSocial(other.gameObject))
        {
            return false;
        }

        if (!requireKnownVillagerToTalk)
        {
            return true;
        }

        if (entityProfile == null ||
            other.entityProfile == null ||
            other.entityProfile.identity == null)
        {
            return false;
        }

        EntityRelationship relationship =
            entityProfile.GetRelationship(other.entityProfile.identity.entityName);

        return relationship != null &&
            relationship.friendship >= minFriendshipToTalk;
    }

    void ApplySocialMemory(VillagerAI other)
    {
        if (entityProfile == null || other == null || other.entityProfile == null)
        {
            return;
        }

        EntityRelationship relationship =
            entityProfile.GetRelationship(other.entityProfile.identity.entityName);
        EntityRelationship otherRelationship =
            other.entityProfile.GetRelationship(entityProfile.identity.entityName);

        int moodBonus = mood == VillagerMood.Happy ? 2 : 1;
        int temperPenalty = entityProfile.personality.hotTemper > 75 && Random.value < 0.25f ? 2 : 0;

        relationship.friendship += moodBonus;
        relationship.hatred += temperPenalty;
        otherRelationship.friendship += moodBonus;
        otherRelationship.hatred += temperPenalty;

        string eventType = temperPenalty > 0 ? "argument" : "conversation";
        entityProfile.Remember(other.entityProfile.identity.entityName, eventType, moodBonus - temperPenalty);
        other.entityProfile.Remember(entityProfile.identity.entityName, eventType, moodBonus - temperPenalty);
    }

    string GetConversationAction(VillagerAI other)
    {
        if (mood == VillagerMood.Happy)
        {
            return NpcText.ActionFormat("happyTalkWith", other.villagerName);
        }

        if (mood == VillagerMood.Sad)
        {
            return NpcText.ActionFormat("sadTalkWith", other.villagerName);
        }

        if (mood == VillagerMood.Tired)
        {
            return NpcText.ActionFormat("tiredTalkWith", other.villagerName);
        }

        if (acquaintances.Contains(other))
        {
            return NpcText.ActionFormat("meetKnown", other.villagerName);
        }

        return NpcText.ActionFormat("talkingWith", other.villagerName);
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

        Vector3 homePosition = GetHomePosition();
        MoveUsingRoad(homePosition);
        currentAction = action;

        if (IsAtPosition(homePosition))
        {
            ClearMovementTargets();
            StopMoving();
            actionTimer = idleAtHomeDuration;
        }
    }

    Vector3 GetHomePosition()
    {
        return homePoint != null
            ? homePoint.position
            : spawnPosition;
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
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null && broker.receiveAllNpcRequests && ShouldVisitCounterBroker())
        {
            NpcMapZone? brokerZone = NpcMapNavigator.GetDestinationZone(broker.transform);
            return brokerZone.HasValue ? brokerZone : NpcMapZone.VanBaoLau;
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
            return broker.GetCustomerPositionFor(gameObject);
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

    NpcMapZone? GetSellGoodsTargetZone()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests &&
            BrokerCanBuyMyGoods(broker))
        {
            NpcMapZone? brokerZone =
                NpcMapNavigator.GetDestinationZone(broker.transform);

            return brokerZone.HasValue
                ? brokerZone
                : NpcMapZone.VanBaoLau;
        }

        if (resolvedSellLocationZone.HasValue)
        {
            return resolvedSellLocationZone;
        }

        return NpcMapNavigator.GetDestinationZone(marketPoint);
    }
    bool BrokerCanBuyMyGoods(NpcCounterBroker broker)
    {
        return broker != null &&
            inventory != null &&
            broker.CanBuyProduceFrom(this, inventory);
    }


    bool ShouldVisitCounterBroker()
    {
        if (HasEnforcedSchedule() &&
            !NpcScheduleController.AllowsTrade(gameObject))
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null || !broker.receiveAllNpcRequests)
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

    bool AddWorkProduct()
    {
        StatItemData product =
            GetProductForJob();

        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();

            if (inventory == null)
            {
                inventory = gameObject.AddComponent<ItemInventory>();
                inventory.shareRuntimeItems = false;
            }
        }

        if (IsScheduledHarvestJob())
        {
            return AddScheduledHarvestProduct(product);
        }

        if (product == null)
        {
            money += GetWorkIncome();
            lastWorkProductStatus = NpcText.Get("workStatus", "noProductPaid");
            currentAction = NpcText.Action("paidWork");
            return true;
        }

        int amount =
            Random.Range(
                Mathf.Max(1, workProductMin),
                Mathf.Max(workProductMin, workProductMax) + 1) +
            GetProfessionProductBonus();

        inventory.AddItem(product, amount);
        lastWorkProductStatus =
            NpcText.Format(
                NpcText.Get("workStatus", "addedItemAmountInventory"),
                product.itemName,
                amount,
                inventory.GetAmount(product));
        currentAction = NpcText.ActionFormat("harvestItemAmount", product.itemName, amount);
        return true;
    }

    void RunScheduledHarvestWork()
    {
        EnsureWorkInventory();

        if (scheduledWorkHarvestInProgress)
        {
            if (actionTimer > 0f)
            {
                RefreshScheduledHarvestAction();
                return;
            }

            CompleteScheduledHarvestWork();
            return;
        }

        if (actionTimer > 0f)
        {
            currentAction = GetWorkingAction();
            return;
        }

        StatItemData product = GetProductForJob();
        if (!CanStartScheduledHarvest(product))
        {
            currentAction = GetWorkingAction();
            actionTimer = GetWorkSessionSeconds();
            return;
        }

        scheduledWorkHarvestProduct = product;
        scheduledWorkHarvestAmount = GetScheduledHarvestAmount();
        scheduledWorkHarvestInProgress = true;
        lastScheduledWorkHarvestSeconds = -1;
        actionTimer = Mathf.Max(0.5f, scheduledWorkHarvestSeconds);
        RefreshScheduledHarvestAction();
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

    bool CanStartScheduledHarvest(StatItemData product)
    {
        if (product == null)
        {
            lastWorkProductStatus = NpcText.Get("workStatus", "farmerWaitMorning");
            return false;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (job == VillagerJob.Farmer &&
            farmerHarvestOnlyInMorning &&
            timeSystem != null &&
            !IsFarmerHarvestTime(timeSystem.CurrentPhase))
        {
            lastWorkProductStatus = NpcText.Get("workStatus", "farmerWaitMorning");
            return false;
        }

        int currentDay =
            timeSystem != null
            ? timeSystem.CurrentDay
            : -1;

        if (limitScheduledHarvestOncePerDay &&
            currentDay >= 0 &&
            lastProfessionHarvestDay == currentDay)
        {
            lastWorkProductStatus =
                NpcText.Format(
                    NpcText.Get("workStatus", "farmerHarvestedDay"),
                    currentDay);
            return false;
        }

        return true;
    }

    int GetScheduledHarvestAmount()
    {
        int amount = job == VillagerJob.Farmer
            ? Mathf.Max(1, farmerHarvestAmountPerDay)
            : Random.Range(
                Mathf.Max(1, workProductMin),
                Mathf.Max(workProductMin, workProductMax) + 1);

        return amount + GetProfessionProductBonus();
    }

    void RefreshScheduledHarvestAction()
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, actionTimer));
        if (seconds == lastScheduledWorkHarvestSeconds)
        {
            return;
        }

        lastScheduledWorkHarvestSeconds = seconds;
        string itemName = scheduledWorkHarvestProduct != null
            ? ItemText.Name(scheduledWorkHarvestProduct)
            : GetWorkingAction();
        currentAction =
            "Đang thu thập " + itemName + " (" + seconds + "s)";
    }

    void CompleteScheduledHarvestWork()
    {
        if (scheduledWorkHarvestProduct != null &&
            scheduledWorkHarvestAmount > 0)
        {
            inventory.AddItem(
                scheduledWorkHarvestProduct,
                scheduledWorkHarvestAmount);

            WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
            lastProfessionHarvestDay = timeSystem != null
                ? timeSystem.CurrentDay
                : lastProfessionHarvestDay;

            lastWorkProductStatus =
                NpcText.Format(
                    NpcText.Get("workStatus", "addedItemAmountInventory"),
                    scheduledWorkHarvestProduct.itemName,
                    scheduledWorkHarvestAmount,
                    inventory.GetAmount(scheduledWorkHarvestProduct));

            currentAction =
                NpcText.ActionFormat(
                    "harvestItemAmount",
                    scheduledWorkHarvestProduct.itemName,
                    scheduledWorkHarvestAmount);
            AddProfessionExp(professionExpPerWork);
        }
        else
        {
            currentAction = GetWorkingAction();
        }

        scheduledWorkHarvestInProgress = false;
        scheduledWorkHarvestProduct = null;
        scheduledWorkHarvestAmount = 0;
        lastScheduledWorkHarvestSeconds = -1;
        actionTimer = Mathf.Max(thinkInterval, GetWorkSessionSeconds());
    }

    float GetWorkSessionSeconds()
    {
        return GameHoursToSeconds(
            Random.Range(
                workSessionMinGameHours,
                workSessionMaxGameHours));
    }

    bool AddScheduledHarvestProduct(StatItemData product)
    {
        if (product == null)
        {
            lastWorkProductStatus = NpcText.Get("workStatus", "farmerWaitMorning");
            currentAction = GetWorkingAction();
            return true;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;

        if (job == VillagerJob.Farmer &&
            farmerHarvestOnlyInMorning &&
            timeSystem != null &&
            !IsFarmerHarvestTime(timeSystem.CurrentPhase))
        {
            lastWorkProductStatus = NpcText.Get("workStatus", "farmerWaitMorning");
            currentAction = GetWorkingAction();
            return true;
        }

        int currentDay =
            timeSystem != null
            ? timeSystem.CurrentDay
            : -1;

        if (limitScheduledHarvestOncePerDay &&
            currentDay >= 0 &&
            lastProfessionHarvestDay == currentDay)
        {
            lastWorkProductStatus =
                NpcText.Format(
                    NpcText.Get("workStatus", "farmerHarvestedDay"),
                    currentDay);
            currentAction = GetWorkingAction();
            return true;
        }

        int amount = job == VillagerJob.Farmer
            ? Mathf.Max(1, farmerHarvestAmountPerDay)
            : Random.Range(
                Mathf.Max(1, workProductMin),
                Mathf.Max(workProductMin, workProductMax) + 1);

        amount += GetProfessionProductBonus();

        inventory.AddItem(product, amount);
        lastProfessionHarvestDay = currentDay;
        lastWorkProductStatus =
            NpcText.Format(
                NpcText.Get("workStatus", "addedItemAmountInventory"),
                product.itemName,
                amount,
                inventory.GetAmount(product));
        currentAction = NpcText.ActionFormat("harvestItemAmount", product.itemName, amount);
        return true;
    }

    bool IsScheduledHarvestJob()
    {
        return job == VillagerJob.Farmer ||
            job == VillagerJob.Fisher ||
            job == VillagerJob.Hunter;
    }

    bool IsFarmerHarvestTime(WorldTimePhase phase)
    {
        return phase == WorldTimePhase.Dawn ||
            phase == WorldTimePhase.Morning;
    }

    int GetProfessionProductBonus()
    {
        int levelsPerBonus =
            Mathf.Max(1, productBonusEveryProfessionLevels);

        return Mathf.Max(0, professionLevel - 1) / levelsPerBonus;
    }

    void AddProfessionExp(int amount)
    {
        if (amount <= 0 ||
            professionLevel >= maxProfessionLevel)
        {
            return;
        }

        professionExp += amount;

        while (professionLevel < maxProfessionLevel)
        {
            int need = GetProfessionExpToNextLevel();

            if (professionExp < need)
            {
                break;
            }

            professionExp -= need;
            professionLevel++;
        }

        if (professionLevel >= maxProfessionLevel)
        {
            professionLevel = maxProfessionLevel;
            professionExp = 0;
        }
    }

    int GetProfessionExpToNextLevel()
    {
        return Mathf.Max(
            1,
            baseProfessionExpToNextLevel +
            Mathf.Max(0, professionLevel - 1) *
            Mathf.Max(0, professionExpGrowthPerLevel));
    }

    StatItemData GetProductForJob()
    {
        switch (job)
        {
            case VillagerJob.Farmer:
                return farmProduct;
            case VillagerJob.Fisher:
                return fishingProduct;
            case VillagerJob.Hunter:
                return huntingProduct;
            case VillagerJob.Worker:
                return workerProduct;
            default:
                return null;
        }
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

        return amount >= Mathf.Max(1, sellGoodsThreshold);
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

        return stack.item == farmProduct ||
            stack.item == fishingProduct ||
            stack.item == huntingProduct ||
            stack.item == workerProduct ||
            stack.item.itemType == ItemType.VatLieu ||
            stack.item.itemType == ItemType.ThucPham;
    }

    bool TrySellGoodsToTrader()
    {
        if (inventory == null)
        {
            return false;
        }

        if (NpcCounterBroker.Active != null &&
            NpcCounterBroker.Active.receiveAllNpcRequests &&
            NpcCounterBroker.Active.CanBuyProduceFrom(this, inventory) &&
            NpcCounterBroker.Active.TryBuyProduceFrom(this, inventory))
        {
            return true;
        }

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

    bool SellGoodsToMarket()
    {
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

            money += price * amount;
            return true;
        }

        return false;
    }

    VillagerAI FindNearbyVillager()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                talkRadius,
                villagerLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.gameObject == gameObject)
            {
                continue;
            }

            VillagerAI villager =
                hit.GetComponentInParent<VillagerAI>();

            if (villager == null ||
                villager == this ||
                villager.IsDead)
            {
                continue;
            }

            return villager;
        }

        return null;
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

        NpcMapZone? homeZone = GetHomeZone();
        if (!homeZone.HasValue)
        {
            return false;
        }

        NpcMapZone? currentZone = GetCurrentMapZone();
        if (!currentZone.HasValue)
        {
            return true;
        }

        return currentZone.Value != homeZone.Value;
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

        Vector2 away = transform.position - origin;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = Random.insideUnitCircle.normalized;
        }

        Vector3 waitPosition =
            origin +
            (Vector3)away.normalized * Mathf.Max(0.5f, safeRadius);

        SetDirectMoveTarget(waitPosition);
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
        treasureHuntTarget = target;
        treasureHuntItem = item;
        treasureWaitLowPowerSkirmish = false;
        actionTimer = 0f;
        SetTarget(
            target,
            NpcText.ActionFormat("treasureHuntNamed", ItemText.Name(item)));
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

        string action = item != null
            ? NpcText.ActionFormat("goGatherNamed", ItemText.Name(item))
            : NpcText.Action("gatherVillageResource");

        if (currentTarget == target &&
            currentAction == action)
        {
            return;
        }

        SetTarget(target, action);
    }

    public void ClearTreasureHunt()
    {
        if (treasureHuntTarget == null && treasureHuntItem == null)
        {
            return;
        }

        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        treasureWaitLowPowerSkirmish = false;
        if (currentTarget != null && currentAction.Contains(NpcText.Action("treasureHunt")))
        {
            ClearMovementTargets();
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

        SetTarget(
            treasureHuntTarget,
            NpcText.ActionFormat(
            "treasureHuntNamed",
            ItemText.Name(treasureHuntItem)));
    }

    void UpdateTreasureWaitAction()
    {
        if (!waitingOutsideTreasureLightning ||
            treasureHuntItem == null)
        {
            return;
        }

        Vector3 waitPosition = hasDirectMoveTarget
            ? directMoveTarget
            : transform.position;

        string itemName = ItemText.Name(treasureHuntItem);
        if (Vector2.Distance(transform.position, waitPosition) <= arriveDistance)
        {
            currentAction = treasureWaitLowPowerSkirmish
                ? NpcText.ActionFormat("outerSkirmishNamed", itemName)
                : NpcText.ActionFormat("waitLightningNamed", itemName);
            return;
        }

        currentAction = NpcText.Action("goHunt");
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
                if (Vector2.Distance(transform.position, directMoveTarget) <= arriveDistance)
                {
                    hasDirectMoveTarget = false;
                    StopMoving();
                    return;
                }

                MoveToPosition(directMoveTarget);
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

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    Vector3 GetWorkPointPosition(VillagerJob targetJob)
    {
        NpcScheduleActivity requestedActivity =
            GetCurrentScheduleActivityForWorkTarget(targetJob);

        if (NpcLocationArea.TryGetPosition(
                gameObject,
                requestedActivity,
                targetJob,
                GetWorkLocationPurpose(targetJob),
                transform.position,
                out Vector3 registryWorkPoint,
                out NpcMapZone? registryWorkZone))
        {
            currentWorkTargetZone = registryWorkZone;
            return registryWorkPoint;
        }

        if (workPoint == null)
        {
            return Vector3.zero;
        }

        NpcWorkArea area = workPoint.GetComponent<NpcWorkArea>();
        if (area != null)
        {
            return area.job == targetJob
                ? area.GetRandomPoint()
                : Vector3.zero;
        }

        if (targetJob == VillagerJob.Hunter)
        {
            return Vector3.zero;
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
            case VillagerJob.Farmer:
                return NpcLocationPurpose.Farming;
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
        int slotCount = 6)
    {
        if (anchor == null)
        {
            return center;
        }

        int safeSlotCount = Mathf.Max(3, slotCount);
        int slotIndex = Mathf.Abs(
            gameObject.GetInstanceID() ^
            anchor.gameObject.GetInstanceID()) % safeSlotCount;
        float angle = (Mathf.PI * 2f * slotIndex) / safeSlotCount;
        Vector3 offset = new Vector3(
            Mathf.Cos(angle),
            Mathf.Sin(angle),
            0f) * Mathf.Max(arriveDistance, sharedAnchorSpacingRadius);

        return ClampToCurrentMapArea(center + offset);
    }
    void MoveUsingRoad(Vector3 target, NpcMapZone? targetZone = null)
    {
        NpcMapZone? previousMovementTargetZone = movementTargetZone;
        movementTargetZone = targetZone;

        try
        {
            bool usingTeleportRoute;
            string routeAction;
            target = NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                target,
                targetZone,
                out usingTeleportRoute,
                out routeAction);

            if (usingTeleportRoute &&
                !string.IsNullOrEmpty(routeAction) &&
                CanRouteActionReplaceCurrentAction())
            {
                currentAction = routeAction;
            }

            if (WorldTilemapManager.Instance == null)
            {
                MoveToPosition(target, targetZone);
                return;
            }

            if (ShouldBypassRoad())
            {
                hasRoadPreference = false;
                MoveToPosition(target, targetZone);
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
                MoveToPosition(target, targetZone);
                return;
            }

            Vector3 roadWaypoint;
            bool hasRoadRoute =
                WorldTilemapManager.Instance.TryGetRoadWaypointToTarget(
                    transform.position,
                    target,
                    GetCurrentMapZone(),
                    IsReachableRoadTile,
                    out roadWaypoint);

            if (!hasRoadRoute)
            {
                MoveToPosition(target, targetZone);
                return;
            }

            float roadDistance =
                Vector2.Distance(
                    transform.position,
                    roadWaypoint);

            if (roadDistance > pathWaypointReachDistance)
            {
                MoveToPosition(roadWaypoint, targetZone);

                if (CanRouteActionReplaceCurrentAction())
                {
                    currentAction = NpcText.Action("walkingRoad");
                }

                return;
            }

            MoveToPosition(target, targetZone);

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
        return string.IsNullOrEmpty(currentAction) ||
            currentAction == NpcText.Action("idle") ||
            currentAction == NpcText.Action("walkingRoad") ||
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

        return mood == VillagerMood.Afraid ||
            currentAction.Contains(NpcText.Action("panicBurned"));
    }

    void MoveToPosition(Vector3 position, NpcMapZone? targetZone = null)
    {
        NpcMapZone? previousMovementTargetZone = movementTargetZone;
        movementTargetZone = targetZone;

        try
        {
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
    public void StopForConversation()
    {
        StopForConversation(conversationPauseDuration);
    }

    public void StopForConversation(float duration)
    {
        movementPausedUntil = Mathf.Max(
            movementPausedUntil,
            Time.time + Mathf.Max(0.2f, duration));
        StopMoving();
    }

    public void StopMoving()
    {
        if (rb != null)
        {
            desiredVelocity = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }
    }

    void ClearMovementTargets()
    {
        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = false;
        hasObstacleAvoidTarget = false;
        movementTargetZone = null;
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
            action == NpcText.Action("goVanBaoLauBroker") ||
            action == NpcText.Action("goVanBaoLauTask") ||
            action == NpcText.Action("goHomeCultivate") ||
            action == NpcText.Action("goCultivatePoint") ||
            action.StartsWith(NpcText.Action("goGatherNamed")
                .Replace("{0}", "")) ||
            action.StartsWith("Äi cá»•ng dá»‹ch chuyá»ƒn");
    }

    void SetDirectMoveTarget(Vector3 position, bool preserveCurrentTarget = false)
    {
        if (!preserveCurrentTarget)
        {
            currentTarget = null;
            hasObstacleAvoidTarget = false;
        }

        hasWanderTarget = false;
        hasDirectMoveTarget = true;
        Vector3 clamped = ClampToCurrentMapArea(position);
        Vector3 clearTarget;
        Vector3 resolvedTarget = TryFindClearPointNear(clamped, out clearTarget)
            ? clearTarget
            : clamped;

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

    void OnNpcMapTeleported()
    {
        OnNpcMapTeleported(null);
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;
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

        if (gate != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
            area = NpcMapNavigator.ResolveMapAreaAfterTeleport(
                gameObject,
                gate.toZone,
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

        if (!hadActiveMoveTarget && gate != null)
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

        ClearActivePath();
        hasObstacleAvoidTarget = false;
        hasRoadPreference = false;
        SetDirectMoveTarget(escapeTarget);
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
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
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
            case VillagerJob.Farmer:
                return "Di thu hoach Linh Me";
            case VillagerJob.Worker:
                return NpcText.Action("goWork");
            case VillagerJob.Guard:
                return NpcText.Action("goPatrol");
            case VillagerJob.Healer:
                return NpcText.Action("goHeal");
            case VillagerJob.Fisher:
                return NpcText.Action("goFish");
            case VillagerJob.Hunter:
                return NpcText.Action("goHunt");
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
            case VillagerJob.Farmer:
                return NpcText.Action("workingFarm");
            case VillagerJob.Worker:
                return NpcText.Action("working");
            case VillagerJob.Guard:
                return NpcText.Action("patrolling");
            case VillagerJob.Healer:
                return NpcText.Action("healing");
            case VillagerJob.Fisher:
                return NpcText.Action("fishing");
            case VillagerJob.Hunter:
                return NpcText.Action("hunting");
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
            case VillagerJob.Guard:
            case VillagerJob.Healer:
                return 2;
            case VillagerJob.Farmer:
            case VillagerJob.Worker:
            case VillagerJob.Fisher:
            case VillagerJob.Hunter:
                return 1;
            default:
                return 0;
        }
    }

    public long ExpToNextRealm()
    {
        if (characterStats != null)
        {
            return characterStats.ExpToNextRealm();
        }

        return CultivationProgression.GetExpToNextLong(
            realm,
            realmStage,
            baseExpToNextRealm);
    }

    public void AddCultivationExp(int amount)
    {
        if (characterStats != null)
        {
            characterStats.AddCultivationExp(amount);
            SyncFromCharacterStats();
            return;
        }

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
    }

    void Breakthrough()
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
            ApplyRealmPower();
            currentHP = maxHP;
            lifespan = GetLifespanForRealm(realm);
            currentAction = NpcText.ActionFormat("breakthroughTo", GetRealmText());
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
                villagerName,
                targetRealm,
                () => CompleteMajorBreakthrough(targetRealm));
            return;
        }

        realmStage += 1;

        ApplyRealmPower();
        currentHP = maxHP;
        lifespan = GetLifespanForRealm(realm);
        currentAction = NpcText.ActionFormat("breakthroughTo", GetRealmText());
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
        ApplyRealmPower();
        currentHP = maxHP;
        lifespan = GetLifespanForRealm(realm);
        currentAction = NpcText.ActionFormat("breakthroughTo", GetRealmText());
    }

    void ApplyRealmPower()
    {
        float power =
            CultivationProgression.GetStatPower(
                realm,
                realmStage,
                EntityKind.Cultivator);

        maxHP = Mathf.Max(1, Mathf.RoundToInt(baseMaxHP * power));
        attack = Mathf.Max(1, Mathf.RoundToInt(baseAttack * power));
        defense = Mathf.Max(0, Mathf.RoundToInt(baseDefense * power));
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
    }

    public string GetRealmText()
    {
        if (characterStats != null)
        {
            return NpcText.RealmWithStage(
                characterStats.realm,
                characterStats.realmStage);
        }

        return NpcText.RealmWithStage(realm, realmStage);
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

    public void TakeDamage(int damage)
    {
        if (characterStats != null)
        {
            characterStats.TakeDamage(damage);
            SyncFromCharacterStats();

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

        int finalDamage = Mathf.Max(1, damage - defense);
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
                AddCultivationExp(intValue);
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
                if (direction > 0)
                {
                    Breakthrough();
                }
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, talkRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);
    }
}
