using UnityEngine;
using UnityEngine.SceneManagement;

public class CloseMap :
    MonoBehaviour
{
    public void ExitMap()
    {
        string lastScene =
            PlayerPrefs.GetString(
                "LastScene",
                "Lang");

        GameSaveSystem.SaveCurrentScene(lastScene);
        SceneManager.LoadScene(
            lastScene);
    }
}
