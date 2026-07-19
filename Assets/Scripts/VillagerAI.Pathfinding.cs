using System.Collections.Generic;
using UnityEngine;

// Path construction, scoring, caching and blocked-route recovery.
public partial class VillagerAI
{
    void HandleBlockedMovement(Vector3 blockedTarget, Vector3 finalTarget)
    {
        if (TryCommitObstacleScanTarget(
                (Vector2)finalTarget - (Vector2)transform.position,
                finalTarget))
        {
            return;
        }

        Vector2 escapeDirection =
            (Vector2)finalTarget - (Vector2)transform.position;

        if (escapeDirection.sqrMagnitude > 0.0001f &&
            TryChooseDetourDirection(
                escapeDirection,
                finalTarget,
                out Vector2 detourDirection) &&
            TryCommitObstacleAvoidTarget(detourDirection))
        {
            return;
        }

        blockedMoveTimer += Time.fixedDeltaTime;
        LogMovementHaltDebug(
            "MoveBlockedHold",
            "blockedTarget=" + blockedTarget +
            " finalTarget=" + finalTarget +
            " blockedMoveTimer=" +
            blockedMoveTimer.ToString("0.00") +
            " retryDelay=" +
            blockedTargetRetryDelay.ToString("0.00"));
        StopMoving();
        ClearActivePath();

        if (blockedMoveTimer < blockedTargetRetryDelay)
        {
            return;
        }

        blockedMoveTimer = 0f;

        if (hasWanderTarget)
        {
            hasWanderTarget = false;
            return;
        }

        if (hasDirectMoveTarget)
        {
            Vector3 fallback;
            Vector3 escapeSeed =
                GetBlockedEscapeSeed(blockedTarget);

            if (TryFindClearPointNear(escapeSeed, out fallback) &&
                Vector2.Distance(fallback, transform.position) >
                arriveDistance)
            {
                directMoveTarget = fallback;
                LogMovementHaltDebug(
                    "MoveBlockedRecover",
                    "escapeSeed=" + escapeSeed +
                    " fallback=" + fallback +
                    " directTargetCleared=0");
            }
            else
            {
                hasDirectMoveTarget = false;
                LogMovementHaltDebug(
                    "MoveBlockedRecover",
                    "escapeSeed=" + escapeSeed +
                    " fallback=none directTargetCleared=1");
            }
        }

        if (!hasWanderTarget &&
            !hasDirectMoveTarget &&
            currentTarget == null)
        {
            actionTimer = Mathf.Max(actionTimer, thinkInterval);

            if (IsActionLocked ||
                IsRoutineTravelOrCultivationAction(currentAction))
            {
                return;
            }

            currentAction = NpcText.Action("idle");
        }
    }

    Vector3 GetBlockedEscapeSeed(Vector3 blockedTarget)
    {
        Vector2 away =
            (Vector2)(transform.position - blockedTarget);

        if (away.sqrMagnitude <= 0.0001f)
        {
            away =
                UnityEngine.Random.insideUnitCircle;
        }

        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.up;
        }

        away.Normalize();

