using UnityEngine;

// Physics setup, collision filtering, obstacle overlap recovery, and cleanup.
public partial class VillagerAI
{
    void ConfigureRigidbody()
    {
        if (rb == null)
        {
            return;
        }

        rb.bodyType = useKinematicNpcMovement
            ? RigidbodyType2D.Kinematic
            : RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryIgnoreNpcCollision(collision.collider);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryIgnoreNpcCollision(collision.collider);

        if (collision.collider != null &&
            IsBlockingObstacle(collision.collider))
        {
            TryEscapeObstacleCollision(collision);
        }
    }

    void TryIgnoreNpcCollision(Collider2D other)
    {
        if (!ignoreNpcBodyCollisions || other == null || other.isTrigger)
        {
            return;
        }

        if (other.GetComponentInParent<VillagerAI>() == null &&
            other.GetComponentInParent<SmartNpcAI>() == null &&
            other.GetComponentInParent<NpcMapMover2D>() == null)
        {
            return;
        }

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        foreach (Collider2D own in ownColliders)
        {
            if (own != null &&
                !own.isTrigger &&
                own != other)
            {
                Physics2D.IgnoreCollision(own, other, true);
            }
        }
    }

    void TryEscapeObstacleCollision(Collision2D collision)
    {
        if (collision == null || collision.contactCount <= 0)
        {
            return;
        }

        if (ignoreNpcBodyCollisions &&
            collision.collider != null &&
            (collision.collider.GetComponentInParent<VillagerAI>() != null ||
             collision.collider.GetComponentInParent<SmartNpcAI>() != null ||
             collision.collider.GetComponentInParent<NpcMapMover2D>() != null))
        {
            return;
        }

        Vector2 normal = collision.GetContact(0).normal;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 escapePoint = transform.position +
            (Vector3)(normal.normalized * Mathf.Max(unstuckOffsetRadius, targetClearRadius * 2f));

        Vector3 clearPoint;
        if (!TryFindClearPointNear(escapePoint, out clearPoint))
        {
            return;
        }

        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = true;
        directMoveTarget = clearPoint;
        desiredVelocity =
            normal.normalized *
            moveSpeed *
            0.75f;
        blockedMoveTimer = 0f;
    }

    void ResolveInitialObstacleOverlap()
    {
        if (!IsPositionBlocked(transform.position) &&
            !HasBlockingColliderOverlap())
        {
            return;
        }

        if (!TryFindClearPointNear(transform.position, out Vector3 clearPoint))
        {
            return;
        }

        transform.position = clearPoint;
        spawnPosition = clearPoint;
        lastUnstuckPosition = clearPoint;

        if (rb != null)
        {
            rb.position = clearPoint;
            rb.linearVelocity = Vector2.zero;
        }
    }

    bool HasBlockingColliderOverlap()
    {
        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        Collider2D[] hits = new Collider2D[32];

        foreach (Collider2D own in ownColliders)
        {
            if (own == null || own.isTrigger || !own.enabled)
            {
                continue;
            }

            int count = Physics2D.OverlapCollider(own, filter, hits);
            for (int i = 0; i < count; i++)
            {
                if (IsBlockingObstacle(hits[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool IsPositionBlocked(Vector3 position)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            GetBodyClearRadius());

        foreach (Collider2D hit in hits)
        {
            if (IsBlockingObstacle(hit))
            {
                LogMovementHaltDebug(
                    "PositionBlocked",
                    "probe=" + position +
                    " radius=" + GetBodyClearRadius().ToString("0.00") +
                    " obstacle=" + DescribeObstacle(hit));
                return true;
            }
        }

        return false;
    }

    float GetBodyClearRadius()
    {
        float radius = Mathf.Max(0.01f, targetClearRadius + navigationClearancePadding);

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        foreach (Collider2D own in ownColliders)
        {
            if (own == null || own.isTrigger)
            {
                continue;
            }

            Bounds bounds = own.bounds;
            radius =
                Mathf.Max(
                    radius,
                    bounds.extents.x,
                    bounds.extents.y);
        }

        return radius;
    }

    bool IsBlockingObstacle(Collider2D hit)
    {
        if (hit == null || hit.isTrigger || IsSelfCollider(hit))
        {
            return false;
        }

        if (IsCounterCustomerZoneCollider(hit))
        {
            return false;
        }

        if (IsTaskProviderInteractionCollider(hit))
        {
            return false;
        }

        if (IsInteractionPointCollider(hit))
        {
            return false;
        }

        if (IsInteriorFocusCollider(hit))
        {
            return false;
        }

        return hit.GetComponentInParent<VillagerAI>() == null &&
            hit.GetComponentInParent<SmartNpcAI>() == null &&
            hit.GetComponentInParent<NpcMapMover2D>() == null;
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

    bool IsTaskProviderInteractionCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        NpcTaskProvider provider =
            hit.GetComponentInParent<NpcTaskProvider>();
        if (provider == null)
        {
            return false;
        }

        Transform providerPoint = provider.providerPoint;
        if (providerPoint != null &&
            (hit.transform == providerPoint ||
            hit.transform.IsChildOf(providerPoint)))
        {
            return true;
        }

        Transform providerStandPoint = provider.providerStandPoint;
        if (providerStandPoint != null &&
            (hit.transform == providerStandPoint ||
            hit.transform.IsChildOf(providerStandPoint)))
        {
            return true;
        }

        return false;
    }

    bool IsInteractionPointCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        NpcInteractionPoint interactionPoint =
            hit.GetComponentInParent<NpcInteractionPoint>();
        if (interactionPoint == null)
        {
            return false;
        }

        return hit.transform == interactionPoint.transform ||
            hit.transform.IsChildOf(interactionPoint.transform);
    }

    bool IsInteriorFocusCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        return hit.GetComponentInParent<InteriorCameraFocus>() != null;
    }

    bool IsSelfCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
        {
            return true;
        }

        if (ownColliders == null)
        {
            return false;
        }

        foreach (Collider2D own in ownColliders)
        {
            if (own == hit)
            {
                return true;
            }
        }

        return false;
    }

    void OnDisable()
    {
        activeVillagers.Remove(this);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
    }

    void OnDestroy()
    {
        activeVillagers.Remove(this);
        TargetReservationSystem.TryGetExistingInstance()?.ReleaseAllByOwner(gameObject);
        UpdateCultivationEffect(false);
        NpcCollisionRegistry.Unregister(this);
    }
}
