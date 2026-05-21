using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Máu")]
    public int maxHP = 100;

    public int currentHP;

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

    void Die()
    {
        Debug.Log(
            "Player đã chết");
    }
}