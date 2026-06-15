using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NpcMapBoundaryClamp : MonoBehaviour
{
    public bool disableMapBoundaryClamp = true;
    public bool forceDisableAtRuntime = true;
    public Collider2D explicitBounds;
    public bool useCurrentNpcMapArea = true;
    public bool useCombinedNpcMapAreas;
    public bool clampToCurrentAreaBounds;
    public bool preferCurrentAreaOverExplicitBounds = true;
    public bool clampToExplicitBoundsWhenOutside;
    public float edgePadding = 0.15f;
    public float checkInterval = 0.1f;

    Rigidbody2D rb;
    NpcMapArea lastArea;
    float checkTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (forceDisableAtRuntime)
        {
            disableMapBoundaryClamp = true;
        }

        RefreshArea();
    }

    void FixedUpdate()
    {
        if (forceDisableAtRuntime ||
            disableMapBoundaryClamp)
        {
            return;
        }

        checkTimer -= Time.fixedDeltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = Mathf.Max(0.02f, checkInterval);
            RefreshArea();
        }

        Vector2 position = rb != null
            ? rb.position
            : (Vector2)transform.position;

        if (useCombinedNpcMapAreas)
        {
            if (NpcMapArea.ContainsAny(position))
            {
                return;
            }

            Vector3 combinedClamped =
                NpcMapArea.ClampToCombinedAreas(
                    position,
                    edgePadding);

            if (Vector2.Distance(combinedClamped, position) > 0.001f)
            {
                if (rb != null)
                {
                    rb.position = combinedClamped;
                    rb.linearVelocity = Vector2.zero;
                }

                transform.position = new Vector3(
                    combinedClamped.x,
                    combinedClamped.y,
                    transform.position.z);
            }

            return;
        }

        Collider2D bounds = GetBounds(position);
        if (bounds == null)
        {
            return;
        }

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

        Vector3 referencePosition = gate != null
            ? gate.ExitPosition
            : transform.position;

        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindArea(referencePosition);
        }

        if (gate != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
            NpcMapArea resolvedArea =
                NpcMapNavigator.ResolveMapAreaAfterTeleport(
                    gameObject,
                    gate.toZone,
                    referencePosition);

            if (resolvedArea != null)
            {
                lastArea = resolvedArea;
            }
        }
        else if (area != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
            lastArea = area;
        }
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

    Collider2D GetBounds(Vector2 position)
    {
        if (explicitBounds != null &&
            IsInside(explicitBounds, position))
        {
            return explicitBounds;
        }

        if (clampToCurrentAreaBounds &&
            preferCurrentAreaOverExplicitBounds &&
            lastArea != null &&
            lastArea.areaBounds != null)
        {
            return lastArea.areaBounds;
        }

        if (explicitBounds != null &&
            clampToExplicitBoundsWhenOutside)
        {
            return explicitBounds;
        }

        return clampToCurrentAreaBounds &&
            lastArea != null
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
