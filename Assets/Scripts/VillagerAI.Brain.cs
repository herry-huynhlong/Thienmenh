using UnityEngine;

public partial class VillagerAI
{
    void Think()
    {
        NpcFixedBlacksmithController fixedBlacksmith =
            GetComponent<NpcFixedBlacksmithController>();
        NpcFixedAlchemistController fixedAlchemist =
            GetComponent<NpcFixedAlchemistController>();
        bool useDedicatedBlacksmithRoutine =
            fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.UseDedicatedRoutine;
        bool useDedicatedAlchemistRoutine =
            fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.UseDedicatedRoutine;

        if (WorldTimeSystem.Instance != null)
        {
            if (WorldTimeSystem.Instance.CurrentDay != lastPlanResetDay)
            {
                lastPlanResetDay = WorldTimeSystem.Instance.CurrentDay;
                ResetDailyTargets();
            }
        }

        if (homeRoutineManagedExternally &&
            !HasEnforcedSchedule() &&
            (WorldTimeSystem.Instance == null ||
            WorldTimeSystem.Instance.CurrentPhase == WorldTimePhase.Night ||
            fatigue >= 85f))
        {
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

        if (actionTimer > 0f &&
            NpcRoleUtility.IsInCombat(gameObject))
        {
            return;
        }

        if (useDedicatedBlacksmithRoutine &&
            fixedBlacksmith.TryRunDedicatedRoutine())
        {
            return;
        }

        if (useDedicatedAlchemistRoutine &&
            fixedAlchemist.TryRunDedicatedRoutine())
        {
            return;
        }

        if (ShouldForceReturnHomeFromSchedule())
        {
            GoHomeToRest();
            return;
        }

        if (TryRunScheduledActivity())
        {
            return;
        }

        if (actionTimer > 0f)
        {
            return;
        }

        if (fatigue >= 85f)
        {
            GoHomeToRest();
            return;
        }

        if (ageGroup == VillagerAgeGroup.Child)
        {
            ThinkChild();
            return;
        }

        ThinkAdult();
    }

    void ThinkChild()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null &&
            (timeSystem.CurrentPhase == WorldTimePhase.Night ||
            timeSystem.CurrentPhase == WorldTimePhase.Dawn))
        {
            GoHomeToRest();
            return;
        }

        if (fun <= 70f)
        {
            GatherAndPlay();
            return;
        }

        GoHomeIdle(NpcText.Action("stayNearHome"));
    }

    void ThinkAdult()
    {
        if (ShouldGoHomeForRest())
        {
            GoHomeToRest();
            return;
        }

        if (!hiddenAtHome &&
            !isReturningHome &&
            IsAtHomePosition(GetHomePosition()) &&
            IsCurrentScheduleActivity(NpcScheduleActivity.Work))
        {
            actionTimer = 0f;
            currentAction = string.Empty;
        }

        if (TryHandleAdultImmediateNeeds())
        {
            return;
        }

        if (dailyRoutineEnabled &&
            IsCultivationCapableVillager() &&
            IsScheduledCultivationTime())
        {
            CultivateNaturally();
            return;
        }

        VillagerJobDispatcher jobDispatcher = EnsureJobDispatcher();
        if (jobDispatcher != null &&
            jobDispatcher.TryHandleAdultThink())
        {
            return;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            switch (timeSystem.CurrentPhase)
            {
                case WorldTimePhase.Dawn:
                    if (fatigue > 35f)
                    {
                        GoHomeToRest();
                        return;
                    }
                    GoWork();
                    return;

                case WorldTimePhase.Morning:
                    GoWork();
                    return;

                case WorldTimePhase.Noon:
                    GoHomeToRest();
                    return;

                case WorldTimePhase.Afternoon:
                    GoWork();
                    return;

                case WorldTimePhase.Evening:
                    if (playPoint != null)
                    {
                        GatherAndPlay();
                        return;
                    }

                    IdleOrGoHome(NpcText.Action("eveningWalkVillage"));
                    return;

                case WorldTimePhase.Night:
                    GoHomeToRest();
                    return;
            }
        }

        if (!autonomousWorkEnabled)
        {
            IdleOrGoHome(NpcText.Action("wanderVillage"));
            return;
        }

        if (ShouldDoMortalWork())
        {
            GoWork();
            return;
        }
    }

    bool ShouldDoMortalWork()
    {
        return true;
    }

    bool IsCurrentScheduleActivity(NpcScheduleActivity activity)
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == activity;
    }

}
