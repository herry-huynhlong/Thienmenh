using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SmartAiPlayModeBatchRunner
{
    const string ScenePath = "Assets/Lang.unity";
    const string ReportFileName = "smartai_playmode_report.txt";
    const string SessionActiveKey = "SmartAiBatchRunner.Active";
    const string SessionStepKey = "SmartAiBatchRunner.Step";
    const string SessionExitCodeKey = "SmartAiBatchRunner.ExitCode";
    const string SessionReportPathKey = "SmartAiBatchRunner.ReportPath";

    static readonly string[] TrackedNpcKeys =
    {
        "laoba1",
        "tusinu1",
        "satthu1"
    };

    static readonly (float hour, string label, SmartAITaskGoal goal)[] ScheduleCases =
    {
        (0.5f, "00:30 Cultivate", SmartAITaskGoal.Cultivate),
        (7.5f, "07:30 DoMission", SmartAITaskGoal.DoMission),
        (13.5f, "13:30 FreeHuntAndGather", SmartAITaskGoal.FreeHuntAndGather),
        (18.5f, "18:30 DoMission", SmartAITaskGoal.DoMission),
        (22.5f, "22:30 TradeBuySell", SmartAITaskGoal.TradeBuySell)
    };

    static readonly FieldInfo HasWanderTargetField =
        typeof(SmartNpcAI).GetField(
            "hasWanderTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo WanderTargetField =
        typeof(SmartNpcAI).GetField(
            "wanderTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo CurrentMonsterTargetField =
        typeof(SmartNpcAI).GetField(
            "currentMonsterTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo IsRetreatingFromMonsterField =
        typeof(SmartNpcAI).GetField(
            "isRetreatingFromMonster",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo RetreatUntilTimeField =
        typeof(SmartNpcAI).GetField(
            "retreatUntilTime",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo RetreatTargetField =
        typeof(SmartNpcAI).GetField(
            "retreatTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo CurrentHelpRequestField =
        typeof(SmartNpcAI).GetField(
            "currentHelpRequest",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly MethodInfo BeginMonsterRetreatMethod =
        typeof(SmartNpcAI).GetMethod(
            "ForceBeginMonsterRetreatForDebug",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
        typeof(SmartNpcAI).GetMethod(
            "BeginMonsterRetreat",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static Dictionary<string, SmartNpcAI> trackedNpcs;
    static MonsterAI sampleMonster;
    static WorldTimeSystem worldTime;
    static int stepIndex;
    static double waitUntil;
    static bool initialized;
    static string reportPath;
    static bool finished;
    static int exitCode;

    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        if (!SessionState.GetBool(SessionActiveKey, false))
        {
            return;
        }

        reportPath = SessionState.GetString(SessionReportPathKey, string.Empty);
        stepIndex = SessionState.GetInt(SessionStepKey, 0);
        exitCode = SessionState.GetInt(SessionExitCodeKey, 0);

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/SmartAI/Run Batch PlayMode Verification")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[SmartAI Test] Unity is already entering Play Mode.");
            EditorApplication.Exit(2);
            return;
        }

        reportPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", ReportFileName));
        if (File.Exists(reportPath))
        {
            File.Delete(reportPath);
        }

        trackedNpcs = new Dictionary<string, SmartNpcAI>();
        sampleMonster = null;
        worldTime = null;
        stepIndex = 0;
        waitUntil = 0d;
        initialized = false;
        finished = false;
        exitCode = 0;

        SessionState.SetBool(SessionActiveKey, true);
        SessionState.SetString(SessionReportPathKey, reportPath);
        SessionState.SetInt(SessionStepKey, stepIndex);
        SessionState.SetInt(SessionExitCodeKey, exitCode);

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Update;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Report("Open scene: " + ScenePath);
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Report("Entered Play Mode");
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Report("Exiting Play Mode");
        }
    }

    static void Update()
    {
        if (finished)
        {
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            return;
        }

        try
        {
            TickPlayMode();
        }
        catch (Exception exception)
        {
            Fail("Unhandled exception: " + exception);
        }
    }

    static void TickPlayMode()
    {
        if (!initialized)
        {
            if (!TryInitializePlayContext())
            {
                return;
            }

            initialized = true;
            waitUntil = EditorApplication.timeSinceStartup + 0.5d;
            return;
        }

        if (EditorApplication.timeSinceStartup < waitUntil)
        {
            return;
        }

        switch (stepIndex)
        {
            case 0:
                StartScheduleCase(0);
                return;
            case 1:
                FinishScheduleCase(0);
                return;
            case 2:
                StartScheduleCase(1);
                return;
            case 3:
                FinishScheduleCase(1);
                return;
            case 4:
                StartScheduleCase(2);
                return;
            case 5:
                FinishScheduleCase(2);
                return;
            case 6:
                StartScheduleCase(3);
                return;
            case 7:
                FinishScheduleCase(3);
                return;
            case 8:
                StartScheduleCase(4);
                return;
            case 9:
                FinishScheduleCase(4);
                return;
            case 10:
                StartCultivateRetreatCase();
                return;
            case 11:
                FinishCultivateRetreatCase();
                return;
            case 12:
                StartLowHpDuringMissionCase();
                return;
            case 13:
                FinishLowHpDuringMissionCase();
                return;
            case 14:
                StartEmergencyCrossSlotCase();
                return;
            case 15:
                FinishEmergencyCrossSlotCase();
                return;
            default:
                CompleteRun();
                return;
        }
    }

    static bool TryInitializePlayContext()
    {
        if (trackedNpcs == null)
        {
            trackedNpcs = new Dictionary<string, SmartNpcAI>();
        }

        worldTime = UnityEngine.Object.FindAnyObjectByType<WorldTimeSystem>();
        if (worldTime == null)
        {
            worldTime = WorldTimeSystem.EnsureInstance();
            if (worldTime == null)
            {
                Report("WorldTimeSystem not found yet, waiting...");
                waitUntil = EditorApplication.timeSinceStartup + 0.5d;
                return false;
            }

            Report("WorldTimeSystem created by EnsureInstance()");
        }

        SmartNpcAI[] smartNpcs =
            UnityEngine.Object.FindObjectsByType<SmartNpcAI>(
                FindObjectsInactive.Exclude);

        trackedNpcs.Clear();
        foreach (SmartNpcAI npc in smartNpcs)
        {
            if (npc == null)
            {
                continue;
            }

            string normalized = Normalize(npc.gameObject.name);
            for (int i = 0; i < TrackedNpcKeys.Length; i++)
            {
                if (normalized == TrackedNpcKeys[i])
                {
                    trackedNpcs[TrackedNpcKeys[i]] = npc;
                }
            }
        }

        sampleMonster = FindSampleMonster();

        Report(
            "Tracked NPCs: " +
            string.Join(", ", trackedNpcs.Keys));
        Report(
            "Sample monster: " +
            (sampleMonster != null ? sampleMonster.monsterName : "none"));

        if (trackedNpcs.Count == 0)
        {
            Fail("No tracked SmartNpcAI found in scene.");
            return false;
        }

        return true;
    }

    static void StartScheduleCase(int index)
    {
        (float hour, string label, SmartAITaskGoal goal) testCase =
            ScheduleCases[index];

        PrepareTrackedNpcsForScenario();
        SetTime(testCase.hour);
        Report("Schedule case start: " + testCase.label);
        waitUntil = EditorApplication.timeSinceStartup + 3.5d;
        AdvanceStep();
    }

    static void FinishScheduleCase(int index)
    {
        ReportScheduleSnapshot(ScheduleCases[index]);
        AdvanceStep();
    }

    static void StartCultivateRetreatCase()
    {
        if (sampleMonster == null)
        {
            Report("Override case A skipped: no monster found.");
            stepIndex = 12;
            SessionState.SetInt(SessionStepKey, stepIndex);
            return;
        }

        SmartNpcAI npc = GetPrimaryNpc();
        PrepareNpcForScenario(npc);
        SetTime(0.5f);
        Report("Override case A start: cultivate -> flee/retreat");
        waitUntil = EditorApplication.timeSinceStartup + 3.0d;
        AdvanceStep();
    }

    static void FinishCultivateRetreatCase()
    {
        SmartNpcAI npc = GetPrimaryNpc();
        if (npc == null || sampleMonster == null)
        {
            Report("Override case A aborted: missing npc or monster.");
            stepIndex = 12;
            SessionState.SetInt(SessionStepKey, stepIndex);
            return;
        }

        BeginMonsterRetreatMethod?.Invoke(npc, new object[] { sampleMonster });
        waitUntil = EditorApplication.timeSinceStartup + 1.0d;
        ReportNpcState(
            "Override case A result",
            npc,
            expectedActivity: "Cultivate",
            expectedGoal: SmartAITaskGoal.Pursued,
            expectedActionContains: "flee");
        AdvanceStep();
    }

    static void StartLowHpDuringMissionCase()
    {
        SmartNpcAI npc = GetPrimaryNpc();
        PrepareNpcForScenario(npc);
        SetTime(7.5f);
        Report("Override case B start: DoMission -> low HP emergency");
        waitUntil = EditorApplication.timeSinceStartup + 3.0d;
        AdvanceStep();
    }

    static void FinishLowHpDuringMissionCase()
    {
        SmartNpcAI npc = GetPrimaryNpc();
        if (npc == null)
        {
            Report("Override case B aborted: missing npc.");
            stepIndex = 14;
            SessionState.SetInt(SessionStepKey, stepIndex);
            return;
        }

        ForceLowHealthAndPingDamage(npc);
        waitUntil = EditorApplication.timeSinceStartup + 2.5d;
        ReportNpcState(
            "Override case B result",
            npc,
            expectedActivity: "DoMission",
            expectedGoal: SmartAITaskGoal.LowHpRecovery,
            expectedActionContains: "rest");
        AdvanceStep();
    }

    static void StartEmergencyCrossSlotCase()
    {
        if (sampleMonster == null)
        {
            Report("Override case C skipped: no monster found.");
            stepIndex = 16;
            SessionState.SetInt(SessionStepKey, stepIndex);
            return;
        }

        SmartNpcAI npc = GetPrimaryNpc();
        PrepareNpcForScenario(npc);
        SetTime(6.8333f);
        Report("Override case C start: 06:50 emergency then 07:30 schedule recovery");
        waitUntil = EditorApplication.timeSinceStartup + 2.0d;
        AdvanceStep();
    }

    static void FinishEmergencyCrossSlotCase()
    {
        SmartNpcAI npc = GetPrimaryNpc();
        if (npc == null || sampleMonster == null)
        {
            Report("Override case C aborted: missing npc or monster.");
            stepIndex = 16;
            SessionState.SetInt(SessionStepKey, stepIndex);
            return;
        }

        BeginMonsterRetreatMethod?.Invoke(npc, new object[] { sampleMonster });
        SetTime(7.5f);
        waitUntil = EditorApplication.timeSinceStartup + 5.0d;
        ReportNpcState(
            "Override case C result",
            npc,
            expectedActivity: "DoMission",
            expectedGoal: SmartAITaskGoal.DoMission,
            expectedActionContains: "task");
        AdvanceStep();
    }

    static void CompleteRun()
    {
        Report("PlayMode verification complete.");
        WriteReportAndExit();
    }

    static void PrepareTrackedNpcsForScenario()
    {
        foreach (SmartNpcAI npc in trackedNpcs.Values)
        {
            PrepareNpcForScenario(npc);
        }
    }

    static void ReportScheduleSnapshot(
        (float hour, string label, SmartAITaskGoal goal) testCase)
    {
        foreach (KeyValuePair<string, SmartNpcAI> pair in trackedNpcs)
        {
            ReportNpcState(
                "Schedule snapshot " + testCase.label + " [" + pair.Key + "]",
                pair.Value,
                expectedActivity: MapGoalToActivity(testCase.goal),
                expectedGoal: testCase.goal,
                expectedActionContains: string.Empty);
        }
    }

    static void ReportNpcState(
        string prefix,
        SmartNpcAI npc,
        string expectedActivity,
        SmartAITaskGoal expectedGoal,
        string expectedActionContains)
    {
        if (npc == null)
        {
            Report(prefix + " -> npc missing");
            return;
        }

        NpcScheduleController schedule = npc.GetComponent<NpcScheduleController>();
        string activity =
            schedule != null
                ? schedule.CurrentActivity.ToString()
                : "None";
        SmartAITask task = npc.CurrentSmartTask;
        string action = npc.currentAction ?? string.Empty;

        bool activityMatch =
            string.IsNullOrEmpty(expectedActivity) ||
            string.Equals(activity, expectedActivity, StringComparison.Ordinal);
        bool goalMatch =
            expectedGoal == SmartAITaskGoal.None ||
            (task != null && task.goal == expectedGoal);
        bool actionMatch =
            string.IsNullOrEmpty(expectedActionContains) ||
            action.IndexOf(
                expectedActionContains,
                StringComparison.OrdinalIgnoreCase) >= 0;

        string verdict =
            activityMatch && goalMatch && actionMatch
                ? "PASS"
                : "WARN";

        Report(
            prefix +
            " -> " + verdict +
            " activity=" + activity +
            " action=" + action +
            " taskGoal=" + (task != null ? task.goal.ToString() : "None") +
            " hp=" + npc.currentHP + "/" + npc.maxHP);
    }

    static void PrepareNpcForScenario(SmartNpcAI npc)
    {
        if (npc == null)
        {
            return;
        }

        npc.debugFlowLogs = true;
        npc.ClearSmartTask();
        npc.currentAction = string.Empty;
        npc.currentTarget = null;
        npc.currentHP = npc.maxHP;
        npc.fatigue = 0f;
        npc.hunger = 0f;

        if (npc.characterStats != null)
        {
            npc.characterStats.currentHP =
                Mathf.Max(1, npc.characterStats.finalHP);
        }

        HasWanderTargetField?.SetValue(npc, false);
        WanderTargetField?.SetValue(npc, npc.transform.position);
        CurrentMonsterTargetField?.SetValue(npc, null);
        IsRetreatingFromMonsterField?.SetValue(npc, false);
        RetreatUntilTimeField?.SetValue(npc, 0f);
        RetreatTargetField?.SetValue(npc, Vector3.zero);
        CurrentHelpRequestField?.SetValue(npc, null);
    }

    static SmartNpcAI GetPrimaryNpc()
    {
        if (trackedNpcs.TryGetValue("satthu1", out SmartNpcAI npc) &&
            npc != null)
        {
            return npc;
        }

        foreach (SmartNpcAI smartNpc in trackedNpcs.Values)
        {
            if (smartNpc != null)
            {
                return smartNpc;
            }
        }

        return null;
    }

    static void ForceLowHealthAndPingDamage(SmartNpcAI npc)
    {
        int forcedHp = Mathf.Max(2, Mathf.CeilToInt(npc.maxHP * 0.25f));
        npc.currentHP = forcedHp;

        if (npc.characterStats != null)
        {
            npc.characterStats.currentHP =
                Mathf.Max(2, Mathf.CeilToInt(npc.characterStats.finalHP * 0.25f));
        }

        npc.TakeDamage(1);
    }

    static MonsterAI FindSampleMonster()
    {
        MonsterAI[] monsters =
            UnityEngine.Object.FindObjectsByType<MonsterAI>(
                FindObjectsInactive.Exclude);

        foreach (MonsterAI monster in monsters)
        {
            if (monster != null &&
                !monster.IsDead)
            {
                return monster;
            }
        }

        return null;
    }

    static void SetTime(float hour)
    {
        if (worldTime == null)
        {
            worldTime = UnityEngine.Object.FindAnyObjectByType<WorldTimeSystem>();
        }

        worldTime?.SetCurrentDayHour(hour, true);
        Report("Set time -> " + hour.ToString("0.00"));
    }

    static string MapGoalToActivity(SmartAITaskGoal goal)
    {
        switch (goal)
        {
            case SmartAITaskGoal.Cultivate:
                return NpcScheduleActivity.Cultivate.ToString();
            case SmartAITaskGoal.DoMission:
                return NpcScheduleActivity.DoMission.ToString();
            case SmartAITaskGoal.FreeHuntAndGather:
                return NpcScheduleActivity.FreeHuntAndGather.ToString();
            case SmartAITaskGoal.TradeBuySell:
                return NpcScheduleActivity.TradeBuySell.ToString();
            default:
                return string.Empty;
        }
    }

    static string Normalize(string value)
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

    static void Report(string line)
    {
        string formatted =
            "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line;
        if (!string.IsNullOrEmpty(reportPath))
        {
            File.AppendAllText(
                reportPath,
                formatted + Environment.NewLine);
        }
        Debug.Log("[SmartAI Test] " + line);
    }

    static void Fail(string message)
    {
        exitCode = 1;
        Report("FAIL: " + message);
        WriteReportAndExit();
    }

    static void WriteReportAndExit()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        SessionState.EraseBool(SessionActiveKey);
        SessionState.EraseString(SessionReportPathKey);
        SessionState.EraseInt(SessionStepKey);
        SessionState.EraseInt(SessionExitCodeKey);

        Debug.Log("[SmartAI Test] Report written to " + reportPath);

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
    }

    static void AdvanceStep()
    {
        stepIndex++;
        SessionState.SetInt(SessionStepKey, stepIndex);
    }
}
