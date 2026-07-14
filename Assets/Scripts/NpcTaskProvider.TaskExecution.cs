using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Gather, hunt, patrol and escort execution state machines.
public partial class NpcTaskProvider
{
    void PrepareTaskWork(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        if (IsEscortTask(task))
        {
            PrepareEscortTask(task);
            return;
        }

        if (IsGatherTask(task))
        {
            task.collectedAmount = Mathf.Clamp(
                task.collectedAmount,
                0,
                GetRequiredAmount(task));

            task.targetPickup = FindGatherPickup(task);
            if (task.targetPickup != null)
            {
                ReserveGatherPickupForTask(task.targetPickup, task.npc);
                task.workPosition = task.targetPickup.transform.position;
            }
            return;
        }

        if (IsPatrolTask(task))
        {
            task.patrolReachedEnd = false;
            task.workPosition = GetPatrolStartPosition(task.offer);
            task.patrolEndPosition = GetPatrolEndPosition(task.offer);
            return;
        }

        if (IsHuntTask(task))
        {
            task.defeatedMonsterCount = Mathf.Clamp(
                task.defeatedMonsterCount,
                0,
                GetRequiredMonsterKills(task.offer));

            task.targetMonster = FindHuntTarget(task);
            if (task.targetMonster != null)
            {
                task.workPosition = task.targetMonster.transform.position;
            }
        }
    }

    void UpdateGatherTravel(RunningNpcTask task)
    {
        if (HasGatherObjectiveComplete(task))
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        if (HandleGatherThreat(task))
        {
            return;
        }

        if (!IsGatherPickupUsable(task.targetPickup, GetTaskRequiredItem(task), task.npc))
        {
            task.targetPickup = FindGatherPickup(task);
            ReserveGatherPickupForTask(task.targetPickup, task.npc);
        }

        if (task.targetPickup == null)
        {
            MoveNpcToWork(task, task.workPosition);
            NpcRoleUtility.SetAction(
                task.npc,
                TaskActionFormat("searchGatherItem", GetTaskRequiredItemName(task), BuildGatherProgressText(task)));

            if (UpdateTaskTravelWatchdog(
                    task,
                    task.workPosition,
                    arriveDistance,
                    GetWorkZone(task.offer),
                    "GatherTargetSearch"))
            {
                return;
            }

            if (Vector2.Distance(
                    task.npc.transform.position,
                    task.workPosition) <= arriveDistance)
            {
                CancelStuckTask(task, "GatherTargetSearch noPickup");
            }
            return;
        }

        ReserveGatherPickupForTask(task.targetPickup, task.npc);
        task.workPosition = task.targetPickup.transform.position;
        MoveNpcToWork(task, task.workPosition);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat("goGatherItem", GetTaskRequiredItemName(task), BuildGatherProgressText(task)));

        if (UpdateTaskTravelWatchdog(
                task,
                task.workPosition,
                Mathf.Max(arriveDistance, gatherInteractDistance),
                GetWorkZone(task.offer),
                "GatherTravel"))
        {
            return;
        }

