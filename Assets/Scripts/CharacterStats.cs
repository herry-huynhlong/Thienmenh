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

    [Header("Base Stats")]
    public int baseMaxHP = 100;
    public int baseAttack = 10;
    public int baseDefense = 5;
    public float baseMoveSpeed = 1.6f;

    [Header("Item Bonus")]
    public int bonusMaxHP;
    public int bonusAttack;
    public int bonusDefense;
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

    public Transform DamageTransform => transform;

    void Awake()
    {
        if (generateFromEntityProfile)
        {
            ApplyEntityProfile();
        }

        RecalculateStats(true);
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
        int realmMultiplier = GetRealmMultiplier();
        baseMaxHP = Mathf.Max(1, entityProfile.stats.maxHP / realmMultiplier);
        baseAttack = Mathf.Max(1, entityProfile.stats.attack / realmMultiplier);
        baseDefense = Mathf.Max(0, entityProfile.stats.defense / realmMultiplier);
        baseMoveSpeed = Mathf.Max(0.1f, entityProfile.stats.moveSpeed);
        currentHP =
            Mathf.Clamp(
                entityProfile.stats.currentHP,
                1,
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

    public int GetRealmMultiplier()
    {
        int multiplier = 1;
        int realmIndex = Mathf.Max(0, (int)realm);
        int stage =
            Mathf.Clamp(
                realmStage,
                1,
                CultivationProgression.MaxStage);

        for (int i = 0; i < realmIndex; i++)
        {
            multiplier *= 10;
        }

        return multiplier * stage;
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
            realm == CultivationRealm.Tribulation)
        {
            return;
        }

        cultivationExp += amount;

        while (cultivationExp >= ExpToNextRealm() &&
            realm != CultivationRealm.Tribulation)
        {
            cultivationExp -= ExpToNextRealm();
            Breakthrough();
        }
    }

    public void Breakthrough()
    {
        if (realm == CultivationRealm.Tribulation)
        {
            cultivationExp = 0;
            return;
        }

        realmStage += 1;

        if (realmStage > CultivationProgression.MaxStage)
        {
            realmStage = 1;
            realm =
                (CultivationRealm)((int)realm + 1);
        }

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

        int multiplier =
            GetRealmMultiplier();

        finalHP =
            Mathf.Max(1, baseMaxHP) *
            multiplier +
            bonusMaxHP;

        attack =
            Mathf.Max(1, baseAttack) *
            multiplier +
            bonusAttack;

        defense =
            Mathf.Max(0, baseDefense) *
            multiplier +
            bonusDefense;

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

            case StatType.CurrentHP:
                currentHP += intValue;
                break;

            case StatType.Attack:
            case StatType.Damage:
                bonusAttack += intValue;
                RecalculateStats();
                break;

            case StatType.Defense:
                bonusDefense += intValue;
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
        if (IsDead)
        {
            return;
        }

        int finalDamage =
            Mathf.Max(1, damage - defense);

        currentHP -= finalDamage;
        currentHP = Mathf.Clamp(currentHP, 0, finalHP);

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = currentHP;
        }
    }

    public string GetRealmText()
    {
        switch (realm)
        {
            case CultivationRealm.Mortal:
                return "Phàm Nhân";
            case CultivationRealm.QiRefining:
                return "Luyện Khí";
            case CultivationRealm.Foundation:
                return "Trúc Cơ";
            case CultivationRealm.GoldenCore:
                return "Kim Đan";
            case CultivationRealm.NascentSoul:
                return "Nguyên Anh";
            case CultivationRealm.SoulFormation:
                return "Hóa Thần";
            case CultivationRealm.Tribulation:
                return "Độ Kiếp";
            default:
                return realm.ToString();
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            RecalculateStats(false);
        }
    }
}
