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
    GoingToBoard,
    ChoosingTask,
    ReturningToProvider,
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
    public float chooseTaskDuration = 3f;
    public Transform defaultWorkPoint;
    public Transform huntPoint;
    public Transform gatherPoint;
    public Transform patrolPoint;
    public Transform deliverPoint;

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
            stage = TavernTaskStage.GoingToBoard,
            boardPosition = GetBoardPosition(),
            providerPosition = GetProviderPosition(),
            workPosition = GetWorkPosition(offer),
            remainingTime = Mathf.Max(0.5f, chooseTaskDuration)
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
                        task.remainingTime = Mathf.Max(0.5f, chooseTaskDuration);
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
        Transform point = defaultWorkPoint;

        if (offer != null)
        {
            switch (offer.taskType)
            {
                case NpcTaskType.HuntMonster:
                    point = huntPoint != null ? huntPoint : point;
                    break;
                case NpcTaskType.GatherResource:
                    point = gatherPoint != null ? gatherPoint : point;
                    break;
                case NpcTaskType.Patrol:
                    point = patrolPoint != null ? patrolPoint : point;
                    break;
                case NpcTaskType.Deliver:
                    point = deliverPoint != null ? deliverPoint : point;
                    break;
            }
        }

        return point != null
            ? point.position
            : transform.position;
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
