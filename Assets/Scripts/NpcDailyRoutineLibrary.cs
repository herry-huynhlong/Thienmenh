using System.Collections.Generic;
using UnityEngine;

public static class NpcDailyRoutineLibrary
{
    public static void BuildDefaultSchedule(
        GameObject npc,
        NpcLifePath lifePath,
        List<NpcScheduleSlot> slots)
    {
        if (slots == null)
        {
            return;
        }

        slots.Clear();

        switch (lifePath)
        {
            case NpcLifePath.Cultivator:
                BuildCultivatorSchedule(npc, slots);
                return;
            case NpcLifePath.SemiCultivator:
                BuildSemiCultivatorSchedule(npc, slots);
                return;
            default:
                BuildCommonerSchedule(npc, slots);
                return;
        }
    }

    static void BuildCommonerSchedule(
        GameObject npc,
        List<NpcScheduleSlot> slots)
    {
        VillagerAI villager = npc != null
            ? npc.GetComponent<VillagerAI>()
            : null;

        VillagerJob job = villager != null
            ? villager.job
            : VillagerJob.None;

        switch (job)
        {
            case VillagerJob.Trader:
                Add(slots, NpcScheduleActivity.BuyGoods, 0f, 24f);
                return;
            case VillagerJob.Farmer:
            case VillagerJob.Fisher:
            case VillagerJob.Hunter:
            case VillagerJob.Worker:
            case VillagerJob.Guard:
                Add(slots, NpcScheduleActivity.Sleep, 20f, 5f);
                Add(slots, NpcScheduleActivity.Work, 5f, 11f);
                Add(slots, NpcScheduleActivity.ReturnHome, 11f, 13f);
                Add(slots, NpcScheduleActivity.Work, 13f, 17f);
                Add(slots, NpcScheduleActivity.ReturnHome, 17f, 18f);
                Add(slots, NpcScheduleActivity.SellGoods, 18f, 19f);
                Add(slots, NpcScheduleActivity.BuyGoods, 19f, 20f);
                return;
            case VillagerJob.Alchemist:
            case VillagerJob.Blacksmith:
                Add(slots, NpcScheduleActivity.Sleep, 20f, 5f);
                Add(slots, NpcScheduleActivity.Work, 5f, 11f);
                Add(slots, NpcScheduleActivity.ReturnHome, 11f, 13f);
                Add(slots, NpcScheduleActivity.Work, 13f, 17f);
                Add(slots, NpcScheduleActivity.ReturnHome, 17f, 18f);
                Add(slots, NpcScheduleActivity.SellGoods, 18f, 19f);
                Add(slots, NpcScheduleActivity.BuyGoods, 19f, 20f);
                return;
            case VillagerJob.Healer:
                Add(slots, NpcScheduleActivity.TakeTask, 0f, 24f);
                return;
            default:
                Add(slots, NpcScheduleActivity.Sleep, 20f, 5f);
                Add(slots, NpcScheduleActivity.Work, 5f, 11f);
                Add(slots, NpcScheduleActivity.ReturnHome, 11f, 13f);
                Add(slots, NpcScheduleActivity.Work, 13f, 17f);
                Add(slots, NpcScheduleActivity.ReturnHome, 17f, 18f);
                Add(slots, NpcScheduleActivity.SellGoods, 18f, 19f);
                Add(slots, NpcScheduleActivity.BuyGoods, 19f, 20f);
                return;
        }
    }

    static void BuildSemiCultivatorSchedule(
        GameObject npc,
        List<NpcScheduleSlot> slots)
    {
        Add(slots, NpcScheduleActivity.Work, 0f, 24f);
    }

    static void BuildCultivatorSchedule(
        GameObject npc,
        List<NpcScheduleSlot> slots)
    {
        NpcAlchemyAgent alchemyAgent = npc != null
            ? npc.GetComponent<NpcAlchemyAgent>()
            : null;
        if (alchemyAgent != null)
        {
            Add(slots, NpcScheduleActivity.Alchemy, 0f, 24f);
            return;
        }

        NpcForgeAgent forgeAgent = npc != null
            ? npc.GetComponent<NpcForgeAgent>()
            : null;
        if (forgeAgent != null)
        {
            Add(slots, NpcScheduleActivity.Forge, 0f, 24f);
            return;
        }

        NpcScheduleActivity dawnActivity =
            PickCultivatorFieldActivity(
                npc,
                salt: 0,
                disallow: NpcScheduleActivity.Cultivate);
        NpcScheduleActivity middayActivity =
            PickCultivatorFieldActivity(
                npc,
                salt: 1,
                disallow: dawnActivity);
        NpcScheduleActivity nightActivity =
            PickCultivatorFieldActivity(
                npc,
                salt: 2,
                disallow: middayActivity);

        Add(slots, NpcScheduleActivity.Cultivate, 0f, 6f);
        Add(slots, dawnActivity, 6f, 12f);
        Add(slots, middayActivity, 12f, 17f);
        Add(slots, NpcScheduleActivity.SellGoods, 17f, 18f);
        Add(slots, NpcScheduleActivity.BuyGoods, 18f, 19f);
        Add(slots, nightActivity, 19f, 24f);
    }

