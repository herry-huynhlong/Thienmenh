using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenLiteMapButton : MonoBehaviour
{
    public string mapSceneName = "LiteMapScene";

    public void OpenLiteMap()
    {
        PlayerPrefs.SetString(
            "LastScene",
            SceneManager.GetActiveScene().name);

        SceneManager.LoadScene(mapSceneName);
    }
}