using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryToggleButton : MonoBehaviour, IPointerDownHandler
{
    public InventoryPanelUI inventoryPanel;
    public float toggleCooldown = 0.15f;

    float lastToggleTime = -1f;
    float pointerDownTime = -1f;
    bool hadPointerDownState;
    bool wasOpenOnPointerDown;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inventoryPanel == null)
        {
            FindInventoryPanel();
        }

        wasOpenOnPointerDown = inventoryPanel != null && inventoryPanel.IsOpen;
        hadPointerDownState = true;
        pointerDownTime = Time.unscaledTime;
    }

    public void ToggleInventory()
    {
        if (Time.unscaledTime - lastToggleTime < toggleCooldown)
        {
            return;
        }

        if (inventoryPanel == null)
        {
            FindInventoryPanel();
        }

        if (inventoryPanel == null)
        {
            Debug.LogWarning("InventoryToggleButton missing InventoryPanelUI.");
            return;
        }

        bool usePointerDownState =
            hadPointerDownState &&
            Time.unscaledTime - pointerDownTime < 0.75f;

        bool shouldOpen =
            usePointerDownState
            ? !wasOpenOnPointerDown
            : !inventoryPanel.IsOpen;

        hadPointerDownState = false;
        lastToggleTime = Time.unscaledTime;

        if (shouldOpen)
        {
            inventoryPanel.Open();
        }
        else
        {
            inventoryPanel.Close();
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
            FindObjectsByType<InventoryPanelUI>(
                FindObjectsInactive.Include);

        foreach (InventoryPanelUI panel in panels)
        {
            if (panel == null ||
                !panel.enabled ||
                HasAncestorNamed(panel.transform, "menupanel"))
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

        foreach (InventoryPanelUI panel in panels)
        {
            if (panel == null ||
                !panel.enabled ||
                HasAncestorNamed(panel.transform, "menupanel"))
            {
                continue;
            }

            inventoryPanel = panel;
            return;
        }
    }

    bool HasAncestorNamed(Transform current, string normalizedName)
    {
        while (current != null)
        {
            string key = current.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (key == normalizedName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
