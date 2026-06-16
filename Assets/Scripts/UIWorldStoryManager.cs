using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class UIWorldStoryManager : MonoBehaviour
{
    public static UIWorldStoryManager Instance;

    [Header("Giao Diện Chính")]
    public GameObject storyPanel;       // Kéo khung to StoryPanel vào đây
    public Transform contentContainer; // Kéo ô Content của Scroll View vào đây
    public ScrollRect scrollRect;       // Kéo Scroll View vào đây

    [Header("Mẫu Dòng Chữ (Prefab)")]
    public GameObject logTextPrefab;   // Kéo Prefab TextMeshPro dòng chữ vào đây

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (storyPanel != null) storyPanel.SetActive(false);
    }

    void OnEnable()
    {
        WorldEventManager.OnLogUpdated += RefreshUI;
    }

    void OnDisable()
    {
        WorldEventManager.OnLogUpdated -= RefreshUI;
    }

    // Gắn hàm này vào Sự kiện OnClick của Nút bấm cuộn giấy lông vũ
    public void ToggleStoryPanel()
    {
        if (storyPanel == null) return;

        bool isActive = !storyPanel.activeSelf;
        storyPanel.SetActive(isActive);

        if (isActive)
        {
            RefreshUI();
        }
    }

    public void RefreshUI()
    {
        if (storyPanel == null || !storyPanel.activeSelf || contentContainer == null || logTextPrefab == null) 
            return;

        // Xóa các dòng chữ UI cũ
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        // Đọc data từ Manager
        List<LogEntry> logs = WorldEventManager.Instance.GetStoryLogs();

        foreach (LogEntry log in logs)
        {
            GameObject newTextObj = Instantiate(logTextPrefab, contentContainer);
            TextMeshProUGUI textMesh = newTextObj.GetComponent<TextMeshProUGUI>();

            if (textMesh != null)
            {
                // Đổi màu dựa theo số int đơn giản (0: Nâu giấy, 1: Xanh lam, 2: Đỏ cam)
                string colorHex = "#D2B48C"; 
                if (log.logColorType == 1) colorHex = "#00FFFF"; 
                if (log.logColorType == 2) colorHex = "#FF4500"; 

                textMesh.text = $"<color={colorHex}>{log.timestamp}</color> {log.content}";
            }
        }

        // Tự cuộn lên đầu trang
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f; 
        }
    }
}
