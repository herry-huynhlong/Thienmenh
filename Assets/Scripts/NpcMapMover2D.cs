using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NpcMapMover2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    public float movementAcceleration = 8f;
    public float movementDeceleration = 12f;
    public float arriveDistance = 0.12f;
    public float waitTimeMin = 5f;
    public float waitTimeMax = 5f;
    public float wanderRadius = 5f;
    public bool onlyPickNewTargetAfterArrive = true;
    public float blockedRetryWait = 1f;

    [Header("Area")]
    public Transform centerPoint;
    public bool useSpawnAsCenter = true;
    public bool keepInsideWanderRadius = true;
    public float returnInsidePadding = 0.5f;
    public Collider2D mapBounds;
    public bool keepInsideMapBounds = true;
    public bool useMapBoundsCenterWhenAvailable = true;
    public float mapBoundsEdgePadding = 0.25f;

    [Header("Obstacle Check")]
    public LayerMask obstacleLayers = ~0;
    public float obstacleCheckDistance = 0.35f;
    public float targetClearRadius = 0.25f;
    public int maxPickTargetAttempts = 16;
    public float stuckTimeToPickNewTarget = 0.8f;

    [Header("Crowd Avoidance")]
    public LayerMask crowdLayers = ~0;
    public float separationRadius = 0.45f;
    public float separationStrength = 1.4f;

    [Header("Runtime")]
    public Vector2 currentTarget;
    public string currentAction = "Idle";
    public Vector2 currentVelocity;

    Rigidbody2D rb;
    NPCVisualAnimation visualAnimation;
    Collider2D[] selfColliders;
    Vector2 centerPosition;
    Vector2 lastPosition;
    float waitTimer;
    float stuckTimer;
    float blockedTimer;
    bool waitingAfterArrive;
    bool hasTarget;

    void Awake()
    {
        VillagerAI villagerAI = GetComponent<VillagerAI>();
        if (villagerAI != null && villagerAI.enabled)
        {
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            enabled = false;
            return;
        }

        visualAnimation = GetComponent<NPCVisualAnimation>();
        selfColliders = GetComponentsInChildren<Collider2D>();
        ConfigureRigidbody();

        centerPosition = GetInitialCenterPosition();

        lastPosition = transform.position;

        if (!IsInsideAllowedArea(rb.position))
        {
            rb.position = ClampToAllowedArea(rb.position);
            transform.position = rb.position;
        }

        PickNewTarget();
    }

    Vector2 GetInitialCenterPosition()
    {
        if (centerPoint != null)
        {
            return ClampToAllowedArea(centerPoint.position);
        }

        if (useMapBoundsCenterWhenAvailable &&
            mapBounds != null)
        {
            return mapBounds.bounds.center;
        }

        if (useSpawnAsCenter)
        {
            return ClampToAllowedArea(transform.position);
        }

        return ClampToAllowedArea(transform.position);
    }

    void Update()
    {
        if (rb == null)
        {
            enabled = false;
            return;
        }

        if (!hasTarget)
        {
            currentAction = "Waiting";
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                waitingAfterArrive = false;
                PickNewTarget();
            }

            return;
        }

        Vector2 position = rb.position;

        if (!IsInsideAllowedArea(position))
        {
            ReturnInsideAllowedArea(position);
            return;
        }

        Vector2 toTarget = currentTarget - position;

        if (toTarget.magnitude <= arriveDistance)
        {
            StopAndWait(true);
            return;
        }

        Vector2 direction = toTarget.normalized;
        if (IsBlocked(direction))
        {
            currentAction = "Blocked";
            blockedTimer += Time.deltaTime;
            currentVelocity = Vector2.zero;

            if (onlyPickNewTargetAfterArrive)
            {
                if (blockedTimer >= stuckTimeToPickNewTarget)
                {
                    StopAndWait(false, blockedRetryWait);
                }

                return;
            }

            StopAndWait(false, blockedRetryWait);
            return;
        }

        blockedTimer = 0f;
        direction = ApplyCrowdAvoidance(direction);
        currentVelocity = direction * moveSpeed;
        currentAction = "Moving";
        UpdateVisualAnimation();
        DetectStuck();
    }

    void FixedUpdate()
    {
        if (rb == null)
        {
            enabled = false;
            return;
        }

        float rate =
            currentVelocity.sqrMagnitude > rb.linearVelocity.sqrMagnitude
            ? movementAcceleration
            : movementDeceleration;

        rb.linearVelocity =
            Vector2.MoveTowards(
                rb.linearVelocity,
                currentVelocity,
                rate * Time.fixedDeltaTime);

        UpdateVisualAnimation();
    }

    public void PickNewTarget()
    {
        for (int i = 0; i < maxPickTargetAttempts; i++)
        {
            Vector2 randomOffset =
                Random.insideUnitCircle *
                Mathf.Max(0.1f, wanderRadius);

            Vector2 candidate =
                ClampToAllowedArea(
                    centerPosition +
                    randomOffset);

            if (IsInsideAllowedArea(candidate) &&
                !IsPositionBlocked(candidate))
            {
                currentTarget = candidate;
                hasTarget = true;
                stuckTimer = 0f;
                blockedTimer = 0f;
                currentAction = "New Target";
                return;
            }
        }

        StopAndWait(false, blockedRetryWait);
        currentAction = "No Clear Target";
    }

    void StopAndWait()
    {
        StopAndWait(true);
    }

    void StopAndWait(bool arrived)
    {
        float time =
            arrived
            ? Random.Range(waitTimeMin, waitTimeMax)
            : blockedRetryWait;

        StopAndWait(arrived, time);
    }

    void StopAndWait(bool arrived, float time)
    {
        hasTarget = false;
        currentVelocity = Vector2.zero;
        waitTimer = Mathf.Max(0f, time);
        waitingAfterArrive = arrived;
        blockedTimer = 0f;
        UpdateVisualAnimation();

        currentAction =
            arrived
            ? "Arrived Wait"
            : "Retry Wait";
    }

    bool IsBlocked(Vector2 direction)
    {
        RaycastHit2D hit =
            Physics2D.Raycast(
                rb.position,
                direction,
                obstacleCheckDistance,
                obstacleLayers);

        return hit.collider != null &&
            !hit.collider.isTrigger &&
            !IsSelfCollider(hit.collider);
    }

    bool IsPositionBlocked(Vector2 position)
    {
        Collider2D hit =
            Physics2D.OverlapCircle(
                position,
                targetClearRadius,
                obstacleLayers);

        return hit != null &&
            !hit.isTrigger &&
            !IsSelfCollider(hit);
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

        for (int i = 0; i < selfColliders.Length; i++)
        {
            if (selfColliders[i] == hit)
            {
                return true;
            }
        }

        return false;
    }

    Vector2 ApplyCrowdAvoidance(Vector2 direction)
    {
        if (separationRadius <= 0f)
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
                IsSelfCollider(hit))
            {
                continue;
            }

            if (hit.GetComponentInParent<NpcMapMover2D>() == null &&
                hit.GetComponentInParent<VillagerAI>() == null &&
                hit.GetComponentInParent<SmartNpcAI>() == null)
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

    void DetectStuck()
    {
        float movedDistance =
            Vector2.Distance(
                rb.position,
                lastPosition);

        if (movedDistance < 0.005f)
        {
            stuckTimer += Time.deltaTime;
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = rb.position;

        if (stuckTimer >= stuckTimeToPickNewTarget)
        {
            if (onlyPickNewTargetAfterArrive)
            {
                currentVelocity = Vector2.zero;
                currentAction = "Stuck";
                return;
            }

            StopAndWait(false, blockedRetryWait);
        }
    }

    bool IsInsideAllowedArea(Vector2 position)
    {
        if (keepInsideMapBounds &&
            mapBounds != null &&
            !IsInsideMapBounds(position))
        {
            return false;
        }

        if (keepInsideWanderRadius)
        {
            float maxDistance =
                Mathf.Max(0.1f, wanderRadius + returnInsidePadding);

            if (Vector2.Distance(position, centerPosition) > maxDistance)
            {
                return false;
            }
        }

        return true;
    }

    void ReturnInsideAllowedArea(Vector2 position)
    {
        Vector2 target =
            ClampToAllowedArea(centerPosition);

        Vector2 direction =
            (target - position).normalized;

        if (direction.sqrMagnitude < 0.0001f)
        {
            rb.position = target;
            transform.position = target;
            currentVelocity = Vector2.zero;
            currentAction = "Clamp Inside";
            StopAndWait(false, blockedRetryWait);
            return;
        }

        currentVelocity =
            direction *
            moveSpeed *
            1.5f;

        currentAction = "Return Inside";
        hasTarget = false;
    }

    Vector2 ClampToAllowedArea(Vector2 position)
    {
        Vector2 result = position;

        if (keepInsideMapBounds &&
            mapBounds != null)
        {
            Bounds bounds = mapBounds.bounds;
            float padding = Mathf.Max(0f, mapBoundsEdgePadding);

            result.x =
                Mathf.Clamp(
                    result.x,
                    bounds.min.x + padding,
                    bounds.max.x - padding);

            result.y =
                Mathf.Clamp(
                    result.y,
                    bounds.min.y + padding,
                    bounds.max.y - padding);
        }

        if (keepInsideWanderRadius)
        {
            Vector2 fromCenter = result - centerPosition;
            float maxDistance =
                Mathf.Max(0.1f, wanderRadius);

            if (fromCenter.magnitude > maxDistance)
            {
                result =
                    centerPosition +
                    fromCenter.normalized *
                    maxDistance;
            }
        }

        return result;
    }

    bool IsInsideMapBounds(Vector2 position)
    {
        if (mapBounds == null)
        {
            return true;
        }

        Bounds bounds = mapBounds.bounds;
        float padding = Mathf.Max(0f, mapBoundsEdgePadding);

        return position.x >= bounds.min.x + padding &&
            position.x <= bounds.max.x - padding &&
            position.y >= bounds.min.y + padding &&
            position.y <= bounds.max.y - padding;
    }

    void ConfigureRigidbody()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if ((rb.constraints & RigidbodyConstraints2D.FreezePositionX) != 0 ||
            (rb.constraints & RigidbodyConstraints2D.FreezePositionY) != 0)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void UpdateVisualAnimation()
    {
        if (visualAnimation == null)
        {
            return;
        }

        bool isIdle = currentVelocity.sqrMagnitude <= 0.0001f;
        Vector2 direction = isIdle ? Vector2.zero : currentVelocity.normalized;
        visualAnimation.UpdateNPCAnimation(direction, isIdle);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center =
            centerPoint != null
            ? centerPoint.position
            : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, wanderRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(currentTarget, 0.15f);
    }
}
