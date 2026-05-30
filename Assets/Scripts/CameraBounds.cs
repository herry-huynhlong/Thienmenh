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
        if (currentBounds != null)
        {
            return;
        }

        GameObject obj =
            GameObject.Find("MapBounds");

        if (obj == null)
        {
            return;
        }

        currentBounds =
            obj.GetComponent<BoxCollider2D>();
    }

    public void RefreshBounds()
    {
        currentBounds = null;
    }
}