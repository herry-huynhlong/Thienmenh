using UnityEngine;

public static class CultivationProgression
{
    public const int MaxStage = 9;
    public const int SpiritStoneBaseExp = 10;
    public const int NpcBaseMaxHP = 100;
    public const int NpcBaseAttack = 10;
    public const int NpcBaseDefense = 3;
    public const float MonsterStatMultiplier = 1.5f;

    static readonly int[] mortalStageExp =
    {
        100,
        140,
        200,
        300,
        450,
        700,
        1100,
        1700,
        3000
    };

    static readonly long[] realmCostMultiplier =
    {
        1L,
        50L,
        600L,
        24000L,
        1200000L,
        90000000L,
        9000000000L
    };

    public static int GetExpToNext(
        CultivationRealm realm,
        int stage,
        int baseExp)
    {
        long value =
            GetExpToNextLong(realm, stage, baseExp);

        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }

        return Mathf.Max(1, (int)value);
    }

    public static long GetExpToNextLong(
        CultivationRealm realm,
        int stage,
        int baseExp)
    {
        if (realm == CultivationRealm.Tribulation)
        {
            return long.MaxValue;
        }

        int realmIndex =
            Mathf.Clamp(
                (int)realm,
                0,
                realmCostMultiplier.Length - 1);

        int stageIndex =
            Mathf.Clamp(stage, 1, MaxStage) - 1;

        long baseStageExp =
            realmIndex == 0
            ? mortalStageExp[stageIndex]
            : Mathf.Max(1, baseExp) *
                (long)Mathf.RoundToInt(
                    Mathf.Pow(1.7f, stageIndex));

        return baseStageExp *
            realmCostMultiplier[realmIndex];
    }

    public static int GetSpiritStoneExp(
        CultivationRealm realm,
        int stage)
    {
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                SpiritStoneBaseExp *
                GetSpiritStoneEfficiency(realm)));
    }

    public static float GetSpiritStoneEfficiency(
        CultivationRealm realm)
    {
        switch (realm)
        {
            case CultivationRealm.QiRefining:
                return 0.9f;

            case CultivationRealm.Foundation:
                return 0.7f;

            case CultivationRealm.GoldenCore:
                return 0.45f;

            case CultivationRealm.NascentSoul:
                return 0.25f;

            case CultivationRealm.SoulFormation:
            case CultivationRealm.Tribulation:
                return 0.1f;

            default:
                return 1f;
        }
    }

    public static bool RequiresHeavenlyTribulation(
        CultivationRealm realm,
        int stage)
    {
        return realm >= CultivationRealm.QiRefining &&
            realm < CultivationRealm.Tribulation &&
            stage >= MaxStage;
    }

    public static bool IsMajorRealmBreakthrough(
        CultivationRealm realm)
    {
        return realm >= CultivationRealm.QiRefining &&
            realm < CultivationRealm.Tribulation;
    }

    public static CultivationRealm GetNextRealm(
        CultivationRealm realm)
    {
        if (realm >= CultivationRealm.Tribulation)
        {
            return CultivationRealm.Tribulation;
        }

        return (CultivationRealm)((int)realm + 1);
    }

    public static int GetRealmPower(
        CultivationRealm realm,
        int stage)
    {
        return Mathf.Max(0, (int)realm) *
            MaxStage +
            Mathf.Clamp(stage, 1, MaxStage);
    }

    public static float GetStatPower(
        CultivationRealm realm,
        int stage,
        EntityKind kind)
    {
        int majorRealmIndex = Mathf.Max(0, (int)realm);
        int minorStageIndex = Mathf.Clamp(stage, 1, MaxStage) - 1;

        return Mathf.Max(
            1f,
            (float)CombatStatCalculator.GetRealmMultiplier(
                majorRealmIndex,
                minorStageIndex));
    }

    public static float GetCultivationRealmMultiplier(
        CultivationRealm realm,
        int stage)
    {
        int majorRealmIndex = Mathf.Max(0, (int)realm);
        int stageIndex = Mathf.Clamp(stage, 1, MaxStage);

        float majorMultiplier = majorRealmIndex <= 0
            ? 0.25f
            : Mathf.Pow(10f, Mathf.Max(0, majorRealmIndex - 1));
        float stageMultiplier = 1f + Mathf.Max(0, stageIndex - 1) * 0.1f;

        return Mathf.Max(0.1f, majorMultiplier * stageMultiplier);
    }

    public static float GetEntityStatMultiplier(EntityKind kind)
    {
        return kind == EntityKind.Beast
            ? MonsterStatMultiplier
            : 1f;
    }

    public static int GetConfiguredBaseMaxHP()
    {
        return CombatStatCalculator.ClampToInt(CombatStatCalculator.BaseHp);
    }

    public static int GetConfiguredBaseAttack()
    {
        return CombatStatCalculator.ClampToInt(CombatStatCalculator.BaseAttack);
    }

    public static int GetConfiguredBaseDefense()
    {
        return CombatStatCalculator.ClampToInt(CombatStatCalculator.BaseDefense);
    }
}

