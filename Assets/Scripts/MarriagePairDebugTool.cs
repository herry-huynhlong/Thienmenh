using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MarriagePairDebugTool : MonoBehaviour
{
    [Header("Targets")]
    public VillagerAI maleVillager;
    public VillagerAI femaleVillager;

    [Header("Debug Setup")]
    [Min(0)] public int spiritStoneGrantMin = 5000;
    [Min(0)] public int spiritStoneGrantMax = 10000;
    [Range(0, 100)] public int affectionAfterMarriage = 100;
    [Min(0)] public int marriedDaysAfterForce;
    public bool overrideExistingPartners = true;
    public bool startMarriageHomeFlowImmediately = true;

    [Header("Runtime Debug")]
    [TextArea(2, 5)]
    public string lastDebugResult;

    [ContextMenu("Debug/Force Marriage Test Pair")]
    public void DebugForceMarriageTestPair()
    {
        bool success =
            TryForceMarriageTestPair(out string result);
        lastDebugResult = result;
        if (success)
        {
            Debug.Log("[MarriageDebug] " + result, this);
        }
        else
        {
            Debug.LogError("[MarriageDebug] " + result, this);
        }
    }

    public bool TryForceMarriageTestPair(out string result)
    {
        if (!Application.isPlaying)
        {
            result =
                "Vao Play Mode roi bam nut de test ket hon va nha tan hon.";
            return false;
        }

        if (maleVillager == null || femaleVillager == null)
        {
            result = "Can gan du 1 NPC nam va 1 NPC nu.";
            return false;
        }

        if (maleVillager == femaleVillager)
        {
            result = "Hai o chon dang cung tro vao mot NPC.";
            return false;
        }

        NPCIdentity maleIdentity = GetIdentity(maleVillager);
        NPCIdentity femaleIdentity = GetIdentity(femaleVillager);
        if (maleIdentity == null || femaleIdentity == null)
        {
            result = "Thieu NPCIdentity tren mot trong hai NPC.";
            return false;
        }

        if (maleIdentity.gender != Gender.Male)
        {
            result = "NPC o o maleVillager khong co gioi tinh Male.";
            return false;
        }

        if (femaleIdentity.gender != Gender.Female)
        {
            result = "NPC o o femaleVillager khong co gioi tinh Female.";
            return false;
        }

        VillagerRelationship maleRelationship =
            GetOrAddRelationship(maleVillager);
        VillagerRelationship femaleRelationship =
            GetOrAddRelationship(femaleVillager);
        if (maleRelationship == null || femaleRelationship == null)
        {
            result = "Khong tao duoc VillagerRelationship cho cap doi test.";
            return false;
        }

        Dictionary<string, VillagerAI> lookup = BuildVillagerLookup();
        if (!TryReleaseExistingPartner(
                maleVillager,
                maleRelationship,
                femaleIdentity.npcId,
                lookup,
                out result) ||
            !TryReleaseExistingPartner(
                femaleVillager,
                femaleRelationship,
                maleIdentity.npcId,
                lookup,
                out result))
        {
            return false;
        }

        maleRelationship.Marry(femaleIdentity.npcId);
        femaleRelationship.Marry(maleIdentity.npcId);

        int affection =
            Mathf.Clamp(affectionAfterMarriage, 0, 100);
        int marriedDays =
            Mathf.Max(0, marriedDaysAfterForce);

        ApplyRelationshipDebugState(maleRelationship, affection, marriedDays);
        ApplyRelationshipDebugState(femaleRelationship, affection, marriedDays);

        int maleGrant = Random.Range(
            Mathf.Max(0, spiritStoneGrantMin),
            Mathf.Max(
                Mathf.Max(0, spiritStoneGrantMin),
                spiritStoneGrantMax) + 1);
        int femaleGrant = Random.Range(
            Mathf.Max(0, spiritStoneGrantMin),
            Mathf.Max(
                Mathf.Max(0, spiritStoneGrantMin),
                spiritStoneGrantMax) + 1);

        AddSpiritStone(maleVillager, maleGrant);
        AddSpiritStone(femaleVillager, femaleGrant);

        maleRelationship.SyncIdentityState();
        femaleRelationship.SyncIdentityState();

        MarriageHomeManager manager =
            MarriageHomeManager.EnsureInstance();
        MarriageHomeManager.NotifyPairMarried(
            maleVillager,
            femaleVillager);

        if (manager != null)
        {
            manager.RequestImmediateRefresh();
            if (startMarriageHomeFlowImmediately)
            {
                manager.DebugProcessNow();
                manager.DebugProcessNow();
            }
        }

        string homeState =
            maleRelationship.marriageHomeProjectState.ToString();
        string homeSiteId =
            maleRelationship.marriageHomeSiteId;
        result =
            "Da ep " +
            GetNpcName(maleIdentity, maleVillager) +
            " va " +
            GetNpcName(femaleIdentity, femaleVillager) +
            " thanh phu the. +" +
            maleGrant +
            " LT cho nam, +" +
            femaleGrant +
            " LT cho nu. Trang thai nha: " +
            homeState +
            (string.IsNullOrWhiteSpace(homeSiteId)
                ? "."
                : " tai site " + homeSiteId + ".");
        return true;
    }

    static void ApplyRelationshipDebugState(
        VillagerRelationship relationship,
        int affection,
        int marriedDays)
    {
        if (relationship == null)
        {
            return;
        }

        relationship.affection = affection;
        relationship.datingDays = 0;
        relationship.marriedDays = marriedDays;
        relationship.pregnant = false;
        relationship.pregnancyDaysLeft = 0;
        relationship.birthCooldownDays = 0;
        relationship.ClearMarriageHomeProject();
    }

    bool TryReleaseExistingPartner(
        VillagerAI villager,
        VillagerRelationship relationship,
        string targetPartnerId,
        Dictionary<string, VillagerAI> lookup,
        out string result)
    {
        result = string.Empty;
        if (villager == null ||
            relationship == null ||
            string.IsNullOrWhiteSpace(relationship.partnerId) ||
            string.Equals(
                relationship.partnerId,
                targetPartnerId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!overrideExistingPartners)
        {
            result =
                GetNpcName(GetIdentity(villager), villager) +
                " dang co doi, hay bat overrideExistingPartners de ep doi moi.";
            return false;
        }

        string previousPartnerId = relationship.partnerId;
        relationship.BecomeSingle();
        relationship.SyncIdentityState();

        if (lookup != null &&
            lookup.TryGetValue(previousPartnerId, out VillagerAI previousPartner) &&
            previousPartner != null)
        {
            VillagerRelationship previousRelationship =
                GetOrAddRelationship(previousPartner);
            if (previousRelationship != null)
            {
                previousRelationship.BecomeSingle();
                previousRelationship.SyncIdentityState();
            }
        }

        return true;
    }

    static Dictionary<string, VillagerAI> BuildVillagerLookup()
    {
        Dictionary<string, VillagerAI> lookup =
            new Dictionary<string, VillagerAI>(
                System.StringComparer.OrdinalIgnoreCase);
        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude);
        for (int i = 0; i < villagers.Length; i++)
        {
            VillagerAI villager = villagers[i];
            NPCIdentity identity = GetIdentity(villager);
            if (villager == null ||
                identity == null ||
                string.IsNullOrWhiteSpace(identity.npcId) ||
                lookup.ContainsKey(identity.npcId))
            {
                continue;
            }

            lookup.Add(identity.npcId, villager);
        }

        return lookup;
    }

    static NPCIdentity GetIdentity(VillagerAI villager)
    {
        return villager != null
            ? villager.GetComponent<NPCIdentity>()
            : null;
    }

    static VillagerRelationship GetOrAddRelationship(VillagerAI villager)
    {
        if (villager == null)
        {
            return null;
        }

        VillagerRelationship relationship =
            villager.GetComponent<VillagerRelationship>();
        if (relationship == null)
        {
            relationship =
                villager.gameObject.AddComponent<VillagerRelationship>();
        }

        return relationship;
    }

    static string GetNpcName(
        NPCIdentity identity,
        VillagerAI villager)
    {
        if (identity != null &&
            !string.IsNullOrWhiteSpace(identity.npcName))
        {
            return identity.npcName;
        }

        if (villager != null &&
            !string.IsNullOrWhiteSpace(villager.villagerName))
        {
            return villager.villagerName;
        }

        return villager != null
            ? villager.gameObject.name
            : "NPC";
    }

    static void AddSpiritStone(
        VillagerAI villager,
        int amount)
    {
        if (villager == null || amount <= 0)
        {
            return;
        }

        villager.spiritStone =
            Mathf.Max(0, villager.spiritStone + amount);
        if (villager.entityProfile != null)
        {
            villager.entityProfile.stats.spiritStone =
                villager.spiritStone;
        }
    }
}
