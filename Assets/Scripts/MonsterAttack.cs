using UnityEngine;

public class MonsterAttack : MonoBehaviour
{
    public int damage = 10;
    public GameObject owner;

    void Awake()
    {
        if (owner == null)
        {
            MonsterAI monster = GetComponentInParent<MonsterAI>();
            if (monster != null)
            {
                owner = monster.gameObject;
                damage = monster.damage;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || IsOwner(other.transform))
        {
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || damageable.IsDead)
        {
            return;
        }

        if (owner != null &&
            damageable.DamageTransform != null &&
            damageable.DamageTransform.gameObject == owner)
        {
            return;
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
                NpcText.Dialogue("combatBeastReason"));

            damageable.TakeDamage(modifiedDamage);
            return;
        }

        damageable.TakeDamage(damage);
    }

    bool IsOwner(Transform target)
    {
        if (owner == null || target == null)
        {
            return false;
        }

        return target.gameObject == owner || target.IsChildOf(owner.transform);
    }
}
