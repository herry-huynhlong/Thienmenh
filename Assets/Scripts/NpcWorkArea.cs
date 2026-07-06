using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NpcWorkArea : MonoBehaviour
{
    public VillagerJob job = VillagerJob.Farmer;
    public bool useColliderBounds = true;
    public Vector2 fallbackSize = new Vector2(2f, 2f);
    public int maxPickAttempts = 16;
    public LayerMask blockedLayers = 0;
    public float blockedCheckRadius = 0.15f;

    Collider2D areaCollider;

    void Awake()
    {
        areaCollider = GetComponent<Collider2D>();
        if (areaCollider != null)
        {
            areaCollider.isTrigger = true;
        }
    }

    void OnValidate()
    {
        fallbackSize.x = Mathf.Max(0.1f, fallbackSize.x);
        fallbackSize.y = Mathf.Max(0.1f, fallbackSize.y);
        maxPickAttempts = Mathf.Max(1, maxPickAttempts);
        blockedCheckRadius = Mathf.Max(0f, blockedCheckRadius);

        Collider2D hit = GetComponent<Collider2D>();
        if (hit != null)
        {
            hit.isTrigger = true;
        }
    }

    public Vector3 GetRandomPoint(GameObject npc = null)
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<Collider2D>();
        }

        for (int i = 0; i < maxPickAttempts; i++)
        {
            Vector3 point = PickPointInBounds();
            if (areaCollider != null && !areaCollider.OverlapPoint(point))
            {
                continue;
            }

            if (IsBlocked(point, npc))
            {
                continue;
            }

            return point;
        }

        return FindFallbackPoint(npc);
    }

    Vector3 PickPointInBounds()
    {
        if (useColliderBounds && areaCollider != null)
        {
            Bounds bounds = areaCollider.bounds;
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                transform.position.z);
        }

        Vector2 half = fallbackSize * 0.5f;
        return transform.position + new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            0f);
    }

    Vector3 FindFallbackPoint(GameObject npc)
    {
        Vector3 center = areaCollider != null
            ? areaCollider.bounds.center
            : transform.position;
        center.z = transform.position.z;

        if ((areaCollider == null || areaCollider.OverlapPoint(center)) &&
            !IsBlocked(center, npc))
        {
            return center;
        }

        float baseRadius = Mathf.Max(
            blockedCheckRadius * 2f,
            0.2f);
        const int rings = 4;
        const int samplesPerRing = 8;

        for (int ring = 1; ring <= rings; ring++)
        {
            float radius = baseRadius * ring;
            for (int sample = 0; sample < samplesPerRing; sample++)
            {
                float angle =
                    (Mathf.PI * 2f * sample) / samplesPerRing;
                Vector3 candidate =
                    center +
                    new Vector3(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle),
                        0f) * radius;

                if (areaCollider != null &&
                    !areaCollider.OverlapPoint(candidate))
                {
                    continue;
                }

                if (IsBlocked(candidate, npc))
                {
                    continue;
                }

                return candidate;
            }
        }

        return center;
    }

    bool IsBlocked(Vector3 point, GameObject npc)
    {
        if (blockedCheckRadius <= 0f)
        {
            return false;
        }

        if (blockedLayers.value != 0)
        {
            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    point,
                    blockedCheckRadius,
                    blockedLayers);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null ||
                    hit.isTrigger ||
                    (npc != null && hit.transform.IsChildOf(npc.transform)))
                {
                    continue;
                }

                return true;
            }
        }

        Collider2D[] occupants =
            Physics2D.OverlapCircleAll(
                point,
                blockedCheckRadius);

        for (int i = 0; i < occupants.Length; i++)
        {
            Collider2D occupant = occupants[i];
            if (occupant == null ||
                occupant.isTrigger)
            {
                continue;
            }

            Transform root = occupant.transform.root;
            if (npc != null &&
                (root == npc.transform ||
                occupant.transform.IsChildOf(npc.transform)))
            {
                continue;
            }

            if (root.GetComponent<VillagerAI>() != null ||
                root.GetComponent<SmartNpcAI>() != null)
            {
                return true;
            }
        }

        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.25f);
        Collider2D hit = GetComponent<Collider2D>();
        if (hit != null)
        {
            Gizmos.DrawCube(hit.bounds.center, hit.bounds.size);
            return;
        }

        Gizmos.DrawCube(transform.position, fallbackSize);
    }
}
