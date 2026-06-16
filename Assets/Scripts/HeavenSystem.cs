using UnityEngine;

public class HeavenSystem : MonoBehaviour
{
    public static HeavenSystem Instance { get; private set; }

    [Header("Heaven Punishment")]
    public GameObject lightningEffectPrefab;
    public int baseTribulationDamage = 100;
    public float punishmentRadius = 1.2f;
    public LayerMask damageLayers = ~0;

    [Header("Heaven Gift")]
    public WorldStatItemPickup worldItemPrefab;
    public float giftDropHeight = 0.25f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StrikeEntity(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        StrikeAt(target.transform.position, baseTribulationDamage);

        EntityProfile profile = target.GetComponent<EntityProfile>();
        if (profile != null)
        {
            profile.Remember("Heaven", "heaven_punishment", -baseTribulationDamage);
            profile.emotion.fear = Mathf.Clamp(profile.emotion.fear + 40f, 0f, 100f);
            profile.currentGoal = EntityGoal.Flee;
        }
    }

    public void StrikeAt(Vector3 position, int damage)
    {
        if (lightningEffectPrefab != null)
        {
            Instantiate(lightningEffectPrefab, position, Quaternion.identity);
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                position,
                punishmentRadius,
                damageLayers);

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable =
                hit.GetComponentInParent<IDamageable>();

            if (damageable == null ||
                damageable.IsDead)
            {
                continue;
            }

            damageable.TakeDamage(damage);
        }
    }

    public WorldStatItemPickup DropItemForEntity(GameObject target, StatItemData item, int amount = 1)
    {
        if (target == null || item == null)
        {
            return null;
        }

        Vector3 dropPosition =
            target.transform.position +
            Vector3.up * giftDropHeight;

        return DropItemAt(dropPosition, item, amount);
    }

    public WorldStatItemPickup DropItemAt(Vector3 position, StatItemData item, int amount = 1)
    {
        if (item == null)
        {
            return null;
        }

        WorldStatItemPickup pickup;
        if (worldItemPrefab != null)
        {
            pickup = Instantiate(worldItemPrefab, position, Quaternion.identity);
        }
        else
        {
            GameObject itemObject = new GameObject("Heaven Gift - " + item.itemName);
            itemObject.transform.position = position;
            pickup = itemObject.AddComponent<WorldStatItemPickup>();

            CircleCollider2D collider =
                itemObject.AddComponent<CircleCollider2D>();

            collider.isTrigger = true;
            collider.radius = 0.25f;
            PickupVisualUtility.ApplySprite(itemObject, item.icon, 20);
        }

        pickup.item = item;
        pickup.amount = Mathf.Max(1, amount);
        pickup.allowNpcPickup = true;
        pickup.allowPlayerPickup = false;
        EnsurePickupCollider(pickup);
        return pickup;
    }

    void EnsurePickupCollider(WorldStatItemPickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        CircleCollider2D collider =
            pickup.GetComponent<CircleCollider2D>();

        if (collider == null)
        {
            collider = pickup.gameObject.AddComponent<CircleCollider2D>();
        }

        collider.isTrigger = true;

        if (collider.radius <= 0f)
        {
            collider.radius = 0.25f;
        }
    }
}
