using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NpcTeleportGate : MonoBehaviour
{
    static readonly List<NpcTeleportGate> gates =
        new List<NpcTeleportGate>();
    static readonly Dictionary<int, float> npcTeleportCooldowns =
        new Dictionary<int, float>();

    public NpcMapZone fromZone = NpcMapZone.Lang;
    public NpcMapZone toZone = NpcMapZone.VanBaoLau;
    public Transform entryPoint;
    public Transform exitPoint;
    public DoorTeleportSameScene sameSceneTeleport;
    public float npcAutoUseRadius = 0.45f;
    public float npcGlobalTeleportCooldown = 10f;
    public bool preferOwnTransformWhenEntryIsParent = true;
    public bool useEntryPointForNpcRoute;

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

    Vector3 GetResolvedEntryPosition()
    {
        Transform resolved = GetResolvedEntryTransform();
        return resolved != null
            ? resolved.position
            : transform.position;
    }

    Transform GetResolvedEntryTransform()
    {
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

        if (useEntryPointForNpcRoute || entryPoint != transform)
        {
            return entryPoint;
        }

        return transform;
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

    void Update()
    {
        if (npcAutoUseRadius <= 0f)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            EntryPosition,
            npcAutoUseRadius);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            GameObject actor = ResolveActorRoot(hit);

            if (!IsNpcActor(actor))
            {
                continue;
            }

            ProcessNpcAtGate(actor);
        }
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

        return actorZone.Value == fromZone;
    }

    static int GetNpcCooldownKey(GameObject actor)
    {
        return actor != null ? actor.GetInstanceID() : 0;
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

        SyncSameSceneTeleportTarget();
        if (sameSceneTeleport != null &&
            sameSceneTeleport.TryTeleport(actor, true))
        {
            return true;
        }

        Vector3 targetPosition = ExitPosition;
        Rigidbody2D rb = actor.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = targetPosition;
            rb.linearVelocity = Vector2.zero;
        }

        actor.transform.position = targetPosition;
        actor.SendMessage(
            "OnNpcMapTeleported",
            gameObject,
            SendMessageOptions.DontRequireReceiver);

        return true;
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(EntryPosition, Mathf.Max(0f, npcAutoUseRadius));
    }
}
