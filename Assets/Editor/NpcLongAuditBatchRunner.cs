using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NpcLongAuditBatchRunner
{
    const string ScenePath = "Assets/Lang.unity";
    const string ReportFileName = "CodexNpcLongAuditReport.txt";
    const string SampleFileName = "CodexNpcLongAuditSamples.csv";
    const string ProgressFileName = "CodexNpcLongAuditProgress.log";
    const string SessionActiveKey = "NpcLongAuditBatchRunner.Active";
    const string SessionExitCodeKey = "NpcLongAuditBatchRunner.ExitCode";
    const float WarmupRealSeconds = 2.5f;
    const float SampleRealSeconds = 0.75f;
    const float AuditGameHours = 48f;
    const float RealSecondsPerGameDay = 180f;
    const float AuditTimeScale = 30f;
    const float StationaryMoveEpsilon = 0.06f;
    const float StuckRealSeconds = 4f;
    const float SamePlaceCellSize = 0.75f;
    const double EnterPlayModeTimeoutSeconds = 60d;

    static readonly BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static string progressPath;
    static string samplePath;
    static string reportPath;
    static WorldTimeSystem worldTime;
    static float originalTimeScale;
    static float originalFixedDeltaTime;
    static float originalRealSecondsPerGameDay;
    static bool originalAutoSaveWorldTime;
    static bool originalLoadSavedTimeOnAwake;
    static double warmupUntil;
    static double sampleAt;
    static float startWorldHour;
    static bool initialized;
    static bool finished;
    static double runStartedAt;
    static int exitCode;
    static int sampleIndex;
    static List<ActorRecord> actors;

    sealed class ActorRecord
    {
        public GameObject gameObject;
        public Component primary;
        public string key;
        public string displayName;
        public string typeName;
        public string job;
        public string roles;
        public Vector3 startPosition;
        public Vector3 lastPosition;
        public string lastAction;
        public string lastActionKey;
        public string lastSchedule;
        public string lastArea;
        public string lastMapZone;
        public float totalDistance;
        public float maxDistanceFromStart;
        public float stationaryIntentSeconds;
        public float maxStationaryIntentSeconds;
        public float outsideAreaSeconds;
        public float maxOutsideAreaSeconds;
        public float obstacleOverlapSeconds;
        public float maxObstacleOverlapSeconds;
        public bool activeStuck;
        public string activeStuckKey;
        public int stuckEventCount;
        public readonly Dictionary<string, float> actionSeconds =
            new Dictionary<string, float>();
        public readonly Dictionary<string, float> scheduleSeconds =
            new Dictionary<string, float>();
        public readonly Dictionary<string, float> jobSeconds =
            new Dictionary<string, float>();
        public readonly List<string> stuckEvents = new List<string>();
    }

    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        if (!SessionState.GetBool(SessionActiveKey, false))
        {
            return;
        }

        SetupPaths();
        exitCode = SessionState.GetInt(SessionExitCodeKey, 0);
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/NPC/Run Codex Long Audit")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[NpcLongAudit] Unity is already entering Play Mode.");
            EditorApplication.Exit(2);
            return;
        }

        SetupPaths();
        DeleteIfExists(progressPath);
        DeleteIfExists(samplePath);
        DeleteIfExists(reportPath);

        worldTime = null;
        originalTimeScale = 1f;
        originalFixedDeltaTime = 0.02f;
        originalRealSecondsPerGameDay = 900f;
        originalAutoSaveWorldTime = false;
        originalLoadSavedTimeOnAwake = true;
        warmupUntil = 0d;
        sampleAt = 0d;
        startWorldHour = 0f;
        initialized = false;
        finished = false;
        runStartedAt = EditorApplication.timeSinceStartup;
        exitCode = 0;
        sampleIndex = 0;
        actors = new List<ActorRecord>();

        SessionState.SetBool(SessionActiveKey, true);
        SessionState.SetInt(SessionExitCodeKey, exitCode);
        SmartNpcAI.SuppressRuntimeDebugOutput = true;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Update;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Report("Open scene: " + ScenePath);
        EditorApplication.delayCall += EnterPlayModeAfterSceneOpen;
    }

    static void EnterPlayModeAfterSceneOpen()
    {
        if (finished ||
            !SessionState.GetBool(SessionActiveKey, false) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Report("Enter Play Mode");
        EditorApplication.EnterPlaymode();
    }

    static void SetupPaths()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        progressPath = Path.Combine(root, ProgressFileName);
        samplePath = Path.Combine(root, SampleFileName);
        reportPath = Path.Combine(root, ReportFileName);
    }

    static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
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
        if (finished)
        {
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.timeSinceStartup - runStartedAt >
                EnterPlayModeTimeoutSeconds)
            {
                Fail(
                    "Timed out entering Play Mode after opening " +
                    ScenePath);
            }

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

            InitializeAudit();
            return;
        }

        if (EditorApplication.timeSinceStartup < sampleAt)
        {
            return;
        }

        if (!RefreshWorldTime())
        {
            sampleAt = EditorApplication.timeSinceStartup + SampleRealSeconds;
            Report("Waiting for WorldTimeSystem...");
            return;
        }

        float currentWorldHour = worldTime.CurrentWorldHour;
        SampleActors(currentWorldHour);
        sampleAt = EditorApplication.timeSinceStartup + SampleRealSeconds;

        if (currentWorldHour - startWorldHour >= AuditGameHours)
        {
            CompleteAudit(currentWorldHour);
        }
    }

    static void InitializeAudit()
    {
        worldTime = WorldTimeSystem.EnsureInstance();
        if (worldTime == null)
        {
            warmupUntil = EditorApplication.timeSinceStartup + 0.5d;
            Report("WorldTimeSystem not ready.");
            return;
        }

        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        originalRealSecondsPerGameDay = worldTime.realSecondsPerGameDay;
        originalAutoSaveWorldTime = worldTime.autoSaveWorldTime;
        originalLoadSavedTimeOnAwake = worldTime.loadSavedTimeOnAwake;

        worldTime.autoSaveWorldTime = false;
        worldTime.loadSavedTimeOnAwake = false;
        worldTime.realSecondsPerGameDay = RealSecondsPerGameDay;
        worldTime.SetTime(1, 1, 1, 6f, true);
        SmartNpcAI.SuppressRuntimeDebugOutput = true;

        DisableRandomWorldEvents();
        DisableBicanhAutoStart();
        EnableUsefulDebugFlags();

        Time.timeScale = AuditTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        actors = BuildActorRecords();
        if (actors.Count == 0)
        {
            Fail("No NPC actors found for long audit.");
            return;
        }

        using (StreamWriter writer = new StreamWriter(samplePath, false, Encoding.UTF8))
        {
            writer.WriteLine(
                "sample,worldHour,day,hour,key,type,display,job,schedule,actionKey,action,roles,x,y,z,mapZone,locationArea,hasMoveIntent,targetName,targetX,targetY,distanceSinceLast,totalDistance,stationaryIntentSeconds,outsideAreaSeconds,obstacleOverlapSeconds");
        }

        RefreshWorldTime();
        startWorldHour = worldTime.CurrentWorldHour;
        sampleAt = EditorApplication.timeSinceStartup + SampleRealSeconds;
        initialized = true;
        Report("Initialized long audit actors=" + actors.Count);
    }

    static bool RefreshWorldTime()
    {
        if (WorldTimeSystem.Instance != null)
        {
            worldTime = WorldTimeSystem.Instance;
            return true;
        }

        WorldTimeSystem found =
            UnityEngine.Object.FindAnyObjectByType<WorldTimeSystem>(
                FindObjectsInactive.Include);
        worldTime = found;
        return worldTime != null;
    }

    static void DisableRandomWorldEvents()
    {
        WorldEventSystem worldEvent =
            UnityEngine.Object.FindAnyObjectByType<WorldEventSystem>(
                FindObjectsInactive.Include);
        if (worldEvent != null)
        {
            worldEvent.eventChance = 0f;
            worldEvent.currentEvent = string.Empty;
        }
    }

    static void DisableBicanhAutoStart()
    {
        BicanhSessionManager manager =
            UnityEngine.Object.FindAnyObjectByType<BicanhSessionManager>(
                FindObjectsInactive.Include);
        if (manager == null)
        {
            return;
        }

        manager.autoStartOnSecretRealmOpen = false;
        if (manager.sessionRunning)
        {
            manager.EndSession("long-audit-disable");
        }
    }

    static void EnableUsefulDebugFlags()
    {
        foreach (SmartNpcAI npc in UnityEngine.Object.FindObjectsByType<SmartNpcAI>(
            FindObjectsInactive.Exclude))
        {
            npc.debugFlowLogs = false;
            npc.runtimeTraceEnabled = false;
            npc.runtimeTraceEveryUpdate = false;
            npc.runtimeTraceEveryThink = false;
            npc.runtimeTraceScheduleChanges = false;

            NPCVisualAnimation visual =
                npc.GetComponent<NPCVisualAnimation>();
            if (visual != null)
            {
                visual.debugVisualLogs = false;
            }
        }

        foreach (VillagerAI villager in UnityEngine.Object.FindObjectsByType<VillagerAI>(
            FindObjectsInactive.Exclude))
        {
            SetFieldValue(villager, "debugWorkLogs", false);
            NPCVisualAnimation visual =
                villager.GetComponent<NPCVisualAnimation>();
            if (visual != null)
            {
                visual.debugVisualLogs = false;
            }
        }
    }

    static List<ActorRecord> BuildActorRecords()
    {
        Dictionary<GameObject, Component> unique =
            new Dictionary<GameObject, Component>();
        AddComponents(unique, UnityEngine.Object.FindObjectsByType<VillagerAI>(
            FindObjectsInactive.Exclude));
        AddComponents(unique, UnityEngine.Object.FindObjectsByType<SmartNpcAI>(
            FindObjectsInactive.Exclude));
        AddComponents(unique, UnityEngine.Object.FindObjectsByType<NpcTaskProvider>(
            FindObjectsInactive.Exclude));
        AddComponents(unique, UnityEngine.Object.FindObjectsByType<NpcVillageSupplyMerchant>(
            FindObjectsInactive.Exclude));
        AddComponents(unique, UnityEngine.Object.FindObjectsByType<NpcFixedBlacksmithController>(
            FindObjectsInactive.Exclude));
        AddComponents(unique, UnityEngine.Object.FindObjectsByType<NpcFixedAlchemistController>(
            FindObjectsInactive.Exclude));

        List<ActorRecord> result = new List<ActorRecord>();
        foreach (KeyValuePair<GameObject, Component> pair in unique)
        {
            GameObject actor = pair.Key;
            if (actor == null)
            {
                continue;
            }

            Component primary = PickPrimaryComponent(actor, pair.Value);
            Vector3 position = actor.transform.position;
            ActorRecord record = new ActorRecord
            {
                gameObject = actor,
                primary = primary,
                key = actor.name,
                displayName = ResolveDisplayName(actor),
                typeName = primary != null ? primary.GetType().Name : "GameObject",
                job = ResolveJob(actor),
                roles = ResolveRoles(actor),
                startPosition = position,
                lastPosition = position
            };
            result.Add(record);
            Report(
                "ACTOR key=" + record.key +
                " display=" + record.displayName +
                " type=" + record.typeName +
                " job=" + record.job +
                " roles=" + record.roles +
                " pos=" + FormatVector(position));
        }

        return result;
    }

    static void AddComponents<T>(
        Dictionary<GameObject, Component> unique,
        T[] components)
        where T : Component
    {
        if (components == null)
        {
            return;
        }

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null &&
                component.gameObject != null &&
                !unique.ContainsKey(component.gameObject))
            {
                unique.Add(component.gameObject, component);
            }
        }
    }

    static Component PickPrimaryComponent(GameObject actor, Component fallback)
    {
        SmartNpcAI smart = actor.GetComponent<SmartNpcAI>();
        if (smart != null)
        {
            return smart;
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager;
        }

        return fallback;
    }

    static void SampleActors(float currentWorldHour)
    {
        sampleIndex++;
        int day = worldTime != null ? worldTime.CurrentDay : 0;
        float hour = worldTime != null ? worldTime.CurrentHour : 0f;

        using (StreamWriter writer = new StreamWriter(samplePath, true, Encoding.UTF8))
        {
            for (int i = 0; i < actors.Count; i++)
            {
                ActorRecord actor = actors[i];
                if (actor == null ||
                    actor.gameObject == null ||
                    !actor.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 position = actor.gameObject.transform.position;
                float distance = Vector2.Distance(position, actor.lastPosition);
                actor.totalDistance += distance;
                actor.maxDistanceFromStart = Mathf.Max(
                    actor.maxDistanceFromStart,
                    Vector2.Distance(position, actor.startPosition));

                string action = ResolveAction(actor.gameObject);
                string actionKey = ResolveActionKey(actor.gameObject);
                string schedule = ResolveSchedule(actor.gameObject);
                string mapZone = ResolveMapZone(position);
                string locationArea = ResolveLocationArea(position);
                bool outsideArea = string.Equals(mapZone, "Outside", StringComparison.Ordinal);
                bool obstacleOverlap = IsObstacleOverlap(actor.gameObject, position);
                MoveIntent intent = ResolveMoveIntent(actor.gameObject, action, actionKey);

                if (intent.hasMoveIntent && distance <= StationaryMoveEpsilon)
                {
                    actor.stationaryIntentSeconds += SampleRealSeconds;
                }
                else
                {
                    actor.stationaryIntentSeconds = 0f;
                    actor.activeStuck = false;
                    actor.activeStuckKey = string.Empty;
                }

                actor.maxStationaryIntentSeconds = Mathf.Max(
                    actor.maxStationaryIntentSeconds,
                    actor.stationaryIntentSeconds);

                if (outsideArea)
                {
                    actor.outsideAreaSeconds += SampleRealSeconds;
                }
                else
                {
                    actor.outsideAreaSeconds = 0f;
                }

                actor.maxOutsideAreaSeconds = Mathf.Max(
                    actor.maxOutsideAreaSeconds,
                    actor.outsideAreaSeconds);

                if (obstacleOverlap)
                {
                    actor.obstacleOverlapSeconds += SampleRealSeconds;
                }
                else
                {
                    actor.obstacleOverlapSeconds = 0f;
                }

                actor.maxObstacleOverlapSeconds = Mathf.Max(
                    actor.maxObstacleOverlapSeconds,
                    actor.obstacleOverlapSeconds);

                AddSeconds(actor.actionSeconds, SafeBucket(actionKey, action), SampleRealSeconds);
                AddSeconds(actor.scheduleSeconds, schedule, SampleRealSeconds);
                AddSeconds(actor.jobSeconds, actor.job, SampleRealSeconds);

                if (actor.stationaryIntentSeconds >= StuckRealSeconds)
                {
                    string stuckKey =
                        Quantize(position, SamePlaceCellSize) + "|" +
                        schedule + "|" +
                        SafeBucket(actionKey, action);
                    if (!actor.activeStuck ||
                        !string.Equals(actor.activeStuckKey, stuckKey, StringComparison.Ordinal))
                    {
                        actor.activeStuck = true;
                        actor.activeStuckKey = stuckKey;
                        actor.stuckEventCount++;
                        string eventLine =
                            "worldHour=" + JsonNumber(currentWorldHour) +
                            " day=" + day +
                            " hour=" + JsonNumber(hour) +
                            " pos=" + FormatVector(position) +
                            " mapZone=" + mapZone +
                            " area=" + locationArea +
                            " schedule=" + schedule +
                            " actionKey=" + actionKey +
                            " action=" + action +
                            " target=" + intent.targetName +
                            " stationaryIntentSeconds=" +
                            JsonNumber(actor.stationaryIntentSeconds);
                        actor.stuckEvents.Add(eventLine);
                        Report("STUCK " + actor.key + " " + eventLine);
                    }
                }

                writer.WriteLine(string.Join(
                    ",",
                    Csv(sampleIndex.ToString(CultureInfo.InvariantCulture)),
                    Csv(JsonNumber(currentWorldHour)),
                    Csv(day.ToString(CultureInfo.InvariantCulture)),
                    Csv(JsonNumber(hour)),
                    Csv(actor.key),
                    Csv(actor.typeName),
                    Csv(actor.displayName),
                    Csv(actor.job),
                    Csv(schedule),
                    Csv(actionKey),
                    Csv(action),
                    Csv(actor.roles),
                    Csv(JsonNumber(position.x)),
                    Csv(JsonNumber(position.y)),
                    Csv(JsonNumber(position.z)),
                    Csv(mapZone),
                    Csv(locationArea),
                    Csv(intent.hasMoveIntent ? "1" : "0"),
                    Csv(intent.targetName),
                    Csv(JsonNumber(intent.targetPosition.x)),
                    Csv(JsonNumber(intent.targetPosition.y)),
                    Csv(JsonNumber(distance)),
                    Csv(JsonNumber(actor.totalDistance)),
                    Csv(JsonNumber(actor.stationaryIntentSeconds)),
                    Csv(JsonNumber(actor.outsideAreaSeconds)),
                    Csv(JsonNumber(actor.obstacleOverlapSeconds))));

                actor.lastPosition = position;
                actor.lastAction = action;
                actor.lastActionKey = actionKey;
                actor.lastSchedule = schedule;
                actor.lastArea = locationArea;
                actor.lastMapZone = mapZone;
            }
        }
    }

    static void CompleteAudit(float currentWorldHour)
    {
        WriteSummary(currentWorldHour);
        WriteReportAndExit();
    }

    static void WriteSummary(float currentWorldHour)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("NPC_LONG_AUDIT_SUMMARY");
        builder.AppendLine("scene=" + ScenePath);
        builder.AppendLine("actors=" + actors.Count);
        builder.AppendLine("samples=" + sampleIndex);
        builder.AppendLine("worldHourStart=" + JsonNumber(startWorldHour));
        builder.AppendLine("worldHourEnd=" + JsonNumber(currentWorldHour));
        builder.AppendLine("sampleCsv=" + samplePath);
        builder.AppendLine();

        builder.AppendLine("STUCK_EVENTS");
        int totalStuck = 0;
        for (int i = 0; i < actors.Count; i++)
        {
            totalStuck += actors[i].stuckEventCount;
            if (actors[i].stuckEventCount <= 0)
            {
                continue;
            }

            builder.AppendLine(
                actors[i].key +
                " display=" + actors[i].displayName +
                " job=" + actors[i].job +
                " type=" + actors[i].typeName +
                " stuckEvents=" + actors[i].stuckEventCount);
            for (int eventIndex = 0; eventIndex < actors[i].stuckEvents.Count; eventIndex++)
            {
                builder.AppendLine("  " + actors[i].stuckEvents[eventIndex]);
            }
        }

        if (totalStuck == 0)
        {
            builder.AppendLine("none");
        }

        builder.AppendLine();
        builder.AppendLine("ACTOR_ROLLUP");
        for (int i = 0; i < actors.Count; i++)
        {
            ActorRecord actor = actors[i];
            builder.AppendLine(
                actor.key +
                " display=" + actor.displayName +
                " type=" + actor.typeName +
                " job=" + actor.job +
                " roles=" + actor.roles +
                " lastSchedule=" + actor.lastSchedule +
                " lastActionKey=" + actor.lastActionKey +
                " lastAction=" + actor.lastAction +
                " lastMapZone=" + actor.lastMapZone +
                " lastArea=" + actor.lastArea +
                " totalDistance=" + JsonNumber(actor.totalDistance) +
                " maxDistanceFromStart=" + JsonNumber(actor.maxDistanceFromStart) +
                " maxStationaryIntentSeconds=" + JsonNumber(actor.maxStationaryIntentSeconds) +
                " maxOutsideAreaSeconds=" + JsonNumber(actor.maxOutsideAreaSeconds) +
                " maxObstacleOverlapSeconds=" + JsonNumber(actor.maxObstacleOverlapSeconds));
        }

        builder.AppendLine();
        builder.AppendLine("COMMON_STUCK_GROUPS");
        Dictionary<string, List<ActorRecord>> groups =
            new Dictionary<string, List<ActorRecord>>();
        for (int i = 0; i < actors.Count; i++)
        {
            ActorRecord actor = actors[i];
            if (actor.stuckEvents.Count == 0)
            {
                continue;
            }

            string groupKey = actor.activeStuckKey;
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                continue;
            }

            if (!groups.TryGetValue(groupKey, out List<ActorRecord> group))
            {
                group = new List<ActorRecord>();
                groups.Add(groupKey, group);
            }

            group.Add(actor);
        }

        bool wroteGroup = false;
        foreach (KeyValuePair<string, List<ActorRecord>> pair in groups)
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            wroteGroup = true;
            List<string> names = new List<string>();
            for (int i = 0; i < pair.Value.Count; i++)
            {
                names.Add(pair.Value[i].key + "/" + pair.Value[i].job);
            }

            builder.AppendLine(pair.Key + " actors=" + string.Join("; ", names));
        }

        if (!wroteGroup)
        {
            builder.AppendLine("none");
        }

        File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
        Report("NPC_LONG_AUDIT_REPORT: " + reportPath);
        Report("NPC_LONG_AUDIT_SAMPLES: " + samplePath);
    }

    static string ResolveDisplayName(GameObject actor)
    {
        SmartNpcAI smart = actor.GetComponent<SmartNpcAI>();
        if (smart != null && !string.IsNullOrWhiteSpace(smart.npcName))
        {
            return smart.npcName;
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager != null && !string.IsNullOrWhiteSpace(villager.villagerName))
        {
            return villager.villagerName;
        }

        NPCIdentity identity = actor.GetComponent<NPCIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.npcName))
        {
            return identity.npcName;
        }

        return actor.name;
    }

    static string ResolveJob(GameObject actor)
    {
        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.job.ToString();
        }

        NpcScheduleController schedule = actor.GetComponent<NpcScheduleController>();
        if (schedule != null)
        {
            return schedule.lifePath.ToString();
        }

        return "None";
    }

    static string ResolveRoles(GameObject actor)
    {
        List<string> roles = new List<string>();
        AddRole<NpcTaskProvider>(actor, roles, "TaskProvider");
        AddRole<NpcVillageSupplyMerchant>(actor, roles, "VillageSupplyMerchant");
        AddRole<NpcTradeAgent>(actor, roles, "TradeAgent");
        AddRole<NpcFixedBlacksmithController>(actor, roles, "FixedBlacksmith");
        AddRole<NpcFixedAlchemistController>(actor, roles, "FixedAlchemist");
        AddRole<NpcForgeAgent>(actor, roles, "ForgeAgent");
        AddRole<NpcAlchemyAgent>(actor, roles, "AlchemyAgent");
        AddRole<FrontierWatchDutyAgent>(actor, roles, "FrontierWatch");
        return roles.Count > 0 ? string.Join("|", roles) : "None";
    }

    static void AddRole<T>(GameObject actor, List<string> roles, string role)
        where T : Component
    {
        if (actor.GetComponent<T>() != null)
        {
            roles.Add(role);
        }
    }

    static string ResolveAction(GameObject actor)
    {
        SmartNpcAI smart = actor.GetComponent<SmartNpcAI>();
        if (smart != null)
        {
            return smart.currentAction ?? string.Empty;
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager != null)
        {
            string publicText = villager.GetPlayerActionText();
            string raw = Convert.ToString(GetFieldValue(villager, "currentAction"), CultureInfo.InvariantCulture);
            return !string.IsNullOrWhiteSpace(publicText) ? publicText : raw;
        }

        NpcTaskProvider provider = actor.GetComponent<NpcTaskProvider>();
        if (provider != null)
        {
            return Convert.ToString(GetFieldValue(provider, "currentAction"), CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    static string ResolveActionKey(GameObject actor)
    {
        SmartNpcAI smart = actor.GetComponent<SmartNpcAI>();
        if (smart != null)
        {
            return smart.currentActionKey ?? string.Empty;
        }

        object value = GetFieldValue(actor.GetComponent<VillagerAI>(), "currentActionKey");
        return value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
    }

    static string ResolveSchedule(GameObject actor)
    {
        NpcScheduleController schedule = actor.GetComponent<NpcScheduleController>();
        if (schedule == null)
        {
            return "None";
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        if (slot == null)
        {
            return schedule.CurrentActivity.ToString();
        }

        return schedule.CurrentActivity +
            "@" +
            slot.startHour.ToString("0.##", CultureInfo.InvariantCulture) +
            "-" +
            slot.endHour.ToString("0.##", CultureInfo.InvariantCulture);
    }

    static string ResolveMapZone(Vector3 position)
    {
        NpcMapArea area = NpcMapArea.FindArea(position);
        if (area != null)
        {
            return area.zone.ToString();
        }

        NpcMapArea nearest = NpcMapArea.FindNearestArea(position);
        return nearest != null
            ? "Outside(nearest=" + nearest.zone + ")"
            : "Outside";
    }

    static string ResolveLocationArea(Vector3 position)
    {
        NpcLocationArea area = NpcLocationArea.FindArea(position);
        if (area == null)
        {
            return "None";
        }

        return area.purpose + "/" + area.activity + "/" + area.zone;
    }

    struct MoveIntent
    {
        public bool hasMoveIntent;
        public string targetName;
        public Vector3 targetPosition;
    }

    static MoveIntent ResolveMoveIntent(GameObject actor, string action, string actionKey)
    {
        MoveIntent intent = new MoveIntent
        {
            hasMoveIntent = LooksLikeMoveAction(action, actionKey),
            targetName = string.Empty,
            targetPosition = Vector3.zero
        };

        Transform target = GetFieldValue(actor.GetComponent<SmartNpcAI>(), "currentTarget") as Transform;
        if (target == null)
        {
            target = GetFieldValue(actor.GetComponent<VillagerAI>(), "currentTarget") as Transform;
        }

        if (target != null)
        {
            intent.hasMoveIntent = true;
            intent.targetName = target.name;
            intent.targetPosition = target.position;
            return intent;
        }

        object hasWander = GetFieldValue(actor.GetComponent<SmartNpcAI>(), "hasWanderTarget");
        object wander = GetFieldValue(actor.GetComponent<SmartNpcAI>(), "wanderTarget");
        if (!(hasWander is bool))
        {
            hasWander = GetFieldValue(actor.GetComponent<VillagerAI>(), "hasWanderTarget");
            wander = GetFieldValue(actor.GetComponent<VillagerAI>(), "wanderTarget");
        }

        if (hasWander is bool hasWanderTarget &&
            hasWanderTarget &&
            wander is Vector3 wanderTarget)
        {
            intent.hasMoveIntent = true;
            intent.targetName = "wander";
            intent.targetPosition = wanderTarget;
        }

        return intent;
    }

    static bool LooksLikeMoveAction(string action, string actionKey)
    {
        string key = (actionKey ?? string.Empty).ToLowerInvariant();
        string text = (action ?? string.Empty).ToLowerInvariant();
        string value = key + " " + text;
        return key.StartsWith("go", StringComparison.Ordinal) ||
            key.StartsWith("move", StringComparison.Ordinal) ||
            key.StartsWith("walk", StringComparison.Ordinal) ||
            value.Contains("move") ||
            value.Contains("walk") ||
            value.Contains("bring") ||
            value.Contains("seek") ||
            value.Contains("hunt") ||
            value.Contains("gather") ||
            value.Contains("taskprovider") ||
            value.Contains("market") ||
            value.Contains("trade") ||
            value.Contains("home") ||
            value.Contains("flee") ||
            value.Contains("patrol") ||
            value.Contains("route") ||
            value.Contains("travel") ||
            value.Contains("đi ") ||
            value.StartsWith("đi ", StringComparison.Ordinal) ||
            value.Contains("dang di") ||
            value.Contains("đang đi") ||
            value.Contains("truy đuổi");
    }

    static bool IsObstacleOverlap(GameObject actor, Vector3 position)
    {
        if (actor == null)
        {
            return false;
        }

        LayerMask mask = ~0;
        SmartNpcAI smart = actor.GetComponent<SmartNpcAI>();
        if (smart != null)
        {
            mask = smart.obstacleLayers;
        }
        else
        {
            VillagerAI villager = actor.GetComponent<VillagerAI>();
            if (villager != null)
            {
                mask = villager.obstacleLayers;
            }
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(position, 0.12f, mask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit != null &&
                !hit.transform.IsChildOf(actor.transform) &&
                !hit.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    static void AddSeconds(Dictionary<string, float> map, string key, float seconds)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            key = "None";
        }

        if (!map.ContainsKey(key))
        {
            map.Add(key, 0f);
        }

        map[key] += seconds;
    }

    static string SafeBucket(string actionKey, string action)
    {
        return !string.IsNullOrWhiteSpace(actionKey) ? actionKey :
            !string.IsNullOrWhiteSpace(action) ? action : "None";
    }

    static object GetFieldValue(Component component, string fieldName)
    {
        if (component == null)
        {
            return null;
        }

        FieldInfo field = component.GetType().GetField(fieldName, InstanceFlags);
        return field != null ? field.GetValue(component) : null;
    }

    static void SetFieldValue(Component component, string fieldName, object value)
    {
        if (component == null)
        {
            return;
        }

        FieldInfo field = component.GetType().GetField(fieldName, InstanceFlags);
        if (field != null)
        {
            field.SetValue(component, value);
        }
    }

    static string Quantize(Vector3 position, float cellSize)
    {
        float size = Mathf.Max(0.1f, cellSize);
        int x = Mathf.RoundToInt(position.x / size);
        int y = Mathf.RoundToInt(position.y / size);
        return "cell=" + x + ":" + y;
    }

    static string FormatVector(Vector3 value)
    {
        return "(" +
            JsonNumber(value.x) + "," +
            JsonNumber(value.y) + "," +
            JsonNumber(value.z) + ")";
    }

    static string JsonNumber(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    static string Csv(string value)
    {
        if (value == null)
        {
            value = string.Empty;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static void Report(string line)
    {
        string formatted =
            "[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + line;
        File.AppendAllText(progressPath, formatted + Environment.NewLine, Encoding.UTF8);
        Debug.Log("[NpcLongAudit] " + line);
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
        RestoreTimeSettings();
        SmartNpcAI.SuppressRuntimeDebugOutput = false;

        SessionState.EraseBool(SessionActiveKey);
        SessionState.EraseInt(SessionExitCodeKey);
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
        if (worldTime != null)
        {
            worldTime.realSecondsPerGameDay = originalRealSecondsPerGameDay;
            worldTime.autoSaveWorldTime = originalAutoSaveWorldTime;
            worldTime.loadSavedTimeOnAwake = originalLoadSavedTimeOnAwake;
        }

        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
    }
}
