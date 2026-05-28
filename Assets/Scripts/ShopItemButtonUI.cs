using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopItemButtonUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
{
    public Button button;
    public Image backgroundImage;
    public Image iconImage;
    public TMP_Text amountText;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public Color priceColor = new Color(1f, 0.82f, 0.18f, 1f);

    int itemIndex;
    ShopPanelUI owner;

    public int ItemIndex => itemIndex;

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

        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, 0f);
        }

        backgroundImage.raycastTarget = true;

        Graphic[] childGraphics =
            GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in childGraphics)
        {
            if (graphic == backgroundImage)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }

        button.targetGraphic = backgroundImage;

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

            RectTransform amountRect =
                amountText.GetComponent<RectTransform>();

            if (amountRect != null)
            {
                amountRect.anchorMin =
                    new Vector2(1f, 1f);

                amountRect.anchorMax =
                    new Vector2(1f, 1f);

                amountRect.pivot =
                    new Vector2(1f, 1f);

                amountRect.anchoredPosition =
                    new Vector2(-6f, -6f);
            }

            amountText.alignment =
                TextAlignmentOptions.TopRight;
        }

        if (nameText != null)
        {
            nameText.text = slot.item.itemName;
        }

        if (priceText != null)
        {
            priceText.text =
                NpcEconomy.FormatTradePrice(
                    slot.item,
                    NpcTradeContext.MarketBuy);
            priceText.color = priceColor;
            priceText.fontStyle |= FontStyles.Bold;
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

    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Select();
    }
}
