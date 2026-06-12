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

public class VillagerAI : MonoBehaviour, IDamageable
{
    [Header("Entity Generation")]
    public bool generateFromEntityProfile = true;
    public EntityProfile entityProfile;

    [Header("Info")]
    public string villagerName = "Người dân";
    public VillagerAgeGroup ageGroup = VillagerAgeGroup.Adult;
    public VillagerJob job = VillagerJob.Farmer;
    public bool keepInspectorJob;

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
    public bool dailyTaskPlanEnabled = true;
    public float dailyTaskPlanStartupDelay = 8f;
    public int dailyTaskPlanMinTasks = 3;
    public int dailyTaskPlanMaxTasks = 5;

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
    Transform treasureHuntTarget;
    StatItemData treasureHuntItem;
    bool waitingOutsideTreasureLightning;
    bool hiddenAtHome;

    Rigidbody2D rb;
    NPCVisualAnimation visualAnimation;
    SpawnedWorldActor spawnedWorldActor;
    Vector3 spawnPosition;
    Vector3 wanderTarget;
    Vector3 directMoveTarget;
    float thinkTimer;
    float actionTimer;
    float nextSocialScanTime;
    float nextConversationAllowedTime;
      float movementPausedUntil;
      float crowdYieldUntil;
      float crowdDirectionCommitUntil;
      Vector2 crowdCommittedDirection;
      Vector3 obstacleAvoidTarget;
    float obstacleAvoidUntil;
    bool hasObstacleAvoidTarget;
    bool hasWanderTarget;
    bool hasDirectMoveTarget;
    bool movingToRoad;
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
    int lastFarmerHarvestDay = -1;
    int lastVanBaoLauVisitDay = -1;
    int vanBaoLauVisitStep;

