using UnityEngine;

public interface IDamageable
{
    bool IsDead { get; }

    Transform DamageTransform { get; }

    DamageResult ReceiveDamage(DamageContext context);

    void TakeDamage(int damage);
}
