using UnityEngine;

public class ShopToggleUI : MonoBehaviour
{
    public GameObject shopPanel;

    public void ToggleShop()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(!shopPanel.activeSelf);
    }

    public void OpenShop()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(true);
    }

    public void CloseShop()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(false);
    }
}