using UnityEngine;

public partial class VillagerAI
{
    bool ShouldLeaveHiddenHomeNow()
    {
        if (FrontierDefenseCoordinator.IsVillageShelterAlertActive)
        {
            return false;
        }

        if (ageGroup == VillagerAgeGroup.Child ||
            ageGroup == VillagerAgeGroup.Teen)
        {
            WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
            if (timeSystem == null)
            {
                return true;
            }

            return timeSystem.CurrentPhase != WorldTimePhase.Night &&
                timeSystem.CurrentPhase != WorldTimePhase.Dawn;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        return activity != NpcScheduleActivity.Sleep &&
            activity != NpcScheduleActivity.ReturnHome;
    }

    public void ForceHiddenAtHome(bool hidden)
    {
        hiddenAtHome = hidden;
        if (hidden)
        {
            isReturningHome = false;
        }
        if (spawnedWorldActor != null)
        {
            spawnedWorldActor.isHiddenAtHome = hidden;
        }

        if (!hidden && !enabled)
        {
            enabled = true;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = !hidden;
        }

        if (ownRenderers == null || ownRenderers.Length == 0)
        {
            ownRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < ownRenderers.Length; i++)
        {
            if (ownRenderers[i] != null)
            {
                ownRenderers[i].enabled = !hidden;
            }
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] != null)
            {
                ownColliders[i].enabled = !hidden;
            }
        }

