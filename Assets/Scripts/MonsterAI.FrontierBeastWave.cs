using UnityEngine;

public partial class MonsterAI
{
    bool frontierBeastWaveActive;
    bool frontierBeastWaveCombatActive;
    bool frontierBeastWaveStateCaptured;
    Vector2 frontierBeastWaveAnchor;
    bool frontierBeastWaveHasAnchor;
    bool frontierOriginalGuardTerritory;
    float frontierOriginalRoamRadius;
    float frontierOriginalTerritoryRadius;
    float frontierOriginalReturnHomeDistance;
    bool frontierOriginalAttackPlayer;
    bool frontierOriginalAttackVillagers;
    bool frontierOriginalAttackSmartNpcs;
    bool frontierOriginalAttackOtherMonsters;
    HuntTargetType frontierOriginalHuntTargetType;

    public bool IsInFrontierBeastWave =>
        frontierBeastWaveActive;

    public bool IsInFrontierBeastWaveCombat =>
        frontierBeastWaveActive &&
        frontierBeastWaveCombatActive;

    public void EnterFrontierBeastWaveStaging(
        Vector3 stagingPoint)
    {
        CaptureFrontierBeastWaveState();

        frontierBeastWaveActive = true;
        frontierBeastWaveCombatActive = false;
        frontierBeastWaveAnchor = stagingPoint;
        frontierBeastWaveHasAnchor = true;

        guardTerritory = false;
        attackPlayer = false;
        attackVillagers = false;
        attackSmartNpcs = false;
        attackOtherMonsters = false;
        huntTargetType = HuntTargetType.Any;
        roamRadius = Mathf.Max(frontierOriginalRoamRadius, 6f);
        territoryRadius = Mathf.Max(frontierOriginalTerritoryRadius, 6f);
        returnHomeDistance = Mathf.Max(frontierOriginalReturnHomeDistance, 12f);

        ClearCurrentTarget();
        StopRetreating();
        hasTarget = false;
        waitTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        desiredVelocity = Vector2.zero;
        currentMoveVelocity = Vector2.zero;
        currentAction = "Tap ket thu trieu";
        SetMovingAnimation(false);
    }

    public void BeginFrontierBeastWaveCombat(
        Vector3 battlePoint,
        bool allowAttackPlayer,
        float aggressionBonus)
    {
        CaptureFrontierBeastWaveState();

        frontierBeastWaveActive = true;
        frontierBeastWaveCombatActive = true;
        frontierBeastWaveAnchor = battlePoint;
        frontierBeastWaveHasAnchor = true;

        guardTerritory = false;
        attackPlayer = allowAttackPlayer;
        attackVillagers = true;
        attackSmartNpcs = true;
        attackOtherMonsters = false;
        huntTargetType = HuntTargetType.Any;
        roamRadius = Mathf.Max(frontierOriginalRoamRadius, 8f);
        territoryRadius = Mathf.Max(frontierOriginalTerritoryRadius, 8f);
        returnHomeDistance = Mathf.Max(frontierOriginalReturnHomeDistance, 16f);

        hasTarget = false;
        waitTimer = 0f;
        ApplyTemperamentSurge(
            Mathf.Max(0f, aggressionBonus),
            Mathf.Max(0f, aggressionBonus * 0.5f));
    }

    public void ExitFrontierBeastWave()
    {
        frontierBeastWaveActive = false;
        frontierBeastWaveCombatActive = false;
        frontierBeastWaveHasAnchor = false;

        if (frontierBeastWaveStateCaptured)
        {
            guardTerritory = frontierOriginalGuardTerritory;
            roamRadius = frontierOriginalRoamRadius;
            territoryRadius = frontierOriginalTerritoryRadius;
            returnHomeDistance = frontierOriginalReturnHomeDistance;
            attackPlayer = frontierOriginalAttackPlayer;
            attackVillagers = frontierOriginalAttackVillagers;
            attackSmartNpcs = frontierOriginalAttackSmartNpcs;
            attackOtherMonsters = frontierOriginalAttackOtherMonsters;
            huntTargetType = frontierOriginalHuntTargetType;
            frontierBeastWaveStateCaptured = false;
        }

        ClearCurrentTarget();
        StopRetreating();
        hasTarget = false;
        desiredVelocity = Vector2.zero;
        currentMoveVelocity = Vector2.zero;
        waitTimer = Mathf.Max(0.25f, waitTime * 0.5f);
        currentAction = NpcText.Action("restTerritory");

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        SetMovingAnimation(false);
    }

