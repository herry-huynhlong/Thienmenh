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

[System.Serializable]
public class NpcTaskOffer
{
    public string taskName = "Thu thap tai nguyen";
    public NpcTaskType taskType = NpcTaskType.GatherResource;
    public CultivationRealm minRealm = CultivationRealm.Foundation;
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
    public Vector3 workPosition;
    public float remainingTime;
    public bool reachedWorkPosition;
    public Behaviour pausedBaseAi;
    public bool pausedBaseAiWasEnabled;
}

public class NpcTaskProvider : MonoBehaviour
{
    [Header("Task Provider")]
    public bool provideTasks = true;
    public float assignRadius = 2.5f;
    public float assignInterval = 5f;
    public float arriveDistance = 0.35f;
    public float fallbackMoveSpeed = 1.6f;
    public bool pauseBaseAiWhileWorking = true;
    public LayerMask npcLayers = ~0;
    public Transform defaultWorkPoint;
    public Transform huntPoint;
    public Transform gatherPoint;
    public Transform patrolPoint;
    public Transform deliverPoint;

    [Header("Offers")]
    public NpcTaskOffer[] offers =
    {
        new NpcTaskOffer()
    };

    readonly List<RunningNpcTask> runningTasks =
        new List<RunningNpcTask>();

    float assignTimer;

    void Awake()
    {
        NpcSpecialProfession profession =
            GetComponent<NpcSpecialProfession>();
        if (profession == null)
        {
            profession = gameObject.AddComponent<NpcSpecialProfession>();
        }

        profession.professionName = "Thuong Nhan Nhiem Vu";
    }

    void Update()
    {
        UpdateRunningTasks();

        if (!provideTasks)
        {
            return;
        }

        assignTimer += Time.deltaTime;
        if (assignTimer < assignInterval)
        {
            return;
        }

        assignTimer = 0f;
        TryAssignTask();
    }

    void TryAssignTask()
    {
        if (offers == null ||
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
                HasRunningTask(npc))
            {
                continue;
            }

            NpcTaskOffer offer = PickOfferFor(npc);
            if (offer == null)
            {
                continue;
            }

            AssignTask(npc, offer);
            return;
        }
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
        NpcTaskOffer[] shuffled =
            ShuffleOffers();

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
            int swapIndex =
                Random.Range(i, shuffled.Length);

            NpcTaskOffer temp = shuffled[i];
            shuffled[i] = shuffled[swapIndex];
            shuffled[swapIndex] = temp;
        }

        return shuffled;
    }

    void AssignTask(GameObject npc, NpcTaskOffer offer)
    {
        RunningNpcTask task =
            new RunningNpcTask
            {
                npc = npc,
                offer = offer,
                workPosition = GetWorkPosition(offer),
                remainingTime = Mathf.Max(1f, offer.workDuration)
            };

        PauseBaseAi(task);
        runningTasks.Add(task);

        NpcRoleUtility.SetAction(
            npc,
            "Nhan nhiem vu: " + offer.taskName);
        NpcRoleUtility.SetAction(
            gameObject,
            "Giao nhiem vu: " + offer.taskName);
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

            if (!task.reachedWorkPosition)
            {
                NpcRoleUtility.MoveTowards(
                    task.npc,
                    task.workPosition,
                    fallbackMoveSpeed);
                NpcRoleUtility.SetAction(
                    task.npc,
                    "Di lam nhiem vu: " + task.offer.taskName);

                if (Vector2.Distance(
                        task.npc.transform.position,
                        task.workPosition) <= arriveDistance)
                {
                    task.reachedWorkPosition = true;
                }

                continue;
            }

            task.remainingTime -= Time.deltaTime;
            NpcRoleUtility.SetAction(
                task.npc,
                "Dang lam nhiem vu: " + task.offer.taskName);

            if (task.remainingTime <= 0f)
            {
                FinishTask(i, true);
            }
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

    void RewardNpc(GameObject npc, NpcTaskOffer offer)
    {
        NpcEconomy.AddNpcMoney(
            npc,
            offer.rewardSpiritStone);
        NpcRoleUtility.AddCultivationExp(
            npc,
            offer.rewardCultivationExp);

        if (offer.rewardItem != null &&
            offer.rewardItemAmount > 0)
        {
            ItemInventory inventory =
                npc.GetComponent<ItemInventory>();

            if (inventory == null)
            {
                inventory = npc.AddComponent<ItemInventory>();
            }

            inventory.AddItem(
                offer.rewardItem,
                offer.rewardItemAmount);
        }

        NpcRoleUtility.SetAction(
            npc,
            "Hoan thanh nhiem vu: " + offer.taskName);

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

    void PauseBaseAi(RunningNpcTask task)
    {
        if (!pauseBaseAiWhileWorking ||
            task == null ||
            task.npc == null)
        {
            return;
        }

        task.pausedBaseAi = task.npc.GetComponent<VillagerAI>();
        if (task.pausedBaseAi == null)
        {
            task.pausedBaseAi = task.npc.GetComponent<SmartNpcAI>();
        }

        if (task.pausedBaseAi == null)
        {
            return;
        }

        task.pausedBaseAiWasEnabled = task.pausedBaseAi.enabled;
        task.pausedBaseAi.enabled = false;
    }

    void ResumeBaseAi(RunningNpcTask task)
    {
        if (task == null ||
            task.pausedBaseAi == null)
        {
            return;
        }

        task.pausedBaseAi.enabled = task.pausedBaseAiWasEnabled;
        task.pausedBaseAi = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, assignRadius);
    }
}
