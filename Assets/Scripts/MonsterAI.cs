using UnityEngine;

public class MonsterAI : MonoBehaviour
{
    [Header("Thông tin")]
    public string monsterName =
        "Fire Dragon";

    [Header("Máu")]
    public int maxHP = 100;

    public int currentHP;

    [Header("Di chuyển")]
    public float moveSpeed = 2f;

    public float detectRange = 6f;

    public float attackRange = 1.5f;

    [Header("Tuần tra")]
    public float roamRadius = 5f;

    public float roamWaitTime = 2f;

    Vector2 spawnPosition;

    Vector2 roamTarget;

    float roamTimer;

    bool hasRoamTarget = false;

    [Header("Tấn công")]
    public float attackCooldown = 1.5f;

    float attackTimer;

    bool isAttacking = false;

    bool isDead = false;

    Animator animator;

    Transform player;

    Rigidbody2D rb;

    void Start()
    {
        currentHP = maxHP;

        animator =
            GetComponent<Animator>();

        rb =
            GetComponent<Rigidbody2D>();

        spawnPosition =
            transform.position;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player");

        if (playerObject != null)
        {
            player =
                playerObject.transform;
        }
    }

    void Update()
    {
        if (isDead)
        {
            return;
        }

        attackTimer -= Time.deltaTime;

        if (player == null)
        {
            Patrol();
            return;
        }

        float distanceToPlayer =
            Vector2.Distance(
                transform.position,
                player.position);

        float distanceFromHome =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (distanceToPlayer <= detectRange &&
            distanceFromHome <= roamRadius * 1.5f)
        {
            FollowPlayer(distanceToPlayer);
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        if (isAttacking)
        {
            return;
        }

        roamTimer -= Time.deltaTime;

        if (!hasRoamTarget ||
            roamTimer <= 0)
        {
            ChooseNewRoamPoint();
        }

        MoveTo(roamTarget);

        float distance =
            Vector2.Distance(
                transform.position,
                roamTarget);

        if (distance < 0.3f)
        {
            hasRoamTarget = false;

            rb.linearVelocity =
                Vector2.zero;
        }
    }

    void ChooseNewRoamPoint()
    {
        Vector2 randomPoint =
            Random.insideUnitCircle *
            roamRadius;

        roamTarget =
            spawnPosition +
            randomPoint;

        roamTimer =
            roamWaitTime;

        hasRoamTarget = true;
    }

    void FollowPlayer(float distance)
    {
        if (isAttacking)
        {
            rb.linearVelocity =
                Vector2.zero;

            return;
        }

        FaceTarget(player.position);

        if (distance > attackRange)
        {
            MoveTo(player.position);
        }
        else
        {
            rb.linearVelocity =
                Vector2.zero;

            if (attackTimer <= 0)
            {
                Attack();
            }
        }
    }

    void MoveTo(Vector2 target)
    {
        Vector2 direction =
            (target -
            (Vector2)transform.position)
            .normalized;

        rb.linearVelocity =
            direction *
            moveSpeed;

        FaceTarget(target);
    }

    void Attack()
    {
        attackTimer =
            attackCooldown;

        isAttacking = true;

        rb.linearVelocity =
            Vector2.zero;

        animator.SetTrigger(
            "attack");

        Invoke(
            nameof(EndAttack),
            0.8f);
    }

    void EndAttack()
    {
        isAttacking = false;
    }

    void FaceTarget(Vector2 target)
    {
        if (target.x <
            transform.position.x)
        {
            transform.localScale =
                new Vector3(-1, 1, 1);
        }
        else
        {
            transform.localScale =
                new Vector3(1, 1, 1);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        currentHP -= damage;

        animator.SetTrigger(
            "hurt");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;

        rb.linearVelocity =
            Vector2.zero;

        animator.SetBool(
            "isDead",
            true);

        Destroy(gameObject, 3f);
    }

    public int GetRealmPower()
    {
        return maxHP;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;

        Gizmos.DrawWireSphere(
            transform.position,
            detectRange);

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            Application.isPlaying
            ? spawnPosition
            : (Vector2)transform.position,
            roamRadius);
    }
}