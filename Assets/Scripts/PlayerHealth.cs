using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Máu")]
    public int maxHP = 100;

    public int currentHP;

    public bool IsDead => currentHP <= 0;

    public Transform DamageTransform => transform;

    void Start()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;

        Debug.Log(
            "Player bị trừ " +
            damage +
            " máu");

        Debug.Log(
            "Máu còn: " +
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
        Debug.Log(
            "Player đã chết");
    }
}
