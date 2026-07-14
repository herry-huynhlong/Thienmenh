using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Gather threats, combat power, objective progress and reward calculation.
public partial class NpcTaskProvider
{
    bool HandleGatherThreat(RunningNpcTask task)
    {
        if (!IsGatherTask(task) ||
            task.npc == null)
        {
            return false;
        }

        if (Time.time < task.avoidUntilTime)
        {
            MoveNpcToWork(task, task.avoidPosition);
            NpcRoleUtility.SetAction(
                task.npc,
                TaskAction("fleeMonsterArea"));
            return true;
        }

        MonsterAI threat =
            FindGatherThreat(task);

        if (!IsHuntTargetUsable(threat))
        {
            task.threatMonster = null;
            return false;
        }

        task.threatMonster = threat;

        LogThreatDecision(
            task,
            threat,
            "GatherThreat",
            GetNpcCombatPower(task.npc),
            GetMonsterCombatPower(threat),
            0.05f,
            Mathf.Max(0.1f, gatherThreatFightPowerRatio));

        if (ShouldFleeGatherThreat(task, threat))
        {
            FleeGatherThreat(task, threat);
            return true;
        }

        if (ShouldFightGatherThreat(task, threat))
        {
            FightGatherThreat(task, threat);
            return true;
        }

        FleeGatherThreat(task, threat);
        return true;
    }

    MonsterAI FindGatherThreat(RunningNpcTask task)
    {
        Vector3 referencePosition =
            task.targetPickup != null
            ? task.targetPickup.transform.position
            : task.workPosition;

        MonsterAI best = null;
        float bestDistance = float.PositiveInfinity;
        float detectRadius =
            Mathf.Max(0.5f, gatherThreatDetectRadius);

        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            if (!IsWorkThreatMonster(monster))
            {
                continue;
            }

            float distanceToNpc = Vector2.Distance(
                task.npc.transform.position,
                monster.transform.position);
            float distanceToWork = Vector2.Distance(
                referencePosition,
                monster.transform.position);

            if (distanceToNpc > detectRadius &&
                distanceToWork > detectRadius)
            {
                continue;
            }

            float score =
                Mathf.Min(distanceToNpc, distanceToWork);

            if (score < bestDistance)
            {
                bestDistance = score;
                best = monster;
            }
        }

