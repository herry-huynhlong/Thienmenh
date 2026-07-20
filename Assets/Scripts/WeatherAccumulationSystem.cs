using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class WeatherAccumulationPersistentState
{
    public bool hasState = true;
    public float rainAccumulation;
    public float snowAccumulation;
}

[DefaultExecutionOrder(1100)]
public class WeatherAccumulationSystem : MonoBehaviour, ISerializationCallbackReceiver
{
    const int CurrentTimeDomainVersion = 1;
    class PuddleEntry
    {
        public GameObject gameObject;
        public SpriteRenderer renderer;
        public float targetAlpha;
    }

    class SnowEntry
    {
        public GameObject gameObject;
        public SpriteRenderer renderer;
        public SpriteRenderer anchor;
        public float targetAlpha;
        public float scale;
        public Vector3 offset;
    }

    public static WeatherAccumulationSystem Instance { get; private set; }
    bool createdAtRuntime;

    [Header("Scene")]
    public Camera targetCamera;
    public float worldZ = 0.1f;
    public LayerMask snowAnchorLayers = 1 << 6;

    [Header("Rain Accumulation")]
    public int maxPuddles = 8;
    [FormerlySerializedAs("rainBuildSeconds")]
    public float rainBuildDurationWorldHours = 18f;
    [FormerlySerializedAs("rainFadeSeconds")]
    public float rainFadeDurationWorldHours = 10f;
    public float thunderBuildMultiplier = 1.5f;
    public float puddleSpawnPadding = 0.8f;
    public float puddleMinSpacing = 1.4f;
    public float puddleMaxAlpha = 0.72f;
    public float puddleMinScale = 0.48f;
    public float puddleMaxScale = 0.82f;
    public int puddleObjectLayer = 0;
    public string puddleSortingLayerName = "Default";
    public int puddleSortingOrder = 0;
    public string puddleResourcePath = "thoitiet/vungnuoc";
    public string puddleEditorAssetPath = "Assets/UI/thoitiet/vungnuoc.png";

    [Header("Snow Accumulation")]
    public int maxSnowCaps = 10;
    [FormerlySerializedAs("snowBuildSeconds")]
    public float snowBuildDurationWorldHours = 22f;
    [FormerlySerializedAs("snowFadeSeconds")]
    public float snowFadeDurationWorldHours = 16f;
    public float snowCapMaxAlpha = 0.92f;
    public float snowTopInsetNormalized = 0.08f;
    public float snowCapWidthMultiplierMin = 0.38f;
    public float snowCapWidthMultiplierMax = 0.7f;
    public float snowCapMinWidth = 0.45f;
    public float snowCapMaxWidthNormalized = 0.72f;
    public int snowObjectLayer = 1;
    public string snowResourcePath = "thoitiet/loptuyet";
    public string snowEditorAssetPath = "Assets/UI/thoitiet/loptuyet.png";

    readonly List<PuddleEntry> puddles = new List<PuddleEntry>();
    readonly List<SnowEntry> snowCaps = new List<SnowEntry>();
    readonly List<SpriteRenderer> obstacleRenderers = new List<SpriteRenderer>();

    Sprite[] puddleSprites;
    Sprite[] snowSprites;
    float rainAccumulation;
    float snowAccumulation;
    float refreshTimerScaledSeconds;
    double lastObservedWorldHour;
    bool hasObservedWorldHour;
    [SerializeField, HideInInspector]
    int timeDomainVersion;

    public float RainAccumulation => rainAccumulation;
    public float SnowAccumulation => snowAccumulation;

    public WeatherAccumulationPersistentState CapturePersistentState()
    {
        return new WeatherAccumulationPersistentState
        {
            hasState = true,
            rainAccumulation = rainAccumulation,
            snowAccumulation = snowAccumulation
        };
    }

    public void RestorePersistentState(
        WeatherAccumulationPersistentState saved)
    {
        if (saved == null || !saved.hasState)
        {
            return;
        }

        rainAccumulation = Mathf.Clamp01(saved.rainAccumulation);
        snowAccumulation = Mathf.Clamp01(saved.snowAccumulation);
        refreshTimerScaledSeconds = 0f;
        ResetWorldHourCursor();
    }

    public void MarkCreatedAtRuntime()
    {
        createdAtRuntime = true;
    }

