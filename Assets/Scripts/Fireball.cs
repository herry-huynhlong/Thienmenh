using UnityEngine;

public class Fireball : MonoBehaviour
{
    public float speed = 8f;
    public int damage = 10;
    public float explosionRadius = 0.6f;
    public GameObject explosionPrefab;
    public LayerMask hitLayers = ~0;

    Vector2 direction;
    GameObject owner;
    bool exploded;

    public void SetOwner(GameObject newOwner)
    {
        owner = newOwner;
    }

    public void SetDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized;

        Destroy(gameObject, 5f);
    }

    void Update()
    {
        transform.Translate(
            direction *
            speed *
            Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryExplode(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryExplode(collision.gameObject);
    }

    void TryExplode(GameObject hitObject)
    {
        if (exploded)
        {
            return;
        }

        if (IsOwner(hitObject))
        {
            return;
        }

        if (!IsInHitLayer(hitObject))
        {
            return;
        }

        exploded = true;

        if (explosionPrefab != null)
        {
            Instantiate(
                explosionPrefab,
                transform.position,
                Quaternion.identity);
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                explosionRadius,
                hitLayers);

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable =
                hit.GetComponentInParent<IDamageable>();

            if (damageable == null ||
                damageable.IsDead)
            {
                continue;
            }

            if (NpcPetCompanion.BlocksMonsterAttacks(damageable.DamageTransform != null
                ? damageable.DamageTransform.gameObject
                : hit.gameObject))
            {
                continue;
            }

            if (owner != null &&
                damageable.DamageTransform != null &&
                damageable.DamageTransform.gameObject == owner)
            {
                continue;
            }

            if (owner != null && damageable.DamageTransform != null)
            {
                int modifiedDamage =
                    NpcCombatTechniqueSystem.ModifyOutgoingDamage(
                        owner,
                        damageable.DamageTransform.gameObject,
                        damage);

                NpcSocialEventBus.PublishHostility(
                    owner,
                    damageable.DamageTransform.gameObject,
                    Mathf.Clamp(modifiedDamage, 1, 100),
                    damageable.DamageTransform.position,
                    NpcText.Dialogue("combatSpellReason"));

                SmartNpcAI smartNpc =
                    damageable as SmartNpcAI;
                if (smartNpc != null)
                {
                    smartNpc.TakeDamage(modifiedDamage, owner);
                    continue;
                }

                damageable.TakeDamage(modifiedDamage);
                continue;
            }

            SmartNpcAI fallbackSmartNpc = damageable as SmartNpcAI;
            if (fallbackSmartNpc != null)
            {
                fallbackSmartNpc.TakeDamage(damage, owner);
                continue;
            }

            damageable.TakeDamage(damage);
        }

        Destroy(gameObject);
    }

    bool IsInHitLayer(GameObject hitObject)
    {
        if (hitObject == null)
        {
            return false;
        }

        int layerMask =
            1 << hitObject.layer;

        return (hitLayers.value & layerMask) != 0;
    }

    bool IsOwner(GameObject hitObject)
    {
        if (owner == null ||
            hitObject == null)
        {
            return false;
        }

        if (hitObject == owner)
        {
            return true;
        }

        return hitObject.transform.IsChildOf(
            owner.transform);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            explosionRadius);
    }
}
