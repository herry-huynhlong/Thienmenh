using UnityEngine;

public class TaskBoardPanelUI : MonoBehaviour
{
    public Transform content;
    public TaskBoardRowUI rowTemplate;

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
            Debug.LogWarning("Chưa gán NpcTaskProvider cho bảng nhiệm vụ.");
            return;
        }

        if (content == null)
        {
            Debug.LogWarning("TaskBoardPanelUI chưa gán Content.");
            return;
        }

        if (rowTemplate == null)
        {
            Debug.LogWarning("TaskBoardPanelUI chưa gán Row Template.");
            return;
        }

        gameObject.SetActive(true);

        HideRowTemplate();
        ClearOldRows();

        foreach (NpcTaskOffer offer in provider.offers)
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

    private void ClearOldRows()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);

            if (rowTemplate != null && child == rowTemplate.transform)
                continue;

            Destroy(child.gameObject);
        }
    }

    private void HideRowTemplate()
    {
        if (rowTemplate != null)
        {
            rowTemplate.gameObject.SetActive(false);
        }
    }
}