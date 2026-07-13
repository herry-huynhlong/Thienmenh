using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BicanhSessionSaveData
{
    public bool sessionRunning;
    public string activeEventName = "";
    public float remainingSeconds;
    public int sessionDeathCount;
    public bool handledSecretRealmEvent;
    public string lastObservedWorldEvent = "";
    public List<BicanhParticipantSaveData> participants =
        new List<BicanhParticipantSaveData>();
}

[Serializable]
public class BicanhParticipantSaveData
{
    public string stateKey;
    public string npcId;
    public string npcDataPersistentId;
    public string worldActorPersistentId;
    public string socialId;
    public string objectName;

    public Vector3 currentPosition;
    public Quaternion currentRotation;
    public bool currentActiveSelf;

    public Vector3 originalPosition;
    public Quaternion originalRotation;
    public bool originalActiveSelf;
    public bool originalWasDead;

    public bool hasRigidbody;
    public int rbBodyType;
    public bool rbSimulated;
    public float rbGravityScale;
    public int rbConstraints;
    public Vector2 rbLinearVelocity;
    public float rbAngularVelocity;

    public bool hasSmartNpc;
    public bool smartNpcAutonomousActivitiesEnabled;
    public bool smartNpcCanCultivate;
    public bool smartNpcCanFight;
    public bool smartNpcCanTrade;
    public bool smartNpcCanGather;
    public bool smartNpcCanSellGoods;
    public bool smartNpcCanMakeFriends;
    public bool smartNpcCanKillOthers;
    public bool smartNpcCanCompeteResource;
    public bool smartNpcCanCreateSect;
    public int smartNpcCurrentHP;
    public int smartNpcCurrentActionId;
    public string smartNpcCurrentActionKey;
    public string smartNpcCurrentAction;
    public int smartNpcOriginalHP;
    public int smartNpcOriginalActionId;
    public string smartNpcOriginalActionKey;
    public string smartNpcOriginalAction;

    public bool hasVillager;
    public bool villagerHomeRoutineManagedExternally;
    public bool villagerDailyTaskPlanEnabled;
    public bool villagerDailyRoutineEnabled;
    public bool villagerAutonomousWorkEnabled;
    public bool villagerAutonomousResourceWorkEnabled;
    public bool villagerAutonomousDangerousWorkEnabled;
    public bool villagerStrongNpcAvoidMortalWork;
    public bool villagerHideAtHome;
    public int villagerCurrentHP;
    public int villagerCurrentActionId;
    public string villagerCurrentActionKey;
    public string villagerCurrentAction;
    public int villagerOriginalHP;
    public int villagerOriginalActionId;
    public string villagerOriginalActionKey;
    public string villagerOriginalAction;

    public bool hasMonster;
    public int monsterCurrentHP;
    public int monsterCurrentActionId;
    public string monsterCurrentActionKey;
    public string monsterCurrentAction;
    public int monsterOriginalHP;
    public int monsterOriginalActionId;
    public string monsterOriginalActionKey;
    public string monsterOriginalAction;
    public bool monsterGuardTerritory;

    public List<BicanhBehaviourSaveData> behaviourStates =
        new List<BicanhBehaviourSaveData>();
}

[Serializable]
public class BicanhBehaviourSaveData
{
    public string typeName;
    public bool enabled;
}

public class BicanhSessionManager : MonoBehaviour
{
    [Header("Unlock")]
    public bool autoStartOnSecretRealmOpen = true;
    public bool requireHeavenDaoPower = true;
    public HeavenDaoPower requiredHeavenDaoPower = HeavenDaoPower.TriggerHeavenEarthOmen;

    [Header("Eligibility")]
    public CultivationRealm minimumRealm = CultivationRealm.Foundation;
    [Range(1, 9)] public int minimumRealmStage = 1;
    public bool includeSmartNpcAI = true;

    [Header("Spawn")]
    public Transform spawnPointsRoot;
    public Transform fallbackReturnPoint;
    public bool requireValidFallbackReturnPoint = true;
    public float minimumSpawnSpacing = 0.9f;
    public float spawnJitterRadius = 0.35f;

    [Header("Session")]
    [Min(0f)] public float sessionDurationSeconds = 900f;
    [Min(1)] public int maxParticipantDeaths = 6;
    public bool restoreDeadParticipantsOnEnd = true;

    [Header("Runtime")]
    public bool sessionRunning;
    public string activeEventName = "";

    readonly List<ParticipantSnapshot> snapshots = new List<ParticipantSnapshot>();
    readonly List<Transform> cachedSpawnPoints = new List<Transform>();
    readonly List<Vector3> occupiedSpawnPositions = new List<Vector3>();
    readonly HashSet<GameObject> spawnedThisSession = new HashSet<GameObject>();
    readonly List<Collider2D> disabledSpawnAreaColliders =
        new List<Collider2D>();
    readonly List<bool> disabledSpawnAreaColliderStates =
        new List<bool>();
    float sessionEndAt;
    int sessionDeathCount;
    bool handledSecretRealmEvent;
    string lastObservedWorldEvent = "";

    public static BicanhSessionManager Instance { get; private set; }

    public static bool IsSessionRunning
    {
        get
        {
            return Instance != null && Instance.sessionRunning;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        NpcSocialEventBus.MonsterDefeated += HandleMonsterDefeated;
    }

    void OnDisable()
    {
        NpcSocialEventBus.MonsterDefeated -= HandleMonsterDefeated;
    }

    void Update()
    {
        if (!sessionRunning)
        {
            TryAutoStartFromWorldEvent();
            return;
        }

        if (sessionDurationSeconds > 0f && Time.time >= sessionEndAt)
        {
            EndSession("timeout");
            return;
        }

        ProcessDungeonDeaths();

        if (sessionDeathCount >= maxParticipantDeaths)
        {
            EndSession("death-limit");
            return;
        }
    }

    [ContextMenu("Begin Bicanh Session")]
    public void BeginSession()
    {
        if (sessionRunning)
        {
            return;
        }

        if (requireValidFallbackReturnPoint &&
            fallbackReturnPoint == null)
        {
            Debug.LogWarning("[Bicanh] Thieu fallbackReturnPoint hop le.");
            return;
        }

        RefreshSpawnPoints();

        List<GameObject> eligibleParticipants = CollectEligibleParticipants();
        if (eligibleParticipants.Count == 0)
        {
            Debug.LogWarning("[Bicanh] Khong tim thay participant hop le.");
            return;
        }

        if (cachedSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[Bicanh] Chua co spawn point hop le.");
            return;
        }

        snapshots.Clear();
        spawnedThisSession.Clear();
        occupiedSpawnPositions.Clear();

        List<Transform> shuffledSpawnPoints = new List<Transform>(cachedSpawnPoints);
        Shuffle(shuffledSpawnPoints);

        for (int i = 0; i < eligibleParticipants.Count; i++)
        {
            GameObject entity = eligibleParticipants[i];
            if (entity == null)
            {
                continue;
            }

            Transform spawnPoint = shuffledSpawnPoints[i % shuffledSpawnPoints.Count];
            Vector3 spawnPosition = GetSpawnPosition(spawnPoint, i);
            CaptureAndApply(entity, spawnPosition);
        }

        sessionRunning = snapshots.Count > 0;
        if (!sessionRunning)
        {
            Debug.LogWarning("[Bicanh] Khong tao duoc session.");
            return;
        }

        sessionEndAt = Time.time + Mathf.Max(1f, sessionDurationSeconds);
        activeEventName = "Bicanh";
        sessionDeathCount = 0;
        handledSecretRealmEvent = true;
        DisableSpawnAreaColliders();

        Debug.Log($"[Bicanh] Session bat dau: {snapshots.Count} participant.");
    }

    [ContextMenu("End Bicanh Session")]
    public void EndSession()
    {
        EndSession("manual");
    }

    [ContextMenu("End Bicanh Session (Player)")]
    public void EndSessionByPlayer()
    {
        EndSession("player");
    }

    public void EndSession(string reason)
    {
        if (!sessionRunning)
        {
            return;
        }

        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            RestoreParticipant(snapshots[i], false);
        }

        snapshots.Clear();
        spawnedThisSession.Clear();
        occupiedSpawnPositions.Clear();
        RestoreSpawnAreaColliders();
        sessionRunning = false;
        activeEventName = "";
        sessionEndAt = 0f;
        sessionDeathCount = 0;

        Debug.Log($"[Bicanh] Session ket thuc: {reason}");
    }

