using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public enum NpcTaskType
{
    GatherResource,
    HuntMonster,
    Cultivate,
    Patrol,
    Deliver,
    HarvestAndDeliver,
    Escort,
    FrontierWatch
}

public enum NpcTaskRank
{
    Ha,
    Trung,
    Thuong
}

public enum NpcTaskAudience
{
    AnyNpc,
    VillagerOnly,
    SmartNpcOnly
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
    TurningIn,
    WaitingForTargetRespawn
}

public enum TavernMealStage
{
    GoingToMealPoint,
    Eating
}

[System.Serializable]
public class NpcTaskOffer
{
    public string taskName = "";
    public NpcTaskType taskType = NpcTaskType.GatherResource;
    public NpcTaskRank rank = NpcTaskRank.Ha;
    public NpcTaskAudience audience = NpcTaskAudience.AnyNpc;
    public CultivationRealm minRealm = CultivationRealm.QiRefining;
    [Range(1, 9)]
    public int minRealmStage = 1;
    public int rewardSpiritStone = 20;
    public int rewardCultivationExp;
    public StatItemData rewardItem;
    public string rewardItemId;
    public int rewardItemAmount;
    public float workDuration = 12f;
    [Min(0f)] public float workDurationWorldHours;
    public string customTaskId = "";
    public string customTargetId = "";

    [Header("Objective")]
    public StatItemData requiredItem;
    public string requiredItemId;
    [Min(1)] public int requiredAmount = 1;
    public bool randomizeRequiredItemAmount = true;
    [Min(1)] public int requiredItemAmountMin = 5;
    [Min(1)] public int requiredItemAmountMax = 10;
    public bool autoPriceRequiredItemReward = true;
    [Min(0f)] public float requiredItemRewardMarkupMin = 0.33f;
    [Min(0f)] public float requiredItemRewardMarkupMax = 0.5f;
    public bool useRankRewardMultiplier = true;
    public bool consumeRequiredItemsOnTurnIn = true;
    [Min(0)] public int requiredBeastLevel;
    public HuntTargetType requiredHuntTargetType = HuntTargetType.Beast;
    [Min(1)] public int requiredMonsterKills = 1;
    public bool useMonsterRealmStageRequirement;
    public CultivationRealm requiredMonsterRealm = CultivationRealm.QiRefining;
    [Range(1, CultivationProgression.MaxStage)]
    public int requiredMonsterMaxStage = 1;
    public bool matchMonsterRealmExactly = true;

    public void ResolveItemReferences()
    {
        requiredItemId = NormalizeItemKey(requiredItem, requiredItemId);
        rewardItemId = NormalizeItemKey(rewardItem, rewardItemId);

        if (requiredItem == null &&
            !string.IsNullOrWhiteSpace(requiredItemId))
        {
            requiredItem = GameSaveSystem.FindItem(requiredItemId);
        }

        if (rewardItem == null &&
            !string.IsNullOrWhiteSpace(rewardItemId))
        {
            rewardItem = GameSaveSystem.FindItem(rewardItemId);
        }
    }

