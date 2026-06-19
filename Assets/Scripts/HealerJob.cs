using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class HealerJob : MonoBehaviour
{
    public bool enabledHealerJob = true;
    public Transform healingPoint;
    public float arriveDistance = 0.35f;
    public float healRestDuration = 6f;
    public NpcJobState currentState = NpcJobState.Idle;

    VillagerAI villager;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
    }

    public bool TryRun()
    {
        if (!enabledHealerJob || villager == null)
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
            villager.GoHomeToRest();
            return true;
        }

        Transform target = healingPoint != null
            ? healingPoint
            : (villager.marketPoint != null ? villager.marketPoint : villager.homePoint);

        if (target == null)
        {
            currentState = NpcJobState.Idle;
            villager.currentAction = NpcText.Action("goHeal");
            return true;
        }

        float distance = Vector2.Distance(villager.transform.position, target.position);
        if (distance <= arriveDistance)
        {
            currentState = NpcJobState.Working;
            villager.currentAction = NpcText.Action("healing");
            villager.transform.position = target.position;
            return true;
        }

        currentState = NpcJobState.Moving;
        villager.currentAction = NpcText.Action("goHeal");
        villager.transform.position = Vector3.MoveTowards(
            villager.transform.position,
            target.position,
            Mathf.Max(0.1f, villager.moveSpeed) * Time.deltaTime);
        return true;
    }
}
