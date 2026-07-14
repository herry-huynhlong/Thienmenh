using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WorldTimeCatchUpRuntimeTests
{
    readonly List<GameObject> createdObjects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();
        DestroyTimeSimulationObjects();
        createdObjects.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        DestroyTimeSimulationObjects();
        PlayerPrefs.DeleteAll();
    }

    [Test]
    public void SetTime_ForwardWithinDay_ReplaysEverySkippedHour()
    {
        Component timeSystem = CreateWorldTime();
        RestoreTime(timeSystem, 1, 1, 1, 6f, false);
        List<int> hours = new List<int>();
        List<int> days = new List<int>();
        Subscribe(timeSystem, "OnHourChanged", hours.Add);
        Subscribe(timeSystem, "OnDayChanged", days.Add);

        SetTime(timeSystem, 1, 1, 1, 10.5f);

        CollectionAssert.AreEqual(new[] { 7, 8, 9, 10 }, hours);
        Assert.IsEmpty(days);
        Assert.AreEqual(10.5f, GetField<float>(timeSystem, "currentHour"), 0.001f);
    }

    [Test]
    public void SetTime_AcrossYear_ReplaysChronologicallyAtIntermediateDates()
    {
        Component timeSystem = CreateWorldTime();
        RestoreTime(timeSystem, 1, 12, 30, 22f, false);
        List<string> hourSnapshots = new List<string>();
        List<string> daySnapshots = new List<string>();
        Subscribe(
            timeSystem,
            "OnHourChanged",
            hour => hourSnapshots.Add(FormatSnapshot(timeSystem, hour)));
        Subscribe(
            timeSystem,
            "OnDayChanged",
            absoluteDay => daySnapshots.Add(
                absoluteDay + ":" +
                FormatSnapshot(
                    timeSystem,
                    Mathf.FloorToInt(
                        GetField<float>(timeSystem, "currentHour")))));

        SetTime(timeSystem, 2, 1, 1, 2f);

        CollectionAssert.AreEqual(
            new[]
            {
                "1/12/30@23",
                "2/1/1@0",
                "2/1/1@1",
                "2/1/1@2"
            },
            hourSnapshots);
        CollectionAssert.AreEqual(
            new[] { "361:2/1/1@0" },
            daySnapshots);
        Assert.AreEqual(2, GetField<int>(timeSystem, "currentYear"));
        Assert.AreEqual(1, GetField<int>(timeSystem, "currentMonth"));
        Assert.AreEqual(1, GetField<int>(timeSystem, "currentDay"));
        Assert.AreEqual(2f, GetField<float>(timeSystem, "currentHour"), 0.001f);
    }

    [Test]
    public void SetTime_AcrossSeveralDays_ReplaysEveryDayOnce()
    {
        Component timeSystem = CreateWorldTime();
        RestoreTime(timeSystem, 1, 1, 1, 23f, false);
        List<int> hours = new List<int>();
        List<int> days = new List<int>();
        Subscribe(timeSystem, "OnHourChanged", hours.Add);
        Subscribe(timeSystem, "OnDayChanged", days.Add);

        SetTime(timeSystem, 1, 1, 4, 1f);

        Assert.AreEqual(50, hours.Count);
        CollectionAssert.AreEqual(new[] { 2, 3, 4 }, days);
        Assert.AreEqual(4, GetProperty<int>(timeSystem, "CurrentAbsoluteDay"));
    }

    [Test]
    public void SetTime_Backward_NotifiesDestinationWithoutReverseReplay()
    {
        Component timeSystem = CreateWorldTime();
        RestoreTime(timeSystem, 1, 1, 5, 10f, false);
        List<int> hours = new List<int>();
        List<int> days = new List<int>();
        Subscribe(timeSystem, "OnHourChanged", hours.Add);
        Subscribe(timeSystem, "OnDayChanged", days.Add);

        SetTime(timeSystem, 1, 1, 4, 3f);

        CollectionAssert.AreEqual(new[] { 3 }, hours);
        CollectionAssert.AreEqual(new[] { 4 }, days);
    }

    [Test]
    public void RestoreTime_NotifiesOnceWithoutRunningCatchUpSimulation()
    {
        Component timeSystem = CreateWorldTime();
        RestoreTime(timeSystem, 1, 1, 1, 6f, false);
        List<int> hours = new List<int>();
        List<int> days = new List<int>();
        Subscribe(timeSystem, "OnHourChanged", hours.Add);
        Subscribe(timeSystem, "OnDayChanged", days.Add);

        RestoreTime(timeSystem, 10, 1, 1, 5f, true);

        CollectionAssert.AreEqual(new[] { 5 }, hours);
        CollectionAssert.AreEqual(new[] { 3241 }, days);
        Assert.IsNull(GetSingleton("VillagerRelationshipManager"));
        Assert.IsNull(GetSingleton("VillagerBirthManager"));
    }

    Component CreateWorldTime()
    {
        Type type = ResolveGameType("WorldTimeSystem");
        Assert.NotNull(type);
        GameObject gameObject = new GameObject("WorldTimeCatchUpTest");
        createdObjects.Add(gameObject);
        Component timeSystem = gameObject.AddComponent(type);
        SetField(timeSystem, "autoSaveWorldTime", false);
        return timeSystem;
    }

    static void SetTime(
        Component timeSystem,
        int year,
        int month,
        int day,
        float hour)
    {
        InvokePublic(timeSystem, "SetTime", year, month, day, hour, true);
    }

    static void RestoreTime(
        Component timeSystem,
        int year,
        int month,
        int day,
        float hour,
        bool notifyEvents)
    {
        InvokePublic(
            timeSystem,
            "RestoreTime",
            year,
            month,
            day,
            hour,
            notifyEvents);
    }

    static void Subscribe(Component target, string eventName, Action<int> handler)
    {
        EventInfo eventInfo = target.GetType().GetEvent(
            eventName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(eventInfo, target.GetType().Name + "." + eventName);
        eventInfo.AddEventHandler(target, handler);
    }

    static string FormatSnapshot(Component timeSystem, int hour)
    {
        return
            GetField<int>(timeSystem, "currentYear") + "/" +
            GetField<int>(timeSystem, "currentMonth") + "/" +
            GetField<int>(timeSystem, "currentDay") + "@" +
            hour;
    }

    static void DestroyTimeSimulationObjects()
    {
        DestroyAll("WorldTimeSystem");
        DestroyAll("VillagerRelationshipManager");
        DestroyAll("VillagerBirthManager");
        SetSingleton("WorldTimeSystem", null);
        SetSingleton("VillagerRelationshipManager", null);
        SetSingleton("VillagerBirthManager", null);
    }

    static void DestroyAll(string typeName)
    {
        Type type = ResolveGameType(typeName);
        if (type == null)
        {
            return;
        }

        UnityEngine.Object[] instances = UnityEngine.Object.FindObjectsByType(
            type,
            FindObjectsInactive.Include);
        for (int i = 0; i < instances.Length; i++)
        {
            Component component = instances[i] as Component;
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }
    }

    static UnityEngine.Object GetSingleton(string typeName)
    {
        Type type = ResolveGameType(typeName);
        FieldInfo field = type != null
            ? type.GetField(
                "Instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            : null;
        return field != null ? field.GetValue(null) as UnityEngine.Object : null;
    }

    static void SetSingleton(string typeName, UnityEngine.Object value)
    {
        Type type = ResolveGameType(typeName);
        FieldInfo field = type != null
            ? type.GetField(
                "Instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            : null;
        if (field != null)
        {
            field.SetValue(null, value);
        }
    }

    static object InvokePublic(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method, target.GetType().Name + "." + methodName);
        return method.Invoke(target, args);
    }

    static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field, target.GetType().Name + "." + fieldName);
        return (T)field.GetValue(target);
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field, target.GetType().Name + "." + fieldName);
        field.SetValue(target, value);
    }

    static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property, target.GetType().Name + "." + propertyName);
        return (T)property.GetValue(target);
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
