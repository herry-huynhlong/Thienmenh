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
    Escort
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
    public CultivationRealm minRealm = CultivationRealm.QiRefining;
    [Range(1, 9)]
    public int minRealmStage = 1;
    public int rewardSpiritStone = 20;
    public int rewardCultivationExp;
    public StatItemData rewardItem;
    public string rewardItemId;
    public int rewardItemAmount;
    public float workDuration = 12f;

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

    void EnsureExpandedDefaultOffers()
    {
        if (!useExpandedDefaultTaskCatalog)
        {
            return;
        }

        offers = BuildExpandedDefaultOffers();
    }

    void NormalizeConfiguredOfferText()
    {
        if (offers == null)
        {
            return;
        }

        foreach (NpcTaskOffer offer in offers)
        {
            if (offer == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(offer.taskName) || LooksCorruptedText(offer.taskName))
            {
                offer.taskName = GetDefaultTaskNameForOffer(offer);
            }
        }
    }

    void ResolveConfiguredOfferItemReferences()
    {
        if (offers == null)
        {
            return;
        }

        foreach (NpcTaskOffer offer in offers)
        {
            ResolveOfferItemReferences(offer);
        }
    }

    static void ResolveOfferItemReferences(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return;
        }

        offer.ResolveItemReferences();
    }

    string TaskName(string key)
    {
        return NpcText.Get("taskNames", key, key);
    }

    string TaskNameFormat(string key, params object[] args)
    {
        return NpcText.Format(TaskName(key), args);
    }

    string TaskAction(string key)
    {
        return NpcText.Get("taskActions", key, key);
    }

    string TaskActionFormat(string key, params object[] args)
    {
        return NpcText.Format(TaskAction(key), args);
    }

    string TaskDisplay(string key)
    {
        return NpcText.Get("taskDisplay", key, key);
    }

    string TaskDisplayFormat(string key, params object[] args)
    {
        return NpcText.Format(TaskDisplay(key), args);
    }

    bool LooksCorruptedText(string value)
    {
        return !string.IsNullOrEmpty(value) &&
            (value.Contains("Ã") ||
                value.Contains("Â") ||
                value.Contains("â€") ||
                value.Contains("\u0081") ||
                value.Contains("\u008D") ||
                value.Contains("\u0090"));
    }

    string GetOfferTaskName(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(offer.taskName) && !LooksCorruptedText(offer.taskName))
        {
            return offer.taskName;
        }

        return GetDefaultTaskNameForOffer(offer);
    }

    string GetDefaultTaskNameForOffer(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return TaskName("gatherResource");
        }

        switch (offer.taskType)
        {
            case NpcTaskType.Patrol:
                return offer.rank == NpcTaskRank.Ha
                    ? TaskName("patrolVillageEdge")
                    : TaskName("patrolOutsideVillage");
            case NpcTaskType.HuntMonster:
                return TaskName("huntDangerousMonster");
            case NpcTaskType.Cultivate:
                return TaskName("protectCultivation");
            case NpcTaskType.Deliver:
                return TaskName("transportSpiritMaterial");
            case NpcTaskType.Escort:
                return TaskName("escortCaravan");
            case NpcTaskType.HarvestAndDeliver:
                return TaskName("harvestLinhRice");
            case NpcTaskType.GatherResource:
                if (offer.rank == NpcTaskRank.Thuong)
                {
                    return TaskName("findRareHerbDeepMountain");
                }

                if (offer.rank == NpcTaskRank.Trung)
                {
                    return TaskName("gatherSpiritMaterialsNearMaThuSon");
                }

                return TaskName("gatherLowSpiritHerb");
            default:
                return TaskName("gatherResource");
        }
    }
    NpcTaskOffer[] BuildExpandedDefaultOffers()
    {
        List<NpcTaskOffer> defaultOffers = new List<NpcTaskOffer>
        {
        };

        if (includeLinhRiceHarvestTask)
        {
            NpcTaskOffer linhRiceOffer = CreateLinhRiceHarvestOffer();
            if (linhRiceOffer != null)
            {
                defaultOffers.Add(linhRiceOffer);
            }
        }

        AppendMapDrivenGatherOffers(defaultOffers);
        AppendMapDrivenHuntOffers(defaultOffers);

        if (defaultOffers.Count == 0)
        {
            defaultOffers.Add(
                CreateGatherOffer(
                    TaskName("gatherLowSpiritHerb"),
                    NpcTaskRank.Ha,
                    CultivationRealm.Mortal,
                    1,
                    6,
                    1200,
                    0,
                    10f));
        }

        SortOffersByDisplayOrder(defaultOffers);
        return defaultOffers.ToArray();
    }

    void SortOffersByDisplayOrder(List<NpcTaskOffer> targetOffers)
    {
        if (targetOffers == null ||
            targetOffers.Count <= 1)
        {
            return;
        }

        targetOffers.Sort(CompareOffersForDisplay);
    }

    int CompareOffersForDisplay(
        NpcTaskOffer left,
        NpcTaskOffer right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int rankCompare =
            GetOfferRankSortValue(left.rank).CompareTo(
                GetOfferRankSortValue(right.rank));
        if (rankCompare != 0)
        {
            return rankCompare;
        }

        int typeCompare =
            left.taskType.CompareTo(right.taskType);
        if (typeCompare != 0)
        {
            return typeCompare;
        }

        return string.Compare(
            left.taskName,
            right.taskName,
            System.StringComparison.OrdinalIgnoreCase);
    }

    int GetOfferRankSortValue(NpcTaskRank rank)
    {
        switch (rank)
        {
            case NpcTaskRank.Ha:
                return 0;
            case NpcTaskRank.Trung:
                return 1;
            case NpcTaskRank.Thuong:
                return 2;
            default:
                return 99;
        }
    }

    NpcTaskOffer CreateLinhRiceHarvestOffer()
    {
        StatItemData item = ResolveLinhRiceItem();
        if (item == null)
        {
            return null;
        }

        NpcTaskOffer offer = CreateSimpleOffer(
            BuildHarvestTaskName(item),
            NpcTaskType.HarvestAndDeliver,
            NpcTaskRank.Ha,
            CultivationRealm.Mortal,
            1,
            linhRiceRewardSpiritStone,
            0,
            linhRiceHarvestDuration);

        offer.requiredItem = item;
        offer.requiredAmount = Mathf.Max(1, linhRiceAmountMin);
        offer.randomizeRequiredItemAmount = true;
        offer.requiredItemAmountMin = Mathf.Max(1, linhRiceAmountMin);
        offer.requiredItemAmountMax = Mathf.Max(offer.requiredItemAmountMin, linhRiceAmountMax);
        offer.autoPriceRequiredItemReward = true;
        ConfigureOfferRewardMarkup(offer, NpcTaskRank.Ha);
        offer.rewardSpiritStone = EstimateOfferRewardSpiritStone(offer, item);
        return offer;
    }

    NpcTaskOffer CreateGatherOffer(string name, NpcTaskRank rank, CultivationRealm realm, int stage, int amount, int reward, int exp, float duration)
    {
        NpcTaskOffer offer = CreateSimpleOffer(name, NpcTaskType.GatherResource, rank, realm, stage, reward, exp, duration);
        offer.requiredAmount = Mathf.Max(1, amount);
        offer.randomizeRequiredItemAmount = true;
        offer.requiredItemAmountMin = Mathf.Max(1, amount - 2);
        offer.requiredItemAmountMax = Mathf.Max(offer.requiredItemAmountMin, amount + 2);
        offer.autoPriceRequiredItemReward = true;
        return offer;
    }

    NpcTaskOffer CreateHuntOffer(string name, NpcTaskRank rank, CultivationRealm realm, int stage, int beastLevel, int amount, int reward, int exp, float duration)
    {
        NpcTaskOffer offer = CreateSimpleOffer(name, NpcTaskType.HuntMonster, rank, realm, stage, reward, exp, duration);
        offer.requiredBeastLevel = Mathf.Max(1, beastLevel);
        offer.requiredMonsterKills = Mathf.Max(1, amount);
        offer.requiredAmount = Mathf.Max(1, amount);
        offer.consumeRequiredItemsOnTurnIn = true;
        offer.autoPriceRequiredItemReward = true;
        offer.useRankRewardMultiplier = false;
        return offer;
    }

    NpcTaskOffer CreateHuntAnimalOffer(string name, NpcTaskRank rank, CultivationRealm realm, int stage, int amount, int reward, int exp, float duration)
    {
        NpcTaskOffer offer = CreateHuntOffer(name, rank, realm, stage, 0, amount, reward, exp, duration);
        offer.requiredHuntTargetType = HuntTargetType.Animal;
        return offer;
    }

    NpcTaskOffer CreatePatrolOffer(string name, NpcTaskRank rank, CultivationRealm realm, int stage, int reward, int exp, float duration)
    {
        return CreateSimpleOffer(name, NpcTaskType.Patrol, rank, realm, stage, reward, exp, duration);
    }

    NpcTaskOffer CreateSimpleOffer(string name, NpcTaskType type, NpcTaskRank rank, CultivationRealm realm, int stage, int reward, int exp, float duration)
    {
        return new NpcTaskOffer
        {
            taskName = name,
            taskType = type,
            rank = rank,
            minRealm = realm,
            minRealmStage = Mathf.Clamp(stage, 1, CultivationProgression.MaxStage),
            rewardSpiritStone = Mathf.Max(0, reward),
            rewardCultivationExp = 0,
            workDuration = Mathf.Max(1f, duration)
        };
    }

    void AppendMapDrivenGatherOffers(List<NpcTaskOffer> targetOffers)
    {
        if (targetOffers == null)
        {
            return;
        }

        List<StatItemData> items = new List<StatItemData>();
        HashSet<StatItemData> seen = new HashSet<StatItemData>();

        foreach (WorldResourceField field in WorldResourceField.Fields)
        {
            if (field == null || field.items == null)
            {
                continue;
            }

            for (int i = 0; i < field.items.Count; i++)
            {
                ResourceFieldItemEntry entry = field.items[i];
                StatItemData item = entry != null ? entry.item : null;
                if (item == null ||
                    IsLinhRiceItem(item) ||
                    !seen.Add(item))
                {
                    continue;
                }

                items.Add(item);
            }
        }

        items.Sort((a, b) =>
        {
            int gradeCompare = a.grade.CompareTo(b.grade);
            if (gradeCompare != 0)
            {
                return gradeCompare;
            }

            return string.Compare(
                ItemText.Name(a),
                ItemText.Name(b),
                System.StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < items.Count; i++)
        {
            NpcTaskOffer offer = CreateGatherOfferForItem(items[i]);
            if (offer != null)
            {
                targetOffers.Add(offer);
            }
        }
    }

    void AppendMapDrivenHuntOffers(List<NpcTaskOffer> targetOffers)
    {
        if (targetOffers == null)
        {
            return;
        }

        List<HuntOfferSeed> seeds = new List<HuntOfferSeed>();

        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            if (!IsWorkThreatMonster(monster))
            {
                continue;
            }

            if (monster.huntTargetType != HuntTargetType.Beast)
            {
                continue;
            }

            NpcMapArea area = NpcMapArea.FindArea(monster.transform.position);
            if (area == null || area.zone != NpcMapZone.MaThuSonMach)
            {
                continue;
            }

            StatItemData loot = monster.GetDeathLoot();
            if (loot == null)
            {
                continue;
            }

            HuntOfferSeed existing = null;
            for (int i = 0; i < seeds.Count; i++)
            {
                HuntOfferSeed seed = seeds[i];
                if (seed != null &&
                    seed.lootItem == loot &&
                    seed.realm == monster.realm &&
                    seed.targetType == monster.huntTargetType)
                {
                    existing = seed;
                    break;
                }
            }

            if (existing == null)
            {
                existing = new HuntOfferSeed
                {
                    lootItem = loot,
                    targetType = monster.huntTargetType,
                    realm = monster.realm,
                    maxStage = Mathf.Clamp(
                        monster.realmStage,
                        1,
                        CultivationProgression.MaxStage)
                };
                seeds.Add(existing);
                continue;
            }

            existing.maxStage = Mathf.Max(
                existing.maxStage,
                Mathf.Clamp(monster.realmStage, 1, CultivationProgression.MaxStage));
        }

        seeds.Sort((a, b) =>
        {
            int powerCompare = CultivationProgression.GetRealmPower(a.realm, a.maxStage)
                .CompareTo(CultivationProgression.GetRealmPower(b.realm, b.maxStage));
            if (powerCompare != 0)
            {
                return powerCompare;
            }

            return string.Compare(
                ItemText.Name(a.lootItem),
                ItemText.Name(b.lootItem),
                System.StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < seeds.Count; i++)
        {
            NpcTaskOffer offer = CreateHuntOfferForSeed(seeds[i]);
            if (offer != null)
            {
                targetOffers.Add(offer);
            }
        }
    }

    NpcTaskOffer CreateGatherOfferForItem(StatItemData item)
    {
        if (item == null)
        {
            return null;
        }

        NpcTaskRank rank = GetRankForItemGrade(item.grade);
        CultivationRealm realm;
        int stage;
        ResolveGatherMinimumRequirement(rank, out realm, out stage);

        Vector2Int amountRange = GetGatherAmountRange(item.grade);
        float duration = Mathf.Lerp(8f, 18f, Mathf.InverseLerp(0f, 3f, (int)item.grade));

        NpcTaskOffer offer = CreateSimpleOffer(
            BuildGatherTaskName(item),
            NpcTaskType.GatherResource,
            rank,
            realm,
            stage,
            0,
            0,
            duration);

        offer.requiredItem = item;
        offer.requiredAmount = amountRange.x;
        offer.randomizeRequiredItemAmount = true;
        offer.requiredItemAmountMin = amountRange.x;
        offer.requiredItemAmountMax = amountRange.y;
        offer.autoPriceRequiredItemReward = true;
        offer.useRankRewardMultiplier = false;
        ConfigureOfferRewardMarkup(offer, rank);
        offer.rewardSpiritStone = EstimateOfferRewardSpiritStone(offer, item);
        return offer;
    }

    NpcTaskOffer CreateHuntOfferForSeed(HuntOfferSeed seed)
    {
        if (seed == null || seed.lootItem == null)
        {
            return null;
        }

        NpcTaskRank rank = GetRankForMonsterRealm(seed.realm);
        CultivationRealm minRealm;
        int minStage;
        ResolveSafeHuntNpcRequirement(
            seed.realm,
            seed.maxStage,
            out minRealm,
            out minStage);

        int killAmount = seed.maxStage <= 4 ? 2 : 1;
        float duration = Mathf.Lerp(
            20f,
            52f,
            Mathf.InverseLerp(
                CultivationProgression.GetRealmPower(CultivationRealm.QiRefining, 1),
                CultivationProgression.GetRealmPower(CultivationRealm.NascentSoul, CultivationProgression.MaxStage),
                CultivationProgression.GetRealmPower(seed.realm, seed.maxStage)));

        NpcTaskOffer offer = CreateSimpleOffer(
            BuildHuntTaskName(seed.realm, seed.maxStage),
            NpcTaskType.HuntMonster,
            rank,
            minRealm,
            minStage,
            0,
            0,
            duration);

        offer.requiredItem = seed.lootItem;
        offer.requiredAmount = killAmount;
        offer.requiredMonsterKills = killAmount;
        offer.consumeRequiredItemsOnTurnIn = true;
        offer.autoPriceRequiredItemReward = true;
        offer.useRankRewardMultiplier = false;
        offer.requiredHuntTargetType = seed.targetType;
        offer.useMonsterRealmStageRequirement = true;
        offer.requiredMonsterRealm = seed.realm;
        offer.requiredMonsterMaxStage = Mathf.Clamp(
            seed.maxStage,
            1,
            CultivationProgression.MaxStage);
        offer.matchMonsterRealmExactly = true;
        ConfigureOfferRewardMarkup(offer, rank);
        offer.rewardSpiritStone = EstimateOfferRewardSpiritStone(offer, seed.lootItem);
        return offer;
    }

    NpcTaskRank GetRankForItemGrade(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return NpcTaskRank.Trung;

            case ItemGrade.Thuong:
            case ItemGrade.Tien:
                return NpcTaskRank.Thuong;

            default:
                return NpcTaskRank.Ha;
        }
    }

    NpcTaskRank GetRankForMonsterRealm(CultivationRealm realm)
    {
        if (realm >= CultivationRealm.NascentSoul)
        {
            return NpcTaskRank.Thuong;
        }

        if (realm >= CultivationRealm.GoldenCore)
        {
            return NpcTaskRank.Trung;
        }

        return NpcTaskRank.Ha;
    }

    void ResolveGatherMinimumRequirement(
        NpcTaskRank rank,
        out CultivationRealm realm,
        out int stage)
    {
        switch (rank)
        {
            case NpcTaskRank.Trung:
                realm = CultivationRealm.QiRefining;
                stage = 7;
                return;

            case NpcTaskRank.Thuong:
                realm = CultivationRealm.Foundation;
                stage = 7;
                return;

            default:
                realm = CultivationRealm.Mortal;
                stage = 1;
                return;
        }
    }

    Vector2Int GetGatherAmountRange(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return new Vector2Int(3, 5);

            case ItemGrade.Thuong:
                return new Vector2Int(2, 3);

            case ItemGrade.Tien:
                return new Vector2Int(1, 2);

            default:
                return new Vector2Int(5, 8);
        }
    }

    void ConfigureOfferRewardMarkup(
        NpcTaskOffer offer,
        NpcTaskRank rank)
    {
        if (offer == null)
        {
            return;
        }

        float minMarkup = Mathf.Max(minimumTaskRewardMarkup, 0.2f);
        float maxMarkup = minMarkup + 0.15f;

        switch (rank)
        {
            case NpcTaskRank.Trung:
                minMarkup += 0.1f;
                maxMarkup += 0.2f;
                break;

            case NpcTaskRank.Thuong:
                minMarkup += 0.25f;
                maxMarkup += 0.4f;
                break;
        }

        offer.requiredItemRewardMarkupMin = minMarkup;
        offer.requiredItemRewardMarkupMax = Mathf.Max(minMarkup, maxMarkup);
    }

    int EstimateOfferRewardSpiritStone(
        NpcTaskOffer offer,
        StatItemData item)
    {
        if (offer == null || item == null)
        {
            return 0;
        }

        int min = Mathf.Max(1, offer.requiredItemAmountMin);
        int max = Mathf.Max(min, offer.requiredItemAmountMax);
        int estimatedAmount = Mathf.RoundToInt((min + max) * 0.5f);

        return ResolveTaskRewardSpiritStone(
            offer,
            item,
            Mathf.Max(1, estimatedAmount));
    }

    string BuildGatherTaskName(StatItemData item)
    {
        if (item == null)
        {
            return TaskName("gatherResource");
        }

        string itemName = ItemText.Name(item);
        if (item.itemType == ItemType.DanDuoc)
        {
            return TaskNameFormat("pickItemFormat", itemName);
        }

        if (item.itemType == ItemType.ThucPham)
        {
            return BuildHarvestTaskName(item);
        }

        return TaskNameFormat("gatherItemFormat", itemName);
    }

    string BuildHarvestTaskName(StatItemData item)
    {
        return item == null
            ? TaskName("harvestLinhRice")
            : TaskNameFormat("harvestItemFormat", ItemText.Name(item));
    }

    string BuildHuntTaskName(CultivationRealm realm, int stage)
    {
        return TaskNameFormat(
            "huntRealmStageBelowFormat",
            NpcText.Realm(realm),
            Mathf.Clamp(stage, 1, CultivationProgression.MaxStage));
    }

    void ResolveSafeHuntNpcRequirement(
        CultivationRealm targetRealm,
        int targetStage,
        out CultivationRealm npcRealm,
        out int npcStage)
    {
        int targetPower = CultivationProgression.GetRealmPower(
            targetRealm,
            Mathf.Clamp(targetStage, 1, CultivationProgression.MaxStage));
        int requiredPower = targetPower + Mathf.Max(1, huntRequiredPowerMargin);

        int realmIndex =
            Mathf.Clamp(
                requiredPower / CultivationProgression.MaxStage,
                0,
                (int)CultivationRealm.Tribulation);
        int stage = requiredPower % CultivationProgression.MaxStage;
        if (stage == 0)
        {
            stage = CultivationProgression.MaxStage;
            realmIndex = Mathf.Max(0, realmIndex - 1);
        }

        npcRealm = (CultivationRealm)realmIndex;
        npcStage = Mathf.Clamp(stage, 1, CultivationProgression.MaxStage);
    }
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
        CaptureEscortAnchorPositions();
        FreezeEscortAnchors();
        ConfigureStationaryProvider();
        EnsureExpandedDefaultOffers();
        NormalizeConfiguredOfferText();
        ResolveConfiguredOfferItemReferences();
    }

    void OnDisable()
    {
        CompleteInterruptedWork();
        providers.Remove(this);
    }

    void RefreshExpandedCatalogWhenIdle()
    {
        if (!useExpandedDefaultTaskCatalog ||
            runningTasks.Count > 0)
        {
            return;
        }

        if (offers == null ||
            offers.Length < Mathf.Max(1, minExpandedTaskOffers))
        {
            offers = BuildExpandedDefaultOffers();
            NormalizeConfiguredOfferText();
        }
    }

    void UpdateTaskCatalogDailyReset()
    {
        int currentDay = GetCurrentWorldDay();
        if (currentDay < 0)
        {
            return;
        }

        if (lastTaskCatalogRefreshDay < 0)
        {
            lastTaskCatalogRefreshDay = currentDay;
            return;
        }

        if (currentDay == lastTaskCatalogRefreshDay &&
            !pendingTaskCatalogRefresh)
        {
            return;
        }

        if (runningTasks.Count > 0)
        {
            pendingTaskCatalogRefresh = true;
            return;
        }

        ResetTaskCatalogForNewDay(currentDay);
    }

    void ResetTaskCatalogForNewDay(int currentDay)
    {
        CleanupCompletedOfferHistory();
        lastCompletedOfferByNpc.Clear();

        if (useExpandedDefaultTaskCatalog)
        {
            offers = BuildExpandedDefaultOffers();
        }

        NormalizeConfiguredOfferText();
        lastTaskCatalogRefreshDay = currentDay;
        pendingTaskCatalogRefresh = false;
    }

    void CompleteInterruptedWork()
    {
        for (int i = runningTasks.Count - 1; i >= 0; i--)
        {
            RunningNpcTask task = runningTasks[i];
            bool canReward = ShouldRewardInterruptedTask(task) &&
                ConsumeTaskItems(task);

            runningTasks.RemoveAt(i);
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
                : Mathf.Max(1f, offer.workDuration),
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
            task.offer != null ? task.offer.workDuration : 1f);

        DisarmTaskTravelWatchdog(task);
        PrepareTaskWork(task);

        if (task.npc != null)
        {
            NpcRoleUtility.SetAction(
                task.npc,
                TaskActionFormat("missingTurnInItems", GetTaskDisplayText(task)));
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

    void RewardNpc(RunningNpcTask task)
    {
        GameObject npc = task.npc;
        NpcTaskOffer offer = task.offer;
        int rewardSpiritStone =
            task.rewardSpiritStone > 0
            ? task.rewardSpiritStone
            : Mathf.Max(0, offer.rewardSpiritStone);

        PayRewardMoney(npc, rewardSpiritStone);
        ResolveOfferItemReferences(offer);

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
            TaskActionFormat("taskCompleted", GetRankText(offer.rank), GetOfferTaskName(offer)));

        if (WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.AddLog(
                NpcText.Format(
                    NpcText.Get("logs", "taskCompletedWorld"),
                    NpcRoleUtility.GetDisplayName(npc),
                    GetOfferTaskName(offer),
                    rewardSpiritStone),
                0, true);
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
        return IsNpcBusyWithAnyProvider(npc) ||
            HasRunningTask(npc) ||
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

    NpcInteractionPoint GetProviderInteractionPoint()
    {
        Transform point = keepProviderStationary
            ? providerStandPoint
            : providerPoint;

        if (point == null)
        {
            return null;
        }

        NpcInteractionPoint interactionPoint =
            point.GetComponent<NpcInteractionPoint>();
        if (interactionPoint == null && Application.isPlaying)
        {
            interactionPoint =
                point.gameObject.AddComponent<NpcInteractionPoint>();
            interactionPoint.interactionRadius =
                Mathf.Max(0.45f, providerTalkDistance);
            interactionPoint.standSpacing =
                Mathf.Max(0.85f, providerVisitorStandRadius);
            interactionPoint.reservationSpacingRadius = 0.75f;
            interactionPoint.reservationHoldSeconds = 15f;
        }

        return interactionPoint;
    }

    public float GetProviderInteractionDistance()
    {
        NpcInteractionPoint interactionPoint =
            GetProviderInteractionPoint();
        if (interactionPoint != null)
        {
            return Mathf.Max(
                providerTalkDistance,
                arriveDistance,
                interactionPoint.interactionRadius);
        }

        return Mathf.Max(
            providerTalkDistance,
            arriveDistance);
    }

    float GetNpcProviderInteractionDistance(GameObject npc)
    {
        if (npc == null)
        {
            return float.PositiveInfinity;
        }

        return Vector2.Distance(
            npc.transform.position,
            GetProviderPositionFor(npc));
    }

    public bool IsNpcInProviderInteractionRange(GameObject npc)
    {
        return GetNpcProviderInteractionDistance(npc) <=
            GetProviderInteractionDistance();
    }

    public Vector3 GetProviderPositionFor(GameObject npc)
    {
        Vector3 center = GetProviderPosition();
        NpcInteractionPoint interactionPoint =
            GetProviderInteractionPoint();
        if (interactionPoint != null)
        {
            return interactionPoint.GetStandPositionFor(npc);
        }

        if (!spreadVisitorsAroundProvider ||
            npc == null ||
            providerVisitorStandRadius <= 0.01f)
        {
            return GetClearTaskPositionNear(center, npc);
        }

        int hash = Mathf.Abs(npc.GetInstanceID());
        float angle = (hash % 360) * Mathf.Deg2Rad;
        float standRadius = Mathf.Max(
            providerVisitorStandRadius,
            arriveDistance * 2.5f,
            1.1f);
        Vector2 offset =
            new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
            standRadius;

        return GetClearTaskPositionNear(center + (Vector3)offset, npc);
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
            NpcInteractionPoint interactionPoint =
                counterPoint.GetComponent<NpcInteractionPoint>();

            if (interactionPoint != null)
            {
                return interactionPoint.GetStandPositionFor(npc);
            }

            return GetClearTaskPositionNear(counterPoint.position, npc);
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null)
        {
            return GetClearTaskPositionNear(transform.position, npc);
        }

        return GetClearTaskPositionNear(broker.GetCustomerPositionFor(npc), npc);
    }

    Vector3 GetBoardPosition(GameObject npc = null)
    {
        Vector3 position = taskBoardPoint != null
            ? taskBoardPoint.position
            : transform.position;

        if (taskBoardPoint != null)
        {
            NpcInteractionPoint interactionPoint =
                taskBoardPoint.GetComponent<NpcInteractionPoint>();

            if (interactionPoint != null)
            {
                return interactionPoint.GetStandPositionFor(npc);
            }
        }

        return GetClearTaskPositionNear(position, npc);
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
        NpcRoleUtility.SetAction(broker.gameObject, TaskAction("counterTradeWithCustomer"));

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

    Vector3 ResolveActiveCounterTradePosition(
        GameObject npc,
        Vector3 fallbackPosition)
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null ||
            !broker.receiveAllNpcRequests)
        {
            return fallbackPosition;
        }

        Vector3 brokerPosition =
            broker.GetCustomerPositionFor(npc);
        brokerPosition.z = fallbackPosition.z;
        return brokerPosition;
    }

    bool IsNpcReadyForCounterTrade(
        GameObject npc,
        Vector3 counterTarget)
    {
        if (npc == null)
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests)
        {
            return broker.IsCustomerAtCounter(npc);
        }

        return Vector2.Distance(
            npc.transform.position,
            counterTarget) <= arriveDistance;
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
                return NpcMapZone.MaThuSonMach;
            case NpcTaskType.Patrol:
                return null;
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
        NpcRouteStatus routeStatus;
        Vector3 moveTarget = NpcMapNavigator.GetNextMoveTarget(
            npc,
            target,
            forcedTargetZone,
            out usingTeleportRoute,
            out routeAction,
            out routeStatus);

        if ((usingTeleportRoute || IsRouteBlocked(routeStatus)) &&
            !string.IsNullOrEmpty(routeAction))
        {
            NpcRoleUtility.SetAction(npc, routeAction);
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null && mover.enabled)
        {
            mover.SetMoveTarget(
                moveTarget,
                !string.IsNullOrEmpty(routeAction) ? routeAction : TaskAction("followTaskRoute"),
                true);
            return;
        }

        float speed = NpcRoleUtility.GetMoveSpeed(npc, fallbackMoveSpeed);
        MoveNpcTransformSafely(
            npc,
            moveTarget,
            speed * Time.deltaTime);
    }

    bool IsRouteBlocked(NpcRouteStatus routeStatus)
    {
        return routeStatus == NpcRouteStatus.NoGate ||
            routeStatus == NpcRouteStatus.InvalidGate;
    }

    void MoveNpcTransformSafely(
        GameObject npc,
        Vector3 moveTarget,
        float maxDistanceDelta)
    {
        Vector3 current = npc.transform.position;
        if (IsTaskPositionBlocked(current, npc))
        {
            npc.transform.position = GetClearTaskPositionNear(current, npc);
            return;
        }

        Vector3 next =
            Vector3.MoveTowards(
                current,
                moveTarget,
                maxDistanceDelta);

        if (!IsTaskPositionBlocked(next, npc))
        {
            npc.transform.position = next;
            return;
        }

        Vector2 direction = (Vector2)(moveTarget - current);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();
        float step = Mathf.Max(maxDistanceDelta, arriveDistance * 0.5f);
        float[] angles = { 35f, -35f, 70f, -70f, 110f, -110f, 180f };
        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 detour = RotateDirection(direction, angles[i]);
            Vector3 candidate =
                current +
                new Vector3(detour.x, detour.y, 0f) * step;

            if (!IsTaskPositionBlocked(candidate, npc))
            {
                npc.transform.position = candidate;
                return;
            }
        }
    }

    bool IsNpcRecoveringFromDamage(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        return smartNpc != null &&
            smartNpc.IsRecoveringFromDamage;
    }

    void HoldNpcForDamage(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        NpcRoleUtility.StopForConversation(npc);
        NpcRoleUtility.SetAction(npc, TaskAction("injured"));
    }

    Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;

        Vector3 referencePosition = gate != null
            ? gate.ExitPosition
            : transform.position;

        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindArea(referencePosition);
        }

        NpcMapZone? resolvedZone =
            area != null
                ? area.zone
                : NpcMapNavigator.ResolveActorZone(gameObject);

        if (gate != null)
        {
            if (resolvedZone.HasValue)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedZone.Value);
            }
            else
            {
                NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
                resolvedZone = gate.toZone;
            }

            area = NpcMapNavigator.ResolveMapAreaAfterTeleport(
                gameObject,
                resolvedZone.Value,
                referencePosition);
        }
        else if (area != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
        }
    }

    string GetRankText(NpcTaskRank rank)
    {
        return NpcText.Get("taskRanks", rank.ToString(), rank.ToString());
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

        if (npc == null)
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

        Rigidbody2D body = npc.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
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

    void CleanupTaskRuntimeState(RunningNpcTask task)
    {
        ClearTaskReservations(task);
        UnmarkNpcBusyWithProvider(task != null ? task.npc : null);
        ReleaseTaskOffer(task != null ? task.offer : null);
        RestoreEscortCompanionHome(task);
        ResumeEscortCompanion(task);
        if (task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.Escort)
        {
            UnlockEscortOffer(task.offer);
        }
        ResetNpcAfterTaskCleanup(task != null ? task.npc : null);
        ResumeBaseAi(task);
    }

    void ResetNpcAfterTaskCleanup(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.StopMoving();
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.StopForConversation(0.05f);
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.StopForConversation(0.05f);
        }

        Rigidbody2D body = npc.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        NpcRoleUtility.SetAction(
            npc,
            NpcText.Action("idle"));
    }

    void ResumeEscortCompanion(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        ResumeBaseAi(task.escortPausedCompanionBaseAi, task.escortPausedCompanionBaseAiWasEnabled);
        task.escortPausedCompanionBaseAi = null;
    }

    void RestoreEscortCompanionHome(RunningNpcTask task)
    {
        if (task == null ||
            task.escortCompanionNpc == null)
        {
            return;
        }

        Vector3 homePosition = task.escortCompanionHomePosition;
        if (homePosition == Vector3.zero &&
            escortAnchorPositionsCaptured)
        {
            homePosition = escortMeetAnchorPosition;
        }

        task.escortCompanionNpc.transform.position = homePosition;

        Rigidbody2D rb = task.escortCompanionNpc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = homePosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        NpcRoleUtility.StopForConversation(task.escortCompanionNpc);
        NpcRoleUtility.SetAction(
            task.escortCompanionNpc,
            TaskAction("pausedTask"));
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







