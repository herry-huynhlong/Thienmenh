using UnityEngine;

public partial class SmartNpcAI
{
    SmartNpcHelpRequestSystem.HelpRequest currentHelpRequest;
    float nextHelpRequestAllowedTime;
    bool isRetreatingFromMonster;
    float retreatUntilTime;
    Vector3 retreatTarget;

    bool TryHandleCombatSupport()
    {
        if (isRetreatingFromMonster)
        {
            if (Time.time >= retreatUntilTime)
            {
                isRetreatingFromMonster = false;
                retreatTarget = Vector3.zero;
                ClearEmergencyTaskIfMatches(SmartAITaskGoal.Pursued);
                if (currentAction == NpcText.Action("fleeMonsterArea"))
                {
                    currentAction = string.Empty;
                }
            }
            else
            {
                currentAction = NpcText.Action("fleeMonsterArea");
                currentTarget = null;
                hasWanderTarget = true;
                wanderTarget = retreatTarget;
                return true;
            }
        }

        if (currentHelpRequest != null)
        {
            if (!IsValidHelpRequest(currentHelpRequest))
            {
                ClearHelpRequestState();
            }
            else
            {
                MonsterAI monster =
                    currentHelpRequest.monster != null
                    ? currentHelpRequest.monster.GetComponent<MonsterAI>()
                    : null;

                if (monster == null || monster.IsDead)
                {
                    ClearHelpRequestState();
                    return false;
                }

                currentMonsterTarget = monster;
                currentTarget = monster.transform;
                hasWanderTarget = false;
                RequestEmergencyTask(
                    SmartAITaskGoal.SupportAlly,
                    SmartAITaskPriority.Emergency,
                    false,
                    "combat support");
                currentAction = NpcText.ActionFormat(
                    "huntMonsterNamed",
                    monster.monsterName);
                return true;
            }
        }

        if (TryAdoptHelpRequest())
        {
            return true;
        }

        return false;
    }

    bool TryAdoptHelpRequest()
    {
        if (Time.time < nextHelpRequestAllowedTime ||
            IsDead ||
            IsLockedRoutineAction(currentAction))
        {
            return false;
        }

        SmartNpcHelpRequestSystem helpSystem =
            SmartNpcHelpRequestSystem.Instance;
        if (helpSystem == null)
        {
            return false;
        }

        SmartNpcHelpRequestSystem.HelpRequest request =
            helpSystem.GetBestRequestNear(
                gameObject,
                helpSystem.nearbyRequestRadius);
        if (request == null ||
            !helpSystem.TryAcceptHelp(gameObject, request))
        {
            nextHelpRequestAllowedTime = Time.time + 2f;
            return false;
        }

        currentHelpRequest = request;
        MonsterAI monster =
            request.monster != null
            ? request.monster.GetComponent<MonsterAI>()
            : null;

        if (monster == null || monster.IsDead)
        {
            ClearHelpRequestState();
            return false;
        }

        currentMonsterTarget = monster;
        currentTarget = monster.transform;
        hasWanderTarget = false;
        RequestEmergencyTask(
            SmartAITaskGoal.SupportAlly,
            SmartAITaskPriority.Emergency,
            false,
            "adopt help request");
        currentAction = NpcText.ActionFormat(
            "huntMonsterNamed",
            monster.monsterName);
        return true;
    }

    void RequestHelpForMonster(MonsterAI monster)
    {
        if (monster == null ||
            Time.time < nextHelpRequestAllowedTime)
        {
            return;
        }

        SmartNpcHelpRequestSystem helpSystem =
            SmartNpcHelpRequestSystem.Instance;
        if (helpSystem == null)
        {
            return;
        }

        helpSystem.RequestHelp(
            gameObject,
            monster.gameObject,
            CombatPowerUtility.GetThreatRatio(
                gameObject,
                monster.gameObject));

        nextHelpRequestAllowedTime =
            Time.time + Random.Range(10f, 20f);
    }

    bool TryReserveMonsterTarget(
        MonsterAI monster,
        float durationSeconds)
    {
        if (monster == null)
        {
            return false;
        }

        TargetReservationSystem reservationSystem =
            TargetReservationSystem.Instance;
        if (reservationSystem == null)
        {
            return true;
        }

        bool reserved = reservationSystem.TryReserve(
            monster.gameObject,
            gameObject,
            durationSeconds,
            "Combat");

        if (reserved)
        {
            currentMonsterTarget = monster;
        }

        return reserved;
    }

    void ReleaseMonsterReservation(MonsterAI monster = null)
    {
        MonsterAI target = monster != null
            ? monster
            : currentMonsterTarget;

        if (target == null)
        {
            return;
        }

        TargetReservationSystem reservationSystem =
            TargetReservationSystem.Instance;
        if (reservationSystem != null)
        {
            reservationSystem.Release(target.gameObject, gameObject);
        }
    }

    void BeginMonsterRetreat(MonsterAI monster)
    {
        if (monster == null)
        {
            return;
        }

        isRetreatingFromMonster = true;
        retreatUntilTime = Time.time + 2.5f;
        retreatTarget = GetRetreatPoint(monster.transform.position);
        currentTarget = null;
        hasWanderTarget = true;
        wanderTarget = retreatTarget;
        hasEscapeTarget = false;
        hasObstacleAvoidTarget = false;
        RequestEmergencyTask(
            SmartAITaskGoal.Pursued,
            SmartAITaskPriority.Emergency,
            false,
            "monster retreat");
        currentAction = NpcText.Action("fleeMonsterArea");
        StopNpcMovement();
    }

    Vector3 GetRetreatPoint(Vector3 threatPosition)
    {
        Vector2 away =
            (Vector2)transform.position - (Vector2)threatPosition;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = Random.insideUnitCircle;
        }

        float retreatDistance =
            Mathf.Max(4f, Mathf.Max(1f, idleWanderRadius) * 1.5f);
        Vector3 point =
            transform.position + (Vector3)away.normalized * retreatDistance;

        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        if (area != null &&
            area.areaBounds != null)
        {
            Bounds bounds = area.areaBounds.bounds;
            point.x = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
            point.y = Mathf.Clamp(point.y, bounds.min.y, bounds.max.y);
        }

        return point;
    }

    bool IsValidHelpRequest(SmartNpcHelpRequestSystem.HelpRequest request)
    {
        return request != null &&
            request.requester != null &&
            request.monster != null &&
            !request.IsExpired &&
            request.requester.activeInHierarchy &&
            request.monster.activeInHierarchy &&
            NpcAreaUtility.IsSameArea(gameObject, request.requester) &&
            NpcAreaUtility.IsSameArea(gameObject, request.monster);
    }

    void ClearHelpRequestState()
    {
        currentHelpRequest = null;
        ClearEmergencyTaskIfMatches(SmartAITaskGoal.SupportAlly);
    }

    bool HasCombatSupportIntent()
    {
        return isRetreatingFromMonster ||
            currentHelpRequest != null;
    }
}
