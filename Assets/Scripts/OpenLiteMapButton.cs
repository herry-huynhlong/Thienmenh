using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenLiteMapButton : MonoBehaviour
{
    public string mapSceneName = "LiteMapScene";

    public void OpenLiteMap()
    {
        CloseInventoryPanel();

        PlayerPrefs.SetString(
            "LastScene",
            SceneManager.GetActiveScene().name);

        GameSaveSystem.SaveCurrentScene(mapSceneName);
        SceneManager.LoadScene(mapSceneName);
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
