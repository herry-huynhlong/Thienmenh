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
    Working
}

public enum TavernMealStage
{
    GoingToMealPoint,
    Eating
}

[System.Serializable]
public class NpcTaskOffer
{
    public string taskName = "Thu thap tai nguyen";
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

public class NpcTaskProvider : MonoBehaviour
{
    [Header("Tavern Service")]
    public bool serveMeals = true;
    public int mealCost = 1;
    [Range(0f, 100f)]
    public float mealHungerThreshold = 55f;
    public float mealServiceRadius = 2f;
    public Transform mealPoint;
    public float mealDuration = 5f;

    [Header("Counter Flow")]
    public bool requireCounterCheckBeforeTask = true;
    public Transform counterPoint;
    public float counterCheckDuration = 4f;

    [Header("Task Provider")]
    public bool provideTasks = true;
    public float assignRadius = 2.5f;
    public float assignInterval = 5f;
    public float arriveDistance = 0.35f;
    public float fallbackMoveSpeed = 1.6f;
    public bool pauseBaseAiWhileWorking = true;
    public LayerMask npcLayers = ~0;
    public Transform taskBoardPoint;
    public Transform providerPoint;
    public float chooseTaskDuration = 8f;
    public float providerReceiveDuration = 5f;
    public Transform defaultWorkPoint;
    public Transform huntPoint;
    public Transform gatherPoint;
    public Transform patrolPoint;
    public Transform deliverPoint;

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
            taskName = "Thu thap linh thao ha pham",
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
            taskName = "Tuan tra ngoai lang",
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
            taskName = "San yeu thu nguy hiem",
            taskType = NpcTaskType.HuntMonster,
            rank = NpcTaskRank.Thuong,
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

    float assignTimer;

    void Awake()
    {
        NpcSpecialProfession profession =
            GetComponent<NpcSpecialProfession>();

        if (profession == null)
        {
            profession = gameObject.AddComponent<NpcSpecialProfession>();
        }

        profession.professionName = "Quan Su Tuu Quan";
    }

    void Update()
    {
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

        NpcRoleUtility.SetAction(npc, "Hoi quan su de an uong");
        NpcRoleUtility.SetAction(gameObject, "Chi dan khach vao khu an");
    }

    void StartTaskRequest(GameObject npc, NpcTaskOffer offer)
    {
        RunningNpcTask task = new RunningNpcTask
        {
            npc = npc,
            offer = offer,
            stage = requireCounterCheckBeforeTask
                ? TavernTaskStage.GoingToCounter
                : TavernTaskStage.GoingToBoard,
            counterPosition = GetCounterPosition(),
            boardPosition = GetBoardPosition(),
            providerPosition = GetProviderPosition(),
            workPosition = GetWorkPosition(offer),
            remainingTime = Mathf.Max(8f, chooseTaskDuration)
        };

        PauseBaseAi(task);
        runningTasks.Add(task);

        NpcRoleUtility.SetAction(
            npc,
            "Hoi quan su tim nhiem vu");

        NpcRoleUtility.SetAction(
            gameObject,
            "Chi bang nhiem vu cho khach");
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
                NpcRoleUtility.SetAction(meal.npc, "Di toi khu an trong tuu quan");

                if (Vector2.Distance(
                        meal.npc.transform.position,
                        meal.mealPosition) <= arriveDistance)
                {
                    meal.stage = TavernMealStage.Eating;
                    NpcEconomy.AddNpcMoney(meal.npc, -mealCost);
                    NpcEconomy.AddNpcMoney(gameObject, mealCost);
                    FeedNpc(meal.npc);
                }

                continue;
            }

