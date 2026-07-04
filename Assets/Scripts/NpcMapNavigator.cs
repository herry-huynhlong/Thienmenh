using System.Collections.Generic;
using UnityEngine;

public static class NpcMapNavigator
{
    static readonly Dictionary<GameObject, NpcMapZone> knownNpcZones =
        new Dictionary<GameObject, NpcMapZone>();

    static readonly Dictionary<GameObject, ZoneLockState> lockedNpcZones =
        new Dictionary<GameObject, ZoneLockState>();

    struct ZoneLockState
    {
        public NpcMapZone zone;
        public float expiresAt;
    }

    public static void ReportNpcZone(GameObject npc, NpcMapZone zone)
    {
        if (npc == null)
        {
            return;
        }

        knownNpcZones[npc] = zone;
    }

    public static void LockNpcZone(
        GameObject npc,
        NpcMapZone zone,
        float duration)
    {
        if (npc == null)
        {
            return;
        }

        ReportNpcZone(npc, zone);

        if (duration <= 0f)
        {
            lockedNpcZones.Remove(npc);
            return;
        }

        lockedNpcZones[npc] = new ZoneLockState
        {
            zone = zone,
            expiresAt = Time.time + duration
        };
    }

    public static void ClearNpcState(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        knownNpcZones.Remove(npc);
        lockedNpcZones.Remove(npc);
    }

    public static bool TryGetKnownNpcZone(
        GameObject npc,
        out NpcMapZone zone)
    {
        zone = default;

        if (npc == null)
        {
            return false;
        }

        return knownNpcZones.TryGetValue(npc, out zone);
    }

    public static NpcMapZone? ResolveActorZone(GameObject npc)
    {
        if (npc == null)
        {
            return null;
        }

        NpcMapArea areaAtPosition =
            NpcMapArea.FindArea(npc.transform.position);

        if (areaAtPosition != null)
        {
            // Khi NPC đã đứng hẳn trong một vùng map, ưu tiên vùng thật
            // hơn cache khóa zone từ teleport trước đó.
            ReportNpcZone(npc, areaAtPosition.zone);
            return areaAtPosition.zone;
        }

        if (TryGetLockedNpcZone(npc, out NpcMapZone lockedZone))
        {
            return lockedZone;
        }

        if (TryGetKnownNpcZone(npc, out NpcMapZone knownZone) &&
            IsNearTeleportBoundary(npc.transform.position, knownZone))
        {
            return knownZone;
        }

        if (TryGetKnownNpcZone(npc, out NpcMapZone known))
        {
            return known;
        }

        NpcMapArea nearestArea =
            NpcMapArea.FindNearestArea(npc.transform.position);

        if (nearestArea != null)
        {
            ReportNpcZone(npc, nearestArea.zone);
            return nearestArea.zone;
        }

        return null;
    }

    public static NpcMapArea ResolveMapAreaAfterTeleport(
        GameObject npc,
        NpcMapZone destinationZone,
        Vector3 referencePosition)
    {
        if (npc == null)
        {
            return null;
        }

        NpcMapArea area = NpcMapArea.FindArea(npc.transform.position);
        if (area != null && area.zone == destinationZone)
        {
            return area;
        }

        area = NpcMapArea.FindArea(referencePosition);
        if (area != null && area.zone == destinationZone)
        {
            return area;
        }

        return NpcMapArea.FindNearestAreaInZone(
            destinationZone,
            referencePosition);
    }

    public static Vector3 GetNextMoveTarget(
        GameObject npc,
        Vector3 finalTarget,
        out bool usingTeleportRoute,
        out string routeAction)
    {
        return GetNextMoveTarget(
            npc,
            finalTarget,
            null,
            out usingTeleportRoute,
            out routeAction);
    }

    public static Vector3 GetNextMoveTarget(
        GameObject npc,
        Vector3 finalTarget,
        NpcMapZone? forcedTargetZone,
        out bool usingTeleportRoute,
        out string routeAction)
    {
        usingTeleportRoute = false;
        routeAction = string.Empty;

        if (npc == null)
        {
            return finalTarget;
        }

        NpcMapZone? currentZone = ResolveActorZone(npc);

        NpcMapZone? targetZone = forcedTargetZone;
        if (!targetZone.HasValue)
        {
            NpcMapArea targetArea =
                NpcMapArea.FindArea(finalTarget);

            if (targetArea != null)
            {
                targetZone = targetArea.zone;
            }
        }

        if (!currentZone.HasValue ||
            !targetZone.HasValue ||
            currentZone.Value == targetZone.Value)
        {
            return finalTarget;
        }

        NpcTeleportGate gate =
            FindNextGate(currentZone.Value, targetZone.Value);

        if (gate == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[NpcMapNavigator] npc=" +
                npc.name +
                " stage=RouteCheck detail=noGate currentZone=" +
                currentZone.Value +
                " targetZone=" +
                targetZone.Value +
                " forcedTargetZone=" +
                (forcedTargetZone.HasValue
                    ? forcedTargetZone.Value.ToString()
                    : "None") +
                " finalTarget=" +
                finalTarget +
                " actorPos=" +
                npc.transform.position);
#endif
            return finalTarget;
        }

