using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DamageContextRuntimeTests
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
    public void Calculation_DefenseArmorPenetrationCriticalAndTrueDamage()
    {
        object context = CreateLegacyContext(100, null);

        Assert.AreEqual(50, CalculateFinalDamage(context, 100));

        SetField(context, "armorPenetration", 100);
        Assert.AreEqual(100, CalculateFinalDamage(context, 100));

        SetField(context, "armorPenetration", 0);
        SetField(context, "isCritical", true);
        SetField(context, "criticalMultiplier", 2f);
        Assert.AreEqual(100, CalculateFinalDamage(context, 100));

        SetField(context, "isCritical", false);
        SetEnumField(context, "damageType", "True");
        Assert.AreEqual(100, CalculateFinalDamage(context, 999999));
    }

    [Test]
    public void LegacyEntry_ResolvesPlayerAndAppliesDefenseExactlyOnce()
    {
        GameObject target = CreateObject("CanonicalPlayerDamageTarget");
        Component stats = AddComponent(target, "CharacterStats");
        Component player = AddComponent(target, "PlayerHealth");

        SetField(stats, "generateFromEntityProfile", false);
        SetField(stats, "finalHP", 250);
        SetField(stats, "currentHP", 250);
        SetField(stats, "defense", 100);
        SetField(player, "characterStats", stats);
        SetField(player, "maxHP", 250);
        SetField(player, "currentHP", 250);

        InvokePublic(stats, "TakeDamage", 100);

        Assert.AreEqual(200, GetField<int>(stats, "currentHP"));
        Assert.AreEqual(200, GetField<int>(player, "currentHP"));
    }

    [Test]
    public void Pipeline_BlocksSelfAndDeclaredFriendlyFaction()
    {
        GameObject target = CreateObject("FriendlyDamageTarget");
        Component stats = AddComponent(target, "CharacterStats");
        SetField(stats, "generateFromEntityProfile", false);
        SetField(stats, "finalHP", 100);
        SetField(stats, "currentHP", 100);
        SetField(stats, "defense", 0);

        object selfContext = CreateLegacyContext(25, target);
        object selfResult = ApplyDamage(target, selfContext);

        Assert.AreEqual(
            "SelfDamage",
            GetField<object>(selfResult, "blockReason").ToString());
        Assert.AreEqual(100, GetField<int>(stats, "currentHP"));

        GameObject ally = CreateObject("FriendlyDamageAttacker");
        object allyContext = CreateLegacyContext(25, ally);
        SetField(allyContext, "attackerFactionId", "sect_a");
        SetField(allyContext, "targetFactionId", "sect_a");
        object allyResult = ApplyDamage(target, allyContext);

        Assert.AreEqual(
            "FriendlyFire",
            GetField<object>(allyResult, "blockReason").ToString());
        Assert.AreEqual(100, GetField<int>(stats, "currentHP"));
    }

    [Test]
    public void MonsterLegacyOverload_PreservesAttackerAttribution()
    {
        GameObject attacker = CreateObject("DamageAttacker");
        GameObject target = CreateObject("DamageMonsterTarget");
        Component monster = AddComponent(target, "MonsterAI");
        SetField(monster, "autoStatsFromRealm", false);
        SetField(monster, "maxHP", 100);
        SetField(monster, "currentHP", 100);
        SetField(monster, "defense", 0);
        SetField(monster, "isDead", false);
        SetField(monster, "isRespawning", false);

        InvokePublic(monster, "TakeDamage", 25, attacker);

        Assert.AreSame(
            attacker,
            GetProperty<GameObject>(monster, "LastDamageSource"));
        Assert.AreEqual(75, GetField<int>(monster, "currentHP"));
    }

    GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    static Component AddComponent(GameObject gameObject, string typeName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, "Missing game type: " + typeName);
        return gameObject.AddComponent(type);
    }

    static object CreateLegacyContext(int amount, GameObject attacker)
    {
        Type contextType = ResolveGameType("DamageContext");
        MethodInfo method = contextType.GetMethod(
            "Legacy",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(int), typeof(GameObject) },
            null);
        Assert.NotNull(method);
        return method.Invoke(null, new object[] { amount, attacker });
    }

    static int CalculateFinalDamage(object context, int defense)
    {
        Type systemType = ResolveGameType("DamageSystem");
        MethodInfo method = systemType.GetMethod(
            "CalculateFinalDamage",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { context.GetType(), typeof(int) },
            null);
        Assert.NotNull(method);
        return (int)method.Invoke(null, new[] { context, (object)defense });
    }

    static object ApplyDamage(GameObject target, object context)
    {
        Type systemType = ResolveGameType("DamageSystem");
        MethodInfo method = systemType.GetMethod(
            "Apply",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(GameObject), context.GetType() },
            null);
        Assert.NotNull(method);
        return method.Invoke(null, new[] { target, context });
    }

    static object InvokePublic(
        object target,
        string methodName,
        params object[] arguments)
    {
        Type[] parameterTypes = new Type[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            parameterTypes[i] = arguments[i].GetType();
        }

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public,
            null,
            parameterTypes,
            null);
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

    static void SetEnumField(
        object target,
        string fieldName,
        string enumValue)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, Enum.Parse(field.FieldType, enumValue));
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
