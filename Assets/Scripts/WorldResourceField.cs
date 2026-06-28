using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class ResourceFieldItemEntry
{
    public StatItemData item;
    [Min(1)] public int weight = 1;
    [Min(1)] public int amount = 1;
}

[Serializable]
public class SavedWorldResourceNode
{
    public string itemKey;
    public int amount;
    public Vector3 position;
    public bool active;
}

[Serializable]
public class SavedWorldResourceField
{
    public List<SavedWorldResourceNode> resources = new List<SavedWorldResourceNode>();
}

[RequireComponent(typeof(SpawnRegion))]
public class WorldResourceField : MonoBehaviour
{
    static readonly List<WorldResourceField> fields = new List<WorldResourceField>();

    [Header("Items")]
    public List<ResourceFieldItemEntry> items = new List<ResourceFieldItemEntry>();

    [Header("Natural Resource Rules")]
    public bool naturalResourcesOnly = true;
    public ItemGrade maxNaturalGrade = ItemGrade.Ha;
    public bool useAreaDangerTierForNaturalGrade = true;
    public bool allowMaterials = true;
    public bool allowFood = true;
    public bool allowMedicine = true;

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
    public bool requireNpcHarvestAction = true;
    public float harvestDuration = 8f;

    [Header("Visual")]
    [Tooltip("Bật nếu muốn item spawn ra tự hiện icon đứng yên. Tắt để ẩn icon item nhưng vẫn giữ logic nhặt/thu hoạch.")]
    public bool showAutoItemVisual = false;

    [Header("Rare Item Effect")]
    public string rareEffectChildName = "ItemAuraParticle";

    [Header("Respawn")]
    public int respawnAmount = 1;
    public float respawnDelay = 30f;
    public ResourceRespawnMode respawnMode = ResourceRespawnMode.AutomaticByGrade;

    [Header("Save")]
    public bool saveResourceState = true;
    public bool loadSavedResourceState = true;
    public string resourceSaveKey = "";
    public float autoSaveInterval = 10f;

    const string ResourceSavePrefix = "ThienMenh.Save.ResourceField.";

    SpawnRegion region;
    float autoSaveTimer;

    public static IReadOnlyList<WorldResourceField> Fields => fields;

    void OnEnable()
    {
        if (!fields.Contains(this))
            fields.Add(this);
    }

    void OnDisable()
    {
        fields.Remove(this);
    }

    void Start()
    {
        RegisterConfiguredItems();
        ConfigureExistingResources();

        if (loadSavedResourceState && TryLoadResourceState())
            return;

        if (spawnOnStart)
            SpawnInitialResources();
    }

    void Update()
    {
        if (!saveResourceState || autoSaveInterval <= 0f)
            return;

        autoSaveTimer += Time.deltaTime;

        if (autoSaveTimer < autoSaveInterval)
            return;

        autoSaveTimer = 0f;
        SaveResourceState();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveResourceState();
    }

    void OnApplicationQuit()
    {
        SaveResourceState();
    }

    void OnValidate()
    {
        initialSpawnCount = Mathf.Max(0, initialSpawnCount);
        colliderRadius = Mathf.Max(0.01f, colliderRadius);
        visualSize = Mathf.Max(0.05f, visualSize);
        respawnAmount = Mathf.Max(1, respawnAmount);
        respawnDelay = Mathf.Max(0f, respawnDelay);
        harvestDuration = Mathf.Max(0.1f, harvestDuration);
        autoSaveInterval = Mathf.Max(0f, autoSaveInterval);

        if (region == null)
            region = GetComponent<SpawnRegion>();
    }

