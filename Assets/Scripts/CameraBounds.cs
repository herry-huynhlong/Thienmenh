using UnityEngine;

public class CameraBounds : MonoBehaviour
{
    BoxCollider2D currentBounds;

    Camera cam;

    float halfHeight;
    float halfWidth;

    void Start()
    {
        cam = GetComponent<Camera>();

        halfHeight =
            cam.orthographicSize;

        halfWidth =
            halfHeight * cam.aspect;
    }

    void LateUpdate()
    {
        FindBounds();

        if (currentBounds == null)
        {
            return;
        }

        Bounds b =
            currentBounds.bounds;

        Vector3 pos =
            transform.position;

        pos.x =
            Mathf.Clamp(
                pos.x,
                b.min.x + halfWidth,
                b.max.x - halfWidth);

        pos.y =
            Mathf.Clamp(
                pos.y,
                b.min.y + halfHeight,
                b.max.y - halfHeight);

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

