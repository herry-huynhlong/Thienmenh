using UnityEngine;

public class TaskBoardPanelUI : MonoBehaviour
{
    public Transform content;
    public TaskBoardRowUI rowTemplate;

    bool warnedMissingProvider;

    private void Awake()
    {
        HideRowTemplate();
    }

    private void OnEnable()
    {
        HideRowTemplate();
    }

    public void Show(NpcTaskProvider provider)
    {
        if (provider == null)
        {
            provider = FindFirstObjectByType<NpcTaskProvider>();
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
}
