using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-70)]
public class VillagerRelationshipManager : MonoBehaviour
{
    public static VillagerRelationshipManager Instance;

    const float MinimumDailyMatchChance = 0.08f;
    const int MaximumDatingDaysToMarry = 3;
    const int MaximumAffectionToMarry = 30;
    const int MinimumAffectionGainPerDay = 4;

    [Header("Matching")]
    [Range(0f, 1f)] public float dailyMatchChance = 0.02f;
    public int minAdultAge = NpcLifeStageDefaults.AdultMinAge;
    public int maxMarriageAge = NpcLifeStageDefaults.MiddleMaxAge;
    public int maxAgeGapForMarriage = 20;
    public int minDatingDaysToMarry = 7;
    [Range(0, 100)] public int minAffectionToMarry = 60;
    [Range(0, 3)] public int affectionGainPerDay = 1;

    readonly List<VillagerAI> villagersBuffer = new List<VillagerAI>();
    readonly List<VillagerAI> singlesBuffer = new List<VillagerAI>();
    readonly HashSet<string> pairedThisTick = new HashSet<string>();

    int lastProcessedDay = int.MinValue;

    public static VillagerRelationshipManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        VillagerRelationshipManager[] managers =
            Object.FindObjectsByType<VillagerRelationshipManager>(
                FindObjectsInactive.Include);

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null)
            {
                Instance = managers[i];
                return Instance;
            }
        }

        GameObject created = new GameObject("VillagerRelationshipManager");
        Instance = created.AddComponent<VillagerRelationshipManager>();
        return Instance;
    }

    void Awake()
    {
        ApplyBaselinePacing();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnValidate()
    {
        ApplyBaselinePacing();
    }

    void Update()
    {
        DailyRelationshipTick();
    }

    public void DailyRelationshipTick()
    {
        int day = GetRelationshipTickDay();
        if (day == lastProcessedDay)
        {
            return;
        }

        lastProcessedDay = day;
        villagersBuffer.Clear();
        singlesBuffer.Clear();
        pairedThisTick.Clear();

        VillagerAI[] villagers =
            Object.FindObjectsByType<VillagerAI>(FindObjectsInactive.Include);

        for (int i = 0; i < villagers.Length; i++)
        {
            VillagerAI villager = villagers[i];
            if (villager == null || villager.IsDead)
            {
                continue;
            }

            villagersBuffer.Add(villager);
        }

        Dictionary<string, VillagerAI> lookup = BuildLookup(villagersBuffer);

        for (int i = 0; i < villagersBuffer.Count; i++)
        {
            VillagerAI villager = villagersBuffer[i];
            if (villager == null || villager.IsDead)
            {
                continue;
            }

            NPCIdentity identity = GetIdentity(villager);
            if (identity == null ||
                string.IsNullOrWhiteSpace(identity.npcId))
            {
                continue;
            }

            VillagerRelationship relationship = GetOrAddRelationship(villager);
            if (relationship == null)
            {
                continue;
            }

            if (!relationship.IsSingle())
            {
                VillagerAI partner = FindPartner(
                    relationship.partnerId,
                    lookup);

                if (partner == null || partner.IsDead)
                {
                    BreakPair(villager, relationship, null);
                    continue;
                }

                NPCIdentity partnerIdentity = GetIdentity(partner);
                if (partnerIdentity == null)
                {
                    BreakPair(villager, relationship, partner);
                    continue;
                }

                VillagerRelationship partnerRelationship =
                    GetOrAddRelationship(partner);
                if (partnerRelationship == null)
                {
                    BreakPair(villager, relationship, partner);
                    continue;
                }

                SyncPairLink(
                    villager,
                    relationship,
                    partner,
                    partnerRelationship);

                relationship.TickDaily();
                partnerRelationship.TickDaily();
                relationship.AddAffection(affectionGainPerDay);
                partnerRelationship.AddAffection(affectionGainPerDay);

                if (relationship.IsDating() &&
                    partnerRelationship.IsDating() &&
                    relationship.datingDays >= minDatingDaysToMarry &&
                    partnerRelationship.datingDays >= minDatingDaysToMarry &&
                    relationship.affection >= minAffectionToMarry &&
                    partnerRelationship.affection >= minAffectionToMarry &&
                    villager.GetAge() <= maxMarriageAge &&
                    partner.GetAge() <= maxMarriageAge)
                {
                    MarryPair(villager, relationship, partner, partnerRelationship);
                }

                continue;
            }

            if (IsEligibleForMatch(villager, relationship))
            {
                singlesBuffer.Add(villager);
            }
        }

        ShuffleSingles();
        TryFormNewPairs(lookup);
    }

    int GetRelationshipTickDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return 0;
        }

        if (!timeSystem.IsOneGameDayPerYearCalendar)
        {
            return timeSystem.CurrentDay;
        }

        timeSystem.GetDisplayCalendarDate(
            out int displayMonth,
            out int displayDay);
        int displayDayOfYear =
            ((Mathf.Max(1, displayMonth) - 1) *
            timeSystem.DisplayDaysPerMonth) +
            Mathf.Max(1, displayDay);
        return ((Mathf.Max(1, timeSystem.currentYear) - 1) *
            timeSystem.DisplayDaysPerYear) +
            displayDayOfYear;
    }

    public void HandleVillagerDeath(VillagerAI deceased)
    {
        if (deceased == null)
        {
            return;
        }

        NPCIdentity deadIdentity = GetIdentity(deceased);
        if (deadIdentity == null ||
            string.IsNullOrWhiteSpace(deadIdentity.npcId))
        {
            return;
        }

        VillagerRelationship deadRelationship = GetOrAddRelationship(deceased);
        if (deadRelationship == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(deadRelationship.partnerId))
        {
            deadIdentity.spouseId = string.Empty;
            return;
        }

        Dictionary<string, VillagerAI> lookup = BuildLookup(CollectVillagers());
        VillagerAI partner = FindPartner(
            deadRelationship.partnerId,
            lookup);
        if (partner != null)
        {
            VillagerRelationship partnerRelationship = GetOrAddRelationship(partner);
            if (partnerRelationship != null)
            {
                partnerRelationship.BecomeSingle();
                partnerRelationship.SyncIdentityState();
            }

            NPCIdentity partnerIdentity = GetIdentity(partner);
            if (partnerIdentity != null)
            {
                partnerIdentity.spouseId = string.Empty;
            }
        }

        deadIdentity.spouseId = string.Empty;
        deadRelationship.BecomeSingle();
        deadRelationship.SyncIdentityState();

        LogRelationship(
            UiText.Format(
                "worldNotifications",
                "relationshipWidowedFormat",
                deadIdentity.npcName));
    }

    void TryFormNewPairs(Dictionary<string, VillagerAI> lookup)
    {
        for (int i = 0; i < singlesBuffer.Count; i++)
        {
            VillagerAI villager = singlesBuffer[i];
            if (villager == null || villager.IsDead)
            {
                continue;
            }

            NPCIdentity identity = GetIdentity(villager);
            if (identity == null ||
                string.IsNullOrWhiteSpace(identity.npcId) ||
                pairedThisTick.Contains(identity.npcId))
            {
                continue;
            }

            if (Random.value > dailyMatchChance)
            {
                continue;
            }

            VillagerAI partner = FindCompatiblePartner(
                villager,
                lookup);
            if (partner == null)
            {
                continue;
            }

            VillagerRelationship relationship =
                GetOrAddRelationship(villager);
            VillagerRelationship partnerRelationship =
                GetOrAddRelationship(partner);
            if (relationship == null || partnerRelationship == null)
            {
                continue;
            }

            NPCIdentity partnerIdentity = GetIdentity(partner);
            if (partnerIdentity == null)
            {
                continue;
            }

            int childLimit = Random.Range(2, 4);
            relationship.StartDating(partnerIdentity.npcId, childLimit);
            partnerRelationship.StartDating(identity.npcId, childLimit);
            relationship.TickDaily();
            partnerRelationship.TickDaily();

            pairedThisTick.Add(identity.npcId);
            pairedThisTick.Add(partnerIdentity.npcId);

            LogRelationship(
                UiText.Format(
                    "worldNotifications",
                    "relationshipDatingStartFormat",
                    FormatName(villager),
                    FormatName(partner)));
        }
    }

    void ShuffleSingles()
    {
        for (int i = singlesBuffer.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            if (swapIndex == i)
            {
                continue;
            }

            VillagerAI temp = singlesBuffer[i];
            singlesBuffer[i] = singlesBuffer[swapIndex];
            singlesBuffer[swapIndex] = temp;
        }
    }

    void MarryPair(
        VillagerAI first,
        VillagerRelationship firstRelationship,
        VillagerAI second,
        VillagerRelationship secondRelationship)
    {
        if (first == null || second == null)
        {
            return;
        }

        NPCIdentity firstIdentity = GetIdentity(first);
        NPCIdentity secondIdentity = GetIdentity(second);
        if (firstIdentity == null || secondIdentity == null)
        {
            return;
        }

        firstRelationship.Marry(secondIdentity.npcId);
        secondRelationship.Marry(firstIdentity.npcId);
        firstRelationship.SyncIdentityState();
        secondRelationship.SyncIdentityState();
        MarriageHomeManager.NotifyPairMarried(first, second);

        LogRelationship(
            UiText.Format(
                "worldNotifications",
                "relationshipMarriedFormat",
                FormatName(first),
                FormatName(second)));
    }

    void BreakPair(
        VillagerAI villager,
        VillagerRelationship relationship,
        VillagerAI partner)
    {
        if (relationship == null)
        {
            return;
        }

        NPCIdentity identity = GetIdentity(villager);
        NPCIdentity partnerIdentity = GetIdentity(partner);
        string partnerName =
            partnerIdentity != null && !string.IsNullOrWhiteSpace(partnerIdentity.npcName)
                ? partnerIdentity.npcName
                : UiText.Get(
                    "worldNotifications",
                    "relationshipPartnerFallback",
                    "partner");

        relationship.BecomeSingle();
        relationship.SyncIdentityState();

        if (identity != null)
        {
            identity.spouseId = string.Empty;
        }

        LogRelationship(
            UiText.Format(
                "worldNotifications",
                "relationshipSeparatedFormat",
                FormatName(villager),
                partnerName));
    }

    void SyncPairLink(
        VillagerAI first,
        VillagerRelationship firstRelationship,
        VillagerAI second,
        VillagerRelationship secondRelationship)
    {
        NPCIdentity firstIdentity = GetIdentity(first);
        NPCIdentity secondIdentity = GetIdentity(second);
        if (firstIdentity == null || secondIdentity == null)
        {
            return;
        }

        if (!string.Equals(
                firstRelationship.partnerId,
                secondIdentity.npcId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            firstRelationship.partnerId = secondIdentity.npcId;
        }

        if (!string.Equals(
                secondRelationship.partnerId,
                firstIdentity.npcId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            secondRelationship.partnerId = firstIdentity.npcId;
        }

        if (firstRelationship.status == VillagerRelationshipStatus.Married ||
            secondRelationship.status == VillagerRelationshipStatus.Married)
        {
            firstRelationship.status = VillagerRelationshipStatus.Married;
            secondRelationship.status = VillagerRelationshipStatus.Married;
        }
        else
        {
            firstRelationship.status = VillagerRelationshipStatus.Dating;
            secondRelationship.status = VillagerRelationshipStatus.Dating;
        }

        firstRelationship.SyncIdentityState();
        secondRelationship.SyncIdentityState();
    }

    VillagerAI FindCompatiblePartner(
        VillagerAI source,
        Dictionary<string, VillagerAI> lookup)
    {
        if (source == null)
        {
            return null;
        }

        NPCIdentity sourceIdentity = GetIdentity(source);
        if (sourceIdentity == null)
        {
            return null;
        }

        int sourceAge = source.GetAge();
        if (sourceAge > maxMarriageAge)
        {
            return null;
        }

        VillagerAI bestPartner = null;
        int bestAgeGap = int.MaxValue;

        for (int i = 0; i < villagersBuffer.Count; i++)
        {
            VillagerAI candidate = villagersBuffer[i];
            if (candidate == null ||
                candidate == source ||
                candidate.IsDead)
            {
                continue;
            }

            NPCIdentity candidateIdentity = GetIdentity(candidate);
            if (candidateIdentity == null ||
                string.IsNullOrWhiteSpace(candidateIdentity.npcId))
            {
                continue;
            }

            if (pairedThisTick.Contains(candidateIdentity.npcId) ||
                candidateIdentity.gender == sourceIdentity.gender)
            {
                continue;
            }

            VillagerRelationship candidateRelationship =
                GetOrAddRelationship(candidate);
            if (candidateRelationship == null ||
                !candidateRelationship.IsSingle())
            {
                continue;
            }

            if (!IsEligibleForMatch(candidate, candidateRelationship))
            {
                continue;
            }

            if (!AreFamilyMatchAllowed(sourceIdentity, candidateIdentity))
            {
                continue;
            }

            int candidateAge = candidate.GetAge();
            if (candidateAge > maxMarriageAge)
            {
                continue;
            }

            int ageGap =
                Mathf.Abs(sourceAge - candidateAge);
            if (ageGap > maxAgeGapForMarriage)
            {
                continue;
            }

            if (ageGap < bestAgeGap)
            {
                bestAgeGap = ageGap;
                bestPartner = candidate;
            }
        }

        return bestPartner;
    }

    bool IsEligibleForMatch(
        VillagerAI villager,
        VillagerRelationship relationship)
    {
        if (villager == null ||
            villager.IsDead ||
            relationship == null ||
            !relationship.IsSingle())
        {
            return false;
        }

        NPCIdentity identity = GetIdentity(villager);
        if (identity == null)
        {
            return false;
        }

        int age = villager.GetAge();
        return age >= minAdultAge &&
            age <= maxMarriageAge;
    }

    bool AreFamilyMatchAllowed(
        NPCIdentity first,
        NPCIdentity second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        if (string.Equals(
                first.npcId,
                second.npcId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsDirectRelative(first, second) ||
            IsDirectRelative(second, first))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(first.fatherId) &&
            string.Equals(
                first.fatherId,
                second.fatherId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(first.motherId) &&
            string.Equals(
                first.motherId,
                second.motherId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    bool IsDirectRelative(
        NPCIdentity first,
        NPCIdentity second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        return string.Equals(
                first.fatherId,
                second.npcId,
                System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                first.motherId,
                second.npcId,
                System.StringComparison.OrdinalIgnoreCase);
    }

    VillagerAI FindPartner(
        string partnerId,
        Dictionary<string, VillagerAI> lookup)
    {
        if (string.IsNullOrWhiteSpace(partnerId))
        {
            return null;
        }

        if (lookup != null &&
            lookup.TryGetValue(partnerId, out VillagerAI directPartner))
        {
            return directPartner;
        }

        for (int i = 0; i < villagersBuffer.Count; i++)
        {
            VillagerAI candidate = villagersBuffer[i];
            NPCIdentity identity = GetIdentity(candidate);
            if (identity != null &&
                string.Equals(
                    identity.npcId,
                    partnerId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    List<VillagerAI> CollectVillagers()
    {
        villagersBuffer.Clear();

        VillagerAI[] villagers =
            Object.FindObjectsByType<VillagerAI>(FindObjectsInactive.Include);
        for (int i = 0; i < villagers.Length; i++)
        {
            VillagerAI villager = villagers[i];
            if (villager != null && !villager.IsDead)
            {
                villagersBuffer.Add(villager);
            }
        }

        return villagersBuffer;
    }

    Dictionary<string, VillagerAI> BuildLookup(List<VillagerAI> villagers)
    {
        Dictionary<string, VillagerAI> lookup =
            new Dictionary<string, VillagerAI>(System.StringComparer.OrdinalIgnoreCase);

        if (villagers == null)
        {
            return lookup;
        }

        for (int i = 0; i < villagers.Count; i++)
        {
            VillagerAI villager = villagers[i];
            NPCIdentity identity = GetIdentity(villager);
            if (villager == null ||
                identity == null ||
                string.IsNullOrWhiteSpace(identity.npcId))
            {
                continue;
            }

            lookup[identity.npcId] = villager;
        }

        return lookup;
    }

    VillagerRelationship GetOrAddRelationship(VillagerAI villager)
    {
        if (villager == null)
        {
            return null;
        }

        VillagerRelationship relationship =
            villager.GetComponent<VillagerRelationship>();
        if (relationship == null)
        {
            relationship = villager.gameObject.AddComponent<VillagerRelationship>();
        }

        relationship.SyncIdentityState();
        return relationship;
    }

    NPCIdentity GetIdentity(VillagerAI villager)
    {
        if (villager == null)
        {
            return null;
        }

        return villager.GetComponent<NPCIdentity>() ??
            villager.GetComponentInParent<NPCIdentity>(true) ??
            villager.GetComponentInChildren<NPCIdentity>(true);
    }

    string FormatName(VillagerAI villager)
    {
        NPCIdentity identity = GetIdentity(villager);
        if (identity != null &&
            !string.IsNullOrWhiteSpace(identity.npcName))
        {
            return identity.npcName;
        }

        return villager != null ? villager.gameObject.name : "NPC";
    }

    void LogRelationship(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.AddLog(message, 0, false);
            return;
        }

        Debug.Log("[VillagerRelationship] " + message);
    }

    void ApplyBaselinePacing()
    {
        minAdultAge = NpcLifeStageDefaults.AdultMinAge;
        maxMarriageAge =
            Mathf.Max(
                minAdultAge,
                NpcLifeStageDefaults.MiddleMaxAge);
        dailyMatchChance =
            Mathf.Clamp(
                Mathf.Max(dailyMatchChance, MinimumDailyMatchChance),
                0f,
                1f);
        minDatingDaysToMarry =
            Mathf.Max(
                1,
                Mathf.Min(
                    minDatingDaysToMarry,
                    MaximumDatingDaysToMarry));
        minAffectionToMarry =
            Mathf.Clamp(
                Mathf.Min(
                    minAffectionToMarry,
                    MaximumAffectionToMarry),
                0,
                100);
        affectionGainPerDay =
            Mathf.Clamp(
                Mathf.Max(
                    affectionGainPerDay,
                    MinimumAffectionGainPerDay),
                0,
                100);
    }
}
