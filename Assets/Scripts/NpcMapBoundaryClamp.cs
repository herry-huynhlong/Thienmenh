using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NpcMapBoundaryClamp : MonoBehaviour
{
    public Collider2D explicitBounds;
    public bool useCurrentNpcMapArea = true;
    public bool preferCurrentAreaOverExplicitBounds = true;
    public float edgePadding = 0.15f;
    public float checkInterval = 0.1f;

    Rigidbody2D rb;
    NpcMapArea lastArea;
    float checkTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        RefreshArea();
    }

    void FixedUpdate()
    {
        checkTimer -= Time.fixedDeltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = Mathf.Max(0.02f, checkInterval);
            RefreshArea();
        }

        Collider2D bounds = GetBounds();
        if (bounds == null)
        {
            return;
        }

        Vector2 position = rb != null
            ? rb.position
            : (Vector2)transform.position;

        if (IsInside(bounds, position))
        {
            return;
        }

        Vector2 clamped = ClampToBounds(bounds, position);

        if (rb != null)
        {
            rb.position = clamped;
            rb.linearVelocity = Vector2.zero;
        }

        transform.position = new Vector3(
            clamped.x,
            clamped.y,
            transform.position.z);
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;

        NpcMapArea area = gate != null
            ? FindAreaByZone(gate.toZone)
            : NpcMapArea.FindArea(transform.position);

        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(transform.position);
        }

        if (area != null)
        {
            lastArea = area;
        }
    }

    NpcMapArea FindAreaByZone(NpcMapZone zone)
    {
        foreach (NpcMapArea area in NpcMapArea.Areas)
        {
            if (area != null && area.zone == zone)
            {
                return area;
            }
        }

        return null;
    }
    void RefreshArea()
    {
        if (!useCurrentNpcMapArea)
        {
            return;
        }

        NpcMapArea area =
            NpcMapArea.FindArea(transform.position);

        if (area != null)
        {
            lastArea = area;
        }
    }

    Collider2D GetBounds()
    {
        if (preferCurrentAreaOverExplicitBounds &&
            lastArea != null &&
            lastArea.areaBounds != null)
        {
            return lastArea.areaBounds;
        }

        if (explicitBounds != null)
        {
            return explicitBounds;
        }

        return lastArea != null
            ? lastArea.areaBounds
            : null;
    }

    bool IsInside(Collider2D bounds, Vector2 position)
    {
        Vector2 closest = bounds.ClosestPoint(position);
        return Vector2.Distance(closest, position) <= 0.02f;
    }

    Vector2 ClampToBounds(Collider2D bounds, Vector2 position)
    {
        Bounds aabb = bounds.bounds;
        float padding = Mathf.Max(0f, edgePadding);

        Vector2 candidate = new Vector2(
            Mathf.Clamp(position.x, aabb.min.x + padding, aabb.max.x - padding),
            Mathf.Clamp(position.y, aabb.min.y + padding, aabb.max.y - padding));

        if (IsInside(bounds, candidate))
        {
            return candidate;
        }

        Vector2 closest = bounds.ClosestPoint(candidate);
        Vector2 center = aabb.center;
        Vector2 inward = center - closest;

        if (inward.sqrMagnitude <= 0.0001f)
        {
            return closest;
        }

        return closest + inward.normalized * padding;
    }
}