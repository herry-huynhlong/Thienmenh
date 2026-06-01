using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ResourceFieldItemEntry
{
    public StatItemData item;
    [Min(1)] public int weight = 1;
    [Min(1)] public int amount = 1;
}

[RequireComponent(typeof(SpawnRegion))]
public class WorldResourceField : MonoBehaviour
{
    static readonly List<WorldResourceField> fields =
        new List<WorldResourceField>();

    [Header("Items")]
    public List<ResourceFieldItemEntry> items =
        new List<ResourceFieldItemEntry>();

    [Header("Natural Resource Rules")]
    public bool naturalResourcesOnly = true;
    public ItemGrade maxNaturalGrade = ItemGrade.Ha;
    public bool allowMaterials = true;
    public bool allowFood = true;

    [Header("Spawn")]
    public int initialSpawnCount = 8;
    public bool spawnOnStart = true;
    public bool skipSpawnWhenResourcesExist = true;
    public GameObject resourcePrefab;

    [Header("Pickup")]
    public bool allowNpcPickup = true;
    public bool allowPlayerPickup = false;
    public float colliderRadius = 0.25f;
    public int sortingOrder = 20;
    public float visualSize = 0.45f;

    [Header("Respawn")]
    public int respawnAmount = 1;
    public float respawnDelay = 30f;
    public ResourceRespawnMode respawnMode =
        ResourceRespawnMode.AutomaticByGrade;

    SpawnRegion region;

    public static IReadOnlyList<WorldResourceField> Fields => fields;

    void OnEnable()
    {
        if (!fields.Contains(this))
        {
            fields.Add(this);
        }
    }

