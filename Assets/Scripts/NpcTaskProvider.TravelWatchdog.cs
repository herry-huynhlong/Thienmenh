using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Travel timeout, recovery and safe interaction-position resolution.
public partial class NpcTaskProvider
{
    void ArmTaskTravelWatchdog(
        RunningNpcTask task,
        Vector3 target)
    {
        if (task == null ||
            task.npc == null)
        {
            return;
        }

        task.travelWatchdogArmed = true;
        task.travelWatchdogStage = task.stage;
        task.travelWatchdogTarget = target;
        task.travelWatchdogLastPosition = task.npc.transform.position;
        task.travelStageStartedAt = Time.time;
        task.travelLastProgressAt = Time.time;
        task.travelLastDistanceToTarget =
            Vector2.Distance(
                task.npc.transform.position,
                target);
        task.maxTravelDuration =
            ComputeTravelWatchdogDuration(
                task.npc,
                task.travelLastDistanceToTarget);
        task.travelRetryCount = 0;
    }

    void DisarmTaskTravelWatchdog(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        task.travelWatchdogArmed = false;
        task.travelLastDistanceToTarget = float.PositiveInfinity;
        task.maxTravelDuration = 0f;
    }

    void HandleNpcTeleportedInternal(
        GameObject npc,
        GameObject gateObject)
    {
        if (npc == null ||
            runningTasks == null ||
            runningTasks.Count == 0)
        {
            return;
        }

        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;

        for (int i = 0; i < runningTasks.Count; i++)
        {
            RunningNpcTask task = runningTasks[i];
            if (task == null ||
                task.npc != npc ||
                !task.travelWatchdogArmed ||
                !ShouldRearmTaskTravelWatchdogAfterTeleport(task))
            {
                continue;
            }

            RearmTaskTravelWatchdogAfterTeleport(task, gate);
        }
    }

    bool ShouldRearmTaskTravelWatchdogAfterTeleport(
        RunningNpcTask task)
    {
        if (task == null)
        {
            return false;
        }

        switch (task.stage)
        {
            case TavernTaskStage.GoingToCounter:
            case TavernTaskStage.GoingToBoard:
            case TavernTaskStage.ReturningToProvider:
            case TavernTaskStage.GoingToWork:
            case TavernTaskStage.ReturningToTurnIn:
            case TavernTaskStage.WaitingForTargetRespawn:
                return true;

            default:
                return false;
        }
    }

    void RearmTaskTravelWatchdogAfterTeleport(
        RunningNpcTask task,
        NpcTeleportGate gate)
    {
        if (task == null ||
            task.npc == null)
        {
            return;
        }

        Vector3 refreshedTarget =
            ResolveTaskRecoveryTarget(
                task,
                task.travelWatchdogTarget);

        task.travelWatchdogStage = task.stage;
        task.travelWatchdogTarget = refreshedTarget;
        task.travelWatchdogLastPosition = task.npc.transform.position;
        task.travelStageStartedAt = Time.time;
        task.travelLastProgressAt = Time.time;
        task.travelLastDistanceToTarget =
            Vector2.Distance(
                task.npc.transform.position,
                refreshedTarget);
        task.maxTravelDuration =
            ComputeTravelWatchdogDuration(
                task.npc,
                task.travelLastDistanceToTarget);

        Debug.LogWarning(
            "[NpcTaskProvider] Re-arm travel after teleport npc=" +
            task.npc.name +
            " stage=" +
            task.stage +
            " gate=" +
            (gate != null ? gate.name : "null") +
            " target=" +
            refreshedTarget +
            " retries=" +
            task.travelRetryCount);
    }

