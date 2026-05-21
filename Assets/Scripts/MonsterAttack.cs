using UnityEngine;

public class MonsterAttack : MonoBehaviour
{
    public int damage = 10;

    void OnTriggerEnter2D(
        Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log(
                "Đánh trúng player");

            PlayerHealth player =
                other.GetComponent<PlayerHealth>();

            if (player != null)
            {
                player.TakeDamage(
                    damage);
            }
        }
    }
}