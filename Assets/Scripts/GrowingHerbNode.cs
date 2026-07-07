using System;
using UnityEngine;

public interface IWorldResourcePersistentState
{
    string CapturePersistentState();
    void RestorePersistentState(string serializedState);
}

[Serializable]
public class GrowingHerbNodeState
{
    public float growthStartWorldHour;
    public float visualRandomScale;
}

public class GrowingHerbNode : MonoBehaviour, IWorldResourcePersistentState
{
    const string VisualChildName = "HerbVisual";

    [SerializeField] WorldStatItemPickup pickup;
    [SerializeField] WorldResourceNode worldNode;
    [SerializeField] SpriteRenderer stageRenderer;
    [SerializeField] Sprite smallSprite;
    [SerializeField] Sprite midSprite;
    [SerializeField] Sprite largeSprite;
    [SerializeField] float matureAfterGameHours = 48f;
    [SerializeField] float midStageThreshold = 0.5f;
    [SerializeField] int initialSmallWeight = 30;
    [SerializeField] int initialMidWeight = 40;
    [SerializeField] int initialLargeWeight = 30;
    [SerializeField] float randomScaleMin = 0.92f;
    [SerializeField] float randomScaleMax = 1.08f;
    [SerializeField] float targetVisualSize = 0.45f;
    [SerializeField] int sortingOrder = 20;
    [SerializeField] float growthStartWorldHour = -1f;
    [SerializeField] float visualRandomScale = 1f;

    bool hasAppliedConfiguration;
    bool subscribed;
    bool configuredAllowNpcPickup;
    bool configuredAllowPlayerPickup;

    public bool IsHarvestAvailable => GetGrowthProgress01() >= 1f;

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        SubscribeEvents();
        if (hasAppliedConfiguration)
        {
            RefreshVisualState();
        }
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    void Update()
    {
        if (hasAppliedConfiguration)
        {
            RefreshVisualState();
        }
    }

    public void Configure(
        WorldStatItemPickup configuredPickup,
        WorldResourceNode configuredWorldNode,
        Sprite configuredSmallSprite,
        Sprite configuredMidSprite,
        Sprite configuredLargeSprite,
        float configuredMatureAfterGameHours,
        float configuredMidStageThreshold,
        int configuredInitialSmallWeight,
        int configuredInitialMidWeight,
        int configuredInitialLargeWeight,
        float configuredRandomScaleMin,
        float configuredRandomScaleMax,
        float configuredTargetVisualSize,
        int configuredSortingOrder)
    {
        pickup = configuredPickup;
        worldNode = configuredWorldNode;
        smallSprite = configuredSmallSprite;
        midSprite = configuredMidSprite;
        largeSprite = configuredLargeSprite;
        matureAfterGameHours = Mathf.Max(0.5f, configuredMatureAfterGameHours);
        midStageThreshold = Mathf.Clamp(configuredMidStageThreshold, 0.05f, 0.95f);
        initialSmallWeight = Mathf.Max(0, configuredInitialSmallWeight);
        initialMidWeight = Mathf.Max(0, configuredInitialMidWeight);
        initialLargeWeight = Mathf.Max(0, configuredInitialLargeWeight);
        randomScaleMin = Mathf.Max(0.1f, configuredRandomScaleMin);
        randomScaleMax = Mathf.Max(randomScaleMin, configuredRandomScaleMax);
        targetVisualSize = Mathf.Max(0.05f, configuredTargetVisualSize);
        sortingOrder = configuredSortingOrder;

        if (pickup != null)
        {
            configuredAllowNpcPickup = pickup.allowNpcPickup;
            configuredAllowPlayerPickup = pickup.allowPlayerPickup;
        }

        hasAppliedConfiguration = true;
        EnsureReferences();
        SubscribeEvents();
        RefreshVisualState();
    }

