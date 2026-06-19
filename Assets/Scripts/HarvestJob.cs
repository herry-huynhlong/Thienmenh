using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class HarvestJob : MonoBehaviour
{
    [Header("Harvest")]
    public bool enabledHarvestJob = true;
    public StatItemData farmProduct;
    public StatItemData fishingProduct;
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

    public bool IsWaitingForRetry => waitingForRetry;
    public int RetrySecondsRemaining =>
        Mathf.CeilToInt(Mathf.Max(0f, retryTimer));

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
        gatherer = GetComponent<NpcResourceGatherer>();
        inventory = GetComponent<ItemInventory>();
#if UNITY_EDITOR
        AssignDefaultProducts();
#endif
    }

    void Update()
    {
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
                    "Assets/Item/NPCitem/Ca.asset");
        }

        if (huntingProduct == null)
        {
            huntingProduct =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/NPCitem/Thit.asset");
        }
    }
#endif

    public bool TryRun()
    {
        if (!IsAllowedJob())
        {
            return false;
        }

        RefreshReferences();
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
            SetActionStable(NpcText.Action("returnStorage"));
            return true;
        }

        StatItemData targetItem = ResolveTargetItem();
        if (targetItem == null)
        {
            currentState = NpcJobState.Idle;
            BeginWaitingCycle();
            return true;
        }

        if (TryStartHarvestNow(targetItem))
        {
            currentState = NpcJobState.Working;
            return true;
        }

        if (inventory != null &&
            VillageStorage.Instance != null &&
            inventory.items.Count > 0)
        {
            VillageStorage.Instance.Store(inventory);
            currentState = NpcJobState.Returning;
            waitingForRetry = false;
            SetActionStable(NpcText.Action("returnStorage"));
            return true;
        }

        currentState = NpcJobState.Moving;
        waitingForRetry = false;
        retryTimer = 0f;
        lastRetrySeconds = -1;
        return false;
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
                    villager.fishingProduct);

            case VillagerJob.Hunter:
                return null;

            default:
                return preferredResourceKind == HarvestResourceKind.Unknown
                    ? farmProduct
                    : FindItemByPreferredKind();
        }
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
        retryTimer = Mathf.Max(0.5f, retryHarvestDelaySeconds);
        lastRetrySeconds = -1;
        RefreshWaitingAction();
    }

    void RefreshWaitingAction()
    {
        string itemName = GetTargetItemName();

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, retryTimer));
        if (seconds == lastRetrySeconds)
        {
            return;
        }

        lastRetrySeconds = seconds;
        SetActionStable(GetWaitingVerb() + itemName + " (" + seconds + "s)");
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

    void SetActionStable(string action)
    {
        if (villager == null || string.IsNullOrEmpty(action))
        {
            return;
        }

        if (villager.currentAction == action)
        {
            return;
        }

        NpcRoleUtility.SetAction(gameObject, action);
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
            BeginWaitingCycle();
            return false;
        }

        currentState = NpcJobState.Working;
        waitingForRetry = false;
        retryTimer = 0f;
        lastRetrySeconds = -1;
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
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

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
