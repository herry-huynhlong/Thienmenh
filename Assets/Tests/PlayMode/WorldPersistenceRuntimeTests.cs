using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WorldPersistenceRuntimeTests
{
    static readonly string[] PersistentTypeNames =
    {
        "FarmPlot",
        "WeatherAccumulationSystem",
        "WeatherSystem",
        "WorldEventSystem",
        "WorldEventManager"
    };

    [SetUp]
    public void SetUp()
    {
        DestroyPersistentObjects();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyPersistentObjects();
    }

    [Test]
    public void FarmPlot_CaptureAndRestore_PreservesGrowthState()
    {
        Component plot = AddComponent(
            "WorldPersistenceFarmPlot",
            "FarmPlot");
        SetField(plot, "cell", new Vector3Int(4, 7, 0));
        SetField(plot, "growDurationGameHours", 1000f);
        SetEnumField(plot, "state", 1);
        SetField(plot, "plantedWorldHour", GetCurrentWorldHour());
        SetField(plot, "nextPlantAllowedDay", 12);

        object captured = InvokePublic(plot, "CapturePersistentState");
        string firstKey = GetField<string>(captured, "persistentKey");

        InvokePublic(plot, "ForceSetEmpty");
        InvokePublic(plot, "RestorePersistentState", captured);

        Assert.AreEqual(1, Convert.ToInt32(GetField<object>(plot, "state")));
        Assert.AreEqual(
            GetField<float>(captured, "plantedWorldHour"),
            GetField<float>(plot, "plantedWorldHour"));
        Assert.AreEqual(12, GetField<int>(plot, "nextPlantAllowedDay"));
        Assert.AreEqual(
            firstKey,
            (string)InvokePublic(plot, "GetPersistentSaveKey"));
    }

    [Test]
    public void WeatherAndEvent_Restore_PreserveSchedulers()
    {
        Component weather = AddComponent(
            "WorldPersistenceWeather",
            "WeatherSystem");
        object weatherState = Create("WeatherPersistentState");
        SetField(weatherState, "currentWeather", 2);
        SetField(weatherState, "manualOverrideActive", true);
        SetField(weatherState, "nextChangeWorldHour", 123.5f);
        SetField(weatherState, "hasScheduledChange", true);
        InvokePublic(weather, "RestorePersistentState", weatherState);

        object capturedWeather =
            InvokePublic(weather, "CapturePersistentState");
        Assert.AreEqual(2, GetField<int>(capturedWeather, "currentWeather"));
        Assert.IsTrue(
            GetField<bool>(capturedWeather, "manualOverrideActive"));
        Assert.IsTrue(
            GetField<bool>(capturedWeather, "hasScheduledChange"));
        Assert.AreEqual(
            123.5f,
            GetField<float>(capturedWeather, "nextChangeWorldHour"));

        Component events = AddComponent(
            "WorldPersistenceEvents",
            "WorldEventSystem");
        object eventState = Create("WorldEventPersistentState");
        SetField(eventState, "currentEvent", "Festival");
        SetField(eventState, "nextCheckWorldHour", 456.25f);
        InvokePublic(events, "RestorePersistentState", eventState);

        object capturedEvent =
            InvokePublic(events, "CapturePersistentState");
        Assert.AreEqual(
            "Festival",
            GetField<string>(capturedEvent, "currentEvent"));
        Assert.AreEqual(
            456.25f,
            GetField<float>(capturedEvent, "nextCheckWorldHour"));
    }

    [Test]
    public void WeatherAccumulation_Restore_ClampsSavedLevels()
    {
        Component accumulation = AddComponent(
            "WorldPersistenceAccumulation",
            "WeatherAccumulationSystem");
        object state = Create("WeatherAccumulationPersistentState");
        SetField(state, "rainAccumulation", 1.5f);
        SetField(state, "snowAccumulation", -0.5f);

        InvokePublic(accumulation, "RestorePersistentState", state);

        Assert.AreEqual(
            1f,
            GetProperty<float>(accumulation, "RainAccumulation"));
        Assert.AreEqual(
            0f,
            GetProperty<float>(accumulation, "SnowAccumulation"));
    }

    [Test]
    public void WorldLog_EnforcesLimit_AndDeepCopiesRestoredEntries()
    {
        Component manager = AddComponent(
            "WorldPersistenceLogManager",
            "WorldEventManager");
        SetField(manager, "maxLogEntries", 3);
        IList emptyLogs = CreateTypedList("LogEntry");
        InvokePublic(manager, "RestoreLogs", emptyLogs);

        for (int i = 0; i < 5; i++)
        {
            InvokePublic(manager, "AddLog", "log-" + i, 0, false);
        }

        IList capped = (IList)InvokePublic(manager, "CaptureLogs");
        Assert.AreEqual(3, capped.Count);
        Assert.AreEqual("log-4", GetField<string>(capped[0], "content"));
        Assert.AreEqual("log-2", GetField<string>(capped[2], "content"));

        SetField(manager, "maxLogEntries", 2);
        IList restoredSource = CreateTypedList("LogEntry");
        restoredSource.Add(CreateLogEntry("t1", "saved-1", 1, true));
        restoredSource.Add(CreateLogEntry("t2", "saved-2", 2, false));
        restoredSource.Add(CreateLogEntry("t3", "saved-3", 0, false));
        InvokePublic(manager, "RestoreLogs", restoredSource);

        SetField(restoredSource[0], "content", "mutated-after-restore");
        IList restored = (IList)InvokePublic(manager, "CaptureLogs");
        Assert.AreEqual(2, restored.Count);
        Assert.AreEqual("saved-1", GetField<string>(restored[0], "content"));
        Assert.AreEqual("saved-2", GetField<string>(restored[1], "content"));
    }

    [Test]
    public void FullSaveJson_RoundTripsWorldSimulationState()
    {
        object original = Create("FullGameSaveData");
        SetField(original, "hasFarmPlotState", true);
        SetField(original, "hasWorldLogState", true);

        object weatherState = Create("WeatherPersistentState");
        SetField(weatherState, "currentWeather", 3);
        SetField(weatherState, "manualOverrideActive", true);
        SetField(weatherState, "nextChangeWorldHour", 81f);
        SetField(weatherState, "hasScheduledChange", true);
        SetField(original, "weatherState", weatherState);

        object accumulationState =
            Create("WeatherAccumulationPersistentState");
        SetField(accumulationState, "rainAccumulation", 0.25f);
        SetField(accumulationState, "snowAccumulation", 0.75f);
        SetField(original, "weatherAccumulationState", accumulationState);

        object eventState = Create("WorldEventPersistentState");
        SetField(eventState, "currentEvent", "SecretRealmOpen");
        SetField(eventState, "nextCheckWorldHour", 90f);
        SetField(original, "worldEventState", eventState);

        object farmState = Create("FarmPlotPersistentState");
        SetField(farmState, "persistentKey", "scene|field|cell=1,2,0");
        SetField(farmState, "state", 2);
        SetField(farmState, "plantedWorldHour", 40f);
        SetField(farmState, "nextPlantAllowedDay", 3);
        GetField<IList>(original, "farmPlots").Add(farmState);
        GetField<IList>(original, "worldLogs").Add(
            CreateLogEntry("[T1-N2]", "saved log", 1, true));

        string json = JsonUtility.ToJson(original);
        Type saveType = ResolveGameType("FullGameSaveData");
        object restored = JsonUtility.FromJson(json, saveType);

        Assert.AreEqual(4, GetField<int>(restored, "saveVersion"));
        Assert.IsTrue(GetField<bool>(restored, "hasFarmPlotState"));
        IList restoredPlots = GetField<IList>(restored, "farmPlots");
        Assert.AreEqual(1, restoredPlots.Count);
        Assert.AreEqual(2, GetField<int>(restoredPlots[0], "state"));
        Assert.AreEqual(
            3,
            GetField<int>(
                GetField<object>(restored, "weatherState"),
                "currentWeather"));
        Assert.AreEqual(
            0.75f,
            GetField<float>(
                GetField<object>(restored, "weatherAccumulationState"),
                "snowAccumulation"));
        Assert.AreEqual(
            "SecretRealmOpen",
            GetField<string>(
                GetField<object>(restored, "worldEventState"),
                "currentEvent"));
        Assert.IsTrue(GetField<bool>(restored, "hasWorldLogState"));
        Assert.AreEqual(
            "saved log",
            GetField<string>(
                GetField<IList>(restored, "worldLogs")[0],
                "content"));
    }

    static object CreateLogEntry(
        string timestamp,
        string content,
        int colorType,
        bool isStoryLog)
    {
        Type type = ResolveGameType("LogEntry");
        return Activator.CreateInstance(
            type,
            new object[] { timestamp, content, colorType, isStoryLog });
    }

    static IList CreateTypedList(string elementTypeName)
    {
        Type elementType = ResolveGameType(elementTypeName);
        Type listType = typeof(System.Collections.Generic.List<>)
            .MakeGenericType(elementType);
        return (IList)Activator.CreateInstance(listType);
    }

    static Component AddComponent(string objectName, string typeName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, "Missing game type: " + typeName);
        return new GameObject(objectName).AddComponent(type);
    }

    static object Create(string typeName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, "Missing game type: " + typeName);
        return Activator.CreateInstance(type);
    }

    static object InvokePublic(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo[] methods = target.GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.Public);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != methodName ||
                !ParametersMatch(method.GetParameters(), arguments))
            {
                continue;
            }

            return method.Invoke(target, arguments);
        }

        Assert.Fail(target.GetType().Name + "." + methodName + " was not found.");
        return null;
    }

    static bool ParametersMatch(
        ParameterInfo[] parameters,
        object[] arguments)
    {
        if (parameters.Length != arguments.Length)
        {
            return false;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (arguments[i] != null &&
                !parameters[i].ParameterType.IsInstanceOfType(arguments[i]))
            {
                return false;
            }
        }

        return true;
    }

    static float GetCurrentWorldHour()
    {
        Type timeType = ResolveGameType("WorldTimeSystem");
        FieldInfo instanceField = timeType.GetField(
            "Instance",
            BindingFlags.Static | BindingFlags.Public);
        object instance = instanceField != null
            ? instanceField.GetValue(null)
            : null;
        return instance != null
            ? GetProperty<float>(instance, "CurrentWorldHour")
            : Time.time / 10f;
    }

    static void SetEnumField(object target, string fieldName, int value)
    {
        FieldInfo field = FindField(target, fieldName);
        field.SetValue(target, Enum.ToObject(field.FieldType, value));
    }

    static void SetField(object target, string fieldName, object value)
    {
        FindField(target, fieldName).SetValue(target, value);
    }

    static T GetField<T>(object target, string fieldName)
    {
        return (T)FindField(target, fieldName).GetValue(target);
    }

    static FieldInfo FindField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field, target.GetType().Name + "." + fieldName);
        return field;
    }

    static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property, target.GetType().Name + "." + propertyName);
        return (T)property.GetValue(target);
    }

    static void DestroyPersistentObjects()
    {
        for (int i = 0; i < PersistentTypeNames.Length; i++)
        {
            Type type = ResolveGameType(PersistentTypeNames[i]);
            if (type == null)
            {
                continue;
            }

            UnityEngine.Object[] objects =
                UnityEngine.Object.FindObjectsByType(
                    type,
                    FindObjectsInactive.Include);
            for (int j = 0; j < objects.Length; j++)
            {
                Component component = objects[j] as Component;
                if (component != null)
                {
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
                }
            }
        }
    }

    static Type ResolveGameType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
