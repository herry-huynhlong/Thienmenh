using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Hunt target respawn, loot matching, deadlines and target selection.
public partial class NpcTaskProvider
{
    void WaitForHuntTargetRespawn(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        bool hasHuntLoot = FindHuntLootPickup(task) != null;
        bool hasPotentialTarget = HasPotentialHuntTargetCandidate(task);
        if (!hasHuntLoot && !hasPotentialTarget)
        {
            task.huntRespawnRetryCount++;
        }
        else
        {
            task.huntRespawnRetryCount = 0;
        }

        if (ShouldCancelWaitingHuntTask(task, out string cancelReason))
        {
            CancelStuckTask(task, cancelReason);
            return;
        }

        task.stage = TavernTaskStage.WaitingForTargetRespawn;
        task.remainingTime = Mathf.Max(1f, huntTargetRetryDelay);
        task.targetMonster = null;
        task.targetLootPickup = null;

        task.resumedBaseAiWhileWaiting = false;

        if (task.npc == null)
        {
            return;
        }

        if (IsNpcAtHuntWorkPosition(task))
        {
            NpcRoleUtility.SetAction(
                task.npc,
                TaskActionFormat("waitHuntRespawn", BuildHuntProgressText(task)));
            return;
        }

        MoveNpc(
            task.npc,
            task.workPosition,
            GetWorkZone(task.offer));
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat("huntSearch", BuildHuntProgressText(task)));
    }

    void ResumeWaitingHuntTask(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        if (ShouldCancelWaitingHuntTask(task, out string cancelReason))
        {
            CancelStuckTask(task, cancelReason);
            return;
        }

        task.resumedBaseAiWhileWaiting = false;
        PrepareTaskWork(task);
        task.stage = TavernTaskStage.GoingToWork;
    }

    bool IsNpcAtHuntWorkPosition(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null)
        {
            return false;
        }

        return Vector2.Distance(
            task.npc.transform.position,
            task.workPosition) <=
            Mathf.Max(arriveDistance, huntAttackRange * 0.5f);
    }

    bool NeedsHuntItem(RunningNpcTask task)
    {
        return IsHuntTask(task) && GetTaskRequiredItem(task) != null &&
            GetTaskGatherProgress(task) < GetRequiredAmount(task);
    }

    bool TryCollectHuntLoot(RunningNpcTask task)
    {
        if (!NeedsHuntItem(task) || task.npc == null)
        {
            return false;
        }

        if (!IsGatherPickupUsable(task.targetLootPickup, GetTaskRequiredItem(task)))
        {
            task.targetLootPickup = FindHuntLootPickup(task);
        }

        if (!IsGatherPickupUsable(task.targetLootPickup, GetTaskRequiredItem(task)))
        {
            return false;
        }

        task.workPosition = task.targetLootPickup.transform.position;
        if (!IsNpcAtHuntLootPickup(task))
        {
            MoveNpcToWork(task, task.workPosition);
            NpcRoleUtility.SetAction(
                task.npc,
                TaskActionFormat("pickItem", GetTaskRequiredItemName(task), BuildHuntProgressText(task)));
            return true;
        }

        StatItemData item = task.targetLootPickup.item;
        if (!task.targetLootPickup.CanNpcActorCollect(task.npc))
        {
            task.targetLootPickup = null;
            return false;
        }

        if (!task.targetLootPickup.TryTake(1))
        {
            task.targetLootPickup = null;
            return false;
        }

        ItemInventory inventory = GetOrCreateInventory(task.npc);
        ItemEffectSpawner.PlayPickupEffect(item, task.npc.transform);
        inventory.AddItem(item, 1);
        task.collectedAmount++;
        task.targetLootPickup = null;

        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Picked,
            item,
            task.npc);
        TreasureHeatSystem.NotifyNpcReceivedItem(task.npc, item);
        return true;
    }

    bool IsNpcAtHuntLootPickup(RunningNpcTask task)
    {
        if (task == null || task.npc == null || task.targetLootPickup == null)
        {
            return false;
        }

        float allowedDistance = Mathf.Max(arriveDistance, gatherInteractDistance);
        Collider2D pickupCollider = task.targetLootPickup.GetComponent<Collider2D>();
        if (pickupCollider != null)
        {
            Vector2 closest = pickupCollider.ClosestPoint(task.npc.transform.position);
            return Vector2.Distance(task.npc.transform.position, closest) <= allowedDistance;
        }

        return Vector2.Distance(task.npc.transform.position, task.targetLootPickup.transform.position) <= allowedDistance;
    }

    WorldStatItemPickup FindHuntLootPickup(RunningNpcTask task)
    {
        StatItemData requiredItem = GetTaskRequiredItem(task);
        if (task == null || task.npc == null || requiredItem == null)
        {
            return null;
        }

        WorldStatItemPickup best = null;
        float bestDistance = float.PositiveInfinity;
        Vector3 searchPosition = GetTaskSearchPosition(task);

        foreach (WorldStatItemPickup pickup in FindObjectsByType<WorldStatItemPickup>(FindObjectsInactive.Exclude))
        {
            if (!IsGatherPickupUsable(pickup, requiredItem) || pickup.RequiresNpcHarvestAction())
            {
                continue;
            }

            NpcMapArea area = NpcMapArea.FindArea(pickup.transform.position);
            if (area != null && area.zone != NpcMapZone.MaThuSonMach)
            {
                continue;
            }

            float distance = Vector2.Distance(searchPosition, pickup.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pickup;
            }
        }

        return best;
    }

    bool CanUseMonsterForHuntTask(RunningNpcTask task, MonsterAI monster)
    {
        if (!IsHuntTargetUsable(monster))
        {
            return false;
        }

        NpcTaskOffer offer = task != null ? task.offer : null;
        if (!MatchesRequiredHuntTargetType(offer != null ? offer.requiredHuntTargetType : HuntTargetType.Beast, monster.huntTargetType))
        {
            return false;
        }

        if (!MatchesHuntMonsterDifficulty(offer, monster))
        {
            return false;
        }

        StatItemData requiredItem = GetTaskRequiredItem(task);
        if (requiredItem != null && monster.GetDeathLoot() != requiredItem)
        {
            return false;
        }

        return monster.GetDeathLoot() != null || requiredItem == null;
    }

    bool CanMonsterEverSatisfyHuntTask(RunningNpcTask task, MonsterAI monster)
    {
        if (monster == null ||
            !monster.gameObject.activeInHierarchy)
        {
            return false;
        }

        NpcTaskOffer offer = task != null ? task.offer : null;
        if (!MatchesRequiredHuntTargetType(
                offer != null ? offer.requiredHuntTargetType : HuntTargetType.Beast,
                monster.huntTargetType))
        {
            return false;
        }

        if (!MatchesHuntMonsterDifficulty(offer, monster))
        {
            return false;
        }

        StatItemData requiredItem = GetTaskRequiredItem(task);
        StatItemData loot = monster.GetDeathLoot();
        if (requiredItem != null &&
            loot != requiredItem)
        {
            return false;
        }

        if (requiredItem != null &&
            loot == null)
        {
            return false;
        }

        return IsHuntTargetUsable(monster) || monster.respawnAfterDeath;
    }

    bool HasPotentialHuntTargetCandidate(RunningNpcTask task)
    {
        if (task == null)
        {
            return false;
        }

        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            if (CanMonsterEverSatisfyHuntTask(task, monster))
            {
                return true;
            }
        }

        return false;
    }

    void ResolveHuntRequiredItemFromMonster(RunningNpcTask task, MonsterAI monster)
    {
        if (task == null || task.requiredItem != null || monster == null)
        {
            return;
        }

        StatItemData loot = monster.GetDeathLoot();
        if (loot != null)
        {
            task.requiredItem = loot;
            if (task.collectedAmount <= 0)
            {
                task.startingRequiredItemAmount = GetNpcItemAmount(task.npc, loot);
            }
        }
    }

    bool MatchesRequiredHuntTargetType(HuntTargetType requiredType, HuntTargetType targetType)
    {
        return requiredType == HuntTargetType.Any || targetType == HuntTargetType.Any || requiredType == targetType;
    }

    bool MatchesHuntMonsterDifficulty(NpcTaskOffer offer, MonsterAI monster)
    {
        if (offer == null || monster == null)
        {
            return false;
        }

        if (offer.useMonsterRealmStageRequirement)
        {
            if (offer.matchMonsterRealmExactly)
            {
                return monster.realm == offer.requiredMonsterRealm &&
                    monster.realmStage <= offer.requiredMonsterMaxStage;
            }

            return CultivationProgression.GetRealmPower(
                monster.realm,
                monster.realmStage) <=
                CultivationProgression.GetRealmPower(
                    offer.requiredMonsterRealm,
                    offer.requiredMonsterMaxStage);
        }

        int requiredLevel = GetRequiredBeastLevel(offer);
        return requiredLevel <= 0 || monster.beastLevel == requiredLevel;
    }

    int GetRequiredBeastLevel(NpcTaskOffer offer)
    {
        return offer != null ? Mathf.Max(0, offer.requiredBeastLevel) : 0;
    }
    MonsterAI FindHuntTarget(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null)
        {
            return null;
        }

        MonsterAI best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            if (!CanUseMonsterForHuntTask(task, monster))
            {
                continue;
            }

            NpcMapArea area = NpcMapArea.FindArea(monster.transform.position);
            if (area == null ||
                area.zone != NpcMapZone.MaThuSonMach)
            {
                continue;
            }

            float distance = Vector2.Distance(
                GetTaskSearchPosition(task),
                monster.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = monster;
            }
        }

        return best;
    }

    bool IsHuntTargetUsable(MonsterAI monster)
    {
        return monster != null &&
            monster.gameObject.activeInHierarchy &&
            !monster.IsDead &&
            monster.currentHP > 0;
    }

    bool ShouldCancelWaitingHuntTask(
        RunningNpcTask task,
        out string reason)
    {
        reason = null;
        if (task == null)
        {
            return false;
        }

        if (HasExceededHuntMissionDeadline(task))
        {
            reason = "HuntTargetRespawn deadlineExceeded";
            return true;
        }

        if (FindHuntLootPickup(task) != null)
        {
            task.huntRespawnRetryCount = 0;
            return false;
        }

        if (HasPotentialHuntTargetCandidate(task))
        {
            task.huntRespawnRetryCount = 0;
            return false;
        }

        if (task.huntRespawnRetryCount >= Mathf.Max(1, maxHuntTargetSearchAttempts))
        {
            reason = "HuntTargetRespawn noMatchingTarget";
            return true;
        }

        return false;
    }

    float GetInitialHuntMissionDeadlineWorldHour()
    {
        float worldHour = GetAbsoluteWorldHour();
        if (worldHour < 0f)
        {
            return float.PositiveInfinity;
        }

        return worldHour + Mathf.Max(1f, maxHuntTaskWaitWorldHours);
    }

    bool HasExceededHuntMissionDeadline(RunningNpcTask task)
    {
        if (task == null)
        {
            return false;
        }

        float worldHour = GetAbsoluteWorldHour();
        if (worldHour >= 0f &&
            !float.IsInfinity(task.huntMissionDeadlineWorldHour))
        {
            return worldHour >= task.huntMissionDeadlineWorldHour;
        }

        return Time.time >= task.huntMissionDeadlineFallbackTime;
    }

    float GetAbsoluteWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return -1f;
        }

        int absoluteDay =
            Mathf.Max(0, timeSystem.CurrentAbsoluteDay - 1);
        return absoluteDay * 24f +
            Mathf.Max(0f, timeSystem.currentHour);
    }

    bool IsWorkThreatMonster(MonsterAI monster)
    {
        if (!IsHuntTargetUsable(monster))
        {
            return false;
        }

        float menace =
            monster.aggression * 0.45f +
            monster.bloodlust * 0.35f +
            monster.territorial * 0.2f;

        if (monster.huntTargetType == HuntTargetType.Animal)
        {
            menace -= 25f;
        }

        return menace >= 35f;
    }

}
