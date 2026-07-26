using System.Collections.Generic;
using UnityEngine;

public partial class FrontierDefenseCoordinator : MonoBehaviour
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
    public bool autoAssignVacantPosts = false;

    [Header("Watch Duty Tuning")]
    public bool relaxFrontierWatchRequirements = true;
    public CultivationRealm relaxedFrontierWatchMinRealm =
        CultivationRealm.QiRefining;
    [Range(1, CultivationProgression.MaxStage)]
    public int relaxedFrontierWatchMinStage = 1;
    [Range(0, 100)]
    public int minimumFrontierWatchBravery = 0;
    public bool urgentVacancyBypassesFatigueAndHunger = true;
    [Min(0f)] public float urgentVacancyAcceptanceBonus = 60f;
    [Min(0f)] public float urgentVacancyCandidateScoreBonus = 120f;

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

    public bool TriggerBeastWaveNow()
    {
        if (activeBeastWave != null)
        {
            TriggerWaveWarningSignals(activeBeastWave);
            AddUrgentLog(
                UiText.Get(
                    "frontierDefense",
                    "manualTriggerAlreadyActive",
                    "Thu trieu dang dien ra."));
            return true;
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
        TryStartBeastWave(true);

        if (activeBeastWave == null)
        {
            AddUrgentLog(
                UiText.Get(
                    "frontierDefense",
                    "manualTriggerNoDefenders",
                    "Khong trieu tap duoc tu si phong thu, nen thu trieu chua the khai hoa."));
        }

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

}
