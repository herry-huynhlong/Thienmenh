using System.Collections.Generic;
using UnityEngine;

public enum NpcLocationPurpose
{
    Any,
    Home,
    Work,
    Market,
    SellGoods,
    BuyGoods,
    Eat,
    Cultivation,
    Alchemy,
    Forge,
    Resource,
    Hunt,
    TaskProvider,
    Fishing,
    Farming
}

public enum NpcDangerTier
{
    Any,
    Low,
    Medium,
    High
}

[RequireComponent(typeof(Collider2D))]
public class NpcLocationArea : MonoBehaviour
{
    static readonly List<NpcLocationArea> areas =
        new List<NpcLocationArea>();

    [Header("Identity")]
    public NpcLocationPurpose purpose = NpcLocationPurpose.Any;
    public NpcScheduleActivity activity = NpcScheduleActivity.Idle;
    public VillagerJob job = VillagerJob.None;
    public NpcLifePath lifePath = NpcLifePath.Commoner;
    public NpcMapZone zone = NpcMapZone.Lang;
    [Header("Danger")]
    public NpcDangerTier dangerTier = NpcDangerTier.Any;
    public int priority;

    [Header("Matching")]
    public bool matchActivity = true;
    public bool matchJob;
    public bool matchLifePath;
    public bool matchZone;
    public bool matchDangerTier;

    [Header("Area")]
    public Collider2D areaBounds;
    public bool useColliderBounds = true;
    public Vector2 fallbackSize = new Vector2(2f, 2f);
    public int maxPickAttempts = 16;
    public LayerMask blockedLayers = 0;
    public float blockedCheckRadius = 0.15f;

    public static IReadOnlyList<NpcLocationArea> Areas => areas;

    void Reset()
    {
        areaBounds = GetComponent<Collider2D>();
        if (areaBounds != null)
        {
            areaBounds.isTrigger = true;
        }
    }

    void Awake()
    {
        EnsureCollider();
    }

    void OnEnable()
    {
        EnsureCollider();
        if (!areas.Contains(this))
        {
            areas.Add(this);
        }
    }

    void OnDisable()
    {
        areas.Remove(this);
    }

    void OnValidate()
    {
        fallbackSize.x = Mathf.Max(0.1f, fallbackSize.x);
        fallbackSize.y = Mathf.Max(0.1f, fallbackSize.y);
        maxPickAttempts = Mathf.Max(1, maxPickAttempts);
        blockedCheckRadius = Mathf.Max(0f, blockedCheckRadius);
        EnsureCollider();
    }

    public static bool TryGetPosition(
        GameObject npc,
        NpcScheduleActivity activity,
        VillagerJob job,
        NpcLocationPurpose purpose,
        NpcMapZone? preferredZone,
        NpcDangerTier? preferredDangerTier,
        Vector3 fallback,
        out Vector3 position,
        out NpcMapZone? zone)
    {
        NpcLocationArea area =
            FindBestArea(
                npc,
                activity,
                job,
                purpose,
                preferredZone,
                preferredDangerTier,
                fallback);

        if (area == null)
        {
            position = fallback;
            zone = null;
            return false;
        }

        position = area.GetRandomPoint(npc);
        zone = area.zone;
        return true;
    }

    public static bool TryGetPosition(
        GameObject npc,
        NpcScheduleActivity activity,
        VillagerJob job,
        NpcLocationPurpose purpose,
        Vector3 fallback,
        out Vector3 position,
        out NpcMapZone? zone)
    {
        return TryGetPosition(
            npc,
            activity,
            job,
            purpose,
            null,
            null,
            fallback,
            out position,
            out zone);
    }

    public static NpcLocationArea FindBestArea(
        GameObject npc,
        NpcScheduleActivity activity,
        VillagerJob job,
        NpcLocationPurpose purpose,
        NpcMapZone? preferredZone,
        NpcDangerTier? preferredDangerTier,
        Vector3 origin)
    {
        NpcLocationArea exactPurposeArea =
            FindBestAreaInternal(
                npc,
                activity,
                job,
                purpose,
                preferredZone,
                preferredDangerTier,
                origin,
                true);

        if (exactPurposeArea != null)
        {
            return exactPurposeArea;
        }

        return FindBestAreaInternal(
            npc,
            activity,
            job,
            purpose,
            preferredZone,
            preferredDangerTier,
            origin,
            false);
    }

    public static NpcLocationArea FindBestArea(
        GameObject npc,
        NpcScheduleActivity activity,
        VillagerJob job,
        NpcLocationPurpose purpose,
        NpcMapZone? preferredZone,
        Vector3 origin)
    {
        return FindBestArea(
            npc,
            activity,
            job,
            purpose,
            preferredZone,
            null,
            origin);
    }

    public static NpcLocationArea FindArea(Vector3 position)
    {
        foreach (NpcLocationArea area in areas)
        {
            if (area != null &&
                area.Contains(position))
            {
                return area;
            }
        }

        return null;
    }

    static NpcLocationArea FindBestAreaInternal(
        GameObject npc,
        NpcScheduleActivity activity,
        VillagerJob job,
        NpcLocationPurpose purpose,
        NpcMapZone? preferredZone,
        NpcDangerTier? preferredDangerTier,
        Vector3 origin,
        bool requireExactPurpose)
    {
        NpcLocationArea best = null;
        float bestScore = float.NegativeInfinity;

        foreach (NpcLocationArea area in areas)
        {
            if (area == null ||
                !area.isActiveAndEnabled ||
                (requireExactPurpose && area.purpose != purpose) ||
                !area.Matches(
                    npc,
                    activity,
                    job,
                    purpose,
                    preferredZone,
                    preferredDangerTier))
            {
                continue;
            }

            float score =
                area.priority * 1000f +
                area.GetMatchScore(
                    activity,
                    job,
                    purpose,
                    preferredZone,
                    preferredDangerTier) -
                Vector2.Distance(origin, area.transform.position);

            if (score > bestScore)
            {
                bestScore = score;
                best = area;
            }
        }

        return best;
    }

