using UnityEngine;

[RequireComponent(typeof(NpcItemCollector))]
public class NpcResourceGatherer : MonoBehaviour
{
    [Header("Gathering")]
    public bool canGather = false;
    public float scanInterval = 2f;
    public float maxSearchDistance = 12f;
    public float arriveDistance = 0.28f;
    public float retargetDistance = 0.6f;
    public float reservationDuration = 3f;
    public float harvestBreakDistance = 2f;
    public float harvestCooldownAfterSuccess = 45f;
    public float harvestCooldownWhenNoTarget = 8f;
    public bool limitHarvestsPerScheduleSlot = false;
    public int maxHarvestsPerScheduleSlot = 0;

    [Header("Needs")]
    [Range(0f, 1f)]
    public float gatherChancePerScan = 0.35f;
    public bool gatherOnlyWhenInventoryExists = false;
    public bool useVillagerPreferredZone = true;

    WorldStatItemPickup targetPickup;
    WorldStatItemPickup harvestingPickup;
    float harvestTimer;
    int lastHarvestActionSeconds = -1;
    NpcMapMover2D mover;
    VillagerAI villager;
    SmartNpcAI smartNpc;
    NpcItemCollector collector;
    ItemInventory inventory;
    float scanTimer;
    float nextGatherAllowedTime;
    float nextInventoryLookupTime;
    string scheduleSessionKey;
    int harvestsThisScheduleSession;
    StatItemData scheduledRequiredItem;
    float lastApproachLogTime = float.NegativeInfinity;
    float lastStartHarvestLogTime = float.NegativeInfinity;
    float lastContinueHarvestLogTime = float.NegativeInfinity;
    float lastCompleteHarvestLogTime = float.NegativeInfinity;
    WorldStatItemPickup stalledPickup;
    float stalledPickupBlockedUntil;

    void LogScheduledGatherDebug(string stage, string detail)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (villager == null || !villager.debugWorkLogs)
        {
            return;
        }

