using UnityEngine;

public partial class VillagerAI
{
    // Legacy compatibility wrapper kept so older callers still compile.
    // Commoner villagers no longer branch into cultivator behavior here.
    void GoWorkOrCultivatorActivity()
    {
        if (IsAlchemyWorker())
        {
            GoAlchemyWorkOrTrade();
            return;
        }

        if (IsForgeWorker())
        {
            GoForgeWorkOrTrade();
            return;
        }

        if (!autonomousWorkEnabled)
        {
            Wander(NpcText.Action("wanderVillage"));
            return;
        }

        GoWork();
    }

    // Legacy cultivation hooks intentionally disabled for commoners.
    bool TryHandleCultivatorDailyRoutine()
    {
        return false;
    }

    void DoCultivatorActivity()
    {
        return;
    }

    void CultivateNaturally()
    {
        return;
    }

    void ClearCompletedCultivationAction()
    {
        if (actionTimer > 0f)
        {
            return;
        }

        if (currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi"))
        {
            currentAction = "";
            UpdateCultivationEffect(false);
        }

        StopMoving();
    }

    bool IsCultivationCapableVillager()
    {
        return false;
    }

    bool IsScheduledCultivationTime()
    {
        EnsureDailyRoutinePlan();
        float hour = GetCurrentWorldHour();

        if (routineCultivationStartHour <= routineCultivationEndHour)
        {
            return hour >= routineCultivationStartHour &&
                hour < routineCultivationEndHour;
        }

        return hour >= routineCultivationStartHour ||
            hour < routineCultivationEndHour;
    }

    void EnsureDailyRoutinePlan()
    {
        int day = GetRoutineWorldDay();
        if (routinePlanDay == day)
        {
            return;
        }

        routinePlanDay = day;

        float minHours =
            Mathf.Clamp(dailyCultivationMinHours, 0f, 24f);
        float maxHours =
            Mathf.Clamp(
                Mathf.Max(dailyCultivationMaxHours, minHours),
                minHours,
                24f);
        float duration = Random.Range(minHours, maxHours);
        float earliestStart =
            Mathf.Clamp(earliestCultivationHour, 0f, 23.9f);
        float latestStart =
            Mathf.Clamp(latestCultivationStartHour, 0f, 23.9f);

        if (latestStart < earliestStart)
        {
            latestStart = earliestStart;
        }

        routineCultivationStartHour =
            Random.Range(earliestStart, latestStart);
        routineCultivationEndHour =
            Mathf.Repeat(routineCultivationStartHour + duration, 24f);
    }

    int GetRoutineWorldDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : Mathf.FloorToInt(Time.time / 900f) + 1;
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            return timeSystem.CurrentHour;
        }

        return Mathf.Repeat(Time.time * 24f / 900f, 24f);
    }

    float GetRemainingScheduledCultivationSeconds()
    {
        if (!dailyRoutineEnabled)
        {
            return float.PositiveInfinity;
        }

        EnsureDailyRoutinePlan();
        float hour = GetCurrentWorldHour();
        float remainingHours =
            routineCultivationEndHour >= hour
            ? routineCultivationEndHour - hour
            : 24f - hour + routineCultivationEndHour;

        return GameHoursToSeconds(Mathf.Max(0.1f, remainingHours));
    }

    bool IsRoutineTravelOrCultivationAction(string action)
    {
        return IsTravelIntentAction(action) ||
            action == NpcText.Action("visitedTaskProvider");
    }

    bool IsTravelIntentAction(string action)
    {
        return action == NpcText.Action("goTaskProviderDaily") ||
            action == NpcText.Action("goMarketTrade") ||
            action == NpcText.Action("goWork") ||
            action == NpcText.Action("goFarmWork") ||
            action == NpcText.Action("goPatrol") ||
            action == NpcText.Action("goHeal") ||
            action == NpcText.Action("goFish") ||
            action == NpcText.Action("goHunt") ||
            action == NpcText.Action("goTavern") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("goHomeCultivate") ||
            action == NpcText.Action("goCultivatePoint") ||
            action == NpcText.Action("moveToTask") ||
            action == NpcText.Action("receiveTask") ||
            action == NpcText.Action("goPlay") ||
            action == NpcText.Action("goHomeRest") ||
            action == NpcText.Action("stayNearHome") ||
            action == NpcText.Action("restNearHome") ||
            action == NpcText.Action("eveningWalkVillage") ||
            action == NpcText.Action("walkingRoad");
    }

    bool TryGoHomeForCultivation()
    {
        if (IsInDungeonCombatSession())
        {
            return false;
        }

        if (homeRoutineManagedExternally &&
            !HasEnforcedSchedule())
        {
            return false;
        }

        Vector3 homePosition = GetHomePosition();
        if (IsAtPosition(homePosition))
        {
            return false;
        }

        MoveUsingRoad(homePosition);
        currentAction = NpcText.Action("goHomeCultivate");
        return true;
    }

    public long ExpToNextRealm()
    {
        return CultivationProgression.GetExpToNextLong(
            realm,
            realmStage,
            baseExpToNextRealm);
    }

    public void AddCultivationExp(int amount)
    {
        return;
    }

    void Breakthrough()
    {
        return;
    }

    void CompleteMajorBreakthrough(CultivationRealm targetRealm)
    {
        return;
    }
}
