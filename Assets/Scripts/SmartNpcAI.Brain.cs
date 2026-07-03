using UnityEngine;

public partial class SmartNpcAI
{
    void ThinkBrainCore()
    {
        if (runtimeTraceEveryThink)
        {
            TraceRuntime("ThinkBrainCore", "enter");
        }

        if (IsDead)
        {
            TraceBranch("ThinkBrainCore", "IsDead", true);
            Die();
            return;
        }

        if (currentHP <= 0)
        {
            TraceBranch("ThinkBrainCore", "HP<=0", true);
            Die();
            return;
        }

        if (ShouldDieFromOldAge())
        {
            TraceBranch("ThinkBrainCore", "OldAgeDeath", true);
            currentAction = NpcText.Action("oldAgeDeath");
            Die();
            return;
        }

        if (IsLockedRoutineAction(currentAction))
        {
            TraceBranch("ThinkBrainCore", "LockedRoutineAction", true);
            DebugFlow("ThinkLocked", "Locked by current action");
            return;
        }

        if (!IsCurrentScheduleActivity(NpcScheduleActivity.Hunt))
        {
            ClearActiveHuntFlow();
        }

        if (TryHandleCombatSupport())
        {
            TraceBranch("ThinkBrainCore", "TryHandleCombatSupport", true);
            DebugFlow("ThinkHunt", "Combat support or retreat");
            return;
        }

        if (TryHandleSmartTaskOverride())
        {
            TraceBranch("ThinkBrainCore", "TryHandleSmartTaskOverride", true);
            DebugFlow(
                "ThinkTask",
                "Emergency task " + currentSmartTask.goal);
            return;
        }

        if (currentMonsterTarget != null)
        {
            TraceBranch("ThinkBrainCore", "ActiveMonsterTarget", true);
            DebugFlow(
                "ThinkHunt",
                "Active monster target " + currentMonsterTarget.monsterName);
            SearchMonster();
            return;
        }

        if (TryRunScheduledActivity())
        {
            TraceBranch("ThinkBrainCore", "TryRunScheduledActivity", true);
            DebugFlow("ThinkSchedule", "Handled by schedule");
            return;
        }

        if (HasActiveHuntTravelIntent())
        {
            if (canFight)
            {
                SearchMonster();
            }

            TraceBranch("ThinkBrainCore", "HasActiveHuntTravelIntent", true);
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
                TraceBranch("ThinkBrainCore", "EveningSocial", true);
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
            TraceBranch("ThinkBrainCore", "DenseSpiritualQi", true);
            DebugFlow("ThinkWeather", "Dense spiritual qi");
            return;
        }

        if (canLive &&
            NeedsFood() &&
            hunger >= 80)
        {
            Eat();
            TraceBranch("ThinkBrainCore", "NeedFood", true);
            DebugFlow("ThinkNeed", "Need food");
            return;
        }

        if (canLive &&
            !IgnoresMortalNeeds() &&
            fatigue >= 85)
        {
            Sleep();
            TraceBranch("ThinkBrainCore", "NeedRest", true);
            DebugFlow("ThinkNeed", "Need rest");
            return;
        }

        if (dailyRoutineEnabled &&
            !HasActiveEnforcedScheduleSlot() &&
            CanVisitTaskProviderToday() &&
            TryVisitTaskProvider())
        {
            TraceBranch("ThinkBrainCore", "TryVisitTaskProvider", true);
            DebugFlow("ThinkTask", "Daily task visit");
            return;
        }

        if (dailyRoutineEnabled &&
            !HasActiveEnforcedScheduleSlot() &&
            canCultivate &&
            IsScheduledCultivationTime())
        {
            CultivateNaturally();
            TraceBranch("ThinkBrainCore", "ScheduledCultivation", true);
            DebugFlow("ThinkCultivate", "Scheduled cultivation");
            return;
        }

        if (canCultivate &&
            (HasAvailablePills() || spiritStone > 0) &&
            CanUseScheduledCultivation())
        {
            Cultivate();
            TraceBranch("ThinkBrainCore", "ConsumePillOrSpiritStone", true);
            DebugFlow("ThinkCultivate", "Consume pill or spirit stone");
            return;
        }

        if (dailyRoutineEnabled &&
            !HasActiveEnforcedScheduleSlot() &&
            TryStartScheduledNonCultivationActivity())
        {
            TraceBranch("ThinkBrainCore", "TryStartScheduledNonCultivationActivity", true);
            DebugFlow("ThinkRoutine", "Scheduled non-cultivation");
            return;
        }

        if (autonomousActivitiesEnabled &&
            canTrade &&
            money >= 50 &&
            !HasAvailablePills())
        {
            if (!GoToTavernAndBuyPill())
            {
                StartIdleWander();
            }
            TraceBranch("ThinkBrainCore", "AutonomousPillPurchase", true);
            DebugFlow("ThinkTrade", "Autonomous pill purchase");
            return;
        }

        if (autonomousActivitiesEnabled &&
            canFight &&
            canCompeteResource)
        {
            SearchMonster();
            TraceBranch("ThinkBrainCore", "AutonomousMonsterSearch", true);
            DebugFlow("ThinkHunt", "Autonomous monster search");
            return;
        }

        if (autonomousActivitiesEnabled &&
            canMakeFriends)
        {
            MakeFriend();
            TraceBranch("ThinkBrainCore", "AutonomousSocial", true);
            DebugFlow("ThinkSocial", "Autonomous social");
            return;
        }

        if (autonomousActivitiesEnabled &&
            canCreateSect)
        {
            TryCreateSect();
            TraceBranch("ThinkBrainCore", "AutonomousSectCreation", true);
            DebugFlow("ThinkSect", "Autonomous sect creation");
            return;
        }

        if (!HasActiveEnforcedScheduleSlot() &&
            TryStartScheduledNonCultivationActivity())
        {
            TraceBranch("ThinkBrainCore", "TryStartScheduledNonCultivationActivityFallback", true);
            DebugFlow("ThinkFallback", "Fallback routine");
            return;
        }

        StartIdleWander();
        TraceBranch("ThinkBrainCore", "StartIdleWander", true);
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