        Debug.LogWarning(
            "[NpcResourceGatherer] " +
            stage +
            " -> " +
            gameObject.name +
            " action=" +
            (villager != null ? villager.currentAction : "null") +
            " job=" +
            (villager != null ? villager.job.ToString() : "None") +
            " targetPickup=" +
            (targetPickup != null && targetPickup.item != null
                ? ItemText.Name(targetPickup.item) + "@" + targetPickup.transform.position
                : "null") +
            " harvestingPickup=" +
            (harvestingPickup != null && harvestingPickup.item != null
                ? ItemText.Name(harvestingPickup.item) + "@" + harvestingPickup.transform.position
                : "null") +
            " detail=" +
            detail +
            " hour=" +
            (WorldTimeSystem.Instance != null
                ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                : "null"));
#endif
    }

    float GetApproachDistance()
    {
        return Mathf.Max(0.22f, arriveDistance);
    }

    float GetPickupSurfaceDistance(WorldStatItemPickup pickup)
    {
        if (pickup == null)
        {
            return float.MaxValue;
        }

        Collider2D pickupCollider =
            pickup.GetComponent<Collider2D>();
        if (pickupCollider != null)
        {
            Vector2 closest =
                pickupCollider.ClosestPoint(transform.position);
            return Vector2.Distance(transform.position, closest);
        }

        return Vector2.Distance(
            transform.position,
            pickup.transform.position);
    }

    bool HasInventory()
    {
        if (inventory != null)
        {
            return true;
        }

        if (Time.time < nextInventoryLookupTime)
        {
            return false;
        }

        nextInventoryLookupTime =
            Time.time + 1f;
        inventory = GetComponent<ItemInventory>();
        return inventory != null;
    }

    public bool HasActiveGatheringFlow =>
        targetPickup != null ||
        harvestingPickup != null;

    bool ShouldPauseForSmartNpcDamageRecovery()
    {
        if (smartNpc == null)
        {
            smartNpc = GetComponent<SmartNpcAI>();
        }

        return smartNpc != null &&
            smartNpc.enabled &&
            smartNpc.IsRecoveringFromDamage;
    }

    public void SuppressGatheringForSeconds(float durationSeconds)
    {
        ClearActiveGathering();
        nextGatherAllowedTime = Mathf.Max(
            nextGatherAllowedTime,
            Time.time + Mathf.Max(0f, durationSeconds));
    }

    void Awake()
    {
        mover = GetComponent<NpcMapMover2D>();
        villager = GetComponent<VillagerAI>();
        smartNpc = GetComponent<SmartNpcAI>();
        collector = GetComponent<NpcItemCollector>();
        inventory = GetComponent<ItemInventory>();
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
            !HasInventory())
        {
            return;
        }

        if (ShouldPauseForSmartNpcDamageRecovery())
        {
            ClearActiveGathering();
            return;
        }

        RefreshScheduleSession();

        if (ShouldCancelSmartNpcGatheringForSchedule())
        {
            ClearActiveGathering();
            nextGatherAllowedTime =
                Mathf.Max(
                    nextGatherAllowedTime,
                    Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget));
            return;
        }

        if (ShouldCancelSmartNpcGatheringForCombat())
        {
            ClearActiveGathering();
            nextGatherAllowedTime =
                Mathf.Max(
                    nextGatherAllowedTime,
                    Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget));
            return;
        }

        if (ShouldBlockScheduledWorkGathering())
        {
            CancelScheduledWorkGathering();
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
        if (desiredItem == null && IsScheduledHarvestJobActive())
        {
            desiredItem = scheduledRequiredItem;
        }

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

        if (IsScheduledHarvestJobActive())
        {
            if (scheduledRequiredItem != null)
            {
                FindTarget(scheduledRequiredItem, true);
            }
            return;
        }

        FindTarget(null, false);
    }

    public bool TryStartGatheringItemNow(StatItemData requiredItem)
    {
        return TryStartGatheringItemNowInternal(requiredItem, false);
    }

    public bool TryStartScheduledHarvestItemNow(StatItemData requiredItem)
    {
        return TryStartGatheringItemNowInternal(requiredItem, true);
    }

    bool TryStartGatheringItemNowInternal(
        StatItemData requiredItem,
        bool allowScheduledWorkHarvest)
    {
        if (allowScheduledWorkHarvest)
        {
            scheduledRequiredItem = requiredItem;
        }

        if (requiredItem == null)
        {
            return allowScheduledWorkHarvest
                ? TryStartGatheringNowInternal(true)
                : TryStartGatheringNow();
        }

        if (!canGather ||
            collector == null ||
            !collector.canPickupItems ||
            (!allowScheduledWorkHarvest &&
            !NpcScheduleController.AllowsGather(gameObject)))
        {
            LogScheduledGatherDebug(
                "Reject",
                "reason=precheck canGather=" + canGather +
                " collector=" + (collector != null) +
                " canPickup=" + (collector != null && collector.canPickupItems) +
                " scheduled=" + allowScheduledWorkHarvest);
            return false;
        }

        RefreshScheduleSession();

        if (ShouldPauseForSmartNpcDamageRecovery())
        {
            LogScheduledGatherDebug("Reject", "reason=damageRecovery");
            return false;
        }

        if (!allowScheduledWorkHarvest &&
            ShouldBlockScheduledWorkGathering())
        {
            CancelScheduledWorkGathering();
            nextGatherAllowedTime =
                Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
            return false;
        }

        if (IsScheduleHarvestLimitReached())
        {
            ClearActiveGathering();
            LogScheduledGatherDebug("Reject", "reason=scheduleLimit");
            return false;
        }

        if (harvestingPickup != null)
        {
            if (MatchesRequiredItem(harvestingPickup, requiredItem))
            {
                LogScheduledGatherDebug(
                    "ReuseHarvest",
                    "required=" + ItemText.Name(requiredItem));
                return true;
            }

            ClearActiveGathering();
            LogScheduledGatherDebug(
                "ClearHarvest",
                "reason=requiredMismatch required=" + ItemText.Name(requiredItem));
        }

        if (targetPickup != null)
        {
            if (MatchesRequiredItem(targetPickup, requiredItem) &&
                IsPickupAvailable(targetPickup))
            {
                MoveToTarget();
                LogScheduledGatherDebug(
                    "ReuseTarget",
                    "required=" + ItemText.Name(requiredItem));
                return true;
            }

            ClearTargetReservation();
            LogScheduledGatherDebug(
                "ClearTarget",
                "reason=invalidOrMismatch required=" + ItemText.Name(requiredItem));
        }

        FindTarget(requiredItem, true);
        if (targetPickup != null)
        {
            LogScheduledGatherDebug(
                "FoundTarget",
                "required=" + ItemText.Name(requiredItem));
            return true;
        }

        nextGatherAllowedTime =
            Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
        LogScheduledGatherDebug(
            "NoTarget",
            "required=" + ItemText.Name(requiredItem));
        return false;
    }

    public bool TryStartGatheringNow()
    {
        return TryStartGatheringNowInternal(false);
    }

    public bool TryHandleSmartNpcBlockedArrival(float extraDistance = 0.2f)
    {
        if (!canGather ||
            collector == null ||
            !collector.canPickupItems)
        {
            return false;
        }

        if (harvestingPickup != null)
        {
            StopNpcMovement();
            SetGatherAction();
            return true;
        }

        if (targetPickup == null ||
            !IsPickupAvailable(targetPickup))
        {
            return false;
        }

        float allowedDistance =
            Mathf.Max(0.01f, GetApproachDistance()) +
            Mathf.Max(0f, extraDistance);
        if (Vector2.Distance(
                transform.position,
                targetPickup.transform.position) > allowedDistance)
        {
            return false;
        }

        StartHarvest();
        return harvestingPickup != null;
    }

    public bool TryRecoverSmartNpcStalledGather(Transform expectedTarget)
    {
        if (smartNpc == null)
        {
            smartNpc = GetComponent<SmartNpcAI>();
        }

        WorldStatItemPickup stalled = targetPickup != null
            ? targetPickup
            : harvestingPickup;
        if (stalled == null ||
            (expectedTarget != null && stalled.transform != expectedTarget))
        {
            if (smartNpc != null && expectedTarget != null)
            {
                smartNpc.ReleaseGatherTarget(expectedTarget);
            }
            return false;
        }

        StatItemData desiredItem = stalled.item;
        stalledPickup = stalled;
        stalledPickupBlockedUntil =
            Time.time + Mathf.Max(3f, harvestCooldownWhenNoTarget);
        ClearActiveGathering();

        if (desiredItem != null && FindTarget(desiredItem, true))
        {
            return true;
        }

        nextGatherAllowedTime =
            Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
        return false;
    }

    bool TryStartGatheringNowInternal(bool allowScheduledWorkHarvest)
    {
        if (!canGather ||
            collector == null ||
            !collector.canPickupItems ||
            (!allowScheduledWorkHarvest &&
            !NpcScheduleController.AllowsGather(gameObject)))
        {
            return false;
        }

        RefreshScheduleSession();

        if (ShouldPauseForSmartNpcDamageRecovery())
        {
            return false;
        }

        if (!allowScheduledWorkHarvest &&
            ShouldBlockScheduledWorkGathering())
        {
            CancelScheduledWorkGathering();
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
            StopNpcMovement();
            SetGatherAction();
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
        NpcMapZone? preferredZone = GetPreferredZone();
        NpcDangerTier? preferredDangerTier = GetPreferredDangerTier();
        WorldStatItemPickup candidate =
            smartNpc != null &&
            requiredItem == null
                ? FindNearestAvailableAutoPickupInAllFields(
                    preferredZone,
                    preferredDangerTier)
                : WorldResourceField.GetNearestAvailablePickupInAllFields(
                    GetSearchPosition(),
                    requiredItem,
                    preferredZone,
                    preferredDangerTier,
                    smartNpc != null,
                    gameObject);

        if (candidate == null && requiredItem != null)
        {
            candidate = FindNearestAvailablePickupInScene(
                requiredItem,
                !preferredZone.HasValue);
        }

        if (candidate == null)
        {
            if (force)
            {
                nextGatherAllowedTime =
                    Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
            }
            LogScheduledGatherDebug(
                "FindTarget",
                "result=null required=" +
                (requiredItem != null ? ItemText.Name(requiredItem) : "null"));
            return false;
        }

        if (candidate != null &&
            IsTooCloseToAnotherReservedPickup(candidate))
        {
            candidate = requiredItem != null
                ? FindNearestAvailablePickupInScene(
                    requiredItem,
                    !preferredZone.HasValue)
                : null;
        }

        if (candidate == null)
        {
            nextGatherAllowedTime =
                Time.time + Mathf.Max(0f, harvestCooldownWhenNoTarget);
            LogScheduledGatherDebug(
                "FindTarget",
                "result=noSpacedCandidate required=" +
                (requiredItem != null ? ItemText.Name(requiredItem) : "null"));
            return false;
        }

        if (!force &&
            !preferredZone.HasValue)
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
            LogScheduledGatherDebug(
                "FindTarget",
                "reserveFailed pickup=" +
                (candidate.item != null ? ItemText.Name(candidate.item) : "null") +
                " pos=" + candidate.transform.position);
            return false;
        }

        targetPickup = candidate;
        MarkCurrentGatherSlotStarted();
        MoveToTarget();
        LogScheduledGatherDebug(
            "FindTarget",
            "reserved pickup=" +
            (candidate.item != null ? ItemText.Name(candidate.item) : "null") +
            " pos=" + candidate.transform.position);
        return true;
    }

    WorldStatItemPickup FindNearestAvailableAutoPickupInAllFields(
        NpcMapZone? preferredZone,
        NpcDangerTier? preferredDangerTier)
    {
        WorldStatItemPickup[] pickups =
            FindObjectsByType<WorldStatItemPickup>(
                FindObjectsInactive.Exclude);

        WorldStatItemPickup best = null;
        float bestDistance = float.MaxValue;
        Vector3 searchPosition = GetSearchPosition();

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (!IsPickupAvailable(pickup, false) ||
                pickup.item == null)
            {
                continue;
            }

            if (pickup.item.itemType == ItemType.ThucPham)
            {
                continue;
            }

            if (!IsConfiguredResourcePickup(pickup))
            {
                continue;
            }

            if (!MatchesPreferredGatherArea(
                    pickup,
                    preferredZone,
                    preferredDangerTier))
            {
                continue;
            }

            float distance = Vector2.Distance(searchPosition, pickup.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pickup;
            }
        }

        return best;
    }

    bool MatchesPreferredGatherArea(
        WorldStatItemPickup pickup,
        NpcMapZone? preferredZone,
        NpcDangerTier? preferredDangerTier)
    {
        if (pickup == null)
        {
            return false;
        }

        NpcMapArea pickupMapArea = NpcMapArea.FindArea(pickup.transform.position);
        NpcLocationArea pickupLocationArea =
            NpcLocationArea.FindArea(pickup.transform.position);

        if (preferredZone.HasValue)
        {
            if (pickupMapArea == null ||
                pickupMapArea.zone != preferredZone.Value)
            {
                return false;
            }
        }

        if (!IsConfiguredResourcePickup(pickup))
        {
            return false;
        }

        if (!preferredDangerTier.HasValue ||
            pickupLocationArea == null ||
            pickupLocationArea.dangerTier == NpcDangerTier.Any)
        {
            return true;
        }

        return pickupLocationArea.dangerTier == preferredDangerTier.Value;
    }

    WorldStatItemPickup FindNearestAvailablePickupInScene(
        StatItemData requiredItem,
        bool ignorePreferredZone)
    {
        WorldStatItemPickup[] pickups =
            FindObjectsByType<WorldStatItemPickup>(
                FindObjectsInactive.Exclude);

        WorldStatItemPickup best = null;
        float bestDistance = float.MaxValue;
        Vector3 searchPosition = GetSearchPosition();

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (!IsPickupAvailable(pickup, ignorePreferredZone) ||
                !MatchesRequiredItem(pickup, requiredItem) ||
                !IsConfiguredResourcePickup(pickup) ||
                (!ignorePreferredZone && !MatchesPreferredZone(pickup)) ||
                IsTooCloseToAnotherReservedPickup(pickup))
            {
                continue;
            }

            float distance =
                Vector2.Distance(searchPosition, pickup.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pickup;
            }
        }

        return best;
    }

    bool IsTooCloseToAnotherReservedPickup(WorldStatItemPickup candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        // Villager body colliders can be wider than the visual pickup. Keep
        // concurrent harvest targets far enough apart that the collectors do
        // not overlap while their countdown actions temporarily hold them.
        const float minimumPickupSpacing = 1.25f;
        WorldStatItemPickup[] pickups =
            FindObjectsByType<WorldStatItemPickup>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0; i < pickups.Length; i++)
        {
            WorldStatItemPickup other = pickups[i];
            if (other == null ||
                other == candidate ||
                !other.IsReservedByOther(gameObject))
            {
                continue;
            }

            if (Vector2.Distance(
                    candidate.transform.position,
                    other.transform.position) < minimumPickupSpacing)
            {
                return true;
            }
        }

        return false;
    }

    bool MatchesRequiredItem(WorldStatItemPickup pickup, StatItemData requiredItem)
    {
        if (requiredItem == null)
        {
            return true;
        }

        if (pickup == null || pickup.item == null)
        {
            return false;
        }

        // Khi job đã truyền item cụ thể từ Inspector
        // ví dụ Fisher.fishingProduct = ca,
        // thì chỉ được nhận đúng asset đó hoặc đúng ItemId.
        // Không fallback sang ResourceKind ở đây, vì các item khác
        // có thể bị nhận nhầm theo tên / loại, ví dụ Kim Cang Diệp.
        if (pickup.item == requiredItem)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(pickup.item.ItemId) &&
            !string.IsNullOrEmpty(requiredItem.ItemId) &&
            pickup.item.ItemId == requiredItem.ItemId)
        {
            return true;
        }

        return false;
    }

    bool IsConfiguredResourcePickup(WorldStatItemPickup pickup)
    {
        if (pickup == null)
        {
            return false;
        }

        NpcLocationArea area = NpcLocationArea.FindArea(pickup.transform.position);
        return area != null &&
            area.purpose == NpcLocationPurpose.Resource;
    }

    Vector3 GetSearchPosition()
    {
        if (villager != null &&
            (villager.job == VillagerJob.Fisher ||
            villager.job == VillagerJob.Hunter) &&
            villager.workPoint != null)
        {
            return villager.workPoint.position;
        }

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

    NpcDangerTier? GetPreferredDangerTier()
    {
        if (smartNpc == null)
        {
            return null;
        }

        return smartNpc.GetSmartDangerTier();
    }
    void MoveToTarget()
    {
        if (targetPickup == null)
        {
            return;
        }

        if (smartNpc == null)
        {
            smartNpc = GetComponent<SmartNpcAI>();
        }

        targetPickup.RefreshReservation(gameObject, reservationDuration);

        float distance =
            GetPickupSurfaceDistance(targetPickup);

        if (distance <= GetApproachDistance())
        {
            if (Time.time - lastApproachLogTime >= 0.5f)
            {
                LogScheduledGatherDebug(
                    "Approach",
                    "distance=" + distance.ToString("0.00") +
                    " threshold=" + GetApproachDistance().ToString("0.00") +
                    " result=startHarvest");
                lastApproachLogTime = Time.time;
            }
            StartHarvest();
            return;
        }

        if (Time.time - lastApproachLogTime >= 0.75f)
        {
            LogScheduledGatherDebug(
                "Approach",
                "distance=" + distance.ToString("0.00") +
                " threshold=" + GetApproachDistance().ToString("0.00") +
                " mover=" + (mover != null && mover.enabled) +
                " villager=" + (villager != null && villager.enabled) +
                " smart=" + (smartNpc != null && smartNpc.enabled));
            lastApproachLogTime = Time.time;
        }

        if (villager != null &&
            villager.enabled)
        {
            villager.ForceGatherTarget(
                targetPickup.transform,
                targetPickup.item);
            return;
        }

        if (smartNpc != null &&
            smartNpc.enabled)
        {
            smartNpc.ForceGatherTarget(
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
            if (Time.time - lastStartHarvestLogTime >= 0.5f)
            {
                LogScheduledGatherDebug(
                    "StartHarvest",
                    "result=pickupInvalid desired=" +
                    (desiredItem != null ? ItemText.Name(desiredItem) : "null"));
                lastStartHarvestLogTime = Time.time;
            }

            FindTarget(desiredItem, true);
            return;
        }

        harvestingPickup = targetPickup;
        targetPickup = null;
        harvestTimer =
            Mathf.Max(
                0.1f,
                GetHarvestDurationForPickup(harvestingPickup));
        lastHarvestActionSeconds = -1;
        harvestingPickup.RefreshReservation(
            gameObject,
            Mathf.Max(reservationDuration, harvestTimer + 1f));
        StopNpcMovement();
        SetGatherAction();
        if (Time.time - lastStartHarvestLogTime >= 0.25f)
        {
            LogScheduledGatherDebug(
                "StartHarvest",
                "result=ok timer=" + harvestTimer.ToString("0.00"));
            lastStartHarvestLogTime = Time.time;
        }
    }

    float GetHarvestDurationForPickup(WorldStatItemPickup pickup)
    {
        if (pickup == null)
        {
            return 0.1f;
        }

        StatItemData item = pickup.item;
        if (item == null)
        {
            return Mathf.Max(
                0.1f,
                pickup.harvestDurationScaledSeconds);
        }

        if (item.materialKind == MaterialKind.Herb ||
            ResourceNode.InferKindFromItem(item) == HarvestResourceKind.ThaoDuoc)
        {
            switch (item.grade)
            {
                case ItemGrade.Trung:
                    return 10f;
                case ItemGrade.Thuong:
                case ItemGrade.Tien:
                    return 15f;
                default:
                    return 5f;
            }
        }

        return Mathf.Max(
            0.1f,
            pickup.harvestDurationScaledSeconds);
    }

    void ContinueHarvest()
    {
        if (!IsPickupAvailable(harvestingPickup))
        {
            StatItemData desiredItem = GetPickupItem(harvestingPickup);
            ReleaseSmartNpcGatherTarget(harvestingPickup);
            ClearPickupReservation(harvestingPickup);
            harvestingPickup = null;
            targetPickup = null;
            harvestTimer = 0f;
            lastHarvestActionSeconds = -1;
            if (Time.time - lastContinueHarvestLogTime >= 0.5f)
            {
                LogScheduledGatherDebug(
                    "Continue",
                    "result=pickupInvalid desired=" +
                    (desiredItem != null ? ItemText.Name(desiredItem) : "null"));
                lastContinueHarvestLogTime = Time.time;
            }
            FindTarget(desiredItem, true);
            return;
        }

        harvestingPickup.RefreshReservation(
            gameObject,
            Mathf.Max(reservationDuration, harvestTimer + 1f));

        float distance =
            GetPickupSurfaceDistance(harvestingPickup);

        if (distance > Mathf.Max(GetApproachDistance() + retargetDistance, harvestBreakDistance))
        {
            harvestingPickup.RefreshReservation(gameObject, reservationDuration);
            targetPickup = harvestingPickup;
            harvestingPickup = null;
            harvestTimer = 0f;
            lastHarvestActionSeconds = -1;
            if (Time.time - lastContinueHarvestLogTime >= 0.5f)
            {
                LogScheduledGatherDebug(
                    "Continue",
                    "result=tooFar distance=" + distance.ToString("0.00") +
                    " break=" +
                    Mathf.Max(
                        GetApproachDistance() + retargetDistance,
                        harvestBreakDistance).ToString("0.00"));
                lastContinueHarvestLogTime = Time.time;
            }
            MoveToTarget();
            return;
        }

        StopNpcMovement();
        harvestTimer -= Time.deltaTime;
        RefreshGatherActionCountdown();

        if (Time.time - lastContinueHarvestLogTime >= 1f)
        {
            LogScheduledGatherDebug(
                "Continue",
                "result=harvesting distance=" + distance.ToString("0.00") +
                " timer=" + harvestTimer.ToString("0.00"));
            lastContinueHarvestLogTime = Time.time;
        }

        if (harvestTimer > 0f)
        {
            return;
        }

        CompleteHarvest();
    }

    void CompleteHarvest()
    {
        WorldStatItemPickup pickup = harvestingPickup;
        ReleaseSmartNpcGatherTarget(pickup);
        harvestingPickup = null;
        targetPickup = null;
        harvestTimer = 0f;
        lastHarvestActionSeconds = -1;

        if (!IsPickupAvailable(pickup))
        {
            if (pickup != null)
            {
                pickup.ClearReservation(gameObject);
            }
            if (Time.time - lastCompleteHarvestLogTime >= 0.5f)
            {
                LogScheduledGatherDebug("Complete", "result=pickupInvalid");
                lastCompleteHarvestLogTime = Time.time;
            }
            return;
        }

        StatItemData item = pickup.item;
        if (item == null ||
            !pickup.TryTake(1))
        {
            pickup.ClearReservation(gameObject);
            if (Time.time - lastCompleteHarvestLogTime >= 0.5f)
            {
                LogScheduledGatherDebug(
                    "Complete",
                    "result=takeFailed item=" +
                    (item != null ? ItemText.Name(item) : "null"));
                lastCompleteHarvestLogTime = Time.time;
            }
            FindTarget(item, true);
            return;
        }

        pickup.ClearReservation(gameObject);

        collector.ReceiveItemWithoutUse(
            item,
            ItemLifecycleEventType.Picked);
        if (Time.time - lastCompleteHarvestLogTime >= 0.25f)
        {
            LogScheduledGatherDebug(
                "Complete",
                "result=ok item=" + ItemText.Name(item));
            lastCompleteHarvestLogTime = Time.time;
        }

        harvestsThisScheduleSession++;
        if (limitHarvestsPerScheduleSlot &&
            maxHarvestsPerScheduleSlot > 0 &&
            harvestsThisScheduleSession >= maxHarvestsPerScheduleSlot)
        {
            MarkCurrentGatherSlotCompleted();
        }
        bool shouldContinueScheduledHarvest =
            IsScheduledHarvestJobActive() ||
            (villager != null &&
            (villager.job == VillagerJob.Farmer ||
            villager.job == VillagerJob.Fisher ||
            villager.job == VillagerJob.Hunter));

        nextGatherAllowedTime = shouldContinueScheduledHarvest
            ? Time.time
            : (NpcScheduleController.AllowsGather(gameObject)
                ? Time.time
                : Time.time + Mathf.Max(0f, harvestCooldownAfterSuccess));
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
        lastHarvestActionSeconds =
            Mathf.CeilToInt(Mathf.Max(0f, harvestTimer));

        string itemName = harvestingPickup != null &&
            harvestingPickup.item != null
            ? ItemText.Name(harvestingPickup.item)
            : NpcText.Label("resource");

        string actionKey = "gatherResourceCountdown";

        if (harvestingPickup != null &&
            harvestingPickup.item != null)
        {
            if (harvestingPickup.item.itemType == ItemType.ThucPham &&
                harvestingPickup.item.foodKind == FoodKind.Fish)
            {
                actionKey = "catchFishCountdown";
            }
            else if (villager != null)
            {
                switch (villager.job)
                {
                    case VillagerJob.Farmer:
                        actionKey = "harvestResourceCountdown";
                        break;
                    case VillagerJob.Fisher:
                        actionKey = "harvestResourceCountdown";
                        break;
                    case VillagerJob.Hunter:
                        actionKey = "butcherResourceCountdown";
                        break;
                }
            }
        }
        else if (villager != null)
        {
            switch (villager.job)
            {
                case VillagerJob.Farmer:
                    actionKey = "harvestResourceCountdown";
                    break;
                case VillagerJob.Fisher:
                    actionKey = "harvestResourceCountdown";
                    break;
                case VillagerJob.Hunter:
                    actionKey = "butcherResourceCountdown";
                    break;
            }
        }

        NpcRoleUtility.SetAction(
            gameObject,
            NpcText.ActionFormat(
                actionKey,
                itemName,
                Mathf.CeilToInt(Mathf.Max(0f, harvestTimer))));
    }

    void RefreshGatherActionCountdown()
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, harvestTimer));
        if (seconds == lastHarvestActionSeconds)
        {
            return;
        }

        SetGatherAction();
    }

    bool IsPickupAvailable(WorldStatItemPickup pickup)
    {
        return IsPickupAvailable(pickup, false);
    }

    bool IsPickupAvailable(
        WorldStatItemPickup pickup,
        bool ignorePreferredZone)
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

        if (pickup == stalledPickup &&
            Time.time < stalledPickupBlockedUntil)
        {
            return false;
        }

        if (pickup == stalledPickup)
        {
            stalledPickup = null;
            stalledPickupBlockedUntil = 0f;
        }

        if (smartNpc != null &&
            !IsConfiguredResourcePickup(pickup))
        {
            return false;
        }

        if (!ignorePreferredZone)
        {
            NpcMapZone? preferredZone = GetPreferredZone();
            if (preferredZone.HasValue)
            {
                NpcMapArea pickupArea = NpcMapArea.FindArea(pickup.transform.position);
                return pickupArea != null && pickupArea.zone == preferredZone.Value;
            }
        }

        if (ShouldApplyPickupDistanceLimit(ignorePreferredZone))
        {
            float distance =
                Vector2.Distance(GetSearchPosition(), pickup.transform.position);

            if (distance > maxSearchDistance + retargetDistance)
            {
                return false;
            }
        }

        return true;
    }

    bool ShouldApplyPickupDistanceLimit(bool ignorePreferredZone)
    {
        if (IsScheduledHarvestJobActive())
        {
            return false;
        }

        if (!ignorePreferredZone && GetPreferredZone().HasValue)
        {
            return false;
        }

        return true;
    }

    bool IsScheduledHarvestJobActive()
    {
        return villager != null && IsScheduledHarvestJob(villager.job);
    }

    bool MatchesPreferredZone(WorldStatItemPickup pickup)
    {
        NpcMapZone? preferredZone = GetPreferredZone();
        if (!preferredZone.HasValue)
        {
            return true;
        }

        if (pickup == null)
        {
            return false;
        }

        NpcMapArea pickupArea = NpcMapArea.FindArea(pickup.transform.position);
        return pickupArea != null && pickupArea.zone == preferredZone.Value;
    }

    public bool HasAutonomousGatherCandidate()
    {
        if (smartNpc == null)
        {
            return false;
        }

        return HasAutonomousGatherCandidate(GetPreferredDangerTier());
    }

    public bool HasAutonomousGatherCandidate(NpcDangerTier? preferredDangerTier)
    {
        if (smartNpc == null)
        {
            return false;
        }

        return FindNearestAvailableAutoPickupInAllFields(
            GetPreferredZone(),
            preferredDangerTier) != null;
    }

    StatItemData GetPickupItem(WorldStatItemPickup pickup)
    {
        return pickup != null ? pickup.item : null;
    }

    void ClearTargetReservation()
    {
        ReleaseSmartNpcGatherTarget(targetPickup);
        ClearPickupReservation(targetPickup);
        targetPickup = null;
    }

    public void CancelGatheringNow()
    {
        ClearActiveGathering();
    }

    void ClearActiveGathering()
    {
        ReleaseSmartNpcGatherTarget(targetPickup);
        ReleaseSmartNpcGatherTarget(harvestingPickup);
        ClearPickupReservation(targetPickup);
        ClearPickupReservation(harvestingPickup);
        targetPickup = null;
        harvestingPickup = null;
        harvestTimer = 0f;
        lastHarvestActionSeconds = -1;
    }

    void ReleaseSmartNpcGatherTarget(WorldStatItemPickup pickup)
    {
        if (smartNpc != null && pickup != null)
        {
            smartNpc.ReleaseGatherTarget(pickup.transform);
        }
    }

    void CancelScheduledWorkGathering()
    {
        ClearActiveGathering();
    }

    bool ShouldCancelSmartNpcGatheringForSchedule()
    {
        if (smartNpc == null ||
            !smartNpc.enabled ||
            (targetPickup == null && harvestingPickup == null))
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

        switch (schedule.CurrentActivity)
        {
            case NpcScheduleActivity.Gather:
            case NpcScheduleActivity.Hunt:
            case NpcScheduleActivity.FreeHuntAndGather:
            case NpcScheduleActivity.TakeTask:
            case NpcScheduleActivity.DoMission:
                return false;

            default:
                return true;
        }
    }

    bool ShouldCancelSmartNpcGatheringForCombat()
    {
        if (smartNpc == null ||
            !smartNpc.enabled ||
            (targetPickup == null && harvestingPickup == null))
        {
            return false;
        }

        SmartAITask task = smartNpc.CurrentSmartTask;
        if (task != null &&
            task.IsValid &&
            (task.goal == SmartAITaskGoal.Combat ||
            task.goal == SmartAITaskGoal.Pursued ||
            task.goal == SmartAITaskGoal.SupportAlly))
        {
            return true;
        }

        return IsSmartNpcCombatAction(smartNpc.currentAction);
    }

    bool IsSmartNpcCombatAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return action == NpcText.Action("goHunt") ||
            action == NpcText.Action("fleeMonsterArea") ||
            action == NpcText.Action("fightBlockingMonster") ||
            action == NpcText.Action("guardSpiritHerbMonster") ||
            action == NpcText.Action("clearHarvestMonster") ||
            ActionStartsWith(action, NpcText.Action("huntMonsterNamed")) ||
            ActionStartsWith(action, NpcText.Action("attackMonsterNamed"));
    }

    static bool ActionStartsWith(string action, string prefix)
    {
        if (string.IsNullOrWhiteSpace(action) ||
            string.IsNullOrWhiteSpace(prefix))
        {
            return false;
        }

        int placeholderIndex = prefix.IndexOf('{');
        string resolvedPrefix = placeholderIndex >= 0
            ? prefix.Substring(0, placeholderIndex).Trim()
            : prefix.Trim();

        return !string.IsNullOrWhiteSpace(resolvedPrefix) &&
            action.StartsWith(
                resolvedPrefix,
                System.StringComparison.OrdinalIgnoreCase);
    }

    void ClearPickupReservation(WorldStatItemPickup pickup)
    {
        if (pickup != null)
        {
            pickup.ClearReservation(gameObject);
        }
    }

    bool ShouldBlockScheduledWorkGathering()
    {
        if (villager == null ||
            !IsScheduledHarvestJob(villager.job))
        {
            return false;
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null &&
            harvestJob.IsWaitingForRetry)
        {
            return true;
        }

        if (harvestingPickup != null ||
            targetPickup != null)
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

        if (schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity != NpcScheduleActivity.Gather)
        {
            return false;
        }

        return IsScheduledWorkAction(villager.currentAction) ||
            villager.currentAction == NpcText.Action("farmerWaitHarvest") ||
            villager.currentAction == NpcText.Action("farmerHarvestedToday");
    }

    bool IsScheduledHarvestJob(VillagerJob job)
    {
        return job == VillagerJob.Farmer ||
            job == VillagerJob.Fisher ||
            job == VillagerJob.Hunter;
    }

    bool IsScheduledWorkAction(string action)
    {
        return action == NpcText.Action("goFarmWork") ||
            action == NpcText.Action("workingFarm") ||
            action == NpcText.Action("goFish") ||
            action == NpcText.Action("fishing") ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("hunting");
    }

    string GetWorkAction(VillagerJob job)
    {
        switch (job)
        {
            case VillagerJob.Fisher:
                return NpcText.Action("goFish");
            case VillagerJob.Hunter:
                return NpcText.Action("goHunt");
            default:
                return NpcText.Action("goFarmWork");
        }
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
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        if (schedule == null ||
            !schedule.enforceSchedule ||
            schedule.CurrentSlot == null)
        {
            return "free";
        }

        NpcScheduleSlot slot = schedule.CurrentSlot;
        return NpcScheduleController.GetStableSlotKey(slot, slot.activity);
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
