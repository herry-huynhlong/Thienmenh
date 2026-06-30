using UnityEngine;

public partial class SmartNpcAI
{
    void ThinkBrainCore()
    {
        if (IsDead)
        {
            Die();
            return;
        }

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        if (ShouldDieFromOldAge())
        {
            currentAction = NpcText.Action("oldAgeDeath");
            Die();
            return;
        }

        if (IsLockedRoutineAction(currentAction))
        {
            DebugFlow("ThinkLocked", "Locked by current action");
            return;
        }

        if (!IsCurrentScheduleActivity(NpcScheduleActivity.Hunt))
        {
            ClearActiveHuntFlow();
        }

        if (TryHandleCombatSupport())
        {
            DebugFlow("ThinkHunt", "Combat support or retreat");
            return;
        }

        if (TryHandleSmartTaskOverride())
        {
            DebugFlow(
                "ThinkTask",
                "Emergency task " + currentSmartTask.goal);
            return;
        }

        if (TryRunScheduledActivity())
        {
            DebugFlow("ThinkSchedule", "Handled by schedule");
            return;
        }

        if (HasActiveHuntTravelIntent())
        {
            if (canFight)
            {
                SearchMonster();
            }

            DebugFlow("ThinkHunt", "Preserve active hunt travel");
            return;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            if (autonomousActivitiesEnabled &&
                timeSystem.CurrentPhase == WorldTimePhase.Evening &&
                canMakeFriends &&
                kindness + greed < 130)
            {
                MakeFriend();
                DebugFlow("ThinkEvening", "Evening social");
                return;
            }
        }

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null &&
            weather.CurrentWeather == WorldWeather.DenseSpiritualQi &&
            CanUseScheduledCultivation())
        {
            Cultivate();
            DebugFlow("ThinkWeather", "Dense spiritual qi");
            return;
        }

        if (!canCultivate &&
            canLive &&
            NeedsFood() &&
            hunger >= 80)
        {
            Eat();
            DebugFlow("ThinkNeed", "Need food");
            return;
        }

        if (!canCultivate &&
            canLive &&
            !IgnoresMortalNeeds() &&
            fatigue >= 85)
        {
            Sleep();
            DebugFlow("ThinkNeed", "Need rest");
            return;
        }

        if (dailyRoutineEnabled &&
            CanVisitTaskProviderToday() &&
            TryVisitTaskProvider())
        {
            DebugFlow("ThinkTask", "Daily task visit");
            return;
        }

        if (dailyRoutineEnabled &&
            canCultivate &&
            IsScheduledCultivationTime())
        {
            CultivateNaturally();
            DebugFlow("ThinkCultivate", "Scheduled cultivation");
            return;
        }

        if (canCultivate &&
            (pill > 0 || spiritStone > 0) &&
            CanUseScheduledCultivation())
        {
            Cultivate();
            DebugFlow("ThinkCultivate", "Consume pill or spirit stone");
            return;
        }

        if (dailyRoutineEnabled &&
            TryStartScheduledNonCultivationActivity())
        {
            DebugFlow("ThinkRoutine", "Scheduled non-cultivation");
            return;
        }

        if (autonomousActivitiesEnabled &&
            canTrade &&
            money >= 50 &&
            pill <= 0)
        {
            GoToTavernAndBuyPill();
            DebugFlow("ThinkTrade", "Autonomous pill purchase");
            return;
        }

        if (autonomousActivitiesEnabled &&
            canFight &&
            canCompeteResource)
        {
            SearchMonster();
            DebugFlow("ThinkHunt", "Autonomous monster search");
            return;
        }

        if (autonomousActivitiesEnabled &&
            canMakeFriends)
        {
            MakeFriend();
            DebugFlow("ThinkSocial", "Autonomous social");
            return;
        }

        if (autonomousActivitiesEnabled &&
            canCreateSect)
        {
            TryCreateSect();
            DebugFlow("ThinkSect", "Autonomous sect creation");
            return;
        }

        if (TryStartScheduledNonCultivationActivity())
        {
            DebugFlow("ThinkFallback", "Fallback routine");
            return;
        }

        StartIdleWander();
        DebugFlow("ThinkFallback", "Idle instead of cultivation");
        return;
    }

    bool HasActiveHuntTravelIntent()
    {
        if (HasCombatSupportIntent())
        {
            return true;
        }

        bool isHuntOrFreeHunt =
            IsCurrentScheduleActivity(NpcScheduleActivity.Hunt) ||
            IsCurrentScheduleActivity(NpcScheduleActivity.FreeHuntAndGather);

        if (!isHuntOrFreeHunt)
        {
            return false;
        }

        return currentMonsterTarget != null ||
            currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true);
    }

    bool IsCurrentScheduleActivity(NpcScheduleActivity activity)
    {
        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();

        return schedule != null &&
            schedule.enforceSchedule &&
            NpcScheduleController.IsMatchingActivity(
                schedule.CurrentActivity,
                activity);
    }

    void ClearActiveHuntFlow()
    {
        ReleaseMonsterReservation();
        ClearHelpRequestState();
        isRetreatingFromMonster = false;
        retreatUntilTime = 0f;
        retreatTarget = Vector3.zero;

        if (currentMonsterTarget == null &&
            currentAction != NpcText.Action("goHunt") &&
            !MatchesSmartAction("huntMonsterNamed", true) &&
            !MatchesSmartAction("attackMonsterNamed", true))
        {
            return;
        }

        currentMonsterTarget = null;
        currentTarget = null;
        hasWanderTarget = false;
        StopNpcMovement();

        if (currentAction == NpcText.Action("goHunt") ||
            MatchesSmartAction("huntMonsterNamed", true) ||
            MatchesSmartAction("attackMonsterNamed", true))
        {
            currentAction = string.Empty;
        }
    }

    bool CanUseScheduledCultivation()
    {
        return canCultivate &&
            dailyRoutineEnabled &&
            IsScheduledCultivationTime();
    }
}
