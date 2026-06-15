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
    const float AuditSeconds = 45f;
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
    static readonly Type NpcTeleportGateType = GetGameType("NpcTeleportGate");
    static readonly Type NpcTextType = GetGameType("NpcText");

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

    static List<ActorState> CreateActorStates()
    {
        List<ActorState> actors = new List<ActorState>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        AddActorsOfType(actors, seen, VillagerType, "VillagerAI");
        AddActorsOfType(actors, seen, SmartNpcType, "SmartNpcAI");

        return actors;
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
        return MatchesAnyAction(action, LegitimateStationaryActionKeys);
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
