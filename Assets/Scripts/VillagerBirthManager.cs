using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-69)]
public class VillagerBirthManager : MonoBehaviour
{
    public static VillagerBirthManager Instance;

    const float MinimumDailyConceptionChance = 0.25f;
    const int MaximumPregnancyDurationDays = 2;
    const int MaximumBirthCooldownDays = 5;
    const int MaximumParentAge = 40;
    const int MaximumMarriageDaysBeforeConception = 2;

    [Header("Birth")]
    public GameObject childPrefab;
    public string childPrefabKey = "villager-child:default";
    public NPCVisualProfile defaultVillagerVisualProfile;
    public float dailyConceptionChance = 0.08f;
    public int pregnancyDurationDays = 3;
    public int birthCooldownDays = 90;
    public int minMarriageDaysBeforeConception = 7;
    public int minMotherAge = NpcLifeStageDefaults.AdultMinAge;
    public int maxMotherAge = MaximumParentAge;
    public int maxFatherAge = MaximumParentAge;
    public int maxPopulation = 0;
    public float spawnOffsetRadius = 0.6f;

    [Header("Rapid Child Growth")]
    [Min(1)] public int rapidChildGrowthDurationDays = 3;
    [Min(0.05f)] public float babySpawnScale = 0.7f;
    [Min(0.05f)] public float childGrowthScale = 0.5f;

    readonly List<VillagerAI> villagersBuffer = new List<VillagerAI>();

    int lastProcessedDay = int.MinValue;

