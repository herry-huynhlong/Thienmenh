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
                Add(slots, NpcScheduleActivity.ReturnHome, 0f, 5f);
                Add(slots, NpcScheduleActivity.Work, 5f, 11f);
                Add(slots, NpcScheduleActivity.ReturnHome, 11f, 13f);
                Add(slots, NpcScheduleActivity.Work, 13f, 17f);
                Add(slots, NpcScheduleActivity.ReturnHome, 17f, 18f);
                Add(slots, NpcScheduleActivity.SellGoods, 18f, 19f);
                Add(slots, NpcScheduleActivity.BuyGoods, 19f, 20f);
                Add(slots, NpcScheduleActivity.Sleep, 20f, 24f);
                return;
            case VillagerJob.Alchemist:
            case VillagerJob.Blacksmith:
                Add(slots, NpcScheduleActivity.ReturnHome, 0f, 5f);
                Add(slots, NpcScheduleActivity.Work, 5f, 11f);
                Add(slots, NpcScheduleActivity.ReturnHome, 11f, 13f);
                Add(slots, NpcScheduleActivity.Work, 13f, 17f);
                Add(slots, NpcScheduleActivity.ReturnHome, 17f, 18f);
                Add(slots, NpcScheduleActivity.SellGoods, 18f, 19f);
                Add(slots, NpcScheduleActivity.BuyGoods, 19f, 20f);
                Add(slots, NpcScheduleActivity.Sleep, 20f, 24f);
                return;
            case VillagerJob.Healer:
                Add(slots, NpcScheduleActivity.TakeTask, 0f, 24f);
                return;
            default:
                Add(slots, NpcScheduleActivity.ReturnHome, 0f, 5f);
                Add(slots, NpcScheduleActivity.Work, 5f, 11f);
                Add(slots, NpcScheduleActivity.ReturnHome, 11f, 13f);
                Add(slots, NpcScheduleActivity.Work, 13f, 17f);
                Add(slots, NpcScheduleActivity.ReturnHome, 17f, 18f);
                Add(slots, NpcScheduleActivity.SellGoods, 18f, 19f);
                Add(slots, NpcScheduleActivity.BuyGoods, 19f, 20f);
                Add(slots, NpcScheduleActivity.Sleep, 20f, 24f);
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

        Add(slots, NpcScheduleActivity.Cultivate, 0f, 24f);
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
