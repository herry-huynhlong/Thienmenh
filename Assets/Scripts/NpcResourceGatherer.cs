using UnityEngine;

[RequireComponent(typeof(NpcItemCollector))]
public class NpcResourceGatherer : MonoBehaviour
{
    [Header("Gathering")]
    public bool canGather = false;
    public float scanInterval = 2f;
    public float maxSearchDistance = 12f;
    public float arriveDistance = 0.35f;
    public float retargetDistance = 0.75f;

    [Header("Needs")]
    [Range(0f, 1f)]
    public float gatherChancePerScan = 0.35f;
    public bool gatherOnlyWhenInventoryExists = false;
    public bool useVillagerPreferredZone = true;

    WorldStatItemPickup targetPickup;
    WorldStatItemPickup harvestingPickup;
    float harvestTimer;
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

        if (harvestingPickup != null)
        {
            ContinueHarvest();
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
                GetSearchPosition(),
                null,
                GetPreferredZone());

        if (candidate == null)
        {
            return;
        }

        if (!GetPreferredZone().HasValue)
        {
            float distance =
                Vector2.Distance(transform.position, candidate.transform.position);

            if (distance > maxSearchDistance)
            {
                return;
            }
        }

        targetPickup = candidate;
        MoveToTarget();
    }


    Vector3 GetSearchPosition()
    {
        NpcMapZone? preferredZone = GetPreferredZone();
        if (!preferredZone.HasValue)
        {
            return transform.position;
        }

        NpcMapArea area = NpcMapArea.FindNearestAreaInZone(
            preferredZone.Value,
            transform.position);

        return area != null && area.areaBounds != null
            ? area.areaBounds.bounds.center
            : transform.position;
    }

    NpcMapZone? GetPreferredZone()
    {
        if (!useVillagerPreferredZone || villager == null)
        {
            return null;
        }

        return villager.GetPreferredResourceGatherZone();
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
            StartHarvest();
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

    void StartHarvest()
    {
        if (!IsPickupAvailable(targetPickup))
        {
            targetPickup = null;
            return;
        }

        harvestingPickup = targetPickup;
        harvestTimer = Mathf.Max(0.1f, harvestingPickup.harvestDuration);
        StopNpcMovement();
        SetGatherAction();
    }

    void ContinueHarvest()
    {
        if (!IsPickupAvailable(harvestingPickup))
        {
            harvestingPickup = null;
            targetPickup = null;
            harvestTimer = 0f;
            return;
        }

        float distance =
            Vector2.Distance(transform.position, harvestingPickup.transform.position);

        if (distance > arriveDistance + retargetDistance)
        {
            targetPickup = harvestingPickup;
            harvestingPickup = null;
            harvestTimer = 0f;
            MoveToTarget();
            return;
        }

        StopNpcMovement();
        harvestTimer -= Time.deltaTime;
        SetGatherAction();

        if (harvestTimer > 0f)
        {
            return;
        }

        CompleteHarvest();
    }

    void CompleteHarvest()
    {
        WorldStatItemPickup pickup = harvestingPickup;
        harvestingPickup = null;
        targetPickup = null;
        harvestTimer = 0f;

        if (!IsPickupAvailable(pickup))
        {
            return;
        }

        StatItemData item = pickup.item;
        if (item == null ||
            !pickup.TryTake(1))
        {
            return;
        }

        collector.ReceiveItem(
            item,
            ItemLifecycleEventType.Picked,
            item.ShouldNpcUseDirectly());
    }

    void StopNpcMovement()
    {
        NpcRoleUtility.StopForConversation(gameObject, 0.35f);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void SetGatherAction()
    {
        string itemName = harvestingPickup != null && harvestingPickup.item != null
            ? harvestingPickup.item.itemName
            : "linh d\u01b0\u1ee3c";

        NpcRoleUtility.SetAction(
            gameObject,
            "\u0110ang h\u00e1i " + itemName +
            " (" + Mathf.CeilToInt(Mathf.Max(0f, harvestTimer)) + "s)");
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

        NpcMapZone? preferredZone = GetPreferredZone();
        if (preferredZone.HasValue)
        {
            NpcMapArea pickupArea = NpcMapArea.FindArea(pickup.transform.position);
            return pickupArea != null && pickupArea.zone == preferredZone.Value;
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
