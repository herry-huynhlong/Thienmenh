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
    public float obstacleDetourLookAhead = 0.65f;
    public float obstacleScanDistance = 8f;
    public float obstacleScanStep = 0.35f;
    public float targetClearRadius = 0.25f;
    public float blockedTargetRetryDelay = 0.8f;
    public int maxPickTargetAttempts = 16;
    public float stuckTimeToPickNewTarget = 0.8f;
    public bool useObstacleAvoidance = true;

    [Header("Crowd Avoidance")]
    public bool ignoreNpcBodyCollisions = true;
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
    float blockedMoveTimer;
    float crowdBlockedTimer;
      float movementPausedUntil;
      float crowdYieldUntil;
      float crowdDirectionCommitUntil;
      Vector2 crowdCommittedDirection;
      Vector2 obstacleAvoidTarget;
    float obstacleAvoidUntil;
    bool waitingAfterArrive;
    bool hasTarget;
    bool hasObstacleAvoidTarget;
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
        NpcCollisionRegistry.Register(this, selfColliders);
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

    void OnDisable()
    {
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        NpcCollisionRegistry.Unregister(this);
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

    void OnNpcMapTeleported()
    {
        OnNpcMapTeleported(null);
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

        NpcMapZone? resolvedZone =
            area != null
                ? area.zone
                : NpcMapNavigator.ResolveActorZone(gameObject);

        if (gate != null)
        {
            if (resolvedZone.HasValue)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedZone.Value);
            }
            else
            {
                NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
                resolvedZone = gate.toZone;
            }

            area = NpcMapNavigator.ResolveMapAreaAfterTeleport(
                gameObject,
                resolvedZone.Value,
                referencePosition);
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
        blockedMoveTimer = 0f;
        crowdBlockedTimer = 0f;
        ClearObstacleAvoidance();
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

        Vector2 routeTarget = currentTarget;
        if (hasObstacleAvoidTarget)
        {
            if (Time.time >= obstacleAvoidUntil ||
                Vector2.Distance(position, obstacleAvoidTarget) <= arriveDistance ||
                IsPositionBlocked(obstacleAvoidTarget))
            {
                hasObstacleAvoidTarget = false;
            }
            else
            {
                routeTarget = obstacleAvoidTarget;
            }
        }

        Vector2 toTarget = routeTarget - position;

        if (toTarget.magnitude <= arriveDistance)
        {
            if (hasObstacleAvoidTarget)
            {
                hasObstacleAvoidTarget = false;
                currentAction = "Detour Complete";
                return;
            }

            StopAndWait(true);
            return;
        }

        Vector2 direction = toTarget.normalized;

        if (IsBlocked(direction))
        {
            if (TryCommitObstacleScanTarget(direction, currentTarget))
            {
                blockedMoveTimer = 0f;
                currentAction = "Scan";
                return;
            }

            if (useObstacleAvoidance &&
                TryChooseObstacleDetourDirection(direction, currentTarget, out Vector2 detourDirection) &&
                TryCommitObstacleAvoidTarget(detourDirection))
            {
                blockedMoveTimer = 0f;
                direction = detourDirection;
            }
            else
            {
                blockedMoveTimer += Time.deltaTime;
                currentAction = "Blocked";
                currentVelocity = Vector2.zero;
                UpdateVisualAnimation();
                DetectStuck();

                if (blockedMoveTimer >= blockedTargetRetryDelay)
                {
                    if (!TryPickStuckEscapeTarget(direction, out Vector2 escapeTarget))
                    {
                        StopAndWait(false, blockedRetryWait);
                        return;
                    }

                    obstacleAvoidTarget = escapeTarget;
                    obstacleAvoidUntil = Time.time + 1.4f;
                    hasObstacleAvoidTarget = true;
                    blockedMoveTimer = 0f;
                    stuckTimer = 0f;
                    currentAction = "Escape Block";
                }

                return;
            }
        }

        if (!TryResolveCrowdAhead(direction, out direction))
        {
            return;
        }

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
        Vector2 resolvedTarget = ignoreAllowedArea
            ? target
            : ClampToAllowedArea(target);

        if (hasTarget &&
            currentTarget == resolvedTarget &&
            currentTargetIgnoresAllowedArea == ignoreAllowedArea &&
            currentAction == action)
        {
            return;
        }

        currentTargetIgnoresAllowedArea = ignoreAllowedArea;
        currentTarget = resolvedTarget;
        hasTarget = true;
        waitingAfterArrive = false;
        waitTimer = 0f;
        stuckTimer = 0f;
        blockedMoveTimer = 0f;
        ClearObstacleAvoidance();
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
        ClearObstacleAvoidance();

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
        ClearObstacleAvoidance();

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
        blockedMoveTimer = 0f;
        crowdBlockedTimer = 0f;
        ClearObstacleAvoidance();
        UpdateVisualAnimation();

        currentAction =
            arrived
            ? "Arrived Wait"
            : "Retry Wait";
    }

    void ClearObstacleAvoidance()
    {
        hasObstacleAvoidTarget = false;
        obstacleAvoidUntil = 0f;
        blockedMoveTimer = 0f;
    }

    bool IsBlocked(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        RaycastHit2D[] hits =
            Physics2D.RaycastAll(
                rb.position,
                direction.normalized,
                GetObstacleLookAheadDistance(),
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
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                targetClearRadius,
                obstacleLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit != null &&
                !hit.isTrigger &&
                !IsSelfCollider(hit) &&
                !IsNpcCollider(hit))
            {
                return true;
            }
        }

        return false;
    }

    bool TryChooseObstacleDetourDirection(
        Vector2 desiredDirection,
        Vector2 finalTarget,
        out Vector2 detourDirection)
    {
        detourDirection = desiredDirection;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = desiredDirection.normalized;
        Vector2 targetDirection = finalTarget - rb.position;
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            targetDirection = desired;
        }
        else
        {
            targetDirection.Normalize();
        }

        float lookAhead = GetObstacleLookAheadDistance();
        float detourStep = Mathf.Max(
            targetClearRadius * 4f,
            obstacleDetourLookAhead * 3f,
            moveSpeed * 1.5f);
        float bestScore = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < DetourAngles.Length; i++)
        {
            float angle = DetourAngles[i];

            if (TryScoreDetourDirection(
                    RotateDirection(desired, angle),
                    desired,
                    targetDirection,
                    Mathf.Max(lookAhead, detourStep),
                    out float score) &&
                score > bestScore)
            {
                bestScore = score;
                detourDirection = RotateDirection(desired, angle);
                found = true;
            }

            if (Mathf.Approximately(angle, 0f))
            {
                continue;
            }

            if (TryScoreDetourDirection(
                    RotateDirection(desired, -angle),
                    desired,
                    targetDirection,
                    Mathf.Max(lookAhead, detourStep),
                    out score) &&
                score > bestScore)
            {
                bestScore = score;
                detourDirection = RotateDirection(desired, -angle);
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        detourDirection.Normalize();
        return true;
    }

    bool TryCommitObstacleAvoidTarget(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = direction.normalized;
        float distance = Mathf.Max(
            targetClearRadius * 5f,
            obstacleDetourLookAhead * 2.5f,
            moveSpeed * 1.5f);

        Vector2 candidate = rb.position + desired * distance;
        Vector2 clearPoint;
        if (!TryFindClearPointNear(candidate, out clearPoint))
        {
            clearPoint = candidate;
        }

        if (IsPositionBlocked(clearPoint) ||
            !HasClearLineTo(clearPoint))
        {
            return false;
        }

        obstacleAvoidTarget = clearPoint;
        obstacleAvoidUntil = Time.time + 1.2f;
        hasObstacleAvoidTarget = true;
        blockedMoveTimer = 0f;
        currentAction = "Detour";
        return true;
    }

    bool TryCommitObstacleScanTarget(
        Vector2 desiredDirection,
        Vector2 finalTarget)
    {
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = desiredDirection.normalized;
        float scanDistance = Mathf.Max(
            obstacleCheckDistance * 2f,
            obstacleScanDistance);
        float scanStep = Mathf.Max(
            0.1f,
            obstacleScanStep);
        float startDistance = Mathf.Max(
            targetClearRadius * 2f,
            obstacleCheckDistance * 0.75f);

        Vector2 side = new Vector2(-desired.y, desired.x);
        float bestScore = float.NegativeInfinity;
        bool found = false;
        Vector2 best = Vector2.zero;

        for (float distance = startDistance; distance <= scanDistance; distance += scanStep)
        {
            Vector2 forwardPoint = rb.position + desired * distance;
            Vector2 towardTarget = (finalTarget - rb.position).normalized;
            float targetProgress = Vector2.Dot(
                (forwardPoint - rb.position).normalized,
                towardTarget);

            if (IsInsideAllowedArea(forwardPoint) &&
                !IsPositionBlocked(forwardPoint) &&
                HasClearLineTo(forwardPoint))
            {
                if (targetProgress > bestScore)
                {
                    bestScore = targetProgress;
                    best = forwardPoint;
                    found = true;
                }
            }

            Vector2 sideOffset = side * Mathf.Max(targetClearRadius * 1.5f, 0.3f);
            Vector2[] candidatePoints =
            {
                forwardPoint + sideOffset,
                forwardPoint - sideOffset
            };

            for (int i = 0; i < candidatePoints.Length; i++)
            {
                Vector2 candidate = candidatePoints[i];
                if (!IsInsideAllowedArea(candidate) ||
                    IsPositionBlocked(candidate) ||
                    !HasClearLineTo(candidate))
                {
                    continue;
                }

                float score =
                    Vector2.Dot(
                        (candidate - rb.position).normalized,
                        towardTarget) +
                    distance * 0.05f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        obstacleAvoidTarget = best;
        obstacleAvoidUntil = Time.time + 1.5f;
        hasObstacleAvoidTarget = true;
        blockedMoveTimer = 0f;
        return true;
    }

    bool TryScoreDetourDirection(
        Vector2 candidate,
        Vector2 desired,
        Vector2 targetDirection,
        float lookAhead,
        out float score)
    {
        score = 0f;

        if (candidate.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        candidate.Normalize();

        Vector2 nextPoint =
            rb.position +
            candidate * Mathf.Max(targetClearRadius * 4f, lookAhead * 1.1f);

        if (!IsInsideAllowedArea(nextPoint) ||
            IsPositionBlocked(nextPoint) ||
            !HasClearLineTo(nextPoint))
        {
            return false;
        }

        float clearDistance = GetClearDistance(candidate, lookAhead);
        if (clearDistance < targetClearRadius * 2f)
        {
            return false;
        }

        float progressScore = Mathf.Max(-0.5f, Vector2.Dot(candidate, targetDirection));
        if (progressScore < -0.05f)
        {
            return false;
        }

        float smoothScore = Mathf.Max(-0.5f, Vector2.Dot(candidate, desired));

        score =
            clearDistance / Mathf.Max(0.01f, lookAhead) * 3f +
            progressScore * 2f +
            smoothScore;
        return true;
    }

    bool TryFindClearPointNear(Vector2 preferred, out Vector2 result)
    {
        preferred = ClampToAllowedArea(preferred);

        if (!IsPositionBlocked(preferred))
        {
            result = preferred;
            return true;
        }

        float baseRadius = Mathf.Max(targetClearRadius * 2f, 0.25f);
        for (int i = 0; i < maxPickTargetAttempts; i++)
        {
            float radius = baseRadius + i * 0.15f;
            Vector2 offset = Random.insideUnitCircle.normalized * radius;
            Vector2 candidate = ClampToAllowedArea(preferred + offset);

            if (!IsPositionBlocked(candidate) &&
                HasClearLineTo(candidate))
            {
                result = candidate;
                return true;
            }
        }

        result = rb.position;
        return !IsPositionBlocked(result);
    }

    bool HasClearLineTo(Vector2 target)
    {
        Vector2 origin = rb.position;
        Vector2 delta = target - origin;
        float distance = delta.magnitude;

        if (distance <= targetClearRadius)
        {
            return true;
        }

        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                origin,
                Mathf.Max(0.01f, targetClearRadius),
                delta.normalized,
                distance,
                obstacleLayers);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null &&
                !hit.collider.isTrigger &&
                !IsSelfCollider(hit.collider) &&
                !IsNpcCollider(hit.collider))
            {
                return false;
            }
        }

        return true;
    }

    float GetClearDistance(Vector2 direction, float maxDistance)
    {
        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                rb.position,
                Mathf.Max(0.01f, targetClearRadius),
                direction.normalized,
                maxDistance,
                obstacleLayers);

        float best = maxDistance;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null &&
                !hit.collider.isTrigger &&
                !IsSelfCollider(hit.collider) &&
                !IsNpcCollider(hit.collider))
            {
                best = Mathf.Min(best, hit.distance);
            }
        }

        return best;
    }

    float GetObstacleLookAheadDistance()
    {
        float speedLookAhead =
            Mathf.Max(0f, moveSpeed) * 0.25f + targetClearRadius * 2f;

        return Mathf.Max(
            obstacleCheckDistance,
            obstacleDetourLookAhead,
            targetClearRadius * 3f,
            speedLookAhead);
    }

    Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    static readonly float[] DetourAngles =
    {
        0f,
        20f,
        35f,
        50f,
        70f,
        90f,
        120f,
        150f
    };

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
        if (ignoreNpcBodyCollisions || separationRadius <= 0f)
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

          if (ignoreNpcBodyCollisions)
          {
              return true;
          }

          if (Time.time < crowdDirectionCommitUntil &&
              crowdCommittedDirection.sqrMagnitude > 0.0001f)
          {
              resolvedDirection = crowdCommittedDirection.normalized;
              return true;
          }

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

        if (TryForceCrowdStepAside(desiredDirection, other, out resolvedDirection))
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
              crowdCommittedDirection = desiredDirection;
              crowdDirectionCommitUntil =
                  Time.time + Mathf.Max(0.1f, crowdYieldDuration * 0.5f);
              StopRigidbodyMotion();
              currentAction = "Yielding";
              return false;
          }

          if (TryChooseCrowdDetourDirection(
                  desiredDirection,
                  other,
                  out resolvedDirection))
          {
              crowdCommittedDirection = resolvedDirection;
              crowdDirectionCommitUntil = Time.time + 0.35f;
              return true;
          }

          crowdYieldUntil =
              Time.time +
              Mathf.Max(0.05f, crowdYieldDuration) *
              Random.Range(0.75f, 1.35f);
          crowdCommittedDirection = desiredDirection;
          crowdDirectionCommitUntil =
              Time.time + Mathf.Max(0.1f, crowdYieldDuration * 0.5f);
          StopRigidbodyMotion();
          currentAction = "Yielding";
          return false;
      }

    bool TryForceCrowdStepAside(
        Vector2 desiredDirection,
        Collider2D other,
        out Vector2 detourDirection)
    {
        detourDirection = desiredDirection;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 desired = desiredDirection.normalized;
        Vector2 side = new Vector2(-desired.y, desired.x);

        if (ShouldUseRightSide(other))
        {
            side = -side;
        }

        float distance = Mathf.Max(
            crowdDetourDistance,
            separationRadius,
            targetClearRadius * 2.5f);

        Vector2[] candidates =
        {
            side,
            -side,
            side + desired * 0.25f,
            -side + desired * 0.25f
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector2 candidate = candidates[i];
            if (candidate.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            candidate.Normalize();
            Vector2 nextPoint = rb.position + candidate * distance;

              if (IsPositionBlocked(nextPoint) ||
                  !HasClearLineTo(nextPoint))
              {
                  continue;
              }

              detourDirection = candidate;
              crowdCommittedDirection = detourDirection;
              crowdDirectionCommitUntil = Time.time + 0.35f;
              return true;
          }

        return false;
    }

    void StopRigidbodyMotion()
    {
        currentVelocity = Vector2.zero;
        blockedMoveTimer = 0f;

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
            Vector2 desiredDirection =
                hasObstacleAvoidTarget
                ? obstacleAvoidTarget - rb.position
                : currentTarget - rb.position;

            if (TryPickStuckEscapeTarget(desiredDirection, out Vector2 escapeTarget))
            {
                obstacleAvoidTarget = escapeTarget;
                obstacleAvoidUntil = Time.time + 1.4f;
                hasObstacleAvoidTarget = true;
                blockedMoveTimer = 0f;
                stuckTimer = 0f;
                currentAction = "Escape Stuck";
                return;
            }

            StopAndWait(false, blockedRetryWait);
        }
    }

    bool TryPickStuckEscapeTarget(
        Vector2 blockedDirection,
        out Vector2 target)
    {
        target = rb != null ? rb.position : (Vector2)transform.position;

        if (rb == null)
        {
            return false;
        }

        Vector2 forward =
            blockedDirection.sqrMagnitude > 0.0001f
            ? blockedDirection.normalized
            : Random.insideUnitCircle.normalized;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector2.up;
        }

        Vector2 side = new Vector2(-forward.y, forward.x);
        int sideSign = (GetInstanceID() & 1) == 0 ? 1 : -1;

        Vector2[] directions =
        {
            side * sideSign,
            -side * sideSign,
            (side * sideSign - forward * 0.5f).normalized,
            (-side * sideSign - forward * 0.5f).normalized,
            -forward,
            (side * sideSign + forward * 0.25f).normalized,
            (-side * sideSign + forward * 0.25f).normalized
        };

        float baseDistance =
            Mathf.Max(targetClearRadius * 4f, obstacleDetourLookAhead, 0.45f);
        float bestScore = float.NegativeInfinity;
        Vector2 best = target;
        bool found = false;

        for (int radiusStep = 0; radiusStep < 4; radiusStep++)
        {
            float distance =
                baseDistance + radiusStep * Mathf.Max(targetClearRadius * 2f, 0.35f);

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 direction = directions[i];
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                direction.Normalize();
                Vector2 candidate =
                    ClampToAllowedArea(rb.position + direction * distance);

                if (!IsInsideAllowedArea(candidate) ||
                    IsPositionBlocked(candidate) ||
                    !HasClearLineTo(candidate) ||
                    IsBlocked(direction))
                {
                    continue;
                }

                float score =
                    GetClearDistance(direction, GetObstacleLookAheadDistance()) +
                    Mathf.Max(-0.25f, Vector2.Dot(direction, -forward)) *
                    baseDistance;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }
        }

        target = best;
        return found;
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
