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

        if (TryRunScheduledActivity())
        {
            DebugFlow("ThinkSchedule", "Handled by schedule");
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
            canCultivate)
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
            dailyTaskVisitEnabled &&
            Random.value < dailyTaskVisitChance &&
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
            (pill > 0 || spiritStone > 0))
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
            canFight)
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

        if (canCultivate)
        {
            CultivateNaturally();
            DebugFlow("ThinkFallback", "Fallback cultivation");
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        currentAction = "";
        DebugFlow("ThinkIdle", "No branch selected");
    }
}
