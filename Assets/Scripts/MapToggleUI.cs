using UnityEngine;

public class MapToggleUI : MonoBehaviour
{
    public GameObject worldMapPanel;

    public void ToggleMap()
    {
        if (worldMapPanel == null) return;
        worldMapPanel.SetActive(!worldMapPanel.activeSelf);
    }

    public void OpenMap()
    {
        if (worldMapPanel == null) return;
        worldMapPanel.SetActive(true);
    }

    public void CloseMap()
    {
        if (worldMapPanel == null) return;
        worldMapPanel.SetActive(false);
    }
}