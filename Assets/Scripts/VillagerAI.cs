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
    Healer
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
    public int cultivationExp;
    public int baseExpToNextRealm = 100;
    public int baseMaxHP = 100;
    public int baseAttack = 5;
    public int baseDefense = 2;
    public int attack = 5;
    public int defense = 2;

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

    [Header("Runtime")]
    public string currentAction = "Dung yen";
    public Transform currentTarget;

    Rigidbody2D rb;
    Vector3 spawnPosition;
    Vector3 wanderTarget;
    float thinkTimer;
    float actionTimer;
    float nextSocialScanTime;
    bool hasWanderTarget;
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
        characterStats = GetComponent<CharacterStats>();
        spawnPosition = transform.position;

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
        job = GetGeneratedJob(entityProfile.personality);
        realm = entityProfile.stats.realm;
        realmStage = entityProfile.stats.realmStage;
        cultivationExp = entityProfile.stats.cultivationExp;
        baseMaxHP = Mathf.Max(1, entityProfile.stats.maxHP);
        baseAttack = Mathf.Max(1, entityProfile.stats.attack);
        baseDefense = Mathf.Max(0, entityProfile.stats.defense);
        maxHP = entityProfile.stats.maxHP;
        currentHP = entityProfile.stats.currentHP;
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
        hunger = Mathf.Clamp(
            hunger + Time.deltaTime * 0.35f,
            0f,
            100f);

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
        else if (hunger >= 80f)
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
        if (actionTimer > 0f)
        {
            return;
        }

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return;
        }

        if (hunger >= 75f)
        {
            GoEat();
            return;
        }

        if (ageGroup == VillagerAgeGroup.Child)
        {
            ThinkChild();
            return;
        }

        ThinkAdult();
    }

    void ThinkChild()
    {
        if (fun <= 70f)
        {
            GatherAndPlay();
            return;
        }

        if (ShouldTalk())
        {
            TalkToNearbyVillager();
            return;
        }

        Wander("Di dao choi");
    }

    void ThinkAdult()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            switch (timeSystem.CurrentPhase)
            {
                case WorldTimePhase.Dawn:
                    if (fatigue > 45f)
                    {
                        GoHomeToRest();
                        return;
                    }
                    break;

                case WorldTimePhase.Morning:
                    if (diligence >= 25)
                    {
                        GoWork();
                        return;
                    }
                    break;

                case WorldTimePhase.Noon:
                    GoEat();
                    return;

                case WorldTimePhase.Afternoon:
                    if (job == VillagerJob.Trader || Random.Range(0, 100) < sociability + greed)
                    {
                        GoTrade();
                        return;
                    }
                    break;

                case WorldTimePhase.Evening:
                    int funSeeking = entityProfile != null ? entityProfile.personality.funSeeking : 0;
                    if (fun < 70f || sociability + funSeeking > 80)
                    {
                        GatherAndPlay();
                        return;
                    }
                    break;

                case WorldTimePhase.Night:
                    if (bravery < 75)
                    {
                        GoHomeToRest();
                        return;
                    }
                    break;
            }
        }

        if (ShouldTalk())
        {
            TalkToNearbyVillager();
            return;
        }

        if (job == VillagerJob.Trader)
        {
            GoTrade();
            return;
        }

        if (diligence >= 30)
        {
            GoWork();
            return;
        }

        Wander("Nghi ngoi quanh lang");
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

    void TryTalkToPassingVillager()
    {
        if (Time.time < nextSocialScanTime ||
            actionTimer > 0f ||
            IsDead)
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
        SetTarget(
            homePoint,
            "Ve nha nghi ngoi");

        if (HasArrived())
        {
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
            actionTimer = 3f;
            currentAction = "Dang nghi ngoi";
        }
    }

    void GoEat()
    {
        SetTarget(
            marketPoint != null ? marketPoint : homePoint,
            "Di an");

        if (HasArrived())
        {
            hunger = 0f;
            money = Mathf.Max(0, money - 1);
            actionTimer = 2f;
            currentAction = "Dang an";
        }
    }

    void GatherAndPlay()
    {
        SetTarget(
            playPoint,
            "Tu tap di choi");

        if (HasArrived())
        {
            fun = 100f;
            actionTimer = 3f;
            currentAction = "Dang choi cung ban";
            TalkToNearbyVillager();
        }
    }

    void GoWork()
    {
        SetTarget(
            workPoint,
            GetWorkAction());

        if (HasArrived())
        {
            money += GetWorkIncome();
            fatigue = Mathf.Clamp(fatigue + 8f, 0f, 100f);
            actionTimer = 3f;
            currentAction = GetWorkingAction();
        }
    }

    void GoTrade()
    {
        SetTarget(
            marketPoint,
            "Ra cho buon ban");

        if (HasArrived())
        {
            money += Random.Range(1, 4);
            actionTimer = 3f;
            currentAction = "Dang buon ban";
            TalkToNearbyVillager();
        }
    }

    void TalkToNearbyVillager()
    {
        VillagerAI other =
            FindNearbyVillager();

        if (other == null)
        {
            Wander("Tim nguoi noi chuyen");
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
        currentAction = action;
        MoveToPosition(wanderTarget);
    }

    void SetTarget(Transform target, string action)
    {
        if (target == null)
        {
            Wander(action);
            return;
        }

        hasWanderTarget = false;
        currentTarget = target;
        currentAction = action;
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

    void MoveToPosition(Vector3 position)
    {
        Vector2 direction =
            (position - transform.position).normalized;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            StopMoving();
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity =
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
            rb.linearVelocity = Vector2.zero;
        }
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
                return 1;
            default:
                return 0;
        }
    }

    public int ExpToNextRealm()
    {
        if (characterStats != null)
        {
            return characterStats.ExpToNextRealm();
        }

        int realmIndex =
            Mathf.Max(0, (int)realm);

        int result =
            Mathf.Max(1, baseExpToNextRealm);

        for (int i = 0; i < realmIndex; i++)
        {
            result *= 10;
        }

        return result;
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

        realm =
            (CultivationRealm)((int)realm + 1);
        realmStage = 1;
        ApplyRealmPower();
        currentHP = maxHP;
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

        for (int i = 0; i < realmIndex; i++)
        {
            multiplier *= 10;
        }

        return multiplier;
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
        if (characterStats != null)
        {
            characterStats.ApplyItem(item, direction);
            SyncFromCharacterStats();
            return;
        }

        if (item == null)
        {
            return;
        }

        foreach (StatModifier modifier in item.GetAllModifiers())
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

        if (destroyOnDeath)
        {
            Destroy(gameObject, 2f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, talkRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);
    }
}