        if (IsNpcAtGatherPickup(task))
        {
            task.stage = TavernTaskStage.Working;
            DisarmTaskTravelWatchdog(task);
            task.remainingTime = GetGatherWorkDuration(task);
        }
    }
    void ReserveGatherPickupForTask(
        WorldStatItemPickup pickup,
        GameObject npc = null)
    {
        if (pickup == null)
        {
            return;
        }

        pickup.allowNpcPickup = true;
        pickup.requireNpcHarvestAction = true;

        if (npc != null)
        {
            pickup.TryReserve(npc, 6f);
        }
    }
    bool IsNpcAtGatherPickup(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.targetPickup == null)
        {
            return false;
        }

        float allowedDistance = Mathf.Max(arriveDistance, gatherInteractDistance);
        Vector3 npcPosition = task.npc.transform.position;
        Vector3 pickupPosition = task.targetPickup.transform.position;

        Collider2D pickupCollider = task.targetPickup.GetComponent<Collider2D>();
        if (pickupCollider != null)
        {
            Vector2 closest = pickupCollider.ClosestPoint(npcPosition);
            if (Vector2.Distance(npcPosition, closest) <= allowedDistance)
            {
                return true;
            }
        }
        return Vector2.Distance(npcPosition, pickupPosition) <= allowedDistance;
    }
    void UpdateGatherWork(RunningNpcTask task)
    {
        DisarmTaskTravelWatchdog(task);
        if (HasGatherObjectiveComplete(task))
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        if (HandleGatherThreat(task))
        {
            return;
        }

        if (!IsGatherPickupUsable(task.targetPickup, GetTaskRequiredItem(task), task.npc))
        {
            task.targetPickup = null;
            task.stage = TavernTaskStage.GoingToWork;
            return;
        }

        NpcRoleUtility.StopForConversation(task.npc, 0.35f);
        task.remainingTime -= Time.deltaTime;
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat("gatheringItem", GetTaskRequiredItemName(task), BuildGatherProgressText(task)));

        if (task.remainingTime > 0f)
        {
            return;
        }

        if (TryCollectGatherItem(task))
        {
            task.targetPickup = null;

            task.stage = HasGatherObjectiveComplete(task)
                ? TavernTaskStage.ReturningToTurnIn
                : TavernTaskStage.GoingToWork;
            return;
        }

        task.targetPickup = null;
        task.stage = TavernTaskStage.GoingToWork;
    }


    void UpdateHuntTravel(RunningNpcTask task)
    {
        if (HasHuntObjectiveComplete(task))
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        if (TryCollectHuntLoot(task))
        {
            task.stage = HasHuntObjectiveComplete(task)
                ? TavernTaskStage.ReturningToTurnIn
                : TavernTaskStage.GoingToWork;
            return;
        }

        if (NeedsHuntItem(task))
        {
            task.targetLootPickup = FindHuntLootPickup(task);
            if (task.targetLootPickup != null)
            {
                task.workPosition = task.targetLootPickup.transform.position;
                MoveNpcToWork(task, task.workPosition);
                NpcRoleUtility.SetAction(
                    task.npc,
                    TaskActionFormat("pickHuntEvidence", BuildHuntProgressText(task)));
                UpdateTaskTravelWatchdog(
                    task,
                    task.workPosition,
                    Mathf.Max(arriveDistance, gatherInteractDistance),
                    GetWorkZone(task.offer),
                    "HuntLootTravel");
                return;
            }
        }

        if (!IsHuntTargetUsable(task.targetMonster) || !CanUseMonsterForHuntTask(task, task.targetMonster))
        {
            task.targetMonster = FindHuntTarget(task);
        }

        if (task.targetMonster == null)
        {
            WaitForHuntTargetRespawn(task);
            return;
        }

        ResolveHuntRequiredItemFromMonster(task, task.targetMonster);
        task.workPosition = task.targetMonster.transform.position;
        MoveNpcToWork(task, task.workPosition);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat("huntSearch", BuildHuntProgressText(task)));

        if (UpdateTaskTravelWatchdog(
                task,
                task.workPosition,
                huntAttackRange,
                GetWorkZone(task.offer),
                "HuntTravel"))
        {
            return;
        }

        if (Vector2.Distance(
                task.npc.transform.position,
                task.targetMonster.transform.position) <= huntAttackRange)
        {
            task.stage = TavernTaskStage.Working;
            task.remainingTime = 0f;
        }
    }

    void UpdateHuntWork(RunningNpcTask task)
    {
        DisarmTaskTravelWatchdog(task);
        if (HasHuntObjectiveComplete(task))
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        if (!IsHuntTargetUsable(task.targetMonster))
        {
            task.defeatedMonsterCount++;
            task.targetMonster = null;

            if (TryCollectHuntLoot(task))
            {
                task.stage = HasHuntObjectiveComplete(task)
                    ? TavernTaskStage.ReturningToTurnIn
                    : TavernTaskStage.GoingToWork;
                return;
            }

            task.stage = HasHuntObjectiveComplete(task)
                ? TavernTaskStage.ReturningToTurnIn
                : TavernTaskStage.GoingToWork;
            return;
        }

        float distance = Vector2.Distance(
            task.npc.transform.position,
            task.targetMonster.transform.position);

        if (distance > huntAttackRange)
        {
            task.stage = TavernTaskStage.GoingToWork;
            return;
        }

        NpcRoleUtility.StopForConversation(task.npc);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat("huntFight", BuildHuntProgressText(task)));

        task.remainingTime -= Time.deltaTime;
        if (task.remainingTime > 0f)
        {
            return;
        }

        task.remainingTime = Mathf.Max(0.2f, huntAttackInterval);
        NpcRoleUtility.Damage(
            task.npc,
            task.targetMonster.gameObject,
            NpcRoleUtility.GetAttack(task.npc),
            "lam nhiem vu san yeu thu");
    }

    void UpdatePatrolWork(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null)
        {
            return;
        }

        if (task.patrolReachedEnd)
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        Vector3 patrolTarget =
            task.patrolEndPosition != Vector3.zero
            ? task.patrolEndPosition
            : GetPatrolEndPosition(task.offer);

        task.patrolEndPosition = patrolTarget;
        MoveNpcToWork(task, patrolTarget);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat("workingTask", GetTaskDisplayText(task)));

        if (UpdateTaskTravelWatchdog(
                task,
                patrolTarget,
                arriveDistance,
                GetWorkZone(task.offer),
                "PatrolWork"))
        {
            return;
        }

        if (Vector2.Distance(
                task.npc.transform.position,
                patrolTarget) <= arriveDistance)
        {
            task.patrolReachedEnd = true;
            task.stage = TavernTaskStage.ReturningToTurnIn;
        }
    }

    void UpdateEscortMeeting(RunningNpcTask task)
    {
        DisarmTaskTravelWatchdog(task);
        if (!IsEscortCompanionUsable(task))
        {
            FinishTask(runningTasks.IndexOf(task), false);
            return;
        }

        if (!task.escortGreetingConversationStarted)
        {
            task.escortGreetingConversationStarted = true;
            task.escortGreetingConversationStep = 0;
            task.remainingTime = 0f;
        }

        if (!UpdateEscortGreetingDialogue(task))
        {
            return;
        }

        NpcRoleUtility.StopForConversation(task.npc);
        NpcRoleUtility.StopForConversation(task.escortCompanionNpc);
        task.escortDepartedFromCompanion = true;
        task.workPosition = GetEscortCompletionPosition(task.offer);
        task.stage = TavernTaskStage.GoingToWork;
    }

    void UpdateEscortTravel(RunningNpcTask task)
    {
        if (!IsEscortCompanionUsable(task))
        {
            FinishTask(runningTasks.IndexOf(task), false);
            return;
        }

        if (!task.escortDepartedFromCompanion)
        {
            Vector3 greetingPosition = GetEscortGreetingPosition(task);
            MoveNpc(task.npc, greetingPosition);
            NpcRoleUtility.SetAction(
                task.npc,
                TaskActionFormat("goWorkTask", GetTaskDisplayText(task)));

            if (UpdateTaskTravelWatchdog(
                    task,
                    greetingPosition,
                    Mathf.Max(
                        arriveDistance,
                        escortFollowDistance * 0.75f),
                    null,
                    "EscortMeetTravel"))
            {
                return;
            }

            if (Vector2.Distance(
                    task.npc.transform.position,
                    greetingPosition) <= Mathf.Max(
                        arriveDistance,
                        escortFollowDistance * 0.75f))
            {
                task.remainingTime = Mathf.Max(
                    1f,
                escortGreetingDuration);
                task.stage = TavernTaskStage.Working;
            }

            return;
        }

        if (HandleEscortThreat(task))
        {
            return;
        }

        Vector3 deliveryGreetingPosition = GetEscortCompletionGreetingPosition(task);
        MoveNpc(task.npc, deliveryGreetingPosition);
        MoveEscortCompanion(task);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat("goWorkTask", GetTaskDisplayText(task)));

        if (UpdateTaskTravelWatchdog(
                task,
                deliveryGreetingPosition,
                Mathf.Max(
                    arriveDistance,
                    escortFollowDistance * 0.75f),
                null,
                "EscortDeliveryTravel"))
        {
            return;
        }

        if (Vector2.Distance(
                task.npc.transform.position,
                deliveryGreetingPosition) <= Mathf.Max(
                    arriveDistance,
                    escortFollowDistance * 0.75f) &&
            Vector2.Distance(
                task.escortCompanionNpc.transform.position,
                deliveryGreetingPosition) <= Mathf.Max(
                    arriveDistance,
                    escortFollowDistance))
        {
            if (!task.escortDeliveryConversationStarted)
            {
                task.escortDeliveryConversationStarted = true;
                task.escortDeliveryConversationStep = 0;
                task.remainingTime = 0f;
            }

            if (!UpdateEscortDeliveryDialogue(task))
            {
                ConfirmEscortDelivery(task);
            }
        }
    }

    bool HandleEscortThreat(RunningNpcTask task)
    {
        if (!IsEscortCompanionUsable(task))
        {
            return false;
        }

        if (Time.time < task.escortAvoidUntilTime)
        {
            MoveNpc(task.npc, task.escortAvoidPosition);
            MoveEscortCompanion(task);
            NpcRoleUtility.SetAction(
                task.npc,
                TaskAction("fleeMonsterArea"));
            return true;
        }

        MonsterAI threat = FindEscortThreat(task);
        if (!IsHuntTargetUsable(threat))
        {
            task.escortThreatMonster = null;
            return false;
        }

        task.escortThreatMonster = threat;

        LogThreatDecision(
            task,
            threat,
            "EscortThreat",
            GetNpcCombatPower(task.npc),
            GetMonsterCombatPower(threat),
            0.05f,
            Mathf.Max(0.1f, escortThreatFightPowerRatio));

        if (ShouldFleeEscortThreat(task, threat))
        {
            FleeEscortThreat(task, threat);
            return true;
        }

        if (ShouldFightEscortThreat(task, threat))
        {
            FightEscortThreat(task, threat);
            return true;
        }

        MoveNpc(
            task.npc,
            GetRetreatPosition(task.npc.transform.position, threat.transform.position));
        MoveEscortCompanion(task);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskAction("guardSpiritHerbMonster"));
        return true;
    }

    MonsterAI FindEscortThreat(RunningNpcTask task)
    {
        if (!IsEscortCompanionUsable(task))
        {
            return null;
        }

        Vector3 companionPosition = task.escortCompanionNpc.transform.position;
        Vector3 leaderPosition = task.npc.transform.position;
        Vector3 destinationPosition = task.workPosition;

        MonsterAI best = null;
        float bestDistance = float.PositiveInfinity;
        float detectRadius = Mathf.Max(0.5f, escortThreatDetectRadius);

        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            if (!IsWorkThreatMonster(monster))
            {
                continue;
            }

            float distanceToLeader = Vector2.Distance(leaderPosition, monster.transform.position);
            float distanceToCompanion = Vector2.Distance(companionPosition, monster.transform.position);
            float distanceToDestination = Vector2.Distance(destinationPosition, monster.transform.position);

            if (distanceToLeader > detectRadius &&
                distanceToCompanion > detectRadius &&
                distanceToDestination > detectRadius)
            {
                continue;
            }

            float score = Mathf.Min(
                distanceToLeader,
                Mathf.Min(distanceToCompanion, distanceToDestination));

            if (score < bestDistance)
            {
                bestDistance = score;
                best = monster;
            }
        }

        return best;
    }

    bool ShouldFightEscortThreat(RunningNpcTask task, MonsterAI threat)
    {
        return GetNpcCombatPower(task.npc) >=
            GetMonsterCombatPower(threat) * Mathf.Max(0.1f, escortThreatFightPowerRatio);
    }

    bool ShouldFleeEscortThreat(RunningNpcTask task, MonsterAI threat)
    {
        return CombatPowerUtility.ShouldRetreat(
            task.npc,
            threat != null ? threat.gameObject : null);
    }

    void LogThreatDecision(
        RunningNpcTask task,
        MonsterAI threat,
        string kind,
        int npcPower,
        int monsterPower,
        float fleeThreshold,
        float fightThreshold)
    {
        if (task == null ||
            task.npc == null ||
            threat == null)
        {
            return;
        }

        Debug.LogWarning(
            "[NpcTaskProvider] kind=" + kind +
            " npc=" + task.npc.name +
            " task=" + GetTaskDisplayText(task) +
            " npcPower=" + npcPower +
            " monster=" + threat.monsterName +
            " monsterPower=" + monsterPower +
            " fleeThreshold=" + fleeThreshold.ToString("0.00") +
            " fightThreshold=" + fightThreshold.ToString("0.00") +
            " npcHp=" + CombatPowerUtility.GetCurrentHpRatio(task.npc).ToString("0.00") +
            " npcPos=" + task.npc.transform.position +
            " threatPos=" + threat.transform.position);
    }

    void FightEscortThreat(RunningNpcTask task, MonsterAI threat)
    {
        if (task == null ||
            task.npc == null ||
            threat == null)
        {
            return;
        }

        float distance = Vector2.Distance(
            task.npc.transform.position,
            threat.transform.position);

        if (distance > huntAttackRange)
        {
            MoveNpcToWork(task, threat.transform.position);
            MoveEscortCompanion(task);
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
            MoveEscortCompanion(task);
            return;
        }

        task.remainingTime = Mathf.Max(0.2f, escortAttackInterval);
        NpcRoleUtility.Damage(
            task.npc,
            threat.gameObject,
            NpcRoleUtility.GetAttack(task.npc),
            "bao ve yeu thu");
        MoveEscortCompanion(task);
    }

    void FleeEscortThreat(RunningNpcTask task, MonsterAI threat)
    {
        task.escortAvoidPosition =
            GetRetreatPosition(task.npc.transform.position, threat.transform.position);
        task.escortAvoidUntilTime =
            Time.time + Mathf.Max(1f, escortThreatAvoidDuration);

        MoveNpc(task.npc, task.escortAvoidPosition);
        MoveEscortCompanion(task);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskAction("tooStrongChangeHarvestArea"));
    }

    void MoveEscortCompanion(RunningNpcTask task)
    {
        if (task == null ||
            task.escortCompanionNpc == null ||
            NpcRoleUtility.IsDead(task.escortCompanionNpc))
        {
            return;
        }

        Vector3 followTarget = task.escortDepartedFromCompanion
            ? GetEscortFollowPosition(task)
            : GetEscortCompanionPosition(task.offer);

        if (Vector2.Distance(
                task.escortCompanionNpc.transform.position,
                followTarget) <= Mathf.Max(0.25f, escortFollowDistance * 0.5f))
        {
            return;
        }

        MoveNpc(task.escortCompanionNpc, followTarget);
        NpcRoleUtility.SetAction(
            task.escortCompanionNpc,
            TaskAction("followTaskRoute"));
    }

    bool IsEscortCompanionUsable(RunningNpcTask task)
    {
        return task != null &&
            task.npc != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.Escort &&
            task.escortCompanionNpc != null &&
            task.escortCompletionNpc != null &&
            task.escortCompanionNpc.activeInHierarchy &&
            task.escortCompletionNpc.activeInHierarchy &&
            !NpcRoleUtility.IsDead(task.npc) &&
            !NpcRoleUtility.IsDead(task.escortCompanionNpc) &&
            !NpcRoleUtility.IsDead(task.escortCompletionNpc);
    }

    void ConfirmEscortDelivery(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null)
        {
            return;
        }

        task.escortConfirmed = true;
        NpcRoleUtility.StopForConversation(task.npc);
        if (task.escortCompanionNpc != null)
        {
            RestoreEscortCompanionHome(task);
        }

        if (task.escortCompletionNpc != null)
        {
            NpcRoleUtility.SetAction(
                task.escortCompletionNpc,
                TaskAction("taskCompleted"));
        }

        task.stage = TavernTaskStage.ReturningToTurnIn;
    }

}