        return ClampToCurrentMapArea(
            transform.position +
            (Vector3)(away * Mathf.Max(unstuckOffsetRadius, targetClearRadius * 3f)));
    }

    bool ShouldRequirePathForDirectMove(Vector3 finalTarget)
    {
        if (!requireClearLineForDirectMove ||
            !useSmartPathfinding)
        {
            return false;
        }

        if (Vector2.Distance(transform.position, finalTarget) <=
            directMovePathDistance)
        {
            return false;
        }

        return !HasClearLineTo(finalTarget);
    }

    bool TryGetSmartPathWaypoint(
        Vector3 finalTarget,
        out Vector3 waypoint)
    {
        waypoint = finalTarget;

        if (!useSmartPathfinding)
        {
            ClearActivePath();
            return false;
        }

        if (activePath.Count > 0 &&
            Vector2.Distance(activePathTarget, finalTarget) >
            pathReplanTargetDistance)
        {
            ClearActivePath();
        }

        if (HasClearLineTo(finalTarget))
        {
            ClearActivePath();
            return false;
        }

        if (activePath.Count == 0 &&
            !TryBuildSmartPath(finalTarget))
        {
            return false;
        }

        SkipVisiblePathWaypoints();

        if (activePathIndex < 0 ||
            activePathIndex >= activePath.Count)
        {
            ClearActivePath();
            return false;
        }

        waypoint = activePath[activePathIndex];
        return true;
    }

    bool TryBuildSmartPath(Vector3 finalTarget)
    {
        if (!useSmartPathfinding ||
            Time.time < nextSmartPathAllowedTime)
        {
            return false;
        }

        nextSmartPathAllowedTime =
            Time.time + Mathf.Max(2f, pathReplanCooldown);

        NpcPerformanceOverlay.RecordPathRequest();
        float pathStartTime =
            Time.realtimeSinceStartup;
        int visited = 0;

        ClearActivePath();

        if (!useSmartPathfinding ||
            pathCellSize <= 0.05f ||
            !IsInsideCurrentMapArea(finalTarget))
        {
            RecordSmartPathResult(false, visited, pathStartTime);
            return false;
        }

        Vector3 start =
            ClampToCurrentMapArea(transform.position);

        finalTarget =
            ClampToCurrentMapArea(finalTarget);

        if (!IsInsidePathSearchDistance(start, finalTarget))
        {
            RecordSmartPathResult(false, visited, pathStartTime);
            return false;
        }

        Vector2Int startCell =
            WorldToPathCell(start);

        Vector2Int targetCell =
            WorldToPathCell(finalTarget);

        if (!IsPathCellWalkable(startCell) &&
            !TryFindNearestWalkableCell(startCell, out startCell))
        {
            RecordSmartPathResult(false, visited, pathStartTime);
            return false;
        }

        if (!IsPathCellWalkable(targetCell) &&
            !TryFindNearestWalkableCell(targetCell, out targetCell))
        {
            RecordSmartPathResult(false, visited, pathStartTime);
            return false;
        }

        start =
            PathCellToWorld(startCell);

        finalTarget =
            PathCellToWorld(targetCell);

        bool hasRememberedPath =
            TryGetRememberedPathCandidate(start, finalTarget);

        if (hasRememberedPath &&
            !compareRememberedPathWithNewPath)
        {
            NpcPerformanceOverlay.RecordPathCacheHit();
            RecordSmartPathResult(true, visited, pathStartTime);
            return ApplyRememberedPath(start, finalTarget);
        }

        Dictionary<Vector2Int, PathNode> nodes =
            new Dictionary<Vector2Int, PathNode>();

        List<PathNode> open =
            new List<PathNode>();

        HashSet<Vector2Int> closed =
            new HashSet<Vector2Int>();

        PathNode startNode =
            new PathNode(startCell, null, 0, GetPathHeuristic(startCell, targetCell));

        nodes[startCell] = startNode;
        open.Add(startNode);

        while (open.Count > 0 && visited < maxPathNodes)
        {
            PathNode current =
                PopLowestCostNode(open);

            if (current.cell == targetCell)
            {
                BuildPathCandidate(
                    current,
                    finalTarget,
                    computedPathBuffer);

                if (computedPathBuffer.Count == 0)
                {
                    if (hasRememberedPath)
                    {
                        NpcPerformanceOverlay.RecordPathCacheHit();
                    }

                    RecordSmartPathResult(hasRememberedPath, visited, pathStartTime);
                    return hasRememberedPath &&
                        ApplyRememberedPath(start, finalTarget);
                }

                if (hasRememberedPath &&
                    IsRememberedPathBetter(
                        start,
                        finalTarget,
                        computedPathBuffer))
                {
                    NpcPerformanceOverlay.RecordPathCacheHit();
                    RecordSmartPathResult(true, visited, pathStartTime);
                    return ApplyRememberedPath(start, finalTarget);
                }

                ApplyPathCandidate(
                    computedPathBuffer,
                    finalTarget);

                RememberActivePath(start, finalTarget);
                RecordSmartPathResult(activePath.Count > 0, visited, pathStartTime);
                return activePath.Count > 0;
            }

            closed.Add(current.cell);
            visited++;

            for (int i = 0; i < PathNeighborOffsets.Length; i++)
            {
                Vector2Int offset =
                    PathNeighborOffsets[i];

                Vector2Int nextCell =
                    current.cell + offset;

                if (closed.Contains(nextCell) ||
                    !IsPathStepWalkable(current.cell, nextCell, offset))
                {
                    continue;
                }

                int stepCost =
                    offset.x != 0 && offset.y != 0
                    ? 14
                    : 10;

                int newCost =
                    current.gCost + stepCost;

                if (nodes.TryGetValue(nextCell, out PathNode nextNode))
                {
                    if (newCost >= nextNode.gCost)
                    {
                        continue;
                    }

                    nextNode.parent = current;
                    nextNode.gCost = newCost;
                    nextNode.hCost =
                        GetPathHeuristic(nextCell, targetCell);
                }
                else
                {
                    nextNode =
                        new PathNode(
                            nextCell,
                            current,
                            newCost,
                            GetPathHeuristic(nextCell, targetCell));

                    nodes[nextCell] = nextNode;
                    open.Add(nextNode);
                }
            }
        }

        if (hasRememberedPath)
        {
            NpcPerformanceOverlay.RecordPathCacheHit();
        }

        RecordSmartPathResult(hasRememberedPath, visited, pathStartTime);

        return hasRememberedPath &&
            ApplyRememberedPath(start, finalTarget);
    }

    float GetElapsedPathMs(float pathStartTime)
    {
        return (Time.realtimeSinceStartup - pathStartTime) * 1000f;
    }

    void RecordSmartPathResult(
        bool success,
        int visited,
        float pathStartTime)
    {
        NpcPerformanceOverlay.RecordPathResult(
            success,
            visited,
            GetElapsedPathMs(pathStartTime));

        float baseDelay = Mathf.Max(2f, pathReplanCooldown);
        if (success)
        {
            consecutiveSmartPathFailures = 0;
            nextSmartPathAllowedTime = Time.time + baseDelay;
            return;
        }

        consecutiveSmartPathFailures =
            Mathf.Min(consecutiveSmartPathFailures + 1, 4);

        float failDelay =
            Mathf.Min(
                Mathf.Max(8f, baseDelay),
                baseDelay * (1f + consecutiveSmartPathFailures));

        nextSmartPathAllowedTime = Time.time + failDelay;
    }

    void BuildPathCandidate(
        PathNode endNode,
        Vector3 finalTarget,
        List<Vector3> output)
    {
        output.Clear();

        List<Vector3> reversed =
            new List<Vector3>();

        PathNode current =
            endNode;

        int steps = 0;

        while (current != null && steps < maxPathSteps)
        {
            reversed.Add(PathCellToWorld(current.cell));
            current = current.parent;
            steps++;
        }

        if (current != null)
        {
            return;
        }

        for (int i = reversed.Count - 1; i >= 0; i--)
        {
            Vector3 point =
                ClampToCurrentMapArea(reversed[i]);

            if (Vector2.Distance(point, transform.position) <=
                pathWaypointReachDistance)
            {
                continue;
            }

            output.Add(point);
        }

        if (output.Count == 0 ||
            Vector2.Distance(output[output.Count - 1], finalTarget) >
            pathWaypointReachDistance)
        {
            output.Add(finalTarget);
        }

        SimplifyPathCandidate(output);
    }

    void ApplyPathCandidate(
        List<Vector3> source,
        Vector3 finalTarget)
    {
        activePath.Clear();
        activePath.AddRange(source);
        activePathTarget = finalTarget;
        activePathIndex = 0;
    }

    bool TryGetRememberedPathCandidate(
        Vector3 start,
        Vector3 finalTarget)
    {
        if (!useSharedPathMemory)
        {
            return false;
        }

        if (!NpcPathMemorySystem.TryGetPath(
                GetPathMemoryMapKey(),
                start,
                finalTarget,
                sharedPathMemoryCellSize,
                IsMoveTargetFeasible,
                HasClearLineTo,
                rememberedPathBuffer))
        {
            return false;
        }

        TrimPathStartForCurrentPosition(rememberedPathBuffer);
        return rememberedPathBuffer.Count > 0;
    }

    bool ApplyRememberedPath(
        Vector3 start,
        Vector3 finalTarget)
    {
        if (rememberedPathBuffer.Count == 0)
        {
            return false;
        }

        ApplyPathCandidate(
            rememberedPathBuffer,
            finalTarget);
        return true;
    }

    bool IsRememberedPathBetter(
        Vector3 start,
        Vector3 finalTarget,
        List<Vector3> computedPath)
    {
        float rememberedScore =
            GetPathCandidateScore(
                start,
                finalTarget,
                rememberedPathBuffer);

        float computedScore =
            GetPathCandidateScore(
                start,
                finalTarget,
                computedPath);

        return rememberedScore <= computedScore;
    }

    float GetPathCandidateScore(
        Vector3 start,
        Vector3 finalTarget,
        List<Vector3> path)
    {
        if (path == null ||
            path.Count == 0)
        {
            return float.PositiveInfinity;
        }

        float score = 0f;
        Vector3 previous = start;
        Vector2 previousDirection = Vector2.zero;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 point = path[i];
            Vector2 segment = point - previous;
            float length = segment.magnitude;

            score += length;

            if (length > 0.001f)
            {
                Vector2 direction = segment / length;
                if (previousDirection.sqrMagnitude > 0.0001f)
                {
                    score +=
                        (1f - Mathf.Clamp01(
                            Vector2.Dot(previousDirection, direction))) *
                        pathTurnPenalty;
                }

                previousDirection = direction;
            }

            previous = point;
        }

        score += Vector2.Distance(previous, finalTarget);
        return score;
    }

    void TrimPathStartForCurrentPosition(List<Vector3> path)
    {
        if (path == null)
        {
            return;
        }

        for (int i = path.Count - 1; i >= 0; i--)
        {
            path[i] =
                ClampToCurrentMapArea(path[i]);

            if (Vector2.Distance(path[i], transform.position) <=
                pathWaypointReachDistance)
            {
                path.RemoveAt(i);
            }
        }
    }

    void RememberActivePath(
        Vector3 start,
        Vector3 finalTarget)
    {
        if (!useSharedPathMemory ||
            activePath.Count == 0)
        {
            return;
        }

        NpcPathMemorySystem.RememberPath(
            GetPathMemoryMapKey(),
            start,
            finalTarget,
            sharedPathMemoryCellSize,
            activePath);
    }

    string GetPathMemoryMapKey()
    {
        RefreshCurrentMapArea();

        if (currentMapArea != null)
        {
            return gameObject.scene.name + ":" +
                currentMapArea.zone + ":" +
                currentMapArea.name;
        }

        return gameObject.scene.name;
    }

    void SimplifyPathCandidate(List<Vector3> path)
    {
        if (path == null ||
            path.Count <= 2)
        {
            return;
        }

        List<Vector3> simplified =
            new List<Vector3>();

        int index = 0;

        while (index < path.Count)
        {
            int next = index + 1;

            for (int i = path.Count - 1; i > index; i--)
            {
                if (HasClearLineTo(path[i], path[index]))
                {
                    next = i;
                    break;
                }
            }

            simplified.Add(path[index]);
            index = next;
        }

        path.Clear();
        path.AddRange(simplified);
    }

    void SkipVisiblePathWaypoints()
    {
        while (activePathIndex < activePath.Count - 1 &&
            HasClearLineTo(activePath[activePathIndex + 1], activePath[activePathIndex]))
        {
            activePathIndex++;
        }
    }

    void AdvanceActivePathWaypoint()
    {
        activePathIndex++;

        if (activePathIndex >= activePath.Count)
        {
            ClearActivePath();
        }
    }

    void ClearActivePath()
    {
        activePath.Clear();
        activePathIndex = 0;
        activePathTarget = Vector3.zero;
    }

    bool TryFindNearestWalkableCell(
        Vector2Int origin,
        out Vector2Int result)
    {
        int maxRadius =
            Mathf.CeilToInt(
                Mathf.Max(targetClearRadius * 3f, pathCellSize) /
                Mathf.Max(0.05f, pathCellSize)) + 3;

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius &&
                        Mathf.Abs(y) != radius)
                    {
                        continue;
                    }

                    Vector2Int candidate =
                        origin + new Vector2Int(x, y);

                    if (IsPathCellWalkable(candidate))
                    {
                        result = candidate;
                        return true;
                    }
                }
            }
        }

        result = origin;
        return false;
    }

    bool IsInsidePathSearchDistance(
        Vector3 start,
        Vector3 target)
    {
        float searchDistance =
            GetEffectivePathSearchDistance();

        return searchDistance <= 0f ||
            Vector2.Distance(start, target) <= searchDistance;
    }

    float GetEffectivePathSearchDistance()
    {
        if (maxPathSearchDistance > 0f)
        {
            return maxPathSearchDistance;
        }

        if (currentMapArea != null &&
            currentMapArea.areaBounds != null)
        {
            Bounds bounds =
                currentMapArea.areaBounds.bounds;

            return Mathf.Max(
                bounds.size.x,
                bounds.size.y) +
                pathCellSize * 4f;
        }

        return 0f;
    }

    bool IsPathStepWalkable(
        Vector2Int from,
        Vector2Int to,
        Vector2Int offset)
    {
        if (!IsPathCellWalkable(to))
        {
            return false;
        }

        if (offset.x == 0 || offset.y == 0)
        {
            return true;
        }

        return IsPathCellWalkable(from + new Vector2Int(offset.x, 0)) &&
            IsPathCellWalkable(from + new Vector2Int(0, offset.y));
    }

    bool IsPathCellWalkable(Vector2Int cell)
    {
        Vector3 world =
            PathCellToWorld(cell);

        return IsMoveTargetFeasible(world);
    }

    Vector2Int WorldToPathCell(Vector3 position)
    {
        float size =
            Mathf.Max(0.05f, pathCellSize);

        return new Vector2Int(
            Mathf.RoundToInt(position.x / size),
            Mathf.RoundToInt(position.y / size));
    }

    Vector3 PathCellToWorld(Vector2Int cell)
    {
        float size =
            Mathf.Max(0.05f, pathCellSize);

        return new Vector3(
            cell.x * size,
            cell.y * size,
            transform.position.z);
    }

    PathNode PopLowestCostNode(List<PathNode> open)
    {
        int bestIndex = 0;
        PathNode best = open[0];

        for (int i = 1; i < open.Count; i++)
        {
            PathNode candidate = open[i];

            if (candidate.FCost < best.FCost ||
                candidate.FCost == best.FCost &&
                candidate.hCost < best.hCost)
            {
                best = candidate;
                bestIndex = i;
            }
        }

        open.RemoveAt(bestIndex);
        return best;
    }

    int GetPathHeuristic(
        Vector2Int from,
        Vector2Int to)
    {
        int dx =
            Mathf.Abs(from.x - to.x);

        int dy =
            Mathf.Abs(from.y - to.y);

        return 10 * (dx + dy) - 6 * Mathf.Min(dx, dy);
    }

    static readonly Vector2Int[] PathNeighborOffsets =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    class PathNode
    {
        public readonly Vector2Int cell;
        public PathNode parent;
        public int gCost;
        public int hCost;

        public int FCost
        {
            get
            {
                return gCost + hCost;
            }
        }

        public PathNode(
            Vector2Int cell,
            PathNode parent,
            int gCost,
            int hCost)
        {
            this.cell = cell;
            this.parent = parent;
            this.gCost = gCost;
            this.hCost = hCost;
        }
    }

    Vector2 GetSeparationDirection()
    {
        if (ignoreNpcBodyCollisions || separationRadius <= 0f)
        {
            return Vector2.zero;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                separationRadius,
                villagerLayers);

        Vector2 push = Vector2.zero;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<VillagerAI>() == null &&
                hit.GetComponentInParent<SmartNpcAI>() == null &&
                hit.GetComponentInParent<NpcMapMover2D>() == null)
            {
                continue;
            }

            Vector2 away =
                (Vector2)transform.position -
                (Vector2)hit.transform.position;

            if (away.sqrMagnitude <= 0.0001f)
            {
                Transform root = GetNpcRoot(hit);
                int otherId =
                    root != null
                    ? UnityObjectIdUtility.GetRuntimeId(root.gameObject)
                    : UnityObjectIdUtility.GetRuntimeId(hit.gameObject);
                away = ((UnityObjectIdUtility.GetRuntimeId(this) ^ otherId) & 1) == 0
                    ? Vector2.right
                    : Vector2.left;
            }

            float distance =
                Mathf.Max(away.magnitude, 0.01f);

            push += away.normalized / distance;
        }

        return push.normalized;
    }

}
