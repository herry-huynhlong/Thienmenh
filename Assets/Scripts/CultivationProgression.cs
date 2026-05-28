using UnityEngine;

public static class CultivationProgression
{
    public const int MaxStage = 9;

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
        int realmIndex =
            Mathf.Max(0, (int)realm);

        float stageBonus =
            1f + (Mathf.Clamp(stage, 1, MaxStage) - 1) * 0.08f;

        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                20f *
                Mathf.Pow(1.25f, realmIndex) *
                stageBonus));
    }

    public static int GetRealmPower(
        CultivationRealm realm,
        int stage)
    {
        return Mathf.Max(0, (int)realm) *
            MaxStage +
            Mathf.Clamp(stage, 1, MaxStage);
    }
}
