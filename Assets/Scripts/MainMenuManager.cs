using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public string characterCreateScene = "CharacterCreate";
    public string firstGameScene = "PersistentScene";

    public void NewGame()
    {
        GameSaveSystem.ClearSave();
        PlayerPrefs.DeleteAll();
        ItemInventory.ClearRuntimeCache();
        SimpleItemShop.ClearRuntimeStockCache();
        SceneManager.LoadScene(characterCreateScene);
    }

    public void ContinueGame()
    {
        string sceneToLoad = "";

        if (GameSaveSystem.HasSave ||
            PlayerPrefs.HasKey("PlayerName"))
        {
            sceneToLoad =
                GameSaveSystem.LoadCurrentScene(firstGameScene);
        }
        else
        {
            sceneToLoad = characterCreateScene;
        }

        if (string.IsNullOrEmpty(sceneToLoad) ||
            !Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogWarning(
                $"Cannot continue to scene '{sceneToLoad}'. Loading '{firstGameScene}' instead.");
            sceneToLoad = firstGameScene;
        }

        SceneManager.LoadScene(sceneToLoad);
    }

    public void About()
    {
        Debug.Log("Thiên Mệnh Chi Tử - Game tu tiên RPG");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
