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
    public float reservationDuration = 3f;

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

        StatItemData desiredItem = GetPickupItem(targetPickup);
        ClearTargetReservation();

        if (FindTarget(desiredItem, true))
        {
            return;
        }

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

        FindTarget(null, false);
    }

    public bool TryStartGatheringNow()
    {
        if (!canGather ||
            collector == null ||
            !collector.canPickupItems)
        {
            return false;
        }

        if (harvestingPickup != null ||
            (targetPickup != null && IsPickupAvailable(targetPickup)))
        {
            MoveToTarget();
            return true;
        }

        FindTarget(null, true);
        return targetPickup != null;
    }

    bool FindTarget(
        StatItemData requiredItem,
        bool force)
    {
        WorldStatItemPickup candidate =
            WorldResourceField.GetNearestAvailablePickupInAllFields(
                GetSearchPosition(),
                requiredItem,
                GetPreferredZone(),
                gameObject);

        if (candidate == null)
        {
            return false;
        }

        if (!force &&
            !GetPreferredZone().HasValue)
        {
            float distance =
                Vector2.Distance(transform.position, candidate.transform.position);

            if (distance > maxSearchDistance)
            {
                return false;
            }
        }

        if (!candidate.TryReserve(gameObject, reservationDuration))
        {
            return false;
        }

        targetPickup = candidate;
        MoveToTarget();
        return true;
    }


    Vector3 GetSearchPosition()
    {
        return transform.position;
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

        targetPickup.RefreshReservation(gameObject, reservationDuration);

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
            villager.ForceGatherTarget(
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
            StatItemData desiredItem = GetPickupItem(targetPickup);
            ClearTargetReservation();

            FindTarget(desiredItem, true);
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
            StatItemData desiredItem = GetPickupItem(harvestingPickup);
            ClearPickupReservation(harvestingPickup);
            harvestingPickup = null;
            targetPickup = null;
            harvestTimer = 0f;
            FindTarget(desiredItem, true);
            return;
        }

        float distance =
            Vector2.Distance(transform.position, harvestingPickup.transform.position);

        if (distance > arriveDistance + retargetDistance)
        {
            harvestingPickup.RefreshReservation(gameObject, reservationDuration);
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
            if (pickup != null)
            {
                pickup.ClearReservation(gameObject);
            }
            return;
        }

        StatItemData item = pickup.item;
        if (item == null ||
            !pickup.TryTake(1))
        {
            pickup.ClearReservation(gameObject);
            FindTarget(item, true);
            return;
        }

        pickup.ClearReservation(gameObject);

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
            ? ItemText.Name(harvestingPickup.item)
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
            pickup.IsReservedByOther(gameObject) ||
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

    StatItemData GetPickupItem(WorldStatItemPickup pickup)
    {
        return pickup != null ? pickup.item : null;
    }

    void ClearTargetReservation()
    {
        ClearPickupReservation(targetPickup);
        targetPickup = null;
    }

    void ClearPickupReservation(WorldStatItemPickup pickup)
    {
        if (pickup != null)
        {
            pickup.ClearReservation(gameObject);
        }
    }
}
