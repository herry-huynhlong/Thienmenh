using System.Collections.Generic;
using UnityEngine;

public partial class NpcFixedBlacksmithController
{
    void Reset()
    {
        CacheReferences();
        ApplyRecommendedSetup();
    }

    void Awake()
    {
        CacheReferences();

        if (autoConfigureVillager)
        {
            ApplyRecommendedSetup();
        }
        else
        {
            EnsureRecommendedScheduleConfigured();
        }

        if (disableLegacyForgeComponents)
        {
            DisableLegacySystems();
        }
    }

    void Start()
    {
        CacheReferences();
        EnsureRecommendedScheduleConfigured();

        if (grantStartingMoneyOnStart)
        {
            EnsureStartingMoney();
        }

        if (lastProgressWorldHour < 0f)
        {
            lastProgressWorldHour = GetCurrentWorldHour();
        }

        SyncSleepVisibility();
    }

    void Update()
    {
        SyncSleepVisibility();
    }

    [ContextMenu("Blacksmith/Debug Current Trade Route")]
    public void DebugCurrentTradeRoute()
    {
        CacheReferences();

        if (villager == null)
        {
            Debug.LogWarning(
                "[NpcFixedBlacksmith] missing VillagerAI for " + name,
                this);
            return;
        }

        if (!TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker broker))
        {
            Debug.LogWarning(
                "[NpcFixedBlacksmith] no buy destination for " + name,
                this);
            return;
        }

        Transform approachPoint = buyApproachPointOverride;
        Transform buyPoint = buyPointOverride;
        float probeRadius = Mathf.Max(0.12f, GetApproachClearanceRadius());

        string detail =
            "actorPos=" + transform.position +
            " actorZone=" + GetZoneText(GetCurrentZone()) +
            " state=" + state +
            " action=" + villager.currentAction +
            " buyApproachPoint=" +
            (approachPoint != null ? approachPoint.position.ToString() : "none") +
            " buyApproachFeasible=" +
            (approachPoint != null &&
                IsVillagerMoveTargetFeasible(approachPoint.position) ? 1 : 0) +
            " buyApproachClearLine=" +
            (approachPoint != null &&
                HasVillagerClearLineTo(approachPoint.position) ? 1 : 0) +
            " buyApproachHits=" +
            DescribeBlockingCollidersAtPoint(
                approachPoint != null ? approachPoint.position : transform.position,
                probeRadius) +
            " buyPoint=" +
            (buyPoint != null ? buyPoint.position.ToString() : "none") +
            " resolvedBuy=" + buyPosition +
            " sourceDetail=" + lastTradeDestinationDetail +
            " buyZone=" + GetZoneText(buyZone) +
            " isBrokerTarget=" + (isBrokerTarget ? 1 : 0) +
            " resolvedBuyFeasible=" +
            (IsVillagerMoveTargetFeasible(buyPosition) ? 1 : 0) +
            " resolvedBuyClearLine=" +
            (HasVillagerClearLineTo(buyPosition) ? 1 : 0) +
            " resolvedBuyHits=" +
            DescribeBlockingCollidersAtPoint(
                buyPosition,
                probeRadius) +
            " rayToApproach=" +
            DescribeRaycastBlockersTo(
                approachPoint != null ? approachPoint.position : buyPosition,
                probeRadius) +
            " rayToResolvedBuy=" +
            DescribeRaycastBlockersTo(
                buyPosition,
                probeRadius);

        if (broker != null)
        {
            BoxCollider2D customerZone = broker.GetCustomerZoneCollider();
            detail +=
                " broker=" + broker.name +
                " customerCenter=" + broker.CustomerPosition +
                " customerRadius=" + broker.CustomerServiceRadius.ToString("0.00");

            if (customerZone != null)
            {
                Bounds bounds = customerZone.bounds;
                detail +=
                    " customerZoneMin=" + bounds.min +
                    " customerZoneMax=" + bounds.max +
                    " approachInsideZone=" +
                    (approachPoint != null && customerZone.OverlapPoint(approachPoint.position) ? 1 : 0) +
                    " resolvedInsideZone=" +
                    (customerZone.OverlapPoint(buyPosition) ? 1 : 0);
            }
        }

