using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Top-level running meal and task state-machine dispatch.
public partial class NpcTaskProvider
{
    void UpdateMeals()
    {
        for (int i = runningMeals.Count - 1; i >= 0; i--)
        {
            RunningTavernMeal meal = runningMeals[i];

            if (meal == null ||
                meal.npc == null ||
                NpcRoleUtility.IsDead(meal.npc))
            {
                FinishMeal(i, false);
                continue;
            }

            if (IsNpcRecoveringFromDamage(meal.npc))
            {
                DisarmMealTravelWatchdog(meal);
                HoldNpcForDamage(meal.npc);
                continue;
            }

            if (meal.stage == TavernMealStage.GoingToMealPoint)
            {
                MoveNpc(meal.npc, meal.mealPosition);
                NpcRoleUtility.SetAction(meal.npc, TaskAction("goTavernMealPoint"));

                if (UpdateMealTravelWatchdog(
                        meal,
                        meal.mealPosition,
                        arriveDistance))
                {
                    continue;
                }

                if (Vector2.Distance(
                        meal.npc.transform.position,
                        meal.mealPosition) <= arriveDistance)
                {
                    meal.stage = TavernMealStage.Eating;
                    DisarmMealTravelWatchdog(meal);
                    NpcEconomy.AddNpcMoney(meal.npc, -mealCost);
                    AddProviderMoney(mealCost);
                    FeedNpc(meal.npc);
                }

                continue;
            }

            DisarmMealTravelWatchdog(meal);
            meal.remainingTime -= Time.deltaTime;
            NpcRoleUtility.SetAction(meal.npc, TaskAction("eatingAtTavern"));

            if (meal.remainingTime <= 0f)
            {
                FinishMeal(i, true);
            }
        }
    }

    void UpdateRunningTasks()
    {
        for (int i = runningTasks.Count - 1; i >= 0; i--)
        {
            RunningNpcTask task = runningTasks[i];

            if (task == null ||
                task.npc == null ||
                NpcRoleUtility.IsDead(task.npc))
            {
                FinishTask(i, false);
                continue;
            }

            if (IsNpcRecoveringFromDamage(task.npc))
            {
                DisarmTaskTravelWatchdog(task);
                HoldNpcForDamage(task.npc);
                continue;
            }

            switch (task.stage)
            {
                case TavernTaskStage.GoingToCounter:
                    Vector3 counterTarget =
                        ResolveActiveCounterTradePosition(
                            task.npc,
                            task.counterPosition);
                    MoveNpc(task.npc, counterTarget);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskAction("goCounterTrade"));

                    if (UpdateTaskTravelWatchdog(
                            task,
                            counterTarget,
                            arriveDistance,
                            null,
                            "GoingToCounter"))
                    {
                        break;
                    }

                    if (IsNpcReadyForCounterTrade(
                            task.npc,
                            counterTarget))
                    {
                        TryTradeAtCounter(task.npc);
                        task.stage = TavernTaskStage.CheckingCounter;
                        task.remainingTime = Mathf.Max(6f, counterCheckDuration);
                    }
                    break;

                case TavernTaskStage.CheckingCounter:
                    DisarmTaskTravelWatchdog(task);
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskAction("checkingCounterTrade"));

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.GoingToBoard;
                    }
                    break;

                case TavernTaskStage.GoingToBoard:
                    MoveNpc(task.npc, task.boardPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("viewTaskBoard", GetRankText(task.offer.rank)));

