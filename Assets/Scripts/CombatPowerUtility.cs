using UnityEngine;

public static class CombatPowerUtility
{
    const float HelpHpThreshold = 0.5f;
    const float RetreatHpThreshold = 0.05f;

    public static float GetPower(GameObject actor)
    {
        if (actor == null)
        {
            return 0f;
        }

        float power = Mathf.Max(1f, NpcRoleUtility.GetRealmPower(actor));

        CharacterStats characterStats = actor.GetComponent<CharacterStats>();
        if (characterStats != null)
        {
            power = Mathf.Max(
                power,
                characterStats.currentHP * 0.15f +
                characterStats.finalHP * 0.25f);
        }

        SmartNpcAI smartNpc = actor.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            power +=
                smartNpc.attack * 3f +
                smartNpc.defense * 2f +
                smartNpc.maxHP * 0.3f +
                smartNpc.currentHP * 0.15f +
                smartNpc.comprehension * 1.5f;
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager != null)
        {
            power +=
                villager.attack * 3f +
                villager.defense * 2f +
                villager.maxHP * 0.3f +
                villager.currentHP * 0.15f;
        }

        MonsterAI monster = actor.GetComponent<MonsterAI>();
        if (monster != null)
        {
            power +=
                monster.damage * 3f +
                monster.defense * 2f +
                monster.maxHP * 0.3f +
                monster.currentHP * 0.15f +
                monster.beastLevel * 20f;
        }

        return Mathf.Max(1f, power);
    }

    public static float GetThreatRatio(GameObject npc, GameObject monster)
    {
        float npcPower = Mathf.Max(1f, GetPower(npc));
        float monsterPower = Mathf.Max(1f, GetPower(monster));
        return monsterPower / npcPower;
    }

    public static bool ShouldFight(GameObject npc, GameObject monster)
    {
        float npcPower = Mathf.Max(1f, GetPower(npc));
        float monsterPower = Mathf.Max(1f, GetPower(monster));
        float npcHpRatio = GetCurrentHpRatio(npc);

        if (npcHpRatio <= 0.35f)
        {
            return false;
        }

        return npcPower >= monsterPower * 0.85f;
    }

    public static bool ShouldRequestHelp(GameObject npc, GameObject monster)
    {
        float npcPower = Mathf.Max(1f, GetPower(npc));
        float monsterPower = Mathf.Max(1f, GetPower(monster));
        float npcHpRatio = GetCurrentHpRatio(npc);

        if (npcHpRatio <= RetreatHpThreshold)
        {
            return false;
        }

        if (npcHpRatio > HelpHpThreshold)
        {
            return false;
        }

        return npcPower < monsterPower * 0.85f &&
            npcPower >= monsterPower * 0.45f;
    }

    public static bool ShouldRetreat(GameObject npc, GameObject monster)
    {
        float npcHpRatio = GetCurrentHpRatio(npc);
        return npcHpRatio <= RetreatHpThreshold;
    }

    public static float EstimateWinChance(
        float allyPower,
        GameObject monster)
    {
        float monsterPower = Mathf.Max(1f, GetPower(monster));
        float ratio = Mathf.Max(0f, allyPower) /
            Mathf.Max(1f, allyPower + monsterPower);
        return Mathf.Clamp01(ratio);
    }

    public static bool ShouldTeamFight(
        float allyPower,
        GameObject monster,
        float requiredWinChance = 0.8f)
    {
        return EstimateWinChance(allyPower, monster) >=
            Mathf.Clamp01(requiredWinChance);
    }

    public static string DescribeNpcVsMonster(GameObject npc, GameObject monster)
    {
        float npcPower = Mathf.Max(1f, GetPower(npc));
        float monsterPower = Mathf.Max(1f, GetPower(monster));
        float npcHpRatio = GetCurrentHpRatio(npc);
        float threatRatio = monsterPower / Mathf.Max(1f, npcPower);
        bool lowHpRetreat = npcHpRatio <= RetreatHpThreshold;
        bool heavilyOutmatched = npcPower < monsterPower * 0.45f;
        bool doubledThreat = monsterPower >= npcPower * 2f;
        bool requestHelp =
            npcHpRatio > RetreatHpThreshold &&
            npcHpRatio <= HelpHpThreshold &&
            npcPower < monsterPower * 0.85f &&
            npcPower >= monsterPower * 0.45f;
        bool canFight =
            npcHpRatio > 0.35f &&
            npcPower >= monsterPower * 0.85f;

        return "npcPower=" + npcPower.ToString("0.0") +
            " monsterPower=" + monsterPower.ToString("0.0") +
            " threatRatio=" + threatRatio.ToString("0.00") +
            " npcHpRatio=" + npcHpRatio.ToString("0.00") +
            " canFight=" + canFight +
            " requestHelp=" + requestHelp +
            " retreatLowHp=" + lowHpRetreat +
            " heavilyOutmatched=" + heavilyOutmatched +
            " doubledThreat=" + doubledThreat;
    }

    public static float GetCurrentHpRatio(GameObject actor)
    {
        if (actor == null)
        {
            return 0f;
        }

        CharacterStats characterStats = actor.GetComponent<CharacterStats>();
        if (characterStats != null && characterStats.finalHP > 0)
        {
            return Mathf.Clamp01(
                (float)Mathf.Max(0, characterStats.currentHP) /
                Mathf.Max(1, characterStats.finalHP));
        }

        SmartNpcAI smartNpc = actor.GetComponent<SmartNpcAI>();
        if (smartNpc != null && smartNpc.maxHP > 0)
        {
            return Mathf.Clamp01(
                (float)Mathf.Max(0, smartNpc.currentHP) /
                Mathf.Max(1, smartNpc.maxHP));
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager != null && villager.maxHP > 0)
        {
            return Mathf.Clamp01(
                (float)Mathf.Max(0, villager.currentHP) /
                Mathf.Max(1, villager.maxHP));
        }

        MonsterAI monster = actor.GetComponent<MonsterAI>();
        if (monster != null && monster.maxHP > 0)
        {
            return Mathf.Clamp01(
                (float)Mathf.Max(0, monster.currentHP) /
                Mathf.Max(1, monster.maxHP));
        }

        return 1f;
    }
}
