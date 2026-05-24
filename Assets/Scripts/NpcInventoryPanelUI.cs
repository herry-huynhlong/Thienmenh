using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcInventoryPanelUI : MonoBehaviour
{
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text itemsText;
    public string emptyText = "Khong co vat pham";
    public bool blockMapDrag;

    void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        ConfigureRaycasts();
        Hide();
    }

    public void Show(Transform npc)
    {
        if (npc == null)
        {
            Hide();
            return;
        }

        ItemInventory inventory =
            npc.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            Hide();
            return;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (titleText != null)
        {
            titleText.text =
                GetNpcName(npc) + " - Tui do";
        }

        if (itemsText != null)
        {
            itemsText.text =
                BuildItemsText(inventory);
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    string BuildItemsText(ItemInventory inventory)
    {
        if (inventory == null ||
            inventory.items.Count == 0)
        {
            return emptyText;
        }

        StringBuilder builder =
            new StringBuilder();

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            builder.Append(stack.item.itemName);
            builder.Append(" x");
            builder.Append(stack.amount);

            if (stack.item.price > 0)
            {
                builder.Append(" - ");
                builder.Append(stack.item.price);
                builder.Append(" LT");
            }

            builder.AppendLine();
        }

        string result =
            builder.ToString().TrimEnd();

        return string.IsNullOrEmpty(result)
            ? emptyText
            : result;
    }

    string GetNpcName(Transform npc)
    {
        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        NpcData npcData =
            npc.GetComponent<NpcData>();

        if (npcData != null)
        {
            return npcData.npcName;
        }

        return npc.name;
    }

    void ConfigureRaycasts()
    {
        Graphic[] graphics =
            GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
            {
                continue;
            }

            graphic.raycastTarget = blockMapDrag;
        }

        CanvasGroup group =
            GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.blocksRaycasts = blockMapDrag;
        group.interactable = blockMapDrag;
    }
}
