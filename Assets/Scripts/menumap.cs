using UnityEngine;

public class Menumap : MonoBehaviour
{
    public GameObject menuPanel;

    bool isOpen = false;

    public void ToggleMenu()
    {
        isOpen = !isOpen;

        menuPanel.SetActive(isOpen);
    }
}