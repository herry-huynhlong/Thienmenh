using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class NpcTeleportGate : MonoBehaviour
{
    static readonly List<NpcTeleportGate> gates =
        new List<NpcTeleportGate>();
    // Cooldowns must be scoped to one actor + one gate. An actor-only key
    // blocks the next gate in a valid multi-hop route and leaves the NPC
    // standing at that gate until the global cooldown expires.
    static readonly Dictionary<long, float> npcTeleportCooldowns =
        new Dictionary<long, float>();
    static readonly Dictionary<long, float> npcTeleportReentryLocks =
        new Dictionary<long, float>();
    static readonly Dictionary<int, float> npcGateDebugTimes =
        new Dictionary<int, float>();
    static readonly Dictionary<int, string> npcGateDebugSignatures =
        new Dictionary<int, string>();

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
    public bool debugNpcGateLogs;

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
        NpcMapArea actorArea =
            NpcMapArea.FindArea(actorPosition);
        if (actorArea != null)
        {
            return GetApproachPositionForZone(actorArea.zone);
        }

        Transform routeEntry = GetRouteEntryTransform();
        if (routeEntry != null)
        {
            return routeEntry.position;
        }

        return EntryPosition;
    }

    public Vector3 GetApproachPositionForZone(NpcMapZone zone)
    {
        if (TryGetTeleportRouteForZone(
                zone,
                out Vector3 entryPosition,
                out _,
                out _))
        {
            return entryPosition;
        }

        return EntryPosition;
    }

    public bool TryForceNpcUse(GameObject actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (!NpcGateTravelPolicy.AllowsAutomaticGateTravel(actor))
        {
            LogGateDebug(actor, "ForceTeleport", "gateTravelDisabled");
            return false;
        }

        NpcMapZone? actorZone = NpcMapNavigator.ResolveActorZone(actor);
        if (!actorZone.HasValue)
        {
            LogGateDebug(actor, "ForceTeleport", "actorZone=None");
            return false;
        }

        if (!TryGetTeleportRouteForZone(
                actorZone.Value,
                out _,
                out Vector3 exitPosition,
                out NpcMapZone destinationZone))
        {
            LogGateDebug(
                actor,
                "ForceTeleport",
                "noRoute actorZone=" +
                actorZone.Value +
                " from=" +
                fromZone +
                " to=" +
                toZone);
            return false;
        }

        if (!CanNpcUseGate(actor))
        {
            LogGateDebug(
                actor,
                "ForceTeleport",
                "canUse=false actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone);
            return false;
        }

        long cooldownKey = GetNpcCooldownKey(actor);
        if (IsNpcReentryLocked(cooldownKey, actor))
        {
            LogGateDebug(
                actor,
                "ForceTeleport",
                "reentryLocked actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone);
            return false;
        }

        if (npcTeleportCooldowns.TryGetValue(
                cooldownKey,
                out float nextAllowedTeleport) &&
            Time.time < nextAllowedTeleport)
        {
            LogGateDebug(
                actor,
                "ForceTeleport",
                "cooldown actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone +
                " nextAllowed=" +
                nextAllowedTeleport.ToString("0.00") +
                " now=" +
                Time.time.ToString("0.00"));
            return false;
        }

        if (!TryTeleportNpc(actor))
        {
            LogGateDebug(
                actor,
                "ForceTeleport",
                "failed actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone +
                " exit=" +
                exitPosition);
            return false;
        }

        npcTeleportCooldowns[cooldownKey] =
            Time.time + Mathf.Max(0.1f, npcGlobalTeleportCooldown);
        npcTeleportReentryLocks[cooldownKey] =
            Time.time + Mathf.Max(4f, npcReentryLockDuration);
        LogGateDebug(
            actor,
            "ForceTeleport",
            "success actorZone=" +
            actorZone.Value +
            " routeTo=" +
            destinationZone +
            " exit=" +
            exitPosition);
        return true;
    }

    Vector3 GetResolvedEntryPosition()
    {
        Transform routeEntry = GetRouteEntryTransform();
        if (routeEntry != null)
        {
            return routeEntry.position;
        }

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

        return transform.position;
    }

    Transform GetRouteEntryTransform()
    {
        if (entryPoint == null)
        {
            return null;
        }

        if (preferOwnTransformWhenEntryIsParent &&
            transform.parent != null &&
            entryPoint == transform.parent)
        {
            return transform;
        }

        return entryPoint;
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

        if (exitPoint == null &&
            (sameSceneTeleport == null ||
             sameSceneTeleport.targetPoint == null))
        {
            Debug.LogWarning(
                "[NpcTeleportGate] " +
                name +
                " is missing both exitPoint and sameSceneTeleport.targetPoint.",
                this);
        }
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

        if (!NpcGateTravelPolicy.AllowsAutomaticGateTravel(actor))
        {
            LogGateDebug(actor, "GateCheck", "gateTravelDisabled");
            return;
        }

        NpcMapZone? actorZone = NpcMapNavigator.ResolveActorZone(actor);
        if (!actorZone.HasValue)
        {
            LogGateDebug(actor, "GateCheck", "actorZone=None");
            return;
        }

        if (!TryGetTeleportRouteForZone(
                actorZone.Value,
                out Vector3 entryPosition,
                out Vector3 exitPosition,
                out NpcMapZone destinationZone))
        {
            LogGateDebug(
                actor,
                "GateCheck",
                "noRoute actorZone=" +
                actorZone.Value +
                " from=" +
                fromZone +
                " to=" +
                toZone +
                " bidirectional=" +
                bidirectional +
                " gatePos=" +
                transform.position +
                " entry=" +
                entryPosition +
                " exit=" +
                exitPosition);
            return;
        }

        if (!IsActorNearRouteEntry(
                actor,
                entryPosition))
        {
            LogGateDebug(
                actor,
                "GateCheck",
                "outsideEntry actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone +
                " actorPos=" +
                actor.transform.position +
                " entry=" +
                entryPosition);
            return;
        }

        if (!CanNpcUseGate(actor))
        {
            LogGateDebug(
                actor,
                "GateCheck",
                "canUse=false actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone +
                " gatePos=" +
                transform.position +
                " entry=" +
                entryPosition +
                " exit=" +
                exitPosition);
            return;
        }

        long cooldownKey = GetNpcCooldownKey(actor);
        if (IsNpcReentryLocked(cooldownKey, actor))
        {
            return;
        }

        if (npcTeleportCooldowns.TryGetValue(
                cooldownKey,
                out float nextAllowedTeleport) &&
            Time.time < nextAllowedTeleport)
        {
            LogGateDebug(
                actor,
                "GateCheck",
                "cooldown actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone +
                " nextAllowed=" +
                nextAllowedTeleport.ToString("0.00") +
                " now=" +
                Time.time.ToString("0.00") +
                " gatePos=" +
                transform.position);
            return;
        }

        if (TryTeleportNpc(actor))
        {
            LogGateDebug(
                actor,
                "GateTeleport",
                "success actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone +
                " gatePos=" +
                transform.position +
                " exit=" +
                exitPosition);
            npcTeleportCooldowns[cooldownKey] =
                Time.time + Mathf.Max(0.1f, npcGlobalTeleportCooldown);
            npcTeleportReentryLocks[cooldownKey] =
                Time.time + Mathf.Max(4f, npcReentryLockDuration);
        }
        else
        {
            LogGateDebug(
                actor,
                "GateTeleport",
                "failed actorZone=" +
                actorZone.Value +
                " routeTo=" +
                destinationZone +
                " gatePos=" +
                transform.position +
                " exit=" +
                exitPosition);
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

        if (!NpcGateTravelPolicy.AllowsAutomaticGateTravel(actor))
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

    long GetNpcCooldownKey(GameObject actor)
    {
        if (actor == null)
        {
            return 0L;
        }

        return ((long)(uint)UnityObjectIdUtility.GetRuntimeId(actor) << 32) |
            (uint)UnityObjectIdUtility.GetRuntimeId(this);
    }

    bool IsNpcReentryLocked(long cooldownKey, GameObject actor)
    {
        if (npcTeleportReentryLocks.TryGetValue(
                cooldownKey,
                out float reentryUnlockedAt))
        {
            // The lock is a short debounce, not an occupancy lock. The latter
            // becomes permanent when an NPC returns to this gate later: no
            // check occurs while it is away, so the old entry is never cleared.
            // The longer per-gate cooldown still protects repeated use.
            if (Time.time < reentryUnlockedAt)
            {
                return true;
            }

            npcTeleportReentryLocks.Remove(cooldownKey);
        }

        return false;
    }

    bool IsActorNearRouteEntry(
        GameObject actor,
        Vector3 entryPosition)
    {
        if (actor == null)
        {
            return false;
        }

        float allowedDistance =
            Mathf.Max(0.18f, npcAutoUseRadius);

        if (Vector2.Distance(
                actor.transform.position,
                entryPosition) <= allowedDistance)
        {
            return true;
        }

        Collider2D[] actorColliders =
            actor.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < actorColliders.Length; i++)
        {
            Collider2D actorCollider = actorColliders[i];
            if (actorCollider == null ||
                !actorCollider.enabled)
            {
                continue;
            }

            Vector2 closestPoint =
                actorCollider.ClosestPoint(entryPosition);
            if (Vector2.Distance(
                    closestPoint,
                    entryPosition) <= allowedDistance)
            {
                return true;
            }
        }

        return false;
    }

    bool IsActorInsideGate(GameObject actor)
    {
        Collider2D gateCollider = GetComponent<Collider2D>();
        if (actor == null || gateCollider == null || !gateCollider.enabled)
        {
            return false;
        }

        if (gateCollider.OverlapPoint(actor.transform.position))
        {
            return true;
        }

        Collider2D[] actorColliders =
            actor.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < actorColliders.Length; i++)
        {
            Collider2D actorCollider = actorColliders[i];
            if (actorCollider == null || !actorCollider.enabled)
            {
                continue;
            }

            if (gateCollider.Distance(actorCollider).isOverlapped)
            {
                return true;
            }
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
            LogGateDebug(actor, "TeleportStart", "actorZone=None");
            return false;
        }

        if (!TryGetTeleportRouteForZone(
                actorZone.Value,
                out Vector3 entryPosition,
                out Vector3 exitPosition,
                out NpcMapZone destinationZone))
        {
            LogGateDebug(
                actor,
                "TeleportStart",
                "noRoute actorZone=" +
                actorZone.Value +
                " from=" +
                fromZone +
                " to=" +
                toZone +
                " bidirectional=" +
                bidirectional);
            return false;
        }

        Vector3 targetPosition = exitPosition;
        Vector2 arrivalDirection =
            (Vector2)exitPosition - (Vector2)entryPosition;
        if (arrivalDirection.sqrMagnitude <= 0.0001f)
        {
            arrivalDirection =
                (Vector2)exitPosition - (Vector2)transform.position;
        }

        if (arrivalDirection.sqrMagnitude <= 0.0001f)
        {
            arrivalDirection = Vector2.up;
        }

        targetPosition +=
            (Vector3)(arrivalDirection.normalized *
            Mathf.Max(0.9f, npcAutoUseRadius * 2.5f));
        LogGateDebug(
            actor,
            "TeleportStart",
            "actorZone=" +
                actorZone.Value +
                " destinationZone=" +
                destinationZone +
                " target=" +
                targetPosition +
                " exit=" +
                exitPosition +
                " gatePos=" +
                transform.position);
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
        NpcTaskProvider.NotifyNpcTeleported(actor, gameObject);

        return true;
    }

    void LogGateDebug(GameObject actor, string stage, string detail)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!debugNpcGateLogs)
        {
            return;
        }

        if (actor == null)
        {
            return;
        }

        int actorKey =
            UnityObjectIdUtility.GetRuntimeId(actor);
        string signature = stage + "|" + detail;
        if (npcGateDebugSignatures.TryGetValue(actorKey, out string lastSignature) &&
            string.Equals(lastSignature, signature, System.StringComparison.Ordinal) &&
            npcGateDebugTimes.TryGetValue(actorKey, out float lastTime) &&
            Time.time - lastTime < 1.5f)
        {
            return;
        }

        npcGateDebugSignatures[actorKey] = signature;
        npcGateDebugTimes[actorKey] = Time.time;

        Debug.Log(
            "[NpcTeleportGate] gate=" + name +
            " stage=" + stage +
            " actor=" + actor.name +
            " from=" + fromZone +
            " to=" + toZone +
            " detail=" + detail);
#endif
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
        RemoveRuntimeStateForGate(
            UnityObjectIdUtility.GetRuntimeId(this));
    }

    static void RemoveRuntimeStateForGate(int gateInstanceId)
    {
        uint gateKey = (uint)gateInstanceId;
        RemoveKeysForGate(npcTeleportCooldowns, gateKey);
        RemoveKeysForGate(npcTeleportReentryLocks, gateKey);
    }

    static void RemoveKeysForGate(
        Dictionary<long, float> source,
        uint gateKey)
    {
        List<long> remove = new List<long>();
        foreach (KeyValuePair<long, float> pair in source)
        {
            if ((uint)pair.Key == gateKey)
            {
                remove.Add(pair.Key);
            }
        }

        for (int i = 0; i < remove.Count; i++)
        {
            source.Remove(remove[i]);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeState()
    {
        gates.Clear();
        npcTeleportCooldowns.Clear();
        npcTeleportReentryLocks.Clear();
        npcGateDebugTimes.Clear();
        npcGateDebugSignatures.Clear();
    }

}
