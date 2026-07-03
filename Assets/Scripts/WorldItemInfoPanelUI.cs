using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WorldItemInfoPanelUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject panelRoot;

    public RectTransform panelRect;

    [Header("Content")]
    public TMP_Text nameText;

    public TMP_Text qualityText;

    public TMP_Text quickInfoText;

    public TMP_Text motaText;

    public TMP_Text infoText;

    public TMP_Text extraInfoText;

    public TMP_Text loreQuoteText;

    public Image panelIcon;

    [Header("Cards")]
    public CardBinding valueCard = new CardBinding();

    public CardBinding sourceCard = new CardBinding();

    public CardBinding usageCard = new CardBinding();

    [System.Serializable]
    public class CardBinding
    {
        public GameObject root;
        public TMP_Text titleText;
        public TMP_Text valueText;
        public Image icon;
    }

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        EnsureReferences();
    }

    public void EnsureReferences()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (panelRoot == null)
        {
            return;
        }

        if (panelRect == null)
        {
            panelRect = panelRoot.GetComponent<RectTransform>();
        }

        if (panelIcon == null)
        {
            panelIcon = FindImage(
                panelRoot.transform,
                "ItemIcon",
                "InfoIcon",
                "IconImage");
        }

        nameText = EnsureText(
            nameText,
            panelRoot.transform,
            "ItemNameText",
            "NameText",
            "TitleText");

        qualityText = EnsureText(
            qualityText,
            panelRoot.transform,
            "Quality");

        quickInfoText = EnsureText(
            quickInfoText,
            panelRoot.transform,
            "QuickInfo");

        motaText = EnsureText(
            motaText,
            panelRoot.transform,
            "mota",
            "DescriptionText",
            "Text (TMP)");

        infoText = EnsureText(
            infoText,
            panelRoot.transform,
            "InfoText",
            "ItemInfoText");

        extraInfoText = EnsureText(
            extraInfoText,
            panelRoot.transform,
            "ExtraInfo");

        loreQuoteText = EnsureText(
            loreQuoteText,
            panelRoot.transform,
            "LoreQuote");

        EnsureCardReferences(ref valueCard, panelRoot.transform, "ValueCard");
        EnsureCardReferences(ref sourceCard, panelRoot.transform, "SourceCard");
        EnsureCardReferences(ref usageCard, panelRoot.transform, "UsageCard");

        if (panelIcon != null)
        {
            panelIcon.raycastTarget = false;
            panelIcon.preserveAspect = true;
        }
    }

    public void Show(WorldStatItemPickup pickup)
    {
        EnsureReferences();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (pickup == null ||
            pickup.item == null)
        {
            Hide();
            return;
        }

        ApplyItemData(pickup);
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    void ApplyItemData(WorldStatItemPickup pickup)
    {
        StatItemData item = pickup.item;

        SetText(nameText, ItemText.Name(item));
        SetText(qualityText, GetQualityText(item));
        SetText(quickInfoText, BuildQuickInfoText(item, pickup.amount));
        SetText(motaText, ItemText.Description(item));
        SetText(infoText, BuildInfoText(item, pickup.amount));
        SetText(extraInfoText, BuildExtraInfoText(item, pickup.amount));
        SetText(loreQuoteText, BuildLoreQuoteText(item));

        if (qualityText != null)
        {
            qualityText.color = GetGradeColor(item.grade);
        }

        SetCard(valueCard, "Giá trị", NpcEconomy.FormatCurrency(Mathf.Max(0, item.price)));
        SetCard(sourceCard, "Nguồn", BuildSourceText(item));
        SetCard(usageCard, "Cách dùng", BuildUsageText(item, pickup.amount));

        if (panelIcon != null)
        {
            Sprite icon = GetPickupIcon(pickup);
            panelIcon.sprite = icon;
            panelIcon.enabled = icon != null;
        }
    }

    static string GetQualityText(StatItemData item)
    {
        if (item == null)
        {
            return "";
        }

        switch (item.grade)
        {
            case ItemGrade.Trung:
                return "Trung phẩm";
            case ItemGrade.Thuong:
                return "Thượng phẩm";
            case ItemGrade.Tien:
                return "Tiên phẩm";
            default:
                return "Hạ phẩm";
        }
    }

    static Color GetGradeColor(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return new Color32(102, 196, 122, 255);
            case ItemGrade.Thuong:
                return new Color32(255, 206, 88, 255);
            case ItemGrade.Tien:
                return new Color32(196, 135, 255, 255);
            default:
                return new Color32(183, 183, 183, 255);
        }
    }

    static string BuildQuickInfoText(StatItemData item, int amount)
    {
        if (item == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(ItemText.Type(item.itemType));
        builder.Append(" · ");
        builder.Append(GetQualityText(item));

        if (amount > 1)
        {
            builder.Append(" · x");
            builder.Append(amount);
        }

        if (item.price > 0)
        {
            builder.Append(" · ");
            builder.Append(NpcEconomy.FormatCurrency(item.price));
        }

        return builder.ToString();
    }

    static string BuildInfoText(StatItemData item, int amount)
    {
        if (item == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Loại: " + ItemText.Type(item.itemType));
        builder.AppendLine("Phẩm chất: " + GetQualityText(item));
        builder.AppendLine("Số lượng: " + Mathf.Max(0, amount));
        return builder.ToString().TrimEnd();
    }

    static string BuildExtraInfoText(StatItemData item, int amount)
    {
        if (item == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Nguồn: " + BuildSourceText(item));
        builder.AppendLine("Cách dùng: " + BuildUsageSummaryText(item));

        switch (item.itemType)
        {
            case ItemType.DanDuoc:
                builder.AppendLine("HP: " + FormatSignedInt(item.hpBonus));
                builder.AppendLine("Tu vi: " + FormatSignedInt(item.cultivationBonus));
                builder.AppendLine("Đột phá: " + (item.breakthroughRealm ? "Có" : "Không"));
                break;
            case ItemType.PhapBao:
                builder.AppendLine("Công: " + FormatSignedInt(item.damageBonus));
                builder.AppendLine("Thủ: " + FormatSignedInt(item.armorBonus));
                builder.AppendLine("Kháng: " + FormatSignedInt(item.effectResistanceBonus));
                break;
            case ItemType.VatLieu:
                builder.AppendLine("Hiệu suất: " + FormatPercent(item.rawUseEfficiency));
                builder.AppendLine("Độc tính: " + FormatSignedInt(item.rawToxicityDamage));
                break;
            case ItemType.CongPhap:
                builder.AppendLine("Tiến độ học: " + item.studyProgressPerUse);
                builder.AppendLine("Bền công pháp: " + item.manualBreakAfterYears.ToString("0.##") + " năm");
                break;
            case ItemType.ThucPham:
                builder.AppendLine("Thời hạn: " + (item.isTemporary ? item.duration.ToString("0.##") + " giây" : "Dùng ngay"));
                break;
        }

        if (amount > 1)
        {
            builder.AppendLine("Số lượng: " + amount);
        }

        return builder.ToString().TrimEnd();
    }

    static string BuildLoreQuoteText(StatItemData item)
    {
        if (item == null)
        {
            return "";
        }

        string description = ItemText.Description(item);
        if (string.IsNullOrWhiteSpace(description))
        {
            return "";
        }

        string[] lines = description.Split('\n');
        string firstLine = lines.Length > 0 ? lines[0].Trim() : description.Trim();

        return firstLine.Length > 140
            ? firstLine.Substring(0, 137).TrimEnd() + "..."
            : firstLine;
    }

    static string BuildSourceText(StatItemData item)
    {
        if (item == null)
        {
            return "";
        }

        switch (item.itemType)
        {
            case ItemType.DanDuoc:
                return GetPillKindText(item.pillKind);
            case ItemType.PhapBao:
                return GetArtifactKindText(item.artifactKind);
            case ItemType.VatLieu:
                return GetMaterialKindText(item.materialKind);
            case ItemType.CongPhap:
                return GetManualKindText(item.manualKind);
            case ItemType.ThucPham:
                return GetFoodKindText(item.foodKind);
            default:
                return ItemText.Type(item.itemType);
        }
    }

    static string BuildUsageSummaryText(StatItemData item)
    {
        if (item == null)
        {
            return "";
        }

        return ItemText.UseStyle(item.GetResolvedUseStyle()) +
            " / " +
            ItemText.Target(item.validTargets);
    }

    static string BuildUsageText(StatItemData item, int amount)
    {
        if (item == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Dùng cho: " + ItemText.Target(item.validTargets));
        builder.AppendLine("Kiểu dùng: " + ItemText.UseStyle(item.GetResolvedUseStyle()));

        if (item.rawUsePolicy != RawUsePolicy.Allowed)
        {
            builder.AppendLine("Dùng sống: " + ItemText.RawUsePolicy(item.rawUsePolicy));
        }

        if (item.UsesDurability())
        {
            builder.AppendLine("Độ bền: " + item.GetMaxDurability());
        }

        if (amount > 1)
        {
            builder.AppendLine("Số lượng: " + amount);
        }

        return builder.ToString().TrimEnd();
    }

    static string FormatSignedInt(int value)
    {
        if (value == 0)
        {
            return "0";
        }

        return value > 0 ? "+" + value : value.ToString();
    }

    static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    static string GetPillKindText(PillKind kind)
    {
        switch (kind)
        {
            case PillKind.Heal:
                return "Hồi phục";
            case PillKind.Cultivation:
                return "Tu luyện";
            case PillKind.Breakthrough:
                return "Đột phá";
            case PillKind.PermanentAttack:
                return "Công kích vĩnh viễn";
            case PillKind.PermanentDefense:
                return "Phòng ngự vĩnh viễn";
            case PillKind.PermanentMaxHP:
                return "Sinh mệnh vĩnh viễn";
            case PillKind.Detox:
                return "Giải độc";
            case PillKind.Poison:
                return "Kịch độc";
            case PillKind.TribulationProtection:
                return "Hộ kiếp";
            default:
                return "Đan dược";
        }
    }

    static string GetArtifactKindText(ArtifactKind kind)
    {
        switch (kind)
        {
            case ArtifactKind.Sword:
                return "Kiếm";
            case ArtifactKind.Saber:
                return "Đao";
            case ArtifactKind.Spear:
                return "Thương";
            case ArtifactKind.Bow:
                return "Cung";
            case ArtifactKind.Staff:
                return "Trượng";
            case ArtifactKind.Armor:
                return "Giáp";
            case ArtifactKind.Robe:
                return "Pháp bào";
            case ArtifactKind.Shield:
                return "Thuẫn";
            case ArtifactKind.Ring:
                return "Nhẫn";
            case ArtifactKind.Amulet:
                return "Bội";
            case ArtifactKind.Talisman:
                return "Phù";
            default:
                return "Pháp bảo";
        }
    }

    static string GetMaterialKindText(MaterialKind kind)
    {
        switch (kind)
        {
            case MaterialKind.Herb:
                return "Linh thảo";
            case MaterialKind.Ore:
                return "Khoáng thạch";
            case MaterialKind.BeastCore:
                return "Yêu hạch";
            case MaterialKind.BeastPart:
                return "Thú liệu";
            case MaterialKind.SpiritStone:
                return "Linh thạch";
            case MaterialKind.CraftingPart:
                return "Bộ phận chế tạo";
            default:
                return "Vật liệu";
        }
    }

    static string GetManualKindText(ManualKind kind)
    {
        switch (kind)
        {
            case ManualKind.Attack:
                return "Công kích";
            case ManualKind.Defense:
                return "Phòng ngự";
            case ManualKind.Movement:
                return "Thân pháp";
            case ManualKind.Cultivation:
                return "Tu luyện";
            case ManualKind.Mixed:
                return "Tổng hợp";
            default:
                return "Công pháp";
        }
    }

    static string GetFoodKindText(FoodKind kind)
    {
        switch (kind)
        {
            case FoodKind.Meal:
                return "Món ăn";
            case FoodKind.Meat:
                return "Thịt";
            case FoodKind.Fish:
                return "Cá";
            case FoodKind.Grain:
                return "Ngũ cốc";
            case FoodKind.SpiritFruit:
                return "Linh quả";
            default:
                return "Thực phẩm";
        }
    }

    void SetCard(
        CardBinding card,
        string title,
        string value)
    {
        if (card == null)
        {
            return;
        }

        if (card.root != null)
        {
            card.root.SetActive(true);
        }

        SetText(card.titleText, title);
        SetText(card.valueText, value);

        if (card.icon != null)
        {
            card.icon.raycastTarget = false;
            card.icon.preserveAspect = true;
        }
    }

    static void EnsureCardReferences(
        ref CardBinding card,
        Transform root,
        string cardName)
    {
        if (card == null)
        {
            card = new CardBinding();
        }

        if (root == null)
        {
            return;
        }

        if (card.root == null)
        {
            Transform found = FindChildByName(root, cardName);
            if (found != null)
            {
                card.root = found.gameObject;
            }
        }

        if (card.root == null)
        {
            return;
        }

        Transform cardRoot = card.root.transform;

        if (card.titleText == null)
        {
            card.titleText = EnsureText(
                null,
                cardRoot,
                "Title",
                "CardTitle",
                "NameText");
        }

        if (card.valueText == null)
        {
            card.valueText = EnsureText(
                null,
                cardRoot,
                "Value",
                "Text",
                "ValueText");
        }

        if (card.icon == null)
        {
            card.icon = FindImage(cardRoot, "Icon");
        }
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text == null)
        {
            return;
        }

        text.text = value ?? string.Empty;
        text.gameObject.SetActive(!string.IsNullOrWhiteSpace(text.text));
    }

    static TMP_Text EnsureText(
        TMP_Text current,
        Transform root,
        params string[] names)
    {
        if (current != null ||
            root == null ||
            names == null)
        {
            return current;
        }

        foreach (string name in names)
        {
            Transform found = FindChildByName(root, name);
            if (found == null)
            {
                continue;
            }

            TMP_Text text = found.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = found.GetComponentInChildren<TMP_Text>(true);
            }

            if (text != null)
            {
                return text;
            }
        }

        return current;
    }

    static Image FindImage(
        Transform root,
        params string[] names)
    {
        if (root == null ||
            names == null)
        {
            return null;
        }

        foreach (string name in names)
        {
            Transform found = FindChildByName(root, name);
            if (found == null)
            {
                continue;
            }

            Image image = found.GetComponent<Image>();
            if (image == null)
            {
                image = found.GetComponentInChildren<Image>(true);
            }

            if (image != null)
            {
                return image;
            }
        }

        return null;
    }

    public static string BuildWorldItemInfo(WorldStatItemPickup pickup)
    {
        if (pickup == null ||
            pickup.item == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Loại: " + ItemText.Type(pickup.item.itemType));
        builder.AppendLine("Phẩm chất: " + GetQualityText(pickup.item));
        builder.AppendLine("Số lượng: " + Mathf.Max(0, pickup.amount));

        string description = ItemText.Description(pickup.item);
        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.AppendLine();
            builder.Append(description.Trim());
        }

        return builder.ToString().Trim();
    }

    public static Sprite GetPickupIcon(WorldStatItemPickup pickup)
    {
        if (pickup == null ||
            pickup.item == null)
        {
            return null;
        }

        if (pickup.item.icon != null)
        {
            return pickup.item.icon;
        }

        SpriteRenderer spriteRenderer =
            pickup.GetComponentInChildren<SpriteRenderer>(true);

        return spriteRenderer != null
            ? spriteRenderer.sprite
            : null;
    }

    static Transform FindChildByName(
        Transform parent,
        string childName)
    {
        if (parent == null ||
            string.IsNullOrEmpty(childName))
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildByName(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
