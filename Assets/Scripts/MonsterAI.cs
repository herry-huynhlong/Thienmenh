using UnityEngine;

public class MonsterAI : MonoBehaviour
{
    [Header("Thông tin quái")]
    public string monsterName = "Yêu Thú";

    [Header("Cảnh giới")]
    public CultivationRealm realm = CultivationRealm.QiRefining;

    [Range(1, 9)]
    public int realmStage = 1;

    [Header("Máu")]
    public int maxHP = 100;
    public int currentHP = 100;

    [Header("Chiến đấu")]
    public int attack = 10;
    public int defense = 5;

    [Header("AI")]
    public bool aggressive = true;

    public float moveSpeed = 2f;

    public float attackRange = 1.5f;

    public float attackCooldown = 1.2f;

    public float maxRoamDistance = 8f;

    public float chaseDistance = 6f;

    private float attackTimer = 0;

    private Rigidbody2D rb;

    private SmartNpcAI currentNpcTarget;

    private Vector3 spawnPosition;

    [Header("Rơi vật phẩm")]
    public int moneyDrop = 10;

    public int spiritStoneDrop = 1;

    [Range(0f, 1f)]
    public float pillDropChance = 0.2f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        spawnPosition = transform.position;

        ApplyRealmPower();
    }

    void Update()
    {
        attackTimer += Time.deltaTime;

        SearchNpc();

        TryAttackNpc();
    }

    void FixedUpdate()
    {
        MoveToTarget();
    }

    void MoveToTarget()
    {
        if (currentNpcTarget == null)
        {
            ReturnToSpawn();

            return;
        }

        float distanceFromSpawn =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (distanceFromSpawn > maxRoamDistance)
        {
            currentNpcTarget = null;

            ReturnToSpawn();

            return;
        }

        Vector2 direction =
            (currentNpcTarget.transform.position -
            transform.position).normalized;

        rb.linearVelocity =
            direction * moveSpeed;
    }

    void ReturnToSpawn()
    {
        float distance =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (distance < 0.5f)
        {
            rb.linearVelocity = Vector2.zero;

            return;
        }

        Vector2 direction =
            (spawnPosition - transform.position).normalized;

        rb.linearVelocity =
            direction * moveSpeed;
    }

    void SearchNpc()
    {
        SmartNpcAI[] npcs =
            FindObjectsOfType<SmartNpcAI>();

        float closestDistance =
            Mathf.Infinity;

        SmartNpcAI bestTarget = null;

        foreach (SmartNpcAI npc in npcs)
        {
            if (npc.currentHP <= 0)
            {
                continue;
            }

            float distanceFromSpawn =
                Vector2.Distance(
                    spawnPosition,
                    npc.transform.position);

            if (distanceFromSpawn > chaseDistance)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    transform.position,
                    npc.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;

                bestTarget = npc;
            }
        }

        currentNpcTarget = bestTarget;
    }

    void TryAttackNpc()
    {
        if (currentNpcTarget == null)
        {
            return;
        }

        float distance =
            Vector2.Distance(
                transform.position,
                currentNpcTarget.transform.position);

        if (distance > attackRange)
        {
            return;
        }

        if (attackTimer < attackCooldown)
        {
            return;
        }

        attackTimer = 0;

        currentNpcTarget.TakeDamage(attack);

        Debug.Log(
            monsterName +
            " tấn công " +
            currentNpcTarget.npcName);
    }

    public int GetRealmPower()
    {
        return ((int)realm * 10) + realmStage;
    }

    void ApplyRealmPower()
    {
        int power = GetRealmPower();

        maxHP = 80 + power * 45;

        currentHP = maxHP;

        attack = 8 + power * 9;

        defense = 4 + power * 5;
    }

    public void TakeDamage(int damage)
    {
        int finalDamage = damage - defense;

        if (finalDamage < 1)
        {
            finalDamage = 1;
        }

        currentHP -= finalDamage;

        Debug.Log(
            monsterName +
            " nhận " +
            finalDamage +
            " sát thương.");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log(monsterName + " đã chết.");

        DropReward();

        Destroy(gameObject);
    }

    void DropReward()
    {
        Debug.Log(monsterName + " rơi " + moneyDrop + " tiền.");

        int random = Random.Range(0, 100);

        if (random <= pillDropChance * 100)
        {
            Debug.Log(monsterName + " rơi đan dược.");
        }
    }
}