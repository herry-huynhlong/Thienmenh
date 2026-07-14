using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class WorldSimulationBootstrapRuntimeTests
{
    static readonly string[] BootstrapSystemTypeNames =
    {
        "DayNightLightingSystem",
        "WeatherSystem",
        "WeatherVisualSystem",
        "WeatherAccumulationSystem",
        "HeavenSystem",
        "WorldEventSystem"
    };

    [SetUp]
    public void SetUp()
    {
        ClearBootstrapInstances();
    }

    [TearDown]
    public void TearDown()
    {
        ClearBootstrapInstances();
    }

    [UnityTest]
    public IEnumerator SceneConfiguredSystems_ReplaceRuntimeFallbacks()
    {
        for (int i = 0; i < BootstrapSystemTypeNames.Length; i++)
        {
            Type systemType = ResolveGameType(BootstrapSystemTypeNames[i]);
            Assert.NotNull(systemType, "Failed to resolve " + BootstrapSystemTypeNames[i] + " type.");

            Component runtimeFallback = CreateSystem(systemType, true);
            yield return null;

            Component sceneConfigured = CreateSystem(systemType, false);
            yield return null;

            Assert.AreSame(
                sceneConfigured,
                GetSingletonInstance(systemType),
                systemType.Name +
                " should keep the scene-configured instance instead of the bootstrap fallback.");

            if (sceneConfigured != null)
            {
                UnityEngine.Object.Destroy(sceneConfigured.gameObject);
            }

            // The scene-configured instance intentionally destroys the
            // bootstrap fallback during Awake, so it can already compare as
            // null by the time cleanup runs.
            if (runtimeFallback != null)
            {
                UnityEngine.Object.Destroy(runtimeFallback.gameObject);
            }
            yield return null;
            ClearBootstrapInstances();
        }
    }

    static Component CreateSystem(Type systemType, bool markRuntimeFallback)
    {
        GameObject gameObject = new GameObject(systemType.Name);
        Component component = gameObject.AddComponent(systemType);

        if (markRuntimeFallback)
        {
            MethodInfo markCreatedAtRuntime = systemType.GetMethod(
                "MarkCreatedAtRuntime",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(
                markCreatedAtRuntime,
                systemType.Name + " is missing MarkCreatedAtRuntime().");
            markCreatedAtRuntime.Invoke(component, null);
        }

        return component;
    }

    static UnityEngine.Object GetSingletonInstance(Type systemType)
    {
        PropertyInfo property = systemType.GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            return property.GetValue(null) as UnityEngine.Object;
        }

        FieldInfo field = systemType.GetField(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null
            ? field.GetValue(null) as UnityEngine.Object
            : null;
    }

    static void ClearBootstrapInstances()
    {
        for (int i = 0; i < BootstrapSystemTypeNames.Length; i++)
        {
            Type systemType = ResolveGameType(BootstrapSystemTypeNames[i]);
            if (systemType == null)
            {
                continue;
            }

            UnityEngine.Object instance = GetSingletonInstance(systemType);
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            SetSingletonInstance(systemType, null);
        }
    }

    static void SetSingletonInstance(Type systemType, UnityEngine.Object value)
    {
        FieldInfo instanceField = systemType.GetField(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (instanceField != null)
        {
            instanceField.SetValue(null, value);
            return;
        }

        FieldInfo backingField = systemType.GetField(
            "<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (backingField != null)
        {
            backingField.SetValue(null, value);
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