    bool UpdateTaskTravelWatchdog(
        RunningNpcTask task,
        Vector3 target,
        float arriveThreshold,
        NpcMapZone? forcedTargetZone,
        string context)
    {
        if (task == null ||
            task.npc == null)
        {
            return false;
        }

        float distanceToTarget =
            Vector2.Distance(
                task.npc.transform.position,
                target);

        if (!task.travelWatchdogArmed ||
            task.travelWatchdogStage != task.stage)
        {
            ArmTaskTravelWatchdog(task, target);
            distanceToTarget = task.travelLastDistanceToTarget;
        }
        else
        {
            // A moving monster or a refreshed clear approach point is still
            // the same travel stage. Re-arming here used to erase both the
            // retry count and the hard stage deadline every frame.
            task.travelWatchdogTarget = target;
        }

        if (distanceToTarget <= arriveThreshold)
        {
            task.travelLastProgressAt = Time.time;
            task.travelLastDistanceToTarget = distanceToTarget;
            task.travelWatchdogLastPosition = task.npc.transform.position;
            return false;
        }

        float progressEpsilon =
            Mathf.Max(0.01f, taskTravelProgressEpsilon);
        float movedSinceProgress = Vector2.Distance(
            task.npc.transform.position,
            task.travelWatchdogLastPosition);
        bool madeProgress =
            distanceToTarget <=
                task.travelLastDistanceToTarget - progressEpsilon ||
            movedSinceProgress >= progressEpsilon;
        if (madeProgress)
        {
            task.travelLastProgressAt = Time.time;
            task.travelLastDistanceToTarget = distanceToTarget;
            task.travelWatchdogLastPosition = task.npc.transform.position;
        }

        if (Time.time - task.travelStageStartedAt >
            Mathf.Max(taskTravelMinStageDuration, task.maxTravelDuration))
        {
            return CancelStuckTask(
                task,
                context + " stageTimeout");
        }

        if (madeProgress)
        {
            return false;
        }

        if (Time.time - task.travelLastProgressAt <
            Mathf.Max(0.25f, taskTravelNoProgressTimeout))
        {
            return false;
        }

        if (task.travelRetryCount >= Mathf.Max(0, taskTravelMaxRecoveries))
        {
            return CancelStuckTask(
                task,
                context + " noProgress");
        }

        RecoverTaskTravel(task, target, forcedTargetZone, context);
        return true;
    }

