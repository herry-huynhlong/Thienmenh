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

    public GameObject namtuoiRoot;

    public TMP_Text namtuoiText;

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
            panelIcon = FindDeepestImage(
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

        if (namtuoiRoot == null)
        {
            Transform growthRoot = FindChildByName(panelRoot.transform, "namtuoi");
            if (growthRoot != null)
            {
                namtuoiRoot = growthRoot.gameObject;
            }
        }

        namtuoiText = EnsureText(
            namtuoiText,
            panelRoot.transform,
            "namtuoi");

        loreQuoteText = EnsureLoreQuoteContentText(
            loreQuoteText,
            panelRoot.transform);

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
        SetOptionalText(namtuoiRoot, namtuoiText, BuildGrowthDurationText(pickup));

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

        string lore = ItemText.Lore(item);
        if (!string.IsNullOrWhiteSpace(lore))
        {
            return lore.Trim();
        }

        string description = ItemText.Description(item);
        if (string.IsNullOrWhiteSpace(description))
        {
            return "";
        }

        return BuildGeneratedLoreText(item, description);
    }

    static string BuildGeneratedLoreText(
        StatItemData item,
        string description)
    {
        string cleanedDescription =
            NormalizeSpacing(description);
        if (string.IsNullOrWhiteSpace(cleanedDescription))
        {
            return "";
        }

        switch (LocalizationSettings.CurrentLanguageCode)
        {
            case "zh":
                return BuildChineseGeneratedLoreText(
                    item,
                    cleanedDescription);

            case "en":
                return BuildEnglishGeneratedLoreText(
                    item,
                    cleanedDescription);

            default:
                return BuildVietnameseGeneratedLoreText(
                    item,
                    cleanedDescription);
        }
    }

    static string BuildVietnameseRumorText(
        string itemName,
        string appearanceClause,
        string effectClause)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Tương truyền ");
        builder.Append(itemName);

        if (!string.IsNullOrWhiteSpace(appearanceClause))
        {
            builder.Append(" ");
            builder.Append(appearanceClause);
        }

        if (!string.IsNullOrWhiteSpace(effectClause))
        {
            builder.Append(" Người trong giới truyền tai rằng ");
            builder.Append(LowercaseFirstCharacter(
                RemoveTrailingSentencePunctuation(
                    effectClause)));
            builder.Append(".");
        }

        return builder.ToString().Trim();
    }

    static string BuildEnglishRumorText(
        string itemName,
        string appearanceClause,
        string effectClause)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Rumor has it ");
        builder.Append(itemName);

        if (!string.IsNullOrWhiteSpace(appearanceClause))
        {
            builder.Append(" ");
            builder.Append(appearanceClause);
        }

        if (!string.IsNullOrWhiteSpace(effectClause))
        {
            builder.Append(" Those familiar with it whisper that ");
            builder.Append(LowercaseFirstCharacter(
                RemoveTrailingSentencePunctuation(
                    effectClause)));
            builder.Append(".");
        }

        return builder.ToString().Trim();
    }

    static string BuildChineseRumorText(
        string itemName,
        string appearanceClause,
        string effectClause)
    {
        string trimmedAppearance =
            RemoveTrailingSentencePunctuation(
                appearanceClause);
        string trimmedEffect =
            RemoveTrailingSentencePunctuation(
                effectClause);
        StringBuilder builder = new StringBuilder();
        builder.Append("相传");
        builder.Append(itemName);

        if (!string.IsNullOrWhiteSpace(trimmedAppearance))
        {
            builder.Append(trimmedAppearance);
        }

        if (!string.IsNullOrWhiteSpace(trimmedEffect))
        {
            builder.Append("，坊间常说它");
            builder.Append(trimmedEffect);
            builder.Append("。");
        }
        else if (!string.IsNullOrWhiteSpace(trimmedAppearance))
        {
            builder.Append("。");
        }

        return builder.ToString().Trim();
    }

    static string BuildFallbackRumorAppearance(StatItemData item)
    {
        switch (LocalizationSettings.CurrentLanguageCode)
        {
            case "zh":
                return "来历隐秘，常在修士之间口耳相传。";
            case "en":
                return "is said to pass quietly from one cultivator to another.";
            default:
                return "được lưu truyền âm thầm giữa các tu sĩ.";
        }
    }

    static string BuildFallbackRumorEffect(StatItemData item)
    {
        string usage = BuildUsageSummaryText(item);
        if (string.IsNullOrWhiteSpace(usage))
        {
            switch (LocalizationSettings.CurrentLanguageCode)
            {
                case "zh":
                    return "ẩn chứa công dụng mà người ngoài khó đoán";
                case "en":
                    return "its true use is known only to a few insiders";
                default:
                    return "công dụng thật sự của nó chỉ người trong nghề mới rõ";
            }
        }

        switch (LocalizationSettings.CurrentLanguageCode)
        {
            case "zh":
                return "在关键时刻往往能派上大用场";
            case "en":
                return "it proves useful when handled by the right person";
            default:
                return "nó phát huy hiệu quả rõ nhất khi rơi vào tay người biết dùng";
        }
    }

    static string[] SplitSentences(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return System.Array.Empty<string>();
        }

        return value.Split(
            new[] { '.', '!', '?', '。', '！', '？' },
            System.StringSplitOptions.RemoveEmptyEntries);
    }

    static string TrimItemNamePrefix(
        string sentence,
        string itemName)
    {
        string trimmed = NormalizeSpacing(sentence);
        if (string.IsNullOrWhiteSpace(trimmed) ||
            string.IsNullOrWhiteSpace(itemName))
        {
            return trimmed;
        }

        if (!trimmed.StartsWith(
                itemName,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        trimmed = trimmed.Substring(itemName.Length).TrimStart();
        if (trimmed.StartsWith("là ",
                System.StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(3).TrimStart();
        }

        return trimmed;
    }

    static string BuildVietnameseGeneratedLoreText(
        StatItemData item,
        string description)
    {
        string itemName = ItemText.Name(item).Trim();
        string originHint =
            BuildVietnameseRumorOrigin(item, description);
        string effectHint =
            BuildVietnameseRumorEffect(item, description);
        string cautionHint =
            BuildVietnameseRumorCaution(item, description);

        StringBuilder builder = new StringBuilder();
        builder.Append("Tương truyền ");
        builder.Append(itemName);
        builder.Append(" ");
        builder.Append(originHint);
        builder.Append(". Người trong giới kháo nhau rằng ");
        builder.Append(effectHint);

        if (!string.IsNullOrWhiteSpace(cautionHint))
        {
            builder.Append(", ");
            builder.Append(cautionHint);
        }

        builder.Append(".");
        return builder.ToString().Trim();
    }

    static string BuildEnglishGeneratedLoreText(
        StatItemData item,
        string description)
    {
        string itemName = ItemText.Name(item).Trim();
        string originHint =
            BuildEnglishRumorOrigin(item, description);
        string effectHint =
            BuildEnglishRumorEffect(item, description);

        return "Rumor has it " +
            itemName +
            " " +
            originHint +
            ". Those who know it best whisper that " +
            effectHint +
            ".";
    }

    static string BuildChineseGeneratedLoreText(
        StatItemData item,
        string description)
    {
        string itemName = ItemText.Name(item).Trim();
        string originHint =
            BuildChineseRumorOrigin(item, description);
        string effectHint =
            BuildChineseRumorEffect(item, description);

        return "相传" +
            itemName +
            originHint +
            "。坊间常说它" +
            effectHint +
            "。";
    }

    static string BuildVietnameseRumorOrigin(
        StatItemData item,
        string description)
    {
        if (ContainsAny(
                description,
                "rễ",
                "bám",
                "đất đá",
                "khe đá",
                "sườn núi"))
        {
            return "thường mọc bám ở những khe đá nặng địa khí";
        }

        if (ContainsAny(
                description,
                "sương",
                "ẩm",
                "hàn",
                "suối",
                "thâm cốc"))
        {
            return "thường sinh ở chỗ ẩm lạnh và hút sương mà lớn";
        }

        switch (item.itemType)
        {
            case ItemType.DanDuoc:
                return "được giữ kín trong túi đan sư chứ ít khi lộ ra ngoài";
            case ItemType.PhapBao:
                return "đã qua nhiều lượt tôi luyện mới giữ được linh tính";
            case ItemType.CongPhap:
                return "từng được chép tay rồi cất kín qua nhiều đời";
            case ItemType.ThucPham:
                return "nhìn qua bình thường nhưng người biết hàng hiếm khi bỏ qua";
            case ItemType.VatLieu:
            default:
                return "là thứ linh vật nhìn không phô trương nhưng giấu khí rất sâu";
        }
    }

    static string BuildVietnameseRumorEffect(
        StatItemData item,
        string description)
    {
        System.Collections.Generic.List<string> fragments =
            new System.Collections.Generic.List<string>();

        if (ContainsAny(
                description,
                "gân cốt",
                "xương",
                "luyện thể",
                "thể chất"))
        {
            fragments.Add(
                "dược lực của nó thiên về luyện thể, giúp gân cốt cứng hơn và thân thể bền hơn");
        }

        if (ContainsAny(
                description,
                "phòng ngự",
                "hộ thân",
                "chống đỡ") ||
            item.armorBonus > 0)
        {
            fragments.Add(
                "nó hợp với người muốn củng cố hộ thân và tăng sức chịu đòn");
        }

        if (ContainsAny(
                description,
                "tu vi",
                "linh khí",
                "hấp thụ") ||
            item.cultivationBonus > 0)
        {
            fragments.Add(
                "người dùng đúng cách có thể mượn nó để bồi thêm linh lực và tích lũy tu vi");
        }

        if (ContainsAny(
                description,
                "đột phá",
                "bình cảnh") ||
            item.breakthroughRealm)
        {
            fragments.Add(
                "nhiều tu sĩ chỉ dám dùng nó vào lúc chạm bình cảnh");
        }

        if (item.damageBonus > 0)
        {
            fragments.Add(
                "linh tính bên trong khá gắt, hợp với kẻ muốn tăng sát lực");
        }

        if (item.effectResistanceBonus > 0)
        {
            fragments.Add(
                "nó cũng giúp giữ tâm mạch ổn định trước dị lực bên ngoài");
        }

        if (fragments.Count == 0)
        {
            fragments.Add(
                "công dụng thật sự của nó chỉ người từng dùng đúng cách mới hiểu hết");
        }

        if (fragments.Count == 1)
        {
            return fragments[0];
        }

        return fragments[0] + "; ngoài ra " + fragments[1];
    }

    static string BuildVietnameseRumorCaution(
        StatItemData item,
        string description)
    {
        if (ContainsAny(
                description,
                "độc",
                "phệ",
                "tạp chất") ||
            item.rawToxicityDamage > 0)
        {
            return "kẻ nóng vội dùng bừa rất dễ chuốc phản phệ";
        }

        if (item.rawUsePolicy != RawUsePolicy.Allowed)
        {
            return "người thiếu kinh nghiệm dùng sống thường không được lợi";
        }

        return "";
    }

    static string BuildEnglishRumorOrigin(
        StatItemData item,
        string description)
    {
        if (ContainsAny(
                description,
                "root",
                "stone",
                "rock",
                "cliff"))
        {
            return "is often found clinging to stone where earth qi runs deep";
        }

        switch (item.itemType)
        {
            case ItemType.DanDuoc:
                return "rarely leaves an alchemist's sleeve";
            case ItemType.PhapBao:
                return "carries the temper of repeated forging";
            default:
                return "passes quietly among cultivators who know its worth";
        }
    }

    static string BuildEnglishRumorEffect(
        StatItemData item,
        string description)
    {
        if (ContainsAny(
                description,
                "bone",
                "body",
                "physique") ||
            item.armorBonus > 0 ||
            item.hpBonus > 0)
        {
            return "it favors body tempering and steadies the user's frame";
        }

        if (item.cultivationBonus > 0 ||
            item.breakthroughRealm)
        {
            return "it is best saved for the moment one's cultivation begins to stall";
        }

        return "its value only shows in the hands of someone who knows how to draw out its nature";
    }

    static string BuildChineseRumorOrigin(
        StatItemData item,
        string description)
    {
        if (ContainsAny(
                description,
                "石",
                "岩",
                "根",
                "地气"))
        {
            return "多生在地气沉厚的岩隙之间";
        }

        switch (item.itemType)
        {
            case ItemType.DanDuoc:
                return "向来只在丹师之间暗中流转";
            case ItemType.PhapBao:
                return "历经多次淬炼方才留住灵性";
            default:
                return "看似寻常，却常被识货之人私下收藏";
        }
    }

    static string BuildChineseRumorEffect(
        StatItemData item,
        string description)
    {
        if (ContainsAny(
                description,
                "骨",
                "体魄",
                "炼体") ||
            item.armorBonus > 0 ||
            item.hpBonus > 0)
        {
            return "偏于淬体固骨";
        }

        if (item.cultivationBonus > 0 ||
            item.breakthroughRealm)
        {
            return "往往被留到冲关前后才舍得动用";
        }

        return "真正妙用只在懂行的人手里才会显现";
    }

    static bool ContainsAny(
        string value,
        params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            needles == null)
        {
            return false;
        }

        for (int i = 0; i < needles.Length; i++)
        {
            string needle = needles[i];
            if (string.IsNullOrWhiteSpace(needle))
            {
                continue;
            }

            if (value.IndexOf(
                    needle,
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    static string NormalizeSpacing(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("  ", " ")
            .Trim();
    }

    static string EnsureSentence(string value)
    {
        string trimmed =
            RemoveTrailingSentencePunctuation(
                NormalizeSpacing(value));
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "";
        }

        return trimmed + ".";
    }

    static string RemoveTrailingSentencePunctuation(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value.TrimEnd(' ', '.', '!', '?', '。', '！', '？');
    }

    static string LowercaseFirstCharacter(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (value.Length == 1)
        {
            return value.ToLowerInvariant();
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
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

    static void SetOptionalText(
        GameObject root,
        TMP_Text text,
        string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
            text.gameObject.SetActive(!string.IsNullOrWhiteSpace(text.text));
        }

        if (root != null)
        {
            root.SetActive(!string.IsNullOrWhiteSpace(value));
        }
    }

    static string BuildGrowthDurationText(WorldStatItemPickup pickup)
    {
        GrowingHerbNode herbNode = FindGrowingHerbNode(pickup);
        if (herbNode == null)
        {
            return "";
        }

        int matureHours = Mathf.Max(1, Mathf.CeilToInt(herbNode.MatureAfterGameHours));
        int currentHours = Mathf.Clamp(
            Mathf.FloorToInt(Mathf.Max(0f, herbNode.AccumulatedGrowthHours)),
            0,
            matureHours);

        string matureText = FormatGrowthHours(matureHours);
        if (currentHours <= 0 ||
            currentHours >= matureHours)
        {
            return matureText;
        }

        return UiText.Format(
            "worldItemInfo",
            "growthProgressFormat",
            FormatGrowthHours(currentHours),
            matureText);
    }

    static GrowingHerbNode FindGrowingHerbNode(WorldStatItemPickup pickup)
    {
        if (pickup == null)
        {
            return null;
        }

        GrowingHerbNode herbNode = pickup.GetComponent<GrowingHerbNode>();
        if (herbNode != null)
        {
            return herbNode;
        }

        herbNode = pickup.GetComponentInParent<GrowingHerbNode>();
        if (herbNode != null)
        {
            return herbNode;
        }

        return pickup.GetComponentInChildren<GrowingHerbNode>(true);
    }

    static string FormatGrowthHours(int totalHours)
    {
        totalHours = Mathf.Max(0, totalHours);
        int days = totalHours / 24;
        int hours = totalHours % 24;

        if (days > 0)
        {
            return UiText.Format(
                "worldItemInfo",
                "growthDaysHoursFormat",
                days,
                hours);
        }

        return UiText.Format(
            "worldItemInfo",
            "growthHoursFormat",
            totalHours);
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

    static TMP_Text EnsureTextInSection(
        TMP_Text current,
        Transform root,
        string sectionName)
    {
        if (current != null ||
            root == null)
        {
            return current;
        }

        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            Transform section =
                FindChildByName(root, sectionName);
            if (section != null)
            {
                TMP_Text sectionText =
                    section.GetComponent<TMP_Text>();
                if (sectionText == null)
                {
                    sectionText =
                        section.GetComponentInChildren<TMP_Text>(true);
                }

                if (sectionText != null)
                {
                    return sectionText;
                }
            }
        }

        return current;
    }

    static TMP_Text EnsureLoreQuoteContentText(
        TMP_Text current,
        Transform root)
    {
        if (current != null ||
            root == null)
        {
            return current;
        }

        Transform section =
            FindChildByName(root, "LoreQuote");
        if (section == null)
        {
            return current;
        }

        TMP_Text contentText =
            FindTextInParent(
                section,
                "noidung",
                "LoreText",
                "QuoteText",
                "ContentText");

        if (contentText != null)
        {
            return contentText;
        }

        TMP_Text[] sectionTexts =
            section.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in sectionTexts)
        {
            if (text == null ||
                text.gameObject.name == "tuongtruyen")
            {
                continue;
            }

            return text;
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

    static TMP_Text FindTextInParent(
        Transform parent,
        params string[] names)
    {
        if (parent == null ||
            names == null)
        {
            return null;
        }

        foreach (string name in names)
        {
            Transform found = FindChildByName(parent, name);
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

        return null;
    }

    static Image FindDeepestImage(
        Transform root,
        params string[] names)
    {
        if (root == null ||
            names == null)
        {
            return null;
        }

        Image bestImage = null;
        int bestDepth = -1;

        foreach (string name in names)
        {
            FindDeepestImageRecursive(root, name, 0, ref bestImage, ref bestDepth);
            if (bestImage != null)
            {
                return bestImage;
            }
        }

        return null;
    }

    static void FindDeepestImageRecursive(
        Transform parent,
        string childName,
        int depth,
        ref Image bestImage,
        ref int bestDepth)
    {
        if (parent == null ||
            string.IsNullOrEmpty(childName))
        {
            return;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                Image image = child.GetComponent<Image>();
                if (image == null)
                {
                    image = child.GetComponentInChildren<Image>(true);
                }

                if (image != null &&
                    depth >= bestDepth)
                {
                    bestImage = image;
                    bestDepth = depth;
                }
            }

            FindDeepestImageRecursive(
                child,
                childName,
                depth + 1,
                ref bestImage,
                ref bestDepth);
        }
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
