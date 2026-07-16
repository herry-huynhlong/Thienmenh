using UnityEngine;

[DisallowMultipleComponent]
public class FrontierWatchDutyAgent : MonoBehaviour
{
    enum DutyStage
    {
        TravelToPatrol,
        TravelToRest,
        Resting
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
            SyncTask(task);
            return true;
        }

        switch (stage)
        {
            case DutyStage.TravelToPatrol:
                UpdateTravelToPatrol(provider, task, arriveDistance);
                break;

            case DutyStage.TravelToRest:
                UpdateTravelToRest(provider, task, arriveDistance);
                break;

            case DutyStage.Resting:
                UpdateRest(task, deltaTime);
                break;
        }

        TryShowAmbientSpeech(task);
        SyncTask(task);
        return false;
    }

    internal void StopDuty()
    {
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
    }

    internal void HoldPosition(float durationSeconds)
    {
        holdUntilTime = Mathf.Max(
            holdUntilTime,
            Time.time + Mathf.Max(0.1f, durationSeconds));
    }

    void UpdateTravelToPatrol(
        NpcTaskProvider provider,
        RunningNpcTask task,
        float arriveDistance)
    {
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

        Vector3 restTarget =
            FrontierDefenseCoordinator.GetRestPosition(
                activePostId,
                currentTarget);

        if (Vector2.Distance(
                transform.position,
                restTarget) <= arriveDistance)
        {
            BeginRest(restTarget);
            return;
        }

        currentTarget = restTarget;
        stage = DutyStage.TravelToRest;
    }

    void UpdateTravelToRest(
        NpcTaskProvider provider,
        RunningNpcTask task,
        float arriveDistance)
    {
        provider.MoveNpcToWork(task, currentTarget);

        if (Vector2.Distance(
                transform.position,
                currentTarget) <= arriveDistance)
        {
            BeginRest(currentTarget);
        }
    }

    void UpdateRest(RunningNpcTask task, float deltaTime)
    {
        NpcRoleUtility.StopForConversation(task.npc, 0.35f);
        remainingRestTime -= Mathf.Max(0f, deltaTime);

        if (remainingRestTime > 0f)
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
        stage = DutyStage.TravelToPatrol;
    }

    void BeginRest(Vector3 restTarget)
    {
        currentTarget = restTarget;
        remainingRestTime =
            FrontierDefenseCoordinator.GetRestDurationSeconds(
                activePostId);
        stage = DutyStage.Resting;
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
}
