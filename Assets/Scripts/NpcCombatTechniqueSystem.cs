using UnityEngine;

public static class NpcCombatTechniqueSystem
{
    public static int ModifyOutgoingDamage(
        GameObject actor,
        GameObject target,
        int damage)
    {
        if (actor == null ||
            damage <= 0 ||
            target == null)
        {
            return damage;
        }

        if (BicanhSessionManager.AreDungeonParticipantsAllies(actor, target))
        {
            return 0;
        }

        if (!HasAppliedManual(actor))
        {
            return damage;
        }

        NpcCombatTechniqueRuntime runtime =
            GetRuntime(actor);

        if (runtime == null)
        {
            return damage;
        }

        return runtime.ModifyOutgoingDamage(target, damage);
    }

    public static void ReactToDamageTaken(
        GameObject target,
        int incomingDamage)
    {
        if (target == null ||
            incomingDamage <= 0 ||
            NpcRoleUtility.IsDead(target) ||
            !HasAppliedManual(target))
        {
            return;
        }

        NpcCombatTechniqueRuntime runtime =
            GetRuntime(target);

        if (runtime == null)
        {
            return;
        }

        runtime.ReactToDamageTaken(incomingDamage);
    }

    static bool HasAppliedManual(GameObject owner)
    {
        ItemInventory inventory =
            owner != null ? owner.GetComponent<ItemInventory>() : null;

        if (inventory == null)
        {
            return false;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (IsAppliedManual(stack))
            {
                return true;
            }
        }

        return false;
    }

    static NpcCombatTechniqueRuntime GetRuntime(GameObject owner)
    {
        NpcCombatTechniqueRuntime runtime =
            owner.GetComponent<NpcCombatTechniqueRuntime>();

        if (runtime == null)
        {
            runtime = owner.AddComponent<NpcCombatTechniqueRuntime>();
        }

        return runtime;
    }

    internal static bool IsAppliedManual(ItemStack stack)
    {
        return stack != null &&
            stack.item != null &&
            stack.item.itemType == ItemType.CongPhap &&
            stack.applied &&
            !stack.broken &&
            !stack.item.IsManualBroken(stack) &&
            GetMasteryPower(stack.item, stack.mastery) > 0f;
    }

    internal static float GetMasteryPower(
        StatItemData item,
        CultivationManualMastery mastery)
    {
        if (item == null)
        {
            return 0f;
        }

        switch (mastery)
        {
            case CultivationManualMastery.TieuThanh:
                return Mathf.Max(0f, item.tieuThanhPower);
            case CultivationManualMastery.TrungThanh:
                return Mathf.Max(0f, item.trungThanhPower);
            case CultivationManualMastery.DaiThanh:
                return Mathf.Max(0f, item.daiThanhPower);
            default:
                return 0f;
        }
    }

    internal static float GetMoveSpeedBonus(
        StatItemData item,
        float masteryPower)
    {
        if (item == null)
        {
            return 0f;
        }

        float bonus = 0f;

        foreach (StatModifier modifier in item.GetAllModifiers(masteryPower))
        {
            if (modifier != null &&
                modifier.statType == StatType.MoveSpeed &&
                modifier.floatValue > 0f)
            {
                bonus += modifier.floatValue;
            }
        }

        if (bonus <= 0f &&
            (item.manualKind == ManualKind.Movement ||
            item.manualKind == ManualKind.Mixed))
        {
            bonus = 0.06f * Mathf.Max(0.25f, masteryPower);
        }

        return Mathf.Clamp(bonus, 0f, 0.8f);
    }

    internal static int GetCurrentHP(GameObject target)
    {
        CharacterStats stats = target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return stats.currentHP;
        }

