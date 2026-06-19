using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class BuyerJob : MonoBehaviour
{
    public bool enabledBuyerJob = true;
    public Transform buyingPoint;
    public float arriveDistance = 1.2f;
    public NpcJobState currentState = NpcJobState.Idle;

    VillagerAI villager;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
    }

    public bool TryRun()
    {
        if (!enabledBuyerJob || villager == null)
        {
            return false;
        }

        currentState = NpcJobState.Moving;
        villager.currentAction = NpcText.Action("goBuyGoods");
        bool arrived = buyingPoint != null &&
            Vector2.Distance(transform.position, buyingPoint.position) <= arriveDistance;

        if (arrived)
        {
            currentState = NpcJobState.Trading;
        }

        return arrived;
    }
}
