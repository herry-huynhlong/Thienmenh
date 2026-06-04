using System.Collections.Generic;
using UnityEngine;

public enum NpcTaskType
{
    GatherResource,
    HuntMonster,
    Cultivate,
    Patrol,
    Deliver
}

public enum NpcTaskRank
{
    Ha,
    Trung,
    Thuong
}

public enum TavernTaskStage
{
    GoingToCounter,
    CheckingCounter,
    GoingToBoard,
    ChoosingTask,
    ReturningToProvider,
    ReceivingTask,
    GoingToWork,
    Working,
    ReturningToTurnIn,
    TurningIn
}

public enum TavernMealStage
{
    GoingToMealPoint,
    Eating
}

[System.Serializable]
public class NpcTaskOffer
{
    public string taskName = "Thu tháº­p tÃ i nguyÃªn";
    public NpcTaskType taskType = NpcTaskType.GatherResource;
    public NpcTaskRank rank = NpcTaskRank.Ha;
    public CultivationRealm minRealm = CultivationRealm.QiRefining;
    [Range(1, 9)]
    public int minRealmStage = 1;
    public int rewardSpiritStone = 20;
    public int rewardCultivationExp;
    public StatItemData rewardItem;
    public int rewardItemAmount;
    public float workDuration = 12f;

    [Header("Objective")]
    public StatItemData requiredItem;
    [Min(1)] public int requiredAmount = 1;
    public bool randomizeRequiredItemAmount = true;
    [Min(1)] public int requiredItemAmountMin = 5;
    [Min(1)] public int requiredItemAmountMax = 10;
    public bool autoPriceRequiredItemReward = true;
    [Min(0f)] public float requiredItemRewardMarkupMin = 0.33f;
    [Min(0f)] public float requiredItemRewardMarkupMax = 0.5f;
    public bool useRankRewardMultiplier = true;
    public bool consumeRequiredItemsOnTurnIn = true;
    [Min(1)] public int requiredMonsterKills = 1;
}

class RunningNpcTask
{
    public GameObject npc;
    public NpcTaskOffer offer;
    public TavernTaskStage stage;
    public Vector3 counterPosition;
    public Vector3 boardPosition;
    public Vector3 providerPosition;
    public Vector3 workPosition;
    public float remainingTime;
    public WorldStatItemPickup targetPickup;
    public StatItemData requiredItem;
    public int requiredAmount;
    public int rewardSpiritStone;
    public int collectedAmount;
    public int defeatedMonsterCount;
    public int startingRequiredItemAmount;
    public MonsterAI targetMonster;
    public MonsterAI threatMonster;
    public Vector3 avoidPosition;
    public float avoidUntilTime;
    public Behaviour pausedBaseAi;
    public bool pausedBaseAiWasEnabled;
}

class RunningTavernMeal
{
    public GameObject npc;
    public TavernMealStage stage;
    public Vector3 mealPosition;
    public float remainingTime;
    public Behaviour pausedBaseAi;
    public bool pausedBaseAiWasEnabled;
}

class PendingTaskGoods
{
    public StatItemData item;
    public int amount;
}

public class NpcTaskProvider : MonoBehaviour
{
    static readonly List<NpcTaskProvider> providers =
        new List<NpcTaskProvider>();

    public static NpcTaskProvider FindNearestProvider(Vector3 position)
    {
        NpcTaskProvider best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (NpcTaskProvider provider in providers)
        {
            if (provider == null ||
                !provider.isActiveAndEnabled ||
                !provider.provideTasks ||
                provider.offers == null ||
                provider.offers.Length == 0)
            {
                continue;
            }

            float distance = Vector2.Distance(position, provider.transform.position);
            if (distance < bestDistance)
            {
                best = provider;
                bestDistance = distance;
            }
        }

        return best;
    }
    [Header("Tavern Service")]
    public bool serveMeals = true;
    public int mealCost = 1;
    [Range(0f, 100f)]
    public float mealHungerThreshold = 55f;
    public float mealServiceRadius = 2f;
    public Transform mealPoint;
    public float mealDuration = 5f;

    [Header("Counter Flow")]
    public bool useFormalTaskReceiveFlow = true;
    public bool requireCounterCheckBeforeTask = true;
    public Transform counterPoint;
    public float counterCheckDuration = 4f;

    [Header("Task Provider")]
    public bool provideTasks = true;
    public float assignRadius = 2.5f;
    public float assignInterval = 5f;
    public float arriveDistance = 0.35f;
    public float gatherInteractDistance = 1.1f;
    public float fallbackMoveSpeed = 1.6f;
    public bool pauseBaseAiWhileWorking = true;
    public LayerMask npcLayers = ~0;
    public Transform taskBoardPoint;
    public Transform providerPoint;
    public float chooseTaskDuration = 8f;
    public float providerReceiveDuration = 5f;
    public float providerTalkDistance = 0.75f;
    public float stuckTurnInDistance = 2.25f;
    public float huntAttackRange = 1.4f;
    public float huntAttackInterval = 1.2f;
    [Header("Task Danger Response")]
    public float gatherThreatDetectRadius = 4f;
    public float gatherThreatAvoidRadius = 6f;
    public float gatherThreatAvoidDuration = 8f;
    public float gatherThreatFightPowerRatio = 1.05f;
    public float gatherThreatFleePowerRatio = 0.85f;
    public Transform defaultWorkPoint;
    public Transform huntPoint;
    public Transform gatherPoint;
    public Transform patrolPoint;
    public Transform deliverPoint;

