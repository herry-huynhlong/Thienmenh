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
        Vector3 from)
    {
        float closest =
            Mathf.Infinity;

        Vector3 best =
            from;

        foreach (Vector3 road in roadTiles)
        {
            float distance =
                Vector2.Distance(
                    from,
                    road);

            if (distance < closest)
            {
                closest = distance;
                best = road;
            }
        }

        return best;
    }

    public Vector3 GetFarmTile()
    {
        if (farmTiles.Count == 0)
        {
            return Vector3.zero;
        }

        return farmTiles[
            Random.Range(0, farmTiles.Count)];
    }

    public Vector3 GetHuntingTile()
    {
        if (huntingTiles.Count == 0)
        {
            return Vector3.zero;
        }

        return huntingTiles[
            Random.Range(0, huntingTiles.Count)];
    }

    public Vector3 GetFishingTile(
        VillagerAI villager)
    {
        List<Vector3> freeTiles =
            new List<Vector3>();

        foreach (Vector3 tile in fishingTiles)
        {
            if (!occupiedFishing.ContainsKey(tile))
            {
                freeTiles.Add(tile);
            }
        }

        if (freeTiles.Count == 0)
        {
            return Vector3.zero;
        }

        Vector3 selected =
            freeTiles[
                Random.Range(0, freeTiles.Count)];

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