        SmartNpcAI smartNpc = target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.currentHP;
        }

        VillagerAI villager = target.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.currentHP;
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();
        return monster != null ? monster.currentHP : 0;
    }

    internal static int GetMaxHP(GameObject target)
    {
        CharacterStats stats = target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return stats.finalHP;
        }

        SmartNpcAI smartNpc = target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.maxHP;
        }

        VillagerAI villager = target.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.maxHP;
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();
        return monster != null ? monster.maxHP : 1;
    }

    internal static int Heal(GameObject target, int amount)
    {
        if (target == null || amount <= 0)
        {
            return 0;
        }

        CharacterStats stats = target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            SmartNpcAI smartNpcWithStats = target.GetComponent<SmartNpcAI>();
            if (smartNpcWithStats != null)
            {
                return smartNpcWithStats.Heal(amount);
            }

            VillagerAI villagerWithStats = target.GetComponent<VillagerAI>();
            if (villagerWithStats != null)
            {
                return villagerWithStats.Heal(amount);
            }

            return stats.Heal(amount);
        }

        SmartNpcAI smartNpc = target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.Heal(amount);
        }

        VillagerAI villager = target.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.Heal(amount);
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();
        if (monster != null)
        {
            int before = monster.currentHP;
            monster.currentHP = Mathf.Clamp(
                monster.currentHP + amount,
                0,
                Mathf.Max(1, monster.maxHP));

            if (monster.entityProfile != null)
            {
                monster.entityProfile.stats.currentHP = monster.currentHP;
            }

            return monster.currentHP - before;
        }

        return 0;
    }
}

public sealed class NpcCombatTechniqueRuntime : MonoBehaviour
{
    const float AttackCooldown = 5f;
    const float DefenseCooldown = 8f;
    const float MovementCooldown = 6f;

    float nextAttackTime;
    float nextDefenseTime;
    float nextMovementTime;

    public int ModifyOutgoingDamage(
        GameObject target,
        int damage)
    {
        TryActivateMovementTechnique();

        if (Time.time < nextAttackTime)
        {
            return damage;
        }

        ItemStack stack =
            FindBestManual(GetAttackScore);

        if (stack == null)
        {
            return damage;
        }

        float masteryPower =
            NpcCombatTechniqueSystem.GetMasteryPower(
                stack.item,
                stack.mastery);

        int bonusDamage = Mathf.RoundToInt(
            Mathf.Max(0, stack.item.damageBonus) *
            Mathf.Clamp01(masteryPower) *
            0.35f);

        if (bonusDamage <= 0)
        {
            return damage;
        }

        nextAttackTime = Time.time + AttackCooldown;
        PlayTechniqueEffect(stack.item);

        return Mathf.Max(1, damage + bonusDamage);
    }

    public void ReactToDamageTaken(int incomingDamage)
    {
        TryActivateDefenseTechnique(incomingDamage);
        TryActivateMovementTechnique();
    }

    void TryActivateDefenseTechnique(int incomingDamage)
    {
        if (Time.time < nextDefenseTime)
        {
            return;
        }

        int maxHP = NpcCombatTechniqueSystem.GetMaxHP(gameObject);
        int currentHP = NpcCombatTechniqueSystem.GetCurrentHP(gameObject);
        int missingHP = Mathf.Max(0, maxHP - currentHP);

        if (missingHP <= 0)
        {
            return;
        }

        ItemStack stack = FindBestManual(GetDefenseScore);
        if (stack == null)
        {
            return;
        }

        float masteryPower =
            NpcCombatTechniqueSystem.GetMasteryPower(
                stack.item,
                stack.mastery);

        int healAmount = Mathf.RoundToInt(
            maxHP * (0.03f + 0.04f * Mathf.Clamp01(masteryPower)) +
            (Mathf.Max(0, stack.item.armorBonus) +
            Mathf.Max(0, stack.item.effectResistanceBonus)) *
            masteryPower * 0.2f);

        healAmount = Mathf.Clamp(
            healAmount,
            1,
            Mathf.Min(missingHP, Mathf.Max(1, incomingDamage)));

        int healed =
            NpcCombatTechniqueSystem.Heal(gameObject, healAmount);

        if (healed <= 0)
        {
            return;
        }

        nextDefenseTime = Time.time + DefenseCooldown;
        PlayTechniqueEffect(stack.item);
    }

    void TryActivateMovementTechnique()
    {
        if (Time.time < nextMovementTime)
        {
            return;
        }

        ItemStack stack = FindBestManual(GetMovementScore);
        if (stack == null)
        {
            return;
        }

        float masteryPower =
            NpcCombatTechniqueSystem.GetMasteryPower(
                stack.item,
                stack.mastery);

        float speedBonus =
            NpcCombatTechniqueSystem.GetMoveSpeedBonus(
                stack.item,
                masteryPower);

        if (speedBonus <= 0f)
        {
            return;
        }

        NpcCombatTechniqueBuff buff =
            GetComponent<NpcCombatTechniqueBuff>();

        if (buff == null)
        {
            buff = gameObject.AddComponent<NpcCombatTechniqueBuff>();
        }

        buff.RestartSpeedBuff(
            speedBonus,
            3f + 3f * Mathf.Clamp01(masteryPower));

        nextMovementTime = Time.time + MovementCooldown;
        PlayTechniqueEffect(stack.item);
    }

