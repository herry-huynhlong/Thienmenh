using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class FullGameSaveData
{
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

public class FullGameSaveController : MonoBehaviour
{
    const string FullSaveKey = "ThienMenh.Save.FullGame";
    static FullGameSaveController instance;

    [Header("Auto Save")]
    public float autoSaveInterval = 8f;
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
        if (autoSaveInterval <= 0f || IsMenuScene())
        {
            return;
        }

        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer >= autoSaveInterval)
        {
            saveTimer = 0f;
            SaveFullGame();
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && saveOnPause)
        {
            SaveFullGame();
        }
    }

    void OnApplicationQuit()
    {
        if (saveOnQuit)
        {
            SaveFullGame();
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

    public void SaveFullGame()
    {
        string sceneName = GetCurrentGameplaySceneName();
        if (string.IsNullOrEmpty(sceneName) || IsNonGameplayScene(sceneName))
        {
            return;
        }

        FullGameSaveData data = new FullGameSaveData();
        data.sceneName = sceneName;

        SavePlayer(data);
        SaveCamera(data);
        SaveFavorites(data);
        SaveWorldTime();
        SaveWallets();
        PlayerPrefs.SetString(FullSaveKey, JsonUtility.ToJson(data));
        GameSaveSystem.RegisterDynamicSaveKey(FullSaveKey);
        GameSaveSystem.SaveCurrentScene(sceneName);
        PlayerPrefs.Save();

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
        if (applyingLoad || !PlayerPrefs.HasKey(FullSaveKey))
        {
            yield break;
        }

        applyingLoad = true;

        yield return null;
        yield return null;

        FullGameSaveData data = JsonUtility.FromJson<FullGameSaveData>(PlayerPrefs.GetString(FullSaveKey));
        if (data != null)
        {
            ApplyWorldTime();
            ApplyPlayer(data);
            ApplyCamera(data);
            yield return null;
            ApplyFavorites(data);
        }

        applyingLoad = false;
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
            health.currentHP = data.playerCurrentHP > 0 ? data.playerCurrentHP : health.currentHP;
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
            time.SetTime(year, month, day, hour);
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