    void OnDisable()
    {
        fields.Remove(this);
    }

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnInitialResources();
        }
    }

    void OnValidate()
    {
        initialSpawnCount = Mathf.Max(0, initialSpawnCount);
        colliderRadius = Mathf.Max(0.01f, colliderRadius);
        visualSize = Mathf.Max(0.05f, visualSize);
        respawnAmount = Mathf.Max(1, respawnAmount);
        respawnDelay = Mathf.Max(0f, respawnDelay);

        if (region == null)
        {
            region = GetComponent<SpawnRegion>();
        }
    }

    [ContextMenu("Spawn Initial Resources")]
    public void SpawnInitialResources()
    {
        int existingResourceCount =
            GetComponentsInChildren<WorldStatItemPickup>(true).Length;

        int spawnCount =
            skipSpawnWhenResourcesExist
            ? Mathf.Max(0, initialSpawnCount - existingResourceCount)
            : initialSpawnCount;

        if (spawnCount <= 0)
        {
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            SpawnResource();
        }
    }

    public WorldStatItemPickup SpawnResource()
    {
        ResourceFieldItemEntry entry =
            PickItemEntry();

        if (entry == null ||
            entry.item == null ||
            !CanSpawnItem(entry.item))
        {
            return null;
        }

        if (region == null)
        {
            region = GetComponent<SpawnRegion>();
        }

        if (region == null ||
            region.size == Vector2.zero)
        {
            Debug.LogWarning(
                name +
                ": WorldResourceField cần SpawnRegion có Size lớn hơn 0 để spawn tài nguyên.",
                this);
            return null;
        }

        Vector3 position =
            region.RandomPoint();

        GameObject resourceObject =
            resourcePrefab != null
            ? Instantiate(resourcePrefab, position, Quaternion.identity, transform)
            : new GameObject("Resource - " + entry.item.itemName);

        if (resourcePrefab == null)
        {
            resourceObject.transform.SetParent(transform, true);
            resourceObject.transform.position = position;
        }

        ConfigureResource(resourceObject, entry);
        return resourceObject.GetComponent<WorldStatItemPickup>();
    }

    public WorldStatItemPickup GetNearestAvailablePickup(Vector3 position)
    {
        WorldStatItemPickup[] pickups =
            GetComponentsInChildren<WorldStatItemPickup>(true);

        WorldStatItemPickup best = null;
        float bestDistance = float.MaxValue;

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (!IsAvailable(pickup))
            {
                continue;
            }

            float distance =
                Vector2.Distance(position, pickup.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pickup;
            }
        }

        return best;
    }

    public static WorldStatItemPickup GetNearestAvailablePickupInAllFields(
        Vector3 position)
    {
        WorldStatItemPickup best = null;
        float bestDistance = float.MaxValue;

        foreach (WorldResourceField field in fields)
        {
            if (field == null ||
                !field.isActiveAndEnabled)
            {
                continue;
            }

            WorldStatItemPickup candidate =
                field.GetNearestAvailablePickup(position);

            if (candidate == null)
            {
                continue;
            }

            float distance =
                Vector2.Distance(position, candidate.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    void ConfigureResource(
        GameObject resourceObject,
        ResourceFieldItemEntry entry)
    {
        WorldStatItemPickup pickup =
            resourceObject.GetComponent<WorldStatItemPickup>();

        if (pickup == null)
        {
            pickup = resourceObject.AddComponent<WorldStatItemPickup>();
        }

        pickup.item = entry.item;
        pickup.amount = Mathf.Max(1, entry.amount);
        pickup.allowNpcPickup = allowNpcPickup;
        pickup.allowPlayerPickup = allowPlayerPickup;
        pickup.destroyWhenEmpty = false;

        WorldResourceNode node =
            resourceObject.GetComponent<WorldResourceNode>();

        if (node == null)
        {
            node = resourceObject.AddComponent<WorldResourceNode>();
        }

        node.pickup = pickup;
        node.respawnAmount = Mathf.Max(1, respawnAmount);
        node.respawnDelay = respawnDelay;
        node.respawnMode = respawnMode;
        node.respawnRegion = region;

        CircleCollider2D collider =
            resourceObject.GetComponent<CircleCollider2D>();

        if (collider == null)
        {
            collider = resourceObject.AddComponent<CircleCollider2D>();
        }

        collider.isTrigger = true;
        collider.radius = colliderRadius;

        EnsureVisual(resourceObject, entry.item);
    }

    void EnsureVisual(GameObject resourceObject, StatItemData item)
    {
        if (item == null ||
            item.icon == null)
        {
            return;
        }

        SpriteRenderer renderer =
            resourceObject.GetComponentInChildren<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = resourceObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = item.icon;
        renderer.sortingOrder = sortingOrder;
        NormalizeRendererSize(renderer);
    }

    void NormalizeRendererSize(SpriteRenderer renderer)
    {
        if (renderer == null ||
            renderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = renderer.sprite.bounds.size;
        float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);

        if (largestSide <= 0f)
        {
            return;
        }

        renderer.transform.localScale =
            Vector3.one * (visualSize / largestSide);
    }

    ResourceFieldItemEntry PickItemEntry()
    {
        int totalWeight = 0;

        foreach (ResourceFieldItemEntry entry in items)
        {
            if (entry != null &&
                entry.item != null &&
                CanSpawnItem(entry.item))
            {
                totalWeight += Mathf.Max(1, entry.weight);
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int roll = Random.Range(0, totalWeight);
        int current = 0;

        foreach (ResourceFieldItemEntry entry in items)
        {
            if (entry == null ||
                entry.item == null ||
                !CanSpawnItem(entry.item))
            {
                continue;
            }

            current += Mathf.Max(1, entry.weight);

            if (roll < current)
            {
                return entry;
            }
        }

        return null;
    }

    bool CanSpawnItem(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (!naturalResourcesOnly)
        {
            return true;
        }

        if (item.grade > maxNaturalGrade)
        {
            return false;
        }

        if (allowMaterials &&
            item.itemType == ItemType.VatLieu)
        {
            return true;
        }

        if (allowFood &&
            item.itemType == ItemType.ThucPham)
        {
            return true;
        }

        return false;
    }

    bool IsAvailable(WorldStatItemPickup pickup)
    {
        return pickup != null &&
            pickup.gameObject.activeInHierarchy &&
            pickup.item != null &&
            pickup.amount > 0 &&
            pickup.allowNpcPickup;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.35f);

        SpawnRegion drawRegion =
            region != null
            ? region
            : GetComponent<SpawnRegion>();

        if (drawRegion != null)
        {
            Gizmos.DrawCube(drawRegion.transform.position, drawRegion.size);
        }
    }
}
