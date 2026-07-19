using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class HarvestJob : MonoBehaviour
{
    [Header("Harvest")]
    public bool enabledHarvestJob = true;
    public StatItemData farmProduct;
    public StatItemData fishingProduct;
    // Legacy migration slot for older scenes; HunterJob owns runtime hunting now.
    [HideInInspector]
    public StatItemData huntingProduct;
    public HarvestResourceKind preferredResourceKind = HarvestResourceKind.Unknown;
    public float storageSearchRadius = 3f;
    public float storageDropDistance = 1.2f;
    public NpcJobState currentState = NpcJobState.Idle;
    public bool limitScheduledHarvestOncePerDay;
    public float retryHarvestDelaySeconds = 2f;

    VillagerAI villager;
    NpcResourceGatherer gatherer;
    ItemInventory inventory;
    float retryTimer;
    int lastRetrySeconds = -1;
    bool waitingForRetry;
    string waitingAction;
    float waitingStandbyRadius = 1.4f;

    public bool IsWaitingForRetry => waitingForRetry;
    public int RetrySecondsRemaining =>
        Mathf.CeilToInt(Mathf.Max(0f, retryTimer));

    void LogHarvestDebug(string stage, string detail, float intervalSeconds = 0.5f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (villager == null || !villager.debugWorkLogs)
        {
            return;
        }

        Debug.LogWarning(
            "[HarvestJob] " +
            stage +
            " -> " +
            gameObject.name +
            " action=" +
            (villager != null ? villager.currentAction : "null") +
            " job=" +
            (villager != null ? villager.job.ToString() : "None") +
            " state=" +
            currentState +
            " waiting=" +
            waitingForRetry +
            " detail=" +
            detail +
            " hour=" +
            (WorldTimeSystem.Instance != null
                ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                : "null"));
#endif
    }

    public void CancelHarvestNow()
    {
        waitingForRetry = false;
        retryTimer = 0f;
        lastRetrySeconds = -1;
        waitingAction = string.Empty;
        waitingStandbyRadius = 1.4f;
        currentState = NpcJobState.Idle;
    }

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
        gatherer = GetComponent<NpcResourceGatherer>();
        inventory = GetComponent<ItemInventory>();
        SyncConfiguredProductsFromVillager();
#if UNITY_EDITOR
        AssignDefaultProducts();
#endif
    }

    void Update()
    {
        if (ShouldAbortHarvestForHome())
        {
            villager.GoHomeToRest();
            return;
        }

        if (!IsAllowedJob())
        {
            return;
        }

        if (!waitingForRetry)
        {
            return;
        }

        retryTimer -= Time.deltaTime;
        if (retryTimer > 0f)
        {
            RefreshWaitingAction();
            return;
        }

        waitingForRetry = false;
        lastRetrySeconds = -1;
        TryRun();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        AssignDefaultProducts();
    }

    void AssignDefaultProducts()
    {
        if (farmProduct == null)
        {
            farmProduct =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/ThucPham/Linh_Me.asset");
        }

        if (fishingProduct == null)
        {
            fishingProduct =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/ThucPham/ca.asset");
        }

        if (huntingProduct == null)
        {
            huntingProduct =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/ThucPham/thit.asset");
        }
    }
