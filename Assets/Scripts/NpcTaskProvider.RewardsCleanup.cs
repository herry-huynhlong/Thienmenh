using UnityEngine;

public partial class NpcTaskProvider
{
    void RewardNpc(RunningNpcTask task)
    {
        GameObject npc = task.npc;
        NpcTaskOffer offer = task.offer;
        int rewardSpiritStone =
            task.rewardSpiritStone > 0
            ? task.rewardSpiritStone
            : Mathf.Max(0, offer.rewardSpiritStone);

        PayRewardMoney(npc, rewardSpiritStone);
        ResolveOfferItemReferences(offer);

        if (offer.rewardItem != null &&
            offer.rewardItemAmount > 0)
        {
            ItemInventory npcInventory = npc.GetComponent<ItemInventory>();

            if (npcInventory == null)
            {
                npcInventory = npc.AddComponent<ItemInventory>();
                npcInventory.shareRuntimeItems = false;
            }

            npcInventory.AddItem(offer.rewardItem, offer.rewardItemAmount);
        }

        NpcRoleUtility.SetAction(
            npc,
            TaskActionFormat("taskCompleted", GetRankText(offer.rank), GetOfferTaskName(offer)));

        if (WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.AddLog(
                NpcText.Format(
                    NpcText.Get("logs", "taskCompletedWorld"),
                    NpcRoleUtility.GetDisplayName(npc),
                    GetOfferTaskName(offer),
                    rewardSpiritStone),
                0, true);
        }
    }

    void PayRewardMoney(GameObject npc, int amount)
    {
        if (npc == null ||
            amount <= 0)
        {
            return;
        }

        EnsureProviderMoney(amount);
        AddProviderMoney(-amount);
        NpcEconomy.AddNpcMoney(npc, amount);
        EnsureProviderMoney(0);
    }

