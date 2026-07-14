using System;
using UnityEngine;

/// <summary>
/// The single public entry point for gameplay damage. It resolves the
/// canonical receiver, applies common immunity/attribution rules, then asks
/// that receiver to mutate health exactly once.
/// </summary>
public static class DamageSystem
{
    public static event Action<DamageContext, DamageResult> OnDamageResolved;

    public static DamageResult Apply(
        GameObject target,
        DamageContext context)
    {
        if (!TryResolveReceiver(target, out IDamageable receiver))
        {
            DamageResult missing = DamageResult.Blocked(
                context,
                null,
                target,
                DamageBlockReason.NoReceiver);
            Notify(context, missing);
            return missing;
        }

        return Apply(receiver, context);
    }

    public static DamageResult Apply(
        IDamageable receiver,
        DamageContext context)
    {
        receiver = ResolveCanonicalReceiver(receiver);
        GameObject target = GetTargetObject(receiver);

        if (receiver == null || target == null)
        {
            DamageResult missing = DamageResult.Blocked(
                context,
                receiver,
                target,
                DamageBlockReason.NoReceiver);
            Notify(context, missing);
            return missing;
        }

        if (context.amount <= 0)
        {
            return Block(
                context,
                receiver,
                target,
                DamageBlockReason.InvalidAmount);
        }

        if (receiver.IsDead)
        {
            return Block(
                context,
                receiver,
                target,
                DamageBlockReason.TargetAlreadyDead);
        }

        if (!context.allowSelfDamage && IsSameEntity(context.attacker, target))
        {
            return Block(
                context,
                receiver,
                target,
                DamageBlockReason.SelfDamage);
        }

        if (!context.allowFriendlyFire &&
            context.attacker != null &&
            (AreSameDeclaredFaction(context) ||
             BicanhSessionManager.AreDungeonParticipantsAllies(
                 context.attacker,
                 target)))
        {
            return Block(
                context,
                receiver,
                target,
                DamageBlockReason.FriendlyFire);
        }

        if (context.sourceCategory == DamageSourceCategory.Unknown)
        {
            context.sourceCategory = InferSourceCategory(context.attacker);
        }

        if (!context.ignorePetImmunity && IsBlockedByPet(context, target))
        {
            return Block(
                context,
                receiver,
                target,
                DamageBlockReason.PetImmunity);
        }

        if (context.applyOutgoingModifiers && context.attacker != null)
        {
            context.amount =
                NpcCombatTechniqueSystem.ModifyOutgoingDamage(
                    context.attacker,
                    target,
                    context.amount);
        }

        if (context.amount <= 0)
        {
            return Block(
                context,
                receiver,
                target,
                DamageBlockReason.OutgoingModifierReducedToZero);
        }

        DamageResult result = receiver.ReceiveDamage(context);
        if (result.target == null)
        {
            result.target = target;
        }

        if (result.receiver == null)
        {
            result.receiver = receiver;
        }

        if (result.wasApplied &&
            result.finalDamage > 0 &&
            context.publishHostility &&
            context.attacker != null &&
            !IsSameEntity(context.attacker, target))
        {
            Vector3 position = context.hasHitPoint
                ? context.hitPoint
                : target.transform.position;

            NpcSocialEventBus.PublishHostility(
                context.attacker,
                target,
                Mathf.Clamp(result.finalDamage, 1, 100),
                position,
                string.IsNullOrEmpty(context.reason)
                    ? NpcText.Dialogue("attackReasonFallback")
                    : context.reason);
        }

        Notify(context, result);
        return result;
    }

    public static int CalculateFinalDamage(
        DamageContext context,
        int defense)
    {
        int requested = Mathf.Max(0, context.amount);
        if (requested <= 0)
        {
            return 0;
        }

        int finalDamage;
        if (context.ignoreDefense || context.damageType == DamageType.True)
        {
            finalDamage = requested;
        }
        else
        {
            float percent = Mathf.Clamp01(context.armorPenetrationPercent);
            int effectiveDefense = Mathf.Max(
                0,
                Mathf.RoundToInt(Mathf.Max(0, defense) * (1f - percent)) -
                Mathf.Max(0, context.armorPenetration));
            finalDamage = CombatStatCalculator.CalculateFinalDamageInt(
                requested,
                effectiveDefense);
        }

        if (context.isCritical)
        {
            float multiplier = Mathf.Max(1f, context.criticalMultiplier);
            finalDamage = CombatStatCalculator.ClampToInt(
                finalDamage * (double)multiplier);
        }

        return Mathf.Max(0, finalDamage);
    }

