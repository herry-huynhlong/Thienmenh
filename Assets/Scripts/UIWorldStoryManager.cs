using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class UIWorldStoryManager : MonoBehaviour
{
    public static UIWorldStoryManager Instance;

    [Header("Giao Diện Chính")]
    public GameObject storyPanel;
    public Transform contentContainer;
    public ScrollRect scrollRect;

    [Header("Mẫu Dòng Chữ (Prefab)")]
    public GameObject logTextPrefab;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (storyPanel != null)
        {
            storyPanel.SetActive(false);
        }
    }

    void OnEnable()
    {
        WorldEventManager.OnLogUpdated += RefreshUI;
    }

    void OnDisable()
    {
        WorldEventManager.OnLogUpdated -= RefreshUI;
    }

    public void ToggleStoryPanel()
    {
        if (storyPanel == null)
        {
            return;
        }

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
        {
            return;
        }

        ClearOldLogs();

        if (WorldEventManager.Instance == null)
        {
            Debug.LogWarning("UIWorldStoryManager: Không tìm thấy WorldEventManager.Instance");
            return;
        }

        List<LogEntry> logs = WorldEventManager.Instance.GetStoryLogs();

        foreach (LogEntry log in logs)
        {
            CreateLogItem(log);
        }

        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void ClearOldLogs()
    {
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void CreateLogItem(LogEntry log)
    {
        GameObject newTextObj = Instantiate(logTextPrefab, contentContainer);

        // Dùng GetComponentInChildren vì TextMeshPro nằm ở object con LogText
        TextMeshProUGUI textMesh = newTextObj.GetComponentInChildren<TextMeshProUGUI>();

        if (textMesh == null)
        {
            Debug.LogWarning("LogTextPrefab chưa có TextMeshProUGUI ở object con.");
            return;
        }

        string timeText = GetLocalizedTimestamp(log.timestamp);
        string contentColor = GetContentColorHex(log.logColorType);

        textMesh.text =
            $"<size=85%><color=#9E6A2E>{timeText}</color></size>\n" +
            $"<color={contentColor}>{log.content}</color>";
    }

    private string GetContentColorHex(int type)
    {
        switch (type)
        {
            case 1:
                return "#2F7C9D"; // tin đặc biệt / xanh lam dịu

            case 2:
                return "#B94735"; // cảnh báo / đỏ cam dịu

            default:
                return "#4B3726"; // chữ thường / nâu đậm
        }
    }

    private string FormatTimestamp(string timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return UiText.Get("worldStory", "dayUnknown");
        }

        string clean = timestamp
            .Replace("[", "")
            .Replace("]", "")
            .Trim();

        int month = 0;
        int day = 1;

        try
        {
            string[] parts = clean.Split('-');

            foreach (string part in parts)
            {
                string p = part.Trim();

                // T1, T2, T3...
                if (p.StartsWith("T"))
                {
                    string number = p.Replace("T", "").Trim();
                    int.TryParse(number, out month);
                }

                // N1, N2, N3...
                // Tranh nham voi "Nam"
                else if (p.StartsWith("N") && !p.StartsWith("Nam"))
                {
                    string number = p.Replace("N", "").Trim();
                    int.TryParse(number, out day);
                }
            }
        }
        catch
        {
            return clean;
        }

        if (month <= 0)
        {
            return UiText.Format("worldStory", "dayOnlyFormat", day);
        }

        return UiText.Format("worldStory", "monthDayFormat", month, day);
    }

    private string GetLocalizedTimestamp(string timestamp)
    {
        string fallback = FormatTimestamp(timestamp);
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return UiText.Get("worldStory", "dayUnknown", fallback);
        }

        string clean = timestamp
            .Replace("[", "")
            .Replace("]", "")
            .Trim();

        int month = 0;
        int day = 1;

        try
        {
            string[] parts = clean.Split('-');

            foreach (string part in parts)
            {
                string p = part.Trim();
                if (p.StartsWith("T"))
                {
                    int.TryParse(
                        p.Replace("T", "").Trim(),
                        out month);
                }
                else if (p.StartsWith("N") &&
                    !p.StartsWith("Nam"))
                {
                    int.TryParse(
                        p.Replace("N", "").Trim(),
                        out day);
                }
            }
        }
        catch
        {
            return fallback;
        }

        if (month <= 0)
        {
            return UiText.Format("worldStory", "dayOnlyFormat", day);
        }

        return UiText.Format("worldStory", "monthDayFormat", month, day);
    }
}