    static NpcScheduleActivity PickCultivatorFieldActivity(
        GameObject npc,
        int salt,
        NpcScheduleActivity disallow)
    {
        List<NpcScheduleActivity> options =
            new List<NpcScheduleActivity>();
        SmartNpcAI smartNpc =
            npc != null
            ? npc.GetComponent<SmartNpcAI>()
            : null;

        if (smartNpc != null)
        {
            if (smartNpc.canFight)
            {
                options.Add(NpcScheduleActivity.Hunt);
            }

            if (smartNpc.canGather)
            {
                options.Add(NpcScheduleActivity.Gather);
            }
        }

        if (options.Count == 0)
        {
            options.Add(NpcScheduleActivity.Cultivate);
        }

        int seed = GetCultivatorScheduleSeed(npc);
        int index = PositiveModulo(seed + salt * 7, options.Count);
        NpcScheduleActivity picked = options[index];

        if (picked == disallow &&
            options.Count > 1)
        {
            picked = options[(index + 1) % options.Count];
        }

        return picked;
    }

    static NpcScheduleActivity PickCultivatorActivity(
        GameObject npc,
        bool useResourcePriority,
        int salt,
        NpcScheduleActivity disallow)
    {
        List<NpcScheduleActivity> options =
            useResourcePriority
            ? BuildCultivatorResourcePriorityPool(npc)
            : BuildCultivatorBalancedPool(npc);

        if (options.Count == 0)
        {
            return NpcScheduleActivity.Cultivate;
        }

        int seed = GetCultivatorScheduleSeed(npc);
        int index = PositiveModulo(seed + salt * 7, options.Count);
        NpcScheduleActivity picked = options[index];

        if (picked == disallow &&
            options.Count > 1)
        {
            picked = options[(index + 1) % options.Count];
        }

        return picked;
    }

    static List<NpcScheduleActivity> BuildCultivatorResourcePriorityPool(
        GameObject npc)
    {
        List<NpcScheduleActivity> options =
            new List<NpcScheduleActivity>();
        SmartNpcAI smartNpc =
            npc != null
            ? npc.GetComponent<SmartNpcAI>()
            : null;

        if (smartNpc != null)
        {
            if (smartNpc.canFight)
            {
                options.Add(NpcScheduleActivity.Hunt);
            }

            if (smartNpc.canGather)
            {
                options.Add(NpcScheduleActivity.Gather);
            }

            if (smartNpc.dailyTaskVisitEnabled)
            {
                options.Add(NpcScheduleActivity.TakeTask);
            }

            if (smartNpc.canCultivate)
            {
                options.Add(NpcScheduleActivity.Cultivate);
            }
        }

        if (options.Count == 0)
        {
            options.Add(NpcScheduleActivity.Cultivate);
        }

        return options;
    }

    static List<NpcScheduleActivity> BuildCultivatorBalancedPool(
        GameObject npc)
    {
        List<NpcScheduleActivity> options =
            new List<NpcScheduleActivity>();
        SmartNpcAI smartNpc =
            npc != null
            ? npc.GetComponent<SmartNpcAI>()
            : null;

        if (smartNpc != null &&
            smartNpc.canCultivate)
        {
            options.Add(NpcScheduleActivity.Cultivate);
        }

        if (smartNpc != null &&
            smartNpc.dailyTaskVisitEnabled)
        {
            options.Add(NpcScheduleActivity.TakeTask);
        }

        if (smartNpc != null &&
            smartNpc.canFight)
        {
            options.Add(NpcScheduleActivity.Hunt);
        }

        if (smartNpc != null &&
            smartNpc.canGather)
        {
            options.Add(NpcScheduleActivity.Gather);
        }

        if (options.Count == 0)
        {
            options.Add(NpcScheduleActivity.Cultivate);
        }

        return options;
    }

    static float GetCultivatorPhaseOffset(GameObject npc)
    {
        int seed = GetCultivatorScheduleSeed(npc);
        return PositiveModulo(seed / 11, 4) * 0.25f;
    }

    static int GetCultivatorScheduleSeed(GameObject npc)
    {
        if (npc == null)
        {
            return 0;
        }

        int seed = 17;
        Vector3 position = npc.transform.position;
        seed = unchecked(seed * 31 + Mathf.RoundToInt(position.x * 100f));
        seed = unchecked(seed * 31 + Mathf.RoundToInt(position.y * 100f));

        EntityProfile profile = npc.GetComponent<EntityProfile>();
        if (profile != null)
        {
            seed = unchecked(seed * 31 + profile.identity.age);
            seed = unchecked(seed * 31 + (int)profile.identity.gender);
            seed = unchecked(seed * 31 + (int)profile.kind);
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            seed = unchecked(seed * 31 + (int)smartNpc.realm);
            seed = unchecked(seed * 31 + smartNpc.realmStage);
            seed = unchecked(seed * 31 + smartNpc.comprehension);
            seed = unchecked(seed * 31 + (int)smartNpc.physique);
        }

        if (seed == int.MinValue)
        {
            return 0;
        }

        return Mathf.Abs(seed);
    }

    static int PositiveModulo(int value, int divisor)
    {
        if (divisor <= 0)
        {
            return 0;
        }

        int result = value % divisor;
        return result < 0
            ? result + divisor
            : result;
    }

    static void Add(
        List<NpcScheduleSlot> slots,
        NpcScheduleActivity activity,
        float startHour,
        float endHour)
    {
        slots.Add(
            new NpcScheduleSlot
            {
                activity = activity,
                startHour = Mathf.Repeat(startHour, 24f),
                endHour = Mathf.Repeat(endHour, 24f)
            });
    }
}
