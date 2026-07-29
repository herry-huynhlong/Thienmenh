using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MarriageHomeManager : MonoBehaviour
{
    struct PairContext
    {
        public VillagerAI first;
        public VillagerAI second;
        public NPCIdentity firstIdentity;
        public NPCIdentity secondIdentity;
        public VillagerRelationship firstRelationship;
        public VillagerRelationship secondRelationship;
    }

    public static MarriageHomeManager Instance { get; private set; }

    static readonly List<MarriageHomeSite> registeredSites =
        new List<MarriageHomeSite>();

    [Header("Build Rules")]
    [Min(0.1f)] public float checkIntervalSeconds = 1f;
    [Min(0)] public int requiredBuildCostSpiritStone = 5000;
    [Min(0.05f)] public float buildDurationWorldDays = 8f / 24f;
    [Min(0.05f)] public float gatherAtBuildPointDistance = 0.25f;
    [Range(0f, 23.99f)] public float weddingNightWakeHour = 6f;
    public bool hideCoupleDuringConstruction = true;
    public bool guaranteeFirstChildAfterWeddingNight = true;

    readonly Dictionary<string, GameObject> runtimeHomesBySiteId =
        new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> processedPairKeys =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    float nextCheckTime;

    public static void RegisterSite(MarriageHomeSite site)
    {
        if (site == null)
        {
            return;
        }

        EnsureInstance();
        if (!registeredSites.Contains(site))
        {
            registeredSites.Add(site);
        }
    }

    public static void UnregisterSite(MarriageHomeSite site)
    {
        if (site == null)
        {
            return;
        }

        registeredSites.Remove(site);
        if (Instance != null)
        {
            Instance.DestroyRuntimeHome(site.GetResolvedSiteId());
        }
    }

    public static MarriageHomeManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        MarriageHomeManager existing =
            FindAnyObjectByType<MarriageHomeManager>(
                FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject created = new GameObject("MarriageHomeManager");
        Instance = created.AddComponent<MarriageHomeManager>();
        return Instance;
    }

    public static void NotifyPairMarried(
        VillagerAI first,
        VillagerAI second)
    {
        if (first == null ||
            second == null)
        {
            return;
        }

        MarriageHomeManager manager = EnsureInstance();
        if (manager != null)
        {
            manager.nextCheckTime = 0f;
        }
    }

    public void RequestImmediateRefresh()
    {
        nextCheckTime = 0f;
    }

    public void DebugProcessNow()
    {
        CleanupInvalidSites();
        SyncAssignedHomes();
        ProcessMarriedPairs();
        SyncRuntimeHomeVisuals();
        nextCheckTime =
            Time.time + Mathf.Max(0.1f, checkIntervalSeconds);
    }

    void Awake()
    {
        if (Instance != null &&
            Instance != this)
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

    void Update()
    {
        bool hasActiveFlow = HasActiveMarriageFlow();
        if (!hasActiveFlow &&
            Time.time < nextCheckTime)
        {
            return;
        }

        if (!hasActiveFlow)
        {
            nextCheckTime =
                Time.time + Mathf.Max(0.1f, checkIntervalSeconds);
        }

        CleanupInvalidSites();
        SyncAssignedHomes();
        ProcessMarriedPairs();
        SyncRuntimeHomeVisuals();
    }

    void ProcessMarriedPairs()
    {
        processedPairKeys.Clear();

        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(FindObjectsInactive.Include);
        Dictionary<string, VillagerAI> lookup =
            BuildLookup(villagers);

        for (int i = 0; i < villagers.Length; i++)
        {
            if (!TryBuildPairContext(
                    villagers[i],
                    lookup,
                    out PairContext pair))
            {
                continue;
            }

            string pairKey = BuildPairKey(
                pair.firstIdentity.npcId,
                pair.secondIdentity.npcId);
            if (!processedPairKeys.Add(pairKey))
            {
                continue;
            }

            ProcessPair(pair);
        }
    }

    void ProcessPair(PairContext pair)
    {
        MarriageHomeProjectState state =
            pair.firstRelationship.marriageHomeProjectState;
        string sharedAssignedHomeId =
            ResolveSharedAssignedHomeId(pair);
        MarriageHomeSite currentSharedSite =
            FindSite(sharedAssignedHomeId);
        if (currentSharedSite != null &&
            state != MarriageHomeProjectState.WeddingNight)
        {
            pair.firstRelationship.ClearMarriageHomeProject();
            pair.secondRelationship.ClearMarriageHomeProject();
            return;
        }

        string siteId = pair.firstRelationship.marriageHomeSiteId;
        MarriageHomeSite site = FindSite(siteId);

        if (state == MarriageHomeProjectState.None ||
            string.IsNullOrWhiteSpace(siteId))
        {
            site = FindAvailableSiteForPair(pair);
            if (site == null)
            {
                return;
            }

            int cost =
                Mathf.Max(
                    0,
                    requiredBuildCostSpiritStone);

            pair.firstRelationship.BeginMarriageHomeSaving(
                site.GetResolvedSiteId(),
                cost);
            pair.secondRelationship.BeginMarriageHomeSaving(
                site.GetResolvedSiteId(),
                cost);
            LogMarriageHome(
                pair,
                UiText.Format(
                    "worldNotifications",
                    "marriageHomeChooseSiteFormat",
                    site.GetDisplayName(),
                    cost));
            return;
        }

        if (site == null ||
            IsSiteOccupiedByOtherPair(site, pair))
        {
            MarriageHomeSite fallbackSite =
                FindAvailableSiteForPair(pair);
            if (fallbackSite == null)
            {
                return;
            }

            pair.firstRelationship.BeginMarriageHomeSaving(
                fallbackSite.GetResolvedSiteId(),
                pair.firstRelationship.marriageHomeCostSpiritStone);
            pair.secondRelationship.BeginMarriageHomeSaving(
                fallbackSite.GetResolvedSiteId(),
                pair.firstRelationship.marriageHomeCostSpiritStone);
            return;
        }

        if (state == MarriageHomeProjectState.Saving)
        {
            TryStartConstruction(pair, site);
            return;
        }

        if (state == MarriageHomeProjectState.Building)
        {
            SyncPairConstructionVisibility(pair, site);
            TryFinishConstruction(pair, site);
            return;
        }

        if (state == MarriageHomeProjectState.WeddingNight)
        {
            SyncPairWeddingNightVisibility(pair, site);
            TryFinishWeddingNight(pair, site);
        }
    }

    void TryStartConstruction(
        PairContext pair,
        MarriageHomeSite site)
    {
        int cost =
            Mathf.Max(
                0,
                pair.firstRelationship.marriageHomeCostSpiritStone);
        if (GetTotalSpiritStone(pair) < cost)
        {
            return;
        }

        if (!GuidePairToBuildSite(pair, site))
        {
            return;
        }

        SpendSpiritStone(pair.first, pair.second, cost);

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        int startDay =
            timeSystem != null
                ? Mathf.Max(1, timeSystem.CurrentAbsoluteDay)
                : 1;
        float startHour =
            timeSystem != null
                ? timeSystem.CurrentHour
                : 6f;

        pair.firstRelationship.BeginMarriageHomeBuilding(
            site.GetResolvedSiteId(),
            cost,
            startDay,
            startHour);
        pair.secondRelationship.BeginMarriageHomeBuilding(
            site.GetResolvedSiteId(),
            cost,
            startDay,
            startHour);

        EnsureRuntimeHome(
            site,
            pair.firstRelationship,
            false);
        SyncPairConstructionVisibility(pair, site);
        LogMarriageHome(
            pair,
            UiText.Format(
                "worldNotifications",
                "marriageHomeBuildStartFormat",
                site.GetDisplayName()));
    }

    void TryFinishConstruction(
        PairContext pair,
        MarriageHomeSite site)
    {
        if (!HasConstructionFinished(pair.firstRelationship))
        {
            return;
        }

        AssignHomeToVillager(pair.first, pair.firstIdentity, site);
        AssignHomeToVillager(pair.second, pair.secondIdentity, site);

        EnsureRuntimeHome(site, null, true);

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        int startDay =
            timeSystem != null
                ? Mathf.Max(1, timeSystem.CurrentAbsoluteDay)
                : 1;
        float startHour =
            timeSystem != null
                ? timeSystem.CurrentHour
                : 21f;

        string siteId = site.GetResolvedSiteId();
        pair.firstRelationship.BeginMarriageHomeWeddingNight(
            siteId,
            startDay,
            startHour);
        pair.secondRelationship.BeginMarriageHomeWeddingNight(
            siteId,
            startDay,
            startHour);
        SyncPairWeddingNightVisibility(pair, site);

        LogMarriageHome(
            pair,
            UiText.Format(
                "worldNotifications",
                "marriageHomeBuildCompleteFormat",
                site.GetDisplayName()));
    }

    void SyncAssignedHomes()
    {
        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(FindObjectsInactive.Include);
        for (int i = 0; i < villagers.Length; i++)
        {
            VillagerAI villager = villagers[i];
            NPCIdentity identity = GetIdentity(villager);
            if (villager == null ||
                identity == null ||
                string.IsNullOrWhiteSpace(identity.homeId))
            {
                continue;
            }

            MarriageHomeSite site = FindSite(identity.homeId);
            if (site == null)
            {
                continue;
            }

            Transform resolvedHome =
                site.GetResolvedHomePoint();
            if (resolvedHome != null &&
                villager.homePoint != resolvedHome)
            {
                villager.homePoint = resolvedHome;
            }
        }
    }

    void SyncRuntimeHomeVisuals()
    {
        for (int i = 0; i < registeredSites.Count; i++)
        {
            MarriageHomeSite site = registeredSites[i];
            if (site == null)
            {
                continue;
            }

            MarriageHomeProjectState state =
                GetSiteProjectState(site, out VillagerRelationship relationship);
            bool shouldExist =
                state == MarriageHomeProjectState.Building ||
                HasResidentsAssignedToSite(site.GetResolvedSiteId());

            if (!shouldExist)
            {
                DestroyRuntimeHome(site.GetResolvedSiteId());
                continue;
            }

            EnsureRuntimeHome(
                site,
                relationship,
                state != MarriageHomeProjectState.Building);
        }
    }

    MarriageHomeProjectState GetSiteProjectState(
        MarriageHomeSite site,
        out VillagerRelationship relationship)
    {
        relationship = null;
        if (site == null)
        {
            return MarriageHomeProjectState.None;
        }

        VillagerRelationship[] relationships =
            FindObjectsByType<VillagerRelationship>(
                FindObjectsInactive.Exclude);
        string siteId = site.GetResolvedSiteId();

        for (int i = 0; i < relationships.Length; i++)
        {
            VillagerRelationship candidate = relationships[i];
            if (candidate == null ||
                candidate.marriageHomeProjectState ==
                    MarriageHomeProjectState.None ||
                !string.Equals(
                    candidate.marriageHomeSiteId,
                    siteId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            relationship = candidate;
            return candidate.marriageHomeProjectState;
        }

        return MarriageHomeProjectState.None;
    }

    void EnsureRuntimeHome(
        MarriageHomeSite site,
        VillagerRelationship relationship,
        bool buildComplete)
    {
        if (site == null ||
            site.housePrefab == null)
        {
            return;
        }

        string siteId = site.GetResolvedSiteId();
        if (!runtimeHomesBySiteId.TryGetValue(
                siteId,
                out GameObject existing) ||
            existing == null)
        {
            existing =
                Instantiate(
                    site.housePrefab,
                    site.GetResolvedBuildPosition(),
                    Quaternion.identity);
            existing.name = "MarriageHome_" + siteId;
            runtimeHomesBySiteId[siteId] = existing;
        }
        else
        {
            existing.transform.position =
                site.GetResolvedBuildPosition();
        }

        WorldMarriageHome worldHome =
            existing.GetComponent<WorldMarriageHome>();
        if (worldHome == null)
        {
            return;
        }

        if (buildComplete)
        {
            worldHome.SetBuiltImmediate();
            return;
        }

        if (relationship == null)
        {
            return;
        }

        worldHome.BeginConstruction(
            relationship.marriageHomeBuildStartAbsoluteDay,
            relationship.marriageHomeBuildStartHour,
            buildDurationWorldDays);
    }

    void DestroyRuntimeHome(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) ||
            !runtimeHomesBySiteId.TryGetValue(siteId, out GameObject existing))
        {
            return;
        }

        runtimeHomesBySiteId.Remove(siteId);
        if (existing != null)
        {
            Destroy(existing);
        }
    }

    void CleanupInvalidSites()
    {
        for (int i = registeredSites.Count - 1; i >= 0; i--)
        {
            if (registeredSites[i] == null)
            {
                registeredSites.RemoveAt(i);
            }
        }
    }

    MarriageHomeSite FindAvailableSiteForPair(PairContext pair)
    {
        List<MarriageHomeSite> orderedSites =
            new List<MarriageHomeSite>(registeredSites);
        orderedSites.Sort(
            (left, right) =>
            {
                int leftPriority =
                    left != null
                        ? left.priority
                        : int.MaxValue;
                int rightPriority =
                    right != null
                        ? right.priority
                        : int.MaxValue;
                if (leftPriority != rightPriority)
                {
                    return leftPriority.CompareTo(rightPriority);
                }

                string leftId =
                    left != null
                        ? left.GetResolvedSiteId()
                        : string.Empty;
                string rightId =
                    right != null
                        ? right.GetResolvedSiteId()
                        : string.Empty;
                return string.Compare(
                    leftId,
                    rightId,
                    System.StringComparison.OrdinalIgnoreCase);
            });

        for (int i = 0; i < orderedSites.Count; i++)
        {
            MarriageHomeSite site = orderedSites[i];
            if (site == null ||
                site.housePrefab == null ||
                site.GetResolvedHomePoint() == null ||
                IsSiteOccupiedByOtherPair(site, pair))
            {
                continue;
            }

            return site;
        }

        return null;
    }

    bool IsSiteOccupiedByOtherPair(
        MarriageHomeSite site,
        PairContext pair)
    {
        if (site == null)
        {
            return true;
        }

        string siteId = site.GetResolvedSiteId();
        string firstId = pair.firstIdentity.npcId;
        string secondId = pair.secondIdentity.npcId;

        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(FindObjectsInactive.Include);
        for (int i = 0; i < villagers.Length; i++)
        {
            NPCIdentity identity = GetIdentity(villagers[i]);
            if (identity == null ||
                string.IsNullOrWhiteSpace(identity.homeId) ||
                !string.Equals(
                    identity.homeId,
                    siteId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(identity.npcId, firstId, System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(identity.npcId, secondId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        VillagerRelationship[] relationships =
            FindObjectsByType<VillagerRelationship>(
                FindObjectsInactive.Exclude);
        for (int i = 0; i < relationships.Length; i++)
        {
            VillagerRelationship relationship = relationships[i];
            if (relationship == null ||
                relationship.marriageHomeProjectState ==
                    MarriageHomeProjectState.None ||
                !string.Equals(
                    relationship.marriageHomeSiteId,
                    siteId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            NPCIdentity identity =
                relationship.GetComponent<NPCIdentity>();
            if (identity == null)
            {
                continue;
            }

            if (string.Equals(identity.npcId, firstId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identity.npcId, secondId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relationship.partnerId, firstId, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relationship.partnerId, secondId, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    bool HasResidentsAssignedToSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return false;
        }

        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(FindObjectsInactive.Include);
        for (int i = 0; i < villagers.Length; i++)
        {
            NPCIdentity identity = GetIdentity(villagers[i]);
            if (identity != null &&
                string.Equals(
                    identity.homeId,
                    siteId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    bool HasConstructionFinished(VillagerRelationship relationship)
    {
        if (relationship == null ||
            relationship.marriageHomeBuildStartAbsoluteDay == int.MinValue)
        {
            return false;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return false;
        }

        float currentAbsoluteHours =
            (timeSystem.CurrentAbsoluteDay - 1) * 24f +
            timeSystem.CurrentHour;
        float startAbsoluteHours =
            (relationship.marriageHomeBuildStartAbsoluteDay - 1) * 24f +
            relationship.marriageHomeBuildStartHour;
        return currentAbsoluteHours - startAbsoluteHours >=
            Mathf.Max(0.25f, buildDurationWorldDays) * 24f;
    }

    bool HasWeddingNightFinished(VillagerRelationship relationship)
    {
        if (relationship == null ||
            relationship.marriageHomeBuildStartAbsoluteDay == int.MinValue)
        {
            return false;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return false;
        }

        float currentAbsoluteHours =
            (timeSystem.CurrentAbsoluteDay - 1) * 24f +
            timeSystem.CurrentHour;
        float startAbsoluteHours =
            (relationship.marriageHomeBuildStartAbsoluteDay - 1) * 24f +
            relationship.marriageHomeBuildStartHour;
        float wakeHour =
            Mathf.Clamp(weddingNightWakeHour, 0f, 23.99f);
        float wakeAbsoluteHours =
            relationship.marriageHomeBuildStartHour < wakeHour
                ? (relationship.marriageHomeBuildStartAbsoluteDay - 1) * 24f +
                    wakeHour
                : relationship.marriageHomeBuildStartAbsoluteDay * 24f +
                    wakeHour;
        wakeAbsoluteHours =
            Mathf.Max(
                wakeAbsoluteHours,
                startAbsoluteHours + 6f);
        return currentAbsoluteHours >= wakeAbsoluteHours;
    }

    void TryFinishWeddingNight(
        PairContext pair,
        MarriageHomeSite site)
    {
        if (!HasWeddingNightFinished(pair.firstRelationship))
        {
            return;
        }

        pair.firstRelationship.ClearMarriageHomeProject();
        pair.secondRelationship.ClearMarriageHomeProject();
        RevealPairAtHome(pair, site);

        LogMarriageHome(
            pair,
            UiText.Get(
                "worldNotifications",
                "marriageHomeWeddingNightComplete",
                "completed their wedding night and began living in their new home."));
    }

    void AssignHomeToVillager(
        VillagerAI villager,
        NPCIdentity identity,
        MarriageHomeSite site)
    {
        if (villager == null ||
            identity == null ||
            site == null)
        {
            return;
        }

        identity.homeId = site.GetResolvedSiteId();
        villager.homePoint = site.GetResolvedHomePoint();
        villager.SyncNpcIdentityData();
    }

    bool HasActiveMarriageFlow()
    {
        VillagerRelationship[] relationships =
            FindObjectsByType<VillagerRelationship>(
                FindObjectsInactive.Exclude);
        for (int i = 0; i < relationships.Length; i++)
        {
            VillagerRelationship relationship = relationships[i];
            if (relationship == null ||
                relationship.marriageHomeProjectState ==
                    MarriageHomeProjectState.None ||
                string.IsNullOrWhiteSpace(
                    relationship.marriageHomeSiteId))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    string ResolveSharedAssignedHomeId(PairContext pair)
    {
        string firstHomeId =
            pair.firstIdentity != null
                ? pair.firstIdentity.homeId
                : string.Empty;
        string secondHomeId =
            pair.secondIdentity != null
                ? pair.secondIdentity.homeId
                : string.Empty;

        return !string.IsNullOrWhiteSpace(firstHomeId) &&
            string.Equals(
                firstHomeId,
                secondHomeId,
                System.StringComparison.OrdinalIgnoreCase)
            ? firstHomeId
            : string.Empty;
    }

    int GetTotalSpiritStone(PairContext pair)
    {
        return Mathf.Max(0, pair.first.spiritStone) +
            Mathf.Max(0, pair.second.spiritStone);
    }

    bool GuidePairToBuildSite(
        PairContext pair,
        MarriageHomeSite site)
    {
        if (pair.first == null ||
            pair.second == null ||
            site == null)
        {
            return false;
        }

        Vector3 target = site.GetResolvedBuildPosition();
        bool firstReady =
            MoveVillagerToMarriageTarget(pair.first, target);
        bool secondReady =
            MoveVillagerToMarriageTarget(pair.second, target);
        return firstReady && secondReady;
    }

    bool MoveVillagerToMarriageTarget(
        VillagerAI villager,
        Vector3 target)
    {
        if (villager == null)
        {
            return false;
        }

        if (!villager.enabled)
        {
            villager.enabled = true;
        }

        villager.ForceHiddenAtHome(false);
        NpcRoleUtility.SetAction(villager.gameObject, "Di xay nha tan hon");
        NpcRoleUtility.MoveTowards(
            villager.gameObject,
            target,
            Mathf.Max(1f, villager.moveSpeed));
        return Vector2.Distance(
            villager.transform.position,
            target) <= Mathf.Max(0.05f, gatherAtBuildPointDistance);
    }

    void SyncPairConstructionVisibility(
        PairContext pair,
        MarriageHomeSite site)
    {
        if (!hideCoupleDuringConstruction ||
            site == null)
        {
            return;
        }

        Vector3 target = site.GetResolvedBuildPosition();
        HideVillagerForMarriagePhase(pair.first, target);
        HideVillagerForMarriagePhase(pair.second, target);
    }

    void SyncPairWeddingNightVisibility(
        PairContext pair,
        MarriageHomeSite site)
    {
        if (site == null)
        {
            return;
        }

        Transform homePoint = site.GetResolvedHomePoint();
        Vector3 target =
            homePoint != null
                ? homePoint.position
                : site.transform.position;
        HideVillagerForMarriagePhase(pair.first, target);
        HideVillagerForMarriagePhase(pair.second, target);
    }

    void HideVillagerForMarriagePhase(
        VillagerAI villager,
        Vector3 target)
    {
        if (villager == null)
        {
            return;
        }

        if (!villager.enabled)
        {
            villager.enabled = true;
        }

        villager.transform.position = target;
        villager.StopMoving();
        villager.ForceHiddenAtHome(true);
        villager.enabled = false;
    }

    void RevealPairAtHome(
        PairContext pair,
        MarriageHomeSite site)
    {
        Transform homePoint =
            site != null
                ? site.GetResolvedHomePoint()
                : null;
        Vector3 target =
            homePoint != null
                ? homePoint.position
                : pair.first != null
                    ? pair.first.transform.position
                    : Vector3.zero;
        RevealVillagerAfterMarriagePhase(pair.first, target);
        RevealVillagerAfterMarriagePhase(pair.second, target);
    }

    void RevealVillagerAfterMarriagePhase(
        VillagerAI villager,
        Vector3 target)
    {
        if (villager == null)
        {
            return;
        }

        villager.enabled = true;
        villager.transform.position = target;
        villager.ForceHiddenAtHome(false);
        NpcRoleUtility.SetAction(villager.gameObject, NpcText.Action("idle"));
    }

    void SpendSpiritStone(
        VillagerAI first,
        VillagerAI second,
        int amount)
    {
        if (first == null ||
            second == null ||
            amount <= 0)
        {
            return;
        }

        int half = Mathf.CeilToInt(amount * 0.5f);
        int takeFirst =
            Mathf.Min(
                Mathf.Max(0, first.spiritStone),
                half);
        int remaining = amount - takeFirst;
        int takeSecond =
            Mathf.Min(
                Mathf.Max(0, second.spiritStone),
                remaining);
        remaining -= takeSecond;

        if (remaining > 0)
        {
            int extraFirst =
                Mathf.Min(
                    Mathf.Max(0, first.spiritStone - takeFirst),
                    remaining);
            takeFirst += extraFirst;
            remaining -= extraFirst;
        }

        if (remaining > 0)
        {
            int extraSecond =
                Mathf.Min(
                    Mathf.Max(0, second.spiritStone - takeSecond),
                    remaining);
            takeSecond += extraSecond;
        }

        first.spiritStone =
            Mathf.Max(0, first.spiritStone - takeFirst);
        second.spiritStone =
            Mathf.Max(0, second.spiritStone - takeSecond);

        if (first.entityProfile != null)
        {
            first.entityProfile.stats.spiritStone =
                first.spiritStone;
        }

        if (second.entityProfile != null)
        {
            second.entityProfile.stats.spiritStone =
                second.spiritStone;
        }
    }

    bool TryBuildPairContext(
        VillagerAI villager,
        Dictionary<string, VillagerAI> lookup,
        out PairContext pair)
    {
        pair = default;
        if (villager == null ||
            villager.IsDead)
        {
            return false;
        }

        NPCIdentity identity = GetIdentity(villager);
        VillagerRelationship relationship =
            villager.GetComponent<VillagerRelationship>();
        if (identity == null ||
            relationship == null ||
            !relationship.IsMarried() ||
            string.IsNullOrWhiteSpace(identity.npcId) ||
            string.IsNullOrWhiteSpace(relationship.partnerId) ||
            lookup == null ||
            !lookup.TryGetValue(relationship.partnerId, out VillagerAI partner) ||
            partner == null ||
            partner.IsDead)
        {
            return false;
        }

        NPCIdentity partnerIdentity = GetIdentity(partner);
        VillagerRelationship partnerRelationship =
            partner.GetComponent<VillagerRelationship>();
        if (partnerIdentity == null ||
            partnerRelationship == null ||
            !partnerRelationship.IsMarried())
        {
            return false;
        }

        pair.first = villager;
        pair.second = partner;
        pair.firstIdentity = identity;
        pair.secondIdentity = partnerIdentity;
        pair.firstRelationship = relationship;
        pair.secondRelationship = partnerRelationship;
        return true;
    }

    static Dictionary<string, VillagerAI> BuildLookup(VillagerAI[] villagers)
    {
        Dictionary<string, VillagerAI> lookup =
            new Dictionary<string, VillagerAI>(System.StringComparer.OrdinalIgnoreCase);
        if (villagers == null)
        {
            return lookup;
        }

        for (int i = 0; i < villagers.Length; i++)
        {
            NPCIdentity identity = GetIdentity(villagers[i]);
            if (villagers[i] == null ||
                identity == null ||
                string.IsNullOrWhiteSpace(identity.npcId))
            {
                continue;
            }

            lookup[identity.npcId] = villagers[i];
        }

        return lookup;
    }

    static string BuildPairKey(
        string firstId,
        string secondId)
    {
        string left = firstId ?? string.Empty;
        string right = secondId ?? string.Empty;
        return string.Compare(
                left,
                right,
                System.StringComparison.OrdinalIgnoreCase) <= 0
            ? left + "|" + right
            : right + "|" + left;
    }

    static MarriageHomeSite FindSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return null;
        }

        for (int i = 0; i < registeredSites.Count; i++)
        {
            MarriageHomeSite site = registeredSites[i];
            if (site != null &&
                string.Equals(
                    site.GetResolvedSiteId(),
                    siteId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return site;
            }
        }

        return null;
    }

    static NPCIdentity GetIdentity(VillagerAI villager)
    {
        if (villager == null)
        {
            return null;
        }

        return villager.GetComponent<NPCIdentity>() ??
            villager.GetComponentInParent<NPCIdentity>(true) ??
            villager.GetComponentInChildren<NPCIdentity>(true);
    }

    void LogMarriageHome(
        PairContext pair,
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string firstName =
            pair.firstIdentity != null &&
            !string.IsNullOrWhiteSpace(pair.firstIdentity.npcName)
                ? pair.firstIdentity.npcName
                : pair.first != null
                    ? pair.first.gameObject.name
                    : "NPC";
        string secondName =
            pair.secondIdentity != null &&
            !string.IsNullOrWhiteSpace(pair.secondIdentity.npcName)
                ? pair.secondIdentity.npcName
                : pair.second != null
                    ? pair.second.gameObject.name
                    : "NPC";
        string content =
            UiText.Format(
                "worldNotifications",
                "marriageHomePairFormat",
                firstName,
                secondName,
                message);

        if (WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.AddLog(content, 0, false);
            return;
        }

        Debug.Log("[MarriageHome] " + content);
    }
}
