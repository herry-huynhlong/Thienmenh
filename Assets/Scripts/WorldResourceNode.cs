using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public enum ResourceRespawnMode
{
    AutomaticByGrade,
    StayInPlace,
    RelocateOnRespawn
}

[RequireComponent(typeof(WorldStatItemPickup))]
public class WorldResourceNode : MonoBehaviour, ISerializationCallbackReceiver
{
    const int CurrentTimeDomainVersion = 1;

    public event System.Action OnRespawnStarted;
    public event System.Action OnRespawnCompleted;

    [Header("Resource")]
    public WorldStatItemPickup pickup;
    public int respawnAmount = 1;
    [FormerlySerializedAs("respawnDelay")]
    [Tooltip("World hours before this depleted resource respawns.")]
    public float respawnDurationWorldHours = 30f;
    public ResourceRespawnMode respawnMode = ResourceRespawnMode.AutomaticByGrade;

    [Header("Relocation")]
    public SpawnRegion respawnRegion;
    public float fallbackRandomRadius = 8f;
    public bool relocateTrungGrade = true;
    public bool relocateThuongGrade = true;
    public bool relocateTienGrade = true;

    [Header("Visual")]
    public bool hideRelocatingResourceWhileWaiting = true;
    public bool hideDepletedResourceWhileWaiting = true;
    [Range(0f, 1f)]
    public float depletedAlpha = 0f;

    Renderer[] renderers;
    Collider2D[] colliders;
    Vector3 startPosition;
    bool respawning;
    bool respawnUsesWorldClock;
    double respawnAtWorldHour;
    float respawnAtScaledSeconds;
    WorldTimeSystem subscribedTimeSystem;
    [SerializeField, HideInInspector]
    int timeDomainVersion;

    public bool IsRespawning => respawning;
    public double RespawnAtWorldHour => respawnAtWorldHour;

    void Awake()
    {
        MigrateTimeDomains();
        CacheReferences();
        startPosition = transform.position;
        EnsurePickupSubscription();
    }

    void OnEnable()
    {
        EnsurePickupSubscription();
        BindWorldTimeSystem();
    }

    void OnDisable()
    {
        UnbindWorldTimeSystem();
    }

    void Update()
    {
        BindWorldTimeSystem();
        TryCompleteRespawn();
    }

    void TryCompleteRespawn()
    {
        if (!respawning)
        {
            return;
        }

        if (respawnUsesWorldClock)
        {
            if (!GameTime.TryGetCurrentWorldHour(out double currentWorldHour) ||
                currentWorldHour < respawnAtWorldHour)
            {
                return;
            }
        }
        else if (GameTime.ScaledNowSeconds < respawnAtScaledSeconds)
        {
            return;
        }

        CompleteRespawn();
    }

    void OnDestroy()
    {
        UnbindWorldTimeSystem();
        RemovePickupSubscription();
    }

    void OnValidate()
    {
        MigrateTimeDomains();

        if (pickup == null)
        {
            pickup = GetComponent<WorldStatItemPickup>();
        }

        respawnAmount = Mathf.Max(1, respawnAmount);
        respawnDurationWorldHours =
            Mathf.Max(0f, respawnDurationWorldHours);
        fallbackRandomRadius = Mathf.Max(0f, fallbackRandomRadius);
    }

    void HandleDepleted()
    {
        if (!isActiveAndEnabled || respawning)
        {
            return;
        }

        BeginRespawn();
    }

    void BeginRespawn()
    {
        respawning = true;
        OnRespawnStarted?.Invoke();

        bool relocate = ShouldRelocate();
        bool hideWhileWaiting =
            hideDepletedResourceWhileWaiting ||
            (relocate && hideRelocatingResourceWhileWaiting);

        SetCollidersActive(false);
        SetVisualActive(!hideWhileWaiting);

        if (!hideWhileWaiting)
        {
            SetRendererAlpha(depletedAlpha);
        }

        float durationWorldHours =
            Mathf.Max(0f, respawnDurationWorldHours);
        if (durationWorldHours <= 0f)
        {
            CompleteRespawn();
            return;
        }

        if (GameTime.TryGetCurrentWorldHour(out double currentWorldHour))
        {
            respawnUsesWorldClock = true;
            respawnAtWorldHour = currentWorldHour + durationWorldHours;
            return;
        }

        respawnUsesWorldClock = false;
        respawnAtScaledSeconds =
            GameTime.ScaledNowSeconds +
            GameTime.WorldHoursToScaledSeconds(durationWorldHours);
    }

