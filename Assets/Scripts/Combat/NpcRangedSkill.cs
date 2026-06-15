using UnityEngine;

public class NpcRangedSkill : MonoBehaviour
{
    [Header("References")]
    public GameObject fireballPrefab;
    public Transform firePoint;
    public Animator animator;

    [Header("Target")]
    public LayerMask targetLayers;
    public float detectRange = 6f;
    public float attackRange = 5f;
    public Transform currentTarget;

    [Header("Skill")]
    public int damage = 20;
    public float cooldown = 1.5f;
    public bool useAnimation = false;
    public string attackTriggerName = "attack";
    public bool faceTarget = true;

    private float nextAttackTime;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        FindNearestTarget();

        if (currentTarget == null) return;

        float distance = Vector2.Distance(transform.position, currentTarget.position);
        if (distance <= attackRange)
        {
            TryCastSkill();
        }
    }

    private void FindNearestTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRange, targetLayers);

        float nearestDistance = Mathf.Infinity;
        Transform nearest = null;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.transform.IsChildOf(transform)) continue;

            float d = Vector2.Distance(transform.position, hit.transform.position);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearest = hit.transform;
            }
        }

        currentTarget = nearest;
    }

    private void TryCastSkill()
    {
        if (Time.time < nextAttackTime) return;
        if (fireballPrefab == null || firePoint == null || currentTarget == null) return;

        nextAttackTime = Time.time + cooldown;

        if (faceTarget)
        {
            Vector2 dir = currentTarget.position - transform.position;

            Vector3 scale = transform.localScale;
            if (dir.x > 0.05f) scale.x = Mathf.Abs(scale.x);
            else if (dir.x < -0.05f) scale.x = -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        if (useAnimation && animator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            animator.SetTrigger(attackTriggerName);
            // nếu muốn bắn đúng lúc animation ra tay thì dùng Animation Event gọi CastNow()
            CastNow();
        }
        else
        {
            CastNow();
        }
    }

    public void CastNow()
    {
        if (fireballPrefab == null || firePoint == null || currentTarget == null) return;

        Vector2 direction = ((Vector2)currentTarget.position - (Vector2)firePoint.position).normalized;

        GameObject obj = Instantiate(fireballPrefab, firePoint.position, Quaternion.identity);
        RangedProjectile2D projectile = obj.GetComponent<RangedProjectile2D>();

        if (projectile != null)
        {
            projectile.Init(direction, gameObject, damage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}