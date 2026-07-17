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

public partial class BicanhSessionManager : MonoBehaviour
{
    [Header("Unlock")]
    public bool autoStartOnSecretRealmOpen = true;
    public bool requireHeavenDaoPower = true;
    public HeavenDaoPower requiredHeavenDaoPower = HeavenDaoPower.OpenSpiritualVein;

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

    [Header("Elimination")]
    [Range(0f, 1f)] public float eliminationHpRatio = 0.05f;
    [Min(1)] public int minimumReturnHp = 1;

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

    bool ShouldEliminateParticipant(ParticipantSnapshot snapshot)
    {
        if (snapshot == null ||
            snapshot.entity == null)
        {
            return false;
        }

        float hpRatio =
            CombatPowerUtility.GetCurrentHpRatio(snapshot.entity);
        return hpRatio > 0f &&
            hpRatio <= eliminationHpRatio;
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

    void ApplyReturnRecoveryState(ParticipantSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        int returnHp = ResolveMinimumReturnHp(snapshot.entity);

        if (snapshot.smartNpc != null)
        {
            snapshot.smartNpc.SetCurrentHealth(
                Mathf.Clamp(
                    returnHp,
                    1,
                    snapshot.smartNpc.AuthoritativeMaxHP));

            SetPrivateBool(snapshot.smartNpc, "isDead", false);
        }

        if (snapshot.villager != null)
        {
            snapshot.villager.SetCurrentHealth(
                Mathf.Clamp(
                    returnHp,
                    1,
                    snapshot.villager.AuthoritativeMaxHP));
        }

        if (snapshot.monster != null)
        {
            snapshot.monster.currentHP =
                Mathf.Clamp(
                    returnHp,
                    1,
                    Mathf.Max(1, snapshot.monster.maxHP));
            if (snapshot.monster.entityProfile != null)
            {
                snapshot.monster.entityProfile.stats.currentHP =
                    snapshot.monster.currentHP;
            }

            SetPrivateBool(snapshot.monster, "isDead", false);
            SetPrivateBool(snapshot.monster, "isRespawning", false);
        }

        ApplyReturnActionState(snapshot);
    }

    void ApplyReturnActionState(ParticipantSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        if (snapshot.smartNpc != null)
        {
            snapshot.smartNpc.ForceSetCurrentAction(
                NpcText.Action("injured"));
        }

        if (snapshot.villager != null)
        {
            snapshot.villager.SetCurrentActionState(
                NpcActionState.FromDisplayText(
                    NpcText.Action("injured")));
        }

        if (snapshot.monster != null)
        {
            snapshot.monster.SetCurrentActionState(
                NpcActionState.FromDisplayText(
                    NpcText.Action("idle")));
        }
    }

    int ResolveMinimumReturnHp(GameObject entity)
    {
        int maxHp = ResolveParticipantMaxHp(entity);
        int ratioHp = maxHp > 0
            ? Mathf.CeilToInt(maxHp * Mathf.Clamp01(eliminationHpRatio))
            : 0;
        return Mathf.Max(1, minimumReturnHp, ratioHp);
    }

    int ResolveParticipantMaxHp(GameObject entity)
    {
        if (entity == null)
        {
            return 0;
        }

        CharacterStats characterStats =
            entity.GetComponent<CharacterStats>();
        if (characterStats != null &&
            characterStats.finalHP > 0)
        {
            return characterStats.finalHP;
        }

        SmartNpcAI smartNpc = entity.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.maxHP;
        }

        VillagerAI villager = entity.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.maxHP;
        }

        MonsterAI monster = entity.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return monster.maxHP;
        }

        return 0;
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
                smartNpcCurrentHP = smartNpc.AuthoritativeCurrentHP;
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
                villagerCurrentHP = villager.AuthoritativeCurrentHP;
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
