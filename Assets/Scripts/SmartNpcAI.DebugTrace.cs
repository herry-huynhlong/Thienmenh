using UnityEngine;

// Debug logging and runtime tracing helpers for smart NPC behavior.
public partial class SmartNpcAI
{
    void DebugFlow(string stage, string detail)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!ShouldLogDebugFlow())
        {
            return;
        }

        string key =
            stage + "|" +
            detail + "|" +
            currentAction + "|" +
            (currentTarget != null
                ? currentTarget.name
                : hasWanderTarget
                    ? "wander"
                    : "none");

        if (lastDebugFlowKey == key &&
            Time.time - lastDebugFlowTime < 0.75f)
        {
            return;
        }

        lastDebugFlowKey = key;
        lastDebugFlowTime = Time.time;

        float hour =
            WorldTimeSystem.Instance != null
                ? WorldTimeSystem.Instance.CurrentHour
                : -1f;

        Debug.LogWarning(
            "[SmartNpcAI] " + gameObject.name +
            " stage=" + stage +
            " detail=" + detail +
            " action=" + currentAction +
            " task=" + DescribeTask(currentSmartTask) +
            " scheduleTask=" + DescribeTask(scheduleSmartTask) +
            " target=" + (currentTarget != null ? currentTarget.name : "null") +
            " wander=" + hasWanderTarget +
            " homeReturn=" + hasHomeReturnTarget +
            " timer=" + actionTimer.ToString("0.00") +
            " hp=" + currentHP + "/" + maxHP +
            " hour=" + hour.ToString("0.00"));
#endif
    }

    bool ShouldLogDebugFlow()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return debugFlowLogs ||
            IsTrackedDebugNpc();
#else
        return false;
#endif
    }

    bool ShouldTraceRuntime()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!runtimeTraceEnabled &&
            !debugFlowLogs)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(runtimeTraceTarget))
        {
            return true;
        }

        string filter = runtimeTraceTarget.Trim();
        return ContainsIgnoreCase(gameObject.name, filter) ||
            ContainsIgnoreCase(npcName, filter);
#else
        return false;
#endif
    }

    bool ShouldLogStateTransition(
        ref string lastSignature,
        ref float lastLoggedTime,
        string signature,
        float repeatIntervalSeconds)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!ShouldLogDebugFlow())
        {
            return false;
        }

        if (string.IsNullOrEmpty(signature))
        {
            signature = "<empty>";
        }

        if (!string.Equals(
                lastSignature,
                signature,
                System.StringComparison.Ordinal))
        {
            lastSignature = signature;
            lastLoggedTime = Time.time;
            return true;
        }

        if (Time.time - lastLoggedTime >=
            Mathf.Max(0.1f, repeatIntervalSeconds))
        {
            lastLoggedTime = Time.time;
            return true;
        }
#endif

        return false;
    }

    static string QuantizeDebugVector(Vector2 value)
    {
        return Mathf.RoundToInt(value.x * 10f) + "," +
            Mathf.RoundToInt(value.y * 10f);
    }

    void TraceRuntime(
        string function,
        string detail,
        bool force = false)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!force &&
            !ShouldTraceRuntime())
        {
            return;
        }

        runtimeTraceSequence++;

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        NpcScheduleSlot slot =
            schedule != null ? schedule.CurrentSlot : null;
        float hour = GetCurrentWorldHour();
        string slotText =
            slot != null
                ? slot.activity + " " +
                    slot.startHour.ToString("0.##") + "-" +
                    slot.endHour.ToString("0.##")
                : "none";
        string scheduleText =
            schedule != null
                ? schedule.CurrentActivity.ToString()
                : "none";

        Debug.LogWarning(
            "[SmartNpcTrace] seq=" + runtimeTraceSequence +
            " frame=" + Time.frameCount +
            " fn=" + function +
            " detail=" + detail +
            " day=" + (WorldTimeSystem.Instance != null
                ? WorldTimeSystem.Instance.CurrentDay.ToString()
                : "null") +
            " hour=" + hour.ToString("0.00") +
            " slot=" + slotText +
            " schedule=" + scheduleText +
            " action=" + currentAction +
            " target=" + (currentTarget != null ? currentTarget.name : "null") +
            " wander=" + hasWanderTarget +
            " homeReturn=" + hasHomeReturnTarget +
            " monster=" + (currentMonsterTarget != null ? currentMonsterTarget.monsterName : "null") +
            " task=" + DescribeTask(currentSmartTask) +
            " scheduleTask=" + DescribeTask(scheduleSmartTask) +
            " thinkTimer=" + thinkTimer.ToString("0.00") +
            " actionTimer=" + actionTimer.ToString("0.00"));
#endif
    }

    void TraceBranch(
        string function,
        string branch,
        bool result)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        TraceRuntime(function, branch + " => " + (result ? "true" : "false"));
#endif
    }

    string DescribeMonsterMatchup(MonsterAI monster)
    {
        if (monster == null)
        {
            return "matchup=null";
        }

        return "monster=" + monster.monsterName +
            " monsterHp=" + monster.currentHP + "/" + monster.maxHP +
            " " + CombatPowerUtility.DescribeNpcVsMonster(
                gameObject,
                monster.gameObject);
    }

    bool IsTrackedDebugNpc()
    {
        return IsTrackedDebugName(gameObject.name) ||
            IsTrackedDebugName(npcName);
    }

    static bool IsTrackedDebugName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string normalized = NormalizeDebugName(value);
        for (int i = 0; i < TrackedDebugNpcNames.Length; i++)
        {
            if (normalized == TrackedDebugNpcNames[i])
            {
                return true;
            }
        }

        return false;
    }

    static string NormalizeDebugName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
