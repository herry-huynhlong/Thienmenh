using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemButtonUI :
    MonoBehaviour,
    IPointerClickHandler,
    IPointerDownHandler
{
    public Button button;
    public Image backgroundImage;
    public Image iconImage;
    public TMP_Text amountText;
    public TMP_Text nameText;
    public TMP_Text priceText;

    [Header("Icon Layout")]
    public bool autoScaleIcon = true;
    public float iconSizeRatio = 0.48f;
    public Vector2 iconSize = new Vector2(72f, 72f);
    public Vector2 iconOffset = new Vector2(0f, -12f);
    public Vector2 iconPivot = new Vector2(0.5f, 1f);

    [Header("Name Layout")]
    public bool autoScaleNameFont = true;
    public float nameFontSizeRatio = 0.11f;
    public float nameFontSize = 22f;
    public Vector2 nameOffset = new Vector2(0f, 10f);
    public float nameHeight = 42f;

    [Header("Amount Layout")]
    public bool autoScaleAmountFont = true;
    public float amountFontSizeRatio = 0.1f;
    public float amountFontSize = 20f;
    public Vector2 amountOffset = new Vector2(-10f, -10f);
    public Vector2 amountSize = new Vector2(48f, 30f);

    int itemIndex;
    InventoryPanelUI owner;
    float lastSelectTime = -1f;

    public bool HasItem
    {
        get
        {
            return itemIndex >= 0;
        }
    }

    public int ItemIndex
    {
        get
        {
            return itemIndex;
        }
    }

    public void Setup(
        InventoryPanelUI newOwner,
        int newItemIndex,
        ItemStack stack)
    {
        owner = newOwner;
        itemIndex = newItemIndex;

        AutoFindReferences();

        EnsureClickable();
        ConfigureChildLayout();

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
        DisableChildRaycastTargets();
        HookButtonClick();

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(true);
            iconImage.sprite = stack.item.icon;
            iconImage.enabled = stack.item.icon != null;
            iconImage.raycastTarget = false;
        }

        if (amountText != null)
        {
            amountText.gameObject.SetActive(true);
            amountText.text =
                stack.amount > 99
                ? "99+"
                : "x" + stack.amount;

            amountText.alignment =
                TextAlignmentOptions.TopRight;

            amountText.raycastTarget = false;
        }

        if (nameText != null)
        {
            nameText.gameObject.SetActive(false);
            nameText.text = "";
            nameText.raycastTarget = false;
        }

        if (priceText != null)
        {
            priceText.gameObject.SetActive(false);
            priceText.raycastTarget = false;
        }

        HideNonAmountTexts();
    }

    public void SetupEmpty(
        InventoryPanelUI newOwner)
    {
        owner = newOwner;
        itemIndex = -1;

        AutoFindReferences();

        EnsureClickable();
        ConfigureChildLayout();

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
        DisableChildRaycastTargets();
        HookButtonClick();

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(false);
            iconImage.raycastTarget = false;
        }

        if (amountText != null)
        {
            amountText.gameObject.SetActive(false);
            amountText.text = "";
            amountText.raycastTarget = false;
        }

        if (nameText != null)
        {
            nameText.gameObject.SetActive(false);
            nameText.text = "";
            nameText.raycastTarget = false;
        }

        if (priceText != null)
        {
            priceText.gameObject.SetActive(false);
            priceText.text = "";
            priceText.raycastTarget = false;
        }

        HideNonAmountTexts();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Select();
    }

    void Select()
    {
        if (Time.unscaledTime - lastSelectTime < 0.15f)
        {
            return;
        }

        lastSelectTime = Time.unscaledTime;

        if (owner != null)
        {
            owner.SelectItemButton(itemIndex);
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

        if (iconImage == null)
        {
            Transform icon =
                transform.Find("icon");

            if (icon == null)
            {
                icon = transform.Find("Icon");
            }

            if (icon != null)
            {
                iconImage = icon.GetComponent<Image>();
            }
        }

        if (amountText == null)
        {
            amountText = FindText("AmountText");
        }

        if (nameText == null)
        {
            nameText = FindText("NameText");
        }

        if (priceText == null)
        {
            priceText = FindText("PriceText");
        }
    }

    TMP_Text FindText(string childName)
    {
        Transform child =
            transform.Find(childName);

        if (child != null)
        {
            return child.GetComponent<TMP_Text>();
        }

        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null &&
                text.name == childName)
            {
                return text;
            }
        }

        return null;
    }

    void HideNonAmountTexts()
    {
        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            text.raycastTarget = false;

            if (text == amountText)
            {
                continue;
            }

            text.text = "";
            text.gameObject.SetActive(false);
        }
    }

    void EnsureClickable()
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

        button.targetGraphic = backgroundImage;
        button.interactable = true;
    }

    void HookButtonClick()
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Select);
    }

    void ConfigureChildLayout()
    {
        ConfigureIconLayout();
        ConfigureNameLayout();
        ConfigureAmountLayout();
    }

    void ConfigureIconLayout()
    {
        if (iconImage == null)
        {
            return;
        }

        RectTransform itemRect =
            GetComponent<RectTransform>();

        RectTransform rect =
            iconImage.GetComponent<RectTransform>();

        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = iconPivot;
        rect.anchoredPosition = iconOffset;

        Vector2 resolvedIconSize =
            autoScaleIcon
            ? Vector2.one * GetIconSize(itemRect)
            : iconSize;

        rect.sizeDelta = resolvedIconSize;

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    void ConfigureNameLayout()
    {
        if (nameText == null)
        {
            return;
        }

        RectTransform itemRect =
            GetComponent<RectTransform>();

        RectTransform rect =
            nameText.GetComponent<RectTransform>();

        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = nameOffset;

        rect.sizeDelta =
            new Vector2(
                -GetPadding(itemRect) * 2f,
                autoScaleNameFont
                ? GetNameHeight(itemRect)
                : nameHeight);

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        nameText.alignment = TextAlignmentOptions.Bottom;
        nameText.enableWordWrapping = true;
        nameText.fontSize =
            autoScaleNameFont
            ? GetNameFontSize(itemRect)
            : nameFontSize;
    }

    void ConfigureAmountLayout()
    {
        if (amountText == null)
        {
            return;
        }

        RectTransform itemRect =
            GetComponent<RectTransform>();

        RectTransform rect =
            amountText.GetComponent<RectTransform>();

        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = amountOffset;

        rect.sizeDelta =
            autoScaleAmountFont
            ? new Vector2(
                GetAmountWidth(itemRect),
                GetAmountHeight(itemRect))
            : amountSize;

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        amountText.alignment = TextAlignmentOptions.TopRight;
        amountText.fontSize =
            autoScaleAmountFont
            ? GetAmountFontSize(itemRect)
            : amountFontSize;
    }

    float GetPadding(RectTransform itemRect)
    {
        if (itemRect == null)
        {
            return 8f;
        }

        return Mathf.Clamp(
            Mathf.Min(itemRect.rect.width, itemRect.rect.height) * 0.08f,
            6f,
            18f);
    }

    float GetIconSize(RectTransform itemRect)
    {
        if (itemRect == null)
        {
            return 48f;
        }

        return Mathf.Clamp(
            Mathf.Min(itemRect.rect.width, itemRect.rect.height) *
                iconSizeRatio,
            48f,
            120f);
    }

    float GetNameHeight(RectTransform itemRect)
    {
        if (itemRect == null)
        {
            return 32f;
        }

        return Mathf.Clamp(
            itemRect.rect.height * 0.28f,
            32f,
            72f);
    }

    float GetNameFontSize(RectTransform itemRect)
    {
        if (itemRect == null)
        {
            return 18f;
        }

        return Mathf.Clamp(
            itemRect.rect.height * nameFontSizeRatio,
            18f,
            32f);
    }

    float GetAmountWidth(RectTransform itemRect)
    {
        if (itemRect == null)
        {
            return 32f;
        }

        return Mathf.Clamp(
            itemRect.rect.width * 0.35f,
            32f,
            80f);
    }

    float GetAmountHeight(RectTransform itemRect)
    {
        if (itemRect == null)
        {
            return 24f;
        }

        return Mathf.Clamp(
            itemRect.rect.height * 0.18f,
            24f,
            48f);
    }

    float GetAmountFontSize(RectTransform itemRect)
    {
        if (itemRect == null)
        {
            return 18f;
        }

        return Mathf.Clamp(
            itemRect.rect.height * amountFontSizeRatio,
            18f,
            28f);
    }

    void DisableChildRaycastTargets()
    {
        Graphic[] graphics =
            GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic == backgroundImage)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        AutoFindReferences();
    }
#endif
}

