using UnityEngine;
using UnityEngine.SceneManagement;

public class CloseMap :
    MonoBehaviour
{
    public string fallbackScene = "Lang";

    public void ExitMap()
    {
        string lastScene =
            PlayerPrefs.GetString(
                "MapReturnScene",
                PlayerPrefs.GetString(
                    "LastScene",
                    fallbackScene));

        if (!IsValidReturnScene(lastScene))
        {
            lastScene =
                PlayerPrefs.GetString(
                    "LastScene",
                    fallbackScene);
        }

        if (!IsValidReturnScene(lastScene))
        {
            lastScene = fallbackScene;
        }

        if (string.IsNullOrEmpty(lastScene) ||
            lastScene == SceneManager.GetActiveScene().name)
        {
            lastScene = fallbackScene;
        }

        Scene returnScene =
            SceneManager.GetSceneByName(lastScene);

        Scene mapScene =
            SceneManager.GetSceneByName("LiteMapScene");

        if (returnScene.IsValid() &&
            returnScene.isLoaded &&
            mapScene.IsValid() &&
            mapScene.isLoaded &&
            SceneManager.sceneCount > 1)
        {
            SceneManager.SetActiveScene(returnScene);
            SceneManager.UnloadSceneAsync(mapScene);
            return;
        }

        SceneManager.LoadScene(lastScene);
    }

    bool IsValidReturnScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) &&
            sceneName != "LiteMapScene" &&
            sceneName != "PersistentScene";
    }
}
