using System.Collections.Generic;
using UnityEngine;

public enum NpcMapZone
{
    Lang,
    VanBaoLau,
    MaThuSonMach,
    BichAnh
}

[RequireComponent(typeof(Collider2D))]
public class NpcMapArea : MonoBehaviour
{
    static readonly List<NpcMapArea> areas =
        new List<NpcMapArea>();

    public NpcMapZone zone = NpcMapZone.Lang;
    public string displayName = "Làng";
    public Collider2D areaBounds;
    public Transform movementCenterPoint;

    public static IReadOnlyList<NpcMapArea> Areas => areas;

    void Reset()
    {
        areaBounds = GetComponent<Collider2D>();
        if (areaBounds != null)
        {
            areaBounds.isTrigger = true;
        }
    }

    void Awake()
    {
        if (areaBounds == null)
        {
            areaBounds = GetComponent<Collider2D>();
        }

        if (areaBounds != null)
        {
            areaBounds.isTrigger = true;
        }
    }

    void OnEnable()
    {
        if (!areas.Contains(this))
        {
            areas.Add(this);
        }
    }

    void OnDisable()
    {
        areas.Remove(this);
    }

    public bool Contains(Vector3 position)
    {
        if (areaBounds == null)
        {
            return false;
        }

        Vector2 closest = areaBounds.ClosestPoint(position);
        return Vector2.Distance(closest, position) <= 0.02f;
    }

    public float DistanceTo(Vector3 position)
    {
        if (areaBounds == null)
        {
            return float.PositiveInfinity;
        }

        Vector2 closest = areaBounds.ClosestPoint(position);
        return Vector2.Distance(closest, position);
    }

    public Vector3 ClosestPoint(Vector3 position)
    {
        if (areaBounds == null)
        {
            return position;
        }

        Vector2 closest = areaBounds.ClosestPoint(position);
        return new Vector3(closest.x, closest.y, position.z);
    }

    public Vector3 GetMovementCenter(Vector3 fallbackPosition)
    {
        if (movementCenterPoint != null)
        {
            Vector3 point = movementCenterPoint.position;
            if (areaBounds == null || Contains(point))
            {
                return point;
            }

            return ClosestPoint(point);
        }

        if (areaBounds != null)
        {
            return areaBounds.bounds.center;
        }

        return fallbackPosition;
    }

    public static NpcMapArea FindArea(Vector3 position)
    {
        foreach (NpcMapArea area in areas)
        {
            if (area != null && area.Contains(position))
            {
                return area;
            }
        }

        return null;
    }

    public static NpcMapArea FindAreaByZone(NpcMapZone zone)
    {
        foreach (NpcMapArea area in areas)
        {
            if (area != null && area.zone == zone)
            {
                return area;
            }
        }

        return null;
    }

    public static NpcMapArea FindNearestAreaInZone(
        NpcMapZone zone,
        Vector3 position)
    {
        NpcMapArea bestArea = null;
        float bestDistance = float.PositiveInfinity;

        foreach (NpcMapArea area in areas)
        {
            if (area == null || area.zone != zone)
            {
                continue;
            }

            float distance = area.DistanceTo(position);
            if (distance < bestDistance)
            {
                bestArea = area;
                bestDistance = distance;
            }
        }

        return bestArea;
    }

    public static NpcMapArea FindNearestArea(Vector3 position)
    {
        NpcMapArea bestArea = null;
        float bestDistance = float.PositiveInfinity;

        foreach (NpcMapArea area in areas)
        {
            if (area == null)
            {
                continue;
            }

            float distance = area.DistanceTo(position);
            if (distance < bestDistance)
            {
                bestArea = area;
                bestDistance = distance;
            }
        }

        return bestArea;
    }

    public static bool ContainsAny(Vector3 position)
    {
        return FindArea(position) != null;
    }

    public static Vector3 ClosestPointInAnyArea(Vector3 position)
    {
        NpcMapArea bestArea = null;
        float bestDistance = float.PositiveInfinity;

        foreach (NpcMapArea area in areas)
        {
            if (area == null ||
                area.areaBounds == null)
            {
                continue;
            }

            float distance = area.DistanceTo(position);
            if (distance < bestDistance)
            {
                bestArea = area;
                bestDistance = distance;
            }
        }

        return bestArea != null
            ? bestArea.ClosestPoint(position)
            : position;
    }

    public static Vector3 ClampToCombinedAreas(
        Vector3 position,
        float padding = 0f)
    {
        if (ContainsAny(position))
        {
            return position;
        }

        Vector3 closest = ClosestPointInAnyArea(position);
        if (padding <= 0f)
        {
            return closest;
        }

        NpcMapArea nearest = FindNearestArea(position);
        if (nearest == null ||
            nearest.areaBounds == null)
        {
            return closest;
        }

        Vector2 inward =
            (Vector2)nearest.areaBounds.bounds.center -
            (Vector2)closest;

        if (inward.sqrMagnitude <= 0.0001f)
        {
            return closest;
        }

        Vector2 padded = (Vector2)closest + inward.normalized * padding;
        return new Vector3(padded.x, padded.y, position.z);
    }
}
