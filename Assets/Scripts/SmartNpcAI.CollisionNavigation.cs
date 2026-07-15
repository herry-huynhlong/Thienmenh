using UnityEngine;

// Collider filtering and obstacle classification for smart NPC movement.
public partial class SmartNpcAI
{
    bool IsBlockingObstacle(Collider2D hit)
    {
        if (hit == null || hit.isTrigger || IsSelfCollider(hit))
        {
            return false;
        }

        if (IsCurrentTargetCollider(hit) ||
            IsCurrentMonsterCollider(hit) ||
            IsCounterCustomerZoneCollider(hit))
        {
            return false;
        }

        return hit.GetComponentInParent<VillagerAI>() == null &&
            hit.GetComponentInParent<SmartNpcAI>() == null &&
            hit.GetComponentInParent<NpcMapMover2D>() == null &&
            hit.GetComponentInParent<MonsterAI>() == null;
    }

    string DescribeObstacle(Collider2D hit)
    {
        if (hit == null)
        {
            return "null";
        }

        Bounds bounds = hit.bounds;
        return hit.name +
            " layer=" + hit.gameObject.layer +
            " trigger=" + (hit.isTrigger ? 1 : 0) +
            " pos=" + hit.transform.position +
            " center=" + bounds.center +
            " size=" + bounds.size +
            " parent=" +
            (hit.transform.parent != null
                ? hit.transform.parent.name
                : "none");
    }

    bool IsCounterCustomerZoneCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        NpcCounterBroker broker =
            hit.GetComponentInParent<NpcCounterBroker>();
        if (broker == null ||
            broker.customerPoint == null)
        {
            return false;
        }

        Collider2D customerZone =
            broker.GetCustomerZoneCollider();
        if (customerZone == null)
        {
            return false;
        }

        return hit == customerZone;
    }

    bool IsCurrentTargetCollider(Collider2D hit)
    {
        if (hit == null || currentTarget == null)
        {
            return false;
        }

        return hit.transform == currentTarget ||
            hit.transform.IsChildOf(currentTarget);
    }

    bool IsCurrentMonsterCollider(Collider2D hit)
    {
        if (hit == null || currentMonsterTarget == null)
        {
            return false;
        }

        Transform monsterTarget =
            currentMonsterTarget.transform;
        return hit.transform == monsterTarget ||
            hit.transform.IsChildOf(monsterTarget);
    }
}