    void RecoverTaskTravel(
        RunningNpcTask task,
        Vector3 target,
        NpcMapZone? forcedTargetZone,
        string context)
    {
        if (task == null ||
            task.npc == null)
        {
            return;
        }

        task.travelRetryCount++;

        Vector3 currentPosition = task.npc.transform.position;
        Vector3 clearCurrentPosition =
            GetClearTaskPositionNear(
                currentPosition,
                task.npc);

        if (Vector2.Distance(currentPosition, clearCurrentPosition) > 0.02f)
        {
            task.npc.transform.position = clearCurrentPosition;

            Rigidbody2D rb = task.npc.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = clearCurrentPosition;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        Vector3 recoveryTarget =
            ResolveTaskRecoveryTarget(task, target);

        task.travelWatchdogTarget = recoveryTarget;
        task.travelWatchdogLastPosition = task.npc.transform.position;
        task.travelLastDistanceToTarget =
            Vector2.Distance(
                task.npc.transform.position,
                recoveryTarget);
        task.travelLastProgressAt = Time.time;
        task.maxTravelDuration =
            ComputeTravelWatchdogDuration(
                task.npc,
                task.travelLastDistanceToTarget);

        MoveNpc(
            task.npc,
            recoveryTarget,
            forcedTargetZone);

        Debug.LogWarning(
            "[NpcTaskProvider] Recover travel npc=" +
            task.npc.name +
            " stage=" +
            task.stage +
            " context=" +
            context +
            " retry=" +
            task.travelRetryCount +
            " target=" +
            recoveryTarget +
            " forcedZone=" +
            (forcedTargetZone.HasValue
                ? forcedTargetZone.Value.ToString()
                : "None"));
    }

    Vector3 ResolveTaskRecoveryTarget(
        RunningNpcTask task,
        Vector3 fallbackTarget)
    {
        if (task == null ||
            task.npc == null)
        {
            return fallbackTarget;
        }

        switch (task.stage)
        {
            case TavernTaskStage.GoingToCounter:
                task.counterPosition = GetCounterPosition(task.npc);
                return ResolveActiveCounterTradePosition(
                    task.npc,
                    task.counterPosition);

            case TavernTaskStage.GoingToBoard:
                task.boardPosition = GetBoardPosition(task.npc);
                return task.boardPosition;

            case TavernTaskStage.ReturningToProvider:
            case TavernTaskStage.ReturningToTurnIn:
                task.providerPosition = GetProviderPositionFor(task.npc);
                return task.providerPosition;

            case TavernTaskStage.GoingToWork:
            case TavernTaskStage.WaitingForTargetRespawn:
                if (IsEscortTask(task))
                {
                    return !task.escortDepartedFromCompanion
                        ? GetEscortGreetingPosition(task)
                        : GetEscortCompletionGreetingPosition(task);
                }

                if (IsPatrolTask(task))
                {
                    task.patrolEndPosition =
                        task.patrolEndPosition != Vector3.zero
                        ? task.patrolEndPosition
                        : GetPatrolEndPosition(task.offer);
                    return task.patrolEndPosition;
                }

                if (task.targetPickup != null)
                {
                    task.workPosition = task.targetPickup.transform.position;
                    return GetClearTaskPositionNear(
                        task.workPosition,
                        task.npc);
                }

                if (task.targetLootPickup != null)
                {
                    task.workPosition = task.targetLootPickup.transform.position;
                    return GetClearTaskPositionNear(
                        task.workPosition,
                        task.npc);
                }

                if (task.targetMonster != null)
                {
                    task.workPosition = task.targetMonster.transform.position;
                    return task.workPosition;
                }

                task.workPosition = GetWorkPosition(task.offer);
                return GetClearTaskPositionNear(
                    task.workPosition,
                    task.npc);
        }

        return GetClearTaskPositionNear(fallbackTarget, task.npc);
    }

    bool CancelStuckTask(
        RunningNpcTask task,
        string reason)
    {
        int index = task != null
            ? runningTasks.IndexOf(task)
            : -1;
        if (index < 0)
        {
            return true;
        }

        Debug.LogWarning(
            "[NpcTaskProvider] Cancel stuck task npc=" +
            (task.npc != null ? task.npc.name : "null") +
            " stage=" +
            task.stage +
            " reason=" +
            reason +
            " target=" +
            task.travelWatchdogTarget +
            " retries=" +
            task.travelRetryCount);

        FinishTask(index, false);
        return true;
    }

    void ArmMealTravelWatchdog(
        RunningTavernMeal meal,
        Vector3 target)
    {
        if (meal == null ||
            meal.npc == null)
        {
            return;
        }

        meal.travelWatchdogArmed = true;
        meal.travelWatchdogTarget = target;
        meal.travelWatchdogLastPosition = meal.npc.transform.position;
        meal.travelStageStartedAt = Time.time;
        meal.travelLastProgressAt = Time.time;
        meal.travelLastDistanceToTarget =
            Vector2.Distance(
                meal.npc.transform.position,
                target);
        meal.maxTravelDuration =
            ComputeTravelWatchdogDuration(
                meal.npc,
                meal.travelLastDistanceToTarget);
        meal.travelRetryCount = 0;
    }

    void DisarmMealTravelWatchdog(RunningTavernMeal meal)
    {
        if (meal == null)
        {
            return;
        }

        meal.travelWatchdogArmed = false;
        meal.travelLastDistanceToTarget = float.PositiveInfinity;
        meal.maxTravelDuration = 0f;
    }

    bool UpdateMealTravelWatchdog(
        RunningTavernMeal meal,
        Vector3 target,
        float arriveThreshold)
    {
        if (meal == null ||
            meal.npc == null)
        {
            return false;
        }

        float distanceToTarget =
            Vector2.Distance(
                meal.npc.transform.position,
                target);

        if (!meal.travelWatchdogArmed ||
            Vector2.Distance(meal.travelWatchdogTarget, target) >
                Mathf.Max(arriveDistance, 0.25f))
        {
            ArmMealTravelWatchdog(meal, target);
            distanceToTarget = meal.travelLastDistanceToTarget;
        }

        if (distanceToTarget <= arriveThreshold)
        {
            meal.travelLastProgressAt = Time.time;
            meal.travelLastDistanceToTarget = distanceToTarget;
            meal.travelWatchdogLastPosition = meal.npc.transform.position;
            return false;
        }

        if (distanceToTarget <=
            meal.travelLastDistanceToTarget - Mathf.Max(0.01f, taskTravelProgressEpsilon))
        {
            meal.travelLastProgressAt = Time.time;
            meal.travelLastDistanceToTarget = distanceToTarget;
            meal.travelWatchdogLastPosition = meal.npc.transform.position;
            return false;
        }

        if (Time.time - meal.travelStageStartedAt >
                Mathf.Max(taskTravelMinStageDuration, meal.maxTravelDuration) ||
            ((Time.time - meal.travelLastProgressAt) >=
                Mathf.Max(0.25f, taskTravelNoProgressTimeout) &&
             meal.travelRetryCount >= Mathf.Max(0, taskTravelMaxRecoveries)))
        {
            int mealIndex = runningMeals.IndexOf(meal);
            if (mealIndex >= 0)
            {
                Debug.LogWarning(
                    "[NpcTaskProvider] Cancel stuck meal npc=" +
                    meal.npc.name +
                    " target=" +
                    target +
                    " retries=" +
                    meal.travelRetryCount);
                FinishMeal(mealIndex, false);
            }
            return true;
        }

        if (Time.time - meal.travelLastProgressAt >=
            Mathf.Max(0.25f, taskTravelNoProgressTimeout))
        {
            meal.travelRetryCount++;
            meal.npc.transform.position =
                GetClearTaskPositionNear(
                    meal.npc.transform.position,
                    meal.npc);
            meal.travelWatchdogTarget =
                GetClearTaskPositionNear(
                    target,
                    meal.npc);
            meal.travelLastProgressAt = Time.time;
            meal.travelLastDistanceToTarget =
                Vector2.Distance(
                    meal.npc.transform.position,
                    meal.travelWatchdogTarget);
            meal.maxTravelDuration =
                ComputeTravelWatchdogDuration(
                    meal.npc,
                    meal.travelLastDistanceToTarget);
        }

        return false;
    }

    float ComputeTravelWatchdogDuration(
        GameObject npc,
        float distanceToTarget)
    {
        float speed =
            NpcRoleUtility.GetMoveSpeed(
                npc,
                fallbackMoveSpeed);
        float expectedDuration =
            (distanceToTarget + Mathf.Max(arriveDistance, 0.5f)) /
            Mathf.Max(0.1f, speed);

        return Mathf.Clamp(
            expectedDuration * Mathf.Max(1f, taskTravelDurationMultiplier),
            Mathf.Max(1f, taskTravelMinStageDuration),
            Mathf.Max(taskTravelMinStageDuration, taskTravelMaxStageDuration));
    }

    void ClearTaskReservations(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null)
        {
            return;
        }

        if (task.targetPickup != null)
        {
            task.targetPickup.ClearReservation(task.npc);
        }

        if (task.targetLootPickup != null)
        {
            task.targetLootPickup.ClearReservation(task.npc);
        }
    }