    ItemStack FindBestManual(System.Func<ItemStack, float> scoreGetter)
    {
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            return null;
        }

        ItemStack best = null;
        float bestScore = 0f;

        foreach (ItemStack stack in inventory.items)
        {
            if (!NpcCombatTechniqueSystem.IsAppliedManual(stack))
            {
                continue;
            }

            float score = scoreGetter(stack);
            if (score <= bestScore)
            {
                continue;
            }

            best = stack;
            bestScore = score;
        }

        return best;
    }

    float GetAttackScore(ItemStack stack)
    {
        if (stack.item.manualKind != ManualKind.Attack &&
            stack.item.manualKind != ManualKind.Mixed &&
            stack.item.damageBonus <= 0)
        {
            return 0f;
        }

        float masteryPower =
            NpcCombatTechniqueSystem.GetMasteryPower(
                stack.item,
                stack.mastery);

        return (Mathf.Max(0, stack.item.damageBonus) + 4f) * masteryPower;
    }

    float GetDefenseScore(ItemStack stack)
    {
        if (stack.item.manualKind != ManualKind.Defense &&
            stack.item.manualKind != ManualKind.Mixed &&
            stack.item.armorBonus <= 0 &&
            stack.item.effectResistanceBonus <= 0)
        {
            return 0f;
        }

        float masteryPower =
            NpcCombatTechniqueSystem.GetMasteryPower(
                stack.item,
                stack.mastery);

        return (Mathf.Max(0, stack.item.armorBonus) +
            Mathf.Max(0, stack.item.effectResistanceBonus) + 4f) * masteryPower;
    }

    float GetMovementScore(ItemStack stack)
    {
        float masteryPower =
            NpcCombatTechniqueSystem.GetMasteryPower(
                stack.item,
                stack.mastery);

        return NpcCombatTechniqueSystem.GetMoveSpeedBonus(
            stack.item,
            masteryPower);
    }

    void PlayTechniqueEffect(StatItemData item)
    {
        ItemEffectSpawner.PlayUseEffect(item, transform);
    }
}

public sealed class NpcCombatTechniqueBuff : MonoBehaviour
{
    float speedBonus;
    float endTime;
    bool active;

    public void RestartSpeedBuff(float bonus, float duration)
    {
        RemoveSpeedBonus();

        speedBonus = Mathf.Max(0f, bonus);
        if (speedBonus <= 0f)
        {
            Destroy(this);
            return;
        }

        ApplySpeedBonus(speedBonus);
        endTime = Time.time + Mathf.Max(0.1f, duration);
        active = true;
    }

    void Update()
    {
        if (!active || Time.time < endTime)
        {
            return;
        }

        RemoveSpeedBonus();
        Destroy(this);
    }

    void OnDisable()
    {
        RemoveSpeedBonus();
    }

    void ApplySpeedBonus(float value)
    {
        CharacterStats stats = GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.bonusMoveSpeed += value;
            stats.RecalculateStats();
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.moveSpeed = Mathf.Max(0.1f, smartNpc.moveSpeed + value);
            return;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.moveSpeed = Mathf.Max(0.1f, villager.moveSpeed + value);
            return;
        }

        MonsterAI monster = GetComponent<MonsterAI>();
        if (monster != null)
        {
            monster.moveSpeed = Mathf.Max(0.1f, monster.moveSpeed + value);
        }
    }

    void RemoveSpeedBonus()
    {
        if (!active)
        {
            return;
        }

        CharacterStats stats = GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.bonusMoveSpeed -= speedBonus;
            stats.RecalculateStats();
        }
        else
        {
            SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
            if (smartNpc != null)
            {
                smartNpc.moveSpeed = Mathf.Max(0.1f, smartNpc.moveSpeed - speedBonus);
            }

            VillagerAI villager = GetComponent<VillagerAI>();
            if (villager != null)
            {
                villager.moveSpeed = Mathf.Max(0.1f, villager.moveSpeed - speedBonus);
            }

            MonsterAI monster = GetComponent<MonsterAI>();
            if (monster != null)
            {
                monster.moveSpeed = Mathf.Max(0.1f, monster.moveSpeed - speedBonus);
            }
        }

        active = false;
        speedBonus = 0f;
    }
}
