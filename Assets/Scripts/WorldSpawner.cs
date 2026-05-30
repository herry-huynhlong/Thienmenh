using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnRegion : MonoBehaviour
{
    public Vector2 size = new Vector2(12f, 8f);

    public Vector3 RandomPoint()
    {
        Vector2 offset = new Vector2(
            UnityEngine.Random.Range(-size.x * 0.5f, size.x * 0.5f),
            UnityEngine.Random.Range(-size.y * 0.5f, size.y * 0.5f));

        return transform.position + (Vector3)offset;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, size);
    }
}

public class SpawnedWorldActor : MonoBehaviour
{
    public string persistentId;
    public EntityKind kind;
    public int prefabSlot;
    public int homeIndex = -1;
    public bool isHiddenAtHome;
}

[Serializable]
public class SavedWorldActorData
{
    public string persistentId;
    public int kind;
    public int prefabSlot;
    public Vector3 position;
    public int homeIndex = -1;
    public bool isHiddenAtHome;
    public EntityIdentity identity = new EntityIdentity();
    public EntityStats stats = new EntityStats();
    public EntityTalent talent = new EntityTalent();
    public EntityPersonality personality = new EntityPersonality();
    public EntityEmotion emotion = new EntityEmotion();
    public EntityNeeds needs = new EntityNeeds();
    public int currentGoal;
    public int villagerJob;
    public int villagerAgeGroup;
    public int professionLevel;
    public int professionExp;
    public string currentAction;
}

[Serializable]
public class SavedWorldActorCollection
{
    public List<SavedWorldActorData> actors = new List<SavedWorldActorData>();
}

public class WorldSpawner : MonoBehaviour
{
    const string SaveKey = "ThienMenh.Save.WorldSpawner.Actors";

    [Header("Prefabs")]
    public GameObject villagerPrefab;
    public GameObject cultivatorPrefab;
    public GameObject beastPrefab;

    [Header("Regions")]
    public SpawnRegion villageRegion;
    public SpawnRegion forestRegion;
    public Transform[] villagerHomePoints;

    [Header("Counts")]
    public int initialVillagers = 20;
    public int initialCultivators = 6;
    public int initialBeasts = 14;
    public bool spawnOnStart = true;
    public bool loadSavedActors = true;
    public bool saveSpawnedActors = true;
    public bool topUpMissingActorsToInitialCounts;
    public bool spawnVillagersAtHomePoints = true;
    public bool spawnActorsOverTime = true;
    public float spawnInterval = 0.25f;

    [Header("Villager Jobs")]
    public bool randomizeVillagerJobs = true;
    public VillagerJob[] randomVillagerJobs =
    {
        VillagerJob.Farmer,
        VillagerJob.Worker,
        VillagerJob.Trader,
        VillagerJob.Guard,
        VillagerJob.Healer,
        VillagerJob.Fisher,
        VillagerJob.Hunter
    };

    [Header("Home Routine")]
    public bool addHomeResidentToVillagers = true;
    public bool hideVillagersAtHome = true;
    public bool villagersReturnHomeAtNight = true;
    public bool villagersReturnHomeWhenTired = true;
    [Range(0f, 100f)] public float tiredReturnThreshold = 85f;

    [Header("Inventory")]
    public bool spawnedActorsUsePrivateInventory = true;
    public bool clearSharedInventoryCopiedOnSpawn = true;

    [Header("Runtime Save")]
    public float autoSaveInterval = 10f;

    float autoSaveTimer;
    int nextActorNumber;

    void Start()
    {
        EnsureWorldSystems();

        if (spawnOnStart)
        {
            StartCoroutine(SpawnStartupRoutine());
        }
    }

    IEnumerator SpawnStartupRoutine()
    {
        if (loadSavedActors && TryLoadSpawnedActors())
        {
            if (topUpMissingActorsToInitialCounts)
            {
                yield return StartCoroutine(TopUpMissingActorsRoutine());
            }

            yield break;
        }

        yield return StartCoroutine(SpawnInitialWorldRoutine());
        SaveSpawnedWorld();
    }

