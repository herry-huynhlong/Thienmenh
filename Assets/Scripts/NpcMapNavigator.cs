using System.Collections.Generic;
using UnityEngine;

public static class NpcMapNavigator
{
    static readonly Dictionary<GameObject, NpcMapZone> knownNpcZones =
        new Dictionary<GameObject, NpcMapZone>();

    public static void ReportNpcZone(GameObject npc, NpcMapZone zone)
    {
        if (npc == null)
        {
            return;
        }

        knownNpcZones[npc] = zone;
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

        NpcMapArea currentArea =
            NpcMapArea.FindArea(npc.transform.position);

        NpcMapZone? currentZone = null;
        if (currentArea != null)
        {
            currentZone = currentArea.zone;
            ReportNpcZone(npc, currentArea.zone);
        }
        else if (knownNpcZones.TryGetValue(npc, out NpcMapZone knownZone))
        {
            currentZone = knownZone;
        }
        else
        {
            NpcMapArea nearestArea =
                NpcMapArea.FindNearestArea(npc.transform.position);
            if (nearestArea != null)
            {
                currentZone = nearestArea.zone;
                ReportNpcZone(npc, nearestArea.zone);
            }
        }

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
            return finalTarget;
        }

        usingTeleportRoute = true;
        routeAction =
            "Đi cổng dịch chuyển đến " +
            GetZoneName(gate.toZone);

        return gate.EntryPosition;
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
                if (gate == null || gate.fromZone != zone)
                {
                    continue;
                }

                if (visited.Contains(gate.toZone))
                {
                    continue;
                }

                NpcTeleportGate firstGate =
                    zone == fromZone
                    ? gate
                    : firstGateByZone[zone];

                if (gate.toZone == targetZone)
                {
                    return firstGate;
                }

                firstGateByZone[gate.toZone] = firstGate;
                visited.Add(gate.toZone);
                queue.Enqueue(gate.toZone);
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
            if (gate != null &&
                gate.fromZone == fromZone &&
                gate.toZone == toZone)
            {
                return gate;
            }
        }

        return null;
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
