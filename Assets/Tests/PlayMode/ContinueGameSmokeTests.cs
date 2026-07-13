using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ContinueGameSmokeTests
{
    readonly List<string> issueLogs = new List<string>();

    [UnitySetUp]
    public IEnumerator UnitySetUp()
    {
        issueLogs.Clear();
        Application.logMessageReceived += HandleLogMessage;
        yield break;
    }

    [UnityTearDown]
    public IEnumerator UnityTearDown()
    {
        Application.logMessageReceived -= HandleLogMessage;
        yield break;
    }

    [UnityTest]
    public IEnumerator ContinueGame_ReachesExpectedGameplayScene_FromMainMenu()
    {
        if (!GetHasSave())
        {
            Assert.Inconclusive("No saved game was found for the ContinueGame smoke test.");
        }

        string expectedScene = GetExpectedGameplayScene();

        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        Assert.NotNull(loadOperation, "MainMenu scene could not be loaded.");

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return null;

        object menuManager = FindFirstObjectByType("MainMenuManager");
        Assert.NotNull(menuManager, "MainMenuManager was not found in MainMenu.");

        InvokeInstanceMethod(menuManager, "ContinueGame");

        yield return WaitUntil(
            () => IsSceneLoaded("Loading"),
            10f,
            "Timed out waiting for Loading scene after ContinueGame.\n" +
            BuildRuntimeState());

        yield return WaitUntil(
            () => SceneManager.GetActiveScene().name == expectedScene,
            60f,
            "Timed out waiting for gameplay scene '" +
            expectedScene +
            "' after ContinueGame.\n" +
            BuildRuntimeState());

        yield return WaitUntil(
            () => !IsSceneLoaded("Loading"),
            10f,
            "Loading scene stayed loaded after reaching gameplay.\n" +
            BuildRuntimeState());

        Assert.AreEqual(
            expectedScene,
            SceneManager.GetActiveScene().name,
            "ContinueGame did not end in the expected gameplay scene.\n" +
            BuildRuntimeState());
    }

    void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error &&
            type != LogType.Assert &&
            type != LogType.Exception &&
            type != LogType.Warning)
        {
            return;
        }

        issueLogs.Add(type + ": " + condition);
    }

    string BuildRuntimeState()
    {
        List<string> loadedScenes = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            loadedScenes.Add(scene.name + "(loaded=" + scene.isLoaded + ")");
        }

        string issues =
            issueLogs.Count == 0
                ? "none"
                : string.Join("\n", issueLogs.ToArray());

        return "ActiveScene=" +
            SceneManager.GetActiveScene().name +
            "\nLoadedScenes=" + string.Join(", ", loadedScenes.ToArray()) +
            "\nIssues:\n" + issues;
    }

    static IEnumerator WaitUntil(
        Func<bool> predicate,
        float timeoutSeconds,
        string failureMessage)
    {
        float startTime = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
        {
            if (predicate())
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail(failureMessage);
    }

    static bool IsSceneLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    static bool GetHasSave()
    {
        Type gameSaveType = ResolveGameType("GameSaveSystem");
        Assert.NotNull(gameSaveType, "GameSaveSystem type was not found.");

        PropertyInfo hasSaveProperty = gameSaveType.GetProperty(
            "HasSave",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(hasSaveProperty, "GameSaveSystem.HasSave was not found.");
        return (bool)hasSaveProperty.GetValue(null);
    }

    static string GetExpectedGameplayScene()
    {
        Type gameSaveType = ResolveGameType("GameSaveSystem");
        Assert.NotNull(gameSaveType, "GameSaveSystem type was not found.");

        MethodInfo loadCurrentScene = gameSaveType.GetMethod(
            "LoadCurrentScene",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(
            loadCurrentScene,
            "GameSaveSystem.LoadCurrentScene was not found.");

        return (string)loadCurrentScene.Invoke(null, new object[] { "Lang" });
    }

    static object FindFirstObjectByType(string typeName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, typeName + " type was not found.");

        UnityEngine.Object[] objects =
            UnityEngine.Object.FindObjectsByType(
                type,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        return objects != null && objects.Length > 0 ? objects[0] : null;
    }

    static void InvokeInstanceMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(
            method,
            target.GetType().Name + "." + methodName + " was not found.");
        method.Invoke(target, null);
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
