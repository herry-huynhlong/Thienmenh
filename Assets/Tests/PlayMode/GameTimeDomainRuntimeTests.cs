using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameTimeDomainRuntimeTests
{
    readonly List<GameObject> createdObjects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();
        DestroyAll("WorldTimeSystem");
        DestroyAll("WeatherAccumulationSystem");
        SetSingleton("WorldTimeSystem", null);
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
        DestroyAll("WorldTimeSystem");
        DestroyAll("WeatherAccumulationSystem");
        DestroyAll("VillagerRelationshipManager");
        DestroyAll("VillagerBirthManager");
        SetSingleton("WorldTimeSystem", null);
        SetSingleton("WeatherAccumulationSystem", null);
        SetSingleton("VillagerRelationshipManager", null);
        SetSingleton("VillagerBirthManager", null);
        PlayerPrefs.DeleteAll();
    }

    [Test]
    public void LegacySeconds_ConvertToEquivalentWorldHours()
    {
        Type gameTime = ResolveGameType("GameTime");
        Assert.NotNull(gameTime);
        MethodInfo convert = gameTime.GetMethod(
            "LegacyScaledSecondsToWorldHours",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(convert);

        float worldHours = (float)convert.Invoke(null, new object[] { 30f });

        Assert.AreEqual(0.8f, worldHours, 0.0001f);
    }

    [Test]
    public void WorldHours_RespectConfiguredDaySpeed()
    {
        Type gameTime = ResolveGameType("GameTime");
        MethodInfo convert = gameTime.GetMethod(
            "WorldHoursToScaledSeconds",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(float), typeof(float) },
            null);
        Assert.NotNull(convert);

        float fastDaySeconds = (float)convert.Invoke(
            null,
            new object[] { 1f, 900f });
        float slowDaySeconds = (float)convert.Invoke(
            null,
            new object[] { 1f, 1800f });

        Assert.AreEqual(37.5f, fastDaySeconds, 0.0001f);
        Assert.AreEqual(75f, slowDaySeconds, 0.0001f);
    }

    [Test]
    public void ResourceNode_MigratesLegacyRespawnSecondsOnce()
    {
        Component node = CreateResourceNode();
        SetField(node, "respawnDurationWorldHours", 30f);
        SetField(node, "timeDomainVersion", 0);

        InvokePublic(node, "OnAfterDeserialize");
        InvokePublic(node, "OnAfterDeserialize");

        Assert.AreEqual(
            0.8f,
            GetField<float>(node, "respawnDurationWorldHours"),
            0.0001f);
        Assert.AreEqual(1, GetField<int>(node, "timeDomainVersion"));
    }

    [Test]
    public void WeatherAccumulation_MigratesLegacySecondsOnce()
    {
        Component weather = AddComponent(
            "WeatherAccumulationTimeTest",
            "WeatherAccumulationSystem");
        SetField(weather, "rainBuildDurationWorldHours", 18f);
        SetField(weather, "rainFadeDurationWorldHours", 10f);
        SetField(weather, "snowBuildDurationWorldHours", 22f);
        SetField(weather, "snowFadeDurationWorldHours", 16f);
        SetField(weather, "timeDomainVersion", 0);

        InvokePublic(weather, "OnAfterDeserialize");
        InvokePublic(weather, "OnAfterDeserialize");

        Assert.AreEqual(
            0.48f,
            GetField<float>(weather, "rainBuildDurationWorldHours"),
            0.0001f);
        Assert.AreEqual(
            16f * 24f / 900f,
            GetField<float>(weather, "snowFadeDurationWorldHours"),
            0.0001f);
        Assert.AreEqual(1, GetField<int>(weather, "timeDomainVersion"));
    }

    [Test]
    public void ResourceRespawn_CompletesFromWorldHourJump()
    {
        Component timeSystem = AddComponent("WorldTimeTest", "WorldTimeSystem");
        SetField(timeSystem, "autoSaveWorldTime", false);
        InvokePublic(timeSystem, "RestoreTime", 1, 1, 1, 6f, false);

        Component node = CreateResourceNode();
        Component pickup = node.GetComponent(ResolveGameType("WorldStatItemPickup"));
        SetField(node, "respawnDurationWorldHours", 1f);
        SetField(node, "timeDomainVersion", 1);
        SetField(pickup, "amount", 0);

        InvokePrivate(node, "HandleDepleted");

        Assert.IsTrue(GetProperty<bool>(node, "IsRespawning"));
        Assert.AreEqual(
            7d,
            GetProperty<double>(node, "RespawnAtWorldHour"),
            0.0001d);

        InvokePublic(timeSystem, "SetTime", 1, 1, 1, 7f, true);
        InvokePrivate(node, "Update");

        Assert.IsFalse(GetProperty<bool>(node, "IsRespawning"));
        Assert.AreEqual(1, GetField<int>(pickup, "amount"));
    }

    [Test]
    public void WorldTime_ExposesExactAbsoluteWorldHour()
    {
        Component timeSystem = AddComponent("WorldTimeExactTest", "WorldTimeSystem");
        SetField(timeSystem, "autoSaveWorldTime", false);

        InvokePublic(timeSystem, "RestoreTime", 2, 1, 1, 2.5f, false);

        Assert.AreEqual(
            8642.5d,
            GetProperty<double>(timeSystem, "CurrentWorldHourExact"),
            0.0001d);
    }

    Component CreateResourceNode()
    {
        GameObject gameObject = new GameObject("WorldResourceNodeTimeTest");
        createdObjects.Add(gameObject);
        gameObject.AddComponent(ResolveGameType("WorldStatItemPickup"));
        return gameObject.AddComponent(ResolveGameType("WorldResourceNode"));
    }

    Component AddComponent(string objectName, string typeName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, "Missing game type: " + typeName);
        GameObject gameObject = new GameObject(objectName);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent(type);
    }

    static object InvokePublic(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method, target.GetType().Name + "." + methodName);
        return method.Invoke(target, arguments);
    }

    static object InvokePrivate(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method, target.GetType().Name + "." + methodName);
        return method.Invoke(target, arguments);
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
