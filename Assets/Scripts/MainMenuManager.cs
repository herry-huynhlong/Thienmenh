using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public string characterCreateScene = "CharacterCreate";
    public string firstGameScene = "VillageMap";

    public void NewGame()
    {
        PlayerPrefs.DeleteAll();
        SceneManager.LoadScene(characterCreateScene);
    }

    public void ContinueGame()
    {
        if (PlayerPrefs.HasKey("PlayerName"))
        {
            SceneManager.LoadScene(firstGameScene);
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