    public static VillagerBirthManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        VillagerBirthManager[] managers =
            Object.FindObjectsByType<VillagerBirthManager>(
                FindObjectsInactive.Include);

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null)
            {
                Instance = managers[i];
                return Instance;
            }
        }

        GameObject created = new GameObject("VillagerBirthManager");
        Instance = created.AddComponent<VillagerBirthManager>();
        return Instance;
    }

    void Awake()
    {
        ResolveDefaultVillagerVisualProfile();
        RegisterChildPrefab();
        ApplyBaselinePacing();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnValidate()
    {
        ResolveDefaultVillagerVisualProfile();
        RegisterChildPrefab();
        ApplyBaselinePacing();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void DailyBirthTick()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        int day = timeSystem != null ? timeSystem.CurrentDay : 0;
        if (day == lastProcessedDay)
        {
            return;
        }

        lastProcessedDay = day;
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

        if (maxPopulation > 0 &&
            villagersBuffer.Count >= maxPopulation)
        {
            return;
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
                identity.gender != Gender.Female)
            {
                continue;
            }

            int age = villager.GetAge();
            if (age < minMotherAge || age > maxMotherAge)
            {
                continue;
            }

            VillagerRelationship relationship = GetOrAddRelationship(villager);
            if (relationship == null ||
                !relationship.IsMarried())
            {
                continue;
            }

            VillagerAI partner = FindPartner(relationship.partnerId, lookup);
            if (partner == null || partner.IsDead)
            {
                continue;
            }

            NPCIdentity partnerIdentity = GetIdentity(partner);
            if (partnerIdentity == null ||
                partnerIdentity.gender != Gender.Male)
            {
                continue;
            }

            int partnerAge = partner.GetAge();
            if (partnerAge < minMotherAge || partnerAge > maxFatherAge)
            {
                continue;
            }

            if (relationship.birthCooldownDays > 0)
            {
                continue;
            }

            if (relationship.pregnant)
            {
                if (relationship.TickPregnancy())
                {
                    SpawnChild(villager, partner, relationship, lookup);
                }

                continue;
            }

            if (relationship.marriedDays < minMarriageDaysBeforeConception)
            {
                continue;
            }

            if (!relationship.CanConceive())
            {
                continue;
            }

            if (Random.value > dailyConceptionChance)
            {
                continue;
            }

            relationship.BeginPregnancy(pregnancyDurationDays);
            LogBirth(
                FormatName(villager) +
                " đã mang thai con đầu lòng của " +
                FormatName(partner) +
                ".");
        }
    }

    public bool TryForceBirthForPair(
        VillagerAI first,
        VillagerAI second)
    {
        if (first == null ||
            second == null ||
            first.IsDead ||
            second.IsDead)
        {
            return false;
        }

        VillagerAI mother = null;
        VillagerAI father = null;

        NPCIdentity firstIdentity = GetIdentity(first);
        NPCIdentity secondIdentity = GetIdentity(second);
        if (firstIdentity != null &&
            firstIdentity.gender == Gender.Female)
        {
            mother = first;
            father = second;
        }
        else if (secondIdentity != null &&
            secondIdentity.gender == Gender.Female)
        {
            mother = second;
            father = first;
        }

        if (mother == null ||
            father == null)
        {
            return false;
        }

        VillagerRelationship motherRelationship =
            GetOrAddRelationship(mother);
        if (motherRelationship == null ||
            !motherRelationship.IsMarried() ||
            !motherRelationship.CanHaveMoreChildren())
        {
            return false;
        }

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

        if (maxPopulation > 0 &&
            villagersBuffer.Count >= maxPopulation)
        {
            return false;
        }

        Dictionary<string, VillagerAI> lookup =
            BuildLookup(villagersBuffer);
        SpawnChild(
            mother,
            father,
            motherRelationship,
            lookup);
        return true;
    }

    void SpawnChild(
        VillagerAI mother,
        VillagerAI father,
        VillagerRelationship motherRelationship,
        Dictionary<string, VillagerAI> lookup)
    {
        if (mother == null || father == null || motherRelationship == null)
        {
            return;
        }

        if (maxPopulation > 0 &&
            villagersBuffer.Count >= maxPopulation)
        {
            return;
        }

        GameObject spawned =
            childPrefab != null
                ? Instantiate(childPrefab)
                : Instantiate(mother.gameObject);

        if (spawned == null)
        {
            return;
        }

        if (!spawned.activeSelf)
        {
            spawned.SetActive(true);
        }

        spawned.name =
            string.IsNullOrWhiteSpace(mother.gameObject.name)
                ? "VillagerChild"
                : mother.gameObject.name + "_Child";
        spawned.transform.position =
            mother.transform.position +
            (Vector3)(Random.insideUnitCircle * spawnOffsetRadius);

        NPCIdentity childIdentity =
            spawned.GetComponent<NPCIdentity>() ??
            spawned.AddComponent<NPCIdentity>();
        childIdentity.npcId = System.Guid.NewGuid().ToString("N");
        EntityProfile childProfile =
            spawned.GetComponent<EntityProfile>() ??
            spawned.AddComponent<EntityProfile>();
        NPCLifecycle lifecycle =
            spawned.GetComponent<NPCLifecycle>() ??
            spawned.AddComponent<NPCLifecycle>();
        NPCVisualResolver visualResolver =
            spawned.GetComponent<NPCVisualResolver>() ??
            spawned.AddComponent<NPCVisualResolver>();
        VillagerAI childVillager =
            spawned.GetComponent<VillagerAI>() ??
            spawned.AddComponent<VillagerAI>();
        VillagerRelationship childRelationship =
            spawned.GetComponent<VillagerRelationship>() ??
            spawned.AddComponent<VillagerRelationship>();

        childProfile.kind = EntityKind.Commoner;
        childProfile.ReloadGeneratedProfile();

        NPCIdentity motherIdentity = GetIdentity(mother);
        NPCIdentity fatherIdentity = GetIdentity(father);

        SpawnedWorldActor childActor =
            spawned.GetComponent<SpawnedWorldActor>() ??
            spawned.AddComponent<SpawnedWorldActor>();
        string resolvedChildPrefabKey = childPrefab != null
            ? ResolveChildPrefabKey()
            : string.Empty;
        childActor.ConfigureRuntimeRespawn(
            resolvedChildPrefabKey,
            childPrefab,
            motherIdentity != null ? motherIdentity.npcId : string.Empty);

        childIdentity.gender =
            Random.value < 0.5f ? Gender.Male : Gender.Female;
        childIdentity.npcName = string.Empty;
        childIdentity.SetCurrentAge(0);
        childIdentity.lifeStage = LifeStage.Baby;
        childIdentity.fatherId =
            fatherIdentity != null ? fatherIdentity.npcId : string.Empty;
        childIdentity.motherId =
            motherIdentity != null ? motherIdentity.npcId : string.Empty;
        childIdentity.spouseId = string.Empty;
        childIdentity.homeId =
            motherIdentity != null && !string.IsNullOrWhiteSpace(motherIdentity.homeId)
                ? motherIdentity.homeId
                : fatherIdentity != null
                    ? fatherIdentity.homeId
                    : string.Empty;

        if (childProfile.identity != null)
        {
            NpcAgeUtility.SetCurrentAge(childProfile.identity, 0);
            childProfile.identity.gender =
                childIdentity.gender == Gender.Female
                    ? EntityGender.Female
                    : EntityGender.Male;
            childProfile.identity.entityName =
                NpcGeneratedIdentityProfiles.GenerateChildName(
                    childProfile.identity.gender,
                    fatherIdentity,
                    motherIdentity);
        }

        childIdentity.npcName =
            childProfile.identity != null
                ? childProfile.identity.entityName
                : childIdentity.npcName;

        childIdentity.visualProfile =
            ResolveChildVisualProfile(
                motherIdentity,
                fatherIdentity);

        childVillager.entityProfile = childProfile;
        CharacterStats childStats = spawned.GetComponent<CharacterStats>();
        if (childStats != null)
        {
            childStats.generatedEntityKind = EntityKind.Commoner;
            childStats.generateFromEntityProfile = true;
            childStats.entityProfile = childProfile;
            childStats.ApplyEntityProfile();
        }

        childRelationship.BecomeSingle();
        childRelationship.totalChildrenBorn = 0;
        childRelationship.childrenBornWithCurrentPartner = 0;
        childRelationship.maxChildrenWithCurrentPartner = 0;

        lifecycle.SetAge(0);
        lifecycle.useRapidRuntimeGrowth = false;
        lifecycle.rapidGrowthStartAbsoluteDay = int.MinValue;

        childVillager.generateFromEntityProfile = true;
        childVillager.SyncNpcIdentityData();
        childVillager.homePoint =
            mother.homePoint != null
                ? mother.homePoint
                : father.homePoint;
        ConfigureSpawnedChildBehaviours(spawned, childVillager);

        if (childStats != null)
        {
            childStats.ApplyEntityProfile();
        }

        if (visualResolver != null)
        {
            visualResolver.RefreshVisual();
        }

        RevealSpawnedChild(spawned, childVillager);

        motherRelationship.RegisterChildBirth(birthCooldownDays);

        VillagerRelationship fatherRelationship = GetOrAddRelationship(father);
        if (fatherRelationship != null)
        {
            fatherRelationship.RegisterChildBirth(birthCooldownDays);
        }

        if (lookup != null &&
            motherIdentity != null &&
            !string.IsNullOrWhiteSpace(motherIdentity.npcId))
        {
            lookup[motherIdentity.npcId] = mother;
        }

        if (lookup != null &&
            fatherIdentity != null &&
            !string.IsNullOrWhiteSpace(fatherIdentity.npcId))
        {
            lookup[fatherIdentity.npcId] = father;
        }

        if (villagersBuffer != null)
        {
            villagersBuffer.Add(childVillager);
        }

        if (lookup != null &&
            childIdentity != null &&
            !string.IsNullOrWhiteSpace(childIdentity.npcId))
        {
            lookup[childIdentity.npcId] = childVillager;
        }

        LogBirth(
            FormatName(mother) +
            " và " +
            FormatName(father) +
            " vừa có thêm một đứa trẻ.");
    }

    void RevealSpawnedChild(
        GameObject spawned,
        VillagerAI childVillager)
    {
        if (spawned == null)
        {
            return;
        }

        if (!spawned.activeSelf)
        {
            spawned.SetActive(true);
        }

        if (childVillager != null)
        {
            childVillager.enabled = true;
            childVillager.ForceHiddenAtHome(false);
            childVillager.StopMoving();
        }

        Renderer[] renderers =
            spawned.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }

        Collider2D[] colliders =
            spawned.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
            }
        }
    }

    void ConfigureSpawnedChildBehaviours(
        GameObject spawned,
        VillagerAI childVillager)
    {
        if (spawned == null)
        {
            return;
        }

        RemoveAdultRoleComponent<NpcMerchantRole>(spawned);
        RemoveAdultRoleComponent<NpcAlchemyRole>(spawned);
        RemoveAdultRoleComponent<NpcForgeRole>(spawned);
        RemoveAdultRoleComponent<NpcVillageSupplyMerchant>(spawned);
        RemoveAdultRoleComponent<VillagerMarketRole>(spawned);
        RemoveAdultRoleComponent<VillagerDailyTradePlan>(spawned);
        RemoveAdultRoleComponent<VillagerProduceSeller>(spawned);
        RemoveAdultRoleComponent<NpcFixedBlacksmithController>(spawned);
        RemoveAdultRoleComponent<NpcFixedAlchemistController>(spawned);
        RemoveAdultRoleComponent<NpcCounterBroker>(spawned);
        RemoveAdultRoleComponent<NpcForgeAgent>(spawned);
        RemoveAdultRoleComponent<NpcAlchemyAgent>(spawned);
        RemoveAdultRoleComponent<NpcTradeAgent>(spawned);
        RemoveAdultRoleComponent<NpcSpecialProfession>(spawned);
        RemoveAdultRoleComponent<HarvestJob>(spawned);
        RemoveAdultRoleComponent<VillagerFarmJob>(spawned);
        RemoveAdultRoleComponent<VillagerFishingJob>(spawned);
        RemoveAdultRoleComponent<HunterJob>(spawned);
        RemoveAdultRoleComponent<NpcResourceGatherer>(spawned);
        RemoveAdultRoleComponent<NpcItemCollector>(spawned);

        DailyConversation conversation =
            spawned.GetComponent<DailyConversation>();
        if (conversation == null)
        {
            conversation = spawned.AddComponent<DailyConversation>();
        }

        conversation.talkRadius = 1.15f;
        conversation.scanInterval = 2.5f;
        conversation.conversationCooldown = 15f;

        if (childVillager != null)
        {
            childVillager.hideAtHome = false;
            childVillager.RefreshAgeSensitiveBehaviours();
        }
    }

    void RemoveAdultRoleComponent<T>(GameObject target)
        where T : Behaviour
    {
        if (target == null)
        {
            return;
        }

        T component = target.GetComponent<T>();
        if (component == null)
        {
            return;
        }

        component.enabled = false;
        Destroy(component);
    }

    void RegisterChildPrefab()
    {
        if (childPrefab == null)
        {
            return;
        }

        SpawnedWorldActor.RegisterRespawnPrefab(
            ResolveChildPrefabKey(),
            childPrefab);
    }

    string ResolveChildPrefabKey()
    {
        return !string.IsNullOrWhiteSpace(childPrefabKey)
            ? childPrefabKey.Trim()
            : SpawnedWorldActor.BuildDefaultPrefabKey(
                "villager-child",
                childPrefab);
    }

    void ResolveDefaultVillagerVisualProfile()
    {
        if (defaultVillagerVisualProfile != null)
        {
            return;
        }

        NPCIdentity[] identities =
            Object.FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include);
        for (int i = 0; i < identities.Length; i++)
        {
            NPCIdentity identity = identities[i];
            if (identity != null &&
                identity.visualProfile != null)
            {
                defaultVillagerVisualProfile = identity.visualProfile;
                return;
            }
        }
    }

    NPCVisualProfile ResolveChildVisualProfile(
        NPCIdentity motherIdentity,
        NPCIdentity fatherIdentity)
    {
        if (motherIdentity != null &&
            motherIdentity.visualProfile != null)
        {
            return motherIdentity.visualProfile;
        }

        if (fatherIdentity != null &&
            fatherIdentity.visualProfile != null)
        {
            return fatherIdentity.visualProfile;
        }

        ResolveDefaultVillagerVisualProfile();
        return defaultVillagerVisualProfile;
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

    VillagerAI FindPartner(
        string partnerId,
        Dictionary<string, VillagerAI> lookup)
    {
        if (string.IsNullOrWhiteSpace(partnerId) ||
            lookup == null)
        {
            return null;
        }

        if (lookup.TryGetValue(partnerId, out VillagerAI partner))
        {
            return partner;
        }

        return null;
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

    void LogBirth(string message)
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

        Debug.Log("[VillagerBirth] " + message);
    }

    void ApplyBaselinePacing()
    {
        minMotherAge = NpcLifeStageDefaults.AdultMinAge;
        maxMotherAge =
            Mathf.Max(
                minMotherAge,
                MaximumParentAge);
        maxFatherAge =
            Mathf.Max(
                minMotherAge,
                MaximumParentAge);
        dailyConceptionChance =
            Mathf.Clamp(
                Mathf.Max(
                    dailyConceptionChance,
                    MinimumDailyConceptionChance),
                0f,
                1f);
        pregnancyDurationDays =
            Mathf.Max(
                1,
                Mathf.Min(
                    pregnancyDurationDays,
                    MaximumPregnancyDurationDays));
        birthCooldownDays =
            Mathf.Max(
                0,
                Mathf.Min(
                    birthCooldownDays,
                    MaximumBirthCooldownDays));
        minMarriageDaysBeforeConception =
            Mathf.Max(
                0,
                Mathf.Min(
                    minMarriageDaysBeforeConception,
                    MaximumMarriageDaysBeforeConception));
    }
}
