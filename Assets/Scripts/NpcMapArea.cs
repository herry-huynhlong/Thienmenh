using System.Collections.Generic;
using UnityEngine;

public enum NpcMapZone
{
    Lang,
    VanBaoLau,
    MaThuSonMach
}

[RequireComponent(typeof(Collider2D))]
public class NpcMapArea : MonoBehaviour
{
    static readonly List<NpcMapArea> areas =
        new List<NpcMapArea>();

    public NpcMapZone zone = NpcMapZone.Lang;
    public string displayName = "Làng";
    public Collider2D areaBounds;

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
}
