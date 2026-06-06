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
    public string displayName;
    public string realmText;
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

        FullGameSaveController existing = FindObjectOfType<FullGameSaveController>(true);
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
        if (!GameSaveSystem.HasSave || IsMenuScene(scene.name) || scene.name == "PersistentScene")
        {
            return;
        }

        StartCoroutine(ApplyFullSaveAfterSceneLoad());
    }

    public void SaveFullGame()
    {
        string sceneName = GetCurrentGameplaySceneName();
        if (string.IsNullOrEmpty(sceneName) || IsMenuScene(sceneName))
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
        SaveWorldSpawners();

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

            SpawnedWorldActor actor = favorite.GetComponent<SpawnedWorldActor>();
            data.favoriteNpcs.Add(new SavedFavoriteNpcData
            {
                persistentId = actor != null ? actor.persistentId : "",
                displayName = favorite.GetDisplayName(),
                realmText = favorite.GetRealmText()
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
            manager.AddFavorite(favorite);
        }
    }

    GameObject FindNpcByFavoriteData(SavedFavoriteNpcData saved)
    {
        if (saved == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(saved.persistentId))
        {
            foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Include))
            {
                if (actor != null && actor.persistentId == saved.persistentId)
                {
                    return actor.gameObject;
                }
            }
        }

        if (!string.IsNullOrEmpty(saved.displayName))
        {
            foreach (NpcFavorite favorite in FindObjectsByType<NpcFavorite>(FindObjectsInactive.Include))
            {
                if (favorite != null && favorite.GetDisplayName() == saved.displayName)
                {
                    return favorite.gameObject;
                }
            }
        }

        return null;
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
            time.currentYear = year;
            time.currentMonth = month;
            time.currentDay = day;
            time.currentHour = hour;
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

    void SaveWorldSpawners()
    {
        foreach (WorldSpawner spawner in FindObjectsByType<WorldSpawner>(FindObjectsInactive.Include))
        {
            if (spawner != null)
            {
                spawner.SaveSpawnedWorld();
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

        PlayerHealth health = FindObjectOfType<PlayerHealth>(true);
        if (health != null)
        {
            return health.gameObject;
        }

        CharacterStats stats = FindObjectOfType<CharacterStats>(true);
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

        return active.isLoaded ? active.name : "";
    }

    bool IsMenuScene()
    {
        return IsMenuScene(SceneManager.GetActiveScene().name);
    }

    bool IsMenuScene(string sceneName)
    {
        return sceneName == "MainMenu" || sceneName == "CharacterCreate";
    }

    bool IsNonGameplayScene(string sceneName)
    {
        return string.IsNullOrEmpty(sceneName) ||
            sceneName == "PersistentScene" ||
            sceneName == "MainMenu" ||
            sceneName == "CharacterCreate" ||
            sceneName == "LiteMapScene";
    }
}
