using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HelloText : MonoBehaviour
{
    public static HelloText Instance;

    [Header("Tilemaps")]
    public Tilemap farmTilemap;
    public Tilemap houseTilemap;
    public Tilemap marketTilemap;
    public Tilemap roadTilemap;

    List<Vector3> farmTiles =
        new List<Vector3>();

    List<Vector3> houseTiles =
        new List<Vector3>();

    List<Vector3> marketTiles =
        new List<Vector3>();

    List<Vector3> roadTiles =
        new List<Vector3>();

    Dictionary<Vector3, VillagerAI> occupiedFarmTiles =
        new Dictionary<Vector3, VillagerAI>();

    void Awake()
    {
        Instance = this;

        CacheTiles(
            farmTilemap,
            farmTiles);

        CacheTiles(
            houseTilemap,
            houseTiles);

        CacheTiles(
            marketTilemap,
            marketTiles);

        CacheTiles(
            roadTilemap,
            roadTiles);
    }

    void CacheTiles(
        Tilemap tilemap,
        List<Vector3> targetList)
    {
        targetList.Clear();

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

            Vector3 worldPos =
                tilemap.GetCellCenterWorld(pos);

            targetList.Add(worldPos);
        }
    }

    public Vector3 GetFarmTile(
        VillagerAI villager)
    {
        List<Vector3> freeTiles =
            new List<Vector3>();

        foreach (Vector3 tile in farmTiles)
        {
            if (!occupiedFarmTiles.ContainsKey(tile))
            {
                freeTiles.Add(tile);
            }
        }

        if (freeTiles.Count == 0)
        {
            return villager.transform.position;
        }

        Vector3 selected =
            freeTiles[
                Random.Range(0, freeTiles.Count)];

        occupiedFarmTiles[selected] =
            villager;

        return selected;
    }

    public Vector3 GetRandomHouseTile()
    {
        if (houseTiles.Count == 0)
        {
            return Vector3.zero;
        }

        return houseTiles[
            Random.Range(0, houseTiles.Count)];
    }

    public Vector3 GetRandomMarketTile()
    {
        if (marketTiles.Count == 0)
        {
            return Vector3.zero;
        }

        return marketTiles[
            Random.Range(0, marketTiles.Count)];
    }

    public Vector3 GetRandomRoadTile()
    {
        if (roadTiles.Count == 0)
        {
            return Vector3.zero;
        }

        return roadTiles[
            Random.Range(0, roadTiles.Count)];
    }

    public void ResetDailyTiles()
    {
        occupiedFarmTiles.Clear();
    }
}