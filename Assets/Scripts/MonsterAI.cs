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
    // Tốc độ di chuyển

    public float roamRadius = 3f;
    // Bán kính đi quanh

    public float waitTime = 2f;
    // Thời gian đứng nghỉ

    [Header("===== ANIMATION =====")]

    public bool useAnimation = true;

    Animator animator;

    Vector2 startPosition;
    // Vị trí spawn ban đầu

    Vector2 targetPosition;
    // Điểm sẽ đi tới

    bool hasTarget = false;

    float waitTimer;

    void Start()
    {
        // Máu hiện tại
        currentHP =
            maxHP;

        // Animator
        animator =
            GetComponent<Animator>();

        // Lưu vị trí spawn
        startPosition =
            transform.position;

        waitTimer =
            waitTime;
    }

    void Update()
    {
        Patrol();
    }

    void Patrol()
    {
        // Nếu chưa có điểm đi
        if (!hasTarget)
        {
            waitTimer -= Time.deltaTime;

            // Idle animation
            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    false);
            }

            // Hết thời gian nghỉ
            if (waitTimer <= 0)
            {
                ChooseNewPoint();
            }

            return;
        }

        // Tính hướng tới điểm
        Vector2 direction =
            targetPosition -
            (Vector2)transform.position;

        float distance =
            direction.magnitude;

        // Nếu tới nơi
        if (distance < 0.1f)
        {
            hasTarget = false;

            waitTimer =
                waitTime;

            // Idle animation
            if (animator != null &&
                useAnimation)
            {
                animator.SetBool(
                    "isMoving",
                    false);
            }

            return;
        }

        // Chuẩn hóa hướng
        direction =
            direction.normalized;

        // Di chuyển
        transform.position +=
            (Vector3)(
            direction *
            moveSpeed *
            Time.deltaTime);

        // Walk animation
        if (animator != null &&
            useAnimation)
        {
            animator.SetBool(
                "isMoving",
                true);
        }

        // Quay mặt
        FaceDirection(direction);
    }

    void ChooseNewPoint()
    {
        // Random điểm trong vòng tròn
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
        // Quay trái phải
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

    // Bị đánh
    public void TakeDamage(int damageAmount)
    {
        currentHP -= damageAmount;

        // Animation hurt
        if (animator != null &&
            useAnimation)
        {
            animator.SetTrigger(
                "hurt");
        }

        // Chết
        if (currentHP <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // Animation chết
        if (animator != null &&
            useAnimation)
        {
            animator.SetTrigger(
                "die");
        }

        Destroy(gameObject, 2f);
    }

    // SmartNpcAI dùng
    public int GetRealmPower()
    {
        return maxHP;
    }

    // Vẽ vòng đỏ trong editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color =
            Color.red;

        Gizmos.DrawWireSphere(
            Application.isPlaying
            ? startPosition
            : (Vector2)transform.position,
            roamRadius);
    }
}