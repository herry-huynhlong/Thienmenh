using UnityEngine;

public enum VillagerRelationshipStatus
{
    Single,
    Dating,
    Married
}

public enum MarriageHomeProjectState
{
    None,
    Saving,
    Building,
    WeddingNight
}

[DisallowMultipleComponent]
public class VillagerRelationship : MonoBehaviour
{
    [Header("Relationship")]
    public VillagerRelationshipStatus status = VillagerRelationshipStatus.Single;
    public string partnerId;
    public int affection;
    public int datingDays;
    public int marriedDays;
    public bool pregnant;
    public int pregnancyDaysLeft;
    public int birthCooldownDays;

    [Header("Couple Children")]
    public int childrenBornWithCurrentPartner;
    public int totalChildrenBorn;
    public int maxChildrenWithCurrentPartner = 3;

    [Header("Marriage Home")]
    public MarriageHomeProjectState marriageHomeProjectState;
    public string marriageHomeSiteId;
    public int marriageHomeCostSpiritStone;
    public int marriageHomeBuildStartAbsoluteDay = int.MinValue;
    [Range(0f, 23.99f)]
    public float marriageHomeBuildStartHour;

    NPCIdentity identity;

    void Awake()
    {
        CacheReferences();
        SyncIdentityState();
    }

    void OnValidate()
    {
        CacheReferences();

        if (!Application.isPlaying)
        {
            SyncIdentityState();
        }
    }

    public bool IsSingle()
    {
        return status == VillagerRelationshipStatus.Single ||
            string.IsNullOrWhiteSpace(partnerId);
    }

    public bool IsDating()
    {
        return status == VillagerRelationshipStatus.Dating &&
            !string.IsNullOrWhiteSpace(partnerId);
    }

    public bool IsMarried()
    {
        return status == VillagerRelationshipStatus.Married &&
            !string.IsNullOrWhiteSpace(partnerId);
    }

    public bool CanHaveMoreChildren()
    {
        return maxChildrenWithCurrentPartner > 0 &&
            childrenBornWithCurrentPartner < maxChildrenWithCurrentPartner;
    }

    public bool CanConceive()
    {
        return IsMarried() &&
            !pregnant &&
            birthCooldownDays <= 0 &&
            CanHaveMoreChildren();
    }

    public void StartDating(string otherPartnerId, int childLimit)
    {
        partnerId = otherPartnerId;
        status = VillagerRelationshipStatus.Dating;
        affection = Mathf.Clamp(Mathf.Max(affection, 24), 0, 100);
        datingDays = 0;
        marriedDays = 0;
        pregnant = false;
        pregnancyDaysLeft = 0;
        birthCooldownDays = 0;
        childrenBornWithCurrentPartner = 0;
        maxChildrenWithCurrentPartner =
            childLimit > 0
                ? Mathf.Clamp(childLimit, 2, 3)
                : Random.Range(2, 4);
        SyncIdentityState();
    }

    public void Marry(string otherPartnerId)
    {
        partnerId = otherPartnerId;
        status = VillagerRelationshipStatus.Married;
        marriedDays = 0;
        pregnant = false;
        pregnancyDaysLeft = 0;
        SyncIdentityState();
    }

    public void BecomeSingle()
    {
        status = VillagerRelationshipStatus.Single;
        partnerId = string.Empty;
        affection = Mathf.Clamp(affection / 2, 0, 100);
        datingDays = 0;
        marriedDays = 0;
        pregnant = false;
        pregnancyDaysLeft = 0;
        birthCooldownDays = 0;
        childrenBornWithCurrentPartner = 0;
        maxChildrenWithCurrentPartner = 3;
        ClearMarriageHomeProject();
        SyncIdentityState();
    }

    public void AddAffection(int amount)
    {
        affection = Mathf.Clamp(affection + amount, 0, 100);
    }

    public bool TickPregnancy()
    {
        if (!pregnant)
        {
            return false;
        }

        pregnancyDaysLeft = Mathf.Max(0, pregnancyDaysLeft - 1);
        return pregnancyDaysLeft <= 0;
    }

    public void BeginPregnancy(int durationDays)
    {
        pregnant = true;
        pregnancyDaysLeft = Mathf.Max(1, durationDays);
    }

    public void RegisterChildBirth(int cooldownDays)
    {
        pregnant = false;
        pregnancyDaysLeft = 0;
        birthCooldownDays = Mathf.Max(0, cooldownDays);
        childrenBornWithCurrentPartner++;
        totalChildrenBorn++;
    }

    public void TickDaily()
    {
        if (birthCooldownDays > 0)
        {
            birthCooldownDays--;
        }

        if (status == VillagerRelationshipStatus.Dating)
        {
            datingDays++;
        }
        else if (status == VillagerRelationshipStatus.Married)
        {
            marriedDays++;
        }
    }

    public bool HasMarriageHomeProject()
    {
        return marriageHomeProjectState != MarriageHomeProjectState.None &&
            !string.IsNullOrWhiteSpace(marriageHomeSiteId);
    }

    public void BeginMarriageHomeSaving(
        string siteId,
        int costSpiritStone)
    {
        marriageHomeProjectState = MarriageHomeProjectState.Saving;
        marriageHomeSiteId = siteId ?? string.Empty;
        marriageHomeCostSpiritStone = Mathf.Max(0, costSpiritStone);
        marriageHomeBuildStartAbsoluteDay = int.MinValue;
        marriageHomeBuildStartHour = 0f;
    }

    public void BeginMarriageHomeBuilding(
        string siteId,
        int costSpiritStone,
        int startAbsoluteDay,
        float startHour)
    {
        marriageHomeProjectState = MarriageHomeProjectState.Building;
        marriageHomeSiteId = siteId ?? string.Empty;
        marriageHomeCostSpiritStone = Mathf.Max(0, costSpiritStone);
        marriageHomeBuildStartAbsoluteDay = Mathf.Max(1, startAbsoluteDay);
        marriageHomeBuildStartHour = Mathf.Repeat(startHour, 24f);
    }

    public void BeginMarriageHomeWeddingNight(
        string siteId,
        int startAbsoluteDay,
        float startHour)
    {
        marriageHomeProjectState = MarriageHomeProjectState.WeddingNight;
        marriageHomeSiteId = siteId ?? string.Empty;
        marriageHomeBuildStartAbsoluteDay = Mathf.Max(1, startAbsoluteDay);
        marriageHomeBuildStartHour = Mathf.Repeat(startHour, 24f);
    }

    public void ClearMarriageHomeProject()
    {
        marriageHomeProjectState = MarriageHomeProjectState.None;
        marriageHomeSiteId = string.Empty;
        marriageHomeCostSpiritStone = 0;
        marriageHomeBuildStartAbsoluteDay = int.MinValue;
        marriageHomeBuildStartHour = 0f;
    }

    public void SyncIdentityState()
    {
        CacheReferences();

        if (identity == null)
        {
            return;
        }

        if (status == VillagerRelationshipStatus.Married &&
            !string.IsNullOrWhiteSpace(partnerId))
        {
            identity.spouseId = partnerId;
        }
        else
        {
            identity.spouseId = string.Empty;
        }
    }

    void CacheReferences()
    {
        if (identity == null)
        {
            identity =
                GetComponent<NPCIdentity>() ??
                GetComponentInParent<NPCIdentity>(true) ??
                GetComponentInChildren<NPCIdentity>(true);
        }
    }
}
