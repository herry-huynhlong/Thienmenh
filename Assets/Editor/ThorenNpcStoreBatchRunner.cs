using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ThorenNpcStoreBatchRunner
{
    const string ScenePath = "Assets/Lang.unity";
    const string ReportFileName = "ThorenNpcStoreBatchReport.txt";
    const string SessionActiveKey = "ThorenNpcStoreBatchRunner.Active";
    const float WarmupSeconds = 1.5f;
    const float TimeoutSeconds = 20f;
    const float SampleSeconds = 0.25f;

    static VillagerAI thoren;
    static NpcFixedBlacksmithController blacksmith;
    static BoxCollider2D customerZone;
    static WorldTimeSystem worldTime;
    static string reportPath;
    static double warmupUntil;
    static double timeoutAt;
    static double nextSampleAt;
    static bool initialized;
    static bool finished;
    static float originalTimeScale;
    static float originalFixedDeltaTime;

    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        if (!SessionState.GetBool(SessionActiveKey, false))
        {
            return;
        }

        reportPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", ReportFileName));

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/NPC/Run Thoren NPC-STORE Batch")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[ThorenNpcStoreBatch] Unity is already entering Play Mode.");
            EditorApplication.Exit(2);
            return;
        }

        reportPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", ReportFileName));
        if (File.Exists(reportPath))
        {
            File.Delete(reportPath);
        }

        thoren = null;
        blacksmith = null;
        customerZone = null;
        worldTime = null;
        warmupUntil = 0d;
        timeoutAt = 0d;
        nextSampleAt = 0d;
        initialized = false;
        finished = false;
        originalTimeScale = 1f;
        originalFixedDeltaTime = 0.02f;

        SessionState.SetBool(SessionActiveKey, true);

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
            originalTimeScale = Time.timeScale;
            originalFixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 8f;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            warmupUntil = EditorApplication.timeSinceStartup + WarmupSeconds;
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
            Tick();
        }
        catch (Exception exception)
        {
            Fail("Unhandled exception: " + exception);
        }
    }

    static void Tick()
    {
        if (!initialized)
        {
            if (EditorApplication.timeSinceStartup < warmupUntil)
            {
                return;
            }

            if (!TryInitializeContext())
            {
                return;
            }

            initialized = true;
            timeoutAt = EditorApplication.timeSinceStartup + TimeoutSeconds;
            nextSampleAt = EditorApplication.timeSinceStartup + SampleSeconds;
            return;
        }

        if (customerZone != null &&
            customerZone.enabled &&
            customerZone.OverlapPoint(thoren.transform.position))
        {
            Report("SUCCESS actorPos=" + thoren.transform.position);
            Finish(0);
            return;
        }

        if (EditorApplication.timeSinceStartup < nextSampleAt)
        {
            return;
        }

        nextSampleAt = EditorApplication.timeSinceStartup + SampleSeconds;

        Report(
            "SAMPLE actorPos=" + thoren.transform.position +
            " action=" + thoren.currentAction +
            " cachedApproach=" + GetCachedApproachText() +
            " directTarget=" + GetPrivateVector3(thoren, "directMoveTarget") +
            " targetZone=" + GetPrivateNullableZoneText(thoren, "directMoveTargetZone"));

        if (EditorApplication.timeSinceStartup >= timeoutAt)
        {
            Report("TIMEOUT " + BuildFailureDetail());
            Finish(1);
        }
    }

    static bool TryInitializeContext()
    {
        if (worldTime == null)
        {
            worldTime = UnityEngine.Object.FindAnyObjectByType<WorldTimeSystem>();
            if (worldTime == null)
            {
                Report("Waiting for WorldTimeSystem...");
                return false;
            }
        }

        if (thoren == null)
        {
            VillagerAI[] villagers =
                UnityEngine.Object.FindObjectsByType<VillagerAI>(
                    FindObjectsInactive.Exclude);
            for (int i = 0; i < villagers.Length; i++)
            {
                if (villagers[i] != null &&
                    villagers[i].name.IndexOf(
                        "thoren",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    thoren = villagers[i];
                    break;
                }
            }

            if (thoren == null)
            {
                Report("Waiting for thoren VillagerAI...");
                return false;
            }
        }

        if (blacksmith == null)
        {
            blacksmith = thoren.GetComponent<NpcFixedBlacksmithController>();
            if (blacksmith == null)
            {
                Fail("thoren is missing NpcFixedBlacksmithController.");
                return false;
            }
        }

        if (customerZone == null)
        {
            GameObject counterCustomerPoint = GameObject.Find("CounterCustomerPoint");
            if (counterCustomerPoint == null)
            {
                Report("Waiting for CounterCustomerPoint...");
                return false;
            }

            customerZone = counterCustomerPoint.GetComponent<BoxCollider2D>();
            if (customerZone == null)
            {
                Fail("CounterCustomerPoint is missing BoxCollider2D.");
                return false;
            }
        }

        SetWorldTime(worldTime, 6.25f);
        SetField(worldTime, "realSecondsPerGameDay", 24f);
        SetField(worldTime, "autoSaveWorldTime", false);

        blacksmith.debugLogs = true;
        thoren.debugWorkLogs = true;
        blacksmith.ResetCycle();
        thoren.SetActionImmediate(string.Empty, 0f);
        SetField(thoren, "thinkTimer", 0f);
        SetField(thoren, "actionTimer", 0f);

        Report(
            "Initialized actorPos=" + thoren.transform.position +
            " zoneCenter=" + customerZone.bounds.center +
            " zoneMin=" + customerZone.bounds.min +
            " zoneMax=" + customerZone.bounds.max);
        return true;
    }

    static string BuildFailureDetail()
    {
        Vector3 cachedApproach = GetCachedApproach();
        Vector3 directTarget = GetPrivateVector3(thoren, "directMoveTarget");
        StringBuilder builder = new StringBuilder();
        builder.Append("actorPos=").Append(thoren.transform.position);
        builder.Append(" action=").Append(thoren.currentAction);
        builder.Append(" cachedApproach=").Append(cachedApproach);
        builder.Append(" directTarget=").Append(directTarget);
        builder.Append(" targetZone=").Append(GetPrivateNullableZoneText(thoren, "directMoveTargetZone"));
        builder.Append(" blockersAtApproach=").Append(DescribeBlockingColliders(cachedApproach, 0.35f));
        builder.Append(" blockersAtActor=").Append(DescribeBlockingColliders(thoren.transform.position, 0.35f));
        builder.Append(" zoneCenter=").Append(customerZone.bounds.center);
        builder.Append(" zoneMin=").Append(customerZone.bounds.min);
        builder.Append(" zoneMax=").Append(customerZone.bounds.max);
        return builder.ToString();
    }

    static string DescribeBlockingColliders(Vector3 position, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
        if (hits == null || hits.Length == 0)
        {
            return "none";
        }

        StringBuilder builder = new StringBuilder();
        bool wroteAny = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                hit.isTrigger ||
                hit.transform.IsChildOf(thoren.transform))
            {
                continue;
            }

            if (wroteAny)
            {
                builder.Append(" | ");
            }

            builder.Append(hit.name)
                .Append("@")
                .Append(hit.bounds.center)
                .Append(" trigger=")
                .Append(hit.isTrigger ? 1 : 0)
                .Append(" layer=")
                .Append(hit.gameObject.layer);
            wroteAny = true;
        }

        return wroteAny ? builder.ToString() : "none";
    }

    static void SetWorldTime(WorldTimeSystem system, float hour)
    {
        if (system == null)
        {
            return;
        }

        MethodInfo setTime =
            typeof(WorldTimeSystem).GetMethod(
                "SetTime",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (setTime != null)
        {
            setTime.Invoke(system, new object[] { 1, 1, 1, hour, false });
        }
    }

    static void SetField(object target, string fieldName, object value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field =
            target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }

    static Vector3 GetCachedApproach()
    {
        object value = GetField(
            blacksmith,
            "cachedBrokerApproachPosition");
        return value is Vector3 vector
            ? vector
            : Vector3.zero;
    }

    static string GetCachedApproachText()
    {
        return GetCachedApproach().ToString();
    }

    static object GetField(object target, string fieldName)
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field =
            target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? field.GetValue(target) : null;
    }

    static Vector3 GetPrivateVector3(object target, string fieldName)
    {
        object value = GetField(target, fieldName);
        return value is Vector3 vector
            ? vector
            : Vector3.zero;
    }

    static string GetPrivateNullableZoneText(object target, string fieldName)
    {
        object value = GetField(target, fieldName);
        return value != null ? value.ToString() : "null";
    }

    static void Report(string message)
    {
        string line =
            "[ThorenNpcStoreBatch] " +
            DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
            " " + message;
        Debug.Log(line);
        File.AppendAllText(reportPath, line + Environment.NewLine);
    }

    static void Fail(string message)
    {
        Report("FAIL " + message);
        Finish(1);
    }

    static void Finish(int exitCode)
    {
        if (finished)
        {
            return;
        }

        finished = true;
        SessionState.EraseBool(SessionActiveKey);
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        EditorApplication.Exit(exitCode);
    }
}
