using UnityEngine;

// Conversation pauses and target-approach helpers used by movement/combat.
public partial class SmartNpcAI
{
    public void StopForConversation()
    {
        StopForConversation(2f);
    }

    public void StopForConversation(float duration)
    {
        movementPausedUntil = Mathf.Max(
            movementPausedUntil,
            Time.time + Mathf.Max(0.2f, duration));

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    Vector3 GetApproachPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        NpcInteractionPoint interactionPoint =
            target.GetComponent<NpcInteractionPoint>();
        if (interactionPoint != null)
        {
            Vector3 standPosition =
                interactionPoint.GetStandPositionFor(gameObject);
            standPosition.z = transform.position.z;
            return standPosition;
        }

        NpcCounterBroker counterBroker =
            target.GetComponentInParent<NpcCounterBroker>();
        if (counterBroker != null)
        {
            Vector3 brokerPosition =
                counterBroker.GetCustomerPositionFor(gameObject);
            brokerPosition.z = transform.position.z;
            return brokerPosition;
        }

        Vector3 targetPosition = target.position;
        if (currentMonsterTarget != null &&
            target == currentMonsterTarget.transform)
        {
            return GetMonsterCombatApproachPosition(target);
        }

        if (!ShouldUseSharedTargetSpacing(target))
        {
            return targetPosition;
        }

        int slotCount = 8;
        int slotIndex = Mathf.Abs(
            gameObject.GetInstanceID() ^
            target.gameObject.GetInstanceID()) % slotCount;
        float spacingRadius = Mathf.Max(
            targetClearRadius * 3f,
            sharedTargetSpacingRadius,
            0.85f);

        return FindOpenSharedTargetSlot(
            targetPosition,
            slotCount,
            slotIndex,
            spacingRadius);
    }

    Vector3 FindOpenSharedTargetSlot(
        Vector3 targetPosition,
        int slotCount,
        int startSlotIndex,
        float spacingRadius)
    {
        Vector3 fallback = targetPosition;

        for (int i = 0; i < slotCount; i++)
        {
            int slotIndex = (startSlotIndex + i) % slotCount;
            float angle = (Mathf.PI * 2f * slotIndex) / slotCount;
            Vector3 candidate =
                targetPosition +
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f) * spacingRadius;

            if (!IsSharedTargetOccupied(candidate))
            {
                return candidate;
            }

            fallback = candidate;
        }

        return fallback;
    }

    Vector3 GetMonsterCombatApproachPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        if (selfColliders == null || selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>(true);
        }

        Vector2 fromPosition = transform.position;
        Vector2 away = fromPosition - (Vector2)target.position;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = Vector2.right;
        }

        float desiredGap =
            Mathf.Max(
                0.28f,
                targetClearRadius * 2f,
                attackRange * 0.3f);

        Vector3 approach =
            target.position +
            (Vector3)(away.normalized * desiredGap);
        approach.z = transform.position.z;
        return approach;
    }

    bool ShouldUseSharedTargetSpacing(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (currentMonsterTarget != null &&
            target == currentMonsterTarget.transform)
        {
            return false;
        }

        if (target.GetComponentInParent<NpcTaskProvider>() != null ||
            target.GetComponentInParent<NpcCounterBroker>() != null)
        {
            return false;
        }

        return target.GetComponentInParent<MonsterAI>() != null;
    }

    bool IsSharedTargetOccupied(Vector3 targetPosition)
    {
        float radius = Mathf.Max(0.18f, sharedTargetOccupancyRadius);
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                targetPosition,
                radius,
                crowdLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || IsSelfCollider(hit))
            {
                continue;
            }

            VillagerAI otherVillager =
                hit.GetComponentInParent<VillagerAI>();
            if (otherVillager != null &&
                otherVillager.gameObject != gameObject &&
                !otherVillager.IsDead)
            {
                return true;
            }

            SmartNpcAI otherCultivator =
                hit.GetComponentInParent<SmartNpcAI>();
            if (otherCultivator != null &&
                otherCultivator.gameObject != gameObject &&
                !otherCultivator.IsDead)
            {
                return true;
            }
        }

        return false;
    }
}