        return best;
    }

    bool ShouldFightGatherThreat(RunningNpcTask task, MonsterAI threat)
    {
        return GetNpcCombatPower(task.npc) >=
            GetMonsterCombatPower(threat) * Mathf.Max(0.1f, gatherThreatFightPowerRatio);
    }

    bool ShouldFleeGatherThreat(RunningNpcTask task, MonsterAI threat)
    {
        return CombatPowerUtility.ShouldRetreat(
            task.npc,
            threat != null ? threat.gameObject : null);
    }

    void FightGatherThreat(RunningNpcTask task, MonsterAI threat)
    {
        float distance = Vector2.Distance(
            task.npc.transform.position,
            threat.transform.position);

        if (distance > huntAttackRange)
        {
            MoveNpcToWork(task, threat.transform.position);
            NpcRoleUtility.SetAction(
                task.npc,
                TaskAction("fightBlockingMonster"));
            return;
        }

        NpcRoleUtility.StopForConversation(task.npc);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskAction("clearHarvestMonster"));

        task.remainingTime -= Time.deltaTime;
        if (task.remainingTime > 0f)
        {
            return;
        }

        task.remainingTime = Mathf.Max(0.2f, huntAttackInterval);
        NpcRoleUtility.Damage(
            task.npc,
            threat.gameObject,
            NpcRoleUtility.GetAttack(task.npc),
            "bao ve khu hai linh thao");
    }

    void FleeGatherThreat(RunningNpcTask task, MonsterAI threat)
    {
        task.targetPickup = null;
        task.threatMonster = threat;
        task.avoidPosition =
            GetRetreatPosition(task.npc.transform.position, threat.transform.position);
        task.workPosition = task.avoidPosition;
        task.avoidUntilTime =
            Time.time + Mathf.Max(1f, gatherThreatAvoidDuration);

        MoveNpcToWork(task, task.avoidPosition);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskAction("tooStrongChangeHarvestArea"));
    }

    Vector3 GetRetreatPosition(Vector3 npcPosition, Vector3 threatPosition)
    {
        Vector2 away =
            (Vector2)(npcPosition - threatPosition);

        if (away.sqrMagnitude < 0.01f)
        {
            away = Random.insideUnitCircle.normalized;
        }
        else
        {
            away.Normalize();
        }

        return npcPosition +
            (Vector3)(away * Mathf.Max(1f, gatherThreatAvoidRadius));
    }

    int GetNpcCombatPower(GameObject npc)
    {
        if (npc == null)
        {
            return 1;
        }

        CharacterStats stats = npc.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return Mathf.Max(
                1,
                stats.attack + stats.defense + stats.finalHP / 10);
        }

        return Mathf.Max(
            1,
            NpcRoleUtility.GetAttack(npc) * 2 + 10);
    }

    int GetMonsterCombatPower(MonsterAI monster)
    {
        if (monster == null)
        {
            return 1;
        }

        return Mathf.Max(
            1,
            monster.damage + monster.defense + monster.maxHP / 10);
    }

    bool IsHuntTask(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.HuntMonster;
    }

    bool HasHuntObjectiveComplete(RunningNpcTask task)
    {
        if (task == null)
        {
            return false;
        }

        if (GetTaskRequiredItem(task) != null)
        {
            return GetTaskGatherProgress(task) >= GetRequiredAmount(task);
        }

        return task.defeatedMonsterCount >= GetRequiredMonsterKills(task.offer);
    }

    int GetRequiredMonsterKills(NpcTaskOffer offer)
    {
        return offer != null
            ? Mathf.Max(1, offer.requiredMonsterKills)
            : 1;
    }

    string BuildHuntProgressText(RunningNpcTask task)
    {
        if (task != null && GetTaskRequiredItem(task) != null)
        {
            return "(" + GetTaskGatherProgress(task) + "/" +
                GetRequiredAmount(task) + " " + GetTaskRequiredItemName(task) + ")";
        }

        int defeated = task != null
            ? Mathf.Max(0, task.defeatedMonsterCount)
            : 0;

        return "(" + defeated + "/" +
            GetRequiredMonsterKills(task != null ? task.offer : null) + ")";
    }
    float GetGatherWorkDuration(RunningNpcTask task)
    {
        float duration = task != null && task.offer != null
            ? task.offer.workDuration
            : 1f;

        if (task != null && task.targetPickup != null)
        {
            duration = Mathf.Max(
                duration,
                task.targetPickup.harvestDurationScaledSeconds);
        }

        return Mathf.Max(1f, duration);
    }
    bool TryCollectGatherItem(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null ||
            !IsGatherPickupUsable(task.targetPickup, GetTaskRequiredItem(task), task.npc))
        {
            return false;
        }

        StatItemData item = task.targetPickup.item;
        if (!task.targetPickup.TryTake(1))
        {
            return false;
        }

        ItemInventory inventory = GetOrCreateInventory(task.npc);
        ItemEffectSpawner.PlayPickupEffect(item, task.npc.transform);
        inventory.AddItem(item, 1);
        task.collectedAmount++;

        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Picked,
            item,
            task.npc);

        return true;
    }

    WorldStatItemPickup FindGatherPickup(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null)
        {
            return null;
        }

        return WorldResourceField.GetNearestAvailablePickupInAllFields(
            GetTaskSearchPosition(task),
            GetTaskRequiredItem(task),
            GetGatherRequiredZone(task.offer),
            null,
            false,
            task.npc);
    }

    Vector3 GetTaskSearchPosition(RunningNpcTask task)
    {
        if (task != null &&
            task.workPosition != Vector3.zero)
        {
            return task.workPosition;
        }

        return task != null && task.npc != null
            ? task.npc.transform.position
            : transform.position;
    }

    bool IsGatherPickupUsable(
        WorldStatItemPickup pickup,
        StatItemData requiredItem,
        GameObject requester = null)
    {
        return pickup != null &&
            pickup.gameObject.activeInHierarchy &&
            pickup.item != null &&
            pickup.amount > 0 &&
            pickup.allowNpcPickup &&
            !pickup.IsReservedByOther(requester) &&
            (requiredItem == null || pickup.item == requiredItem);
    }

    bool IsGatherTask(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            IsGatherTaskType(task.offer.taskType);
    }

    bool IsPatrolTask(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.Patrol;
    }

    bool IsGatherTaskType(NpcTaskType taskType)
    {
        return taskType == NpcTaskType.GatherResource ||
            taskType == NpcTaskType.HarvestAndDeliver;
    }

    NpcMapZone? GetGatherRequiredZone(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

        if (offer.taskType != NpcTaskType.GatherResource)
        {
            return null;
        }

        if (IsLinhRiceItem(offer.requiredItem))
        {
            return NpcMapZone.Lang;
        }

        return NpcMapZone.MaThuSonMach;
    }

    bool HasGatherObjectiveComplete(RunningNpcTask task)
    {
        return task != null &&
            GetTaskGatherProgress(task) >= GetRequiredAmount(task);
    }

    bool HasTaskObjectiveComplete(RunningNpcTask task)
    {
        if (task == null || task.offer == null)
        {
            return false;
        }

        if (IsEscortTask(task))
        {
            return task.escortConfirmed;
        }

        if (IsGatherTask(task))
        {
            return HasGatherObjectiveComplete(task);
        }

        if (IsHuntTask(task))
        {
            return HasHuntObjectiveComplete(task);
        }

        if (IsPatrolTask(task))
        {
            return task.patrolReachedEnd;
        }

        return true;
    }

    StatItemData GetTaskRequiredItem(RunningNpcTask task)
    {
        if (task == null)
        {
            return null;
        }

        if (task.requiredItem != null)
        {
            return task.requiredItem;
        }

        return task.offer != null
            ? task.offer.requiredItem
            : null;
    }

    string GetTaskRequiredItemName(RunningNpcTask task)
    {
        StatItemData item = GetTaskRequiredItem(task);
        if (item != null)
        {
            return ItemText.Name(item);
        }

        if (task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.HarvestAndDeliver)
        {
            return TaskDisplay("linhRice");
        }

        return NpcText.Get("taskDisplay", "spiritHerb");
    }

    int GetTaskGatherProgress(RunningNpcTask task)
    {
        if (task == null)
        {
            return 0;
        }

        int inventoryProgress = GetTaskInventoryProgress(task);
        if (UsesInventoryTurnInItems(task))
        {
            return inventoryProgress;
        }

        return Mathf.Max(
            Mathf.Max(0, task.collectedAmount),
            inventoryProgress);
    }

    int GetTaskInventoryProgress(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null ||
            GetTaskRequiredItem(task) == null ||
            task.npc == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            GetNpcItemAmount(task.npc, GetTaskRequiredItem(task)) -
                task.startingRequiredItemAmount);
    }

    bool UsesInventoryTurnInItems(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            task.offer.consumeRequiredItemsOnTurnIn &&
            GetTaskRequiredItem(task) != null;
    }

    int GetNpcItemAmount(GameObject npc, StatItemData item)
    {
        if (npc == null ||
            item == null)
        {
            return 0;
        }

        ItemInventory inventory = npc.GetComponent<ItemInventory>();
        return inventory != null
            ? inventory.GetAmount(item)
            : 0;
    }

    int GetRequiredAmount(NpcTaskOffer offer)
    {
        return offer != null
            ? Mathf.Max(1, offer.requiredAmount)
            : 1;
    }

    int GetRequiredAmount(RunningNpcTask task)
    {
        if (task != null &&
            task.requiredAmount > 0)
        {
            return Mathf.Max(1, task.requiredAmount);
        }

        return GetRequiredAmount(task != null ? task.offer : null);
    }

    int ResolveTaskRequiredAmount(
        NpcTaskOffer offer,
        StatItemData requiredItem)
    {
        if (offer == null)
        {
            return 1;
        }

        if (requiredItem == null ||
            !offer.randomizeRequiredItemAmount)
        {
            return GetRequiredAmount(offer);
        }

        int min = Mathf.Max(1, offer.requiredItemAmountMin);
        int max = Mathf.Max(min, offer.requiredItemAmountMax);

        return Random.Range(min, max + 1);
    }

    int ResolveTaskRewardSpiritStone(
        NpcTaskOffer offer,
        StatItemData requiredItem,
        int requiredAmount)
    {
        if (offer == null)
        {
            return 0;
        }

        if (!offer.autoPriceRequiredItemReward ||
            requiredItem == null ||
            requiredAmount <= 0)
        {
            return Mathf.Max(0, offer.rewardSpiritStone);
        }

        int itemValue =
            NpcEconomy.GetItemValue(requiredItem);

        if (itemValue <= 0)
        {
            return Mathf.Max(0, offer.rewardSpiritStone);
        }

        int baseValue =
            itemValue * Mathf.Max(1, requiredAmount);

        float multiplier = ResolveRankBountyMultiplier(offer);

        return Mathf.Max(
            Mathf.Max(0, offer.rewardSpiritStone),
            Mathf.RoundToInt(baseValue * multiplier));
    }
    float ResolveRankBountyMultiplier(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return 1f;
        }

        if (!useDefaultRankBountyMultipliers ||
            !offer.useRankRewardMultiplier)
        {
            float minMarkup = Mathf.Max(
                Mathf.Max(minimumTaskRewardMarkup, 0.2f),
                offer.requiredItemRewardMarkupMin);
            float maxMarkup = Mathf.Max(minMarkup, offer.requiredItemRewardMarkupMax);
            return 1f + Random.Range(minMarkup, maxMarkup);
        }

        Vector2 range;
        switch (offer.rank)
        {
            case NpcTaskRank.Trung:
                range = trungRankBountyMultiplier;
                break;
            case NpcTaskRank.Thuong:
                range = thuongRankBountyMultiplier;
                break;
            default:
                range = haRankBountyMultiplier;
                break;
        }

        float min = Mathf.Max(0f, range.x);
        float max = Mathf.Max(min, range.y);
        return Random.Range(min, max);
    }

    string GetRequiredItemName(NpcTaskOffer offer)
    {
        StatItemData requiredItem = GetPlannedRequiredItem(offer);

        if (requiredItem != null)
        {
            return ItemText.Name(requiredItem);
        }

        if (offer != null &&
            offer.taskType == NpcTaskType.HarvestAndDeliver)
        {
            return TaskDisplay("linhRice");
        }

        return NpcText.Get("taskDisplay", "spiritHerb");
    }

    string BuildGatherProgressText(RunningNpcTask task)
    {
        int collected = GetTaskGatherProgress(task);

        return "(" + collected + "/" + GetRequiredAmount(task) + ")";
    }

    string GetTaskDisplayText(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null)
        {
            return string.Empty;
        }

        string text = TaskDisplayFormat(
            "rankedTask",
            GetRankText(task.offer.rank),
            GetOfferTaskName(task.offer));

        if (IsGatherTask(task))
        {
            text += TaskDisplayFormat(
                "itemObjective",
                GetTaskRequiredItemName(task),
                GetRequiredAmount(task));
        }
        else if (IsHuntTask(task))
        {
            if (GetTaskRequiredItem(task) != null)
            {
                text += TaskDisplayFormat(
                    "itemObjective",
                    GetTaskRequiredItemName(task),
                    GetRequiredAmount(task));
            }
            else
            {
                int requiredLevel = GetRequiredBeastLevel(task.offer);
                string levelText = requiredLevel > 0
                    ? TaskDisplayFormat("beastLevel", requiredLevel)
                    : string.Empty;

                text += TaskDisplayFormat(
                    "huntObjective",
                    TaskDisplay("beast"),
                    levelText,
                    GetRequiredMonsterKills(task.offer));
            }
        }

        int rewardSpiritStone =
            task.rewardSpiritStone > 0
            ? task.rewardSpiritStone
            : Mathf.Max(0, task.offer.rewardSpiritStone);

        if (rewardSpiritStone > 0)
        {
            text += TaskDisplayFormat("reward", rewardSpiritStone);
        }

        return text;
    }
}
