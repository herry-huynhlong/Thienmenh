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
        return TryTeleport(actor, false);
    }

    public bool TryTeleport(GameObject actor, bool allowNpcFromGate)
    {
        actor = ResolveActorRoot(actor);

        if (targetPoint == null ||
            actor == null)
        {
            return false;
        }

        if (!IsTeleportActor(actor))
        {
            return false;
        }

        if (!allowNpcFromGate &&
            GetComponent<NpcTeleportGate>() != null &&
            IsNpcActor(actor))
        {
            return false;
        }

        Rigidbody2D rb =
            actor.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            return false;
        }

        if (!allowNpcFromGate &&
            teleportCooldowns.TryGetValue(
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

    bool IsTeleportActor(GameObject actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (actor.CompareTag("Player") ||
            actor.tag == "NPC")
        {
            return true;
        }

        return IsNpcActor(actor);
    }

    bool IsNpcActor(GameObject actor)
    {
        if (actor == null)
        {
            return false;
        }

        return actor.GetComponentInParent<VillagerAI>() != null ||
            actor.GetComponentInParent<SmartNpcAI>() != null ||
            actor.GetComponentInParent<NpcTradeAgent>() != null ||
            actor.GetComponentInParent<NpcTaskProvider>() != null;
    }

    GameObject ResolveActorRoot(GameObject actor)
    {
        if (actor == null)
        {
            return null;
        }

        Rigidbody2D rb = actor.GetComponentInParent<Rigidbody2D>();
        if (rb != null)
        {
            return rb.gameObject;
        }

        VillagerAI villager = actor.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc = actor.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        NpcMapMover2D mover = actor.GetComponentInParent<NpcMapMover2D>();
        if (mover != null)
        {
            return mover.gameObject;
        }

        return actor;
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