    public static bool TryResolveReceiver(
        GameObject target,
        out IDamageable receiver)
    {
        receiver = null;
        if (target == null)
        {
            return false;
        }

        VillagerAI villager = target.GetComponentInParent<VillagerAI>(true);
        if (villager != null)
        {
            receiver = villager;
            return true;
        }

        SmartNpcAI smartNpc = target.GetComponentInParent<SmartNpcAI>(true);
        if (smartNpc != null)
        {
            receiver = smartNpc;
            return true;
        }

        MonsterAI monster = target.GetComponentInParent<MonsterAI>(true);
        if (monster != null)
        {
            receiver = monster;
            return true;
        }

        PlayerHealth player = target.GetComponentInParent<PlayerHealth>(true);
        if (player != null)
        {
            receiver = player;
            return true;
        }

        CharacterStats stats = target.GetComponentInParent<CharacterStats>(true);
        if (stats != null)
        {
            receiver = stats;
            return true;
        }

        receiver = target.GetComponentInParent<IDamageable>();
        return receiver != null;
    }

    public static DamageSourceCategory InferSourceCategory(
        GameObject attacker)
    {
        if (attacker == null)
        {
            return DamageSourceCategory.Environment;
        }

        if (attacker.GetComponentInParent<MonsterAI>() != null)
        {
            return DamageSourceCategory.Monster;
        }

        if (attacker.GetComponentInParent<VillagerAI>() != null ||
            attacker.GetComponentInParent<SmartNpcAI>() != null)
        {
            return DamageSourceCategory.Npc;
        }

        if (attacker.GetComponentInParent<PlayerHealth>() != null)
        {
            return DamageSourceCategory.Player;
        }

        return DamageSourceCategory.Unknown;
    }

    static IDamageable ResolveCanonicalReceiver(IDamageable receiver)
    {
        if (receiver == null)
        {
            return null;
        }

        Component component = receiver as Component;
        if (component != null &&
            TryResolveReceiver(component.gameObject, out IDamageable canonical))
        {
            return canonical;
        }

        return receiver;
    }

    static bool IsBlockedByPet(DamageContext context, GameObject target)
    {
        switch (context.sourceCategory)
        {
            case DamageSourceCategory.Monster:
                return NpcPetCompanion.BlocksMonsterAttacks(target);
            case DamageSourceCategory.Npc:
            case DamageSourceCategory.Player:
                return NpcPetCompanion.BlocksNpcAttacks(target);
            case DamageSourceCategory.Social:
                return NpcPetCompanion.BlocksSocialDamage(target);
            default:
                return false;
        }
    }

    static bool AreSameDeclaredFaction(DamageContext context)
    {
        return
            !string.IsNullOrEmpty(context.attackerFactionId) &&
            !string.IsNullOrEmpty(context.targetFactionId) &&
            string.Equals(
                context.attackerFactionId,
                context.targetFactionId,
                StringComparison.Ordinal);
    }

    static bool IsSameEntity(GameObject first, GameObject second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        return first == second ||
            first.transform.IsChildOf(second.transform) ||
            second.transform.IsChildOf(first.transform);
    }

    static GameObject GetTargetObject(IDamageable receiver)
    {
        if (receiver == null)
        {
            return null;
        }

        Transform damageTransform = receiver.DamageTransform;
        if (damageTransform != null)
        {
            return damageTransform.gameObject;
        }

        Component component = receiver as Component;
        return component != null ? component.gameObject : null;
    }

    static DamageResult Block(
        DamageContext context,
        IDamageable receiver,
        GameObject target,
        DamageBlockReason reason)
    {
        DamageResult result = DamageResult.Blocked(
            context,
            receiver,
            target,
            reason);
        Notify(context, result);
        return result;
    }

    static void Notify(DamageContext context, DamageResult result)
    {
        OnDamageResolved?.Invoke(context, result);
    }
}
