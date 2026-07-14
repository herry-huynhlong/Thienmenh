using UnityEngine;

// Cross-map travel intent, broker approach and the central movement update.
public partial class SmartNpcAI
{
    string ResolvePostTeleportTravelAction()
    {
        if (currentMonsterTarget != null)
        {
            return NpcText.Action("goHunt");
        }

        if (ShouldResumeCultivationTravel())
        {
            return NpcText.Action("goCultivatePoint");
        }

        if (currentSmartTask != null &&
            currentSmartTask.IsValid)
        {
            string taskAction =
                GetRuntimeActionForSmartTask(currentSmartTask);
            if (!string.IsNullOrWhiteSpace(taskAction) &&
                !IsTeleportRouteAction(taskAction))
            {
                return taskAction;
            }
        }

        if (scheduleSmartTask != null &&
            scheduleSmartTask.IsValid)
        {
            string scheduleTaskAction =
                GetRuntimeActionForSmartTask(scheduleSmartTask);
            if (!string.IsNullOrWhiteSpace(scheduleTaskAction) &&
                !IsTeleportRouteAction(scheduleTaskAction))
            {
                return scheduleTaskAction;
            }
        }

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        if (schedule != null &&
            schedule.enforceSchedule)
        {
            string scheduleAction =
                GetScheduleActionTextForDisplay(
                    schedule.CurrentActivity);
            if (!string.IsNullOrWhiteSpace(scheduleAction) &&
                !IsTeleportRouteAction(scheduleAction))
            {
                return scheduleAction;
            }
        }

        if (currentTarget != null ||
            hasWanderTarget)
        {
            return NpcText.Action("walkingRoad");
        }

        return NpcText.Action("idle");
    }

    bool TryRebuildPostTeleportTravelIntent(string restoredAction)
    {
        if (string.IsNullOrEmpty(restoredAction))
        {
            return false;
        }

        if (restoredAction == NpcText.Action("goCultivatePoint"))
        {
            if (currentTarget != null)
            {
                return false;
            }

            if (hasWanderTarget)
            {
                if (TryRefreshPendingCultivationTravelTarget())
                {
                    DebugFlow(
                        "MoveRoute",
                        "Refreshed cultivate travel after teleport restore");
                    return true;
                }

                return false;
            }

            ClearTravelTargetsAndStop();
            hasCultivationTarget = false;

            if (TryGoToCultivationPoint())
            {
                DebugFlow(
                    "MoveRoute",
                    "Rebuilt cultivate travel after teleport restore");
                return true;
            }

            CultivateNaturally();
            if (currentAction == NpcText.Action("cultivate") ||
                currentAction == NpcText.Action("cultivateAbsorbQi"))
            {
                DebugFlow(
                    "MoveRoute",
                    "Resumed cultivate action after teleport restore");
                return true;
            }

            return false;
        }

        if (restoredAction == NpcText.Action("goTaskProviderDaily"))
        {
            if (currentTarget != null ||
                hasWanderTarget)
            {
                return false;
            }

            ClearTravelTargetsAndStop();

            if (TryVisitTaskProvider())
            {
                DebugFlow(
                    "MoveRoute",
                    "Rebuilt task-provider travel after teleport restore");
                return true;
            }

            return false;
        }

        return false;
    }

    string GetRuntimeActionForSmartTask(SmartAITask task)
    {
        if (task == null ||
            !task.IsValid)
        {
            return string.Empty;
        }

        if (task.goal == SmartAITaskGoal.NeedPotion)
        {
            NpcCounterBroker broker = NpcCounterBroker.Active;
            return broker != null &&
                broker.receiveAllNpcRequests
                ? NpcText.Action("goVanBaoLauBroker")
                : NpcText.Action("goTavern");
        }

        if (task.goal == SmartAITaskGoal.Cultivate &&
            ShouldResumeCultivationTravel())
        {
            return NpcText.Action("goCultivatePoint");
        }

        return GetTaskActionTextForDisplay(task);
    }

