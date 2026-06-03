using System;
using System.Collections.Generic;
using UnityEngine;

public static class NpcPathMemorySystem
{
    const int MaxRememberedPaths = 240;
    const float DefaultRouteCellSize = 2f;

    static readonly Dictionary<string, RememberedPath> paths =
        new Dictionary<string, RememberedPath>();

    static readonly Queue<string> insertionOrder =
        new Queue<string>();

    public static bool TryGetPath(
        string mapKey,
        Vector3 start,
        Vector3 target,
        float routeCellSize,
        Func<Vector3, bool> isWalkable,
        Func<Vector3, Vector3, bool> hasClearLine,
        List<Vector3> output)
    {
        if (output == null)
        {
            return false;
        }

        output.Clear();

        string key =
            BuildKey(mapKey, start, target, routeCellSize);

        if (!paths.TryGetValue(key, out RememberedPath path) ||
            path == null ||
            path.points == null ||
            path.points.Count == 0)
        {
            return false;
        }

        if (!IsPathStillValid(
                start,
                target,
                path.points,
                isWalkable,
                hasClearLine))
        {
            paths.Remove(key);
            return false;
        }

        output.AddRange(path.points);
        path.lastUsedFrame = Time.frameCount;
        path.useCount++;
        return true;
    }

    public static void RememberPath(
        string mapKey,
        Vector3 start,
        Vector3 target,
        float routeCellSize,
        List<Vector3> points)
    {
        if (points == null ||
            points.Count == 0)
        {
            return;
        }

        string key =
            BuildKey(mapKey, start, target, routeCellSize);

        if (!paths.ContainsKey(key))
        {
            insertionOrder.Enqueue(key);
        }

        paths[key] =
            new RememberedPath
            {
                points = new List<Vector3>(points),
                lastUsedFrame = Time.frameCount,
                useCount = 0
            };

        TrimOldPaths();
    }

    public static void ForgetPath(
        string mapKey,
        Vector3 start,
        Vector3 target,
        float routeCellSize)
    {
        paths.Remove(
            BuildKey(mapKey, start, target, routeCellSize));
    }

    public static void Clear()
    {
        paths.Clear();
        insertionOrder.Clear();
    }

    static bool IsPathStillValid(
        Vector3 start,
        Vector3 target,
        List<Vector3> points,
        Func<Vector3, bool> isWalkable,
        Func<Vector3, Vector3, bool> hasClearLine)
    {
        if (isWalkable == null ||
            hasClearLine == null)
        {
            return false;
        }

        if (!hasClearLine(points[0], start))
        {
            return false;
        }

        for (int i = 0; i < points.Count; i++)
        {
            if (!isWalkable(points[i]))
            {
                return false;
            }

            if (i > 0 &&
                !hasClearLine(points[i], points[i - 1]))
            {
                return false;
            }
        }

        return hasClearLine(target, points[points.Count - 1]);
    }

    static string BuildKey(
        string mapKey,
        Vector3 start,
        Vector3 target,
        float routeCellSize)
    {
        float cellSize =
            Mathf.Max(0.1f, routeCellSize <= 0f
                ? DefaultRouteCellSize
                : routeCellSize);

        Vector2Int startCell =
            ToRouteCell(start, cellSize);

        Vector2Int targetCell =
            ToRouteCell(target, cellSize);

        return mapKey + "|" +
            startCell.x + "," + startCell.y + ">" +
            targetCell.x + "," + targetCell.y;
    }

    static Vector2Int ToRouteCell(
        Vector3 position,
        float cellSize)
    {
        return new Vector2Int(
            Mathf.RoundToInt(position.x / cellSize),
            Mathf.RoundToInt(position.y / cellSize));
    }

    static void TrimOldPaths()
    {
        while (paths.Count > MaxRememberedPaths &&
            insertionOrder.Count > 0)
        {
            string key =
                insertionOrder.Dequeue();

            paths.Remove(key);
        }
    }

    class RememberedPath
    {
        public List<Vector3> points;
        public int lastUsedFrame;
        public int useCount;
    }
}
