using UnityEngine;

public static class NpcRoleUtility
{
    public static string GetDisplayName(GameObject npc)
    {
        if (npc == null)
        {
            return "Khong ro";
        }

        EntityProfile profile = npc.GetComponent<EntityProfile>();
        if (profile != null &&
            profile.identity != null &&
            !string.IsNullOrEmpty(profile.identity.entityName))
        {
            return profile.identity.entityName;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        return npc.name;
    }

    public static int GetRealmPower(GameObject npc)
    {
        if (npc == null)
        {
            return 0;
        }

        CharacterStats stats = npc.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return CultivationProgression.GetRealmPower(
                stats.realm,
                stats.realmStage);
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return CultivationProgression.GetRealmPower(
                villager.realm,
                villager.realmStage);
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.GetRealmPower();
        }

        return 0;
    }

    public static bool MeetsRealm(
        GameObject npc,
        CultivationRealm minRealm,
        int minStage)
    {
        if (npc == null)
        {
            return false;
        }

        return GetRealmPower(npc) >=
            CultivationProgression.GetRealmPower(
                minRealm,
                Mathf.Clamp(minStage, 1, CultivationProgression.MaxStage));
    }

    public static bool IsDead(GameObject npc)
    {
        if (npc == null)
        {
            return true;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.IsDead;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.IsDead;
        }

        CharacterStats stats = npc.GetComponent<CharacterStats>();
        return stats != null && stats.IsDead;
    }

    public static int GetAttack(GameObject npc)
    {
        if (npc == null)
        {
            return 1;
        }

        CharacterStats stats = npc.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return Mathf.Max(1, stats.attack);
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return Mathf.Max(1, villager.attack);
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return Mathf.Max(1, smartNpc.attack);
        }

        return 1;
    }

    public static float GetMoveSpeed(GameObject npc, float fallback)
    {
        if (npc == null)
        {
            return fallback;
        }

        CharacterStats stats = npc.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return Mathf.Max(0.1f, stats.moveSpeed);
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return Mathf.Max(0.1f, villager.moveSpeed);
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return Mathf.Max(0.1f, smartNpc.moveSpeed);
        }

        return fallback;
    }

    public static void SetAction(GameObject npc, string action)
    {
        if (npc == null ||
            string.IsNullOrEmpty(action))
        {
            return;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.currentAction = action;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.currentAction = action;
        }
    }

    public static void MoveTowards(
        GameObject npc,
        Vector3 target,
        float fallbackSpeed)
    {
        if (npc == null)
        {
            return;
        }

        float speed = GetMoveSpeed(npc, fallbackSpeed);
        npc.transform.position =
            Vector3.MoveTowards(
                npc.transform.position,
                target,
                speed * Time.deltaTime);
    }

    public static void AddCultivationExp(GameObject npc, int amount)
    {
        if (npc == null ||
            amount <= 0)
        {
            return;
        }

        CharacterStats stats = npc.GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.AddCultivationExp(amount);
            return;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.AddCultivationExp(amount);
        }
    }

    public static void Damage(GameObject target, int amount)
    {
        if (target == null ||
            amount <= 0)
        {
            return;
        }

        VillagerAI villager = target.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.TakeDamage(amount);
            return;
        }

        SmartNpcAI smartNpc = target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.TakeDamage(amount);
            return;
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();
        if (monster != null)
        {
            monster.TakeDamage(amount);
            return;
        }

        CharacterStats stats = target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.TakeDamage(amount);
        }
    }
}
