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
        Add(slots, NpcScheduleActivity.Sleep, 20f, 5f);
        Add(slots, NpcScheduleActivity.Work, 5f, 11f);
        Add(slots, NpcScheduleActivity.ReturnHome, 11f, 13f);
        Add(slots, NpcScheduleActivity.Work, 13f, 17f);
        Add(slots, NpcScheduleActivity.ReturnHome, 17f, 20f);
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

        SmartNpcAI smartNpc = npc != null
            ? npc.GetComponent<SmartNpcAI>()
            : null;
        if (smartNpc != null)
        {
            BuildSmartCultivatorSchedule(npc, slots);
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

        ApplySchedulePhaseOffset(slots, GetCultivatorPhaseOffset(npc));
    }

    static void BuildSmartCultivatorSchedule(
        GameObject npc,
        List<NpcScheduleSlot> slots)
    {
        const float restGapHours = 5f / 60f;

        Add(slots, NpcScheduleActivity.DoMission, 7f, 13f - restGapHours);
        Add(slots, NpcScheduleActivity.Idle, 13f - restGapHours, 13f);
        Add(slots, NpcScheduleActivity.FreeHuntAndGather, 13f, 18f - restGapHours);
        Add(slots, NpcScheduleActivity.Idle, 18f - restGapHours, 18f);
        Add(slots, NpcScheduleActivity.TradeBuySell, 18f, 19f - restGapHours);
        Add(slots, NpcScheduleActivity.Idle, 19f - restGapHours, 19f);
        Add(slots, NpcScheduleActivity.Cultivate, 19f, 7f - restGapHours);
        Add(slots, NpcScheduleActivity.Idle, 7f - restGapHours, 7f);

        // SmartAI dùng lịch gốc nhưng lệch giờ theo seed từng NPC
        // để tránh tất cả cùng dồn vào một hoạt động tại cùng thời điểm.
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
            if (smartNpc.canFight &&
                smartNpc.canCompeteResource)
            {
                options.Add(NpcScheduleActivity.Hunt);
            }

            if (smartNpc.canGather &&
                smartNpc.canCompeteResource)
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
            if (smartNpc.canFight &&
                smartNpc.canCompeteResource)
            {
                options.Add(NpcScheduleActivity.Hunt);
            }

            if (smartNpc.canGather &&
                smartNpc.canCompeteResource)
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
            smartNpc.canFight &&
            smartNpc.canCompeteResource)
        {
            options.Add(NpcScheduleActivity.Hunt);
        }

        if (smartNpc != null &&
            smartNpc.canGather &&
            smartNpc.canCompeteResource)
        {
            options.Add(NpcScheduleActivity.Gather);
        }

        if (options.Count == 0)
        {
            options.Add(NpcScheduleActivity.Cultivate);
        }

        return options;
    }

    static List<NpcScheduleActivity> BuildSmartCultivatorSupportPool(
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
            options.Add(NpcScheduleActivity.TakeTask);
        }

        if (options.Count == 0)
        {
            options.Add(NpcScheduleActivity.Cultivate);
        }

        return options;
    }

    static NpcScheduleActivity PickSmartSupportActivity(
        GameObject npc,
        int salt)
    {
        List<NpcScheduleActivity> options =
            BuildSmartCultivatorSupportPool(npc);

        int seed = GetCultivatorScheduleSeed(npc);
        int index = PositiveModulo(seed + salt * 7, options.Count);
        return options[index];
    }

    static NpcScheduleActivity[] ApplySmartBlockOrder(
        NpcScheduleActivity[] blocks,
        int seed)
    {
        if (blocks == null || blocks.Length != 4)
        {
            return blocks;
        }

        switch (PositiveModulo(seed, 4))
        {
            case 0:
                return new[]
                {
                    blocks[0],
                    blocks[1],
                    blocks[2],
                    blocks[3]
                };
            case 1:
                return new[]
                {
                    blocks[3],
                    blocks[2],
                    blocks[1],
                    blocks[0]
                };
            case 2:
                return new[]
                {
                    blocks[1],
                    blocks[2],
                    blocks[3],
                    blocks[0]
                };
            default:
                return new[]
                {
                    blocks[2],
                    blocks[3],
                    blocks[0],
                    blocks[1]
                };
        }
    }

    static float GetCultivatorPhaseOffset(GameObject npc)
    {
        int seed = GetCultivatorScheduleSeed(npc);
        return PositiveModulo(seed / 11, 8) * 0.5f;
    }

    static float GetSmartCultivatorPhaseOffset(GameObject npc)
    {
        SmartNpcAI smartNpc =
            npc != null
            ? npc.GetComponent<SmartNpcAI>()
            : null;
        if (smartNpc == null)
        {
            return 0f;
        }

        SmartNpcAI[] smartNpcs =
            Object.FindObjectsByType<SmartNpcAI>(
                FindObjectsInactive.Exclude);
        if (smartNpcs == null || smartNpcs.Length == 0)
        {
            return 0f;
        }

        List<SmartNpcAI> ordered = new List<SmartNpcAI>(smartNpcs.Length);
        for (int i = 0; i < smartNpcs.Length; i++)
        {
            SmartNpcAI candidate = smartNpcs[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            ordered.Add(candidate);
        }

        if (ordered.Count <= 1)
        {
            return 0f;
        }

        ordered.Sort(CompareSmartNpcOrder);

        int index = ordered.IndexOf(smartNpc);
        if (index < 0)
        {
            return 0f;
        }

        float stepHours =
            Mathf.Clamp(24f / Mathf.Max(1, ordered.Count), 0.5f, 4f);
        return Mathf.Repeat(index * stepHours, 24f);
    }

    static int CompareSmartNpcOrder(
        SmartNpcAI first,
        SmartNpcAI second)
    {
        if (first == second)
        {
            return 0;
        }

        string firstKey = GetSmartNpcOrderKey(first);
        string secondKey = GetSmartNpcOrderKey(second);

        int compare = string.CompareOrdinal(firstKey, secondKey);
        if (compare != 0)
        {
            return compare;
        }

        return first.GetInstanceID().CompareTo(second.GetInstanceID());
    }

    static string GetSmartNpcOrderKey(SmartNpcAI smartNpc)
    {
        if (smartNpc == null)
        {
            return string.Empty;
        }

        NPCIdentity identity =
            smartNpc.GetComponent<NPCIdentity>() ??
            smartNpc.GetComponentInParent<NPCIdentity>(true) ??
            smartNpc.GetComponentInChildren<NPCIdentity>(true);
        if (identity != null &&
            !string.IsNullOrWhiteSpace(identity.npcId))
        {
            return "0:" + identity.npcId;
        }

        return "1:" +
            smartNpc.gameObject.scene.name + ":" +
            smartNpc.gameObject.name + ":" +
            smartNpc.GetInstanceID();
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

    static void ApplySchedulePhaseOffset(
        List<NpcScheduleSlot> slots,
        float offsetHours)
    {
        if (slots == null ||
            slots.Count == 0 ||
            Mathf.Abs(offsetHours) <= 0.0001f)
        {
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            NpcScheduleSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            slot.startHour = Mathf.Repeat(slot.startHour + offsetHours, 24f);
            slot.endHour = Mathf.Repeat(slot.endHour + offsetHours, 24f);
        }
    }
}