        Debug.LogWarning(
            "[NpcFixedBlacksmith] " + name + " DebugCurrentTradeRoute " + detail,
            this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ClampConfig();
        CacheReferences();

        if (!Application.isPlaying &&
            autoConfigureVillager)
        {
            ApplyRecommendedSetup();
        }
    }
#endif

    [ContextMenu("Blacksmith/Apply Recommended Setup")]
    public void ApplyRecommendedSetup()
    {
        CacheReferences();
        ClampConfig();

        if (villager != null)
        {
            villager.job = VillagerJob.Blacksmith;
            villager.keepInspectorJob = true;
            villager.hideAtHome = true;
            villager.homeRoutineManagedExternally = false;
            villager.autonomousWorkEnabled = true;
            villager.dailyRoutineEnabled = false;
            villager.dailyTaskPlanEnabled = false;
        }

        NpcSpecialProfession specialProfession =
            GetComponent<NpcSpecialProfession>();
        if (specialProfession != null)
        {
            specialProfession.professionName = professionName;
            specialProfession.lockVillagerJob = true;
            specialProfession.villagerJob = VillagerJob.Blacksmith;
        }

        if (schedule != null)
        {
            schedule.enforceSchedule = true;
            schedule.autoBuildDefaultSchedule = false;
            schedule.lifePath = NpcLifePath.Commoner;
            schedule.canCultivate = false;
            schedule.slots = BuildRecommendedSlots();
        }
    }

    [ContextMenu("Blacksmith/Reset Cycle")]
    public void ResetCycle()
    {
        state = ForgeCycleState.NeedMaterials;
        forgedWorkHours = 0f;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        lastPurchaseDay = -1;
        lastSaleDay = -1;
    }

    public bool TryRunWorkCycle()
    {
        CacheReferences();
        EnsureRecommendedScheduleConfigured();

        if (villager == null ||
            !enabled ||
            !isActiveAndEnabled)
        {
            return false;
        }

        switch (state)
        {
            case ForgeCycleState.BuyingMaterials:
                return HandleBuyingMaterials();
            case ForgeCycleState.Forging:
                return HandleForging();
            case ForgeCycleState.ReadyToSell:
                return HandleReadyToSell();
            case ForgeCycleState.Selling:
                return HandleSelling();
            default:
                return HandleNeedMaterials();
        }
    }

    void CacheReferences()
    {
        villager = villager != null
            ? villager
            : GetComponent<VillagerAI>();
        inventory = inventory != null
            ? inventory
            : GetComponent<ItemInventory>();
        schedule = schedule != null
            ? schedule
            : GetComponent<NpcScheduleController>();
        selfColliders = selfColliders != null &&
            selfColliders.Length > 0
            ? selfColliders
            : GetComponentsInChildren<Collider2D>(true);
    }

    bool IsSelfCollider(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.transform == transform ||
            hit.transform.IsChildOf(transform))
        {
            return true;
        }

        if (selfColliders == null ||
            selfColliders.Length == 0)
        {
            selfColliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < selfColliders.Length; i++)
        {
            if (selfColliders[i] == hit)
            {
                return true;
            }
        }

