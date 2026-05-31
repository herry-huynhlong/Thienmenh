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

    // cooldown riêng cho từng NPC/player
    private static Dictionary<GameObject, float> teleportCooldowns =
        new Dictionary<GameObject, float>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (targetPoint == null)
        {
            return;
        }

        // chỉ cho NPC/player dùng cửa
        if (!other.CompareTag("NPC") &&
            !other.CompareTag("Player"))
        {
            return;
        }

        Rigidbody2D rb =
            other.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            return;
        }

        // kiểm tra cooldown riêng
        if (teleportCooldowns.TryGetValue(
                other.gameObject,
                out float nextTeleportTime))
        {
            if (Time.time < nextTeleportTime)
            {
                return;
            }
        }

        // teleport
        other.transform.position =
            targetPoint.position;

        // refresh camera bounds nếu có
        if (refreshCameraBounds)
        {
            RefreshCameraBounds();
        }

        // gửi event cho AI tavern nếu có
        SendMessage(
            "OnDoorTeleported",
            other.gameObject,
            SendMessageOptions.DontRequireReceiver);

        // set cooldown riêng cho NPC này
        teleportCooldowns[other.gameObject] =
            Time.time + teleportCooldown;
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