    static string NormalizeItemKey(StatItemData item, string itemKey)
    {
        if (item != null)
        {
            return GameSaveSystem.GetItemKey(item);
        }

        return string.IsNullOrWhiteSpace(itemKey)
            ? string.Empty
            : itemKey.Trim();
    }

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
    public Vector3 patrolEndPosition;
    public float remainingTime;
    public WorldStatItemPickup targetPickup;
    public StatItemData requiredItem;
    public int requiredAmount;
    public int rewardSpiritStone;
    public int collectedAmount;
    public int defeatedMonsterCount;
    public int frontierPatrolIndex;
    public int frontierPatrolDirection = 1;
    public int startingRequiredItemAmount;
    public MonsterAI targetMonster;
    public WorldStatItemPickup targetLootPickup;
    public MonsterAI threatMonster;
    public bool patrolReachedEnd;
    public bool resumedBaseAiWhileWaiting;
    public Vector3 avoidPosition;
    public float avoidUntilTime;
    public GameObject escortCompanionNpc;
    public GameObject escortCompletionNpc;
    public bool escortDepartedFromCompanion;
    public bool escortConfirmed;
    public bool escortGreetingConversationStarted;
    public bool escortDeliveryConversationStarted;
    public int escortGreetingConversationStep;
    public int escortDeliveryConversationStep;
    public MonsterAI escortThreatMonster;
    public Vector3 escortAvoidPosition;
    public float escortAvoidUntilTime;
    public Behaviour escortPausedCompanionBaseAi;
    public bool escortPausedCompanionBaseAiWasEnabled;
    public Vector3 escortCompanionHomePosition;
    public Behaviour pausedBaseAi;
    public bool pausedBaseAiWasEnabled;
    public bool travelWatchdogArmed;
    public TavernTaskStage travelWatchdogStage;
    public Vector3 travelWatchdogTarget;
    public Vector3 travelWatchdogLastPosition;
    public float travelStageStartedAt;
    public float travelLastProgressAt;
    public float travelLastDistanceToTarget = float.PositiveInfinity;
    public float maxTravelDuration;
    public int travelRetryCount;
    public int huntRespawnRetryCount;
    public float huntMissionDeadlineWorldHour = float.PositiveInfinity;
    public float huntMissionDeadlineFallbackTime = float.PositiveInfinity;
}

class RunningTavernMeal
{
    public GameObject npc;
    public TavernMealStage stage;
    public Vector3 mealPosition;
    public float remainingTime;
    public Behaviour pausedBaseAi;
    public bool pausedBaseAiWasEnabled;
    public bool travelWatchdogArmed;
    public Vector3 travelWatchdogTarget;
    public Vector3 travelWatchdogLastPosition;
    public float travelStageStartedAt;
    public float travelLastProgressAt;
    public float travelLastDistanceToTarget = float.PositiveInfinity;
    public float maxTravelDuration;
    public int travelRetryCount;
}

class PendingTaskGoods
{
    public StatItemData item;
    public int amount;
}

class HuntOfferSeed
{
    public StatItemData lootItem;
    public HuntTargetType targetType;
    public CultivationRealm realm;
    public int maxStage;
}

[RequireComponent(typeof(NpcSpecialProfession))]
public partial class NpcTaskProvider : MonoBehaviour
{
    public static event System.Action<GameObject, NpcTaskOffer> TaskStarted;
    public static event System.Action<GameObject, NpcTaskOffer, bool> TaskFinished;

    const string LinhRiceItemId = "caf4f5e611fac2d4bb6815a50dc1060b";

    static readonly List<NpcTaskProvider> providers =
        new List<NpcTaskProvider>();
    static readonly Dictionary<GameObject, int> busyNpcCounts =
        new Dictionary<GameObject, int>();
    static readonly List<GameObject> staleBusyNpcs =
        new List<GameObject>();
    static readonly HashSet<NpcTaskOffer> lockedEscortOffers =
        new HashSet<NpcTaskOffer>();
    static readonly List<NpcTaskOffer> staleLockedEscortOffers =
        new List<NpcTaskOffer>();
    static readonly HashSet<NpcTaskOffer> claimedTaskOffers =
        new HashSet<NpcTaskOffer>();
    static readonly List<NpcTaskOffer> staleClaimedTaskOffers =
        new List<NpcTaskOffer>();
    readonly Dictionary<GameObject, NpcTaskOffer> lastCompletedOfferByNpc =
        new Dictionary<GameObject, NpcTaskOffer>();
    readonly List<GameObject> staleCompletedOfferNpcs =
        new List<GameObject>();

    public static NpcTaskProvider FindNearestProvider(Vector3 position)
    {
        return FindNearestProvider(null, position);
    }

    public static NpcTaskProvider FindNearestProvider(
        GameObject npc,
        Vector3 position)
    {
        return FindNearestProvider(npc, position, true);
    }

    public static NpcTaskProvider FindNearestProvider(
        GameObject npc,
        Vector3 position,
        bool autoAssigned)
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