    [Header("Provider Placement")]
    public bool keepProviderStationary = true;
    public Transform providerStandPoint;

    [Header("Reward Wallet")]
    public ItemInventory inventory;
    [InspectorName("Quá»¹ thÆ°á»Ÿng Linh Tháº¡ch ban Ä‘áº§u")]
    public int startingRewardMoney = 100000;
    [InspectorName("Dá»± trá»¯ Linh Tháº¡ch tá»‘i thiá»ƒu")]
    public int minimumRewardMoneyReserve = 50000;
    public bool refillRewardMoneyWhenLow = true;
    [SerializeField, InspectorName("Quá»¹ thÆ°á»Ÿng Linh Tháº¡ch")] int serviceRewardMoney;

    public int CurrentRewardMoney => GetProviderMoney();

    [Header("Task Goods Handoff")]
    public bool storeTurnedInTaskGoods = true;
    public bool transferTaskGoodsToCounterAtDayEnd = true;
    public NpcCounterBroker taskGoodsReceiver;
    public bool useActiveCounterBrokerIfReceiverMissing = true;

    
    [Header("Default Bounty Pricing")]
    public bool useDefaultRankBountyMultipliers = true;
    public Vector2 haRankBountyMultiplier = new Vector2(1f, 1.5f);
    public Vector2 trungRankBountyMultiplier = new Vector2(3f, 3.5f);
    public Vector2 thuongRankBountyMultiplier = new Vector2(5f, 5.5f);
[Header("Forest Depth")]
    public NpcMapArea forestSearchArea;
    public Transform forestEntryPoint;
    public Transform forestDeepPoint;
    [Range(0f, 1f)]
    public float gatherDepthMin = 0.15f;
    [Range(0f, 1f)]
    public float gatherDepthMax = 0.65f;
    [Range(0f, 1f)]
    public float huntDepthMin = 0.45f;
    [Range(0f, 1f)]
    public float huntDepthMax = 0.95f;
    public int forestPointPickAttempts = 24;

    [Header("Offers")]
    public NpcTaskOffer[] offers =
    {
        new NpcTaskOffer
        {
            taskName = "Thu tháº­p linh tháº£o háº¡ pháº©m",
            taskType = NpcTaskType.GatherResource,
            rank = NpcTaskRank.Ha,
            minRealm = CultivationRealm.QiRefining,
            minRealmStage = 1,
            rewardSpiritStone = 10,
            rewardCultivationExp = 5,
            workDuration = 10f
        },
        new NpcTaskOffer
        {
            taskName = "Tuáº§n tra ngoÃ i lÃ ng",
            taskType = NpcTaskType.Patrol,
            rank = NpcTaskRank.Trung,
            minRealm = CultivationRealm.GoldenCore,
            minRealmStage = 1,
            rewardSpiritStone = 45,
            rewardCultivationExp = 25,
            workDuration = 18f
        },
        new NpcTaskOffer
        {
            taskName = "SÄƒn yÃªu thÃº nguy hiá»ƒm",
            taskType = NpcTaskType.HuntMonster,
            rank = NpcTaskRank.Thuong,
            requiredMonsterKills = 1,
            minRealm = CultivationRealm.NascentSoul,
            minRealmStage = 1,
            rewardSpiritStone = 150,
            rewardCultivationExp = 90,
            workDuration = 30f
        }
    };

    readonly List<RunningNpcTask> runningTasks =
        new List<RunningNpcTask>();

    readonly List<RunningTavernMeal> runningMeals =
        new List<RunningTavernMeal>();

    readonly List<PendingTaskGoods> pendingTaskGoods =
        new List<PendingTaskGoods>();

    float assignTimer;
    int lastTaskGoodsTransferDay = -1;
    Rigidbody2D providerRb;
    Vector3 stationaryPosition;

    void OnEnable()
    {
        if (!providers.Contains(this))
        {
            providers.Add(this);
        }

        CaptureStationaryPosition();
        ConfigureStationaryProvider();
    }

    void OnDisable()
    {
        CompleteInterruptedWork();
        providers.Remove(this);
    }

