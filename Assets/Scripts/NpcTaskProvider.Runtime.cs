using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public partial class NpcTaskProvider
{
    void CompleteInterruptedWork()
    {
        for (int i = runningTasks.Count - 1; i >= 0; i--)
        {
            RunningNpcTask task = runningTasks[i];
            bool canReward = ShouldRewardInterruptedTask(task) &&
                ConsumeTaskItems(task);

            runningTasks.RemoveAt(i);
            NotifyTaskFinished(task, false);
            CleanupTaskRuntimeState(task);

            if (canReward)
            {
                RewardNpc(task);
                continue;
            }

            if (task != null &&
                task.npc != null)
            {
                NpcRoleUtility.SetAction(task.npc, TaskAction("pausedTask"));
            }
        }

        for (int i = runningMeals.Count - 1; i >= 0; i--)
        {
            RunningTavernMeal meal = runningMeals[i];
            runningMeals.RemoveAt(i);
            UnmarkNpcBusyWithProvider(meal != null ? meal.npc : null);
            ResumeBaseAi(meal);

            if (meal != null &&
                meal.npc != null)
            {
                NpcRoleUtility.SetAction(meal.npc, TaskAction("pausedMeal"));
            }
        }
    }


    bool ShouldRewardInterruptedTask(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null ||
            !HasTaskObjectiveComplete(task))
        {
            return false;
        }

        if (task.stage == TavernTaskStage.ReturningToTurnIn ||
            task.stage == TavernTaskStage.TurningIn)
        {
            return true;
        }

        return IsGatherTask(task) || IsHuntTask(task);
    }

    void Awake()
    {
        providerRb = GetComponent<Rigidbody2D>();
        EnsureProviderInventory();
        lastTaskGoodsTransferDay = GetCurrentWorldDay();
        lastTaskCatalogRefreshDay = GetCurrentWorldDay();
        CaptureStationaryPosition();
        CaptureEscortAnchorPositions();
        FreezeEscortAnchors();
        ConfigureStationaryProvider();
        EnsureExpandedDefaultOffers();
        NormalizeConfiguredOfferText();
        ResolveConfiguredOfferItemReferences();

        NpcSpecialProfession profession =
            GetComponent<NpcSpecialProfession>();

        if (profession == null)
        {
            return;
        }

        profession.professionName = NpcText.Get("professions", "tavernManager");
    }

    void FixedUpdate()
    {
        KeepProviderAtStation();
    }

    void Update()
    {
        UpdateTaskCatalogDailyReset();
        UpdateTaskGoodsDailyTransfer();
        UpdateMeals();
        UpdateRunningTasks();

        assignTimer += Time.deltaTime;
        if (assignTimer < assignInterval)
        {
            return;
        }

        assignTimer = 0f;
        TryServeMeal();
        if (autoAssignNearbyTasks)
        {
            TryStartTaskRequest();
        }
    }

    public bool TryHandleVisitor(GameObject npc, bool autoAssigned = false)
    {
        RefreshExpandedCatalogWhenIdle();

        if (!IsNpcEligibleForProviderService(npc))
        {
            return false;
        }

        if (serveMeals &&
            NpcScheduleController.AllowsActivity(npc, NpcScheduleActivity.Eat) &&
            NeedsMeal(npc) &&
            NpcEconomy.GetNpcMoney(npc) >= mealCost)
        {
            StartMeal(npc);
            return true;
        }

        if (provideTasks &&
            NpcScheduleController.AllowsTask(npc) &&
            offers != null &&
            offers.Length > 0)
        {
            NpcTaskOffer offer = PickOfferFor(npc, autoAssigned);
            if (offer != null &&
                StartTaskRequest(npc, offer, false, autoAssigned))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryStartPlannedTask(
        GameObject npc,
        NpcTaskOffer offer)
    {
        if (offer == null ||
            !IsNpcEligibleForProviderService(npc) ||
            !NpcScheduleController.AllowsTask(npc) ||
            !CanNpcAcceptOffer(npc, offer, true))
        {
            return false;
        }

        return StartTaskRequest(npc, offer, true, true);
    }

    public List<NpcTaskOffer> PickDailyOffersFor(
        GameObject npc,
        int minCount,
        int maxCount)
    {
        RefreshExpandedCatalogWhenIdle();

        List<NpcTaskOffer> result =
            new List<NpcTaskOffer>();

        if (npc == null ||
            offers == null ||
            offers.Length == 0)
        {
            return result;
        }

        int targetCount =
            Random.Range(
                Mathf.Max(1, minCount),
                Mathf.Max(minCount, maxCount) + 1);

        NpcTaskOffer[] shuffled =
            ShuffleOffers();

        foreach (NpcTaskOffer offer in shuffled)
        {
            if (offer == null ||
                !IsOfferWorldAvailable(offer) ||
                !CanNpcAcceptOffer(npc, offer, true))
            {
                continue;
            }

            result.Add(offer);

            if (result.Count >= targetCount)
            {
                break;
            }
        }

        int guard = 0;
        while (result.Count < targetCount &&
            result.Count > 0 &&
            guard < targetCount * 4)
        {
            guard++;
            NpcTaskOffer offer =
                shuffled[Random.Range(0, shuffled.Length)];

            if (offer == null ||
                !IsOfferWorldAvailable(offer) ||
                !CanNpcAcceptOffer(npc, offer, true))
            {
                continue;
            }

            result.Add(offer);
        }

        return result;
    }

    public List<NpcTaskOffer> GetVisibleOffers()
    {
        RefreshExpandedCatalogWhenIdle();

        List<NpcTaskOffer> result = new List<NpcTaskOffer>();

        if (offers == null ||
            offers.Length == 0)
        {
            return result;
        }

        foreach (NpcTaskOffer offer in offers)
        {
            ResolveOfferItemReferences(offer);
            if (!IsOfferWorldAvailable(offer))
            {
                continue;
            }

            result.Add(offer);
        }

        SortOffersByDisplayOrder(result);
        return result;
    }

    public StatItemData GetPlannedRequiredItem(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

        ResolveOfferItemReferences(offer);

        if (offer.requiredItem != null)
        {
            return offer.requiredItem;
        }

        return offer.taskType == NpcTaskType.HarvestAndDeliver
            ? ResolveLinhRiceItem()
            : null;
    }

    public int GetPlannedRequiredAmount(NpcTaskOffer offer)
    {
        StatItemData plannedItem = GetPlannedRequiredItem(offer);

        if (offer == null ||
            plannedItem == null)
        {
            return 0;
        }

        if (!offer.randomizeRequiredItemAmount)
        {
            return GetRequiredAmount(offer);
        }

        int min = Mathf.Max(1, offer.requiredItemAmountMin);
        int max = Mathf.Max(min, offer.requiredItemAmountMax);
        return Random.Range(min, max + 1);
    }

    void TryServeMeal()
    {
        if (!serveMeals)
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                mealServiceRadius,
                npcLayers);

        foreach (Collider2D hit in hits)
        {
            GameObject npc = GetNpcFromHit(hit);

            if (!IsNpcEligibleForProviderService(npc) ||
                !NpcScheduleController.AllowsActivity(npc, NpcScheduleActivity.Eat) ||
                !NeedsMeal(npc) ||
                NpcEconomy.GetNpcMoney(npc) < mealCost)
            {
                continue;
            }

            StartMeal(npc);
            return;
        }
    }

    bool IsNpcEligibleForProviderService(GameObject npc)
    {
        return npc != null &&
            npc != gameObject &&
            !HasBusyNpc(npc) &&
            !NpcRoleUtility.IsDead(npc);
    }

    void TryStartTaskRequest()
    {
        if (!provideTasks ||
            offers == null ||
            offers.Length == 0)
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                assignRadius,
                npcLayers);

        foreach (Collider2D hit in hits)
        {
            GameObject npc = GetNpcFromHit(hit);

            if (!IsNpcEligibleForProviderService(npc) ||
                !NpcScheduleController.AllowsTask(npc))
            {
                continue;
            }

            NpcTaskOffer offer = PickOfferFor(npc, true);
            if (offer == null)
            {
                continue;
            }

            if (StartTaskRequest(npc, offer, false, true))
            {
                return;
            }
        }
    }


    void CaptureStationaryPosition()
    {
        stationaryPosition = providerStandPoint != null
            ? providerStandPoint.position
            : transform.position;
    }

    void ConfigureStationaryProvider()
    {
        if (!keepProviderStationary)
        {
            return;
        }

        NpcMapMover2D mover = GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.enabled = false;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.autonomousActivitiesEnabled = false;
            smartNpc.currentTarget = null;
            if (disableBaseAiWhileStationary)
            {
                smartNpc.enabled = false;
            }
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.currentTarget = null;
            villager.StopMoving();
            if (disableBaseAiWhileStationary)
            {
                villager.enabled = false;
            }
        }
    }

    void KeepProviderAtStation()
    {
        if (!keepProviderStationary)
        {
            return;
        }

        if (providerRb == null)
        {
            providerRb = GetComponent<Rigidbody2D>();
        }

        if (providerRb != null)
        {
            providerRb.linearVelocity = Vector2.zero;
            providerRb.position = stationaryPosition;
        }

        transform.position = new Vector3(
            stationaryPosition.x,
            stationaryPosition.y,
            transform.position.z);
    }

    void StartMeal(GameObject npc)
    {
        RunningTavernMeal meal = new RunningTavernMeal
        {
            npc = npc,
            stage = TavernMealStage.GoingToMealPoint,
            mealPosition = GetMealPosition(),
            remainingTime = Mathf.Max(1f, mealDuration)
        };

        PauseBaseAi(meal);
        runningMeals.Add(meal);
        MarkNpcBusyWithProvider(npc);

        NpcRoleUtility.SetAction(npc, TaskAction("goMealPoint"));
        NpcRoleUtility.SetAction(gameObject, TaskAction("serveMeal"));
    }

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

            switch (meal.stage)
            {
                case TavernMealStage.GoingToMealPoint:
                    MoveNpc(meal.npc, meal.mealPosition);
                    NpcRoleUtility.SetAction(
                        meal.npc,
                        TaskAction("goMealPoint"));

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
                    }
                    break;

                case TavernMealStage.Eating:
                    DisarmMealTravelWatchdog(meal);
                    NpcRoleUtility.StopForConversation(meal.npc, 0.35f);
                    NpcRoleUtility.SetAction(
                        meal.npc,
                        TaskAction("eating"));
                    meal.remainingTime -= Time.deltaTime;

                    if (meal.remainingTime <= 0f)
                    {
                        FeedNpc(meal.npc);
                        FinishMeal(i, true);
                    }
                    break;
            }
        }
    }

    bool StartTaskRequest(GameObject npc, NpcTaskOffer offer)
    {
        return StartTaskRequest(npc, offer, false);
    }

    bool StartTaskRequest(
        GameObject npc,
        NpcTaskOffer offer,
        bool startAtProvider = false,
        bool autoAssigned = false)
    {
        if (npc == null ||
            offer == null ||
            npc == gameObject ||
            HasBusyNpc(npc) ||
            NpcRoleUtility.IsDead(npc) ||
            !NpcScheduleController.AllowsTask(npc) ||
            !CanNpcAcceptOffer(npc, offer, autoAssigned) ||
            !ClaimTaskOffer(offer))
        {
            return false;
        }

        StatItemData requiredItem = ResolveTaskRequiredItem(npc, offer);
        if (RequiresExplicitRequiredItem(offer) &&
            requiredItem == null)
        {
            ReleaseTaskOffer(offer);
            return false;
        }

        int requiredAmount = ResolveTaskRequiredAmount(offer, requiredItem);
        int rewardSpiritStone =
            ResolveTaskRewardSpiritStone(offer, requiredItem, requiredAmount);
        bool formalFlow =
            useFormalTaskReceiveFlow &&
            !startAtProvider;

        RunningNpcTask task = new RunningNpcTask
        {
            npc = npc,
            offer = offer,
            stage = startAtProvider
                ? TavernTaskStage.ReceivingTask
                : formalFlow
                ? (requireCounterCheckBeforeTask
                    ? TavernTaskStage.GoingToCounter
                    : TavernTaskStage.GoingToBoard)
                : TavernTaskStage.GoingToWork,
            counterPosition = GetCounterPosition(npc),
            boardPosition = GetBoardPosition(npc),
            providerPosition = GetProviderPositionFor(npc),
            workPosition = GetWorkPosition(offer),
            remainingTime = startAtProvider
                ? Mathf.Max(1f, providerReceiveDuration)
                : formalFlow
                ? Mathf.Max(8f, chooseTaskDuration)
                : ResolveTaskRuntimeDurationSeconds(offer),
            huntMissionDeadlineWorldHour = GetInitialHuntMissionDeadlineWorldHour(),
            huntMissionDeadlineFallbackTime = Time.time +
                Mathf.Max(1f, maxHuntTaskWaitFallbackSeconds),
            requiredItem = requiredItem,
            requiredAmount = requiredAmount,
            rewardSpiritStone = rewardSpiritStone,
            startingRequiredItemAmount = GetNpcItemAmount(npc, requiredItem)
        };

        if (!formalFlow &&
            !startAtProvider)
        {
            PrepareTaskWork(task);
        }

        PauseBaseAi(task);
        runningTasks.Add(task);
        MarkNpcBusyWithProvider(npc);
        NotifyTaskStarted(task);

        if (offer.taskType == NpcTaskType.Escort)
        {
            LockEscortOffer(offer);
        }

        NpcRoleUtility.SetAction(
            npc,
            startAtProvider
            ? TaskActionFormat("receiveTask", GetTaskDisplayText(task))
            : formalFlow
            ? TaskAction("askProviderFindTask")
            : TaskAction("assignedTask"));

        NpcRoleUtility.SetAction(
            gameObject,
            startAtProvider
            ? TaskActionFormat("giveTask", GetRankText(offer.rank), GetOfferTaskName(offer))
            : formalFlow
            ? TaskAction("showTaskBoard")
            : TaskAction("assignNpcWork"));

        return true;
    }

    void UpdateRunningTasks()
    {
        for (int i = runningTasks.Count - 1; i >= 0; i--)
        {
            RunningNpcTask task = runningTasks[i];
            if (task == null ||
                task.npc == null ||
                task.offer == null ||
                NpcRoleUtility.IsDead(task.npc))
            {
                FinishTask(i, false);
                continue;
            }

            if (IsNpcRecoveringFromDamage(task.npc))
            {
                HoldNpcForDamage(task.npc);
                continue;
            }

            switch (task.stage)
            {
                case TavernTaskStage.GoingToCounter:
                    task.counterPosition = ResolveActiveCounterTradePosition(
                        task.npc,
                        task.counterPosition != Vector3.zero
                            ? task.counterPosition
                            : GetCounterPosition(task.npc));
                    MoveNpc(task.npc, task.counterPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskAction("askProviderFindTask"));

                    if (UpdateTaskTravelWatchdog(
                            task,
                            task.counterPosition,
                            arriveDistance,
                            null,
                            "TaskCounterTravel"))
                    {
                        continue;
                    }

                    if (IsNpcReadyForCounterTrade(task.npc, task.counterPosition))
                    {
                        TryTradeAtCounter(task.npc);
                        task.stage = TavernTaskStage.CheckingCounter;
                        task.remainingTime = Mathf.Max(0.5f, counterCheckDuration);
                        DisarmTaskTravelWatchdog(task);
                    }
                    break;

                case TavernTaskStage.CheckingCounter:
                    DisarmTaskTravelWatchdog(task);
                    NpcRoleUtility.StopForConversation(task.npc, 0.35f);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskAction("askProviderFindTask"));
                    task.remainingTime -= Time.deltaTime;
                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.GoingToBoard;
                        task.boardPosition = GetBoardPosition(task.npc);
                    }
                    break;

                case TavernTaskStage.GoingToBoard:
                    task.boardPosition = GetBoardPosition(task.npc);
                    MoveNpc(task.npc, task.boardPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskAction("showTaskBoard"));

                    if (UpdateTaskTravelWatchdog(
                            task,
                            task.boardPosition,
                            arriveDistance,
                            null,
                            "TaskBoardTravel"))
                    {
                        continue;
                    }

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.boardPosition) <= arriveDistance)
                    {
                        task.stage = TavernTaskStage.ChoosingTask;
                        task.remainingTime = Mathf.Max(0.5f, chooseTaskDuration);
                        DisarmTaskTravelWatchdog(task);
                    }
                    break;

                case TavernTaskStage.ChoosingTask:
                    DisarmTaskTravelWatchdog(task);
                    NpcRoleUtility.StopForConversation(task.npc, 0.35f);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskAction("showTaskBoard"));
                    task.remainingTime -= Time.deltaTime;
                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.ReturningToProvider;
                        task.providerPosition = GetProviderPositionFor(task.npc);
                    }
                    break;

                case TavernTaskStage.ReturningToProvider:
                    task.providerPosition = GetProviderPositionFor(task.npc);
                    MoveNpc(task.npc, task.providerPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("receiveTask", GetTaskDisplayText(task)));

                    if (UpdateTaskTravelWatchdog(
                            task,
                            task.providerPosition,
                            GetProviderInteractionDistance(),
                            null,
                            "TaskProviderReturn"))
                    {
                        continue;
                    }

                    if (IsNpcInProviderInteractionRange(task.npc))
                    {
                        task.stage = TavernTaskStage.ReceivingTask;
                        task.remainingTime = Mathf.Max(0.5f, providerReceiveDuration);
                        DisarmTaskTravelWatchdog(task);
                    }
                    break;

                case TavernTaskStage.ReceivingTask:
                    DisarmTaskTravelWatchdog(task);
                    NpcRoleUtility.StopForConversation(task.npc, 0.35f);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("receiveTask", GetTaskDisplayText(task)));
                    task.remainingTime -= Time.deltaTime;
                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.GoingToWork;
                        task.remainingTime = Mathf.Max(
                            1f,
                            ResolveTaskRuntimeDurationSeconds(task.offer));
                        PrepareTaskWork(task);
                    }
                    break;

                case TavernTaskStage.GoingToWork:
                    if (IsEscortTask(task))
                    {
                        UpdateEscortTravel(task);
                    }
                    else if (IsGatherTask(task))
                    {
                        UpdateGatherTravel(task);
                    }
                    else if (IsHuntTask(task))
                    {
                        UpdateHuntTravel(task);
                    }
                    else if (IsPatrolTask(task))
                    {
                        UpdatePatrolWork(task);
                    }
                    else if (IsFrontierWatchTask(task))
                    {
                        UpdateFrontierWatchWork(task);
                    }
                    else
                    {
                        MoveNpcToWork(task, task.workPosition);
                        NpcRoleUtility.SetAction(
                            task.npc,
                            TaskActionFormat("goWorkTask", GetTaskDisplayText(task)));

                        if (UpdateTaskTravelWatchdog(
                                task,
                                task.workPosition,
                                arriveDistance,
                                GetWorkZone(task.offer),
                                "TaskWorkTravel"))
                        {
                            continue;
                        }

                        if (Vector2.Distance(
                                task.npc.transform.position,
                                task.workPosition) <= arriveDistance)
                        {
                            task.stage = TavernTaskStage.Working;
                            DisarmTaskTravelWatchdog(task);
                        }
                    }
                    break;

                case TavernTaskStage.Working:
                    if (IsEscortTask(task))
                    {
                        if (task.escortDepartedFromCompanion)
                        {
                            UpdateEscortTravel(task);
                        }
                        else
                        {
                            UpdateEscortMeeting(task);
                        }
                    }
                    else if (IsGatherTask(task))
                    {
                        UpdateGatherWork(task);
                    }
                    else if (IsHuntTask(task))
                    {
                        UpdateHuntWork(task);
                    }
                    else if (IsPatrolTask(task))
                    {
                        UpdatePatrolWork(task);
                    }
                    else if (IsFrontierWatchTask(task))
                    {
                        UpdateFrontierWatchWork(task);
                    }
                    else
                    {
                        DisarmTaskTravelWatchdog(task);
                        NpcRoleUtility.StopForConversation(task.npc, 0.35f);
                        NpcRoleUtility.SetAction(
                            task.npc,
                            TaskActionFormat("workingTask", GetTaskDisplayText(task)));
                        task.remainingTime -= Time.deltaTime;
                        if (task.remainingTime <= 0f)
                        {
                            task.stage = TavernTaskStage.ReturningToTurnIn;
                        }
                    }
                    break;

                case TavernTaskStage.ReturningToTurnIn:
                    task.providerPosition = GetProviderPositionFor(task.npc);
                    MoveNpc(task.npc, task.providerPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("taskCompleted", GetTaskDisplayText(task)));

                    if (UpdateTaskTravelWatchdog(
                            task,
                            task.providerPosition,
                            GetProviderInteractionDistance(),
                            null,
                            "TaskTurnInTravel"))
                    {
                        continue;
                    }

                    if (IsNpcInProviderInteractionRange(task.npc))
                    {
                        task.stage = TavernTaskStage.TurningIn;
                        task.remainingTime = Mathf.Max(0.5f, providerReceiveDuration);
                        DisarmTaskTravelWatchdog(task);
                    }
                    break;

                case TavernTaskStage.TurningIn:
                    DisarmTaskTravelWatchdog(task);
                    NpcRoleUtility.StopForConversation(task.npc, 0.35f);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskActionFormat("taskCompleted", GetTaskDisplayText(task)));
                    task.remainingTime -= Time.deltaTime;
                    if (task.remainingTime <= 0f)
                    {
                        FinishTask(i, true);
                    }
                    break;

                case TavernTaskStage.WaitingForTargetRespawn:
                    if (IsHuntTask(task))
                    {
                        task.remainingTime -= Time.deltaTime;
                        if (task.remainingTime <= 0f)
                        {
                            ResumeWaitingHuntTask(task);
                        }
                        else
                        {
                            if (task.npc != null)
                            {
                                if (IsNpcAtHuntWorkPosition(task))
                                {
                                    NpcRoleUtility.SetAction(
                                        task.npc,
                                        TaskActionFormat(
                                            "waitHuntRespawn",
                                            BuildHuntProgressText(task)));
                                }
                                else
                                {
                                    MoveNpc(
                                        task.npc,
                                        task.workPosition,
                                        GetWorkZone(task.offer));
                                    NpcRoleUtility.SetAction(
                                        task.npc,
                                        TaskActionFormat(
                                            "huntSearch",
                                            BuildHuntProgressText(task)));
                                }
                            }
                        }
                    }
                    else
                    {
                        task.stage = TavernTaskStage.GoingToWork;
                    }
                    break;
            }
        }
    }

    StatItemData ResolveTaskRequiredItem(GameObject npc, NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

        ResolveOfferItemReferences(offer);

        if (offer.requiredItem != null)
        {
            return offer.requiredItem;
        }

        if (offer.taskType == NpcTaskType.HuntMonster)
        {
            return FindDeathLootForHuntOffer(offer);
        }

        if (offer.taskType == NpcTaskType.HarvestAndDeliver)
        {
            return ResolveLinhRiceItem();
        }

        if (offer.taskType != NpcTaskType.GatherResource)
        {
            return null;
        }

        WorldStatItemPickup pickup =
            FindRandomGatherPickupInMaThuSonMach();

        return pickup != null
            ? pickup.item
            : null;
    }

    bool RequiresExplicitRequiredItem(NpcTaskOffer offer)
    {
        return offer != null &&
            offer.taskType == NpcTaskType.HarvestAndDeliver;
    }

    StatItemData ResolveLinhRiceItem()
    {
        if (IsLinhRiceItem(linhRiceItem))
        {
            GameSaveSystem.RegisterItem(linhRiceItem);
            return linhRiceItem;
        }

        StatItemData item = FindLinhRiceItemInResourceFields();
        if (item == null)
        {
            item = FindLinhRiceItemInPickups();
        }

        if (item == null)
        {
            item = FindLinhRiceItemInVillagers();
        }

        if (item != null)
        {
            linhRiceItem = item;
            GameSaveSystem.RegisterItem(item);
        }

        return item;
    }

    StatItemData FindLinhRiceItemInResourceFields()
    {
        foreach (WorldResourceField field in WorldResourceField.Fields)
        {
            if (field == null || field.items == null)
            {
                continue;
            }

            foreach (ResourceFieldItemEntry entry in field.items)
            {
                if (entry != null && IsLinhRiceItem(entry.item))
                {
                    return entry.item;
                }
            }
        }

        return null;
    }

    StatItemData FindLinhRiceItemInPickups()
    {
        foreach (WorldStatItemPickup pickup in FindObjectsByType<WorldStatItemPickup>(FindObjectsInactive.Exclude))
        {
            if (pickup != null && IsLinhRiceItem(pickup.item))
            {
                return pickup.item;
            }
        }

        return null;
    }

    StatItemData FindLinhRiceItemInVillagers()
    {
        foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Include))
        {
            if (villager == null)
            {
                continue;
            }

            HarvestJob harvestJob = villager.GetComponent<HarvestJob>();
            if (harvestJob != null && IsLinhRiceItem(harvestJob.farmProduct))
            {
                return harvestJob.farmProduct;
            }
        }

        return null;
    }

    bool IsLinhRiceItem(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.ItemId == LinhRiceItemId)
        {
            return true;
        }

        string itemName = !string.IsNullOrWhiteSpace(item.itemName)
            ? item.itemName
            : item.name;

        if (string.IsNullOrWhiteSpace(itemName))
        {
            return false;
        }

        string normalized = RemoveDiacritics(itemName).Trim().ToLowerInvariant();
        return normalized == "lua" ||
            normalized == "lúa" ||
            normalized.Contains("linh gao") ||
            normalized.Contains("linh gạo");
    }

    string RemoveDiacritics(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);

        foreach (char character in normalized)
        {
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    bool HasAvailableTaskPickup(
        NpcTaskOffer offer,
        StatItemData requiredItem)
    {
        if (offer == null)
        {
            return false;
        }

        if (!IsGatherTaskType(offer.taskType))
        {
            return true;
        }

        if (RequiresExplicitRequiredItem(offer) && requiredItem == null)
        {
            return false;
        }

        return WorldResourceField.GetNearestAvailablePickupInAllFields(
            GetWorkPosition(offer),
            requiredItem,
            GetGatherRequiredZone(offer),
            null,
            false,
            null) != null;
    }

    StatItemData FindDeathLootForHuntOffer(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            if (!IsWorkThreatMonster(monster))
            {
                continue;
            }

            if (!MatchesRequiredHuntTargetType(offer.requiredHuntTargetType, monster.huntTargetType))
            {
                continue;
            }

            if (!MatchesHuntMonsterDifficulty(offer, monster))
            {
                continue;
            }

            StatItemData loot = monster.GetDeathLoot();
            if (offer.requiredItem != null &&
                loot != offer.requiredItem)
            {
                continue;
            }

            if (loot != null)
            {
                return loot;
            }
        }

        return null;
    }

    WorldStatItemPickup FindRandomGatherPickupInMaThuSonMach()
    {
        WorldStatItemPickup selected = null;
        int seen = 0;

        foreach (WorldResourceField field in WorldResourceField.Fields)
        {
            if (field == null ||
                !field.isActiveAndEnabled)
            {
                continue;
            }

            WorldStatItemPickup[] pickups =
                field.GetComponentsInChildren<WorldStatItemPickup>(true);

            foreach (WorldStatItemPickup pickup in pickups)
            {
                if (!IsGatherPickupUsable(pickup, null))
                {
                    continue;
                }

                NpcMapArea area = NpcMapArea.FindArea(pickup.transform.position);
                if (area == null ||
                    area.zone != NpcMapZone.MaThuSonMach)
                {
                    continue;
                }

                seen++;
                if (Random.Range(0, seen) == 0)
                {
                    selected = pickup;
                }
            }
        }

        return selected;
    }

    ItemInventory GetOrCreateInventory(GameObject npc)
    {
        ItemInventory inventory = npc.GetComponent<ItemInventory>();
        if (inventory == null)
        {
            inventory = npc.AddComponent<ItemInventory>();
            inventory.shareRuntimeItems = false;
        }

        return inventory;
    }

    void FinishMeal(int index, bool completed)
    {
        RunningTavernMeal meal = runningMeals[index];
        runningMeals.RemoveAt(index);
        UnmarkNpcBusyWithProvider(meal != null ? meal.npc : null);
        ResumeBaseAi(meal);

        if (completed &&
            meal != null &&
            meal.npc != null)
        {
            NpcRoleUtility.SetAction(meal.npc, TaskAction("mealComplete"));
        }
    }

    void FinishTask(int index, bool completed)
    {
        RunningNpcTask task = runningTasks[index];

        if (completed &&
            task != null &&
            task.npc != null &&
            task.offer != null &&
            !ConsumeTaskItems(task))
        {
            RestartTaskWorkAfterMissingTurnInItems(task);
            return;
        }

        runningTasks.RemoveAt(index);
        NotifyTaskFinished(task, completed);
        CleanupTaskRuntimeState(task);

        if (!completed ||
            task == null ||
            task.npc == null ||
            task.offer == null)
        {
            return;
        }

        RecordCompletedOffer(task.npc, task.offer);
        MoveOfferToEnd(task.offer);
        RewardNpc(task);

        NpcScheduleController schedule =
            task.npc != null
                ? NpcScheduleController.GetSchedule(task.npc)
                : null;
        if (schedule != null &&
            schedule.enforceSchedule &&
            (schedule.CurrentActivity == NpcScheduleActivity.DoMission ||
            schedule.CurrentActivity == NpcScheduleActivity.TakeTask))
        {
            schedule.MarkCurrentSlotActivityCompleted(schedule.CurrentActivity);
        }
    }

    void RestartTaskWorkAfterMissingTurnInItems(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        task.collectedAmount = Mathf.Min(
            Mathf.Max(0, task.collectedAmount),
            GetTaskInventoryProgress(task));
        task.targetPickup = null;
        task.targetLootPickup = null;
        task.targetMonster = null;
        task.threatMonster = null;
        task.stage = TavernTaskStage.GoingToWork;
        task.remainingTime = Mathf.Max(
            1f,
            ResolveTaskRuntimeDurationSeconds(
                task.offer));
        DisarmTaskTravelWatchdog(task);
        PrepareTaskWork(task);

        if (task.npc != null)
        {
            NpcRoleUtility.SetAction(
                task.npc,
                TaskActionFormat(
                    "missingTurnInItems",
                    GetTaskDisplayText(task)));
        }
    }


}
