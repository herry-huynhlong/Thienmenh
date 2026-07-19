using UnityEngine;

// Broker routing, cultivation-zone travel, teleport-route recovery, and map-teleport restoration.
public partial class SmartNpcAI
{
    bool TryHandleBrokerPillPurchaseIfReady(
        NpcCounterBroker broker,
        Vector3 approachPosition,
        float extraDistance = 0f)
    {
        if (broker == null ||
            !broker.receiveAllNpcRequests)
        {
            return false;
        }

        float allowedDistance =
            Mathf.Max(0.5f, broker.CustomerServiceRadius) +
            Mathf.Max(0f, extraDistance);
        bool atBroker =
            broker.IsCustomerAtCounter(gameObject) ||
            Vector2.Distance(transform.position, approachPosition) <=
            allowedDistance;

        if (!atBroker)
        {
            return false;
        }

        if (!EnsureTradeAgentReady())
        {
            return false;
        }

        int pillCountBefore = CountOwnedPillItems();
        bool hadPillBefore = HasAvailablePills();

        if (broker.TryTradeWithNpc(tradeAgent))
        {
            int pillCountAfter = CountOwnedPillItems();
            if (pillCountAfter > pillCountBefore)
            {
                pill += pillCountAfter - pillCountBefore;
            }

            ClearTravelTargetsAndStop();
            if (HasAvailablePills() || hadPillBefore)
            {
                ClearSmartTaskIfGoal(SmartAITaskGoal.NeedPotion);
                nextNeedPotionRetryTime = 0f;
                actionTimer = Mathf.Max(0.05f, thinkDelay * 0.25f);
                currentAction = NpcText.Action("idle");
            }
            else
            {
                DeferNeedPotionRetry(
                    NpcText.Action("checkedVanBaoLau"));
            }
            return true;
        }

        DeferNeedPotionRetry(
            NpcText.Action("checkedVanBaoLau"));
        return true;
    }

    Vector3 ResolveBrokerApproachPosition(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return transform.position;
        }

        if (currentAction == NpcText.Action("goVanBaoLauBroker") &&
            hasWanderTarget &&
            IsBrokerApproachPositionUsable(wanderTarget, broker))
        {
            return wanderTarget;
        }

        Vector3 preferred = broker.GetCustomerPositionFor(gameObject);
        if (TryFindClearBrokerApproachPosition(
                preferred,
                broker,
                out Vector3 clearApproach))
        {
            if (currentAction == NpcText.Action("goVanBaoLauBroker"))
            {
                wanderTarget = clearApproach;
                hasWanderTarget = true;
            }

            return clearApproach;
        }

