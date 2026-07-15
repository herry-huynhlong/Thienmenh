using System.Collections.Generic;
using UnityEngine;

public class FrontierDefenseCoordinator : MonoBehaviour
{
    class ActiveBeastWave
    {
        public FrontierBattleLine line;
        public readonly List<SmartNpcAI> defenders =
            new List<SmartNpcAI>();
        public readonly List<MonsterAI> monsters =
            new List<MonsterAI>();
        public int totalSpawnedMonsters;
        public float startedAtTime;
        public float combatStartsAtTime;
        public float endAtTime;
        public bool combatStarted;
    }

    class PostDangerState
    {
        public float vacantSinceTime = -1f;
        public int vacancyAlertCount;
    }

    public static FrontierDefenseCoordinator Instance { get; private set; }

    static readonly List<FrontierWatchPost> posts =
        new List<FrontierWatchPost>();
    static readonly List<FrontierBattleLine> battleLines =
        new List<FrontierBattleLine>();
    static StatItemData cachedLowGradeRewardItem;

    [Header("Assignment")]
    public float assignCheckIntervalSeconds = 3f;
    public float retryAssignmentDelaySeconds = 10f;
    public bool autoCreateRuntimeInstance = true;

    [Header("Escalation")]
    [Min(0.5f)] public float vacancyAlertDelayWorldHours = 6f;
    [Min(0.5f)] public float repeatedVacancyAlertWorldHours = 12f;
    [Min(0f)] public float threatIncreaseOnVacancyAlert = 8f;
    [Min(0f)] public float threatIncreaseOnWatcherDeath = 20f;
    [Range(1f, 1000f)] public float maxFrontierThreat = 100f;
    [SerializeField] float currentFrontierThreat;

    [Header("Beast Wave")]
    [Range(1f, 100f)] public float beastWaveTriggerThreatPercent = 60f;
    [Min(0.5f)] public float beastWavePreparationMinWorldHours = 3f;
    [Min(0.5f)] public float beastWavePreparationMaxWorldHours = 5f;
    [Min(1f)] public float beastWaveDurationWorldHours = 18f;
    [Min(1f)] public float beastWaveCooldownWorldHours = 72f;
    [Min(1f)] public float defenderDraftWorldHours = 24f;
    [Min(0f)] public float threatReductionOnVictory = 35f;
    [Min(0f)] public float threatReductionOnFailure = 12f;

    readonly Dictionary<string, float> nextAssignmentTimes =
        new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, PostDangerState> postDangerStates =
        new Dictionary<string, PostDangerState>(System.StringComparer.OrdinalIgnoreCase);
    readonly HashSet<int> alertedWatcherInstanceIds =
        new HashSet<int>();
    float assignTimer;
    ActiveBeastWave activeBeastWave;
    float nextBeastWaveAllowedTime;

    public float CurrentFrontierThreat =>
        currentFrontierThreat;

    public bool IsBeastWaveActive =>
        activeBeastWave != null;

    public static bool IsVillageShelterAlertActive =>
        Instance != null &&
        Instance.activeBeastWave != null;

    public static void RegisterPost(FrontierWatchPost post)
    {
        if (post == null)
        {
            return;
        }

        EnsureInstance();
        if (!posts.Contains(post))
        {
            posts.Add(post);
        }
    }

    public static void UnregisterPost(FrontierWatchPost post)
    {
        if (post == null)
        {
            return;
        }

        posts.Remove(post);
    }

    public static void RegisterBattleLine(FrontierBattleLine line)
    {
        if (line == null)
        {
            return;
        }

        EnsureInstance();
        if (!battleLines.Contains(line))
        {
            battleLines.Add(line);
        }
    }

    public static void UnregisterBattleLine(FrontierBattleLine line)
    {
        if (line == null)
        {
            return;
        }

        battleLines.Remove(line);
    }

    public static bool IsOfferAvailable(string postId)
    {
        FrontierWatchPost post = FindPost(postId);
        return post != null &&
            post.isActiveAndEnabled &&
            !post.IsOccupied &&
            !post.assignmentInProgress;
    }

