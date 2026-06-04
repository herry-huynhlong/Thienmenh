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

    [Header("Thông tin NPC")]
    public string npcName = "NPC";

    [Header("Bật / Tắt chức năng")]
    public bool canLive = true;
    public bool canCultivate = true;
    public bool canFight = true;
    public bool canTrade = true;
    public bool canMakeFriends = true;
    public bool canKillOthers = true;
    public bool canCompeteResource = true;
    public bool canCreateSect = true;
    public bool autonomousActivitiesEnabled = false;

    [Header("Cảnh giới")]
    public CultivationRealm realm = CultivationRealm.Mortal;

    [Range(1, 9)]
    public int realmStage = 1;

    [Header("Thiên phú")]
    [Range(1, 100)]
    public int comprehension = 10;

    public PhysiqueType physique = PhysiqueType.MortalBody;

    [Header("Chỉ số")]
    public int maxHP = 100;

    public int currentHP = 100;

    public int attack = 10;

    public int defense = 5;

    public int effectResistance = 0;
    public CharacterStats characterStats;
    public int lifespan = 80;
    public bool dieWhenLifespanEnds = true;

    [Header("Tu luyện")]
    public long cultivation = 0;

    public long breakthroughNeed = 100;

    public bool readyForHeavenlyTribulation = false;

    [Header("Tài sản")]
    [InspectorName("Linh Thạch")]
    public int money = 100;

    public int spiritStone = 0;

    public int pill = 0;

    [Header("Tính cách")]
    [Range(0, 100)]
    public int bravery = 50;

    [Range(0, 100)]
    public int greed = 50;

    [Range(0, 100)]
    public int kindness = 50;

    [Header("Nhu cầu sống")]
    [Range(0, 100)]
    public float hunger = 0;

    [Range(0, 100)]
    public float fatigue = 0;

    [Header("Di chuyển")]
    public float moveSpeed = 2f;

    public Transform currentTarget;

    private Rigidbody2D rb;

    [Header("Chiến đấu")]
    public float attackRange = 1.5f;

    public float attackCooldown = 1f;

    private float attackTimer = 0;

    bool isDead;

    private MonsterAI currentMonsterTarget;
    float movementPausedUntil;

    [Header("Skill")]
    public GameObject fireballPrefab;

    public Transform firePoint;

    [Header("Khoảng cách hoạt động")]
    public float maxRoamDistance = 10f;

    private Vector3 spawnPosition;

    [Header("Địa điểm")]
    public Transform homePoint;

    public Transform tavernPoint;

    public Transform forestPoint;

    public Transform farmPoint;

    [Header("Trạng thái hiện tại")]
    public string currentAction = "Đứng yên";

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
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        inventory.UsePrivateNpcRuntimeItems(false);

        rb = GetComponent<Rigidbody2D>();

        spawnPosition = transform.position;

        characterStats = GetComponent<CharacterStats>();

        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
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
            ApplyRealmPower();
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
            Mathf.Clamp(entityProfile.stats.currentHP, 1, maxHP);
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
        if (Time.time < movementPausedUntil)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;

            return;
        }

        bool usingTeleportRoute;
        string routeAction;
        Vector3 moveTarget =
            NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                currentTarget.position,
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
            NpcMapArea.FindArea(currentTarget.position);
        bool targetInSpawnArea =
            spawnArea == null ||
            targetArea == null ||
            spawnArea.zone == targetArea.zone;

        float distanceFromSpawn =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (!usingTeleportRoute &&
            targetInSpawnArea &&
            distanceFromSpawn > maxRoamDistance)
        {
            ReturnToSpawn();

            return;
        }

        Vector2 direction =
            (moveTarget -
            transform.position).normalized;

        rb.linearVelocity =
            direction * moveSpeed;
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

        currentTarget = target;
        currentAction =
            "Truy đoạt " + item.itemName;
    }

    void ReturnToSpawn()
    {
        Vector2 direction =
            (spawnPosition -
            transform.position).normalized;

        rb.linearVelocity =
            direction * moveSpeed;

        currentAction = "Quay về lãnh địa";

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
            currentAction = "Thọ nguyên đã tận";
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

        currentAction = "Đi dạo trong làng";
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

        currentAction = "Đi ăn";

        Debug.Log(
            npcName + " đang đi ăn.");
    }

    void Sleep()
    {
        currentAction = "Đi ngủ";

        currentTarget = homePoint;

        float distance =
            Vector2.Distance(
                transform.position,
                homePoint.position);

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

            Debug.Log(
                npcName + " đang nghỉ ngơi.");
        }
    }

    void Cultivate()
    {
        if (IsDead)
        {
            return;
        }

        currentAction = "Tu luy\u1ec7n";

        if (pill > 0)
        {
            pill -= 1;

            int gain =
                Mathf.RoundToInt(
                    30 *
                    GetCultivationMultiplier());

            AddCultivationProgress(gain);

            Debug.Log(
                npcName +
                " hấp thụ đan dược tăng " +
                gain +
                " tu vi.");
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

            Debug.Log(
                npcName +
                " hấp thụ linh thạch tăng " +
                gain +
                " tu vi.");
        }

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
            currentAction = "Dot pha";
            return;
        }

        if (realm ==
            CultivationRealm.Tribulation)
        {
            readyForHeavenlyTribulation = true;

            currentAction = "Chờ thiên kiếp";

            Debug.Log(
                npcName +
                " đã viên mãn Độ Kiếp.");

            return;
        }

        cultivation = 0;

        realmStage += 1;

        if (realmStage > 9)
        {
            realmStage = 1;

            realm += 1;
        }

        ApplyRealmPower();
        lifespan = GetLifespanForRealm(realm);

        currentAction = "Đột phá";

        Debug.Log(
            npcName +
            " đột phá lên " +
            GetRealmName() +
            " tầng " +
            realmStage);
    }

    void GoToTavernAndBuyPill()
    {
        currentAction =
            "Đi tửu lâu";

        currentTarget =
            tavernPoint;

        float distance =
            Vector2.Distance(
                transform.position,
                tavernPoint.position);

        if (distance < 1.5f)
        {
            money -= 50;

            pill += 1;

            Debug.Log(
                npcName +
                " mua đan dược.");
        }
    }

    void SearchMonster()
{
    // nếu đang có mục tiêu sống
    // thì tiếp tục đánh luôn
    if (currentMonsterTarget != null)
    {
        // quái chết thì bỏ target
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

        // quái chạy quá xa
        if (currentDistance > maxRoamDistance)
        {
            currentMonsterTarget = null;

            currentTarget = null;

            return;
        }

        // tiếp tục đánh
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
        // bỏ qua quái chết
        if (monster.currentHP <= 0)
        {
            continue;
        }

        // kiểm tra nên đánh không
        if (!ShouldFightMonster(monster))
        {
            continue;
        }

        // quái quá xa lãnh địa
        float distanceFromSpawn =
            Vector2.Distance(
                spawnPosition,
                monster.transform.position);

        if (distanceFromSpawn >
            maxRoamDistance)
        {
            continue;
        }

        // khoảng cách hiện tại
        float distance =
            Vector2.Distance(
                transform.position,
                monster.transform.position);

        // chọn mục tiêu gần nhất
        if (distance < closestDistance)
        {
            closestDistance =
                distance;

            bestTarget =
                monster;
        }
    }

    // tìm được quái
    if (bestTarget != null)
    {
        currentMonsterTarget =
            bestTarget;

        currentTarget =
            bestTarget.transform;

        currentAction =
            "Săn " +
            bestTarget.monsterName;
    }
}

