using UnityEngine;

public partial class VillagerAI
{
    void SetDirectMoveTarget(
        Vector3 position,
        bool preserveCurrentTarget = false,
        NpcMapZone? targetZone = null)
    {
        if (!preserveCurrentTarget)
        {
            currentTarget = null;
            hasObstacleAvoidTarget = false;
        }

        hasWanderTarget = false;
        hasDirectMoveTarget = true;
        directMoveTargetZone = targetZone;
        directMoveTargetUsesRoad = true;

        NpcMapZone? currentZone = GetCurrentMapZone();
        bool crossZoneTarget =
            targetZone.HasValue &&
            currentZone.HasValue &&
            targetZone.Value != currentZone.Value;

        Vector3 resolvedTarget;
        if (crossZoneTarget)
        {
            resolvedTarget = position;
        }
        else
        {
            Vector3 clamped = ClampToCurrentMapArea(position);
            Vector3 clearTarget;
            bool foundClearTarget =
                TryFindClearPointNear(clamped, out clearTarget);
            if (foundClearTarget &&
                ShouldRejectFallbackTarget(clamped, clearTarget))
            {
                LogMovementHaltDebug(
                    "DirectTargetFallbackRejected",
                    "requested=" + position +
                    " clamped=" + clamped +
                    " fallback=" + clearTarget +
                    " requestedDist=" +
                    Vector2.Distance(
                        transform.position,
                        clamped).ToString("0.00") +
                    " fallbackDist=" +
                    Vector2.Distance(
                        transform.position,
                        clearTarget).ToString("0.00"));
                resolvedTarget = clamped;
            }
            else
            {
                resolvedTarget = foundClearTarget
                    ? clearTarget
                    : clamped;
            }
        }

        if (Vector2.Distance(directMoveTarget, resolvedTarget) >
            pathReplanTargetDistance)
        {
            ClearActivePath();
        }

        directMoveTarget = resolvedTarget;
    }

    NpcMapZone? GetCurrentMapZone()
    {
        RefreshCurrentMapArea();
        return currentMapArea != null
            ? currentMapArea.zone
            : (NpcMapZone?)null;
    }

    void RefreshCurrentMapArea(bool allowNearest = false)
    {
        if (!keepInsideNpcMapArea)
        {
            currentMapArea = null;
            return;
        }

        NpcMapArea area = NpcMapArea.FindArea(transform.position);

        if (area == null &&
            NpcMapNavigator.TryGetKnownNpcZone(
                gameObject,
                out NpcMapZone knownZone))
        {
            area = NpcMapArea.FindNearestAreaInZone(
                knownZone,
                transform.position);
        }

        if (area == null)
        {
            return;
        }

        if (allowCrossNpcMapAreas ||
            allowNearest ||
            currentMapArea == null ||
            area == currentMapArea)
        {
            currentMapArea = area;
        }
    }

    void ClampInsideCurrentMapArea()
    {
        if (!keepInsideNpcMapArea ||
            allowCrossNpcMapAreas ||
            currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return;
        }

        Vector3 clamped = ClampToCurrentMapArea(transform.position);
        if (Vector2.Distance(clamped, transform.position) <= 0.001f)
        {
            return;
        }

        if (rb != null)
        {
            rb.position = clamped;
            rb.linearVelocity = Vector2.zero;
            desiredVelocity = Vector2.zero;
        }

        transform.position = new Vector3(
            clamped.x,
            clamped.y,
            transform.position.z);
    }

    Vector3 ClampToCurrentMapArea(Vector3 position)
    {
        if (!keepInsideNpcMapArea ||
            allowCrossNpcMapAreas ||
            currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return position;
        }

        if (IsTeleportEntryTargetForCurrentArea(position))
        {
            return position;
        }

        if (movementTargetZone.HasValue &&
            movementTargetZone.Value == currentMapArea.zone)
        {
            NpcMapArea areaAtPosition = NpcMapArea.FindArea(position);
            if (areaAtPosition != null &&
                areaAtPosition.zone == currentMapArea.zone)
            {
                return position;
            }

            NpcMapArea nearestSameZone =
                NpcMapArea.FindNearestAreaInZone(
                    currentMapArea.zone,
                    position);

            if (nearestSameZone != null &&
                nearestSameZone.areaBounds != null)
            {
                Vector3 nearestPoint = nearestSameZone.ClosestPoint(position);
                if (Vector2.Distance(nearestPoint, position) <=
                    Mathf.Max(0.05f, mapAreaEdgePadding + targetClearRadius))
                {
                    return nearestPoint;
                }
            }
        }

        Collider2D boundsCollider = currentMapArea.areaBounds;
        Vector2 point = position;
        Vector2 closest = boundsCollider.ClosestPoint(point);
        if (Vector2.Distance(closest, point) <= 0.02f)
        {
            return position;
        }

        Bounds bounds = boundsCollider.bounds;
        float padding = Mathf.Max(0f, mapAreaEdgePadding);
        Vector2 candidate = new Vector2(
            Mathf.Clamp(point.x, bounds.min.x + padding, bounds.max.x - padding),
            Mathf.Clamp(point.y, bounds.min.y + padding, bounds.max.y - padding));

        if (IsInsideCurrentMapArea(candidate))
        {
            return new Vector3(candidate.x, candidate.y, position.z);
        }

        closest = boundsCollider.ClosestPoint(candidate);
        Vector2 inward = (Vector2)bounds.center - closest;
        if (inward.sqrMagnitude > 0.0001f)
        {
            closest += inward.normalized * padding;
        }

        return new Vector3(closest.x, closest.y, position.z);
    }

