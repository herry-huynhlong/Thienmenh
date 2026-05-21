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

        SceneManager.LoadScene(
            lastScene);
    }
}