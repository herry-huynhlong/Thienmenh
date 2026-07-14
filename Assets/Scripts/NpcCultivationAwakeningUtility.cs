using UnityEngine;

public static class NpcCultivationAwakeningUtility
{
    public static bool TryConvertVillagerToSmartNpc(
        GameObject target,
        CultivationRealm startRealm,
        int startStage)
    {
        if (target == null)
        {
            return false;
        }

        SmartNpcAI activeSmartNpc =
            target.GetComponent<SmartNpcAI>();

        if (activeSmartNpc != null &&
            activeSmartNpc.enabled)
        {
            return false;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager == null ||
            !villager.enabled ||
            villager.IsDead)
        {
            return false;
        }

        startRealm =
            startRealm == CultivationRealm.Mortal
                ? CultivationRealm.QiRefining
                : startRealm;

        startStage =
            Mathf.Clamp(
                startStage,
                1,
                CultivationProgression.MaxStage);

        villager.StopMoving();
        villager.currentAction = NpcText.Action("breakthrough");

        Transform homePoint = villager.homePoint;
        Transform workPoint = villager.workPoint;
        float preservedMoveSpeed = Mathf.Max(0.1f, villager.moveSpeed);
        int preservedSpiritStone = Mathf.Max(0, villager.spiritStone);
        int preservedMoney = Mathf.Max(0, villager.money);
        int preservedCurrentHp = Mathf.Max(1, villager.currentHP);
        int preservedBaseMaxHp = Mathf.Max(1, villager.baseMaxHP);
        int preservedBaseAttack = Mathf.Max(1, villager.baseAttack);
        int preservedBaseDefense = Mathf.Max(0, villager.baseDefense);
        int preservedLifespan = Mathf.Max(80, villager.lifespan);

        EntityProfile profile =
            target.GetComponent<EntityProfile>();

        if (profile == null)
        {
            profile =
                target.AddComponent<EntityProfile>();
        }

        profile.generateOnAwake = false;
        profile.lockGeneratedValues = true;
        profile.kind = EntityKind.Cultivator;
        profile.currentGoal = EntityGoal.Cultivate;

        if (profile.identity == null)
        {
            profile.identity = new EntityIdentity();
        }

        if (profile.stats == null)
        {
            profile.stats = new EntityStats();
        }

        if (profile.talent == null)
        {
            profile.talent = new EntityTalent();
        }

        if (profile.personality == null)
        {
            profile.personality = new EntityPersonality();
        }

        if (profile.needs == null)
        {
            profile.needs = new EntityNeeds();
        }

        if (!string.IsNullOrWhiteSpace(villager.villagerName))
        {
            profile.identity.entityName = villager.villagerName;
        }

        if (string.IsNullOrWhiteSpace(profile.identity.entityName))
        {
            profile.identity.entityName = target.name;
        }

        profile.identity.kind = EntityKind.Cultivator;
        int currentAge = NpcAgeUtility.GetCurrentAge(profile.identity);
        if (currentAge < 14)
        {
            NpcAgeUtility.SetCurrentAge(profile.identity, 14);
        }

        if (profile.talent.comprehension <= 0)
        {
            profile.talent.comprehension = 10;
        }

        if (profile.talent.cultivationSpeed <= 0f)
        {
            profile.talent.cultivationSpeed = 1f;
        }

        if (profile.talent.combatMultiplier <= 0f)
        {
            profile.talent.combatMultiplier = 1f;
        }

        profile.personality.greed = villager.greed;
        profile.personality.bravery = villager.bravery;
        profile.personality.sociability = villager.sociability;
        profile.personality.diligence = villager.diligence;
        profile.personality.cultivationDesire =
            Mathf.Max(
                profile.personality.cultivationDesire,
                55);

        profile.needs.hunger = villager.hunger;
        profile.needs.fatigue = villager.fatigue;
        profile.needs.cultivationNeed =
            Mathf.Max(
                profile.needs.cultivationNeed,
                35f);

        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats == null)
        {
            characterStats =
                target.AddComponent<CharacterStats>();
        }

