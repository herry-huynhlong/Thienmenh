using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenMap :
    MonoBehaviour
{
    public void OpenWorldMap()
    {
        CloseInventoryPanel();

        PlayerPrefs.SetString(
            "LastScene",
            SceneManager
            .GetActiveScene()
            .name);

        GameSaveSystem.SaveCurrentScene("LiteMapScene");
        SceneManager.LoadScene(
            "LiteMapScene");
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