    void CompleteInterruptedWork()
    {
        for (int i = runningTasks.Count - 1; i >= 0; i--)
        {
            RunningNpcTask task = runningTasks[i];
            bool canReward = ShouldRewardInterruptedTask(task) &&
                ConsumeTaskItems(task);

            runningTasks.RemoveAt(i);
            ResumeBaseAi(task);

            if (canReward)
            {
                RewardNpc(task);
                continue;
            }

            if (task != null &&
                task.npc != null)
            {
                NpcRoleUtility.SetAction(task.npc, "Tam dung nhiem vu");
            }
        }

        for (int i = runningMeals.Count - 1; i >= 0; i--)
        {
            RunningTavernMeal meal = runningMeals[i];
            runningMeals.RemoveAt(i);
            ResumeBaseAi(meal);

            if (meal != null &&
                meal.npc != null)
            {
                NpcRoleUtility.SetAction(meal.npc, "Tam dung bua an");
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
        CaptureStationaryPosition();
        ConfigureStationaryProvider();

        NpcSpecialProfession profession =
            GetComponent<NpcSpecialProfession>();

        if (profession == null)
        {
            profession = gameObject.AddComponent<NpcSpecialProfession>();
        }

        profession.professionName = "Quáº£n Sá»± Tá»­u QuÃ¡n";
    }

    void FixedUpdate()
    {
        KeepProviderAtStation();
    }

    void Update()
    {
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
        TryStartTaskRequest();
    }

    public bool TryHandleVisitor(GameObject npc)
    {
        if (npc == null ||
            npc == gameObject ||
            HasBusyNpc(npc) ||
            NpcRoleUtility.IsDead(npc))
        {
            return false;
        }

        if (serveMeals &&
            NeedsMeal(npc) &&
            NpcEconomy.GetNpcMoney(npc) >= mealCost)
        {
            StartMeal(npc);
            return true;
        }

        if (provideTasks &&
            offers != null &&
            offers.Length > 0)
        {
            NpcTaskOffer offer = PickOfferFor(npc);
            if (offer != null)
            {
                StartTaskRequest(npc, offer);
                return true;
            }
        }

        return false;
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

            if (npc == null ||
                npc == gameObject ||
                HasBusyNpc(npc) ||
                !NeedsMeal(npc) ||
                NpcEconomy.GetNpcMoney(npc) < mealCost)
            {
                continue;
            }

            StartMeal(npc);
            return;
        }
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

            if (npc == null ||
                npc == gameObject ||
                HasBusyNpc(npc))
            {
                continue;
            }

            NpcTaskOffer offer = PickOfferFor(npc);
            if (offer == null)
            {
                continue;
            }

            StartTaskRequest(npc, offer);
            return;
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

        NpcRoleUtility.SetAction(npc, "Di toi khu an");
        NpcRoleUtility.SetAction(gameObject, "Phuc vu bua an");
    }

    void StartTaskRequest(GameObject npc, NpcTaskOffer offer)
    {
        StatItemData requiredItem = ResolveTaskRequiredItem(npc, offer);
        int requiredAmount = ResolveTaskRequiredAmount(offer, requiredItem);
        int rewardSpiritStone =
            ResolveTaskRewardSpiritStone(offer, requiredItem, requiredAmount);

        RunningNpcTask task = new RunningNpcTask
        {
            npc = npc,
            offer = offer,
            stage = useFormalTaskReceiveFlow
                ? (requireCounterCheckBeforeTask
                    ? TavernTaskStage.GoingToCounter
                    : TavernTaskStage.GoingToBoard)
                : TavernTaskStage.GoingToWork,
            counterPosition = GetCounterPosition(npc),
            boardPosition = GetBoardPosition(),
            providerPosition = GetProviderPosition(),
            workPosition = GetWorkPosition(offer),
            remainingTime = useFormalTaskReceiveFlow
                ? Mathf.Max(8f, chooseTaskDuration)
                : Mathf.Max(1f, offer != null ? offer.workDuration : 1f),
            requiredItem = requiredItem,
            requiredAmount = requiredAmount,
            rewardSpiritStone = rewardSpiritStone,
            startingRequiredItemAmount = GetNpcItemAmount(npc, requiredItem)
        };

        if (!useFormalTaskReceiveFlow)
        {
            PrepareTaskWork(task);
        }

        PauseBaseAi(task);
        runningTasks.Add(task);

        NpcRoleUtility.SetAction(
            npc,
            useFormalTaskReceiveFlow
            ? "Hoi quan su tim nhiem vu"
            : "Nhan viec duoc giao");

        NpcRoleUtility.SetAction(
            gameObject,
            useFormalTaskReceiveFlow
            ? "Chi bang nhiem vu cho khach"
            : "Giao viec cho NPC");
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

            if (meal.stage == TavernMealStage.GoingToMealPoint)
            {
                MoveNpc(meal.npc, meal.mealPosition);
                NpcRoleUtility.SetAction(meal.npc, "Äi tá»›i khu Äƒn trong tá»­u quÃ¡n");

                if (Vector2.Distance(
                        meal.npc.transform.position,
                        meal.mealPosition) <= arriveDistance)
                {
                    meal.stage = TavernMealStage.Eating;
                    NpcEconomy.AddNpcMoney(meal.npc, -mealCost);
                    AddProviderMoney(mealCost);
                    FeedNpc(meal.npc);
                }

                continue;
            }

            meal.remainingTime -= Time.deltaTime;
            NpcRoleUtility.SetAction(meal.npc, "Äang Äƒn uá»‘ng táº¡i tá»­u quÃ¡n");

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

            switch (task.stage)
            {
                case TavernTaskStage.GoingToCounter:
                    MoveNpc(task.npc, task.counterPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Äáº¿n trÆ°á»ng quay kiá»ƒm tra mua bÃ¡n");

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.counterPosition) <= arriveDistance)
                    {
                        TryTradeAtCounter(task.npc);
                        task.stage = TavernTaskStage.CheckingCounter;
                        task.remainingTime = Mathf.Max(6f, counterCheckDuration);
                    }
                    break;

                case TavernTaskStage.CheckingCounter:
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Äang kiá»ƒm tra mua bÃ¡n táº¡i trÆ°á»ng quay");

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.GoingToBoard;
                    }
                    break;

                case TavernTaskStage.GoingToBoard:
                    MoveNpc(task.npc, task.boardPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Xem báº£ng nhiá»‡m vá»¥ " + GetRankText(task.offer.rank));

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.boardPosition) <= arriveDistance)
                    {
                        task.stage = TavernTaskStage.ChoosingTask;
                        task.remainingTime = Mathf.Max(8f, chooseTaskDuration);
                    }
                    break;

