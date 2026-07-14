using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NpcHealthSourceRuntimeTests
{
    readonly List<GameObject> createdObjects = new List<GameObject>();

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
    }

    [Test]
    public void VillagerHealth_CharacterStatsUpdatesLegacyMirrorAndProfile()
    {
        Component villager = AddComponent("HealthTestVillager", "VillagerAI");

        InvokePublic(villager, "RestoreHealthState", 250, 90);

        Component stats = GetField<Component>(villager, "characterStats");
        Assert.NotNull(stats);
        Assert.AreEqual(250, GetProperty<int>(stats, "MaxHP"));
        Assert.AreEqual(90, GetProperty<int>(stats, "CurrentHP"));
        Assert.AreEqual(GetProperty<int>(stats, "MaxHP"), GetField<int>(villager, "maxHP"));
        Assert.AreEqual(GetProperty<int>(stats, "CurrentHP"), GetField<int>(villager, "currentHP"));
        AssertProfileHealth(stats, 250, 90);

        InvokePublic(stats, "Heal", 40);

        Assert.AreEqual(130, GetProperty<int>(stats, "CurrentHP"));
        Assert.AreEqual(130, GetField<int>(villager, "currentHP"));
        AssertProfileHealth(stats, 250, 130);
    }

    [Test]
    public void SmartNpcHealth_CanonicalSourceExistsBeforeStart()
    {
        Component smartNpc = AddComponent("HealthTestSmartNpc", "SmartNpcAI");

        Assert.NotNull(GetField<Component>(smartNpc, "characterStats"));

        InvokePublic(smartNpc, "RestoreHealthState", 320, 75);

        Component stats = GetField<Component>(smartNpc, "characterStats");
        Assert.NotNull(stats);
        Assert.AreEqual(320, GetProperty<int>(stats, "MaxHP"));
        Assert.AreEqual(75, GetProperty<int>(stats, "CurrentHP"));
        Assert.AreEqual(320, GetField<int>(smartNpc, "maxHP"));
        Assert.AreEqual(75, GetField<int>(smartNpc, "currentHP"));

        InvokePublic(smartNpc, "Heal", 30);

        Assert.AreEqual(105, GetProperty<int>(stats, "CurrentHP"));
        Assert.AreEqual(105, GetField<int>(smartNpc, "currentHP"));
    }

    [Test]
    public void FullSaveVillagerHealth_UsesCharacterStatsAndRestoresBothViews()
    {
        Component controller = AddComponent(
            "HealthTestSaveController",
            "FullGameSaveController");
        Component villager = AddComponent(
            "HealthTestSavedVillager",
            "VillagerAI");
        InvokePublic(villager, "RestoreHealthState", 275, 88);

        SetField(villager, "maxHP", 2);
        SetField(villager, "currentHP", 1);

        object saved = Activator.CreateInstance(ResolveGameType("SavedNpcStateData"));
        InvokePrivate(controller, "SaveVillagerState", saved, villager);

        Assert.AreEqual(275, GetField<int>(saved, "villagerMaxHP"));
        Assert.AreEqual(88, GetField<int>(saved, "villagerCurrentHP"));

        InvokePublic(villager, "RestoreHealthState", 500, 400);
        InvokePrivate(controller, "ApplyVillagerState", saved, villager);

        Component stats = GetField<Component>(villager, "characterStats");
        Assert.AreEqual(275, GetProperty<int>(stats, "MaxHP"));
        Assert.AreEqual(88, GetProperty<int>(stats, "CurrentHP"));
        Assert.AreEqual(275, GetField<int>(villager, "maxHP"));
        Assert.AreEqual(88, GetField<int>(villager, "currentHP"));
        AssertProfileHealth(villager, 275, 88);
    }

    [Test]
    public void FullSaveSmartNpcHealth_UsesCharacterStatsAndRestoresBothViews()
    {
        Component controller = AddComponent(
            "HealthTestSmartSaveController",
            "FullGameSaveController");
        Component smartNpc = AddComponent(
            "HealthTestSavedSmartNpc",
            "SmartNpcAI");
        InvokePublic(smartNpc, "RestoreHealthState", 360, 123);

        SetField(smartNpc, "maxHP", 2);
        SetField(smartNpc, "currentHP", 1);

        object saved = Activator.CreateInstance(ResolveGameType("SavedNpcStateData"));
        InvokePrivate(controller, "SaveSmartNpcState", saved, smartNpc);

        Assert.AreEqual(360, GetField<int>(saved, "smartMaxHP"));
        Assert.AreEqual(123, GetField<int>(saved, "smartCurrentHP"));

        InvokePublic(smartNpc, "RestoreHealthState", 600, 500);
        InvokePrivate(controller, "ApplySmartNpcState", saved, smartNpc);

        Component stats = GetField<Component>(smartNpc, "characterStats");
        Assert.AreEqual(360, GetProperty<int>(stats, "MaxHP"));
        Assert.AreEqual(123, GetProperty<int>(stats, "CurrentHP"));
        Assert.AreEqual(360, GetField<int>(smartNpc, "maxHP"));
        Assert.AreEqual(123, GetField<int>(smartNpc, "currentHP"));
        AssertProfileHealth(smartNpc, 360, 123);
    }

    Component AddComponent(string objectName, string typeName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, "Missing game type: " + typeName);
        GameObject gameObject = new GameObject(objectName);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent(type);
    }

    static void AssertProfileHealth(object source, int maxHP, int currentHP)
    {
        object profile = GetField<object>(source, "entityProfile");
        Assert.NotNull(profile);
        object profileStats = GetField<object>(profile, "stats");
        Assert.NotNull(profileStats);
        Assert.AreEqual(maxHP, GetField<int>(profileStats, "maxHP"));
        Assert.AreEqual(currentHP, GetField<int>(profileStats, "currentHP"));
    }

    static object InvokePublic(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method, "Missing method: " + methodName);
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
        Assert.NotNull(method, "Missing method: " + methodName);
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
