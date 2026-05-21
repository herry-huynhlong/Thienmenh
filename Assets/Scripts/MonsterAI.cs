using UnityEngine;

public class MonsterAI : MonoBehaviour
{
    [Header("===== THÔNG TIN =====")]

    public string monsterName =
        "Yêu Thú";

    [Header("===== MÁU =====")]

    public int maxHP = 100;

    public int currentHP = 100;

    [Header("===== DAMAGE =====")]

    public int damage = 10;

    [Header("===== DI CHUYỂN =====")]

    public float moveSpeed = 2f;

    public float roamRadius = 3f;

    public float waitTime = 2f;

    [Header("===== PLAYER =====")]

    public float detectRange = 6f;

    public float attackRange = 1.5f;

    Transform player;

    [Header("===== TẤN CÔNG =====")]

    public float attackCooldown = 2f;

    float attackTimer;

    bool isAttacking = false;

    [Header("===== FIREBALL =====")]

    public GameObject fireballPrefab;

    public Transform firePoint;

    [Header("===== ANIMATION =====")]

    public bool useAnimation = true;

    Animator animator;

    Vector2 startPosition;

    Vector2 targetPosition;

    bool hasTarget = false;

    float waitTimer;

    bool isDead = false;

    void Start()
    {
        currentHP =
            maxHP;

        animator =
            GetComponent<Animator>();

        startPosition =
            transform.position;

        waitTimer =
            waitTime;

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

        if (player != null)
        {
            float distanceToPlayer =
                Vector2.Distance(
                    transform.position,
                    player.position);

            if (distanceToPlayer <= detectRange)
            {
                FollowPlayer(
                    distanceToPlayer);

                return;
            }
        }

        Patrol();
    }

    void Patrol()
    {
        if (!hasTarget)
        {
            waitTimer -= Time.deltaTime;

            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    false);
            }

            if (waitTimer <= 0)
            {
                ChooseNewPoint();
            }

            return;
        }

        Vector2 direction =
            targetPosition -
            (Vector2)transform.position;

        float distance =
            direction.magnitude;

        if (distance < 0.1f)
        {
            hasTarget = false;

            waitTimer =
                waitTime;

            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    false);
            }

            return;
        }

        direction =
            direction.normalized;

        transform.position +=
            (Vector3)(
            direction *
            moveSpeed *
            Time.deltaTime);

        if (animator != null &&
            useAnimation)
        {
            animator.SetBool(
                "isMoving",
                true);
        }

        FaceDirection(direction);
    }

    void FollowPlayer(float distance)
    {
        if (isAttacking)
        {
            return;
        }

        Vector2 direction =
            player.position -
            transform.position;

        FaceDirection(direction);

        if (distance > attackRange)
        {
            transform.position +=
                (Vector3)(
                direction.normalized *
                moveSpeed *
                Time.deltaTime);

            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    true);
            }
        }
        else
        {
            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    false);
            }

            if (attackTimer <= 0)
            {
                Attack();
            }
        }
    }

    void Attack()
    {
        attackTimer =
            attackCooldown;

        isAttacking = true;

        if (animator != null &&
            useAnimation)
        {
            animator.SetTrigger(
                "attack");
        }

        Invoke(
            nameof(EndAttack),
            1f);
    }

    void EndAttack()
    {
        isAttacking = false;
    }

    public void ShootFireball()
    {
        if (fireballPrefab == null)
        {
            return;
        }

        if (firePoint == null)
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        GameObject fireball =
            Instantiate(
                fireballPrefab,
                firePoint.position,
                Quaternion.identity);

        Vector2 direction =
            player.position -
            firePoint.position;

        Fireball fb =
            fireball.GetComponent<Fireball>();

        if (fb != null)
        {
            fb.SetDirection(
                direction);
        }
    }

    void ChooseNewPoint()
    {
        Vector2 randomPoint =
            Random.insideUnitCircle *
            roamRadius;

        targetPosition =
            startPosition +
            randomPoint;

        hasTarget = true;
    }

    void FaceDirection(Vector2 direction)
    {
        if (direction.x < 0)
        {
            transform.localScale =
                new Vector3(-1, 1, 1);
        }
        else if (direction.x > 0)
        {
            transform.localScale =
                new Vector3(1, 1, 1);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead)
        {
            return;
        }

        currentHP -= damageAmount;

        if (animator != null &&
            useAnimation)
        {
            animator.SetTrigger(
                "hurt");
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;

        if (animator != null &&
            useAnimation)
        {
            animator.SetTrigger(
                "die");
        }

        Destroy(gameObject, 2f);
    }

    public int GetRealmPower()
    {
        return maxHP;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color =
            Color.red;

        Gizmos.DrawWireSphere(
            Application.isPlaying
            ? startPosition
            : (Vector2)transform.position,
            roamRadius);

        Gizmos.color =
            Color.white;

        Gizmos.DrawWireSphere(
            transform.position,
            detectRange);
    }
}