    public bool Matches(
        GameObject npc,
        NpcScheduleActivity requestedActivity,
        VillagerJob requestedJob,
        NpcLocationPurpose requestedPurpose,
        NpcMapZone? preferredZone,
        NpcDangerTier? preferredDangerTier)
    {
        if (requestedPurpose != NpcLocationPurpose.Any &&
            purpose != NpcLocationPurpose.Any &&
            purpose != requestedPurpose)
        {
            return false;
        }

        if (matchActivity &&
            activity != NpcScheduleActivity.Idle &&
            activity != requestedActivity)
        {
            return false;
        }

        if (matchJob && job != VillagerJob.None && job != requestedJob)
        {
            return false;
        }

        if (matchZone && preferredZone.HasValue && zone != preferredZone.Value)
        {
            return false;
        }

        if (matchDangerTier &&
            preferredDangerTier.HasValue &&
            dangerTier != NpcDangerTier.Any &&
            dangerTier != preferredDangerTier.Value)
        {
            return false;
        }

        if (matchLifePath && npc != null)
        {
            NpcScheduleController schedule =
                npc.GetComponent<NpcScheduleController>();

            if (schedule != null && schedule.lifePath != lifePath)
            {
                return false;
            }
        }

        return true;
    }

    float GetMatchScore(
        NpcScheduleActivity requestedActivity,
        VillagerJob requestedJob,
        NpcLocationPurpose requestedPurpose,
        NpcMapZone? preferredZone,
        NpcDangerTier? preferredDangerTier)
    {
        float score = 0f;

        if (requestedPurpose != NpcLocationPurpose.Any &&
            purpose == requestedPurpose)
        {
            score += 10000f;
        }

        if (matchJob &&
            job != VillagerJob.None &&
            job == requestedJob)
        {
            score += 250f;
        }

        if (matchActivity &&
            activity == requestedActivity)
        {
            score += 125f;
        }

        if (matchZone &&
            preferredZone.HasValue &&
            zone == preferredZone.Value)
        {
            score += 75f;
        }

        if (matchDangerTier &&
            preferredDangerTier.HasValue &&
            dangerTier == preferredDangerTier.Value)
        {
            score += 175f;
        }

        return score;
    }

    bool Contains(Vector3 position)
    {
        if (areaBounds == null)
        {
            return false;
        }

        Vector2 closest = areaBounds.ClosestPoint(position);
        return Vector2.Distance(closest, position) <= 0.02f;
    }

    public Vector3 GetRandomPoint(GameObject npc = null)
    {
        EnsureCollider();

        for (int i = 0; i < maxPickAttempts; i++)
        {
            Vector3 point = PickPointInBounds();
            if (areaBounds != null && !areaBounds.OverlapPoint(point))
            {
                continue;
            }

            if (IsBlocked(point, npc))
            {
                continue;
            }

            return point;
        }

        return transform.position;
    }

    void EnsureCollider()
    {
        if (areaBounds == null)
        {
            areaBounds = GetComponent<Collider2D>();
        }

        if (areaBounds != null)
        {
            areaBounds.isTrigger = true;
        }
    }

    Vector3 PickPointInBounds()
    {
        if (useColliderBounds && areaBounds != null)
        {
            Bounds bounds = areaBounds.bounds;
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                transform.position.z);
        }

        Vector2 half = fallbackSize * 0.5f;
        return transform.position +
            new Vector3(
                Random.Range(-half.x, half.x),
                Random.Range(-half.y, half.y),
                0f);
    }

    bool IsBlocked(Vector3 point, GameObject npc)
    {
        if (blockedCheckRadius <= 0f)
        {
            return false;
        }

        if (blockedLayers.value != 0)
        {
            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    point,
                    blockedCheckRadius,
                    blockedLayers);

            foreach (Collider2D hit in hits)
            {
                if (hit == null ||
                    hit.isTrigger ||
                    (npc != null && hit.transform.IsChildOf(npc.transform)))
                {
                    continue;
                }

                return true;
            }
        }

        Collider2D[] npcHits =
            Physics2D.OverlapCircleAll(point, blockedCheckRadius);
        foreach (Collider2D hit in npcHits)
        {
            if (hit == null ||
                hit.isTrigger ||
                (npc != null && hit.transform.IsChildOf(npc.transform)))
            {
                continue;
            }

            if (GetNpcRoot(hit) != null)
            {
                return true;
            }
        }

        return false;
    }

    Transform GetNpcRoot(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        SmartNpcAI smartNpc = hit.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        VillagerAI villager = hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.transform;
        }

        NpcMapMover2D mover = hit.GetComponentInParent<NpcMapMover2D>();
        return mover != null ? mover.transform : null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Collider2D hit = areaBounds != null
            ? areaBounds
            : GetComponent<Collider2D>();

        if (hit != null)
        {
            Gizmos.DrawCube(hit.bounds.center, hit.bounds.size);
            return;
        }

        Gizmos.DrawCube(transform.position, fallbackSize);
    }
}
