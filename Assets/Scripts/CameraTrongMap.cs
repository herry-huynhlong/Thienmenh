using UnityEngine;

public class CameraTrongMap : MonoBehaviour
{
    [Header("Player")]
    public Transform target;

    [Header("Giới hạn map")]
    public BoxCollider2D mapBounds;

    Camera cam;

    float halfHeight;
    float halfWidth;

    void Start()
    {
        cam =
            GetComponent<Camera>();

        halfHeight =
            cam.orthographicSize;

        halfWidth =
            halfHeight *
            cam.aspect;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Bounds bounds =
            mapBounds.bounds;

        float clampX =
            Mathf.Clamp(
                target.position.x,
                bounds.min.x + halfWidth,
                bounds.max.x - halfWidth);

        float clampY =
            Mathf.Clamp(
                target.position.y,
                bounds.min.y + halfHeight,
                bounds.max.y - halfHeight);

        transform.position =
            new Vector3(
                clampX,
                clampY,
                -10f);
    }
}