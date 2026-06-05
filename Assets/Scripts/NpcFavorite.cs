using System.Reflection;
using UnityEngine;

public class NpcFavorite : MonoBehaviour
{
    [Header("Nếu để trống sẽ tự lấy tên từ NpcIdentity hoặc tên GameObject")]
    public string npcDisplayName = "";

    [Header("Chỉ dùng khi không đọc được CharacterStats")]
    public string realmText = "Phàm nhân";

    [Header("Dấu sao trên đầu NPC")]
    public GameObject starMark;

    public bool IsFavorite { get; private set; }

    private void Awake()
    {
        RefreshStar();
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(npcDisplayName))
        {
            return npcDisplayName;
        }

        Component identity = GetComponent("NpcIdentity");

        if (identity != null)
        {
            string name = ReadMember(identity, "npcName");

            if (string.IsNullOrEmpty(name))
            {
                name = ReadMember(identity, "displayName");
            }

            if (string.IsNullOrEmpty(name))
            {
                name = ReadMember(identity, "characterName");
            }

            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }

        return gameObject.name;
    }

    public string GetRealmText()
    {
        Component stats = GetComponent("CharacterStats");

        if (stats == null)
        {
            stats = GetComponentInChildren(typeof(Component), true);
        }

        Component characterStats = GetComponent("CharacterStats");

        if (characterStats == null)
        {
            return realmText;
        }

        string realm = ReadMember(characterStats, "cultivationRealm");

        if (string.IsNullOrEmpty(realm))
        {
            realm = ReadMember(characterStats, "realm");
        }

        if (string.IsNullOrEmpty(realm))
        {
            realm = ReadMember(characterStats, "currentRealm");
        }

        if (string.IsNullOrEmpty(realm))
        {
            realm = ReadMember(characterStats, "realmName");
        }

        string stage = ReadMember(characterStats, "realmStage");

        if (string.IsNullOrEmpty(stage))
        {
            stage = ReadMember(characterStats, "stage");
        }

        if (string.IsNullOrEmpty(stage))
        {
            stage = ReadMember(characterStats, "cultivationStage");
        }

        if (string.IsNullOrEmpty(realm))
        {
            return realmText;
        }

        string vietnameseRealm = ConvertRealmToVietnamese(realm);

        if (!string.IsNullOrEmpty(stage))
        {
            return vietnameseRealm + " " + stage;
        }

        return vietnameseRealm;
    }

    public void SetFavoriteState(bool value)
    {
        IsFavorite = value;
        RefreshStar();
    }

    private void RefreshStar()
    {
        if (starMark != null)
        {
            starMark.SetActive(IsFavorite);
        }
    }

    private string ReadMember(Component component, string memberName)
    {
        if (component == null)
        {
            return "";
        }

        System.Type type = component.GetType();

        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (field != null)
        {
            object value = field.GetValue(component);
            return value != null ? value.ToString() : "";
        }

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (property != null)
        {
            object value = property.GetValue(component);
            return value != null ? value.ToString() : "";
        }

        return "";
    }

    private string ConvertRealmToVietnamese(string rawRealm)
    {
        switch (rawRealm)
        {
            case "Mortal":
                return "Phàm nhân";

            case "QiRefining":
            case "LuyenKhi":
            case "Luyện Khí":
                return "Luyện Khí";

            case "Foundation":
            case "FoundationBuilding":
            case "TrucCo":
            case "Trúc Cơ":
                return "Trúc Cơ";

            case "GoldenCore":
            case "KimDan":
            case "Kim Đan":
                return "Kim Đan";

            case "NascentSoul":
            case "NguyenAnh":
            case "Nguyên Anh":
                return "Nguyên Anh";

            case "SoulFormation":
            case "HoaThan":
            case "Hóa Thần":
                return "Hóa Thần";

            case "Tribulation":
            case "DoKiep":
            case "Độ Kiếp":
                return "Độ Kiếp";

            default:
                return rawRealm;
        }
    }
}