        if (hidden)
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("rest");
            actionTimer = Mathf.Max(actionTimer, restDuration);
            UpdateCultivationEffect(false);
        }
        else
        {
            currentAction = NpcText.Action("idle");
            thinkTimer = 0f;
            actionTimer = 0f;
        }
    }

    public void GoHomeToRest()
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            currentAction = NpcText.Action("idle");
            isReturningHome = false;
            return;
        }

        if (ShouldContinueExistingHomeReturn())
        {
            return;
        }

        if (Time.frameCount == lastHomeTravelFrame &&
            currentAction == NpcText.Action("goHomeRest") &&
            (currentTarget != null ||
            hasDirectMoveTarget ||
            hasWanderTarget ||
            hasObstacleAvoidTarget))
        {
            return;
        }

        if (!enabled)
        {
            enabled = true;
        }

        if (homePoint == null)
        {
            ResolveMissingHomePoint();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Time.time - lastGoHomeLogTime >= 1f)
        {
            Vector3 debugHomePosition = GetHomePosition();
            float distanceToHome =
                Vector2.Distance(transform.position, debugHomePosition);

            Debug.LogWarning(
                "[VillagerAI] GoHomeToRest -> " +
                gameObject.name +
                " action=" + currentAction +
                " job=" + job +
                " homePoint=" + (homePoint != null ? homePoint.name : "null") +
                " hiddenAtHome=" + hiddenAtHome +
                " atHome=" + IsAtHomePosition(debugHomePosition) +
                " distanceToHome=" + distanceToHome.ToString("0.00") +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
            lastGoHomeLogTime = Time.time;
        }
#endif

        CancelScheduledWorkState();
        isReturningHome = true;
        lastHomeTravelFrame = Time.frameCount;
        lastHomeTravelIssueTime = Time.time;
        actionTimer = 0f;

        Vector3 homePosition = GetHomePosition();
        Vector3 travelTarget = homePosition;
        TryResolveHomeTravelTarget(ref travelTarget);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Vector2.Distance(homePosition, travelTarget) > 0.01f)
        {
            Debug.LogWarning(
                "[VillagerAI] Home fallback target -> " +
                gameObject.name +
                " home=" + homePosition +
                " fallback=" + travelTarget +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
        }
#endif
        SetDirectMoveTarget(travelTarget, false, GetHomeZone());
        MoveUsingRoad(travelTarget, GetHomeZone());
        currentAction = NpcText.Action("goHomeRest");

        if (IsAtHomePosition(homePosition) ||
            IsAtResolvedHomeTravelPosition(homePosition, travelTarget))
        {
            CompleteHomeArrival();
        }
    }

    bool ShouldContinueExistingHomeReturn()
    {
        if (!isReturningHome ||
            IsAtHomePosition(GetHomePosition()))
        {
            return false;
        }

        float retryDelay =
            Mathf.Max(
                0.75f,
                unstuckCheckDelay);
        if (Time.time - lastHomeTravelIssueTime < retryDelay)
        {
            return true;
        }

        return currentTarget != null ||
            hasDirectMoveTarget ||
            hasWanderTarget ||
            hasObstacleAvoidTarget;
    }

    void CompleteHomeArrival()
    {
        ClearMovementTargets();
        StopMoving();
        fatigue = 0f;
        Heal(10);

        actionTimer = restDuration;
        currentAction = NpcText.Action("rest");
        ResetDailyTargets();
        isReturningHome = false;

        if (hideAtHome)
        {
            ForceHiddenAtHome(true);
        }
    }

    bool IsAtResolvedHomeTravelPosition(
        Vector3 homePosition,
        Vector3 travelTarget)
    {
        return Vector2.Distance(homePosition, travelTarget) > 0.01f &&
            IsAtHomePosition(travelTarget);
    }

    void CancelScheduledWorkState()
    {
        ResetDailyTargets();
        ClearMovementTargets();

        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer != null)
        {
            gatherer.CancelGatheringNow();
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null)
        {
            harvestJob.CancelHarvestNow();
        }

        HunterJob hunterJob = GetComponent<HunterJob>();
        if (hunterJob != null)
        {
            hunterJob.CancelHunterNow();
        }
    }

    void GoHomeIdle(string action)
    {
        if (IsInDungeonCombatSession())
        {
            StopMoving();
            ClearMovementTargets();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            currentAction = action;
            return;
        }

        if (homePoint == null)
        {
            Wander(action);
            return;
        }

        Vector3 homePosition = GetHomePosition();
        Vector3 travelTarget = homePosition;
        TryResolveHomeTravelTarget(ref travelTarget);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Vector2.Distance(homePosition, travelTarget) > 0.01f)
        {
            Debug.LogWarning(
                "[VillagerAI] Home idle fallback target -> " +
                gameObject.name +
                " home=" + homePosition +
                " fallback=" + travelTarget +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
        }
#endif
        SetDirectMoveTarget(travelTarget, false, GetHomeZone());
        MoveUsingRoad(travelTarget, GetHomeZone());
        currentAction = action;

        if (IsAtHomePosition(homePosition) ||
            IsAtResolvedHomeTravelPosition(homePosition, travelTarget))
        {
            ClearMovementTargets();
            StopMoving();
            actionTimer = idleAtHomeDuration;
            if (hideAtHome &&
                IsCurrentScheduleActivity(NpcScheduleActivity.ReturnHome))
            {
                ForceHiddenAtHome(true);
            }
        }
    }

    Vector3 GetHomePosition()
    {
        return homePoint != null
            ? homePoint.position
            : spawnPosition;
    }

    bool TryResolveHomeTravelTarget(ref Vector3 homePosition)
    {
        if (IsMoveTargetFeasible(homePosition))
        {
            return true;
        }

        if (TryFindClearPointNear(homePosition, out Vector3 clearPoint) &&
            IsMoveTargetFeasible(clearPoint))
        {
            homePosition = clearPoint;
            return true;
        }

        NpcMapZone? homeZone = GetHomeZone();
        if (homeZone.HasValue)
        {
            Vector3 zoneFallback = GetFallbackPositionInZone(homeZone.Value);
            if (IsMoveTargetFeasible(zoneFallback))
            {
                homePosition = zoneFallback;
                return true;
            }
        }

        return false;
    }

    void ResolveMissingHomePoint()
    {
        if (homePoint != null)
        {
            return;
        }

        Transform[] candidates =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include);

        NpcMapZone? actorZone = GetCurrentMapZone();
        Transform bestCandidate = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];
            if (candidate == null ||
                candidate == transform ||
                candidate.IsChildOf(transform))
            {
                continue;
            }

            if (!IsLikelyHomePointName(candidate.name))
            {
                continue;
            }

            float score = Vector2.Distance(spawnPosition, candidate.position);
            if (actorZone.HasValue)
            {
                NpcMapZone? candidateZone =
                    NpcMapNavigator.GetDestinationZone(candidate);
                if (candidateZone.HasValue &&
                    candidateZone.Value != actorZone.Value)
                {
                    score += 1000f;
                }
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate != null)
        {
            homePoint = bestCandidate;
        }
    }
}
