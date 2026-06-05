using UnityEngine;

public class TaskBoardInteract : MonoBehaviour
{
    public NpcTaskProvider provider;
    public TaskBoardPanelUI panel;

    private void OnMouseDown()
    {
        if (panel == null)
        {
            Debug.LogWarning("Chưa gán TaskBoardPanelUI.");
            return;
        }

        if (provider == null)
        {
            Debug.LogWarning("Chưa gán NpcTaskProvider.");
            return;
        }

        panel.Show(provider);
    }
}