using UnityEngine;

[DisallowMultipleComponent]
public class VillageHomeManager : MonoBehaviour
{
    [Header("Home Routine")]
    public bool enableHomeRoutine = true;
    public bool returnVillagersHomeAtNight = true;
    public bool returnVillagersHomeWhenTired = true;
    [Range(0f, 100f)] public float tiredReturnThreshold = 85f;
    public WorldTimePhase leaveHomePhase = WorldTimePhase.Dawn;
    public float checkInterval = 0.5f;

    float nextCheckTime;

    void Update()
    {
        if (!enableHomeRoutine || Time.time < nextCheckTime)
        {
            return;
        }

        nextCheckTime = Time.time + Mathf.Max(0.1f, checkInterval);
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;

        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude);

        for (int i = 0; i < villagers.Length; i++)
        {
            VillagerAI villager = villagers[i];
            if (villager == null ||
                (!villager.homeRoutineManagedExternally &&
                !villager.hideAtHome))
            {
                continue;
            }

            if (villager.IsDead || villager.homePoint == null)
            {
                continue;
            }

            if (villager.IsHiddenAtHome)
            {
                if (ShouldLeaveHome(villager, timeSystem))
                {
                    villager.ForceHiddenAtHome(false);
                }

                continue;
            }

            if (ShouldReturnHome(villager, timeSystem))
            {
                villager.GoHomeToRest();
            }
        }
    }

    bool ShouldReturnHome(VillagerAI villager, WorldTimeSystem timeSystem)
    {
        if (villager == null)
        {
            return false;
        }

        if (returnVillagersHomeWhenTired &&
            villager.fatigue >= tiredReturnThreshold)
        {
            return true;
        }

        return returnVillagersHomeAtNight &&
            timeSystem != null &&
            timeSystem.CurrentPhase == WorldTimePhase.Night;
    }

    bool ShouldLeaveHome(VillagerAI villager, WorldTimeSystem timeSystem)
    {
        if (villager != null)
        {
            NpcScheduleController schedule =
                NpcScheduleController.GetSchedule(villager.gameObject);

            if (schedule != null && schedule.enforceSchedule)
            {
                NpcScheduleActivity activity = schedule.CurrentActivity;
                return activity != NpcScheduleActivity.Sleep &&
                    activity != NpcScheduleActivity.ReturnHome;
            }
        }

        return timeSystem != null &&
            timeSystem.CurrentPhase == leaveHomePhase;
    }
}
