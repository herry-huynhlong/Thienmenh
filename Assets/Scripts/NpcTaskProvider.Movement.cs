using UnityEngine;

public partial class NpcTaskProvider
{
    NpcInteractionPoint GetProviderInteractionPoint()
    {
        Transform point = keepProviderStationary
            ? providerStandPoint
            : providerPoint;

        if (point == null)
        {
            return null;
        }

        NpcInteractionPoint interactionPoint =
            point.GetComponent<NpcInteractionPoint>();
        if (interactionPoint == null && Application.isPlaying)
        {
            interactionPoint =
                point.gameObject.AddComponent<NpcInteractionPoint>();
            interactionPoint.interactionRadius =
                Mathf.Max(0.45f, providerTalkDistance);
            interactionPoint.standSpacing =
                Mathf.Max(0.85f, providerVisitorStandRadius);
            interactionPoint.reservationSpacingRadius = 0.75f;
            interactionPoint.reservationHoldSeconds = 15f;
        }

        return interactionPoint;
    }

    public float GetProviderInteractionDistance()
    {
        NpcInteractionPoint interactionPoint =
            GetProviderInteractionPoint();
        if (interactionPoint != null)
        {
            return Mathf.Max(
                providerTalkDistance,
                arriveDistance,
                interactionPoint.interactionRadius);
        }

        return Mathf.Max(
            providerTalkDistance,
            arriveDistance);
    }

    float GetNpcProviderInteractionDistance(GameObject npc)
    {
        if (npc == null)
        {
            return float.PositiveInfinity;
        }

        return Vector2.Distance(
            npc.transform.position,
            GetProviderPositionFor(npc));
    }

    public bool IsNpcInProviderInteractionRange(GameObject npc)
    {
        return GetNpcProviderInteractionDistance(npc) <=
            GetProviderInteractionDistance();
    }

    public Vector3 GetProviderPositionFor(GameObject npc)
    {
        Vector3 center = GetProviderPosition();
        NpcInteractionPoint interactionPoint =
            GetProviderInteractionPoint();
        if (interactionPoint != null)
        {
            return interactionPoint.GetStandPositionFor(npc);
        }

        if (!spreadVisitorsAroundProvider ||
            npc == null ||
            providerVisitorStandRadius <= 0.01f)
        {
            return GetClearTaskPositionNear(center, npc);
        }

        int hash = Mathf.Abs(npc.GetInstanceID());
        float angle = (hash % 360) * Mathf.Deg2Rad;
        float standRadius = Mathf.Max(
            providerVisitorStandRadius,
            arriveDistance * 2.5f,
            1.1f);
        Vector2 offset =
            new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
            standRadius;

        return GetClearTaskPositionNear(center + (Vector3)offset, npc);
    }

    Vector3 GetMealPosition()
    {
        return mealPoint != null
            ? mealPoint.position
            : transform.position;
    }

    Vector3 GetCounterPosition(GameObject npc = null)
    {
        if (counterPoint != null)
        {
            NpcInteractionPoint interactionPoint =
                counterPoint.GetComponent<NpcInteractionPoint>();

            if (interactionPoint != null)
            {
                return interactionPoint.GetStandPositionFor(npc);
            }

            return GetClearTaskPositionNear(counterPoint.position, npc);
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null)
        {
            return GetClearTaskPositionNear(transform.position, npc);
        }

        return GetClearTaskPositionNear(broker.GetCustomerPositionFor(npc), npc);
    }

    Vector3 GetBoardPosition(GameObject npc = null)
    {
        Vector3 position = taskBoardPoint != null
            ? taskBoardPoint.position
            : transform.position;

        if (taskBoardPoint != null)
        {
            NpcInteractionPoint interactionPoint =
                taskBoardPoint.GetComponent<NpcInteractionPoint>();

            if (interactionPoint != null)
            {
                return interactionPoint.GetStandPositionFor(npc);
            }
        }

        return GetClearTaskPositionNear(position, npc);
    }

