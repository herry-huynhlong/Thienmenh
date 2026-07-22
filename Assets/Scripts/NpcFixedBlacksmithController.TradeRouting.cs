using UnityEngine;

public partial class NpcFixedBlacksmithController
{
    bool TrySellForgedItemToMarket(
        StatItemData item,
        out int earnedMoney,
        out string detail)
    {
        earnedMoney = salePrice;
        detail = "missingItem";

        if (item == null ||
            inventory == null ||
            inventory.GetAmount(item) <= 0)
        {
            return false;
        }

        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop))
        {
            detail = "missingShop";
            return false;
        }

        int soldAmount;
        int totalPrice;
        if (!shop.SellNpcItemFromInventory(
                item,
                gameObject,
                inventory,
                1,
                out soldAmount,
                out totalPrice) ||
            soldAmount <= 0)
        {
            detail = "sellFailed";
            return false;
        }

        earnedMoney = totalPrice;
        detail =
            item.itemName +
            "x" + soldAmount +
            " price=" + totalPrice;
        return true;
    }

    bool TryFindPreferredTradeShop(out SimpleItemShop shop)
    {
        shop = null;
        lastTradeShopName = "none";

        Transform marketPoint = GetMarketPoint();
        if (marketPoint != null)
        {
            shop = marketPoint.GetComponent<SimpleItemShop>();
            if (shop != null)
            {
                lastTradeShopName = shop.name;
                return true;
            }

            shop = marketPoint.GetComponentInParent<SimpleItemShop>();
            if (shop != null)
            {
                lastTradeShopName = shop.name;
                return true;
            }
        }

        if (TryFindPreferredBrokerBackedShop(out shop))
        {
            lastTradeShopName = shop.name;
            return true;
        }

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude);

        float bestDistance = float.PositiveInfinity;
        Vector3 searchOrigin =
            marketPoint != null
                ? marketPoint.position
                : transform.position;

        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveShopZone(candidate);
            if (candidateZone.HasValue &&
                candidateZone.Value != preferredTradeZone)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    searchOrigin,
                    candidate.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                shop = candidate;
            }
        }

        if (shop != null)
        {
            lastTradeShopName = shop.name;
        }

        return shop != null;
    }

    bool TryFindPreferredBrokerBackedShop(out SimpleItemShop shop)
    {
        shop = null;

        SimpleItemShop[] shops =
            FindObjectsByType<SimpleItemShop>(
                FindObjectsInactive.Exclude);

        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < shops.Length; i++)
        {
            SimpleItemShop candidate = shops[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            NpcCounterBroker broker =
                candidate.GetComponent<NpcCounterBroker>();
            if (broker == null ||
                !broker.isActiveAndEnabled ||
                !broker.receiveAllNpcRequests)
            {
                continue;
            }

            Vector3 targetPosition =
                broker.customerPoint != null
                    ? broker.customerPoint.position
                    : broker.CustomerPosition;

            float distance =
                Vector2.Distance(
                    transform.position,
                    targetPosition);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                shop = candidate;
            }
        }

        return shop != null;
    }

    NpcMapZone? ResolveShopZone(SimpleItemShop shop)
    {
        if (shop == null)
        {
            return null;
        }

        NpcCounterBroker broker =
            shop.GetComponent<NpcCounterBroker>();
        if (broker != null)
        {
            NpcMapZone? brokerZone =
                ResolveBrokerZone(broker);
            if (brokerZone.HasValue)
            {
                return brokerZone;
            }
        }

        if (shop.sellerObject != null)
        {
            NpcMapZone? sellerZone =
                ResolveZoneForTransform(
                    shop.sellerObject.transform);
            if (sellerZone.HasValue)
            {
                return sellerZone;
            }
        }

        if (shop.sellerInventory != null)
        {
            NpcMapZone? inventoryZone =
                ResolveZoneForTransform(
                    shop.sellerInventory.transform);
            if (inventoryZone.HasValue)
            {
                return inventoryZone;
            }
        }

        return ResolveZoneForTransform(shop.transform);
    }

    bool TryGetPreferredTradeDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        broker = null;

        SimpleItemShop shop;
        if (!TryFindPreferredTradeShop(out shop) ||
            shop == null)
        {
            lastTradeDestinationSource = "missingPreferredShop";
            return false;
        }

        broker = shop.GetComponent<NpcCounterBroker>();
        if (broker != null &&
            broker.customerPoint != null)
        {
            lastTradeDestinationSource = "preferredShopBroker";
            targetPosition =
                GetBrokerApproachPosition(broker);
            targetZone = ResolveBrokerZone(broker);
            isBrokerTarget = broker.receiveAllNpcRequests;
            return true;
        }

        lastTradeDestinationSource = "preferredShopRoot";
        targetPosition = shop.transform.position;
        targetZone = ResolveShopZone(shop);
        isBrokerTarget = false;
        return true;
    }

    bool TryGetBuyDestination(
        out Vector3 targetPosition,
        out NpcMapZone? targetZone,
        out bool isBrokerTarget,
        out NpcCounterBroker broker)
    {
        lastTradeDestinationDetail = "none";

        if (TryGetManualTradePoint(
                buyPointOverride,
                "buyPointOverride",
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (HasConfiguredMaterialRequirements() &&
            TryGetPreferredTradeDestination(
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        Transform marketPoint = GetMarketPoint();
        NpcMapZone? marketZone = ResolveZoneForTransform(marketPoint);

        if (marketPointOverride != null &&
            marketPoint != null &&
            marketZone.HasValue)
        {
            lastTradeDestinationSource = "marketPointOverride";
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

        lastTradeDestinationSource = "marketPoint";
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
        lastTradeDestinationDetail = "none";

        if (TryGetManualTradePoint(
                sellPointOverride,
                "sellPointOverride",
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        if (TryGetPreferredTradeDestination(
                out targetPosition,
                out targetZone,
                out isBrokerTarget,
                out broker))
        {
            return true;
        }

        Transform marketPoint = GetMarketPoint();
        NpcMapZone? marketZone = ResolveZoneForTransform(marketPoint);

        if (marketPointOverride != null &&
            marketPoint != null &&
            marketZone.HasValue)
        {
            lastTradeDestinationSource = "marketPointOverride";
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

        lastTradeDestinationSource = "marketPoint";
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
                lastTradeDestinationSource =
                    "zoneFallback:" +
                    GetZoneText(zone);
                targetPosition = area.areaBounds.bounds.center;
                targetZone = zone.Value;
                return true;
            }
        }

        lastTradeDestinationSource =
            "missingZoneFallback:" +
            GetZoneText(zone);
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
            lastTradeDestinationSource =
                "preferredZoneBroker:" +
                GetZoneLabel(preferredTradeZone);
            targetPosition = GetBrokerApproachPosition(broker);
            targetZone = preferredTradeZone;
            isBrokerTarget = true;
            return true;
        }

        lastTradeDestinationSource =
            allowBroker
                ? "missingPreferredZoneBroker:" +
                    GetZoneLabel(preferredTradeZone)
                : "brokerDisabled";
        targetPosition = Vector3.zero;
        targetZone = null;
        isBrokerTarget = false;
        return false;
    }

    Vector3 GetBrokerApproachPosition(NpcCounterBroker broker)
    {
        if (broker == null)
        {
            lastBrokerApproachDetail = "broker=null";
            return Vector3.zero;
        }

        Vector3 customerCenter = broker.CustomerPosition;
        Vector3 approachPosition =
            broker.GetCustomerPositionFor(gameObject);
        BoxCollider2D customerZone =
            broker.GetCustomerZoneCollider();

        if (customerZone != null &&
            customerZone.enabled)
        {
            if (customerZone.OverlapPoint(transform.position))
            {
                hasCachedBrokerApproachPosition = false;
                approachPosition = transform.position;
                approachPosition.z = customerCenter.z;
                lastBrokerApproachDetail =
                    "mode=actorInsideCustomerZone actorPos=" +
                    transform.position +
                    " resolved=" + approachPosition;
                return approachPosition;
            }

            if (hasCachedBrokerApproachPosition &&
                cachedBrokerApproachBroker == broker &&
                cachedBrokerApproachState == state &&
                customerZone.OverlapPoint(cachedBrokerApproachPosition))
            {
                lastBrokerApproachDetail =
                    "mode=cached state=" + cachedBrokerApproachState +
                    " resolved=" + cachedBrokerApproachPosition;
                return cachedBrokerApproachPosition;
            }

            if (!TryGetClearCustomerZoneApproachPosition(
                    broker,
                    customerZone,
                    customerCenter,
                    out approachPosition))
            {
                approachPosition =
                    GetRandomCustomerZoneApproachPosition(
                        customerZone,
                        customerCenter);
                lastBrokerApproachDetail =
                    "mode=randomCustomerZone center=" +
                    customerCenter +
                    " resolved=" + approachPosition;
            }
            else
            {
                lastBrokerApproachDetail =
                    "mode=clearCustomerZone center=" +
                    customerCenter +
                    " preferred=" +
                    broker.GetCustomerPositionFor(gameObject) +
                    " resolved=" + approachPosition;
            }
            cachedBrokerApproachBroker = broker;
            cachedBrokerApproachPosition = approachPosition;
            cachedBrokerApproachState = state;
            hasCachedBrokerApproachPosition = true;
            return approachPosition;
        }

        float serviceRadius =
            Mathf.Max(0.1f, broker.CustomerServiceRadius);
        Vector2 offset =
            (Vector2)(approachPosition - customerCenter);

        if (offset.sqrMagnitude >
            serviceRadius * serviceRadius)
        {
            approachPosition =
                customerCenter +
                (Vector3)(offset.normalized * serviceRadius * 0.85f);
        }

        approachPosition.z = customerCenter.z;
        lastBrokerApproachDetail =
            "mode=serviceRadiusClamp center=" + customerCenter +
            " preferred=" + broker.GetCustomerPositionFor(gameObject) +
            " resolved=" + approachPosition +
            " radius=" + serviceRadius.ToString("0.00");
        return approachPosition;
    }

    bool ShouldUseRoadForTradeMove(
        NpcMapZone? targetZone,
        bool isBrokerTarget)
    {
        if (!isBrokerTarget)
        {
            return true;
        }

        if (!targetZone.HasValue)
        {
            return false;
        }

        NpcMapZone? currentZone = ResolveZoneForPosition(transform.position);
        return !currentZone.HasValue ||
            currentZone.Value != targetZone.Value;
    }

    void MoveVillagerToTradeTarget(
        Vector3 targetPosition,
        string action,
        NpcMapZone? targetZone,
        bool isBrokerTarget)
    {
        if (villager == null)
        {
            return;
        }

        if (TryGetExactTradeApproachTarget(
                targetZone,
                out Vector3 approachPosition,
                out NpcMapZone? approachZone))
        {
            ForceVillagerRoadPreference(approachPosition);
            villager.ForceJobMoveTo(
                approachPosition,
                action,
                approachZone,
                true);
            return;
        }

        if (isBrokerTarget)
        {
            ForceVillagerRoadPreference(targetPosition);
        }

        villager.ForceJobMoveTo(
            targetPosition,
            action,
            targetZone,
            isBrokerTarget ||
            ShouldUseRoadForTradeMove(
                targetZone,
                isBrokerTarget));
    }

    bool TryGetExactTradeApproachTarget(
        NpcMapZone? fallbackZone,
        out Vector3 approachPosition,
        out NpcMapZone? approachZone)
    {
        approachPosition = Vector3.zero;
        approachZone = fallbackZone;

        Transform point = GetActiveTradeApproachPoint();
        if (point == null)
        {
            return false;
        }

        if (!IsTradeApproachPointCompatible(point, fallbackZone))
        {
            return false;
        }

        approachPosition = point.position;
        approachZone =
            ResolveZoneForTransform(point) ??
            fallbackZone;

        return !IsNear(approachPosition);
    }

    Transform GetActiveTradeApproachPoint()
    {
        switch (state)
        {
            case ForgeCycleState.NeedMaterials:
            case ForgeCycleState.BuyingMaterials:
                return buyApproachPointOverride;

            case ForgeCycleState.ReadyToSell:
            case ForgeCycleState.Selling:
                return sellApproachPointOverride;

            default:
                return null;
        }
    }

    Transform GetActiveTradePointOverride()
    {
        switch (state)
        {
            case ForgeCycleState.NeedMaterials:
            case ForgeCycleState.BuyingMaterials:
                return buyPointOverride;

            case ForgeCycleState.ReadyToSell:
            case ForgeCycleState.Selling:
                return sellPointOverride;

            default:
                return null;
        }
    }

    bool IsTradeApproachPointCompatible(
        Transform approachPoint,
        NpcMapZone? fallbackZone)
    {
        if (approachPoint == null)
        {
            return false;
        }

        Transform targetPoint = GetActiveTradePointOverride();
        if (targetPoint == null)
        {
            return true;
        }

        if (approachPoint == targetPoint ||
            approachPoint.IsChildOf(targetPoint) ||
            targetPoint.IsChildOf(approachPoint) ||
            approachPoint.parent == targetPoint.parent)
        {
            return true;
        }

        NpcCounterBroker approachBroker =
            approachPoint.GetComponentInParent<NpcCounterBroker>();
        NpcCounterBroker targetBroker =
            targetPoint.GetComponentInParent<NpcCounterBroker>();
        if (approachBroker != null &&
            approachBroker == targetBroker)
        {
            return true;
        }

        NpcMapZone? approachZone =
            ResolveZoneForTransform(approachPoint) ??
            fallbackZone;
        NpcMapZone? targetZone =
            ResolveZoneForTransform(targetPoint) ??
            fallbackZone;
        if (approachZone.HasValue &&
            targetZone.HasValue &&
            approachZone.Value == targetZone.Value &&
            Vector2.Distance(
                approachPoint.position,
                targetPoint.position) <= 6f)
        {
            return true;
        }

        return false;
    }

    void ForceVillagerRoadPreference(Vector3 targetPosition)
    {
        if (villager == null)
        {
            return;
        }

        villagerHasRoadPreferenceField?.SetValue(
            villager,
            true);
        villagerPrefersRoadForCurrentRouteField?.SetValue(
            villager,
            true);
        villagerRoadPreferenceTargetField?.SetValue(
            villager,
            targetPosition);
    }

    NpcMapZone? ResolveZoneForPosition(Vector3 position)
    {
        NpcMapArea area = NpcMapArea.FindArea(position);
        if (area == null)
        {
            area = NpcMapArea.FindNearestArea(position);
        }

        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    Vector3 GetRandomCustomerZoneApproachPosition(
        BoxCollider2D customerZone,
        Vector3 customerCenter)
    {
        Bounds bounds = customerZone.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float marginX = Mathf.Clamp(
            extents.x * 0.08f,
            0.05f,
            Mathf.Max(0.05f, extents.x - 0.02f));
        float marginY = Mathf.Clamp(
            extents.y * 0.08f,
            0.05f,
            Mathf.Max(0.05f, extents.y - 0.02f));
        float minX = center.x - extents.x + marginX;
        float maxX = center.x + extents.x - marginX;
        float minY = center.y - extents.y + marginY;
        float maxY = center.y + extents.y - marginY;
        float edgeInset = Mathf.Clamp(
            GetArrivalDistance() + 0.2f,
            0.25f,
            Mathf.Max(
                0.25f,
                Mathf.Min(extents.x, extents.y) - 0.05f));

        float safeMinX = Mathf.Min(maxX, minX + edgeInset);
        float safeMaxX = Mathf.Max(minX, maxX - edgeInset);
        float safeMinY = Mathf.Min(maxY, minY + edgeInset);
        float safeMaxY = Mathf.Max(minY, maxY - edgeInset);

        if (safeMinX > safeMaxX)
        {
            float midX = (minX + maxX) * 0.5f;
            safeMinX = midX;
            safeMaxX = midX;
        }

        if (safeMinY > safeMaxY)
        {
            float midY = (minY + maxY) * 0.5f;
            safeMinY = midY;
            safeMaxY = midY;
        }

        float standX =
            Mathf.Approximately(safeMinX, safeMaxX)
                ? safeMinX
                : Random.Range(safeMinX, safeMaxX);
        float standY =
            Mathf.Approximately(safeMinY, safeMaxY)
                ? safeMinY
                : Random.Range(safeMinY, safeMaxY);

        return new Vector3(
            standX,
            standY,
            customerCenter.z);
    }

    bool TryGetClearCustomerZonePreferredPoint(
        NpcCounterBroker broker,
        Vector3 preferredPosition,
        out Vector3 approachPosition)
    {
        approachPosition = preferredPosition;

        if (broker == null)
        {
            return false;
        }

        BoxCollider2D customerZone =
            broker.GetCustomerZoneCollider();
        Vector3 customerCenter = broker.CustomerPosition;

        return TryGetClearCustomerZoneApproachPosition(
            broker,
            customerZone,
            customerCenter,
            preferredPosition,
            out approachPosition);
    }

    bool TryGetClearCustomerZoneApproachPosition(
        NpcCounterBroker broker,
        BoxCollider2D customerZone,
        Vector3 customerCenter,
        out Vector3 approachPosition)
    {
        Vector3 preferred =
            broker != null
                ? broker.GetCustomerPositionFor(gameObject)
                : customerCenter;

        return TryGetClearCustomerZoneApproachPosition(
            broker,
            customerZone,
            customerCenter,
            preferred,
            out approachPosition);
    }

    bool TryGetClearCustomerZoneApproachPosition(
        NpcCounterBroker broker,
        BoxCollider2D customerZone,
        Vector3 customerCenter,
        Vector3 preferred,
        out Vector3 approachPosition)
    {
        approachPosition = customerCenter;

        if (customerZone == null ||
            !customerZone.enabled)
        {
            return false;
        }

        preferred.z = customerCenter.z;

        if (TryResolveReachableCustomerZonePoint(
                preferred,
                customerZone,
                out Vector3 resolvedPreferred))
        {
            approachPosition = resolvedPreferred;
            return true;
        }

        Bounds bounds = customerZone.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float margin = Mathf.Clamp(
            GetApproachClearanceRadius() * 0.75f,
            0.05f,
            Mathf.Max(0.05f, Mathf.Min(extents.x, extents.y) - 0.02f));
        float minX = center.x - extents.x + margin;
        float maxX = center.x + extents.x - margin;
        float minY = center.y - extents.y + margin;
        float maxY = center.y + extents.y - margin;
        float step = Mathf.Max(
            0.14f,
            GetApproachClearanceRadius() * 0.85f);
        float bestDistance = float.PositiveInfinity;
        bool found = false;

        for (float y = minY; y <= maxY; y += step)
        {
            for (float x = minX; x <= maxX; x += step)
            {
                Vector3 candidate = new Vector3(
                    x,
                    y,
                    customerCenter.z);
                if (!TryResolveReachableCustomerZonePoint(
                        candidate,
                        customerZone,
                        out Vector3 resolvedCandidate))
                {
                    continue;
                }

                float distance =
                    (resolvedCandidate - preferred).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    approachPosition = resolvedCandidate;
                    found = true;
                }
            }
        }

        return found;
    }

    NpcCounterBroker FindBrokerForManualTradePoint(
        Vector3 position,
        NpcMapZone? targetZone)
    {
        NpcCounterBroker[] brokers =
            FindObjectsByType<NpcCounterBroker>(
                FindObjectsInactive.Exclude);

        NpcCounterBroker bestBroker = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < brokers.Length; i++)
        {
            NpcCounterBroker candidate = brokers[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled)
            {
                continue;
            }

            NpcMapZone? candidateZone =
                ResolveBrokerZone(candidate);
            if (targetZone.HasValue &&
                candidateZone.HasValue &&
                candidateZone.Value != targetZone.Value)
            {
                continue;
            }

            BoxCollider2D customerZone =
                candidate.GetCustomerZoneCollider();
            bool coversPoint =
                customerZone != null &&
                customerZone.enabled &&
                customerZone.bounds.Contains(position);
            if (!coversPoint)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    position,
                    candidate.CustomerPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestBroker = candidate;
            }
        }

        if (bestBroker != null)
        {
            return bestBroker;
        }

        if (targetZone.HasValue &&
            TryFindBrokerInZone(targetZone.Value, out NpcCounterBroker zoneBroker))
        {
            return zoneBroker;
        }

        return null;
    }

    bool TryResolveReachableCustomerZonePoint(
        Vector3 candidate,
        BoxCollider2D customerZone,
        out Vector3 resolvedPoint)
    {
        resolvedPoint = candidate;

        if (!IsCustomerZoneApproachClear(
                candidate,
                customerZone))
        {
            return false;
        }

        if (IsVillagerMoveTargetFeasible(candidate) &&
            HasVillagerClearLineTo(candidate))
        {
            return true;
        }

        if (!TryFindVillagerClearPointNear(
                candidate,
                customerZone,
                out Vector3 clearPoint))
        {
            return false;
        }

        clearPoint.z = candidate.z;
        if (!IsCustomerZoneApproachClear(
                clearPoint,
                customerZone) ||
            !HasVillagerClearLineTo(clearPoint))
        {
            return false;
        }

        resolvedPoint = clearPoint;
        return true;
    }

    bool IsCustomerZoneApproachClear(
        Vector3 candidate,
        BoxCollider2D customerZone)
    {
        if (customerZone == null ||
            !customerZone.enabled)
        {
            return false;
        }

        Bounds bounds = customerZone.bounds;
        if (!bounds.Contains(candidate))
        {
            return false;
        }

        float clearanceRadius = GetApproachClearanceRadius();
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            candidate,
            clearanceRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                hit.isTrigger ||
                hit == customerZone ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<VillagerAI>() != null ||
                hit.GetComponentInParent<SmartNpcAI>() != null ||
                hit.GetComponentInParent<NpcMapMover2D>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    bool IsVillagerMoveTargetFeasible(Vector3 candidate)
    {
        if (villager == null ||
            villagerIsMoveTargetFeasibleMethod == null)
        {
            return true;
        }

        object result =
            villagerIsMoveTargetFeasibleMethod.Invoke(
                villager,
                new object[] { candidate });
        return result is bool feasible &&
            feasible;
    }

    bool HasVillagerClearLineTo(Vector3 candidate)
    {
        if (villager == null ||
            villagerHasClearLineToMethod == null)
        {
            return true;
        }

        object result =
            villagerHasClearLineToMethod.Invoke(
                villager,
                new object[] { candidate });
        return result is bool clear &&
            clear;
    }

    bool TryFindVillagerClearPointNear(
        Vector3 candidate,
        BoxCollider2D customerZone,
        out Vector3 clearPoint)
    {
        clearPoint = candidate;

        if (villager == null ||
            customerZone == null ||
            !customerZone.enabled ||
            villagerTryFindClearPointNearMethod == null)
        {
            return false;
        }

        object[] args =
        {
            candidate,
            candidate
        };
        object result =
            villagerTryFindClearPointNearMethod.Invoke(
                villager,
                args);
        if (!(result is bool found) ||
            !found)
        {
            return false;
        }

        if (!(args[1] is Vector3 resolved))
        {
            return false;
        }

        if (!customerZone.bounds.Contains(resolved))
        {
            return false;
        }

        clearPoint = resolved;
        return true;
    }

    float GetApproachClearanceRadius()
    {
        float radius =
            Mathf.Max(0.12f, GetArrivalDistance() * 0.8f);
        Collider2D[] ownColliders =
            GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D own = ownColliders[i];
            if (own == null ||
                own.isTrigger)
            {
                continue;
            }

            Bounds bounds = own.bounds;
            radius = Mathf.Max(
                radius,
                Mathf.Min(
                    0.35f,
                    Mathf.Max(
                        bounds.extents.x,
                        bounds.extents.y)));
        }

        return Mathf.Clamp(radius, 0.12f, 0.35f);
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

    string DescribeTradeTarget(
        Vector3 targetPosition,
        NpcMapZone? targetZone,
        bool isBrokerTarget,
        NpcCounterBroker broker,
        bool arrived)
    {
        string detail =
            "actorPos=" + transform.position +
            " currentZone=" + GetZoneText(GetCurrentZone()) +
            " target=" + targetPosition +
            " targetZone=" + GetZoneText(targetZone) +
            " source=" + lastTradeDestinationSource +
            " sourceDetail=" + lastTradeDestinationDetail +
            " shop=" + lastTradeShopName +
            " isBrokerTarget=" + (isBrokerTarget ? 1 : 0) +
            " arrived=" + (arrived ? 1 : 0) +
            " distTarget=" +
            Vector2.Distance(transform.position, targetPosition).ToString("0.00") +
            " arriveDistance=" +
            GetArrivalDistance().ToString("0.00");

        if (broker == null)
        {
            return detail + " broker=null";
        }

        Vector3 brokerCenter = broker.CustomerPosition;
        Vector3 brokerStand =
            GetBrokerApproachPosition(broker);
        BoxCollider2D customerZone =
            broker.GetCustomerZoneCollider();

        string zoneDetail = " customerZone=none";
        if (customerZone != null &&
            customerZone.enabled)
        {
            Bounds bounds = customerZone.bounds;
            zoneDetail =
                " customerZoneMin=" + bounds.min +
                " customerZoneMax=" + bounds.max +
                " customerZoneCenter=" + bounds.center;
        }

        return detail +
            " broker=" + broker.name +
            " brokerZone=" + GetZoneText(ResolveBrokerZone(broker)) +
            " brokerCenter=" + brokerCenter +
            " brokerStand=" + brokerStand +
            " brokerRadius=" +
            broker.CustomerServiceRadius.ToString("0.00") +
            " distCenter=" +
            Vector2.Distance(transform.position, brokerCenter).ToString("0.00") +
            " distStand=" +
            Vector2.Distance(transform.position, brokerStand).ToString("0.00") +
            " atCounter=" +
            (broker.IsCustomerAtCounter(gameObject) ? 1 : 0) +
            zoneDetail;
    }

    bool TryFindBrokerInZone(
        NpcMapZone zone,
        out NpcCounterBroker broker)
    {
        broker = null;

        NpcCounterBroker[] brokers =
            FindObjectsByType<NpcCounterBroker>(
                FindObjectsInactive.Exclude);

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

        for (int i = 0; i < brokers.Length; i++)
        {
            NpcCounterBroker candidate = brokers[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.receiveAllNpcRequests)
            {
                continue;
            }

            broker = candidate;
            return true;
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

}
