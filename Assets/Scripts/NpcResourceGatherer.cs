using UnityEngine;

[RequireComponent(typeof(NpcItemCollector))]
public class NpcResourceGatherer : MonoBehaviour
{
    [Header("Gathering")]
    public bool canGather = true;
    public float scanInterval = 2f;
    public float maxSearchDistance = 12f;
    public float arriveDistance = 0.35f;
    public float retargetDistance = 0.75f;

    [Header("Needs")]
    [Range(0f, 1f)]
    public float gatherChancePerScan = 0.35f;
    public bool gatherOnlyWhenInventoryExists = false;

    WorldStatItemPickup targetPickup;
    NpcMapMover2D mover;
    VillagerAI villager;
    NpcItemCollector collector;
    float scanTimer;

    void Awake()
    {
        mover = GetComponent<NpcMapMover2D>();
        villager = GetComponent<VillagerAI>();
        collector = GetComponent<NpcItemCollector>();
    }

    void Update()
    {
        if (!canGather ||
            collector == null ||
            !collector.canPickupItems)
        {
            return;
        }

        if (gatherOnlyWhenInventoryExists &&
            GetComponent<ItemInventory>() == null)
        {
            return;
        }

        if (targetPickup != null &&
            IsPickupAvailable(targetPickup))
        {
            MoveToTarget();
            return;
        }

        targetPickup = null;
        scanTimer += Time.deltaTime;

        if (scanTimer < scanInterval)
        {
            return;
        }

        scanTimer = 0f;

        if (Random.value > gatherChancePerScan)
        {
            return;
        }

        FindTarget();
    }

    void FindTarget()
    {
        WorldStatItemPickup candidate =
            WorldResourceField.GetNearestAvailablePickupInAllFields(
                transform.position);

        if (candidate == null)
        {
            return;
        }

        float distance =
            Vector2.Distance(transform.position, candidate.transform.position);

        if (distance > maxSearchDistance)
        {
            return;
        }

        targetPickup = candidate;
        MoveToTarget();
    }

    void MoveToTarget()
    {
        if (targetPickup == null)
        {
            return;
        }

        float distance =
            Vector2.Distance(transform.position, targetPickup.transform.position);

        if (distance <= arriveDistance)
        {
            return;
        }

        if (villager != null &&
            villager.enabled)
        {
            villager.ForceTreasureHunt(
                targetPickup.transform,
                targetPickup.item);
            return;
        }

        if (mover != null &&
            mover.enabled)
        {
            mover.SetMoveTarget(
                targetPickup.transform.position,
                "Gather Resource");
        }
    }

    bool IsPickupAvailable(WorldStatItemPickup pickup)
    {
        if (pickup == null ||
            pickup.item == null ||
            pickup.amount <= 0 ||
            !pickup.allowNpcPickup ||
            !pickup.gameObject.activeInHierarchy)
        {
            return false;
        }

        float distance =
            Vector2.Distance(transform.position, pickup.transform.position);

        if (distance > maxSearchDistance + retargetDistance)
        {
            return false;
        }

        return true;
    }
}
