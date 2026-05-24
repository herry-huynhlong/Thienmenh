using UnityEngine;

public class CharacterStats : MonoBehaviour, IDamageable
{
    [Header("Cultivation")]
    public CultivationRealm realm = CultivationRealm.Mortal;
    [Range(1, 9)]
    public int realmStage = 1;
    public int cultivationExp;
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
        RecalculateStats(true);
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

        for (int i = 0; i < realmIndex; i++)
        {
            multiplier *= 10;
        }

        return multiplier;
    }

    public int ExpToNextRealm()
    {
        int result = Mathf.Max(1, baseExpToNextRealm);
        int realmIndex = Mathf.Max(0, (int)realm);

        for (int i = 0; i < realmIndex; i++)
        {
            result *= 10;
        }

        return result;
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

        realm =
            (CultivationRealm)((int)realm + 1);

        realmStage = 1;
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
        if (item == null)
        {
            return;
        }

        foreach (StatModifier modifier in item.GetAllModifiers())
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
                        Mathf.Max(
                            0,
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
    }

    public string GetRealmText()
    {
        switch (realm)
        {
            case CultivationRealm.Mortal:
                return "Pham Nhan";
            case CultivationRealm.QiRefining:
                return "Luyen Khi";
            case CultivationRealm.Foundation:
                return "Truc Co";
            case CultivationRealm.GoldenCore:
                return "Kim Dan";
            case CultivationRealm.NascentSoul:
                return "Nguyen Anh";
            case CultivationRealm.SoulFormation:
                return "Hoa Than";
            case CultivationRealm.Tribulation:
                return "Do Kiep";
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
