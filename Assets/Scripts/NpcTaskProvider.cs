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
    public string taskName = "Thu thập tài nguyên";
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
    [Min(0)] public int requiredBeastLevel;
    public HuntTargetType requiredHuntTargetType = HuntTargetType.Beast;
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

[RequireComponent(typeof(NpcSpecialProfession))]
public class NpcTaskProvider : MonoBehaviour
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
    public float stuckTurnInDistance = 2.25f;
    public float huntAttackRange = 1.4f;
    public float huntAttackInterval = 1.2f;
    public float huntTargetRetryDelay = 18f;
    public bool requireNpcPowerAboveBeastLevel = true;
    public int huntRequiredPowerMargin = 2;
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

    [Header("Harvest Delivery")]
    public bool includeLinhRiceHarvestTask = true;
    public StatItemData linhRiceItem;
    public Transform linhRiceFieldPoint;
    [Min(1)] public int linhRiceAmountMin = 5;
    [Min(1)] public int linhRiceAmountMax = 9;
    public int linhRiceRewardSpiritStone = 900;
    public int linhRiceRewardCultivationExp = 12;
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

    [Header("Provider Placement")]
    public bool keepProviderStationary = true;
    public Transform providerStandPoint;

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
            taskName = "Thu thập linh thảo hạ phẩm",
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
            taskName = "Tuần tra ngoài làng",
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
            taskName = "Săn yêu thú nguy hiểm",
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

        int currentCount = offers != null ? offers.Length : 0;
        if (currentCount >= Mathf.Max(1, minExpandedTaskOffers))
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

    string TaskName(string key)
    {
        return NpcText.Get("taskNames", key, key);
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
            CreateGatherOffer(TaskName("gatherHerbsAroundForest"), NpcTaskRank.Ha, CultivationRealm.Mortal, 1, 6, 900, 15, 10f),
            CreateGatherOffer(TaskName("gatherLowHerbs"), NpcTaskRank.Ha, CultivationRealm.Mortal, 1, 8, 1400, 25, 12f),
            CreateGatherOffer(TaskName("gatherSpiritMaterialsNearMaThuSon"), NpcTaskRank.Trung, CultivationRealm.Foundation, 1, 10, 6500, 80, 15f),
            CreateGatherOffer(TaskName("gatherMidSpiritMedicine"), NpcTaskRank.Trung, CultivationRealm.Foundation, 4, 12, 9500, 120, 18f),
            CreateGatherOffer(TaskName("findRareHerbDeepMountain"), NpcTaskRank.Thuong, CultivationRealm.GoldenCore, 1, 8, 42000, 420, 24f),
            CreateHuntOffer(TaskName("huntBeastLv1ForDemonCore"), NpcTaskRank.Ha, CultivationRealm.QiRefining, 1, 1, 2, 3500, 45, 24f),
            CreateHuntOffer(TaskName("clearBeastGroupLv1"), NpcTaskRank.Ha, CultivationRealm.QiRefining, 4, 1, 4, 7600, 90, 30f),
            CreateHuntOffer(TaskName("huntBeastLv2ForDemonCore"), NpcTaskRank.Trung, CultivationRealm.Foundation, 1, 2, 2, 28000, 260, 36f),
            CreateHuntOffer(TaskName("clearBeastDenLv2"), NpcTaskRank.Trung, CultivationRealm.Foundation, 4, 2, 4, 62000, 520, 42f),
            CreateHuntOffer(TaskName("huntBeastLv3ForInnerCore"), NpcTaskRank.Thuong, CultivationRealm.GoldenCore, 1, 3, 2, 180000, 1400, 54f),
            CreateHuntOffer(TaskName("killGoldenCoreDangerousBeast"), NpcTaskRank.Thuong, CultivationRealm.GoldenCore, 5, 3, 3, 320000, 2400, 60f),
            CreateHuntOffer(TaskName("pursueBeastLv4"), NpcTaskRank.Thuong, CultivationRealm.NascentSoul, 1, 4, 1, 650000, 5200, 72f),
            CreateHuntAnimalOffer(TaskName("huntDeerAntler"), NpcTaskRank.Ha, CultivationRealm.Mortal, 1, 2, 1600, 15, 18f),
            CreateHuntAnimalOffer(TaskName("trapForestRabbit"), NpcTaskRank.Ha, CultivationRealm.Mortal, 1, 3, 1200, 12, 16f),
            CreatePatrolOffer(TaskName("patrolVillageEdge"), NpcTaskRank.Ha, CultivationRealm.Mortal, 1, 1800, 20, 16f),
            CreatePatrolOffer(TaskName("patrolMaThuSonRoad"), NpcTaskRank.Trung, CultivationRealm.Foundation, 2, 12000, 130, 22f),
            CreatePatrolOffer(TaskName("suppressDemonicAura"), NpcTaskRank.Thuong, CultivationRealm.GoldenCore, 3, 90000, 800, 32f),
            CreateSimpleOffer(TaskName("escortCaravan"), NpcTaskType.Escort, NpcTaskRank.Ha, CultivationRealm.QiRefining, 1, 2200, 20, 14f),
            CreateSimpleOffer(TaskName("transportSpiritMaterial"), NpcTaskType.Deliver, NpcTaskRank.Trung, CultivationRealm.Foundation, 2, 15000, 150, 20f),
        };

        if (includeLinhRiceHarvestTask)
        {
            defaultOffers.Insert(0, CreateLinhRiceHarvestOffer());
        }

        return defaultOffers.ToArray();
    }

    NpcTaskOffer CreateLinhRiceHarvestOffer()
    {
        NpcTaskOffer offer = CreateSimpleOffer(
            TaskName("harvestLinhRice"),
            NpcTaskType.GatherResource,
            NpcTaskRank.Ha,
            CultivationRealm.Mortal,
            1,
            linhRiceRewardSpiritStone,
            linhRiceRewardCultivationExp,
            linhRiceHarvestDuration);

        offer.requiredItem = ResolveLinhRiceItem();
        offer.requiredAmount = Mathf.Max(1, linhRiceAmountMin);
        offer.randomizeRequiredItemAmount = true;
        offer.requiredItemAmountMin = Mathf.Max(1, linhRiceAmountMin);
        offer.requiredItemAmountMax = Mathf.Max(offer.requiredItemAmountMin, linhRiceAmountMax);
        offer.autoPriceRequiredItemReward = true;
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
            rewardCultivationExp = Mathf.Max(0, exp),
            workDuration = Mathf.Max(1f, duration)
        };
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
            UnmarkNpcBusyWithProvider(task != null ? task.npc : null);
            RestoreEscortCompanionHome(task);
            ResumeBaseAi(task);

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
        CaptureStationaryPosition();
        CaptureEscortAnchorPositions();
        FreezeEscortAnchors();
        ConfigureStationaryProvider();
        EnsureExpandedDefaultOffers();
        NormalizeConfiguredOfferText();

        NpcSpecialProfession profession =
            GetComponent<NpcSpecialProfession>();

        if (profession == null)
        {
            return;
        }

        profession.professionName = NpcText.Get("professions", "tavernManager", "Quản Sự Tửu Quán");
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
        if (autoAssignNearbyTasks)
        {
            TryStartTaskRequest();
        }
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
            NpcTaskOffer offer = PickOfferFor(npc, false);
            if (offer != null &&
                StartTaskRequest(npc, offer))
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
        if (npc == null ||
            offer == null ||
            npc == gameObject ||
            HasBusyNpc(npc) ||
            NpcRoleUtility.IsDead(npc) ||
            !NpcScheduleController.AllowsTask(npc) ||
            !CanNpcAcceptOffer(npc, offer))
        {
            return false;
        }

        return StartTaskRequest(npc, offer, true);
    }

    public List<NpcTaskOffer> PickDailyOffersFor(
        GameObject npc,
        int minCount,
        int maxCount)
    {
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
                !CanNpcAcceptOffer(npc, offer))
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
                !CanNpcAcceptOffer(npc, offer))
            {
                continue;
            }

            result.Add(offer);
        }

        return result;
    }

    public List<NpcTaskOffer> GetVisibleOffers()
    {
        List<NpcTaskOffer> result = new List<NpcTaskOffer>();

        if (offers == null ||
            offers.Length == 0)
        {
            return result;
        }

        foreach (NpcTaskOffer offer in offers)
        {
            if (!IsOfferWorldAvailable(offer))
            {
                continue;
            }

            result.Add(offer);
        }

        return result;
    }

    public StatItemData GetPlannedRequiredItem(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

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

            if (npc == null ||
                npc == gameObject ||
                HasBusyNpc(npc) ||
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
                HasBusyNpc(npc) ||
                !NpcScheduleController.AllowsTask(npc))
            {
                continue;
            }

            NpcTaskOffer offer = PickOfferFor(npc, true);
            if (offer == null)
            {
                continue;
            }

            if (StartTaskRequest(npc, offer))
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
        bool startAtProvider)
    {
        if (npc == null ||
            offer == null ||
            npc == gameObject ||
            HasBusyNpc(npc) ||
            NpcRoleUtility.IsDead(npc) ||
            !NpcScheduleController.AllowsTask(npc) ||
            !CanNpcAcceptOffer(npc, offer))
        {
            return false;
        }

        StatItemData requiredItem = ResolveTaskRequiredItem(npc, offer);
        if (RequiresExplicitRequiredItem(offer) &&
            requiredItem == null)
        {
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
                NpcRoleUtility.SetAction(meal.npc, TaskAction("goTavernMealPoint"));

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

            switch (task.stage)
            {
                case TavernTaskStage.GoingToCounter:
                    MoveNpc(task.npc, task.counterPosition);
                    NpcRoleUtility.SetAction(
                        task.npc,
                        TaskAction("goCounterTrade"));

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
                    break;

                case TavernTaskStage.ReceivingTask:
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

                    if (Vector2.Distance(
                            task.npc.transform.position,
                            task.workPosition) <= arriveDistance)
                    {
                        task.stage = TavernTaskStage.Working;
                        task.remainingTime = Mathf.Max(1f, task.offer.workDuration);
                    }
                    break;

                case TavernTaskStage.Working:
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
                    if (!IsNpcAtHuntWorkPosition(task))
                    {
                        MoveNpc(
                            task.npc,
                            task.workPosition,
                            GetWorkZone(task.offer));
                        NpcRoleUtility.SetAction(
                            task.npc,
                            TaskActionFormat("huntSearch", BuildHuntProgressText(task)));
                        break;
                    }

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
                    break;

                case TavernTaskStage.TurningIn:
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
            if (villager != null && IsLinhRiceItem(villager.farmProduct))
            {
                return villager.farmProduct;
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
            GetGatherRequiredZone(offer)) != null;
    }

    StatItemData FindDeathLootForHuntOffer(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

        int requiredLevel = GetRequiredBeastLevel(offer);
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

            if (requiredLevel > 0 && monster.beastLevel != requiredLevel)
            {
                continue;
            }

            StatItemData loot = monster.GetDeathLoot();
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
            return;
        }

        ReserveGatherPickupForTask(task.targetPickup, task.npc);
        task.workPosition = task.targetPickup.transform.position;
        MoveNpcToWork(task, task.workPosition);
        NpcRoleUtility.SetAction(
            task.npc,
            TaskActionFormat("goGatherItem", GetTaskRequiredItemName(task), BuildGatherProgressText(task)));

        if (IsNpcAtGatherPickup(task))
        {
            task.stage = TavernTaskStage.Working;
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

            if (Vector2.Distance(
                    task.npc.transform.position,
                    greetingPosition) <= Mathf.Max(
                        arriveDistance,
                        escortFollowDistance * 0.75f))
            {
                NpcRoleUtility.StopForConversation(task.npc);
                NpcRoleUtility.StopForConversation(task.escortCompanionNpc);
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
        return GetNpcCombatPower(task.npc) <=
            GetMonsterCombatPower(threat) * Mathf.Max(0.1f, escortThreatFleePowerRatio);
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

    void WaitForHuntTargetRespawn(RunningNpcTask task)
    {
        if (task == null)
        {
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

        int requiredLevel = GetRequiredBeastLevel(offer);
        if (requiredLevel > 0 && monster.beastLevel != requiredLevel)
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
                TaskAction("fleeMonsterArea"));
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
            TaskAction("guardSpiritHerbMonster"));
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
            if (!IsWorkThreatMonster(monster))
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
            TaskAction("tooStrongChangeHarvestArea"));
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
        if (task == null)
        {
            return false;
        }

        if (GetTaskRequiredItem(task) != null)
        {
            return GetTaskGatherProgress(task) >= GetRequiredAmount(task);
        }

        return task.defeatedMonsterCount >= GetRequiredMonsterKills(task.offer);
    }

    int GetRequiredMonsterKills(NpcTaskOffer offer)
    {
        return offer != null
            ? Mathf.Max(1, offer.requiredMonsterKills)
            : 1;
    }

    string BuildHuntProgressText(RunningNpcTask task)
    {
        if (task != null && GetTaskRequiredItem(task) != null)
        {
            return "(" + GetTaskGatherProgress(task) + "/" +
                GetRequiredAmount(task) + " " + GetTaskRequiredItemName(task) + ")";
        }

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
            !IsGatherPickupUsable(task.targetPickup, GetTaskRequiredItem(task), task.npc))
        {
            return false;
        }

        StatItemData item = task.targetPickup.item;
        if (!task.targetPickup.TryTake(1))
        {
            return false;
        }

        ItemInventory inventory = GetOrCreateInventory(task.npc);
        ItemEffectSpawner.PlayPickupEffect(item, task.npc.transform);
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
            GetGatherRequiredZone(task.offer),
            task.npc);
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
        StatItemData requiredItem,
        GameObject requester = null)
    {
        return pickup != null &&
            pickup.gameObject.activeInHierarchy &&
            pickup.item != null &&
            pickup.amount > 0 &&
            pickup.allowNpcPickup &&
            !pickup.IsReservedByOther(requester) &&
            (requiredItem == null || pickup.item == requiredItem);
    }

    bool IsGatherTask(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            IsGatherTaskType(task.offer.taskType);
    }

    bool IsPatrolTask(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.Patrol;
    }

    bool IsGatherTaskType(NpcTaskType taskType)
    {
        return taskType == NpcTaskType.GatherResource ||
            taskType == NpcTaskType.HarvestAndDeliver;
    }

    NpcMapZone? GetGatherRequiredZone(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

        if (offer.taskType != NpcTaskType.GatherResource)
        {
            return null;
        }

        if (IsLinhRiceItem(offer.requiredItem))
        {
            return NpcMapZone.Lang;
        }

        return NpcMapZone.MaThuSonMach;
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

        if (IsEscortTask(task))
        {
            return task.escortConfirmed;
        }

        if (IsGatherTask(task))
        {
            return HasGatherObjectiveComplete(task);
        }

        if (IsHuntTask(task))
        {
            return HasHuntObjectiveComplete(task);
        }

        if (IsPatrolTask(task))
        {
            return task.patrolReachedEnd;
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
        if (item != null)
        {
            return ItemText.Name(item);
        }

        if (task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.HarvestAndDeliver)
        {
            return TaskDisplay("linhRice");
        }

        return NpcText.Get("taskDisplay", "spiritHerb", "linh thảo");
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
        StatItemData requiredItem = GetPlannedRequiredItem(offer);

        if (requiredItem != null)
        {
            return ItemText.Name(requiredItem);
        }

        if (offer != null &&
            offer.taskType == NpcTaskType.HarvestAndDeliver)
        {
            return TaskDisplay("linhRice");
        }

        return NpcText.Get("taskDisplay", "spiritHerb", "linh thảo");
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

        string text = TaskDisplayFormat(
            "rankedTask",
            GetRankText(task.offer.rank),
            GetOfferTaskName(task.offer));

        if (IsGatherTask(task))
        {
            text += TaskDisplayFormat(
                "itemObjective",
                GetTaskRequiredItemName(task),
                GetRequiredAmount(task));
        }
        else if (IsHuntTask(task))
        {
            if (GetTaskRequiredItem(task) != null)
            {
                text += TaskDisplayFormat(
                    "itemObjective",
                    GetTaskRequiredItemName(task),
                    GetRequiredAmount(task));
            }
            else
            {
                int requiredLevel = GetRequiredBeastLevel(task.offer);
                string levelText = requiredLevel > 0
                    ? TaskDisplayFormat("beastLevel", requiredLevel)
                    : string.Empty;

                text += TaskDisplayFormat(
                    "huntObjective",
                    TaskDisplay("beast"),
                    levelText,
                    GetRequiredMonsterKills(task.offer));
            }
        }

        int rewardSpiritStone =
            task.rewardSpiritStone > 0
            ? task.rewardSpiritStone
            : Mathf.Max(0, task.offer.rewardSpiritStone);

        if (rewardSpiritStone > 0)
        {
            text += TaskDisplayFormat("reward", rewardSpiritStone);
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
            NpcRoleUtility.SetAction(
                task.npc,
                TaskActionFormat("missingTurnInItems", GetTaskDisplayText(task)));
            task.stage = TavernTaskStage.ReturningToTurnIn;
            return;
        }

        runningTasks.RemoveAt(index);
        UnmarkNpcBusyWithProvider(task != null ? task.npc : null);
        RestoreEscortCompanionHome(task);
        ResumeEscortCompanion(task);
        if (task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.Escort)
        {
            UnlockEscortOffer(task.offer);
        }
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
        return PickOfferFor(npc, false);
    }

    NpcTaskOffer PickOfferFor(GameObject npc, bool autoAssigned)
    {
        if (npc == null ||
            offers == null ||
            offers.Length == 0)
        {
            return null;
        }

        NpcTaskOffer best = null;
        float minScore = GetMinAcceptanceScore(autoAssigned);
        float bestScore = minScore - 0.01f;

        foreach (NpcTaskOffer offer in offers)
        {
            if (!IsOfferWorldAvailable(offer))
            {
                continue;
            }

            float score = GetOfferAcceptanceScore(npc, offer, autoAssigned);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = offer;
        }

        return best;
    }

    bool CanNpcAcceptOffer(GameObject npc, NpcTaskOffer offer)
    {
        return GetOfferAcceptanceScore(npc, offer, false) >=
            GetMinAcceptanceScore(false);
    }

    float GetOfferAcceptanceScore(
        GameObject npc,
        NpcTaskOffer offer,
        bool autoAssigned)
    {
        float score = GetOfferSuitabilityScore(npc, offer);
        if (score <= 0f)
        {
            return 0f;
        }

        if (!requireNpcTaskWillingness)
        {
            return score;
        }

        if (autoAssigned &&
            autoAssignRequiresTaskIntent &&
            !HasNpcTaskIntent(npc))
        {
            return 0f;
        }

        if (ShouldDeclineTaskByState(npc, offer))
        {
            return 0f;
        }

        return Mathf.Max(0f, score + GetNpcTaskWillingnessBonus(npc, offer));
    }

    float GetMinAcceptanceScore(bool autoAssigned)
    {
        if (!requireNpcTaskWillingness)
        {
            return 0.01f;
        }

        float score = Mathf.Max(0f, minTaskWillingnessScore);
        if (autoAssigned)
        {
            score = Mathf.Max(score, minAutoAssignWillingnessScore);
        }

        return score;
    }

    bool ShouldDeclineTaskByState(GameObject npc, NpcTaskOffer offer)
    {
        if (npc == null || offer == null)
        {
            return true;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            if (rejectNonAdultVillagerTasks &&
                villager.ageGroup != VillagerAgeGroup.Adult)
            {
                return true;
            }

            if (villager.fatigue >= maxTaskAcceptFatigue)
            {
                return true;
            }

            if (villager.realm < CultivationRealm.Foundation &&
                villager.hunger >= maxTaskAcceptHunger)
            {
                return true;
            }

            if (offer.taskType == NpcTaskType.HuntMonster &&
                villager.bravery < minHuntTaskBravery)
            {
                return true;
            }

            return offer.taskType == NpcTaskType.Escort &&
                villager.bravery < minHuntTaskBravery;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            if (smartNpc.fatigue >= maxTaskAcceptFatigue)
            {
                return true;
            }

            if (smartNpc.realm < CultivationRealm.Foundation &&
                smartNpc.hunger >= maxTaskAcceptHunger)
            {
                return true;
            }

            if (offer.taskType == NpcTaskType.HuntMonster &&
                (!smartNpc.canFight || smartNpc.bravery < minHuntTaskBravery))
            {
                return true;
            }

            if (offer.taskType == NpcTaskType.Escort &&
                (!smartNpc.canFight || smartNpc.bravery < minHuntTaskBravery))
            {
                return true;
            }

            return offer.taskType == NpcTaskType.Cultivate &&
                !smartNpc.canCultivate;
        }

        return false;
    }

    float GetNpcTaskWillingnessBonus(GameObject npc, NpcTaskOffer offer)
    {
        float bonus = Mathf.Clamp(
            Mathf.Max(0, offer.rewardSpiritStone) / 1000f,
            0f,
            20f);

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            bonus += Mathf.Clamp(100f - villager.fatigue, 0f, 100f) * 0.08f;

            if (villager.realm < CultivationRealm.Foundation)
            {
                bonus += Mathf.Clamp(100f - villager.hunger, 0f, 100f) * 0.05f;
            }

            bonus += (villager.diligence - 50) * 0.2f;

            if (offer.taskType == NpcTaskType.HuntMonster ||
                offer.taskType == NpcTaskType.Patrol ||
                offer.taskType == NpcTaskType.Escort)
            {
                bonus += (villager.bravery - 50) * 0.25f;
            }
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            bonus += Mathf.Clamp(100f - smartNpc.fatigue, 0f, 100f) * 0.08f;

            if (smartNpc.realm < CultivationRealm.Foundation)
            {
                bonus += Mathf.Clamp(100f - smartNpc.hunger, 0f, 100f) * 0.05f;
            }

            if ((offer.taskType == NpcTaskType.HuntMonster ||
                offer.taskType == NpcTaskType.Escort) &&
                smartNpc.canFight)
            {
                bonus += 20f;
            }

            if (offer.taskType == NpcTaskType.Cultivate && smartNpc.canCultivate)
            {
                bonus += 20f;
            }

            if (offer.taskType == NpcTaskType.HuntMonster ||
                offer.taskType == NpcTaskType.Patrol ||
                offer.taskType == NpcTaskType.Escort)
            {
                bonus += (smartNpc.bravery - 50) * 0.25f;
            }
        }

        return bonus;
    }

    bool HasNpcTaskIntent(GameObject npc)
    {
        string action = GetNpcCurrentAction(npc);
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        return CurrentActionContains(action, "goTaskProviderDaily") ||
            CurrentActionContains(action, "goVanBaoLauTask") ||
            CurrentActionContains(action, "askProviderFindTask") ||
            CurrentActionContains(action, "returnProviderReceiveTask") ||
            CurrentActionContains(action, "receiveTask");
    }

    bool CurrentActionContains(string action, string key)
    {
        string text = TaskAction(key);
        if (!string.IsNullOrEmpty(text) &&
            action.Contains(text))
        {
            return true;
        }

        text = NpcText.Action(key);
        return !string.IsNullOrEmpty(text) &&
            action.Contains(text);
    }

    string GetNpcCurrentAction(GameObject npc)
    {
        if (npc == null)
        {
            return string.Empty;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.currentAction;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        return smartNpc != null
            ? smartNpc.currentAction
            : string.Empty;
    }

    bool MeetsHuntBeastPowerRequirement(int npcPower, NpcTaskOffer offer)
    {
        if (!requireNpcPowerAboveBeastLevel ||
            offer == null ||
            offer.taskType != NpcTaskType.HuntMonster ||
            offer.requiredHuntTargetType == HuntTargetType.Animal ||
            offer.requiredBeastLevel <= 0)
        {
            return true;
        }

        CultivationRealm beastRealm = GetRealmForBeastLevel(offer.requiredBeastLevel);
        int beastPower = CultivationProgression.GetRealmPower(beastRealm, 1);
        return npcPower >= beastPower + Mathf.Max(0, huntRequiredPowerMargin);
    }

    CultivationRealm GetRealmForBeastLevel(int beastLevel)
    {
        switch (Mathf.Max(1, beastLevel))
        {
            case 1:
                return CultivationRealm.QiRefining;
            case 2:
                return CultivationRealm.Foundation;
            case 3:
                return CultivationRealm.GoldenCore;
            default:
                return CultivationRealm.NascentSoul;
        }
    }
    float GetOfferSuitabilityScore(GameObject npc, NpcTaskOffer offer)
    {
        if (npc == null ||
            offer == null ||
            !NpcRoleUtility.MeetsRealm(npc, offer.minRealm, offer.minRealmStage))
        {
            return 0f;
        }

        if (!IsOfferWorldAvailable(offer))
        {
            return 0f;
        }

        float score = 10f;
        int npcPower = NpcRoleUtility.GetRealmPower(npc);
        int requiredPower = CultivationProgression.GetRealmPower(
            offer.minRealm,
            Mathf.Clamp(offer.minRealmStage, 1, CultivationProgression.MaxStage));

        score += Mathf.Clamp(npcPower - requiredPower, 0, 80) * 0.25f;

        if (!MeetsHuntBeastPowerRequirement(npcPower, offer))
        {
            return 0f;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            switch (villager.job)
            {
                case VillagerJob.Hunter:
                case VillagerJob.Guard:
                    if (offer.taskType == NpcTaskType.HuntMonster ||
                        offer.taskType == NpcTaskType.Patrol ||
                        offer.taskType == NpcTaskType.Escort)
                    {
                        score += 35f;
                    }
                    break;

                case VillagerJob.Farmer:
                case VillagerJob.Fisher:
                case VillagerJob.Worker:
                    if (offer.taskType == NpcTaskType.GatherResource ||
                        offer.taskType == NpcTaskType.Deliver ||
                        offer.taskType == NpcTaskType.HarvestAndDeliver)
                    {
                        score += 30f;
                    }
                    break;

                case VillagerJob.Trader:
                    if (offer.taskType == NpcTaskType.Deliver ||
                        offer.taskType == NpcTaskType.GatherResource ||
                        offer.taskType == NpcTaskType.HarvestAndDeliver)
                    {
                        score += 20f;
                    }
                    break;
            }

            if (villager.bravery < 45 &&
                (offer.taskType == NpcTaskType.HuntMonster ||
                    offer.taskType == NpcTaskType.Escort))
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

            if (offer.taskType == NpcTaskType.Escort &&
                smartNpc.canFight)
            {
                score += 25f;
            }

            if (offer.taskType == NpcTaskType.Cultivate &&
                smartNpc.canCultivate)
            {
                score += 25f;
            }
        }

        if ((offer.taskType == NpcTaskType.HuntMonster ||
            offer.taskType == NpcTaskType.Patrol ||
            offer.taskType == NpcTaskType.Escort) &&
            NpcMapArea.FindNearestAreaInZone(NpcMapZone.MaThuSonMach, transform.position) != null)
        {
            score += 10f;
        }

        return Mathf.Max(0f, score);
    }

    bool IsOfferWorldAvailable(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return false;
        }

        switch (offer.taskType)
        {
            case NpcTaskType.GatherResource:
            {
                StatItemData requiredItem = ResolveTaskRequiredItem(null, offer);
                return HasAvailableTaskPickup(offer, requiredItem);
            }

            case NpcTaskType.HuntMonster:
                return FindDeathLootForHuntOffer(offer) != null;

            case NpcTaskType.HarvestAndDeliver:
            {
                StatItemData linhRice = ResolveLinhRiceItem();
                return linhRice != null &&
                    HasAvailableTaskPickup(offer, linhRice);
            }

            case NpcTaskType.Patrol:
                return patrolPoint != null || patrolPointB != null;

            case NpcTaskType.Deliver:
                return deliverPoint != null;

            case NpcTaskType.Escort:
                return IsEscortConfigured(offer) &&
                    !IsEscortOfferLocked(offer) &&
                    HasEscortParticipantsAvailable(offer);

            case NpcTaskType.Cultivate:
                return false;

            default:
                return false;
        }
    }

    NpcTaskOffer[] ShuffleOffers()
    {
        if (offers == null ||
            offers.Length == 0)
        {
            return new NpcTaskOffer[0];
        }

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

    float GetProviderInteractionDistance()
    {
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

    bool IsNpcInProviderInteractionRange(GameObject npc)
    {
        return GetNpcProviderInteractionDistance(npc) <=
            GetProviderInteractionDistance();
    }

    public Vector3 GetProviderPositionFor(GameObject npc)
    {
        Vector3 center = GetProviderPosition();

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

    Vector3 GetClearTaskPositionNear(Vector3 position, GameObject npc)
    {
        position.z = transform.position.z;
        if (!IsTaskPositionBlocked(position, npc))
        {
            return position;
        }

        float baseRadius = Mathf.Max(arriveDistance, providerVisitorStandRadius, 0.45f);
        for (int radiusStep = 0; radiusStep < 6; radiusStep++)
        {
            float radius = baseRadius + radiusStep * 0.25f;
            for (int angleStep = 0; angleStep < 16; angleStep++)
            {
                float angle =
                    (angleStep / 16f) * Mathf.PI * 2f +
                    radiusStep * 0.31f;
                Vector3 candidate =
                    position +
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

                if (!IsTaskPositionBlocked(candidate, npc))
                {
                    return candidate;
                }
            }
        }

        return position;
    }

    bool IsTaskPositionBlocked(Vector3 position, GameObject npc)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                Mathf.Max(0.25f, arriveDistance));

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.isTrigger ||
                (npc != null && hit.transform.IsChildOf(npc.transform)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    Vector3 GetPatrolStartPosition(NpcTaskOffer offer)
    {
        if (patrolPoint != null)
        {
            return patrolPoint.position;
        }

        return GetFallbackWorkPosition();
    }

    Vector3 GetPatrolEndPosition(NpcTaskOffer offer)
    {
        if (patrolPointB != null)
        {
            return patrolPointB.position;
        }

        if (patrolPoint != null)
        {
            return patrolPoint.position;
        }

        return GetFallbackWorkPosition();
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
                        Vector3 huntTarget = huntPoint.position;
                        if (IsSafeForestWorkTarget(
                                huntTarget,
                                GetDepthMinForRank(offer.rank, huntDepthMin),
                                GetDepthMaxForRank(offer.rank, huntDepthMax)))
                        {
                            return huntTarget;
                        }

                        return GetForestWorkPosition(
                            null,
                            GetDepthMinForRank(offer.rank, huntDepthMin),
                            GetDepthMaxForRank(offer.rank, huntDepthMax));
                    }

                    return GetForestWorkPosition(
                        null,
                        GetDepthMinForRank(offer.rank, huntDepthMin),
                        GetDepthMaxForRank(offer.rank, huntDepthMax));

                case NpcTaskType.GatherResource:
                    if (IsLinhRiceItem(offer.requiredItem) &&
                        linhRiceFieldPoint != null)
                    {
                        return linhRiceFieldPoint.position;
                    }

                    if (gatherPoint != null)
                    {
                        Vector3 gatherTarget = gatherPoint.position;
                        if (IsSafeForestWorkTarget(
                                gatherTarget,
                                GetDepthMinForRank(offer.rank, gatherDepthMin),
                                GetDepthMaxForRank(offer.rank, gatherDepthMax)))
                        {
                            return gatherTarget;
                        }

                        return GetForestWorkPosition(
                            null,
                            GetDepthMinForRank(offer.rank, gatherDepthMin),
                            GetDepthMaxForRank(offer.rank, gatherDepthMax));
                    }

                    return GetForestWorkPosition(
                        null,
                        GetDepthMinForRank(offer.rank, gatherDepthMin),
                        GetDepthMaxForRank(offer.rank, gatherDepthMax));

                case NpcTaskType.HarvestAndDeliver:
                    if (linhRiceFieldPoint != null)
                    {
                        return linhRiceFieldPoint.position;
                    }

                    if (gatherPoint != null)
                    {
                        return gatherPoint.position;
                    }

                    return GetFallbackWorkPosition();

                case NpcTaskType.Patrol:
                    return GetPatrolStartPosition(offer);

                case NpcTaskType.Deliver:
                    return deliverPoint != null
                        ? deliverPoint.position
                        : GetFallbackWorkPosition();

                case NpcTaskType.Escort:
                    return GetEscortCompanionPosition(offer);
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

        if (fallbackPoint != null &&
            IsSafeForestWorkTarget(
                fallbackPoint.position,
                area,
                entry,
                deep,
                depthDirection,
                minDepth,
                maxDepth))
        {
            return fallbackPoint.position;
        }

        Bounds bounds = area.areaBounds.bounds;
        Vector3 best = GetForestDepthFallbackCandidate(
            area,
            entry,
            deep,
            depthDirection,
            minDepth,
            maxDepth);
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
            if (IsNearForestTeleportExit(candidate))
            {
                continue;
            }

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

        if (IsSafeForestWorkTarget(
                best,
                area,
                entry,
                deep,
                depthDirection,
                minDepth,
                maxDepth))
        {
            return best;
        }

        Vector3 safeFallback = GetForestDepthFallbackCandidate(
            area,
            entry,
            deep,
            depthDirection,
            minDepth,
            maxDepth);
        if (IsSafeForestWorkTarget(
                safeFallback,
                area,
                entry,
                deep,
                depthDirection,
                minDepth,
                maxDepth))
        {
            return safeFallback;
        }

        return best;
    }

    bool IsSafeForestWorkTarget(
        Vector3 candidate,
        float minDepth,
        float maxDepth)
    {
        NpcMapArea area = forestSearchArea != null
            ? forestSearchArea
            : NpcMapArea.FindNearestAreaInZone(
                NpcMapZone.MaThuSonMach,
                candidate);

        if (area == null || area.areaBounds == null)
        {
            return false;
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

        return IsSafeForestWorkTarget(
            candidate,
            area,
            entry,
            deep,
            depthDirection,
            Mathf.Clamp01(minDepth),
            Mathf.Clamp(maxDepth, Mathf.Clamp01(minDepth), 1f));
    }

    bool IsSafeForestWorkTarget(
        Vector3 candidate,
        NpcMapArea area,
        Vector3 entry,
        Vector3 deep,
        Vector2 depthDirection,
        float minDepth,
        float maxDepth)
    {
        if (area == null ||
            area.areaBounds == null)
        {
            return false;
        }

        Vector2 closest = area.areaBounds.ClosestPoint(candidate);
        if (Vector2.Distance(closest, candidate) > 0.02f)
        {
            return false;
        }

        float depth = GetDepth01(candidate, entry, deep, depthDirection);
        if (depth < minDepth ||
            depth > maxDepth)
        {
            return false;
        }

        return !IsNearForestTeleportExit(candidate);
    }

    Vector3 GetForestDepthFallbackCandidate(
        NpcMapArea area,
        Vector3 entry,
        Vector3 deep,
        Vector2 depthDirection,
        float minDepth,
        float maxDepth)
    {
        if (area == null ||
            area.areaBounds == null)
        {
            return transform.position;
        }

        float depth = Mathf.Clamp01((minDepth + maxDepth) * 0.5f);
        Vector3 candidate = Vector3.Lerp(entry, deep, depth);
        candidate = area.areaBounds.ClosestPoint(candidate);

        if (!IsNearForestTeleportExit(candidate))
        {
            return candidate;
        }

        Vector3 deeperCandidate = Vector3.Lerp(entry, deep, Mathf.Clamp01(maxDepth));
        deeperCandidate = area.areaBounds.ClosestPoint(deeperCandidate);
        if (!IsNearForestTeleportExit(deeperCandidate))
        {
            return deeperCandidate;
        }

        Vector3 centerCandidate = area.areaBounds.bounds.center;
        centerCandidate = area.areaBounds.ClosestPoint(centerCandidate);
        return centerCandidate;
    }

    bool IsNearForestTeleportExit(Vector3 candidate)
    {
        float buffer = Mathf.Max(0.1f, forestTeleportExitBuffer);

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                gate.toZone != NpcMapZone.MaThuSonMach)
            {
                continue;
            }

            if (Vector2.Distance(candidate, gate.ExitPosition) <= buffer)
            {
                return true;
            }
        }

        return false;
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

    bool IsEscortTask(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.Escort;
    }

    bool IsEscortConfigured(NpcTaskOffer offer)
    {
        return offer != null &&
            offer.taskType == NpcTaskType.Escort &&
            escortMeetPoint != null &&
            escortCompletionPoint != null;
    }

    Vector3 GetEscortCompanionPosition(NpcTaskOffer offer)
    {
        if (escortAnchorPositionsCaptured)
        {
            return escortMeetAnchorPosition;
        }

        if (escortMeetPoint != null)
        {
            return escortMeetPoint.position;
        }

        return GetFallbackWorkPosition();
    }

    Vector3 GetEscortGreetingPosition(RunningNpcTask task)
    {
        if (task == null)
        {
            return Vector3.zero;
        }

        Vector3 companionPosition = GetEscortCompanionPosition(task.offer);
        Vector3 leaderPosition = task.npc != null
            ? task.npc.transform.position
            : companionPosition + Vector3.left;
        Vector2 awayFromCompanion = (Vector2)(leaderPosition - companionPosition);
        if (awayFromCompanion.sqrMagnitude < 0.01f)
        {
            awayFromCompanion = Random.insideUnitCircle.normalized;
        }
        else
        {
            awayFromCompanion.Normalize();
        }

        float greetingOffset = Mathf.Max(0.65f, escortFollowDistance * 0.75f);
        return companionPosition + (Vector3)(awayFromCompanion * greetingOffset);
    }

    Vector3 GetEscortCompletionPosition(NpcTaskOffer offer)
    {
        if (escortAnchorPositionsCaptured)
        {
            return escortCompletionAnchorPosition;
        }

        if (escortCompletionPoint != null)
        {
            return escortCompletionPoint.position;
        }

        return GetProviderPosition();
    }

    Vector3 GetEscortCompletionGreetingPosition(RunningNpcTask task)
    {
        if (task == null)
        {
            return Vector3.zero;
        }

        Vector3 completionPosition = GetEscortCompletionPosition(task.offer);
        Vector3 leaderPosition = task.npc != null
            ? task.npc.transform.position
            : completionPosition + Vector3.left;
        Vector2 awayFromCompletion = (Vector2)(leaderPosition - completionPosition);
        if (awayFromCompletion.sqrMagnitude < 0.01f)
        {
            awayFromCompletion = Random.insideUnitCircle.normalized;
        }
        else
        {
            awayFromCompletion.Normalize();
        }

        float greetingOffset = Mathf.Max(0.65f, escortFollowDistance * 0.75f);
        return completionPosition + (Vector3)(awayFromCompletion * greetingOffset);
    }

    void ShowEscortDialogueLine(GameObject npc, string line, float duration = 2.4f)
    {
        if (npc == null || string.IsNullOrEmpty(line))
        {
            return;
        }

        NpcOverheadDialogueUI overhead = npc.GetComponent<NpcOverheadDialogueUI>();
        if (overhead == null)
        {
            overhead = npc.AddComponent<NpcOverheadDialogueUI>();
        }

        overhead.ShowLine(line, duration, 2);
    }

    bool UpdateEscortGreetingDialogue(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.escortCompanionNpc == null)
        {
            return false;
        }

        task.remainingTime -= Time.deltaTime;

        if (task.escortGreetingConversationStep == 0)
        {
            NpcRoleUtility.StopForConversation(task.npc);
            NpcRoleUtility.StopForConversation(task.escortCompanionNpc);
            ShowEscortDialogueLine(
                task.npc,
                "Tại hạ phụng mệnh hộ tống đạo hữu trong chuyến này.");
            NpcRoleUtility.SetAction(task.npc, NpcText.Action("talking"));
            NpcRoleUtility.SetAction(task.escortCompanionNpc, NpcText.Action("talking"));
            task.remainingTime = Mathf.Max(1.1f, escortGreetingDuration * 0.45f);
            task.escortGreetingConversationStep = 1;
            return true;
        }

        if (task.escortGreetingConversationStep == 1)
        {
            if (task.remainingTime > 0f)
            {
                return true;
            }

            ShowEscortDialogueLine(
                task.escortCompanionNpc,
                "Làm phiền đạo hữu, xin hộ tống ta một đoạn.");
            NpcRoleUtility.SetAction(task.npc, NpcText.Action("talking"));
            NpcRoleUtility.SetAction(task.escortCompanionNpc, NpcText.Action("talking"));
            task.remainingTime = Mathf.Max(1.1f, escortGreetingDuration * 0.45f);
            task.escortGreetingConversationStep = 2;
            return true;
        }

        if (task.escortGreetingConversationStep == 2)
        {
            if (task.remainingTime > 0f)
            {
                return true;
            }

            task.escortGreetingConversationStep = 3;
            return false;
        }

        return task.escortGreetingConversationStep < 3;
    }

    bool UpdateEscortDeliveryDialogue(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.escortCompanionNpc == null ||
            task.escortCompletionNpc == null)
        {
            return false;
        }

        task.remainingTime -= Time.deltaTime;

        if (task.escortDeliveryConversationStep == 0)
        {
            NpcRoleUtility.StopForConversation(task.npc);
            NpcRoleUtility.StopForConversation(task.escortCompanionNpc);
            NpcRoleUtility.StopForConversation(task.escortCompletionNpc);
            ShowEscortDialogueLine(
                task.escortCompanionNpc,
                "Đây là linh vật / linh tài mà đạo hữu đã dặn mang tới.");
            NpcRoleUtility.SetAction(task.npc, NpcText.Action("talking"));
            NpcRoleUtility.SetAction(task.escortCompanionNpc, NpcText.Action("talking"));
            NpcRoleUtility.SetAction(task.escortCompletionNpc, NpcText.Action("talking"));
            task.remainingTime = Mathf.Max(1.1f, escortGreetingDuration * 0.45f);
            task.escortDeliveryConversationStep = 1;
            return true;
        }

        if (task.escortDeliveryConversationStep == 1)
        {
            if (task.remainingTime > 0f)
            {
                return true;
            }

            ShowEscortDialogueLine(
                task.escortCompletionNpc,
                "Đa tạ, hữu lễ.");
            NpcRoleUtility.SetAction(task.npc, NpcText.Action("talking"));
            NpcRoleUtility.SetAction(task.escortCompanionNpc, NpcText.Action("talking"));
            NpcRoleUtility.SetAction(task.escortCompletionNpc, NpcText.Action("talking"));
            task.remainingTime = Mathf.Max(1.1f, escortGreetingDuration * 0.45f);
            task.escortDeliveryConversationStep = 2;
            return true;
        }

        if (task.escortDeliveryConversationStep == 2)
        {
            if (task.remainingTime > 0f)
            {
                return true;
            }

            task.escortDeliveryConversationStep = 3;
            return false;
        }

        return task.escortDeliveryConversationStep < 3;
    }

    void CaptureEscortAnchorPositions()
    {
        if (escortAnchorPositionsCaptured)
        {
            return;
        }

        escortMeetAnchorPosition = escortMeetPoint != null
            ? escortMeetPoint.position
            : transform.position;
        escortCompletionAnchorPosition = escortCompletionPoint != null
            ? escortCompletionPoint.position
            : transform.position;
        escortAnchorPositionsCaptured = true;
    }

    void FreezeEscortAnchors()
    {
        FreezeEscortAnchor(escortMeetPoint != null ? escortMeetPoint.gameObject : null);

        if (escortCompletionPoint != null &&
            (escortMeetPoint == null ||
                escortCompletionPoint.gameObject != escortMeetPoint.gameObject))
        {
            FreezeEscortAnchor(escortCompletionPoint.gameObject);
        }
    }

    void FreezeEscortAnchor(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        NpcRoleUtility.StopForConversation(npc);

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.enabled = false;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.enabled = false;
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.enabled = false;
        }

        Rigidbody2D rb = npc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        NpcRoleUtility.SetAction(npc, TaskAction("pausedTask"));
    }

    GameObject FindEscortNpcAtPoint(Transform point, GameObject exclude = null)
    {
        if (point == null)
        {
            return null;
        }

        float searchRadius = Mathf.Max(0.5f, escortNpcSearchRadius);
        Vector3 pointPosition = point.position;
        GameObject best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude))
        {
            if (villager == null ||
                villager.gameObject == exclude ||
                NpcRoleUtility.IsDead(villager.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(pointPosition, villager.transform.position);
            if (distance <= searchRadius && distance < bestDistance)
            {
                bestDistance = distance;
                best = villager.gameObject;
            }
        }

        foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude))
        {
            if (smartNpc == null ||
                smartNpc.gameObject == exclude ||
                NpcRoleUtility.IsDead(smartNpc.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(pointPosition, smartNpc.transform.position);
            if (distance <= searchRadius && distance < bestDistance)
            {
                bestDistance = distance;
                best = smartNpc.gameObject;
            }
        }

        return best;
    }

    GameObject FindEscortNpcAtPoint(Vector3 pointPosition, GameObject exclude = null)
    {
        float searchRadius = Mathf.Max(0.5f, escortNpcSearchRadius);
        GameObject best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude))
        {
            if (villager == null ||
                villager.gameObject == exclude ||
                NpcRoleUtility.IsDead(villager.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(pointPosition, villager.transform.position);
            if (distance <= searchRadius && distance < bestDistance)
            {
                bestDistance = distance;
                best = villager.gameObject;
            }
        }

        foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude))
        {
            if (smartNpc == null ||
                smartNpc.gameObject == exclude ||
                NpcRoleUtility.IsDead(smartNpc.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(pointPosition, smartNpc.transform.position);
            if (distance <= searchRadius && distance < bestDistance)
            {
                bestDistance = distance;
                best = smartNpc.gameObject;
            }
        }

        return best;
    }

    bool HasEscortParticipantsAvailable(NpcTaskOffer offer)
    {
        return FindEscortNpcAtPoint(GetEscortCompanionPosition(offer)) != null &&
            FindEscortNpcAtPoint(GetEscortCompletionPosition(offer)) != null;
    }

    Vector3 GetEscortFollowPosition(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null)
        {
            return Vector3.zero;
        }

        Vector3 leader = task.npc.transform.position;
        Vector3 destination = GetEscortCompletionPosition(task.offer);
        Vector2 awayFromDestination = (Vector2)(leader - destination);
        if (awayFromDestination.sqrMagnitude < 0.01f)
        {
            awayFromDestination = Random.insideUnitCircle.normalized;
        }
        else
        {
            awayFromDestination.Normalize();
        }

        return leader +
            (Vector3)(awayFromDestination *
                Mathf.Max(0.5f, escortFollowDistance));
    }

    void PrepareEscortTask(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null)
        {
            return;
        }

        task.escortConfirmed = false;
        task.escortGreetingConversationStarted = false;
        task.escortDeliveryConversationStarted = false;
        task.escortGreetingConversationStep = 0;
        task.escortDeliveryConversationStep = 0;
        task.escortDepartedFromCompanion = false;
        task.escortCompanionNpc = FindEscortNpcAtPoint(escortMeetPoint, task.npc);
        task.escortCompletionNpc = FindEscortNpcAtPoint(escortCompletionPoint, task.npc);
        task.escortCompanionHomePosition = GetEscortCompanionPosition(task.offer);
        task.workPosition = GetEscortCompanionPosition(task.offer);
        task.escortAvoidUntilTime = 0f;
        task.escortThreatMonster = null;

        if (task.escortCompanionNpc == null ||
            task.escortCompletionNpc == null)
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            task.remainingTime = 0f;
            return;
        }

        PauseBaseAi(
            task.escortCompanionNpc,
            out task.escortPausedCompanionBaseAi,
            out task.escortPausedCompanionBaseAiWasEnabled);

        if (task.escortCompanionNpc != null)
        {
            NpcRoleUtility.StopForConversation(task.escortCompanionNpc);
            NpcRoleUtility.SetAction(
                task.escortCompanionNpc,
                TaskAction("followTaskRoute"));
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

    Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    void OnNpcMapTeleported()
    {
        OnNpcMapTeleported(null);
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

        if (gate != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
            area = NpcMapNavigator.ResolveMapAreaAfterTeleport(
                gameObject,
                gate.toZone,
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






