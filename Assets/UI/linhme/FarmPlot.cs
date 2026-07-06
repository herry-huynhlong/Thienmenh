using UnityEngine;
using UnityEngine.Tilemaps;

public enum FarmPlotState
{
    Empty,
    Growing,
    Mature,
    Cooldown
}

public enum FarmPlotReservationKind
{
    None,
    Plant,
    Harvest
}

public class FarmPlot : MonoBehaviour
{
    const string CropVisualChildName = "CropVisual";

    [Header("Alignment")]
    public Tilemap alignmentTilemap;
    public Grid alignmentGrid;
    public Vector3Int cell;
    public bool snapToCellCenter = true;
    public Vector3 cropLocalOffset = new Vector3(0f, 0.18f, 0f);

    [Header("Crop Visual")]
    public SpriteRenderer cropRenderer;
    public Sprite riceSeedSprite;
    public Sprite riceSmallSprite;
    public Sprite riceGrowingSprite;
    public Sprite riceMatureSprite;
    public int cropSortingOrder = 6;
    public bool hideCropRendererWhenInactive = true;

    [Header("Growth")]
    [Min(0.5f)] public float growDurationGameHours = 24f;
    [Range(0f, 1f)] public float smallStageThreshold = 0.3f;
    [Range(0f, 1f)] public float growingStageThreshold = 0.65f;
    [Min(1)] public int daysBeforeReplant = 1;

    [Header("Harvest Reward")]
    public StatItemData harvestItem;
    [Min(1)] public int minHarvestYield = 1;
    [Min(1)] public int maxHarvestYield = 2;

    [Header("Runtime")]
    public FarmPlotState state = FarmPlotState.Empty;
    public float plantedWorldHour = -1f;
    public int nextPlantAllowedDay = 1;

    [Header("Debug")]
    public bool debugLogs;

    GameObject reservedBy;
    float reservationExpiresAt;
    FarmPlotReservationKind reservationKind;

    public Vector3Int Cell => cell;
    public FarmPlotReservationKind ReservationKind => reservationKind;
    public GameObject ReservedBy => reservedBy;

    public bool CanPlantNow =>
        state == FarmPlotState.Empty &&
        GetCurrentAbsoluteDay() >= nextPlantAllowedDay;

    public bool CanHarvestNow =>
        state == FarmPlotState.Mature;

    void Awake()
    {
        EnsureCropRenderer();
        RefreshAlignment();
        RefreshVisual();
    }

    void OnEnable()
    {
        EnsureCropRenderer();
        RefreshAlignment();
        RefreshVisual();
    }

    void Update()
    {
        ReleaseExpiredReservation();
        UpdateStateFromWorldTime();
        RefreshVisual();
    }

    void OnValidate()
    {
        growDurationGameHours = Mathf.Max(0.5f, growDurationGameHours);
        daysBeforeReplant = Mathf.Max(1, daysBeforeReplant);
        minHarvestYield = Mathf.Max(1, minHarvestYield);
        maxHarvestYield = Mathf.Max(minHarvestYield, maxHarvestYield);
        growingStageThreshold =
            Mathf.Max(smallStageThreshold, growingStageThreshold);

#if UNITY_EDITOR
        AssignDefaultSpritesIfMissing();
#endif

        EnsureCropRenderer();
        RefreshAlignment();
        RefreshVisual();
    }

    public void ApplyManagerDefaults(
        Tilemap tilemap,
        Vector3Int tileCell,
        Sprite seedSprite,
        Sprite smallSprite,
        Sprite growingSprite,
        Sprite matureSprite,
        float growHours,
        int replantDays,
        Vector3 visualOffset,
        int sortingOrder)
    {
        alignmentTilemap = tilemap;
        alignmentGrid =
            tilemap != null
                ? tilemap.layoutGrid
                : alignmentGrid;
        cell = tileCell;
        riceSeedSprite = seedSprite;
        riceSmallSprite = smallSprite;
        riceGrowingSprite = growingSprite;
        riceMatureSprite = matureSprite;
        growDurationGameHours = Mathf.Max(0.5f, growHours);
        daysBeforeReplant = Mathf.Max(1, replantDays);
        cropLocalOffset = visualOffset;
        cropSortingOrder = sortingOrder;

        EnsureCropRenderer();
        RefreshAlignment();
        RefreshVisual();
    }

    public bool TryReserveForPlant(
        GameObject requester,
        float durationSeconds)
    {
        if (!CanPlantNow ||
            IsReservedByOther(requester))
        {
            return false;
        }

        return SetReservation(
            requester,
            durationSeconds,
            FarmPlotReservationKind.Plant);
    }

    public bool TryReserveForHarvest(
        GameObject requester,
        float durationSeconds)
    {
        if (!CanHarvestNow ||
            IsReservedByOther(requester))
        {
            return false;
        }

        return SetReservation(
            requester,
            durationSeconds,
            FarmPlotReservationKind.Harvest);
    }