    int GetProviderMoney()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.money;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.money;
        }

        EnsureProviderMoney(0);
        return serviceRewardMoney;
    }

    void AddProviderMoney(int amount)
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.money = Mathf.Max(0, villager.money + amount);
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.money = Mathf.Max(0, smartNpc.money + amount);
            return;
        }

        serviceRewardMoney = Mathf.Max(0, serviceRewardMoney + amount);
    }

    void EnsureProviderMoney(int requiredAmount)
    {
        int target = Mathf.Max(startingRewardMoney, minimumRewardMoneyReserve, requiredAmount);

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            if (refillRewardMoneyWhenLow && villager.money < target)
            {
                villager.money = target;
            }
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            if (refillRewardMoneyWhenLow && smartNpc.money < target)
            {
                smartNpc.money = target;
            }
            return;
        }

        if (refillRewardMoneyWhenLow && serviceRewardMoney < target)
        {
            serviceRewardMoney = target;
        }
    }

    void EnsureProviderInventory()
    {
        EnsureProviderMoney(0);

        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }

        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }
    }

    bool NeedsMeal(GameObject npc)
    {
        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.hunger >= mealHungerThreshold;
        }

        return false;
    }

    void FeedNpc(GameObject npc)
    {
        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.hunger = 0f;
            villager.fatigue = Mathf.Max(0f, villager.fatigue - 8f);
            return;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.hunger = 0f;
            smartNpc.fatigue = Mathf.Max(0f, smartNpc.fatigue - 8f);
        }
    }

    bool HasBusyNpc(GameObject npc)
    {
        return IsNpcBusyWithAnyProvider(npc) ||
            HasRunningTask(npc) ||
            HasRunningMeal(npc);
    }

    bool HasRunningTask(GameObject npc)
    {
        foreach (RunningNpcTask task in runningTasks)
        {
            if (task != null &&
                task.npc == npc)
            {
                return true;
            }
        }

        return false;
    }

    bool HasRunningMeal(GameObject npc)
    {
        foreach (RunningTavernMeal meal in runningMeals)
        {
            if (meal != null &&
                meal.npc == npc)
            {
                return true;
            }
        }

        return false;
    }

    string GetRankText(NpcTaskRank rank)
    {
        return NpcText.Get("taskRanks", rank.ToString(), rank.ToString());
    }

    void PauseBaseAi(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        PauseBaseAi(task.npc, out task.pausedBaseAi, out task.pausedBaseAiWasEnabled);
    }

    void PauseBaseAi(RunningTavernMeal meal)
    {
        if (meal == null)
        {
            return;
        }

        PauseBaseAi(meal.npc, out meal.pausedBaseAi, out meal.pausedBaseAiWasEnabled);
    }

    void PauseBaseAi(
        GameObject npc,
        out Behaviour pausedBaseAi,
        out bool pausedBaseAiWasEnabled)
    {
        pausedBaseAi = null;
        pausedBaseAiWasEnabled = false;

        if (npc == null)
        {
            return;
        }

        pausedBaseAi = npc.GetComponent<VillagerAI>();
        if (pausedBaseAi == null)
        {
            pausedBaseAi = npc.GetComponent<SmartNpcAI>();
        }

        if (pausedBaseAi == null)
        {
            return;
        }

        pausedBaseAiWasEnabled = pausedBaseAi.enabled;
        pausedBaseAi.enabled = false;

        Rigidbody2D body = npc.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    void ResumeBaseAi(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        ResumeBaseAi(task.pausedBaseAi, task.pausedBaseAiWasEnabled);
        task.pausedBaseAi = null;
    }

    void ResumeBaseAi(RunningTavernMeal meal)
    {
        if (meal == null)
        {
            return;
        }

        ResumeBaseAi(meal.pausedBaseAi, meal.pausedBaseAiWasEnabled);
        meal.pausedBaseAi = null;
    }

    void ResumeBaseAi(Behaviour pausedBaseAi, bool wasEnabled)
    {
        if (pausedBaseAi == null)
        {
            return;
        }

        pausedBaseAi.enabled = wasEnabled;
    }

    void CleanupTaskRuntimeState(RunningNpcTask task)
    {
        ClearTaskReservations(task);
        UnmarkNpcBusyWithProvider(task != null ? task.npc : null);
        ReleaseTaskOffer(task != null ? task.offer : null);
        RestoreEscortCompanionHome(task);
        ResumeEscortCompanion(task);
        CleanupFrontierWatchDuty(task);
        if (task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.Escort)
        {
            UnlockEscortOffer(task.offer);
        }

        ResetNpcAfterTaskCleanup(task != null ? task.npc : null);
        ResumeBaseAi(task);
    }

    void NotifyTaskStarted(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null ||
            task.startNotified)
        {
            return;
        }

        task.startNotified = true;
        TaskStarted?.Invoke(task.npc, task.offer);
    }

    void NotifyTaskFinished(RunningNpcTask task, bool completed)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null)
        {
            return;
        }

        TaskFinished?.Invoke(task.npc, task.offer, completed);
    }

    float ResolveTaskRuntimeDurationSeconds(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return 1f;
        }

        if (offer.workDurationWorldHours > 0f)
        {
            return Mathf.Max(
                1f,
                GameTime.WorldHoursToScaledSeconds(
                    offer.workDurationWorldHours));
        }

        return Mathf.Max(1f, offer.workDuration);
    }

    void ResetNpcAfterTaskCleanup(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.StopMoving();
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.StopForConversation(0.05f);
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.StopForConversation(0.05f);
        }

        Rigidbody2D body = npc.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        NpcRoleUtility.SetAction(
            npc,
            NpcText.Action("idle"));
    }

    void ResumeEscortCompanion(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        ResumeBaseAi(task.escortPausedCompanionBaseAi, task.escortPausedCompanionBaseAiWasEnabled);
        task.escortPausedCompanionBaseAi = null;
    }

    void RestoreEscortCompanionHome(RunningNpcTask task)
    {
        if (task == null ||
            task.escortCompanionNpc == null)
        {
            return;
        }

        Vector3 homePosition = task.escortCompanionHomePosition;
        if (homePosition == Vector3.zero &&
            escortAnchorPositionsCaptured)
        {
            homePosition = escortMeetAnchorPosition;
        }

        task.escortCompanionNpc.transform.position = homePosition;

        Rigidbody2D rb = task.escortCompanionNpc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = homePosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        NpcRoleUtility.StopForConversation(task.escortCompanionNpc);
        NpcRoleUtility.SetAction(
            task.escortCompanionNpc,
            TaskAction("pausedTask"));
    }
}
