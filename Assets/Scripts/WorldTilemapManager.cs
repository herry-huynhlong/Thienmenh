using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldTilemapManager : MonoBehaviour
{
    public static WorldTilemapManager Instance;

    [Header("Tilemaps")]
    public Tilemap farmTilemap;
    public Tilemap fishingTilemap;
    public Tilemap huntingTilemap;
    public Tilemap roadTilemap;
    public Tilemap marketTilemap;

    List<Vector3> farmTiles =
        new List<Vector3>();

    List<Vector3> fishingTiles =
        new List<Vector3>();

    List<Vector3> huntingTiles =
        new List<Vector3>();

    List<Vector3> roadTiles =
        new List<Vector3>();

    List<Vector3> marketTiles =
        new List<Vector3>();

    readonly HashSet<Vector3Int> roadCells =
        new HashSet<Vector3Int>();

    readonly Dictionary<Vector3Int, Vector3> roadCellCenters =
        new Dictionary<Vector3Int, Vector3>();

    Dictionary<Vector3, VillagerAI> occupiedFishing =
        new Dictionary<Vector3, VillagerAI>();

    void Awake()
    {
        Instance = this;

        CacheTiles(
            farmTilemap,
            farmTiles);

        CacheTiles(
            fishingTilemap,
            fishingTiles);

        CacheTiles(
            huntingTilemap,
            huntingTiles);

        CacheTiles(
            roadTilemap,
            roadTiles);
        CacheRoadCells();

        CacheTiles(
            marketTilemap,
            marketTiles);
    }

    void CacheTiles(
        Tilemap tilemap,
        List<Vector3> list)
    {
        list.Clear();

        if (tilemap == null)
        {
            return;
        }

        BoundsInt bounds =
            tilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(pos))
            {
                continue;
            }

            list.Add(
                tilemap.GetCellCenterWorld(pos));
        }
    }

    public Vector3 GetNearestRoad(
        Vector3 from,
        NpcMapZone? zone = null)
    {
        Vector3 road;
        return TryGetNearestRoad(from, zone, null, out road)
            ? road
            : from;
    }

    void CacheRoadCells()
    {
        roadCells.Clear();
        roadCellCenters.Clear();

        if (roadTilemap == null)
        {
            return;
        }

        foreach (Vector3 road in roadTiles)
        {
            Vector3Int cell =
                roadTilemap.WorldToCell(road);

            if (roadCells.Add(cell))
            {
                roadCellCenters[cell] =
                    roadTilemap.GetCellCenterWorld(cell);
            }
        }
    }

    public bool TryGetNearestRoad(
        Vector3 from,
        NpcMapZone? zone,
        System.Predicate<Vector3> roadFilter,
        out Vector3 bestRoad)
    {
        float closest =
            Mathf.Infinity;

        bestRoad =
            from;
        bool found = false;

        foreach (Vector3 road in roadTiles)
        {
            if (zone.HasValue)
            {
                NpcMapArea roadArea =
                    NpcMapArea.FindArea(road);

                if (roadArea == null ||
                    roadArea.zone != zone.Value)
                {
                    continue;
                }
            }

            if (roadFilter != null &&
                !roadFilter(road))
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    from,
                    road);

            if (distance < closest)
            {
                closest = distance;
                bestRoad = road;
                found = true;
            }
        }

        return found;
    }

    public bool TryGetRoadWaypointToTarget(
        Vector3 from,
        Vector3 target,
        NpcMapZone? zone,
        System.Predicate<Vector3> entryFilter,
        out Vector3 waypoint)
    {
        waypoint = from;

        if (roadTilemap == null ||
            roadCells.Count == 0)
        {
            return false;
        }

        Vector3Int startCell;
        Vector3Int targetCell;

        if (!TryFindBestRoadCell(
                from,
                zone,
                entryFilter,
                out startCell) ||
            !TryFindBestRoadCell(
                target,
                zone,
                null,
                out targetCell))
        {
            return false;
        }

        if (startCell == targetCell)
        {
            waypoint = roadCellCenters[startCell];
            return true;
        }

        List<Vector3Int> route =
            new List<Vector3Int>();

        if (!TryBuildRoadRoute(startCell, targetCell, route) ||
            route.Count == 0)
        {
            return false;
        }

        int waypointIndex =
            route.Count > 1 ? 1 : 0;

        waypoint = roadCellCenters[route[waypointIndex]];
        return true;
    }

    bool TryFindBestRoadCell(
        Vector3 reference,
        NpcMapZone? zone,
        System.Predicate<Vector3> filter,
        out Vector3Int bestCell)
    {
        bestCell = Vector3Int.zero;
        float bestDistance =
            Mathf.Infinity;
        bool found = false;

        foreach (Vector3Int cell in roadCells)
        {
            Vector3 center =
                roadCellCenters[cell];

            if (zone.HasValue)
            {
                NpcMapArea area =
                    NpcMapArea.FindArea(center);

                if (area == null ||
                    area.zone != zone.Value)
                {
                    continue;
                }
            }

            if (filter != null &&
                !filter(center))
            {
                continue;
            }

            float distance =
                Vector2.Distance(reference, center);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCell = cell;
                found = true;
            }
        }

        return found;
    }

    bool TryBuildRoadRoute(
        Vector3Int start,
        Vector3Int target,
        List<Vector3Int> route)
    {
        route.Clear();

        Queue<Vector3Int> open =
            new Queue<Vector3Int>();

        HashSet<Vector3Int> visited =
            new HashSet<Vector3Int>();

        Dictionary<Vector3Int, Vector3Int> parent =
            new Dictionary<Vector3Int, Vector3Int>();

        open.Enqueue(start);
        visited.Add(start);

        while (open.Count > 0)
        {
            Vector3Int current =
                open.Dequeue();

            if (current == target)
            {
                BuildRoute(start, target, parent, route);
                return route.Count > 0;
            }

            foreach (Vector3Int offset in RoadNeighborOffsets)
            {
                Vector3Int next =
                    current + offset;

                if (!roadCells.Contains(next) ||
                    visited.Contains(next))
                {
                    continue;
                }

                visited.Add(next);
                parent[next] = current;
                open.Enqueue(next);
            }
        }

        return false;
    }

    void BuildRoute(
        Vector3Int start,
        Vector3Int target,
        Dictionary<Vector3Int, Vector3Int> parent,
        List<Vector3Int> route)
    {
        route.Clear();

        Vector3Int current =
            target;

        route.Add(current);

        while (current != start)
        {
            if (!parent.TryGetValue(current, out current))
            {
                route.Clear();
                return;
            }

            route.Add(current);
        }

        route.Reverse();
    }

    static readonly Vector3Int[] RoadNeighborOffsets =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(-1, -1, 0)
    };

    public Vector3 GetFarmTile()
    {
        if (farmTiles.Count == 0)
        {
            return Vector3.zero;
        }

        return farmTiles[
            Random.Range(0, farmTiles.Count)];
    }

    public Vector3 GetHuntingTile(NpcMapZone? preferredZone = null)
    {
        if (huntingTiles.Count == 0)
        {
            return Vector3.zero;
        }

        if (preferredZone.HasValue)
        {
            List<Vector3> preferredTiles =
                new List<Vector3>();

            foreach (Vector3 tile in huntingTiles)
            {
                NpcMapArea area = NpcMapArea.FindArea(tile);
                if (area != null && area.zone == preferredZone.Value)
                {
                    preferredTiles.Add(tile);
                }
            }

            if (preferredTiles.Count > 0)
            {
                return preferredTiles[
                    Random.Range(0, preferredTiles.Count)];
            }
        }

        return huntingTiles[
            Random.Range(0, huntingTiles.Count)];
    }

    public Vector3 GetFishingTile(
        VillagerAI villager,
        NpcMapZone? preferredZone = null)
    {
        List<Vector3> freeTiles =
            new List<Vector3>();
        List<Vector3> preferredTiles =
            new List<Vector3>();

        foreach (Vector3 tile in fishingTiles)
        {
            if (!occupiedFishing.ContainsKey(tile))
            {
                freeTiles.Add(tile);

                if (preferredZone.HasValue)
                {
                    NpcMapArea area = NpcMapArea.FindArea(tile);
                    if (area != null && area.zone == preferredZone.Value)
                    {
                        preferredTiles.Add(tile);
                    }
                }
            }
        }

        List<Vector3> candidates =
            preferredZone.HasValue && preferredTiles.Count > 0
            ? preferredTiles
            : freeTiles;

        if (candidates.Count == 0)
        {
            return Vector3.zero;
        }

        Vector3 selected =
            candidates[
                Random.Range(0, candidates.Count)];

        occupiedFishing[selected] =
            villager;

        return selected;
    }

    public void ReleaseFishingTile(
        VillagerAI villager)
    {
        if (villager == null)
        {
            return;
        }

        List<Vector3> remove =
            new List<Vector3>();

        foreach (KeyValuePair<Vector3, VillagerAI> pair in occupiedFishing)
        {
            if (pair.Value == villager)
            {
                remove.Add(pair.Key);
            }
        }

        foreach (Vector3 tile in remove)
        {
            occupiedFishing.Remove(tile);
        }
    }

    public Vector3 GetMarketTile()
    {
        if (marketTiles.Count == 0)
        {
            return Vector3.zero;
        }

        return marketTiles[
            Random.Range(0, marketTiles.Count)];
    }
}
