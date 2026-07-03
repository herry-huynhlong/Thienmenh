using System.Reflection;
using UnityEngine;

public class NpcFavorite : MonoBehaviour
{
    public const int GiftBackThreshold = 20;
    public const int WorshipThreshold = 50;

    [Header("Saved NPC info")]
    public string npcDisplayName = "";
    public string realmText = "";
    public int heavenFavorFear;
    public bool unlockedGiftBackThreshold;
    public bool unlockedWorshipThreshold;

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
        if (!string.IsNullOrWhiteSpace(realmText) &&
            !IsLegacyDefaultRealmText(realmText))
        {
            return realmText;
        }

        string resolvedRealm = ResolveRealmText();
        return string.IsNullOrWhiteSpace(resolvedRealm)
            ? NpcText.Realm(CultivationRealm.Mortal)
            : resolvedRealm;
    }

    public int GetHeavenFavorFear()
    {
        return Mathf.Max(0, heavenFavorFear);
    }

    public bool AddHeavenFavorFear(int amount)
    {
        if (amount == 0)
        {
            return false;
        }

        int previousFear = Mathf.Max(0, heavenFavorFear);
        heavenFavorFear = Mathf.Max(0, previousFear + amount);

        if (!unlockedGiftBackThreshold &&
            heavenFavorFear >= GiftBackThreshold)
        {
            unlockedGiftBackThreshold = true;
        }

        if (!unlockedWorshipThreshold &&
            heavenFavorFear >= WorshipThreshold)
        {
            unlockedWorshipThreshold = true;
        }

        return heavenFavorFear != previousFear;
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

        Component identity = GetComponent("NPCIdentity");

        if (identity == null)
        {
            identity = GetComponent("NpcSocialIdentity");
        }

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
            return NpcText.Realm(CultivationRealm.Mortal);
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
            return NpcText.Realm(CultivationRealm.Mortal);
        }

        string vietnameseRealm = ConvertRealmToLocalizedRealm(realm);

        if (!string.IsNullOrWhiteSpace(stage))
        {
            return vietnameseRealm + " " + stage;
        }

        return vietnameseRealm;
    }

    private bool IsLegacyDefaultRealmText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return string.Equals(value, "Pham nhan", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, NpcText.Realm(CultivationRealm.Mortal), System.StringComparison.OrdinalIgnoreCase);
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

    private string ConvertRealmToLocalizedRealm(string rawRealm)
    {
        switch (rawRealm)
        {
            case "Mortal":
                return NpcText.Realm(CultivationRealm.Mortal);
            case "QiRefining":
            case "LuyenKhi":
            case "Luyen Khi":
                return NpcText.Realm(CultivationRealm.QiRefining);
            case "Foundation":
            case "FoundationBuilding":
            case "TrucCo":
            case "Truc Co":
                return NpcText.Realm(CultivationRealm.Foundation);
            case "GoldenCore":
            case "KimDan":
            case "Kim Dan":
                return NpcText.Realm(CultivationRealm.GoldenCore);
            case "NascentSoul":
            case "NguyenAnh":
            case "Nguyen Anh":
                return NpcText.Realm(CultivationRealm.NascentSoul);
            case "SoulFormation":
            case "HoaThan":
            case "Hoa Than":
                return NpcText.Realm(CultivationRealm.SoulFormation);
            case "Tribulation":
            case "DoKiep":
            case "Do Kiep":
                return NpcText.Realm(CultivationRealm.Tribulation);
            default:
                return rawRealm;
        }
    }
}
