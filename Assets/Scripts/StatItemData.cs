using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    DanDuoc,
    PhapBao,
    VatLieu,
    CongPhap,
    ThucPham
}

public enum ItemGrade
{
    Ha,
    Trung,
    Thuong,
    Tien
}

public enum CultivationManualMastery
{
    None,
    TieuThanh,
    TrungThanh,
    DaiThanh
}

[System.Flags]
public enum ItemTargetType
{
    None = 0,
    Player = 1,
    Npc = 2,
    Monster = 4,
    All = Player | Npc | Monster
}

public enum StatType
{
    MaxHP,
    CurrentHP,
    Attack,
    Defense,
    EffectResistance,
    MoveSpeed,
    Damage,
    Cultivation,
    Breakthrough,
    Money,
    SpiritStone,
    Pill
}

[System.Serializable]
public class StatModifier
{
    public StatType statType;
    public int intValue;
    public float floatValue;
}

[CreateAssetMenu(
    fileName = "NewStatItem",
    menuName = "ThienMenh/Stat Item")]
public class StatItemData : ScriptableObject
{
    [Header("Info")]
    public string itemName;
    [TextArea]
    public string description;
    public ItemType itemType = ItemType.DanDuoc;
    public ItemGrade grade = ItemGrade.Ha;
    public ItemTargetType validTargets = ItemTargetType.All;
    public int price;
    public Sprite icon;
    public bool consumeOnUse = true;

    [Header("Vong Doi")]
    public bool canBeRefinedIntoPill;
    [Range(0f, 1f)]
    public float rawUseEfficiency = 0.35f;
    [Range(0f, 1f)]
    public float useSuccessChance = 1f;

    [Header("Do Ben")]
    public int maxDurability;
    public int durabilityLossPerUse = 1;
    public bool breaksAtZero = true;

    [Header("Dan Duoc")]
    public int hpBonus;
    public int cultivationBonus;
    public bool breakthroughRealm;

    [Header("Phap Bao")]
    public int damageBonus;
    public int armorBonus;
    public int effectResistanceBonus;

    [Header("Cong Phap")]
    public bool canBeTaught = true;
    public int studyProgressPerUse = 1;
    [Range(0f, 1f)]
    public float tieuThanhPower = 0.3f;
    [Range(0f, 1f)]
    public float trungThanhPower = 0.6f;
    [Range(0f, 1f)]
    public float daiThanhPower = 1f;

    [Header("Buff Tam Thoi")]
    public bool isTemporary;
    public float duration = 10f;

    [Header("Tuy Chinh Them")]
    public List<StatModifier> modifiers =
        new List<StatModifier>();

    public List<StatModifier> GetAllModifiers()
    {
        return GetAllModifiers(1f);
    }

    public List<StatModifier> GetAllModifiers(float powerMultiplier)
    {
        List<StatModifier> result =
            new List<StatModifier>();

        float useMultiplier =
            GetDirectUsePowerMultiplier() *
            Mathf.Max(0f, powerMultiplier);

        AddModifier(
            result,
            StatType.CurrentHP,
            Mathf.RoundToInt(hpBonus * useMultiplier));

        AddModifier(
            result,
            StatType.Cultivation,
            Mathf.RoundToInt(cultivationBonus * useMultiplier));

        if (breakthroughRealm)
        {
            AddModifier(result, StatType.Breakthrough, 1);
        }

        AddModifier(
            result,
            StatType.Attack,
            Mathf.RoundToInt(damageBonus * useMultiplier));

        AddModifier(
            result,
            StatType.Defense,
            Mathf.RoundToInt(armorBonus * useMultiplier));

        AddModifier(
            result,
            StatType.EffectResistance,
            Mathf.RoundToInt(effectResistanceBonus * useMultiplier));

        result.AddRange(modifiers);

        return result;
    }

    void AddModifier(
        List<StatModifier> targetModifiers,
        StatType statType,
        int value)
    {
        if (value == 0)
        {
            return;
        }

        targetModifiers.Add(
            new StatModifier
            {
                statType = statType,
                intValue = value
            });
    }

    public bool ApplyTo(GameObject target)
    {
        if (!CanUseOn(target))
        {
            return false;
        }

        if (isTemporary)
        {
            TimedStatItemBuff buff =
                target.AddComponent<TimedStatItemBuff>();

            buff.StartBuff(this);

            return true;
        }

        return ApplyTo(target, 1);
    }

    public bool RemoveFrom(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        return ApplyTo(target, -1);
    }

    public bool CanUseOn(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        return GetTargetType(target) != ItemTargetType.None &&
            (validTargets & GetTargetType(target)) != 0;
    }

    float GetDirectUsePowerMultiplier()
    {
        if (itemType == ItemType.VatLieu &&
            canBeRefinedIntoPill)
        {
            return Mathf.Clamp01(rawUseEfficiency);
        }

        return 1f;
    }

    public bool ConsumesWhenUsed()
    {
        return itemType == ItemType.DanDuoc ||
            itemType == ItemType.ThucPham ||
            consumeOnUse;
    }

    public bool UsesDurability()
    {
        return itemType == ItemType.PhapBao ||
            maxDurability > 0;
    }

    public int GetMaxDurability()
    {
        if (!UsesDurability())
        {
            return 0;
        }

        if (maxDurability > 0)
        {
            return maxDurability;
        }

        switch (grade)
        {
            case ItemGrade.Trung:
                return 180;
            case ItemGrade.Thuong:
                return 500;
            case ItemGrade.Tien:
                return 1200;
            default:
                return 80;
        }
    }

    public int GetDurabilityLossPerUse()
    {
        return Mathf.Max(1, durabilityLossPerUse);
    }

    public bool RollUseSuccess()
    {
        return Random.value <= Mathf.Clamp01(useSuccessChance);
    }

    public bool ApplyTo(GameObject target, int direction)
    {
        return ApplyTo(target, direction, 1f);
    }

    public bool ApplyTo(
        GameObject target,
        int direction,
        float powerMultiplier)
    {
        if (target == null)
        {
            return false;
        }

        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            characterStats.ApplyItem(
                this,
                direction,
                powerMultiplier);
            return true;
        }

        SmartNpcAI npc =
            target.GetComponent<SmartNpcAI>();

        if (npc != null)
        {
            npc.ApplyItem(this, direction, powerMultiplier);
            return true;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            villager.ApplyItem(this, direction, powerMultiplier);
            return true;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            monster.ApplyItem(this, direction, powerMultiplier);
            return true;
        }

        PlayerHealth player =
            target.GetComponent<PlayerHealth>();

        if (player != null)
        {
            player.ApplyItem(this, direction, powerMultiplier);
            return true;
        }

        return false;
    }

    ItemTargetType GetTargetType(GameObject target)
    {
        if (target.GetComponent<SmartNpcAI>() != null)
        {
            return ItemTargetType.Npc;
        }

        if (target.GetComponent<VillagerAI>() != null)
        {
            return ItemTargetType.Npc;
        }

        if (target.GetComponent<MonsterAI>() != null)
        {
            return ItemTargetType.Monster;
        }

        if (target.GetComponent<PlayerHealth>() != null ||
            target.GetComponent<CharacterStats>() != null)
        {
            return ItemTargetType.Player;
        }

        return ItemTargetType.None;
    }
}