    Vector3 GetProviderPosition()
    {
        if (keepProviderStationary)
        {
            if (providerStandPoint != null)
            {
                return providerStandPoint.position;
            }

            return stationaryPosition;
        }

        return providerPoint != null
            ? providerPoint.position
            : transform.position;
    }

    Vector3 GetClearTaskPositionNear(Vector3 position, GameObject npc)
    {
        position.z = transform.position.z;
        if (!IsTaskPositionBlocked(position, npc, null, true))
        {
            return position;
        }

        if (TryFindClearTaskPositionInBox(position, npc, out Vector3 boxPosition))
        {
            return boxPosition;
        }

        float baseRadius = Mathf.Max(arriveDistance, providerVisitorStandRadius, 0.45f);
        for (int radiusStep = 0; radiusStep < 6; radiusStep++)
        {
            float radius = baseRadius + radiusStep * 0.25f;
            for (int angleStep = 0; angleStep < 16; angleStep++)
            {
                float angle =
                    (angleStep / 16f) * Mathf.PI * 2f +
                    radiusStep * 0.31f;
                Vector3 candidate =
                    position +
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

                bool candidateBlocked =
                    IsTaskPositionBlocked(candidate, npc, null, true);
                bool hasBoxCandidate =
                    candidateBlocked &&
                    TryFindClearTaskPositionInBox(candidate, npc, out boxPosition);

                if (!candidateBlocked || hasBoxCandidate)
                {
                    return hasBoxCandidate ? boxPosition : candidate;
                }
            }
        }

        return position;
    }

