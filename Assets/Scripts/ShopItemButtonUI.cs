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
    public Image auraGlowImage;
    public Image energyRingImage;
    public TMP_Text amountText;
    public TMP_Text nameText;
    public TMP_Text descText;
    public TMP_Text priceText;
    public Button buyButton;
    public TMP_Text buyButtonText;
    public Color priceColor = new Color(0.76f, 0.50f, 0.10f, 1f);
    Outline iconBgOutline;
    Shadow iconBgShadow;
    Outline gradeBorderOutline;
    Vector3 auraGlowBaseScale = Vector3.one;
    Vector3 energyRingBaseScale = Vector3.one;
    Vector3 auraGlowBaseEuler = Vector3.zero;
    Vector3 energyRingBaseEuler = Vector3.zero;
    bool rarityBaseCaptured;
    bool hasCustomGradeFrame;

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
        EnsureGradeEffects();

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
            ApplyPriceStyle();
        }

        ApplyGradeFrame(
            slot.item.grade);
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
                hasCustomGradeFrame
                    ? Color.white
                    : GetGradeBorderColor(
                        currentSlot.item.grade,
                        pulse);
        }

        if (iconBgImage != null)
        {
            iconBgImage.color =
                GetGradeBackgroundColor(
                    currentSlot.item.grade,
                    pulse);
        }

        ApplyRarityVisuals(
            currentSlot.item.grade,
            pulse);

        ApplyGradeEffects(
            currentSlot.item.grade,
            pulse);

        ApplyGradeNameStyle(
            currentSlot.item.grade,
            pulse);
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
                GetGradeBackgroundColor(
                    grade,
                    0.72f);
        }

        if (gradeBorderImage != null)
        {
            gradeBorderImage.color =
                hasCustomGradeFrame
                    ? Color.white
                    : GetGradeBorderColor(
                        grade,
                        0.72f);
            gradeBorderImage.raycastTarget = false;
        }

        ApplyRarityVisuals(
            grade,
            0.72f);

        ApplyGradeEffects(
            grade,
            0.72f);

        ApplyGradeNameStyle(
            grade,
            0.72f);

        if (descText != null)
        {
            descText.enableVertexGradient = false;
            descText.color = new Color(0.36f, 0.28f, 0.18f, 1f);
        }
    }

    Color GetGradeBorderColor(
        ItemGrade grade,
        float pulse)
    {
        Color baseColor =
            ShopPanelUI.GetGradeBaseColor(grade);
        Color accentColor =
            ShopPanelUI.GetGradeAccentColor(grade);
        Color vivid =
            Color.Lerp(
                baseColor,
                accentColor,
                0.34f + Mathf.Clamp01(pulse) * 0.18f);
        Color rim =
            Color.Lerp(
                vivid,
                Color.white,
                0.16f + Mathf.Clamp01(pulse) * 0.22f);
        rim.a = 1f;
        return rim;
    }

    Color GetGradeBackgroundColor(
        ItemGrade grade,
        float pulse)
    {
        Color baseColor =
            ShopPanelUI.GetGradeBaseColor(grade);
        Color accentColor =
            ShopPanelUI.GetGradeAccentColor(grade);
        Color mixed =
            Color.Lerp(
                baseColor,
                accentColor,
                0.58f + Mathf.Clamp01(pulse) * 0.16f);

        Color.RGBToHSV(
            mixed,
            out float hue,
            out float saturation,
            out float value);

        saturation = Mathf.Clamp01(saturation + 0.22f);
        value = Mathf.Clamp01(value * 0.82f);

        Color vivid =
            Color.HSVToRGB(hue, saturation, value);
        vivid.a = 0.97f;
        return vivid;
    }

    Color GetRarityAuraColor(
        ItemGrade grade,
        float pulse)
    {
        Color baseColor =
            ShopPanelUI.GetGradeBaseColor(grade);
        Color accentColor =
            ShopPanelUI.GetGradeAccentColor(grade);
        Color mixed =
            Color.Lerp(
                baseColor,
                accentColor,
                0.40f + Mathf.Clamp01(pulse) * 0.22f);

        Color.RGBToHSV(
            mixed,
            out float hue,
            out float saturation,
            out float value);

        saturation = Mathf.Clamp01(saturation + 0.18f);
        value = Mathf.Clamp01(value + 0.16f);

        Color aura =
            Color.HSVToRGB(hue, saturation, value);
        aura.a = 0.22f + Mathf.Clamp01(pulse) * 0.18f;
        return aura;
    }

    Color GetRarityEnergyColor(
        ItemGrade grade,
        float pulse)
    {
        Color baseColor =
            ShopPanelUI.GetGradeBaseColor(grade);
        Color accentColor =
            ShopPanelUI.GetGradeAccentColor(grade);
        Color mixed =
            Color.Lerp(
                baseColor,
                accentColor,
                0.68f + Mathf.Clamp01(pulse) * 0.14f);

        Color.RGBToHSV(
            mixed,
            out float hue,
            out float saturation,
            out float value);

        saturation = Mathf.Clamp01(saturation + 0.34f);
        value = Mathf.Clamp01(value + 0.24f);

        Color energy =
            Color.HSVToRGB(hue, saturation, value);
        energy.a = 0.38f + Mathf.Clamp01(pulse) * 0.22f;
        return energy;
    }

    static float GetRarityStrength(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return 0.16f;
            case ItemGrade.Trung:
                return 0.30f;
            case ItemGrade.Thuong:
                return 0.52f;
            case ItemGrade.Tien:
                return 0.72f;
            default:
                return 0.2f;
        }
    }

    static float GetRarityRotationAmplitude(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return 4.0f;
            case ItemGrade.Trung:
                return 9.0f;
            case ItemGrade.Thuong:
                return 15.0f;
            case ItemGrade.Tien:
                return 22.0f;
            default:
                return 6.0f;
        }
    }

    static float GetRaritySpinSpeed(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return 0.90f;
            case ItemGrade.Trung:
                return 1.20f;
            case ItemGrade.Thuong:
                return 1.55f;
            case ItemGrade.Tien:
                return 1.95f;
            default:
                return 1.0f;
        }
    }

    void CaptureRarityBaseState()
    {
        if (auraGlowImage != null)
        {
            auraGlowBaseScale = auraGlowImage.transform.localScale;
            auraGlowBaseEuler = auraGlowImage.transform.localEulerAngles;
            auraGlowImage.raycastTarget = false;
        }

        if (energyRingImage != null)
        {
            energyRingBaseScale = energyRingImage.transform.localScale;
            energyRingBaseEuler = energyRingImage.transform.localEulerAngles;
            energyRingImage.raycastTarget = false;
        }

        rarityBaseCaptured = true;
    }

    void EnsureGradeEffects()
    {
        if (iconBgImage != null)
        {
            iconBgOutline =
                iconBgImage.GetComponent<Outline>();
            if (iconBgOutline == null)
            {
                iconBgOutline =
                    iconBgImage.gameObject.AddComponent<Outline>();
            }

            iconBgShadow =
                iconBgImage.GetComponent<Shadow>();
            if (iconBgShadow == null)
            {
                iconBgShadow =
                    iconBgImage.gameObject.AddComponent<Shadow>();
            }
        }

        if (gradeBorderImage != null)
        {
            gradeBorderOutline =
                gradeBorderImage.GetComponent<Outline>();
            if (gradeBorderOutline == null)
            {
                gradeBorderOutline =
                    gradeBorderImage.gameObject.AddComponent<Outline>();
            }
        }

        if (!rarityBaseCaptured &&
            (auraGlowImage != null || energyRingImage != null))
        {
            CaptureRarityBaseState();
        }
    }

    void ApplyGradeEffects(
        ItemGrade grade,
        float pulse)
    {
        Color baseColor =
            ShopPanelUI.GetGradeBaseColor(grade);
        Color accentColor =
            ShopPanelUI.GetGradeAccentColor(grade);
        float glow =
            0.32f + Mathf.Clamp01(pulse) * 0.34f;

        if (iconBgOutline != null)
        {
            Color outlineColor =
                Color.Lerp(
                    accentColor,
                    Color.white,
                    0.62f);
            outlineColor =
                Color.Lerp(
                    outlineColor,
                    baseColor,
                    0.18f);
            outlineColor.a = 0.94f;
            iconBgOutline.effectColor = outlineColor;
            iconBgOutline.effectDistance =
                new Vector2(2.9f, 2.9f);
            iconBgOutline.useGraphicAlpha = true;
        }

        if (iconBgShadow != null)
        {
            Color shadowColor =
                Color.Lerp(
                    new Color(0.10f, 0.07f, 0.04f, 1f),
                    baseColor,
                    0.22f);
            shadowColor.a = 0.48f;
            iconBgShadow.effectColor = shadowColor;
            iconBgShadow.effectDistance =
                new Vector2(2.2f, -2.0f);
            iconBgShadow.useGraphicAlpha = true;
        }

        if (gradeBorderOutline != null)
        {
            Color borderGlow =
                Color.Lerp(
                    accentColor,
                    Color.white,
                    0.26f + glow * 0.18f);
            borderGlow.a = 0.96f;
            gradeBorderOutline.effectColor = borderGlow;
            gradeBorderOutline.effectDistance =
                new Vector2(1.8f, 1.8f);
            gradeBorderOutline.useGraphicAlpha = true;
        }
    }

    void ApplyRarityVisuals(
        ItemGrade grade,
        float pulse)
    {
        if (auraGlowImage == null &&
            energyRingImage == null)
        {
            return;
        }

        if (!rarityBaseCaptured)
        {
            CaptureRarityBaseState();
        }

        float strength =
            GetRarityStrength(grade);
        float spinSpeed =
            GetRaritySpinSpeed(grade);
        float amplitude =
            GetRarityRotationAmplitude(grade);
        float time =
            Time.unscaledTime + pulseSeed;

        if (auraGlowImage != null)
        {
            auraGlowImage.color =
                GetRarityAuraColor(grade, pulse);

            float rotation =
                Mathf.Sin(time * spinSpeed) *
                amplitude;
            float auraScale =
                1f + strength * (0.03f + pulse * 0.04f);
            auraGlowImage.transform.localRotation =
                Quaternion.Euler(
                    auraGlowBaseEuler.x,
                    auraGlowBaseEuler.y,
                    auraGlowBaseEuler.z + rotation);
            auraGlowImage.transform.localScale =
                new Vector3(
                    auraGlowBaseScale.x * auraScale,
                    auraGlowBaseScale.y * auraScale,
                    auraGlowBaseScale.z);
        }

        if (energyRingImage != null)
        {
            energyRingImage.color =
                GetRarityEnergyColor(grade, pulse);

            float rotation =
                Mathf.Sin(
                    time * (spinSpeed * 1.18f) + 1.7f) *
                amplitude *
                1.08f;
            float energyScale =
                1f + strength * (0.02f + pulse * 0.03f);
            energyRingImage.transform.localRotation =
                Quaternion.Euler(
                    energyRingBaseEuler.x,
                    energyRingBaseEuler.y,
                    energyRingBaseEuler.z - rotation);
            energyRingImage.transform.localScale =
                new Vector3(
                    energyRingBaseScale.x * energyScale,
                    energyRingBaseScale.y * energyScale,
                    energyRingBaseScale.z);
        }
    }

    void ApplyPriceStyle()
    {
        if (priceText == null)
        {
            return;
        }

        priceText.color = priceColor;
        priceText.fontStyle |= FontStyles.Bold;
        priceText.enableVertexGradient = true;
        priceText.colorGradient =
            new VertexGradient(
                Color.Lerp(priceColor, Color.white, 0.14f),
                Color.Lerp(priceColor, Color.white, 0.14f),
                Color.Lerp(priceColor, new Color(0.34f, 0.20f, 0.05f, 1f), 0.24f),
                Color.Lerp(priceColor, new Color(0.34f, 0.20f, 0.05f, 1f), 0.24f));
        priceText.outlineWidth = 0.2f;
        priceText.outlineColor =
            new Color(0.28f, 0.14f, 0.03f, 0.82f);
    }

    void ApplyGradeNameStyle(
        ItemGrade grade,
        float pulse)
    {
        if (nameText == null)
        {
            return;
        }

        ShopPanelUI.ApplyGradeTextStyle(
            nameText,
            grade,
            0.58f + Mathf.Clamp01(pulse) * 0.42f);
    }

    void ApplyGradeFrame(
        ItemGrade grade)
    {
        if (gradeBorderImage == null)
        {
            hasCustomGradeFrame = false;
            return;
        }

        Sprite frame =
            ItemGradeFrameLibrary.GetFrame(grade);
        hasCustomGradeFrame =
            frame != null;

        if (!hasCustomGradeFrame)
        {
            return;
        }

        gradeBorderImage.sprite = frame;
        gradeBorderImage.enabled = true;
        gradeBorderImage.color = Color.white;
        gradeBorderImage.preserveAspect = false;
        gradeBorderImage.raycastTarget = false;
    }

    string BuildShortDescription(StatItemData item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        return ItemText.GradeLong(item.grade);
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

        if (auraGlowImage == null)
        {
            auraGlowImage =
                FindImage("auraGlow") ??
                FindImage("AuraGlow") ??
                FindImage("RarityAura") ??
                FindImage("Aura");
        }

        if (energyRingImage == null)
        {
            energyRingImage =
                FindImage("energyRing") ??
                FindImage("EnergyRing") ??
                FindImage("RarityEnergy") ??
                FindImage("Energy");
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