                case TavernTaskStage.ChoosingTask:
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Chá»n nhiá»‡m vá»¥ " + GetRankText(task.offer.rank));

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.ReturningToProvider;
                    }
                    break;

                case TavernTaskStage.ReturningToProvider:
                    MoveNpc(task.npc, task.providerPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Quay láº¡i quáº£n sá»± nháº­n nhiá»‡m vá»¥");

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.providerPosition) <= providerTalkDistance)
                    {
                        NpcRoleUtility.StopForConversation(task.npc);
                        NpcRoleUtility.StopForConversation(gameObject);
                        NpcRoleUtility.SetAction(
                            gameObject,
                            "Giao nhiá»‡m vá»¥ " + GetRankText(task.offer.rank) + ": " + task.offer.taskName);
                        task.stage = TavernTaskStage.ReceivingTask;
                        task.remainingTime = Mathf.Max(3f, providerReceiveDuration);
                    }
                    break;

                case TavernTaskStage.ReceivingTask:
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Dang nhan nhiem vu " + GetTaskDisplayText(task));

                    if (task.remainingTime <= 0f)
                    {
                        PrepareTaskWork(task);
                        task.stage = TavernTaskStage.GoingToWork;
                    }
                    break;

                case TavernTaskStage.GoingToWork:
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
                        "Di lam nhiem vu " + GetTaskDisplayText(task));

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.workPosition) <= arriveDistance)
                    {
                        task.stage = TavernTaskStage.Working;
                        task.remainingTime = Mathf.Max(1f, task.offer.workDuration);
                    }
                    break;

                case TavernTaskStage.Working:
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

                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Dang lam nhiem vu " + GetTaskDisplayText(task));

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.ReturningToTurnIn;
                    }
                    break;

                case TavernTaskStage.ReturningToTurnIn:
                    MoveNpc(task.npc, task.providerPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Mang ket qua ve tra nhiem vu " + GetTaskDisplayText(task));

                    float turnInDistance = Vector2.Distance(
                        task.npc.transform.position,
                        task.providerPosition);

                    bool canTurnIn = turnInDistance <= providerTalkDistance ||
                        (HasTaskObjectiveComplete(task) &&
                            turnInDistance <= Mathf.Max(providerTalkDistance, stuckTurnInDistance));

                    if (canTurnIn)
                    {
                        NpcRoleUtility.StopForConversation(task.npc);
                        NpcRoleUtility.StopForConversation(gameObject);
                        task.stage = TavernTaskStage.TurningIn;
                        task.remainingTime = Mathf.Max(1f, providerReceiveDuration);
                    }
                    break;

                case TavernTaskStage.TurningIn:
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Dang tra nhiem vu " + GetTaskDisplayText(task));

                    if (task.remainingTime <= 0f)
                    {
                        FinishTask(i, true);
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

        if (offer.requiredItem != null)
        {
            return offer.requiredItem;
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

    void PrepareTaskWork(RunningNpcTask task)
    {
        if (task == null)
        {
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
                ReserveGatherPickupForTask(task.targetPickup);
                task.workPosition = task.targetPickup.transform.position;
            }
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

        if (!IsGatherPickupUsable(task.targetPickup, GetTaskRequiredItem(task)))
        {
            task.targetPickup = FindGatherPickup(task);
            ReserveGatherPickupForTask(task.targetPickup);
        }

        if (task.targetPickup == null)
        {
            MoveNpcToWork(task, task.workPosition);
            NpcRoleUtility.SetAction(
                task.npc,
                "Vao khu gatherPoint tim " + GetTaskRequiredItemName(task) + " " +
                BuildGatherProgressText(task));
            return;
        }

        ReserveGatherPickupForTask(task.targetPickup);
        task.workPosition = task.targetPickup.transform.position;
        MoveNpcToWork(task, task.workPosition);
        NpcRoleUtility.SetAction(
            task.npc,
            "Den gatherPoint hai " + GetTaskRequiredItemName(task) + " " +
            BuildGatherProgressText(task));

        if (IsNpcAtGatherPickup(task))
        {
            task.stage = TavernTaskStage.Working;
            task.remainingTime = GetGatherWorkDuration(task);
        }
    }
    void ReserveGatherPickupForTask(WorldStatItemPickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        pickup.allowNpcPickup = true;
        pickup.requireNpcHarvestAction = true;
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
        if (HasGatherObjectiveComplete(task))
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        if (HandleGatherThreat(task))
        {
            return;
        }

        if (!IsGatherPickupUsable(task.targetPickup, GetTaskRequiredItem(task)))
        {
            task.targetPickup = null;
            task.stage = TavernTaskStage.GoingToWork;
            return;
        }

        NpcRoleUtility.StopForConversation(task.npc, 0.35f);
        task.remainingTime -= Time.deltaTime;
        NpcRoleUtility.SetAction(
            task.npc,
            "\u0110ang h\u00e1i " + GetTaskRequiredItemName(task) + " " +
            BuildGatherProgressText(task));

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

        if (!IsHuntTargetUsable(task.targetMonster))
        {
            task.targetMonster = FindHuntTarget(task);
        }

        if (task.targetMonster == null)
        {
            MoveNpcToWork(task, task.workPosition);
            NpcRoleUtility.SetAction(
                task.npc,
                "Vao Ma Thu Son Mach tim yeu thu " +
                BuildHuntProgressText(task));
            return;
        }

        task.workPosition = task.targetMonster.transform.position;
        MoveNpcToWork(task, task.workPosition);
        NpcRoleUtility.SetAction(
            task.npc,
            "Truy tim yeu thu o Ma Thu Son Mach " +
            BuildHuntProgressText(task));

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
        if (HasHuntObjectiveComplete(task))
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        if (!IsHuntTargetUsable(task.targetMonster))
        {
            task.defeatedMonsterCount++;
            task.targetMonster = null;
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
            "Dang chien dau voi yeu thu " +
            BuildHuntProgressText(task));

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
            if (!IsHuntTargetUsable(monster))
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
                "Rut lui khoi khu co yeu thu");
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

        MoveNpcToWork(task, GetRetreatPosition(task.npc.transform.position, threat.transform.position));
        NpcRoleUtility.SetAction(
            task.npc,
            "Canh giac yeu thu gan linh thao");
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
            if (!IsHuntTargetUsable(monster))
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
        return GetNpcCombatPower(task.npc) <=
            GetMonsterCombatPower(threat) * Mathf.Max(0.1f, gatherThreatFleePowerRatio);
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
                "Chuyen sang chien dau voi yeu thu can duong");
            return;
        }

        NpcRoleUtility.StopForConversation(task.npc);
        NpcRoleUtility.SetAction(
            task.npc,
            "Dang diet yeu thu chiem khu hai");

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
        task.avoidUntilTime =
            Time.time + Mathf.Max(1f, gatherThreatAvoidDuration);

        MoveNpcToWork(task, task.avoidPosition);
        NpcRoleUtility.SetAction(
            task.npc,
            "Yeu thu qua manh, doi khu hai khac");
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
        return task != null &&
            task.defeatedMonsterCount >= GetRequiredMonsterKills(task.offer);
    }

    int GetRequiredMonsterKills(NpcTaskOffer offer)
    {
        return offer != null
            ? Mathf.Max(1, offer.requiredMonsterKills)
            : 1;
    }

    string BuildHuntProgressText(RunningNpcTask task)
    {
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
            duration = Mathf.Max(duration, task.targetPickup.harvestDuration);
        }

        return Mathf.Max(1f, duration);
    }
    bool TryCollectGatherItem(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.offer == null ||
            !IsGatherPickupUsable(task.targetPickup, GetTaskRequiredItem(task)))
        {
            return false;
        }

        StatItemData item = task.targetPickup.item;
        if (!task.targetPickup.TryTake(1))
        {
            return false;
        }

        ItemInventory inventory = GetOrCreateInventory(task.npc);
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
            NpcMapZone.MaThuSonMach);
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
        StatItemData requiredItem)
    {
        return pickup != null &&
            pickup.gameObject.activeInHierarchy &&
            pickup.item != null &&
            pickup.amount > 0 &&
            pickup.allowNpcPickup &&
            (requiredItem == null || pickup.item == requiredItem);
    }

    bool IsGatherTask(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.GatherResource;
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

        if (IsGatherTask(task))
        {
            return HasGatherObjectiveComplete(task);
        }

        if (IsHuntTask(task))
        {
            return HasHuntObjectiveComplete(task);
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
        if (item != null &&
            !string.IsNullOrEmpty(item.itemName))
        {
            return item.itemName;
        }

        return "linh thao";
    }

    int GetTaskGatherProgress(RunningNpcTask task)
    {
        if (task == null)
        {
            return 0;
        }

        int inventoryProgress = 0;
        if (task.offer != null &&
            GetTaskRequiredItem(task) != null &&
            task.npc != null)
        {
            inventoryProgress = Mathf.Max(
                0,
                GetNpcItemAmount(task.npc, GetTaskRequiredItem(task)) -
                    task.startingRequiredItemAmount);
        }

        return Mathf.Max(
            Mathf.Max(0, task.collectedAmount),
            inventoryProgress);
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
            float minMarkup = Mathf.Max(0f, offer.requiredItemRewardMarkupMin);
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
        if (offer != null &&
            offer.requiredItem != null &&
            !string.IsNullOrEmpty(offer.requiredItem.itemName))
        {
            return offer.requiredItem.itemName;
        }

        return "linh thao";
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

        string text =
            GetRankText(task.offer.rank) + ": " + task.offer.taskName;

        if (IsGatherTask(task))
        {
            text += " - " + GetTaskRequiredItemName(task) + " x" +
                GetRequiredAmount(task);
        }
        else if (IsHuntTask(task))
        {
            text += " - yeu thu x" +
                GetRequiredMonsterKills(task.offer);
        }

        int rewardSpiritStone =
            task.rewardSpiritStone > 0
            ? task.rewardSpiritStone
            : Mathf.Max(0, task.offer.rewardSpiritStone);

        if (rewardSpiritStone > 0)
        {
            text += " - thuong " + rewardSpiritStone + " LT";
        }

        return text;
    }

    bool ConsumeTaskItems(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null ||
            GetTaskRequiredItem(task) == null ||
            !task.offer.consumeRequiredItemsOnTurnIn)
        {
            return true;
        }

        ItemInventory inventory = task.npc != null
            ? task.npc.GetComponent<ItemInventory>()
            : null;

        if (inventory == null ||
            inventory.GetAmount(GetTaskRequiredItem(task)) < GetRequiredAmount(task))
        {
            return false;
        }

        StatItemData item = GetTaskRequiredItem(task);
        int amount = GetRequiredAmount(task);

        if (!inventory.RemoveItem(item, amount))
        {
            return false;
        }

        StoreTurnedInTaskGoods(item, amount);
        return true;
    }

    void StoreTurnedInTaskGoods(StatItemData item, int amount)
    {
        if (!storeTurnedInTaskGoods ||
            item == null ||
            amount <= 0)
        {
            return;
        }

        EnsureProviderInventory();

        if (inventory != null)
        {
            inventory.AddItem(item, amount);
        }

        PendingTaskGoods pending =
            pendingTaskGoods.Find(entry => entry != null && entry.item == item);

        if (pending == null)
        {
            pending = new PendingTaskGoods { item = item };
            pendingTaskGoods.Add(pending);
        }

        pending.amount += amount;
    }

    void UpdateTaskGoodsDailyTransfer()
    {
        if (!transferTaskGoodsToCounterAtDayEnd)
        {
            return;
        }

        int currentDay = GetCurrentWorldDay();
        if (currentDay < 0)
        {
            return;
        }

        if (lastTaskGoodsTransferDay < 0)
        {
            lastTaskGoodsTransferDay = currentDay;
            return;
        }

        if (currentDay == lastTaskGoodsTransferDay)
        {
            return;
        }

        TransferTaskGoodsToReceiver();
        lastTaskGoodsTransferDay = currentDay;
    }

    int GetCurrentWorldDay()
    {
        return WorldTimeSystem.Instance != null
            ? WorldTimeSystem.Instance.CurrentDay
            : -1;
    }

    void TransferTaskGoodsToReceiver()
    {
        if (pendingTaskGoods.Count == 0)
        {
            return;
        }

        NpcCounterBroker receiver = taskGoodsReceiver;
        if (receiver == null && useActiveCounterBrokerIfReceiverMissing)
        {
            receiver = NpcCounterBroker.Active;
        }

        if (receiver == null)
        {
            return;
        }

        ItemInventory receiverInventory = receiver.inventory;
        if (receiverInventory == null)
        {
            receiverInventory = receiver.GetComponent<ItemInventory>();
        }

        if (receiverInventory == null)
        {
            receiverInventory = receiver.gameObject.AddComponent<ItemInventory>();
            receiverInventory.shareRuntimeItems = false;
        }

        EnsureProviderInventory();

        for (int i = pendingTaskGoods.Count - 1; i >= 0; i--)
        {
            PendingTaskGoods pending = pendingTaskGoods[i];
            if (pending == null ||
                pending.item == null ||
                pending.amount <= 0)
            {
                pendingTaskGoods.RemoveAt(i);
                continue;
            }

            int transferAmount = pending.amount;
            if (inventory != null)
            {
                transferAmount = Mathf.Min(
                    transferAmount,
                    inventory.GetAmount(pending.item));
            }

            if (transferAmount <= 0)
            {
                pendingTaskGoods.RemoveAt(i);
                continue;
            }

            if (inventory != null)
            {
                inventory.RemoveItem(pending.item, transferAmount);
            }

            receiverInventory.AddItem(pending.item, transferAmount);
            pending.amount -= transferAmount;

            if (pending.amount <= 0)
            {
                pendingTaskGoods.RemoveAt(i);
            }
        }
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
        ResumeBaseAi(meal);

        if (completed &&
            meal != null &&
            meal.npc != null)
        {
            NpcRoleUtility.SetAction(meal.npc, "Ä‚n uá»‘ng xong táº¡i tá»­u quÃ¡n");
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
            NpcRoleUtility.SetAction(
                task.npc,
                "Khong du vat pham de tra nhiem vu " + GetTaskDisplayText(task));
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        runningTasks.RemoveAt(index);
        ResumeBaseAi(task);

        if (!completed ||
            task == null ||
            task.npc == null ||
            task.offer == null)
        {
            return;
        }

        RewardNpc(task);
    }

    GameObject GetNpcFromHit(Collider2D hit)
    {
        if (hit == null ||
            hit.transform == transform ||
            hit.transform.IsChildOf(transform))
        {
            return null;
        }

        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();

        return smartNpc != null
            ? smartNpc.gameObject
            : null;
    }

    NpcTaskOffer PickOfferFor(GameObject npc)
    {
        NpcTaskOffer best = null;
        float bestScore = 0f;

        foreach (NpcTaskOffer offer in offers)
        {
            float score = GetOfferSuitabilityScore(npc, offer);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = offer;
        }

        return best;
    }

    float GetOfferSuitabilityScore(GameObject npc, NpcTaskOffer offer)
    {
        if (npc == null ||
            offer == null ||
            !NpcRoleUtility.MeetsRealm(npc, offer.minRealm, offer.minRealmStage))
        {
            return 0f;
        }

        float score = 10f;
        int npcPower = NpcRoleUtility.GetRealmPower(npc);
        int requiredPower = CultivationProgression.GetRealmPower(
            offer.minRealm,
            Mathf.Clamp(offer.minRealmStage, 1, CultivationProgression.MaxStage));

        score += Mathf.Clamp(npcPower - requiredPower, 0, 80) * 0.25f;

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            switch (villager.job)
            {
                case VillagerJob.Hunter:
                case VillagerJob.Guard:
                    if (offer.taskType == NpcTaskType.HuntMonster ||
                        offer.taskType == NpcTaskType.Patrol)
                    {
                        score += 35f;
                    }
                    break;

                case VillagerJob.Farmer:
                case VillagerJob.Fisher:
                case VillagerJob.Worker:
                    if (offer.taskType == NpcTaskType.GatherResource ||
                        offer.taskType == NpcTaskType.Deliver)
                    {
                        score += 30f;
                    }
                    break;

                case VillagerJob.Trader:
                    if (offer.taskType == NpcTaskType.Deliver ||
                        offer.taskType == NpcTaskType.GatherResource)
                    {
                        score += 20f;
                    }
                    break;
            }

            if (villager.bravery < 45 &&
                offer.taskType == NpcTaskType.HuntMonster)
            {
                score -= 45f;
            }

            if (villager.fatigue >= 70f)
            {
                score -= 20f;
            }
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            if (offer.taskType == NpcTaskType.HuntMonster &&
                smartNpc.canFight)
            {
                score += 30f;
            }

            if (offer.taskType == NpcTaskType.Cultivate &&
                smartNpc.canCultivate)
            {
                score += 25f;
            }
        }

        if ((offer.taskType == NpcTaskType.HuntMonster ||
            offer.taskType == NpcTaskType.Patrol) &&
            NpcMapArea.FindNearestAreaInZone(NpcMapZone.MaThuSonMach, transform.position) != null)
        {
            score += 10f;
        }

        return Mathf.Max(0f, score);
    }

    NpcTaskOffer[] ShuffleOffers()
    {
        NpcTaskOffer[] shuffled =
            new NpcTaskOffer[offers.Length];

        offers.CopyTo(shuffled, 0);

        for (int i = 0; i < shuffled.Length; i++)
        {
            int swapIndex = Random.Range(i, shuffled.Length);
            NpcTaskOffer temp = shuffled[i];
            shuffled[i] = shuffled[swapIndex];
            shuffled[swapIndex] = temp;
        }

        return shuffled;
    }

    void RewardNpc(RunningNpcTask task)
    {
        GameObject npc = task.npc;
        NpcTaskOffer offer = task.offer;
        int rewardSpiritStone =
            task.rewardSpiritStone > 0
            ? task.rewardSpiritStone
            : Mathf.Max(0, offer.rewardSpiritStone);

        PayRewardMoney(npc, rewardSpiritStone);
        NpcRoleUtility.AddCultivationExp(npc, offer.rewardCultivationExp);

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
            "HoÃ n thÃ nh nhiá»‡m vá»¥ " + GetRankText(offer.rank) + ": " + offer.taskName);

        if (WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.AddLog(
                NpcRoleUtility.GetDisplayName(npc) +
                " hoÃ n thÃ nh nhiá»‡m vá»¥ " +
                offer.taskName +
                ", nháº­n " +
                rewardSpiritStone +
                " LT.",
                0);
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

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        return smartNpc != null &&
            smartNpc.hunger >= mealHungerThreshold;
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
        return HasRunningTask(npc) ||
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

    Vector3 GetMealPosition()
    {
        return mealPoint != null
            ? mealPoint.position
            : transform.position;
    }

    Vector3 GetCounterPosition(GameObject npc = null)
    {
        if (counterPoint != null)
        {
            return counterPoint.position;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null)
        {
            return transform.position;
        }

        return broker.GetCustomerPositionFor(npc);
    }

    Vector3 GetBoardPosition()
    {
        return taskBoardPoint != null
            ? taskBoardPoint.position
            : transform.position;
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

    Vector3 GetWorkPosition(NpcTaskOffer offer)
    {
        if (offer != null)
        {
            switch (offer.taskType)
            {
                case NpcTaskType.HuntMonster:
                    if (huntPoint != null)
                    {
                        return huntPoint.position;
                    }

                    return GetForestWorkPosition(
                        huntPoint,
                        GetDepthMinForRank(offer.rank, huntDepthMin),
                        GetDepthMaxForRank(offer.rank, huntDepthMax));

                case NpcTaskType.GatherResource:
                    if (gatherPoint != null)
                    {
                        return gatherPoint.position;
                    }

                    return GetForestWorkPosition(
                        gatherPoint,
                        GetDepthMinForRank(offer.rank, gatherDepthMin),
                        GetDepthMaxForRank(offer.rank, gatherDepthMax));

                case NpcTaskType.Patrol:
                    return patrolPoint != null
                        ? patrolPoint.position
                        : GetForestWorkPosition(defaultWorkPoint, 0.25f, 0.75f);

                case NpcTaskType.Deliver:
                    return deliverPoint != null
                        ? deliverPoint.position
                        : GetFallbackWorkPosition();
            }
        }

        return GetFallbackWorkPosition();
    }

    Vector3 GetFallbackWorkPosition()
    {
        return defaultWorkPoint != null
            ? defaultWorkPoint.position
            : transform.position;
    }

    Vector3 GetForestWorkPosition(
        Transform fallbackPoint,
        float minDepth,
        float maxDepth)
    {
        NpcMapArea area = forestSearchArea != null
            ? forestSearchArea
            : NpcMapArea.FindNearestAreaInZone(
                NpcMapZone.MaThuSonMach,
                fallbackPoint != null ? fallbackPoint.position : transform.position);

        if (area == null || area.areaBounds == null)
        {
            return fallbackPoint != null
                ? fallbackPoint.position
                : GetFallbackWorkPosition();
        }

        Vector3 entry = forestEntryPoint != null
            ? forestEntryPoint.position
            : area.areaBounds.bounds.min;

        Vector3 deep = forestDeepPoint != null
            ? forestDeepPoint.position
            : area.areaBounds.bounds.max;

        Vector2 depthDirection = (Vector2)(deep - entry);
        if (depthDirection.sqrMagnitude <= 0.0001f)
        {
            depthDirection = Vector2.right;
        }

        depthDirection.Normalize();
        minDepth = Mathf.Clamp01(minDepth);
        maxDepth = Mathf.Clamp(maxDepth, minDepth, 1f);

        Bounds bounds = area.areaBounds.bounds;
        Vector3 best = fallbackPoint != null
            ? fallbackPoint.position
            : bounds.center;
        float bestPenalty = float.PositiveInfinity;

        for (int i = 0; i < Mathf.Max(1, forestPointPickAttempts); i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                transform.position.z);

            Vector2 closest = area.areaBounds.ClosestPoint(candidate);
            if (Vector2.Distance(closest, candidate) > 0.02f)
            {
                continue;
            }

            float depth = GetDepth01(candidate, entry, deep, depthDirection);
            if (depth >= minDepth && depth <= maxDepth)
            {
                return candidate;
            }

            float penalty = depth < minDepth
                ? minDepth - depth
                : depth - maxDepth;

            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                best = candidate;
            }
        }

        return best;
    }

    float GetDepth01(
        Vector3 position,
        Vector3 entry,
        Vector3 deep,
        Vector2 depthDirection)
    {
        float length = Vector2.Distance(entry, deep);
        if (length <= 0.0001f)
        {
            return 0.5f;
        }

        return Mathf.Clamp01(
            Vector2.Dot((Vector2)(position - entry), depthDirection) / length);
    }

    float GetDepthMinForRank(NpcTaskRank rank, float baseMin)
    {
        return Mathf.Clamp01(baseMin + GetRankDepthBonus(rank));
    }

    float GetDepthMaxForRank(NpcTaskRank rank, float baseMax)
    {
        return Mathf.Clamp01(baseMax + GetRankDepthBonus(rank));
    }

    float GetRankDepthBonus(NpcTaskRank rank)
    {
        switch (rank)
        {
            case NpcTaskRank.Trung:
                return 0.12f;
            case NpcTaskRank.Thuong:
                return 0.25f;
            default:
                return 0f;
        }
    }

    void TryTradeAtCounter(GameObject npc)
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null ||
            !broker.receiveAllNpcRequests ||
            npc == null)
        {
            return;
        }

        NpcRoleUtility.StopForConversation(npc);
        NpcRoleUtility.StopForConversation(broker.gameObject);
        NpcRoleUtility.SetAction(broker.gameObject, "Noi chuyen mua ban voi khach");

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            ItemInventory inventory = villager.inventory != null
                ? villager.inventory
                : npc.GetComponent<ItemInventory>();

            broker.TryBuyProduceFrom(villager, inventory);
        }

        NpcTradeAgent tradeAgent = npc.GetComponent<NpcTradeAgent>();
        if (tradeAgent != null)
        {
            broker.TryTradeWithNpc(tradeAgent);
        }
    }

    void MoveNpcToWork(RunningNpcTask task, Vector3 target)
    {
        if (task == null)
        {
            return;
        }

        MoveNpc(
            task.npc,
            target,
            GetWorkZone(task.offer));
    }

    NpcMapZone? GetWorkZone(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

        switch (offer.taskType)
        {
            case NpcTaskType.GatherResource:
            case NpcTaskType.HuntMonster:
            case NpcTaskType.Patrol:
                return NpcMapZone.MaThuSonMach;
            default:
                return null;
        }
    }

    void MoveNpc(GameObject npc, Vector3 target)
    {
        MoveNpc(npc, target, null);
    }

    void MoveNpc(
        GameObject npc,
        Vector3 target,
        NpcMapZone? forcedTargetZone)
    {
        if (npc == null)
        {
            return;
        }
        bool usingTeleportRoute;
        string routeAction;
        Vector3 moveTarget = NpcMapNavigator.GetNextMoveTarget(
            npc,
            target,
            forcedTargetZone,
            out usingTeleportRoute,
            out routeAction);

        if (usingTeleportRoute &&
            !string.IsNullOrEmpty(routeAction))
        {
            NpcRoleUtility.SetAction(npc, routeAction);
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null && mover.enabled)
        {
            mover.SetMoveTarget(
                moveTarget,
                !string.IsNullOrEmpty(routeAction) ? routeAction : "Di theo nhiem vu");
            return;
        }

        float speed = NpcRoleUtility.GetMoveSpeed(npc, fallbackMoveSpeed);
        npc.transform.position = Vector3.MoveTowards(
            npc.transform.position,
            moveTarget,
            speed * Time.deltaTime);
    }

    string GetRankText(NpcTaskRank rank)
    {
        switch (rank)
        {
            case NpcTaskRank.Trung:
                return "Trung";
            case NpcTaskRank.Thuong:
                return "ThÆ°á»ng";
            default:
                return "Háº¡";
        }
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

        if (!pauseBaseAiWhileWorking ||
            npc == null)
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, assignRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, mealServiceRadius);

        if (mealPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(mealPoint.position, 0.25f);
        }

        if (taskBoardPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(taskBoardPoint.position, 0.25f);
        }
    }
}