    bool HandleFrontierBeastWaveUpdate()
    {
        if (!frontierBeastWaveActive)
        {
            return false;
        }

        if (!frontierBeastWaveHasAnchor)
        {
            desiredVelocity = Vector2.zero;
            currentAction = frontierBeastWaveCombatActive
                ? "Tan cong thu trieu"
                : "Tap ket thu trieu";
            SetMovingAnimation(false);
            return true;
        }

        if (frontierBeastWaveCombatActive)
        {
            UpdateFrontierBeastWaveCombat();
        }
        else
        {
            UpdateFrontierBeastWaveStaging();
        }

        return true;
    }

    bool HandleFrontierBeastWaveMovementRecovery()
    {
        if (!frontierBeastWaveActive)
        {
            return false;
        }

        if (desiredVelocity.sqrMagnitude <= 0.0001f)
        {
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            patrolRecoveryAttempts = 0;
            return true;
        }

        float moved =
            Vector2.Distance(
                transform.position,
                lastUnstuckPosition);
        if (moved <= unstuckMinMoveDistance)
        {
            stuckMoveTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
        }

        if (stuckMoveTimer < unstuckCheckDelay)
        {
            return true;
        }

        desiredVelocity = Vector2.zero;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        SetMovingAnimation(false);
        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;
        return true;
    }

    void CaptureFrontierBeastWaveState()
    {
        if (frontierBeastWaveStateCaptured)
        {
            return;
        }

        frontierBeastWaveStateCaptured = true;
        frontierOriginalGuardTerritory = guardTerritory;
        frontierOriginalRoamRadius = roamRadius;
        frontierOriginalTerritoryRadius = territoryRadius;
        frontierOriginalReturnHomeDistance = returnHomeDistance;
        frontierOriginalAttackPlayer = attackPlayer;
        frontierOriginalAttackVillagers = attackVillagers;
        frontierOriginalAttackSmartNpcs = attackSmartNpcs;
        frontierOriginalAttackOtherMonsters = attackOtherMonsters;
        frontierOriginalHuntTargetType = huntTargetType;
    }

    void UpdateFrontierBeastWaveStaging()
    {
        if (HasValidTarget())
        {
            ClearCurrentTarget();
        }

        MoveTowardFrontierBeastWaveAnchor(
            "Tap ket thu trieu");
    }

    void UpdateFrontierBeastWaveCombat()
    {
        if (HasValidTarget())
        {
            float distanceToTarget =
                Vector2.Distance(
                    transform.position,
                    currentTarget.position);
            if (distanceToTarget > forgetTargetRange)
            {
                ClearCurrentTarget();
            }
            else
            {
                if (isRetreating)
                {
                    if (!ShouldContinueRetreating(currentTarget))
                    {
                        StopRetreating();
                    }
                    else
                    {
                        FleeFrom(currentTarget);
                        return;
                    }
                }
                else if (TryBeginRetreat(currentTarget))
                {
                    FleeFrom(currentTarget);
                    return;
                }

                if (ShouldAttackTarget(currentTarget))
                {
                    FollowTarget(distanceToTarget);
                    return;
                }
            }
        }

        if (!usePerformanceThrottle || Time.time >= nextDetectTime)
        {
            nextDetectTime = Time.time + GetDetectDelay();
            AcquireIntruderTarget();
            if (HasValidTarget())
            {
                return;
            }
        }

        MoveTowardFrontierBeastWaveAnchor(
            "Tan cong thu trieu");
    }

    void MoveTowardFrontierBeastWaveAnchor(string actionLabel)
    {
        Vector2 direction =
            frontierBeastWaveAnchor -
            (Vector2)transform.position;
        float distance = direction.magnitude;
        if (distance <= 0.2f)
        {
            desiredVelocity = Vector2.zero;
            currentAction = actionLabel;
            SetMovingAnimation(false);
            return;
        }

        direction /= Mathf.Max(0.0001f, distance);
        desiredVelocity = direction * moveSpeed;
        currentAction = actionLabel;
        SetMovingAnimation(true);
        FaceDirection(direction);
    }
}
