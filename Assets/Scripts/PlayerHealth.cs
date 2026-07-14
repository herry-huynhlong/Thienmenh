using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public int maxHP = 100;
    public int currentHP;
    public CharacterStats characterStats;

    public bool IsDead =>
        characterStats != null ?
        characterStats.IsDead :
        currentHP <= 0;

    public Transform DamageTransform => transform;

    void Start()
    {
        characterStats = GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            SyncFromCharacterStats();
        }
        else
        {
            currentHP = maxHP;
        }
    }

    public void TakeDamage(int damage)
    {
        DamageSystem.Apply(this, DamageContext.Legacy(damage));
    }

    public DamageResult ReceiveDamage(DamageContext context)
    {
        if (characterStats != null)
        {
            DamageResult result = characterStats.ReceiveDamage(context);
            SyncFromCharacterStats();

            if (characterStats.IsDead)
            {
                Die();
            }

            result.receiver = this;
            result.target = gameObject;
            return result;
        }

        if (IsDead)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.TargetAlreadyDead);
        }

        int healthBefore = currentHP;
        int finalDamage = DamageSystem.CalculateFinalDamage(context, 0);
        if (finalDamage <= 0)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.InvalidAmount);
        }

        currentHP = Mathf.Clamp(currentHP - finalDamage, 0, maxHP);

        Debug.Log(
            "Player bi tru " +
            finalDamage +
            " máu");

        Debug.Log(
            "Máu còn: " +
            currentHP);

        if (currentHP <= 0)
        {
            Die();
        }

        return DamageResult.Applied(
            context,
            this,
            gameObject,
            finalDamage,
            healthBefore,
            currentHP);
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
        if (characterStats != null)
        {
            characterStats.ApplyItem(item, direction, powerMultiplier);
            SyncFromCharacterStats();
            return;
        }

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

            switch (modifier.statType)
            {
                case StatType.MaxHP:
                    maxHP += modifier.intValue * direction;
                    currentHP += modifier.intValue * direction;
                    break;

                case StatType.CurrentHP:
                    currentHP += modifier.intValue * direction;
                    break;
            }
        }

        currentHP =
            Mathf.Clamp(currentHP, 0, maxHP);
    }

    void Die()
    {
        Debug.Log("Player đã chết");
    }

    void SyncFromCharacterStats()
    {
        if (characterStats == null)
        {
            return;
        }

        maxHP = characterStats.finalHP;
        currentHP = characterStats.currentHP;
    }
}
