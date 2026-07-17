using UnityEngine;

public class CharacterStats : MonoBehaviour, IDamageable
{
    [Header("Entity Generation")]
    public bool generateFromEntityProfile = true;
    public EntityKind generatedEntityKind = EntityKind.Cultivator;
    public EntityProfile entityProfile;

    [Header("Cultivation")]
    public CultivationRealm realm = CultivationRealm.Mortal;
    [Range(1, 9)]
    public int realmStage = 1;
    public long cultivationExp;
    public int baseExpToNextRealm = 100;
    public bool waitingForHeavenlyTribulation;

    [Header("Base Stats")]
    public int baseMaxHP = 100;
    public int baseAttack = 10;
    public int baseDefense = 5;
    public float baseMoveSpeed = 1.6f;

    [Header("Item Bonus")]
    public int bonusMaxHP;
    public int bonusAttack;
    public int bonusDefense;
    public float bonusMaxHPPercent;
    public float bonusAttackPercent;
    public float bonusDefensePercent;
    public int bonusEffectResistance;
    public float bonusMoveSpeed;

    [Header("Final Stats")]
    public int finalHP = 100;
    public int currentHP = 100;
    public int attack = 10;
    public int defense = 5;
    public int effectResistance;
    public float moveSpeed = 1.6f;

    [Header("UI")]
    public GameObject statusBarPrefab;

    public bool IsDead => currentHP <= 0;
    public int MaxHP => Mathf.Max(1, finalHP);
    public int CurrentHP => Mathf.Clamp(currentHP, 0, MaxHP);

    public Transform DamageTransform => transform;

    void Awake()
    {
        bool appliedProfile = false;
        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
            appliedProfile = entityProfile != null;
        }

