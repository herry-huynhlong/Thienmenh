using UnityEngine;

public partial class MonsterAI
{
    public void ApplyItem(StatItemData item)
    {
        ApplyItem(item, 1);
    }

    public void ApplyItem(StatItemData item, int direction)
    {
        ApplyItem(item, direction, 1f);
    }

    public void ApplyItem(
        StatItemData item,
        int direction,
        float powerMultiplier)
    {
        if (item == null)
        {
            return;
        }

        if (direction > 0)
        {
            HeavenlyTribulationSystem.MarkPillProtectionIfEligible(
                gameObject,
                item);
        }

        foreach (StatModifier modifier in item.GetAllModifiers(powerMultiplier))
        {
            ApplyModifier(modifier, direction);
        }

        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
    }

    void ApplyModifier(StatModifier modifier, int direction)
    {
        if (modifier == null)
        {
            return;
        }

        int intValue = modifier.intValue * direction;
        float floatValue = modifier.floatValue * direction;

        switch (modifier.statType)
        {
            case StatType.MaxHP:
                baseMaxHP += intValue;
                RecalculateRealmStats(false);
                currentHP += intValue;
                break;
            case StatType.CurrentHP:
                currentHP += intValue;
                break;
            case StatType.Damage:
            case StatType.Attack:
                baseDamage += intValue;
                RecalculateRealmStats(false);
                break;
            case StatType.Defense:
                baseDefense += intValue;
                RecalculateRealmStats(false);
                break;
            case StatType.EffectResistance:
                baseEffectResistance += intValue;
                RecalculateRealmStats(false);
                break;
            case StatType.MoveSpeed:
                baseMoveSpeed += floatValue;
                RecalculateRealmStats(false);
                break;
            case StatType.Cultivation:
                if (direction > 0)
                {
                    AddCultivationExp(modifier.intValue);
                }
                else
                {
                    cultivationExp =
                        System.Math.Max(
                            0L,
                            cultivationExp - modifier.intValue);
                }
                break;
            case StatType.Breakthrough:
                if (direction > 0)
                {
                    Breakthrough();
                }
                break;
        }
    }
}
