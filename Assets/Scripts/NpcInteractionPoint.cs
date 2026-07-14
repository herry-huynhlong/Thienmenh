using System.Collections.Generic;
using UnityEngine;

public class NpcInteractionPoint : MonoBehaviour
{
    class StandReservation
    {
        public int ownerId;
        public Vector3 position;
        public float expiresAt;
    }

    static readonly Dictionary<int, List<StandReservation>> reservationsByPoint =
        new Dictionary<int, List<StandReservation>>();

    [Header("Interaction")]
    public float interactionRadius = 0.45f;
    public bool spreadNpcAroundPoint = true;
    public float standSpacing = 0.85f;
    public bool requireClearStandSpot = true;
    public LayerMask blockedLayers = ~0;
    [Min(0.1f)]
    public float reservationHoldSeconds = 15f;
    [Min(0.05f)]
    public float reservationSpacingRadius = 0.75f;

    public Vector3 GetStandPositionFor(GameObject npc)
    {
        CleanupExpiredReservations();

        Vector3 seed = transform.position;
        if (TryGetReservedStandSpotFor(npc, out Vector3 reservedPosition))
        {
            return reservedPosition;
        }

        // A trigger box usually represents the valid service zone. Prefer
        // reserving distinct positions inside it so customers remain in
        // interaction range while still respecting body spacing.
        if (TryFindClearStandSpotInBox(seed, npc, out Vector3 boxStandPosition))
        {
            return boxStandPosition;
        }

        if (spreadNpcAroundPoint &&
            npc != null &&
            standSpacing > 0.01f)
        {
            int hash = Mathf.Abs(npc.GetInstanceID());
            float angle = (hash % 360) * Mathf.Deg2Rad;
            Vector2 offset =
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                Mathf.Max(0f, standSpacing);

            seed += (Vector3)offset;
        }

        seed.z = transform.position.z;

        if (!requireClearStandSpot)
        {
            return seed;
        }

        Vector3 standPosition = FindClearStandSpot(seed, npc);
        if (TryReserveStandSpot(npc, standPosition))
        {
            return standPosition;
        }

        if (TryFindClearStandSpotInBox(seed, npc, out Vector3 boxPosition))
        {
            return boxPosition;
        }

        return standPosition;
    }

    public bool IsNpcInRange(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        return Vector2.Distance(
            npc.transform.position,
            transform.position) <= Mathf.Max(0.05f, interactionRadius);
    }