        if (!gate.TryGetTeleportRouteForZone(
                currentZone.Value,
                out _,
                out _,
                out NpcMapZone destinationZone))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[NpcMapNavigator] npc=" +
                npc.name +
                " stage=RouteCheck detail=gateMismatch gate=" +
                gate.name +
                " currentZone=" +
                currentZone.Value +
                " targetZone=" +
                targetZone.Value +
                " finalTarget=" +
                finalTarget +
                " actorPos=" +
                npc.transform.position);
#endif
            return finalTarget;
        }

        Vector3 entryPosition =
            gate.GetApproachPosition(npc.transform.position);

        usingTeleportRoute = true;
        routeAction = NpcText.ActionFormat(
            "teleportGateTo",
            GetZoneName(destinationZone));

        return entryPosition;
    }

    public static NpcMapZone? GetDestinationZone(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        NpcMapDestination destination =
            target.GetComponent<NpcMapDestination>();

        if (destination == null)
        {
            destination = target.GetComponentInParent<NpcMapDestination>();
        }

        return destination != null
            ? destination.zone
            : (NpcMapZone?)null;
    }

    static NpcTeleportGate FindNextGate(
        NpcMapZone fromZone,
        NpcMapZone targetZone)
    {
        NpcTeleportGate direct = FindDirectGate(fromZone, targetZone);
        if (direct != null)
        {
            return direct;
        }

        Queue<NpcMapZone> queue = new Queue<NpcMapZone>();
        Dictionary<NpcMapZone, NpcTeleportGate> firstGateByZone =
            new Dictionary<NpcMapZone, NpcTeleportGate>();
        HashSet<NpcMapZone> visited = new HashSet<NpcMapZone>();

        queue.Enqueue(fromZone);
        visited.Add(fromZone);

        while (queue.Count > 0)
        {
            NpcMapZone zone = queue.Dequeue();

            foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
            {
                if (gate == null ||
                    !gate.TryGetOtherZone(zone, out NpcMapZone nextZone))
                {
                    continue;
                }

                if (visited.Contains(nextZone))
                {
                    continue;
                }

                NpcTeleportGate firstGate =
                    zone == fromZone
                    ? gate
                    : firstGateByZone[zone];

                if (nextZone == targetZone)
                {
                    return firstGate;
                }

                firstGateByZone[nextZone] = firstGate;
                visited.Add(nextZone);
                queue.Enqueue(nextZone);
            }
        }

        return null;
    }

    static NpcTeleportGate FindDirectGate(
        NpcMapZone fromZone,
        NpcMapZone toZone)
    {
        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                !gate.Connects(fromZone, toZone) ||
                ResolvePhysicalGateZone(gate) != fromZone)
            {
                continue;
            }

            return gate;
        }

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate != null &&
                gate.Connects(fromZone, toZone))
            {
                return gate;
            }
        }

        return null;
    }

    static bool TryGetLockedNpcZone(
        GameObject npc,
        out NpcMapZone zone)
    {
        zone = default;

        if (npc == null)
        {
            return false;
        }

        if (!lockedNpcZones.TryGetValue(npc, out ZoneLockState lockState))
        {
            return false;
        }

        if (Time.time >= lockState.expiresAt)
        {
            lockedNpcZones.Remove(npc);
            return false;
        }

        zone = lockState.zone;
        return true;
    }

    static NpcMapZone? ResolvePhysicalGateZone(NpcTeleportGate gate)
    {
        if (gate == null)
        {
            return null;
        }

        NpcMapArea area =
            NpcMapArea.FindArea(gate.transform.position);
        if (area != null)
        {
            return area.zone;
        }

        area = NpcMapArea.FindArea(gate.EntryPosition);
        if (area != null)
        {
            return area.zone;
        }

        return null;
    }

    static bool IsNearTeleportBoundary(
        Vector3 position,
        NpcMapZone zone)
    {
        const float exitBuffer = 2.5f;
        const float entryBuffer = 1.25f;

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null)
            {
                continue;
            }

            if (gate.TryGetTeleportRouteForZone(
                    zone,
                    out Vector3 entryPosition,
                    out Vector3 exitPosition,
                    out _))
            {
                if (Vector2.Distance(position, exitPosition) <= exitBuffer ||
                    Vector2.Distance(position, entryPosition) <= entryBuffer)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static string GetZoneName(NpcMapZone zone)
    {
        switch (zone)
        {
            case NpcMapZone.VanBaoLau:
                return "Vạn Bảo Lâu";
            case NpcMapZone.MaThuSonMach:
                return "Ma Thú Sơn Mạch";
            default:
                return "Làng";
        }
    }
}

