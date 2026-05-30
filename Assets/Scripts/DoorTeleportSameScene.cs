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
        SendMessage("OnDoorTeleported", other.gameObject, SendMessageOptions.DontRequireReceiver);

        canTeleport = false;
        Invoke(nameof(ResetTeleport), teleportCooldown);
    }

    void ResetTeleport()
    {
        canTeleport = true;
    }
}
