using UnityEngine;

public class TribulationTestButton : MonoBehaviour
{
    [Header("Test")]
    public int triggerExpAmount = 1;

    [ContextMenu("Test Major Breakthrough Tribulation")]
    public void TestMajorBreakthroughTribulation()
    {
        CharacterStats stats = GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.realmStage = CultivationProgression.MaxStage;
            stats.cultivationExp = stats.ExpToNextRealm();
            stats.AddCultivationExp(Mathf.Max(1, triggerExpAmount));
            return;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.realmStage = CultivationProgression.MaxStage;
            villager.cultivationExp = villager.ExpToNextRealm();
            villager.AddCultivationExp(Mathf.Max(1, triggerExpAmount));
            return;
        }

        MonsterAI monster = GetComponent<MonsterAI>();
        if (monster != null)
        {
            monster.realmStage = CultivationProgression.MaxStage;
            monster.cultivationExp = monster.ExpToNextRealm();
            monster.AddCultivationExp(Mathf.Max(1, triggerExpAmount));
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.realmStage = CultivationProgression.MaxStage;
            smartNpc.cultivation = smartNpc.breakthroughNeed;
            SendMessage("Breakthrough", SendMessageOptions.DontRequireReceiver);
        }
    }
}