    public bool IsReservedByOther(GameObject requester)
    {
        ReleaseExpiredReservation();

        if (reservedBy == null)
        {
            return false;
        }

        return reservedBy != requester;
    }

    public void ReleaseReservation(GameObject requester)
    {
        if (reservedBy == null)
        {
            return;
        }

        if (requester != null &&
            reservedBy != requester)
        {
            return;
        }

        reservedBy = null;
        reservationExpiresAt = 0f;
        reservationKind = FarmPlotReservationKind.None;
    }

    public bool RefreshReservation(
        GameObject requester,
        float durationSeconds)
    {
        if (requester == null)
        {
            return false;
        }

        ReleaseExpiredReservation();

        if (reservedBy != requester)
        {
            return false;
        }

        reservationExpiresAt =
            Time.time + Mathf.Max(0.25f, durationSeconds);
        return true;
    }

    public bool BeginPlanting(GameObject requester = null)
    {
        if (!CanPlantNow)
        {
            return false;
        }

        if (IsReservedByOther(requester))
        {
            return false;
        }

        plantedWorldHour = GetCurrentWorldHour();
        state = FarmPlotState.Growing;
        reservationKind = FarmPlotReservationKind.None;
        reservedBy = null;
        reservationExpiresAt = 0f;
        LogDebug("BeginPlanting");
        RefreshVisual();
        return true;
    }

    public bool CompleteHarvest(GameObject requester = null)
    {
        if (!CanHarvestNow)
        {
            return false;
        }

        if (IsReservedByOther(requester))
        {
            return false;
        }

        int currentDay = GetCurrentAbsoluteDay();
        plantedWorldHour = -1f;
        nextPlantAllowedDay = currentDay + Mathf.Max(1, daysBeforeReplant);
        state = FarmPlotState.Cooldown;
        reservationKind = FarmPlotReservationKind.None;
        reservedBy = null;
        reservationExpiresAt = 0f;
        LogDebug("CompleteHarvest");
        RefreshVisual();
        return true;
    }

    public bool HarvestToInventory(
        ItemInventory inventory,
        out int harvestedAmount,
        GameObject requester = null)
    {
        harvestedAmount = 0;

        if (inventory == null ||
            harvestItem == null)
        {
            return false;
        }

        if (!CompleteHarvest(requester))
        {
            return false;
        }

        harvestedAmount = RollHarvestAmount();
        if (harvestedAmount <= 0)
        {
            return true;
        }

        GameSaveSystem.RegisterItem(harvestItem);
        inventory.AddItem(harvestItem, harvestedAmount);
        return true;
    }

    public bool HarvestToActor(
        GameObject harvester,
        out int harvestedAmount)
    {
        harvestedAmount = 0;

        if (harvester == null)
        {
            return false;
        }

        ItemInventory inventory =
            harvester.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory = harvester.AddComponent<ItemInventory>();
            inventory.shareRuntimeItems = false;
        }

