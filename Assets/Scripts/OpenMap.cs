using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenMap :
    MonoBehaviour
{
    public void OpenWorldMap()
    {
        PlayerPrefs.SetString(
            "LastScene",
            SceneManager
            .GetActiveScene()
            .name);

        SceneManager.LoadScene(
            "LiteMapScene");
    }
}