using UnityEngine;

[DisallowMultipleComponent]
public class FrontierWatchDutyAgent : MonoBehaviour
{
    enum DutyStage
    {
        TravelToPatrol,
        TravelToRest,
        Resting,
        TravelToCampfire,
        BuildingCampfire
    }

    enum DutyTimePhase
    {
        MorningPatrol,
        MiddayRest,
        AfternoonPatrol,
        NightRest
    }

    [SerializeField] string activePostId = string.Empty;
    [SerializeField] DutyStage stage = DutyStage.TravelToPatrol;
    [SerializeField] int patrolIndex;
    [SerializeField] int patrolDirection = 1;
    [SerializeField] float remainingDutyTime;
    [SerializeField] float remainingRestTime;
    [SerializeField] float holdUntilTime;
    [SerializeField] float nextAmbientSpeechTime;
    [SerializeField] Vector3 currentTarget;
    [SerializeField] bool initialized;
    [SerializeField] bool campfireRequestedForDuty;
    [SerializeField] bool campfireBuildStartedForDuty;
    [SerializeField] int lastCampfireBuildDay = int.MinValue;
    [SerializeField] float campfireBuildHour = 18f;
    [SerializeField] float campfireBuildDurationSeconds = 3f;
    [SerializeField] float remainingCampfireBuildTime;
    [SerializeField] Vector3 campfireBuildPosition;
    [SerializeField] Vector3 campfireSpawnOffset;
    [SerializeField] float campfireBlockRadius;
    [SerializeField] DutyStage resumeStageAfterCampfire = DutyStage.TravelToRest;
    [SerializeField] Vector3 resumeTargetAfterCampfire;
    [SerializeField] float morningPatrolStartHour = 8f;
    [SerializeField] float morningPatrolEndHour = 12f;
    [SerializeField] float afternoonPatrolStartHour = 15f;
    [SerializeField] float afternoonPatrolEndHour = 18f;
    [SerializeField] bool hideDuringNightRest = true;
    [SerializeField] bool hiddenAtRest;
    GameObject campfirePrefab;
    VillagerAI villager;
    Renderer[] cachedRenderers;
    Collider2D[] cachedColliders;

    public bool IsResting =>
        initialized &&
        stage == DutyStage.Resting;

    public Vector3 CurrentTarget =>
        currentTarget;

    internal void BeginDuty(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null)
        {
            return;
        }

        string postId = task.offer.customTargetId;
        if (initialized &&
            string.Equals(
                activePostId,
                postId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            SyncTask(task);
            return;
        }

        activePostId = string.IsNullOrWhiteSpace(postId)
            ? string.Empty
            : postId.Trim();
        patrolIndex = Mathf.Max(0, task.frontierPatrolIndex);
        patrolDirection = task.frontierPatrolDirection >= 0 ? 1 : -1;
        remainingDutyTime = Mathf.Max(1f, task.remainingTime);
        remainingRestTime = 0f;
        nextAmbientSpeechTime =
            Time.time + Random.Range(6f, 12f);
        stage = DutyStage.TravelToPatrol;
        initialized = true;
        villager = GetComponent<VillagerAI>();
        CacheVisibilityTargets();
        SetHiddenAtRest(false);

        FrontierDefenseCoordinator.GetDailyPatrolSchedule(
            activePostId,
            out morningPatrolStartHour,
            out morningPatrolEndHour,
            out afternoonPatrolStartHour,
            out afternoonPatrolEndHour,
            out hideDuringNightRest);

        campfireRequestedForDuty =
            FrontierDefenseCoordinator.TryGetCampfireSetup(
                activePostId,
                out campfirePrefab,
                out campfireBuildPosition,
                out campfireSpawnOffset,
                out campfireBlockRadius,
                out campfireBuildHour,
                out float campfireBuildDurationWorldHours);
        campfireBuildStartedForDuty = false;
        remainingCampfireBuildTime = 0f;
        campfireBuildDurationSeconds =
            GameTime.WorldHoursToScaledSeconds(
                campfireBuildDurationWorldHours);
        resumeStageAfterCampfire = DutyStage.TravelToRest;
        resumeTargetAfterCampfire = Vector3.zero;

        currentTarget =
            FrontierDefenseCoordinator.GetNextPatrolTarget(
                activePostId,
                ref patrolIndex,
                ref patrolDirection,
                transform.position);

        SyncTask(task);
    }

