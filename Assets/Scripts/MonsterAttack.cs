using UnityEngine;
using System.Collections.Generic;

public class MonsterAttack : MonoBehaviour
{
    public GameObject owner;
    MonsterAI ownerMonster;
    readonly Dictionary<IDamageable, int> lastHitAttackSequence =
        new Dictionary<IDamageable, int>();

    void Awake()
    {
        if (owner == null)
        {
            ownerMonster = GetComponentInParent<MonsterAI>();
            if (ownerMonster != null)
            {
                owner = ownerMonster.gameObject;
            }
        }
        else
        {
            ownerMonster = owner.GetComponentInParent<MonsterAI>();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryApplyDamage(other);
    }

    void OnDisable()
    {
        lastHitAttackSequence.Clear();
    }

    void TryApplyDamage(Collider2D other)
    {
        if (other == null || IsOwner(other.transform))
        {
            return;
        }

        if (!CanDealTriggerDamage())
        {
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || damageable.IsDead)
        {
            return;
        }

        if (HasAlreadyHitThisAttack(damageable))
        {
            return;
        }

        if (owner != null &&
            damageable.DamageTransform != null &&
            damageable.DamageTransform.gameObject == owner)
        {
            return;
        }

        MarkHitForCurrentAttack(damageable);

        Vector3 hitPosition = damageable.DamageTransform != null
            ? damageable.DamageTransform.position
            : other.bounds.center;
        DamageContext context = DamageContext.Attack(
            GetBaseDamage(),
            owner,
            this,
            DamageSourceCategory.Monster,
            DamageType.Physical,
            NpcText.Dialogue("combatBeastReason"),
            hitPosition,
            owner != null);
        DamageSystem.Apply(damageable, context);
    }

    int GetBaseDamage()
    {
        if (ownerMonster != null)
        {
            return Mathf.Max(1, ownerMonster.damage);
        }

        if (owner != null)
        {
            MonsterAI resolvedOwner =
                owner.GetComponentInParent<MonsterAI>();
            if (resolvedOwner != null)
            {
                ownerMonster = resolvedOwner;
                return Mathf.Max(1, resolvedOwner.damage);
            }
        }

        return 10;
    }

    bool CanDealTriggerDamage()
    {
        if (ownerMonster == null && owner != null)
        {
            ownerMonster = owner.GetComponentInParent<MonsterAI>();
        }

        if (ownerMonster == null)
        {
            return true;
        }

        if (ownerMonster.UsesDirectAttackDamage)
        {
            return false;
        }

        return ownerMonster.IsAttackActive;
    }

    bool HasAlreadyHitThisAttack(IDamageable damageable)
    {
        if (damageable == null || ownerMonster == null)
        {
            return false;
        }

        return lastHitAttackSequence.TryGetValue(
            damageable,
            out int lastSequence) &&
            lastSequence == ownerMonster.AttackSequence;
    }

    void MarkHitForCurrentAttack(IDamageable damageable)
    {
        if (damageable == null || ownerMonster == null)
        {
            return;
        }

        lastHitAttackSequence[damageable] =
            ownerMonster.AttackSequence;
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
