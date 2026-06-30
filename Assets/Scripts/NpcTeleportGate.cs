using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NpcTeleportGate : MonoBehaviour
{
    static readonly List<NpcTeleportGate> gates =
        new List<NpcTeleportGate>();
    static readonly Dictionary<int, float> npcTeleportCooldowns =
        new Dictionary<int, float>();
    static readonly Dictionary<int, float> npcTeleportReentryLocks =
        new Dictionary<int, float>();

    public NpcMapZone fromZone = NpcMapZone.Lang;
    public NpcMapZone toZone = NpcMapZone.VanBaoLau;
    public Transform entryPoint;
    public Transform exitPoint;
    public DoorTeleportSameScene sameSceneTeleport;
    [HideInInspector]
    public float npcAutoUseRadius = 0.45f;
    public float npcGlobalTeleportCooldown = 10f;
    public float npcReentryLockDuration = 1.5f;
    public bool preferOwnTransformWhenEntryIsParent = true;
    public bool useEntryPointForNpcRoute;
    public bool bidirectional = true;

    public static IReadOnlyList<NpcTeleportGate> Gates => gates;

    public Vector3 EntryPosition => GetResolvedEntryPosition();

    public Vector3 ExitPosition
    {
        get
        {
            if (exitPoint != null)
            {
                return exitPoint.position;
            }

            if (sameSceneTeleport != null &&
                sameSceneTeleport.targetPoint != null)
            {
                return sameSceneTeleport.targetPoint.position;
            }

            return EntryPosition;
        }
    }

    public bool TryGetOtherZone(
        NpcMapZone zone,
        out NpcMapZone otherZone)
    {
        if (zone == fromZone)
        {
            otherZone = toZone;
            return true;
        }

        if (bidirectional && zone == toZone)
        {
            otherZone = fromZone;
            return true;
        }

        otherZone = default;
        return false;
    }

    public bool Connects(NpcMapZone zoneA, NpcMapZone zoneB)
    {
        if (fromZone == zoneA && toZone == zoneB)
        {
            return true;
        }

        return bidirectional &&
            fromZone == zoneB &&
            toZone == zoneA;
    }

    public bool TryGetTeleportRouteForZone(
        NpcMapZone zone,
        out Vector3 entryPosition,
        out Vector3 exitPosition,
        out NpcMapZone destinationZone)
    {
        entryPosition = EntryPosition;
        exitPosition = ExitPosition;
        destinationZone = toZone;

        if (zone == fromZone)
        {
            return true;
        }

        if (bidirectional && zone == toZone)
        {
            entryPosition = ExitPosition;
            exitPosition = EntryPosition;
            destinationZone = fromZone;
            return true;
        }

        return false;
    }

    public Vector3 GetApproachPosition(Vector3 actorPosition)
    {
        if (useEntryPointForNpcRoute)
        {
            return EntryPosition;
        }

        return EntryPosition;
    }

    Vector3 GetResolvedEntryPosition()
    {
        if (!useEntryPointForNpcRoute)
        {
            Collider2D gateCollider = GetComponent<Collider2D>();
            if (gateCollider != null)
            {
                Vector3 center = gateCollider.bounds.center;
                center.z = transform.position.z;
                return center;
            }
        }

        Transform resolved = GetResolvedEntryTransform();
        return resolved != null
            ? resolved.position
            : transform.position;
    }

    Transform GetResolvedEntryTransform()
    {
        if (!useEntryPointForNpcRoute)
        {
            return transform;
        }

        if (entryPoint == null)
        {
            return transform;
        }

        if (preferOwnTransformWhenEntryIsParent &&
            transform.parent != null &&
            entryPoint == transform.parent)
        {
            return transform;
        }

        return entryPoint;
    }
    void Reset()
    {
        sameSceneTeleport = GetComponent<DoorTeleportSameScene>();

        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    void Awake()
    {
        if (sameSceneTeleport == null)
        {
            sameSceneTeleport = GetComponent<DoorTeleportSameScene>();
        }

        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }

        SyncSameSceneTeleportTarget();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sameSceneTeleport == null)
        {
            sameSceneTeleport = GetComponent<DoorTeleportSameScene>();
        }

        SyncSameSceneTeleportTarget();
    }
