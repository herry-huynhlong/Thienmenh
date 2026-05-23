using UnityEngine;

public interface IDamageable
{
    bool IsDead { get; }

    Transform DamageTransform { get; }

    void TakeDamage(int damage);
}
