using UnityEngine;

[DisallowMultipleComponent]
public class BicanhBoneMonsterAI : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Rigidbody2D rb;

    [Header("Animation State Names")]
    public string flyRightState = "yeuphuong_walk_right";
    public string attackRightState = "yeuphuong_attack_right";

    [Header("Visual Attack Detect")]
    public float visualAttackRange = 1.6f;
    public float visualDetectRadius = 5f;
    public LayerMask targetLayers;
    public string[] targetTags = { "NPC", "Player" };

    [Header("Visual Timing")]
    public float attackVisualCooldown = 1.2f;

    [Header("Visual")]
    public bool spriteFacesRightByDefault = true;

    Transform target;
    string currentState = "";
    float nextAttackVisualTime;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
    }

    void OnEnable()
    {
        target = null;
        currentState = "";
        nextAttackVisualTime = 0f;
        PlayState(flyRightState, true);
    }

    void Update()
    {
        FindTargetForVisual();
        UpdateFlip();
        UpdateAnimation();
    }

    void FindTargetForVisual()
    {
        if (target != null)
        {
            float oldDistance = Vector2.Distance(transform.position, target.position);

            if (oldDistance <= visualDetectRadius * 1.5f)
            {
                return;
            }

            target = null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visualDetectRadius, targetLayers);

        float nearestDistance = float.MaxValue;
        Transform nearestTarget = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];

            if (hit == null)
            {
                continue;
            }

            if (!IsValidTarget(hit.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, hit.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = hit.transform;
            }
        }

        target = nearestTarget;
    }

    bool IsValidTarget(GameObject obj)
    {
        for (int i = 0; i < targetTags.Length; i++)
        {
            if (obj.CompareTag(targetTags[i]))
            {
                return true;
            }
        }

        return false;
    }

    void UpdateFlip()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float directionX = 0f;

        if (target != null)
        {
            directionX = target.position.x - transform.position.x;
        }
        else if (rb != null)
        {
            directionX = rb.linearVelocity.x;
        }

        if (Mathf.Abs(directionX) < 0.05f)
        {
            return;
        }

        bool faceRight = directionX > 0f;

        if (spriteFacesRightByDefault)
        {
            spriteRenderer.flipX = !faceRight;
        }
        else
        {
            spriteRenderer.flipX = faceRight;
        }
    }

    void UpdateAnimation()
    {
        if (target == null)
        {
            PlayState(flyRightState, false);
            return;
        }

        float distance = Vector2.Distance(transform.position, target.position);

        if (distance <= visualAttackRange)
        {
            if (Time.time >= nextAttackVisualTime)
            {
                nextAttackVisualTime = Time.time + attackVisualCooldown;
                PlayState(attackRightState, true);
            }

            return;
        }

        PlayState(flyRightState, false);
    }

    void PlayState(string stateName, bool restart)
    {
        if (animator == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        if (!restart && currentState == stateName)
        {
            return;
        }

        currentState = stateName;
        animator.Play(stateName, 0, 0f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void PlayAttackVisual()
    {
        nextAttackVisualTime = Time.time + attackVisualCooldown;
        PlayState(attackRightState, true);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visualDetectRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, visualAttackRange);
    }
}