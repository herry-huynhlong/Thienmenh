using UnityEngine;

public class InventoryToggleButton : MonoBehaviour
{
    public InventoryPanelUI inventoryPanel;
    public float toggleCooldown = 0.15f;

    float lastToggleTime = -1f;

    public void ToggleInventory()
    {
        if (Time.unscaledTime - lastToggleTime < toggleCooldown)
        {
            return;
        }

        lastToggleTime = Time.unscaledTime;

        if (inventoryPanel == null)
        {
            FindInventoryPanel();
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.Toggle();
        }
        else
        {
            Debug.LogWarning("InventoryToggleButton missing InventoryPanelUI.");
        }
    }

    public void CloseInventory()
    {
        if (inventoryPanel == null)
        {
            FindInventoryPanel();
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.Close();
        }
    }

    void FindInventoryPanel()
    {
        InventoryPanelUI[] panels =
            FindObjectsOfType<InventoryPanelUI>(true);

        foreach (InventoryPanelUI panel in panels)
        {
            if (panel == null)
            {
                continue;
            }

            if (panel.name == "BaloPanel" ||
                panel.name == "Balo")
            {
                inventoryPanel = panel;
                return;
            }
        }

        if (panels.Length > 0)
        {
            inventoryPanel = panels[0];
        }
    }
}