#endif

    public bool TryRun()
    {
        if (ShouldAbortHarvestForHome())
        {
            villager.GoHomeToRest();
            return true;
        }

        if (!IsAllowedJob())
        {
            return false;
        }

        RefreshReferences();
        SyncConfiguredProductsFromVillager();
        if (gatherer != null)
        {
            gatherer.useVillagerPreferredZone = true;
        }

        if (waitingForRetry && retryTimer > 0f)
        {
            RefreshWaitingAction();
            return true;
        }

        if (TryStoreToVillageStorage())
        {
            currentState = NpcJobState.Returning;
            waitingForRetry = false;
            waitingAction = string.Empty;
            SetActionStable(NpcText.Action("returnStorage"));
            return true;
        }

        StatItemData targetItem = ResolveTargetItem();
        if (targetItem == null)
        {
            currentState = NpcJobState.Idle;
            waitingForRetry = false;
            retryTimer = 0f;
            lastRetrySeconds = -1;
            waitingAction = string.Empty;
            return false;
        }

        if (TryStartHarvestNow(targetItem))
        {
            currentState = NpcJobState.Working;
            LogHarvestDebug(
                "Start",
                "item=" + ItemText.Name(targetItem));
            return true;
        }

        if (inventory != null &&
            VillageStorage.Instance != null &&
            inventory.items.Count > 0)
        {
            VillageStorage.Instance.Store(inventory);
            currentState = NpcJobState.Returning;
            waitingForRetry = false;
            waitingAction = string.Empty;
            SetActionStable(NpcText.Action("returnStorage"));
            return true;
        }

        currentState = NpcJobState.Moving;
        waitingForRetry = false;
        retryTimer = 0f;
        lastRetrySeconds = -1;
        waitingAction = string.Empty;
        LogHarvestDebug(
            "NoTarget",
            "item=" + ItemText.Name(targetItem));
        return false;
    }

    bool ShouldAbortHarvestForHome()
    {
        if (villager == null)
        {
            return false;
        }

        if (villager.IsReturningHome)
        {
            return false;
        }

        if (villager.ShouldGoHomeForRest())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[HarvestJob] Abort for home (villager rule) -> " +
                gameObject.name +
                " action=" + villager.currentAction +
                " job=" + villager.job +
                " state=" + currentState +
                " waiting=" + waitingForRetry +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
#endif
            return true;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == NpcScheduleActivity.ReturnHome)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[HarvestJob] Abort for home (schedule ReturnHome) -> " +
                gameObject.name +
                " action=" + villager.currentAction +
                " job=" + villager.job +
                " state=" + currentState +
                " waiting=" + waitingForRetry +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
#endif
            return true;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        bool shouldAbort =
            timeSystem != null &&
            (timeSystem.CurrentPhase == WorldTimePhase.Noon ||
            (timeSystem.CurrentHour >= 11f &&
                timeSystem.CurrentHour < 13f) ||
            timeSystem.CurrentPhase == WorldTimePhase.Night);

        if (shouldAbort)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[HarvestJob] Abort for home (time window) -> " +
                gameObject.name +
                " phase=" + (timeSystem != null ? timeSystem.CurrentPhase.ToString() : "null") +
                " hour=" + (timeSystem != null ? timeSystem.CurrentHour.ToString("0.##") : "null") +
                " action=" + villager.currentAction +
                " state=" + currentState +
                " waiting=" + waitingForRetry);