    bool IsTaskPositionBlocked(
        Vector3 position,
        GameObject npc,
        Collider2D allowedCollider = null,
        bool blockNpcBodies = false)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                Mathf.Max(0.25f, arriveDistance));

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.isTrigger ||
                hit == allowedCollider ||
                (npc != null && hit.transform.IsChildOf(npc.transform)))
            {
                continue;
            }

            if (!blockNpcBodies &&
                (hit.GetComponentInParent<VillagerAI>() != null ||
                 hit.GetComponentInParent<SmartNpcAI>() != null ||
                 hit.GetComponentInParent<NpcMapMover2D>() != null))
            {
                continue;
            }

            if (IsTaskInteractionCollider(hit))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    bool IsTaskInteractionCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.GetComponentInParent<InteriorCameraFocus>() != null)
        {
            return true;
        }

        NpcInteractionPoint interactionPoint =
            hit.GetComponentInParent<NpcInteractionPoint>();
        if (interactionPoint != null &&
            (hit.transform == interactionPoint.transform ||
            hit.transform.IsChildOf(interactionPoint.transform)))
        {
            return true;
        }

        NpcCounterBroker broker =
            hit.GetComponentInParent<NpcCounterBroker>();
        if (broker != null &&
            broker.customerPoint != null)
        {
            Collider2D customerZone =
                broker.GetCustomerZoneCollider();
            if (customerZone != null &&
                hit == customerZone)
            {
                return true;
            }
        }

        return IsProviderPointCollider(hit, counterPoint) ||
            IsProviderPointCollider(hit, taskBoardPoint) ||
            IsProviderPointCollider(hit, providerPoint) ||
            IsProviderPointCollider(hit, providerStandPoint);
    }

    bool IsProviderPointCollider(Collider2D hit, Transform point)
    {
        if (hit == null ||
            point == null)
        {
            return false;
        }

        return hit.transform == point ||
            hit.transform.IsChildOf(point);
    }

    bool TryFindClearTaskPositionInBox(
        Vector3 seed,
        GameObject npc,
        out Vector3 position)
    {
        position = seed;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = GetComponentInParent<BoxCollider2D>();
        }

        if (box == null || !box.enabled)
        {
            return false;
        }

        Bounds bounds = box.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float margin = Mathf.Max(0.05f, Mathf.Min(extents.x, extents.y) * 0.12f);
        float startX = center.x - extents.x + margin;
        float endX = center.x + extents.x - margin;
        float startY = center.y - extents.y + margin;
        float endY = center.y + extents.y - margin;

        float stepSize = Mathf.Max(
            0.18f,
            Mathf.Min(providerVisitorStandRadius, Mathf.Min(extents.x, extents.y) * 0.35f));
        float stepX = stepSize;
        float stepY = stepSize;

        Vector3 best = seed;
        float bestDistance = float.PositiveInfinity;

        for (float y = startY; y <= endY; y += stepY)
        {
            for (float x = startX; x <= endX; x += stepX)
            {
                Vector3 candidate = new Vector3(x, y, transform.position.z);
                if (!bounds.Contains(candidate))
                {
                    continue;
                }

                if (IsTaskPositionBlocked(candidate, npc, box, true))
                {
                    continue;
                }

                float distance = (candidate - seed).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }

        if (bestDistance < float.PositiveInfinity)
        {
            position = best;
            return true;
        }

        return false;
    }

}