    void TryAutoStartFromWorldEvent()
    {
        if (!autoStartOnSecretRealmOpen || handledSecretRealmEvent)
        {
            UpdateEventLatch();
            return;
        }

        if (WorldEventSystem.Instance == null)
        {
            return;
        }

        string currentEvent = WorldEventSystem.Instance.currentEvent;
        if (string.IsNullOrEmpty(currentEvent))
        {
            UpdateEventLatch();
            return;
        }

        if (!string.Equals(currentEvent, WorldEventType.SecretRealmOpen.ToString(), StringComparison.Ordinal))
        {
            UpdateEventLatch();
            return;
        }

        if (requireHeavenDaoPower &&
            (HeavenDaoSystem.Instance == null ||
            !HeavenDaoSystem.Instance.HasPower(requiredHeavenDaoPower)))
        {
            return;
        }

        if (lastObservedWorldEvent != currentEvent)
        {
            lastObservedWorldEvent = currentEvent;
            BeginSession();
        }
    }

    public bool ContainsParticipant(GameObject entity)
    {
        if (entity == null)
        {
            return false;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            ParticipantSnapshot snapshot = snapshots[i];
            if (snapshot != null && snapshot.entity == entity)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsDungeonParticipant(GameObject entity)
    {
        return Instance != null &&
            Instance.sessionRunning &&
            Instance.ContainsParticipant(entity);
    }

    public static bool AreDungeonParticipantsAllies(
        GameObject first,
        GameObject second)
    {
        return IsDungeonParticipant(first) &&
            IsDungeonParticipant(second);
    }

    public static bool ShouldPreserveDungeonDeath(GameObject entity)
    {
        return IsDungeonParticipant(entity);
    }

    public static bool TryGetDungeonRetreatPoint(
        GameObject entity,
        out Vector3 point)
    {
        point = Vector3.zero;
        if (Instance == null ||
            !Instance.sessionRunning ||
            entity == null ||
            !Instance.ContainsParticipant(entity))
        {
            return false;
        }

        if (Instance.cachedSpawnPoints.Count > 0 &&
            Instance.cachedSpawnPoints[0] != null)
        {
            point = Instance.cachedSpawnPoints[0].position;
            return true;
        }

        if (Instance.fallbackReturnPoint != null)
        {
            point = Instance.fallbackReturnPoint.position;
            return true;
        }

        point = Instance.transform.position;
        return true;
    }

    public BicanhSessionSaveData CaptureSaveData()
    {
        BicanhSessionSaveData data = new BicanhSessionSaveData
        {
            sessionRunning = sessionRunning,
            activeEventName = activeEventName ?? "",
            remainingSeconds =
                sessionDurationSeconds > 0f && sessionRunning
                    ? Mathf.Max(0f, sessionEndAt - Time.time)
                    : 0f,
            sessionDeathCount = sessionDeathCount,
            handledSecretRealmEvent = handledSecretRealmEvent,
            lastObservedWorldEvent = lastObservedWorldEvent ?? ""
        };

        if (!sessionRunning)
        {
            return data;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            BicanhParticipantSaveData participant =
                CaptureParticipantSaveData(snapshots[i]);
            if (participant != null)
            {
                data.participants.Add(participant);
            }
        }

        return data;
    }

    public void RestoreFromSaveData(BicanhSessionSaveData data)
    {
        snapshots.Clear();
        spawnedThisSession.Clear();
        occupiedSpawnPositions.Clear();
        RestoreSpawnAreaColliders();

        if (data == null || !data.sessionRunning)
        {
            sessionRunning = false;
            activeEventName = "";
            sessionEndAt = 0f;
            sessionDeathCount = 0;
            return;
        }

        sessionRunning = false;
        activeEventName = data.activeEventName ?? "";
        sessionDeathCount = Mathf.Max(0, data.sessionDeathCount);
        handledSecretRealmEvent = data.handledSecretRealmEvent;
        lastObservedWorldEvent = data.lastObservedWorldEvent ?? "";
        sessionEndAt = sessionDurationSeconds > 0f
            ? Time.time + Mathf.Max(1f, data.remainingSeconds)
            : 0f;

        if (data.participants != null)
        {
            for (int i = 0; i < data.participants.Count; i++)
            {
                RestoreParticipantSaveData(data.participants[i]);
            }
        }

        sessionRunning = snapshots.Count > 0;
        if (!sessionRunning)
        {
            activeEventName = "";
            sessionEndAt = 0f;
            RestoreSpawnAreaColliders();
        }
        else
        {
            DisableSpawnAreaColliders();
        }
    }

    void UpdateEventLatch()
    {
        if (WorldEventSystem.Instance == null)
        {
            handledSecretRealmEvent = false;
            lastObservedWorldEvent = "";
            return;
        }

        string currentEvent = WorldEventSystem.Instance.currentEvent;
        if (!string.Equals(currentEvent, WorldEventType.SecretRealmOpen.ToString(), StringComparison.Ordinal))
        {
            handledSecretRealmEvent = false;
            lastObservedWorldEvent = currentEvent ?? "";
        }
    }

    void ProcessDungeonDeaths()
    {
        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            ParticipantSnapshot snapshot = snapshots[i];
            if (snapshot == null ||
                snapshot.entity == null ||
                !IsDead(snapshot.entity))
            {
                continue;
            }

            HandleDungeonDeath(snapshot);
            snapshots.RemoveAt(i);
        }
    }

    void HandleMonsterDefeated(
        MonsterAI monster,
        Vector3 position,
        string monsterName,
        int monsterLevel)
    {
        if (!sessionRunning ||
            monster == null)
        {
            return;
        }

        SmartNpcAI killer = monster.LastSmartNpcAttacker;
        if (killer == null ||
            !IsDungeonParticipant(killer.gameObject))
        {
            return;
        }

        int cultivationReward = Mathf.Max(10, monsterLevel * 15);
        int originReward = Mathf.Max(1, monsterLevel * 2);

        NpcRoleUtility.AddCultivationExp(killer.gameObject, cultivationReward);

        if (HeavenDaoSystem.Instance != null)
        {
            string rewardReason =
                "Bich Anh diet " +
                (!string.IsNullOrWhiteSpace(monsterName)
                    ? monsterName
                    : "quai");
            HeavenDaoSystem.Instance.AddOrigin(originReward, rewardReason);
        }

        Debug.Log(
            "[Bicanh] " +
            killer.name +
            " ha " +
            (!string.IsNullOrWhiteSpace(monsterName) ? monsterName : "quai") +
            ", + " + cultivationReward +
            " tu vi, + " + originReward +
            " origin.");
    }

    void HandleDungeonDeath(ParticipantSnapshot snapshot)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        bool countTowardLimit =
            snapshot.smartNpc != null ||
            snapshot.villager != null;

        if (countTowardLimit)
        {
            sessionDeathCount += 1;
        }

        RestoreParticipant(snapshot, true);
        spawnedThisSession.Remove(snapshot.entity);
    }

