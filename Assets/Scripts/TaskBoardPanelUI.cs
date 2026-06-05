using UnityEngine;

public class TaskBoardPanelUI : MonoBehaviour
{
    public Transform content;
    public TaskBoardRowUI rowTemplate;

    public void Show(NpcTaskProvider provider)
    {
        if (provider == null)
        {
            Debug.LogWarning("Chưa gán NpcTaskProvider cho bảng nhiệm vụ.");
            return;
        }

        ClearOldRows();

        foreach (NpcTaskOffer offer in provider.offers)
        {
            TaskBoardRowUI row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.SetData(offer);
        }

        gameObject.SetActive(true);
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

            if (child == rowTemplate.transform)
                continue;

            Destroy(child.gameObject);
        }
    }
}