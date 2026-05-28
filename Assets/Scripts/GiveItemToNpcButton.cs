using UnityEngine;

public class GiveItemToNpcButton : MonoBehaviour
{
    public InventoryPanelUI playerInventoryUI;

    public void GiveSelectedItem()
    {
        if (playerInventoryUI == null)
        {
            return;
        }

        playerInventoryUI.GiveSelectedItemToSelectedNpc();
    }
}