        return HarvestToInventory(
            inventory,
            out harvestedAmount,
            harvester);
    }

    public void ForceSetEmpty()
    {
        plantedWorldHour = -1f;
        state = FarmPlotState.Empty;
        nextPlantAllowedDay = Mathf.Max(1, GetCurrentAbsoluteDay());
        ReleaseReservation(null);
        RefreshVisual();
    }

    [ContextMenu("Farm/Test Begin Planting")]
    void DebugBeginPlanting()
    {
        BeginPlanting();
    }

    [ContextMenu("Farm/Test Force Mature")]
    void DebugForceMature()
    {
        plantedWorldHour =
            GetCurrentWorldHour() - Mathf.Max(0.5f, growDurationGameHours);
        state = FarmPlotState.Growing;
        UpdateStateFromWorldTime();
        RefreshVisual();
    }

    public void DebugForceMatureFromManager()
    {
        DebugForceMature();
    }

    [ContextMenu("Farm/Test Complete Harvest")]
    void DebugCompleteHarvest()
    {
        if (state != FarmPlotState.Mature)
        {
            DebugForceMature();
        }

        CompleteHarvest();
    }

    public int RollHarvestAmount()
    {
        return Random.Range(
            Mathf.Max(1, minHarvestYield),
            Mathf.Max(minHarvestYield, maxHarvestYield) + 1);
    }

    public void RefreshAlignment()
    {
        if (!snapToCellCenter)
        {
            PositionCropRenderer();
            return;
        }

        if (alignmentTilemap != null)
        {
            transform.position = alignmentTilemap.GetCellCenterWorld(cell);
        }
        else if (alignmentGrid != null)
        {
            transform.position = alignmentGrid.GetCellCenterWorld(cell);
        }

        PositionCropRenderer();
    }

    public void RefreshVisual()
    {
        EnsureCropRenderer();

        if (cropRenderer == null)
        {
            return;
        }

        cropRenderer.sortingOrder = cropSortingOrder;

        Sprite stageSprite = ResolveCurrentStageSprite();
        bool shouldShow =
            stageSprite != null &&
            (state == FarmPlotState.Growing ||
            state == FarmPlotState.Mature ||
            !hideCropRendererWhenInactive);

        cropRenderer.sprite = stageSprite;
        cropRenderer.enabled = shouldShow;
        PositionCropRenderer();
    }

    public float GetGrowthProgress01()
    {
        if (state != FarmPlotState.Growing &&
            state != FarmPlotState.Mature)
        {
            return 0f;
        }

        if (plantedWorldHour < 0f)
        {
            return state == FarmPlotState.Mature ? 1f : 0f;
        }

        float elapsed = GetCurrentWorldHour() - plantedWorldHour;
        if (growDurationGameHours <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(elapsed / growDurationGameHours);
    }

    void UpdateStateFromWorldTime()
    {
        if (state == FarmPlotState.Cooldown &&
            GetCurrentAbsoluteDay() >= nextPlantAllowedDay)
        {
            state = FarmPlotState.Empty;
            LogDebug("CooldownFinished");
        }

        if (state != FarmPlotState.Growing)
        {
            return;
        }

        if (GetGrowthProgress01() >= 1f)
        {
            state = FarmPlotState.Mature;
            LogDebug("ReachedMature");
        }
    }

    bool SetReservation(
        GameObject requester,
        float durationSeconds,
        FarmPlotReservationKind kind)
    {
        if (requester == null)
        {
            return false;
        }

        reservedBy = requester;
        reservationExpiresAt =
            Time.time + Mathf.Max(0.25f, durationSeconds);
        reservationKind = kind;
        return true;
    }

    void ReleaseExpiredReservation()
    {
        if (reservedBy == null)
        {
            return;
        }

        if (reservationExpiresAt > Time.time &&
            reservedBy != null)
        {
            return;
        }

        ReleaseReservation(null);
    }

    void EnsureCropRenderer()
    {
        if (cropRenderer != null)
        {
            return;
        }

        Transform child = transform.Find(CropVisualChildName);
        if (child == null)
        {
            GameObject visualObject = new GameObject(CropVisualChildName);
            child = visualObject.transform;
            child.SetParent(transform, false);
        }

        cropRenderer = child.GetComponent<SpriteRenderer>();
        if (cropRenderer == null)
        {
            cropRenderer = child.gameObject.AddComponent<SpriteRenderer>();
        }

        cropRenderer.sortingOrder = cropSortingOrder;
        PositionCropRenderer();
    }

    void PositionCropRenderer()
    {
        if (cropRenderer == null)
        {
            return;
        }

        cropRenderer.transform.localPosition = cropLocalOffset;
        cropRenderer.transform.localRotation = Quaternion.identity;
        cropRenderer.transform.localScale = Vector3.one;
    }

    Sprite ResolveCurrentStageSprite()
    {
        switch (state)
        {
            case FarmPlotState.Growing:
                return ResolveGrowingStageSprite(GetGrowthProgress01());

            case FarmPlotState.Mature:
                return riceMatureSprite != null
                    ? riceMatureSprite
                    : riceGrowingSprite;

            default:
                return riceSeedSprite;
        }
    }

    Sprite ResolveGrowingStageSprite(float progress)
    {
        if (progress >= 1f && riceMatureSprite != null)
        {
            return riceMatureSprite;
        }

        if (progress >= growingStageThreshold &&
            riceGrowingSprite != null)
        {
            return riceGrowingSprite;
        }

        if (progress >= smallStageThreshold &&
            riceSmallSprite != null)
        {
            return riceSmallSprite;
        }

        if (riceSeedSprite != null)
        {
            return riceSeedSprite;
        }

        return riceSmallSprite != null
            ? riceSmallSprite
            : riceGrowingSprite;
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            return timeSystem.CurrentWorldHour;
        }

        return Time.time / 10f;
    }

    int GetCurrentAbsoluteDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            return Mathf.Max(1, timeSystem.CurrentDay);
        }

        return 1;
    }

    void LogDebug(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log(
            "[FarmPlot] " +
            name +
            " state=" + state +
            " day=" + GetCurrentAbsoluteDay() +
            " hour=" + GetCurrentWorldHour().ToString("0.00") +
            " -> " + message,
            this);
    }

#if UNITY_EDITOR
    void AssignDefaultSpritesIfMissing()
    {
        if (riceSeedSprite == null)
        {
            riceSeedSprite =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/UI/linhme/Rice_Seed.png");
        }

        if (riceSmallSprite == null)
        {
            riceSmallSprite =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/UI/linhme/Rice_Small.png");
        }

        if (riceGrowingSprite == null)
        {
            riceGrowingSprite =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/UI/linhme/Rice_Growing.png");
        }

        if (riceMatureSprite == null)
        {
            riceMatureSprite =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/UI/linhme/Rice_Mature.png");
        }

        if (harvestItem == null)
        {
            harvestItem =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/ThucPham/Linh_Me.asset");
        }
    }
#endif
}