            if (npc != null &&
                !provider.HasAnyOfferForNpc(npc, autoAssigned))
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

    public bool HasAnyOfferForNpc(GameObject npc)
    {
        return HasAnyOfferForNpc(npc, true);
    }

    public bool HasAnyOfferForNpc(
        GameObject npc,
        bool autoAssigned)
    {
        if (npc == null ||
            !provideTasks ||
            offers == null ||
            offers.Length == 0)
        {
            return false;
        }

        return PickOfferFor(npc, autoAssigned) != null;
    }

    public static bool IsNpcBusyWithAnyProvider(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        CleanupBusyNpcEntries();

        int count;
        return busyNpcCounts.TryGetValue(npc, out count) &&
            count > 0;
    }

    public static bool ReleaseNpcFromProviderTasksForCombat(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        bool releasedAny = false;
        NpcTaskProvider[] providers =
            FindObjectsByType<NpcTaskProvider>(FindObjectsInactive.Exclude);

        for (int i = 0; i < providers.Length; i++)
        {
            NpcTaskProvider provider = providers[i];
            if (provider == null)
            {
                continue;
            }

            if (provider.ReleaseNpcForCombatInternal(npc))
            {
                releasedAny = true;
            }
        }

        return releasedAny;
    }

    static void MarkNpcBusyWithProvider(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        CleanupBusyNpcEntries();

        int count;
        busyNpcCounts.TryGetValue(npc, out count);
        busyNpcCounts[npc] = count + 1;
    }

    static void UnmarkNpcBusyWithProvider(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        int count;
        if (!busyNpcCounts.TryGetValue(npc, out count))
        {
            return;
        }

        count--;
        if (count <= 0)
        {
            busyNpcCounts.Remove(npc);
            return;
        }

        busyNpcCounts[npc] = count;
    }

    static void CleanupBusyNpcEntries()
    {
        staleBusyNpcs.Clear();

        foreach (KeyValuePair<GameObject, int> pair in busyNpcCounts)
        {
            if (pair.Key == null || pair.Value <= 0)
            {
                staleBusyNpcs.Add(pair.Key);
            }
        }

        foreach (GameObject npc in staleBusyNpcs)
        {
            busyNpcCounts.Remove(npc);
        }
    }

    bool ReleaseNpcForCombatInternal(GameObject npc)
    {
        bool released = false;

        for (int i = runningMeals.Count - 1; i >= 0; i--)
        {
            RunningTavernMeal meal = runningMeals[i];
            if (meal == null ||
                meal.npc != npc)
            {
                continue;
            }

            FinishMeal(i, false);
            released = true;
        }

        for (int i = runningTasks.Count - 1; i >= 0; i--)
        {
            RunningNpcTask task = runningTasks[i];
            if (task == null ||
                task.npc != npc)
            {
                continue;
            }

            FinishTask(i, false);
            released = true;
        }

        return released;
    }

    static bool IsEscortOfferLocked(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return false;
        }