    void TryTradeAtCounter(GameObject npc)
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null ||
            !broker.receiveAllNpcRequests ||
            npc == null)
        {
            return;
        }

        NpcRoleUtility.StopForConversation(npc);
        NpcRoleUtility.StopForConversation(broker.gameObject);
        NpcRoleUtility.SetAction(broker.gameObject, TaskAction("counterTradeWithCustomer"));

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            ItemInventory inventory = villager.inventory != null
                ? villager.inventory
                : npc.GetComponent<ItemInventory>();

            broker.TryBuyProduceFrom(villager, inventory);
        }

        NpcTradeAgent tradeAgent = npc.GetComponent<NpcTradeAgent>();
        if (tradeAgent != null)
        {
            broker.TryTradeWithNpc(tradeAgent);
        }
    }

    Vector3 ResolveActiveCounterTradePosition(
        GameObject npc,
        Vector3 fallbackPosition)
    {
        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker == null ||
            !broker.receiveAllNpcRequests)
        {
            return fallbackPosition;
        }

        Vector3 brokerPosition =
            broker.GetCustomerPositionFor(npc);
        brokerPosition.z = fallbackPosition.z;
        return brokerPosition;
    }

    bool IsNpcReadyForCounterTrade(
        GameObject npc,
        Vector3 counterTarget)
    {
        if (npc == null)
        {
            return false;
        }

        NpcCounterBroker broker = NpcCounterBroker.Active;
        if (broker != null &&
            broker.receiveAllNpcRequests)
        {
            return broker.IsCustomerAtCounter(npc);
        }

        return Vector2.Distance(
            npc.transform.position,
            counterTarget) <= arriveDistance;
    }

    internal void MoveNpcToWork(RunningNpcTask task, Vector3 target)
    {
        if (task == null)
        {
            return;
        }

        MoveNpc(
            task.npc,
            target,
            GetWorkZone(task.offer));
    }

    NpcMapZone? GetWorkZone(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return null;
        }

        switch (offer.taskType)
        {
            case NpcTaskType.GatherResource:
            case NpcTaskType.HuntMonster:
            case NpcTaskType.FrontierWatch:
                return NpcMapZone.MaThuSonMach;
            case NpcTaskType.Patrol:
                return null;
            default:
                return null;
        }
    }

    void MoveNpc(GameObject npc, Vector3 target)
    {
        MoveNpc(npc, target, null);
    }

    void MoveNpc(
        GameObject npc,
        Vector3 target,
        NpcMapZone? forcedTargetZone)
    {
        if (npc == null)
        {
            return;
        }
        bool usingTeleportRoute;
        string routeAction;
        NpcRouteStatus routeStatus;
        Vector3 moveTarget = NpcMapNavigator.GetNextMoveTarget(
            npc,
            target,
            forcedTargetZone,
            out usingTeleportRoute,
            out routeAction,
            out routeStatus);

        if ((usingTeleportRoute || IsRouteBlocked(routeStatus)) &&
            !string.IsNullOrEmpty(routeAction))
        {
            NpcRoleUtility.SetAction(npc, routeAction);
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null && mover.enabled)
        {
            mover.SetMoveTarget(
                moveTarget,
                !string.IsNullOrEmpty(routeAction) ? routeAction : TaskAction("followTaskRoute"),
                true);
            return;
        }

        float speed = NpcRoleUtility.GetMoveSpeed(npc, fallbackMoveSpeed);
        MoveNpcTransformSafely(
            npc,
            moveTarget,
            speed * Time.deltaTime);
    }

    bool IsRouteBlocked(NpcRouteStatus routeStatus)
    {
        return routeStatus == NpcRouteStatus.NoGate ||
            routeStatus == NpcRouteStatus.InvalidGate;
    }

    void MoveNpcTransformSafely(
        GameObject npc,
        Vector3 moveTarget,
        float maxDistanceDelta)
    {
        Vector3 current = npc.transform.position;
        if (IsTaskPositionBlocked(current, npc))
        {
            npc.transform.position = GetClearTaskPositionNear(current, npc);
            return;
        }

        Vector3 next =
            Vector3.MoveTowards(
                current,
                moveTarget,
                maxDistanceDelta);

        if (!IsTaskPositionBlocked(next, npc))
        {
            npc.transform.position = next;
            return;
        }

        Vector2 direction = (Vector2)(moveTarget - current);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();
        float step = Mathf.Max(maxDistanceDelta, arriveDistance * 0.5f);
        float[] angles = { 35f, -35f, 70f, -70f, 110f, -110f, 180f };
        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 detour = RotateDirection(direction, angles[i]);
            Vector3 candidate =
                current +
                new Vector3(detour.x, detour.y, 0f) * step;

            if (!IsTaskPositionBlocked(candidate, npc))
            {
                npc.transform.position = candidate;
                return;
            }
        }
    }

    bool IsNpcRecoveringFromDamage(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        return smartNpc != null &&
            smartNpc.IsRecoveringFromDamage;
    }

    void HoldNpcForDamage(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        NpcRoleUtility.StopForConversation(npc);
        NpcRoleUtility.SetAction(npc, TaskAction("injured"));
    }

    Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    void OnNpcMapTeleported(GameObject gateObject)
    {
        NpcTeleportGate gate = gateObject != null
            ? gateObject.GetComponent<NpcTeleportGate>()
            : null;

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
    }
}
