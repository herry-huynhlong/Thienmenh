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
    public float harvestBreakDistance = 2f;
    public float harvestCooldownAfterSuccess = 45f;
    public float harvestCooldownWhenNoTarget = 8f;
    public bool limitHarvestsPerScheduleSlot = true;
    public int maxHarvestsPerScheduleSlot = 1;

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
    float nextGatherAllowedTime;
    string scheduleSessionKey;
    int harvestsThisScheduleSession;

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

        RefreshScheduleSession();

        if (ShouldBlockFarmerWorkGathering())
        {
            ClearActiveGathering();
            nextGatherAllowedTime =
                Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
            return;
        }

        if (IsScheduleHarvestLimitReached())
        {
            ClearActiveGathering();
            return;
        }

        if (harvestingPickup == null &&
            targetPickup == null &&
            !NpcScheduleController.AllowsGather(gameObject))
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

        if (Time.time < nextGatherAllowedTime)
        {
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
            !collector.canPickupItems ||
            !NpcScheduleController.AllowsGather(gameObject))
        {
            return false;
        }

        RefreshScheduleSession();

        if (ShouldBlockFarmerWorkGathering())
        {
            ClearActiveGathering();
            nextGatherAllowedTime =
                Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
            return false;
        }

        if (IsScheduleHarvestLimitReached())
        {
            ClearActiveGathering();
            return false;
        }

        if (Time.time < nextGatherAllowedTime &&
            harvestingPickup == null &&
            targetPickup == null)
        {
            return false;
        }

        if (harvestingPickup != null)
        {
            return true;
        }

        if (targetPickup != null && IsPickupAvailable(targetPickup))
        {
            MoveToTarget();
            return true;
        }

        FindTarget(null, true);
        if (targetPickup != null)
        {
            return true;
        }

        nextGatherAllowedTime =
            Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
        return false;
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
            if (force)
            {
                nextGatherAllowedTime =
                    Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
            }
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
        MarkCurrentGatherSlotStarted();
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
        targetPickup = null;
        harvestTimer = Mathf.Max(0.1f, harvestingPickup.harvestDuration);
        harvestingPickup.RefreshReservation(
            gameObject,
            Mathf.Max(reservationDuration, harvestTimer + 1f));
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

        harvestingPickup.RefreshReservation(
            gameObject,
            Mathf.Max(reservationDuration, harvestTimer + 1f));

        float distance =
            Vector2.Distance(transform.position, harvestingPickup.transform.position);

        if (distance > Mathf.Max(arriveDistance + retargetDistance, harvestBreakDistance))
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

        harvestsThisScheduleSession++;
        MarkCurrentGatherSlotCompleted();
        nextGatherAllowedTime =
            Time.time + Mathf.Max(0f, harvestCooldownAfterSuccess);
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

    void ClearActiveGathering()
    {
        ClearPickupReservation(targetPickup);
        ClearPickupReservation(harvestingPickup);
        targetPickup = null;
        harvestingPickup = null;
        harvestTimer = 0f;
    }

    void ClearPickupReservation(WorldStatItemPickup pickup)
    {
        if (pickup != null)
        {
            pickup.ClearReservation(gameObject);
        }
    }

    bool ShouldBlockFarmerWorkGathering()
    {
        if (villager == null ||
            villager.job != VillagerJob.Farmer)
        {
            return false;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == NpcScheduleActivity.Work)
        {
            return true;
        }

        return villager.currentAction == NpcText.Action("goFarmWork") ||
            villager.currentAction == NpcText.Action("workingFarm") ||
            villager.currentAction == NpcText.Action("farmerWaitHarvest") ||
            villager.currentAction == NpcText.Action("farmerHarvestedToday");
    }

    void RefreshScheduleSession()
    {
        string key = BuildScheduleSessionKey();
        if (scheduleSessionKey == key)
        {
            return;
        }

        scheduleSessionKey = key;
        harvestsThisScheduleSession = 0;
    }

    string BuildScheduleSessionKey()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        int day = timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.FloorToInt(Time.time / 900f);

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule == null ||
            !schedule.enforceSchedule ||
            schedule.CurrentSlot == null)
        {
            return day + ":free";
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        return day + ":" +
            slot.activity + ":" +
            Mathf.RoundToInt(slot.startHour * 100f) + ":" +
            Mathf.RoundToInt(slot.endHour * 100f);
    }

    bool IsScheduleHarvestLimitReached()
    {
        if (!limitHarvestsPerScheduleSlot)
        {
            return false;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        if (activity != NpcScheduleActivity.Gather &&
            activity != NpcScheduleActivity.Work &&
            activity != NpcScheduleActivity.Hunt)
        {
            return false;
        }

        return harvestsThisScheduleSession >=
            Mathf.Max(1, maxHarvestsPerScheduleSlot);
    }

    void MarkCurrentGatherSlotStarted()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        if (activity == NpcScheduleActivity.Gather ||
            activity == NpcScheduleActivity.Work ||
            activity == NpcScheduleActivity.Hunt)
        {
            schedule.MarkCurrentSlotActivityStarted(activity);
        }
    }

    void MarkCurrentGatherSlotCompleted()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);
        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return;
        }

        NpcScheduleActivity activity = schedule.CurrentActivity;
        if (activity == NpcScheduleActivity.Gather ||
            activity == NpcScheduleActivity.Work ||
            activity == NpcScheduleActivity.Hunt)
        {
            schedule.MarkCurrentSlotActivityCompleted(activity);
        }
    }
}
