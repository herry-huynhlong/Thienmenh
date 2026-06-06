using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenLiteMapButton : MonoBehaviour
{
    public string mapSceneName = "LiteMapScene";
    public string fallbackReturnScene = "Lang";

    public void OpenLiteMap()
    {
        CloseInventoryPanel();
        FullGameSaveController.EnsureInstance().SaveFullGame();

        string returnScene =
            GetCurrentGameplaySceneName();

        PlayerPrefs.SetString(
            "LastScene",
            returnScene);
        PlayerPrefs.SetString(
            "MapReturnScene",
            returnScene);
        PlayerPrefs.Save();

        Scene mapScene =
            SceneManager.GetSceneByName(mapSceneName);

        if (!mapScene.isLoaded)
        {
            SceneManager.LoadScene(
                mapSceneName,
                LoadSceneMode.Additive);

            mapScene =
                SceneManager.GetSceneByName(mapSceneName);
        }

        if (mapScene.IsValid() && mapScene.isLoaded)
        {
            SceneManager.SetActiveScene(mapScene);
        }
    }

    string GetCurrentGameplaySceneName()
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
            FindObjectOfType<InventoryPanelUI>(true);

        if (inventoryPanel != null)
        {
            inventoryPanel.Close();
        }
    }
}
