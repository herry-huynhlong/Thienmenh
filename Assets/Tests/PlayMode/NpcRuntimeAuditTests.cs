using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class NpcRuntimeAuditTests
{
    const string ScenePath = "Assets/Lang.unity";
    const float WarmupSeconds = 5f;
    const float QuickWarmupSeconds = 1.5f;
    const float AuditSeconds = 45f;
    const float ScheduleAuditGameHours = 24f;
    const float ScheduleAuditRealSecondsPerGameDay = 24f;
    const float ScheduleAuditTimeScale = 20f;
    const float ScheduleAuditSampleInterval = 0.25f;
    const float SampleInterval = 0.5f;
    const float StationarySampleDistance = 0.035f;
    const float MovingIntentStuckSeconds = 6f;
    const float SuspiciousStationarySeconds = 12f;
    const float OutsideAreaSeconds = 3f;
    const float NpcOverlapSeconds = 4f;

    static readonly string[] MovingActionKeys =
    {
        "wanderVillage",
        "returnTerritory",
        "goHomeCultivate",
        "goTavern",
        "huntMonster",
        "huntMonsterNamed",
        "attackMonsterNamed",
        "treasureHunt",
        "treasureHuntNamed",
        "goStoreBuyItem",
        "goTaskProviderDaily",
        "goVanBaoLauBroker",
        "goVanBaoLauTask",
        "goHomeRest",
        "goPlay",
        "goMarketTrade",
        "bringGoodsToCounter",
        "walkingRoad",
        "avoidObstacle",
        "goFarmWork",
        "goWork",
        "goPatrol",
        "goHeal",
        "goFish",
        "goHunt",
        "goMealPoint",
        "goTavernMealPoint",
        "goCounterTrade",
        "goWorkTask",
        "goGatherItem",
        "searchGatherItem",
        "huntSearch",
        "huntFight",
        "returnProviderReceiveTask",
        "returnTurnInTask",
        "followTaskRoute",
        "goGatherNamed",
        "huntForestResource",
        "gatherVillageResource",
        "gatherHerbsAroundForest",
        "trapForestRabbit"
    };

    static readonly string[] LegitimateStationaryActionKeys =
    {
        "cultivate",
        "cultivateAbsorbQi",
        "breakthrough",
        "breakthroughTo",
        "waitTribulation",
        "rest",
        "sleep",
        "eating",
        "eatAtShop",
        "eatingAtTavern",
        "trading",
        "tradeSeek",
        "buyPill",
        "makeFriend",
        "talking",
        "playWithFriends",
        "taskWorking",
        "working",
        "workingFarm",
        "workingTask",
        "receiveTask",
        "viewTaskBoard",
        "chooseTask",
        "turnInTask",
        "gatheringItem",
        "pickHuntEvidence",
        "pickItem",
        "waitHuntRespawn",
        "taskCompleted",
        "pausedTask",
        "checkingCounterTrade",
        "patrolling",
        "healing",
        "fishing",
        "hunting",
        "harvestResource",
        "paidWork",
        "harvestItemAmount",
        "farmerWaitHarvest",
        "farmerHarvestedToday",
        "noTrade",
        "injured",
        "checkedVanBaoLau",
        "visitedTaskProvider",
        "storeMissingItem",
        "skipUnavailableTask",
        "soldGoods",
        "waitTraderBuyGoods",
        "waitLightning",
        "waitLightningNamed",
        "idle",
        "restNearHome",
        "restVillageNoon",
        "stayNearHome",
        "dead",
        "oldAgeDeath"
    };

    static readonly string[] CultivationActionKeys =
    {
        "cultivate",
        "cultivateAbsorbQi",
        "goHomeCultivate",
        "breakthrough",
        "breakthroughTo",
        "waitTribulation"
    };

    static readonly Type VillagerType = GetGameType("VillagerAI");
    static readonly Type SmartNpcType = GetGameType("SmartNpcAI");
    static readonly Type NpcMapMoverType = GetGameType("NpcMapMover2D");
    static readonly Type NpcMapAreaType = GetGameType("NpcMapArea");
    static readonly Type NpcLocationAreaType = GetGameType("NpcLocationArea");
    static readonly Type NpcCounterBrokerType = GetGameType("NpcCounterBroker");
    static readonly Type NpcTeleportGateType = GetGameType("NpcTeleportGate");
    static readonly Type NpcTextType = GetGameType("NpcText");
    static readonly Type NpcScheduleControllerType = GetGameType("NpcScheduleController");
    static readonly Type WorldTimeSystemType = GetGameType("WorldTimeSystem");
    static readonly Type NpcTaskProviderType = GetGameType("NpcTaskProvider");
    static readonly Type NpcTaskOfferType = GetGameType("NpcTaskOffer");

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator AuditLangSceneNpcMovementAndCultivation()
    {
        float originalTimeScale = Time.timeScale;
        float originalFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = 5f;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        try
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            EnsureAudioListener();
            yield return new WaitForSeconds(WarmupSeconds);

            List<ActorState> actors = CreateActorStates();
            Assert.Greater(actors.Count, 0, "No SmartNpcAI or VillagerAI actors were found in " + ScenePath);

            List<string> issues = new List<string>();
            List<PairOverlapState> pairOverlaps = new List<PairOverlapState>();
            float elapsed = 0f;

            while (elapsed < AuditSeconds)
            {
                yield return new WaitForSeconds(SampleInterval);
                elapsed += SampleInterval;

                RefreshActorStates(actors);
                SampleActors(actors, issues, elapsed);
                SampleNpcPairOverlaps(actors, pairOverlaps, issues, elapsed);
            }

            string reportPath = WriteReport(actors, pairOverlaps, issues);
            Debug.Log("NPC_RUNTIME_AUDIT_REPORT: " + reportPath);
            Debug.Log(BuildSummary(actors, pairOverlaps, issues));

            Assert.That(
                issues,
                Is.Empty,
                "NPC runtime audit found issues. See " + reportPath + Environment.NewLine +
                string.Join(Environment.NewLine, issues));
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
        }
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator AuditOneDayScheduleSamples()
    {
        float originalTimeScale = Time.timeScale;
        float originalFixedDeltaTime = Time.fixedDeltaTime;
        object timeSystem = null;
        float originalRealSecondsPerGameDay = 0f;
        float startWorldHour = 0f;

        LogAssert.ignoreFailingMessages = true;
        Time.timeScale = ScheduleAuditTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        try
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            EnsureAudioListener();
            yield return new WaitForSeconds(1f);

            timeSystem = EnsureWorldTimeSystem();
            originalRealSecondsPerGameDay = GetFloatMember(timeSystem, "realSecondsPerGameDay", 900f);
            SetFloatMember(timeSystem, "realSecondsPerGameDay", ScheduleAuditRealSecondsPerGameDay);
            startWorldHour = GetFloatMember(timeSystem, "CurrentWorldHour", 0f);

            List<ActorState> actors = CreateActorStates();
            List<ActorState> samples = PickScheduleSamples(actors);
            Assert.AreEqual(
                4,
                samples.Count,
                "Need one Farmer, one Hunter, one Fisher, and one SmartNpcAI in " + ScenePath);

            List<string> issues = new List<string>();
            Dictionary<int, ScheduleSampleState> states = new Dictionary<int, ScheduleSampleState>();
            for (int i = 0; i < samples.Count; i++)
            {
                states[samples[i].InstanceId] = new ScheduleSampleState(samples[i]);
            }

            while (GetFloatMember(timeSystem, "CurrentWorldHour", startWorldHour) - startWorldHour <
                ScheduleAuditGameHours)
            {
                yield return new WaitForSeconds(ScheduleAuditSampleInterval);

                float currentWorldHour = GetFloatMember(timeSystem, "CurrentWorldHour", startWorldHour);
                for (int i = 0; i < samples.Count; i++)
                {
                    ActorState actor = samples[i];
                    if (actor.GameObject == null || !actor.GameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    ScheduleSampleState state = states[actor.InstanceId];
                    SampleScheduleActor(actor, state, issues, currentWorldHour);
                }
            }

            string reportPath = WriteScheduleReport(samples, states, issues, startWorldHour);
            Debug.Log("NPC_SCHEDULE_24H_AUDIT_REPORT: " + reportPath);
            Debug.Log(BuildScheduleSummary(samples, states, issues));

            Assert.That(
                issues,
                Is.Empty,
                "NPC 24h schedule audit found issues. See " + reportPath + Environment.NewLine +
                string.Join(Environment.NewLine, issues));
        }
        finally
        {
            if (timeSystem != null)
            {
                SetFloatMember(timeSystem, "realSecondsPerGameDay", originalRealSecondsPerGameDay);
            }

            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
        }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator HybridBrainConflictIsResolvedBothWays()
    {
        yield return LoadSceneAndWarmup(QuickWarmupSeconds);

        Behaviour villager = FindFirstActiveBehaviour(VillagerType);
        Behaviour smart = FindFirstActiveBehaviour(SmartNpcType);
        Assert.NotNull(villager, "No active VillagerAI was found in the loaded scene.");
        Assert.NotNull(smart, "No active SmartNpcAI was found in the loaded scene.");

        Component addedSmart = null;
        Component addedVillager = null;

        try
        {
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "^\\[NPC\\] .* has both SmartNpcAI and VillagerAI\\. SmartNpcAI will stay passive to avoid conflicting NPC logic\\.$"));

            addedSmart = villager.gameObject.AddComponent(SmartNpcType);
            Assert.NotNull(addedSmart, "Failed to add SmartNpcAI to a VillagerAI GameObject.");
            yield return null;

            Assert.IsTrue(
                villager.enabled,
                "VillagerAI should stay enabled when SmartNpcAI is attached to the same GameObject.");
            Assert.IsFalse(
                ((Behaviour)addedSmart).enabled,
                "SmartNpcAI should disable itself when VillagerAI is already active.");

            addedVillager = smart.gameObject.AddComponent(VillagerType);
            Assert.NotNull(addedVillager, "Failed to add VillagerAI to a SmartNpcAI GameObject.");
            yield return null;

            Assert.IsTrue(
                ((Behaviour)addedVillager).enabled,
                "VillagerAI should stay enabled when it is the primary brain on the GameObject.");
            Assert.IsFalse(
                smart.enabled,
                "SmartNpcAI should be disabled once a VillagerAI is active on the same GameObject.");
        }
        finally
        {
            if (addedSmart != null)
            {
                UnityEngine.Object.Destroy(addedSmart);
            }

            if (addedVillager != null)
            {
                UnityEngine.Object.Destroy(addedVillager);
            }
        }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator BusyTaskProviderFreezesActionTimerForBothNpcBrains()
    {
        yield return LoadSceneAndWarmup(QuickWarmupSeconds);

        Behaviour villager = FindFirstActiveBehaviour(VillagerType);
        Behaviour smart = FindFirstActiveBehaviour(SmartNpcType);
        Assert.NotNull(villager, "No active VillagerAI was found in the loaded scene.");
        Assert.NotNull(smart, "No active SmartNpcAI was found in the loaded scene.");

        Dictionary<GameObject, int> busyCounts = GetBusyNpcCounts();
        float villagerTimer = 7.5f;
        float smartTimer = 7.5f;
        SetFieldValue(villager, "actionTimer", villagerTimer);
        SetFieldValue(smart, "actionTimer", smartTimer);

        try
        {
            busyCounts[villager.gameObject] = 1;
            busyCounts[smart.gameObject] = 1;

            InvokePrivateMethod(villager, "Update");
            InvokePrivateMethod(smart, "Update");

            Assert.That(
                GetFloatField(villager, "actionTimer"),
                Is.EqualTo(villagerTimer).Within(0.0001f),
                "VillagerAI actionTimer should not tick down while the NPC is busy with a task provider.");
            Assert.That(
                GetFloatField(smart, "actionTimer"),
                Is.EqualTo(smartTimer).Within(0.0001f),
                "SmartNpcAI actionTimer should not tick down while the NPC is busy with a task provider.");
        }
        finally
        {
            busyCounts.Remove(villager.gameObject);
            busyCounts.Remove(smart.gameObject);
        }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator HungryNpcDeclinesTasksAndTaskVisitCooldownBlocksRepeat()
    {
        yield return LoadSceneAndWarmup(QuickWarmupSeconds);

        Component worldTime = EnsureWorldTimeSystem();
        SetWorldTime(worldTime, 1, 1, 3, 10f);

        Behaviour villager = FindFirstActiveBehaviour(VillagerType);
        Behaviour smart = FindFirstActiveBehaviour(SmartNpcType);
        Behaviour provider = FindFirstActiveBehaviour(NpcTaskProviderType);
        Assert.NotNull(villager, "No active VillagerAI was found in the loaded scene.");
        Assert.NotNull(smart, "No active SmartNpcAI was found in the loaded scene.");
        Assert.NotNull(provider, "No active NpcTaskProvider was found in the loaded scene.");

        SetFieldValue(villager, "hunger", 95f);
        SetFieldValue(villager, "fatigue", 0f);
        SetFieldValue(smart, "hunger", 95f);
        SetFieldValue(smart, "fatigue", 0f);

        object cultivateOffer = CreateTaskOffer("Cultivate");
        bool villagerDeclines =
            InvokePrivateMethod<bool>(
                provider,
                "ShouldDeclineTaskByState",
                villager.gameObject,
                cultivateOffer);
        bool smartDeclines =
            InvokePrivateMethod<bool>(
                provider,
                "ShouldDeclineTaskByState",
                smart.gameObject,
                cultivateOffer);

        Assert.IsTrue(
            villagerDeclines,
            "A hungry VillagerAI should decline task offers instead of being pushed into task logic.");
        Assert.IsTrue(
            smartDeclines,
            "A hungry SmartNpcAI should decline task offers instead of being pushed into task logic.");

        SetFieldValue(smart, "staggerDailyTaskVisits", false);
        SetFieldValue(smart, "lastTaskProviderVisitDay", 3);

        bool visitAllowed = InvokePrivateMethod<bool>(smart, "TryVisitTaskProvider");
        Assert.IsFalse(
            visitAllowed,
            "SmartNpcAI should not revisit a task provider again on the same world day.");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator SmartNpcBuyGoodsUsesFallbackAreaInsteadOfStandingStill()
    {
        yield return LoadSceneAndWarmup(QuickWarmupSeconds);

        Behaviour smart = FindFirstActiveBehaviour(SmartNpcType);
        Assert.NotNull(smart, "No active SmartNpcAI was found in the loaded scene.");
        Assert.NotNull(NpcLocationAreaType, "NpcLocationArea type was not found.");

        Behaviour broker = FindFirstActiveBehaviour(NpcCounterBrokerType);
        bool brokerReceiveAll = false;
        if (broker != null)
        {
            brokerReceiveAll = Convert.ToBoolean(GetFieldValue(broker, "receiveAllNpcRequests"));
            SetFieldValue(broker, "receiveAllNpcRequests", false);
        }

        GameObject fallbackAreaObject = new GameObject("SmartBuyFallbackArea");
        try
        {
            BoxCollider2D collider = fallbackAreaObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            Component fallbackArea = fallbackAreaObject.AddComponent(NpcLocationAreaType);
            SetFieldValue(fallbackArea, "purpose", Enum.Parse(
                NpcLocationAreaType.GetField("purpose").FieldType,
                "Any"));
            SetFieldValue(fallbackArea, "activity", Enum.Parse(
                fallbackArea.GetType().GetField("activity").FieldType,
                "BuyGoods"));
            SetFieldValue(fallbackArea, "matchActivity", false);
            SetFieldValue(fallbackArea, "matchJob", false);
            SetFieldValue(fallbackArea, "matchZone", false);
            SetFieldValue(fallbackArea, "matchDangerTier", false);
            SetFieldValue(fallbackArea, "areaBounds", collider);
            SetFieldValue(fallbackArea, "fallbackSize", new Vector2(4f, 4f));

            yield return null;

            SetFieldValue(smart, "tavernPoint", null);
            SetFieldValue(smart, "money", 100);
            SetFieldValue(smart, "pill", 0);

            bool started = InvokePrivateMethod<bool>(smart, "GoToTavernAndBuyPill");
            Assert.IsTrue(started, "SmartNpcAI should start a buy-travel fallback instead of stopping.");

            bool hasWanderTarget = Convert.ToBoolean(GetFieldValue(smart, "hasWanderTarget"));
            Assert.IsTrue(
                hasWanderTarget,
                "SmartNpcAI should keep a wander target when no tavern point is assigned.");

            Assert.IsNull(
                GetFieldValue(smart, "currentTarget"),
                "Fallback buy travel should not require a direct transform target.");
        }
        finally
        {
            if (broker != null)
            {
                SetFieldValue(broker, "receiveAllNpcRequests", brokerReceiveAll);
            }

            UnityEngine.Object.Destroy(fallbackAreaObject);
        }
    }

    static List<ActorState> CreateActorStates()
    {
        List<ActorState> actors = new List<ActorState>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        AddActorsOfType(actors, seen, SmartNpcType, "SmartNpcAI");
        AddActorsOfType(actors, seen, VillagerType, "VillagerAI");

        return actors;
    }


    static List<ActorState> PickScheduleSamples(List<ActorState> actors)
    {
        ActorState farmer = null;
        ActorState hunter = null;
        ActorState fisher = null;
        ActorState smartNpc = null;

        for (int i = 0; i < actors.Count; i++)
        {
            ActorState actor = actors[i];
            if (actor.GameObject == null)
            {
                continue;
            }

            if (smartNpc == null && GetComponent(actor.GameObject, SmartNpcType) != null)
            {
                smartNpc = actor;
                continue;
            }

            Component villager = GetComponent(actor.GameObject, VillagerType);
            if (villager == null)
            {
                continue;
            }

            string job = Convert.ToString(GetFieldValue(villager, "job"), CultureInfo.InvariantCulture);
            if (farmer == null && string.Equals(job, "Farmer", StringComparison.OrdinalIgnoreCase))
            {
                farmer = actor;
            }
            else if (hunter == null && string.Equals(job, "Hunter", StringComparison.OrdinalIgnoreCase))
            {
                hunter = actor;
            }
            else if (fisher == null && string.Equals(job, "Fisher", StringComparison.OrdinalIgnoreCase))
            {
                fisher = actor;
            }
        }

        List<ActorState> samples = new List<ActorState>();
        if (farmer != null) samples.Add(farmer);
        if (hunter != null) samples.Add(hunter);
        if (fisher != null) samples.Add(fisher);
        if (smartNpc != null) samples.Add(smartNpc);
        return samples;
    }

    static void SampleScheduleActor(
        ActorState actor,
        ScheduleSampleState state,
        List<string> issues,
        float currentWorldHour)
    {
        Vector3 position = actor.GameObject.transform.position;
        float moved = Vector2.Distance(state.LastPosition, position);
        string action = actor.Action;
        string activity = GetCurrentScheduleActivity(actor.GameObject);
        string job = actor.Job;

        state.TotalSamples++;
        state.TotalDistance += moved;
        state.MaxStationarySeconds = Mathf.Max(state.MaxStationarySeconds, state.StationarySeconds);

        if (!string.Equals(activity, state.LastActivity, StringComparison.Ordinal) ||
            !string.Equals(action, state.LastAction, StringComparison.Ordinal))
        {
            state.Transitions.Add(
                currentWorldHour.ToString("0.00", CultureInfo.InvariantCulture) + "h " +
                actor.DisplayName + " job=" + job + " activity=" + activity +
                " action=" + Safe(action) + " pos=" + FormatVector(position));
            state.LastActivity = activity;
            state.LastAction = action;
        }

        if (moved <= StationarySampleDistance)
        {
            state.StationarySeconds += ScheduleAuditSampleInterval;
        }
        else
        {
            state.StationarySeconds = 0f;
        }

        bool badAction = IsActionForbiddenForSchedule(job, activity, action);
        TrackForbiddenScheduleAction(
            issues,
            state,
            actor,
            activity,
            action,
            badAction,
            currentWorldHour);

        bool movingIntent = IsMovingIntentAction(action);
        AddScheduleIssueOnce(
            issues,
            state,
            actor,
            "moving_stuck_" + activity + "_" + action,
            movingIntent && state.StationarySeconds >= MovingIntentStuckSeconds,
            currentWorldHour,
            "moving action stayed still for " + FormatSeconds(state.StationarySeconds) +
            " activity=" + activity + " action=" + Safe(action) + " pos=" + FormatVector(position));

        if (string.Equals(job, "Farmer", StringComparison.OrdinalIgnoreCase))
        {
            AddScheduleIssueOnce(
                issues,
                state,
                actor,
                "farmer_generic_working_farm",
                ContainsActionText(action, "workingFarm") || ContainsIgnoreCase(action, "lam ruong"),
                currentWorldHour,
                "farmer used generic farm action instead of item harvest. action=" + Safe(action));
        }

        if (string.Equals(job, "Hunter", StringComparison.OrdinalIgnoreCase))
        {
            AddScheduleIssueOnce(
                issues,
                state,
                actor,
                "hunter_stationary_hunting",
                ContainsActionText(action, "hunting") && state.StationarySeconds >= MovingIntentStuckSeconds,
                currentWorldHour,
                "hunter is stationary while saying hunting. pos=" + FormatVector(position));
        }

        if (actor.TypeName == "SmartNpcAI")
        {
            AddScheduleIssueOnce(
                issues,
                state,
                actor,
                "smartnpc_cultivation_search_loop",
                ContainsActionText(action, "goCultivatePoint") && state.StationarySeconds >= MovingIntentStuckSeconds,
                currentWorldHour,
                "SmartNpc kept finding cultivation point while stationary. pos=" + FormatVector(position));
        }

        state.LastPosition = position;
    }

    static bool IsActionForbiddenForSchedule(string job, string activity, string action)
    {
        if (string.IsNullOrEmpty(activity) || string.Equals(activity, "None", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ContainsActionText(action, "tradeSeek") ||
            ContainsActionText(action, "goMarketTrade") ||
            ContainsActionText(action, "trading"))
        {
            return activity != "BuyGoods" && activity != "SellGoods" && activity != "TakeTask";
        }

        if (ContainsActionText(action, "goTaskProviderDaily") ||
            ContainsActionText(action, "receiveTask") ||
            ContainsActionText(action, "visitedTaskProvider") ||
            ContainsActionText(action, "viewTaskBoard"))
        {
            return activity != "TakeTask";
        }

        if (ContainsActionText(action, "goHunt") ||
            ContainsActionText(action, "hunting") ||
            ContainsActionText(action, "huntMonsterNamed") ||
            ContainsActionText(action, "attackMonsterNamed"))
        {
            return activity != "Hunt" &&
                !(activity == "Work" && string.Equals(job, "Hunter", StringComparison.OrdinalIgnoreCase));
        }

        if (ContainsActionText(action, "goFish") || ContainsActionText(action, "fishing"))
        {
            return activity != "Work" || !string.Equals(job, "Fisher", StringComparison.OrdinalIgnoreCase);
        }

        if (ContainsActionText(action, "goFarmWork") || ContainsActionText(action, "workingFarm"))
        {
            return true;
        }

        if (ContainsActionText(action, "gatherResource") ||
            ContainsActionText(action, "goGatherNamed") ||
            ContainsActionText(action, "gatherVillageResource"))
        {
            return activity != "Gather" && activity != "Work" && activity != "Hunt";
        }

        if (ContainsActionText(action, "goCultivatePoint") ||
            ContainsActionText(action, "goHomeCultivate") ||
            ContainsActionText(action, "cultivate") ||
            ContainsActionText(action, "cultivateAbsorbQi"))
        {
            return activity != "Cultivate";
        }

        return false;
    }

    static void TrackForbiddenScheduleAction(
        List<string> issues,
        ScheduleSampleState state,
        ActorState actor,
        string activity,
        string action,
        bool badAction,
        float worldHour)
    {
        string key = activity + "|" + action;
        if (badAction && state.LastForbiddenActionKey == key)
        {
            state.ForbiddenActionSamples++;
        }
        else if (badAction)
        {
            state.LastForbiddenActionKey = key;
            state.ForbiddenActionSamples = 1;
        }
        else
        {
            state.LastForbiddenActionKey = string.Empty;
            state.ForbiddenActionSamples = 0;
            return;
        }

        AddScheduleIssueOnce(
            issues,
            state,
            actor,
            "forbidden_action_" + activity + "_" + action,
            state.ForbiddenActionSamples >= 2,
            worldHour,
            "activity=" + activity + " action=" + Safe(action));
    }
    static void AddScheduleIssueOnce(
        List<string> issues,
        ScheduleSampleState state,
        ActorState actor,
        string issueKey,
        bool condition,
        float worldHour,
        string detail)
    {
        if (!condition || state.ReportedIssues.Contains(issueKey))
        {
            return;
        }

        state.ReportedIssues.Add(issueKey);
        issues.Add(
            "[" + worldHour.ToString("0.00", CultureInfo.InvariantCulture) + "h] " +
            issueKey + ": " + actor.DisplayName + " " + detail);
    }

    static bool ContainsActionText(string action, string key)
    {
        return MatchesAnyAction(action, new[] { key }) || ContainsIgnoreCase(action, key);
    }

    static string GetCurrentScheduleActivity(GameObject gameObject)
    {
        Component schedule = GetComponent(gameObject, NpcScheduleControllerType);
        if (schedule == null)
        {
            return "None";
        }

        object value = GetPropertyValue(schedule, "CurrentActivity");
        return value != null ? value.ToString() : "None";
    }

    static Component EnsureWorldTimeSystem()
    {
        if (WorldTimeSystemType == null)
        {
            Assert.Fail("WorldTimeSystem type was not found.");
        }

        Component existing = FindFirstActiveComponent(WorldTimeSystemType);
        if (existing != null)
        {
            return existing;
        }

        GameObject worldTimeObject = new GameObject("NpcRuntimeAuditWorldTime");
        return worldTimeObject.AddComponent(WorldTimeSystemType);
    }

    static object GetPropertyValue(object instance, string propertyName)
    {
        if (instance == null)
        {
            return null;
        }

        PropertyInfo property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property != null ? property.GetValue(instance, null) : null;
    }

    static float GetFloatMember(object instance, string memberName, float fallback)
    {
        object value = GetPropertyValue(instance, memberName);
        if (value == null && instance != null)
        {
            FieldInfo field = instance.GetType().GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            value = field != null ? field.GetValue(instance) : null;
        }

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

    static void SetFloatMember(object instance, string fieldName, float value)
    {
        if (instance == null)
        {
            return;
        }

        FieldInfo field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(instance, value);
        }
    }

    static string WriteScheduleReport(
        List<ActorState> actors,
        Dictionary<int, ScheduleSampleState> states,
        List<string> issues,
        float startWorldHour)
    {
        string reportPath = Path.Combine(Application.dataPath, "..", "NpcSchedule24hAuditReport.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"scene\": " + Json(ScenePath) + ",");
        builder.AppendLine("  \"gameHours\": " + JsonNumber(ScheduleAuditGameHours) + ",");
        builder.AppendLine("  \"startWorldHour\": " + JsonNumber(startWorldHour) + ",");
        builder.AppendLine("  \"issues\": [");
        for (int i = 0; i < issues.Count; i++)
        {
            builder.Append("    ").Append(Json(issues[i]));
            builder.AppendLine(i + 1 < issues.Count ? "," : "");
        }
        builder.AppendLine("  ],");
        builder.AppendLine("  \"samples\": [");
        for (int i = 0; i < actors.Count; i++)
        {
            ActorState actor = actors[i];
            ScheduleSampleState state = states[actor.InstanceId];
            builder.AppendLine("    {");
            builder.AppendLine("      \"name\": " + Json(actor.DisplayName) + ",");
            builder.AppendLine("      \"type\": " + Json(actor.TypeName) + ",");
            builder.AppendLine("      \"job\": " + Json(actor.Job) + ",");
            builder.AppendLine("      \"totalDistance\": " + JsonNumber(state.TotalDistance) + ",");
            builder.AppendLine("      \"maxStationarySeconds\": " + JsonNumber(state.MaxStationarySeconds) + ",");
            builder.AppendLine("      \"transitions\": [");
            for (int t = 0; t < state.Transitions.Count; t++)
            {
                builder.Append("        ").Append(Json(state.Transitions[t]));
                builder.AppendLine(t + 1 < state.Transitions.Count ? "," : "");
            }
            builder.AppendLine("      ]");
            builder.Append("    }");
            builder.AppendLine(i + 1 < actors.Count ? "," : "");
        }
        builder.AppendLine("  ]");
        builder.AppendLine("}");

        File.WriteAllText(reportPath, builder.ToString());
        return reportPath;
    }

    static string BuildScheduleSummary(
        List<ActorState> actors,
        Dictionary<int, ScheduleSampleState> states,
        List<string> issues)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("NPC_SCHEDULE_24H_AUDIT_SUMMARY samples=").Append(actors.Count);
        builder.Append(" issues=").Append(issues.Count);
        for (int i = 0; i < actors.Count; i++)
        {
            ActorState actor = actors[i];
            ScheduleSampleState state = states[actor.InstanceId];
            builder.Append(" | ").Append(actor.Job).Append(":")
                .Append(actor.DisplayName)
                .Append(" distance=").Append(JsonNumber(state.TotalDistance))
                .Append(" transitions=").Append(state.Transitions.Count);
        }
        return builder.ToString();
    }

    static void AddActorsOfType(
        List<ActorState> actors,
        HashSet<GameObject> seen,
        Type type,
        string typeName)
    {
        if (type == null)
        {
            return;
        }

        UnityEngine.Object[] found =
            UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            Component component = found[i] as Component;
            if (component != null &&
                component.gameObject.activeInHierarchy &&
                seen.Add(component.gameObject))
            {
                actors.Add(new ActorState(component.gameObject, typeName));
            }
        }
    }

    static void RefreshActorStates(List<ActorState> actors)
    {
        for (int i = actors.Count - 1; i >= 0; i--)
        {
            if (actors[i].GameObject == null || !actors[i].GameObject.activeInHierarchy)
            {
                actors.RemoveAt(i);
            }
        }
    }

    static void SampleActors(List<ActorState> actors, List<string> issues, float elapsed)
    {
        for (int i = 0; i < actors.Count; i++)
        {
            ActorState actor = actors[i];
            Vector3 position = actor.GameObject.transform.position;
            float moved = Vector2.Distance(actor.LastPosition, position);
            string action = actor.Action;
            bool stationary = moved <= StationarySampleDistance;
            bool movingIntent = IsMovingIntentAction(action);
            bool legitimateStationary = IsLegitimateStationaryAction(action);
            bool cultivating = IsCultivationAction(action);
            long cultivation = actor.Cultivation;

            actor.TotalDistance += moved;
            actor.MaxDistanceFromStart = Mathf.Max(
                actor.MaxDistanceFromStart,
                Vector2.Distance(actor.StartPosition, position));

            if (stationary)
            {
                actor.StationarySeconds += SampleInterval;
            }
            else
            {
                actor.StationarySeconds = 0f;
            }

            if (stationary && movingIntent)
            {
                actor.MovingIntentStationarySeconds += SampleInterval;
            }
            else
            {
                actor.MovingIntentStationarySeconds = 0f;
            }

            if (stationary &&
                !cultivating &&
                !legitimateStationary &&
                cultivation <= actor.LastCultivation)
            {
                actor.NonCultivatingStationarySeconds += SampleInterval;
            }
            else
            {
                actor.NonCultivatingStationarySeconds = 0f;
            }

            if (GetNpcMapAreaCount() > 0 && FindNpcMapArea(position) == null)
            {
                actor.OutsideAreaSeconds += SampleInterval;
            }
            else
            {
                actor.OutsideAreaSeconds = 0f;
            }

            if (HasObstacleOverlap(actor))
            {
                actor.ObstacleOverlapSeconds += SampleInterval;
            }
            else
            {
                actor.ObstacleOverlapSeconds = 0f;
            }

            actor.MaxStationarySeconds = Mathf.Max(actor.MaxStationarySeconds, actor.StationarySeconds);
            actor.MaxMovingIntentStationarySeconds = Mathf.Max(actor.MaxMovingIntentStationarySeconds, actor.MovingIntentStationarySeconds);
            actor.MaxNonCultivatingStationarySeconds = Mathf.Max(actor.MaxNonCultivatingStationarySeconds, actor.NonCultivatingStationarySeconds);
            actor.MaxOutsideAreaSeconds = Mathf.Max(actor.MaxOutsideAreaSeconds, actor.OutsideAreaSeconds);
            actor.MaxObstacleOverlapSeconds = Mathf.Max(actor.MaxObstacleOverlapSeconds, actor.ObstacleOverlapSeconds);

            AddIssueOnce(
                issues,
                actor,
                "moving_intent_stationary",
                actor.MovingIntentStationarySeconds >= MovingIntentStuckSeconds,
                elapsed,
                "Actor is trying to move but barely changed position for " +
                FormatSeconds(actor.MovingIntentStationarySeconds) +
                ". action=" + Safe(action) + " pos=" + FormatVector(position));

            AddIssueOnce(
                issues,
                actor,
                "stationary_without_cultivation",
                actor.NonCultivatingStationarySeconds >= SuspiciousStationarySeconds,
                elapsed,
                "Actor stayed in place without cultivation progress for " +
                FormatSeconds(actor.NonCultivatingStationarySeconds) +
                ". action=" + Safe(action) + " pos=" + FormatVector(position));

            AddIssueOnce(
                issues,
                actor,
                "outside_npc_map_area",
                actor.OutsideAreaSeconds >= OutsideAreaSeconds,
                elapsed,
                "Actor stayed outside all NpcMapArea bounds for " +
                FormatSeconds(actor.OutsideAreaSeconds) +
                ". action=" + Safe(action) + " pos=" + FormatVector(position));

            AddIssueOnce(
                issues,
                actor,
                "obstacle_overlap",
                actor.ObstacleOverlapSeconds >= SampleInterval,
                elapsed,
                "Actor overlaps a non-trigger non-NPC obstacle collider. action=" +
                Safe(action) + " pos=" + FormatVector(position));

            actor.LastPosition = position;
            actor.LastCultivation = cultivation;
            actor.LastAction = action;
        }
    }

    static void SampleNpcPairOverlaps(
        List<ActorState> actors,
        List<PairOverlapState> pairOverlaps,
        List<string> issues,
        float elapsed)
    {
        Dictionary<GameObject, ActorState> actorByGameObject =
            new Dictionary<GameObject, ActorState>();
        for (int i = 0; i < actors.Count; i++)
        {
            if (actors[i].GameObject != null &&
                !actorByGameObject.ContainsKey(actors[i].GameObject))
            {
                actorByGameObject.Add(actors[i].GameObject, actors[i]);
            }
        }

        Dictionary<string, PairOverlapState> existing = new Dictionary<string, PairOverlapState>();
        for (int i = 0; i < pairOverlaps.Count; i++)
        {
            existing[pairOverlaps[i].Key] = pairOverlaps[i];
        }

        HashSet<string> touched = new HashSet<string>();
        for (int i = 0; i < actors.Count; i++)
        {
            ActorState a = actors[i];
            if (a.GameObject == null)
            {
                continue;
            }

            Collider2D[] nearby = Physics2D.OverlapCircleAll(
                a.GameObject.transform.position,
                0.45f);

            for (int j = 0; j < nearby.Length; j++)
            {
                GameObject otherObject = GetNpcActorGameObject(nearby[j]);
                if (otherObject == null || otherObject == a.GameObject)
                {
                    continue;
                }

                ActorState b;
                if (!actorByGameObject.TryGetValue(otherObject, out b))
                {
                    continue;
                }

                if (a.InstanceId >= b.InstanceId ||
                    !ActorsOverlap(a, b))
                {
                    continue;
                }

                string key = a.InstanceId < b.InstanceId
                    ? a.InstanceId + ":" + b.InstanceId
                    : b.InstanceId + ":" + a.InstanceId;

                touched.Add(key);
                PairOverlapState state;
                if (!existing.TryGetValue(key, out state))
                {
                    state = new PairOverlapState(key, a.DisplayName, b.DisplayName);
                    pairOverlaps.Add(state);
                    existing[key] = state;
                }

                state.CurrentSeconds += SampleInterval;
                state.MaxSeconds = Mathf.Max(state.MaxSeconds, state.CurrentSeconds);

                if (!state.Reported && state.CurrentSeconds >= NpcOverlapSeconds)
                {
                    state.Reported = true;
                    issues.Add(
                        "[" + FormatSeconds(elapsed) + "] npc_overlap: " +
                        state.ActorA + " and " + state.ActorB +
                        " overlapped for " + FormatSeconds(state.CurrentSeconds));
                }
            }
        }

        for (int i = 0; i < pairOverlaps.Count; i++)
        {
            if (!touched.Contains(pairOverlaps[i].Key))
            {
                pairOverlaps[i].CurrentSeconds = 0f;
            }
        }
    }

    static void EnsureAudioListener()
    {
        if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null)
        {
            return;
        }

        GameObject listener = new GameObject("NpcRuntimeAuditAudioListener");
        listener.AddComponent<AudioListener>();
    }

    static bool HasObstacleOverlap(ActorState actor)
    {
        if (actor.Colliders.Count == 0)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        Collider2D[] hits = new Collider2D[32];

        for (int i = 0; i < actor.Colliders.Count; i++)
        {
            Collider2D own = actor.Colliders[i];
            if (own == null || own.isTrigger || !own.enabled)
            {
                continue;
            }

            int count = Physics2D.OverlapCollider(own, filter, hits);
            for (int h = 0; h < count; h++)
            {
                Collider2D hit = hits[h];
                if (hit == null ||
                    hit == own ||
                    hit.isTrigger ||
                    hit.transform.IsChildOf(actor.GameObject.transform) ||
                    IsNpcCollider(hit))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    static bool ActorsOverlap(ActorState a, ActorState b)
    {
        if (a.GameObject == null || b.GameObject == null)
        {
            return false;
        }

        if (Vector2.Distance(a.GameObject.transform.position, b.GameObject.transform.position) > 1.5f)
        {
            return false;
        }

        for (int i = 0; i < a.Colliders.Count; i++)
        {
            Collider2D ca = a.Colliders[i];
            if (ca == null || ca.isTrigger || !ca.enabled)
            {
                continue;
            }

            for (int j = 0; j < b.Colliders.Count; j++)
            {
                Collider2D cb = b.Colliders[j];
                if (cb == null || cb.isTrigger || !cb.enabled)
                {
                    continue;
                }

                if (ca == cb)
                {
                    continue;
                }

                ColliderDistance2D distance = ca.Distance(cb);
                if (distance.isOverlapped)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool IsNpcCollider(Collider2D collider)
    {
        return GetComponentInParent(collider, VillagerType) != null ||
            GetComponentInParent(collider, SmartNpcType) != null ||
            GetComponentInParent(collider, NpcMapMoverType) != null;
    }

    static GameObject GetNpcActorGameObject(Collider2D collider)
    {
        Component actor =
            GetComponentInParent(collider, VillagerType) ??
            GetComponentInParent(collider, SmartNpcType) ??
            GetComponentInParent(collider, NpcMapMoverType);

        return actor != null ? actor.gameObject : null;
    }

    static void AddIssueOnce(
        List<string> issues,
        ActorState actor,
        string issueKey,
        bool condition,
        float elapsed,
        string detail)
    {
        if (!condition || actor.ReportedIssues.Contains(issueKey))
        {
            return;
        }

        actor.ReportedIssues.Add(issueKey);
        issues.Add("[" + FormatSeconds(elapsed) + "] " + issueKey + ": " + actor.DisplayName + " " + detail);
    }

    static bool IsMovingIntentAction(string action)
    {
        return MatchesAnyAction(action, MovingActionKeys) ||
            ContainsIgnoreCase(action, "gate") ||
            ContainsIgnoreCase(action, "cổng") ||
            ContainsIgnoreCase(action, "cong") ||
            ContainsIgnoreCase(action, "dịch chuyển") ||
            ContainsIgnoreCase(action, "dich chuyen");
    }

    static bool IsLegitimateStationaryAction(string action)
    {
        return MatchesAnyAction(action, LegitimateStationaryActionKeys) ||
            ContainsIgnoreCase(action, "waitSchedule");
    }

    static bool IsCultivationAction(string action)
    {
        return MatchesAnyAction(action, CultivationActionKeys);
    }

    static bool MatchesAnyAction(string action, string[] keys)
    {
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            string template = GetNpcActionText(keys[i]);
            if (string.IsNullOrEmpty(template))
            {
                continue;
            }

            if (string.Equals(action, template, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int placeholder = template.IndexOf('{');
            if (placeholder > 0)
            {
                string prefix = template.Substring(0, placeholder);
                if (action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source) &&
            source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static Type GetGameType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        if (type != null)
        {
            return type;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            type = assemblies[i].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    static Component GetComponent(GameObject gameObject, Type type)
    {
        return gameObject != null && type != null
            ? gameObject.GetComponent(type)
            : null;
    }

    static Component GetComponentInParent(Component component, Type type)
    {
        return component != null && type != null
            ? component.GetComponentInParent(type)
            : null;
    }

    static string GetStringField(Component component, string fieldName)
    {
        object value = GetFieldValue(component, fieldName);
        return value != null ? value.ToString() : string.Empty;
    }

    static long GetLongField(Component component, string fieldName)
    {
        object value = GetFieldValue(component, fieldName);
        if (value == null)
        {
            return 0L;
        }

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (InvalidCastException)
        {
            return 0L;
        }
        catch (FormatException)
        {
            return 0L;
        }
        catch (OverflowException)
        {
            return 0L;
        }
    }

    static float GetFloatField(Component component, string fieldName)
    {
        object value = GetFieldValue(component, fieldName);
        if (value == null)
        {
            return 0f;
        }

        try
        {
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        catch (InvalidCastException)
        {
            return 0f;
        }
        catch (FormatException)
        {
            return 0f;
        }
        catch (OverflowException)
        {
            return 0f;
        }
    }

    static object GetFieldValue(Component component, string fieldName)
    {
        if (component == null || string.IsNullOrEmpty(fieldName))
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
        if (component == null || string.IsNullOrEmpty(fieldName))
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

    static Behaviour FindFirstActiveBehaviour(Type type)
    {
        Component component = FindFirstActiveComponent(type);
        return component as Behaviour;
    }

    static Component FindFirstActiveComponent(Type type)
    {
        if (type == null)
        {
            return null;
        }

        UnityEngine.Object[] found =
            UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            Component component = found[i] as Component;
            if (component != null &&
                component.gameObject.activeInHierarchy &&
                (!(component is Behaviour) || ((Behaviour)component).enabled))
            {
                return component;
            }
        }

        return null;
    }

    static Dictionary<GameObject, int> GetBusyNpcCounts()
    {
        if (NpcTaskProviderType == null)
        {
            Assert.Fail("NpcTaskProvider type was not found.");
        }

        FieldInfo field = NpcTaskProviderType.GetField(
            "busyNpcCounts",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field, "NpcTaskProvider.busyNpcCounts was not found.");

        object value = field.GetValue(null);
        Assert.IsInstanceOf<Dictionary<GameObject, int>>(
            value,
            "NpcTaskProvider.busyNpcCounts has an unexpected type.");
        return (Dictionary<GameObject, int>)value;
    }

    static object CreateTaskOffer(string taskTypeName)
    {
        if (NpcTaskOfferType == null)
        {
            Assert.Fail("NpcTaskOffer type was not found.");
        }

        object offer = Activator.CreateInstance(NpcTaskOfferType);
        Assert.NotNull(offer, "Failed to create NpcTaskOffer instance.");

        FieldInfo taskTypeField = NpcTaskOfferType.GetField(
            "taskType",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(taskTypeField, "NpcTaskOffer.taskType field was not found.");

        object enumValue = Enum.Parse(taskTypeField.FieldType, taskTypeName);
        taskTypeField.SetValue(offer, enumValue);
        return offer;
    }

    static void SetWorldTime(
        Component worldTime,
        int year,
        int month,
        int day,
        float hour)
    {
        Assert.NotNull(worldTime, "WorldTimeSystem component is required for this test.");

        MethodInfo setTime = worldTime.GetType().GetMethod(
            "SetTime",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(int), typeof(int), typeof(int), typeof(float), typeof(bool) },
            null);

        Assert.NotNull(setTime, "WorldTimeSystem.SetTime was not found.");
        setTime.Invoke(worldTime, new object[] { year, month, day, hour, false });
    }

    static IEnumerator LoadSceneAndWarmup(float warmupSeconds)
    {
        yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
        EnsureAudioListener();
        yield return new WaitForSeconds(warmupSeconds);
    }

    static T InvokePrivateMethod<T>(
        object target,
        string methodName,
        params object[] args)
    {
        object result = InvokePrivateMethod(target, methodName, args);
        if (result == null)
        {
            return default;
        }

        return (T)result;
    }

    static object InvokePrivateMethod(
        object target,
        string methodName,
        params object[] args)
    {
        Assert.NotNull(target, "Target object is required.");

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(method, target.GetType().Name + "." + methodName + " was not found.");

        return method.Invoke(target, args);
    }

    static string GetNpcActionText(string key)
    {
        if (NpcTextType == null)
        {
            return key;
        }

        MethodInfo action = NpcTextType.GetMethod(
            "Action",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(string) },
            null);

        if (action == null)
        {
            return key;
        }

        object value = action.Invoke(null, new object[] { key });
        string actionValue = value != null ? value.ToString() : key;
        if (!string.Equals(actionValue, key, StringComparison.OrdinalIgnoreCase))
        {
            return actionValue;
        }

        MethodInfo get = NpcTextType.GetMethod(
            "Get",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(string), typeof(string), typeof(string) },
            null);

        if (get == null)
        {
            return actionValue;
        }

        object taskValue = get.Invoke(null, new object[] { "taskActions", key, key });
        return taskValue != null ? taskValue.ToString() : actionValue;
    }

    static int GetNpcMapAreaCount()
    {
        return GetStaticCollectionCount(NpcMapAreaType, "Areas");
    }

    static int GetNpcTeleportGateCount()
    {
        return GetStaticCollectionCount(NpcTeleportGateType, "Gates");
    }

    static int GetStaticCollectionCount(Type type, string propertyName)
    {
        if (type == null)
        {
            return 0;
        }

        PropertyInfo property = type.GetProperty(
            propertyName,
            BindingFlags.Static | BindingFlags.Public);

        object value = property != null ? property.GetValue(null, null) : null;
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

    static object FindNpcMapArea(Vector3 position)
    {
        if (NpcMapAreaType == null)
        {
            return null;
        }

        MethodInfo findArea = NpcMapAreaType.GetMethod(
            "FindArea",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(Vector3) },
            null);

        return findArea != null
            ? findArea.Invoke(null, new object[] { position })
            : null;
    }

    static string WriteReport(
        List<ActorState> actors,
        List<PairOverlapState> pairOverlaps,
        List<string> issues)
    {
        string reportPath = Path.Combine(Application.dataPath, "..", "NpcRuntimeAuditReport.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"scene\": " + Json(ScenePath) + ",");
        builder.AppendLine("  \"auditSeconds\": " + JsonNumber(AuditSeconds) + ",");
        builder.AppendLine("  \"actorCount\": " + actors.Count + ",");
        builder.AppendLine("  \"mapAreaCount\": " + GetNpcMapAreaCount() + ",");
        builder.AppendLine("  \"teleportGateCount\": " + GetNpcTeleportGateCount() + ",");
        builder.AppendLine("  \"issues\": [");
        for (int i = 0; i < issues.Count; i++)
        {
            builder.Append("    ").Append(Json(issues[i]));
            builder.AppendLine(i + 1 < issues.Count ? "," : "");
        }
        builder.AppendLine("  ],");
        builder.AppendLine("  \"actors\": [");
        for (int i = 0; i < actors.Count; i++)
        {
            ActorState actor = actors[i];
            builder.AppendLine("    {");
            builder.AppendLine("      \"name\": " + Json(actor.DisplayName) + ",");
            builder.AppendLine("      \"type\": " + Json(actor.TypeName) + ",");
            builder.AppendLine("      \"lastAction\": " + Json(actor.LastAction) + ",");
            builder.AppendLine("      \"position\": " + Json(FormatVector(actor.GameObject.transform.position)) + ",");
            builder.AppendLine("      \"totalDistance\": " + JsonNumber(actor.TotalDistance) + ",");
            builder.AppendLine("      \"maxDistanceFromStart\": " + JsonNumber(actor.MaxDistanceFromStart) + ",");
            builder.AppendLine("      \"cultivationGain\": " + (actor.Cultivation - actor.StartCultivation) + ",");
            builder.AppendLine("      \"maxStationarySeconds\": " + JsonNumber(actor.MaxStationarySeconds) + ",");
            builder.AppendLine("      \"maxMovingIntentStationarySeconds\": " + JsonNumber(actor.MaxMovingIntentStationarySeconds) + ",");
            builder.AppendLine("      \"maxNonCultivatingStationarySeconds\": " + JsonNumber(actor.MaxNonCultivatingStationarySeconds) + ",");
            builder.AppendLine("      \"maxOutsideAreaSeconds\": " + JsonNumber(actor.MaxOutsideAreaSeconds) + ",");
            builder.AppendLine("      \"maxObstacleOverlapSeconds\": " + JsonNumber(actor.MaxObstacleOverlapSeconds));
            builder.Append("    }");
            builder.AppendLine(i + 1 < actors.Count ? "," : "");
        }
        builder.AppendLine("  ],");
        builder.AppendLine("  \"npcPairOverlaps\": [");
        int writtenPairs = 0;
        for (int i = 0; i < pairOverlaps.Count; i++)
        {
            if (pairOverlaps[i].MaxSeconds < SampleInterval)
            {
                continue;
            }

            if (writtenPairs > 0)
            {
                builder.AppendLine(",");
            }

            PairOverlapState pair = pairOverlaps[i];
            builder.AppendLine("    {");
            builder.AppendLine("      \"actorA\": " + Json(pair.ActorA) + ",");
            builder.AppendLine("      \"actorB\": " + Json(pair.ActorB) + ",");
            builder.AppendLine("      \"maxSeconds\": " + JsonNumber(pair.MaxSeconds));
            builder.Append("    }");
            writtenPairs++;
        }
        builder.AppendLine();
        builder.AppendLine("  ]");
        builder.AppendLine("}");

        File.WriteAllText(reportPath, builder.ToString());
        return reportPath;
    }

    static string BuildSummary(
        List<ActorState> actors,
        List<PairOverlapState> pairOverlaps,
        List<string> issues)
    {
        int cultivating = 0;
        int movedFar = 0;
        int stationaryNoCultivation = 0;
        int movingStuck = 0;
        int obstacleOverlap = 0;
        int outsideArea = 0;

        for (int i = 0; i < actors.Count; i++)
        {
            ActorState actor = actors[i];
            if (actor.Cultivation > actor.StartCultivation)
            {
                cultivating++;
            }

            if (actor.TotalDistance >= 1f)
            {
                movedFar++;
            }

            if (actor.MaxNonCultivatingStationarySeconds >= SuspiciousStationarySeconds)
            {
                stationaryNoCultivation++;
            }

            if (actor.MaxMovingIntentStationarySeconds >= MovingIntentStuckSeconds)
            {
                movingStuck++;
            }

            if (actor.MaxObstacleOverlapSeconds >= SampleInterval)
            {
                obstacleOverlap++;
            }

            if (actor.MaxOutsideAreaSeconds >= OutsideAreaSeconds)
            {
                outsideArea++;
            }
        }

        int longPairOverlaps = 0;
        for (int i = 0; i < pairOverlaps.Count; i++)
        {
            if (pairOverlaps[i].MaxSeconds >= NpcOverlapSeconds)
            {
                longPairOverlaps++;
            }
        }

        return "NPC_RUNTIME_AUDIT_SUMMARY actors=" + actors.Count +
            " movedFar=" + movedFar +
            " cultivationGain=" + cultivating +
            " movingIntentStuck=" + movingStuck +
            " stationaryNoCultivation=" + stationaryNoCultivation +
            " obstacleOverlap=" + obstacleOverlap +
            " outsideArea=" + outsideArea +
            " longNpcOverlaps=" + longPairOverlaps +
            " issues=" + issues.Count;
    }

    static string Json(string value)
    {
        if (value == null)
        {
            return "null";
        }

        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n") + "\"";
    }

    static string JsonNumber(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    static string FormatSeconds(float seconds)
    {
        return seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
    }

    static string FormatVector(Vector3 position)
    {
        return "(" +
            position.x.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            position.y.ToString("0.00", CultureInfo.InvariantCulture) + "," +
            position.z.ToString("0.00", CultureInfo.InvariantCulture) + ")";
    }

    static string Safe(string value)
    {
        return string.IsNullOrEmpty(value) ? "<empty>" : value;
    }

    sealed class ActorState
    {
        public readonly GameObject GameObject;
        public readonly string TypeName;
        public readonly int InstanceId;
        public readonly Vector3 StartPosition;
        public readonly long StartCultivation;
        public readonly List<Collider2D> Colliders;
        public readonly HashSet<string> ReportedIssues = new HashSet<string>();
        public Vector3 LastPosition;
        public long LastCultivation;
        public string LastAction;
        public float StationarySeconds;
        public float MovingIntentStationarySeconds;
        public float NonCultivatingStationarySeconds;
        public float OutsideAreaSeconds;
        public float ObstacleOverlapSeconds;
        public float MaxStationarySeconds;
        public float MaxMovingIntentStationarySeconds;
        public float MaxNonCultivatingStationarySeconds;
        public float MaxOutsideAreaSeconds;
        public float MaxObstacleOverlapSeconds;
        public float TotalDistance;
        public float MaxDistanceFromStart;

        public ActorState(GameObject gameObject, string typeName)
        {
            GameObject = gameObject;
            TypeName = typeName;
            InstanceId = gameObject.GetInstanceID();
            StartPosition = gameObject.transform.position;
            LastPosition = StartPosition;
            StartCultivation = Cultivation;
            LastCultivation = StartCultivation;
            LastAction = Action;
            Colliders = new List<Collider2D>(gameObject.GetComponentsInChildren<Collider2D>());
        }

        public string DisplayName
        {
            get
            {
                if (GameObject == null)
                {
                    return "<destroyed>";
                }

                Component villager = GetComponent(GameObject, VillagerType);
                if (villager != null)
                {
                    string villagerName = GetStringField(villager, "villagerName");
                    if (!string.IsNullOrEmpty(villagerName))
                    {
                        return GameObject.name + "/" + villagerName;
                    }
                }

                Component smartNpc = GetComponent(GameObject, SmartNpcType);
                if (smartNpc != null)
                {
                    string npcName = GetStringField(smartNpc, "npcName");
                    if (!string.IsNullOrEmpty(npcName))
                    {
                        return GameObject.name + "/" + npcName;
                    }
                }

                return GameObject.name;
            }
        }


        public string Job
        {
            get
            {
                if (GameObject == null)
                {
                    return string.Empty;
                }

                Component villager = GetComponent(GameObject, VillagerType);
                if (villager != null)
                {
                    object job = GetFieldValue(villager, "job");
                    return job != null ? job.ToString() : string.Empty;
                }

                return TypeName;
            }
        }
        public string Action
        {
            get
            {
                if (GameObject == null)
                {
                    return string.Empty;
                }

                Component villager = GetComponent(GameObject, VillagerType);
                if (villager != null)
                {
                    return GetStringField(villager, "currentAction");
                }

                Component smartNpc = GetComponent(GameObject, SmartNpcType);
                return smartNpc != null
                    ? GetStringField(smartNpc, "currentAction")
                    : string.Empty;
            }
        }

        public long Cultivation
        {
            get
            {
                if (GameObject == null)
                {
                    return 0L;
                }

                Component villager = GetComponent(GameObject, VillagerType);
                if (villager != null)
                {
                    return GetLongField(villager, "cultivationExp");
                }

                Component smartNpc = GetComponent(GameObject, SmartNpcType);
                return smartNpc != null
                    ? GetLongField(smartNpc, "cultivation")
                    : 0L;
            }
        }
    }


    sealed class ScheduleSampleState
    {
        public readonly ActorState Actor;
        public readonly List<string> Transitions = new List<string>();
        public readonly HashSet<string> ReportedIssues = new HashSet<string>();
        public Vector3 LastPosition;
        public string LastActivity = string.Empty;
        public string LastAction = string.Empty;
        public float StationarySeconds;
        public float MaxStationarySeconds;
        public float TotalDistance;
        public int TotalSamples;
        public string LastForbiddenActionKey = string.Empty;
        public int ForbiddenActionSamples;

        public ScheduleSampleState(ActorState actor)
        {
            Actor = actor;
            LastPosition = actor.GameObject.transform.position;
            LastAction = actor.Action;
            LastActivity = GetCurrentScheduleActivity(actor.GameObject);
        }
    }

    sealed class PairOverlapState
    {
        public readonly string Key;
        public readonly string ActorA;
        public readonly string ActorB;
        public float CurrentSeconds;
        public float MaxSeconds;
        public bool Reported;

        public PairOverlapState(string key, string actorA, string actorB)
        {
            Key = key;
            ActorA = actorA;
            ActorB = actorB;
        }
    }
}