    void Update()
    {
        if (!saveSpawnedActors || autoSaveInterval <= 0f)
        {
            return;
        }

        autoSaveTimer += Time.deltaTime;
        if (autoSaveTimer >= autoSaveInterval)
        {
            autoSaveTimer = 0f;
            SaveSpawnedWorld();
        }
    }

    void OnApplicationQuit()
    {
        SaveSpawnedWorld();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveSpawnedWorld();
        }
    }

    public void SpawnInitialWorld()
    {
        StartCoroutine(SpawnInitialWorldRoutine());
    }

    IEnumerator SpawnInitialWorldRoutine()
    {
        yield return StartCoroutine(SpawnManyRoutine(villagerPrefab, EntityKind.Villager, initialVillagers, villageRegion, 0, CountExisting(EntityKind.Villager)));
        yield return StartCoroutine(SpawnManyRoutine(cultivatorPrefab, EntityKind.Cultivator, initialCultivators, villageRegion, 1, CountExisting(EntityKind.Cultivator)));
        yield return StartCoroutine(SpawnManyRoutine(beastPrefab, EntityKind.Beast, initialBeasts, forestRegion, 2, CountExisting(EntityKind.Beast)));
    }

    void TopUpMissingActors()
    {
        StartCoroutine(TopUpMissingActorsRoutine());
    }

    IEnumerator TopUpMissingActorsRoutine()
    {
        int villagers = CountExisting(EntityKind.Villager);
        int cultivators = CountExisting(EntityKind.Cultivator);
        int beasts = CountExisting(EntityKind.Beast);

        yield return StartCoroutine(SpawnManyRoutine(villagerPrefab, EntityKind.Villager, initialVillagers - villagers, villageRegion, 0, villagers));
        yield return StartCoroutine(SpawnManyRoutine(cultivatorPrefab, EntityKind.Cultivator, initialCultivators - cultivators, villageRegion, 1, cultivators));
        yield return StartCoroutine(SpawnManyRoutine(beastPrefab, EntityKind.Beast, initialBeasts - beasts, forestRegion, 2, beasts));
        SaveSpawnedWorld();
    }

    int CountExisting(EntityKind kind)
    {
        int count = 0;
        foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Exclude))
        {
            if (actor != null && actor.kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    IEnumerator SpawnManyRoutine(GameObject prefab, EntityKind kind, int count, SpawnRegion region, int prefabSlot, int startIndex)
    {
        if (prefab == null || count <= 0)
        {
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            int actorIndex = startIndex + i;
            int homeIndex = GetHomeIndex(kind, actorIndex);
            Vector3 position = GetSpawnPosition(kind, region, homeIndex);
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            ConfigureNewActor(instance, kind, prefabSlot, homeIndex, actorIndex);

            if (spawnActorsOverTime && spawnInterval > 0f)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }

    void ConfigureNewActor(GameObject instance, EntityKind kind, int prefabSlot, int homeIndex, int actorIndex)
    {
        if (instance == null)
        {
            return;
        }

        EntityProfile profile = EntityGenerator.EnsureProfile(instance, kind);
        profile.kind = kind;
        EntityGenerator.FillProfile(profile, kind);
        MakeGeneratedActorDistinct(profile, kind, actorIndex);
        profile.lockGeneratedValues = true;

        SpawnedWorldActor marker = EnsureMarker(instance);
        marker.kind = kind;
        marker.prefabSlot = prefabSlot;
        marker.homeIndex = homeIndex;
        marker.isHiddenAtHome = false;
        marker.persistentId = MakePersistentId(kind);

        ApplyProfileToActor(instance, profile, kind, true);
        ConfigureHomeResident(instance, marker, false);
        instance.name = profile.identity.entityName + " (" + profile.identity.kind + ")";
        ConfigureSpawnedInventory(instance, marker.persistentId, true);
    }

    bool TryLoadSpawnedActors()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return false;
        }

        SavedWorldActorCollection data = JsonUtility.FromJson<SavedWorldActorCollection>(PlayerPrefs.GetString(SaveKey));
        if (data == null || data.actors == null || data.actors.Count == 0)
        {
            return false;
        }

        DestroyExistingSpawnedActors();

        foreach (SavedWorldActorData actorData in data.actors)
        {
            LoadActor(actorData);
        }

        return true;
    }

    void LoadActor(SavedWorldActorData actorData)
    {
        if (actorData == null)
        {
            return;
        }

        EntityKind kind = (EntityKind)actorData.kind;
        GameObject prefab = GetPrefab(actorData.prefabSlot, kind);
        if (prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, actorData.position, Quaternion.identity);
        EntityProfile profile = EntityGenerator.EnsureProfile(instance, kind);
        ApplySavedProfile(profile, actorData, kind);

        SpawnedWorldActor marker = EnsureMarker(instance);
        marker.persistentId = actorData.persistentId;
        marker.kind = kind;
        marker.prefabSlot = actorData.prefabSlot;
        marker.homeIndex = actorData.homeIndex;
        marker.isHiddenAtHome = actorData.isHiddenAtHome;

        ApplyProfileToActor(instance, profile, kind, false);
        ApplySavedRuntime(instance, actorData);
        ConfigureHomeResident(instance, marker, actorData.isHiddenAtHome);
        instance.name = profile.identity.entityName + " (" + profile.identity.kind + ")";
        ConfigureSpawnedInventory(instance, marker.persistentId, false);
    }

    public void SaveSpawnedWorld()
    {
        if (!saveSpawnedActors)
        {
            return;
        }

        SavedWorldActorCollection data = new SavedWorldActorCollection();
        foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Exclude))
        {
            SavedWorldActorData saved = CreateSavedActor(actor);
            if (saved != null)
            {
                data.actors.Add(saved);
            }
        }

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        GameSaveSystem.MarkSaveExists();
        PlayerPrefs.Save();
    }

    SavedWorldActorData CreateSavedActor(SpawnedWorldActor actor)
    {
        if (actor == null || actor.gameObject == null)
        {
            return null;
        }

        EntityProfile profile = actor.GetComponent<EntityProfile>();
        if (profile == null)
        {
            profile = EntityGenerator.EnsureProfile(actor.gameObject, actor.kind);
        }

        SyncProfileFromActor(actor.gameObject, profile);

        NpcHomeResident resident = actor.GetComponent<NpcHomeResident>();
        if (resident != null)
        {
            actor.isHiddenAtHome = resident.IsHiddenAtHome;
        }

        SavedWorldActorData saved = new SavedWorldActorData
        {
            persistentId = actor.persistentId,
            kind = (int)actor.kind,
            prefabSlot = actor.prefabSlot,
            position = actor.transform.position,
            homeIndex = actor.homeIndex,
            isHiddenAtHome = actor.isHiddenAtHome,
            currentGoal = (int)profile.currentGoal
        };

        CopyIdentity(profile.identity, saved.identity);
        CopyStats(profile.stats, saved.stats);
        CopyTalent(profile.talent, saved.talent);
        CopyPersonality(profile.personality, saved.personality);
        CopyEmotion(profile.emotion, saved.emotion);
        CopyNeeds(profile.needs, saved.needs);

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager != null)
        {
            saved.villagerJob = (int)villager.job;
            saved.villagerAgeGroup = (int)villager.ageGroup;
            saved.professionLevel = villager.professionLevel;
            saved.professionExp = villager.professionExp;
            saved.currentAction = villager.currentAction;
        }

        return saved;
    }

    void ApplySavedProfile(EntityProfile profile, SavedWorldActorData saved, EntityKind kind)
    {
        if (profile == null || saved == null)
        {
            return;
        }

        profile.kind = kind;
        CopyIdentity(saved.identity, profile.identity);
        CopyStats(saved.stats, profile.stats);
        CopyTalent(saved.talent, profile.talent);
        CopyPersonality(saved.personality, profile.personality);
        CopyEmotion(saved.emotion, profile.emotion);
        CopyNeeds(saved.needs, profile.needs);
        profile.currentGoal = (EntityGoal)saved.currentGoal;
        profile.lockGeneratedValues = true;
    }

    void ApplyProfileToActor(GameObject instance, EntityProfile profile, EntityKind kind, bool randomizeJob)
    {
        if (instance == null || profile == null)
        {
            return;
        }

        VillagerAI villager = instance.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.entityProfile = profile;
            villager.generateFromEntityProfile = false;
            villager.villagerName = profile.identity.entityName;
            villager.ageGroup = GetAgeGroup(profile.identity.age);
            villager.realm = profile.stats.realm;
            villager.realmStage = profile.stats.realmStage;
            villager.cultivationExp = profile.stats.cultivationExp;
            villager.maxHP = profile.stats.maxHP;
            villager.currentHP = Mathf.Clamp(profile.stats.currentHP, 1, profile.stats.maxHP);
            villager.attack = profile.stats.attack;
            villager.defense = profile.stats.defense;
            villager.moveSpeed = profile.stats.moveSpeed;
            villager.money = profile.stats.money;
            villager.sociability = profile.personality.sociability;
            villager.greed = profile.personality.greed;
            villager.diligence = profile.personality.diligence;
            villager.bravery = profile.personality.bravery;
            villager.hunger = profile.needs.hunger;
            villager.fatigue = profile.needs.fatigue;
            villager.fun = Mathf.Clamp(100f - profile.needs.socialNeed, 0f, 100f);

            if (kind == EntityKind.Villager && randomizeJob && randomizeVillagerJobs)
            {
                villager.job = PickRandomVillagerJob();
                villager.keepInspectorJob = true;
            }
        }

        SmartNpcAI smartNpc = instance.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.entityProfile = profile;
            smartNpc.generateFromEntityProfile = false;
            smartNpc.npcName = profile.identity.entityName;
            smartNpc.realm = profile.stats.realm;
            smartNpc.realmStage = profile.stats.realmStage;
            smartNpc.money = profile.stats.money;
            smartNpc.spiritStone = profile.stats.spiritStone;
            smartNpc.hunger = profile.needs.hunger;
            smartNpc.fatigue = profile.needs.fatigue;
        }
    }

    void ApplySavedRuntime(GameObject instance, SavedWorldActorData saved)
    {
        VillagerAI villager = instance.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.job = (VillagerJob)saved.villagerJob;
            villager.keepInspectorJob = true;
            villager.ageGroup = (VillagerAgeGroup)saved.villagerAgeGroup;
            villager.professionLevel = Mathf.Max(1, saved.professionLevel);
            villager.professionExp = Mathf.Max(0, saved.professionExp);
            if (!string.IsNullOrEmpty(saved.currentAction))
            {
                villager.currentAction = saved.currentAction;
            }
        }
    }

    void ConfigureHomeResident(GameObject instance, SpawnedWorldActor marker, bool forceHidden)
    {
        if (!addHomeResidentToVillagers || instance == null || marker == null || marker.kind != EntityKind.Villager)
        {
            return;
        }

        Transform home = GetHomePoint(marker.homeIndex);
        if (home == null)
        {
            return;
        }

        VillagerAI villager = instance.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.homePoint = home;
        }

        NpcHomeResident resident = instance.GetComponent<NpcHomeResident>();
        if (resident == null)
        {
            resident = instance.AddComponent<NpcHomeResident>();
        }

        resident.homePoint = home;
        resident.hideAtHome = hideVillagersAtHome;
        resident.returnHomeAtNight = villagersReturnHomeAtNight;
        resident.returnHomeWhenTired = villagersReturnHomeWhenTired;
        resident.tiredThreshold = tiredReturnThreshold;

        if (forceHidden)
        {
            resident.ForceHiddenAtHome(true);
        }
    }

    void SyncProfileFromActor(GameObject instance, EntityProfile profile)
    {
        if (instance == null || profile == null)
        {
            return;
        }

        VillagerAI villager = instance.GetComponent<VillagerAI>();
        if (villager != null)
        {
            profile.identity.entityName = villager.villagerName;
            profile.identity.kind = EntityKind.Villager;
            profile.stats.realm = villager.realm;
            profile.stats.realmStage = villager.realmStage;
            profile.stats.maxHP = villager.maxHP;
            profile.stats.currentHP = villager.currentHP;
            profile.stats.attack = villager.attack;
            profile.stats.defense = villager.defense;
            profile.stats.moveSpeed = villager.moveSpeed;
            profile.stats.cultivationExp = Mathf.Clamp((int)villager.cultivationExp, 0, int.MaxValue);
            profile.stats.money = villager.money;
            profile.personality.sociability = villager.sociability;
            profile.personality.greed = villager.greed;
            profile.personality.diligence = villager.diligence;
            profile.personality.bravery = villager.bravery;
            profile.needs.hunger = villager.hunger;
            profile.needs.fatigue = villager.fatigue;
            profile.needs.socialNeed = Mathf.Clamp(100f - villager.fun, 0f, 100f);
            return;
        }

        SmartNpcAI smartNpc = instance.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            profile.identity.entityName = smartNpc.npcName;
            profile.stats.realm = smartNpc.realm;
            profile.stats.realmStage = smartNpc.realmStage;
            profile.stats.money = smartNpc.money;
            profile.stats.spiritStone = smartNpc.spiritStone;
            profile.needs.hunger = smartNpc.hunger;
            profile.needs.fatigue = smartNpc.fatigue;
        }
    }

    void DestroyExistingSpawnedActors()
    {
        foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Exclude))
        {
            if (actor != null)
            {
                Destroy(actor.gameObject);
            }
        }
    }

    SpawnedWorldActor EnsureMarker(GameObject instance)
    {
        SpawnedWorldActor marker = instance.GetComponent<SpawnedWorldActor>();
        if (marker == null)
        {
            marker = instance.AddComponent<SpawnedWorldActor>();
        }

        return marker;
    }

    int GetHomeIndex(EntityKind kind, int actorIndex)
    {
        if (kind != EntityKind.Villager || !spawnVillagersAtHomePoints || villagerHomePoints == null || villagerHomePoints.Length == 0)
        {
            return -1;
        }

        return Mathf.Abs(actorIndex) % villagerHomePoints.Length;
    }

    Transform GetHomePoint(int homeIndex)
    {
        if (homeIndex < 0 || villagerHomePoints == null || villagerHomePoints.Length == 0)
        {
            return null;
        }

        return villagerHomePoints[homeIndex % villagerHomePoints.Length];
    }

    Vector3 GetSpawnPosition(EntityKind kind, SpawnRegion region, int homeIndex)
    {
        Transform home = kind == EntityKind.Villager ? GetHomePoint(homeIndex) : null;
        if (home != null)
        {
            return home.position;
        }

        return region != null ? region.RandomPoint() : transform.position;
    }

    GameObject GetPrefab(int prefabSlot, EntityKind kind)
    {
        switch (prefabSlot)
        {
            case 0:
                return villagerPrefab;
            case 1:
                return cultivatorPrefab != null ? cultivatorPrefab : villagerPrefab;
            case 2:
                return beastPrefab;
            default:
                if (kind == EntityKind.Beast)
                {
                    return beastPrefab;
                }

                if (kind == EntityKind.Cultivator)
                {
                    return cultivatorPrefab != null ? cultivatorPrefab : villagerPrefab;
                }

                return villagerPrefab;
        }
    }

    void MakeGeneratedActorDistinct(EntityProfile profile, EntityKind kind, int actorIndex)
    {
        if (profile == null)
        {
            return;
        }

        int seed = Mathf.Max(0, actorIndex) + 1;
        if (profile.identity != null)
        {
            string baseName = string.IsNullOrEmpty(profile.identity.entityName)
                ? kind.ToString()
                : profile.identity.entityName;
            profile.identity.entityName = baseName + " " + seed.ToString("000");
            profile.identity.age = Mathf.Clamp(profile.identity.age + seed % 7, 1, 9999);
        }

        if (profile.stats != null)
        {
            int hpBonus = seed * 3;
            profile.stats.maxHP = Mathf.Max(1, profile.stats.maxHP + hpBonus);
            profile.stats.currentHP = profile.stats.maxHP;
            profile.stats.attack = Mathf.Max(1, profile.stats.attack + seed % 9);
            profile.stats.defense = Mathf.Max(0, profile.stats.defense + seed % 5);
            profile.stats.effectResistance = Mathf.Max(0, profile.stats.effectResistance + seed % 4);
            profile.stats.moveSpeed = Mathf.Clamp(profile.stats.moveSpeed + (seed % 6) * 0.03f, 0.5f, 8f);
            profile.stats.cultivationExp = Mathf.Max(0, profile.stats.cultivationExp + seed * 11);
            profile.stats.money = Mathf.Max(0, profile.stats.money + seed * 2);
            profile.stats.spiritStone = Mathf.Max(0, profile.stats.spiritStone + seed % 3);
            profile.stats.realmStage = Mathf.Clamp(((profile.stats.realmStage + seed - 1) % CultivationProgression.MaxStage) + 1, 1, CultivationProgression.MaxStage);
        }

        if (profile.personality != null)
        {
            profile.personality.greed = WrapStat(profile.personality.greed + seed * 7);
            profile.personality.bravery = WrapStat(profile.personality.bravery + seed * 11);
            profile.personality.kindness = WrapStat(profile.personality.kindness + seed * 13);
            profile.personality.sociability = WrapStat(profile.personality.sociability + seed * 17);
            profile.personality.diligence = WrapStat(profile.personality.diligence + seed * 19);
            profile.personality.hotTemper = WrapStat(profile.personality.hotTemper + seed * 23);
            profile.personality.funSeeking = WrapStat(profile.personality.funSeeking + seed * 29);
            profile.personality.loneliness = WrapStat(profile.personality.loneliness + seed * 31);
            profile.personality.cultivationDesire = WrapStat(profile.personality.cultivationDesire + seed * 37);
        }

        if (profile.needs != null)
        {
            profile.needs.hunger = Mathf.Repeat(profile.needs.hunger + seed * 3.5f, 100f);
            profile.needs.fatigue = Mathf.Repeat(profile.needs.fatigue + seed * 2.5f, 100f);
            profile.needs.socialNeed = Mathf.Repeat(profile.needs.socialNeed + seed * 4.5f, 100f);
            profile.needs.cultivationNeed = Mathf.Repeat(profile.needs.cultivationNeed + seed * 5.5f, 100f);
        }
    }

    int WrapStat(int value)
    {
        return Mathf.Clamp(value % 101, 0, 100);
    }
    string MakePersistentId(EntityKind kind)
    {
        nextActorNumber++;
        return kind + "_" + DateTime.UtcNow.Ticks + "_" + nextActorNumber + "_" + UnityEngine.Random.Range(1000, 9999);
    }

    VillagerJob PickRandomVillagerJob()
    {
        if (randomVillagerJobs == null || randomVillagerJobs.Length == 0)
        {
            return UnityEngine.Random.value < 0.5f ? VillagerJob.Farmer : VillagerJob.Worker;
        }

        return randomVillagerJobs[UnityEngine.Random.Range(0, randomVillagerJobs.Length)];
    }

    VillagerAgeGroup GetAgeGroup(int age)
    {
        if (age < 18)
        {
            return VillagerAgeGroup.Child;
        }

        return age > 60 ? VillagerAgeGroup.Elder : VillagerAgeGroup.Adult;
    }

    void ConfigureSpawnedInventory(GameObject instance, string persistentId, bool clearCurrentItems)
    {
        if (!spawnedActorsUsePrivateInventory || instance == null)
        {
            return;
        }

        ItemInventory inventory = instance.GetComponent<ItemInventory>();
        if (inventory == null)
        {
            return;
        }

        inventory.UsePrivateRuntimeItems("WorldActor_" + persistentId, clearCurrentItems && clearSharedInventoryCopiedOnSpawn);
    }

    void EnsureWorldSystems()
    {
        if (WorldTimeSystem.Instance == null)
        {
            GameObject time = new GameObject("WorldTimeSystem");
            time.AddComponent<WorldTimeSystem>();
        }

        if (WeatherSystem.Instance == null)
        {
            GameObject weather = new GameObject("WeatherSystem");
            weather.AddComponent<WeatherSystem>();
        }
    }

    static void CopyIdentity(EntityIdentity source, EntityIdentity target)
    {
        if (source == null || target == null) return;
        target.entityName = source.entityName;
        target.kind = source.kind;
        target.gender = source.gender;
        target.age = source.age;
    }

    static void CopyStats(EntityStats source, EntityStats target)
    {
        if (source == null || target == null) return;
        target.realm = source.realm;
        target.realmStage = source.realmStage;
        target.maxHP = source.maxHP;
        target.currentHP = source.currentHP;
        target.attack = source.attack;
        target.defense = source.defense;
        target.effectResistance = source.effectResistance;
        target.moveSpeed = source.moveSpeed;
        target.cultivationExp = source.cultivationExp;
        target.money = source.money;
        target.spiritStone = source.spiritStone;
    }

    static void CopyTalent(EntityTalent source, EntityTalent target)
    {
        if (source == null || target == null) return;
        target.grade = source.grade;
        target.comprehension = source.comprehension;
        target.cultivationSpeed = source.cultivationSpeed;
        target.combatMultiplier = source.combatMultiplier;
        target.luck = source.luck;
    }

    static void CopyPersonality(EntityPersonality source, EntityPersonality target)
    {
        if (source == null || target == null) return;
        target.greed = source.greed;
        target.bravery = source.bravery;
        target.kindness = source.kindness;
        target.sociability = source.sociability;
        target.diligence = source.diligence;
        target.hotTemper = source.hotTemper;
        target.funSeeking = source.funSeeking;
        target.loneliness = source.loneliness;
        target.cultivationDesire = source.cultivationDesire;
    }

    static void CopyEmotion(EntityEmotion source, EntityEmotion target)
    {
        if (source == null || target == null) return;
        target.mood = source.mood;
        target.happiness = source.happiness;
        target.anger = source.anger;
        target.sadness = source.sadness;
        target.fear = source.fear;
        target.loneliness = source.loneliness;
    }

    static void CopyNeeds(EntityNeeds source, EntityNeeds target)
    {
        if (source == null || target == null) return;
        target.hunger = source.hunger;
        target.fatigue = source.fatigue;
        target.socialNeed = source.socialNeed;
        target.cultivationNeed = source.cultivationNeed;
    }
}
[RequireComponent(typeof(SpawnedWorldActor))]
public class NpcHomeResident : MonoBehaviour
{
    public Transform homePoint;
    public bool hideAtHome = true;
    public bool returnHomeAtNight = true;
    public bool returnHomeWhenTired = true;
    [Range(0f, 100f)] public float tiredThreshold = 85f;
    public float arriveDistance = 0.25f;
    public float fallbackMoveSpeed = 1.6f;
    public WorldTimePhase leaveHomePhase = WorldTimePhase.Dawn;