    internal bool TickDuty(
        NpcTaskProvider provider,
        RunningNpcTask task,
        float arriveDistance,
        float deltaTime)
    {
        if (provider == null ||
            task == null ||
            task.npc == null ||
            task.offer == null)
        {
            return true;
        }

        if (!initialized)
        {
            BeginDuty(task);
        }

        if (Time.time < holdUntilTime)
        {
            NpcRoleUtility.StopForConversation(task.npc, 0.35f);
            SyncTask(task);
            return false;
        }

        remainingDutyTime -= Mathf.Max(0f, deltaTime);
        if (remainingDutyTime <= 0f)
        {
            remainingDutyTime = 0f;
            SetHiddenAtRest(false);
            SyncTask(task);
            return true;
        }

        if (stage == DutyStage.TravelToCampfire)
        {
            SetHiddenAtRest(false);
            UpdateTravelToCampfire(provider, task, arriveDistance);
        }
        else if (stage == DutyStage.BuildingCampfire)
        {
            SetHiddenAtRest(false);
            UpdateBuildCampfire(task, deltaTime);
        }
        else
        {
            switch (ResolveCurrentPhase())
            {
                case DutyTimePhase.MorningPatrol:
                case DutyTimePhase.AfternoonPatrol:
                    UpdatePatrolWindow(provider, task, arriveDistance);
                    break;

                case DutyTimePhase.MiddayRest:
                    UpdateRestWindow(
                        provider,
                        task,
                        arriveDistance,
                        false);
                    break;

                default:
                    UpdateRestWindow(
                        provider,
                        task,
                        arriveDistance,
                        hideDuringNightRest);
                    break;
            }
        }

        TryShowAmbientSpeech(task);
        SyncTask(task);
        return false;
    }

    internal void StopDuty()
    {
        SetHiddenAtRest(false);
        initialized = false;
        activePostId = string.Empty;
        remainingDutyTime = 0f;
        remainingRestTime = 0f;
        holdUntilTime = 0f;
        nextAmbientSpeechTime = 0f;
        currentTarget = Vector3.zero;
        patrolIndex = 0;
        patrolDirection = 1;
        stage = DutyStage.TravelToPatrol;
        campfireRequestedForDuty = false;
        campfireBuildStartedForDuty = false;
        lastCampfireBuildDay = int.MinValue;
        remainingCampfireBuildTime = 0f;
        resumeStageAfterCampfire = DutyStage.TravelToRest;
        resumeTargetAfterCampfire = Vector3.zero;
        campfirePrefab = null;
        campfireBuildPosition = Vector3.zero;
        campfireSpawnOffset = Vector3.zero;
        campfireBlockRadius = 0f;
        campfireBuildHour = 18f;
        campfireBuildDurationSeconds = 3f;
    }

    internal void HoldPosition(float durationSeconds)
    {
        holdUntilTime = Mathf.Max(
            holdUntilTime,
            Time.time + Mathf.Max(0.1f, durationSeconds));
    }

    DutyTimePhase ResolveCurrentPhase()
    {
        float hour = WorldTimeSystem.Instance != null
            ? Mathf.Repeat(WorldTimeSystem.Instance.CurrentHour, 24f)
            : 12f;

        if (hour >= morningPatrolStartHour &&
            hour < morningPatrolEndHour)
        {
            return DutyTimePhase.MorningPatrol;
        }

        if (hour >= afternoonPatrolStartHour &&
            hour < afternoonPatrolEndHour)
        {
            return DutyTimePhase.AfternoonPatrol;
        }

        if (hour >= morningPatrolEndHour &&
            hour < afternoonPatrolStartHour)
        {
            return DutyTimePhase.MiddayRest;
        }

        return DutyTimePhase.NightRest;
    }

    void UpdatePatrolWindow(
        NpcTaskProvider provider,
        RunningNpcTask task,
        float arriveDistance)
    {
        SetHiddenAtRest(false);
        stage = DutyStage.TravelToPatrol;
        currentTarget =
            FrontierDefenseCoordinator.GetNextPatrolTarget(
                activePostId,
                ref patrolIndex,
                ref patrolDirection,
                transform.position);

        provider.MoveNpcToWork(task, currentTarget);

        if (Vector2.Distance(
                transform.position,
                currentTarget) > arriveDistance)
        {
            return;
        }

        FrontierDefenseCoordinator.AdvancePatrolIndex(
            activePostId,
            ref patrolIndex,
            ref patrolDirection);
        currentTarget =
            FrontierDefenseCoordinator.GetNextPatrolTarget(
                activePostId,
                ref patrolIndex,
                ref patrolDirection,
                transform.position);
        HoldPosition(0.35f);
    }

    void UpdateRestWindow(
        NpcTaskProvider provider,
        RunningNpcTask task,
        float arriveDistance,
        bool hideWhenResting)
    {
        Vector3 restTarget = hideWhenResting
            ? FrontierDefenseCoordinator.GetNightRestPosition(
                activePostId,
                transform.position)
            : FrontierDefenseCoordinator.GetRestPosition(
                activePostId,
                transform.position);

        if (hideWhenResting &&
            TryScheduleCampfireBuild(restTarget))
        {
            return;
        }

        currentTarget = restTarget;
        float distanceToRest =
            Vector2.Distance(
                transform.position,
                currentTarget);

        if (distanceToRest > arriveDistance)
        {
            SetHiddenAtRest(false);
            stage = DutyStage.TravelToRest;
            provider.MoveNpcToWork(task, currentTarget);
            return;
        }

        stage = DutyStage.Resting;
        if (task != null &&
            task.npc != null)
        {
            NpcRoleUtility.StopForConversation(task.npc, 0.35f);
        }

        if (hideWhenResting)
        {
            SetHiddenAtRest(true);
        }
    }

