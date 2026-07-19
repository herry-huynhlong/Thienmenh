using UnityEngine;

[DisallowMultipleComponent]
public class FrontierWatchPost : MonoBehaviour
{
    [Header("Identity")]
    public string postId = "";
    public string displayName = "";
    public string taskName = "Tran thu Ma Thu Son Mach";

    [Header("Provider")]
    public NpcTaskProvider assignedProvider;

    [Header("Positions")]
    public Transform primaryPoint;
    public Transform secondaryPoint;
    public Transform restPoint;
    public Transform campfirePoint;
    public Transform[] patrolPoints;

    [Header("Campfire")]
    public bool buildCampfireOnFirstRest = true;
    public GameObject campfirePrefab;
    public Vector3 campfireSpawnOffset;
    [Min(0f)] public float campfireBlockRadius = 1f;
    [Range(0f, 23.99f)] public float campfireBuildHour = 18f;
    [Min(0.05f)] public float campfireBuildDurationWorldHours = 0.2f;

    [Header("Signal")]
    public Transform signalPoint;
    public GameObject signalPrefab;
    public Vector3 signalSpawnOffset = new Vector3(0f, 1.25f, 0f);
    public bool signalOnBeastWaveWarning = true;
    public bool signalOnWatcherDeath = true;

    [Header("Requirements")]
    public CultivationRealm minimumRealm = CultivationRealm.QiRefining;
    [Range(1, CultivationProgression.MaxStage)]
    public int minimumRealmStage = 1;
    [Min(1f)] public float shiftDurationDays = 3f;
    [Min(0.25f)] public float restDurationWorldHours = 6f;

    [Header("Daily Schedule")]
    [Range(0f, 23.99f)] public float morningPatrolStartHour = 8f;
    [Range(0f, 23.99f)] public float morningPatrolEndHour = 12f;
    [Range(0f, 23.99f)] public float afternoonPatrolStartHour = 15f;
    [Range(0f, 23.99f)] public float afternoonPatrolEndHour = 18f;
    public bool hideDuringNightRest = true;

    [Header("Rewards")]
    public NpcTaskRank rewardRank = NpcTaskRank.Ha;
    [Min(0)] public int rewardSpiritStone = 1800;
    public StatItemData rewardItem;
    [Min(0)] public int rewardItemAmount = 1;
    public bool autoResolveLowGradeReward = true;

    [Header("Runtime")]
    public GameObject currentAssignee;
    public GameObject pendingAssignee;
    public bool assignmentInProgress;

    public bool IsOccupied =>
        currentAssignee != null;

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(postId))
        {
            return postId.Trim();
        }

        return gameObject.name;
    }

    public Vector3 GetPrimaryPosition()
    {
        if (primaryPoint != null)
        {
            return primaryPoint.position;
        }

        if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                {
                    return patrolPoints[i].position;
                }
            }
        }

        return transform.position;
    }

    public Vector3 GetSecondaryPosition()
    {
        if (secondaryPoint != null)
        {
            return secondaryPoint.position;
        }

        if (patrolPoints != null)
        {
            for (int i = patrolPoints.Length - 1; i >= 0; i--)
            {
                if (patrolPoints[i] != null)
                {
                    return patrolPoints[i].position;
                }
            }
        }

        return GetPrimaryPosition();
    }

    public Vector3 GetPatrolPoint(
        int index,
        Vector3 fallbackPosition)
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return fallbackPosition;
        }

        int clampedIndex =
            Mathf.Clamp(index, 0, patrolPoints.Length - 1);
        Transform point = patrolPoints[clampedIndex];
        return point != null
            ? point.position
            : fallbackPosition;
    }

    public Vector3 GetRestPosition(Vector3 fallbackPosition)
    {
        if (restPoint != null)
        {
            return restPoint.position;
        }

        if (secondaryPoint != null)
        {
            return secondaryPoint.position;
        }

        return fallbackPosition;
    }

    public Vector3 GetSignalPosition()
    {
        if (signalPoint != null)
        {
            return signalPoint.position;
        }

        if (campfirePoint != null)
        {
            return campfirePoint.position;
        }

        if (restPoint != null)
        {
            return restPoint.position;
        }

        return GetPrimaryPosition();
    }

    void OnEnable()
    {
        FrontierDefenseCoordinator.RegisterPost(this);
    }

    void OnDisable()
    {
        FrontierDefenseCoordinator.UnregisterPost(this);
    }

    void OnValidate()
    {
        minimumRealmStage =
            Mathf.Clamp(
                minimumRealmStage,
                1,
                CultivationProgression.MaxStage);
        shiftDurationDays = Mathf.Max(1f, shiftDurationDays);
        restDurationWorldHours = Mathf.Max(0.25f, restDurationWorldHours);
        morningPatrolStartHour =
            Mathf.Clamp(morningPatrolStartHour, 0f, 23.99f);
        morningPatrolEndHour =
            Mathf.Clamp(
                morningPatrolEndHour,
                morningPatrolStartHour,
                23.99f);
        afternoonPatrolStartHour =
            Mathf.Clamp(
                afternoonPatrolStartHour,
                morningPatrolEndHour,
                23.99f);
        afternoonPatrolEndHour =
            Mathf.Clamp(
                afternoonPatrolEndHour,
                afternoonPatrolStartHour,
                23.99f);
        campfireBlockRadius = Mathf.Max(0f, campfireBlockRadius);
        campfireBuildHour = Mathf.Clamp(campfireBuildHour, 0f, 23.99f);
        campfireBuildDurationWorldHours =
            Mathf.Max(0.05f, campfireBuildDurationWorldHours);
        rewardSpiritStone = Mathf.Max(0, rewardSpiritStone);
        rewardItemAmount = Mathf.Max(0, rewardItemAmount);
    }
}
