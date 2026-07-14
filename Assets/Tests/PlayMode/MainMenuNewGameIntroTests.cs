using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class MainMenuNewGameIntroTests
{
    [SetUp]
    public void SetUp()
    {
        DestroyPersistentSystems();
        PlayerPrefs.DeleteAll();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyPersistentSystems();
        PlayerPrefs.DeleteAll();
    }

    [UnityTest]
    public IEnumerator LoadingScene_RevealsMainMenu_AndNewGameShowsIntro()
    {
        Type loadingControllerType = ResolveGameType("LoadingSceneController");
        Assert.NotNull(
            loadingControllerType,
            "LoadingSceneController type was not found.");

        MethodInfo tryLoadLoadingScene =
            loadingControllerType.GetMethod(
                "TryLoadLoadingScene",
                BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(
            tryLoadLoadingScene,
            "LoadingSceneController.TryLoadLoadingScene was not found.");

        bool started =
            (bool)tryLoadLoadingScene.Invoke(null, new object[] { "MainMenu" });
        Assert.IsTrue(started, "Loading scene did not start for MainMenu.");

        yield return WaitUntilSceneActive("MainMenu", 35f);
        yield return WaitUntilSceneUnloaded("Loading", 5f);

        Assert.AreEqual(
            "MainMenu",
            SceneManager.GetActiveScene().name,
            "MainMenu was not the active scene after Loading.");

        Type menuManagerType = ResolveGameType("MainMenuManager");
        Assert.NotNull(menuManagerType, "MainMenuManager type was not found.");
        Assert.NotNull(
            FindFirstObjectByType(menuManagerType),
            "MainMenuManager was not found after Loading.");

        Button newGameButton = FindButtonByName("New Game");
        Assert.NotNull(newGameButton, "New Game button was not found.");
        Assert.IsTrue(
            newGameButton.gameObject.activeInHierarchy,
            "New Game button is not active after Loading.");
        Assert.IsTrue(
            newGameButton.interactable,
            "New Game button is not interactable after Loading.");

        GameObject introPanelRoot = FindIntroPanelRoot();
        Assert.NotNull(introPanelRoot, "Intro panel root is missing.");
        Assert.IsFalse(
            introPanelRoot.activeSelf,
            "Intro panel should be hidden before clicking New Game.");

        newGameButton.onClick.Invoke();
        yield return null;

        Assert.IsTrue(
            introPanelRoot.activeSelf,
            "Clicking New Game after Loading did not show the intro panel.");
    }

    [UnityTest]
    public IEnumerator NewGameButtonClick_ShowsIntroPanelImmediately()
    {
        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        Assert.NotNull(loadOperation, "MainMenu scene could not be loaded.");

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return null;

        Type menuManagerType = ResolveGameType("MainMenuManager");
        Assert.NotNull(menuManagerType, "MainMenuManager type was not found.");
        UnityEngine.Object menuManager =
            FindFirstObjectByType(menuManagerType);
        Assert.NotNull(menuManager, "MainMenuManager was not found.");

        Button newGameButton = FindButtonByName("New Game");
        Assert.NotNull(newGameButton, "New Game button was not found.");
        Assert.IsTrue(
            newGameButton.gameObject.activeInHierarchy,
            "New Game button is not active.");
        Assert.IsTrue(
            newGameButton.interactable,
            "New Game button is not interactable.");

        GameObject introPanelRoot = FindIntroPanelRoot();
        Assert.NotNull(introPanelRoot, "Intro panel root is missing.");

        newGameButton.onClick.Invoke();
        yield return null;

        Assert.IsTrue(
            introPanelRoot.activeSelf,
            "Clicking New Game did not show the intro panel.");
    }

    [UnityTest]
    public IEnumerator LoadingScene_KeepsOverlayUntilGameplaySceneIsReady()
    {
        InvokeStaticMethod("GameSaveSystem", "RequestNewGameStart");

        Type loadingControllerType = ResolveGameType("LoadingSceneController");
        Assert.NotNull(
            loadingControllerType,
            "LoadingSceneController type was not found.");

        MethodInfo tryLoadLoadingScene =
            loadingControllerType.GetMethod(
                "TryLoadLoadingScene",
                BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(
            tryLoadLoadingScene,
            "LoadingSceneController.TryLoadLoadingScene was not found.");

        bool started =
            (bool)tryLoadLoadingScene.Invoke(
                null,
                new object[] { "PersistentScene" });
        Assert.IsTrue(started, "Loading scene did not start for PersistentScene.");

        yield return WaitUntilSceneLoaded("PersistentScene", 20f);

        Scene loadingScene = SceneManager.GetSceneByName("Loading");
        Assert.IsTrue(
            loadingScene.IsValid() && loadingScene.isLoaded,
            "Loading overlay should remain while PersistentScene bootstraps gameplay.");

        yield return WaitUntilSceneActive("Lang", 35f);
        yield return WaitUntilSceneUnloaded("Loading", 5f);

        Assert.AreEqual(
            "Lang",
            SceneManager.GetActiveScene().name,
            "Lang should be the active scene after gameplay startup.");
    }

    static IEnumerator WaitUntilSceneActive(string sceneName, float timeoutSeconds)
    {
        float startTime = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail(
            "Timed out waiting for active scene '" +
            sceneName +
            "'. Active scene was '" +
            SceneManager.GetActiveScene().name +
            "'.");
    }

    static IEnumerator WaitUntilSceneUnloaded(string sceneName, float timeoutSeconds)
    {
        float startTime = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail("Timed out waiting for scene '" + sceneName + "' to unload.");
    }

    static IEnumerator WaitUntilSceneLoaded(string sceneName, float timeoutSeconds)
    {
        float startTime = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail("Timed out waiting for scene '" + sceneName + "' to load.");
    }

    static GameObject FindIntroPanelRoot()
    {
        Type introPanelType = ResolveGameType("NewGameIntroPanel");
        Assert.NotNull(introPanelType, "NewGameIntroPanel type was not found.");
        UnityEngine.Object introPanel =
            FindFirstObjectByType(
                introPanelType,
                FindObjectsInactive.Include);
        Assert.NotNull(introPanel, "NewGameIntroPanel was not found.");
        return GetFieldValue<GameObject>(introPanel, "introPanel");
    }

    static Button FindButtonByName(string buttonName)
    {
        Button[] buttons =
            UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    static UnityEngine.Object FindFirstObjectByType(
        Type type,
        FindObjectsInactive inactiveMode = FindObjectsInactive.Exclude)
    {
        UnityEngine.Object[] objects =
            UnityEngine.Object.FindObjectsByType(
                type,
                inactiveMode,
                FindObjectsSortMode.None);
        return objects != null && objects.Length > 0 ? objects[0] : null;
    }

    static void DestroyPersistentSystems()
    {
        Type dontDestroyType = ResolveGameType("DontDestroy");
        if (dontDestroyType == null)
        {
            return;
        }

        UnityEngine.Object[] objects =
            UnityEngine.Object.FindObjectsByType(
                dontDestroyType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            Component component = objects[i] as Component;
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }
    }

    static void InvokeStaticMethod(string typeName, string methodName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, typeName + " type was not found.");

        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method, typeName + "." + methodName + " was not found.");
        method.Invoke(null, null);
    }

    static T GetFieldValue<T>(object target, string fieldName)
        where T : class
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field, target.GetType().Name + "." + fieldName + " was not found.");
        return field.GetValue(target) as T;
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
