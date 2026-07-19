using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NpcObstacleBatchRunner
{
    const string ScenePath = "Assets/Lang.unity";
    const string AutoStartFlagFileName = "TempCodexObj/NpcObstacleBatch.autorun";
    const string ReportFileName = "NpcObstacleBatchReport.txt";
    const string SessionActiveKey = "NpcObstacleBatchRunner.Active";
    const string SessionStepKey = "NpcObstacleBatchRunner.Step";
    const string SessionExitCodeKey = "NpcObstacleBatchRunner.ExitCode";
    const double WarmupDelaySeconds = 1.5d;
    const double ScenarioDurationSeconds = 4.0d;
    const float WallOffsetX = 1.25f;
    const float TargetOffsetX = 3.75f;
    const float WallWidth = 0.45f;
    const float WallHeight = 30f;
    const float PassThresholdPadding = 0.2f;

    static readonly string[] TrackedNpcKeys =
    {
        "daocot",
        "satthu1"
    };

    static readonly string[] TrackedNpcObjectNames =
    {
        "daocot",
        "satthu (1)"
    };

    static readonly FieldInfo CurrentMonsterTargetField =
        typeof(SmartNpcAI).GetField(
            "currentMonsterTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo HasEscapeTargetField =
        typeof(SmartNpcAI).GetField(
            "hasEscapeTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo HasObstacleAvoidTargetField =
        typeof(SmartNpcAI).GetField(
            "hasObstacleAvoidTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo MovementPausedUntilField =
        typeof(SmartNpcAI).GetField(
            "movementPausedUntil",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo ActionTimerField =
        typeof(SmartNpcAI).GetField(
            "actionTimer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo CrowdYieldUntilField =
        typeof(SmartNpcAI).GetField(
            "crowdYieldUntil",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo PostTeleportRecoveryUntilField =
        typeof(SmartNpcAI).GetField(
            "postTeleportRecoveryUntil",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo DamageRecoveryUntilField =
        typeof(SmartNpcAI).GetField(
            "damageRecoveryUntil",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo EscapeTargetField =
        typeof(SmartNpcAI).GetField(
            "escapeTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo ObstacleAvoidTargetField =
        typeof(SmartNpcAI).GetField(
            "obstacleAvoidTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo ObstacleAvoidUntilField =
        typeof(SmartNpcAI).GetField(
            "obstacleAvoidUntil",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo LastUnstuckPositionField =
        typeof(SmartNpcAI).GetField(
            "lastUnstuckPosition",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo StuckMoveTimerField =
        typeof(SmartNpcAI).GetField(
            "stuckMoveTimer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly FieldInfo BlockedMoveTimerField =
        typeof(SmartNpcAI).GetField(
            "blockedMoveTimer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static Dictionary<string, SmartNpcAI> trackedNpcs;
    static string reportPath;
    static int stepIndex;
    static int exitCode;
    static bool initialized;
    static bool finished;
    static double waitUntil;
    static ScenarioState activeScenario;

    sealed class ScenarioState
    {
        public string key;
        public string objectName;
        public SmartNpcAI npc;
        public Rigidbody2D rigidbody;
        public GameObject arenaRoot;
        public Transform target;
        public float barrierX;
        public float passThresholdX;
        public float maxObservedX;
        public Vector3 startPosition;
    }

    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        EditorApplication.update -= WatchForAutoStart;
        EditorApplication.update += WatchForAutoStart;

        if (!SessionState.GetBool(SessionActiveKey, false))
        {
            return;
        }

        reportPath = SessionState.GetString(nameof(reportPath), string.Empty);
        stepIndex = SessionState.GetInt(SessionStepKey, 0);
        exitCode = SessionState.GetInt(SessionExitCodeKey, 0);

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Update;
    }

    static void WatchForAutoStart()
    {
        if (Application.isBatchMode ||
            SessionState.GetBool(SessionActiveKey, false) ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string autoStartPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", AutoStartFlagFileName));
        if (!File.Exists(autoStartPath))
        {
            return;
        }

        File.Delete(autoStartPath);
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/NPC/Run Obstacle Batch")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[NpcObstacleBatch] Unity is already entering Play Mode.");
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
        stepIndex = 0;
        exitCode = 0;
        initialized = false;
        finished = false;
        waitUntil = 0d;
        activeScenario = null;

        SessionState.SetBool(SessionActiveKey, true);
        SessionState.SetString(nameof(reportPath), reportPath);
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
            waitUntil = EditorApplication.timeSinceStartup + WarmupDelaySeconds;
            Report("Entered Play Mode");
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Report("Exiting Play Mode");
        }
    }

    static void Update()
    {
        if (finished || !EditorApplication.isPlaying)
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
            if (EditorApplication.timeSinceStartup < waitUntil)
            {
                return;
            }

            if (!TryInitializePlayContext())
            {
                return;
            }

            initialized = true;
            waitUntil = EditorApplication.timeSinceStartup + 0.25d;
            return;
        }

        SampleActiveScenario();

        if (EditorApplication.timeSinceStartup < waitUntil)
        {
            return;
        }

        switch (stepIndex)
        {
            case 0:
                StartScenario(0);
                return;
            case 1:
                FinishScenario();
                return;
            case 2:
                StartScenario(1);
                return;
            case 3:
                FinishScenario();
                return;
            default:
                CompleteRun();
                return;
        }
    }

    static bool TryInitializePlayContext()
    {
        Report("Init context begin");

        if (trackedNpcs == null)
        {
            trackedNpcs = new Dictionary<string, SmartNpcAI>();
            Report("Init context created dictionary");
        }

        trackedNpcs.Clear();
        Report("Init context cleared dictionary");

        SmartNpcAI[] smartNpcs =
            UnityEngine.Object.FindObjectsByType<SmartNpcAI>(
                FindObjectsInactive.Exclude);
        Report(
            "Init context found SmartNpcAI count=" +
            (smartNpcs != null ? smartNpcs.Length.ToString() : "null"));

        foreach (SmartNpcAI npc in smartNpcs)
        {
            if (npc == null)
            {
                continue;
            }

            string objectName =
                npc.gameObject != null
                    ? npc.gameObject.name
                    : "null";
            string npcName = npc.npcName ?? string.Empty;
            string normalizedObjectName = Normalize(objectName);
            string normalizedNpcName = Normalize(npcName);

            for (int i = 0; i < TrackedNpcKeys.Length; i++)
            {
                if (normalizedObjectName == TrackedNpcKeys[i] ||
                    normalizedNpcName == TrackedNpcKeys[i])
                {
                    trackedNpcs[TrackedNpcKeys[i]] = npc;
                    Report(
                        "Init matched key=" +
                        TrackedNpcKeys[i] +
                        " object=" +
                        objectName +
                        " npcName=" +
                        npcName);
                }
            }
        }

        Report("Tracked NPCs: " + string.Join(", ", trackedNpcs.Keys));

        for (int i = 0; i < TrackedNpcKeys.Length; i++)
        {
            if (!trackedNpcs.ContainsKey(TrackedNpcKeys[i]))
            {
                Fail("Missing tracked NPC: " + TrackedNpcObjectNames[i]);
                return false;
            }
        }

        return true;
    }

    static void StartScenario(int npcIndex)
    {
        string key = TrackedNpcKeys[npcIndex];
        if (!trackedNpcs.TryGetValue(key, out SmartNpcAI npc) ||
            npc == null)
        {
            Fail("Tracked NPC reference lost: " + key);
            return;
        }

        CleanupScenario();

        Rigidbody2D rb = npc.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Fail("NPC has no Rigidbody2D: " + npc.gameObject.name);
            return;
        }

        Vector3 startPosition = npc.transform.position;
        GameObject arenaRoot = new GameObject("__ObstacleBatch_" + key);

        GameObject wall = new GameObject("Wall");
        wall.transform.SetParent(arenaRoot.transform, false);
        wall.transform.position =
            startPosition +
            new Vector3(WallOffsetX, 0f, 0f);
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0)
        {
            wall.layer = obstacleLayer;
        }

        BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.isTrigger = false;
        wallCollider.size = new Vector2(WallWidth, WallHeight);

        Rigidbody2D wallBody = wall.AddComponent<Rigidbody2D>();
        wallBody.bodyType = RigidbodyType2D.Static;
        wallBody.simulated = true;

        GameObject targetObject = new GameObject("Target");
        targetObject.transform.SetParent(arenaRoot.transform, false);
        targetObject.transform.position =
            startPosition +
            new Vector3(TargetOffsetX, 0f, 0f);

        PrepareNpcForScenario(npc, rb, targetObject.transform, startPosition);

        activeScenario = new ScenarioState
        {
            key = key,
            objectName = npc.gameObject.name,
            npc = npc,
            rigidbody = rb,
            arenaRoot = arenaRoot,
            target = targetObject.transform,
            barrierX = wall.transform.position.x,
            passThresholdX =
                wall.transform.position.x +
                WallWidth * 0.5f +
                PassThresholdPadding,
            maxObservedX = npc.transform.position.x,
            startPosition = startPosition
        };

        Report(
            "Scenario start [" + npc.gameObject.name + "]" +
            " bodyType=" + rb.bodyType +
            " kinematicContacts=" + rb.useFullKinematicContacts +
            " collisionMode=" + rb.collisionDetectionMode +
            " start=" + startPosition +
            " wallX=" + activeScenario.barrierX.ToString("0.00") +
            " target=" + targetObject.transform.position);

        waitUntil = EditorApplication.timeSinceStartup + ScenarioDurationSeconds;
        stepIndex++;
        SessionState.SetInt(SessionStepKey, stepIndex);
    }

    static void FinishScenario()
    {
        if (activeScenario == null ||
            activeScenario.npc == null)
        {
            Fail("Active scenario was lost before evaluation.");
            return;
        }

        Vector3 finalPosition = activeScenario.npc.transform.position;
        bool crossedWall = activeScenario.maxObservedX > activeScenario.passThresholdX;
        string verdict = crossedWall ? "FAIL" : "PASS";

        Report(
            "Scenario result [" + activeScenario.objectName + "] -> " +
            verdict +
            " final=" + finalPosition +
            " maxX=" + activeScenario.maxObservedX.ToString("0.00") +
            " thresholdX=" + activeScenario.passThresholdX.ToString("0.00"));

        if (crossedWall)
        {
            exitCode = 1;
        }

        CleanupScenario();
        stepIndex++;
        SessionState.SetInt(SessionStepKey, stepIndex);
        waitUntil = EditorApplication.timeSinceStartup + 0.4d;
    }

    static void SampleActiveScenario()
    {
        if (activeScenario == null ||
            activeScenario.npc == null)
        {
            return;
        }

        activeScenario.maxObservedX =
            Mathf.Max(
                activeScenario.maxObservedX,
                activeScenario.npc.transform.position.x);
    }

    static void PrepareNpcForScenario(
        SmartNpcAI npc,
        Rigidbody2D rb,
        Transform target,
        Vector3 startPosition)
    {
        npc.debugFlowLogs = true;
        npc.dailyRoutineEnabled = false;
        npc.ClearSmartTask();
        npc.currentTarget = target;
        npc.currentAction = "Obstacle Test";
        npc.SetCurrentHealth(npc.AuthoritativeMaxHP);

        CurrentMonsterTargetField?.SetValue(npc, null);
        HasEscapeTargetField?.SetValue(npc, false);
        HasObstacleAvoidTargetField?.SetValue(npc, false);
        MovementPausedUntilField?.SetValue(npc, 0f);
        ActionTimerField?.SetValue(npc, 0f);
        CrowdYieldUntilField?.SetValue(npc, 0f);
        PostTeleportRecoveryUntilField?.SetValue(npc, 0f);
        DamageRecoveryUntilField?.SetValue(npc, 0f);
        EscapeTargetField?.SetValue(npc, Vector3.zero);
        ObstacleAvoidTargetField?.SetValue(npc, Vector3.zero);
        ObstacleAvoidUntilField?.SetValue(npc, 0f);
        LastUnstuckPositionField?.SetValue(npc, startPosition);
        StuckMoveTimerField?.SetValue(npc, 0f);
        BlockedMoveTimerField?.SetValue(npc, 0f);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = startPosition;
        npc.transform.position = startPosition;
    }

    static void CleanupScenario()
    {
        if (activeScenario?.npc != null)
        {
            activeScenario.npc.currentTarget = null;
            activeScenario.npc.currentAction = "idle";
            activeScenario.npc.ClearSmartTask();

            if (activeScenario.rigidbody != null)
            {
                activeScenario.rigidbody.linearVelocity = Vector2.zero;
                activeScenario.rigidbody.angularVelocity = 0f;
            }
        }

        if (activeScenario?.arenaRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(activeScenario.arenaRoot);
        }

        activeScenario = null;
    }

    static void CompleteRun()
    {
        Report("Obstacle batch verification complete.");
        WriteReportAndExit();
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

        Debug.Log("[NpcObstacleBatch] " + line);
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
        CleanupScenario();
        SessionState.EraseBool(SessionActiveKey);
        SessionState.EraseString(nameof(reportPath));
        SessionState.EraseInt(SessionStepKey);
        SessionState.EraseInt(SessionExitCodeKey);

        Debug.Log("[NpcObstacleBatch] Report written to " + reportPath);

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
    }
}
