using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopItemButtonUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
{
    public Button button;
    public Image backgroundImage;
    public Image iconImage;
    public Image iconBgImage;
    public Image gradeBorderImage;
    public TMP_Text amountText;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text priceText;
    public Button buyButton;
    public TMP_Text buyButtonText;
    public Color priceColor = new Color(1f, 0.82f, 0.18f, 1f);

    int itemIndex;
    ShopPanelUI owner;
    ShopItemSlot currentSlot;
    float pulseSeed;

    public int ItemIndex => itemIndex;

    public void Setup(
        ShopPanelUI newOwner,
        int newItemIndex,
        ShopItemSlot slot)
    {
        owner = newOwner;
        itemIndex = newItemIndex;
        currentSlot = slot;
        pulseSeed =
            Mathf.Abs((GetInstanceID() ^ newItemIndex) * 0.173f);

        AutoFindReferences();
        EnsureRootClickable();
        ConfigureRaycastTargets();
        NormalizeRootRect();

        if (iconImage != null)
        {
            iconImage.sprite = slot.item.icon;
            iconImage.enabled = slot.item.icon != null;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }

        if (slot.item.icon == null)
        {
            Debug.LogWarning(
                "[ShopItemButtonUI] Missing icon for item=" +
                ItemText.Name(slot.item) +
                " asset=" + slot.item.name);
        }

        if (amountText != null)
        {
            amountText.text =
                slot.amount > 99
                ? "99+"
                : slot.amount.ToString();
            amountText.alignment =
                TextAlignmentOptions.TopRight;
        }

        if (nameText != null)
        {
            nameText.text = ItemText.Name(slot.item);
        }

        if (descText != null)
        {
            descText.text = BuildShortDescription(slot.item);
            descText.fontStyle &= ~FontStyles.Bold;
        }

        if (priceText != null)
        {
            priceText.text = owner != null
                ? owner.FormatDisplayPrice(slot.item)
                : NpcEconomy.FormatTradePrice(
                    slot.item,
                    NpcTradeContext.MarketBuy);
            priceText.color = priceColor;
            priceText.fontStyle |= FontStyles.Bold;
        }

        ApplyGradeVisuals(slot.item.grade);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Select);
            button.interactable = slot.amount > 0;
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuyInline);
        }

        RefreshAffordability();
    }

    void Update()
    {
        if (currentSlot == null ||
            currentSlot.item == null)
        {
            return;
        }

        float pulse =
            0.68f +
            0.32f * Mathf.Sin(
                Time.unscaledTime * 3.2f +
                pulseSeed);

        if (gradeBorderImage != null)
        {
            gradeBorderImage.color =
                Color.Lerp(
                    ShopPanelUI.GetGradeBaseColor(currentSlot.item.grade),
                    ShopPanelUI.GetGradeAccentColor(currentSlot.item.grade),
                    0.35f + pulse * 0.25f);
        }
    }

    public void RefreshAffordability()
    {
        bool hasStock =
            currentSlot != null &&
            currentSlot.item != null &&
            currentSlot.amount > 0;
        bool canAfford =
            owner != null &&
            currentSlot != null &&
            currentSlot.item != null &&
            owner.CanAffordDisplayedPrice(currentSlot.item);
        bool canTradeNormally =
            currentSlot != null &&
            currentSlot.item != null &&
            NpcEconomy.CanTradeNormally(currentSlot.item);

        if (buyButton != null)
        {
            buyButton.interactable =
                hasStock &&
                canAfford &&
                canTradeNormally;
        }

        if (buyButtonText != null)
        {
            buyButtonText.text =
                !canTradeNormally
                    ? UiText.Get("shop", "buyButtonUnavailable")
                    : canAfford
                        ? UiText.Get("shop", "buyButtonBuy")
                        : UiText.Get("shop", "buyButtonNotEnough");
        }
    }

    void BuyInline()
    {
        if (owner != null)
        {
            owner.TryBuyItemFromInlineButton(itemIndex);
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
        if (IsInlineBuyPointer(eventData))
        {
            return;
        }

        Select();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsInlineBuyPointer(eventData))
        {
            return;
        }

        Select();
    }

    bool IsInlineBuyPointer(PointerEventData eventData)
    {
        return eventData != null &&
            buyButton != null &&
            eventData.pointerPressRaycast.gameObject != null &&
            eventData.pointerPressRaycast.gameObject.transform.IsChildOf(
                buyButton.transform);
    }

    void ApplyGradeVisuals(ItemGrade grade)
    {
        if (iconBgImage != null)
        {
            iconBgImage.color =
                new Color(1f, 1f, 1f, 0.96f);
        }

        if (gradeBorderImage != null)
        {
            gradeBorderImage.color =
                ShopPanelUI.GetGradeBaseColor(grade);
            gradeBorderImage.raycastTarget = false;
        }

        if (descText != null)
        {
            descText.enableVertexGradient = false;
            descText.color = new Color(0.36f, 0.28f, 0.18f, 1f);
        }
    }

    string BuildShortDescription(StatItemData item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string localizedDescription =
            ItemText.Description(item);
        if (!string.IsNullOrWhiteSpace(localizedDescription))
        {
            string compact =
                localizedDescription
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Trim();
            int cutIndex = compact.IndexOf('.');
            if (cutIndex > 0)
            {
                compact =
                    compact.Substring(0, cutIndex + 1);
            }

            if (compact.Length > 42)
            {
                compact =
                    compact.Substring(0, 39).TrimEnd() + "...";
            }

            return compact;
        }

        switch (item.GetResolvedUseStyle())
        {
            case ItemUseStyle.Consumable:
                return ItemText.Get("shopCard", "useConsumable", "Vat pham tieu hao");
            case ItemUseStyle.RawMaterial:
                return ItemText.Get("shopCard", "useRawMaterial", "Nguyen lieu luyen che");
            case ItemUseStyle.DurableEquipment:
                return ItemText.Get("shopCard", "useEquipment", "Trang bi su dung lau dai");
            case ItemUseStyle.StudyManual:
                return ItemText.Get("shopCard", "useManual", "Cong phap de tham ngo");
            default:
                return ItemText.Type(item.itemType);
        }
    }

    void AutoFindReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = FindImage("iconFrame");
        }

        if (iconImage == null)
        {
            iconImage =
                FindImage("icon") ??
                FindImage("Icon");
        }

        if (iconBgImage == null)
        {
            iconBgImage =
                FindImage("iconbg") ??
                FindImage("IconBg");
        }

        if (gradeBorderImage == null)
        {
            gradeBorderImage =
                FindImage("gradeborder") ??
                FindImage("GradeBorder");
        }

        if (amountText == null)
        {
            amountText = FindText("AmountText");
        }

        if (nameText == null)
        {
            nameText = FindText("NameText");
        }

        if (descText == null)
        {
            descText =
                FindText("DescText") ??
                FindText("GradeText");
        }

        if (priceText == null)
        {
            priceText = FindText("PriceText");
        }

        if (buyButton == null)
        {
            buyButton =
                FindButton("Button Mua") ??
                FindButton("ButtonMua") ??
                FindButton("BuyButton");
        }

        if (buyButtonText == null &&
            buyButton != null)
        {
            buyButtonText =
                buyButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    void EnsureRootClickable()
    {
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
        button.targetGraphic = backgroundImage;
    }

    void NormalizeRootRect()
    {
        RectTransform rect =
            transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition3D = Vector3.zero;
    }

    void ConfigureRaycastTargets()
    {
        Graphic[] childGraphics =
            GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < childGraphics.Length; i++)
        {
            Graphic graphic = childGraphics[i];
            if (graphic == null)
            {
                continue;
            }

            bool isInteractive =
                graphic == backgroundImage ||
                (buyButton != null &&
                graphic.transform.IsChildOf(buyButton.transform));
            graphic.raycastTarget = isInteractive;
        }
    }

    TMP_Text FindText(string childName)
    {
        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);
        string normalizedName =
            NormalizeNodeName(childName);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (string.Equals(
                    text.name.Trim(),
                    childName,
                    StringComparison.OrdinalIgnoreCase) ||
                NormalizeNodeName(text.name) == normalizedName)
            {
                return text;
            }
        }

        return null;
    }

    Image FindImage(string childName)
    {
        Image[] images =
            GetComponentsInChildren<Image>(true);
        string normalizedName =
            NormalizeNodeName(childName);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            if (string.Equals(
                    image.name.Trim(),
                    childName,
                    StringComparison.OrdinalIgnoreCase) ||
                NormalizeNodeName(image.name) == normalizedName)
            {
                return image;
            }
        }

        return null;
    }

    Button FindButton(string childName)
    {
        Button[] buttons =
            GetComponentsInChildren<Button>(true);
        string normalizedName =
            NormalizeNodeName(childName);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null ||
                candidate == button)
            {
                continue;
            }

            if (string.Equals(
                    candidate.name.Trim(),
                    childName,
                    StringComparison.OrdinalIgnoreCase) ||
                NormalizeNodeName(candidate.name) == normalizedName)
            {
                return candidate;
            }
        }

        return null;
    }

    static string NormalizeNodeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder =
            new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}
