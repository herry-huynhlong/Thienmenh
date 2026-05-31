using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NpcTeleportGate : MonoBehaviour
{
    static readonly List<NpcTeleportGate> gates =
        new List<NpcTeleportGate>();

    public NpcMapZone fromZone = NpcMapZone.Lang;
    public NpcMapZone toZone = NpcMapZone.VanBaoLau;
    public Transform entryPoint;
    public Transform exitPoint;
    public DoorTeleportSameScene sameSceneTeleport;
    public float npcAutoUseRadius = 0.45f;

    public static IReadOnlyList<NpcTeleportGate> Gates => gates;

    public Vector3 EntryPosition =>
        entryPoint != null ? entryPoint.position : transform.position;

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

            if (actor == null ||
                !actor.CompareTag("NPC"))
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

            sameSceneTeleport.TryTeleport(actor);
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(EntryPosition, Mathf.Max(0f, npcAutoUseRadius));
    }
}