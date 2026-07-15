using UnityEngine;

// Travel-intent restoration, pill ownership checks, and cultivation-travel state.
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

    bool TryRecoverTeleportRouteActionWithoutTarget()
    {
        if (!IsTeleportRouteAction(currentAction) ||
            currentTarget != null ||
            hasWanderTarget ||
            hasEscapeTarget ||
            hasObstacleAvoidTarget)
        {
            return false;
        }

        string restoredAction =
            ResolvePostTeleportTravelAction();
        if (string.IsNullOrWhiteSpace(restoredAction) ||
            IsTeleportRouteAction(restoredAction))
        {
            restoredAction = NpcText.Action("idle");
        }

        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        blockedMoveTimer = 0f;
        stuckMoveTimer = 0f;
        lastUnstuckPosition = transform.position;

        if (TryRebuildPostTeleportTravelIntent(restoredAction))
        {
            DebugFlow(
                "MoveRoute",
                "Recovered teleport route intent -> " +
                restoredAction);
            return true;
        }

        currentAction = restoredAction;
        DebugFlow(
            "MoveRoute",
            "Recovered teleport route fallback -> " +
            currentAction);
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
}
