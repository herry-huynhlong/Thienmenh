using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NpcTeleportGate : MonoBehaviour
{
    static readonly List<NpcTeleportGate> gates =
        new List<NpcTeleportGate>();
    static readonly Dictionary<GameObject, float> npcTeleportCooldowns =
        new Dictionary<GameObject, float>();

    public NpcMapZone fromZone = NpcMapZone.Lang;
    public NpcMapZone toZone = NpcMapZone.VanBaoLau;
    public Transform entryPoint;
    public Transform exitPoint;
    public DoorTeleportSameScene sameSceneTeleport;
    public float npcAutoUseRadius = 0.45f;
    public float npcGlobalTeleportCooldown = 1.25f;
    public bool preferOwnTransformWhenEntryIsParent = true;

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
        if (sameSceneTeleport == null ||
            npcAutoUseRadius <= 0f)
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

            GameObject actor = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (!IsNpcActor(actor))
            {
                continue;
            }

            if (npcTeleportCooldowns.TryGetValue(
                    actor,
                    out float nextAllowedTeleport) &&
                Time.time < nextAllowedTeleport)
            {
                continue;
            }

            NpcMapArea area =
                NpcMapArea.FindArea(actor.transform.position);

            if (area != null &&
                area.zone != fromZone)
            {
                continue;
            }

            SyncSameSceneTeleportTarget();
            if (sameSceneTeleport.TryTeleport(actor))
            {
                npcTeleportCooldowns[actor] =
                    Time.time + Mathf.Max(0.1f, npcGlobalTeleportCooldown);
            }
        }
    }

    bool IsNpcActor(GameObject actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (actor.CompareTag("NPC"))
        {
            return true;
        }

        return actor.GetComponent<VillagerAI>() != null ||
            actor.GetComponent<SmartNpcAI>() != null ||
            actor.GetComponent<NpcTradeAgent>() != null ||
            actor.GetComponent<NpcTaskProvider>() != null;
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
