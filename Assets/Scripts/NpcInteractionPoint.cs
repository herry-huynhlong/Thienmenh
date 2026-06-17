using UnityEngine;

public class NpcInteractionPoint : MonoBehaviour
{
    [Header("Interaction")]
    public float interactionRadius = 0.45f;
    public bool spreadNpcAroundPoint = true;
    public float standSpacing = 0.35f;
    public bool requireClearStandSpot = true;
    public LayerMask blockedLayers = ~0;

    public Vector3 GetStandPositionFor(GameObject npc)
    {
        Vector3 seed = transform.position;

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

        return FindClearStandSpot(seed, npc);
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
        if (!IsBlocked(seed, npc))
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
                    return candidate;
                }
            }
        }

        return seed;
    }

    bool IsBlocked(Vector3 position, GameObject npc)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                Mathf.Max(0.1f, interactionRadius),
                blockedLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.isTrigger ||
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

        return false;
    }
}
