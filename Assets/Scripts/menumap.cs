using UnityEngine;

public class Menumap : MonoBehaviour
{
    public GameObject menuPanel;
    public bool closePanelsWhenMenuCloses = true;

    void Awake()
    {
        ResolveMenuPanel();
    }

    public void ToggleMenu()
    {
        ResolveMenuPanel();

        if (menuPanel == null)
        {
            Debug.LogWarning("Menumap missing MenuPanel reference.", this);
            return;
        }

        bool shouldOpen = !menuPanel.activeSelf;
        menuPanel.SetActive(shouldOpen);

        if (shouldOpen)
        {
            RestoreMenuButtons();
        }
        else if (closePanelsWhenMenuCloses)
        {
            CloseMenuOwnedPanels();
        }
    }

    void RestoreMenuButtons()
    {
        if (menuPanel == null)
        {
            return;
        }

        SetDirectChildActive("map", true);
        SetDirectChildActive("Shop", true);
        SetDirectChildActive("Story", true);
        SetDirectChildActive("Balo", true);
    }

    void CloseMenuOwnedPanels()
    {
        InventoryPanelUI[] inventories =
            FindObjectsByType<InventoryPanelUI>(FindObjectsInactive.Include);

        foreach (InventoryPanelUI inventory in inventories)
        {
            if (inventory != null && !inventory.alwaysVisible)
            {
                inventory.Close();
            }
        }

        ShopPanelUI[] shops =
            FindObjectsByType<ShopPanelUI>(FindObjectsInactive.Include);

        foreach (ShopPanelUI shop in shops)
        {
            if (shop != null)
            {
                shop.Close();
            }
        }
    }

    void SetDirectChildActive(string childName, bool active)
    {
        Transform child = menuPanel.transform.Find(childName);
        if (child != null)
        {
            child.gameObject.SetActive(active);
        }
    }

    void ResolveMenuPanel()
    {
        if (menuPanel != null && menuPanel.name == "MenuPanel")
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            Transform panel = FindChildByName(canvas.transform, "MenuPanel");
            if (panel != null)
            {
                menuPanel = panel.gameObject;
                return;
            }
        }

        Transform[] transforms =
            FindObjectsByType<Transform>(FindObjectsInactive.Include);

        foreach (Transform found in transforms)
        {
            if (found != null &&
                found.name == "MenuPanel" &&
                found.gameObject.scene == gameObject.scene)
            {
                menuPanel = found.gameObject;
                return;
            }
        }
    }

    Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildByName(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}