    void Awake()
    {
        MigrateTimeDomains();

        if (Instance != null && Instance != this)
        {
            if (Instance.createdAtRuntime && !createdAtRuntime)
            {
                Destroy(Instance.gameObject);
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Instance = this;
        }

        DontDestroyOnLoad(gameObject);
        EnsureDefaultSnowAnchorLayers();
        puddleSprites = LoadRequiredSprites(
            puddleResourcePath,
            "WeatherAccumulationSystem puddles");
        snowSprites = LoadRequiredSprites(
            snowResourcePath,
            "WeatherAccumulationSystem snow caps");
        ResetWorldHourCursor();
    }

    void OnValidate()
    {
        MigrateTimeDomains();
        EnsureDefaultSnowAnchorLayers();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        UpdateAccumulation(ConsumeElapsedWorldHours());
        UpdateEntries(GameTime.ScaledDeltaSeconds);

        refreshTimerScaledSeconds -= GameTime.ScaledDeltaSeconds;
        if (refreshTimerScaledSeconds <= 0f)
        {
            refreshTimerScaledSeconds = 1.2f;
            RefreshObstacleRenderers();
            SyncPuddles();
            SyncSnowCaps();
        }
    }

    void UpdateAccumulation(float elapsedWorldHours)
    {
        WeatherSystem weather = WeatherSystem.Instance;
        WorldWeather currentWeather =
            weather != null ? weather.CurrentWeather : WorldWeather.Clear;

        float rainBuildRate =
            1f / Mathf.Max(0.01f, rainBuildDurationWorldHours);
        if (currentWeather == WorldWeather.Thunder)
        {
            rainBuildRate *= Mathf.Max(1f, thunderBuildMultiplier);
        }

        float rainTarget =
            currentWeather == WorldWeather.Rain ||
            currentWeather == WorldWeather.Thunder
                ? 1f
                : 0f;
        float rainChangeRate =
            rainTarget > rainAccumulation
                ? rainBuildRate
                : 1f / Mathf.Max(0.01f, rainFadeDurationWorldHours);
        rainAccumulation = Mathf.MoveTowards(
            rainAccumulation,
            rainTarget,
            elapsedWorldHours * rainChangeRate);

        float snowTarget = currentWeather == WorldWeather.Snow ? 1f : 0f;
        float snowChangeRate =
            snowTarget > snowAccumulation
                ? 1f / Mathf.Max(0.01f, snowBuildDurationWorldHours)
                : 1f / Mathf.Max(0.01f, snowFadeDurationWorldHours);
        snowAccumulation = Mathf.MoveTowards(
            snowAccumulation,
            snowTarget,
            elapsedWorldHours * snowChangeRate);
    }

    float ConsumeElapsedWorldHours()
    {
        if (GameTime.TryGetCurrentWorldHour(out double currentWorldHour))
        {
            if (!hasObservedWorldHour)
            {
                lastObservedWorldHour = currentWorldHour;
                hasObservedWorldHour = true;
                return 0f;
            }

            double elapsed = currentWorldHour - lastObservedWorldHour;
            lastObservedWorldHour = currentWorldHour;
            if (elapsed <= 0d)
            {
                return 0f;
            }

            return elapsed >= float.MaxValue
                ? float.MaxValue
                : (float)elapsed;
        }

        hasObservedWorldHour = false;
        return GameTime.ScaledSecondsToWorldHours(
            GameTime.ScaledDeltaSeconds,
            GameTime.LegacyRealSecondsPerWorldDay);
    }

    void ResetWorldHourCursor()
    {
        hasObservedWorldHour =
            GameTime.TryGetCurrentWorldHour(out lastObservedWorldHour);
    }

    void MigrateTimeDomains()
    {
        if (timeDomainVersion >= CurrentTimeDomainVersion)
        {
            return;
        }

        rainBuildDurationWorldHours =
            GameTime.LegacyScaledSecondsToWorldHours(
                rainBuildDurationWorldHours);
        rainFadeDurationWorldHours =
            GameTime.LegacyScaledSecondsToWorldHours(
                rainFadeDurationWorldHours);
        snowBuildDurationWorldHours =
            GameTime.LegacyScaledSecondsToWorldHours(
                snowBuildDurationWorldHours);
        snowFadeDurationWorldHours =
            GameTime.LegacyScaledSecondsToWorldHours(
                snowFadeDurationWorldHours);
        timeDomainVersion = CurrentTimeDomainVersion;
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        MigrateTimeDomains();
    }

    void RefreshObstacleRenderers()
    {
        obstacleRenderers.Clear();
        if (snowAnchorLayers.value == 0)
        {
            return;
        }

        SpriteRenderer[] renderers =
            FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Exclude);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null ||
                renderer.sprite == null ||
                (snowAnchorLayers.value & (1 << renderer.gameObject.layer)) == 0 ||
                !renderer.enabled)
            {
                continue;
            }