    List<GameObject> CollectEligibleParticipants()
    {
        List<GameObject> result = new List<GameObject>();

        if (includeSmartNpcAI)
        {
            foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Include))
            {
                GameObject entity = smartNpc != null ? smartNpc.gameObject : null;
                if (IsDungeonParticipant(entity) ||
                    !IsEligibleBicanhNpc(entity))
                {
                    continue;
                }

                result.Add(entity);
            }
        }

        return result;
    }

    bool IsEligibleBicanhNpc(GameObject entity)
    {
        if (entity == null)
        {
            return false;
        }

        if (IsDead(entity))
        {
            return false;
        }

        return !IsExcludedBicanhSpecialNpc(entity);
    }

    bool IsExcludedBicanhSpecialNpc(GameObject entity)
    {
        if (entity == null)
        {
            return true;
        }

        return entity.GetComponent<NpcTaskProvider>() != null ||
            entity.GetComponent<NpcCounterBroker>() != null ||
            entity.GetComponent<NpcShopStockRefill>() != null ||
            string.Equals(entity.name, "NPC_B", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entity.name, "NPC_C", StringComparison.OrdinalIgnoreCase);
    }

    void CaptureAndApply(GameObject entity, Vector3 destination)
    {
        if (entity == null)
        {
            return;
        }

        ParticipantSnapshot snapshot = new ParticipantSnapshot(entity);
        snapshots.Add(snapshot);

        ApplyDungeonMode(snapshot, destination);
        occupiedSpawnPositions.Add(destination);
        spawnedThisSession.Add(entity);
    }

    BicanhParticipantSaveData CaptureParticipantSaveData(
        ParticipantSnapshot snapshot)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return null;
        }

        GameObject entity = snapshot.entity;
        BicanhParticipantSaveData data =
            new BicanhParticipantSaveData
            {
                stateKey = GetEntityStateKey(entity),
                objectName = entity.name,
                currentPosition = entity.transform.position,
                currentRotation = entity.transform.rotation,
                currentActiveSelf = entity.activeSelf,
                originalPosition = snapshot.position,
                originalRotation = snapshot.rotation,
                originalActiveSelf = snapshot.wasActiveSelf,
                originalWasDead = snapshot.wasDead
            };

        NPCIdentity identity = entity.GetComponent<NPCIdentity>();
        if (identity != null)
        {
            data.npcId = identity.npcId;
        }

        NpcData npcData = entity.GetComponent<NpcData>();
        if (npcData != null)
        {
            npcData.EnsurePersistentId();
            data.npcDataPersistentId = npcData.persistentId;
        }

        SpawnedWorldActor actor = entity.GetComponent<SpawnedWorldActor>();
        if (actor != null)
        {
            actor.EnsurePersistentId();
            data.worldActorPersistentId = actor.persistentId;
        }

        NpcSocialIdentity socialIdentity =
            entity.GetComponent<NpcSocialIdentity>();
        if (socialIdentity != null)
        {
            data.socialId = socialIdentity.socialId;
        }

        data.hasRigidbody = snapshot.rb != null;
        if (snapshot.rb != null)
        {
            data.rbBodyType = (int)snapshot.rbBodyType;
            data.rbSimulated = snapshot.rbSimulated;
            data.rbGravityScale = snapshot.rbGravityScale;
            data.rbConstraints = (int)snapshot.rbConstraints;
            data.rbLinearVelocity = snapshot.rbLinearVelocity;
            data.rbAngularVelocity = snapshot.rbAngularVelocity;
        }

        CaptureSmartNpcSaveData(snapshot, data);
        CaptureVillagerSaveData(snapshot, data);
        CaptureMonsterSaveData(snapshot, data);
        CaptureBehaviourSaveData(snapshot, data);
        return data;
    }

    void CaptureSmartNpcSaveData(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        SmartNpcAI smartNpc = snapshot.smartNpc;
        data.hasSmartNpc = smartNpc != null;
        if (smartNpc == null)
        {
            return;
        }

        data.smartNpcAutonomousActivitiesEnabled =
            snapshot.smartNpcAutonomousActivitiesEnabled;
        data.smartNpcCanCultivate = snapshot.smartNpcCanCultivate;
        data.smartNpcCanFight = snapshot.smartNpcCanFight;
        data.smartNpcCanTrade = snapshot.smartNpcCanTrade;
        data.smartNpcCanGather = snapshot.smartNpcCanGather;
        data.smartNpcCanSellGoods = snapshot.smartNpcCanSellGoods;
        data.smartNpcCanMakeFriends = snapshot.smartNpcCanMakeFriends;
        data.smartNpcCanKillOthers = snapshot.smartNpcCanKillOthers;
        data.smartNpcCanCompeteResource =
            snapshot.smartNpcCanCompeteResource;
        data.smartNpcCanCreateSect = snapshot.smartNpcCanCreateSect;
        data.smartNpcOriginalHP = snapshot.smartNpcCurrentHP;
        data.smartNpcOriginalAction = snapshot.smartNpcCurrentAction;
        NpcActionState originalAction =
            NpcActionState.FromDisplayText(snapshot.smartNpcCurrentAction);
        data.smartNpcOriginalActionId = (int)originalAction.id;
        data.smartNpcOriginalActionKey = originalAction.key;
        data.smartNpcCurrentHP = smartNpc.currentHP;
        data.smartNpcCurrentAction = smartNpc.currentAction;
        NpcActionState currentAction = smartNpc.CurrentActionState;
        data.smartNpcCurrentActionId = (int)currentAction.id;
        data.smartNpcCurrentActionKey = currentAction.key;
    }

    void CaptureVillagerSaveData(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        VillagerAI villager = snapshot.villager;
        data.hasVillager = villager != null;
        if (villager == null)
        {
            return;
        }

        data.villagerHomeRoutineManagedExternally =
            snapshot.villagerHomeRoutineManagedExternally;
        data.villagerDailyTaskPlanEnabled =
            snapshot.villagerDailyTaskPlanEnabled;
        data.villagerDailyRoutineEnabled =
            snapshot.villagerDailyRoutineEnabled;
        data.villagerAutonomousWorkEnabled =
            snapshot.villagerAutonomousWorkEnabled;
        data.villagerAutonomousResourceWorkEnabled =
            snapshot.villagerAutonomousResourceWorkEnabled;
        data.villagerAutonomousDangerousWorkEnabled =
            snapshot.villagerAutonomousDangerousWorkEnabled;
        data.villagerStrongNpcAvoidMortalWork =
            snapshot.villagerStrongNpcAvoidMortalWork;
        data.villagerHideAtHome = snapshot.villagerHideAtHome;
        data.villagerOriginalHP = snapshot.villagerCurrentHP;
        data.villagerOriginalAction = snapshot.villagerCurrentAction;
        NpcActionState originalAction =
            NpcActionState.FromDisplayText(snapshot.villagerCurrentAction);
        data.villagerOriginalActionId = (int)originalAction.id;
        data.villagerOriginalActionKey = originalAction.key;
        data.villagerCurrentHP = villager.currentHP;
        data.villagerCurrentAction = villager.currentAction;
        NpcActionState currentAction = villager.CurrentActionState;
        data.villagerCurrentActionId = (int)currentAction.id;
        data.villagerCurrentActionKey = currentAction.key;
    }

    void CaptureMonsterSaveData(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        MonsterAI monster = snapshot.monster;
        data.hasMonster = monster != null;
        if (monster == null)
        {
            return;
        }

        data.monsterGuardTerritory = snapshot.monsterGuardTerritory;
        data.monsterOriginalHP = snapshot.monsterCurrentHP;
        data.monsterOriginalAction = snapshot.monsterCurrentAction;
        NpcActionState originalAction =
            NpcActionState.FromDisplayText(snapshot.monsterCurrentAction);
        data.monsterOriginalActionId = (int)originalAction.id;
        data.monsterOriginalActionKey = originalAction.key;
        data.monsterCurrentHP = monster.currentHP;
        data.monsterCurrentAction = monster.currentAction;
        NpcActionState currentAction = monster.CurrentActionState;
        data.monsterCurrentActionId = (int)currentAction.id;
        data.monsterCurrentActionKey = currentAction.key;
    }

    void CaptureBehaviourSaveData(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        for (int i = 0; i < snapshot.behaviourStates.Count; i++)
        {
            BehaviourState state = snapshot.behaviourStates[i];
            if (state == null || state.behaviour == null)
            {
                continue;
            }

            data.behaviourStates.Add(new BicanhBehaviourSaveData
            {
                typeName = state.behaviour.GetType().FullName,
                enabled = state.enabled
            });
        }
    }

    void RestoreParticipantSaveData(BicanhParticipantSaveData data)
    {
        if (data == null)
        {
            return;
        }

        GameObject entity = FindEntityBySaveData(data);
        if (entity == null)
        {
            Debug.LogWarning(
                "[Bicanh] Khong khoi phuc duoc participant: " +
                data.stateKey);
            return;
        }

        ParticipantSnapshot snapshot = new ParticipantSnapshot(entity);
        ApplySavedSnapshotState(snapshot, data);
        ApplySavedDungeonState(snapshot, data);

        snapshots.Add(snapshot);
        spawnedThisSession.Add(entity);
        occupiedSpawnPositions.Add(entity.transform.position);
    }

    void ApplySavedSnapshotState(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        snapshot.position = data.originalPosition;
        snapshot.rotation = data.originalRotation;
        snapshot.wasActiveSelf = data.originalActiveSelf;
        snapshot.wasDead = data.originalWasDead;

        if (snapshot.rb != null && data.hasRigidbody)
        {
            snapshot.rbBodyType = (RigidbodyType2D)data.rbBodyType;
            snapshot.rbSimulated = data.rbSimulated;
            snapshot.rbGravityScale = data.rbGravityScale;
            snapshot.rbConstraints =
                (RigidbodyConstraints2D)data.rbConstraints;
            snapshot.rbLinearVelocity = data.rbLinearVelocity;
            snapshot.rbAngularVelocity = data.rbAngularVelocity;
        }

        if (snapshot.smartNpc != null && data.hasSmartNpc)
        {
            snapshot.smartNpcAutonomousActivitiesEnabled =
                data.smartNpcAutonomousActivitiesEnabled;
            snapshot.smartNpcCanCultivate = data.smartNpcCanCultivate;
            snapshot.smartNpcCanFight = data.smartNpcCanFight;
            snapshot.smartNpcCanTrade = data.smartNpcCanTrade;
            snapshot.smartNpcCanGather = data.smartNpcCanGather;
            snapshot.smartNpcCanSellGoods = data.smartNpcCanSellGoods;
            snapshot.smartNpcCanMakeFriends = data.smartNpcCanMakeFriends;
            snapshot.smartNpcCanKillOthers = data.smartNpcCanKillOthers;
            snapshot.smartNpcCanCompeteResource =
                data.smartNpcCanCompeteResource;
            snapshot.smartNpcCanCreateSect = data.smartNpcCanCreateSect;
            snapshot.smartNpcCurrentHP = data.smartNpcOriginalHP;
            snapshot.smartNpcCurrentAction = data.smartNpcOriginalAction;
        }

        if (snapshot.villager != null && data.hasVillager)
        {
            snapshot.villagerHomeRoutineManagedExternally =
                data.villagerHomeRoutineManagedExternally;
            snapshot.villagerDailyTaskPlanEnabled =
                data.villagerDailyTaskPlanEnabled;
            snapshot.villagerDailyRoutineEnabled =
                data.villagerDailyRoutineEnabled;
            snapshot.villagerAutonomousWorkEnabled =
                data.villagerAutonomousWorkEnabled;
            snapshot.villagerAutonomousResourceWorkEnabled =
                data.villagerAutonomousResourceWorkEnabled;
            snapshot.villagerAutonomousDangerousWorkEnabled =
                data.villagerAutonomousDangerousWorkEnabled;
            snapshot.villagerStrongNpcAvoidMortalWork =
                data.villagerStrongNpcAvoidMortalWork;
            snapshot.villagerHideAtHome = data.villagerHideAtHome;
            snapshot.villagerCurrentHP = data.villagerOriginalHP;
            snapshot.villagerCurrentAction = data.villagerOriginalAction;
        }

        if (snapshot.monster != null && data.hasMonster)
        {
            snapshot.monsterCurrentHP = data.monsterOriginalHP;
            snapshot.monsterCurrentAction = data.monsterOriginalAction;
            snapshot.monsterGuardTerritory = data.monsterGuardTerritory;
        }
    }

    void ApplySavedDungeonState(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        GameObject entity = snapshot.entity;
        entity.SetActive(data.currentActiveSelf);
        SetWorldPosition(entity, data.currentPosition);
        entity.transform.rotation = data.currentRotation;
        NotifyTeleported(entity);

        if (snapshot.smartNpc != null && data.hasSmartNpc)
        {
            snapshot.smartNpc.isBicanhParticipant = true;
        }

        RestoreCurrentCombatState(snapshot, data);
        ApplyDungeonRestrictions(snapshot);
        RestoreSavedBehaviourSuspension(snapshot, data);
        RestoreRenderersAndColliders(entity);
    }

    void RestoreCurrentCombatState(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        if (snapshot.smartNpc != null && data.hasSmartNpc)
        {
            snapshot.smartNpc.currentHP = data.smartNpcCurrentHP;
            if (snapshot.smartNpc.characterStats != null)
            {
                snapshot.smartNpc.characterStats.currentHP =
                    data.smartNpcCurrentHP;
            }

            if (!string.IsNullOrWhiteSpace(data.smartNpcCurrentAction))
            {
                snapshot.smartNpc.SetCurrentActionState(
                    ResolveSavedActionState(
                        data.smartNpcCurrentActionKey,
                        data.smartNpcCurrentActionId,
                        data.smartNpcCurrentAction));
            }
        }

        if (snapshot.villager != null && data.hasVillager)
        {
            snapshot.villager.currentHP = data.villagerCurrentHP;
            if (snapshot.villager.characterStats != null)
            {
                snapshot.villager.characterStats.currentHP =
                    data.villagerCurrentHP;
            }

            if (!string.IsNullOrWhiteSpace(data.villagerCurrentAction))
            {
                snapshot.villager.SetCurrentActionState(
                    ResolveSavedActionState(
                        data.villagerCurrentActionKey,
                        data.villagerCurrentActionId,
                        data.villagerCurrentAction));
            }
        }

        if (snapshot.monster != null && data.hasMonster)
        {
            snapshot.monster.currentHP = data.monsterCurrentHP;
            if (snapshot.monster.entityProfile != null)
            {
                snapshot.monster.entityProfile.stats.currentHP =
                    data.monsterCurrentHP;
            }

            if (!string.IsNullOrWhiteSpace(data.monsterCurrentAction))
            {
                snapshot.monster.SetCurrentActionState(
                    ResolveSavedActionState(
                        data.monsterCurrentActionKey,
                        data.monsterCurrentActionId,
                        data.monsterCurrentAction));
            }
        }
    }

    void ApplyDungeonRestrictions(ParticipantSnapshot snapshot)
    {
        if (snapshot.smartNpc != null)
        {
            snapshot.smartNpc.autonomousActivitiesEnabled = true;
            snapshot.smartNpc.canCultivate = false;
            snapshot.smartNpc.canFight = true;
            snapshot.smartNpc.canTrade = false;
            snapshot.smartNpc.canGather = false;
            snapshot.smartNpc.canSellGoods = false;
            snapshot.smartNpc.canMakeFriends = false;
            snapshot.smartNpc.canKillOthers = true;
            snapshot.smartNpc.canCompeteResource = true;
            snapshot.smartNpc.canCreateSect = false;
        }

        if (snapshot.villager != null)
        {
            snapshot.villager.homeRoutineManagedExternally = true;
            snapshot.villager.dailyTaskPlanEnabled = false;
            snapshot.villager.dailyRoutineEnabled = false;
            snapshot.villager.autonomousWorkEnabled = false;
            snapshot.villager.autonomousResourceWorkEnabled = false;
            snapshot.villager.autonomousDangerousWorkEnabled = false;
            snapshot.villager.strongNpcAvoidMortalWork = false;
            snapshot.villager.hideAtHome = false;
        }

        if (snapshot.monster != null)
        {
            snapshot.monster.guardTerritory = false;
        }
    }

    void RestoreSavedBehaviourSuspension(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        snapshot.behaviourStates.Clear();

        MonoBehaviour[] behaviours =
            snapshot.entity.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null ||
                !ShouldSuspendBehaviour(behaviour) ||
                behaviour is SmartNpcAI ||
                behaviour is VillagerAI ||
                behaviour is MonsterAI)
            {
                continue;
            }

            bool originalEnabled =
                FindSavedBehaviourEnabled(data, behaviour, behaviour.enabled);
            snapshot.behaviourStates.Add(
                new BehaviourState(behaviour, originalEnabled));
            behaviour.enabled = false;
        }
    }

    bool FindSavedBehaviourEnabled(
        BicanhParticipantSaveData data,
        Behaviour behaviour,
        bool fallback)
    {
        if (data == null ||
            data.behaviourStates == null ||
            behaviour == null)
        {
            return fallback;
        }

        string fullName = behaviour.GetType().FullName;
        for (int i = 0; i < data.behaviourStates.Count; i++)
        {
            BicanhBehaviourSaveData saved = data.behaviourStates[i];
            if (saved != null &&
                string.Equals(
                    saved.typeName,
                    fullName,
                    StringComparison.Ordinal))
            {
                return saved.enabled;
            }
        }

        return fallback;
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

    void ApplyDungeonMode(ParticipantSnapshot snapshot, Vector3 destination)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        GameObject entity = snapshot.entity;
        SetWorldPosition(entity, destination);
        if (!entity.activeSelf)
        {
            entity.SetActive(true);
        }

        SmartNpcAI smartNpc = snapshot.smartNpc;
        if (smartNpc != null)
        {
            snapshot.smartNpcAutonomousActivitiesEnabled = smartNpc.autonomousActivitiesEnabled;
            snapshot.smartNpcCanCultivate = smartNpc.canCultivate;
            snapshot.smartNpcCanFight = smartNpc.canFight;
            snapshot.smartNpcCanTrade = smartNpc.canTrade;
            snapshot.smartNpcCanGather = smartNpc.canGather;
            snapshot.smartNpcCanSellGoods = smartNpc.canSellGoods;
            snapshot.smartNpcCanMakeFriends = smartNpc.canMakeFriends;
            snapshot.smartNpcCanKillOthers = smartNpc.canKillOthers;
            snapshot.smartNpcCanCompeteResource = smartNpc.canCompeteResource;
            snapshot.smartNpcCanCreateSect = smartNpc.canCreateSect;
            snapshot.smartNpcCurrentAction = smartNpc.currentAction;

            smartNpc.autonomousActivitiesEnabled = true;
            smartNpc.canCultivate = false;
            smartNpc.canFight = true;
            smartNpc.canTrade = false;
            smartNpc.canGather = false;
            smartNpc.canSellGoods = false;
            smartNpc.canMakeFriends = false;
            smartNpc.canKillOthers = true;
            smartNpc.canCompeteResource = true;
            smartNpc.canCreateSect = false;
            smartNpc.EnterBicanhSessionMode();
        }

        NotifyTeleported(entity);

        NpcRoleUtility.StopForConversation(entity);
        NpcRoleUtility.SetAction(entity, NpcText.Action("idle"));

        VillagerAI villager = snapshot.villager;
        if (villager != null)
        {
            snapshot.villagerHomeRoutineManagedExternally = villager.homeRoutineManagedExternally;
            snapshot.villagerDailyTaskPlanEnabled = villager.dailyTaskPlanEnabled;
            snapshot.villagerDailyRoutineEnabled = villager.dailyRoutineEnabled;
            snapshot.villagerAutonomousWorkEnabled = villager.autonomousWorkEnabled;
            snapshot.villagerAutonomousResourceWorkEnabled = villager.autonomousResourceWorkEnabled;
            snapshot.villagerAutonomousDangerousWorkEnabled = villager.autonomousDangerousWorkEnabled;
            snapshot.villagerStrongNpcAvoidMortalWork = villager.strongNpcAvoidMortalWork;
            snapshot.villagerHideAtHome = villager.hideAtHome;
            snapshot.villagerCurrentAction = villager.currentAction;

            villager.homeRoutineManagedExternally = true;
            villager.dailyTaskPlanEnabled = false;
            villager.dailyRoutineEnabled = false;
            villager.autonomousWorkEnabled = false;
            villager.autonomousResourceWorkEnabled = false;
            villager.autonomousDangerousWorkEnabled = false;
            villager.strongNpcAvoidMortalWork = false;
            villager.hideAtHome = false;
        }

        MonsterAI monster = snapshot.monster;
        if (monster != null)
        {
            snapshot.monsterGuardTerritory = monster.guardTerritory;
            monster.guardTerritory = false;
        }

        DisableNonCombatBehaviours(snapshot);
    }

    void DisableNonCombatBehaviours(ParticipantSnapshot snapshot)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        MonoBehaviour[] behaviours =
            snapshot.entity.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null ||
                !ShouldSuspendBehaviour(behaviour))
            {
                continue;
            }

            if (behaviour is SmartNpcAI ||
                behaviour is VillagerAI ||
                behaviour is MonsterAI)
            {
                continue;
            }

            snapshot.behaviourStates.Add(new BehaviourState(behaviour, behaviour.enabled));
            behaviour.enabled = false;
        }
    }

    bool ShouldSuspendBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null)
        {
            return false;
        }

        string name = behaviour.GetType().Name;
        return name == nameof(NpcTaskProvider) ||
            name == nameof(NpcResourceGatherer) ||
            name == nameof(NpcTradeAgent);
    }

    void RestoreParticipant(ParticipantSnapshot snapshot, bool restoreInventory)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        GameObject entity = snapshot.entity;
        RestoreNonCombatBehaviours(snapshot);
        RestoreTypeSpecificState(snapshot);
        RestoreRenderersAndColliders(entity);

        if (entity.activeSelf != snapshot.wasActiveSelf)
        {
            entity.SetActive(snapshot.wasActiveSelf);
        }

        if (snapshot.rb != null)
        {
            snapshot.rb.bodyType = snapshot.rbBodyType;
            snapshot.rb.simulated = snapshot.rbSimulated;
            snapshot.rb.gravityScale = snapshot.rbGravityScale;
            snapshot.rb.constraints = snapshot.rbConstraints;
            snapshot.rb.linearVelocity = snapshot.rbLinearVelocity;
            snapshot.rb.angularVelocity = snapshot.rbAngularVelocity;
        }

        SetWorldPosition(entity, ResolveReturnPosition(snapshot));
        entity.transform.rotation = snapshot.rotation;

        NotifyTeleported(entity);

        if (IsDead(entity) || snapshot.wasDead)
        {
            RestoreDeathState(snapshot);
        }
    }

    Vector3 ResolveReturnPosition(ParticipantSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return GetFallbackReturnPosition();
        }

        if (IsValidReturnPosition(snapshot.position))
        {
            return snapshot.position;
        }

        return GetFallbackReturnPosition();
    }

    bool IsValidReturnPosition(Vector3 position)
    {
        if (!float.IsFinite(position.x) ||
            !float.IsFinite(position.y) ||
            !float.IsFinite(position.z))
        {
            return false;
        }

        return NpcMapArea.FindArea(position) != null;
    }

    Vector3 GetFallbackReturnPosition()
    {
        if (fallbackReturnPoint != null)
        {
            return fallbackReturnPoint.position;
        }

        return transform.position;
    }

    void RestoreInventory(ParticipantSnapshot snapshot)
    {
        if (snapshot == null ||
            snapshot.inventory == null ||
            snapshot.inventorySnapshot == null)
        {
            return;
        }

        CopyInventoryItems(snapshot.inventorySnapshot, snapshot.inventory.items);
        snapshot.inventory.MarkDirty();
    }

    void RestoreRenderersAndColliders(GameObject entity)
    {
        if (entity == null)
        {
            return;
        }

        Renderer[] renderers = entity.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }

        Collider2D[] colliders = entity.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
            }
        }
    }

    void RestoreNonCombatBehaviours(ParticipantSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.behaviourStates.Count; i++)
        {
            BehaviourState state = snapshot.behaviourStates[i];
            if (state.behaviour != null)
            {
                state.behaviour.enabled = state.enabled;
            }
        }
    }

    void RestoreTypeSpecificState(ParticipantSnapshot snapshot)
    {
        SmartNpcAI smartNpc = snapshot.smartNpc;
        if (smartNpc != null)
        {
            smartNpc.autonomousActivitiesEnabled = snapshot.smartNpcAutonomousActivitiesEnabled;
            smartNpc.canCultivate = snapshot.smartNpcCanCultivate;
            smartNpc.canFight = snapshot.smartNpcCanFight;
            smartNpc.canTrade = snapshot.smartNpcCanTrade;
            smartNpc.canGather = snapshot.smartNpcCanGather;
            smartNpc.canSellGoods = snapshot.smartNpcCanSellGoods;
            smartNpc.canMakeFriends = snapshot.smartNpcCanMakeFriends;
            smartNpc.canKillOthers = snapshot.smartNpcCanKillOthers;
            smartNpc.canCompeteResource = snapshot.smartNpcCanCompeteResource;
            smartNpc.canCreateSect = snapshot.smartNpcCanCreateSect;
            smartNpc.ExitBicanhSessionMode();
            if (!string.IsNullOrEmpty(snapshot.smartNpcCurrentAction))
            {
                smartNpc.ForceSetCurrentAction(snapshot.smartNpcCurrentAction);
            }
        }

        VillagerAI villager = snapshot.villager;
        if (villager != null)
        {
            villager.homeRoutineManagedExternally = snapshot.villagerHomeRoutineManagedExternally;
            villager.dailyTaskPlanEnabled = snapshot.villagerDailyTaskPlanEnabled;
            villager.dailyRoutineEnabled = snapshot.villagerDailyRoutineEnabled;
            villager.autonomousWorkEnabled = snapshot.villagerAutonomousWorkEnabled;
            villager.autonomousResourceWorkEnabled = snapshot.villagerAutonomousResourceWorkEnabled;
            villager.autonomousDangerousWorkEnabled = snapshot.villagerAutonomousDangerousWorkEnabled;
            villager.strongNpcAvoidMortalWork = snapshot.villagerStrongNpcAvoidMortalWork;
            villager.hideAtHome = snapshot.villagerHideAtHome;
            if (!string.IsNullOrEmpty(snapshot.villagerCurrentAction))
            {
                villager.SetCurrentActionState(
                    NpcActionState.FromDisplayText(
                        snapshot.villagerCurrentAction));
            }
        }
    }

    void RestoreDeathState(ParticipantSnapshot snapshot)
    {
        if (snapshot.smartNpc != null)
        {
            snapshot.smartNpc.currentHP = snapshot.smartNpcCurrentHP;
            if (snapshot.smartNpc.characterStats != null)
            {
                snapshot.smartNpc.characterStats.currentHP = snapshot.smartNpcCurrentHP;
            }

            SetPrivateBool(snapshot.smartNpc, "isDead", false);
            snapshot.smartNpc.ExitBicanhSessionMode();
            if (!string.IsNullOrEmpty(snapshot.smartNpcCurrentAction))
            {
                snapshot.smartNpc.ForceSetCurrentAction(snapshot.smartNpcCurrentAction);
            }
        }

        if (snapshot.villager != null)
        {
            snapshot.villager.currentHP = snapshot.villagerCurrentHP;
            if (snapshot.villager.characterStats != null)
            {
                snapshot.villager.characterStats.currentHP = snapshot.villagerCurrentHP;
            }

            if (!string.IsNullOrEmpty(snapshot.villagerCurrentAction))
            {
                snapshot.villager.SetCurrentActionState(
                    NpcActionState.FromDisplayText(
                        snapshot.villagerCurrentAction));
            }
            RestoreRenderersAndColliders(snapshot.villager.gameObject);
        }

        if (snapshot.monster != null)
        {
            snapshot.monster.currentHP = snapshot.monsterCurrentHP;
            if (snapshot.monster.entityProfile != null)
            {
                snapshot.monster.entityProfile.stats.currentHP = snapshot.monsterCurrentHP;
            }
            snapshot.monster.guardTerritory = snapshot.monsterGuardTerritory;

            SetPrivateBool(snapshot.monster, "isDead", false);
            SetPrivateBool(snapshot.monster, "isRespawning", false);
            if (!string.IsNullOrEmpty(snapshot.monsterCurrentAction))
            {
                snapshot.monster.SetCurrentActionState(
                    NpcActionState.FromDisplayText(
                        snapshot.monsterCurrentAction));
            }
            RestoreRenderersAndColliders(snapshot.monster.gameObject);

            if (snapshot.rb != null)
            {
                snapshot.rb.simulated = true;
            }
        }
    }

    static void SetPrivateBool(object target, string fieldName, bool value)
    {
        if (target == null || string.IsNullOrEmpty(fieldName))
        {
            return;
        }

        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(target, value);
        }
    }

    static void SetWorldPosition(GameObject entity, Vector3 position)
    {
        if (entity == null)
        {
            return;
        }

        Rigidbody2D rb = entity.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        entity.transform.position = position;
    }

    static void NotifyTeleported(GameObject entity)
    {
        if (entity == null)
        {
            return;
        }

        entity.SendMessage(
            "OnNpcMapTeleported",
            entity,
            SendMessageOptions.DontRequireReceiver);
    }

    static void CopyInventoryItems(
        List<ItemStack> source,
        List<ItemStack> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();

        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            ItemStack stack = source[i];
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            destination.Add(CloneItemStack(stack));
        }
    }

    static List<ItemStack> CloneInventoryItems(List<ItemStack> source)
    {
        List<ItemStack> result = new List<ItemStack>();
        CopyInventoryItems(source, result);
        return result;
    }

    static ItemStack CloneItemStack(ItemStack source)
    {
        if (source == null)
        {
            return null;
        }

        return new ItemStack
        {
            item = source.item,
            amount = source.amount,
            durability = source.durability,
            maxDurability = source.maxDurability,
            mastery = source.mastery,
            manualUseYears = source.manualUseYears,
            broken = source.broken,
            applied = source.applied
        };
    }

    Vector3 GetSpawnPosition(Transform spawnPoint, int participantIndex)
    {
        if (spawnPoint == null)
        {
            return transform.position;
        }

        Vector3 basePosition = spawnPoint.position;
        Vector2 jitter = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, spawnJitterRadius);
        Vector3 candidate = basePosition + (Vector3)jitter;

        if (!IsNearOccupiedPosition(candidate))
        {
            return candidate;
        }

        for (int i = 0; i < 6; i++)
        {
            Vector2 randomJitter = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, spawnJitterRadius);
            candidate = basePosition + (Vector3)randomJitter;
            if (!IsNearOccupiedPosition(candidate))
            {
                return candidate;
            }
        }

        float angle = (participantIndex * 137.5f) % 360f;
        Vector2 fallbackOffset =
            Quaternion.Euler(0f, 0f, angle) * Vector2.right * Mathf.Max(0.15f, minimumSpawnSpacing * 0.35f);
        return basePosition + (Vector3)fallbackOffset;
    }

    bool IsNearOccupiedPosition(Vector3 candidate)
    {
        for (int i = 0; i < occupiedSpawnPositions.Count; i++)
        {
            if (Vector2.Distance(candidate, occupiedSpawnPositions[i]) < minimumSpawnSpacing)
            {
                return true;
            }
        }

        return false;
    }

    void RefreshSpawnPoints()
    {
        cachedSpawnPoints.Clear();

        Transform root = spawnPointsRoot != null ? spawnPointsRoot : transform;
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null)
            {
                cachedSpawnPoints.Add(child);
            }
        }

        if (cachedSpawnPoints.Count == 0)
        {
            cachedSpawnPoints.Add(root);
        }
    }

    void DisableSpawnAreaColliders()
    {
        RestoreSpawnAreaColliders();

        if (spawnPointsRoot == null)
        {
            return;
        }

        Collider2D[] colliders =
            spawnPointsRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider2d = colliders[i];
            if (collider2d == null)
            {
                continue;
            }

            disabledSpawnAreaColliders.Add(collider2d);
            disabledSpawnAreaColliderStates.Add(collider2d.enabled);
            collider2d.enabled = false;
        }
    }

    void RestoreSpawnAreaColliders()
    {
        for (int i = 0; i < disabledSpawnAreaColliders.Count; i++)
        {
            Collider2D collider2d = disabledSpawnAreaColliders[i];
            if (collider2d != null)
            {
                collider2d.enabled =
                    i < disabledSpawnAreaColliderStates.Count &&
                    disabledSpawnAreaColliderStates[i];
            }
        }

        disabledSpawnAreaColliders.Clear();
        disabledSpawnAreaColliderStates.Clear();
    }

    static void Shuffle<T>(List<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    bool IsDead(GameObject entity)
    {
        if (entity == null)
        {
            return true;
        }

        SmartNpcAI smartNpc = entity.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.IsDead;
        }

        VillagerAI villager = entity.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.IsDead;
        }

        MonsterAI monster = entity.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return monster.IsDead;
        }

        return false;
    }

    GameObject FindEntityBySaveData(BicanhParticipantSaveData data)
    {
        if (data == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(data.npcId))
        {
            foreach (NPCIdentity identity in FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include))
            {
                if (identity != null &&
                    string.Equals(
                        identity.npcId,
                        data.npcId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return identity.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(data.worldActorPersistentId))
        {
            foreach (SpawnedWorldActor actor in FindObjectsByType<SpawnedWorldActor>(FindObjectsInactive.Include))
            {
                if (actor != null &&
                    string.Equals(
                        actor.persistentId,
                        data.worldActorPersistentId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return actor.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(data.npcDataPersistentId))
        {
            foreach (NpcData npcData in FindObjectsByType<NpcData>(FindObjectsInactive.Include))
            {
                if (npcData != null &&
                    string.Equals(
                        npcData.persistentId,
                        data.npcDataPersistentId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return npcData.gameObject;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(data.socialId))
        {
            foreach (NpcSocialIdentity socialIdentity in FindObjectsByType<NpcSocialIdentity>(FindObjectsInactive.Include))
            {
                if (socialIdentity != null &&
                    string.Equals(
                        socialIdentity.socialId,
                        data.socialId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return socialIdentity.gameObject;
                }
            }
        }

        return !string.IsNullOrWhiteSpace(data.objectName)
            ? GameObject.Find(data.objectName)
            : null;
    }

    string GetEntityStateKey(GameObject entity)
    {
        if (entity == null)
        {
            return "";
        }

        SpawnedWorldActor actor = entity.GetComponent<SpawnedWorldActor>();
        if (actor != null)
        {
            actor.EnsurePersistentId();
            if (!string.IsNullOrWhiteSpace(actor.persistentId))
            {
                return "actor:" + actor.persistentId;
            }
        }

        NpcData npcData = entity.GetComponent<NpcData>();
        if (npcData != null)
        {
            npcData.EnsurePersistentId();
            if (!string.IsNullOrWhiteSpace(npcData.persistentId))
            {
                return "npcData:" + npcData.persistentId;
            }
        }

        NPCIdentity identity = entity.GetComponent<NPCIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.npcId))
        {
            return "npc:" + identity.npcId;
        }

        NpcSocialIdentity socialIdentity =
            entity.GetComponent<NpcSocialIdentity>();
        if (socialIdentity != null &&
            !string.IsNullOrWhiteSpace(socialIdentity.socialId))
        {
            return "social:" + socialIdentity.socialId;
        }

        return "name:" + entity.name;
    }

    class BehaviourState
    {
        public Behaviour behaviour;
        public bool enabled;

        public BehaviourState(Behaviour behaviour, bool enabled)
        {
            this.behaviour = behaviour;
            this.enabled = enabled;
        }
    }

    class ParticipantSnapshot
    {
        public GameObject entity;
        public Vector3 position;
        public Quaternion rotation;
        public bool wasActiveSelf;
        public bool wasDead;
        public Rigidbody2D rb;
        public RigidbodyType2D rbBodyType;
        public bool rbSimulated;
        public float rbGravityScale;
        public RigidbodyConstraints2D rbConstraints;
        public Vector2 rbLinearVelocity;
        public float rbAngularVelocity;
        public readonly List<BehaviourState> behaviourStates = new List<BehaviourState>();

        public SmartNpcAI smartNpc;
        public bool smartNpcAutonomousActivitiesEnabled;
        public bool smartNpcCanCultivate;
        public bool smartNpcCanFight;
        public bool smartNpcCanTrade;
        public bool smartNpcCanGather;
        public bool smartNpcCanSellGoods;
        public bool smartNpcCanMakeFriends;
        public bool smartNpcCanKillOthers;
        public bool smartNpcCanCompeteResource;
        public bool smartNpcCanCreateSect;
        public int smartNpcCurrentHP;
        public string smartNpcCurrentAction;

        public VillagerAI villager;
        public bool villagerHomeRoutineManagedExternally;
        public bool villagerDailyTaskPlanEnabled;
        public bool villagerDailyRoutineEnabled;
        public bool villagerAutonomousWorkEnabled;
        public bool villagerAutonomousResourceWorkEnabled;
        public bool villagerAutonomousDangerousWorkEnabled;
        public bool villagerStrongNpcAvoidMortalWork;
        public bool villagerHideAtHome;
        public int villagerCurrentHP;
        public string villagerCurrentAction;

        public MonsterAI monster;
        public int monsterCurrentHP;
        public string monsterCurrentAction;
        public bool monsterGuardTerritory;
        public ItemInventory inventory;
        public List<ItemStack> inventorySnapshot;

        public ParticipantSnapshot(GameObject entity)
        {
            this.entity = entity;
            position = entity.transform.position;
            rotation = entity.transform.rotation;
            wasActiveSelf = entity.activeSelf;
            wasDead = false;

            rb = entity.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rbBodyType = rb.bodyType;
                rbSimulated = rb.simulated;
                rbGravityScale = rb.gravityScale;
                rbConstraints = rb.constraints;
                rbLinearVelocity = rb.linearVelocity;
                rbAngularVelocity = rb.angularVelocity;
            }

            smartNpc = entity.GetComponent<SmartNpcAI>();
            if (smartNpc != null)
            {
                smartNpcAutonomousActivitiesEnabled = smartNpc.autonomousActivitiesEnabled;
                smartNpcCanCultivate = smartNpc.canCultivate;
                smartNpcCanFight = smartNpc.canFight;
                smartNpcCanTrade = smartNpc.canTrade;
                smartNpcCanGather = smartNpc.canGather;
                smartNpcCanSellGoods = smartNpc.canSellGoods;
                smartNpcCanMakeFriends = smartNpc.canMakeFriends;
                smartNpcCanKillOthers = smartNpc.canKillOthers;
                smartNpcCanCompeteResource = smartNpc.canCompeteResource;
                smartNpcCanCreateSect = smartNpc.canCreateSect;
                smartNpcCurrentHP = smartNpc.currentHP;
                smartNpcCurrentAction = smartNpc.currentAction;
                wasDead = smartNpc.IsDead;
            }

            villager = entity.GetComponent<VillagerAI>();
            if (villager != null)
            {
                villagerHomeRoutineManagedExternally = villager.homeRoutineManagedExternally;
                villagerDailyTaskPlanEnabled = villager.dailyTaskPlanEnabled;
                villagerDailyRoutineEnabled = villager.dailyRoutineEnabled;
                villagerAutonomousWorkEnabled = villager.autonomousWorkEnabled;
                villagerAutonomousResourceWorkEnabled = villager.autonomousResourceWorkEnabled;
                villagerAutonomousDangerousWorkEnabled = villager.autonomousDangerousWorkEnabled;
                villagerStrongNpcAvoidMortalWork = villager.strongNpcAvoidMortalWork;
                villagerHideAtHome = villager.hideAtHome;
                villagerCurrentHP = villager.currentHP;
                villagerCurrentAction = villager.currentAction;
                wasDead = villager.IsDead;
            }

            monster = entity.GetComponent<MonsterAI>();
            if (monster != null)
            {
                monsterGuardTerritory = monster.guardTerritory;
                monster.guardTerritory = false;
                monsterCurrentHP = monster.currentHP;
                monsterCurrentAction = monster.currentAction;
                wasDead = monster.IsDead;
            }

            inventory = entity.GetComponent<ItemInventory>();
            inventorySnapshot = inventory != null
                ? CloneInventoryItems(inventory.items)
                : new List<ItemStack>();
        }
    }
}