    public static void AppendVisibleOffersForProvider(
        NpcTaskProvider provider,
        List<NpcTaskOffer> targetOffers)
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        if (coordinator == null ||
            provider == null ||
            targetOffers == null)
        {
            return;
        }

        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null ||
                !post.isActiveAndEnabled ||
                post.IsOccupied ||
                post.assignmentInProgress)
            {
                continue;
            }

            NpcTaskProvider resolvedProvider =
                post.assignedProvider != null
                    ? post.assignedProvider
                    : NpcTaskProvider.FindNearestProvider(
                        post.transform.position);
            if (resolvedProvider != provider)
            {
                continue;
            }

            string postId = GetResolvedPostId(post);
            bool alreadyAdded = false;
            for (int j = 0; j < targetOffers.Count; j++)
            {
                NpcTaskOffer existing = targetOffers[j];
                if (!IsFrontierWatchOffer(existing) ||
                    !string.Equals(
                        existing.customTargetId,
                        postId,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                alreadyAdded = true;
                break;
            }

            if (!alreadyAdded)
            {
                targetOffers.Add(coordinator.BuildOffer(post));
            }
        }
    }

    public static Vector3 GetPrimaryPosition(
        string postId,
        Vector3 fallbackPosition)
    {
        FrontierWatchPost post = FindPost(postId);
        return post != null
            ? post.GetPrimaryPosition()
            : fallbackPosition;
    }

    public static Vector3 GetSecondaryPatrolPosition(
        string postId,
        Vector3 fallbackPosition)
    {
        FrontierWatchPost post = FindPost(postId);
        return post != null
            ? post.GetSecondaryPosition()
            : fallbackPosition;
    }

    public static Vector3 GetNextPatrolTarget(
        string postId,
        ref int patrolIndex,
        ref int patrolDirection,
        Vector3 fallbackPosition)
    {
        FrontierWatchPost post = FindPost(postId);
        if (post == null)
        {
            return fallbackPosition;
        }

        if (post.patrolPoints == null ||
            post.patrolPoints.Length == 0)
        {
            return post.GetPrimaryPosition();
        }

        int clampedDirection = patrolDirection >= 0 ? 1 : -1;
        patrolDirection = clampedDirection;
        patrolIndex =
            Mathf.Clamp(
                patrolIndex,
                0,
                post.patrolPoints.Length - 1);
        return post.GetPatrolPoint(
            patrolIndex,
            fallbackPosition);
    }

    public static void AdvancePatrolIndex(
        string postId,
        ref int patrolIndex,
        ref int patrolDirection)
    {
        FrontierWatchPost post = FindPost(postId);
        if (post == null ||
            post.patrolPoints == null ||
            post.patrolPoints.Length <= 1)
        {
            patrolIndex = 0;
            patrolDirection = 1;
            return;
        }

        patrolIndex += patrolDirection >= 0 ? 1 : -1;
        if (patrolIndex >= post.patrolPoints.Length)
        {
            patrolIndex = post.patrolPoints.Length - 2;
            patrolDirection = -1;
        }
        else if (patrolIndex < 0)
        {
            patrolIndex = 1;
            patrolDirection = 1;
        }
    }

    public static FrontierDefenseCoordinator EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        FrontierDefenseCoordinator existing =
            FindAnyObjectByType<FrontierDefenseCoordinator>(
                FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject created = new GameObject("FrontierDefenseCoordinator");
        Instance = created.AddComponent<FrontierDefenseCoordinator>();
        return Instance;
    }

    public bool TriggerBeastWaveNow()
    {
        if (activeBeastWave != null)
        {
            AddUrgentLog(
                UiText.Get(
                    "frontierDefense",
                    "manualTriggerAlreadyActive",
                    "Thu trieu dang dien ra."));
            return false;
        }

        FrontierBattleLine line = FindBestBattleLine();
        if (line == null)
        {
            AddUrgentLog(
                UiText.Get(
                    "frontierDefense",
                    "manualTriggerMissingLine",
                    "Chua co chien tuyen de kich hoat thu trieu."));
            return false;
        }

        if (CountEligibleWaveMonsters(line) <= 0)
        {
            AddUrgentLog(
                UiText.Get(
                    "frontierDefense",
                    "manualTriggerNoMonsters",
                    "Khong co yeu thu san co trong Ma Thu Son Mach de khai hoa thu trieu."));
            return false;
        }

        nextBeastWaveAllowedTime = 0f;
        TryStartBeastWave();
        return activeBeastWave != null;
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
        NpcTaskProvider.TaskStarted += HandleTaskStarted;
        NpcTaskProvider.TaskFinished += HandleTaskFinished;
    }

    void OnDisable()
    {
        NpcTaskProvider.TaskStarted -= HandleTaskStarted;
        NpcTaskProvider.TaskFinished -= HandleTaskFinished;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        assignTimer += Time.deltaTime;
        if (assignTimer < Mathf.Max(0.5f, assignCheckIntervalSeconds))
        {
            return;
        }

        assignTimer = 0f;
        AssignVacantPosts();
        UpdateDangerStates();
        UpdateBeastWave();
    }

    void AssignVacantPosts()
    {
        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null ||
                !post.isActiveAndEnabled)
            {
                continue;
            }

            if (post.currentAssignee != null &&
                !NpcRoleUtility.IsDead(post.currentAssignee))
            {
                ClearVacancyTracking(GetResolvedPostId(post));
                continue;
            }

            if (post.currentAssignee != null)
            {
                HandleWatcherDeath(post, post.currentAssignee);
            }

            post.currentAssignee = null;

            string postId = GetResolvedPostId(post);
            MarkPostVacant(postId);
            if (nextAssignmentTimes.TryGetValue(postId, out float nextTime) &&
                Time.time < nextTime)
            {
                continue;
            }

            if (post.assignmentInProgress)
            {
                continue;
            }

            TryAssignPost(post);
        }
    }

    void TryAssignPost(FrontierWatchPost post)
    {
        if (post == null)
        {
            return;
        }

        NpcTaskProvider provider =
            post.assignedProvider != null
                ? post.assignedProvider
                : NpcTaskProvider.FindNearestProvider(
                    post.transform.position);
        if (provider == null)
        {
            return;
        }

        SmartNpcAI candidate =
            FindBestCandidate(post);
        if (candidate == null)
        {
            SetRetry(post);
            return;
        }

        NpcTaskOffer offer = BuildOffer(post);

        if (!provider.TryStartPlannedTask(candidate.gameObject, offer))
        {
            SetRetry(post);
        }
    }

    SmartNpcAI FindBestCandidate(FrontierWatchPost post)
    {
        SmartNpcAI[] npcs =
            FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude);
        SmartNpcAI best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < npcs.Length; i++)
        {
            SmartNpcAI npc = npcs[i];
            if (!IsEligibleCandidate(npc, post))
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    npc.transform.position,
                    post.GetPrimaryPosition());
            float score =
                npc.bravery * 0.6f +
                CultivationProgression.GetRealmPower(
                    npc.realm,
                    npc.realmStage) *
                0.02f -
                distance * 0.12f;

            if (score > bestScore)
            {
                bestScore = score;
                best = npc;
            }
        }

        return best;
    }

    bool IsEligibleCandidate(
        SmartNpcAI npc,
        FrontierWatchPost post)
    {
        return npc != null &&
            npc.enabled &&
            !npc.IsDead &&
            npc.canFight &&
            NpcRoleUtility.MeetsRealm(
                npc.gameObject,
                post.minimumRealm,
                post.minimumRealmStage);
    }

    NpcTaskOffer BuildOffer(FrontierWatchPost post)
    {
        StatItemData rewardItem = post.rewardItem;
        if (rewardItem == null &&
            post.autoResolveLowGradeReward)
        {
            rewardItem = ResolveLowGradeRewardItem();
        }

        return new NpcTaskOffer
        {
            taskName = string.IsNullOrWhiteSpace(post.taskName)
                ? "Tran thu Ma Thu Son Mach"
                : post.taskName,
            taskType = NpcTaskType.FrontierWatch,
            audience = NpcTaskAudience.SmartNpcOnly,
            rank = post.rewardRank,
            minRealm = post.minimumRealm,
            minRealmStage = post.minimumRealmStage,
            rewardSpiritStone = Mathf.Max(0, post.rewardSpiritStone),
            rewardItem = rewardItem,
            rewardItemAmount =
                rewardItem != null
                    ? Mathf.Max(1, post.rewardItemAmount)
                    : 0,
            workDurationWorldHours =
                Mathf.Max(24f, post.shiftDurationDays * 24f),
            customTaskId = "frontier_watch",
            customTargetId = GetResolvedPostId(post)
        };
    }

    StatItemData ResolveLowGradeRewardItem()
    {
        if (cachedLowGradeRewardItem != null)
        {
            return cachedLowGradeRewardItem;
        }

        StatItemData[] items =
            Resources.LoadAll<StatItemData>(string.Empty);
        for (int i = 0; i < items.Length; i++)
        {
            StatItemData item = items[i];
            if (item == null ||
                item.grade != ItemGrade.Ha)
            {
                continue;
            }

            if (item.itemType == ItemType.DanDuoc ||
                item.itemType == ItemType.VatLieu)
            {
                cachedLowGradeRewardItem = item;
                return cachedLowGradeRewardItem;
            }
        }

        return null;
    }

    void HandleTaskStarted(
        GameObject npc,
        NpcTaskOffer offer)
    {
        if (!IsFrontierWatchOffer(offer))
        {
            return;
        }

        FrontierWatchPost post =
            FindPost(offer.customTargetId);
        if (post == null)
        {
            return;
        }

        post.currentAssignee = npc;
        post.assignmentInProgress = false;
        string postId = GetResolvedPostId(post);
        nextAssignmentTimes[postId] = 0f;
        ClearVacancyTracking(postId);
    }

    void HandleTaskFinished(
        GameObject npc,
        NpcTaskOffer offer,
        bool completed)
    {
        if (!IsFrontierWatchOffer(offer))
        {
            return;
        }

        FrontierWatchPost post =
            FindPost(offer.customTargetId);
        if (post == null)
        {
            return;
        }

        if (!completed)
        {
            HandleWatcherDeath(post, npc);
        }

        if (post.currentAssignee == npc)
        {
            post.currentAssignee = null;
        }

        post.assignmentInProgress = false;
        MarkPostVacant(GetResolvedPostId(post));
        SetRetry(post);
    }

    void UpdateDangerStates()
    {
        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null ||
                !post.isActiveAndEnabled)
            {
                continue;
            }

            if (post.currentAssignee != null &&
                !NpcRoleUtility.IsDead(post.currentAssignee))
            {
                ClearVacancyTracking(GetResolvedPostId(post));
                continue;
            }

            if (post.currentAssignee != null)
            {
                HandleWatcherDeath(post, post.currentAssignee);
                post.currentAssignee = null;
            }

            string postId = GetResolvedPostId(post);
            PostDangerState state = GetOrCreateDangerState(postId);
            if (state.vacantSinceTime < 0f)
            {
                state.vacantSinceTime = Time.time;
            }

            float firstDelay =
                GameTime.WorldHoursToScaledSeconds(vacancyAlertDelayWorldHours);
            float repeatDelay =
                GameTime.WorldHoursToScaledSeconds(repeatedVacancyAlertWorldHours);
            float elapsed = Mathf.Max(0f, Time.time - state.vacantSinceTime);

            if (elapsed < firstDelay)
            {
                continue;
            }

            int expectedAlertCount = 1;
            if (repeatDelay > 0.01f)
            {
                expectedAlertCount +=
                    Mathf.FloorToInt(
                        Mathf.Max(0f, elapsed - firstDelay) / repeatDelay);
            }

            while (state.vacancyAlertCount < expectedAlertCount)
            {
                state.vacancyAlertCount++;
                RaiseVacancyAlert(post, state.vacancyAlertCount);
            }
        }
    }

    void SetRetry(FrontierWatchPost post)
    {
        if (post == null)
        {
            return;
        }

        nextAssignmentTimes[GetResolvedPostId(post)] =
            Time.time + Mathf.Max(1f, retryAssignmentDelaySeconds);
    }

    void UpdateBeastWave()
    {
        if (activeBeastWave != null)
        {
            UpdateActiveBeastWave();
            return;
        }

        if (Time.time < nextBeastWaveAllowedTime)
        {
            return;
        }

        float triggerThreat =
            Mathf.Clamp(
                beastWaveTriggerThreatPercent,
                1f,
                100f);
        float currentThreatPercent =
            Mathf.Clamp(
                currentFrontierThreat /
                Mathf.Max(1f, maxFrontierThreat) * 100f,
                0f,
                100f);
        if (currentThreatPercent < triggerThreat)
        {
            return;
        }

        TryStartBeastWave();
    }

    void TryStartBeastWave()
    {
        FrontierBattleLine line = FindBestBattleLine();
        if (line == null)
        {
            return;
        }

        ActiveBeastWave wave = new ActiveBeastWave
        {
            line = line,
            startedAtTime = Time.time
        };

        DraftDefenders(wave);
        if (wave.defenders.Count == 0)
        {
            return;
        }

        SpawnWaveMonsters(wave);

        if (wave.monsters.Count == 0)
        {
            ReleaseWaveDefenders(wave);
            return;
        }

        activeBeastWave = wave;
        float preparationHours =
            Random.Range(
                Mathf.Max(0.5f, beastWavePreparationMinWorldHours),
                Mathf.Max(
                    beastWavePreparationMinWorldHours,
                    beastWavePreparationMaxWorldHours));
        float preparationSeconds =
            GameTime.WorldHoursToScaledSeconds(
                preparationHours);
        wave.combatStartsAtTime =
            Time.time + preparationSeconds;
        wave.endAtTime =
            wave.combatStartsAtTime +
            GameTime.WorldHoursToScaledSeconds(
                beastWaveDurationWorldHours);
        nextBeastWaveAllowedTime =
            Time.time +
            GameTime.WorldHoursToScaledSeconds(
                beastWaveCooldownWorldHours);

        AddUrgentLog(
            UiText.Format(
                "frontierDefense",
                "beastWaveWarning",
                line.GetDisplayName(),
                wave.defenders.Count,
                wave.monsters.Count,
                Mathf.CeilToInt(preparationHours)));
    }

    void UpdateActiveBeastWave()
    {
        if (activeBeastWave == null)
        {
            return;
        }

        if (!activeBeastWave.combatStarted)
        {
            if (Time.time < activeBeastWave.combatStartsAtTime)
            {
                return;
            }

            StartBeastWaveCombat(activeBeastWave);
        }

        int aliveMonsters =
            CountAliveMonsters(activeBeastWave.monsters);
        int aliveDefenders =
            CountAliveDefenders(activeBeastWave.defenders);
        int retreatThreshold =
            Mathf.Max(
                0,
                Mathf.FloorToInt(
                    activeBeastWave.totalSpawnedMonsters * 0.5f));

        if (aliveMonsters <= retreatThreshold)
        {
            FinishBeastWave(true, aliveMonsters, aliveDefenders);
            return;
        }

        if (aliveDefenders <= 0 &&
            Time.time >= activeBeastWave.startedAtTime + 5f)
        {
            FinishBeastWave(false, aliveMonsters, aliveDefenders);
            return;
        }

        if (Time.time >= activeBeastWave.endAtTime)
        {
            bool success =
                aliveMonsters <=
                Mathf.CeilToInt(
                    activeBeastWave.totalSpawnedMonsters * 0.6f);
            FinishBeastWave(success, aliveMonsters, aliveDefenders);
        }
    }

    void StartBeastWaveCombat(ActiveBeastWave wave)
    {
        if (wave == null ||
            wave.combatStarted)
        {
            return;
        }

        wave.combatStarted = true;

        for (int i = 0; i < wave.monsters.Count; i++)
        {
            ActivateWaveMonsterCombat(
                wave.monsters[i],
                wave.line,
                i);
        }

        if (WorldEventSystem.Instance != null)
        {
            WorldEventSystem.Instance.TriggerEvent(WorldEventType.BeastWave);
        }

        AddUrgentLog(
            UiText.Format(
                "frontierDefense",
                "beastWaveStarted",
                wave.line != null
                    ? wave.line.GetDisplayName()
                    : "Ma Thu Son Mach",
                wave.defenders.Count,
                wave.monsters.Count));
    }

    void FinishBeastWave(
        bool success,
        int aliveMonsters,
        int aliveDefenders)
    {
        if (activeBeastWave == null)
        {
            return;
        }

        FrontierBattleLine line = activeBeastWave.line;
        if (success)
        {
            currentFrontierThreat =
                Mathf.Max(
                    0f,
                    currentFrontierThreat -
                    Mathf.Max(0f, threatReductionOnVictory));
            AddUrgentLog(
                UiText.Format(
                    "frontierDefense",
                    "beastWaveRepelled",
                    line != null ? line.GetDisplayName() : "Ma Thu Son Mach",
                    aliveDefenders));
        }
        else
        {
            currentFrontierThreat =
                Mathf.Max(
                    0f,
                    currentFrontierThreat -
                    Mathf.Max(0f, threatReductionOnFailure));
            AddUrgentLog(
                UiText.Format(
                    "frontierDefense",
                    "beastWaveBreached",
                    line != null ? line.GetDisplayName() : "Ma Thu Son Mach",
                    aliveMonsters));
        }

        ReleaseWaveDefenders(activeBeastWave);
        activeBeastWave = null;
    }

    FrontierBattleLine FindBestBattleLine()
    {
        FrontierBattleLine best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < battleLines.Count; i++)
        {
            FrontierBattleLine line = battleLines[i];
            if (line == null ||
                !line.isActiveAndEnabled ||
                !line.HasBattleArea)
            {
                continue;
            }

            int score =
                Mathf.Max(1, line.defenderCount) +
                Mathf.Max(1, line.monsterCount);
            if (score > bestScore)
            {
                best = line;
                bestScore = score;
            }
        }

        return best;
    }

    void DraftDefenders(ActiveBeastWave wave)
    {
        if (wave == null ||
            wave.line == null)
        {
            return;
        }

        SmartNpcAI[] npcs =
            FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude);
        System.Array.Sort(
            npcs,
            (left, right) =>
            {
                float rightScore = GetDefenderScore(right, wave.line);
                float leftScore = GetDefenderScore(left, wave.line);
                return rightScore.CompareTo(leftScore);
            });

        int targetCount =
            Mathf.Max(
                1,
                wave.line.defenderCount);
        float defenseDuration =
            GameTime.WorldHoursToScaledSeconds(
                defenderDraftWorldHours);

        for (int i = 0; i < npcs.Length && wave.defenders.Count < targetCount; i++)
        {
            SmartNpcAI npc = npcs[i];
            if (!IsValidWaveDefender(npc))
            {
                continue;
            }

            wave.defenders.Add(npc);
            Vector3 defensePoint =
                wave.line.GetDefenderPoint(
                    wave.defenders.Count - 1);
            npc.EnterFrontierDefenseMode(
                defensePoint,
                defenseDuration,
                "frontier beast wave");
        }
    }

    float GetDefenderScore(
        SmartNpcAI npc,
        FrontierBattleLine line)
    {
        if (!IsValidWaveDefender(npc))
        {
            return float.NegativeInfinity;
        }

        float power =
            CultivationProgression.GetRealmPower(
                npc.realm,
                npc.realmStage);
        float distance =
            Vector2.Distance(
                npc.transform.position,
                line.GetBattleCenter());
        float emergencyBonus =
            npc.IsInFrontierDefenseMode
                ? 1000f
                : 0f;

        return emergencyBonus +
            power * 0.035f +
            npc.bravery * 0.75f -
            distance * 0.15f;
    }

    bool IsValidWaveDefender(SmartNpcAI npc)
    {
        return npc != null &&
            npc.enabled &&
            !npc.IsDead &&
            npc.canFight &&
            NpcRoleUtility.MeetsRealm(
                npc.gameObject,
                CultivationRealm.Foundation,
                1);
    }

    void SpawnWaveMonsters(ActiveBeastWave wave)
    {
        if (wave == null ||
            wave.line == null)
        {
            return;
        }

        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);
        System.Array.Sort(
            monsters,
            (left, right) =>
            {
                float leftDistance =
                    GetBattleMonsterDistanceScore(left, wave.line);
                float rightDistance =
                    GetBattleMonsterDistanceScore(right, wave.line);
                return leftDistance.CompareTo(rightDistance);
            });

        int targetCount =
            Mathf.Max(1, wave.line.monsterCount);
        for (int i = 0; i < monsters.Length && wave.monsters.Count < targetCount; i++)
        {
            MonsterAI monster = monsters[i];
            if (!IsValidWaveMonster(monster, wave.line))
            {
                continue;
            }

            PrepareWaveMonster(
                monster,
                wave.line,
                wave.monsters.Count);
            wave.monsters.Add(monster);
        }

        wave.totalSpawnedMonsters = wave.monsters.Count;
    }

    int CountEligibleWaveMonsters(FrontierBattleLine line)
    {
        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);
        int count = 0;

        for (int i = 0; i < monsters.Length; i++)
        {
            if (IsValidWaveMonster(monsters[i], line))
            {
                count++;
            }
        }

        return count;
    }

    bool IsValidWaveMonster(
        MonsterAI monster,
        FrontierBattleLine line)
    {
        if (monster == null ||
            monster.IsDead ||
            !monster.gameObject.activeInHierarchy)
        {
            return false;
        }

        NpcMapZone expectedZone =
            line != null
                ? line.battleZone
                : NpcMapZone.MaThuSonMach;
        NpcMapZone? actualZone =
            NpcMapNavigator.ResolveActorZone(
                monster.gameObject);
        return actualZone.HasValue &&
            actualZone.Value == expectedZone;
    }

    float GetBattleMonsterDistanceScore(
        MonsterAI monster,
        FrontierBattleLine line)
    {
        if (monster == null ||
            line == null)
        {
            return float.PositiveInfinity;
        }

        return Vector2.Distance(
            monster.transform.position,
            line.GetBattleCenter());
    }

    void PrepareWaveMonster(
        MonsterAI monster,
        FrontierBattleLine line,
        int index)
    {
        if (monster == null)
        {
            return;
        }

        monster.generateFromEntityProfile = true;
        monster.autoStatsFromRealm = true;
        monster.syncBeastLevelFromRealm = true;
        monster.guardTerritory = true;
        monster.roamRadius = Mathf.Max(monster.roamRadius, 6f);
        monster.territoryRadius = Mathf.Max(monster.territoryRadius, 6f);
        monster.returnHomeDistance = Mathf.Max(monster.returnHomeDistance, 10f);
        monster.attackPlayer = false;
        monster.attackVillagers = false;
        monster.attackSmartNpcs = false;
        monster.attackOtherMonsters = false;
        monster.huntTargetType = HuntTargetType.Any;
        if (line != null &&
            line.HasStagingArea)
        {
            monster.transform.position =
                line.GetStagingPoint(
                    index);
        }

        NpcMapNavigator.ReportNpcZone(
            monster.gameObject,
            line != null
                ? line.battleZone
                : NpcMapZone.MaThuSonMach);
    }

    void ActivateWaveMonsterCombat(
        MonsterAI monster,
        FrontierBattleLine line,
        int index)
    {
        if (monster == null ||
            monster.IsDead)
        {
            return;
        }

        if (line != null &&
            line.HasBattleArea)
        {
            monster.transform.position =
                line.GetDistributedBattlePoint(index);
        }

        monster.guardTerritory = false;
        monster.roamRadius = Mathf.Max(monster.roamRadius, 8f);
        monster.territoryRadius = Mathf.Max(monster.territoryRadius, 8f);
        monster.returnHomeDistance = Mathf.Max(monster.returnHomeDistance, 16f);
        monster.attackPlayer = line != null && line.monstersAttackPlayer;
        monster.attackVillagers = true;
        monster.attackSmartNpcs = true;
        monster.attackOtherMonsters = false;
        monster.ApplyTemperamentSurge(
            line != null ? line.monsterAggressionBonus : 35f,
            line != null ? line.monsterAggressionBonus * 0.5f : 15f);
    }

    int CountAliveMonsters(List<MonsterAI> monsters)
    {
        int alive = 0;
        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterAI monster = monsters[i];
            if (monster != null &&
                !monster.IsDead)
            {
                alive++;
            }
        }

        return alive;
    }

    int CountAliveDefenders(List<SmartNpcAI> defenders)
    {
        int alive = 0;
        for (int i = 0; i < defenders.Count; i++)
        {
            SmartNpcAI defender = defenders[i];
            if (defender != null &&
                !defender.IsDead)
            {
                alive++;
            }
        }

        return alive;
    }

    void ReleaseWaveDefenders(ActiveBeastWave wave)
    {
        if (wave == null)
        {
            return;
        }

        for (int i = 0; i < wave.defenders.Count; i++)
        {
            SmartNpcAI defender = wave.defenders[i];
            if (defender != null &&
                !defender.IsDead)
            {
                defender.ExitFrontierDefenseMode();
            }
        }
    }

    void MarkPostVacant(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId))
        {
            return;
        }

        PostDangerState state = GetOrCreateDangerState(postId);
        if (state.vacantSinceTime < 0f)
        {
            state.vacantSinceTime = Time.time;
        }
    }

    void ClearVacancyTracking(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId) ||
            !postDangerStates.TryGetValue(postId, out PostDangerState state))
        {
            return;
        }

        state.vacantSinceTime = -1f;
        state.vacancyAlertCount = 0;
    }

    PostDangerState GetOrCreateDangerState(string postId)
    {
        if (!postDangerStates.TryGetValue(postId, out PostDangerState state) ||
            state == null)
        {
            state = new PostDangerState();
            postDangerStates[postId] = state;
        }

        return state;
    }

    void HandleWatcherDeath(
        FrontierWatchPost post,
        GameObject npc)
    {
        if (post == null ||
            npc == null ||
            !NpcRoleUtility.IsDead(npc))
        {
            return;
        }

        int instanceId = npc.GetInstanceID();
        if (!alertedWatcherInstanceIds.Add(instanceId))
        {
            return;
        }

        float threatPercent =
            IncreaseThreat(threatIncreaseOnWatcherDeath);
        AddUrgentLog(
            UiText.Format(
                "frontierDefense",
                "watcherDeathAlert",
                post.GetDisplayName(),
                threatPercent.ToString("0")));
    }

    void RaiseVacancyAlert(
        FrontierWatchPost post,
        int vacancyStage)
    {
        if (post == null)
        {
            return;
        }

        float threatPercent =
            IncreaseThreat(threatIncreaseOnVacancyAlert);
        AddUrgentLog(
            UiText.Format(
                "frontierDefense",
                vacancyStage > 1
                    ? "vacancyEscalationAlert"
                    : "vacancyAlert",
                post.GetDisplayName(),
                threatPercent.ToString("0")));
    }

    float IncreaseThreat(float amount)
    {
        currentFrontierThreat =
            Mathf.Clamp(
                currentFrontierThreat + Mathf.Max(0f, amount),
                0f,
                Mathf.Max(1f, maxFrontierThreat));

        return Mathf.Clamp(
            currentFrontierThreat /
            Mathf.Max(1f, maxFrontierThreat) * 100f,
            0f,
            100f);
    }

    void AddUrgentLog(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            WorldEventManager.Instance == null)
        {
            return;
        }

        WorldEventManager.Instance.AddLog(content, 2, true);
    }

    static bool IsFrontierWatchOffer(NpcTaskOffer offer)
    {
        return offer != null &&
            offer.taskType == NpcTaskType.FrontierWatch &&
            string.Equals(
                offer.customTaskId,
                "frontier_watch",
                System.StringComparison.OrdinalIgnoreCase);
    }

    static FrontierWatchPost FindPost(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId))
        {
            return null;
        }

        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null)
            {
                continue;
            }

            if (string.Equals(
                    GetResolvedPostId(post),
                    postId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return post;
            }
        }

        return null;
    }

    static string GetResolvedPostId(FrontierWatchPost post)
    {
        if (post == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(post.postId)
            ? post.postId.Trim()
            : post.gameObject.name;
    }
}
