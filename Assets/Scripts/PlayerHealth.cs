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
        if (characterStats != null)
        {
            characterStats.TakeDamage(damage);
            SyncFromCharacterStats();

            if (characterStats.IsDead)
            {
                Die();
            }

            return;
        }

        currentHP -= damage;

        Debug.Log(
            "Player bi tru " +
            damage +
            " mau");

        Debug.Log(
            "Mau con: " +
            currentHP);

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void ApplyItem(StatItemData item)
    {
        ApplyItem(item, 1);
    }

    public void ApplyItem(StatItemData item, int direction)
    {
        if (characterStats != null)
        {
            characterStats.ApplyItem(item, direction);
            SyncFromCharacterStats();
            return;
        }

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
        Debug.Log("Player da chet");
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
