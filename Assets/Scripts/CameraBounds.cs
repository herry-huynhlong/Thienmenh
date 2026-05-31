using UnityEngine;

public class CameraBounds : MonoBehaviour
{
    BoxCollider2D currentBounds;
    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        FindBounds();

        if (currentBounds == null || cam == null)
        {
            return;
        }

        ClampZoomToBounds();
        ClampPositionToBounds();
    }

    void ClampZoomToBounds()
    {
        Bounds b = currentBounds.bounds;
        float maxByHeight = b.size.y * 0.5f;
        float maxByWidth = b.size.x / (2f * cam.aspect);
        float allowedSize = Mathf.Max(0.1f, Mathf.Min(maxByHeight, maxByWidth));

        if (cam.orthographicSize > allowedSize)
        {
            cam.orthographicSize = allowedSize;
        }
    }

    void ClampPositionToBounds()
    {
        Bounds b = currentBounds.bounds;
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        Vector3 pos = transform.position;

        float minX = b.min.x + halfWidth;
        float maxX = b.max.x - halfWidth;
        float minY = b.min.y + halfHeight;
        float maxY = b.max.y - halfHeight;

        pos.x = minX <= maxX
            ? Mathf.Clamp(pos.x, minX, maxX)
            : b.center.x;

        pos.y = minY <= maxY
            ? Mathf.Clamp(pos.y, minY, maxY)
            : b.center.y;

        transform.position = pos;
    }

    void FindBounds()
    {
        if (currentBounds != null &&
            ContainsXY(currentBounds.bounds, transform.position))
        {
            return;
        }

        BoxCollider2D[] colliders =
            FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Exclude);

        BoxCollider2D bestBounds = null;
        float bestDistance = float.PositiveInfinity;

        foreach (BoxCollider2D collider in colliders)
        {
            if (collider == null ||
                collider.name != "MapBounds" ||
                !collider.gameObject.scene.IsValid() ||
                !collider.gameObject.scene.isLoaded)
            {
                continue;
            }

            if (ContainsXY(collider.bounds, transform.position))
            {
                bestBounds = collider;
                break;
            }

            Vector3 closest = collider.bounds.ClosestPoint(transform.position);
            float distance =
                ((Vector2)closest - (Vector2)transform.position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestBounds = collider;
            }
        }

        currentBounds = bestBounds;
    }

    bool ContainsXY(Bounds bounds, Vector3 position)
    {
        return position.x >= bounds.min.x &&
            position.x <= bounds.max.x &&
            position.y >= bounds.min.y &&
            position.y <= bounds.max.y;
    }

    public void RefreshBounds()
    {
        currentBounds = null;
        FindBounds();
    }
}