        CleanupEscortLocks();
        return lockedEscortOffers.Contains(offer);
    }

    static void LockEscortOffer(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return;
        }

        CleanupEscortLocks();
        lockedEscortOffers.Add(offer);
    }

    static void UnlockEscortOffer(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return;
        }

        lockedEscortOffers.Remove(offer);
    }

    static bool IsTaskOfferClaimed(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return false;
        }

        CleanupClaimedTaskOffers();
        return claimedTaskOffers.Contains(offer);
    }

    static bool ClaimTaskOffer(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return false;
        }

        CleanupClaimedTaskOffers();
        if (claimedTaskOffers.Contains(offer))
        {
            return false;
        }

        claimedTaskOffers.Add(offer);
        return true;
    }

    static void ReleaseTaskOffer(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return;
        }

        claimedTaskOffers.Remove(offer);
    }

    static void CleanupClaimedTaskOffers()
    {
        staleClaimedTaskOffers.Clear();
        foreach (NpcTaskOffer offer in claimedTaskOffers)
        {
            if (offer == null)
            {
                staleClaimedTaskOffers.Add(offer);
            }
        }

        foreach (NpcTaskOffer offer in staleClaimedTaskOffers)
        {
            claimedTaskOffers.Remove(offer);
        }
    }

    static void CleanupEscortLocks()
    {
        staleLockedEscortOffers.Clear();
        foreach (NpcTaskOffer offer in lockedEscortOffers)
        {
            if (offer == null)
            {
                staleLockedEscortOffers.Add(offer);
            }
        }

        foreach (NpcTaskOffer offer in staleLockedEscortOffers)
        {
            lockedEscortOffers.Remove(offer);
        }
    }

    void CleanupCompletedOfferHistory()
    {
        staleCompletedOfferNpcs.Clear();

        foreach (KeyValuePair<GameObject, NpcTaskOffer> pair in
            lastCompletedOfferByNpc)
        {
            if (pair.Key == null || pair.Value == null)
            {
                staleCompletedOfferNpcs.Add(pair.Key);
            }
        }

        foreach (GameObject npc in staleCompletedOfferNpcs)
        {
            lastCompletedOfferByNpc.Remove(npc);
        }
    }

    NpcTaskOffer GetLastCompletedOfferForNpc(GameObject npc)
    {
        CleanupCompletedOfferHistory();

        if (npc == null)
        {
            return null;
        }

        NpcTaskOffer offer;
        return lastCompletedOfferByNpc.TryGetValue(npc, out offer)
            ? offer
            : null;
    }

    void RecordCompletedOffer(GameObject npc, NpcTaskOffer offer)
    {
        if (npc == null || offer == null)
        {
            return;
        }

        CleanupCompletedOfferHistory();
        lastCompletedOfferByNpc[npc] = offer;
    }

    void MoveOfferToEnd(NpcTaskOffer offer)
    {
        if (offer == null ||
            offers == null ||
            offers.Length <= 1)
        {
            return;
        }

        int index = System.Array.IndexOf(offers, offer);
        if (index < 0 || index >= offers.Length - 1)
        {
            return;
        }

        for (int i = index; i < offers.Length - 1; i++)
        {
            offers[i] = offers[i + 1];
        }

        offers[offers.Length - 1] = offer;
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
    public bool autoAssignNearbyTasks;
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
    public bool spreadVisitorsAroundProvider = true;
    public float providerVisitorStandRadius = 0.65f;
    [Header("Task Travel Watchdog")]
    [Min(0.05f)] public float taskTravelProgressEpsilon = 0.12f;
    [Min(0.25f)] public float taskTravelNoProgressTimeout = 3f;
    [Min(1f)] public float taskTravelMinStageDuration = 10f;
    [Min(1f)] public float taskTravelMaxStageDuration = 45f;
    [Min(1f)] public float taskTravelDurationMultiplier = 4f;
    [Min(0)] public int taskTravelMaxRecoveries = 2;
    public float stuckTurnInDistance = 2.25f;
    public float huntAttackRange = 1.4f;
    public float huntAttackInterval = 1.2f;
    public float huntTargetRetryDelay = 18f;
    [Min(1)] public int maxHuntTargetSearchAttempts = 6;
    [Min(1f)] public float maxHuntTaskWaitFallbackSeconds = 180f;
    [Min(1f)] public float maxHuntTaskWaitWorldHours = 72f;
    public bool requireNpcPowerAboveBeastLevel = true;
    public int huntRequiredPowerMargin = 2;
    [Range(0f, 2f)] public float minimumTaskRewardMarkup = 0.2f;
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
    public Transform patrolPointB;
    public Transform deliverPoint;

    [Header("Escort Task")]
    public Transform escortMeetPoint;
    public Transform escortCompletionPoint;
    [Min(0.5f)] public float escortNpcSearchRadius = 1.6f;
    [Min(0.5f)] public float escortGreetingDuration = 3f;
    [Min(0.1f)] public float escortFollowDistance = 0.9f;
    [Min(0.5f)] public float escortThreatDetectRadius = 5f;
    [Min(0.5f)] public float escortThreatAvoidRadius = 6f;
    [Min(0.1f)] public float escortThreatAvoidDuration = 8f;
    [Min(0.1f)] public float escortThreatFightPowerRatio = 1.05f;
    [Min(0.1f)] public float escortThreatFleePowerRatio = 0.85f;
    [Min(0.2f)] public float escortAttackInterval = 1.2f;
    Vector3 escortMeetAnchorPosition;
    Vector3 escortCompletionAnchorPosition;
    bool escortAnchorPositionsCaptured;
    int lastTaskCatalogRefreshDay = -1;
    bool pendingTaskCatalogRefresh;

    [Header("Harvest Delivery")]
    public bool includeLinhRiceHarvestTask = true;
    public StatItemData linhRiceItem;
    public Transform linhRiceFieldPoint;
    [Min(1)] public int linhRiceAmountMin = 5;
    [Min(1)] public int linhRiceAmountMax = 9;
    public int linhRiceRewardSpiritStone = 900;
    public float linhRiceHarvestDuration = 8f;

    [Header("Task Acceptance")]
    public bool requireNpcTaskWillingness = true;
    public bool autoAssignRequiresTaskIntent = true;
    [Range(0f, 100f)]
    public float maxTaskAcceptFatigue = 78f;
    [Range(0f, 100f)]
    public float maxTaskAcceptHunger = 70f;
    [Range(0, 100)]
    public int minHuntTaskBravery = 45;
    [Range(0f, 100f)]
    public float minTaskWillingnessScore = 20f;
    [Range(0f, 100f)]
    public float minAutoAssignWillingnessScore = 45f;
    public bool rejectNonAdultVillagerTasks = true;

    [Header("Auto Assign Rank Gates")]
    public bool enforceAutoAssignRankGates = true;
    public CultivationRealm autoAssignTrungMinRealm = CultivationRealm.Foundation;
    [Range(1, CultivationProgression.MaxStage)]
    public int autoAssignTrungMinStage = 1;
    public CultivationRealm autoAssignThuongMinRealm = CultivationRealm.NascentSoul;
    [Range(1, CultivationProgression.MaxStage)]
    public int autoAssignThuongMinStage = 1;

    [Header("Provider Placement")]
    public bool keepProviderStationary = true;
    public Transform providerStandPoint;
    public bool disableBaseAiWhileStationary = true;

    [Header("Reward Wallet")]
    public ItemInventory inventory;
    [InspectorName("Quỹ thưởng Linh Thạch ban đầu")]
    public int startingRewardMoney = 100000;
    [InspectorName("Dự trữ Linh Thạch tối thiểu")]
    public int minimumRewardMoneyReserve = 50000;
    public bool refillRewardMoneyWhenLow = true;
    [SerializeField, InspectorName("Quỹ thưởng Linh Thạch")] int serviceRewardMoney;

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
    [Min(0.1f)]
    public float forestTeleportExitBuffer = 1.5f;
    [Range(0f, 1f)]
    public float gatherDepthMin = 0.15f;
    [Range(0f, 1f)]
    public float gatherDepthMax = 0.65f;
    [Range(0f, 1f)]
    public float huntDepthMin = 0.45f;
    [Range(0f, 1f)]
    public float huntDepthMax = 0.95f;
    public int forestPointPickAttempts = 24;

    [Header("Expanded Task Catalog")]
    public bool useExpandedDefaultTaskCatalog = true;
    public int minExpandedTaskOffers = 10;

    [Header("Offers")]
    public NpcTaskOffer[] offers =
    {
        new NpcTaskOffer
        {
            taskName = "",
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
            taskName = "",
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
            taskName = "",
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

        if (patrolPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(patrolPoint.position, 0.3f);
        }

        if (patrolPointB != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(patrolPointB.position, 0.3f);
        }

        if (patrolPoint != null && patrolPointB != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(patrolPoint.position, patrolPointB.position);
        }
    }
}







