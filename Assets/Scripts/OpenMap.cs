using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenMap :
    MonoBehaviour
{
    public string fallbackReturnScene = "Lang";

    public void OpenWorldMap()
    {
        CloseInventoryPanel();

        string returnScene =
            GetCurrentGameplaySceneName("LiteMapScene");

        PlayerPrefs.SetString(
            "LastScene",
            returnScene);
        PlayerPrefs.SetString(
            "MapReturnScene",
            returnScene);
        PlayerPrefs.Save();

        SceneManager.LoadScene(
            "LiteMapScene");
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
            FindObjectOfType<InventoryPanelUI>(true);

        if (inventoryPanel != null)
        {
            inventoryPanel.Close();
        }
    }
}
