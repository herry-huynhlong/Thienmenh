using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public string characterCreateScene = "CharacterCreate";
    public string firstGameScene = "VillageMap";

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
        if (GameSaveSystem.HasSave ||
            PlayerPrefs.HasKey("PlayerName"))
        {
            SceneManager.LoadScene(
                GameSaveSystem.LoadCurrentScene(firstGameScene));
        }
        else
        {
            SceneManager.LoadScene(characterCreateScene);
        }
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
