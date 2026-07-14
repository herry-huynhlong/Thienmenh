using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NpcAgeRuntimeTests
{
    readonly List<GameObject> createdObjects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();
        ClearSingleton("WorldTimeSystem", "Instance");
        ClearSingleton("FullGameSaveController", "instance");
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
        ClearSingleton("WorldTimeSystem", "Instance");
        ClearSingleton("FullGameSaveController", "instance");
        PlayerPrefs.DeleteAll();
    }

    [Test]
    public void LegacySceneNpc_PreservesWorldStartAgeBehavior()
    {
        Component timeSystem = CreateWorldTime();
        SetWorldTime(timeSystem, 10, 6, 15, false);

        Component identity = CreateIdentity("LegacyNpc");
        SetField(identity, "age", 30);
        SetField(identity, "hasBirthAbsoluteDay", false);

        Assert.AreEqual(39, GetCurrentAge(identity));
        Assert.IsTrue(GetField<bool>(identity, "hasBirthAbsoluteDay"));
        Assert.AreEqual(
            1 - 30 * GetDaysPerYear(),
            GetField<int>(identity, "birthAbsoluteDay"));
    }

    [Test]
    public void RuntimeChild_BornInLaterYearStartsAtZero()
    {
        Component timeSystem = CreateWorldTime();
        SetWorldTime(timeSystem, 10, 1, 1, false);

        Component child = CreateIdentity("RuntimeChild");
        SetField(child, "motherId", "mother");
        InvokePublic(child, "SetCurrentAge", 0);

        Assert.AreEqual(0, GetCurrentAge(child));

        SetWorldTime(timeSystem, 10, 12, 30, false);
        Assert.AreEqual(0, GetCurrentAge(child));

        SetWorldTime(timeSystem, 11, 1, 1, false);
        Assert.AreEqual(1, GetCurrentAge(child));
    }

    [Test]
    public void Lifecycle_RefreshesStageWhenWorldDayChanges()
    {
        Component timeSystem = CreateWorldTime();
        SetWorldTime(timeSystem, 10, 1, 1, false);

        Component identity = CreateIdentity("LifecycleNpc");
        InvokePublic(identity, "SetCurrentAge", 3);
        Component lifecycle = identity.gameObject.AddComponent(
            ResolveGameType("NPCLifecycle"));

        Assert.AreEqual(0, GetEnumFieldValue(identity, "lifeStage"));

        SetWorldTime(timeSystem, 11, 1, 1, true);

        Assert.AreEqual(4, GetField<int>(identity, "age"));
        Assert.AreEqual(1, GetEnumFieldValue(identity, "lifeStage"));
        Assert.AreSame(identity, GetField<Component>(lifecycle, "identity"));
    }

    [Test]
    public void FullSaveIdentity_RoundTripsBirthAbsoluteDay()
    {
        Component timeSystem = CreateWorldTime();
        SetWorldTime(timeSystem, 10, 1, 1, false);

        Component controller = CreateComponent(
            "FullGameSaveControllerAgeTest",
            "FullGameSaveController");
        Component source = CreateIdentity("SourceChild");
        SetField(source, "motherId", "mother");
        InvokePublic(source, "SetCurrentAge", 0);

        object saved = Activator.CreateInstance(
            ResolveGameType("SavedNpcStateData"));
        InvokeNonPublic(
            controller,
            "SaveIdentityState",
            saved,
            source);

        Assert.IsTrue(GetField<bool>(saved, "hasBirthAbsoluteDay"));
        Assert.AreEqual(
            GetField<int>(source, "birthAbsoluteDay"),
            GetField<int>(saved, "birthAbsoluteDay"));

        Component restored = CreateIdentity("RestoredChild");
        InvokeNonPublic(
            controller,
            "ApplyIdentityState",
            saved,
            restored);

        Assert.IsTrue(GetField<bool>(restored, "hasBirthAbsoluteDay"));
        Assert.AreEqual(
            GetField<int>(source, "birthAbsoluteDay"),
            GetField<int>(restored, "birthAbsoluteDay"));
        Assert.AreEqual(0, GetCurrentAge(restored));

        SetWorldTime(timeSystem, 11, 1, 1, false);
        Assert.AreEqual(1, GetCurrentAge(restored));
    }

    Component CreateWorldTime()
    {
        Component timeSystem =
            CreateComponent("WorldTimeSystemAgeTest", "WorldTimeSystem");
        SetField(timeSystem, "autoSaveWorldTime", false);
        return timeSystem;
    }

    Component CreateIdentity(string objectName)
    {
        Component identity = CreateComponent(objectName, "NPCIdentity");
        SetField(identity, "npcName", objectName);
        return identity;
    }

    Component CreateComponent(string objectName, string typeName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, "Failed to resolve " + typeName + ".");

        GameObject gameObject = new GameObject(objectName);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent(type);
    }

    static void SetWorldTime(
        Component timeSystem,
        int year,
        int month,
        int day,
        bool triggerEvents)
    {
        InvokePublic(
            timeSystem,
            "SetTime",
            year,
            month,
            day,
            6f,
            triggerEvents);
    }

    static int GetCurrentAge(Component identity)
    {
        return (int)InvokePublic(identity, "GetCurrentAge");
    }

    static int GetDaysPerYear()
    {
        Type utilityType = ResolveGameType("NpcAgeUtility");
        FieldInfo field = utilityType.GetField(
            "DaysPerYear",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(field);
        return (int)field.GetRawConstantValue();
    }

    static int GetEnumFieldValue(object target, string fieldName)
    {
        object value = GetField<object>(target, fieldName);
        return Convert.ToInt32(value);
    }

    static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(
            field,
            target.GetType().Name + "." + fieldName + " was not found.");
        return (T)field.GetValue(target);
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(
            field,
            target.GetType().Name + "." + fieldName + " was not found.");
        field.SetValue(target, value);
    }

    static object InvokePublic(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method, methodName + " was not found.");
        return method.Invoke(target, arguments);
    }

    static object InvokeNonPublic(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method, methodName + " was not found.");
        return method.Invoke(target, arguments);
    }

    static void ClearSingleton(string typeName, string fieldName)
    {
        Type type = ResolveGameType(typeName);
        if (type == null)
        {
            return;
        }

        FieldInfo field = type.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
        {
            return;
        }

        UnityEngine.Object instance =
            field.GetValue(null) as UnityEngine.Object;
        if (instance != null)
        {
            Component component = instance as Component;
            UnityEngine.Object.DestroyImmediate(
                component != null ? component.gameObject : instance);
        }

        field.SetValue(null, null);
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
