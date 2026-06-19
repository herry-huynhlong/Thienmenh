using UnityEngine;

public enum HarvestResourceKind
{
    Unknown,
    LinhMe,
    Ca,
    Thit,
    ThaoDuoc
}

[RequireComponent(typeof(WorldStatItemPickup))]
public class ResourceNode : MonoBehaviour
{
    public WorldStatItemPickup pickup;
    public HarvestResourceKind resourceKind = HarvestResourceKind.Unknown;

    void Awake()
    {
        if (pickup == null)
        {
            pickup = GetComponent<WorldStatItemPickup>();
        }

        RefreshResourceKind();
    }

    void OnValidate()
    {
        if (pickup == null)
        {
            pickup = GetComponent<WorldStatItemPickup>();
        }

        RefreshResourceKind();
    }

    public void RefreshResourceKind()
    {
        if (pickup == null)
        {
            return;
        }

        resourceKind = InferKindFromItem(pickup.item);
    }

    public bool Matches(StatItemData item)
    {
        if (pickup == null || pickup.item == null || item == null)
        {
            return false;
        }

        if (pickup.item == item)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(pickup.item.ItemId) &&
            !string.IsNullOrEmpty(item.ItemId) &&
            pickup.item.ItemId == item.ItemId)
        {
            return true;
        }

        HarvestResourceKind itemKind = InferKindFromItem(item);
        return resourceKind != HarvestResourceKind.Unknown &&
            itemKind != HarvestResourceKind.Unknown &&
            resourceKind == itemKind;
    }

    public static HarvestResourceKind InferKindFromItem(StatItemData item)
    {
        if (item == null)
        {
            return HarvestResourceKind.Unknown;
        }

        string itemName = ToTokenText(item.itemName);
        string assetName = ToTokenText(item.name);
        string itemId = item.ItemId ?? string.Empty;

        // Thảo dược phải kiểm tra trước Linh Mễ.
        // Chỉ so khớp theo TỪ, không dùng Contains("ca") / Contains("linh").
        // Ví dụ "Kim Cang Diệp" có chữ "cang", nếu dùng Contains("ca")
        // sẽ bị nhận nhầm thành Cá.
        if (HasAnyToken(itemName, "thao", "thuoc", "duoc") ||
            HasAnyToken(assetName, "thao", "thuoc", "duoc"))
        {
            return HarvestResourceKind.ThaoDuoc;
        }

        if (HasAnyToken(itemName, "ca") ||
            HasAnyToken(assetName, "ca"))
        {
            return HarvestResourceKind.Ca;
        }

        if (HasAnyToken(itemName, "thit") ||
            HasAnyToken(assetName, "thit"))
        {
            return HarvestResourceKind.Thit;
        }

        if (itemId == "46fa9c5a1da91f041b6da40762359694" ||
            itemId == "caf4f5e611fac2d4bb6815a50dc1060b" ||
            HasToken(itemName, "linh") && HasToken(itemName, "me") ||
            HasToken(assetName, "linh") && HasToken(assetName, "me") ||
            HasAnyToken(itemName, "linhme") ||
            HasAnyToken(assetName, "linhme"))
        {
            return HarvestResourceKind.LinhMe;
        }

        return HarvestResourceKind.Unknown;
    }

    static bool HasAnyToken(string tokenText, params string[] tokens)
    {
        if (string.IsNullOrEmpty(tokenText) || tokens == null)
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (HasToken(tokenText, tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasToken(string tokenText, string token)
    {
        if (string.IsNullOrEmpty(tokenText) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        return tokenText.Contains(" " + token + " ");
    }

    static string ToTokenText(string value)
    {
        string normalized = Normalize(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return " ";
        }

        char[] chars = normalized.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = ' ';
            }
        }

        return " " + new string(chars) + " ";
    }

    static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrEmpty(value) || tokens == null)
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (!string.IsNullOrEmpty(tokens[i]) &&
                value.Contains(tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .ToLowerInvariant()
            .Replace("á", "a")
            .Replace("à", "a")
            .Replace("ả", "a")
            .Replace("ã", "a")
            .Replace("ạ", "a")
            .Replace("ă", "a")
            .Replace("ắ", "a")
            .Replace("ằ", "a")
            .Replace("ẳ", "a")
            .Replace("ẵ", "a")
            .Replace("ặ", "a")
            .Replace("â", "a")
            .Replace("ấ", "a")
            .Replace("ầ", "a")
            .Replace("ẩ", "a")
            .Replace("ẫ", "a")
            .Replace("ậ", "a")
            .Replace("đ", "d")
            .Replace("é", "e")
            .Replace("è", "e")
            .Replace("ẻ", "e")
            .Replace("ẽ", "e")
            .Replace("ẹ", "e")
            .Replace("ê", "e")
            .Replace("ế", "e")
            .Replace("ề", "e")
            .Replace("ể", "e")
            .Replace("ễ", "e")
            .Replace("ệ", "e")
            .Replace("í", "i")
            .Replace("ì", "i")
            .Replace("ỉ", "i")
            .Replace("ĩ", "i")
            .Replace("ị", "i")
            .Replace("ó", "o")
            .Replace("ò", "o")
            .Replace("ỏ", "o")
            .Replace("õ", "o")
            .Replace("ọ", "o")
            .Replace("ô", "o")
            .Replace("ố", "o")
            .Replace("ồ", "o")
            .Replace("ổ", "o")
            .Replace("ỗ", "o")
            .Replace("ộ", "o")
            .Replace("ơ", "o")
            .Replace("ớ", "o")
            .Replace("ờ", "o")
            .Replace("ở", "o")
            .Replace("ỡ", "o")
            .Replace("ợ", "o")
            .Replace("ú", "u")
            .Replace("ù", "u")
            .Replace("ủ", "u")
            .Replace("ũ", "u")
            .Replace("ụ", "u")
            .Replace("ư", "u")
            .Replace("ứ", "u")
            .Replace("ừ", "u")
            .Replace("ử", "u")
            .Replace("ữ", "u")
            .Replace("ự", "u")
            .Replace("ý", "y")
            .Replace("ỳ", "y")
            .Replace("ỷ", "y")
            .Replace("ỹ", "y")
            .Replace("ỵ", "y");
    }
}