    Vector3 FindClearStandSpot(Vector3 seed, GameObject npc)
    {
        if (!IsBlocked(seed, npc) && TryReserveStandSpot(npc, seed))
        {
            return seed;
        }

        float baseRadius = Mathf.Max(
            0.25f,
            Mathf.Max(interactionRadius, standSpacing));

        for (int radiusStep = 0; radiusStep < 5; radiusStep++)
        {
            float radius = baseRadius + radiusStep * 0.2f;

            for (int angleStep = 0; angleStep < 16; angleStep++)
            {
                float angle =
                    (angleStep / 16f) * Mathf.PI * 2f +
                    radiusStep * 0.27f;

                Vector3 candidate =
                    transform.position +
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                candidate.z = transform.position.z;

                if (!IsBlocked(candidate, npc))
                {
                    if (TryReserveStandSpot(npc, candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return seed;
    }

    bool TryFindClearStandSpotInBox(
        Vector3 seed,
        GameObject npc,
        out Vector3 position)
    {
        position = seed;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = GetComponentInParent<BoxCollider2D>();
        }

        if (box == null || !box.enabled)
        {
            return false;
        }

        Bounds bounds = box.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float margin = Mathf.Max(0.05f, Mathf.Min(extents.x, extents.y) * 0.12f);
        float startX = center.x - extents.x + margin;
        float endX = center.x + extents.x - margin;
        float startY = center.y - extents.y + margin;
        float endY = center.y + extents.y - margin;
        float stepSize = Mathf.Max(
            0.16f,
            Mathf.Min(
                Mathf.Max(0.1f, interactionRadius),
                Mathf.Max(0.1f, standSpacing),
                Mathf.Max(0.05f, reservationSpacingRadius * 1.5f)));
        float stepX = stepSize;
        float stepY = stepSize;

        Vector3 best = seed;
        float bestDistance = float.PositiveInfinity;

        if (bounds.Contains(seed) && !IsBlocked(seed, npc, box))
        {
            if (TryReserveStandSpot(npc, seed))
            {
                position = seed;
                return true;
            }
        }

        for (float y = startY; y <= endY; y += stepY)
        {
            for (float x = startX; x <= endX; x += stepX)
            {
                Vector3 candidate = new Vector3(x, y, transform.position.z);
                if (!bounds.Contains(candidate))
                {
                    continue;
                }

                if (IsBlocked(candidate, npc, box))
                {
                    continue;
                }

                float distance = (candidate - seed).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }

        if (bestDistance < float.PositiveInfinity)
        {
            if (TryReserveStandSpot(npc, best))
            {
                position = best;
                return true;
            }
        }

        return false;
    }

    bool IsBlocked(Vector3 position, GameObject npc, Collider2D allowedCollider = null)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                // Spacing controls distance between visitors, not the body
                // clearance probe against nearby counter/wall colliders.
                Mathf.Clamp(interactionRadius, 0.1f, 0.45f),
                blockedLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.isTrigger ||
                hit == allowedCollider ||
                (npc != null && hit.transform.IsChildOf(npc.transform)))
            {
                continue;
            }

            if (hit.GetComponentInParent<VillagerAI>() != null ||
                hit.GetComponentInParent<SmartNpcAI>() != null ||
                hit.GetComponentInParent<NpcMapMover2D>() != null)
            {
                continue;
            }

            return true;
        }

        if (IsReservedByAnotherNpc(position, npc))
        {
            return true;
        }

        return false;
    }

    bool TryGetReservedStandSpotFor(GameObject npc, out Vector3 position)
    {
        position = transform.position;

        if (npc == null)
        {
            return false;
        }

        int pointId = GetInstanceID();
        int ownerId = npc.GetInstanceID();

        if (!reservationsByPoint.TryGetValue(pointId, out List<StandReservation> reservations))
        {
            return false;
        }

        for (int i = reservations.Count - 1; i >= 0; i--)
        {
            StandReservation reservation = reservations[i];
            if (reservation == null ||
                reservation.expiresAt <= Time.time)
            {
                reservations.RemoveAt(i);
                continue;
            }

            if (reservation.ownerId == ownerId)
            {
                reservation.expiresAt =
                    Time.time + Mathf.Max(0.1f, reservationHoldSeconds);
                position = reservation.position;
                return true;
            }
        }

        if (reservations.Count == 0)
        {
            reservationsByPoint.Remove(pointId);
        }

        return false;
    }

    bool TryReserveStandSpot(GameObject npc, Vector3 position)
    {
        if (npc == null)
        {
            return false;
        }

        if (IsReservedByAnotherNpc(position, npc))
        {
            return false;
        }

        int pointId = GetInstanceID();
        int ownerId = npc.GetInstanceID();
        if (!reservationsByPoint.TryGetValue(pointId, out List<StandReservation> reservations))
        {
            reservations = new List<StandReservation>();
            reservationsByPoint[pointId] = reservations;
        }

        for (int i = reservations.Count - 1; i >= 0; i--)
        {
            StandReservation reservation = reservations[i];
            if (reservation == null ||
                reservation.expiresAt <= Time.time)
            {
                reservations.RemoveAt(i);
                continue;
            }

            if (reservation.ownerId == ownerId)
            {
                reservation.position = position;
                reservation.expiresAt =
                    Time.time + Mathf.Max(0.1f, reservationHoldSeconds);
                return true;
            }
        }

        reservations.Add(new StandReservation
        {
            ownerId = ownerId,
            position = position,
            expiresAt = Time.time + Mathf.Max(0.1f, reservationHoldSeconds)
        });
        return true;
    }

    bool IsReservedByAnotherNpc(Vector3 position, GameObject npc)
    {
        int pointId = GetInstanceID();
        if (!reservationsByPoint.TryGetValue(pointId, out List<StandReservation> reservations))
        {
            return false;
        }

        int ownerId = npc != null ? npc.GetInstanceID() : 0;
        float minSpacing = Mathf.Max(
            0.1f,
            Mathf.Max(
                Mathf.Max(interactionRadius, standSpacing),
                reservationSpacingRadius));

        for (int i = reservations.Count - 1; i >= 0; i--)
        {
            StandReservation reservation = reservations[i];
            if (reservation == null ||
                reservation.expiresAt <= Time.time)
            {
                reservations.RemoveAt(i);
                continue;
            }

            if (reservation.ownerId == ownerId)
            {
                continue;
            }

            if (Vector2.Distance(reservation.position, position) <= minSpacing)
            {
                return true;
            }
        }

        if (reservations.Count == 0)
        {
            reservationsByPoint.Remove(pointId);
        }

        return false;
    }

    static void CleanupExpiredReservations()
    {
        if (reservationsByPoint.Count == 0)
        {
            return;
        }

        List<int> emptyKeys = null;
        foreach (KeyValuePair<int, List<StandReservation>> pair in reservationsByPoint)
        {
            List<StandReservation> reservations = pair.Value;
            if (reservations == null)
            {
                if (emptyKeys == null)
                {
                    emptyKeys = new List<int>();
                }

                emptyKeys.Add(pair.Key);
                continue;
            }

            for (int i = reservations.Count - 1; i >= 0; i--)
            {
                StandReservation reservation = reservations[i];
                if (reservation == null ||
                    reservation.expiresAt <= Time.time)
                {
                    reservations.RemoveAt(i);
                }
            }

            if (reservations.Count == 0)
            {
                if (emptyKeys == null)
                {
                    emptyKeys = new List<int>();
                }

                emptyKeys.Add(pair.Key);
            }
        }

        if (emptyKeys == null)
        {
            return;
        }

        foreach (int key in emptyKeys)
        {
            reservationsByPoint.Remove(key);
        }
    }
}
