using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[Serializable]
public class FullGameSaveData
{
    public int saveVersion = 4;
    public long savedAtUtcTicks;
    public string sceneName;
    public bool hasPlayer;
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public int playerMaxHP;
    public int playerCurrentHP;
    public int playerRealm;
    public int playerRealmStage;
    public long playerCultivationExp;
    public bool hasCamera;
    public Vector3 cameraPosition;
    public Quaternion cameraRotation;
    public float cameraOrthographicSize;
    public List<SavedFavoriteNpcData> favoriteNpcs = new List<SavedFavoriteNpcData>();
    public List<SavedNpcStateData> npcStates = new List<SavedNpcStateData>();
    public BicanhSessionSaveData bicanhSession;
    public bool hasFarmPlotState;
    public List<FarmPlotPersistentState> farmPlots =
        new List<FarmPlotPersistentState>();
    public WeatherPersistentState weatherState;
    public WeatherAccumulationPersistentState weatherAccumulationState;
    public WorldEventPersistentState worldEventState;
    public bool hasWorldLogState;
    public List<LogEntry> worldLogs = new List<LogEntry>();
}

[Serializable]
public class SavedFavoriteNpcData
{
    public string persistentId;
    public string worldActorPersistentId;
    public string npcId;
    public string npcDataPersistentId;
    public string displayName;
    public string realmText;
    public int heavenFavorFear;
    public bool unlockedGiftBackThreshold;
    public bool unlockedWorshipThreshold;
}

[Serializable]
public class SavedNpcStateData
{
    public string stateKey;
    public string npcId;
    public string npcDataPersistentId;
    public string worldActorPersistentId;
    public string socialId;
    public string displayName;
    public Vector3 position;
    public bool activeSelf;

    public bool isRuntimeSpawn;
    public bool respawnFromFullSave;
    public string prefabKey;
    public string respawnTemplateNpcId;
    public string savedObjectName;

    public bool hasIdentity;
    public string npcName;
    public int gender;
    public int age;
    public int birthAbsoluteDay;
    public bool hasBirthAbsoluteDay;
    public int lifeStage;
    public string homeId;
    public string fatherId;
    public string motherId;
    public string spouseId;
    public bool useRapidRuntimeGrowth;
    public int rapidGrowthStartAbsoluteDay;
    public int rapidGrowthDurationDays;
    public float rapidGrowthBabyScale;
    public float rapidGrowthChildScale;
    public Vector3 rapidGrowthAdultScale;

    public bool hasSchedule;
    public int lifePath;
    public bool canCultivate;
    public bool awakenedCultivation;
    public bool awakenedByMarrowCleansingPill;

    public bool hasVillager;
    public bool villagerEnabled;
    public int villagerJob;
    public int villagerCurrentHP;
    public int villagerMaxHP;
    public int villagerMoney;
    public int villagerSpiritStone;
    public int villagerProfessionLevel;
    public int villagerProfessionExp;
    public int villagerCurrentActionId;
    public string villagerCurrentActionKey;
    public string villagerCurrentAction;

    public bool hasSmartNpc;
    public bool smartNpcEnabled;
    public int smartRealm;
    public int smartRealmStage;
    public long smartCultivation;
    public int smartCurrentHP;
    public int smartMaxHP;
    public int smartMoney;
    public int smartSpiritStone;
    public bool smartCanCultivate;
    public bool waitingForHeavenlyTribulation;
    public int smartCurrentActionId;
    public string smartCurrentActionKey;
    public string smartCurrentAction;

    public bool hasRelationship;
    public int relationshipStatus;
    public string partnerId;
    public int affection;
    public int datingDays;
    public int marriedDays;
    public bool pregnant;
    public int pregnancyDaysLeft;
    public int birthCooldownDays;
    public int childrenBornWithCurrentPartner;
    public int totalChildrenBorn;
    public int maxChildrenWithCurrentPartner;
    public int marriageHomeProjectState;
    public string marriageHomeSiteId;
    public int marriageHomeCostSpiritStone;
    public int marriageHomeBuildStartAbsoluteDay;
    public float marriageHomeBuildStartHour;

    public List<NpcSocialRelationship> socialRelationships =
        new List<NpcSocialRelationship>();
    public List<NpcMemoryRecord> memories =
        new List<NpcMemoryRecord>();
}

public class FullGameSaveController : MonoBehaviour
{
    const int CurrentSaveVersion = 4;
    const string FullSaveKey = "ThienMenh.Save.FullGame";
    const string FullSaveBackupKey = "ThienMenh.Save.FullGame.Backup";
    const string FullSaveFileName = "save_v5_fullgame.json";
    const string FullSaveBackupFileName = "save_v5_fullgame.bak";
    const string FullSaveTempFileName = "save_v5_fullgame.tmp";
    static FullGameSaveController instance;

    [Header("Auto Save")]
    [FormerlySerializedAs("autoSaveInterval")]
    public float autoSaveIntervalUnscaledSeconds = 30f;
    public bool saveOnPause = true;
    public bool saveOnQuit = true;
    public bool debugLog;