    bool IsInsideCurrentMapArea(Vector2 position)
    {
        if (allowCrossNpcMapAreas ||
            currentMapArea == null ||
            currentMapArea.areaBounds == null)
        {
            return true;
        }

        if (IsTeleportEntryTargetForCurrentArea(position))
        {
            return true;
        }

        if (movementTargetZone.HasValue &&
            movementTargetZone.Value == currentMapArea.zone)
        {
            NpcMapArea areaAtPosition = NpcMapArea.FindArea(position);
            if (areaAtPosition != null &&
                areaAtPosition.zone == currentMapArea.zone)
            {
                return true;
            }

            NpcMapArea nearestSameZone =
                NpcMapArea.FindNearestAreaInZone(
                    currentMapArea.zone,
                    position);

            if (nearestSameZone != null &&
                nearestSameZone.DistanceTo(position) <=
                Mathf.Max(0.05f, mapAreaEdgePadding + targetClearRadius))
            {
                return true;
            }
        }

        Vector2 closest = currentMapArea.areaBounds.ClosestPoint(position);
        return Vector2.Distance(closest, position) <= 0.02f;
    }

    bool IsTeleportEntryTargetForCurrentArea(Vector3 position)
    {
        if (currentMapArea == null ||
            !movementTargetZone.HasValue ||
            movementTargetZone.Value == currentMapArea.zone)
        {
            return false;
        }

        float entryTolerance =
            Mathf.Max(0.35f, targetClearRadius * 2f);

        foreach (NpcTeleportGate gate in NpcTeleportGate.Gates)
        {
            if (gate == null ||
                gate.fromZone != currentMapArea.zone ||
                gate.toZone != movementTargetZone.Value)
            {
                continue;
            }

            if (Vector2.Distance(position, gate.EntryPosition) <= entryTolerance)
            {
                return true;
            }
        }

        return false;
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;
        bool shouldResumeHomeReturn =
            isReturningHome ||
            currentAction == NpcText.Action("goHomeRest");
        bool hadActiveMoveTarget =
            currentTarget != null ||
            hasDirectMoveTarget ||
            hasWanderTarget;

        Vector3 referencePosition = gate != null
            ? gate.ExitPosition
            : transform.position;

        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        if (area == null)
        {
            area = NpcMapArea.FindArea(referencePosition);
        }

        NpcMapZone? resolvedZone =
            area != null
                ? area.zone
                : NpcMapNavigator.ResolveActorZone(gameObject);

        if (gate != null)
        {
            if (resolvedZone.HasValue)
            {
                NpcMapNavigator.ReportNpcZone(gameObject, resolvedZone.Value);
            }
            else
            {
                NpcMapNavigator.ReportNpcZone(gameObject, gate.toZone);
                resolvedZone = gate.toZone;
            }

            area = NpcMapNavigator.ResolveMapAreaAfterTeleport(
                gameObject,
                resolvedZone.Value,
                referencePosition);
        }
        else if (area != null)
        {
            NpcMapNavigator.ReportNpcZone(gameObject, area.zone);
        }

        currentMapArea = area;
        desiredVelocity = Vector2.zero;
        currentTarget = null;
        hasWanderTarget = false;
        hasDirectMoveTarget = false;
        waitingOutsideTreasureLightning = false;
        treasureHuntTarget = null;
        treasureHuntItem = null;
        treasureWaitLowPowerSkirmish = false;
        hasRoadPreference = false;
        prefersRoadForCurrentRoute = false;
        hasObstacleAvoidTarget = false;
        movementTargetZone = null;
        movementPausedUntil = 0f;
        crowdYieldUntil = 0f;
        blockedMoveTimer = 0f;
        crowdBlockedTimer = 0f;
        stuckMoveTimer = 0f;
        thinkTimer = 0f;
        actionTimer = 0f;
        currentAction = NpcText.Action("idle");
        ClearActivePath();
        UpdateCultivationEffect(false);

        LogJobRouteDebug(
            "OnNpcMapTeleported",
            "gate=" + (gate != null ? gate.name : "null") +
            " resolvedZone=" +
            (resolvedZone.HasValue
                ? resolvedZone.Value.ToString()
                : "None") +
            " actorPos=" + transform.position);

        if (shouldResumeHomeReturn && !hiddenAtHome)
        {
            Vector3 homePosition = GetHomePosition();
            Vector3 travelTarget = homePosition;
            TryResolveHomeTravelTarget(ref travelTarget);

            isReturningHome = true;
            currentAction = NpcText.Action("goHomeRest");
            SetDirectMoveTarget(travelTarget, false, GetHomeZone());
            MoveUsingRoad(travelTarget, GetHomeZone());

            if (IsAtHomePosition(homePosition) ||
                IsAtResolvedHomeTravelPosition(homePosition, travelTarget))
            {
                CompleteHomeArrival();
            }
        }
        else if (!hadActiveMoveTarget && gate != null)
        {
            Vector2 awayFromGate =
                ((Vector2)gate.ExitPosition - (Vector2)gate.EntryPosition);

            if (awayFromGate.sqrMagnitude <= 0.0001f)
            {
                awayFromGate = Vector2.up;
            }

            Vector3 nudgeTarget =
                gate.ExitPosition +
                (Vector3)(awayFromGate.normalized *
                Mathf.Max(0.75f, targetClearRadius * 3f));

            if (TryFindClearPointNear(nudgeTarget, out Vector3 clearPoint))
            {
                SetDirectMoveTarget(clearPoint);
            }
        }

        ClampInsideCurrentMapArea();
    }
}