#endif

    void SyncSameSceneTeleportTarget()
    {
        if (sameSceneTeleport == null ||
            exitPoint == null)
        {
            return;
        }

        sameSceneTeleport.targetPoint = exitPoint;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        ProcessNpcAtGate(ResolveActorRoot(other));
    }

    void OnTriggerStay2D(Collider2D other)
    {
        ProcessNpcAtGate(ResolveActorRoot(other));
    }

    void ProcessNpcAtGate(GameObject actor)
    {
        if (!IsNpcActor(actor))
        {
            return;
        }

        if (!CanNpcUseGate(actor))
        {
            return;
        }

        int cooldownKey = GetNpcCooldownKey(actor);
        if (IsNpcReentryLocked(cooldownKey))
        {
            return;
        }

        if (npcTeleportCooldowns.TryGetValue(
                cooldownKey,
                out float nextAllowedTeleport) &&
            Time.time < nextAllowedTeleport)
        {
            return;
        }

        if (TryTeleportNpc(actor))
        {
            npcTeleportCooldowns[cooldownKey] =
                Time.time + Mathf.Max(0.1f, npcGlobalTeleportCooldown);
            npcTeleportReentryLocks[cooldownKey] =
                Time.time + Mathf.Max(0.1f, npcReentryLockDuration);
        }
    }

    bool IsNpcActor(GameObject actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (actor.tag == "NPC")
        {
            return true;
        }

        return actor.GetComponent<VillagerAI>() != null ||
            actor.GetComponent<SmartNpcAI>() != null ||
            actor.GetComponent<NpcTradeAgent>() != null ||
            actor.GetComponent<NpcTaskProvider>() != null;
    }

    bool CanNpcUseGate(GameObject actor)
    {
        if (actor == null)
        {
            return false;
        }

        NpcMapZone? actorZone = NpcMapNavigator.ResolveActorZone(actor);
        if (!actorZone.HasValue)
        {
            return false;
        }

        return TryGetTeleportRouteForZone(
            actorZone.Value,
            out _,
            out _,
            out _);
    }

    static int GetNpcCooldownKey(GameObject actor)
    {
        return actor != null ? actor.GetInstanceID() : 0;
    }

    static bool IsNpcReentryLocked(int cooldownKey)
    {
        if (npcTeleportReentryLocks.TryGetValue(
                cooldownKey,
                out float reentryUnlockedAt) &&
            Time.time < reentryUnlockedAt)
        {
            return true;
        }

        if (npcTeleportReentryLocks.ContainsKey(cooldownKey) &&
            Time.time >= npcTeleportReentryLocks[cooldownKey])
        {
            npcTeleportReentryLocks.Remove(cooldownKey);
        }

        return false;
    }

    GameObject ResolveActorRoot(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        if (hit.attachedRigidbody != null)
        {
            return hit.attachedRigidbody.gameObject;
        }

        VillagerAI villager = hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc = hit.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        NpcMapMover2D mover = hit.GetComponentInParent<NpcMapMover2D>();
        if (mover != null)
        {
            return mover.gameObject;
        }

        return hit.gameObject;
    }

    bool TryTeleportNpc(GameObject actor)
    {
        if (actor == null)
        {
            return false;
        }

        NpcMapZone? actorZone = NpcMapNavigator.ResolveActorZone(actor);
        if (!actorZone.HasValue)
        {
            return false;
        }

        if (!TryGetTeleportRouteForZone(
                actorZone.Value,
                out _,
                out Vector3 exitPosition,
                out NpcMapZone destinationZone))
        {
            return false;
        }

        Vector3 targetPosition = exitPosition;
        Rigidbody2D rb = actor.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = targetPosition;
            rb.linearVelocity = Vector2.zero;
        }

        actor.transform.position = targetPosition;
        NpcMapNavigator.LockNpcZone(actor, destinationZone, 3f);

        if (sameSceneTeleport == null ||
            sameSceneTeleport.refreshCameraBounds)
        {
            RefreshCameraBounds();
        }

        actor.SendMessage(
            "OnNpcMapTeleported",
            gameObject,
            SendMessageOptions.DontRequireReceiver);

        return true;
    }

    void RefreshCameraBounds()
    {
        CameraBounds bounds = FindAnyObjectByType<CameraBounds>();
        if (bounds != null)
        {
            bounds.RefreshBounds();
        }
    }

    void OnEnable()
    {
        if (!gates.Contains(this))
        {
            gates.Add(this);
        }
    }

    void OnDisable()
    {
        gates.Remove(this);
    }

}
