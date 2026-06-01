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
    public string villagerName = "Nguoi dan";
    public VillagerAgeGroup ageGroup = VillagerAgeGroup.Adult;
    public VillagerJob job = VillagerJob.Farmer;
    public bool keepInspectorJob;

    [Header("Stats")]
    public int maxHP = 100;
    public int currentHP = 100;
    public int money = 20;
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
    public Transform workPoint;
    public Transform marketPoint;
    public Transform playPoint;

    [Header("Behavior")]
    public float thinkInterval = 2f;
    public float arriveDistance = 0.25f;
    public float wanderRadius = 3f;
    public float talkRadius = 1.2f;
    public LayerMask villagerLayers = ~0;
    public int acquaintanceTalkChanceBonus = 30;
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
    public bool ignoreNpcBodyCollisions = true;
    public float unstuckCheckDelay = 1.2f;
    public float unstuckMinMoveDistance = 0.03f;
    public float unstuckOffsetRadius = 0.7f;
    public float movementAcceleration = 8f;
    public float movementDeceleration = 12f;
    public float animationIdleSpeed = 0.03f;
    public float idleAtHomeDuration = 6f;

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
    public string currentAction = "Dung yen";
    public Transform currentTarget;
    public string lastWorkProductStatus;

    Rigidbody2D rb;
    NPCVisualAnimation visualAnimation;
    Vector3 spawnPosition;
    Vector3 wanderTarget;
    Vector3 directMoveTarget;
    float thinkTimer;
    float actionTimer;
    float nextSocialScanTime;
    bool hasWanderTarget;
    bool hasDirectMoveTarget;
    bool movingToRoad;
    bool hasRoadPreference;
    bool prefersRoadForCurrentRoute;
    Vector3 roadPreferenceTarget;
    Vector2 desiredVelocity;
    Vector3 lastUnstuckPosition;
    float stuckMoveTimer;
    Collider2D[] ownColliders;
    int lastPlanResetDay = -1;
    int lastFarmerHarvestDay = -1;

    Vector3 currentWorkTarget;
    NpcMapZone? currentWorkTargetZone;
    Vector3 currentTradeTarget;
    Vector3 currentEatTarget;
    Vector3 currentSellTarget;
    NpcMapZone? currentSellTargetZone;

    bool hasWorkTarget;
    bool hasTradeTarget;
    bool hasEatTarget;
    bool hasSellTarget;
    readonly System.Collections.Generic.HashSet<VillagerAI> acquaintances =
        new System.Collections.Generic.HashSet<VillagerAI>();

    public bool IsDead =>
        characterStats != null ?
        characterStats.IsDead :
        currentHP <= 0;

    public Transform DamageTransform => transform;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ownColliders = GetComponentsInChildren<Collider2D>();
        lastUnstuckPosition = transform.position;
        visualAnimation = GetComponent<NPCVisualAnimation>();
        characterStats = GetComponent<CharacterStats>();
        inventory = inventory != null
            ? inventory
            : GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        spawnPosition = transform.position;

        ConfigureRigidbody();

        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
        }

        if (characterStats != null)
        {
            characterStats.generatedEntityKind = EntityKind.Villager;
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
            currentHP = Mathf.Clamp(currentHP, 1, maxHP);
        }
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
                EntityKind.Villager);

        if (entityProfile == null)
        {
            return;
        }

        if (entityProfile.kind != EntityKind.Villager)
        {
            EntityGenerator.FillProfile(entityProfile, EntityKind.Villager);
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
            Mathf.Clamp(entityProfile.stats.currentHP, 1, maxHP);
        attack = entityProfile.stats.attack;
        defense = entityProfile.stats.defense;
        moveSpeed = entityProfile.stats.moveSpeed;
        money = entityProfile.stats.money;
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

        entityProfile.kind = EntityKind.Villager;
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
        SyncFromCharacterStats();

        if (IsDead)
        {
            StopMoving();
            return;
        }

        UpdateNeeds();
        UpdateMood();
        TryTalkToPassingVillager();

        thinkTimer += Time.deltaTime;
        actionTimer -= Time.deltaTime;

        if (thinkTimer >= thinkInterval)
        {
            thinkTimer = 0f;
            Think();
        }
    }

    void FixedUpdate()
    {
        SyncFromCharacterStats();

        if (IsDead)
        {
            StopMoving();
            return;
        }

        MoveToCurrentTarget();
        UpdateUnstuck();
        ApplySmoothVelocity();
        UpdateVisualAnimation();
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
            currentAction = "Tho nguyen da tan";
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
            GoTrade();
            return;
        }

        switch (timeSystem.CurrentPhase)
        {
            case WorldTimePhase.Evening:
            case WorldTimePhase.Night:
                GoHomeIdle("Dong tiem ve nha");
                return;
            default:
                GoTrade();
                return;
        }
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

        GoHomeIdle("O gan nha");
    }

    void ThinkAdult()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            switch (timeSystem.CurrentPhase)
            {
                case WorldTimePhase.Dawn:
                    if (job == VillagerJob.Trader)
                    {
                        GoTrade();
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
                        GoTrade();
                        return;
                    }

                    GoWorkOrCultivatorActivity();
                    return;

                case WorldTimePhase.Noon:
                    if (NeedsFood())
                    {
                        GoEat();
                    }
                    else
                    {
                        DoCultivatorActivity();
                    }
                    return;

                case WorldTimePhase.Afternoon:
                    if (job == VillagerJob.Trader)
                    {
                        GoTrade();
                        return;
                    }

                    GoWorkOrCultivatorActivity();
                    return;

                case WorldTimePhase.Evening:
                    if (job == VillagerJob.Trader)
                    {
                        GoHomeIdle("Dong tiem ve nha");
                        return;
                    }

                    if (NeedsFood() &&
                        hunger >= 45f)
                    {
                        GoEat();
                        return;
                    }

                    if (fun < 55f && playPoint != null)
                    {
                        GatherAndPlay();
                        return;
                    }

                    GoHomeIdle("Ve nha sinh hoat");
                    return;

                case WorldTimePhase.Night:
                    GoHomeToRest();
                    return;
            }
        }

        if (job == VillagerJob.Trader)
        {
            GoTrade();
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
        if (ShouldDoMortalWork())
        {
            GoWork();
            return;
        }

        DoCultivatorActivity();
    }

    void DoCultivatorActivity()
    {
        if (realm >= CultivationRealm.Foundation &&
            Random.value < cultivatorResourceWorkChance)
        {
            GoResourceWork();
            return;
        }

        CultivateNaturally();
    }

    void GoResourceWork()
    {
        if (job == VillagerJob.Hunter ||
            job == VillagerJob.Guard ||
            job == VillagerJob.Worker)
        {
            GoWork();
            return;
        }

        if (bravery >= 55)
        {
            GoToResourcePoint(
                WorldTilemapManager.Instance != null
                ? WorldTilemapManager.Instance.GetHuntingTile()
                : Vector3.zero,
                "Di san yeu thu / tim tai nguyen",
                NpcMapZone.MaThuSonMach);
            return;
        }

        GoToResourcePoint(
            workPoint != null
            ? workPoint.position
            : GetFallbackActivityPosition(),
            "Thu hoach tai nguyen tu luyen");
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
            currentAction = "Dang thu hoach tai nguyen";
        }
    }

    void CultivateNaturally()
    {
        int gain =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    (1 + (int)realm + realmStage * 0.2f) *
                    Mathf.Max(0.5f, diligence / 50f)));

        AddCultivationExp(gain);
        currentAction = "Tu luyen hap thu linh khi";

        Vector3 target =
            GetFallbackActivityPosition();

        MoveUsingRoad(target);
    }

    void ResetDailyTargets()
    {
        if (WorldTilemapManager.Instance != null)
        {
            WorldTilemapManager.Instance.ReleaseFishingTile(this);
        }

        hasWorkTarget = false;
        hasTradeTarget = false;
        hasEatTarget = false;
        hasSellTarget = false;
        currentSellTargetZone = null;
        movingToRoad = false;
        hasRoadPreference = false;
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

        return currentAction.Contains("Dang buon ban") ||
            currentAction.Contains("Dang choi") ||
            currentAction.Contains("Ve nha") ||
            currentAction.Contains("gan nha") ||
            currentAction.Contains("sinh hoat");
    }

    void TryTalkToPassingVillager()
    {
        if (Time.time < nextSocialScanTime ||
            actionTimer > 0f ||
            IsDead ||
            !CanSocializeNow())
        {
            return;
        }

        nextSocialScanTime = Time.time + 1f;

        VillagerAI other =
            FindNearbyVillager();

        if (other == null)
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

    void GoHomeToRest()
    {
        Vector3 homePosition = GetHomePosition();
        MoveUsingRoad(homePosition);
        currentAction = "Ve nha nghi ngoi";

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
            currentAction = "Dang nghi ngoi";
            ResetDailyTargets();

            NpcHomeResident resident = GetComponent<NpcHomeResident>();
            if (resident != null && resident.hideAtHome)
            {
                resident.ForceHiddenAtHome(true);
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
        currentAction = "Di an";

        if (IsAtPosition(currentEatTarget))
        {
            ClearMovementTargets();
            StopMoving();
            hasEatTarget = false;
            hunger = 0f;
            money = Mathf.Max(0, money - 1);
            actionTimer = eatDuration;
            currentAction = "Dang an";
        }
    }

    void EatWhereTraderIs()
    {
        StopMoving();
        ClearMovementTargets();
        hunger = 0f;
        money = Mathf.Max(0, money - 1);
        actionTimer = eatDuration;
        currentAction = "An tai tiem";
    }

    void GatherAndPlay()
    {
        if (playPoint == null)
        {
            GoHomeIdle("Nghi ngoi gan nha");
            return;
        }

        SetTarget(
            playPoint,
            "Tu tap di choi");

        if (HasArrived())
        {
            ClearMovementTargets();
            StopMoving();
            fun = 100f;
            actionTimer = playDuration;
            currentAction = "Dang choi cung ban";
            TalkToNearbyVillager();
        }
    }
    void GoWork()
{
    if (job == VillagerJob.Trader)
    {
        GoTrade();
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
                }
                else
                {
                    currentWorkTarget =
                        worldTilemap != null
                        ? worldTilemap.GetFarmTile()
                        : Vector3.zero;
                }

                break;

            case VillagerJob.Fisher:

                currentWorkTarget =
                    worldTilemap != null
                    ? worldTilemap.GetFishingTile(this)
                    : Vector3.zero;
                currentWorkTargetZone = NpcMapNavigator.GetDestinationZone(workPoint);

                // Hồ đông thì đổi nghề tạm
                if (currentWorkTarget ==
                    Vector3.zero)
                {
                    currentWorkTarget =
                        worldTilemap != null
                        ? worldTilemap.GetFarmTile()
                        : Vector3.zero;

                    currentAction =
                        "Ho dong nguoi, doi di lam ruong";
                }

                break;

            case VillagerJob.Hunter:

                currentWorkTarget =
                    worldTilemap != null
                    ? worldTilemap.GetHuntingTile()
                    : Vector3.zero;
                currentWorkTargetZone = NpcMapZone.MaThuSonMach;

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

    currentAction = GetWorkAction();

    MoveUsingRoad(
        currentWorkTarget,
        currentWorkTargetZone);

    float distance =
        Vector2.Distance(
            transform.position,
            currentWorkTarget);

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

            hasTradeTarget = true;
        }

        MoveUsingRoad(currentTradeTarget);
        currentAction = "Ra cho buon ban";

        if (IsAtPosition(currentTradeTarget))
        {
            ClearMovementTargets();
            StopMoving();
            actionTimer = tradeDuration;
            currentAction = "Dang buon ban";
        }
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
        currentAction = "Mang hang den truong quay";

        if (!IsAtPosition(currentSellTarget))
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
            currentAction = "Da ban hang hoa";
            return;
        }

        actionTimer = sellGoodsDuration;
        currentAction = "Cho thuong nhan mua hang";
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
            other.IsDead)
        {
            return;
        }

        acquaintances.Add(other);
        other.acquaintances.Add(this);

        ApplySocialMemory(other);

        currentAction =
            GetConversationAction(other);

        other.currentAction =
            other.GetConversationAction(this);

        actionTimer = 2f;
        other.actionTimer = 2f;
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
            return "Vui ve noi chuyen voi " + other.villagerName;
        }

        if (mood == VillagerMood.Sad)
        {
            return "Tam su voi " + other.villagerName;
        }

        if (mood == VillagerMood.Tired)
        {
            return "Hoi tham " + other.villagerName;
        }

        if (acquaintances.Contains(other))
        {
            return "Gap nguoi quen: " + other.villagerName;
        }

        return "Noi chuyen voi " + other.villagerName;
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
        if (workPoint != null)
        {
            return workPoint.position;
        }

        if (marketPoint != null)
        {
            return marketPoint.position;
        }

        return GetMarketPosition(GetFallbackActivityPosition());
    }

    Vector3 GetSellGoodsTarget()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null && broker.receiveAllNpcRequests)
        {
            return broker.transform.position;
        }

        return GetMarketPosition(
            marketPoint != null
            ? marketPoint.position
            : GetFallbackActivityPosition());
    }

    NpcMapZone? GetSellGoodsTargetZone()
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null && broker.receiveAllNpcRequests)
        {
            NpcMapZone? brokerZone =
                NpcMapNavigator.GetDestinationZone(broker.transform);

            return brokerZone.HasValue
                ? brokerZone
                : NpcMapZone.VanBaoLau;
        }

        return NpcMapNavigator.GetDestinationZone(marketPoint);
    }

    bool IsAtPosition(Vector3 position)
    {
        return Vector2.Distance(
            transform.position,
            position) <= arriveDistance;
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
            lastWorkProductStatus = "Khong co product, nhan tien cong";
            currentAction = "Lam viec nhan tien cong";
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
            "Da them " + product.itemName + " x" + amount +
            ", trong balo: " + inventory.GetAmount(product);
        currentAction =
            "Thu hoach " + product.itemName + " x" + amount;
        return true;
    }

    bool AddFarmerProduct(StatItemData product)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;

        if (farmerHarvestOnlyInMorning &&
            timeSystem != null &&
            !IsFarmerHarvestTime(timeSystem.CurrentPhase))
        {
            lastWorkProductStatus =
                "Chua thu hoach: chi thu vao Dawn/Morning";
            currentAction = "Cham soc ruong, chua den gio thu hoach";
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
                "Chua thu hoach: da nhan trong ngay " + currentDay;
            currentAction = "Da thu hoach hom nay";
            return false;
        }

        int amount =
            Mathf.Max(1, farmerHarvestAmountPerDay) +
            GetProfessionProductBonus();

        inventory.AddItem(product, amount);
        lastFarmerHarvestDay = currentDay;
        lastWorkProductStatus =
            "Da them " + product.itemName + " x" + amount +
            ", trong balo: " + inventory.GetAmount(product);
        currentAction =
            "Thu hoach " + product.itemName + " x" + amount;
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
            arriveDistance)
        {
            Vector2 random =
                Random.insideUnitCircle * wanderRadius;

            wanderTarget =
                spawnPosition +
                new Vector3(random.x, random.y, 0f);

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

        SetTarget(
            target,
            "Truy doat " + item.itemName);
    }

    bool HasArrived()
    {
        if (currentTarget == null)
        {
            return false;
        }

        return Vector2.Distance(
            transform.position,
            currentTarget.position) <= arriveDistance;
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

        MoveToPosition(currentTarget.position);
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

        return workPoint.position;
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
        SetDirectMoveTarget(target);
        return;
    }

    if (ShouldBypassRoad())
    {
        movingToRoad = false;
        hasRoadPreference = false;
        SetDirectMoveTarget(target);
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
        SetDirectMoveTarget(target);
        return;
    }

    Vector3 road =
        WorldTilemapManager.Instance
        .GetNearestRoad(
            transform.position);

    float roadDistance =
        Vector2.Distance(
            transform.position,
            road);

    float targetDistance =
        Vector2.Distance(
            transform.position,
            target);

    if (!movingToRoad &&
        roadDistance < targetDistance * 0.5f)
    {
        SetDirectMoveTarget(road);

        currentAction =
            "Dang di tren duong";

        if (roadDistance < 0.4f)
        {
            movingToRoad = true;
        }

        return;
    }

    SetDirectMoveTarget(target);

    if (targetDistance < 0.4f)
    {
        movingToRoad = false;
        hasRoadPreference = false;
    }
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
            currentAction.Contains("Hoang so") ||
            currentAction.Contains("bo chay");
    }

    void MoveToPosition(Vector3 position)
    {
        Vector2 direction =
            (position - transform.position).normalized;

        Vector2 separation =
            GetSeparationDirection();

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

        if (rb != null)
        {
            desiredVelocity =
                direction * moveSpeed;
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

    void StopMoving()
    {
        if (rb != null)
        {
            desiredVelocity = Vector2.zero;
        }
    }

    void ClearMovementTargets()
    {
        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = false;
    }

    void SetDirectMoveTarget(Vector3 position)
    {
        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = true;
        directMoveTarget = position;
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

        Vector2 offset = Random.insideUnitCircle.normalized * Mathf.Max(0.1f, unstuckOffsetRadius);
        SetDirectMoveTarget(transform.position + (Vector3)offset);
        currentAction = "Dang tach khoi dam dong";
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

        rb.bodyType = RigidbodyType2D.Dynamic;
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

        Vector2 animationVelocity = desiredVelocity;
        if (animationVelocity.sqrMagnitude <= 0.0001f && rb != null)
        {
            animationVelocity = rb.linearVelocity;
        }

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
    }

    void TryIgnoreNpcCollision(Collider2D other)
    {
        if (!ignoreNpcBodyCollisions || other == null)
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
            if (own != null && own != other)
            {
                Physics2D.IgnoreCollision(own, other, true);
            }
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
                return "Ra dong lam ruong";
            case VillagerJob.Worker:
                return "Di lam viec";
            case VillagerJob.Guard:
                return "Di tuan tra";
            case VillagerJob.Healer:
                return "Di chua tri";
            case VillagerJob.Fisher:
                return "Di cau ca";
            case VillagerJob.Hunter:
                return "Di san ban";
            case VillagerJob.Trader:
                return "Ra cho buon ban";
            default:
                return "Di lam";
        }
    }

    string GetWorkingAction()
    {
        switch (job)
        {
            case VillagerJob.Farmer:
                return "Dang lam ruong";
            case VillagerJob.Worker:
                return "Dang lam viec";
            case VillagerJob.Guard:
                return "Dang tuan tra";
            case VillagerJob.Healer:
                return "Dang chua tri";
            case VillagerJob.Fisher:
                return "Dang cau ca";
            case VillagerJob.Hunter:
                return "Dang san ban";
            case VillagerJob.Trader:
                return "Dang buon ban";
            default:
                return "Dang lam";
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
            realm == CultivationRealm.Tribulation)
        {
            return;
        }

        cultivationExp += amount;

        while (cultivationExp >= ExpToNextRealm() &&
            realm != CultivationRealm.Tribulation)
        {
            cultivationExp -= ExpToNextRealm();
            Breakthrough();
        }
    }

    void Breakthrough()
    {
        if (realm == CultivationRealm.Tribulation)
        {
            cultivationExp = 0;
            return;
        }

        realmStage += 1;

        if (realmStage > CultivationProgression.MaxStage)
        {
            realmStage = 1;
            realm =
                (CultivationRealm)((int)realm + 1);
        }

        ApplyRealmPower();
        currentHP = maxHP;
        lifespan = GetLifespanForRealm(realm);
        currentAction = "Dot pha len " + GetRealmText();
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
            return characterStats.GetRealmText();
        }

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
                currentAction = "Hoang so bo chay";
                currentTarget = homePoint;
            }

            return;
        }

        if (IsDead)
        {
            return;
        }

        currentHP -= Mathf.Max(1, damage);

        if (currentHP <= 0)
        {
            Die();
        }
        else if (bravery < 50)
        {
            currentAction = "Hoang so bo chay";
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
            case StatType.SpiritStone:
                money += intValue;
                break;

            case StatType.MoveSpeed:
                moveSpeed += modifier.floatValue * direction;
                break;
        }
    }

    void Die()
    {
        currentHP = 0;
        currentAction = "Da chet";
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
