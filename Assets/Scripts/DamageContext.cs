using System;
using UnityEngine;

public enum DamageType
{
    Physical,
    Magical,
    True,
    Environmental,
    HeavenlyTribulation
}

public enum DamageSourceCategory
{
    Unknown,
    Player,
    Npc,
    Monster,
    Social,
    Heaven,
    Environment
}

public enum DamageElement
{
    None,
    Metal,
    Wood,
    Water,
    Fire,
    Earth,
    Lightning,
    Ice,
    Poison,
    Spiritual
}

public enum DamageBlockReason
{
    None,
    InvalidAmount,
    NoReceiver,
    TargetAlreadyDead,
    SelfDamage,
    FriendlyFire,
    PetImmunity,
    OutgoingModifierReducedToZero
}

/// <summary>
/// Carries all attribution and calculation inputs for one damage attempt.
/// The amount is attack power unless ignoreDefense or True damage is used.
/// </summary>
[Serializable]
public struct DamageContext
{
    public int amount;
    public GameObject attacker;
    public UnityEngine.Object source;
    public string sourceId;
    public string skillId;
    public string reason;
    public DamageType damageType;
    public DamageElement element;
    public DamageSourceCategory sourceCategory;
    public string attackerFactionId;
    public string targetFactionId;
    public Vector3 hitPoint;
    public bool hasHitPoint;

    public bool isCritical;
    public float criticalMultiplier;
    public int armorPenetration;
    [Range(0f, 1f)] public float armorPenetrationPercent;

    public bool applyOutgoingModifiers;
    public bool publishHostility;
    public bool allowFriendlyFire;
    public bool allowSelfDamage;
    public bool ignorePetImmunity;
    public bool ignoreDefense;

    public static DamageContext Legacy(
        int amount,
        GameObject attacker = null)
    {
        return new DamageContext
        {
            amount = amount,
            attacker = attacker,
            damageType = DamageType.Physical,
            element = DamageElement.None,
            sourceCategory = DamageSourceCategory.Unknown,
            criticalMultiplier = 1f
        };
    }

    public static DamageContext Attack(
        int amount,
        GameObject attacker,
        UnityEngine.Object source,
        DamageSourceCategory sourceCategory,
        DamageType damageType,
        string reason,
        Vector3 hitPoint,
        bool publishHostility = true)
    {
        return new DamageContext
        {
            amount = amount,
            attacker = attacker,
            source = source,
            reason = reason,
            damageType = damageType,
            element = DamageElement.None,
            sourceCategory = sourceCategory,
            hitPoint = hitPoint,
            hasHitPoint = true,
            criticalMultiplier = 1f,
            applyOutgoingModifiers = attacker != null,
            publishHostility = publishHostility
        };
    }

    public static DamageContext Environment(
        int amount,
        UnityEngine.Object source,
        DamageType damageType,
        string sourceId,
        Vector3 hitPoint)
    {
        return new DamageContext
        {
            amount = amount,
            source = source,
            sourceId = sourceId,
            damageType = damageType,
            element = damageType == DamageType.HeavenlyTribulation
                ? DamageElement.Lightning
                : DamageElement.None,
            sourceCategory = damageType == DamageType.HeavenlyTribulation
                ? DamageSourceCategory.Heaven
                : DamageSourceCategory.Environment,
            hitPoint = hitPoint,
            hasHitPoint = true,
            criticalMultiplier = 1f
        };
    }
}

public struct DamageResult
{
    public bool wasApplied;
    public bool wasKilled;
    public int requestedDamage;
    public int finalDamage;
    public int healthBefore;
    public int healthAfter;
    public GameObject target;
    public IDamageable receiver;
    public DamageBlockReason blockReason;

    public static DamageResult Blocked(
        DamageContext context,
        IDamageable receiver,
        GameObject target,
        DamageBlockReason reason)
    {
        return new DamageResult
        {
            requestedDamage = Mathf.Max(0, context.amount),
            receiver = receiver,
            target = target,
            blockReason = reason
        };
    }

    public static DamageResult Applied(
        DamageContext context,
        IDamageable receiver,
        GameObject target,
        int finalDamage,
        int healthBefore,
        int healthAfter)
    {
        return new DamageResult
        {
            wasApplied = finalDamage > 0,
            wasKilled = healthBefore > 0 && healthAfter <= 0,
            requestedDamage = Mathf.Max(0, context.amount),
            finalDamage = Mathf.Max(0, finalDamage),
            healthBefore = Mathf.Max(0, healthBefore),
            healthAfter = Mathf.Max(0, healthAfter),
            receiver = receiver,
            target = target,
            blockReason = DamageBlockReason.None
        };
    }
}