        return false;
    }

    void ClampConfig()
    {
        craftDays = Mathf.Max(1, craftDays);
        workHoursPerDay = Mathf.Clamp(workHoursPerDay, 1f, 24f);
        buyDurationSeconds = Mathf.Max(0.25f, buyDurationSeconds);
        sellDurationSeconds = Mathf.Max(0.25f, sellDurationSeconds);
        tradeStart = Mathf.Max(sleepEnd, tradeStart);
        morningWorkStart =
            Mathf.Max(tradeStart + 0.1f, morningWorkStart);
        morningWorkEnd = Mathf.Max(morningWorkStart + 0.1f, morningWorkEnd);
        afternoonWorkStart = Mathf.Max(morningWorkEnd + 0.1f, afternoonWorkStart);
        afternoonWorkEnd =
            Mathf.Max(afternoonWorkStart + 0.1f, afternoonWorkEnd);
    }

    List<NpcScheduleSlot> BuildRecommendedSlots()
    {
        return new List<NpcScheduleSlot>
        {
            CreateSlot(NpcScheduleActivity.Sleep, sleepStart, sleepEnd),
            CreateSlot(NpcScheduleActivity.ReturnHome, sleepEnd, tradeStart),
            CreateSlot(NpcScheduleActivity.TradeBuySell, tradeStart, morningWorkStart),
            CreateSlot(NpcScheduleActivity.Work, morningWorkStart, morningWorkEnd),
            CreateSlot(NpcScheduleActivity.ReturnHome, morningWorkEnd, afternoonWorkStart),
            CreateSlot(NpcScheduleActivity.Work, afternoonWorkStart, afternoonWorkEnd)
        };
    }

    static NpcScheduleSlot CreateSlot(
        NpcScheduleActivity activity,
        float startHour,
        float endHour)
    {
        return new NpcScheduleSlot
        {
            activity = activity,
            startHour = startHour,
            endHour = endHour,
            allowDangerInterrupt = true,
            allowHungerInterrupt = true,
            allowFatigueInterrupt = true,
            allowSocialInterrupt = false
        };
    }

    void EnsureRecommendedScheduleConfigured()
    {
        if (schedule == null)
        {
            return;
        }

        schedule.enforceSchedule = true;
        schedule.autoBuildDefaultSchedule = false;
        schedule.lifePath = NpcLifePath.Commoner;
        schedule.canCultivate = false;

        if (HasRecommendedSchedule())
        {
            return;
        }

        schedule.slots = BuildRecommendedSlots();
    }

    bool HasRecommendedSchedule()
    {
        if (schedule == null ||
            schedule.slots == null ||
            schedule.slots.Count != 6)
        {
            return false;
        }

        return IsMatchingSlot(schedule.slots[0], NpcScheduleActivity.Sleep, sleepStart, sleepEnd) &&
            IsMatchingSlot(schedule.slots[1], NpcScheduleActivity.ReturnHome, sleepEnd, tradeStart) &&
            IsMatchingSlot(schedule.slots[2], NpcScheduleActivity.TradeBuySell, tradeStart, morningWorkStart) &&
            IsMatchingSlot(schedule.slots[3], NpcScheduleActivity.Work, morningWorkStart, morningWorkEnd) &&
            IsMatchingSlot(schedule.slots[4], NpcScheduleActivity.ReturnHome, morningWorkEnd, afternoonWorkStart) &&
            IsMatchingSlot(schedule.slots[5], NpcScheduleActivity.Work, afternoonWorkStart, afternoonWorkEnd);
    }

    static bool IsMatchingSlot(
        NpcScheduleSlot slot,
        NpcScheduleActivity activity,
        float startHour,
        float endHour)
    {
        return slot != null &&
            slot.activity == activity &&
            Mathf.Abs(slot.startHour - startHour) <= 0.01f &&
            Mathf.Abs(slot.endHour - endHour) <= 0.01f;
    }

    void DisableLegacySystems()
    {
        DisableComponent(GetComponent<NpcForgeAgent>());
        DisableComponent(GetComponent<NpcTradeAgent>());
        DisableComponent(GetComponent<NpcForgeRole>());
    }

    static void DisableComponent(Behaviour behaviour)
    {
        if (behaviour != null)
        {
            behaviour.enabled = false;
        }
    }

    void EnsureStartingMoney()
    {
        int currentMoney = NpcEconomy.GetNpcMoney(gameObject);
        if (currentMoney >= startingMoney)
        {
            return;
        }

        NpcEconomy.AddNpcMoney(gameObject, startingMoney - currentMoney);
        LogDebug("SeedMoney", "money=" + NpcEconomy.GetNpcMoney(gameObject));
    }

}
