using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class VillagerHuntDangerResponder : MonoBehaviour
{
    [Header("Danger Response")]
    public bool avoidDangerousMonsters = true;
    [Range(0f, 1f)] public float minimumEngageHpRatio = 0.55f;
    [Range(0f, 1f)] public float retreatHpRatio = 0.22f;
    public float riskyThreatRatioLimit = 1.1f;
    public float nearbyMonsterThreatRadius = 2.5f;
    public int maxNearbyMonstersBeforeRetreat = 1;

    VillagerAI villager;

    void Awake()
    {
        villager = GetComponent<VillagerAI>();
    }

    public bool IsPreferredTarget(MonsterAI monster)
    {
        return IsValidMonster(monster) &&
            IsSafeToEngage(monster);
    }

    public bool ShouldRetreatFrom(MonsterAI monster)
    {
        if (monster == null)
        {
            return false;
        }

        float hpRatio =
            CombatPowerUtility.GetCurrentHpRatio(gameObject);
        if (hpRatio <= retreatHpRatio)
        {
            return true;
        }

        if (CombatPowerUtility.ShouldRetreat(
                gameObject,
                monster.gameObject))
        {
            return true;
        }

        if (CountNearbyThreatMonsters(monster) >
            maxNearbyMonstersBeforeRetreat)
        {
            return true;
        }

        return !IsSafeToEngage(monster);
    }

    public void ReactToDanger(HunterJob hunterJob, MonsterAI monster)
    {
        if (hunterJob == null)
        {
            return;
        }

        hunterJob.ReleaseDangerousTarget(monster);

        if (villager != null)
        {
            villager.SetActionImmediate(
                NpcText.Action("panicBurned"),
                0.75f);
            villager.GoHomeToRest();
        }
    }

    bool IsSafeToEngage(MonsterAI monster)
    {
        if (!avoidDangerousMonsters ||
            monster == null)
        {
            return true;
        }

        float hpRatio =
            CombatPowerUtility.GetCurrentHpRatio(gameObject);
        if (hpRatio < minimumEngageHpRatio)
        {
            return false;
        }

        int nearbyMonsterCount =
            CountNearbyThreatMonsters(monster);
        if (nearbyMonsterCount > maxNearbyMonstersBeforeRetreat)
        {
            return false;
        }

        if (villager == null)
        {
            return true;
        }

        float threatRatio =
            CombatPowerUtility.GetThreatRatio(
                gameObject,
                monster.gameObject);

        if (!villager.autonomousDangerousWorkEnabled)
        {
            return CombatPowerUtility.ShouldFight(
                gameObject,
                monster.gameObject);
        }

        return threatRatio <= riskyThreatRatioLimit;
    }

    int CountNearbyThreatMonsters(MonsterAI primaryMonster)
    {
        if (primaryMonster == null)
        {
            return 0;
        }

        MonsterAI[] monsters = FindObjectsByType<MonsterAI>(
            FindObjectsInactive.Exclude);
        int count = 0;
        Vector3 center = primaryMonster.transform.position;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterAI candidate = monsters[i];
            if (!IsValidMonster(candidate) ||
                candidate == primaryMonster)
            {
                continue;
            }

            float distance = Vector2.Distance(
                center,
                candidate.transform.position);
            if (distance <= nearbyMonsterThreatRadius)
            {
                count++;
            }
        }

        return count;
    }

    bool IsValidMonster(MonsterAI monster)
    {
        return monster != null &&
            monster.gameObject.activeInHierarchy &&
            !monster.IsDead;
    }
}
