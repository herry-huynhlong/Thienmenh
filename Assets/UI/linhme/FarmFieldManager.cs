using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FarmFieldManager : MonoBehaviour
{
    [Header("Field")]
    public Tilemap farmTilemap;
    public Transform plotsRoot;
    public bool autoRefreshPlotCache = true;

    [Header("Default Plot Setup")]
    public Sprite riceSeedSprite;
    public Sprite riceSmallSprite;
    public Sprite riceGrowingSprite;
    public Sprite riceMatureSprite;
    public StatItemData defaultHarvestItem;
    [Min(0.5f)] public float defaultGrowDurationGameHours = 24f;
    [Min(1)] public int defaultDaysBeforeReplant = 1;
    [Min(1)] public int defaultMinHarvestYield = 1;
    [Min(1)] public int defaultMaxHarvestYield = 2;
    public Vector3 defaultCropLocalOffset = new Vector3(0f, 0.18f, 0f);
    public int defaultCropSortingOrder = 6;

    [Header("Reservations")]
    [Min(0.25f)] public float defaultReservationSeconds = 8f;

    [Header("Runtime")]
    public List<FarmPlot> plots = new List<FarmPlot>();

    void Awake()
    {
        RefreshPlotCache();
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        AssignDefaultSpritesIfMissing();
#endif

        defaultGrowDurationGameHours =
            Mathf.Max(0.5f, defaultGrowDurationGameHours);
        defaultDaysBeforeReplant =
            Mathf.Max(1, defaultDaysBeforeReplant);
        defaultMinHarvestYield =
            Mathf.Max(1, defaultMinHarvestYield);
        defaultMaxHarvestYield =
            Mathf.Max(defaultMinHarvestYield, defaultMaxHarvestYield);
        defaultReservationSeconds =
            Mathf.Max(0.25f, defaultReservationSeconds);

        if (autoRefreshPlotCache)
        {
            RefreshPlotCache();
        }
    }

    public void RefreshPlotCache()
    {
        Transform root = plotsRoot != null
            ? plotsRoot
            : transform;

        FarmPlot[] foundPlots =
            root.GetComponentsInChildren<FarmPlot>(true);

        plots.Clear();

        foreach (FarmPlot plot in foundPlots)
        {
            if (plot != null &&
                !plots.Contains(plot))
            {
                plots.Add(plot);
            }
        }
    }

    [ContextMenu("Farm/Generate Or Sync Plots From Tilemap")]
    public void GenerateOrSyncPlotsFromTilemap()
    {
        if (farmTilemap == null)
        {
            Debug.LogWarning(
                "FarmFieldManager needs a farmTilemap before generating plots.",
                this);
            return;
        }

        Transform root = plotsRoot != null
            ? plotsRoot
            : transform;

        Dictionary<Vector3Int, FarmPlot> existingPlots =
            new Dictionary<Vector3Int, FarmPlot>();

        FarmPlot[] currentPlots =
            root.GetComponentsInChildren<FarmPlot>(true);

        foreach (FarmPlot plot in currentPlots)
        {
            if (plot == null ||
                existingPlots.ContainsKey(plot.Cell))
            {
                continue;
            }

            existingPlots.Add(plot.Cell, plot);
        }

        BoundsInt bounds = farmTilemap.cellBounds;

        foreach (Vector3Int tileCell in bounds.allPositionsWithin)
        {
            if (!farmTilemap.HasTile(tileCell))
            {
                continue;
            }

            if (!existingPlots.TryGetValue(tileCell, out FarmPlot plot) ||
                plot == null)
            {
                GameObject plotObject =
                    new GameObject(
                        "FarmPlot_" +
                        tileCell.x + "_" +
                        tileCell.y);
                plotObject.transform.SetParent(root, false);
                plot = plotObject.AddComponent<FarmPlot>();
                existingPlots[tileCell] = plot;
            }

            ConfigurePlot(plot, tileCell);
        }

        RefreshPlotCache();
    }

    [ContextMenu("Farm/Sync Existing Plot Defaults")]
    public void SyncExistingPlotDefaults()
    {
        RefreshPlotCache();

        foreach (FarmPlot plot in plots)
        {
            if (plot == null)
            {
                continue;
            }

            ConfigurePlot(plot, plot.Cell);
        }
    }

    [ContextMenu("Farm/Reset All Plots To Empty")]
    public void ResetAllPlotsToEmpty()
    {
        RefreshPlotCache();

        foreach (FarmPlot plot in plots)
        {
            if (plot == null)
            {
                continue;
            }

            plot.ForceSetEmpty();
        }
    }

    [ContextMenu("Farm/Test Plant All Plots")]
    public void TestPlantAllPlots()
    {
        RefreshPlotCache();

        foreach (FarmPlot plot in plots)
        {
            if (plot == null)
            {
                continue;
            }

            plot.BeginPlanting();
        }
    }

    [ContextMenu("Farm/Test Force Mature All Plots")]
    public void TestForceMatureAllPlots()
    {
        RefreshPlotCache();

        foreach (FarmPlot plot in plots)
        {
            if (plot == null)
            {
                continue;
            }

            plot.DebugForceMatureFromManager();
        }
    }

    public FarmPlot FindNearestPlotForPlant(
        Vector3 origin,
        GameObject requester = null)
    {
        return FindNearestPlot(
            origin,
            requester,
            true);
    }

    public FarmPlot FindNearestPlotForHarvest(
        Vector3 origin,
        GameObject requester = null)
    {
        return FindNearestPlot(
            origin,
            requester,
            false);
    }

    public bool TryReserveNearestPlotForPlant(
        Vector3 origin,
        GameObject requester,
        out FarmPlot reservedPlot)
    {
        FarmPlot candidate =
            FindNearestPlotForPlant(origin, requester);

        if (candidate != null &&
            candidate.TryReserveForPlant(
                requester,
                defaultReservationSeconds))
        {
            reservedPlot = candidate;
            return true;
        }

        reservedPlot = null;
        return false;
    }

    public bool TryReserveNearestPlotForHarvest(
        Vector3 origin,
        GameObject requester,
        out FarmPlot reservedPlot)
    {
        FarmPlot candidate =
            FindNearestPlotForHarvest(origin, requester);

        if (candidate != null &&
            candidate.TryReserveForHarvest(
                requester,
                defaultReservationSeconds))
        {
            reservedPlot = candidate;
            return true;
        }

        reservedPlot = null;
        return false;
    }

    void ConfigurePlot(
        FarmPlot plot,
        Vector3Int tileCell)
    {
        if (plot == null)
        {
            return;
        }

        plot.ApplyManagerDefaults(
            farmTilemap,
            tileCell,
            riceSeedSprite,
            riceSmallSprite,
            riceGrowingSprite,
            riceMatureSprite,
            defaultGrowDurationGameHours,
            defaultDaysBeforeReplant,
            defaultCropLocalOffset,
            defaultCropSortingOrder);
        plot.harvestItem = defaultHarvestItem;
        plot.minHarvestYield = defaultMinHarvestYield;
        plot.maxHarvestYield = defaultMaxHarvestYield;
    }

    FarmPlot FindNearestPlot(
        Vector3 origin,
        GameObject requester,
        bool forPlant)
    {
        if (autoRefreshPlotCache && plots.Count == 0)
        {
            RefreshPlotCache();
        }

        FarmPlot bestPlot = null;
        float bestDistanceSqr = float.PositiveInfinity;

        foreach (FarmPlot plot in plots)
        {
            if (plot == null)
            {
                continue;
            }

            bool isAvailable =
                forPlant
                    ? plot.CanPlantNow
                    : plot.CanHarvestNow;

            if (!isAvailable ||
                plot.IsReservedByOther(requester))
            {
                continue;
            }

            float distanceSqr =
                (plot.transform.position - origin).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestPlot = plot;
            }
        }

        return bestPlot;
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

        if (defaultHarvestItem == null)
        {
            defaultHarvestItem =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/ThucPham/Linh_Me.asset");
        }
    }
#endif
}
