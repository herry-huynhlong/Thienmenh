using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class FullGameSaveControllerRuntimeTests
{
    const string FullSaveKey = "ThienMenh.Save.FullGame";
    const string FullSaveBackupKey = "ThienMenh.Save.FullGame.Backup";

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();
        ClearControllerInstance();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteAll();
        ClearControllerInstance();
    }

    [Test]
    public void TryReadSaveData_FallsBackToBackupWhenPrimaryIsInvalid()
    {
        Component controller = CreateController();

        PlayerPrefs.SetString(FullSaveKey, "{invalid");
        PlayerPrefs.SetString(
            FullSaveBackupKey,
            "{\"saveVersion\":1,\"sceneName\":\"Lang\",\"favoriteNpcs\":[]}");

        bool loaded = InvokeTryReadSaveData(
            controller,
            out object data);

        Assert.IsTrue(loaded);
        Assert.NotNull(data);
        Assert.AreEqual("Lang", GetStringField(data, "sceneName"));
    }

    [UnityTest]
    public IEnumerator ApplyFullSaveAfterSceneLoad_AlwaysClearsApplyingLoad()
    {
        Component controller = CreateController();
        PlayerPrefs.SetString(FullSaveKey, "{invalid");

        IEnumerator routine = InvokeApplyRoutine(controller);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }

        Assert.IsFalse(GetApplyingLoad(controller));
    }

    static Component CreateController()
    {
        Type controllerType = ResolveGameType("FullGameSaveController");
        Assert.NotNull(controllerType, "Failed to resolve FullGameSaveController type.");

        GameObject gameObject = new GameObject("FullGameSaveControllerTest");
        return gameObject.AddComponent(controllerType);
    }

    static bool InvokeTryReadSaveData(
        Component controller,
        out object data)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "TryReadSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        object[] parameters = { null };
        bool loaded = (bool)method.Invoke(controller, parameters);
        data = parameters[0];
        return loaded;
    }

    static IEnumerator InvokeApplyRoutine(
        Component controller)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "ApplyFullSaveAfterSceneLoad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (IEnumerator)method.Invoke(controller, null);
    }

    static bool GetApplyingLoad(Component controller)
    {
        FieldInfo field = controller.GetType().GetField(
            "applyingLoad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (bool)field.GetValue(controller);
    }

    static void ClearControllerInstance()
    {
        Type controllerType = ResolveGameType("FullGameSaveController");
        if (controllerType == null)
        {
            return;
        }

        FieldInfo instanceField = controllerType.GetField(
            "instance",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(instanceField);

        Component instance =
            instanceField.GetValue(null) as Component;
        if (instance != null)
        {
            UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }

        instanceField.SetValue(null, null);
    }

    static string GetStringField(
        object target,
        string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field, target.GetType().Name + "." + fieldName + " was not found.");
        return field.GetValue(target) as string;
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
