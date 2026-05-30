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

    public Vector3 GetRandomPoint()
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

            if (IsBlocked(point))
            {
                continue;
            }

            return point;
        }

        return transform.position;
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

    bool IsBlocked(Vector3 point)
    {
        if (blockedLayers.value == 0 || blockedCheckRadius <= 0f)
        {
            return false;
        }

        Collider2D hit = Physics2D.OverlapCircle(point, blockedCheckRadius, blockedLayers);
        return hit != null && !hit.isTrigger;
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