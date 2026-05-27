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

    [Header("Dan Duoc")]
    public int hpBonus;
    public int cultivationBonus;
    public bool breakthroughRealm;

    [Header("Phap Bao")]
    public int damageBonus;
    public int armorBonus;
    public int effectResistanceBonus;

    [Header("Buff Tam Thoi")]
    public bool isTemporary;
    public float duration = 10f;

    [Header("Tuy Chinh Them")]
    public List<StatModifier> modifiers =
        new List<StatModifier>();

    public List<StatModifier> GetAllModifiers()
    {
        List<StatModifier> result =
            new List<StatModifier>();

        AddModifier(result, StatType.CurrentHP, hpBonus);
        AddModifier(result, StatType.Cultivation, cultivationBonus);

        if (breakthroughRealm)
        {
            AddModifier(result, StatType.Breakthrough, 1);
        }

        AddModifier(result, StatType.Attack, damageBonus);
        AddModifier(result, StatType.Defense, armorBonus);
        AddModifier(result, StatType.EffectResistance, effectResistanceBonus);

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

    public bool ApplyTo(GameObject target, int direction)
    {
        if (target == null)
        {
            return false;
        }

        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            characterStats.ApplyItem(this, direction);
            return true;
        }

        SmartNpcAI npc =
            target.GetComponent<SmartNpcAI>();

        if (npc != null)
        {
            npc.ApplyItem(this, direction);
            return true;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            villager.ApplyItem(this, direction);
            return true;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            monster.ApplyItem(this, direction);
            return true;
        }

        PlayerHealth player =
            target.GetComponent<PlayerHealth>();

        if (player != null)
        {
            player.ApplyItem(this, direction);
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
