using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NpcThreeDayAuditBatchRunner
{
    const string ScenePath = "Assets/Lang.unity";
    const string ProgressReportFileName = "NpcThreeDayBatchRunner.log";
    const string SessionActiveKey = "NpcThreeDayAuditBatchRunner.Active";
    const string SessionExitCodeKey = "NpcThreeDayAuditBatchRunner.ExitCode";
    const float WarmupRealSeconds = 2f;
    const float SampleRealSeconds = 0.5f;
    const float AuditGameHours = 72f;
    const float RealSecondsPerGameDay = 180f;
    const float AuditTimeScale = 10f;

    static readonly string[] RequiredVillagerObjectNames =
    {
        "fish",
        "hunt",
        "farm"
    };

    static readonly string[] RequiredSmartNpcObjectNames =
    {
        "daocot",
        "satthu (1)",
        "tusinu (1)"
    };

    static readonly string[] RequiredMonsterObjectNames =
    {
        "YeuThu_Cap1",
        "cap2",
        "cap3"
    };

    static readonly Type RuntimeAuditType =
        Type.GetType("NpcRuntimeAuditTests, NpcRuntimeAudit.PlayMode");

    static object actors;
    static List<string> issues;
    static Component worldTime;
    static string progressReportPath;
    static float originalTimeScale;
    static float originalFixedDeltaTime;
    static float originalRealSecondsPerGameDay;
    static bool originalAutoSaveWorldTime;
    static BicanhSessionManager bicanhSessionManager;
    static bool originalBicanhAutoStart;
    static WorldEventSystem worldEventSystem;
    static float originalWorldEventChance;
    static string originalWorldEvent;
    static float startWorldHour;
    static double warmupUntil;
    static double sampleAt;
    static bool initialized;
    static bool finished;
    static int exitCode;

    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        if (!SessionState.GetBool(SessionActiveKey, false))
        {
            return;
        }

        progressReportPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", ProgressReportFileName));
        exitCode = SessionState.GetInt(SessionExitCodeKey, 0);

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/NPC/Run 3-Day Audit Batch")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[NpcThreeDayAudit] Unity is already entering Play Mode.");
            EditorApplication.Exit(2);
            return;
        }

        progressReportPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", ProgressReportFileName));
        if (File.Exists(progressReportPath))
        {
            File.Delete(progressReportPath);
        }

        actors = null;
        issues = null;
        worldTime = null;
        originalTimeScale = 1f;
        originalFixedDeltaTime = 0.02f;
        originalRealSecondsPerGameDay = 0f;
        originalAutoSaveWorldTime = false;
        bicanhSessionManager = null;
        originalBicanhAutoStart = false;
        worldEventSystem = null;
        originalWorldEventChance = 0f;
        originalWorldEvent = string.Empty;
        startWorldHour = 0f;
        warmupUntil = 0d;
        sampleAt = 0d;
        initialized = false;
        finished = false;
        exitCode = 0;

        SessionState.SetBool(SessionActiveKey, true);
        SessionState.SetInt(SessionExitCodeKey, exitCode);

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Update;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Report("Open scene: " + ScenePath);
        ReportConfiguredBindings("EditMode");
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            warmupUntil = EditorApplication.timeSinceStartup + WarmupRealSeconds;
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
        if (RuntimeAuditType == null)
        {
            Fail("NpcRuntimeAuditTests type was not found.");
            return;
        }

        if (!initialized)
        {
            if (EditorApplication.timeSinceStartup < warmupUntil)
            {
                return;
            }

            InitializeAudit();
            return;
        }

        if (EditorApplication.timeSinceStartup < sampleAt)
        {
            return;
        }

        if (!TryRefreshWorldTimeReference())
        {
            Report("WorldTimeSystem reference was lost, waiting for live singleton...");
            sampleAt = EditorApplication.timeSinceStartup + SampleRealSeconds;
            return;
        }

        float currentWorldHour = GetFloatMember(worldTime, "CurrentWorldHour", startWorldHour);
        InvokeAuditMethod(
            "SampleThreeDayActors",
            actors,
            issues,
            currentWorldHour,
            SampleRealSeconds);

        sampleAt = EditorApplication.timeSinceStartup + SampleRealSeconds;

        if (currentWorldHour - startWorldHour >= AuditGameHours)
        {
            CompleteAudit(currentWorldHour);
        }
    }

    static void InitializeAudit()
    {
        InvokeAuditMethod("EnsureAudioListener");

        worldTime = ResolveLiveWorldTime(createIfMissing: true);
        if (worldTime == null)
        {
            Report("WorldTimeSystem not found yet, waiting...");
            warmupUntil = EditorApplication.timeSinceStartup + 0.5d;
            return;
        }

        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        originalRealSecondsPerGameDay = GetFloatMember(worldTime, "realSecondsPerGameDay", 900f);
        originalAutoSaveWorldTime = Convert.ToBoolean(GetFieldValue(worldTime, "autoSaveWorldTime"));

        SetFieldValue(worldTime, "autoSaveWorldTime", false);
        SetFloatMember(worldTime, "realSecondsPerGameDay", RealSecondsPerGameDay);
        InvokeAuditMethod(
            "SetWorldTime",
            worldTime,
            GetIntField(worldTime, "currentYear", 1),
            GetIntField(worldTime, "currentMonth", 1),
            GetIntField(worldTime, "currentDay", 1),
            0.5f);

        bicanhSessionManager =
            UnityEngine.Object.FindAnyObjectByType<BicanhSessionManager>(
                FindObjectsInactive.Include);
        if (bicanhSessionManager != null)
        {
            originalBicanhAutoStart =
                bicanhSessionManager.autoStartOnSecretRealmOpen;
            bicanhSessionManager.autoStartOnSecretRealmOpen = false;
            if (bicanhSessionManager.sessionRunning)
            {
                bicanhSessionManager.EndSession("audit-disable");
            }
        }

        worldEventSystem =
            UnityEngine.Object.FindAnyObjectByType<WorldEventSystem>(
                FindObjectsInactive.Include);
        if (worldEventSystem != null)
        {
            originalWorldEventChance = worldEventSystem.eventChance;
            originalWorldEvent = worldEventSystem.currentEvent ?? string.Empty;
            worldEventSystem.eventChance = 0f;
            worldEventSystem.currentEvent = string.Empty;
        }

        Time.timeScale = AuditTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        ReportConfiguredBindings("PlayMode");
        actors = InvokeAuditMethod("PickThreeDayAuditActorsByExactNames");
        int actorCount = GetCollectionCount(actors);
        if (actorCount != 9)
        {
            Fail(
                "Expected exact 9 configured actors, but found " +
                actorCount +
                ". Check the binding report above for missing objects or wrong AI scripts.");
            return;
        }

        InvokeAuditMethod("EnableThreeDayDebugFlags", actors);

        issues = new List<string>();
        TryRefreshWorldTimeReference();
        startWorldHour = GetFloatMember(worldTime, "CurrentWorldHour", 0f);
        sampleAt = EditorApplication.timeSinceStartup + SampleRealSeconds;
        initialized = true;

        Report("Initialized 3-day audit with " + actorCount + " actors.");
    }

    static void ReportConfiguredBindings(string phase)
    {
        Report("Configured actor binding report (" + phase + ")");
        ReportBindingGroup("VillagerAI", typeof(VillagerAI), RequiredVillagerObjectNames, "villagerName");
        ReportBindingGroup("SmartNpcAI", typeof(SmartNpcAI), RequiredSmartNpcObjectNames, "npcName");
        ReportBindingGroup("MonsterAI", typeof(MonsterAI), RequiredMonsterObjectNames, "monsterName");
    }

    static void ReportBindingGroup(
        string expectedTypeName,
        Type componentType,
        string[] requiredObjectNames,
        string displayFieldName)
    {
        for (int i = 0; i < requiredObjectNames.Length; i++)
        {
            string requiredObjectName = requiredObjectNames[i];
            Component component = FindNamedComponent(
                componentType,
                requiredObjectName,
                displayFieldName);
            if (component == null)
            {
                Report(
                    "BINDING " +
                    expectedTypeName +
                    " target=" +
                    requiredObjectName +
                    " status=MISSING");
                continue;
            }

            string displayName = Convert.ToString(
                GetFieldValue(component, displayFieldName),
                CultureInfo.InvariantCulture);
            Component[] components = component.gameObject.GetComponents<Component>();
            List<string> componentNames = new List<string>(components.Length);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                Component attached = components[componentIndex];
                if (attached == null)
                {
                    componentNames.Add("<missing-script>");
                    continue;
                }

                string status = string.Empty;
                Behaviour behaviour = attached as Behaviour;
                if (behaviour != null)
                {
                    status = behaviour.enabled ? "[on]" : "[off]";
                }

                componentNames.Add(attached.GetType().Name + status);
            }

            int missingScriptCount =
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(component.gameObject);

            Report(
                "BINDING " +
                expectedTypeName +
                " target=" +
                requiredObjectName +
                " object=" +
                component.gameObject.name +
                " display=" +
                (string.IsNullOrWhiteSpace(displayName) ? "<empty>" : displayName) +
                " matchedType=" +
                component.GetType().Name +
                " missingScripts=" +
                missingScriptCount +
                " components=" +
                string.Join(", ", componentNames));
        }
    }

    static Component FindNamedComponent(
        Type componentType,
        string requiredObjectName,
        string displayFieldName)
    {
        if (componentType == null || string.IsNullOrWhiteSpace(requiredObjectName))
        {
            return null;
        }

        string requiredNormalized = NormalizeAuditName(requiredObjectName);
        UnityEngine.Object[] found =
            UnityEngine.Object.FindObjectsByType(
                componentType,
                FindObjectsInactive.Include);
        for (int i = 0; i < found.Length; i++)
        {
            Component component = found[i] as Component;
            if (component == null || component.gameObject == null)
            {
                continue;
            }

            if (NamesMatch(component.gameObject.name, requiredNormalized))
            {
                return component;
            }

            string displayName = Convert.ToString(
                GetFieldValue(component, displayFieldName),
                CultureInfo.InvariantCulture);
            if (NamesMatch(displayName, requiredNormalized))
            {
                return component;
            }
        }

        return null;
    }

    static bool NamesMatch(string candidate, string requiredNormalized)
    {
        if (string.IsNullOrWhiteSpace(candidate) ||
            string.IsNullOrWhiteSpace(requiredNormalized))
        {
            return false;
        }

        return string.Equals(
            NormalizeAuditName(candidate),
            requiredNormalized,
            StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeAuditName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (char.IsLetterOrDigit(current))
            {
                builder.Append(char.ToLowerInvariant(current));
            }
        }

        return builder.ToString();
    }

    static void EnsureMinimumActorCounts()
    {
        SmartNpcAI[] smartNpcs =
            UnityEngine.Object.FindObjectsByType<SmartNpcAI>(
                FindObjectsInactive.Exclude);
        VillagerAI[] villagers =
            UnityEngine.Object.FindObjectsByType<VillagerAI>(
                FindObjectsInactive.Exclude);
        MonsterAI[] monsters =
            UnityEngine.Object.FindObjectsByType<MonsterAI>(
                FindObjectsInactive.Exclude);

        int livingMonsterCount = 0;
        MonsterAI templateMonster = null;
        VillagerAI templateVillager = villagers.Length > 0 ? villagers[0] : null;
        SmartNpcAI templateSmartNpc = smartNpcs.Length > 0 ? smartNpcs[0] : null;
        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterAI monster = monsters[i];
            if (monster == null || monster.IsDead)
            {
                continue;
            }

            livingMonsterCount++;
            if (templateMonster == null)
            {
                templateMonster = monster;
            }
        }

        Report(
            "Actor pool before fill: villagers=" +
            villagers.Length +
            " smartNpcs=" +
            smartNpcs.Length +
            " livingMonsters=" +
            livingMonsterCount);

        if (villagers.Length < 3)
        {
            if (templateVillager == null)
            {
                Fail("Need at least 1 active VillagerAI to build the audit pool.");
                return;
            }

            for (int i = villagers.Length; i < 3; i++)
            {
                CloneVillagerForAudit(templateVillager, i - villagers.Length + 1);
            }
        }

        if (smartNpcs.Length < 3)
        {
            if (templateSmartNpc == null)
            {
                Fail("Need at least 1 active SmartNpcAI to build the audit pool.");
                return;
            }

            for (int i = smartNpcs.Length; i < 3; i++)
            {
                CloneSmartNpcForAudit(templateSmartNpc, i - smartNpcs.Length + 1);
            }
        }

        if (livingMonsterCount == 0 || templateMonster == null)
        {
            Fail("Need at least 1 living MonsterAI to build the audit pool.");
            return;
        }

        for (int i = livingMonsterCount; i < 3; i++)
        {
            CloneMonsterForAudit(templateMonster, i - livingMonsterCount + 1);
        }

        MonsterAI[] refreshedMonsters =
            UnityEngine.Object.FindObjectsByType<MonsterAI>(
                FindObjectsInactive.Exclude);
        int refreshedLivingMonsters = 0;
        for (int i = 0; i < refreshedMonsters.Length; i++)
        {
            if (refreshedMonsters[i] != null && !refreshedMonsters[i].IsDead)
            {
                refreshedLivingMonsters++;
            }
        }

        Report("Actor pool after fill: livingMonsters=" + refreshedLivingMonsters);
    }

    static void CloneVillagerForAudit(VillagerAI templateVillager, int cloneIndex)
    {
        if (templateVillager == null)
        {
            return;
        }

        Vector3 sourcePosition = templateVillager.transform.position;
        Vector3 offset = new Vector3(1.25f * cloneIndex, 0f, 0f);
        GameObject cloneObject = UnityEngine.Object.Instantiate(
            templateVillager.gameObject,
            sourcePosition + offset,
            templateVillager.transform.rotation);
        cloneObject.name = templateVillager.gameObject.name + "_AuditClone_" + cloneIndex;

        VillagerAI cloneVillager = cloneObject.GetComponent<VillagerAI>();
        if (cloneVillager != null)
        {
            cloneVillager.villagerName = string.IsNullOrWhiteSpace(templateVillager.villagerName)
                ? cloneObject.name
                : templateVillager.villagerName + " AuditClone " + cloneIndex;
        }

        Report(
            "Cloned villager for audit: source=" +
            templateVillager.gameObject.name +
            " clone=" +
            cloneObject.name +
            " position=" +
            cloneObject.transform.position.ToString("F2", CultureInfo.InvariantCulture));
    }

    static void CloneSmartNpcForAudit(SmartNpcAI templateSmartNpc, int cloneIndex)
    {
        if (templateSmartNpc == null)
        {
            return;
        }

        Vector3 sourcePosition = templateSmartNpc.transform.position;
        Vector3 offset = new Vector3(1.25f * cloneIndex, 0f, 0f);
        GameObject cloneObject = UnityEngine.Object.Instantiate(
            templateSmartNpc.gameObject,
            sourcePosition + offset,
            templateSmartNpc.transform.rotation);
        cloneObject.name = templateSmartNpc.gameObject.name + "_AuditClone_" + cloneIndex;

        SmartNpcAI cloneSmartNpc = cloneObject.GetComponent<SmartNpcAI>();
        if (cloneSmartNpc != null)
        {
            cloneSmartNpc.npcName = string.IsNullOrWhiteSpace(templateSmartNpc.npcName)
                ? cloneObject.name
                : templateSmartNpc.npcName + " AuditClone " + cloneIndex;
        }

        Report(
            "Cloned smart npc for audit: source=" +
            templateSmartNpc.gameObject.name +
            " clone=" +
            cloneObject.name +
            " position=" +
            cloneObject.transform.position.ToString("F2", CultureInfo.InvariantCulture));
    }

    static void CloneMonsterForAudit(MonsterAI templateMonster, int cloneIndex)
    {
        if (templateMonster == null)
        {
            return;
        }

        Vector3 sourcePosition = templateMonster.transform.position;
        Vector3 offset = new Vector3(1.5f * cloneIndex, 0f, 0f);
        GameObject cloneObject = UnityEngine.Object.Instantiate(
            templateMonster.gameObject,
            sourcePosition + offset,
            templateMonster.transform.rotation);
        cloneObject.name = templateMonster.gameObject.name + "_AuditClone_" + cloneIndex;

        MonsterAI cloneMonster = cloneObject.GetComponent<MonsterAI>();
        if (cloneMonster != null)
        {
            cloneMonster.monsterName = string.IsNullOrWhiteSpace(templateMonster.monsterName)
                ? cloneObject.name
                : templateMonster.monsterName + " AuditClone " + cloneIndex;
            cloneMonster.currentHP = Mathf.Max(1, cloneMonster.maxHP);
        }

        Report(
            "Cloned monster for audit: source=" +
            templateMonster.gameObject.name +
            " clone=" +
            cloneObject.name +
            " position=" +
            cloneObject.transform.position.ToString("F2", CultureInfo.InvariantCulture));
    }

    static void CompleteAudit(float currentWorldHour)
    {
        string reportPath = Convert.ToString(
            InvokeAuditMethod(
                "WriteThreeDayReport",
                actors,
                issues,
                startWorldHour,
                currentWorldHour),
            CultureInfo.InvariantCulture);

        string summary = Convert.ToString(
            InvokeAuditMethod(
                "BuildThreeDaySummary",
                actors,
                issues,
                startWorldHour,
                currentWorldHour),
            CultureInfo.InvariantCulture);

        Report("NPC_THREE_DAY_AUDIT_REPORT: " + reportPath);
        Report(summary);

        if (issues != null && issues.Count > 0)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                Report("ISSUE: " + issues[i]);
            }

            exitCode = 1;
        }

        WriteBatchResultsFile(summary);
        WriteReportAndExit();
    }

    static object InvokeAuditMethod(string methodName, params object[] args)
    {
        MethodInfo method = RuntimeAuditType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        if (method == null)
        {
            throw new MissingMethodException(RuntimeAuditType.FullName, methodName);
        }

        return method.Invoke(null, args);
    }

    static bool TryRefreshWorldTimeReference()
    {
        Component liveWorldTime = ResolveLiveWorldTime(createIfMissing: false);
        if (liveWorldTime == null)
        {
            worldTime = null;
            return false;
        }

        worldTime = liveWorldTime;
        return true;
    }

    static Component ResolveLiveWorldTime(bool createIfMissing)
    {
        if (WorldTimeSystem.Instance != null)
        {
            return WorldTimeSystem.Instance;
        }

        WorldTimeSystem foundWorldTime = UnityEngine.Object.FindAnyObjectByType<WorldTimeSystem>(
            FindObjectsInactive.Include);
        if (foundWorldTime != null)
        {
            return foundWorldTime;
        }

        return createIfMissing
            ? InvokeAuditMethod("EnsureWorldTimeSystem") as Component
            : null;
    }

    static float GetFloatMember(object target, string memberName, float fallback)
    {
        if (target == null)
        {
            return fallback;
        }

        PropertyInfo property = target.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            object value = property.GetValue(target, null);
            return ToSingle(value, fallback);
        }

        FieldInfo field = target.GetType().GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            object value = field.GetValue(target);
            return ToSingle(value, fallback);
        }

        return fallback;
    }

    static float ToSingle(object value, float fallback)
    {
        if (value == null)
        {
            return fallback;
        }

        try
        {
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    static int GetIntField(Component component, string fieldName, int fallback)
    {
        object value = GetFieldValue(component, fieldName);
        if (value == null)
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    static object GetFieldValue(Component component, string fieldName)
    {
        if (component == null)
        {
            return null;
        }

        FieldInfo field = component.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? field.GetValue(component) : null;
    }

    static void SetFieldValue(Component component, string fieldName, object value)
    {
        if (component == null)
        {
            return;
        }

        FieldInfo field = component.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(component, value);
        }
    }

    static void SetFloatMember(object target, string memberName, float value)
    {
        if (target == null)
        {
            return;
        }

        PropertyInfo property = target.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
            return;
        }

        FieldInfo field = target.GetType().GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }

    static int GetCollectionCount(object value)
    {
        ICollection collection = value as ICollection;
        if (collection != null)
        {
            return collection.Count;
        }

        IEnumerable enumerable = value as IEnumerable;
        if (enumerable == null)
        {
            return 0;
        }

        int count = 0;
        foreach (object ignored in enumerable)
        {
            count++;
        }

        return count;
    }

    static void Report(string line)
    {
        string formatted =
            "[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + line;
        if (!string.IsNullOrEmpty(progressReportPath))
        {
            File.AppendAllText(progressReportPath, formatted + Environment.NewLine);
        }

        Debug.Log("[NpcThreeDayAudit] " + line);
    }

    static void Fail(string message)
    {
        exitCode = 1;
        Report("FAIL: " + message);
        WriteBatchResultsFile(message);
        WriteReportAndExit();
    }

    static void WriteReportAndExit()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        RestoreTimeSettings();

        SessionState.EraseBool(SessionActiveKey);
        SessionState.EraseInt(SessionExitCodeKey);

        Debug.Log("[NpcThreeDayAudit] Progress log written to " + progressReportPath);

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
    }

    static void RestoreTimeSettings()
    {
        TryRefreshWorldTimeReference();
        if (worldTime != null)
        {
            SetFloatMember(worldTime, "realSecondsPerGameDay", originalRealSecondsPerGameDay);
            SetFieldValue(worldTime, "autoSaveWorldTime", originalAutoSaveWorldTime);
        }

        if (bicanhSessionManager != null)
        {
            bicanhSessionManager.autoStartOnSecretRealmOpen =
                originalBicanhAutoStart;
        }

        if (worldEventSystem != null)
        {
            worldEventSystem.eventChance = originalWorldEventChance;
            worldEventSystem.currentEvent = originalWorldEvent ?? string.Empty;
        }

        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
    }

    static void WriteBatchResultsFile(string message)
    {
        string resultsPath = GetCommandLineValue("-testResults");
        if (string.IsNullOrEmpty(resultsPath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(resultsPath);
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        int failureCount = exitCode == 0 ? 0 : 1;
        int passCount = exitCode == 0 ? 1 : 0;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        builder.Append("<test-run");
        builder.Append(" result=\"").Append(exitCode == 0 ? "Passed" : "Failed").Append("\"");
        builder.Append(" total=\"1\"");
        builder.Append(" passed=\"").Append(passCount).Append("\"");
        builder.Append(" failed=\"").Append(failureCount).Append("\"");
        builder.Append(" skipped=\"0\">");
        builder.AppendLine();
        builder.Append("  <test-case name=\"NpcThreeDayAuditBatchRunner\"");
        builder.Append(" result=\"").Append(exitCode == 0 ? "Passed" : "Failed").Append("\"");
        builder.Append(" message=\"").Append(XmlEscape(message)).AppendLine("\" />");
        builder.AppendLine("</test-run>");
        File.WriteAllText(fullPath, builder.ToString(), Encoding.UTF8);
        Report("Wrote batch results to " + fullPath);
    }

    static string GetCommandLineValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }

    static string XmlEscape(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
    }
}