        return preferred;
    }

    bool TryFindClearBrokerApproachPosition(
        Vector3 preferred,
        NpcCounterBroker broker,
        out Vector3 position)
    {
        position = preferred;
        if (IsBrokerApproachPositionUsable(preferred, broker))
        {
            return true;
        }

        if (TryFindClearPointNear(preferred, out Vector3 clearPoint, false) &&
            IsBrokerApproachPositionUsable(clearPoint, broker))
        {
            position = clearPoint;
            return true;
        }

        return false;
    }

    bool IsBrokerApproachPositionUsable(
        Vector3 position,
        NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return false;
        }

        position.z = transform.position.z;
        if (!IsMoveTargetFeasible(position))
        {
            return false;
        }

        Vector3 customerPosition = broker.CustomerPosition;
        customerPosition.z = position.z;

        float allowedDistance =
            Mathf.Max(
                broker.CustomerServiceRadius,
                broker.customerArriveDistance,
                escapeTargetReachDistance) +
            Mathf.Max(GetBodyClearRadius(), 0.2f);

        return Vector2.Distance(position, customerPosition) <=
            allowedDistance;
    }

    NpcMapZone? ResolveBrokerTargetZone(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return null;
        }

        Transform brokerTarget =
            broker.customerPoint != null
                ? broker.customerPoint
                : broker.transform;

        NpcMapZone? targetZone =
            NpcMapNavigator.GetDestinationZone(brokerTarget);
        if (targetZone.HasValue)
        {
            return targetZone;
        }

        NpcMapArea brokerArea =
            NpcMapArea.FindArea(broker.CustomerPosition);
        if (brokerArea != null)
        {
            return brokerArea.zone;
        }

        return NpcMapNavigator.ResolveActorZone(broker.gameObject);
    }

    NpcMapZone? ResolveTransformZone(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        NpcMapZone? destinationZone =
            NpcMapNavigator.GetDestinationZone(target);
        if (destinationZone.HasValue)
        {
            return destinationZone;
        }

        NpcMapArea area = NpcMapArea.FindArea(target.position);
        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(target.position);
        }

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    NpcMapZone? ResolveCultivationPreferredZone()
    {
        NpcMapZone? zone =
            ResolveTransformZone(cultivationPoint);
        if (zone.HasValue)
        {
            return zone;
        }

        return ResolveTransformZone(homePoint);
    }

    Vector3 GetCultivationZoneAnchorPosition()
    {
        if (cultivationPoint != null)
        {
            return cultivationPoint.position;
        }

        if (homePoint != null)
        {
            return homePoint.position;
        }

        return transform.position;
    }

    bool TryGetCultivationAreaPosition(
        NpcMapZone? preferredZone,
        bool requirePreferredZone,
        out Vector3 cultivationPosition,
        out NpcMapZone? cultivationZone)
    {
        if (!NpcLocationArea.TryGetPosition(
                gameObject,
                NpcScheduleActivity.Cultivate,
                VillagerJob.None,
                NpcLocationPurpose.Cultivation,
                preferredZone,
                null,
                transform.position,
                out cultivationPosition,
                out cultivationZone))
        {
            return false;
        }

        cultivationPosition =
            ResolveCultivationStandPosition(cultivationPosition);

        if (requirePreferredZone &&
            preferredZone.HasValue &&
            (!cultivationZone.HasValue ||
            cultivationZone.Value != preferredZone.Value))
        {
            return false;
        }

        return true;
    }

    Vector3 ResolveCultivationStandPosition(Vector3 basePosition)
    {
        basePosition.z = transform.position.z;

        int slotIndex =
            Mathf.Abs(
                UnityObjectIdUtility.GetRuntimeId(gameObject)) % 8;
        float angle =
            (slotIndex / 8f) * Mathf.PI * 2f;
        float radius =
            Mathf.Max(
                targetClearRadius * 2.5f,
                escapeTargetReachDistance * 1.2f,
                0.8f);
        Vector3 preferred =
            basePosition +
            new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f) * radius;

        if (TryFindClearPointNear(preferred, out Vector3 clearPoint, false))
        {
            clearPoint.z = basePosition.z;
            return clearPoint;
        }

        return basePosition;
    }

    bool TryResolveCultivationZoneEntryPosition(
        NpcMapZone preferredZone,
        out Vector3 cultivationPosition)
    {
        NpcMapArea destinationArea =
            NpcMapArea.FindNearestAreaInZone(
                preferredZone,
                GetCultivationZoneAnchorPosition());
        if (destinationArea == null)
        {
            cultivationPosition = Vector3.zero;
            return false;
        }

        cultivationPosition =
            destinationArea.ClosestPoint(
                GetCultivationZoneAnchorPosition());
        return true;
    }

    bool TryGetCultivationZoneMismatch(
        out NpcMapZone preferredZone,
        out NpcMapZone currentZone)
    {
        preferredZone = default;
        currentZone = default;

        NpcMapZone? resolvedPreferredZone =
            ResolveCultivationPreferredZone();
        NpcMapZone? resolvedCurrentZone =
            NpcMapNavigator.ResolveActorZone(gameObject);
        if (!resolvedPreferredZone.HasValue ||
            !resolvedCurrentZone.HasValue ||
            resolvedPreferredZone.Value == resolvedCurrentZone.Value)
        {
            return false;
        }

        preferredZone = resolvedPreferredZone.Value;
        currentZone = resolvedCurrentZone.Value;
        return true;
    }

    bool TryHandleBrokerArrivalFromMovement(Vector3 desiredTarget)
    {
        if (currentAction != NpcText.Action("goVanBaoLauBroker"))
        {
            return false;
        }

        NpcCounterBroker broker =
            NpcCounterBroker.FindBestBrokerForNpc(gameObject);
        if (broker == null ||
            !broker.receiveAllNpcRequests)
        {
            return false;
        }

        Vector3 brokerApproach =
            ResolveBrokerApproachPosition(broker);
        float approachDistance =
            Vector2.Distance(
                transform.position,
                brokerApproach);
        float desiredDistance =
            Vector2.Distance(
                transform.position,
                desiredTarget);
        float arriveDistance =
            Mathf.Max(
                escapeTargetReachDistance,
                broker.CustomerServiceRadius);

        if (approachDistance > arriveDistance &&
            desiredDistance > arriveDistance)
        {
            return false;
        }

        if (TryHandleBrokerPillPurchaseIfReady(
                broker,
                brokerApproach,
                0.15f))
        {
            DebugFlow(
                "Trade",
                "Handled broker arrival approachDist=" +
                approachDistance.ToString("0.00") +
                " desiredDist=" +
                desiredDistance.ToString("0.00") +
                " arrive=" +
                arriveDistance.ToString("0.00"));
            return true;
        }

        return false;
    }

    bool TryRestoreStaleTravelIntent()
    {
        bool staleProviderCounterAction =
            currentAction == NpcText.Action("goCounterTrade") ||
            currentAction == NpcText.Action("checkingCounterTrade");
        if (staleProviderCounterAction &&
            !NpcTaskProvider.IsNpcBusyWithAnyProvider(gameObject))
        {
            currentTarget = null;
            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            movementPausedUntil = 0f;
            crowdYieldUntil = 0f;
            blockedMoveTimer = 0f;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;

            string restoredAction =
                ResolvePostTeleportTravelAction();
            if (string.IsNullOrWhiteSpace(restoredAction) ||
                restoredAction == NpcText.Action("goCounterTrade") ||
                restoredAction == NpcText.Action("checkingCounterTrade"))
            {
                restoredAction = NpcText.Action("idle");
            }

            currentAction = restoredAction;
            DebugFlow(
                "MoveRoute",
                "Cleared stale provider counter action -> " +
                currentAction);
            return false;
        }

        if (currentTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget)
        {
            return false;
        }

        if (TryRecoverTeleportRouteActionWithoutTarget())
        {
            return true;
        }

        bool isRecoverableTravelAction =
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goVanBaoLauTask") ||
            currentAction == NpcText.Action("goCounterTrade") ||
            currentAction == NpcText.Action("goVanBaoLauBroker");
        if (!isRecoverableTravelAction)
        {
            return false;
        }

        Vector2 toWanderTarget =
            (Vector2)(wanderTarget - transform.position);
        float minRestoreDistance =
            Mathf.Max(0.35f, escapeTargetReachDistance);
        if (wanderTarget == Vector3.zero ||
            toWanderTarget.sqrMagnitude <=
            minRestoreDistance * minRestoreDistance)
        {
            return false;
        }

        hasWanderTarget = true;
        currentTarget = null;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        blockedMoveTimer = 0f;
        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;
        DebugFlow(
            "MoveRoute",
            "Restored stale travel action=" +
            currentAction +
            " wanderTarget=" +
            wanderTarget);
        return true;
    }

    bool ShouldBypassCrowdBlockForTravelAction()
    {
        return IsTeleportRouteAction(currentAction) ||
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goVanBaoLauBroker") ||
            currentAction == NpcText.Action("goVanBaoLauTask");
    }

    bool TryForceTeleportRouteProgress(
        Vector3 moveTarget,
        NpcMapZone? currentZone)
    {
        if (!currentZone.HasValue)
        {
            return false;
        }

        float forceDistance =
            Mathf.Max(
                1.05f,
                targetClearRadius * 4f,
                moveSpeed * 0.5f);

        if (Vector2.Distance(transform.position, moveTarget) > forceDistance)
        {
            return false;
        }

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                !gate.TryGetTeleportRouteForZone(
                    currentZone.Value,
                    out Vector3 gateApproach,
                    out _,
                    out _) ||
                Vector2.Distance(
                    gateApproach,
                    moveTarget) > 0.2f)
            {
                continue;
            }

            if (!gate.TryForceNpcUse(gameObject))
            {
                return false;
            }

            blockedMoveTimer = 0f;
            stuckMoveTimer = 0f;
            unstuckRecoveryAttempts = 0;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            DebugFlow(
                "MoveRoute",
                "Forced teleport near gate moveTarget=" +
                moveTarget +
                " gate=" +
                gate.name +
                " currentZone=" +
                currentZone.Value);
            return true;
        }

        return false;
    }

    bool TryHandleGatherArrivalFromMovement(
        Vector3 desiredTarget,
        bool blockedRecovery = false)
    {
        if (currentAction != NpcText.Action("gatherResource") ||
            currentTarget == null)
        {
            return false;
        }

        if (resourceGatherer == null)
        {
            resourceGatherer = GetComponent<NpcResourceGatherer>();
        }

        if (resourceGatherer == null ||
            !resourceGatherer.enabled)
        {
            return false;
        }

        float extraDistance = blockedRecovery
            ? Mathf.Max(targetClearRadius, escapeTargetReachDistance, 0.18f)
            : 0.02f;
        if (!resourceGatherer.TryHandleSmartNpcBlockedArrival(extraDistance))
        {
            return false;
        }

        DebugFlow(
            "Gather",
            (blockedRecovery
                ? "Handled blocked gather arrival"
                : "Handled gather arrival") +
            " target=" +
            currentTarget.name +
            " desired=" +
            desiredTarget +
            " dist=" +
            Vector2.Distance(
                transform.position,
                currentTarget.position).ToString("0.00"));
        return true;
    }

    bool ShouldSuspendAutonomousDamageResponse()
    {
        return NpcTaskProvider.IsNpcBusyWithAnyProvider(gameObject);
    }

    bool TryRecoverBlockedTeleportRoute(
        Vector3 blockedTarget,
        NpcMapZone? currentZone,
        NpcMapArea currentArea)
    {
        if (!currentZone.HasValue ||
            !IsTeleportRouteAction(currentAction))
        {
            return false;
        }

        float recoveryDistance =
            Mathf.Max(
                16f,
                moveSpeed * 8f);

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                !gate.TryGetTeleportRouteForZone(
                    currentZone.Value,
                    out Vector3 gateApproach,
                    out _,
                    out _) ||
                Vector2.Distance(
                    gateApproach,
                    blockedTarget) > 0.35f)
            {
                continue;
            }

            NpcMapArea gateArea =
                NpcMapArea.FindArea(gateApproach);
            if (currentArea != null &&
                gateArea != null &&
                gateArea != currentArea)
            {
                continue;
            }

            if (Vector2.Distance(
                    transform.position,
                    gateApproach) > recoveryDistance)
            {
                continue;
            }

            if (!gate.TryForceNpcUse(gameObject))
            {
                continue;
            }

            blockedMoveTimer = 0f;
            stuckMoveTimer = 0f;
            unstuckRecoveryAttempts = 0;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            DebugFlow(
                "Move",
                "Recovered blocked teleport route via " +
                gate.name +
                " target=" +
                blockedTarget);
            return true;
        }

        return false;
    }

    void OnDisable()
    {
        UpdateCultivationEffect(false);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        UpdateCultivationEffect(false);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        NpcCollisionRegistry.Unregister(this);
        NpcMapNavigator.ClearNpcState(gameObject);
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        DebugFlow("Teleport", "Teleported gate=" +
            (gateObject != null ? gateObject.name : "null") +
            " source=" +
            (gateObject != null ? "gate" : "fallback") +
            " pos=" +
            transform.position +
            " area=" +
            (NpcMapArea.FindArea(transform.position) != null
                ? NpcMapArea.FindArea(transform.position).name
                : "null"));

        bool preserveTravelState =
            currentMonsterTarget != null ||
            currentTarget != null ||
            hasWanderTarget ||
            waitingOutsideTreasureLightning ||
            hasTreasureWaitPosition ||
            treasureHuntTarget != null ||
            treasureHuntItem != null ||
            hasHomeReturnTarget;

        if (!NpcMapBehaviorPolicy.AllowsNormalWorldTravel(gameObject) &&
            IsNormalWorldTravelAction(currentAction))
        {
            currentMonsterTarget = null;
            ClearMonsterCombatState();
            ClearTaskProviderVisitState();
            ClearCultivationTravelState();
            ClearTravelTargetsAndStop();
            waitingOutsideTreasureLightning = false;
            hasTreasureWaitPosition = false;
            treasureWaitLowPowerSkirmish = false;
            treasureHuntTarget = null;
            treasureHuntItem = null;
            hasHomeReturnTarget = false;
            preserveTravelState = false;
            currentAction = NpcText.Action("idle");
        }

        spawnPosition = transform.position;
        lastUnstuckPosition = transform.position;
        if (!preserveTravelState)
        {
            hasWanderTarget = false;
            currentTarget = null;
            currentMonsterTarget = null;
            ClearMonsterCombatState();
            waitingOutsideTreasureLightning = false;
            hasTreasureWaitPosition = false;
            treasureWaitLowPowerSkirmish = false;
            treasureHuntTarget = null;
            treasureHuntItem = null;
            hasHomeReturnTarget = false;
        }

        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        postTeleportRecoveryUntil = Time.time + 0.35f;
        stuckMoveTimer = 0f;
        blockedMoveTimer = 0f;
        unstuckRecoveryAttempts = 0;
        actionTimer = 0f;
        thinkTimer = 0f;
        if (!preserveTravelState)
        {
            currentAction = NpcText.Action("idle");
        }

        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;
        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        NpcMapZone? resolvedZone =
            area != null
                ? area.zone
                : NpcMapNavigator.ResolveActorZone(gameObject);
        Vector3 clearReference = transform.position;

        if (gate != null)
        {
            Vector3 gateEntryPosition = gate.EntryPosition;
            Vector3 gateExitPosition = gate.ExitPosition;
            bool landedNearEntry =
                ((Vector2)(transform.position - gateEntryPosition)).sqrMagnitude <=
                ((Vector2)(transform.position - gateExitPosition)).sqrMagnitude;
            Vector3 landedSidePosition =
                landedNearEntry
                    ? gateEntryPosition
                    : gateExitPosition;
            Vector3 oppositeSidePosition =
                landedNearEntry
                    ? gateExitPosition
                    : gateEntryPosition;
            NpcMapZone landedZone =
                landedNearEntry
                    ? gate.fromZone
                    : gate.toZone;

            resolvedZone = landedZone;

            NpcMapNavigator.LockNpcZone(gameObject, landedZone, 3f);
            NpcMapNavigator.ReportNpcZone(gameObject, resolvedZone.Value);

            NpcMapArea resolvedArea =
                NpcMapNavigator.ResolveMapAreaAfterTeleport(
                    gameObject,
                    resolvedZone.Value,
                    transform.position);

            if (resolvedArea != null &&
                resolvedZone.HasValue &&
                resolvedArea.zone == resolvedZone.Value)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedArea.zone);
            }

            Vector2 awayFromGate =
                (Vector2)landedSidePosition -
                (Vector2)oppositeSidePosition;
            if (awayFromGate.sqrMagnitude <= 0.0001f)
            {
                awayFromGate =
                    (Vector2)transform.position -
                    (Vector2)landedSidePosition;
            }

            if (awayFromGate.sqrMagnitude <= 0.0001f)
            {
                awayFromGate = Vector2.up;
            }

            clearReference =
                landedSidePosition +
                (Vector3)(awayFromGate.normalized *
                Mathf.Max(
                    0.9f,
                    targetClearRadius * 3f,
                    moveSpeed * 0.35f));
        }
        else
        {
            NpcMapArea fallbackArea = NpcMapArea.FindArea(transform.position);
            if (fallbackArea != null)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, fallbackArea.zone);
            }
        }

        if (TryFindClearPointNear(clearReference, out Vector3 clearPoint, false))
        {
            transform.position = clearPoint;
            spawnPosition = clearPoint;
            lastUnstuckPosition = clearPoint;

            if (rb != null)
            {
                rb.position = clearPoint;
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        UpdateCultivationEffect(false);
    }
}