    void CompleteRespawn()
    {
        if (ShouldRelocate())
        {
            transform.position = GetRespawnPosition();
        }

        if (pickup != null)
        {
            pickup.amount = Mathf.Max(1, respawnAmount);
        }

        OnRespawnCompleted?.Invoke();
        SetRendererAlpha(1f);
        SetVisualActive(true);
        SetCollidersActive(true);
        respawning = false;
    }

    void MigrateTimeDomains()
    {
        if (timeDomainVersion >= CurrentTimeDomainVersion)
        {
            return;
        }

        respawnDurationWorldHours =
            GameTime.LegacyScaledSecondsToWorldHours(
                respawnDurationWorldHours);
        timeDomainVersion = CurrentTimeDomainVersion;
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        MigrateTimeDomains();
    }

    void BindWorldTimeSystem()
    {
        WorldTimeSystem found = WorldTimeSystem.Instance;
        if (subscribedTimeSystem == found)
        {
            return;
        }

        UnbindWorldTimeSystem();
        subscribedTimeSystem = found;
        if (subscribedTimeSystem != null)
        {
            subscribedTimeSystem.OnHourChanged += HandleWorldHourChanged;
        }
    }

    void UnbindWorldTimeSystem()
    {
        if (subscribedTimeSystem != null)
        {
            subscribedTimeSystem.OnHourChanged -= HandleWorldHourChanged;
            subscribedTimeSystem = null;
        }
    }

    void HandleWorldHourChanged(int hour)
    {
        TryCompleteRespawn();
    }

    public void SetPickup(WorldStatItemPickup configuredPickup)
    {
        if (pickup == configuredPickup)
        {
            EnsurePickupSubscription();
            return;
        }

        RemovePickupSubscription();
        pickup = configuredPickup;
        EnsurePickupSubscription();
    }

    bool ShouldRelocate()
    {
        if (respawnMode == ResourceRespawnMode.StayInPlace)
        {
            return false;
        }

        if (respawnMode == ResourceRespawnMode.RelocateOnRespawn)
        {
            return true;
        }

        if (pickup == null || pickup.item == null)
        {
            return false;
        }

        switch (pickup.item.grade)
        {
            case ItemGrade.Trung:
                return relocateTrungGrade;
            case ItemGrade.Thuong:
                return relocateThuongGrade;
            case ItemGrade.Tien:
                return relocateTienGrade;
            default:
                return false;
        }
    }

    Vector3 GetRespawnPosition()
    {
        if (respawnRegion != null)
        {
            return respawnRegion.RandomPoint();
        }

        Vector2 offset =
            Random.insideUnitCircle * fallbackRandomRadius;

        return startPosition + (Vector3)offset;
    }

    void CacheReferences()
    {
        if (pickup == null)
        {
            pickup = GetComponent<WorldStatItemPickup>();
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    void EnsurePickupSubscription()
    {
        if (pickup == null)
        {
            pickup = GetComponent<WorldStatItemPickup>();
        }

        if (pickup == null)
        {
            return;
        }

        pickup.destroyWhenEmpty = false;
        pickup.OnDepleted -= HandleDepleted;
        pickup.OnDepleted += HandleDepleted;
    }

    void RemovePickupSubscription()
    {
        if (pickup != null)
        {
            pickup.OnDepleted -= HandleDepleted;
        }
    }

    void SetVisualActive(bool active)
    {
        if (renderers == null ||
            renderers.Length == 0)
        {
            CacheReferences();
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = active;
            }
        }
    }

    void SetCollidersActive(bool active)
    {
        if (colliders == null ||
            colliders.Length == 0)
        {
            CacheReferences();
        }

        foreach (Collider2D resourceCollider in colliders)
        {
            if (resourceCollider != null)
            {
                resourceCollider.enabled = active;
            }
        }
    }

    void SetRendererAlpha(float alpha)
    {
        if (renderers == null ||
            renderers.Length == 0)
        {
            CacheReferences();
        }

        foreach (Renderer renderer in renderers)
        {
            SpriteRenderer spriteRenderer =
                renderer as SpriteRenderer;

            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = color;
        }
    }
}



