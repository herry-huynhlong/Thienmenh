using UnityEngine;

public class LuckyWheelOpenClose : MonoBehaviour
{
    [Header("Panel")]
    public GameObject luckyWheelPanel;

    [Header("Optional")]
    public GameObject openButton;

    private void Start()
    {
        CloseWheel();
    }

    public void OpenWheel()
    {
        if (luckyWheelPanel != null)
            luckyWheelPanel.SetActive(true);

        if (openButton != null)
            openButton.SetActive(false);
    }

    public void CloseWheel()
    {
        if (luckyWheelPanel != null)
            luckyWheelPanel.SetActive(false);

        if (openButton != null)
            openButton.SetActive(true);
    }
}