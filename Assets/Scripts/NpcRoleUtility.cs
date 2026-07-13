using UnityEngine;

public static class NpcRoleUtility
{
    static VillagerAI GetVillagerAI(GameObject npc)
    {
        return npc != null
            ? npc.GetComponent<VillagerAI>()
            : null;
    }

    static SmartNpcAI GetSmartNpcAI(GameObject npc)
    {
        return npc != null
            ? npc.GetComponent<SmartNpcAI>()
            : null;
    }

    static VillagerAI GetActiveVillagerAI(GameObject npc)
    {
        VillagerAI villager = GetVillagerAI(npc);
        return villager != null && villager.enabled
            ? villager
            : null;
    }

    static SmartNpcAI GetActiveSmartNpcAI(GameObject npc)
    {
        SmartNpcAI smartNpc = GetSmartNpcAI(npc);
        return smartNpc != null && smartNpc.enabled
            ? smartNpc
            : null;
    }

    public static bool IsCommoner(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        if (GetActiveVillagerAI(npc) != null)
        {
            return true;
        }

        return GetVillagerAI(npc) != null &&
            GetSmartNpcAI(npc) == null;
    }

    public static bool IsCultivator(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        if (GetActiveSmartNpcAI(npc) != null)
        {
            return true;
        }

        return GetSmartNpcAI(npc) != null &&
            GetVillagerAI(npc) == null;
    }

    public static string GetRoleLabel(GameObject npc)
    {
        if (IsCommoner(npc))
        {
            return NpcText.Get("entityTypes", "commoner");
        }

        if (IsCultivator(npc))
        {
            return NpcText.Get("entityTypes", "cultivator");
        }

        return NpcText.Get("entityTypes", "unknown");
    }

    public static string GetDisplayName(GameObject npc)
    {
        if (npc == null)
        {
            return NpcText.Get("entityTypes", "unknown");
        }

        MonsterAI monster = npc.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return monster.monsterName;
        }

