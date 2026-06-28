using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenMap :
    MonoBehaviour
{
    public string fallbackReturnScene = "Lang";

    public void OpenWorldMap()
    {
        CloseInventoryPanel();
        FullGameSaveController.EnsureInstance().SaveFullGame();

        string returnScene =
            GetCurrentGameplaySceneName("LiteMapScene");

        PlayerPrefs.SetString(
            "LastScene",
            returnScene);
        PlayerPrefs.SetString(
            "MapReturnScene",
            returnScene);
        PlayerPrefs.Save();

        Scene mapScene =
            SceneManager.GetSceneByName("LiteMapScene");

        if (!mapScene.isLoaded)
        {
            SceneManager.LoadScene(
                "LiteMapScene",
                LoadSceneMode.Additive);

            mapScene =
                SceneManager.GetSceneByName("LiteMapScene");
        }

        if (mapScene.IsValid() && mapScene.isLoaded)
        {
            SceneManager.SetActiveScene(mapScene);
        }
    }

    string GetCurrentGameplaySceneName(string mapSceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene =
                SceneManager.GetSceneAt(i);

            if (!scene.isLoaded ||
                scene.name == "PersistentScene" ||
                scene.name == mapSceneName)
            {
                continue;
            }

            return scene.name;
        }

        Scene activeScene =
            SceneManager.GetActiveScene();

        if (activeScene.isLoaded &&
            activeScene.name != "PersistentScene" &&
            activeScene.name != mapSceneName)
        {
            return activeScene.name;
        }

        return fallbackReturnScene;
    }

    void CloseInventoryPanel()
    {
        InventoryPanelUI inventoryPanel =
            FindAnyObjectByType<InventoryPanelUI>(
                FindObjectsInactive.Include);

        if (inventoryPanel != null)
        {
            inventoryPanel.Close();
        }
    }
}
