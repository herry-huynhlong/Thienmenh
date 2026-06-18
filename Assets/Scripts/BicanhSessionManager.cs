using System;
using System.Collections.Generic;
using UnityEngine;

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
    public bool includeMonsterAI = true;
    public bool includeVillagerAI = true;

    [Header("Spawn")]
    public Transform spawnPointsRoot;
    public Transform fallbackReturnPoint;
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

    public static bool ShouldPreserveDungeonDeath(GameObject entity)
    {
        return IsDungeonParticipant(entity);
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
            foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude))
            {
                GameObject entity = smartNpc != null ? smartNpc.gameObject : null;
                if (IsDungeonResident(entity) ||
                    !IsEligibleCombatant(entity))
                {
                    continue;
                }

                result.Add(entity);
            }
        }

        if (includeVillagerAI)
        {
            foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude))
            {
                GameObject entity = villager != null ? villager.gameObject : null;
                if (IsDungeonResident(entity) ||
                    !IsEligibleCombatant(entity))
                {
                    continue;
                }

                result.Add(entity);
            }
        }

        if (includeMonsterAI)
        {
            foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
            {
                GameObject entity = monster != null ? monster.gameObject : null;
                if (IsDungeonResident(entity) ||
                    !IsEligibleCombatant(entity))
                {
                    continue;
                }

                result.Add(entity);
            }
        }

        return result;
    }

    bool IsDungeonResident(GameObject entity)
    {
        return entity != null &&
            entity.GetComponent<BicanhDungeonResident>() != null;
    }

    bool IsEligibleCombatant(GameObject entity)
    {
        if (entity == null || !entity.activeInHierarchy)
        {
            return false;
        }

        if (IsDead(entity))
        {
            return false;
        }

        int power = GetRealmPower(entity);
        int requiredPower = CultivationProgression.GetRealmPower(
            minimumRealm,
            Mathf.Clamp(minimumRealmStage, 1, CultivationProgression.MaxStage));

        return power >= requiredPower;
    }

    int GetRealmPower(GameObject entity)
    {
        if (entity == null)
        {
            return 0;
        }

        SmartNpcAI smartNpc = entity.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return CultivationProgression.GetRealmPower(smartNpc.realm, smartNpc.realmStage);
        }

        VillagerAI villager = entity.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return CultivationProgression.GetRealmPower(villager.realm, villager.realmStage);
        }

        MonsterAI monster = entity.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return CultivationProgression.GetRealmPower(monster.realm, monster.realmStage);
        }

        return NpcRoleUtility.GetRealmPower(entity);
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

    void ApplyDungeonMode(ParticipantSnapshot snapshot, Vector3 destination)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        GameObject entity = snapshot.entity;
        SetWorldPosition(entity, destination);
        NotifyTeleported(entity);

        NpcRoleUtility.StopForConversation(entity);
        NpcRoleUtility.SetAction(entity, NpcText.Action("idle"));

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
        }

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
            name == nameof(NpcSocialSystem) ||
            name == nameof(NpcConversationAgent) ||
            name == nameof(NpcDecisionBrain) ||
            name == nameof(NpcNegotiationAgent) ||
            name == nameof(NpcResourceGatherer) ||
            name == nameof(NpcTradeAgent) ||
            name == nameof(NpcItemCollector) ||
            name == nameof(NpcFavorite) ||
            name == nameof(NpcIdentity) ||
            name == nameof(NpcNeeds) ||
            name == nameof(NpcPersonality) ||
            name == nameof(NpcRelationshipGraph) ||
            name == nameof(NpcMemory) ||
            name == nameof(NpcOverheadDialogueUI) ||
            name == nameof(NpcFavoriteManager);
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
        if (restoreInventory)
        {
            RestoreInventory(snapshot);
        }
        RestoreRenderersAndColliders(entity);

        if (snapshot.rb != null)
        {
            snapshot.rb.bodyType = snapshot.rbBodyType;
            snapshot.rb.simulated = snapshot.rbSimulated;
            snapshot.rb.gravityScale = snapshot.rbGravityScale;
            snapshot.rb.constraints = snapshot.rbConstraints;
            snapshot.rb.linearVelocity = snapshot.rbLinearVelocity;
            snapshot.rb.angularVelocity = snapshot.rbAngularVelocity;
        }

        SetWorldPosition(entity, snapshot.position);
        entity.transform.rotation = snapshot.rotation;

        NotifyTeleported(entity);

        if (snapshot.wasActiveSelf && !entity.activeSelf)
        {
            entity.SetActive(true);
        }

        if (IsDead(entity) || snapshot.wasDead)
        {
            RestoreDeathState(snapshot);
        }
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
            if (!string.IsNullOrEmpty(snapshot.smartNpcCurrentAction))
            {
                smartNpc.currentAction = snapshot.smartNpcCurrentAction;
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
                villager.currentAction = snapshot.villagerCurrentAction;
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
            if (!string.IsNullOrEmpty(snapshot.smartNpcCurrentAction))
            {
                snapshot.smartNpc.currentAction = snapshot.smartNpcCurrentAction;
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
                snapshot.villager.currentAction = snapshot.villagerCurrentAction;
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
                snapshot.monster.currentAction = snapshot.monsterCurrentAction;
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