        EntityProfile profile = npc.GetComponent<EntityProfile>();
        if (profile != null &&
            profile.identity != null &&
            !string.IsNullOrEmpty(profile.identity.entityName))
        {
            return profile.identity.entityName;
        }

        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null)
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        string petName = NpcPetCompanion.GetDisplayName(npc);
        if (!string.IsNullOrEmpty(petName))
        {
            return petName;
        }

        return npc.name;
    }

    public static bool IsPetCompanion(GameObject npc)
    {
        return NpcPetCompanion.HasPetCompanion(npc);
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

        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null)
        {
            return CultivationProgression.GetRealmPower(
                villager.realm,
                villager.realmStage);
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
        if (smartNpc != null)
        {
            return CultivationProgression.GetRealmPower(
                smartNpc.realm,
                smartNpc.realmStage);
        }

        MonsterAI monster = npc.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return CultivationProgression.GetRealmPower(
                monster.realm,
                monster.realmStage);
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

        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null)
        {
            return villager.IsDead;
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
        if (smartNpc != null)
        {
            return smartNpc.IsDead;
        }

        CharacterStats stats = npc.GetComponent<CharacterStats>();
        return stats != null && stats.IsDead;
    }

    public static bool IsInCombat(GameObject npc)
    {
        if (npc == null || IsDead(npc))
        {
            return false;
        }

        string action = GetCurrentAction(npc);
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        return ContainsActionPattern(action, "fight") ||
            ContainsActionPattern(action, "huntMonster") ||
            ContainsActionPattern(action, "huntMonsterNamed") ||
            ContainsActionPattern(action, "attackMonsterNamed") ||
            ContainsActionPattern(action, "treasureHunt") ||
            ContainsActionPattern(action, "treasureHuntNamed") ||
            ContainsActionPattern(action, "outerSkirmish") ||
            ContainsActionPattern(action, "outerSkirmishNamed") ||
            ContainsActionPattern(action, "waitLightning") ||
            ContainsActionPattern(action, "waitLightningNamed") ||
            ContainsActionPattern(action, "rob") ||
            ContainsActionPattern(action, "revenge") ||
            ContainsActionPattern(action, "injured") ||
            ContainsActionPattern(action, "panicBurned") ||
            ContainsLiteral(action, "Tan cong") ||
            ContainsLiteral(action, "Duoi ke") ||
            ContainsLiteral(action, "Phat hien") ||
            ContainsLiteral(action, "Bo chay") ||
            ContainsLiteral(action, "Hon chien") ||
            ContainsLiteral(action, "Phat cuong") ||
            ContainsLiteral(action, "Canh giu") ||
            ContainsLiteral(action, "Ran minh") ||
            ContainsLiteral(action, "Doi thien loi");
    }

    static string GetCurrentAction(GameObject npc)
    {
        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null &&
            !string.IsNullOrWhiteSpace(villager.currentAction))
        {
            return villager.currentAction;
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
        if (smartNpc != null &&
            !string.IsNullOrWhiteSpace(smartNpc.currentAction))
        {
            return smartNpc.currentAction;
        }

        MonsterAI monster = npc.GetComponent<MonsterAI>();
        if (monster != null &&
            !string.IsNullOrWhiteSpace(monster.currentAction))
        {
            return monster.currentAction;
        }

        NpcMapMover2D mapMover = npc.GetComponent<NpcMapMover2D>();
        if (mapMover != null &&
            !string.IsNullOrWhiteSpace(mapMover.currentAction))
        {
            return mapMover.currentAction;
        }

        NpcData npcData = npc.GetComponent<NpcData>();
        if (npcData != null &&
            !string.IsNullOrWhiteSpace(npcData.currentAction))
        {
            return npcData.currentAction;
        }

        return "";
    }

    static bool ContainsActionPattern(string action, string key)
    {
        string pattern = NpcText.Get("actions", key, "");
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        int placeholderIndex = pattern.IndexOf('{');
        if (placeholderIndex >= 0)
        {
            pattern = pattern.Substring(0, placeholderIndex).Trim();
        }

        return ContainsLiteral(action, pattern);
    }

    static bool ContainsLiteral(string action, string pattern)
    {
        return !string.IsNullOrEmpty(action) &&
            !string.IsNullOrEmpty(pattern) &&
            action.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0;
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

        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null)
        {
            return Mathf.Max(1, villager.attack);
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
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

        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null)
        {
            return Mathf.Max(0.1f, villager.moveSpeed);
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
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

        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null)
        {
            if (!villager.IsActionLocked)
            {
                villager.currentAction = action;
            }
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
        if (smartNpc != null)
        {
            smartNpc.ForceSetCurrentAction(action);
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

        bool usingTeleportRoute;
        string routeAction;
        NpcRouteStatus routeStatus;
        Vector3 moveTarget =
            NpcMapNavigator.GetNextMoveTarget(
                npc,
                target,
                out usingTeleportRoute,
                out routeAction,
                out routeStatus);

        if ((usingTeleportRoute || IsRouteBlocked(routeStatus)) &&
            CanRouteActionReplaceCurrentAction(npc))
        {
            SetAction(npc, routeAction);
        }

        float speed = GetMoveSpeed(npc, fallbackSpeed);
        npc.transform.position =
            Vector3.MoveTowards(
                npc.transform.position,
                moveTarget,
            speed * Time.deltaTime);
    }

    static bool IsRouteBlocked(NpcRouteStatus routeStatus)
    {
        return routeStatus == NpcRouteStatus.NoGate ||
            routeStatus == NpcRouteStatus.InvalidGate;
    }

    static bool CanRouteActionReplaceCurrentAction(GameObject npc)
    {
        string action = GetCurrentAction(npc);
        if (string.IsNullOrEmpty(action))
        {
            return true;
        }

        string teleportPrefix =
            NpcText.Action("teleportGateTo").Replace("{0}", "");
        return action == NpcText.Action("idle") ||
            action == NpcText.Action("walkingRoad") ||
            action.StartsWith(teleportPrefix) ||
            action.StartsWith("Đi cổng dịch chuyển");
    }

    public static void StopForConversation(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null)
        {
            villager.StopForConversation();
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
        if (smartNpc != null)
        {
            smartNpc.StopForConversation();
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.StopForConversation();
            return;
        }

        Rigidbody2D rb = npc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public static void StopForConversation(GameObject npc, float duration)
    {
        if (npc == null)
        {
            return;
        }

        VillagerAI villager = GetActiveVillagerAI(npc) ??
            GetVillagerAI(npc);
        if (villager != null)
        {
            villager.StopForConversation(duration);
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(npc) ??
            GetSmartNpcAI(npc);
        if (smartNpc != null)
        {
            smartNpc.StopForConversation(duration);
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.StopForConversation(duration);
            return;
        }

        Rigidbody2D rb = npc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
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
    }

    public static void Damage(GameObject target, int amount)
    {
        Damage(null, target, amount, NpcText.Dialogue("attackReasonFallback"));
    }

    public static void Damage(
        GameObject actor,
        GameObject target,
        int amount,
        string reason)
    {
        if (target == null ||
            amount <= 0)
        {
            return;
        }

        if (NpcPetCompanion.BlocksSocialDamage(target))
        {
            return;
        }

        amount = NpcCombatTechniqueSystem.ModifyOutgoingDamage(
            actor,
            target,
            amount);

        if (actor != null &&
            actor != target)
        {
            SetCombatAttackAction(actor, target);

            NpcSocialEventBus.PublishHostility(
                actor,
                target,
                Mathf.Clamp(amount, 1, 100),
                target.transform.position,
                reason);
        }

        VillagerAI villager = GetActiveVillagerAI(target);
        if (villager != null)
        {
            villager.TakeDamage(amount);
            return;
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(target);
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

    public static void SetCombatAttackAction(GameObject actor, GameObject target)
    {
        if (actor == null)
        {
            return;
        }

        string targetName = GetDisplayName(target);
        string attackAction = string.IsNullOrEmpty(targetName)
            ? NpcText.Action("attackMonsterNamed")
            : NpcText.ActionFormat("attackMonsterNamed", targetName);

        VillagerAI villager = GetActiveVillagerAI(actor);
        if (villager != null)
        {
            villager.SetActionImmediate(attackAction, 0.45f);
            return;
        }

        SmartNpcAI smartNpc = GetActiveSmartNpcAI(actor);
        if (smartNpc != null)
        {
            smartNpc.SetActionImmediate(attackAction, 0.45f);
            return;
        }

        MonsterAI monster = actor.GetComponent<MonsterAI>();
        if (monster != null)
        {
            monster.currentAction = attackAction;
        }
    }
}

