using System.Collections.Generic;
using UnityEngine;

public enum NpcLifePath
{
    Commoner,
    SemiCultivator,
    Cultivator,
    Beast
}

public enum NpcScheduleActivity
{
    Idle,
    Sleep,
    Eat,
    Work,
    SellGoods,
    BuyGoods,
    Gather,
    Hunt,
    Cultivate,
    Alchemy,
    Forge,
    TakeTask,
    ReturnHome,
    DoMission,
    FreeHuntAndGather,
    TradeBuySell
}

[System.Serializable]
public class NpcScheduleSlot
{
    public NpcScheduleActivity activity = NpcScheduleActivity.Idle;
    [Range(0f, 24f)] public float startHour;
    [Range(0f, 24f)] public float endHour = 1f;
    public bool allowDangerInterrupt = true;
    public bool allowHungerInterrupt = true;
    public bool allowFatigueInterrupt = true;
    public bool allowSocialInterrupt;
}

public class NpcScheduleController : MonoBehaviour
{
    [Header("Schedule")]
    public bool enforceSchedule = true;
    public bool autoBuildDefaultSchedule = true;
    public NpcLifePath lifePath = NpcLifePath.Commoner;
    public List<NpcScheduleSlot> slots = new List<NpcScheduleSlot>();

    [Header("Commoner Awakening")]
    public bool canCultivate;
    public bool awakenedCultivation;
    public bool awakenedByMarrowCleansingPill;

    [Header("Runtime Debug")]
    public float debugCurrentHour;
    public NpcScheduleActivity debugCurrentActivity;
    public string debugCurrentSlot;

    readonly HashSet<string> completedSlotActivities =
        new HashSet<string>();
    readonly HashSet<string> startedSlotActivities =
        new HashSet<string>();

    public NpcScheduleActivity CurrentActivity => GetCurrentActivity();
    public NpcScheduleSlot CurrentSlot => GetCurrentSlot();

    void Reset()
    {
        DetectLifePath();
        RebuildDefaultSchedule();
    }

    void Awake()
    {
        DetectLifePath();

        if (autoBuildDefaultSchedule)
        {
            RebuildDefaultSchedule();
        }
    }

    void Update()
    {
        RefreshDebugState();
    }

    void OnValidate()
    {
        if (autoBuildDefaultSchedule)
        {
            DetectLifePath();
            RebuildDefaultSchedule();
        }
    }