    public void InitializeForInitialSpawn()
    {
        visualRandomScale = UnityEngine.Random.Range(randomScaleMin, randomScaleMax);

        int totalWeight =
            Mathf.Max(0, initialSmallWeight) +
            Mathf.Max(0, initialMidWeight) +
            Mathf.Max(0, initialLargeWeight);

        if (totalWeight <= 0)
        {
            SetGrowthProgress01(0f);
            RefreshVisualState();
            return;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int current = Mathf.Max(0, initialSmallWeight);
        if (roll < current)
        {
            SetGrowthProgress01(UnityEngine.Random.Range(0f, Mathf.Max(0.05f, midStageThreshold * 0.95f)));
            RefreshVisualState();
            return;
        }

        current += Mathf.Max(0, initialMidWeight);
        if (roll < current)
        {
            SetGrowthProgress01(UnityEngine.Random.Range(midStageThreshold, 0.99f));
            RefreshVisualState();
            return;
        }

        SetGrowthProgress01(1f);
        RefreshVisualState();
    }

    public void ResetToRespawnSmall()
    {
        visualRandomScale = UnityEngine.Random.Range(randomScaleMin, randomScaleMax);
        SetGrowthProgress01(0f);
        RefreshVisualState();
    }

    public void RefreshVisualState()
    {
        EnsureReferences();

        if (stageRenderer != null)
        {
            stageRenderer.sprite = ResolveStageSprite();
            stageRenderer.sortingOrder = sortingOrder;
            stageRenderer.enabled = stageRenderer.sprite != null;
            ApplyRendererScale();
        }

        if (pickup != null)
        {
            pickup.allowNpcPickup = configuredAllowNpcPickup && IsHarvestAvailable;
            pickup.allowPlayerPickup = configuredAllowPlayerPickup && IsHarvestAvailable;
        }
    }

    public string CapturePersistentState()
    {
        GrowingHerbNodeState state = new GrowingHerbNodeState
        {
            growthStartWorldHour = growthStartWorldHour,
            visualRandomScale = visualRandomScale
        };

        return JsonUtility.ToJson(state);
    }

    public void RestorePersistentState(string serializedState)
    {
        if (string.IsNullOrEmpty(serializedState))
        {
            RefreshVisualState();
            return;
        }

        GrowingHerbNodeState state =
            JsonUtility.FromJson<GrowingHerbNodeState>(serializedState);

        if (state != null)
        {
            growthStartWorldHour = state.growthStartWorldHour;
            visualRandomScale = state.visualRandomScale <= 0f
                ? 1f
                : state.visualRandomScale;
        }

        RefreshVisualState();
    }

    float GetGrowthProgress01()
    {
        if (growthStartWorldHour < 0f)
        {
            return 0f;
        }

        float matureHours = Mathf.Max(0.5f, matureAfterGameHours);
        float elapsed = GetCurrentWorldHour() - growthStartWorldHour;
        return Mathf.Clamp01(elapsed / matureHours);
    }

    void SetGrowthProgress01(float progress)
    {
        float currentWorldHour = GetCurrentWorldHour();
        float clampedProgress = Mathf.Clamp01(progress);
        growthStartWorldHour =
            currentWorldHour - clampedProgress * Mathf.Max(0.5f, matureAfterGameHours);
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            timeSystem = WorldTimeSystem.EnsureInstance();
        }

        return timeSystem != null ? timeSystem.CurrentWorldHour : 0f;
    }

    Sprite ResolveStageSprite()
    {
        if (IsHarvestAvailable && largeSprite != null)
        {
            return largeSprite;
        }

        if (GetGrowthProgress01() >= midStageThreshold && midSprite != null)
        {
            return midSprite;
        }

        if (smallSprite != null)
        {
            return smallSprite;
        }

        return midSprite != null ? midSprite : largeSprite;
    }

    void ApplyRendererScale()
    {
        if (stageRenderer == null || stageRenderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = stageRenderer.sprite.bounds.size;
        float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
        if (largestSide <= 0f)
        {
            return;
        }

        float normalizedScale = targetVisualSize / largestSide;
        float finalScale =
            normalizedScale * Mathf.Max(0.1f, visualRandomScale <= 0f ? 1f : visualRandomScale);

        stageRenderer.transform.localScale = Vector3.one * finalScale;
    }

    void EnsureReferences()
    {
        if (pickup == null)
        {
            pickup = GetComponent<WorldStatItemPickup>();
        }

        if (worldNode == null)
        {
            worldNode = GetComponent<WorldResourceNode>();
        }

        if (stageRenderer == null)
        {
            Transform existingVisual = transform.Find(VisualChildName);
            if (existingVisual != null)
            {
                stageRenderer = existingVisual.GetComponent<SpriteRenderer>();
            }
        }

        if (stageRenderer == null)
        {
            GameObject visual = new GameObject(VisualChildName);
            visual.transform.SetParent(transform, false);
            stageRenderer = visual.AddComponent<SpriteRenderer>();
        }
    }

    void SubscribeEvents()
    {
        if (subscribed)
        {
            return;
        }

        EnsureReferences();

        if (worldNode != null)
        {
            worldNode.OnRespawnCompleted += HandleRespawnCompleted;
            subscribed = true;
        }
    }

    void UnsubscribeEvents()
    {
        if (!subscribed)
        {
            return;
        }

        if (worldNode != null)
        {
            worldNode.OnRespawnCompleted -= HandleRespawnCompleted;
        }

        subscribed = false;
    }

    void HandleRespawnCompleted()
    {
        ResetToRespawnSmall();
    }
}
