using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class TaskBoardPanelUI : MonoBehaviour
{
    public Transform content;
    public TaskBoardRowUI rowTemplate;
    public Button closeButton;

    bool warnedMissingProvider;

    private void Awake()
    {
        HideRowTemplate();
        CacheCloseButton();
        BindCloseButton();
        BringCloseButtonToFront();
    }

    private void OnEnable()
    {
        HideRowTemplate();
        CacheCloseButton();
        BindCloseButton();
        BringCloseButtonToFront();
    }

    public void Show(NpcTaskProvider provider)
    {
        if (provider == null)
        {
            provider = FindAnyObjectByType<NpcTaskProvider>();
        }

        if (provider == null)
        {
            if (!warnedMissingProvider)
            {
                warnedMissingProvider = true;
                Debug.LogWarning("Missing NpcTaskProvider for task board.");
            }

            return;
        }

        if (content == null)
        {
            Debug.LogWarning("TaskBoardPanelUI missing Content.");
            return;
        }

        if (rowTemplate == null)
        {
            Debug.LogWarning("TaskBoardPanelUI missing Row Template.");
            return;
        }

        gameObject.SetActive(true);
        CacheCloseButton();
        BindCloseButton();
        BringCloseButtonToFront();

        HideRowTemplate();
        ClearOldRows();

        foreach (NpcTaskOffer offer in provider.GetVisibleOffers())
        {
            TaskBoardRowUI row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.SetData(offer);
        }

        HideRowTemplate();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void ClearOldRows()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);

            if (rowTemplate != null && child == rowTemplate.transform)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    void HideRowTemplate()
    {
        if (rowTemplate != null)
        {
            rowTemplate.gameObject.SetActive(false);
        }
    }

    void CacheCloseButton()
    {
        if (closeButton != null)
        {
            return;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            string normalizedName = NormalizeName(button.name);

            if (normalizedName.Contains("dong") ||
                normalizedName.Contains("close"))
            {
                closeButton = button;
                return;
            }
        }

        if (buttons.Length > 0)
        {
            closeButton = buttons[0];
        }
    }

    void BindCloseButton()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(Hide);
        closeButton.onClick.AddListener(Hide);
    }

    void BringCloseButtonToFront()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.transform.SetAsLastSibling();
    }

    static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c == '\u0111' || c == '\u0110')
            {
                builder.Append('d');
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
