using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldCampfire : MonoBehaviour
{
    [System.Serializable]
    public struct RuntimeFrameSlice
    {
        public string name;
        public Rect rect;
        public Vector2 pivot;
        public float pixelsPerUnit;
    }

    static readonly HashSet<WorldCampfire> activeCampfires =
        new HashSet<WorldCampfire>();

    [Header("Visual")]
    public SpriteRenderer targetRenderer;
    public Sprite[] frames = new Sprite[24];
    public Texture2D sourceTexture;
    public RuntimeFrameSlice[] runtimeSlices = new RuntimeFrameSlice[0];

    [Header("Frame Ranges")]
    public int buildStartFrame = 0;
    public int buildEndFrame = 5;
    public int burnStartFrame = 15;
    public int burnEndFrame = 23;

    [Header("Timing")]
    [Min(0.1f)] public float burnDurationWorldHours = 8f;
    [Min(0.1f)] public float buildFramesPerSecond = 10f;
    [Min(0.1f)] public float burnLoopFramesPerSecond = 8f;

    [Header("Frame Blending")]
    [Range(0f, 1f)] public float generatedFramePivotY = 0.12f;
    [Range(0.1f, 2f)] public float visualScaleMultiplier = 0.82f;
    [Range(0f, 1f)] public float frameBlendStrength = 0.92f;

    [Header("Burn Motion")]
    [Min(0f)] public float burnPeakHoldFrames = 0.85f;
    [Min(0f)] public float burnFlickerScaleX = 0.025f;
    [Min(0f)] public float burnFlickerScaleY = 0.045f;
    [Min(0f)] public float burnFlickerOffsetX = 0.018f;
    [Min(0f)] public float burnFlickerOffsetY = 0.03f;

    [Header("Lifetime")]
    public bool autoStartOnEnable = true;
    public bool destroyOnExpire = true;
    public bool hideRendererOnExpire = true;

    [Header("Weather")]
    public bool extinguishOnRain = true;
    public bool extinguishOnThunder = true;

    double startedWorldHour = double.NaN;
    float startedScaledTime = float.NaN;
    bool expired;
    WeatherSystem subscribedWeather;
    readonly List<Sprite> generatedSprites = new List<Sprite>();
    readonly Dictionary<int, Sprite> frameLookup =
        new Dictionary<int, Sprite>();
    readonly List<int> buildFrameIds = new List<int>();
    readonly List<int> burnFrameIds = new List<int>();
    SpriteRenderer blendRenderer;
    Vector3 rendererBaseLocalPosition;
    Vector3 rendererBaseLocalScale = Vector3.one;
    bool rendererBaseCaptured;
    float flickerSeed;

    public float RemainingWorldHours
    {
        get
        {
            return Mathf.Max(0f, burnDurationWorldHours - GetElapsedWorldHours());
        }
    }

    public static bool IsCampfireNear(Vector3 position, float radius)
    {
        float safeRadius = Mathf.Max(0f, radius);
        float sqrRadius = safeRadius * safeRadius;

        foreach (WorldCampfire campfire in activeCampfires)
        {
            if (campfire == null)
            {
                continue;
            }

            if ((campfire.transform.position - position).sqrMagnitude <= sqrRadius)
            {
                return true;
            }
        }

        return false;
    }

    void Awake()
    {
        EnsureRenderer();
        EnsureBlendRenderer();
        EnsureRuntimeFrames();
        CaptureRendererBaseTransform();
        flickerSeed = Random.Range(-1000f, 1000f);
    }

    void OnEnable()
    {
        activeCampfires.Add(this);
        EnsureRenderer();
        EnsureBlendRenderer();
        EnsureRuntimeFrames();
        CaptureRendererBaseTransform();
        EnsureWeatherSubscription();

        if (autoStartOnEnable)
        {
            BeginNowIfNeeded();
        }
    }

    void OnDisable()
    {
        activeCampfires.Remove(this);
        ClearWeatherSubscription();
    }

    void Update()
    {
        EnsureRenderer();
        EnsureBlendRenderer();
        EnsureRuntimeFrames();
        EnsureWeatherSubscription();
        BeginNowIfNeeded();

        if (expired)
        {
            return;
        }

        if (ShouldExtinguishForCurrentWeather())
        {
            ExtinguishByWeather();
            return;
        }

        if (targetRenderer == null || frames == null || frames.Length == 0)
        {
            return;
        }

        float elapsedWorldHours = GetElapsedWorldHours();
        if (elapsedWorldHours >= burnDurationWorldHours)
        {
            Expire();
            return;
        }

        UpdateVisual();
    }

    public void BeginNow()
    {
        startedScaledTime = Time.time;

        if (GameTime.TryGetCurrentWorldHour(out double worldHour))
        {
            startedWorldHour = worldHour;
        }
        else
        {
            startedWorldHour = double.NaN;
        }

        expired = false;

        if (targetRenderer != null)
        {
            targetRenderer.enabled = true;
        }

        if (blendRenderer != null)
        {
            blendRenderer.enabled = false;
        }

        ResetBurnFlicker();
        ApplyFrameBlend(buildFrameIds, 0f);
    }

    void BeginNowIfNeeded()
    {
        if (!float.IsNaN(startedScaledTime))
        {
            return;
        }

        BeginNow();
    }

    void UpdateVisual()
    {
        float buildDurationSeconds = GetBuildDurationSeconds();
        float elapsedScaledSeconds = Mathf.Max(0f, Time.time - startedScaledTime);

        if (elapsedScaledSeconds < buildDurationSeconds)
        {
            ResetBurnFlicker();
            float buildProgress = EvaluateForwardFrameProgress(
                buildFrameIds,
                elapsedScaledSeconds,
                buildFramesPerSecond);
            ApplyFrameBlend(buildFrameIds, buildProgress);
            return;
        }

        float burnElapsedSeconds = Mathf.Max(0f, elapsedScaledSeconds - buildDurationSeconds);
        float burnProgress = EvaluateOrganicPingPongProgress(
            burnFrameIds,
            burnElapsedSeconds,
            burnLoopFramesPerSecond);
        ApplyFrameBlend(burnFrameIds, burnProgress);
        ApplyBurnFlicker(burnElapsedSeconds);
    }

    float GetElapsedWorldHours()
    {
        if (!float.IsNaN(startedScaledTime))
        {
            if (!double.IsNaN(startedWorldHour) &&
                GameTime.TryGetCurrentWorldHour(out double currentWorldHour))
            {
                return Mathf.Max(0f, (float)(currentWorldHour - startedWorldHour));
            }

            WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
            float secondsPerDay = timeSystem != null
                ? Mathf.Max(1f, timeSystem.realSecondsPerGameDay)
                : GameTime.LegacyRealSecondsPerWorldDay;
            return GameTime.ScaledSecondsToWorldHours(
                Time.time - startedScaledTime,
                secondsPerDay);
        }

        return 0f;
    }

    float GetBuildDurationSeconds()
    {
        int frameCount = Mathf.Abs(buildEndFrame - buildStartFrame) + 1;
        return frameCount / Mathf.Max(0.1f, buildFramesPerSecond);
    }

    float EvaluateForwardFrameProgress(
        List<int> frameIds,
        float elapsedSeconds,
        float framesPerSecond)
    {
        if (frameIds == null || frameIds.Count == 0)
        {
            return 0f;
        }

        int frameCount = frameIds.Count;
        float rawProgress =
            elapsedSeconds * Mathf.Max(0.1f, framesPerSecond);
        return Mathf.Clamp(rawProgress, 0f, frameCount - 1f);
    }

    float EvaluateOrganicPingPongProgress(
        List<int> frameIds,
        float elapsedSeconds,
        float framesPerSecond)
    {
        if (frameIds == null || frameIds.Count == 0)
        {
            return 0f;
        }

        if (frameIds.Count == 1)
        {
            return 0f;
        }

        float span = frameIds.Count - 1;
        float cycleLength = span * 2f + Mathf.Max(0f, burnPeakHoldFrames);
        float phase = Mathf.Repeat(
            elapsedSeconds * Mathf.Max(0.1f, framesPerSecond),
            Mathf.Max(0.01f, cycleLength));
        float progress;

        if (phase <= span)
        {
            progress = phase;
        }
        else if (phase <= span + burnPeakHoldFrames)
        {
            progress = span;
        }
        else
        {
            progress = span - (phase - span - burnPeakHoldFrames);
        }

        return Mathf.Clamp(progress, 0f, span);
    }

    void ApplyFrameBlend(
        List<int> frameIds,
        float frameProgress)
    {
        if (targetRenderer == null ||
            frameIds == null ||
            frameIds.Count == 0)
        {
            return;
        }

        float clampedProgress =
            Mathf.Clamp(frameProgress, 0f, frameIds.Count - 1f);
        int currentIndex =
            Mathf.Clamp(
                Mathf.FloorToInt(clampedProgress),
                0,
                frameIds.Count - 1);
        int nextIndex =
            Mathf.Clamp(
                currentIndex + 1,
                0,
                frameIds.Count - 1);

        Sprite currentSprite =
            ResolveFrame(frameIds[currentIndex]);
        Sprite nextSprite =
            ResolveFrame(frameIds[nextIndex]);

        if (currentSprite == null &&
            nextSprite == null)
        {
            return;
        }

        float rawBlend =
            nextIndex == currentIndex
                ? 0f
                : Mathf.Clamp01(
                    clampedProgress - currentIndex);
        float easedBlend =
            Mathf.SmoothStep(0f, 1f, rawBlend);
        float blend =
            Mathf.Lerp(
                rawBlend,
                easedBlend,
                Mathf.Clamp01(frameBlendStrength));

        if (currentSprite != null)
        {
            targetRenderer.sprite = currentSprite;
        }

        SetRendererAlpha(
            targetRenderer,
            1f - blend);

        if (blendRenderer == null)
        {
            return;
        }

        if (nextSprite == null ||
            blend <= 0.001f)
        {
            blendRenderer.enabled = false;
            SetRendererAlpha(blendRenderer, 0f);
            return;
        }

        blendRenderer.sprite = nextSprite;
        blendRenderer.enabled = true;
        SetRendererAlpha(blendRenderer, blend);
    }

    void Expire()
    {
        expired = true;

        if (hideRendererOnExpire && targetRenderer != null)
        {
            targetRenderer.enabled = false;
        }

        if (hideRendererOnExpire && blendRenderer != null)
        {
            blendRenderer.enabled = false;
        }

        ResetBurnFlicker();

        if (destroyOnExpire)
        {
            Destroy(gameObject);
        }
    }

    void ExtinguishByWeather()
    {
        Expire();
    }

    void EnsureRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        CaptureRendererBaseTransform();
    }

    void EnsureBlendRenderer()
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (blendRenderer == null)
        {
            Transform child =
                transform.Find("CampfireBlendRenderer");

            if (child != null)
            {
                blendRenderer =
                    child.GetComponent<SpriteRenderer>();
            }
        }

        if (blendRenderer == null)
        {
            GameObject child =
                new GameObject("CampfireBlendRenderer");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            blendRenderer =
                child.AddComponent<SpriteRenderer>();
        }

        blendRenderer.sharedMaterial =
            targetRenderer.sharedMaterial;
        blendRenderer.sortingLayerID =
            targetRenderer.sortingLayerID;
        blendRenderer.sortingOrder =
            targetRenderer.sortingOrder + 1;
        blendRenderer.flipX = targetRenderer.flipX;
        blendRenderer.flipY = targetRenderer.flipY;
        blendRenderer.maskInteraction =
            targetRenderer.maskInteraction;
        blendRenderer.drawMode =
            targetRenderer.drawMode;
        blendRenderer.size =
            targetRenderer.size;
        blendRenderer.enabled =
            blendRenderer.sprite != null &&
            blendRenderer.color.a > 0.001f;
    }

    void EnsureRuntimeFrames()
    {
        if (HasUsableFrames())
        {
            RefreshFrameLookup();
            return;
        }

        if (sourceTexture == null ||
            runtimeSlices == null ||
            runtimeSlices.Length == 0)
        {
            return;
        }

        CleanupGeneratedSprites();

        frames = new Sprite[runtimeSlices.Length];
        for (int i = 0; i < runtimeSlices.Length; i++)
        {
            RuntimeFrameSlice slice = runtimeSlices[i];
            if (slice.rect.width <= 0f ||
                slice.rect.height <= 0f)
            {
                continue;
            }

            Sprite created = Sprite.Create(
                sourceTexture,
                slice.rect,
                new Vector2(
                    slice.pivot == Vector2.zero
                        ? 0.5f
                        : slice.pivot.x,
                    Mathf.Clamp01(generatedFramePivotY)),
                slice.pixelsPerUnit > 0f
                    ? slice.pixelsPerUnit
                    : 100f,
                0,
                SpriteMeshType.FullRect);
            if (created == null)
            {
                continue;
            }

            created.name = string.IsNullOrWhiteSpace(slice.name)
                ? "campfire_frame_" + i
                : slice.name;
            generatedSprites.Add(created);
            frames[i] = created;
        }

        RefreshFrameLookup();
    }

    bool HasUsableFrames()
    {
        if (frames == null || frames.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    void CleanupGeneratedSprites()
    {
        for (int i = 0; i < generatedSprites.Count; i++)
        {
            Sprite generated = generatedSprites[i];
            if (generated == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(generated);
            }
            else
            {
                DestroyImmediate(generated);
            }
        }

        generatedSprites.Clear();
    }

    void EnsureWeatherSubscription()
    {
        WeatherSystem weather = WeatherSystem.Instance;
        if (weather == subscribedWeather)
        {
            return;
        }

        ClearWeatherSubscription();
        subscribedWeather = weather;

        if (subscribedWeather != null)
        {
            subscribedWeather.OnWeatherChanged += HandleWeatherChanged;
        }
    }

    void ClearWeatherSubscription()
    {
        if (subscribedWeather != null)
        {
            subscribedWeather.OnWeatherChanged -= HandleWeatherChanged;
            subscribedWeather = null;
        }
    }

    void HandleWeatherChanged(WorldWeather weather)
    {
        if (expired)
        {
            return;
        }

        if (ShouldExtinguishForWeather(weather))
        {
            ExtinguishByWeather();
        }
    }

    bool ShouldExtinguishForCurrentWeather()
    {
        WeatherSystem weather = WeatherSystem.Instance;
        return weather != null &&
            ShouldExtinguishForWeather(weather.CurrentWeather);
    }

    bool ShouldExtinguishForWeather(WorldWeather weather)
    {
        return (extinguishOnRain && weather == WorldWeather.Rain) ||
            (extinguishOnThunder && weather == WorldWeather.Thunder);
    }

    void CaptureRendererBaseTransform()
    {
        if (targetRenderer == null || rendererBaseCaptured)
        {
            return;
        }

        rendererBaseLocalPosition =
            targetRenderer.transform.localPosition;
        rendererBaseLocalScale =
            targetRenderer.transform.localScale;
        rendererBaseCaptured = true;
    }

    void RefreshFrameLookup()
    {
        frameLookup.Clear();
        buildFrameIds.Clear();
        burnFrameIds.Clear();

        if (frames == null || frames.Length == 0)
        {
            return;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            Sprite sprite = frames[i];
            if (sprite == null)
            {
                continue;
            }

            int frameId;
            if (TryParseFrameId(sprite.name, out frameId))
            {
                frameLookup[frameId] = sprite;
                continue;
            }

            frameLookup[i] = sprite;
        }

        PopulateFrameRange(buildFrameIds, buildStartFrame, buildEndFrame);
        PopulateFrameRange(burnFrameIds, burnStartFrame, burnEndFrame);
    }

    void PopulateFrameRange(
        List<int> target,
        int startFrame,
        int endFrame)
    {
        if (target == null)
        {
            return;
        }

        int minFrame = Mathf.Min(startFrame, endFrame);
        int maxFrame = Mathf.Max(startFrame, endFrame);
        for (int frameId = minFrame; frameId <= maxFrame; frameId++)
        {
            if (frameLookup.ContainsKey(frameId))
            {
                target.Add(frameId);
            }
        }

        if (startFrame > endFrame)
        {
            target.Reverse();
        }
    }

    Sprite ResolveFrame(int frameIndex)
    {
        Sprite sprite;
        if (frameLookup.TryGetValue(frameIndex, out sprite))
        {
            return sprite;
        }

        if (frames == null ||
            frameIndex < 0 ||
            frameIndex >= frames.Length)
        {
            return null;
        }

        return frames[frameIndex];
    }

    bool TryParseFrameId(string name, out int frameId)
    {
        frameId = -1;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        int underscoreIndex = name.LastIndexOf('_');
        string suffix =
            underscoreIndex >= 0 &&
            underscoreIndex < name.Length - 1
                ? name.Substring(underscoreIndex + 1)
                : name;
        return int.TryParse(suffix, out frameId);
    }

    void ApplyBurnFlicker(float burnElapsedSeconds)
    {
        if (targetRenderer == null)
        {
            return;
        }

        CaptureRendererBaseTransform();
        Transform rendererTransform = targetRenderer.transform;
        float horizontalNoise =
            Mathf.PerlinNoise(flickerSeed, burnElapsedSeconds * 2.35f) * 2f - 1f;
        float verticalNoise =
            Mathf.PerlinNoise(flickerSeed + 17.41f, burnElapsedSeconds * 1.9f) * 2f - 1f;
        float scaleNoise =
            Mathf.PerlinNoise(flickerSeed - 8.22f, burnElapsedSeconds * 2.8f) * 2f - 1f;

        rendererTransform.localPosition =
            rendererBaseLocalPosition +
            new Vector3(
                horizontalNoise * burnFlickerOffsetX,
                Mathf.Abs(verticalNoise) * burnFlickerOffsetY,
                0f);
        rendererTransform.localScale =
            new Vector3(
                rendererBaseLocalScale.x *
                visualScaleMultiplier *
                (1f + horizontalNoise * burnFlickerScaleX),
                rendererBaseLocalScale.y *
                visualScaleMultiplier *
                (1f + Mathf.Abs(scaleNoise) * burnFlickerScaleY),
                rendererBaseLocalScale.z);
    }

    void ResetBurnFlicker()
    {
        if (targetRenderer == null || !rendererBaseCaptured)
        {
            return;
        }

        targetRenderer.transform.localPosition =
            rendererBaseLocalPosition;
        targetRenderer.transform.localScale =
            new Vector3(
                rendererBaseLocalScale.x * visualScaleMultiplier,
                rendererBaseLocalScale.y * visualScaleMultiplier,
                rendererBaseLocalScale.z);
    }

    void SetRendererAlpha(
        SpriteRenderer renderer,
        float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.color = color;
    }
}
