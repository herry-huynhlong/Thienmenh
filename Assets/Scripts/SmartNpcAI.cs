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

    [Header("Tu luyện")]
    public int cultivation = 0;

    public int breakthroughNeed = 100;

    public bool readyForHeavenlyTribulation = false;

    [Header("Tài sản")]
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

    private MonsterAI currentMonsterTarget;

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

    public bool IsDead => currentHP <= 0;

    public Transform DamageTransform => transform;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        spawnPosition = transform.position;

        ApplyRealmPower();
    }

    void Update()
    {
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
        UpdateMovement();
    }

    void UpdateMovement()
    {
        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;

            return;
        }

        float distanceFromSpawn =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (distanceFromSpawn > maxRoamDistance)
        {
            ReturnToSpawn();

            return;
        }

        Vector2 direction =
            (currentTarget.position -
            transform.position).normalized;

        rb.linearVelocity =
            direction * moveSpeed;
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

    void Think()
    {
        if (currentHP <= 0)
        {
            Die();

            return;
        }

        if (canLive && hunger >= 80)
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

        if (canTrade &&
            money >= 50 &&
            pill <= 0)
        {
            GoToTavernAndBuyPill();

            return;
        }

        if (canFight)
        {
            SearchMonster();

            return;
        }

        if (canMakeFriends)
        {
            MakeFriend();

            return;
        }

        if (canCreateSect)
        {
            TryCreateSect();

            return;
        }

        currentAction = "Không có việc làm";
    }

    void UpdateNeeds()
    {
        hunger += Time.deltaTime * 0.05f;

        fatigue += Time.deltaTime * 0.04f;
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

            currentHP += 30;

            if (currentHP > maxHP)
            {
                currentHP = maxHP;
            }

            Debug.Log(
                npcName + " đang nghỉ ngơi.");
        }
    }

    void Cultivate()
    {
        currentAction = "Tu luyện";

        if (pill > 0)
        {
            pill -= 1;

            int gain =
                Mathf.RoundToInt(
                    30 *
                    GetCultivationMultiplier());

            cultivation += gain;

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
                    20 *
                    GetCultivationMultiplier());

            cultivation += gain;

            Debug.Log(
                npcName +
                " hấp thụ linh thạch tăng " +
                gain +
                " tu vi.");
        }

        if (cultivation >= breakthroughNeed)
        {
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

        return multiplier;
    }

    void Breakthrough()
    {
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
        return ((int)realm * 10)
            + realmStage;
    }

    public void ApplyItem(StatItemData item)
    {
        ApplyItem(item, 1);
    }

    public void ApplyItem(StatItemData item, int direction)
    {
        if (item == null)
        {
            return;
        }

        foreach (StatModifier modifier in item.GetAllModifiers())
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
                cultivation += intValue;
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
            100 + power * 120;
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

    void Die()
    {
        currentAction =
            "Đã chết";

        Debug.Log(
            npcName +
            " đã chết.");

        Destroy(gameObject);
    }
}