            obstacleRenderers.Add(renderer);
        }
    }

    void SyncPuddles()
    {
        int desiredCount =
            puddleSprites != null && puddleSprites.Length > 0
                ? Mathf.RoundToInt(rainAccumulation * maxPuddles)
                : 0;

        for (int i = 0; i < puddles.Count; i++)
        {
            puddles[i].targetAlpha =
                i < desiredCount
                    ? puddleMaxAlpha
                    : 0f;
        }

        int spawnAttempts = 0;
        while (puddles.Count < desiredCount && spawnAttempts < desiredCount * 8)
        {
            spawnAttempts++;
            TrySpawnPuddle();
        }
    }

    void SyncSnowCaps()
    {
        int desiredCount =
            snowSprites != null && snowSprites.Length > 0
                ? Mathf.RoundToInt(snowAccumulation * maxSnowCaps)
                : 0;

        for (int i = 0; i < snowCaps.Count; i++)
        {
            snowCaps[i].targetAlpha = 0f;
        }

        if (desiredCount <= 0 || obstacleRenderers.Count == 0)
        {
            return;
        }

        List<SpriteRenderer> candidates = new List<SpriteRenderer>(obstacleRenderers);
        candidates.Sort((a, b) => b.bounds.size.x.CompareTo(a.bounds.size.x));

        int activated = 0;
        for (int i = 0; i < candidates.Count && activated < desiredCount; i++)
        {
            SpriteRenderer candidate = candidates[i];
            if (candidate == null || !IsRendererVisible(candidate))
            {
                continue;
            }

            SnowEntry existing = FindSnowEntry(candidate);
            if (existing == null)
            {
                existing = CreateSnowEntry(candidate);
                if (existing == null)
                {
                    continue;
                }
            }

            UpdateSnowPlacement(existing);
            existing.targetAlpha = snowCapMaxAlpha;
            activated++;
        }
    }

    void UpdateEntries(float deltaTime)
    {
        for (int i = puddles.Count - 1; i >= 0; i--)
        {
            PuddleEntry entry = puddles[i];
            if (entry == null || entry.renderer == null)
            {
                puddles.RemoveAt(i);
                continue;
            }

            Color color = entry.renderer.color;
            color.a = Mathf.MoveTowards(
                color.a,
                entry.targetAlpha,
                deltaTime * 0.75f);
            entry.renderer.color = color;

            if (entry.targetAlpha <= 0f && color.a <= 0.01f)
            {
                Destroy(entry.gameObject);
                puddles.RemoveAt(i);
            }
        }

        for (int i = snowCaps.Count - 1; i >= 0; i--)
        {
            SnowEntry entry = snowCaps[i];
            if (entry == null || entry.renderer == null || entry.anchor == null)
            {
                if (entry != null && entry.gameObject != null)
                {
                    Destroy(entry.gameObject);
                }

                snowCaps.RemoveAt(i);
                continue;
            }

            UpdateSnowPlacement(entry);

            Color color = entry.renderer.color;
            color.a = Mathf.MoveTowards(
                color.a,
                entry.targetAlpha,
                deltaTime * 0.7f);
            entry.renderer.color = color;

            if (entry.targetAlpha <= 0f && color.a <= 0.01f)
            {
                Destroy(entry.gameObject);
                snowCaps.RemoveAt(i);
            }
        }
    }

    bool TrySpawnPuddle()
    {
        if (targetCamera == null || puddleSprites == null || puddleSprites.Length == 0)
        {
            return false;
        }

        Bounds cameraBounds = GetCameraBounds();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 position = new Vector3(
                UnityEngine.Random.Range(
                    cameraBounds.min.x + puddleSpawnPadding,
                    cameraBounds.max.x - puddleSpawnPadding),
                UnityEngine.Random.Range(
                    cameraBounds.min.y + puddleSpawnPadding,
                    cameraBounds.max.y - puddleSpawnPadding),
                worldZ);

            if (IntersectsObstacle(position) || TooCloseToOtherPuddles(position))
            {
                continue;
            }

            Sprite sprite = puddleSprites[UnityEngine.Random.Range(0, puddleSprites.Length)];
            if (sprite == null)
            {
                continue;
            }

            GameObject puddleObject = new GameObject("WeatherPuddle");
            puddleObject.transform.SetParent(transform, false);
            puddleObject.layer = Mathf.Clamp(puddleObjectLayer, 0, 31);
            puddleObject.transform.position = position;

            float randomScale = UnityEngine.Random.Range(
                Mathf.Min(puddleMinScale, puddleMaxScale),
                Mathf.Max(puddleMinScale, puddleMaxScale));
            puddleObject.transform.localScale = new Vector3(
                randomScale,
                randomScale,
                1f);

            SpriteRenderer renderer = puddleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, 0f);
            renderer.sortingLayerName = puddleSortingLayerName;
            renderer.sortingOrder = puddleSortingOrder;

            puddles.Add(new PuddleEntry
            {
                gameObject = puddleObject,
                renderer = renderer,
                targetAlpha = puddleMaxAlpha
            });
            return true;
        }

        return false;
    }

    SnowEntry CreateSnowEntry(SpriteRenderer anchor)
    {
        if (anchor == null || snowSprites == null || snowSprites.Length == 0)
        {
            return null;
        }

        Sprite sprite = PickBestSnowSprite(anchor.bounds.size.x);
        if (sprite == null)
        {
            return null;
        }

        GameObject snowObject = new GameObject("WeatherSnowCap");
        snowObject.transform.SetParent(transform, false);
        snowObject.layer = Mathf.Clamp(snowObjectLayer, 0, 31);
        snowObject.transform.position = anchor.bounds.center;

        SpriteRenderer renderer = snowObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 1f, 1f, 0f);
        renderer.sortingLayerID = anchor.sortingLayerID;
        renderer.sortingOrder = anchor.sortingOrder + 1;

        SnowEntry entry = new SnowEntry
        {
            gameObject = snowObject,
            renderer = renderer,
            anchor = anchor,
            targetAlpha = 0f
        };

        snowCaps.Add(entry);
        ConfigureSnowShape(entry);
        UpdateSnowPlacement(entry);
        return entry;
    }

    void ConfigureSnowShape(SnowEntry entry)
    {
        if (entry == null || entry.anchor == null || entry.renderer == null)
        {
            return;
        }

        Bounds anchorBounds = entry.anchor.bounds;
        Vector2 spriteSize = entry.renderer.sprite != null
            ? entry.renderer.sprite.bounds.size
            : Vector2.one;
        if (spriteSize.x <= 0.001f)
        {
            spriteSize = Vector2.one;
        }

        float random01 = Mathf.Abs(
            Mathf.Sin(
                UnityObjectIdUtility.GetRuntimeId(entry.anchor) * 0.173f));
        float widthMultiplier = Mathf.Lerp(
            Mathf.Min(
                snowCapWidthMultiplierMin,
                snowCapWidthMultiplierMax),
            Mathf.Max(
                snowCapWidthMultiplierMin,
                snowCapWidthMultiplierMax),
            random01);
        float desiredWidth = Mathf.Clamp(
            anchorBounds.size.x * widthMultiplier,
            Mathf.Max(0.1f, snowCapMinWidth),
            anchorBounds.size.x *
                Mathf.Clamp01(snowCapMaxWidthNormalized));
        entry.scale = desiredWidth / spriteSize.x;

        float horizontalSlack =
            Mathf.Max(
                0f,
                anchorBounds.extents.x - spriteSize.x * entry.scale * 0.5f);
        float offsetX = Mathf.Lerp(
            -horizontalSlack * 0.55f,
            horizontalSlack * 0.55f,
            Mathf.Abs(
                Mathf.Sin(
                    UnityObjectIdUtility.GetRuntimeId(entry.anchor) * 0.417f)));

        entry.offset = new Vector3(offsetX, 0f, 0f);
    }

    void UpdateSnowPlacement(SnowEntry entry)
    {
        if (entry == null || entry.anchor == null || entry.renderer == null)
        {
            return;
        }

        Bounds anchorBounds = entry.anchor.bounds;
        if (entry.renderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = entry.renderer.sprite.bounds.size;
        if (spriteSize.x <= 0.001f)
        {
            return;
        }

        if (entry.scale <= 0.001f)
        {
            ConfigureSnowShape(entry);
        }

        float offsetY =
            anchorBounds.extents.y -
            spriteSize.y * entry.scale * 0.42f -
            anchorBounds.size.y * snowTopInsetNormalized;
        float horizontalSlack =
            Mathf.Max(
                0f,
                anchorBounds.extents.x - spriteSize.x * entry.scale * 0.5f);
        float offsetX = Mathf.Clamp(entry.offset.x, -horizontalSlack, horizontalSlack);

        entry.offset = new Vector3(offsetX, offsetY, 0f);
        entry.gameObject.transform.position =
            new Vector3(
                anchorBounds.center.x + entry.offset.x,
                anchorBounds.center.y + entry.offset.y,
                worldZ);
        entry.gameObject.transform.localScale =
            new Vector3(entry.scale, entry.scale, 1f);

        entry.renderer.sortingLayerID = entry.anchor.sortingLayerID;
        entry.renderer.sortingOrder = entry.anchor.sortingOrder + 1;
    }

    SnowEntry FindSnowEntry(SpriteRenderer anchor)
    {
        for (int i = 0; i < snowCaps.Count; i++)
        {
            if (snowCaps[i] != null && snowCaps[i].anchor == anchor)
            {
                return snowCaps[i];
            }
        }

        return null;
    }

    Sprite PickBestSnowSprite(float targetWidth)
    {
        Sprite bestSprite = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < snowSprites.Length; i++)
        {
            Sprite sprite = snowSprites[i];
            if (sprite == null)
            {
                continue;
            }

            float spriteWidth = sprite.bounds.size.x;
            float distance = Mathf.Abs(targetWidth - spriteWidth);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSprite = sprite;
            }
        }

        return bestSprite;
    }

    bool IntersectsObstacle(Vector3 position)
    {
        for (int i = 0; i < obstacleRenderers.Count; i++)
        {
            SpriteRenderer renderer = obstacleRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds expanded = renderer.bounds;
            expanded.Expand(0.2f);
            if (expanded.Contains(position))
            {
                return true;
            }
        }

        return false;
    }

    bool TooCloseToOtherPuddles(Vector3 position)
    {
        for (int i = 0; i < puddles.Count; i++)
        {
            PuddleEntry entry = puddles[i];
            if (entry == null || entry.gameObject == null)
            {
                continue;
            }

            if (Vector2.Distance(
                    entry.gameObject.transform.position,
                    position) < puddleMinSpacing)
            {
                return true;
            }
        }

        return false;
    }

    bool IsRendererVisible(SpriteRenderer renderer)
    {
        if (renderer == null || targetCamera == null)
        {
            return false;
        }

        Bounds cameraBounds = GetCameraBounds();
        Bounds rendererBounds = renderer.bounds;
        return rendererBounds.max.x >= cameraBounds.min.x &&
            rendererBounds.min.x <= cameraBounds.max.x &&
            rendererBounds.max.y >= cameraBounds.min.y &&
            rendererBounds.min.y <= cameraBounds.max.y;
    }

    Bounds GetCameraBounds()
    {
        if (targetCamera == null || !targetCamera.orthographic)
        {
            return new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f));
        }

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;
        return new Bounds(
            new Vector3(
                targetCamera.transform.position.x,
                targetCamera.transform.position.y,
                worldZ),
            new Vector3(width, height, 100f));
    }

    public static void ValidateRequiredSpriteResource(
        string resourcePath,
        string systemLabel)
    {
        Sprite[] sprites =
            Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length == 0)
        {
            throw new InvalidOperationException(
                systemLabel +
                " requires sprite assets at Resources/" +
                resourcePath +
                " but none were loaded.");
        }

        bool hasUsableSprite = false;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                hasUsableSprite = true;
                break;
            }
        }

        if (!hasUsableSprite)
        {
            throw new InvalidOperationException(
                systemLabel +
                " requires usable sprites at Resources/" +
                resourcePath +
                " but all loaded entries were null.");
        }
    }

    Sprite[] LoadRequiredSprites(string resourcePath, string systemLabel)
    {
        ValidateRequiredSpriteResource(resourcePath, systemLabel);
        return Resources.LoadAll<Sprite>(resourcePath);
    }

    void EnsureDefaultSnowAnchorLayers()
    {
        IncludeLayer(ref snowAnchorLayers, "Obstacle");
        IncludeLayer(ref snowAnchorLayers, "nha");
    }

    static void IncludeLayer(ref LayerMask mask, string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            return;
        }

        mask |= 1 << layer;
    }
}
