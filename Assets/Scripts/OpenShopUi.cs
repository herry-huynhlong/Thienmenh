using UnityEngine;

public class OpenShopUI : MonoBehaviour
{
    public GameObject shopPanel;

    public void OpenShop()
    {
        shopPanel.SetActive(true);
    }
}