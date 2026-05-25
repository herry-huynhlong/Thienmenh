using UnityEngine;

public class SpawnRegion : MonoBehaviour
{
    public Vector2 size = new Vector2(12f, 8f);

    public Vector3 RandomPoint()
    {
        Vector2 offset = new Vector2(
            Random.Range(-size.x * 0.5f, size.x * 0.5f),
            Random.Range(-size.y * 0.5f, size.y * 0.5f));

        return transform.position + (Vector3)offset;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, size);
    }
}

public class WorldSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject villagerPrefab;
    public GameObject cultivatorPrefab;
    public GameObject beastPrefab;

    [Header("Regions")]
    public SpawnRegion villageRegion;
    public SpawnRegion forestRegion;

    [Header("Counts")]
    public int initialVillagers = 20;
    public int initialCultivators = 6;
    public int initialBeasts = 14;
    public bool spawnOnStart = true;

    void Start()
    {
        EnsureWorldSystems();

        if (spawnOnStart)
        {
            SpawnInitialWorld();
        }
    }

    public void SpawnInitialWorld()
    {
        SpawnMany(villagerPrefab, EntityKind.Villager, initialVillagers, villageRegion);
        SpawnMany(cultivatorPrefab, EntityKind.Cultivator, initialCultivators, villageRegion);
        SpawnMany(beastPrefab, EntityKind.Beast, initialBeasts, forestRegion);
    }

    void SpawnMany(GameObject prefab, EntityKind kind, int count, SpawnRegion region)
    {
        if (prefab == null || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 position = region != null
                ? region.RandomPoint()
                : transform.position;

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            EntityProfile profile = EntityGenerator.EnsureProfile(instance, kind);
            profile.kind = kind;
            EntityGenerator.FillProfile(profile, kind);
            profile.lockGeneratedValues = true;
            instance.name = profile.identity.entityName + " (" + profile.identity.kind + ")";
        }
    }

    void EnsureWorldSystems()
    {
        if (WorldTimeSystem.Instance == null)
        {
            GameObject time = new GameObject("WorldTimeSystem");
            time.AddComponent<WorldTimeSystem>();
        }

        if (WeatherSystem.Instance == null)
        {
            GameObject weather = new GameObject("WeatherSystem");
            weather.AddComponent<WeatherSystem>();
        }
    }
}
