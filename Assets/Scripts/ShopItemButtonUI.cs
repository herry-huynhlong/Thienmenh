using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemButtonUI : MonoBehaviour
{
    public Button button;
    public Image iconImage;
    public TMP_Text amountText;
    public TMP_Text nameText;
    public TMP_Text priceText;

    int itemIndex;
    ShopPanelUI owner;

    public void Setup(
        ShopPanelUI newOwner,
        int newItemIndex,
        ShopItemSlot slot)
    {
        owner = newOwner;
        itemIndex = newItemIndex;

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (iconImage != null)
        {
            iconImage.sprite = slot.item.icon;
            iconImage.enabled = slot.item.icon != null;
        }

        if (amountText != null)
        {
            amountText.text =
                slot.amount > 99
                ? "99+"
                : slot.amount.ToString();
        }

        if (nameText != null)
        {
            nameText.text = slot.item.itemName;
        }

        if (priceText != null)
        {
            priceText.text = slot.item.price + " LT";
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Select);
            button.interactable = slot.amount > 0;
        }
    }

    void Select()
    {
        if (owner != null)
        {
            owner.SelectItem(itemIndex);
        }
    }
}
