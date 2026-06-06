using System.Reflection;
using UnityEngine;

public class NpcFavorite : MonoBehaviour
{
    [Header("Saved NPC info")]
    public string npcDisplayName = "";
    public string realmText = "Pham nhan";

    [Header("Star mark above NPC")]
    public GameObject starMark;

    public bool IsFavorite { get; private set; }

    private void Awake()
    {
        RefreshStar();
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(npcDisplayName))
        {
            return npcDisplayName;
        }

        return ResolveDisplayName();
    }

    public string GetRealmText()
    {
        if (!string.IsNullOrWhiteSpace(realmText) && realmText != "Pham nhan")
        {
            return realmText;
        }

        string resolvedRealm = ResolveRealmText();
        return string.IsNullOrWhiteSpace(resolvedRealm) ? realmText : resolvedRealm;
    }

    public void SetFavoriteState(bool value)
    {
        IsFavorite = value;

        if (IsFavorite)
        {
            CaptureInfoSnapshot();
        }

        RefreshStar();
    }

    private void CaptureInfoSnapshot()
    {
        string name = ResolveDisplayName();

        if (!string.IsNullOrWhiteSpace(name))
        {
            npcDisplayName = name;
        }

        string realm = ResolveRealmText();

        if (!string.IsNullOrWhiteSpace(realm))
        {
            realmText = realm;
        }
    }

    private string ResolveDisplayName()
    {
        string roleName = NpcRoleUtility.GetDisplayName(gameObject);

        if (!string.IsNullOrWhiteSpace(roleName) && roleName != "NPC")
        {
            return roleName;
        }

        VillagerAI villager = GetComponent<VillagerAI>();

        if (villager != null && !string.IsNullOrWhiteSpace(villager.villagerName))
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();

        if (smartNpc != null && !string.IsNullOrWhiteSpace(smartNpc.npcName))
        {
            return smartNpc.npcName;
        }

        NpcData npcData = GetComponent<NpcData>();

        if (npcData != null && !string.IsNullOrWhiteSpace(npcData.npcName))
        {
            return npcData.npcName;
        }

        Component identity = GetComponent("NpcIdentity");

        if (identity != null)
        {
            string name = ReadMember(identity, "npcName");

            if (string.IsNullOrWhiteSpace(name))
            {
                name = ReadMember(identity, "displayName");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = ReadMember(identity, "characterName");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = ReadMember(identity, "entityName");
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return gameObject.name;
    }

    private string ResolveRealmText()
    {
        Component characterStats = GetComponent("CharacterStats");

        if (characterStats == null)
        {
            return realmText;
        }

        string realm = ReadMember(characterStats, "cultivationRealm");

        if (string.IsNullOrWhiteSpace(realm))
        {
            realm = ReadMember(characterStats, "realm");
        }

        if (string.IsNullOrWhiteSpace(realm))
        {
            realm = ReadMember(characterStats, "currentRealm");
        }

        if (string.IsNullOrWhiteSpace(realm))
        {
            realm = ReadMember(characterStats, "realmName");
        }

        string stage = ReadMember(characterStats, "realmStage");

        if (string.IsNullOrWhiteSpace(stage))
        {
            stage = ReadMember(characterStats, "stage");
        }

        if (string.IsNullOrWhiteSpace(stage))
        {
            stage = ReadMember(characterStats, "cultivationStage");
        }

        if (string.IsNullOrWhiteSpace(realm))
        {
            return realmText;
        }

        string vietnameseRealm = ConvertRealmToVietnamese(realm);

        if (!string.IsNullOrWhiteSpace(stage))
        {
            return vietnameseRealm + " " + stage;
        }

        return vietnameseRealm;
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
                return "Pham nhan";
            case "QiRefining":
            case "LuyenKhi":
            case "Luyen Khi":
                return "Luyen Khi";
            case "Foundation":
            case "FoundationBuilding":
            case "TrucCo":
            case "Truc Co":
                return "Truc Co";
            case "GoldenCore":
            case "KimDan":
            case "Kim Dan":
                return "Kim Dan";
            case "NascentSoul":
            case "NguyenAnh":
            case "Nguyen Anh":
                return "Nguyen Anh";
            case "SoulFormation":
            case "HoaThan":
            case "Hoa Than":
                return "Hoa Than";
            case "Tribulation":
            case "DoKiep":
            case "Do Kiep":
                return "Do Kiep";
            default:
                return rawRealm;
        }
    }
}
