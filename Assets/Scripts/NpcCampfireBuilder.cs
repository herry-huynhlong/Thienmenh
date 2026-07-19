using UnityEngine;

[DisallowMultipleComponent]
public class NpcCampfireBuilder : MonoBehaviour
{
    enum BuildStage
    {
        None,
        Moving,
        Building
    }

    [Header("Prefab")]
    public GameObject campfirePrefab;
    public Vector3 spawnOffset;

    [Header("Build")]
    [Min(0.05f)] public float arrivalDistance = 0.35f;
    [Min(0.05f)] public float buildDurationWorldHours = 0.2f;
    [Min(0f)] public float blockExistingCampfireRadius = 0.75f;
    public bool useRoadTravel = true;

    [Header("Action Text")]
    public string moveActionText = "Go build campfire";
    public string buildActionText = "Build campfire";

    VillagerAI villager;
    SmartNpcAI smartNpc;
    BuildStage stage;
    GameObject activeCampfirePrefab;
    Vector3 targetPosition;
    NpcMapZone? targetZone;
    float buildTimerSeconds;

    public bool IsBuilding => stage != BuildStage.None;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
        smartNpc = GetComponent<SmartNpcAI>();
    }

    void Update()
    {
        if (stage == BuildStage.None)
        {
            return;
        }

        if (NpcRoleUtility.IsDead(gameObject))
        {
            CancelBuild();
            return;
        }

        switch (stage)
        {
            case BuildStage.Moving:
                UpdateMoveStage();
                break;

            case BuildStage.Building:
                UpdateBuildStage();
                break;
        }
    }

    public bool BeginBuildAt(Vector3 position)
    {
        return BeginBuildAt(campfirePrefab, position, null);
    }

    public bool BeginBuildAt(
        Vector3 position,
        NpcMapZone? zone)
    {
        return BeginBuildAt(campfirePrefab, position, zone);
    }

    public bool BeginBuildAt(
        GameObject prefab,
        Vector3 position,
        NpcMapZone? zone = null)
    {
        if (prefab == null)
        {
            return false;
        }

        if (NpcRoleUtility.IsDead(gameObject))
        {
            return false;
        }

        activeCampfirePrefab = prefab;
        targetPosition = position;
        targetZone = zone;
        buildTimerSeconds = 0f;
        stage = BuildStage.Moving;
        return true;
    }

    public void CancelBuild()
    {
        activeCampfirePrefab = null;
        buildTimerSeconds = 0f;
        stage = BuildStage.None;
    }

    void UpdateMoveStage()
    {
        if (activeCampfirePrefab == null)
        {
            CancelBuild();
            return;
        }

        if (villager != null)
        {
            villager.ForceJobMoveTo(
                targetPosition,
                moveActionText,
                targetZone,
                useRoadTravel);
        }
        else if (smartNpc != null)
        {
            NpcRoleUtility.SetAction(gameObject, moveActionText);
        }

        if (Vector2.Distance(transform.position, targetPosition) > arrivalDistance)
        {
            return;
        }

        buildTimerSeconds = GameTime.WorldHoursToScaledSeconds(buildDurationWorldHours);
        NpcRoleUtility.StopForConversation(gameObject, buildTimerSeconds);

        if (villager != null)
        {
            villager.SetActionImmediate(buildActionText, buildTimerSeconds);
        }
        else if (smartNpc != null)
        {
            smartNpc.SetActionImmediate(buildActionText, buildTimerSeconds);
        }
        else
        {
            NpcRoleUtility.SetAction(gameObject, buildActionText);
        }

        stage = BuildStage.Building;
    }

    void UpdateBuildStage()
    {
        NpcRoleUtility.StopForConversation(gameObject, 0.25f);
        buildTimerSeconds -= Time.deltaTime;

        if (buildTimerSeconds > 0f)
        {
            return;
        }

        SpawnCampfire();
        CancelBuild();
    }

    void SpawnCampfire()
    {
        if (activeCampfirePrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = targetPosition + spawnOffset;
        if (blockExistingCampfireRadius > 0f &&
            WorldCampfire.IsCampfireNear(
                spawnPosition,
                blockExistingCampfireRadius))
        {
            return;
        }

        GameObject spawned = Instantiate(
            activeCampfirePrefab,
            spawnPosition,
            Quaternion.identity);

        WorldCampfire campfire = spawned.GetComponent<WorldCampfire>();
        if (campfire != null)
        {
            campfire.BeginNow();
        }
    }
}
