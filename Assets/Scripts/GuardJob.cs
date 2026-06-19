using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class GuardJob : MonoBehaviour
{
    public bool enabledGuardJob = true;
    public Transform patrolPointA;
    public Transform patrolPointB;
    public float arriveDistance = 0.35f;
    public float patrolSpeedMultiplier = 0.9f;
    public float patrolWanderRadius = 3f;
    public NpcJobState currentState = NpcJobState.Idle;

    VillagerAI villager;
    int patrolDirection = 1;
    Vector3 wanderTarget;
    bool hasWanderTarget;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
    }

    public bool TryRun()
    {
        if (!enabledGuardJob || villager == null)
        {
            return false;
        }

        if (villager.IsDead)
        {
            return false;
        }

        if (villager.fatigue >= 85f)
        {
            currentState = NpcJobState.Returning;
            if (villager.homePoint != null)
            {
                villager.GoHomeToRest();
            }
            else
            {
                villager.currentAction = NpcText.Action("idle");
            }
            return true;
        }

        Transform target = GetCurrentPatrolTarget();
        if (target == null)
        {
            currentState = NpcJobState.Moving;
            villager.currentAction = NpcText.Action("goPatrol");
            MoveTowardWanderTarget();
            return true;
        }

        float distance = Vector2.Distance(villager.transform.position, target.position);
        if (distance <= arriveDistance)
        {
            patrolDirection *= -1;
            currentState = NpcJobState.Working;
            villager.currentAction = NpcText.Action("patrolling");
            if (patrolPointA == null || patrolPointB == null)
            {
                hasWanderTarget = false;
            }
            return true;
        }

        currentState = NpcJobState.Moving;
        villager.currentAction = NpcText.Action("goPatrol");
        villager.transform.position = Vector3.MoveTowards(
            villager.transform.position,
            target.position,
            GetMoveSpeed() * Time.deltaTime);
        return true;
    }

    Transform GetCurrentPatrolTarget()
    {
        if (patrolPointA == null && patrolPointB == null)
        {
            return null;
        }

        if (patrolPointA != null && patrolPointB == null)
        {
            return patrolPointA;
        }

        if (patrolPointA == null && patrolPointB != null)
        {
            return patrolPointB;
        }

        return patrolDirection > 0 ? patrolPointA : patrolPointB;
    }

    void MoveTowardWanderTarget()
    {
        if (!hasWanderTarget ||
            Vector2.Distance(villager.transform.position, wanderTarget) <= arriveDistance)
        {
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0.5f, patrolWanderRadius);
            wanderTarget = villager.transform.position + new Vector3(offset.x, offset.y, 0f);
            hasWanderTarget = true;
        }

        villager.transform.position = Vector3.MoveTowards(
            villager.transform.position,
            wanderTarget,
            GetMoveSpeed() * Time.deltaTime);
    }

    float GetMoveSpeed()
    {
        return Mathf.Max(0.1f, villager.moveSpeed * patrolSpeedMultiplier);
    }
}