        characterStats.generateFromEntityProfile = true;
        characterStats.generatedEntityKind = EntityKind.Cultivator;
        characterStats.entityProfile = profile;
        characterStats.realm = startRealm;
        characterStats.realmStage = startStage;
        characterStats.cultivationExp = 0;
        characterStats.waitingForHeavenlyTribulation = false;
        characterStats.baseMaxHP = preservedBaseMaxHp;
        characterStats.baseAttack = preservedBaseAttack;
        characterStats.baseDefense = preservedBaseDefense;
        characterStats.baseMoveSpeed = preservedMoveSpeed;
        characterStats.bonusMaxHP = 0;
        characterStats.bonusAttack = 0;
        characterStats.bonusDefense = 0;
        characterStats.bonusEffectResistance = 0;
        characterStats.bonusMoveSpeed = 0f;

        float realmPower =
            CultivationProgression.GetStatPower(
                startRealm,
                startStage,
                EntityKind.Cultivator);

        int finalHp =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    Mathf.Max(1, preservedBaseMaxHp) *
                    realmPower));

        int finalAttack =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    Mathf.Max(1, preservedBaseAttack) *
                    realmPower));

        int finalDefense =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    Mathf.Max(0, preservedBaseDefense) *
                    realmPower));

        profile.stats.realm = startRealm;
        profile.stats.realmStage = startStage;
        profile.stats.cultivationExp = 0;
        profile.stats.maxHP = finalHp;
        profile.stats.currentHP = finalHp;
        profile.stats.attack = finalAttack;
        profile.stats.defense = finalDefense;
        profile.stats.effectResistance = 0;
        profile.stats.moveSpeed = preservedMoveSpeed;
        profile.stats.money = preservedMoney;
        profile.stats.spiritStone = preservedSpiritStone;

        characterStats.ApplyEntityProfile();
        characterStats.RecalculateStats(true);

        profile.stats.currentHP = characterStats.currentHP;
        profile.stats.maxHP = characterStats.finalHP;
        profile.stats.attack = characterStats.attack;
        profile.stats.defense = characterStats.defense;
        profile.stats.moveSpeed = characterStats.moveSpeed;

        SmartNpcAI smartNpc = activeSmartNpc;
        if (smartNpc == null)
        {
            smartNpc =
                target.AddComponent<SmartNpcAI>();
        }

        smartNpc.enabled = false;
        smartNpc.generateFromEntityProfile = true;
        smartNpc.refreshGeneratedProfileOnStart = false;
        smartNpc.entityProfile = profile;
        smartNpc.characterStats = characterStats;
        smartNpc.npcName = profile.identity.entityName;
        smartNpc.canCultivate = true;
        smartNpc.realm = startRealm;
        smartNpc.realmStage = startStage;
        smartNpc.homePoint = homePoint;
        smartNpc.cultivationPoint =
            smartNpc.cultivationPoint != null
                ? smartNpc.cultivationPoint
                : homePoint;
        smartNpc.farmPoint =
            smartNpc.farmPoint != null
                ? smartNpc.farmPoint
                : workPoint;
        smartNpc.moveSpeed = characterStats.moveSpeed;
        smartNpc.money = preservedMoney;
        smartNpc.spiritStone = preservedSpiritStone;
        smartNpc.currentHP = characterStats.currentHP;
        smartNpc.maxHP = characterStats.finalHP;
        smartNpc.baseMaxHP = characterStats.baseMaxHP;
        smartNpc.baseAttack = characterStats.baseAttack;
        smartNpc.baseDefense = characterStats.baseDefense;
        smartNpc.attack = characterStats.attack;
        smartNpc.defense = characterStats.defense;
        smartNpc.lifespan = Mathf.Max(preservedLifespan, 120);
        smartNpc.waitingForHeavenlyTribulation = false;
        smartNpc.RequestEmergencyTask(
            SmartAITaskGoal.CriticalBreakthrough,
            SmartAITaskPriority.Critical,
            false,
            "cultivation awakening");
        smartNpc.ForceSetCurrentAction(NpcText.Action("breakthrough"));

        NpcScheduleController schedule =
            target.GetComponent<NpcScheduleController>();

        if (schedule == null)
        {
            schedule =
                target.AddComponent<NpcScheduleController>();
        }

        schedule.AwakenCultivationPath(true);
        schedule.lifePath = NpcLifePath.Cultivator;
        schedule.canCultivate = true;
        schedule.RebuildDefaultSchedule();

        villager.enabled = false;
        smartNpc.enabled = true;

        return true;
    }
}
