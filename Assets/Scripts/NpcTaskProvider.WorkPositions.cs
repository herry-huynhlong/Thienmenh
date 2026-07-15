using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Patrol/resource work-position selection and safe forest depth.
public partial class NpcTaskProvider
{
    Vector3 GetPatrolStartPosition(NpcTaskOffer offer)
    {
        if (offer != null &&
            offer.taskType == NpcTaskType.FrontierWatch)
        {
            return FrontierDefenseCoordinator.GetPrimaryPosition(
                offer.customTargetId,
                GetFallbackWorkPosition());
        }

        if (patrolPoint != null)
        {
            return patrolPoint.position;
        }

        return GetFallbackWorkPosition();
    }

    Vector3 GetPatrolEndPosition(NpcTaskOffer offer)
    {
        if (offer != null &&
            offer.taskType == NpcTaskType.FrontierWatch)
        {
            return FrontierDefenseCoordinator.GetSecondaryPatrolPosition(
                offer.customTargetId,
                GetPatrolStartPosition(offer));
        }

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

                case NpcTaskType.FrontierWatch:
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

}