#endif
        }

        return shouldAbort;
    }

    public StatItemData ResolveTargetItem()
    {
        if (villager == null)
        {
            return null;
        }

        switch (villager.job)
        {
            case VillagerJob.Farmer:
                return ResolveProductForKind(
                    HarvestResourceKind.LinhMe,
                    farmProduct,
                    null);

            case VillagerJob.Fisher:
                return ResolveProductForKind(
                    HarvestResourceKind.Ca,
                    fishingProduct,
                    null);

            case VillagerJob.Hunter:
                return null;

            default:
                return preferredResourceKind == HarvestResourceKind.Unknown
                    ? farmProduct
                    : FindItemByPreferredKind();
        }
    }

    public bool IsProducedItem(StatItemData item)
    {
        return ItemsMatch(item, farmProduct) ||
            ItemsMatch(item, fishingProduct);
    }

    StatItemData ResolveProductForKind(
        HarvestResourceKind kind,
        StatItemData primary,
        StatItemData secondary)
    {
        // Item gán trực tiếp trong Inspector là nguồn đúng nhất.
        // Không cần suy luận lại theo tên/kind, vì suy luận tên dễ nhầm
        // và có thể làm Fisher đi câu item khác như Kim Cang Diệp.
        if (primary != null)
        {
            return primary;
        }

        if (secondary != null)
        {
            return secondary;
        }

        // Chỉ auto tìm theo kind khi chưa gán item cụ thể.
        return FindItemByKind(kind);
    }

    bool IsItemKind(StatItemData item, HarvestResourceKind kind)
    {
        return item != null &&
            ResourceNode.InferKindFromItem(item) == kind;
    }

    bool ItemsMatch(StatItemData a, StatItemData b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        return !string.IsNullOrEmpty(a.ItemId) &&
            !string.IsNullOrEmpty(b.ItemId) &&
            a.ItemId == b.ItemId;
    }

    bool IsAllowedJob()
    {
        return enabledHarvestJob &&
            villager != null &&
            (villager.job == VillagerJob.Farmer ||
            villager.job == VillagerJob.Fisher);
    }

    void BeginWaitingCycle()
    {
        waitingForRetry = true;
        retryTimer = ResolveWaitingRetryDuration();
        lastRetrySeconds = -1;
        waitingAction = BuildWaitingAction();
        waitingStandbyRadius = ResolveWaitingStandbyRadius();
        RefreshWaitingAction();
    }

    void RefreshWaitingAction()
    {
        if (villager == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(waitingAction))
        {
            waitingAction = BuildWaitingAction();
        }

        if (villager.TryRunWorkStandby(
                waitingAction,
                waitingStandbyRadius))
        {
            return;
        }

        SetActionStable(waitingAction);
    }

    string GetWaitingVerb()
    {
        if (villager == null)
        {
            return "Đang chờ hái ";
        }

        switch (villager.job)
        {
            case VillagerJob.Farmer:
                return "Đang chờ thu hoạch ";
            case VillagerJob.Fisher:
                return "Đang chờ câu ";
            case VillagerJob.Hunter:
                return "Đang chờ thu thịt ";
            default:
                return "Đang chờ hái ";
        }
    }

    float ResolveWaitingRetryDuration()
    {
        float baseDelay = Mathf.Max(0.5f, retryHarvestDelaySeconds);

        switch (villager != null ? villager.job : VillagerJob.None)
        {
            case VillagerJob.Farmer:
                return Random.Range(
                    Mathf.Max(2.5f, baseDelay * 2.5f),
                    Mathf.Max(6f, baseDelay * 5f));

            case VillagerJob.Fisher:
                return Random.Range(
                    Mathf.Max(2f, baseDelay * 2f),
                    Mathf.Max(4.5f, baseDelay * 4f));

            default:
                return Random.Range(
                    baseDelay,
                    Mathf.Max(baseDelay + 0.5f, baseDelay * 2f));
        }
    }

    float ResolveWaitingStandbyRadius()
    {
        switch (villager != null ? villager.job : VillagerJob.None)
        {
            case VillagerJob.Farmer:
                return 1.35f;

            case VillagerJob.Fisher:
                return 1.8f;

            case VillagerJob.Hunter:
                return 2.2f;

            default:
                return 1.4f;
        }
    }

    string BuildWaitingAction()
    {
        string itemName = GetTargetItemName();
        int variant = Random.Range(0, 4);

        switch (villager != null ? villager.job : VillagerJob.None)
        {
            case VillagerJob.Farmer:
                switch (variant)
                {
                    case 0:
                        return "Cham soc " + itemName;
                    case 1:
                        return "Don co quanh " + itemName;
                    case 2:
                        return "Kiem tra luong " + itemName;
                    default:
                        return "Xoi dat quanh " + itemName;
                }

            case VillagerJob.Fisher:
                switch (variant)
                {
                    case 0:
                        return "Kiem tra be ca";
                    case 1:
                        return "Sua luoi ca";
                    case 2:
                        return "Don ben nuoc";
                    default:
                        return "Canh diem cau";
                }

            case VillagerJob.Hunter:
                switch (variant)
                {
                    case 0:
                        return "Kiem tra duong san";
                    case 1:
                        return "Lan dau vet thu";
                    case 2:
                        return "Canh bai san";
                    default:
                        return "Quan sat dau vet quai";
                }

            default:
                return "Chuan bi thu hoach " + itemName;
        }
    }

    void SetActionStable(string action)
    {
        if (villager == null || string.IsNullOrEmpty(action))
        {
            return;
        }

        villager.SetActionImmediate(action);
    }

    bool TryStartHarvestNow(StatItemData targetItem)
    {
        if (gatherer == null ||
            targetItem == null ||
            !gatherer.enabled ||
            !gatherer.canGather)
        {
            return false;
        }

        if (!gatherer.TryStartScheduledHarvestItemNow(targetItem))
        {
            LogHarvestDebug(
                "GatherFail",
                "item=" + ItemText.Name(targetItem));
            BeginWaitingCycle();
            return false;
        }

        currentState = NpcJobState.Working;
        waitingForRetry = false;
        retryTimer = 0f;
        lastRetrySeconds = -1;
        waitingAction = string.Empty;
        LogHarvestDebug(
            "GatherOk",
            "item=" + ItemText.Name(targetItem));
        return true;
    }

    string GetTargetItemName()
    {
        StatItemData targetItem = ResolveTargetItem();
        if (targetItem != null)
        {
            return ItemText.Name(targetItem);
        }

        switch (villager != null ? villager.job : VillagerJob.None)
        {
            case VillagerJob.Fisher:
                return "cá";
            case VillagerJob.Hunter:
                return "thịt";
            case VillagerJob.Farmer:
                return "linh mễ";
            default:
                return "tài nguyên";
        }
    }

    bool TryStoreToVillageStorage()
    {
        if (inventory == null ||
            inventory.items.Count == 0)
        {
            return false;
        }

        VillageStorage storage =
            VillageStorage.Instance != null
            ? VillageStorage.Instance
            : VillageStorage.FindNearest(transform.position);

        if (storage == null)
        {
            return false;
        }

        float distance =
            Vector2.Distance(transform.position, storage.transform.position);

        if (distance > storageSearchRadius)
        {
            return false;
        }

        return storage.Store(inventory);
    }

    void RefreshReferences()
    {
        NpcItemCollector collector = GetComponent<NpcItemCollector>();
        if (collector == null)
        {
            collector = gameObject.AddComponent<NpcItemCollector>();
        }

        collector.canPickupItems = true;

        if (gatherer == null)
        {
            gatherer = GetComponent<NpcResourceGatherer>();
        }

        if (gatherer == null)
        {
            gatherer = gameObject.AddComponent<NpcResourceGatherer>();
        }

        gatherer.canGather = true;
        gatherer.useVillagerPreferredZone = true;

        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }
    }

    void SyncConfiguredProductsFromVillager()
    {
        if (villager == null)
        {
            return;
        }

        if (fishingProduct == null)
        {
            fishingProduct = villager.fishingProduct;
        }

        if (huntingProduct == null)
        {
            huntingProduct = villager.huntingProduct;
        }
    }

    StatItemData FindItemByPreferredKind()
    {
        return FindItemByKind(preferredResourceKind);
    }

    StatItemData FindItemByKind(HarvestResourceKind kind)
    {
        if (kind == HarvestResourceKind.Unknown)
        {
            return null;
        }

        WorldStatItemPickup[] pickups =
            FindObjectsByType<WorldStatItemPickup>(
                FindObjectsInactive.Exclude);

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (pickup == null ||
                pickup.item == null ||
                ResourceNode.InferKindFromItem(pickup.item) != kind)
            {
                continue;
            }

            return pickup.item;
        }

        return null;
    }
}
