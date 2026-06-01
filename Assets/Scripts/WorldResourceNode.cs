using System.Collections;
using UnityEngine;

public enum ResourceRespawnMode
{
    AutomaticByGrade,
    StayInPlace,
    RelocateOnRespawn
}

[RequireComponent(typeof(WorldStatItemPickup))]
public class WorldResourceNode : MonoBehaviour
{
    [Header("Resource")]
    public WorldStatItemPickup pickup;
    public int respawnAmount = 1;
    public float respawnDelay = 30f;
    public ResourceRespawnMode respawnMode = ResourceRespawnMode.AutomaticByGrade;

    [Header("Relocation")]
    public SpawnRegion respawnRegion;
    public float fallbackRandomRadius = 8f;
    public bool relocateTrungGrade = true;
    public bool relocateThuongGrade = true;
    public bool relocateTienGrade = true;

    [Header("Visual")]
    public bool hideRelocatingResourceWhileWaiting = true;
    [Range(0f, 1f)]
    public float depletedAlpha = 1f;

    Renderer[] renderers;
    Collider2D[] colliders;
    Vector3 startPosition;
    bool respawning;

    void Awake()
    {
        CacheReferences();
        startPosition = transform.position;

        if (pickup != null)
        {
            pickup.destroyWhenEmpty = false;
            pickup.OnDepleted += HandleDepleted;
        }
    }

    void OnDestroy()
    {
        if (pickup != null)
        {
            pickup.OnDepleted -= HandleDepleted;
        }
    }

    void OnValidate()
    {
        if (pickup == null)
        {
            pickup = GetComponent<WorldStatItemPickup>();
        }

        respawnAmount = Mathf.Max(1, respawnAmount);
        respawnDelay = Mathf.Max(0f, respawnDelay);
        fallbackRandomRadius = Mathf.Max(0f, fallbackRandomRadius);
    }

    void HandleDepleted()
    {
        if (!isActiveAndEnabled || respawning)
        {
            return;
        }

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        respawning = true;

        bool relocate = ShouldRelocate();
        SetCollidersActive(false);
        SetVisualActive(!relocate || !hideRelocatingResourceWhileWaiting);

        if (!relocate)
        {
            SetRendererAlpha(depletedAlpha);
        }

        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        if (relocate)
        {
            transform.position = GetRespawnPosition();
        }

        if (pickup != null)
        {
            pickup.amount = Mathf.Max(1, respawnAmount);
        }

        SetRendererAlpha(1f);
        SetVisualActive(true);
        SetCollidersActive(true);
        respawning = false;
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
