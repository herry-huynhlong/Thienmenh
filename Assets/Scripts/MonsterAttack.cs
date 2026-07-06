using UnityEngine;
using System.Collections.Generic;

public class MonsterAttack : MonoBehaviour
{
    public int damage = 10;
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
                damage = ownerMonster.damage;
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

        if (NpcPetCompanion.BlocksMonsterAttacks(damageable.DamageTransform != null
            ? damageable.DamageTransform.gameObject
            : other.gameObject))
        {
            return;
        }

        if (owner != null &&
            damageable.DamageTransform != null &&
            damageable.DamageTransform.gameObject == owner)
        {
            return;
        }

        int appliedDamage = damage;
        if (owner != null && damageable.DamageTransform != null)
        {
            appliedDamage =
                NpcCombatTechniqueSystem.ModifyOutgoingDamage(
                    owner,
                    damageable.DamageTransform.gameObject,
                    damage);

            NpcSocialEventBus.PublishHostility(
                owner,
                damageable.DamageTransform.gameObject,
                Mathf.Clamp(appliedDamage, 1, 100),
                damageable.DamageTransform.position,
                NpcText.Dialogue("combatBeastReason"));
        }

        MarkHitForCurrentAttack(damageable);

        SmartNpcAI smartNpc =
            damageable as SmartNpcAI;
        if (smartNpc != null)
        {
            smartNpc.TakeDamage(appliedDamage, owner);
            return;
        }

        damageable.TakeDamage(appliedDamage);
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
