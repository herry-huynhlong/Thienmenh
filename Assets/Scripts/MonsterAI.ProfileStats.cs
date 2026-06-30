using UnityEngine;

public partial class MonsterAI
{
    void ApplyEntityProfile()
    {
        entityProfile = EntityGenerator.EnsureProfile(gameObject, EntityKind.Beast);
        if (entityProfile == null)
        {
            return;
        }

        if (entityProfile.kind != EntityKind.Beast)
        {
            EntityGenerator.FillProfile(entityProfile, EntityKind.Beast);
            entityProfile.lockGeneratedValues = true;
        }

        double realmPower =
            CombatStatCalculator.GetRealmMultiplier(
                Mathf.Max(0, (int)entityProfile.stats.realm),
                Mathf.Clamp(entityProfile.stats.realmStage, 1, CultivationProgression.MaxStage) - 1);
        baseMaxHP =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(
                    entityProfile.stats.maxHP / realmPower));
        baseDamage =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(
                    entityProfile.stats.attack / realmPower));
        baseDefense =
            Mathf.Max(
                0,
                CombatStatCalculator.ClampToInt(
                    entityProfile.stats.defense / realmPower));
        baseEffectResistance = Mathf.Max(0, entityProfile.stats.effectResistance);
        baseMoveSpeed = Mathf.Max(0.1f, entityProfile.stats.moveSpeed);
        maxHP = Mathf.Max(1, entityProfile.stats.maxHP);
        currentHP =
            Mathf.Clamp(
                entityProfile.stats.currentHP,
                0,
                maxHP);

        if (!autoStatsFromRealm)
        {
            realm = entityProfile.stats.realm;
            realmStage = entityProfile.stats.realmStage;
            cultivationExp = Mathf.Max(0, entityProfile.stats.cultivationExp);
        }

        beastInstinct = Mathf.Clamp(
            45f + entityProfile.talent.combatMultiplier * 15f,
            0f,
            100f);
        aggression = Mathf.Clamp(
            entityProfile.personality.bravery +
            entityProfile.personality.hotTemper * 0.5f,
            0f,
            100f);
        fear = Mathf.Clamp(
            100f - entityProfile.personality.bravery,
            0f,
            100f);
        hunger = entityProfile.needs.hunger;
        territorial = Random.Range(35f, 95f);
        bloodlust = Mathf.Clamp(entityProfile.personality.hotTemper, 0f, 100f);
        survivalInstinct = Random.Range(35f, 100f);
    }

    public void RecalculateRealmStats(bool fillHP)
    {
        if (!autoStatsFromRealm)
        {
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            return;
        }

        int oldMaxHP = Mathf.Max(1, maxHP);
        float hpPercent = Mathf.Clamp01((float)currentHP / oldMaxHP);
        double power =
            CombatStatCalculator.GetRealmMultiplier(
                Mathf.Max(0, (int)realm),
                Mathf.Clamp(realmStage, 1, CultivationProgression.MaxStage) - 1);

        maxHP = Mathf.Max(1, CombatStatCalculator.ClampToInt(Mathf.Max(1, baseMaxHP) * power));
        damage = Mathf.Max(1, CombatStatCalculator.ClampToInt(Mathf.Max(1, baseDamage) * power));
        defense = Mathf.Max(0, CombatStatCalculator.ClampToInt(Mathf.Max(0, baseDefense) * power));
        effectResistance =
            Mathf.Max(
                0,
                baseEffectResistance +
                (int)realm * 2 +
                Mathf.Max(0, realmStage - 1) / 3);
        moveSpeed =
            Mathf.Max(
                0.1f,
                baseMoveSpeed +
                Mathf.Max(0, (int)realm) * 0.12f +
                Mathf.Max(0, realmStage - 1) * 0.02f);

        if (syncBeastLevelFromRealm)
        {
            beastLevel = GetBeastLevelForRealm();
        }

        currentHP = fillHP
            ? maxHP
            : Mathf.Clamp(
                CombatStatCalculator.ClampToInt(maxHP * hpPercent),
                0,
                maxHP);
        SyncEntityProfileStats();
    }

    int GetBeastLevelForRealm()
    {
        switch (realm)
        {
            case CultivationRealm.Mortal:
            case CultivationRealm.QiRefining:
                return 1;
            case CultivationRealm.Foundation:
                return 2;
            case CultivationRealm.GoldenCore:
                return 3;
            default:
                return 4;
        }
    }

    void SyncEntityProfileStats()
    {
        if (entityProfile == null)
        {
            return;
        }

        entityProfile.stats.realm = realm;
        entityProfile.stats.realmStage =
            Mathf.Clamp(
                realmStage,
                1,
                CultivationProgression.MaxStage);
        entityProfile.stats.maxHP = maxHP;
        entityProfile.stats.currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        entityProfile.stats.attack = damage;
        entityProfile.stats.defense = defense;
        entityProfile.stats.effectResistance = effectResistance;
        entityProfile.stats.moveSpeed = moveSpeed;
        entityProfile.stats.cultivationExp =
            Mathf.Clamp(
                cultivationExp > int.MaxValue ? int.MaxValue : (int)cultivationExp,
                0,
                int.MaxValue);
    }

    public string GetRealmText()
    {
        switch (realm)
        {
            case CultivationRealm.Mortal:
            case CultivationRealm.QiRefining:
            case CultivationRealm.Foundation:
            case CultivationRealm.GoldenCore:
            case CultivationRealm.NascentSoul:
            case CultivationRealm.SoulFormation:
            case CultivationRealm.Tribulation:
                return NpcText.Realm(realm);
            default:
                return realm.ToString();
        }
    }

    void OnValidate()
    {
        realmStage =
            Mathf.Clamp(
                realmStage,
                1,
                CultivationProgression.MaxStage);
        baseMaxHP = Mathf.Max(1, baseMaxHP);
        baseDamage = Mathf.Max(1, baseDamage);
        baseDefense = Mathf.Max(0, baseDefense);
        baseEffectResistance = Mathf.Max(0, baseEffectResistance);
        baseMoveSpeed = Mathf.Max(0.1f, baseMoveSpeed);
        baseExpToNextRealm = Mathf.Max(1, baseExpToNextRealm);

        if (!Application.isPlaying)
        {
            RecalculateRealmStats(false);
        }
    }
}
