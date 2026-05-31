using UnityEngine;

public class DoorTeleportSameScene : MonoBehaviour
{
    [Header("Teleport Point")]
    public Transform targetPoint;

    [Header("Cooldown")]
    public float teleportCooldown = 1f;

    bool canTeleport = true;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canTeleport || targetPoint == null)
        {
            return;
        }

        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            return;
        }

        other.transform.position = targetPoint.position;
        RefreshCameraBounds(targetPoint.position);
        SendMessage("OnDoorTeleported", other.gameObject, SendMessageOptions.DontRequireReceiver);

        canTeleport = false;
        Invoke(nameof(ResetTeleport), teleportCooldown);
    }


    void RefreshCameraBounds(Vector3 targetPosition)
    {
        MobileCameraController mobileCamera =
            FindAnyObjectByType<MobileCameraController>();

        if (mobileCamera != null)
        {
            mobileCamera.RefreshMapBoundsForPosition(targetPosition);
        }

        CameraBounds cameraBounds =
            FindAnyObjectByType<CameraBounds>();

        if (cameraBounds != null)
        {
            cameraBounds.RefreshBounds();
        }
    }
    void ResetTeleport()
    {
        canTeleport = true;
    }
}
