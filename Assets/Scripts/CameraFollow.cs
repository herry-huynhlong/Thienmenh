using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 newPosition = target.position;
        newPosition.z = -10f;

        transform.position = Vector3.Lerp(
            transform.position,
            newPosition,
            smoothSpeed * Time.deltaTime
        );
    }
}