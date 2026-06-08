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

    public int effectResistance = 0;
    public CharacterStats characterStats;
    public int lifespan = 80;
    public bool dieWhenLifespanEnds = true;

    [Header("Tu luyen")]
    public long cultivation = 0;

    public long breakthroughNeed = 100;

    public bool readyForHeavenlyTribulation = false;

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
    public float separationRadius = 0.55f;
    public float separationStrength = 1.5f;
    public float crowdLookAheadDistance = 0.7f;
    public float crowdDetourDistance = 0.6f;
    public float crowdYieldDuration = 0.22f;

    public Transform currentTarget;
    Transform treasureHuntTarget;
    StatItemData treasureHuntItem;
    bool waitingOutsideTreasureLightning;
    Vector3 treasureWaitPosition;
    bool hasTreasureWaitPosition;

    private Rigidbody2D rb;
    Collider2D[] selfColliders;

    [Header("Chien dau")]
    public float attackRange = 1.5f;

    public float attackCooldown = 1f;

    private float attackTimer = 0;

    bool isDead;

    private MonsterAI currentMonsterTarget;
    float movementPausedUntil;
    float crowdYieldUntil;

    [Header("Skill")]
    public GameObject fireballPrefab;

    public Transform firePoint;

    [Header("Khoang cach hoat dong")]
    public float maxRoamDistance = 10f;

    private Vector3 spawnPosition;

    [Header("Dia diem")]
    public Transform homePoint;

    public Transform tavernPoint;

    public Transform forestPoint;

    public Transform farmPoint;

    [Header("Trang thai hien tai")]
    public string currentAction = "idle";

    private float thinkTimer = 0;

    public float thinkDelay = 2f;

    public bool IsDead =>
        isDead ||
        (characterStats != null ?
        characterStats.IsDead :
        currentHP <= 0);

    public Transform DamageTransform => transform;

    void Start()
    {
        currentAction = NpcText.Action("idle");
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        inventory.UsePrivateNpcRuntimeItems(false);

        rb = GetComponent<Rigidbody2D>();
        if (rb != null && useKinematicNpcMovement)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        selfColliders = GetComponentsInChildren<Collider2D>();

        spawnPosition = transform.position;

        characterStats = GetComponent<CharacterStats>();

        bool appliedProfile = false;
        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
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
    }

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

        thinkTimer += Time.deltaTime;

        attackTimer += Time.deltaTime;

        if (treasureHuntTarget != null || waitingOutsideTreasureLightning)
        {
            RefreshTreasureHuntAction();
            thinkTimer = 0f;
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
        if (IsDead)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        UpdateMovement();
    }

    void UpdateMovement()
    {
        if (Time.time < movementPausedUntil ||
            Time.time < crowdYieldUntil)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        bool movingToTreasureWait =
            waitingOutsideTreasureLightning &&
            hasTreasureWaitPosition;

        if (currentTarget == null &&
            !movingToTreasureWait)
        {
            rb.linearVelocity = Vector2.zero;

            return;
        }

        Vector3 desiredTarget = movingToTreasureWait
            ? treasureWaitPosition
            : currentTarget.position;

        bool usingTeleportRoute;
        string routeAction;
        Vector3 moveTarget =
            NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                desiredTarget,
                out usingTeleportRoute,
                out routeAction);

        if (usingTeleportRoute &&
            !string.IsNullOrEmpty(routeAction))
        {
            currentAction = routeAction;
        }

        NpcMapArea spawnArea =
            NpcMapArea.FindArea(spawnPosition);
        NpcMapArea targetArea =
            NpcMapArea.FindArea(desiredTarget);
        bool targetInSpawnArea =
            spawnArea == null ||
            targetArea == null ||
            spawnArea.zone == targetArea.zone;

        float distanceFromSpawn =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (!movingToTreasureWait &&
            treasureHuntTarget == null &&
            !usingTeleportRoute &&
            targetInSpawnArea &&
            distanceFromSpawn > maxRoamDistance)
        {
            ReturnToSpawn();

            return;
        }

        Vector2 direction =
            (moveTarget -
            transform.position).normalized;

        if (!TryResolveCrowdAhead(direction, out direction))
        {
            return;
        }

        direction = ApplyCrowdAvoidance(direction);

        rb.linearVelocity =
            direction * moveSpeed;
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;

        if (gate != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
        }
        else
        {
            NpcMapArea area = NpcMapArea.FindArea(transform.position);
            if (area != null)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
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
        hasTreasureWaitPosition = false;
        treasureHuntTarget = target;
        treasureHuntItem = item;
        currentTarget = target;
        currentAction = NpcText.ActionFormat("treasureHuntNamed", item.itemName);
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
        currentAction = NpcText.ActionFormat("treasureHuntNamed", treasureHuntItem.itemName);
    }

    void ReturnToSpawn()
    {
        Vector2 direction =
            (spawnPosition -
            transform.position).normalized;

        if (!TryResolveCrowdAhead(direction, out direction))
        {
            return;
        }

        direction = ApplyCrowdAvoidance(direction);

        rb.linearVelocity =
            direction * moveSpeed;

        currentAction = NpcText.Action("returnTerritory");

        float distance =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (distance < 1f)
        {
            rb.linearVelocity = Vector2.zero;

            currentTarget = null;
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

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    bool TryResolveCrowdAhead(
        Vector2 desiredDirection,
        out Vector2 resolvedDirection)
    {
        resolvedDirection = desiredDirection;

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
        if (separationRadius <= 0f ||
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

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            if (timeSystem.CurrentPhase == WorldTimePhase.Night &&
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

        if (canLive && fatigue >= 85)
        {
            Sleep();

            return;
        }

        if (canCultivate &&
            (pill > 0 || spiritStone > 0))
        {
            Cultivate();

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

        currentAction = NpcText.Action("wanderVillage");
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

        Debug.Log(NpcText.Format(NpcText.Get("logs", "eat"), npcName));
    }

    void Sleep()
    {
        if (homePoint == null)
        {
            fatigue = 0;
            currentTarget = null;
            currentAction = NpcText.Action("rest");
            return;
        }

        currentTarget = homePoint;

        float distance =
            Vector2.Distance(
                transform.position,
                homePoint.position);

        if (distance >= 1.5f)
        {
            currentAction = NpcText.Action("goHomeRest");
            return;
        }

        currentAction = NpcText.Action("rest");

        if (distance < 1.5f)
        {
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
    }

    void Cultivate()
    {
        if (IsDead)
        {
            return;
        }

        if (TryGoHomeForCultivation())
        {
            return;
        }

        currentAction = NpcText.Action("cultivate");

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

    bool TryGoHomeForCultivation()
    {
        if (homePoint == null)
        {
            return false;
        }

        float distance =
            Vector2.Distance(
                transform.position,
                homePoint.position);

        if (distance <= 1.2f)
        {
            return false;
        }

        currentTarget = homePoint;
        currentAction = NpcText.Action("goHomeCultivate");
        return true;
    }

    bool NeedsFood()
    {
        return realm < CultivationRealm.Foundation;
    }

    void AddCultivationProgress(int amount)
    {
        if (IsDead)
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

        while (cultivation >= breakthroughNeed &&
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
        if (characterStats != null)
        {
            characterStats.Breakthrough();
            SyncFromCharacterStats();
            currentAction = NpcText.Action("breakthrough");
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

        realmStage += 1;

        if (realmStage > 9)
        {
            realmStage = 1;

            realm += 1;
        }

        ApplyRealmPower(true);
        lifespan = GetLifespanForRealm(realm);

        currentAction = NpcText.Action("breakthrough");

        Debug.Log(NpcText.Format(NpcText.Get("logs", "breakthrough"), npcName, GetRealmName(), realmStage));
    }

    void GoToTavernAndBuyPill()
    {
        if (tavernPoint == null)
        {
            currentTarget = null;
            currentAction = NpcText.Action("calm");
            return;
        }

        currentAction = NpcText.Action("goTavern");

        currentTarget =
            tavernPoint;

        float distance =
            Vector2.Distance(
                transform.position,
                tavernPoint.position);

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
                spawnPosition,
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

    currentMonsterTarget.TakeDamage(attackDamage);

    currentAction = NpcText.ActionFormat("attackMonsterNamed", currentMonsterTarget.monsterName);

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
        GetRealmPower();

    int monsterPower =
        monster.GetRealmPower();

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

    public int GetRealmPower()
    {
        return CultivationProgression.GetRealmPower(
            realm,
            realmStage);
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

        int power =
            GetRealmPower();

        maxHP =
            100 + power * 40;

        currentHP =
            fillHP
                ? maxHP
                : Mathf.Clamp(
                    Mathf.RoundToInt(maxHP * hpPercent),
                    0,
                    maxHP);

        attack =
            10 + power * 8;

        defense =
            5 + power * 5;

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

        if (characterStats != null)
        {
            characterStats.currentHP = 0;
        }

        currentTarget = null;
        currentMonsterTarget = null;
        currentAction = NpcText.Action("dead");

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

        Destroy(gameObject);
    }

}



