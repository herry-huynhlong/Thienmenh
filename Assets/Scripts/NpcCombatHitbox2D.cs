using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NpcCombatHitbox2D : MonoBehaviour
{
    [Header("Owner")]
    public GameObject owner;
    public bool autoFindOwner = true;

    [Header("Damage")]
    public int damage = 10;
    public LayerMask hitLayers = ~0;
    public bool useCombatTechniqueModifier = true;
    public bool publishHostility = true;

    [Header("Lifetime")]
    public float lifeTime = 0.4f;
    public bool destroyAfterFirstHit = true;

    [Header("FX")]
    public GameObject impactPrefab;

    readonly HashSet<int> hitTargets = new HashSet<int>();

    void Awake()
    {
        if (owner == null && autoFindOwner)
        {
            owner = FindOwner();
        }

        if (lifeTime > 0f)
        {
            Destroy(gameObject, lifeTime);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
        {
            TryHit(collision.collider);
        }
    }

    public void SetOwner(GameObject newOwner)
    {
        owner = newOwner;
    }

    public void SetDamage(int newDamage)
    {
        damage = Mathf.Max(1, newDamage);
    }

    public void SetHitLayers(LayerMask newHitLayers)
    {
        hitLayers = newHitLayers;
    }

    public void ClearHitHistory()
    {
        hitTargets.Clear();
    }

    void TryHit(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        if (owner != null)
        {
            if (other.gameObject == owner ||
                other.transform.IsChildOf(owner.transform))
            {
                return;
            }
        }

        IDamageable damageable =
            other.GetComponentInParent<IDamageable>();

        if (damageable == null || damageable.IsDead)
        {
            return;
        }

        GameObject targetObject =
            damageable.DamageTransform != null
            ? damageable.DamageTransform.gameObject
            : other.gameObject;

        if (targetObject == null)
        {
            return;
        }

        if (owner != null &&
            BicanhSessionManager.AreDungeonParticipantsAllies(
                owner,
                targetObject))
        {
            return;
        }

        if (!hitTargets.Add(targetObject.GetInstanceID()))
        {
            return;
        }

        int finalDamage = Mathf.Max(1, damage);

        if (owner != null && useCombatTechniqueModifier)
        {
            finalDamage =
                NpcCombatTechniqueSystem.ModifyOutgoingDamage(
                    owner,
                    targetObject,
                    finalDamage);
        }

        if (publishHostility && owner != null)
        {
            Vector3 hitPosition =
                damageable.DamageTransform != null
                ? damageable.DamageTransform.position
                : other.bounds.center;

            NpcSocialEventBus.PublishHostility(
                owner,
                targetObject,
                Mathf.Clamp(finalDamage, 1, 100),
                hitPosition,
                NpcText.Dialogue("combatSpellReason"));
        }

        if (impactPrefab != null)
        {
            Instantiate(
                impactPrefab,
                other.bounds.center,
                Quaternion.identity);
        }

        MonsterAI monster = damageable as MonsterAI;
        if (monster != null)
        {
            monster.TakeDamage(finalDamage, owner);
        }
        else
        {
            damageable.TakeDamage(finalDamage);
        }

        if (destroyAfterFirstHit)
        {
            Destroy(gameObject);
        }
    }

    GameObject FindOwner()
    {
        MonsterAI monster = GetComponentInParent<MonsterAI>();
        if (monster != null)
        {
            return monster.gameObject;
        }

        VillagerAI villager = GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc = GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        return transform.root.gameObject;
    }
}