    void ConfigureExistingResources()
    {
        WorldStatItemPickup[] pickups = GetComponentsInChildren<WorldStatItemPickup>(true);

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (pickup == null)
                continue;

            pickup.allowNpcPickup = allowNpcPickup;
            pickup.allowPlayerPickup = allowPlayerPickup;
            pickup.requireNpcHarvestAction = requireNpcHarvestAction;
            pickup.harvestDuration = Mathf.Max(0.1f, harvestDuration);
            pickup.destroyWhenEmpty = false;
            pickup.OnDepleted -= SaveResourceState;
            pickup.OnDepleted += SaveResourceState;

            ResourceNode resourceNode = pickup.GetComponent<ResourceNode>();
            if (resourceNode == null)
            {
                resourceNode = pickup.gameObject.AddComponent<ResourceNode>();
            }

            resourceNode.pickup = pickup;
            resourceNode.RefreshResourceKind();

            SetupRareItemEffect(pickup.gameObject, pickup.item);
        }
    }

    [ContextMenu("Spawn Initial Resources")]
    public void SpawnInitialResources()
    {
        int existingResourceCount = GetComponentsInChildren<WorldStatItemPickup>(true).Length;

        int spawnCount = skipSpawnWhenResourcesExist
            ? Mathf.Max(0, initialSpawnCount - existingResourceCount)
            : initialSpawnCount;

        if (spawnCount <= 0)
            return;

        for (int i = 0; i < spawnCount; i++)
            SpawnResource();
    }

    public WorldStatItemPickup SpawnResource()
    {
        ResourceFieldItemEntry entry = PickItemEntry();

        if (entry == null || entry.item == null || !CanSpawnItem(entry.item))
            return null;

        if (region == null)
            region = GetComponent<SpawnRegion>();

        if (region == null || region.size == Vector2.zero)
        {
            Debug.LogWarning(name + ": WorldResourceField cần SpawnRegion có Size lớn hơn 0 để spawn tài nguyên.", this);
            return null;
        }

        Vector3 position = region.RandomPoint();

        GameObject resourceObject = resourcePrefab != null
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

    void ConfigureResource(GameObject resourceObject, ResourceFieldItemEntry entry)
    {
        WorldStatItemPickup pickup = resourceObject.GetComponent<WorldStatItemPickup>();

        if (pickup == null)
            pickup = resourceObject.AddComponent<WorldStatItemPickup>();

        pickup.item = entry.item;
        pickup.amount = Mathf.Max(1, entry.amount);
        pickup.allowNpcPickup = allowNpcPickup;
        pickup.allowPlayerPickup = allowPlayerPickup;
        pickup.requireNpcHarvestAction = requireNpcHarvestAction;
        pickup.harvestDuration = Mathf.Max(0.1f, harvestDuration);
        pickup.destroyWhenEmpty = false;

        WorldResourceNode node = resourceObject.GetComponent<WorldResourceNode>();

        if (node == null)
            node = resourceObject.AddComponent<WorldResourceNode>();

        node.pickup = pickup;
        node.respawnAmount = Mathf.Max(1, respawnAmount);
        node.respawnDelay = respawnDelay;
        node.respawnMode = respawnMode;
        node.respawnRegion = region;

        ResourceNode resourceNode = resourceObject.GetComponent<ResourceNode>();
        if (resourceNode == null)
        {
            resourceNode = resourceObject.AddComponent<ResourceNode>();
        }

        resourceNode.pickup = pickup;
        resourceNode.RefreshResourceKind();

        CircleCollider2D collider = resourceObject.GetComponent<CircleCollider2D>();

        if (collider == null)
            collider = resourceObject.AddComponent<CircleCollider2D>();

        collider.isTrigger = true;
        collider.radius = colliderRadius;

        if (showAutoItemVisual)
        {
            EnsureVisual(resourceObject, entry.item);
        }

        SetupRareItemEffect(resourceObject, entry.item);
    }

    void SetupRareItemEffect(GameObject resourceObject, StatItemData item)
    {
        if (resourceObject == null || item == null)
            return;

        // TEST: Trung sáng.
        // Muốn chỉ Thượng sáng thì đổi ItemGrade.Trung thành ItemGrade.Thuong.
        bool shouldGlow = item.grade == ItemGrade.Trung;

        Transform aura = FindChildRecursive(resourceObject.transform, rareEffectChildName);

        if (aura == null)
        {
            GameObject auraObject = new GameObject(rareEffectChildName);
            auraObject.transform.SetParent(resourceObject.transform, false);
            auraObject.transform.localPosition = Vector3.zero;

            ParticleSystem ps = auraObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psRenderer = auraObject.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.18f;
            main.startSize = 0.06f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 18f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.45f;
            shape.radiusThickness = 0f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalZ = 1.2f;

            var color = ps.colorOverLifetime;
            color.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.cyan, 0f),
                    new GradientColorKey(Color.magenta, 0.35f),
                    new GradientColorKey(Color.yellow, 0.7f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.3f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );

            color.color = gradient;

            psRenderer.sortingOrder = sortingOrder + 50;

            aura = auraObject.transform;
        }

        aura.gameObject.SetActive(shouldGlow);

        ParticleSystem[] particles = aura.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particles)
        {
            if (ps == null)
                continue;

            if (shouldGlow)
            {
                ps.Clear(true);
                ps.Play(true);
            }
            else
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);

            if (found != null)
                return found;
        }

        return null;
    }

    void EnsureVisual(GameObject resourceObject, StatItemData item)
    {
        if (item == null || item.icon == null)
            return;

        SpriteRenderer renderer = resourceObject.GetComponentInChildren<SpriteRenderer>();

        if (renderer == null)
            renderer = resourceObject.AddComponent<SpriteRenderer>();

        renderer.sprite = item.icon;
        renderer.sortingOrder = sortingOrder;
        NormalizeRendererSize(renderer);
    }

    void NormalizeRendererSize(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);

        if (largestSide <= 0f)
            return;

        renderer.transform.localScale = Vector3.one * (visualSize / largestSide);
    }

    void RegisterConfiguredItems()
    {
        foreach (ResourceFieldItemEntry entry in items)
        {
            if (entry != null && entry.item != null)
                GameSaveSystem.RegisterItem(entry.item);
        }
    }

    string GetResourceSaveKey()
    {
        string key = string.IsNullOrEmpty(resourceSaveKey) ? name : resourceSaveKey;
        return ResourceSavePrefix + SceneManager.GetActiveScene().name + "." + key;
    }

    public void SaveResourceState()
    {
        if (!saveResourceState)
            return;

        SavedWorldResourceField data = new SavedWorldResourceField();
        WorldStatItemPickup[] pickups = GetComponentsInChildren<WorldStatItemPickup>(true);

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (pickup == null || pickup.item == null || pickup.amount <= 0)
                continue;

            GameSaveSystem.RegisterItem(pickup.item);

            data.resources.Add(new SavedWorldResourceNode
            {
                itemKey = GameSaveSystem.GetItemKey(pickup.item),
                amount = pickup.amount,
                position = pickup.transform.position,
                active = pickup.gameObject.activeSelf
            });
        }

        string saveKey = GetResourceSaveKey();
        GameSaveSystem.RegisterDynamicSaveKey(saveKey);

        PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
        GameSaveSystem.MarkSaveExists();
        PlayerPrefs.Save();
    }

    bool TryLoadResourceState()
    {
        if (!saveResourceState || !GameSaveSystem.HasSave)
            return false;

        string key = GetResourceSaveKey();

        if (!PlayerPrefs.HasKey(key))
            return false;

        SavedWorldResourceField data =
            JsonUtility.FromJson<SavedWorldResourceField>(PlayerPrefs.GetString(key));

        ClearExistingResources();

        if (data == null || data.resources == null)
            return true;

        foreach (SavedWorldResourceNode saved in data.resources)
        {
            if (saved == null || saved.amount <= 0)
                continue;

            StatItemData item = GameSaveSystem.FindItem(saved.itemKey);

            if (item == null)
                continue;

            ResourceFieldItemEntry entry = new ResourceFieldItemEntry
            {
                item = item,
                amount = Mathf.Max(1, saved.amount),
                weight = 1
            };

            GameObject resourceObject = resourcePrefab != null
                ? Instantiate(resourcePrefab, saved.position, Quaternion.identity, transform)
                : new GameObject("Resource - " + item.itemName);

            if (resourcePrefab == null)
            {
                resourceObject.transform.SetParent(transform, true);
                resourceObject.transform.position = saved.position;
            }

            ConfigureResource(resourceObject, entry);
            resourceObject.SetActive(saved.active);
        }

        return true;
    }

    void ClearExistingResources()
    {
        WorldStatItemPickup[] pickups = GetComponentsInChildren<WorldStatItemPickup>(true);

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (pickup != null)
                Destroy(pickup.gameObject);
        }
    }

    ResourceFieldItemEntry PickItemEntry()
    {
        int totalWeight = 0;

        foreach (ResourceFieldItemEntry entry in items)
        {
            if (entry != null && entry.item != null && CanSpawnItem(entry.item))
                totalWeight += Mathf.Max(1, entry.weight);
        }

        if (totalWeight <= 0)
            return null;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int current = 0;

        foreach (ResourceFieldItemEntry entry in items)
        {
            if (entry == null || entry.item == null || !CanSpawnItem(entry.item))
                continue;

            current += Mathf.Max(1, entry.weight);

            if (roll < current)
                return entry;
        }

        return null;
    }

    bool CanSpawnItem(StatItemData item)
    {
        if (item == null)
            return false;

        if (!naturalResourcesOnly)
            return true;

        if (item.grade > GetNaturalGradeCap())
            return false;

        if (allowMaterials && item.itemType == ItemType.VatLieu)
            return true;

        if (allowFood && item.itemType == ItemType.ThucPham)
            return true;

        if (allowMedicine && item.itemType == ItemType.DanDuoc)
            return true;

        return false;
    }

    ItemGrade GetNaturalGradeCap()
    {
        if (!useAreaDangerTierForNaturalGrade)
        {
            return maxNaturalGrade;
        }

        NpcLocationArea area = GetComponentInParent<NpcLocationArea>();
        if (area == null)
        {
            return maxNaturalGrade;
        }

        switch (area.dangerTier)
        {
            case NpcDangerTier.Low:
                return ItemGrade.Ha;

            case NpcDangerTier.Medium:
                return ItemGrade.Trung;

            case NpcDangerTier.High:
                return ItemGrade.Thuong;

            default:
                return maxNaturalGrade;
        }
    }

    public WorldStatItemPickup GetNearestAvailablePickup(
        Vector3 position,
        StatItemData requiredItem = null,
        NpcMapZone? requiredZone = null,
        NpcDangerTier? requiredDangerTier = null,
        GameObject requester = null)
    {
        WorldStatItemPickup[] pickups = GetComponentsInChildren<WorldStatItemPickup>(true);

        WorldStatItemPickup best = null;
        float bestDistance = float.MaxValue;

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (!IsAvailable(pickup) ||
                pickup.IsReservedByOther(requester) ||
                !MatchesRequiredItem(pickup, requiredItem) ||
                !MatchesRequiredZone(pickup, requiredZone) ||
                !MatchesRequiredDangerTier(pickup, requiredDangerTier))
                continue;

            float distance = Vector2.Distance(position, pickup.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pickup;
            }
        }

        return best;
    }

    public static WorldStatItemPickup GetNearestAvailablePickupInAllFields(
        Vector3 position,
        StatItemData requiredItem = null,
        NpcMapZone? requiredZone = null,
        NpcDangerTier? requiredDangerTier = null,
        GameObject requester = null)
    {
        WorldStatItemPickup best = null;
        float bestDistance = float.MaxValue;

        foreach (WorldResourceField field in fields)
        {
            if (field == null || !field.isActiveAndEnabled)
                continue;

            WorldStatItemPickup candidate =
                field.GetNearestAvailablePickup(
                    position,
                    requiredItem,
                    requiredZone,
                    requiredDangerTier,
                    requester);

            if (candidate == null)
                continue;

            float distance = Vector2.Distance(position, candidate.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    bool MatchesRequiredZone(WorldStatItemPickup pickup, NpcMapZone? requiredZone)
    {
        if (!requiredZone.HasValue)
            return true;

        NpcMapArea area = NpcMapArea.FindArea(pickup.transform.position);

        return area != null && area.zone == requiredZone.Value;
    }

    bool MatchesRequiredDangerTier(
        WorldStatItemPickup pickup,
        NpcDangerTier? requiredDangerTier)
    {
        if (!requiredDangerTier.HasValue)
        {
            return true;
        }

        if (pickup == null)
        {
            return false;
        }

        NpcLocationArea area =
            NpcLocationArea.FindArea(pickup.transform.position);

        if (area == null || area.dangerTier == NpcDangerTier.Any)
        {
            return true;
        }

        return area.dangerTier == requiredDangerTier.Value;
    }

    bool MatchesRequiredItem(WorldStatItemPickup pickup, StatItemData requiredItem)
    {
        if (requiredItem == null)
        {
            return true;
        }

        if (pickup == null || pickup.item == null)
        {
            return false;
        }

        // Có requiredItem nghĩa là NPC đang đi lấy đúng item được chỉ định.
        // Không suy luận theo ResourceKind để tránh lấy nhầm item cùng nhóm
        // hoặc item bị nhận diện sai tên.
        if (pickup.item == requiredItem)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(pickup.item.ItemId) &&
            !string.IsNullOrEmpty(requiredItem.ItemId) &&
            pickup.item.ItemId == requiredItem.ItemId)
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

        SpawnRegion drawRegion = region != null ? region : GetComponent<SpawnRegion>();

        if (drawRegion != null)
            Gizmos.DrawCube(drawRegion.transform.position, drawRegion.size);
    }
}
