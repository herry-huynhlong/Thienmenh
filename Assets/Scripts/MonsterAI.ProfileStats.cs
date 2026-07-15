using UnityEngine;

public partial class MonsterAI
{
    void ApplyEntityProfile()
    {
        EntityKind desiredKind = IsAnimalTargetType()
            ? EntityKind.Animal
            : EntityKind.Beast;

        entityProfile = EntityGenerator.EnsureProfile(gameObject, desiredKind);
        if (entityProfile == null)
        {
            return;
        }

        if (entityProfile.kind != desiredKind)
        {
            EntityGenerator.FillProfile(entityProfile, desiredKind);
            entityProfile.lockGeneratedValues = true;
        }

        if (IsAnimalTargetType())
        {
            ApplyAnimalProfile(true);
            return;
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
        if (IsAnimalTargetType())
        {
            if (maxHP <= 0)
            {
                ApplyAnimalProfile(false);
            }
            else
            {
                realm = CultivationRealm.Mortal;
                realmStage = 1;
                damage = Mathf.Max(1, baseDamage);
                defense = Mathf.Max(0, baseDefense);
                effectResistance = Mathf.Max(0, baseEffectResistance);
                moveSpeed = Mathf.Max(0.1f, baseMoveSpeed);
                beastLevel = 1;
                currentHP = fillHP
                    ? maxHP
                    : Mathf.Clamp(currentHP, 1, maxHP);
                SyncEntityProfileStats();
            }

            return;
        }

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

    bool IsAnimalTargetType()
    {
        return huntTargetType == HuntTargetType.Animal;
    }

    void ApplyAnimalProfile(bool randomizeHp)
    {
        int resolvedHp = randomizeHp
            ? Random.Range(80, 91)
            : Mathf.Clamp(maxHP > 0 ? maxHP : 85, 80, 90);

        baseMaxHP = resolvedHp;
        baseDamage = Mathf.Max(1, randomizeHp ? Random.Range(4, 7) : Mathf.Max(1, baseDamage));
        baseDefense = Mathf.Max(0, randomizeHp ? Random.Range(1, 4) : Mathf.Max(0, baseDefense));
        baseEffectResistance = Mathf.Max(0, randomizeHp ? Random.Range(0, 3) : baseEffectResistance);
        baseMoveSpeed = Mathf.Max(0.1f, randomizeHp ? Random.Range(1.0f, 1.8f) : baseMoveSpeed);
        maxHP = resolvedHp;
        damage = baseDamage;
        defense = baseDefense;
        effectResistance = baseEffectResistance;
        moveSpeed = baseMoveSpeed;
        realm = CultivationRealm.Mortal;
        realmStage = 1;
        beastLevel = 1;
        currentHP = Mathf.Clamp(currentHP <= 0 ? resolvedHp : currentHP, 1, maxHP);
        cultivationExp = 0;
        beastInstinct = Mathf.Clamp(Random.Range(10f, 30f), 0f, 100f);
        aggression = Mathf.Clamp(Random.Range(5f, 20f), 0f, 100f);
        fear = Mathf.Clamp(Random.Range(20f, 60f), 0f, 100f);
        hunger = Mathf.Clamp(Random.Range(5f, 55f), 0f, 100f);
        territorial = Mathf.Clamp(Random.Range(10f, 35f), 0f, 100f);
        bloodlust = Mathf.Clamp(Random.Range(0f, 10f), 0f, 100f);
        survivalInstinct = Mathf.Clamp(Random.Range(55f, 100f), 0f, 100f);

        if (entityProfile != null)
        {
            entityProfile.kind = EntityKind.Animal;
            if (entityProfile.identity != null)
            {
                entityProfile.identity.kind = EntityKind.Animal;
                entityProfile.identity.gender = EntityGender.Unknown;
                int currentAge =
                    NpcAgeUtility.GetCurrentAge(entityProfile.identity);
                NpcAgeUtility.SetCurrentAge(
                    entityProfile.identity,
                    Mathf.Clamp(currentAge <= 0 ? 8 : currentAge, 1, 25));
                entityProfile.identity.entityName = string.IsNullOrWhiteSpace(monsterName) ? "Phàm thú" : monsterName;
            }

            entityProfile.currentGoal = EntityGoal.Survive;
            entityProfile.stats.realm = realm;
            entityProfile.stats.realmStage = realmStage;
            entityProfile.stats.maxHP = maxHP;
            entityProfile.stats.currentHP = currentHP;
            entityProfile.stats.attack = damage;
            entityProfile.stats.defense = defense;
            entityProfile.stats.effectResistance = effectResistance;
            entityProfile.stats.moveSpeed = moveSpeed;
            entityProfile.stats.cultivationExp = cultivationExp > int.MaxValue
                ? int.MaxValue
                : (int)cultivationExp;
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
        if (IsAnimalTargetType())
        {
            return "PhÃ m thÃº";
        }

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

    public int GetAge()
    {
        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            return Mathf.Max(
                0,
                NpcAgeUtility.GetCurrentAge(entityProfile.identity));
        }

        return 0;
    }

    public int GetLifespan()
    {
        return GetLifespanForRealm(realm);
    }

    int GetLifespanForRealm(CultivationRealm targetRealm)
    {
        switch (targetRealm)
        {
            case CultivationRealm.QiRefining:
                return 120;
            case CultivationRealm.Foundation:
                return 220;
            case CultivationRealm.GoldenCore:
                return 500;
            case CultivationRealm.NascentSoul:
                return 1200;
            case CultivationRealm.SoulFormation:
                return 3000;
            case CultivationRealm.Tribulation:
                return 10000;
            default:
                return 80;
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
            if (IsAnimalTargetType())
            {
                ApplyAnimalProfile(false);
                return;
            }

            RecalculateRealmStats(false);
        }
    }
}