    float saveTimer;
    bool applyingLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static FullGameSaveController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        FullGameSaveController existing =
            FindAnyObjectByType<FullGameSaveController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return instance;
        }

        GameObject saveObject = new GameObject("FullGameSaveController");
        DontDestroyOnLoad(saveObject);
        instance = saveObject.AddComponent<FullGameSaveController>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        if (autoSaveIntervalUnscaledSeconds <= 0f || IsMenuScene())
        {
            return;
        }

        saveTimer += GameTime.UnscaledDeltaSeconds;
        if (saveTimer >= autoSaveIntervalUnscaledSeconds)
        {
            saveTimer = 0f;
            SaveFullGame(true);
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && saveOnPause)
        {
            SaveFullGame(true);
        }
    }

    void OnApplicationQuit()
    {
        if (saveOnQuit)
        {
            SaveFullGame(true);
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!GameSaveSystem.ShouldLoadSavedGame ||
            IsMenuScene(scene.name) ||
            scene.name == "PersistentScene")
        {
            return;
        }

        StartCoroutine(ApplyFullSaveAfterSceneLoad());
    }

    public void SaveFullGame(bool flushNow = false)
    {
        if (HeavenlyTribulationSystem.HasActiveTribulation)
        {
            if (debugLog)
            {
                Debug.Log(
                    "FullGameSaveController skipped save while Heavenly Tribulation is active.");
            }

            return;
        }

        string sceneName = GetCurrentGameplaySceneName();
        if (string.IsNullOrEmpty(sceneName) || IsNonGameplayScene(sceneName))
        {
            return;
        }

        FullGameSaveData data = new FullGameSaveData();
        data.saveVersion = CurrentSaveVersion;
        data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        data.sceneName = sceneName;

        SavePlayer(data);
        SaveCamera(data);
        SaveFavorites(data);
        SaveNpcStates(data);
        SaveBicanhSession(data);
        SaveWorldTime();
        SaveWorldSimulation(data);
        SaveWallets();

        string serialized = JsonUtility.ToJson(data);
        if (!TryWriteSaveFiles(serialized))
        {
            Debug.LogError(
                "FullGameSaveController failed to persist full save file. Save aborted.");
            return;
        }

        GameSaveSystem.MarkSaveExists();
        GameSaveSystem.SaveCurrentScene(sceneName);
        GameSaveSystem.QueuePendingCommit();

        if (flushNow)
        {
            GameSaveSystem.FlushPendingCommit();
        }

        if (debugLog)
        {
            Debug.Log("FullGameSaveController saved scene: " + sceneName);
        }
    }

    public void LoadFullGameNow()
    {
        GameSaveSystem.RequestContinueGameStart();
        StartCoroutine(ApplyFullSaveAfterSceneLoad());
    }

    IEnumerator ApplyFullSaveAfterSceneLoad()
    {
        if (applyingLoad ||
            !HasAnyPersistedFullSave())
        {
            yield break;
        }

        applyingLoad = true;
        try
        {
            yield return null;
            yield return null;

            if (TryReadSaveData(out FullGameSaveData data))
            {
                ApplyWorldTime();
                ApplyWorldSimulation(data);
                ApplyPlayer(data);
                ApplyCamera(data);
                yield return null;
                ApplyNpcStates(data);
                ApplyBicanhSession(data);
                ApplyFavorites(data);
            }
        }
        finally
        {
            applyingLoad = false;
        }
    }

    void SavePlayer(FullGameSaveData data)
    {
        GameObject player = FindPlayerObject();
        if (player == null)
        {
            return;
        }

        data.hasPlayer = true;
        data.playerPosition = player.transform.position;
        data.playerRotation = player.transform.rotation;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            data.playerMaxHP = health.maxHP;
            data.playerCurrentHP = health.currentHP;
        }

        CharacterStats stats = player.GetComponent<CharacterStats>();
        if (stats != null)
        {
            data.playerMaxHP = stats.finalHP;
            data.playerCurrentHP = stats.currentHP;
            data.playerRealm = (int)stats.realm;
            data.playerRealmStage = stats.realmStage;
            data.playerCultivationExp = stats.cultivationExp;
        }
    }

    void ApplyPlayer(FullGameSaveData data)
    {
        if (data == null || !data.hasPlayer)
        {
            return;
        }

        GameObject player = FindPlayerObject();
        if (player == null)
        {
            return;
        }

        player.transform.SetPositionAndRotation(data.playerPosition, data.playerRotation);

        CharacterStats stats = player.GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.realm = (CultivationRealm)Mathf.Max(0, data.playerRealm);
            stats.realmStage = Mathf.Max(1, data.playerRealmStage);
            stats.cultivationExp = Math.Max(0, data.playerCultivationExp);
            stats.RecalculateStats(false);
            stats.currentHP = Mathf.Clamp(data.playerCurrentHP, 0, stats.finalHP);
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.maxHP = data.playerMaxHP > 0 ? data.playerMaxHP : health.maxHP;
            health.currentHP =
                Mathf.Clamp(
                    data.playerCurrentHP,
                    0,
                    Mathf.Max(health.maxHP, 0));
        }
    }

    void SaveCamera(FullGameSaveData data)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        data.hasCamera = true;
        data.cameraPosition = camera.transform.position;
        data.cameraRotation = camera.transform.rotation;
        data.cameraOrthographicSize = camera.orthographicSize;
    }

    void ApplyCamera(FullGameSaveData data)
    {
        if (data == null || !data.hasCamera)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        camera.transform.SetPositionAndRotation(data.cameraPosition, data.cameraRotation);
        if (data.cameraOrthographicSize > 0f)
        {
            camera.orthographicSize = data.cameraOrthographicSize;
        }

        MobileCameraController controller = camera.GetComponent<MobileCameraController>();
        if (controller != null)
        {
            controller.followTarget = null;
            GameObject player = FindPlayerObject();
            if (player != null)
            {
                controller.followTarget = player.transform;
                controller.RefreshMapBoundsForPosition(player.transform.position);
                return;
            }

            controller.RefreshMapBoundsForPosition(camera.transform.position);
        }
    }

    void SaveFavorites(FullGameSaveData data)
    {
        NpcFavoriteManager manager = NpcFavoriteManager.Instance;
        if (manager == null)
        {
            return;
        }

        foreach (NpcFavorite favorite in manager.Favorites)
        {
            if (favorite == null || !favorite.IsFavorite)
            {
                continue;
            }

            GameObject target =
                favorite.gameObject;
            SpawnedWorldActor actor =
                ResolveComponent<SpawnedWorldActor>(target);
            NPCIdentity identity =
                ResolveComponent<NPCIdentity>(target);
            NpcData npcData =
                ResolveComponent<NpcData>(target);

            if (actor != null)
            {
                actor.EnsurePersistentId();
            }

            if (npcData != null)
            {
                npcData.EnsurePersistentId();
            }

            data.favoriteNpcs.Add(new SavedFavoriteNpcData
            {
                persistentId = actor != null ? actor.persistentId : "",
                worldActorPersistentId = actor != null ? actor.persistentId : "",
                npcId = identity != null ? identity.npcId : "",
                npcDataPersistentId = npcData != null ? npcData.persistentId : "",
                displayName = favorite.GetDisplayName(),
                realmText = favorite.GetRealmText(),
                heavenFavorFear = favorite.GetHeavenFavorFear(),
                unlockedGiftBackThreshold =
                    favorite.unlockedGiftBackThreshold,
                unlockedWorshipThreshold =
                    favorite.unlockedWorshipThreshold
            });
        }
    }

    void ApplyFavorites(FullGameSaveData data)
    {
        if (data == null || data.favoriteNpcs == null)
        {
            return;
        }

        NpcFavoriteManager manager = NpcFavoriteManager.EnsureInstance();
        manager.ClearFavorites(false);

        foreach (SavedFavoriteNpcData saved in data.favoriteNpcs)
        {
            GameObject npcObject = FindNpcByFavoriteData(saved);
            if (npcObject == null)
            {
                continue;
            }

            NpcFavorite favorite = npcObject.GetComponent<NpcFavorite>();
            if (favorite == null)
            {
                favorite = npcObject.AddComponent<NpcFavorite>();
            }

            favorite.npcDisplayName = saved.displayName;
            favorite.realmText = saved.realmText;
            favorite.heavenFavorFear = Mathf.Max(0, saved.heavenFavorFear);
            favorite.unlockedGiftBackThreshold =
                saved.unlockedGiftBackThreshold ||
                favorite.heavenFavorFear >= NpcFavorite.GiftBackThreshold;
            favorite.unlockedWorshipThreshold =
                saved.unlockedWorshipThreshold ||
                favorite.heavenFavorFear >= NpcFavorite.WorshipThreshold;
            manager.AddFavorite(favorite);
        }
    }

    void SaveNpcStates(FullGameSaveData data)
    {
        if (data == null)
        {
            return;
        }

        HashSet<GameObject> savedObjects =
            new HashSet<GameObject>();

        foreach (NPCIdentity identity in FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include))
        {
            SaveNpcState(data, identity != null ? identity.gameObject : null, savedObjects);
        }

        foreach (NpcData npcData in FindObjectsByType<NpcData>(FindObjectsInactive.Include))
        {
            SaveNpcState(data, npcData != null ? npcData.gameObject : null, savedObjects);
        }

        foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Include))
        {
            SaveNpcState(data, actor != null ? actor.gameObject : null, savedObjects);
        }

        foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Include))
        {
            SaveNpcState(data, villager != null ? villager.gameObject : null, savedObjects);
        }

        foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Include))
        {
            SaveNpcState(data, smartNpc != null ? smartNpc.gameObject : null, savedObjects);
        }
    }

    void SaveBicanhSession(FullGameSaveData data)
    {
        if (data == null || BicanhSessionManager.Instance == null)
        {
            return;
        }

        data.bicanhSession =
            BicanhSessionManager.Instance.CaptureSaveData();
    }

    void ApplyBicanhSession(FullGameSaveData data)
    {
        if (data == null ||
            data.bicanhSession == null)
        {
            return;
        }

        BicanhSessionManager manager = BicanhSessionManager.Instance;
        if (manager == null)
        {
            manager =
                FindAnyObjectByType<BicanhSessionManager>(
                    FindObjectsInactive.Include);
        }

        if (manager == null)
        {
            Debug.LogWarning(
                "FullGameSaveController could not restore Bicanh session: missing BicanhSessionManager.");
            return;
        }

        manager.RestoreFromSaveData(data.bicanhSession);
    }

    void SaveNpcState(
        FullGameSaveData data,
        GameObject npcObject,
        HashSet<GameObject> savedObjects)
    {
        if (data == null ||
            npcObject == null ||
            savedObjects == null ||
            npcObject.CompareTag("Player"))
        {
            return;
        }

        npcObject = ResolveNpcRoot(npcObject);
        if (npcObject == null ||
            savedObjects.Contains(npcObject) ||
            npcObject.CompareTag("Player"))
        {
            return;
        }

        string stateKey = GetNpcStateKey(npcObject);
        if (string.IsNullOrWhiteSpace(stateKey))
        {
            return;
        }

        savedObjects.Add(npcObject);

        SpawnedWorldActor actor =
            ResolveComponent<SpawnedWorldActor>(npcObject);
        NpcData npcData =
            ResolveComponent<NpcData>(npcObject);
        NPCIdentity identity =
            ResolveComponent<NPCIdentity>(npcObject);
        NpcSocialIdentity socialIdentity =
            ResolveComponent<NpcSocialIdentity>(npcObject);
        NpcScheduleController schedule =
            ResolveComponent<NpcScheduleController>(npcObject);
        VillagerAI villager =
            ResolveComponent<VillagerAI>(npcObject);
        SmartNpcAI smartNpc =
            ResolveComponent<SmartNpcAI>(npcObject);
        VillagerRelationship relationship =
            ResolveComponent<VillagerRelationship>(npcObject);
        NpcRelationshipGraph relationshipGraph =
            ResolveComponent<NpcRelationshipGraph>(npcObject);
        NpcMemory memory =
            ResolveComponent<NpcMemory>(npcObject);

        if (actor != null)
        {
            actor.EnsurePersistentId();
        }

        if (npcData != null)
        {
            npcData.EnsurePersistentId();
        }

        if (socialIdentity != null)
        {
            socialIdentity.Refresh();
        }

        bool inferredRuntimeSpawn =
            IsLikelyRuntimeBornIdentity(identity);
        bool isRuntimeSpawn =
            (actor != null && actor.spawnedAtRuntime) ||
            inferredRuntimeSpawn;
        bool respawnFromFullSave =
            actor != null && actor.spawnedAtRuntime
                ? actor.respawnFromFullSave
                : inferredRuntimeSpawn;

        SavedNpcStateData saved =
            new SavedNpcStateData
            {
                stateKey = stateKey,
                npcId = identity != null ? identity.npcId : "",
                npcDataPersistentId = npcData != null ? npcData.persistentId : "",
                worldActorPersistentId = actor != null ? actor.persistentId : "",
                socialId = socialIdentity != null ? socialIdentity.socialId : "",
                displayName = NpcRoleUtility.GetDisplayName(npcObject),
                position = npcObject.transform.position,
                activeSelf = npcObject.activeSelf,
                isRuntimeSpawn = isRuntimeSpawn,
                respawnFromFullSave = respawnFromFullSave,
                prefabKey = actor != null ? actor.prefabKey : "",
                respawnTemplateNpcId =
                    actor != null &&
                    !string.IsNullOrWhiteSpace(actor.respawnTemplateNpcId)
                        ? actor.respawnTemplateNpcId
                        : identity != null
                            ? identity.motherId
                            : "",
                savedObjectName = npcObject.name
            };

        SaveIdentityState(saved, identity);
        SaveScheduleState(saved, schedule);
        SaveVillagerState(saved, villager);
        SaveSmartNpcState(saved, smartNpc);
        SaveRelationshipState(saved, relationship);
        SaveSocialState(saved, relationshipGraph, memory);

        data.npcStates.Add(saved);
    }

    bool IsLikelyRuntimeBornIdentity(NPCIdentity identity)
    {
        return identity != null &&
            (!string.IsNullOrWhiteSpace(identity.fatherId) ||
             !string.IsNullOrWhiteSpace(identity.motherId));
    }

    void SaveIdentityState(
        SavedNpcStateData saved,
        NPCIdentity identity)
    {
        if (saved == null || identity == null)
        {
            return;
        }

        saved.hasIdentity = true;
        saved.npcName = identity.npcName;
        saved.gender = (int)identity.gender;
        saved.age = identity.GetCurrentAge();
        saved.birthAbsoluteDay = identity.birthAbsoluteDay;
        saved.hasBirthAbsoluteDay = identity.hasBirthAbsoluteDay;
        saved.lifeStage = (int)identity.lifeStage;
        saved.homeId = identity.homeId;
        saved.fatherId = identity.fatherId;
        saved.motherId = identity.motherId;
        saved.spouseId = identity.spouseId;

        NPCLifecycle lifecycle =
            ResolveComponent<NPCLifecycle>(identity.gameObject);
        if (lifecycle != null)
        {
            saved.useRapidRuntimeGrowth =
                lifecycle.useRapidRuntimeGrowth;
            saved.rapidGrowthStartAbsoluteDay =
                lifecycle.rapidGrowthStartAbsoluteDay;
            saved.rapidGrowthDurationDays =
                lifecycle.rapidGrowthDurationDays;
            saved.rapidGrowthBabyScale =
                lifecycle.rapidGrowthBabyScale;
            saved.rapidGrowthChildScale =
                lifecycle.rapidGrowthChildScale;
            saved.rapidGrowthAdultScale =
                lifecycle.rapidGrowthAdultScale;
        }
    }

    void SaveScheduleState(
        SavedNpcStateData saved,
        NpcScheduleController schedule)
    {
        if (saved == null || schedule == null)
        {
            return;
        }

        saved.hasSchedule = true;
        saved.lifePath = (int)schedule.lifePath;
        saved.canCultivate = schedule.canCultivate;
        saved.awakenedCultivation = schedule.awakenedCultivation;
        saved.awakenedByMarrowCleansingPill =
            schedule.awakenedByMarrowCleansingPill;
    }

    void SaveVillagerState(
        SavedNpcStateData saved,
        VillagerAI villager)
    {
        if (saved == null || villager == null)
        {
            return;
        }

        saved.hasVillager = true;
        saved.villagerEnabled = villager.enabled;
        saved.villagerJob = (int)villager.job;
        saved.villagerCurrentHP = villager.AuthoritativeCurrentHP;
        saved.villagerMaxHP = villager.AuthoritativeMaxHP;
        saved.villagerMoney = villager.money;
        saved.villagerSpiritStone = villager.spiritStone;
        saved.villagerProfessionLevel = villager.professionLevel;
        saved.villagerProfessionExp = villager.professionExp;
        NpcActionState villagerActionState =
            villager.CurrentActionState;
        saved.villagerCurrentActionId = (int)villagerActionState.id;
        saved.villagerCurrentActionKey = villagerActionState.key;
        saved.villagerCurrentAction = villager.currentAction;
    }

    void SaveSmartNpcState(
        SavedNpcStateData saved,
        SmartNpcAI smartNpc)
    {
        if (saved == null || smartNpc == null)
        {
            return;
        }

        saved.hasSmartNpc = true;
        saved.smartNpcEnabled = smartNpc.enabled;
        saved.smartRealm = (int)smartNpc.realm;
        saved.smartRealmStage = smartNpc.realmStage;
        saved.smartCultivation = smartNpc.cultivation;
        saved.smartCurrentHP = smartNpc.AuthoritativeCurrentHP;
        saved.smartMaxHP = smartNpc.AuthoritativeMaxHP;
        saved.smartMoney = smartNpc.money;
        saved.smartSpiritStone = smartNpc.spiritStone;
        saved.smartCanCultivate = smartNpc.canCultivate;
        saved.waitingForHeavenlyTribulation =
            smartNpc.waitingForHeavenlyTribulation;
        NpcActionState smartActionState =
            smartNpc.CurrentActionState;
        saved.smartCurrentActionId = (int)smartActionState.id;
        saved.smartCurrentActionKey = smartActionState.key;
        saved.smartCurrentAction = smartNpc.currentAction;
    }

    void SaveRelationshipState(
        SavedNpcStateData saved,
        VillagerRelationship relationship)
    {
        if (saved == null || relationship == null)
        {
            return;
        }

        saved.hasRelationship = true;
        saved.relationshipStatus = (int)relationship.status;
        saved.partnerId = relationship.partnerId;
        saved.affection = relationship.affection;
        saved.datingDays = relationship.datingDays;
        saved.marriedDays = relationship.marriedDays;
        saved.pregnant = relationship.pregnant;
        saved.pregnancyDaysLeft = relationship.pregnancyDaysLeft;
        saved.birthCooldownDays = relationship.birthCooldownDays;
        saved.childrenBornWithCurrentPartner =
            relationship.childrenBornWithCurrentPartner;
        saved.totalChildrenBorn = relationship.totalChildrenBorn;
        saved.maxChildrenWithCurrentPartner =
            relationship.maxChildrenWithCurrentPartner;
        saved.marriageHomeProjectState =
            (int)relationship.marriageHomeProjectState;
        saved.marriageHomeSiteId = relationship.marriageHomeSiteId;
        saved.marriageHomeCostSpiritStone =
            relationship.marriageHomeCostSpiritStone;
        saved.marriageHomeBuildStartAbsoluteDay =
            relationship.marriageHomeBuildStartAbsoluteDay;
        saved.marriageHomeBuildStartHour =
            relationship.marriageHomeBuildStartHour;
    }

    void SaveSocialState(
        SavedNpcStateData saved,
        NpcRelationshipGraph relationshipGraph,
        NpcMemory memory)
    {
        if (saved == null)
        {
            return;
        }

        if (relationshipGraph != null &&
            relationshipGraph.relationships != null)
        {
            saved.socialRelationships =
                CloneSocialRelationships(relationshipGraph.relationships);
        }

        if (memory != null &&
            memory.memories != null)
        {
            saved.memories =
                CloneMemories(memory.memories);
        }
    }

    void ApplyNpcStates(FullGameSaveData data)
    {
        if (data == null || data.npcStates == null)
        {
            return;
        }

        RestoreMissingRuntimeNpcs(data.npcStates);

        foreach (SavedNpcStateData saved in data.npcStates)
        {
            GameObject npcObject = FindNpcByStateData(saved);
            if (npcObject == null)
            {
                continue;
            }

            ApplyNpcState(saved, npcObject);
        }
    }

    void RestoreMissingRuntimeNpcs(
        List<SavedNpcStateData> savedStates)
    {
        if (savedStates == null || savedStates.Count == 0)
        {
            return;
        }

        int maxPasses = Mathf.Max(1, savedStates.Count);
        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool restoredAny = false;

            for (int i = 0; i < savedStates.Count; i++)
            {
                SavedNpcStateData saved = savedStates[i];
                if (saved == null ||
                    !saved.isRuntimeSpawn ||
                    !saved.respawnFromFullSave ||
                    FindNpcByStateData(saved) != null)
                {
                    continue;
                }

                GameObject restored = CreateMissingRuntimeNpc(saved);
                if (restored != null)
                {
                    restoredAny = true;
                }
            }

            if (!restoredAny)
            {
                break;
            }
        }

        List<string> unresolvedRuntimeNpcSummaries = null;
        for (int i = 0; i < savedStates.Count; i++)
        {
            SavedNpcStateData saved = savedStates[i];
            if (saved == null ||
                !saved.isRuntimeSpawn ||
                !saved.respawnFromFullSave ||
                FindNpcByStateData(saved) != null)
            {
                continue;
            }

            if (unresolvedRuntimeNpcSummaries == null)
            {
                unresolvedRuntimeNpcSummaries = new List<string>();
            }

            unresolvedRuntimeNpcSummaries.Add(DescribeMissingRuntimeNpc(saved));
        }

        if (unresolvedRuntimeNpcSummaries != null &&
            unresolvedRuntimeNpcSummaries.Count > 0)
        {
            Debug.LogError(
                "FullGameSaveController could not restore " +
                unresolvedRuntimeNpcSummaries.Count +
                " runtime NPC(s) from save. Missing entries: " +
                string.Join(" | ", unresolvedRuntimeNpcSummaries));
        }
    }

    GameObject CreateMissingRuntimeNpc(SavedNpcStateData saved)
    {
        if (saved == null)
        {
            return null;
        }

        GameObject source = ResolveRuntimeNpcRespawnSource(saved);

        if (source == null)
        {
            Debug.LogWarning(
                "FullGameSaveController could not respawn runtime NPC '" +
                GetSavedNpcDebugName(saved) +
                "': no respawn source was available. " +
                DescribeMissingRuntimeNpc(saved));

            return null;
        }

        GameObject restored = Instantiate(
            source,
            saved.position,
            source.transform.rotation);
        if (restored == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(saved.savedObjectName))
        {
            restored.name = saved.savedObjectName;
        }

        PrepareRespawnedNpc(saved, restored);
        return restored;
    }

    GameObject ResolveRuntimeNpcRespawnSource(SavedNpcStateData saved)
    {
        if (saved == null)
        {
            return null;
        }

        GameObject source =
            SpawnedWorldActor.ResolveRespawnPrefab(saved.prefabKey);
        if (source != null)
        {
            return source;
        }

        if (!string.IsNullOrWhiteSpace(saved.respawnTemplateNpcId))
        {
            source = ResolveNpcRoot(FindNpcById(saved.respawnTemplateNpcId));
            if (source != null)
            {
                return source;
            }
        }

        if (!string.IsNullOrWhiteSpace(saved.displayName))
        {
            source = FindRespawnSourceByDisplayName(saved.displayName);
            if (source != null)
            {
                return source;
            }
        }

        return null;
    }

    GameObject FindRespawnSourceByDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        GameObject uniqueCandidate = null;
        foreach (NPCIdentity identity in FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include))
        {
            GameObject candidate =
                identity != null ? ResolveNpcRoot(identity.gameObject) : null;
            if (candidate == null ||
                candidate.CompareTag("Player"))
            {
                continue;
            }

            string candidateName =
                NpcRoleUtility.GetDisplayName(candidate);
            if (!string.Equals(
                    candidateName,
                    displayName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            SpawnedWorldActor actor = ResolveComponent<SpawnedWorldActor>(candidate);
            if (actor != null && actor.spawnedAtRuntime)
            {
                continue;
            }

            if (uniqueCandidate != null &&
                uniqueCandidate != candidate)
            {
                return null;
            }

            uniqueCandidate = candidate;
        }

        return uniqueCandidate;
    }

    string GetSavedNpcDebugName(SavedNpcStateData saved)
    {
        if (saved == null)
        {
            return "(null)";
        }

        if (!string.IsNullOrWhiteSpace(saved.displayName))
        {
            return saved.displayName;
        }

        if (!string.IsNullOrWhiteSpace(saved.npcName))
        {
            return saved.npcName;
        }

        if (!string.IsNullOrWhiteSpace(saved.savedObjectName))
        {
            return saved.savedObjectName;
        }

        if (!string.IsNullOrWhiteSpace(saved.npcId))
        {
            return saved.npcId;
        }

        return "(unnamed runtime npc)";
    }

    string DescribeMissingRuntimeNpc(SavedNpcStateData saved)
    {
        if (saved == null)
        {
            return "name=(null)";
        }

        return
            "name=" + GetSavedNpcDebugName(saved) +
            ", npcId=" + (saved.npcId ?? "") +
            ", prefabKey=" + (saved.prefabKey ?? "") +
            ", templateNpcId=" + (saved.respawnTemplateNpcId ?? "") +
            ", stateKey=" + (saved.stateKey ?? "");
    }

    void PrepareRespawnedNpc(
        SavedNpcStateData saved,
        GameObject restored)
    {
        if (saved == null || restored == null)
        {
            return;
        }

        restored = ResolveNpcRoot(restored);
        if (restored == null)
        {
            return;
        }

        SpawnedWorldActor actor =
            ResolveComponent<SpawnedWorldActor>(restored);
        if (actor == null)
        {
            actor = restored.AddComponent<SpawnedWorldActor>();
        }

        actor.spawnedAtRuntime = true;
        actor.respawnFromFullSave = saved.respawnFromFullSave;
        actor.prefabKey = saved.prefabKey ?? "";
        actor.respawnTemplateNpcId =
            saved.respawnTemplateNpcId ?? "";
        if (!string.IsNullOrWhiteSpace(saved.worldActorPersistentId))
        {
            actor.persistentId = saved.worldActorPersistentId;
        }
        actor.EnsurePersistentId();

        NPCIdentity identity = ResolveComponent<NPCIdentity>(restored);
        if (identity == null && saved.hasIdentity)
        {
            identity = restored.AddComponent<NPCIdentity>();
        }

        if (identity != null &&
            !string.IsNullOrWhiteSpace(saved.npcId))
        {
            identity.npcId = saved.npcId;
        }

        NpcData npcData = ResolveComponent<NpcData>(restored);
        if (npcData != null &&
            !string.IsNullOrWhiteSpace(saved.npcDataPersistentId))
        {
            npcData.persistentId = saved.npcDataPersistentId;
            npcData.EnsurePersistentId();
        }
    }

    void ApplyNpcState(
        SavedNpcStateData saved,
        GameObject npcObject)
    {
        if (saved == null || npcObject == null)
        {
            return;
        }

        npcObject = ResolveNpcRoot(npcObject);
        if (npcObject == null)
        {
            return;
        }

        npcObject.transform.position = saved.position;
        npcObject.SetActive(saved.activeSelf);

        ApplyPersistentIds(saved, npcObject);

        NPCIdentity identity =
            ResolveComponent<NPCIdentity>(npcObject);
        ApplyIdentityState(saved, identity);

        NpcScheduleController schedule =
            ResolveComponent<NpcScheduleController>(npcObject);
        if (schedule == null && saved.hasSchedule)
        {
            schedule = npcObject.AddComponent<NpcScheduleController>();
        }

        VillagerAI villager =
            ResolveComponent<VillagerAI>(npcObject);
        SmartNpcAI smartNpc =
            ResolveComponent<SmartNpcAI>(npcObject);

        if (saved.hasVillager && villager == null)
        {
            villager = npcObject.AddComponent<VillagerAI>();
        }

        if (saved.hasSmartNpc && smartNpc == null)
        {
            smartNpc = npcObject.AddComponent<SmartNpcAI>();
        }

        if (saved.hasVillager)
        {
            ApplyVillagerState(saved, villager);
        }

        if (saved.hasSmartNpc)
        {
            ApplySmartNpcState(saved, smartNpc);
        }

        if (saved.hasVillager && villager != null)
        {
            villager.enabled = saved.villagerEnabled;
        }

        if (saved.hasSmartNpc && smartNpc != null)
        {
            smartNpc.enabled = saved.smartNpcEnabled;
        }

        ApplyScheduleState(saved, schedule);
        ApplyRelationshipState(saved, npcObject);
        ApplySocialState(saved, npcObject);
    }

    void ApplyIdentityState(
        SavedNpcStateData saved,
        NPCIdentity identity)
    {
        if (saved == null ||
            identity == null ||
            !saved.hasIdentity)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(saved.npcId))
        {
            identity.npcId = saved.npcId;
        }

        identity.npcName = saved.npcName;
        identity.gender = (Gender)Mathf.Clamp(
            saved.gender,
            0,
            Enum.GetValues(typeof(Gender)).Length - 1);
        identity.age = Mathf.Max(0, saved.age);
        identity.birthAbsoluteDay = saved.birthAbsoluteDay;
        identity.hasBirthAbsoluteDay = saved.hasBirthAbsoluteDay;
        identity.homeId = saved.homeId;
        identity.fatherId = saved.fatherId;
        identity.motherId = saved.motherId;
        identity.spouseId = saved.spouseId;

        NPCLifecycle lifecycle =
            ResolveComponent<NPCLifecycle>(identity.gameObject);
        if (lifecycle != null)
        {
            lifecycle.useRapidRuntimeGrowth =
                saved.useRapidRuntimeGrowth;
            lifecycle.rapidGrowthStartAbsoluteDay =
                saved.rapidGrowthStartAbsoluteDay;
            lifecycle.rapidGrowthDurationDays =
                Mathf.Max(1, saved.rapidGrowthDurationDays);
            lifecycle.rapidGrowthBabyScale =
                saved.rapidGrowthBabyScale > 0f
                    ? saved.rapidGrowthBabyScale
                    : lifecycle.rapidGrowthBabyScale;
            lifecycle.rapidGrowthChildScale =
                saved.rapidGrowthChildScale > 0f
                    ? saved.rapidGrowthChildScale
                    : lifecycle.rapidGrowthChildScale;
            lifecycle.rapidGrowthAdultScale =
                saved.rapidGrowthAdultScale != Vector3.zero
                    ? saved.rapidGrowthAdultScale
                    : lifecycle.rapidGrowthAdultScale;
        }

        int savedAge = Mathf.Max(0, saved.age);
        bool shouldRebuildBirthDayFromSavedAge =
            !saved.hasBirthAbsoluteDay;

        if (!shouldRebuildBirthDayFromSavedAge)
        {
            int recalculatedAge =
                NpcAgeUtility.CalculateAge(saved.birthAbsoluteDay);
            shouldRebuildBirthDayFromSavedAge =
                Mathf.Abs(recalculatedAge - savedAge) > 1;
        }

        if (shouldRebuildBirthDayFromSavedAge)
        {
            identity.SetCurrentAge(savedAge);
        }
        else
        {
            identity.EnsureBirthAbsoluteDay();
            identity.age = identity.GetCurrentAge();
        }

        NPCLifecycle stageResolver = lifecycle;
        if (stageResolver != null)
        {
            identity.lifeStage =
                stageResolver.ResolveLifeStage(identity.age);
        }
        else
        {
            if (identity.age <= NpcLifeStageDefaults.BabyMaxAge)
            {
                identity.lifeStage = LifeStage.Baby;
            }
            else if (identity.age <= NpcLifeStageDefaults.ChildMaxAge)
            {
                identity.lifeStage = LifeStage.Child;
            }
            else if (identity.age <= NpcLifeStageDefaults.YouthMaxAge)
            {
                identity.lifeStage = LifeStage.Youth;
            }
            else if (identity.age <= NpcLifeStageDefaults.MiddleMaxAge)
            {
                identity.lifeStage = LifeStage.Middle;
            }
            else
            {
                identity.lifeStage = LifeStage.Old;
            }
        }

        if (lifecycle != null)
        {
            lifecycle.RefreshAgeNow(true);
        }
    }

    void ApplyPersistentIds(
        SavedNpcStateData saved,
        GameObject npcObject)
    {
        if (saved == null || npcObject == null)
        {
            return;
        }

        SpawnedWorldActor actor =
            ResolveComponent<SpawnedWorldActor>(npcObject);
        if (actor == null && saved.isRuntimeSpawn)
        {
            actor = npcObject.AddComponent<SpawnedWorldActor>();
        }

        if (actor != null && saved.isRuntimeSpawn)
        {
            actor.spawnedAtRuntime = true;
            actor.respawnFromFullSave = saved.respawnFromFullSave;
            actor.prefabKey = saved.prefabKey ?? "";
            actor.respawnTemplateNpcId =
                saved.respawnTemplateNpcId ?? "";
        }

        if (actor != null &&
            !string.IsNullOrWhiteSpace(saved.worldActorPersistentId))
        {
            actor.persistentId = saved.worldActorPersistentId;
            actor.EnsurePersistentId();
        }

        NpcData npcData =
            ResolveComponent<NpcData>(npcObject);
        if (npcData != null &&
            !string.IsNullOrWhiteSpace(saved.npcDataPersistentId))
        {
            npcData.persistentId = saved.npcDataPersistentId;
            npcData.EnsurePersistentId();
        }
    }

    void ApplyScheduleState(
        SavedNpcStateData saved,
        NpcScheduleController schedule)
    {
        if (saved == null ||
            schedule == null ||
            !saved.hasSchedule)
        {
            return;
        }

        schedule.lifePath = (NpcLifePath)Mathf.Clamp(
            saved.lifePath,
            0,
            Enum.GetValues(typeof(NpcLifePath)).Length - 1);
        schedule.canCultivate = saved.canCultivate;
        schedule.awakenedCultivation = saved.awakenedCultivation;
        schedule.awakenedByMarrowCleansingPill =
            saved.awakenedByMarrowCleansingPill;

        if (schedule.autoBuildDefaultSchedule)
        {
            schedule.RebuildDefaultSchedule();
        }
    }

    void ApplyVillagerState(
        SavedNpcStateData saved,
        VillagerAI villager)
    {
        if (saved == null || villager == null)
        {
            return;
        }

        villager.job = (VillagerJob)Mathf.Clamp(
            saved.villagerJob,
            0,
            Enum.GetValues(typeof(VillagerJob)).Length - 1);
        villager.RestoreHealthState(
            Mathf.Max(1, saved.villagerMaxHP),
            saved.villagerCurrentHP);
        villager.money = Mathf.Max(0, saved.villagerMoney);
        villager.spiritStone = Mathf.Max(0, saved.villagerSpiritStone);
        villager.RestoreProfessionProgress(
            Mathf.Max(1, saved.villagerProfessionLevel),
            Mathf.Max(0, saved.villagerProfessionExp));
        villager.SetCurrentActionState(
            ResolveSavedActionState(
                saved.villagerCurrentActionKey,
                saved.villagerCurrentActionId,
                saved.villagerCurrentAction));
    }

    void ApplySmartNpcState(
        SavedNpcStateData saved,
        SmartNpcAI smartNpc)
    {
        if (saved == null || smartNpc == null)
        {
            return;
        }

        smartNpc.realm = (CultivationRealm)Mathf.Clamp(
            saved.smartRealm,
            0,
            Enum.GetValues(typeof(CultivationRealm)).Length - 1);
        smartNpc.realmStage = Mathf.Clamp(
            saved.smartRealmStage,
            1,
            CultivationProgression.MaxStage);
        smartNpc.cultivation = Math.Max(0L, saved.smartCultivation);
        smartNpc.money = Mathf.Max(0, saved.smartMoney);
        smartNpc.spiritStone = Mathf.Max(0, saved.smartSpiritStone);
        smartNpc.canCultivate = saved.smartCanCultivate;
        smartNpc.waitingForHeavenlyTribulation =
            saved.waitingForHeavenlyTribulation;
        smartNpc.SetCurrentActionState(
            ResolveSavedActionState(
                saved.smartCurrentActionKey,
                saved.smartCurrentActionId,
                saved.smartCurrentAction));

        CharacterStats stats =
            ResolveComponent<CharacterStats>(smartNpc.gameObject);
        if (stats == null)
        {
            smartNpc.RestoreHealthState(
                Mathf.Max(1, saved.smartMaxHP),
                saved.smartCurrentHP);
            stats = smartNpc.characterStats;
        }

        if (stats != null)
        {
            stats.realm = smartNpc.realm;
            stats.realmStage = smartNpc.realmStage;
            stats.cultivationExp = smartNpc.cultivation;
            stats.waitingForHeavenlyTribulation =
                smartNpc.waitingForHeavenlyTribulation;
            stats.RecalculateStats(false);
        }

        smartNpc.RestoreHealthState(
            Mathf.Max(1, saved.smartMaxHP),
            saved.smartCurrentHP);
    }

    NpcActionState ResolveSavedActionState(
        string actionKey,
        int actionId,
        string displayText)
    {
        NpcActionState state =
            !string.IsNullOrWhiteSpace(actionKey)
                ? NpcActionState.FromKey(actionKey)
                : NpcActionState.FromDisplayText(displayText);

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            state.displayText = displayText;
        }

        if (state.id == NpcActionId.Unknown &&
            Enum.IsDefined(typeof(NpcActionId), actionId))
        {
            state.id = (NpcActionId)actionId;
        }

        return state;
    }

    void ApplyRelationshipState(
        SavedNpcStateData saved,
        GameObject npcObject)
    {
        if (saved == null ||
            npcObject == null ||
            !saved.hasRelationship)
        {
            return;
        }

        VillagerRelationship relationship =
            ResolveComponent<VillagerRelationship>(npcObject);
        if (relationship == null)
        {
            relationship = npcObject.AddComponent<VillagerRelationship>();
        }

        relationship.status =
            (VillagerRelationshipStatus)Mathf.Clamp(
                saved.relationshipStatus,
                0,
                Enum.GetValues(typeof(VillagerRelationshipStatus)).Length - 1);
        relationship.partnerId = saved.partnerId;
        relationship.affection = Mathf.Clamp(saved.affection, 0, 100);
        relationship.datingDays = Mathf.Max(0, saved.datingDays);
        relationship.marriedDays = Mathf.Max(0, saved.marriedDays);
        relationship.pregnant = saved.pregnant;
        relationship.pregnancyDaysLeft =
            Mathf.Max(0, saved.pregnancyDaysLeft);
        relationship.birthCooldownDays =
            Mathf.Max(0, saved.birthCooldownDays);
        relationship.childrenBornWithCurrentPartner =
            Mathf.Max(0, saved.childrenBornWithCurrentPartner);
        relationship.totalChildrenBorn =
            Mathf.Max(0, saved.totalChildrenBorn);
        relationship.maxChildrenWithCurrentPartner =
            Mathf.Max(1, saved.maxChildrenWithCurrentPartner);
        relationship.marriageHomeProjectState =
            (MarriageHomeProjectState)Mathf.Clamp(
                saved.marriageHomeProjectState,
                0,
                Enum.GetValues(typeof(MarriageHomeProjectState)).Length - 1);
        relationship.marriageHomeSiteId =
            saved.marriageHomeSiteId ?? string.Empty;
        relationship.marriageHomeCostSpiritStone =
            Mathf.Max(0, saved.marriageHomeCostSpiritStone);
        relationship.marriageHomeBuildStartAbsoluteDay =
            saved.marriageHomeBuildStartAbsoluteDay;
        relationship.marriageHomeBuildStartHour =
            Mathf.Repeat(saved.marriageHomeBuildStartHour, 24f);
        relationship.SyncIdentityState();
    }

    void ApplySocialState(
        SavedNpcStateData saved,
        GameObject npcObject)
    {
        if (saved == null || npcObject == null)
        {
            return;
        }

        NpcSocialIdentity socialIdentity =
            ResolveComponent<NpcSocialIdentity>(npcObject);
        if (socialIdentity == null &&
            !string.IsNullOrWhiteSpace(saved.socialId))
        {
            socialIdentity = npcObject.AddComponent<NpcSocialIdentity>();
        }

        if (socialIdentity != null)
        {
            socialIdentity.socialId = saved.socialId;
            socialIdentity.displayName = saved.displayName;
            socialIdentity.Refresh();
        }

        if (saved.socialRelationships != null &&
            saved.socialRelationships.Count > 0)
        {
            NpcRelationshipGraph graph =
                ResolveComponent<NpcRelationshipGraph>(npcObject);
            if (graph == null)
            {
                graph = npcObject.AddComponent<NpcRelationshipGraph>();
            }

            graph.relationships =
                CloneSocialRelationships(saved.socialRelationships);
        }

        if (saved.memories != null &&
            saved.memories.Count > 0)
        {
            NpcMemory memory =
                ResolveComponent<NpcMemory>(npcObject);
            if (memory == null)
            {
                memory = npcObject.AddComponent<NpcMemory>();
            }

            memory.memories = CloneMemories(saved.memories);
            memory.Prune();
        }
    }

    GameObject FindNpcById(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            return null;
        }

        foreach (NPCIdentity identity in
                 FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include))
        {
            if (identity != null &&
                string.Equals(
                    identity.npcId,
                    npcId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return identity.gameObject;
            }
        }

        return null;
    }

    GameObject FindNpcByStateData(SavedNpcStateData saved)
    {
        if (saved == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(saved.npcId))
        {
            foreach (NPCIdentity identity in FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include))
            {
                if (identity != null &&
                    string.Equals(
                        identity.npcId,
                        saved.npcId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return identity.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(saved.worldActorPersistentId))
        {
            foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Include))
            {
                if (actor != null &&
                    string.Equals(
                        actor.persistentId,
                        saved.worldActorPersistentId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return actor.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(saved.npcDataPersistentId))
        {
            foreach (NpcData npcData in FindObjectsByType<NpcData>(FindObjectsInactive.Include))
            {
                if (npcData != null &&
                    string.Equals(
                        npcData.persistentId,
                        saved.npcDataPersistentId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return npcData.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(saved.stateKey))
        {
            GameObject byKnownComponent =
                FindNpcByKnownComponentStateKey(saved.stateKey);
            if (byKnownComponent != null)
            {
                return byKnownComponent;
            }
        }

        return null;
    }

    GameObject FindNpcByKnownComponentStateKey(string stateKey)
    {
        foreach (NPCIdentity identity in FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include))
        {
            GameObject candidate =
                identity != null ? ResolveNpcRoot(identity.gameObject) : null;
            if (candidate != null &&
                string.Equals(
                    GetNpcStateKey(candidate),
                    stateKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        foreach (NpcData npcData in FindObjectsByType<NpcData>(FindObjectsInactive.Include))
        {
            GameObject candidate =
                npcData != null ? ResolveNpcRoot(npcData.gameObject) : null;
            if (candidate != null &&
                string.Equals(
                    GetNpcStateKey(candidate),
                    stateKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Include))
        {
            GameObject candidate =
                actor != null ? ResolveNpcRoot(actor.gameObject) : null;
            if (candidate != null &&
                string.Equals(
                    GetNpcStateKey(candidate),
                    stateKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Include))
        {
            GameObject candidate =
                villager != null ? ResolveNpcRoot(villager.gameObject) : null;
            if (candidate != null &&
                string.Equals(
                    GetNpcStateKey(candidate),
                    stateKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Include))
        {
            GameObject candidate =
                smartNpc != null ? ResolveNpcRoot(smartNpc.gameObject) : null;
            if (candidate != null &&
                string.Equals(
                    GetNpcStateKey(candidate),
                    stateKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    GameObject ResolveNpcRoot(GameObject npcObject)
    {
        if (npcObject == null)
        {
            return null;
        }

        VillagerAI villager =
            npcObject.GetComponentInParent<VillagerAI>(true);
        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc =
            npcObject.GetComponentInParent<SmartNpcAI>(true);
        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        SpawnedWorldActor actor =
            npcObject.GetComponentInParent<SpawnedWorldActor>(true);
        if (actor != null)
        {
            return actor.gameObject;
        }

        NpcData npcData =
            npcObject.GetComponentInParent<NpcData>(true);
        if (npcData != null)
        {
            return npcData.gameObject;
        }

        return npcObject;
    }

    string GetNpcStateKey(GameObject npcObject)
    {
        if (npcObject == null)
        {
            return "";
        }

        NPCIdentity identity =
            ResolveComponent<NPCIdentity>(npcObject);
        if (identity != null &&
            !string.IsNullOrWhiteSpace(identity.npcId))
        {
            return "identity:" + identity.npcId;
        }

        SpawnedWorldActor actor =
            ResolveComponent<SpawnedWorldActor>(npcObject);
        if (actor != null)
        {
            actor.EnsurePersistentId();
            if (!string.IsNullOrWhiteSpace(actor.persistentId))
            {
                return "actor:" + actor.persistentId;
            }
        }

        NpcData npcData =
            ResolveComponent<NpcData>(npcObject);
        if (npcData != null)
        {
            npcData.EnsurePersistentId();
            if (!string.IsNullOrWhiteSpace(npcData.persistentId))
            {
                return "npcData:" + npcData.persistentId;
            }
        }

        return "scene:" +
            npcObject.scene.name +
            ":" +
            npcObject.name;
    }

    List<NpcSocialRelationship> CloneSocialRelationships(
        List<NpcSocialRelationship> source)
    {
        List<NpcSocialRelationship> result =
            new List<NpcSocialRelationship>();

        if (source == null)
        {
            return result;
        }

        foreach (NpcSocialRelationship relationship in source)
        {
            if (relationship == null)
            {
                continue;
            }

            result.Add(
                new NpcSocialRelationship
                {
                    targetId = relationship.targetId,
                    affection = relationship.affection,
                    trust = relationship.trust,
                    fear = relationship.fear,
                    grudge = relationship.grudge,
                    debt = relationship.debt,
                    respect = relationship.respect,
                    alliance = relationship.alliance,
                    hostility = relationship.hostility,
                    lastInteractionDay = relationship.lastInteractionDay,
                    lastTopic = relationship.lastTopic
                });
        }

        return result;
    }

    List<NpcMemoryRecord> CloneMemories(List<NpcMemoryRecord> source)
    {
        List<NpcMemoryRecord> result =
            new List<NpcMemoryRecord>();

        if (source == null)
        {
            return result;
        }

        foreach (NpcMemoryRecord memory in source)
        {
            if (memory == null)
            {
                continue;
            }

            result.Add(
                new NpcMemoryRecord
                {
                    type = memory.type,
                    subjectId = memory.subjectId,
                    targetId = memory.targetId,
                    topic = memory.topic,
                    itemName = memory.itemName,
                    locationName = memory.locationName,
                    worldDay = memory.worldDay,
                    worldHour = memory.worldHour,
                    importance = memory.importance,
                    confidence = memory.confidence,
                    resolved = memory.resolved,
                    expiryDay = memory.expiryDay
                });
        }

        return result;
    }

    GameObject FindNpcByFavoriteData(SavedFavoriteNpcData saved)
    {
        if (saved == null)
        {
            return null;
        }

        string worldActorPersistentId =
            !string.IsNullOrWhiteSpace(saved.worldActorPersistentId)
            ? saved.worldActorPersistentId
            : saved.persistentId;

        if (!string.IsNullOrWhiteSpace(worldActorPersistentId))
        {
            foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Include))
            {
                if (actor != null &&
                    string.Equals(
                        actor.persistentId,
                        worldActorPersistentId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return actor.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(saved.npcId))
        {
            foreach (NPCIdentity identity in FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include))
            {
                if (identity != null &&
                    string.Equals(
                        identity.npcId,
                        saved.npcId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return identity.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(saved.npcDataPersistentId))
        {
            foreach (NpcData npcData in FindObjectsByType<NpcData>(FindObjectsInactive.Include))
            {
                if (npcData != null &&
                    string.Equals(
                        npcData.persistentId,
                        saved.npcDataPersistentId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return npcData.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(saved.displayName))
        {
            foreach (NpcFavorite favorite in FindObjectsByType<NpcFavorite>(FindObjectsInactive.Include))
            {
                if (favorite != null &&
                    string.Equals(
                        favorite.GetDisplayName(),
                        saved.displayName,
                        StringComparison.Ordinal))
                {
                    return favorite.gameObject;
                }
            }
        }

        return null;
    }

    T ResolveComponent<T>(GameObject target) where T : Component
    {
        if (target == null)
        {
            return null;
        }

        return target.GetComponent<T>() ??
            target.GetComponentInParent<T>(true) ??
            target.GetComponentInChildren<T>(true);
    }

    void SaveWorldSimulation(FullGameSaveData data)
    {
        if (data == null)
        {
            return;
        }

        TryReadSaveData(out FullGameSaveData previous);
        SaveFarmPlots(data, previous);

        WeatherSystem weather = WeatherSystem.Instance;
        data.weatherState = weather != null
            ? weather.CapturePersistentState()
            : previous != null
                ? previous.weatherState
                : null;

        WeatherAccumulationSystem accumulation =
            WeatherAccumulationSystem.Instance;
        data.weatherAccumulationState = accumulation != null
            ? accumulation.CapturePersistentState()
            : previous != null
                ? previous.weatherAccumulationState
                : null;

        WorldEventSystem worldEvents = WorldEventSystem.Instance;
        data.worldEventState = worldEvents != null
            ? worldEvents.CapturePersistentState()
            : previous != null
                ? previous.worldEventState
                : null;

        WorldEventManager logManager = WorldEventManager.Instance;
        if (logManager != null)
        {
            data.hasWorldLogState = true;
            data.worldLogs = logManager.CaptureLogs();
        }
        else if (previous != null && previous.hasWorldLogState)
        {
            data.hasWorldLogState = true;
            data.worldLogs = previous.worldLogs ?? new List<LogEntry>();
        }
    }

    void SaveFarmPlots(
        FullGameSaveData data,
        FullGameSaveData previous)
    {
        Dictionary<string, FarmPlotPersistentState> states =
            new Dictionary<string, FarmPlotPersistentState>(
                StringComparer.Ordinal);

        if (previous != null &&
            previous.hasFarmPlotState &&
            previous.farmPlots != null)
        {
            for (int i = 0; i < previous.farmPlots.Count; i++)
            {
                FarmPlotPersistentState saved = previous.farmPlots[i];
                if (saved != null &&
                    !string.IsNullOrWhiteSpace(saved.persistentKey))
                {
                    states[saved.persistentKey] = saved;
                }
            }
        }

        FarmPlot[] plots = FindObjectsByType<FarmPlot>(
            FindObjectsInactive.Include);
        HashSet<string> capturedKeys = new HashSet<string>(
            StringComparer.Ordinal);
        for (int i = 0; i < plots.Length; i++)
        {
            FarmPlot plot = plots[i];
            if (plot == null)
            {
                continue;
            }

            FarmPlotPersistentState captured =
                plot.CapturePersistentState();
            if (captured == null ||
                string.IsNullOrWhiteSpace(captured.persistentKey))
            {
                continue;
            }

            bool duplicateCurrentKey =
                !capturedKeys.Add(captured.persistentKey);
            if (debugLog && duplicateCurrentKey)
            {
                Debug.LogWarning(
                    "FullGameSaveController replaced duplicate farm key: " +
                    captured.persistentKey,
                    plot);
            }

            states[captured.persistentKey] = captured;
        }

        List<string> orderedKeys = new List<string>(states.Keys);
        orderedKeys.Sort(StringComparer.Ordinal);

        data.hasFarmPlotState =
            plots.Length > 0 ||
            (previous != null && previous.hasFarmPlotState);
        data.farmPlots = new List<FarmPlotPersistentState>(
            orderedKeys.Count);
        for (int i = 0; i < orderedKeys.Count; i++)
        {
            data.farmPlots.Add(states[orderedKeys[i]]);
        }
    }

    void ApplyWorldSimulation(FullGameSaveData data)
    {
        if (data == null)
        {
            return;
        }

        ApplyFarmPlots(data);

        if (WeatherSystem.Instance != null)
        {
            WeatherSystem.Instance.RestorePersistentState(
                data.weatherState);
        }

        if (WeatherAccumulationSystem.Instance != null)
        {
            WeatherAccumulationSystem.Instance.RestorePersistentState(
                data.weatherAccumulationState);
        }

        if (WorldEventSystem.Instance != null)
        {
            WorldEventSystem.Instance.RestorePersistentState(
                data.worldEventState);
        }

        if (data.hasWorldLogState &&
            WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.RestoreLogs(data.worldLogs);
        }
    }

    void ApplyFarmPlots(FullGameSaveData data)
    {
        if (data == null ||
            !data.hasFarmPlotState ||
            data.farmPlots == null ||
            data.farmPlots.Count == 0)
        {
            return;
        }

        Dictionary<string, FarmPlotPersistentState> states =
            new Dictionary<string, FarmPlotPersistentState>(
                StringComparer.Ordinal);
        for (int i = 0; i < data.farmPlots.Count; i++)
        {
            FarmPlotPersistentState saved = data.farmPlots[i];
            if (saved != null &&
                !string.IsNullOrWhiteSpace(saved.persistentKey))
            {
                states[saved.persistentKey] = saved;
            }
        }

        FarmPlot[] plots = FindObjectsByType<FarmPlot>(
            FindObjectsInactive.Include);
        for (int i = 0; i < plots.Length; i++)
        {
            FarmPlot plot = plots[i];
            if (plot != null &&
                states.TryGetValue(
                    plot.GetPersistentSaveKey(),
                    out FarmPlotPersistentState saved))
            {
                plot.RestorePersistentState(saved);
            }
        }
    }

    void SaveWorldTime()
    {
        WorldTimeSystem time = WorldTimeSystem.Instance;
        if (time != null)
        {
            GameSaveSystem.SaveWorldTime(time.currentYear, time.currentMonth, time.currentDay, time.currentHour);
        }
    }

    void ApplyWorldTime()
    {
        WorldTimeSystem time = WorldTimeSystem.Instance;
        if (time == null)
        {
            return;
        }

        if (GameSaveSystem.TryLoadWorldTime(out int year, out int month, out int day, out float hour))
        {
            time.RestoreTime(year, month, day, hour);
        }
    }

    void SaveWallets()
    {
        foreach (PlayerWallet wallet in FindObjectsByType<PlayerWallet>(FindObjectsInactive.Include))
        {
            if (wallet != null)
            {
                wallet.Save();
            }
        }
    }

    public static void DeletePersistedSaveFiles()
    {
        DeleteFileIfExists(GetFullSaveFilePath());
        DeleteFileIfExists(GetFullSaveBackupFilePath());
        DeleteFileIfExists(GetFullSaveTempFilePath());
    }

    bool TryReadSaveData(out FullGameSaveData data)
    {
        if (TryReadSaveDataFromFile(GetFullSaveFilePath(), out data))
        {
            return true;
        }

        if (TryReadSaveDataFromFile(GetFullSaveBackupFilePath(), out data))
        {
            if (debugLog)
            {
                Debug.LogWarning(
                    "FullGameSaveController restored from backup save file.");
            }

            return true;
        }

        if (TryReadSaveDataFromKey(FullSaveKey, out data))
        {
            return true;
        }

        if (TryReadSaveDataFromKey(FullSaveBackupKey, out data))
        {
            if (debugLog)
            {
                Debug.LogWarning(
                    "FullGameSaveController restored from legacy backup PlayerPrefs save data.");
            }

            return true;
        }

        data = null;
        return false;
    }

    static bool HasAnyPersistedFullSave()
    {
        return File.Exists(GetFullSaveFilePath()) ||
            File.Exists(GetFullSaveBackupFilePath()) ||
            PlayerPrefs.HasKey(FullSaveKey) ||
            PlayerPrefs.HasKey(FullSaveBackupKey);
    }

    static string GetFullSaveDirectoryPath()
    {
        return Application.persistentDataPath;
    }

    static string GetFullSaveFilePath()
    {
        return Path.Combine(GetFullSaveDirectoryPath(), FullSaveFileName);
    }

    static string GetFullSaveBackupFilePath()
    {
        return Path.Combine(GetFullSaveDirectoryPath(), FullSaveBackupFileName);
    }

    static string GetFullSaveTempFilePath()
    {
        return Path.Combine(GetFullSaveDirectoryPath(), FullSaveTempFileName);
    }

    static void DeleteFileIfExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "FullGameSaveController failed to delete save file '" +
                path +
                "': " +
                exception.Message);
        }
    }

    bool TryWriteSaveFiles(string serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return false;
        }

        string directoryPath = GetFullSaveDirectoryPath();
        string primaryPath = GetFullSaveFilePath();
        string backupPath = GetFullSaveBackupFilePath();
        string tempPath = GetFullSaveTempFilePath();

        try
        {
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(tempPath, serialized);

            if (File.Exists(primaryPath))
            {
                File.Replace(tempPath, primaryPath, backupPath, true);
            }
            else
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(tempPath, primaryPath);
                File.Copy(primaryPath, backupPath, true);
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "FullGameSaveController failed to write full save file: " +
                exception.Message);

            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }

            return false;
        }
    }

    bool TryReadSaveDataFromFile(
        string path,
        out FullGameSaveData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path))
        {
            return false;
        }

        string serialized;
        try
        {
            serialized = File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "FullGameSaveController failed to read save file '" +
                path +
                "': " +
                exception.Message);
            return false;
        }

        return TryDeserializeSaveData(serialized, path, out data);
    }

    bool TryReadSaveDataFromKey(
        string key,
        out FullGameSaveData data)
    {
        data = null;

        if (!PlayerPrefs.HasKey(key))
        {
            return false;
        }

        string serialized = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return false;
        }

        return TryDeserializeSaveData(serialized, key, out data);
    }

    bool TryDeserializeSaveData(
        string serialized,
        string sourceLabel,
        out FullGameSaveData data)
    {
        data = null;

        try
        {
            data = JsonUtility.FromJson<FullGameSaveData>(serialized);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "FullGameSaveController failed to parse save source '" +
                sourceLabel +
                "': " +
                exception.Message);
            return false;
        }

        if (data == null)
        {
            return false;
        }

        if (data.saveVersion <= 0)
        {
            data.saveVersion = 1;
        }

        if (data.favoriteNpcs == null)
        {
            data.favoriteNpcs = new List<SavedFavoriteNpcData>();
        }

        if (data.npcStates == null)
        {
            data.npcStates = new List<SavedNpcStateData>();
        }

        if (data.farmPlots == null)
        {
            data.farmPlots = new List<FarmPlotPersistentState>();
        }

        if (data.worldLogs == null)
        {
            data.worldLogs = new List<LogEntry>();
        }

        MigrateSaveData(data);

        return true;
    }

    void MigrateSaveData(FullGameSaveData data)
    {
        if (data == null)
        {
            return;
        }

        if (data.saveVersion < 3 && data.npcStates != null)
        {
            for (int i = 0; i < data.npcStates.Count; i++)
            {
                SavedNpcStateData saved = data.npcStates[i];
                if (saved == null ||
                    !saved.hasIdentity ||
                    (string.IsNullOrWhiteSpace(saved.motherId) &&
                     string.IsNullOrWhiteSpace(saved.fatherId)))
                {
                    continue;
                }

                saved.isRuntimeSpawn = true;
                saved.respawnFromFullSave = true;
                saved.respawnTemplateNpcId =
                    !string.IsNullOrWhiteSpace(saved.motherId)
                        ? saved.motherId
                        : saved.fatherId;
                if (string.IsNullOrWhiteSpace(saved.savedObjectName))
                {
                    saved.savedObjectName = saved.displayName;
                }
            }
        }

        data.saveVersion = Mathf.Max(
            data.saveVersion,
            CurrentSaveVersion);
    }

    GameObject FindPlayerObject()
    {
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            return taggedPlayer;
        }

        PlayerHealth health =
            FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (health != null)
        {
            return health.gameObject;
        }

        CharacterStats stats =
            FindAnyObjectByType<CharacterStats>(FindObjectsInactive.Include);
        return stats != null ? stats.gameObject : null;
    }

    string GetCurrentGameplaySceneName()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.isLoaded && !IsNonGameplayScene(active.name))
        {
            return active.name;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && !IsNonGameplayScene(scene.name))
            {
                return scene.name;
            }
        }

        return "";
    }

    bool IsMenuScene()
    {
        return IsMenuScene(SceneManager.GetActiveScene().name);
    }

    bool IsMenuScene(string sceneName)
    {
        return sceneName == "MainMenu";
    }

    bool IsNonGameplayScene(string sceneName)
    {
        return string.IsNullOrEmpty(sceneName) ||
            sceneName == "PersistentScene" ||
            sceneName == "MainMenu" ||
            sceneName == "LiteMapScene";
    }
}
