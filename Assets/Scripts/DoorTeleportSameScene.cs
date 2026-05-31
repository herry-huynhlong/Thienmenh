using System.Collections.Generic;
using UnityEngine;

public class DoorTeleportSameScene : MonoBehaviour
{
    [Header("Teleport")]
    public Transform targetPoint;

    [Header("Cooldown")]
    public float teleportCooldown = 5f;

    [Header("Camera")]
    public bool refreshCameraBounds = true;

    static readonly Dictionary<GameObject, float> teleportCooldowns =
        new Dictionary<GameObject, float>();

    void OnTriggerEnter2D(Collider2D other)
    {
        TryTeleport(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryTeleport(other);
    }

    public bool TryTeleport(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        return TryTeleport(other.gameObject);
    }

    public bool TryTeleport(GameObject actor)
    {
        if (targetPoint == null ||
            actor == null)
        {
            return false;
        }

        if (!actor.CompareTag("NPC") &&
            !actor.CompareTag("Player"))
        {
            return false;
        }

        Rigidbody2D rb =
            actor.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            return false;
        }

        if (teleportCooldowns.TryGetValue(
                actor,
                out float nextTeleportTime) &&
            Time.time < nextTeleportTime)
        {
            return false;
        }

        rb.position = targetPoint.position;
        rb.linearVelocity = Vector2.zero;
        actor.transform.position = targetPoint.position;

        if (refreshCameraBounds)
        {
            RefreshCameraBounds();
        }

        SendMessage(
            "OnDoorTeleported",
            actor,
            SendMessageOptions.DontRequireReceiver);

        actor.SendMessage(
            "OnNpcMapTeleported",
            gameObject,
            SendMessageOptions.DontRequireReceiver);

        teleportCooldowns[actor] =
            Time.time + teleportCooldown;

        return true;
    }

    void RefreshCameraBounds()
    {
        CameraBounds bounds =
            FindFirstObjectByType<CameraBounds>();

        if (bounds != null)
        {
            bounds.RefreshBounds();
        }
    }
}