    void SyncTask(RunningNpcTask task)
    {
        if (task == null)
        {
            return;
        }

        task.remainingTime = remainingDutyTime;
        task.workPosition = currentTarget;
        task.frontierPatrolIndex = patrolIndex;
        task.frontierPatrolDirection = patrolDirection;
    }

    void TryShowAmbientSpeech(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            hiddenAtRest ||
            Time.time < nextAmbientSpeechTime)
        {
            return;
        }

        string category =
            stage == DutyStage.Resting
                ? "frontier_watch_rest_self"
                : "frontier_watch_patrol_self";

        bool shown =
            NpcSpeechController.TryShowSpeech(
                task.npc,
                null,
                category);

        nextAmbientSpeechTime =
            Time.time + (shown
                ? Random.Range(18f, 30f)
                : Random.Range(6f, 10f));
    }

    bool TryScheduleCampfireBuild(Vector3 restTarget)
    {
        if (!campfireRequestedForDuty ||
            campfireBuildPosition == Vector3.zero ||
            campfireBuildStartedForDuty ||
            stage == DutyStage.TravelToCampfire ||
            stage == DutyStage.BuildingCampfire)
        {
            return false;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return false;
        }

        int currentDay = timeSystem.CurrentDay;
        if (currentDay == lastCampfireBuildDay ||
            timeSystem.CurrentHour < campfireBuildHour)
        {
            return false;
        }

        if (WorldCampfire.IsCampfireNear(
                campfireBuildPosition + campfireSpawnOffset,
                Mathf.Max(0f, campfireBlockRadius)))
        {
            lastCampfireBuildDay = currentDay;
            campfireBuildStartedForDuty = true;
            return false;
        }

        resumeStageAfterCampfire = DutyStage.TravelToRest;
        resumeTargetAfterCampfire = restTarget;
        currentTarget = campfireBuildPosition;
        stage = DutyStage.TravelToCampfire;
        return true;
    }

    void UpdateTravelToCampfire(
        NpcTaskProvider provider,
        RunningNpcTask task,
        float arriveDistance)
    {
        provider.MoveNpcToWork(task, currentTarget);
        NpcRoleUtility.SetAction(task.npc, "Go build campfire");

        if (Vector2.Distance(transform.position, currentTarget) > arriveDistance)
        {
            return;
        }

        remainingCampfireBuildTime =
            Mathf.Max(0.25f, campfireBuildDurationSeconds);
        stage = DutyStage.BuildingCampfire;
    }

    void UpdateBuildCampfire(RunningNpcTask task, float deltaTime)
    {
        NpcRoleUtility.StopForConversation(task.npc, 0.35f);
        NpcRoleUtility.SetAction(task.npc, "Build campfire");

        remainingCampfireBuildTime -= Mathf.Max(0f, deltaTime);
        if (remainingCampfireBuildTime > 0f)
        {
            return;
        }

        TrySpawnCampfire();
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            lastCampfireBuildDay = timeSystem.CurrentDay;
        }

        campfireBuildStartedForDuty = true;
        remainingCampfireBuildTime = 0f;
        currentTarget = resumeTargetAfterCampfire;
        stage = resumeStageAfterCampfire;
    }

    void TrySpawnCampfire()
    {
        if (campfirePrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = campfireBuildPosition + campfireSpawnOffset;
        if (WorldCampfire.IsCampfireNear(
                spawnPosition,
                Mathf.Max(0f, campfireBlockRadius)))
        {
            return;
        }

        GameObject spawned =
            Instantiate(
                campfirePrefab,
                spawnPosition,
                Quaternion.identity);

        WorldCampfire campfire = spawned.GetComponent<WorldCampfire>();
        if (campfire != null)
        {
            campfire.BeginNow();
        }
    }

    void CacheVisibilityTargets()
    {
        if (cachedRenderers == null ||
            cachedRenderers.Length == 0)
        {
            cachedRenderers =
                GetComponentsInChildren<Renderer>(true);
        }

        if (cachedColliders == null ||
            cachedColliders.Length == 0)
        {
            cachedColliders =
                GetComponentsInChildren<Collider2D>(true);
        }
    }

    void SetHiddenAtRest(bool hidden)
    {
        if (hiddenAtRest == hidden)
        {
            return;
        }

        hiddenAtRest = hidden;

        if (villager != null)
        {
            villager.ForceHiddenAtHome(hidden);
            return;
        }

        CacheVisibilityTargets();

        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer renderer = cachedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = !hidden;
            }
        }

        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                Collider2D collider = cachedColliders[i];
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = !hidden;
            }
        }
    }
}