                    if (UpdateTaskTravelWatchdog(
                            task,
                            task.boardPosition,
                            arriveDistance,
                            null,
                            "GoingToBoard"))
                    {
                        break;
                    }

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.boardPosition) <= arriveDistance)
                    {
                        task.stage = TavernTaskStage.ChoosingTask;
                        task.remainingTime = Mathf.Max(8f, chooseTaskDuration);
                    }
                    break;

                case TavernTaskStage.ChoosingTask:
                    DisarmTaskTravelWatchdog(task);
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("chooseTask", GetRankText(task.offer.rank)));

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.ReturningToProvider;
                    }
                    break;

                case TavernTaskStage.ReturningToProvider:
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskAction("returnProviderReceiveTask"));

                    if (IsNpcInProviderInteractionRange(task.npc))
                    {
                        NpcRoleUtility.StopForConversation(task.npc);
                        NpcRoleUtility.StopForConversation(gameObject);
                        NpcRoleUtility.SetAction(
                            gameObject,
                            TaskActionFormat("giveTask", GetRankText(task.offer.rank), GetOfferTaskName(task.offer)));
                        task.stage = TavernTaskStage.ReceivingTask;
                        task.remainingTime = Mathf.Max(3f, providerReceiveDuration);
                        break;
                    }

                    MoveNpc(task.npc, task.providerPosition);
                    UpdateTaskTravelWatchdog(
                        task,
                        task.providerPosition,
                        GetProviderInteractionDistance(),
                        null,
                        "ReturningToProvider");
                    break;

                case TavernTaskStage.ReceivingTask:
                    DisarmTaskTravelWatchdog(task);
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("receiveTask", GetTaskDisplayText(task)));

                    if (task.remainingTime <= 0f)
                    {
                        PrepareTaskWork(task);
                        task.stage = TavernTaskStage.GoingToWork;
                    }
                    break;

                case TavernTaskStage.GoingToWork:
                    if (IsEscortTask(task))
                    {
                        UpdateEscortTravel(task);
                        break;
                    }

                    if (IsGatherTask(task))
                    {
                        UpdateGatherTravel(task);
                        break;
                    }

                    if (IsHuntTask(task))
                    {
                        UpdateHuntTravel(task);
                        break;
                    }

                    MoveNpcToWork(task, task.workPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("goWorkTask", GetTaskDisplayText(task)));

                    if (UpdateTaskTravelWatchdog(
                            task,
                            task.workPosition,
                            arriveDistance,
                            GetWorkZone(task.offer),
                            "GoingToWork"))
                    {
                        break;
                    }

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.workPosition) <= arriveDistance)
                    {
                        task.stage = TavernTaskStage.Working;
                        task.remainingTime = Mathf.Max(1f, task.offer.workDuration);
                    }
                    break;

                case TavernTaskStage.Working:
                    DisarmTaskTravelWatchdog(task);
                    if (IsEscortTask(task))
                    {
                        UpdateEscortMeeting(task);
                        break;
                    }

                    if (IsGatherTask(task))
                    {
                        UpdateGatherWork(task);
                        break;
                    }

                    if (IsHuntTask(task))
                    {
                        UpdateHuntWork(task);
                        break;
                    }

                    if (IsPatrolTask(task))
                    {
                        UpdatePatrolWork(task);
                        break;
                    }

                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("workingTask", GetTaskDisplayText(task)));

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.ReturningToTurnIn;
                    }
                    break;
                case TavernTaskStage.WaitingForTargetRespawn:
                    task.remainingTime -= Time.deltaTime;
                    if (ShouldCancelWaitingHuntTask(task, out string huntWaitCancelReason))
                    {
                        CancelStuckTask(task, huntWaitCancelReason);
                        break;
                    }

                    if (!IsNpcAtHuntWorkPosition(task))
                    {
                        MoveNpc(
                            task.npc,
                            task.workPosition,
                            GetWorkZone(task.offer));
                        NpcRoleUtility.SetAction(
                            task.npc,
                            TaskActionFormat("huntSearch", BuildHuntProgressText(task)));
                        UpdateTaskTravelWatchdog(
                            task,
                            task.workPosition,
                            Mathf.Max(arriveDistance, huntAttackRange * 0.5f),
                            GetWorkZone(task.offer),
                            "WaitingForTargetRespawn");
                        break;
                    }

                    DisarmTaskTravelWatchdog(task);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("waitHuntRespawn", BuildHuntProgressText(task)));

                    if (task.remainingTime <= 0f || FindHuntTarget(task) != null || FindHuntLootPickup(task) != null)
                    {
                        ResumeWaitingHuntTask(task);
                    }
                    break;
                case TavernTaskStage.ReturningToTurnIn:
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("returnTurnInTask", GetTaskDisplayText(task)));

                    float turnInDistance =
                        GetNpcProviderInteractionDistance(task.npc);

                    bool canTurnIn = turnInDistance <= GetProviderInteractionDistance() ||
                        (HasTaskObjectiveComplete(task) &&
                            turnInDistance <= Mathf.Max(GetProviderInteractionDistance(), stuckTurnInDistance));

                    if (canTurnIn)
                    {
                        NpcRoleUtility.StopForConversation(task.npc);
                        NpcRoleUtility.StopForConversation(gameObject);
                        task.stage = TavernTaskStage.TurningIn;
                        task.remainingTime = Mathf.Max(1f, providerReceiveDuration);
                        break;
                    }

                    MoveNpc(task.npc, task.providerPosition);
                    UpdateTaskTravelWatchdog(
                        task,
                        task.providerPosition,
                        Mathf.Max(
                            GetProviderInteractionDistance(),
                            stuckTurnInDistance),
                        null,
                        "ReturningToTurnIn");
                    break;

                case TavernTaskStage.TurningIn:
                    DisarmTaskTravelWatchdog(task);
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("turnInTask", GetTaskDisplayText(task)));

                    if (task.remainingTime <= 0f)
                    {
                        FinishTask(i, true);
                    }
                    break;
            }
        }
    }

}
