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
    public Transform[] patrolPoints;

    [Header("Requirements")]
    public CultivationRealm minimumRealm = CultivationRealm.Foundation;
    [Range(1, CultivationProgression.MaxStage)]
    public int minimumRealmStage = 1;
    [Min(1f)] public float shiftDurationDays = 3f;
    [Min(0.25f)] public float restDurationWorldHours = 6f;

    [Header("Rewards")]
    public NpcTaskRank rewardRank = NpcTaskRank.Thuong;
    [Min(0)] public int rewardSpiritStone = 1800;
    public StatItemData rewardItem;
    [Min(0)] public int rewardItemAmount = 1;
    public bool autoResolveLowGradeReward = true;

    [Header("Runtime")]
    public GameObject currentAssignee;
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
        rewardSpiritStone = Mathf.Max(0, rewardSpiritStone);
        rewardItemAmount = Mathf.Max(0, rewardItemAmount);
    }
}