    bool returningHome;
    bool hiddenAtHome;
    Behaviour pausedAi;
    bool pausedAiWasEnabled;
    Renderer[] renderers;
    Collider2D[] colliders;
    Rigidbody2D rb;
    SpawnedWorldActor marker;

    public bool IsHiddenAtHome => hiddenAtHome;

    void Awake()
    {
        marker = GetComponent<SpawnedWorldActor>();
        rb = GetComponent<Rigidbody2D>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    void Update()
    {
        if (homePoint == null)
        {
            return;
        }

        if (hiddenAtHome)
        {
            TryLeaveHome();
            return;
        }

        if (!returningHome && ShouldReturnHome())
        {
            BeginReturnHome();
        }

        if (returningHome)
        {
            MoveHome();
        }
    }

    public void ForceHiddenAtHome(bool hidden)
    {
        if (hidden)
        {
            HideAtHome();
        }
        else
        {
            ShowFromHome();
        }
    }

    bool ShouldReturnHome()
    {
        if (NpcRoleUtility.IsDead(gameObject))
        {
            return false;
        }

        WorldTimeSystem time = WorldTimeSystem.Instance;
        if (returnHomeAtNight && time != null && time.CurrentPhase == WorldTimePhase.Night)
        {
            return true;
        }

        if (returnHomeWhenTired)
        {
            VillagerAI villager = GetComponent<VillagerAI>();
            if (villager != null && villager.fatigue >= tiredThreshold)
            {
                return true;
            }

            SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
            if (smartNpc != null && smartNpc.fatigue >= tiredThreshold)
            {
                return true;
            }
        }

        return false;
    }

    void BeginReturnHome()
    {
        returningHome = true;
        PauseAi();
        NpcRoleUtility.SetAction(gameObject, "Ve nha cu tru");
    }

    void MoveHome()
    {
        NpcRoleUtility.MoveTowards(gameObject, homePoint.position, fallbackMoveSpeed);
        NpcRoleUtility.SetAction(gameObject, "Dang ve nha");

        if (Vector2.Distance(transform.position, homePoint.position) <= arriveDistance)
        {
            if (hideAtHome)
            {
                HideAtHome();
            }
            else
            {
                FinishRestAtHome();
                ResumeAi();
            }
        }
    }

    void HideAtHome()
    {
        returningHome = false;
        hiddenAtHome = true;

        if (marker != null)
        {
            marker.isHiddenAtHome = true;
        }

        transform.position = homePoint.position;
        StopRigidbody();
        FinishRestAtHome();
        SetVisible(false);
        SetColliders(false);
        NpcRoleUtility.SetAction(gameObject, "Da vao nha");
    }

    void TryLeaveHome()
    {
        WorldTimeSystem time = WorldTimeSystem.Instance;
        if (time == null || time.CurrentPhase != leaveHomePhase)
        {
            return;
        }

        ShowFromHome();
    }

    void ShowFromHome()
    {
        hiddenAtHome = false;
        returningHome = false;

        if (marker != null)
        {
            marker.isHiddenAtHome = false;
        }

        transform.position = homePoint.position;
        SetVisible(true);
        SetColliders(true);
        ResumeAi();
        NpcRoleUtility.SetAction(gameObject, "Ra khoi nha");
    }

    void FinishRestAtHome()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.fatigue = 0f;
            villager.currentHP = Mathf.Min(villager.maxHP, villager.currentHP + 10);
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.fatigue = 0f;
        }
    }

    void PauseAi()
    {
        pausedAi = GetComponent<VillagerAI>();
        if (pausedAi == null)
        {
            pausedAi = GetComponent<SmartNpcAI>();
        }

        if (pausedAi == null)
        {
            return;
        }

        pausedAiWasEnabled = pausedAi.enabled;
        pausedAi.enabled = false;
    }

    void ResumeAi()
    {
        if (pausedAi != null)
        {
            pausedAi.enabled = pausedAiWasEnabled;
            pausedAi = null;
            return;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.enabled = true;
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.enabled = true;
        }
    }

    void StopRigidbody()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void SetVisible(bool visible)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = visible;
            }
        }
    }

    void SetColliders(bool enabledValue)
    {
        if (colliders == null)
        {
            return;
        }

        foreach (Collider2D hit in colliders)
        {
            if (hit != null)
            {
                hit.enabled = enabledValue;
            }
        }
    }
}