        RecalculateStats(!appliedProfile);
    }

    public void ApplyEntityProfile()
    {
        entityProfile =
            EntityGenerator.EnsureProfile(
                gameObject,
                generatedEntityKind);

        if (entityProfile == null)
        {
            return;
        }

        realm = entityProfile.stats.realm;
        realmStage = entityProfile.stats.realmStage;
        cultivationExp = entityProfile.stats.cultivationExp;
        double realmMultiplier =
            CombatStatCalculator.GetRealmMultiplier(
                Mathf.Max(0, (int)realm),
                Mathf.Clamp(realmStage, 1, CultivationProgression.MaxStage) - 1);
        baseMaxHP = Mathf.Max(1, CombatStatCalculator.ClampToInt(entityProfile.stats.maxHP / realmMultiplier));
        baseAttack = Mathf.Max(1, CombatStatCalculator.ClampToInt(entityProfile.stats.attack / realmMultiplier));
        baseDefense = Mathf.Max(0, CombatStatCalculator.ClampToInt(entityProfile.stats.defense / realmMultiplier));
        baseMoveSpeed = Mathf.Max(0.1f, entityProfile.stats.moveSpeed);
        finalHP = Mathf.Max(1, entityProfile.stats.maxHP);
        currentHP =
            Mathf.Clamp(
                entityProfile.stats.currentHP,
                0,
                Mathf.Max(1, entityProfile.stats.maxHP));
    }

    void Start()
    {
        if (statusBarPrefab != null)
        {
            GameObject bar =
                Instantiate(
                    statusBarPrefab,
                    transform);

            mau ui =
                bar.GetComponent<mau>();

            ui.targetStats = this;
        }
    }

    public long ExpToNextRealm()
    {
        return CultivationProgression.GetExpToNextLong(
            realm,
            realmStage,
            baseExpToNextRealm);
    }

    public void AddCultivationExp(int amount)
    {
        if (amount <= 0 ||
            waitingForHeavenlyTribulation ||
            realm == CultivationRealm.Tribulation)
        {
            return;
        }

        cultivationExp += amount;

        while (!waitingForHeavenlyTribulation &&
            cultivationExp >= ExpToNextRealm() &&
            realm != CultivationRealm.Tribulation)
        {
            cultivationExp -= ExpToNextRealm();
            Breakthrough();
        }
    }

    public void Breakthrough()
    {
        if (waitingForHeavenlyTribulation)
        {
            return;
        }

        if (realm == CultivationRealm.Tribulation)
        {
            cultivationExp = 0;
            return;
        }

        if (realm == CultivationRealm.Mortal &&
            realmStage >= CultivationProgression.MaxStage)
        {
            realmStage = 1;
            realm = CultivationRealm.QiRefining;
            RecalculateStats(true);
            currentHP = finalHP;
            return;
        }

        if (CultivationProgression.RequiresHeavenlyTribulation(
                realm,
                realmStage))
        {
            CultivationRealm targetRealm =
                CultivationProgression.GetNextRealm(realm);

            waitingForHeavenlyTribulation = true;
            HeavenlyTribulationSystem.Request(
                gameObject,
                gameObject.name,
                targetRealm,
                () => CompleteMajorBreakthrough(targetRealm),
                passed =>
                {
                    if (!passed)
                    {
                        waitingForHeavenlyTribulation = false;
                    }
                });
            return;
        }

        realmStage += 1;

        RecalculateStats(true);
        currentHP = finalHP;
    }

    void CompleteMajorBreakthrough(CultivationRealm targetRealm)
    {
        waitingForHeavenlyTribulation = false;
        if (IsDead)
        {
            return;
        }

        realmStage = 1;
        realm = targetRealm;
        RecalculateStats(true);
        currentHP = finalHP;
    }

    public void RecalculateStats()
    {
        RecalculateStats(false);
    }

    public void RecalculateStats(bool fillHP)
    {
        int oldFinalHP =
            Mathf.Max(1, finalHP);

        float hpPercent =
            Mathf.Clamp01((float)currentHP / oldFinalHP);

        double multiplier =
            CombatStatCalculator.GetRealmMultiplier(
                Mathf.Max(0, (int)realm),
                Mathf.Clamp(realmStage, 1, CultivationProgression.MaxStage) - 1);

        double scaledHp =
            (double)CombatStatCalculator.ClampToInt(
                Mathf.Max(1, baseMaxHP) *
                multiplier) +
            bonusMaxHP;
        double scaledAttack =
            (double)CombatStatCalculator.ClampToInt(
                Mathf.Max(1, baseAttack) *
                multiplier) +
            bonusAttack;
        double scaledDefense =
            (double)CombatStatCalculator.ClampToInt(
                Mathf.Max(0, baseDefense) *
                multiplier) +
            bonusDefense;

        scaledHp *= 1d + bonusMaxHPPercent;
        scaledAttack *= 1d + bonusAttackPercent;
        scaledDefense *= 1d + bonusDefensePercent;

        finalHP = (int)System.Math.Max(
            1L,
            System.Math.Min(
                (long)int.MaxValue,
                (long)CombatStatCalculator.ClampToInt(scaledHp)));
        attack = (int)System.Math.Max(
            1L,
            System.Math.Min(
                (long)int.MaxValue,
                (long)CombatStatCalculator.ClampToInt(scaledAttack)));
        defense = (int)System.Math.Max(
            0L,
            System.Math.Min(
                (long)int.MaxValue,
                (long)CombatStatCalculator.ClampToInt(scaledDefense)));

        effectResistance =
            bonusEffectResistance;

        moveSpeed =
            Mathf.Max(0f, baseMoveSpeed + bonusMoveSpeed);

        finalHP = Mathf.Max(1, finalHP);

        if (fillHP)
        {
            currentHP = finalHP;
        }
        else
        {
            currentHP =
                Mathf.Clamp(
                    Mathf.RoundToInt(finalHP * hpPercent),
                    0,
                    finalHP);
        }

        SyncHealthToEntityProfile();
    }

    public void RestoreHealthState(
        int savedMaxHP,
        int savedCurrentHP)
    {
        int restoredMaxHP = savedMaxHP > 0
            ? savedMaxHP
            : MaxHP;

        finalHP = Mathf.Max(1, restoredMaxHP);
        currentHP = Mathf.Clamp(savedCurrentHP, 0, finalHP);

        double multiplier =
            CombatStatCalculator.GetRealmMultiplier(
                Mathf.Max(0, (int)realm),
                Mathf.Clamp(realmStage, 1, CultivationProgression.MaxStage) - 1);
        double unscaledBaseHP =
            (finalHP - bonusMaxHP) /
            System.Math.Max(0.0001d, multiplier);
        baseMaxHP =
            Mathf.Max(
                1,
                CombatStatCalculator.ClampToInt(unscaledBaseHP));

        if (IsDead)
        {
            waitingForHeavenlyTribulation = false;
        }

        SyncHealthToEntityProfile();
    }

    public void SetCurrentHP(int value)
    {
        currentHP = Mathf.Clamp(value, 0, MaxHP);

        if (IsDead)
        {
            waitingForHeavenlyTribulation = false;
        }

        SyncHealthToEntityProfile();
    }

    public int Heal(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int before = CurrentHP;
        int healedHP = amount >= MaxHP - before
            ? MaxHP
            : before + amount;
        SetCurrentHP(healedHP);
        return CurrentHP - before;
    }

    public void SyncHealthToEntityProfile()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.maxHP = MaxHP;
            villager.currentHP = CurrentHP;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.maxHP = MaxHP;
            smartNpc.currentHP = CurrentHP;
        }

        if (entityProfile == null || entityProfile.stats == null)
        {
            return;
        }

        entityProfile.stats.maxHP = MaxHP;
        entityProfile.stats.currentHP = CurrentHP;
    }

    public void ApplyItem(StatItemData item)
    {
        ApplyItem(item, 1);
    }

    public void ApplyItem(StatItemData item, int direction)
    {
        ApplyItem(item, direction, 1f);
    }

    public void ApplyItem(
        StatItemData item,
        int direction,
        float powerMultiplier)
    {
        if (item == null)
        {
            return;
        }

        if (direction > 0)
        {
            HeavenlyTribulationSystem.MarkPillProtectionIfEligible(
                gameObject,
                item);
        }

        foreach (StatModifier modifier in item.GetAllModifiers(powerMultiplier))
        {
            if (modifier == null)
            {
                continue;
            }

            ApplyModifier(modifier, direction);
        }

        currentHP =
            Mathf.Clamp(currentHP, 0, finalHP);
        SyncHealthToEntityProfile();
    }

    void ApplyModifier(StatModifier modifier, int direction)
    {
        int intValue =
            modifier.intValue * direction;

        float floatValue =
            modifier.floatValue * direction;

        switch (modifier.statType)
        {
            case StatType.MaxHP:
                bonusMaxHP += intValue;
                RecalculateStats();
                currentHP += intValue;
                break;

            case StatType.MaxHPPercent:
                bonusMaxHPPercent += floatValue;
                RecalculateStats();
                break;

            case StatType.CurrentHP:
                currentHP += intValue;
                break;

            case StatType.Attack:
            case StatType.Damage:
                bonusAttack += intValue;
                RecalculateStats();
                break;

            case StatType.AttackPercent:
                bonusAttackPercent += floatValue;
                RecalculateStats();
                break;

            case StatType.Defense:
                bonusDefense += intValue;
                RecalculateStats();
                break;

            case StatType.DefensePercent:
                bonusDefensePercent += floatValue;
                RecalculateStats();
                break;

            case StatType.EffectResistance:
                bonusEffectResistance += intValue;
                RecalculateStats();
                break;

            case StatType.MoveSpeed:
                bonusMoveSpeed += floatValue;
                RecalculateStats();
                break;

            case StatType.Cultivation:
                if (direction > 0)
                {
                    AddCultivationExp(modifier.intValue);
                }
                else
                {
                    cultivationExp =
                        System.Math.Max(
                            0L,
                            cultivationExp - modifier.intValue);
                }
                break;

            case StatType.Breakthrough:
                if (direction > 0)
                {
                    Breakthrough();
                }
                break;
        }
    }

    public void TakeDamage(int damage)
    {
        DamageSystem.Apply(this, DamageContext.Legacy(damage));
    }

    public DamageResult ReceiveDamage(DamageContext context)
    {
        if (IsDead)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.TargetAlreadyDead);
        }

        int healthBefore = CurrentHP;
        int finalDamage =
            DamageSystem.CalculateFinalDamage(
                context,
                defense);

        if (finalDamage <= 0)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.InvalidAmount);
        }

        SetCurrentHP(currentHP - finalDamage);

        if (IsDead)
        {
            waitingForHeavenlyTribulation = false;
        }

        if (!IsDead)
        {
            NpcCombatTechniqueSystem.ReactToDamageTaken(
                gameObject,
                finalDamage);
        }

        return DamageResult.Applied(
            context,
            this,
            gameObject,
            finalDamage,
            healthBefore,
            CurrentHP);
    }

    public string GetRealmText()
    {
        return NpcText.RealmWithStage(realm, realmStage);
    }


    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            RecalculateStats(false);
        }
    }
}
