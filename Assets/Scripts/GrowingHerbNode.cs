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
    public float accumulatedGrowthHours;
    public float lastGrowthSampleWorldHour;
    public float lastSnowDamageCheckWorldHour;
    public float visualRandomScale;
}

public enum GrowingHerbVisualStage
{
    Small = 0,
    Mid = 1,
    Mature = 2
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
    [SerializeField] float rainGrowthMultiplier = 1.5f;
    [SerializeField] float snowGrowthMultiplier = 0.5f;
    [SerializeField] float snowDamageCheckIntervalHours = 6f;
    [SerializeField] float snowDamageChancePerCheck = 0.18f;
    [SerializeField] int initialSmallWeight = 30;
    [SerializeField] int initialMidWeight = 40;
    [SerializeField] int initialLargeWeight = 30;
    [SerializeField] float randomScaleMin = 0.92f;
    [SerializeField] float randomScaleMax = 1.08f;
    [SerializeField] float targetVisualSize = 0.45f;
    [SerializeField] int sortingOrder = 20;
    [SerializeField] float growthStartWorldHour = -1f;
    [SerializeField] float accumulatedGrowthHours;
    [SerializeField] float lastGrowthSampleWorldHour = -1f;
    [SerializeField] float lastSnowDamageCheckWorldHour = -1f;
    [SerializeField] float visualRandomScale = 1f;

    bool hasAppliedConfiguration;
    bool subscribed;
    bool configuredAllowNpcPickup;
    bool configuredAllowPlayerPickup;
    bool isDepleted;

    public float MatureAfterGameHours => Mathf.Max(0.5f, matureAfterGameHours);
    public float AccumulatedGrowthHours => Mathf.Clamp(accumulatedGrowthHours, 0f, MatureAfterGameHours);
    public bool IsHarvestAvailable => GetGrowthProgress01() >= 1f;
    public GrowingHerbVisualStage CurrentVisualStage => ResolveVisualStage();

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        SubscribeEvents();
        if (hasAppliedConfiguration)
        {
            UpdateGrowthFromWorldTime();
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
            UpdateGrowthFromWorldTime();
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
        float configuredRainGrowthMultiplier,
        float configuredSnowGrowthMultiplier,
        float configuredSnowDamageCheckIntervalHours,
        float configuredSnowDamageChancePerCheck,
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
        rainGrowthMultiplier = Mathf.Max(1f, configuredRainGrowthMultiplier);
        snowGrowthMultiplier = Mathf.Clamp(configuredSnowGrowthMultiplier, 0.1f, 1f);
        snowDamageCheckIntervalHours = Mathf.Max(0.25f, configuredSnowDamageCheckIntervalHours);
        snowDamageChancePerCheck = Mathf.Clamp01(configuredSnowDamageChancePerCheck);
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
        if (lastGrowthSampleWorldHour < 0f)
        {
            lastGrowthSampleWorldHour = GetCurrentWorldHour();
        }

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

        bool depleted = pickup != null && pickup.amount <= 0;
        isDepleted = depleted;

        if (stageRenderer != null)
        {
            stageRenderer.sprite = depleted ? null : ResolveStageSprite();
            stageRenderer.sortingOrder = sortingOrder;
            stageRenderer.enabled = !depleted && stageRenderer.sprite != null;

            if (!depleted)
            {
                ApplyRendererScale();
            }
        }

        if (pickup != null)
        {
            bool harvestAvailable = !depleted && IsHarvestAvailable;
            pickup.allowNpcPickup = configuredAllowNpcPickup && harvestAvailable;
            pickup.allowPlayerPickup = configuredAllowPlayerPickup && harvestAvailable;
        }
    }

    public string CapturePersistentState()
    {
        GrowingHerbNodeState state = new GrowingHerbNodeState
        {
            growthStartWorldHour = growthStartWorldHour,
            accumulatedGrowthHours = accumulatedGrowthHours,
            lastGrowthSampleWorldHour = lastGrowthSampleWorldHour,
            lastSnowDamageCheckWorldHour = lastSnowDamageCheckWorldHour,
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
            accumulatedGrowthHours = Mathf.Max(0f, state.accumulatedGrowthHours);
            lastGrowthSampleWorldHour = state.lastGrowthSampleWorldHour;
            lastSnowDamageCheckWorldHour = state.lastSnowDamageCheckWorldHour;
            visualRandomScale = state.visualRandomScale <= 0f
                ? 1f
                : state.visualRandomScale;
        }

        if (accumulatedGrowthHours <= 0f && growthStartWorldHour >= 0f)
        {
            accumulatedGrowthHours = Mathf.Clamp(
                GetCurrentWorldHour() - growthStartWorldHour,
                0f,
                Mathf.Max(0.5f, matureAfterGameHours));
        }

        RefreshVisualState();
    }