void TryAttackMonster()
{
    if (currentMonsterTarget == null)
    {
        return;
    }

    // quái chết
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

    // chưa tới tầm đánh
    if (distance > attackRange)
    {
        return;
    }

    // hồi chiêu
    if (attackTimer < attackCooldown)
    {
        return;
    }

    // reset cooldown
    attackTimer = 0;

    // gây damage
    currentMonsterTarget.TakeDamage(attack);

    currentAction =
        "Đánh " +
        currentMonsterTarget.monsterName;

    Debug.Log(
        npcName +
        " tấn công " +
        currentMonsterTarget.monsterName +
        " gây " +
        attack +
        " sát thương.");
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
    // quái chết
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

    // máu thấp thì chạy
    if (myHpPercent <= 0.3f)
    {
        currentAction = "Bỏ chạy";

        return false;
    }

    // quái mạnh hơn nhiều
    if (difference >= 2)
    {
        // chỉ đánh nếu quái gần chết
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
        currentAction =
            "Kết bạn";

        Debug.Log(
            npcName +
            " đang giao tiếp.");
    }

    void TryCreateSect()
    {
        if (realm >=
            CultivationRealm.SoulFormation)
        {
            currentAction =
                "Lập tông môn";

            Debug.Log(
                npcName +
                " có thể lập tông môn.");
        }
    }

    public void TakeDamage(int damage)
    {
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

        currentHP -= finalDamage;

        Debug.Log(
            npcName +
            " nhận " +
            finalDamage +
            " sát thương.");

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

    void ApplyRealmPower()
    {
        int power =
            GetRealmPower();

        maxHP =
            100 + power * 40;

        currentHP =
            maxHP;

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
        if (realm ==
            CultivationRealm.Mortal)
            return "Người thường";

        if (realm ==
            CultivationRealm.QiRefining)
            return "Luyện Khí";

        if (realm ==
            CultivationRealm.Foundation)
            return "Trúc Cơ";

        if (realm ==
            CultivationRealm.GoldenCore)
            return "Kim Đan";

        if (realm ==
            CultivationRealm.NascentSoul)
            return "Nguyên Anh";

        if (realm ==
            CultivationRealm.SoulFormation)
            return "Hóa Thần";

        if (realm ==
            CultivationRealm.Tribulation)
            return "Độ Kiếp";

        return "Không rõ";
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
        currentAction = "\u0110\u00e3 ch\u1ebft";

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

        Debug.Log(
            npcName +
            " \u0111\u00e3 ch\u1ebft.");

        Destroy(gameObject);
    }

}



