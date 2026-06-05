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
    public bool useKinematicNpcMovement = true;
    public bool onlyPickNewTargetAfterArrive = true;
    public float blockedRetryWait = 1f;

    [Header("Area")]
    public Transform centerPoint;
    public bool useSpawnAsCenter = true;
    public bool keepInsideWanderRadius = true;
    public float returnInsidePadding = 0.5f;
    public Collider2D mapBounds;
    public bool keepInsideMapBounds = true;
    public bool allowCrossMapAreas = true;
    public bool keepInsideCombinedMapAreas;
    public bool autoResolveMapArea = true;
    public bool useNearestAreaWhenOutsideBounds;
    public bool clampInitialPositionToBounds;
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
    public float crowdLookAheadDistance = 0.65f;
    public float crowdDetourDistance = 0.55f;
    public float crowdYieldDuration = 0.2f;

    [Header("Runtime")]
    public Vector2 currentTarget;
    public string currentAction = "Idle";
    public Vector2 currentVelocity;

    Rigidbody2D rb;
    NPCVisualAnimation visualAnimation;
    Collider2D[] selfColliders;
    NpcMapArea currentMapArea;
    Vector2 centerPosition;
    Vector2 lastPosition;
    float waitTimer;
    float stuckTimer;
    float blockedTimer;
    float crowdBlockedTimer;
    float movementPausedUntil;
    float crowdYieldUntil;
    bool waitingAfterArrive;
    bool hasTarget;
    bool currentTargetIgnoresAllowedArea;

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
        AutoResolveMapBounds();

        centerPosition = GetInitialCenterPosition();

        lastPosition = transform.position;

        if (clampInitialPositionToBounds &&
            !IsInsideAllowedArea(rb.position))
        {
            rb.position = ClampToAllowedArea(rb.position);
            transform.position = rb.position;
        }

        PickNewTarget();
    }
    void AutoResolveMapBounds()
    {
        if (!autoResolveMapArea)
        {
            return;
        }

        NpcMapArea area =
            NpcMapArea.FindArea(transform.position);

        if (!allowCrossMapAreas &&
            area == null &&
            useNearestAreaWhenOutsideBounds)
        {
            area = NpcMapArea.FindNearestArea(transform.position);
        }

        ApplyMapArea(area);
    }

    void ApplyMapArea(NpcMapArea area)
    {
        if (area == null ||
            area.areaBounds == null ||
            area == currentMapArea)
        {
            return;
        }

        currentMapArea = area;
        mapBounds = area.areaBounds;
        centerPosition = GetInitialCenterPosition();
        if (!currentTargetIgnoresAllowedArea)
        {
            currentTarget = ClampToAllowedArea(currentTarget);
        }
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;

        Vector3 referencePosition = gate != null
            ? gate.ExitPosition
            : transform.position;

        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindArea(referencePosition);
        }

        if (gate != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
        }
        else if (area != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
        }

        currentMapArea = null;
        ApplyMapArea(area);
        hasTarget = false;
        waitingAfterArrive = false;
        currentTargetIgnoresAllowedArea = false;
        waitTimer = Mathf.Max(waitTimer, 0.15f);
        currentVelocity = Vector2.zero;
        blockedTimer = 0f;
        crowdBlockedTimer = 0f;
        currentAction = "Teleported";

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }


    Vector2 GetInitialCenterPosition()
    {
        if (centerPoint != null &&
            IsPointInsideMapBounds(centerPoint.position))
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
        AutoResolveMapBounds();

        if (Time.time < movementPausedUntil)
        {
            currentVelocity = Vector2.zero;
            currentAction = "Talking";
            UpdateVisualAnimation();
            return;
        }

        if (Time.time < crowdYieldUntil)
        {
            StopRigidbodyMotion();
            currentAction = "Yielding";
            UpdateVisualAnimation();
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
        if (!TryResolveCrowdAhead(direction, out direction))
        {
            return;
        }

        if (IsBlocked(direction))
        {
            if (TryChooseObstacleDetourDirection(direction, out direction))
            {
                blockedTimer = 0f;
                direction = ApplyCrowdAvoidance(direction);
                currentVelocity = direction * moveSpeed;
                currentAction = "Detour";
                UpdateVisualAnimation();
                DetectStuck();
                return;
            }

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

    public void SetMoveTarget(Vector2 target, string action = "Move Target")
    {
        SetMoveTarget(target, action, false);
    }

    public void SetMoveTarget(
        Vector2 target,
        string action,
        bool ignoreAllowedArea)
    {
        AutoResolveMapBounds();
        currentTargetIgnoresAllowedArea = ignoreAllowedArea;
        currentTarget = ignoreAllowedArea
            ? target
            : ClampToAllowedArea(target);
        hasTarget = true;
        waitingAfterArrive = false;
        waitTimer = 0f;
        stuckTimer = 0f;
        blockedTimer = 0f;
        currentAction = action;
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
        currentVelocity = Vector2.zero;
        currentAction = "Talking";

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        UpdateVisualAnimation();
    }

    public bool IsAtMoveTarget(float extraDistance = 0f)
    {
        AutoResolveMapBounds();

        if (!hasTarget)
        {
            return false;
        }

        return Vector2.Distance(rb.position, currentTarget) <=
            arriveDistance + Mathf.Max(0f, extraDistance);
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
        currentTargetIgnoresAllowedArea = false;
        StopRigidbodyMotion();
        waitTimer = Mathf.Max(0f, time);
        waitingAfterArrive = arrived;
        blockedTimer = 0f;
        crowdBlockedTimer = 0f;
        UpdateVisualAnimation();

        currentAction =
            arrived
            ? "Arrived Wait"
            : "Retry Wait";
    }

    bool IsBlocked(Vector2 direction)
    {
        RaycastHit2D[] hits =
            Physics2D.RaycastAll(
                rb.position,
                direction,
                obstacleCheckDistance,
                obstacleLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null &&
                !hit.collider.isTrigger &&
                !IsSelfCollider(hit.collider) &&
                !IsNpcCollider(hit.collider))
            {
                return true;
            }
        }

        return false;
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
            !IsSelfCollider(hit) &&
            !IsNpcCollider(hit);
    }

    bool TryChooseObstacleDetourDirection(
        Vector2 desiredDirection,
        out Vector2 detourDirection)
    {
        detourDirection = desiredDirection;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = desiredDirection.normalized;
        Vector2 side = new Vector2(-desired.y, desired.x);
        float step = Mathf.Max(targetClearRadius * 2f, obstacleCheckDistance);

        for (int i = 0; i < 4; i++)
        {
            Vector2 candidateDirection =
                i == 0 ? side :
                i == 1 ? -side :
                i == 2 ? (side + desired * 0.35f).normalized :
                (-side + desired * 0.35f).normalized;

            if (candidateDirection.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            Vector2 candidatePosition =
                ClampToAllowedArea(rb.position + candidateDirection * step);

            if (!IsInsideAllowedArea(candidatePosition) ||
                IsPositionBlocked(candidatePosition) ||
                IsBlocked(candidateDirection))
            {
                continue;
            }

            detourDirection = candidateDirection.normalized;
            return true;
        }

        return false;
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

    bool TryResolveCrowdAhead(
        Vector2 desiredDirection,
        out Vector2 resolvedDirection)
    {
        resolvedDirection = desiredDirection;

        if (desiredDirection.sqrMagnitude <= 0.0001f ||
            crowdLookAheadDistance <= 0f)
        {
            return true;
        }

        Collider2D other;
        if (!TryFindNpcAhead(desiredDirection, out other))
        {
            crowdBlockedTimer = 0f;
            return true;
        }

        crowdBlockedTimer += Time.deltaTime;
        if (crowdBlockedTimer >= stuckTimeToPickNewTarget &&
            TryChooseCrowdDetourDirection(
                desiredDirection,
                other,
                out resolvedDirection))
        {
            crowdBlockedTimer = 0f;
            return true;
        }

        if (ShouldYieldToNpc(other))
        {
            crowdYieldUntil =
                Time.time +
                Mathf.Max(0.05f, crowdYieldDuration) *
                Random.Range(0.75f, 1.35f);
            StopRigidbodyMotion();
            currentAction = "Yielding";
            return false;
        }

        if (TryChooseCrowdDetourDirection(
                desiredDirection,
                other,
                out resolvedDirection))
        {
            return true;
        }

        crowdYieldUntil =
            Time.time +
            Mathf.Max(0.05f, crowdYieldDuration) *
            Random.Range(0.75f, 1.35f);
        StopRigidbodyMotion();
        currentAction = "Yielding";
        return false;
    }

    void StopRigidbodyMotion()
    {
        currentVelocity = Vector2.zero;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    bool TryFindNpcAhead(
        Vector2 direction,
        out Collider2D npcCollider)
    {
        npcCollider = null;

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                rb.position,
                Mathf.Max(0.01f, targetClearRadius),
                direction.normalized,
                Mathf.Max(separationRadius, crowdLookAheadDistance),
                crowdLayers);

        float nearestDistance =
            float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D collider = hit.collider;
            if (collider == null ||
                IsSelfCollider(collider) ||
                !IsNpcCollider(collider))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                npcCollider = collider;
            }
        }

        return npcCollider != null;
    }

    bool TryChooseCrowdDetourDirection(
        Vector2 desiredDirection,
        Collider2D other,
        out Vector2 detourDirection)
    {
        detourDirection = desiredDirection;

        Vector2 desired =
            desiredDirection.normalized;

        Vector2 side =
            new Vector2(-desired.y, desired.x);

        if (ShouldUseRightSide(other))
        {
            side = -side;
        }

        float distance =
            Mathf.Max(crowdDetourDistance, separationRadius, targetClearRadius * 2f);

        for (int i = 0; i < 2; i++)
        {
            Vector2 candidateSide =
                i == 0 ? side : -side;

            Vector2 candidate =
                ClampToAllowedArea(
                    rb.position +
                    (candidateSide + desired * 0.35f).normalized * distance);

            if (IsPositionBlocked(candidate) ||
                !IsInsideAllowedArea(candidate))
            {
                continue;
            }

            Vector2 toCandidate =
                candidate - rb.position;

            if (toCandidate.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            detourDirection = toCandidate.normalized;
            return true;
        }

        return false;
    }

    bool ShouldYieldToNpc(Collider2D other)
    {
        Transform otherRoot = GetNpcRoot(other);
        if (otherRoot == null)
        {
            return false;
        }

        return GetInstanceID() > otherRoot.gameObject.GetInstanceID();
    }

    bool ShouldUseRightSide(Collider2D other)
    {
        Transform otherRoot = GetNpcRoot(other);
        int otherId = otherRoot != null
            ? otherRoot.gameObject.GetInstanceID()
            : 0;

        return ((GetInstanceID() ^ otherId) & 1) == 0;
    }

    bool IsNpcCollider(Collider2D hit)
    {
        return GetNpcRoot(hit) != null;
    }

    Transform GetNpcRoot(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        NpcMapMover2D mover =
            hit.GetComponentInParent<NpcMapMover2D>();
        if (mover != null)
        {
            return mover.transform;
        }

        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.transform;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();
        return smartNpc != null ? smartNpc.transform : null;
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
                StopAndWait(false, blockedRetryWait);
                return;
            }

            StopAndWait(false, blockedRetryWait);
        }
    }

    bool IsInsideAllowedArea(Vector2 position)
    {
        if (!allowCrossMapAreas &&
            !currentTargetIgnoresAllowedArea &&
            keepInsideMapBounds &&
            mapBounds != null &&
            !IsInsideMapBounds(position))
        {
            return false;
        }

        if (!currentTargetIgnoresAllowedArea &&
            keepInsideWanderRadius)
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

        if (!allowCrossMapAreas &&
            keepInsideMapBounds &&
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


    bool IsPointInsideMapBounds(Vector2 position)
    {
        if (allowCrossMapAreas ||
            mapBounds == null)
        {
            return true;
        }

        Vector2 closest = mapBounds.ClosestPoint(position);
        return Vector2.Distance(closest, position) <= 0.02f;
    }

    bool IsInsideMapBounds(Vector2 position)
    {
        if (allowCrossMapAreas ||
            mapBounds == null)
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
        rb.bodyType = useKinematicNpcMovement
            ? RigidbodyType2D.Kinematic
            : RigidbodyType2D.Dynamic;
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

        Vector2 animationVelocity =
            rb != null
            ? rb.linearVelocity
            : currentVelocity;

        bool isIdle = animationVelocity.sqrMagnitude <= 0.0001f;
        Vector2 direction = isIdle ? Vector2.zero : animationVelocity.normalized;
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