    float GetGrowthProgress01()
    {
        float matureHours = Mathf.Max(0.5f, matureAfterGameHours);
        return Mathf.Clamp01(accumulatedGrowthHours / matureHours);
    }

    void SetGrowthProgress01(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        float currentWorldHour = GetCurrentWorldHour();
        accumulatedGrowthHours =
            clampedProgress * Mathf.Max(0.5f, matureAfterGameHours);
        growthStartWorldHour =
            currentWorldHour - accumulatedGrowthHours;
        lastGrowthSampleWorldHour = currentWorldHour;
        lastSnowDamageCheckWorldHour = currentWorldHour;
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
        GrowingHerbVisualStage visualStage = ResolveVisualStage();

        if (visualStage == GrowingHerbVisualStage.Mature && largeSprite != null)
        {
            return largeSprite;
        }

        if (visualStage == GrowingHerbVisualStage.Mid && midSprite != null)
        {
            return midSprite;
        }

        if (smallSprite != null)
        {
            return smallSprite;
        }

        return midSprite != null ? midSprite : largeSprite;
    }

    GrowingHerbVisualStage ResolveVisualStage()
    {
        if (IsHarvestAvailable)
        {
            return GrowingHerbVisualStage.Mature;
        }

        if (GetGrowthProgress01() >= midStageThreshold)
        {
            return GrowingHerbVisualStage.Mid;
        }

        return GrowingHerbVisualStage.Small;
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
        isDepleted = false;
        ResetToRespawnSmall();
    }

    void UpdateGrowthFromWorldTime()
    {
        float currentWorldHour = GetCurrentWorldHour();
        if (lastGrowthSampleWorldHour < 0f)
        {
            lastGrowthSampleWorldHour = currentWorldHour;
            if (lastSnowDamageCheckWorldHour < 0f)
            {
                lastSnowDamageCheckWorldHour = currentWorldHour;
            }

            return;
        }

        float elapsedWorldHours = Mathf.Max(0f, currentWorldHour - lastGrowthSampleWorldHour);
        if (elapsedWorldHours <= 0f)
        {
            return;
        }

        accumulatedGrowthHours += elapsedWorldHours * ResolveGrowthMultiplier();
        accumulatedGrowthHours =
            Mathf.Clamp(accumulatedGrowthHours, 0f, Mathf.Max(0.5f, matureAfterGameHours));
        growthStartWorldHour = currentWorldHour - accumulatedGrowthHours;
        lastGrowthSampleWorldHour = currentWorldHour;

        TryApplySnowDamage(currentWorldHour);
    }

    float ResolveGrowthMultiplier()
    {
        WeatherSystem weather = WeatherSystem.Instance;
        if (weather == null)
        {
            return 1f;
        }

        switch (weather.CurrentWeather)
        {
            case WorldWeather.Rain:
            case WorldWeather.Thunder:
                return Mathf.Max(1f, rainGrowthMultiplier);
            case WorldWeather.Snow:
                return Mathf.Clamp(snowGrowthMultiplier, 0.1f, 1f);
            default:
                return 1f;
        }
    }

    void TryApplySnowDamage(float currentWorldHour)
    {
        WeatherSystem weather = WeatherSystem.Instance;
        if (weather == null || weather.CurrentWeather != WorldWeather.Snow)
        {
            return;
        }

        if (IsHarvestAvailable)
        {
            return;
        }

        if (lastSnowDamageCheckWorldHour < 0f)
        {
            lastSnowDamageCheckWorldHour = currentWorldHour;
            return;
        }

        float interval = Mathf.Max(0.25f, snowDamageCheckIntervalHours);
        while (currentWorldHour - lastSnowDamageCheckWorldHour >= interval)
        {
            lastSnowDamageCheckWorldHour += interval;
            if (UnityEngine.Random.value <= snowDamageChancePerCheck)
            {
                ResetToRespawnSmall();
                break;
            }
        }
    }
}
