using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NpcFixedBlacksmithController : MonoBehaviour
{
    public enum ForgeCycleState
    {
        NeedMaterials,
        BuyingMaterials,
        Forging,
        ReadyToSell,
        Selling
    }

    [Header("Auto Setup")]
    public bool autoConfigureVillager = true;
    public bool disableLegacyForgeComponents = true;
    public string professionName = "Lo Ren";
    public bool suppressBaseTimeRestRules = true;

    [Header("Economy")]
    [Min(0)] public int startingMoney = 100000;
    public bool grantStartingMoneyOnStart = true;
    public StatItemData forgedItem;
    [Min(0)] public int materialCost = 100000;
    [Min(0)] public int salePrice = 200000;
    public bool buyMaterialsAtVanBaoLau = true;
    public bool sellAtVanBaoLau = true;
    public NpcMapZone preferredTradeZone = NpcMapZone.VanBaoLau;

    [Header("Production")]
    [Min(1)] public int craftDays = 3;
    [Range(1f, 24f)] public float workHoursPerDay = 8f;
    [Min(0.25f)] public float buyDurationSeconds = 5f;
    [Min(0.25f)] public float sellDurationSeconds = 10f;

    [Header("Schedule")]
    [Range(0f, 24f)] public float sleepStart = 20f;
    [Range(0f, 24f)] public float sleepEnd = 6f;
    [Range(0f, 24f)] public float morningWorkStart = 8f;
    [Range(0f, 24f)] public float morningWorkEnd = 12f;
    [Range(0f, 24f)] public float afternoonWorkStart = 13f;
    [Range(0f, 24f)] public float afternoonWorkEnd = 17f;

    [Header("Points")]
    public Transform forgePointOverride;
    public Transform marketPointOverride;

    [Header("Actions")]
    public string buyAction = "Di mua nguyen lieu ren";
    public string forgeAction = "Dang ren phap bao";
    public string sellAction = "Di ban phap bao";
    public string waitAction = "Cho ca ren tiep theo";

    [Header("Runtime")]
    public ForgeCycleState state = ForgeCycleState.NeedMaterials;
    [Min(0f)] public float forgedWorkHours;
    [Min(0)] public int completedCycles;
    public float lastProgressWorldHour = -1f;
    public float stateStartedAtRealtime = -1f;
    public int lastPurchaseDay = -1;
    public int lastSaleDay = -1;
    public bool debugLogs;

    VillagerAI villager;
    ItemInventory inventory;
    NpcScheduleController schedule;

    public bool SuppressBaseTimeRestRules => suppressBaseTimeRestRules;

    float RequiredWorkHours =>
        Mathf.Max(1, craftDays) * Mathf.Max(1f, workHoursPerDay);

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

        if (disableLegacyForgeComponents)
        {
            DisableLegacySystems();
        }
    }

    void Start()
    {
        CacheReferences();

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
            villager.hideAtHome = false;
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
    }

    void ClampConfig()
    {
        craftDays = Mathf.Max(1, craftDays);
        workHoursPerDay = Mathf.Clamp(workHoursPerDay, 1f, 24f);
        buyDurationSeconds = Mathf.Max(0.25f, buyDurationSeconds);
        sellDurationSeconds = Mathf.Max(0.25f, sellDurationSeconds);
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
            CreateSlot(NpcScheduleActivity.Idle, sleepEnd, morningWorkStart),
            CreateSlot(NpcScheduleActivity.Work, morningWorkStart, morningWorkEnd),
            CreateSlot(NpcScheduleActivity.Idle, morningWorkEnd, afternoonWorkStart),
            CreateSlot(NpcScheduleActivity.Work, afternoonWorkStart, afternoonWorkEnd),
            CreateSlot(NpcScheduleActivity.Idle, afternoonWorkEnd, sleepStart)
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

    bool HandleNeedMaterials()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (NpcEconomy.GetNpcMoney(gameObject) < materialCost)
        {
            villager.SetActionImmediate("Thieu linh thach de mua nguyen lieu", 2f);
            return true;
        }

        if (!TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker buyBroker))
        {
            villager.SetActionImmediate(
                "Khong tim thay diem mua o " +
                GetZoneLabel(preferredTradeZone),
                Mathf.Max(1f, villager.thinkInterval));
            LogDebug(
                "NeedMaterials",
                "missingBuyDestination preferredZone=" + preferredTradeZone);
            return true;
        }

        if (!HasArrivedAtTradeDestination(
                buyPosition,
                isBrokerTarget,
                buyBroker))
        {
            villager.ForceJobMoveTo(
                buyPosition,
                buyAction,
                buyZone);
            return true;
        }

        BeginBuyingMaterials();
        return true;
    }

    void BeginBuyingMaterials()
    {
        state = ForgeCycleState.BuyingMaterials;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate("Dang mua nguyen lieu ren", buyDurationSeconds);
        LogDebug("State", "BuyingMaterials");
    }

    bool HandleBuyingMaterials()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (TryGetBuyDestination(
                out Vector3 buyPosition,
                out NpcMapZone? buyZone,
                out bool isBrokerTarget,
                out NpcCounterBroker buyBroker) &&
            !HasArrivedAtTradeDestination(
                buyPosition,
                isBrokerTarget,
                buyBroker))
        {
            villager.ForceJobMoveTo(
                buyPosition,
                buyAction,
                buyZone);
            return true;
        }

        if (!HasActionFinished(buyDurationSeconds))
        {
            villager.SetActionImmediate("Dang mua nguyen lieu ren", buyDurationSeconds);
            return true;
        }

        if (NpcEconomy.GetNpcMoney(gameObject) < materialCost)
        {
            state = ForgeCycleState.NeedMaterials;
            villager.SetActionImmediate("Khong du linh thach mua nguyen lieu", 2f);
            return true;
        }

        NpcEconomy.AddNpcMoney(gameObject, -materialCost);
        state = ForgeCycleState.Forging;
        forgedWorkHours = 0f;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        lastPurchaseDay = GetCurrentWorldDay();
        villager.SetActionImmediate("Da mua xong nguyen lieu", 1f);
        LogDebug(
            "BuyComplete",
            "money=" + NpcEconomy.GetNpcMoney(gameObject));
        return true;
    }

    bool HandleForging()
    {
        Transform forgePoint = GetForgePoint();
        float currentWorldHour = GetCurrentWorldHour();

        if (forgePoint != null &&
            !IsNear(forgePoint.position))
        {
            lastProgressWorldHour = currentWorldHour;
            villager.ForceJobMoveTo(
                forgePoint.position,
                forgeAction,
                NpcMapNavigator.GetDestinationZone(forgePoint));
            return true;
        }

        if (lastProgressWorldHour < 0f)
        {
            lastProgressWorldHour = currentWorldHour;
        }

        float gainedWorkHours =
            CalculateWorkHoursBetween(lastProgressWorldHour, currentWorldHour);
        lastProgressWorldHour = currentWorldHour;

        if (gainedWorkHours > 0f)
        {
            forgedWorkHours =
                Mathf.Min(
                    RequiredWorkHours,
                    forgedWorkHours + gainedWorkHours);
        }

        if (forgedWorkHours >= RequiredWorkHours)
        {
            CompleteForging();
            return true;
        }

        villager.SetActionImmediate(
            forgeAction + " (" +
            Mathf.CeilToInt(forgedWorkHours) + "/" +
            Mathf.CeilToInt(RequiredWorkHours) + "h)",
            Mathf.Max(1f, villager.thinkInterval));
        return true;
    }

    void CompleteForging()
    {
        if (inventory != null &&
            forgedItem != null)
        {
            inventory.AddItem(forgedItem, 1);
        }

        state = ForgeCycleState.ReadyToSell;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        villager.SetActionImmediate("Da ren xong, chuan bi dem ban", 1f);
        LogDebug(
            "ForgeComplete",
            "cycles=" + completedCycles +
            " item=" + (forgedItem != null ? forgedItem.itemName : "null"));
    }

    bool HandleReadyToSell()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (!TryGetSellDestination(
                out Vector3 sellPosition,
                out NpcMapZone? sellZone,
                out bool isBrokerTarget,
                out NpcCounterBroker sellBroker))
        {
            villager.SetActionImmediate(
                "Khong tim thay diem ban o " +
                GetZoneLabel(preferredTradeZone),
                Mathf.Max(1f, villager.thinkInterval));
            LogDebug(
                "ReadyToSell",
                "missingSellDestination preferredZone=" + preferredTradeZone);
            return true;
        }

        if (!HasArrivedAtTradeDestination(
                sellPosition,
                isBrokerTarget,
                sellBroker))
        {
            villager.ForceJobMoveTo(
                sellPosition,
                sellAction,
                sellZone);
            return true;
        }

        BeginSelling();
        return true;
    }

    void BeginSelling()
    {
        state = ForgeCycleState.Selling;
        stateStartedAtRealtime = Time.time;
        villager.SetActionImmediate("Dang ban phap bao", sellDurationSeconds);
        LogDebug("State", "Selling");
    }

    bool HandleSelling()
    {
        lastProgressWorldHour = GetCurrentWorldHour();

        if (TryGetSellDestination(
                out Vector3 sellPosition,
                out NpcMapZone? sellZone,
                out bool isBrokerTarget,
                out NpcCounterBroker sellBroker) &&
            !HasArrivedAtTradeDestination(
                sellPosition,
                isBrokerTarget,
                sellBroker))
        {
            villager.ForceJobMoveTo(
                sellPosition,
                sellAction,
                sellZone);
            return true;
        }

        if (!HasActionFinished(sellDurationSeconds))
        {
            villager.SetActionImmediate("Dang ban phap bao", sellDurationSeconds);
            return true;
        }

        if (inventory != null &&
            forgedItem != null)
        {
            inventory.RemoveItem(forgedItem, 1);
        }

        NpcEconomy.AddNpcMoney(gameObject, salePrice);
        completedCycles++;
        state = ForgeCycleState.NeedMaterials;
        forgedWorkHours = 0f;
        stateStartedAtRealtime = -1f;
        lastProgressWorldHour = GetCurrentWorldHour();
        lastSaleDay = GetCurrentWorldDay();
        villager.SetActionImmediate(waitAction, 1f);
        LogDebug(
            "SellComplete",
            "money=" + NpcEconomy.GetNpcMoney(gameObject) +
            " cycles=" + completedCycles);
        return true;
    }

    bool HasActionFinished(float durationSeconds)
    {
        if (stateStartedAtRealtime < 0f)
        {
            return true;
        }

        return Time.time - stateStartedAtRealtime >= Mathf.Max(0.01f, durationSeconds);
    }

    Transform GetForgePoint()
    {
        if (forgePointOverride != null)
        {
            return forgePointOverride;
        }

        return villager != null
            ? villager.workPoint
            : null;
    }

    Transform GetMarketPoint()
    {
        if (marketPointOverride != null)
        {
            return marketPointOverride;
        }

        if (villager != null &&
            villager.marketPoint != null)
        {
            return villager.marketPoint;
        }

        return GetForgePoint();
    }

    bool TryGetBuyDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        Transform marketPoint = GetMarketPoint();
        NpcMapZone? marketZone = ResolveZoneForTransform(marketPoint);

        if (marketPointOverride != null &&
            marketPoint != null &&
            marketZone.HasValue)
        {
            targetPosition = marketPoint.position;
            targetZone = marketZone;
            isBrokerTarget = false;
            broker = null;
            return true;
        }

        if (TryGetBrokerDestination(
                buyMaterialsAtVanBaoLau,
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (marketPoint == null ||
            (buyMaterialsAtVanBaoLau &&
            marketZone.HasValue &&
            marketZone.Value != preferredTradeZone))
        {
            return TryGetZoneFallbackDestination(
                buyMaterialsAtVanBaoLau
                    ? preferredTradeZone
                    : ResolveFallbackZone(marketZone),
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker);
        }

        targetPosition = marketPoint.position;
        targetZone = buyMaterialsAtVanBaoLau
            ? preferredTradeZone
            : marketZone;
        isBrokerTarget = false;
        broker = null;
        return true;
    }

    bool TryGetSellDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        Transform marketPoint = GetMarketPoint();
        NpcMapZone? marketZone = ResolveZoneForTransform(marketPoint);

        if (marketPointOverride != null &&
            marketPoint != null &&
            marketZone.HasValue)
        {
            targetPosition = marketPoint.position;
            targetZone = marketZone;
            isBrokerTarget = false;
            broker = null;
            return true;
        }

        if (TryGetBrokerDestination(
                sellAtVanBaoLau,
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (marketPoint == null ||
            (sellAtVanBaoLau &&
            marketZone.HasValue &&
            marketZone.Value != preferredTradeZone))
        {
            return TryGetZoneFallbackDestination(
                sellAtVanBaoLau
                    ? preferredTradeZone
                    : ResolveFallbackZone(marketZone),
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker);
        }

        targetPosition = marketPoint.position;
        targetZone = sellAtVanBaoLau
            ? preferredTradeZone
            : marketZone;
        isBrokerTarget = false;
        broker = null;
        return true;
    }

    bool TryGetZoneFallbackDestination(
        NpcMapZone? zone,
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        broker = null;
        isBrokerTarget = false;

        if (zone.HasValue)
        {
            NpcMapArea area =
                NpcMapArea.FindNearestAreaInZone(
                    zone.Value,
                    transform.position);
            if (area != null &&
                area.areaBounds != null)
            {
                targetPosition = area.areaBounds.bounds.center;
                targetZone = zone.Value;
                return true;
            }
        }

        targetPosition = Vector3.zero;
        targetZone = null;
        return false;
    }

    NpcMapZone? ResolveFallbackZone(NpcMapZone? zone)
    {
        if (zone.HasValue)
        {
            return zone;
        }

        Transform marketPoint = GetMarketPoint();
        if (marketPoint != null)
        {
            NpcMapArea nearest =
                NpcMapArea.FindNearestArea(marketPoint.position);
            if (nearest != null)
            {
                return nearest.zone;
            }
        }

        return null;
    }

    bool TryGetBrokerDestination(
        bool allowBroker,
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        broker = null;

        if (allowBroker &&
            TryFindBrokerInZone(
                preferredTradeZone,
                out broker))
        {
            targetPosition = broker.CustomerPosition;
            targetZone = preferredTradeZone;
            isBrokerTarget = true;
            return true;
        }

        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        return false;
    }

    bool HasArrivedAtTradeDestination(
        Vector3 targetPosition,
        bool isBrokerTarget,
        NpcCounterBroker broker)
    {
        if (isBrokerTarget)
        {
            if (broker != null &&
                broker.receiveAllNpcRequests)
            {
                return broker.IsCustomerAtCounter(gameObject);
            }
        }

        return IsNear(targetPosition);
    }

    bool TryFindBrokerInZone(
        NpcMapZone zone,
        out NpcCounterBroker broker)
    {
        broker = null;

        NpcCounterBroker[] brokers =
            FindObjectsByType<NpcCounterBroker>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < brokers.Length; i++)
        {
            NpcCounterBroker candidate = brokers[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.receiveAllNpcRequests)
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveBrokerZone(candidate);
            if (!candidateZone.HasValue ||
                candidateZone.Value != zone)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    transform.position,
                    candidate.CustomerPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                broker = candidate;
            }
        }

        if (broker != null)
        {
            return true;
        }

        NpcCounterBroker activeBroker = NpcCounterBroker.Active;
        if (activeBroker != null &&
            activeBroker.receiveAllNpcRequests)
        {
            NpcMapZone? activeZone =
                ResolveBrokerZone(activeBroker);
            if (activeZone.HasValue &&
                activeZone.Value == zone)
            {
                broker = activeBroker;
                return true;
            }
        }

        return false;
    }

    NpcMapZone? ResolveBrokerZone(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            return null;
        }

        if (broker.customerPoint != null)
        {
            NpcMapZone? customerZone =
                ResolveZoneForTransform(broker.customerPoint);
            if (customerZone.HasValue)
            {
                return customerZone;
            }
        }

        return ResolveZoneForTransform(broker.transform);
    }

    NpcMapZone? ResolveZoneForTransform(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        NpcMapZone? destinationZone =
            NpcMapNavigator.GetDestinationZone(target);
        if (destinationZone.HasValue)
        {
            return destinationZone;
        }

        NpcMapArea area = NpcMapArea.FindArea(target.position);
        if (area != null)
        {
            return area.zone;
        }

        area = NpcMapArea.FindNearestArea(target.position);
        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    bool IsNear(Vector3 targetPosition)
    {
        if (villager == null)
        {
            return Vector2.Distance(transform.position, targetPosition) <= 0.25f;
        }

        return Vector2.Distance(transform.position, targetPosition) <=
            Mathf.Max(0.25f, villager.arriveDistance);
    }

    float CalculateWorkHoursBetween(float startWorldHour, float endWorldHour)
    {
        if (endWorldHour <= startWorldHour)
        {
            return 0f;
        }

        float total = 0f;
        int startDayIndex = Mathf.FloorToInt(startWorldHour / 24f);
        int endDayIndex = Mathf.FloorToInt(endWorldHour / 24f);

        for (int dayIndex = startDayIndex; dayIndex <= endDayIndex; dayIndex++)
        {
            float dayBaseHour = dayIndex * 24f;
            total += Overlap(
                startWorldHour,
                endWorldHour,
                dayBaseHour + morningWorkStart,
                dayBaseHour + morningWorkEnd);
            total += Overlap(
                startWorldHour,
                endWorldHour,
                dayBaseHour + afternoonWorkStart,
                dayBaseHour + afternoonWorkEnd);
        }

        return total;
    }

    static float Overlap(
        float rangeStart,
        float rangeEnd,
        float slotStart,
        float slotEnd)
    {
        float start = Mathf.Max(rangeStart, slotStart);
        float end = Mathf.Min(rangeEnd, slotEnd);
        return Mathf.Max(0f, end - start);
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentWorldHour
            : 0f;
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : 0;
    }

    void LogDebug(string stage, string detail)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log(
            "[NpcFixedBlacksmith] " +
            gameObject.name +
            " stage=" + stage +
            " state=" + state +
            " detail=" + detail,
            this);
    }

    static string GetZoneLabel(NpcMapZone zone)
    {
        switch (zone)
        {
            case NpcMapZone.VanBaoLau:
                return "Van Bao Lau";
            case NpcMapZone.MaThuSonMach:
                return "Ma Thu Son Mach";
            default:
                return "Lang";
        }
    }

    void SyncSleepVisibility()
    {
        if (villager == null)
        {
            return;
        }

        bool shouldHide =
            IsSleepScheduleActive() &&
            IsAtHomePoint();

        if (shouldHide)
        {
            if (!villager.IsHiddenAtHome)
            {
                villager.ForceHiddenAtHome(true);
                LogDebug("Visibility", "hideAtHome sleepSlot=1");
            }

            return;
        }

        if (villager.IsHiddenAtHome)
        {
            villager.ForceHiddenAtHome(false);
            LogDebug("Visibility", "hideAtHome sleepSlot=0");
        }
    }

    bool IsSleepScheduleActive()
    {
        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        return schedule.CurrentActivity == NpcScheduleActivity.Sleep;
    }

    bool IsAtHomePoint()
    {
        if (villager == null ||
            villager.homePoint == null)
        {
            return false;
        }

        return Vector2.Distance(
            transform.position,
            villager.homePoint.position) <=
            Mathf.Max(0.25f, villager.arriveDistance);
    }
}
