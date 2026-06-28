using System;

public static class CombatStatCalculator
{
    public const double BaseHp = 100d;
    public const double BaseAttack = 10d;
    public const double BaseDefense = 3d;

    public const double MajorRealmMultiplier = 10d;
    public const double MonsterHpMultiplier = 1.2d;
    public const double MonsterAttackMultiplier = 1.5d;
    public const double MonsterDefenseMultiplier = 1.15d;

    public static double GetMajorMultiplier(int majorRealmIndex)
    {
        return Math.Pow(
            MajorRealmMultiplier,
            Math.Max(0, majorRealmIndex));
    }

    public static double GetMinorMultiplier(int minorStageIndex)
    {
        return Math.Pow(
            10d,
            Math.Max(0, minorStageIndex) / 10.0d);
    }

    public static double GetRealmMultiplier(
        int majorRealmIndex,
        int minorStageIndex)
    {
        return GetMajorMultiplier(majorRealmIndex) *
            GetMinorMultiplier(minorStageIndex);
    }

    public static double CalculateNpcHp(
        int majorRealmIndex,
        int minorStageIndex)
    {
        return BaseHp * GetRealmMultiplier(majorRealmIndex, minorStageIndex);
    }

    public static double CalculateNpcAttack(
        int majorRealmIndex,
        int minorStageIndex)
    {
        return BaseAttack * GetRealmMultiplier(majorRealmIndex, minorStageIndex);
    }

    public static double CalculateNpcDefense(
        int majorRealmIndex,
        int minorStageIndex)
    {
        return BaseDefense * GetRealmMultiplier(majorRealmIndex, minorStageIndex);
    }

    public static double CalculateMonsterHp(
        int majorRealmIndex,
        int minorStageIndex)
    {
        return CalculateNpcHp(majorRealmIndex, minorStageIndex) *
            MonsterHpMultiplier;
    }

    public static double CalculateMonsterAttack(
        int majorRealmIndex,
        int minorStageIndex)
    {
        return CalculateNpcAttack(majorRealmIndex, minorStageIndex) *
            MonsterAttackMultiplier;
    }

    public static double CalculateMonsterDefense(
        int majorRealmIndex,
        int minorStageIndex)
    {
        return CalculateNpcDefense(majorRealmIndex, minorStageIndex) *
            MonsterDefenseMultiplier;
    }

    public static double CalculateFinalDamage(
        double attack,
        double defense)
    {
        if (attack <= 0d)
        {
            return 0d;
        }

        defense = Math.Max(0d, defense);

        double rawDamage =
            attack * attack / (attack + defense);
        double minDamage =
            attack * 0.05d;

        double finalDamage =
            Math.Max(rawDamage, minDamage);

        return Math.Max(1d, finalDamage);
    }

    public static int CalculateFinalDamageInt(
        int attack,
        int defense)
    {
        return ClampToInt(CalculateFinalDamage(attack, defense));
    }

    public static int ClampToInt(double value)
    {
        if (double.IsNaN(value) || double.IsNegativeInfinity(value))
        {
            return 0;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value <= 0d)
        {
            return 0;
        }

        return (int)Math.Round(
            value,
            MidpointRounding.AwayFromZero);
    }
}