    public static bool AllowsTrade(GameObject npc)
    {
        NpcScheduleController schedule = GetSchedule(npc);
        if (schedule == null || !schedule.enforceSchedule)
        {
            return true;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        return activity == NpcScheduleActivity.BuyGoods ||
            activity == NpcScheduleActivity.SellGoods ||
            activity == NpcScheduleActivity.TakeTask ||
            activity == NpcScheduleActivity.DoMission ||
            activity == NpcScheduleActivity.TradeBuySell;
    }

    public static bool AllowsGather(GameObject npc)
    {
        NpcScheduleController schedule = GetSchedule(npc);
        if (schedule == null || !schedule.enforceSchedule)
        {
            return true;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        return activity == NpcScheduleActivity.Gather ||
            activity == NpcScheduleActivity.FreeHuntAndGather;
    }

    public static bool AllowsAlchemy(GameObject npc)
    {
        return AllowsActivity(npc, NpcScheduleActivity.Alchemy);
    }

    public static bool AllowsForge(GameObject npc)
    {
        return AllowsActivity(npc, NpcScheduleActivity.Forge);
    }

    public static bool AllowsTask(GameObject npc)
    {
        return AllowsActivity(npc, NpcScheduleActivity.TakeTask) ||
            AllowsActivity(npc, NpcScheduleActivity.DoMission);
    }

    public static bool AllowsSocial(GameObject npc)
    {
        NpcScheduleController schedule = GetSchedule(npc);
        if (schedule == null || !schedule.enforceSchedule)
        {
            return true;
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        if (slot == null)
        {
            return false;
        }

        if (slot.allowSocialInterrupt)
        {
            return true;
        }

        NpcScheduleActivity activity = slot.activity;
        return activity == NpcScheduleActivity.Idle ||
            activity == NpcScheduleActivity.SellGoods ||
            activity == NpcScheduleActivity.BuyGoods ||
            activity == NpcScheduleActivity.TakeTask;
    }

    public static bool AllowsActivity(
        GameObject npc,
        NpcScheduleActivity activity)
    {
        NpcScheduleController schedule = GetSchedule(npc);
        return schedule == null ||
            !schedule.enforceSchedule ||
            IsMatchingActivity(schedule.CurrentActivity, activity);
    }

    public static bool IsMatchingActivity(
        NpcScheduleActivity current,
        NpcScheduleActivity requested)
    {
        if (current == requested)
        {
            return true;
        }

        if (current == NpcScheduleActivity.DoMission)
        {
            return requested == NpcScheduleActivity.TakeTask ||
                requested == NpcScheduleActivity.DoMission;
        }

        if (current == NpcScheduleActivity.FreeHuntAndGather)
        {
            return requested == NpcScheduleActivity.Hunt ||
                requested == NpcScheduleActivity.Gather ||
                requested == NpcScheduleActivity.FreeHuntAndGather;
        }

        if (current == NpcScheduleActivity.TradeBuySell)
        {
            return requested == NpcScheduleActivity.BuyGoods ||
                requested == NpcScheduleActivity.SellGoods ||
                requested == NpcScheduleActivity.TradeBuySell;
        }

        return current == requested;
    }

    public static NpcScheduleController GetSchedule(GameObject npc)
    {
        if (npc == null)
        {
            return null;
        }

        NpcScheduleController schedule =
            npc.GetComponent<NpcScheduleController>();

        if (schedule == null &&
            Application.isPlaying &&
            IsScheduleDrivenNpc(npc))
        {
            schedule = npc.AddComponent<NpcScheduleController>();
        }

        if (schedule != null &&
            schedule.autoBuildDefaultSchedule &&
            (schedule.slots == null || schedule.slots.Count == 0))
        {
            schedule.DetectLifePath();
            schedule.RebuildDefaultSchedule();
        }

        return schedule;
    }

    static bool IsScheduleDrivenNpc(GameObject npc)
    {
        return npc.GetComponent<VillagerAI>() != null ||
            npc.GetComponent<SmartNpcAI>() != null;
    }

    public NpcScheduleSlot GetCurrentSlot()
    {
        if (slots == null || slots.Count == 0)
        {
            return null;
        }

        float hour = GetCurrentWorldHour();
        foreach (NpcScheduleSlot slot in slots)
        {
            if (slot != null && ContainsHour(slot, hour))
            {
                return slot;
            }
        }

        return null;
    }

    public NpcScheduleActivity GetCurrentActivity()
    {
        NpcScheduleSlot slot = GetCurrentSlot();
        return slot != null
            ? slot.activity
            : NpcScheduleActivity.Idle;
    }

    void RefreshDebugState()
    {
        debugCurrentHour = GetCurrentWorldHour();
        NpcScheduleSlot slot = GetCurrentSlot();
        debugCurrentActivity = slot != null
            ? slot.activity
            : NpcScheduleActivity.Idle;
        debugCurrentSlot = slot != null
            ? slot.activity + " " +
            slot.startHour.ToString("0.##") + "-" +
            slot.endHour.ToString("0.##")
            : "No slot";
    }

    public bool HasCompletedCurrentSlotActivity(
        NpcScheduleActivity activity)
    {
        string key = GetCurrentSlotActivityKey(activity);
        return !string.IsNullOrEmpty(key) &&
            completedSlotActivities.Contains(key);
    }

    public bool HasStartedCurrentSlotActivity(
        NpcScheduleActivity activity)
    {
        string key = GetCurrentSlotActivityKey(activity);
        return !string.IsNullOrEmpty(key) &&
            startedSlotActivities.Contains(key);
    }

    public void MarkCurrentSlotActivityStarted(
        NpcScheduleActivity activity)
    {
        string key = GetCurrentSlotActivityKey(activity);
        if (!string.IsNullOrEmpty(key))
        {
            startedSlotActivities.Add(key);
        }
    }

    public void MarkCurrentSlotActivityCompleted(
        NpcScheduleActivity activity)
    {
        string key = GetCurrentSlotActivityKey(activity);
        if (!string.IsNullOrEmpty(key))
        {
            completedSlotActivities.Add(key);
        }
    }

    public void ClearCurrentSlotActivityState(
        NpcScheduleActivity activity)
    {
        string key = GetCurrentSlotActivityKey(activity);
        if (!string.IsNullOrEmpty(key))
        {
            startedSlotActivities.Remove(key);
            completedSlotActivities.Remove(key);
        }
    }

    public void AwakenCultivationPath(bool byMarrowCleansingPill)
    {
        canCultivate = true;
        awakenedCultivation = true;
        awakenedByMarrowCleansingPill |= byMarrowCleansingPill;
        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null &&
            smartNpc.enabled)
        {
            lifePath = NpcLifePath.Cultivator;
        }

        if (autoBuildDefaultSchedule)
        {
            RebuildDefaultSchedule();
        }
    }

    public void RebuildDefaultSchedule()
    {
        NpcDailyRoutineLibrary.BuildDefaultSchedule(
            gameObject,
            lifePath,
            slots);
    }

    void DetectLifePath()
    {
        if (GetComponent<MonsterAI>() != null)
        {
            lifePath = NpcLifePath.Beast;
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null &&
            smartNpc.enabled)
        {
            lifePath = NpcLifePath.Cultivator;
            canCultivate = true;
            return;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null &&
            villager.enabled)
        {
            lifePath = NpcLifePath.Commoner;
            canCultivate = false;
            awakenedCultivation = false;
        }
    }

    bool ContainsHour(NpcScheduleSlot slot, float hour)
    {
        float start = Mathf.Repeat(slot.startHour, 24f);
        float end = Mathf.Repeat(slot.endHour, 24f);

        if (Mathf.Approximately(start, end))
        {
            return true;
        }

        if (start < end)
        {
            return hour >= start && hour < end;
        }

        return hour >= start || hour < end;
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            return timeSystem.CurrentHour;
        }

        return Mathf.Repeat(Time.time * 24f / 900f, 24f);
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.FloorToInt(Time.time / 900f);
    }

    string GetCurrentSlotActivityKey(NpcScheduleActivity activity)
    {
        NpcScheduleSlot slot = GetCurrentSlot();
        if (slot == null ||
            slot.activity != activity)
        {
            return "";
        }

        float hour = GetCurrentWorldHour();
        int day = GetCurrentWorldDay();
        float start = Mathf.Repeat(slot.startHour, 24f);
        float end = Mathf.Repeat(slot.endHour, 24f);

        if (start > end && hour < end)
        {
            day--;
        }

        return day + ":" +
            activity + ":" +
            Mathf.RoundToInt(start * 100f) + ":" +
            Mathf.RoundToInt(end * 100f);
    }
}