    ItemInventory GetNpcItemInventory()
    {
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null &&
            tradeAgent != null)
        {
            inventory = tradeAgent.inventory;
        }

        return inventory;
    }

    bool IsCountedPillItem(StatItemData item)
    {
        return item != null &&
            item.itemType == ItemType.DanDuoc &&
            item.CanUseOn(gameObject);
    }

    int CountOwnedPillItems()
    {
        ItemInventory inventory = GetNpcItemInventory();
        if (inventory == null ||
            inventory.items == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < inventory.items.Count; i++)
        {
            ItemStack stack = inventory.items[i];
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !IsCountedPillItem(stack.item))
            {
                continue;
            }

            total += stack.amount;
        }

        return total;
    }

    bool HasAvailablePills()
    {
        if (pill > 0)
        {
            return true;
        }

        int ownedPills = CountOwnedPillItems();
        if (ownedPills > 0)
        {
            pill = Mathf.Max(pill, ownedPills);
            return true;
        }

        return false;
    }

    bool TryConsumeAvailablePill()
    {
        if (!HasAvailablePills())
        {
            return false;
        }

        ItemInventory inventory = GetNpcItemInventory();
        if (inventory != null &&
            inventory.items != null)
        {
            for (int i = 0; i < inventory.items.Count; i++)
            {
                ItemStack stack = inventory.items[i];
                if (stack == null ||
                    stack.item == null ||
                    stack.amount <= 0 ||
                    !IsCountedPillItem(stack.item))
                {
                    continue;
                }

                inventory.RemoveItem(stack.item, 1);
                break;
            }
        }

        pill = Mathf.Max(0, pill - 1);
        return true;
    }

    bool ShouldResumeCultivationTravel()
    {
        if (!canCultivate ||
            IsRestrictedMapSessionActive())
        {
            return false;
        }

        bool hasCultivateIntent =
            (currentSmartTask != null &&
            currentSmartTask.IsValid &&
            currentSmartTask.goal == SmartAITaskGoal.Cultivate) ||
            (scheduleSmartTask != null &&
            scheduleSmartTask.IsValid &&
            scheduleSmartTask.goal == SmartAITaskGoal.Cultivate);

        if (!hasCultivateIntent)
        {
            NpcScheduleController schedule =
                GetComponent<NpcScheduleController>();
            hasCultivateIntent =
                schedule != null &&
                schedule.enforceSchedule &&
                schedule.CurrentActivity == NpcScheduleActivity.Cultivate;
        }

        if (!hasCultivateIntent)
        {
            return false;
        }

        return IsCultivationTravelPending();
    }

    bool IsCultivationTravelPending()
    {
        float cultivationArriveDistance =
            GetCultivationArriveDistance();

        if (hasCultivationTarget)
        {
            return Vector2.Distance(
                       transform.position,
                       cultivationTarget) >
                cultivationArriveDistance;
        }

        if (!TryResolveCultivationTravelDestination(
                out _,
                out Vector3 targetPosition))
        {
            return false;
        }

        return Vector2.Distance(transform.position, targetPosition) >
            cultivationArriveDistance;
    }

    bool EnsureTradeAgentReady()
    {
        if (tradeAgent == null)
        {
            tradeAgent = GetComponent<NpcTradeAgent>();
        }

        if (tradeAgent == null)
        {
            tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
        }

        if (tradeAgent == null)
        {
            return false;
        }

        if (tradeAgent.inventory == null)
        {
            tradeAgent.inventory = GetComponent<ItemInventory>();
        }

        if (tradeAgent.inventory == null)
        {
            tradeAgent.inventory = gameObject.AddComponent<ItemInventory>();
        }

        return tradeAgent.inventory != null;
    }

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
        if (currentAction == NpcText.Action("goVanBaoLauBroker") &&
            hasWanderTarget)
        {
            return wanderTarget;
        }

        return broker != null
            ? broker.GetCustomerPositionFor(gameObject)
            : transform.position;
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
            Mathf.Abs(gameObject.GetInstanceID()) % 8;
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

        NpcCounterBroker broker = NpcCounterBroker.Active;
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

    void UpdateMovement()
    {
        if ((Time.time < movementPausedUntil ||
            Time.time < crowdYieldUntil) &&
            !hasEscapeTarget)
        {
            DebugFlow(
                "Move",
                "Paused movement pausedUntil=" +
                movementPausedUntil.ToString("0.00") +
                " crowdUntil=" +
                crowdYieldUntil.ToString("0.00"));

            if (!TryApplyNpcOverlapSeparation() && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        TryRestoreStaleTravelIntent();

        if (ShouldHoldCombatPosition())
        {
            if (visualAnimation != null &&
                currentMonsterTarget != null)
            {
                visualAnimation.SetFacingTarget(
                    currentMonsterTarget.transform.position);
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            blockedMoveTimer = 0f;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;
            DebugFlow(
                "Move",
                "Hold combat position target=" +
                (currentMonsterTarget != null
                    ? currentMonsterTarget.monsterName
                    : "null"));
            return;
        }

        // Hunt state and movement state can be updated by different systems
        // (schedule, help request and reservation). Restore the Transform
        // target if one of those systems preserved a live monster but cleared
        // currentTarget; otherwise the NPC says it is hunting while standing.
        if (currentTarget == null &&
            currentMonsterTarget != null &&
            currentMonsterTarget.currentHP > 0 &&
            !isRetreatingFromMonster &&
            (currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true)))
        {
            currentTarget = currentMonsterTarget.transform;
            hasWanderTarget = false;
            TryIgnoreCombatTargetCollision(currentTarget);
        }

        bool movingToTreasureWait =
            waitingOutsideTreasureLightning &&
            hasTreasureWaitPosition;
        bool holdPositionWithoutTarget =
            currentTarget == null &&
            !movingToTreasureWait &&
            Time.time >= postTeleportRecoveryUntil &&
            IsStationaryAction(currentAction) &&
            !hasWanderTarget &&
            !hasObstacleAvoidTarget &&
            !hasEscapeTarget;

        if (currentTarget == null &&
            !movingToTreasureWait &&
            holdPositionWithoutTarget)
        {
            string holdDebugSignature =
                "HoldWithoutTarget|" +
                currentAction + "|" +
                (currentSmartTask != null && currentSmartTask.IsValid
                    ? currentSmartTask.goal.ToString()
                    : "None");
            if (ShouldLogStateTransition(
                    ref lastMoveHoldDebugSignature,
                    ref lastMoveHoldDebugTime,
                    holdDebugSignature,
                    3.5f))
            {
                DebugFlow(
                    "Move",
                    "Hold without target action=" + currentAction);
            }

            bool suppressStationarySeparation =
                currentAction == NpcText.Action("buyPill") ||
                currentAction == NpcText.Action("checkedVanBaoLau");

            if (!suppressStationarySeparation &&
                TryApplyNpcOverlapSeparation())
            {
                return;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            hasEscapeTarget = false;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;

            return;
        }

        Vector3 desiredTarget;

        if (hasEscapeTarget)
        {
            desiredTarget = escapeTarget;
        }
        else if (movingToTreasureWait)
        {
            desiredTarget = treasureWaitPosition;
        }
        else if (currentTarget != null)
        {
            desiredTarget = GetApproachPosition(currentTarget);
            hasWanderTarget = false;

            if (currentTarget.GetComponentInParent<NpcTaskProvider>() != null ||
                currentAction == NpcText.Action("goTaskProviderDaily") ||
                currentAction == NpcText.Action("visitedTaskProvider"))
            {
                DebugFlow(
                    "Move",
                    "Approach provider target=" +
                    currentTarget.name +
                    " desired=" +
                    desiredTarget +
                    " pos=" +
                    transform.position +
                    " dist=" +
                    Vector2.Distance(transform.position, desiredTarget).ToString("0.00"));
            }
        }
        else if (hasWanderTarget)
        {
            desiredTarget = wanderTarget;
        }
        else
        {
            if (currentAction == NpcText.Action("goTaskProviderDaily"))
            {
                DebugFlow("Move", "Reached task provider route end");

                if (TryVisitTaskProvider())
                {
                    return;
                }

                currentAction = NpcText.Action("visitedTaskProvider");
                actionTimer = Mathf.Max(
                    actionTimer,
                    GameHoursToSeconds(0.2f));

                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (IsTeleportRouteAction(currentAction))
            {
                if (ShouldLogStateTransition(
                        ref lastMoveHoldDebugSignature,
                        ref lastMoveHoldDebugTime,
                        "TeleportRouteEnded|" + currentAction,
                        3.5f))
                {
                    DebugFlow(
                        "Move",
                        "Teleport route ended without follow target");
                }

                currentAction = NpcText.Action("idle");

                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                return;
            }

            // A hunt action is only meaningful while an actual hunt route or
            // target still exists.  Keeping this preserved action after all
            // of its movement state has been cleared leaves the NPC standing
            // forever while still reporting moving intent.
            if (currentAction == NpcText.Action("goHunt") &&
                currentMonsterTarget == null &&
                !hasWanderTarget &&
                !hasEscapeTarget &&
                !hasObstacleAvoidTarget)
            {
                currentAction = NpcText.Action("idle");
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (IsPreservedTravelAction(currentAction))
            {
                if (ShouldLogStateTransition(
                        ref lastMoveHoldDebugSignature,
                        ref lastMoveHoldDebugTime,
                        "PreservedTravel|" + currentAction,
                        3.5f))
                {
                    DebugFlow("Move", "Preserved travel action waiting");
                }

                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            stuckMoveTimer = 0f;
            lastUnstuckPosition = transform.position;

            if (!IsStationaryAction(currentAction))
            {
                currentAction = NpcText.Action("idle");
            }

            return;
        }

        if (TryHandleBrokerArrivalFromMovement(desiredTarget))
        {
            return;
        }

        if (TryHandleGatherArrivalFromMovement(desiredTarget))
        {
            return;
        }

        if (hasObstacleAvoidTarget)
        {
            if (Time.time >= obstacleAvoidUntil ||
                Vector2.Distance(transform.position, obstacleAvoidTarget) <=
                escapeTargetReachDistance ||
                !IsMoveTargetFeasible(obstacleAvoidTarget))
            {
                hasObstacleAvoidTarget = false;
            }
            else
            {
                desiredTarget = obstacleAvoidTarget;
            }
        }

        if (hasEscapeTarget)
        {
            if (Vector2.Distance(transform.position, escapeTarget) <=
                escapeTargetReachDistance)
            {
                hasEscapeTarget = false;
            }
            else
            {
                desiredTarget = escapeTarget;
            }
        }

        NpcCounterBroker activeBroker =
            currentAction == NpcText.Action("goVanBaoLauBroker")
                ? NpcCounterBroker.Active
                : null;
        NpcMapZone? forcedTargetZone =
            ResolveBrokerTargetZone(activeBroker);

        bool usingTeleportRoute;
        string routeAction;
        NpcRouteStatus routeStatus;
        Vector3 moveTarget =
            NpcMapNavigator.GetNextMoveTarget(
                gameObject,
                desiredTarget,
                forcedTargetZone,
                out usingTeleportRoute,
                out routeAction,
                out routeStatus);

        NpcMapArea currentArea = NpcMapArea.FindArea(transform.position);
        NpcMapArea desiredTargetArea = NpcMapArea.FindArea(desiredTarget);
        NpcMapZone? currentZone = NpcMapNavigator.ResolveActorZone(gameObject);
        NpcMapZone? desiredZone = forcedTargetZone ??
            (currentTarget != null
                ? NpcMapNavigator.GetDestinationZone(currentTarget)
                : (NpcMapZone?)null);
        if (!desiredZone.HasValue && desiredTargetArea != null)
        {
            desiredZone = desiredTargetArea.zone;
        }

        if (routeStatus == NpcRouteStatus.NoGate ||
            routeStatus == NpcRouteStatus.InvalidGate)
        {
            if (!string.IsNullOrEmpty(routeAction))
            {
                currentAction = routeAction;
            }

            StopNpcMovement();
            DebugFlow(
                "MoveRoute",
                "Blocked no route status=" +
                routeStatus +
                " currentZone=" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                " desiredZone=" +
                (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                " desiredTarget=" +
                desiredTarget +
                " target=" +
                (currentTarget != null ? currentTarget.name : "null"));
            return;
        }

        if (usingTeleportRoute)
        {
            string routeDebugSignature =
                "TeleportRoute|" +
                routeAction + "|" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                "|" +
                (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                "|" +
                (currentTarget != null ? currentTarget.name : "null") +
                "|" + hasWanderTarget;
            if (ShouldTraceRuntime() &&
                ShouldLogStateTransition(
                    ref lastRouteDebugSignature,
                    ref lastRouteDebugTime,
                    routeDebugSignature,
                    2.5f))
            {
                DebugFlow(
                    "MoveRoute",
                    "Teleport route action=" +
                    routeAction +
                    " currentZone=" +
                    (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                    " currentArea=" +
                    (currentArea != null ? currentArea.name : "null") +
                    " desiredZone=" +
                    (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                    " desiredArea=" +
                    (desiredTargetArea != null ? desiredTargetArea.name : "null") +
                    " moveTarget=" +
                    moveTarget +
                    " desiredTarget=" +
                    desiredTarget +
                    " target=" +
                    (currentTarget != null ? currentTarget.name : "null") +
                    " wander=" +
                    hasWanderTarget);
            }
        }
        else if (IsTeleportRouteAction(currentAction))
        {
            string restoredAction =
                ResolvePostTeleportTravelAction();

            if (TryRebuildPostTeleportTravelIntent(restoredAction))
            {
                return;
            }

            string routeRestoreSignature =
                "TeleportRestore|" +
                currentAction + "|" +
                restoredAction + "|" +
                (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                "|" +
                (desiredZone.HasValue ? desiredZone.Value.ToString() : "None");
            if (ShouldTraceRuntime() &&
                ShouldLogStateTransition(
                    ref lastRouteDebugSignature,
                    ref lastRouteDebugTime,
                    routeRestoreSignature,
                    2.5f))
            {
                DebugFlow(
                    "MoveRoute",
                    "Teleport action without route currentAction=" +
                    currentAction +
                    " restoredAction=" +
                    restoredAction +
                    " currentZone=" +
                    (currentZone.HasValue ? currentZone.Value.ToString() : "None") +
                    " currentArea=" +
                    (currentArea != null ? currentArea.name : "null") +
                    " desiredZone=" +
                    (desiredZone.HasValue ? desiredZone.Value.ToString() : "None") +
                    " desiredArea=" +
                    (desiredTargetArea != null ? desiredTargetArea.name : "null") +
                    " moveTarget=" +
                    moveTarget +
                    " desiredTarget=" +
                    desiredTarget +
                    " target=" +
                    (currentTarget != null ? currentTarget.name : "null") +
                    " wander=" +
                    hasWanderTarget);
            }

            currentAction = restoredAction;
        }

        if (usingTeleportRoute &&
            !string.IsNullOrEmpty(routeAction))
        {
            currentAction = routeAction;
        }

        if (usingTeleportRoute &&
            TryForceTeleportRouteProgress(moveTarget, currentZone))
        {
            return;
        }

        NpcMapArea spawnArea =
            NpcMapArea.FindArea(spawnPosition);
        NpcMapArea targetArea =
            NpcMapArea.FindArea(desiredTarget);
        bool targetInSpawnArea =
            spawnArea == null ||
            targetArea == null ||
            spawnArea.zone == targetArea.zone;

        bool isCultivationTravelRoute =
            currentTarget == cultivationPoint ||
            currentAction == NpcText.Action("goCultivatePoint");

        bool isAutonomousWorkRoute =
            currentAction == NpcText.Action("tradeSeek") ||
            currentAction == NpcText.Action("gatherResource") ||
            currentAction == NpcText.Action("goTaskProviderDaily") ||
            currentAction == NpcText.Action("goTavern") ||
            currentAction == NpcText.Action("buyPill") ||
            currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);

        bool hasActiveDirectedTarget =
            currentTarget != null ||
            currentMonsterTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget;

        float distanceFromSpawn =
            Vector2.Distance(
                transform.position,
                spawnPosition);

        if (!movingToTreasureWait &&
            treasureHuntTarget == null &&
            !usingTeleportRoute &&
            targetInSpawnArea &&
            !isCultivationTravelRoute &&
            !hasActiveDirectedTarget &&
            !isAutonomousWorkRoute &&
            distanceFromSpawn > maxRoamDistance)
        {
            DebugFlow(
                "Move",
                "Stopped by roam limit distance=" +
                distanceFromSpawn.ToString("0.00") +
                " max=" +
                maxRoamDistance.ToString("0.00"));

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;
            hasHomeReturnTarget = false;

            if (!IsStationaryAction(currentAction))
            {
                currentAction = NpcText.Action("idle");
            }

            return;
        }

        if (currentAction == NpcText.Action("goCultivatePoint") &&
            Vector2.Distance(transform.position, desiredTarget) <=
            GetCultivationArriveDistance())
        {
            if (TryRefreshPendingCultivationTravelTarget())
            {
                DebugFlow(
                    "Move",
                    "Refreshed cultivate route after intermediate target");
                return;
            }

            DebugFlow("Move", "Reached cultivate point");

            currentTarget = null;
            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            SyncCultivationEffect();
            DebugFlow("Move", "Arrived at cultivate point, start cultivate immediately");

            CultivateNaturally();
            return;
        }

        if (currentTarget == null &&
            hasWanderTarget &&
            Vector2.Distance(transform.position, desiredTarget) <= escapeTargetReachDistance)
        {
            DebugFlow("Move", "Reached wander target");

            hasWanderTarget = false;
            hasEscapeTarget = false;
            hasObstacleAvoidTarget = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (currentAction == NpcText.Action("goHunt"))
            {
                currentAction = NpcText.Action("idle");
            }

            return;
        }

        Vector2 direction =
            (moveTarget -
            transform.position).normalized;

        if (!useObstacleAvoidance)
        {
            if (!TryResolveCrowdAhead(direction, out direction))
            {
                if (ShouldBypassCrowdBlockForTravelAction())
                {
                    direction = ApplyCrowdAvoidance(direction);
                    rb.linearVelocity = direction * moveSpeed;
                    UpdateUnstuck(direction);
                    return;
                }

                DebugFlow("Move", "Crowd ahead blocked");

                if (currentTarget != null ||
                    hasWanderTarget ||
                    HasLockedDirectedTarget())
                {
                    if (currentTarget != null &&
                        currentTarget.GetComponentInParent<NpcTaskProvider>() != null)
                    {
                        DebugFlow(
                            "Move",
                            "Crowd blocked near provider target=" +
                            currentTarget.name +
                            " desired=" +
                            desiredTarget +
                            " moveTarget=" +
                            moveTarget +
                            " pos=" +
                            transform.position);
                    }

                    HandleBlockedMovement(moveTarget, desiredTarget);
                }
                return;
            }

            direction = ApplyCrowdAvoidance(direction);
            rb.linearVelocity = direction * moveSpeed;
            UpdateUnstuck(direction);
            return;
        }

        if (IsMovementBlocked(direction))
        {
            if (TryChooseObstacleDetourDirection(direction, desiredTarget, out Vector2 detourDirection))
            {
                if (TryCommitObstacleAvoidTarget(detourDirection))
                {
                    blockedMoveTimer = 0f;
                    return;
                }

                HandleBlockedMovement(moveTarget, desiredTarget);
                return;
            }
            else
            {
                HandleBlockedMovement(moveTarget, desiredTarget);
                return;
            }
        }

        if (!TryResolveCrowdAhead(direction, out direction))
        {
            if (ShouldBypassCrowdBlockForTravelAction())
            {
                direction = ApplyCrowdAvoidance(direction);
                rb.linearVelocity = direction * moveSpeed;
                UpdateUnstuck(direction);
                return;
            }

            DebugFlow("Move", "Crowd ahead blocked with obstacle avoidance");

            if (currentTarget != null ||
                hasWanderTarget ||
                HasLockedDirectedTarget())
            {
                if (currentTarget != null &&
                    currentTarget.GetComponentInParent<NpcTaskProvider>() != null)
                {
                    DebugFlow(
                        "Move",
                        "Crowd blocked with avoidance near provider target=" +
                        currentTarget.name +
                        " desired=" +
                        desiredTarget +
                        " moveTarget=" +
                        moveTarget +
                        " pos=" +
                        transform.position);
                }

                HandleBlockedMovement(moveTarget, desiredTarget);
            }
            return;
        }

        direction = ApplyCrowdAvoidance(direction);

        rb.linearVelocity =
            direction * moveSpeed;

        UpdateUnstuck(direction);
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

    bool ShouldHoldCombatPosition()
    {
        if (currentMonsterTarget == null ||
            isRetreatingFromMonster ||
            IsDead)
        {
            return false;
        }

        if (currentMonsterTarget.currentHP <= 0)
        {
            return false;
        }

        if (currentTarget != null &&
            currentTarget != currentMonsterTarget.transform)
        {
            return false;
        }

        float distance =
            GetCombatSurfaceDistance(
                currentMonsterTarget.transform);

        float holdRange =
            Mathf.Max(
                // Match TryAttackMonster's engage range. A larger hold radius
                // creates a dead band where the NPC neither advances nor
                // attacks a live target.
                attackRange + 0.35f,
                0.45f);

        return distance <= holdRange;
    }

    bool IsMonsterCombatApproachActive()
    {
        if (currentMonsterTarget == null ||
            currentTarget == null ||
            currentTarget != currentMonsterTarget.transform ||
            isRetreatingFromMonster ||
            IsDead ||
            currentMonsterTarget.currentHP <= 0)
        {
            return false;
        }

        return MatchesSmartAction("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);
    }

    bool MatchesSmartAction(string key, bool allowPrefix = false)
    {
        if (string.IsNullOrEmpty(currentAction) ||
            string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.Equals(
                currentAction,
                key,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string pattern = NpcText.Action(key);
        if (!string.IsNullOrEmpty(pattern) &&
            string.Equals(
                currentAction,
                pattern,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowPrefix)
        {
            return false;
        }

        return MatchesActionKey(
            currentAction,
            key,
            allowPrefix);
    }

    bool MatchesActionKey(
        string action,
        string key,
        bool allowPrefix = false)
    {
        if (string.IsNullOrEmpty(action) ||
            string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.Equals(
                action,
                key,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string pattern = NpcText.Action(key);
        if (!string.IsNullOrEmpty(pattern) &&
            string.Equals(
                action,
                pattern,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowPrefix)
        {
            return false;
        }

        return ActionMatchesPrefix(action, key) ||
            ActionMatchesPrefix(action, pattern);
    }

    static bool ActionMatchesPrefix(string action, string pattern)
    {
        if (string.IsNullOrEmpty(action) ||
            string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        int placeholderIndex = pattern.IndexOf('{');
        if (placeholderIndex < 0)
        {
            return action.StartsWith(
                pattern,
                System.StringComparison.OrdinalIgnoreCase);
        }

        string prefix = pattern.Substring(0, placeholderIndex).TrimEnd();
        return !string.IsNullOrEmpty(prefix) &&
            action.StartsWith(
                prefix,
                System.StringComparison.OrdinalIgnoreCase);
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

        spawnPosition = transform.position;       // Đặt lại điểm gốc di chuyển tại map mới
        lastUnstuckPosition = transform.position; // Reset vị trí chống kẹt
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