    Vector3 currentWorkTarget;
    NpcMapZone? currentWorkTargetZone;
    Vector3 currentTradeTarget;
    NpcMapZone? currentTradeTargetZone;
    Vector3 currentEatTarget;
    Vector3 currentSellTarget;
    NpcMapZone? currentSellTargetZone;

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
                    "Assets/Item/NPCitem/lua.asset");
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
        if (!keepInspectorJob)
        {
            job = GetGeneratedJob(entityProfile.personality);
        }
        realm = entityProfile.stats.realm;
        lifespan = GetLifespanForRealm(realm);
        realmStage = entityProfile.stats.realmStage;
        cultivationExp = entityProfile.stats.cultivationExp;
        baseMaxHP = Mathf.Max(1, entityProfile.stats.maxHP);
        baseAttack = Mathf.Max(1, entityProfile.stats.attack);
        baseDefense = Mathf.Max(0, entityProfile.stats.defense);
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

        UpdateNeeds();
        UpdateMood();

        thinkTimer += Time.deltaTime;
        actionTimer -= Time.deltaTime;

        if (treasureHuntTarget != null || waitingOutsideTreasureLightning)
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
            (WorldTimeSystem.Instance == null ||
            WorldTimeSystem.Instance.CurrentPhase == WorldTimePhase.Night ||
            fatigue >= 85f))
        {
            return;
        }

        if (actionTimer > 0f)
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

        GoHomeIdle(NpcText.Action("noTrade"));
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

        if (TryProcessDailyTaskPlan())
        {
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
                        Wander(NpcText.Action("restVillageNoon"));
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

                    Wander(NpcText.Action("eveningWalkVillage"));
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
            Wander(NpcText.Action("wanderVillage"));
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

        DoCultivatorActivity();
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
            actionTimer = Random.Range(4f, 8f);
            currentAction = NpcText.Action("harvestResource");
        }
    }

    void CultivateNaturally()
    {
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
        actionTimer =
            Mathf.Max(
                thinkInterval,
                Random.Range(6f, 12f));
        currentAction = NpcText.Action("cultivateAbsorbQi");
    }

    bool TryGoHomeForCultivation()
    {
        if (homeRoutineManagedExternally)
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
        movingToRoad = false;
        hasRoadPreference = false;
    }

    bool TryProcessDailyTaskPlan()
    {
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
            (timeSystem.CurrentPhase == WorldTimePhase.Night ||
            timeSystem.CurrentPhase == WorldTimePhase.Dawn))
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

        currentAction = NpcText.ActionFormat("requestBuyTaskItem", missingNeed.item.itemName);

        if (requiredMoney > 0 &&
            NpcEconomy.GetNpcMoney(gameObject) < requiredMoney)
        {
            currentAction = NpcText.Action("notEnoughSpiritStoneWorkTask");
            return false;
        }

        currentAction = NpcText.ActionFormat("goStoreBuyItem", missingNeed.item.itemName);

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
            currentAction = NpcText.ActionFormat("boughtTaskItem", missingNeed.item.itemName);

            if (GetMissingDailyTaskItemAmount(missingNeed) <= 0)
            {
                dailyTaskNeeds.Remove(missingNeed);
            }

            return GetFirstMissingDailyTaskNeed() != null;
        }

        dailyTaskNeeds.Remove(missingNeed);
        currentAction = NpcText.ActionFormat("storeMissingItem", missingNeed.item.itemName);
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
                currentAction = NpcText.Action("noTrade");
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
        StopMoving();
        ClearMovementTargets();
        hunger = 0f;
        money = Mathf.Max(0, money - 1);
        actionTimer = eatDuration;
        currentAction = NpcText.Action("eatAtShop");
    }

    void GatherAndPlay()
    {
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
    if (job == VillagerJob.Trader)
    {
        TryTradeOrTaskOrIdle();
        return;
    }

    NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
    if (gatherer != null &&
        gatherer.enabled &&
        gatherer.canGather &&
        gatherer.TryStartGatheringNow())
    {
        currentAction = GetWorkingAction();
        return;
    }

    if (!hasWorkTarget)
    {
        WorldTilemapManager worldTilemap =
            WorldTilemapManager.Instance;

        switch (job)
        {
            case VillagerJob.Farmer:

                if (farmerPreferWorkPoint &&
                    workPoint != null)
                {
                    currentWorkTarget = GetWorkPointPosition(VillagerJob.Farmer);
                    currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);
                    if (!currentWorkTargetZone.HasValue)
                    {
                        currentWorkTargetZone = NpcMapZone.Lang;
                    }
                }
                else
                {
                    currentWorkTarget =
                        worldTilemap != null
                        ? worldTilemap.GetFarmTile()
                        : Vector3.zero;
                    currentWorkTargetZone = NpcMapZone.Lang;
                }

                break;

            case VillagerJob.Fisher:

                currentWorkTarget =
                    worldTilemap != null
                    ? worldTilemap.GetFishingTile(this)
                    : Vector3.zero;
                currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);

                // Neu khong co diem cau ca thi doi sang lam ruong tam.
                if (currentWorkTarget ==
                    Vector3.zero)
                {
                    currentWorkTarget =
                        worldTilemap != null
                        ? worldTilemap.GetFarmTile()
                        : Vector3.zero;

                    currentAction = NpcText.Action("noFishingSpotFarmFallback");
                }

                break;

            case VillagerJob.Hunter:

                if (autonomousDangerousWorkEnabled)
                {
                    currentWorkTarget =
                        worldTilemap != null
                        ? worldTilemap.GetHuntingTile()
                        : Vector3.zero;
                    currentWorkTargetZone = NpcMapZone.MaThuSonMach;
                }
                else if (workPoint != null)
                {
                    currentWorkTarget = GetWorkPointPosition(job);
                    currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);
                }
                else
                {
                    currentWorkTarget = GetFallbackActivityPosition();
                    currentWorkTargetZone = GetCurrentMapZone();
                }

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
            currentWorkTarget =
                workPoint != null
                ? workPoint.position
                : GetFallbackActivityPosition();
            currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);
        }

        hasWorkTarget = true;
    }

    float distance =
        Vector2.Distance(
            transform.position,
            currentWorkTarget);

    if (distance >= 0.5f)
    {
        currentAction = GetWorkAction();

        MoveUsingRoad(
            currentWorkTarget,
            currentWorkTargetZone);
        return;
    }

    currentAction = GetWorkingAction();

    if (distance < 0.5f)
    {
        ClearMovementTargets();
        StopMoving();

        bool produced = AddWorkProduct();

        fatigue =
            Mathf.Clamp(
                fatigue + 8f,
                0f,
                100f);

        actionTimer = produced
            ? Random.Range(workDurationMin, workDurationMax)
            : Mathf.Max(thinkInterval, 2f);

        if (produced)
        {
            AddProfessionExp(professionExpPerWork);
        }
    }
}

    void GoTrade()
    {
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
            currentAction = NpcText.Action("noTrade");
        }
    }

    bool TryTradeAtCounterOrTakeTask()
    {
        bool traded = false;
        NpcCounterBroker broker = NpcCounterBroker.Active;

        if (broker != null &&
            broker.receiveAllNpcRequests &&
            IsInsideBrokerServiceArea(broker))
        {
            NpcTradeAgent tradeAgent = GetComponent<NpcTradeAgent>();
            if (tradeAgent != null && broker.CanTradeWithNpc(tradeAgent))
            {
                traded = broker.TryTradeWithNpc(tradeAgent);
            }
        }

        if (!traded && HasSellableGoods())
        {
            traded = TrySellGoodsToTrader();
        }

        if (traded)
        {
            actionTimer = tradeDuration;
            currentAction = NpcText.Action("trading");
            return true;
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

        if (marketPoint != null)
        {
            return marketPoint.position;
        }

        if (workPoint != null)
        {
            return workPoint.position;
        }

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
        if (!ShouldUseSharedTargetSpacing(target) ||
            !IsSharedTargetOccupied(targetPosition))
        {
            return targetPosition;
        }

        int slotCount = 6;
        int slotIndex = Mathf.Abs(
            gameObject.GetInstanceID() ^
            target.gameObject.GetInstanceID()) % slotCount;
        float angle = (Mathf.PI * 2f * slotIndex) / slotCount;
        Vector3 offset = new Vector3(
            Mathf.Cos(angle),
            Mathf.Sin(angle),
            0f) * Mathf.Max(arriveDistance, sharedTargetSpacingRadius);

        return ClampToCurrentMapArea(targetPosition + offset);
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
        float radius = Mathf.Max(0.05f, sharedTargetOccupancyRadius);
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

        if (product == null)
        {
            money += GetWorkIncome();
            lastWorkProductStatus = NpcText.Get("workStatus", "noProductPaid");
            currentAction = NpcText.Action("paidWork");
            return true;
        }

        if (job == VillagerJob.Farmer)
        {
            return AddFarmerProduct(product);
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

    bool AddFarmerProduct(StatItemData product)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;

        if (farmerHarvestOnlyInMorning &&
            timeSystem != null &&
            !IsFarmerHarvestTime(timeSystem.CurrentPhase))
        {
            lastWorkProductStatus = NpcText.Get("workStatus", "farmerWaitMorning");
            currentAction = NpcText.Action("farmerWaitHarvest");
            return false;
        }

        int currentDay =
            timeSystem != null
            ? timeSystem.CurrentDay
            : -1;

        if (currentDay >= 0 &&
            lastFarmerHarvestDay == currentDay)
        {
            lastWorkProductStatus =
                NpcText.Format(
                    NpcText.Get("workStatus", "farmerHarvestedDay"),
                    currentDay);
            currentAction = NpcText.Action("farmerHarvestedToday");
            return false;
        }

        int amount =
            Mathf.Max(1, farmerHarvestAmountPerDay) +
            GetProfessionProductBonus();

        inventory.AddItem(product, amount);
        lastFarmerHarvestDay = currentDay;
        lastWorkProductStatus =
            NpcText.Format(
                NpcText.Get("workStatus", "addedItemAmountInventory"),
                product.itemName,
                amount,
                inventory.GetAmount(product));
        currentAction = NpcText.ActionFormat("harvestItemAmount", product.itemName, amount);
        return true;
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

        Vector2 away = transform.position - origin;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = Random.insideUnitCircle.normalized;
        }

        Vector3 waitPosition =
            origin +
            (Vector3)away.normalized * Mathf.Max(0.5f, safeRadius);

        SetDirectMoveTarget(waitPosition);
        currentAction = lowPowerSkirmish
            ? NpcText.ActionFormat("outerSkirmishNamed", item.itemName)
            : NpcText.ActionFormat("waitLightningNamed", item.itemName);
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
        actionTimer = 0f;
        SetTarget(
            target,
            NpcText.ActionFormat("treasureHuntNamed", item.itemName));
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
            ? "Đi hái " + item.itemName
            : NpcText.Action("gatherVillageResource");

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
            NpcText.ActionFormat("treasureHuntNamed", treasureHuntItem.itemName));
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
        if (workPoint == null)
        {
            return Vector3.zero;
        }

        NpcWorkArea area = workPoint.GetComponent<NpcWorkArea>();
        if (area != null && area.job == targetJob)
        {
            return area.GetRandomPoint();
        }

        return GetDistributedPointAround(workPoint.position, workPoint);
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
    bool usingTeleportRoute;
    string routeAction;
    target = NpcMapNavigator.GetNextMoveTarget(
        gameObject,
        target,
        targetZone,
        out usingTeleportRoute,
        out routeAction);

    if (usingTeleportRoute &&
        !string.IsNullOrEmpty(routeAction))
    {
        currentAction = routeAction;
    }

    if (WorldTilemapManager.Instance == null)
    {
        MoveToPosition(target);
        return;
    }

    if (ShouldBypassRoad())
    {
        movingToRoad = false;
        hasRoadPreference = false;
        MoveToPosition(target);
        return;
    }

    if (!hasRoadPreference ||
        Vector2.Distance(roadPreferenceTarget, target) > 0.5f)
    {
        roadPreferenceTarget = target;
        prefersRoadForCurrentRoute =
            Random.value < roadPreferenceChance;
        hasRoadPreference = true;
        movingToRoad = false;
    }

    if (!prefersRoadForCurrentRoute)
    {
        MoveToPosition(target);
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
        MoveToPosition(target);
        return;
    }

    float roadDistance =
        Vector2.Distance(
            transform.position,
            roadWaypoint);

    if (roadDistance > pathWaypointReachDistance)
    {
        MoveToPosition(roadWaypoint);

        currentAction = NpcText.Action("walkingRoad");

        return;
    }

    MoveToPosition(target);

    if (Vector2.Distance(transform.position, target) < 0.4f)
    {
        movingToRoad = false;
        hasRoadPreference = false;
    }
}

    bool IsReachableRoadTile(Vector3 road)
    {
        return IsMoveTargetFeasible(road) &&
            HasClearLineTo(road);
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

    void MoveToPosition(Vector3 position)
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
        ClearActivePath();
    }

    bool IsBusyActionActive()
    {
        return actionTimer > 0f;
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
        if (currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return true;
        }

        Vector2 closest = currentMapArea.areaBounds.ClosestPoint(position);
        return Vector2.Distance(closest, position) <= 0.02f;
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;

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
        }
        else if (area != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
        }

        currentMapArea = area;
        desiredVelocity = Vector2.zero;
        hasDirectMoveTarget = false;
        hasWanderTarget = false;
        hasRoadPreference = false;
        movingToRoad = false;
        hasObstacleAvoidTarget = false;
        blockedMoveTimer = 0f;
        crowdBlockedTimer = 0f;
        ClearActivePath();
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

        if (desiredVelocity.sqrMagnitude <= 0.0001f)
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
        Vector2 escapeDirection = desiredVelocity.sqrMagnitude > 0.0001f
            ? desiredVelocity.normalized
            : GetDirectionToActiveMoveTarget();

        if (!TryPickObstacleEscapeTarget(escapeDirection, out escapeTarget) &&
            !TryPickCrowdEscapeTarget(out escapeTarget))
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
        movingToRoad = false;
        SetDirectMoveTarget(escapeTarget);
        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;
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

        visualAnimation.UpdateNPCAnimation(direction, isIdle);
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
        for (int i = 0; i < maxPickTargetAttempts; i++)
        {
            float radius = baseRadius + i * 0.15f;
            Vector2 offset = Random.insideUnitCircle.normalized * radius;
            Vector3 candidate = ClampToCurrentMapArea(preferred + new Vector3(offset.x, offset.y, 0f));
            if (IsMoveTargetFeasible(candidate))
            {
                result = candidate;
                return true;
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
            Mathf.Max(0.01f, targetClearRadius),
            delta.normalized,
            distance,
            obstacleLayers);

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
            Mathf.Max(0.01f, targetClearRadius),
            direction.normalized,
            GetObstacleLookAheadDistance(),
            obstacleLayers);

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
                currentAction = NpcText.Action("avoidObstacle");
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
        currentAction = NpcText.Action("avoidObstacle");
        return true;
    }

    void OnDisable()
    {
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
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
                currentAction = NpcText.Action("avoidObstacle");
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
                Mathf.Max(0.01f, targetClearRadius),
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
                Mathf.Max(0.01f, targetClearRadius),
                direction.normalized,
                maxDistance,
                obstacleLayers);

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
            targetClearRadius,
            obstacleLayers);

        foreach (Collider2D hit in hits)
        {
            if (IsBlockingObstacle(hit))
            {
                return true;
            }
        }

        return false;
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
        if (separationRadius <= 0f)
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
                hit.GetComponentInParent<SmartNpcAI>() == null)
            {
                continue;
            }

            Vector2 away =
                (Vector2)transform.position -
                (Vector2)hit.transform.position;

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
                return NpcText.Action("goFarmWork");
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
        int multiplier =
            GetRealmMultiplier();

        maxHP = baseMaxHP * multiplier;
        attack = baseAttack * multiplier;
        defense = baseDefense * multiplier;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
    }

    int GetRealmMultiplier()
    {
        int multiplier = 1;
        int realmIndex = Mathf.Max(0, (int)realm);
        int stage =
            Mathf.Clamp(
                realmStage,
                1,
                CultivationProgression.MaxStage);

        for (int i = 0; i < realmIndex; i++)
        {
            multiplier *= 10;
        }

        return multiplier * stage;
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

        Collider2D collider2d =
            GetComponent<Collider2D>();

        if (collider2d != null)
        {
            collider2d.enabled = false;
        }

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