            meal.remainingTime -= Time.deltaTime;
            NpcRoleUtility.SetAction(meal.npc, "Dang an uong tai tuu quan");

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
                        "Den truong quay kiem tra mua ban");

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
                        "Dang kiem tra mua ban tai truong quay");

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.GoingToBoard;
                    }
                    break;

                case TavernTaskStage.GoingToBoard:
                    MoveNpc(task.npc, task.boardPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Xem bang nhiem vu " + GetRankText(task.offer.rank));

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
                        "Chon nhiem vu " + GetRankText(task.offer.rank));

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.ReturningToProvider;
                    }
                    break;

                case TavernTaskStage.ReturningToProvider:
                    MoveNpc(task.npc, task.providerPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Quay lai quan su nhan nhiem vu");

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.providerPosition) <= arriveDistance)
                    {
                        NpcRoleUtility.SetAction(
                            gameObject,
                            "Giao nhiem vu " + GetRankText(task.offer.rank) + ": " + task.offer.taskName);
                        task.stage = TavernTaskStage.ReceivingTask;
                        task.remainingTime = Mathf.Max(3f, providerReceiveDuration);
                    }
                    break;

                case TavernTaskStage.ReceivingTask:
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Dang nhan nhiem vu " + GetRankText(task.offer.rank) + ": " + task.offer.taskName);

                    if (task.remainingTime <= 0f)
                    {
                        task.stage = TavernTaskStage.GoingToWork;
                    }
                    break;

                case TavernTaskStage.GoingToWork:
                    MoveNpc(task.npc, task.workPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Di lam nhiem vu " + GetRankText(task.offer.rank) + ": " + task.offer.taskName);

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.workPosition) <= arriveDistance)
                    {
                        task.stage = TavernTaskStage.Working;
                        task.remainingTime = Mathf.Max(1f, task.offer.workDuration);
                    }
                    break;

                case TavernTaskStage.Working:
                    task.remainingTime -= Time.deltaTime;
                    NpcRoleUtility.SetAction(
                        task.npc,
                        "Dang lam nhiem vu " + GetRankText(task.offer.rank) + ": " + task.offer.taskName);

                    if (task.remainingTime <= 0f)
                    {
                        FinishTask(i, true);
                    }
                    break;
            }
        }
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
            NpcRoleUtility.SetAction(meal.npc, "An uong xong tai tuu quan");
        }
    }

    void FinishTask(int index, bool completed)
    {
        RunningNpcTask task = runningTasks[index];
        runningTasks.RemoveAt(index);
        ResumeBaseAi(task);

        if (!completed ||
            task == null ||
            task.npc == null ||
            task.offer == null)
        {
            return;
        }

        RewardNpc(task.npc, task.offer);
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
        NpcTaskOffer[] shuffled = ShuffleOffers();

        foreach (NpcTaskOffer offer in shuffled)
        {
            if (offer == null ||
                !NpcRoleUtility.MeetsRealm(
                    npc,
                    offer.minRealm,
                    offer.minRealmStage))
            {
                continue;
            }

            return offer;
        }

        return null;
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

    void RewardNpc(GameObject npc, NpcTaskOffer offer)
    {
        NpcEconomy.AddNpcMoney(npc, offer.rewardSpiritStone);
        NpcRoleUtility.AddCultivationExp(npc, offer.rewardCultivationExp);

        if (offer.rewardItem != null &&
            offer.rewardItemAmount > 0)
        {
            ItemInventory inventory = npc.GetComponent<ItemInventory>();

            if (inventory == null)
            {
                inventory = npc.AddComponent<ItemInventory>();
                inventory.shareRuntimeItems = false;
            }

            inventory.AddItem(offer.rewardItem, offer.rewardItemAmount);
        }

        NpcRoleUtility.SetAction(
            npc,
            "Hoan thanh nhiem vu " + GetRankText(offer.rank) + ": " + offer.taskName);

        if (WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.AddLog(
                NpcRoleUtility.GetDisplayName(npc) +
                " hoan thanh nhiem vu " +
                offer.taskName +
                ", nhan " +
                offer.rewardSpiritStone +
                " LT.",
                0);
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

    Vector3 GetCounterPosition()
    {
        if (counterPoint != null)
        {
            return counterPoint.position;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        return broker != null
            ? broker.transform.position
            : transform.position;
    }

    Vector3 GetBoardPosition()
    {
        return taskBoardPoint != null
            ? taskBoardPoint.position
            : transform.position;
    }

    Vector3 GetProviderPosition()
    {
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
                    return GetForestWorkPosition(
                        huntPoint,
                        GetDepthMinForRank(offer.rank, huntDepthMin),
                        GetDepthMaxForRank(offer.rank, huntDepthMax));

                case NpcTaskType.GatherResource:
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

    void MoveNpc(GameObject npc, Vector3 target)
    {
        NpcRoleUtility.MoveTowards(npc, target, fallbackMoveSpeed);
    }

    string GetRankText(NpcTaskRank rank)
    {
        switch (rank)
        {
            case NpcTaskRank.Trung:
                return "Trung";
            case NpcTaskRank.Thuong:
                return "Thuong";
            default:
                return "Ha";
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
