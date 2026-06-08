using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public string fallbackGameplayScene = "Lang";

    IEnumerator Start()
    {
        string gameplaySceneName = ResolveGameplaySceneName();
        Scene gameplayScene = SceneManager.GetSceneByName(gameplaySceneName);

        if (!gameplayScene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                gameplaySceneName,
                LoadSceneMode.Additive);

            if (loadOperation != null)
            {
                yield return loadOperation;
            }

            gameplayScene = SceneManager.GetSceneByName(gameplaySceneName);
        }

        if (gameplayScene.IsValid() && gameplayScene.isLoaded)
        {
            SceneManager.SetActiveScene(gameplayScene);
        }
    }

    string ResolveGameplaySceneName()
    {
        if (!GameSaveSystem.SavedGameLoadRequested)
        {
            GameSaveSystem.ClearSave();
            return fallbackGameplayScene;
        }

        if (!GameSaveSystem.ShouldLoadSavedGame)
        {
            return fallbackGameplayScene;
        }

        string savedScene =
            GameSaveSystem.LoadCurrentScene(fallbackGameplayScene);

        if (!IsValidGameplayScene(savedScene))
        {
            GameSaveSystem.ClearSave();
            return fallbackGameplayScene;
        }

        return savedScene;
    }

    bool IsValidGameplayScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) &&
            sceneName != "PersistentScene" &&
            sceneName != "MainMenu" &&
            sceneName != "LiteMapScene" &&
            Application.CanStreamedLevelBeLoaded(sceneName);
    }
}
