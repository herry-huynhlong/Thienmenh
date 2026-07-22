using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class WeatherVisualSystem : MonoBehaviour
{
    public static WeatherVisualSystem Instance { get; private set; }
    bool createdAtRuntime;

    [Header("Target")]
    public Camera targetCamera;
    public float worldZ;
    public float screenMargin = 2f;
    public int sortingOrder = 25000;

    [Header("Rain")]
    public float rainRate = 180f;
    public float thunderRainRate = 300f;
    public Color rainColor = new Color(0.62f, 0.78f, 1f, 0.62f);
    [Range(0.1f, 1.5f)] public float rainFarLayerRateMultiplier = 0.65f;
    [Range(0.1f, 1.5f)] public float rainNearLayerRateMultiplier = 1.15f;
    [Range(0f, 4f)] public float rainWindJitter = 0.45f;

    [Header("Snow")]
    public float snowRate = 60f;
    public Color snowColor = new Color(1f, 1f, 1f, 0.86f);
    public float snowflakeMinSize = 0.18f;
    public float snowflakeMaxSize = 0.32f;
    [Range(0.1f, 1.5f)] public float snowFarLayerRateMultiplier = 0.65f;
    [Range(0.1f, 1.5f)] public float snowNearLayerRateMultiplier = 1.2f;
    [Range(0f, 2f)] public float snowDriftStrength = 0.55f;

    [Header("Spiritual Qi")]
    public float qiRate = 45f;
    public Color qiColor = new Color(0.54f, 1f, 0.82f, 0.55f);

    [Header("Thunder")]
    [FormerlySerializedAs("thunderFlashMinDelay")]
    public float thunderFlashMinDelayUnscaledSeconds = 4f;
    [FormerlySerializedAs("thunderFlashMaxDelay")]
    public float thunderFlashMaxDelayUnscaledSeconds = 8f;
    public float thunderFlashFadeSpeed = 3.8f;

    ParticleSystem rainParticles;
    ParticleSystem rainFarParticles;
    ParticleSystem snowParticles;
    ParticleSystem snowFarParticles;
    ParticleSystem qiParticles;
    Image flashImage;
    WeatherSystem subscribedWeather;
    WorldWeather activeWeather = (WorldWeather)(-1);
    float thunderTimerUnscaledSeconds;
    float thunderFlashAlpha;
    float windTime;

    public void MarkCreatedAtRuntime()
    {
        createdAtRuntime = true;
    }

    void Awake()
    {
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
        BuildVisuals();
        ApplyWeather(WorldWeather.Clear);
    }

    void OnEnable()
    {
        EnsureWeatherSubscription();
    }

    void OnDisable()
    {
        if (subscribedWeather != null)
        {
            subscribedWeather.OnWeatherChanged -= ApplyWeather;
            subscribedWeather = null;
        }
    }

    void Update()
    {
        EnsureWeatherSubscription();
        UpdateWeatherMotion();
        UpdateThunderFlash();
    }

    void LateUpdate()
    {
        FollowCamera();
        UpdateEmitterBounds();
    }

    void EnsureWeatherSubscription()
    {
        WeatherSystem weather = WeatherSystem.Instance;
        if (weather == subscribedWeather)
        {
            return;
        }

        if (subscribedWeather != null)
        {
            subscribedWeather.OnWeatherChanged -= ApplyWeather;
        }

        subscribedWeather = weather;

        if (subscribedWeather != null)
        {
            subscribedWeather.OnWeatherChanged += ApplyWeather;
            ApplyWeather(subscribedWeather.CurrentWeather);
        }
        else
        {
            ApplyWeather(WorldWeather.Clear);
        }
    }

    void ApplyWeather(WorldWeather weather)
    {
        activeWeather = weather;

        float rainIntensity = GetRainRate(weather);
        SetEmission(rainParticles, rainIntensity * rainNearLayerRateMultiplier);
        SetEmission(rainFarParticles, rainIntensity * rainFarLayerRateMultiplier);

        float snowIntensity = weather == WorldWeather.Snow ? snowRate : 0f;
        SetEmission(snowParticles, snowIntensity * snowNearLayerRateMultiplier);
        SetEmission(snowFarParticles, snowIntensity * snowFarLayerRateMultiplier);
        SetEmission(qiParticles, weather == WorldWeather.DenseSpiritualQi ? qiRate : 0f);

        if (weather != WorldWeather.Thunder)
        {
            thunderTimerUnscaledSeconds = 0f;
            thunderFlashAlpha = 0f;
            SetFlashAlpha(0f);
        }
    }

    float GetRainRate(WorldWeather weather)
    {
        if (weather == WorldWeather.Rain)
        {
            return rainRate;
        }

        if (weather == WorldWeather.Thunder)
        {
            return thunderRainRate;
        }

        return 0f;
    }

    void SetEmission(ParticleSystem particles, float rate)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;

        if (rate > 0f)
        {
            if (!particles.isPlaying)
            {
                particles.Play();
            }
        }
        else if (particles.isPlaying)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void BuildVisuals()
    {
        rainParticles = CreateRainParticles(false);
        rainFarParticles = CreateRainParticles(true);
        snowParticles = CreateSnowParticles(false);
        snowFarParticles = CreateSnowParticles(true);
        qiParticles = CreateQiParticles();
        BuildThunderFlashOverlay();
    }

    ParticleSystem CreateRainParticles(bool farLayer)
    {
        ParticleSystem particles = CreateParticleObject(
            farLayer ? "Weather Rain Far" : "Weather Rain Near");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = farLayer
            ? new ParticleSystem.MinMaxCurve(0.95f, 1.35f)
            : new ParticleSystem.MinMaxCurve(0.6f, 0.95f);
        main.startSpeed = 0f;
        main.startSize = farLayer
            ? new ParticleSystem.MinMaxCurve(0.16f, 0.24f)
            : new ParticleSystem.MinMaxCurve(0.24f, 0.38f);
        main.startColor = farLayer
            ? MultiplyAlpha(rainColor, 0.55f)
            : rainColor;
        main.maxParticles = farLayer ? 900 : 1500;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = farLayer
            ? new ParticleSystem.MinMaxCurve(-2.25f, -1.15f)
            : new ParticleSystem.MinMaxCurve(-4.9f, -2.6f);
        velocity.y = farLayer
            ? new ParticleSystem.MinMaxCurve(-13f, -17f)
            : new ParticleSystem.MinMaxCurve(-21f, -28f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = farLayer ? 0.1f : 0.18f;
        noise.strengthY = farLayer ? 0.06f : 0.1f;
        noise.frequency = farLayer ? 0.18f : 0.24f;
        noise.scrollSpeed = 0.3f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ConfigureFadeByLifetime(
            particles,
            farLayer ? 0.1f : 0.18f,
            farLayer ? 0.7f : 0.82f,
            farLayer ? 0.85f : 0.95f);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = sortingOrder + (farLayer ? -2 : 0);
        ConfigureRainParticles(particles, renderer, farLayer);

        return particles;
    }

    ParticleSystem CreateSnowParticles(bool farLayer)
    {
        ParticleSystem particles = CreateParticleObject(
            farLayer ? "Weather Snow Far" : "Weather Snow Near");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = farLayer
            ? new ParticleSystem.MinMaxCurve(5.5f, 8.5f)
            : new ParticleSystem.MinMaxCurve(4.2f, 6.6f);
        main.startSpeed = 0f;
        main.startSize = farLayer
            ? new ParticleSystem.MinMaxCurve(
                snowflakeMinSize * 0.55f,
                snowflakeMaxSize * 0.75f)
            : new ParticleSystem.MinMaxCurve(
                snowflakeMinSize,
                snowflakeMaxSize * 1.15f);
        main.startColor = farLayer
            ? MultiplyAlpha(snowColor, 0.55f)
            : snowColor;
        main.maxParticles = farLayer ? 520 : 820;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = farLayer
            ? new ParticleSystem.MinMaxCurve(-0.28f, 0.28f)
            : new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        velocity.y = farLayer
            ? new ParticleSystem.MinMaxCurve(-0.45f, -0.95f)
            : new ParticleSystem.MinMaxCurve(-0.9f, -1.65f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = farLayer ? 0.18f : 0.42f;
        noise.strengthY = farLayer ? 0.08f : 0.12f;
        noise.frequency = farLayer ? 0.14f : 0.26f;
        noise.scrollSpeed = 0.18f;
        noise.damping = true;

        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = farLayer
            ? new ParticleSystem.MinMaxCurve(-0.15f, 0.15f)
            : new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        ConfigureFadeByLifetime(
            particles,
            farLayer ? 0.08f : 0.12f,
            farLayer ? 0.72f : 0.86f,
            farLayer ? 0.86f : 0.95f);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = sortingOrder + (farLayer ? -1 : 1);
        ConfigureSnowParticles(particles, renderer, farLayer);

        return particles;
    }

    ParticleSystem CreateQiParticles()
    {
        ParticleSystem particles = CreateParticleObject("Weather Spiritual Qi");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 4.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = qiColor;
        main.maxParticles = 360;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = sortingOrder;
        AssignMaterial(renderer, qiColor, CreateSoftCircleTexture(24));

        return particles;
    }

    ParticleSystem CreateParticleObject(string objectName)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(transform, false);
        return particleObject.AddComponent<ParticleSystem>();
    }

    void BuildThunderFlashOverlay()
    {
        GameObject canvasObject = new GameObject("Weather Thunder Flash Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder + 10;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject imageObject = new GameObject("Weather Thunder Flash");
        imageObject.transform.SetParent(canvasObject.transform, false);
        flashImage = imageObject.AddComponent<Image>();
        flashImage.raycastTarget = false;
        flashImage.color = new Color(0.82f, 0.9f, 1f, 0f);

        RectTransform rect = flashImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void FollowCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = targetCamera.transform.position;
        transform.position = new Vector3(cameraPosition.x, cameraPosition.y, worldZ);
    }

    void UpdateEmitterBounds()
    {
        float width = 18f;
        float height = 10f;

        if (targetCamera != null && targetCamera.orthographic)
        {
            height = targetCamera.orthographicSize * 2f;
            width = height * targetCamera.aspect;
        }

        float emitterWidth = width + screenMargin * 2f;
        float topY = height * 0.5f + screenMargin;

        SetTopEmitter(rainParticles, emitterWidth, topY);
        SetTopEmitter(rainFarParticles, emitterWidth, topY);
        SetFullScreenEmitter(snowParticles, emitterWidth, height + screenMargin * 2f);
        SetFullScreenEmitter(snowFarParticles, emitterWidth, height + screenMargin * 2f);
        SetFullScreenEmitter(qiParticles, emitterWidth, height + screenMargin * 2f);
    }

    void SetTopEmitter(
        ParticleSystem particles,
        float width,
        float topY)
    {
        if (particles == null)
        {
            return;
        }

        particles.transform.localPosition = new Vector3(0f, topY, 0f);
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.scale = new Vector3(width, 0.1f, 0.1f);
    }

    void SetFullScreenEmitter(
        ParticleSystem particles,
        float width,
        float height)
    {
        if (particles == null)
        {
            return;
        }

        particles.transform.localPosition = Vector3.zero;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.scale = new Vector3(width, height, 0.1f);
    }

    void UpdateThunderFlash()
    {
        if (activeWeather == WorldWeather.Thunder)
        {
            thunderTimerUnscaledSeconds -= GameTime.UnscaledDeltaSeconds;
            if (thunderTimerUnscaledSeconds <= 0f)
            {
                thunderTimerUnscaledSeconds = UnityEngine.Random.Range(
                    Mathf.Max(0.5f, thunderFlashMinDelayUnscaledSeconds),
                    Mathf.Max(
                        thunderFlashMinDelayUnscaledSeconds + 0.5f,
                        thunderFlashMaxDelayUnscaledSeconds));
                thunderFlashAlpha = UnityEngine.Random.Range(0.28f, 0.46f);
            }
        }

        thunderFlashAlpha = Mathf.MoveTowards(
            thunderFlashAlpha,
            0f,
            GameTime.UnscaledDeltaSeconds * thunderFlashFadeSpeed);

        SetFlashAlpha(thunderFlashAlpha);
    }

    void UpdateWeatherMotion()
    {
        windTime += GameTime.UnscaledDeltaSeconds;

        float rainWind =
            Mathf.Sin(windTime * 0.8f) * rainWindJitter +
            (Mathf.PerlinNoise(windTime * 0.22f, 0.17f) - 0.5f) * rainWindJitter;
        float snowWind =
            Mathf.Sin(windTime * 0.45f) * snowDriftStrength +
            (Mathf.PerlinNoise(windTime * 0.12f, 0.51f) - 0.5f) * snowDriftStrength;

        SetForceX(rainParticles, -0.18f + rainWind * 0.55f);
        SetForceX(rainFarParticles, -0.1f + rainWind * 0.35f);
        SetForceX(snowParticles, snowWind);
        SetForceX(snowFarParticles, snowWind * 0.55f);
    }

    void SetFlashAlpha(float alpha)
    {
        if (flashImage == null)
        {
            return;
        }

        Color color = flashImage.color;
        color.a = Mathf.Clamp01(alpha);
        flashImage.color = color;
    }

    void AssignMaterial(ParticleSystemRenderer renderer, Color color, Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            return;
        }

        Material material = new Material(shader);
        material.color = color;
        if (texture != null)
        {
            material.mainTexture = texture;
        }

        renderer.material = material;
    }

    void ConfigureRainParticles(
        ParticleSystem particles,
        ParticleSystemRenderer renderer,
        bool farLayer)
    {
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = farLayer ? 1.4f : 1.9f;
        renderer.velocityScale = farLayer ? 0.16f : 0.24f;
        renderer.cameraVelocityScale = 0f;

        AssignMaterial(
            renderer,
            farLayer ? MultiplyAlpha(rainColor, 0.55f) : rainColor,
            CreateRainStreakTexture(farLayer ? 20 : 28));
    }

    void ConfigureSnowParticles(
        ParticleSystem particles,
        ParticleSystemRenderer renderer,
        bool farLayer)
    {
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.lengthScale = 1f;
        renderer.velocityScale = 0f;

        AssignMaterial(
            renderer,
            farLayer ? MultiplyAlpha(snowColor, 0.55f) : snowColor,
            CreateSnowflakeTexture(farLayer ? 18 : 24));
    }

    void ConfigureFadeByLifetime(
        ParticleSystem particles,
        float fadeInEnd,
        float stableAlpha,
        float fadeOutStart)
    {
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(stableAlpha, Mathf.Clamp01(fadeInEnd)),
                new GradientAlphaKey(stableAlpha, Mathf.Clamp01(fadeOutStart)),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    void SetForceX(ParticleSystem particles, float forceX)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.ForceOverLifetimeModule force = particles.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.Local;
        force.x = new ParticleSystem.MinMaxCurve(forceX);
    }

    public static void ValidateRequiredSpriteResource(
        string resourcePath,
        string systemLabel)
    {
        Shader spriteShader = Shader.Find("Sprites/Default");
        Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (spriteShader == null && urpParticleShader == null)
        {
            throw new InvalidOperationException(
                systemLabel +
                " requires either 'Sprites/Default' or " +
                "'Universal Render Pipeline/Particles/Unlit' to be available.");
        }
    }

    Texture2D CreateSoftCircleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - distance / radius);
                alpha *= alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    Texture2D CreateRainStreakTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float centerX = (size - 1) * 0.5f;
        float halfWidth = Mathf.Max(1.25f, size * 0.09f);

        for (int y = 0; y < size; y++)
        {
            float vertical = Mathf.InverseLerp(0f, size - 1f, y);
            float tipFade = Mathf.SmoothStep(0f, 1f, vertical) *
                Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(vertical - 0.72f) * 1.1f);

            for (int x = 0; x < size; x++)
            {
                float distanceX = Mathf.Abs(x - centerX);
                float sideFade = Mathf.Clamp01(1f - distanceX / halfWidth);
                float alpha = tipFade * sideFade * sideFade;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    Texture2D CreateSnowflakeTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y) - center;
                float distance = point.magnitude;
                if (distance > radius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float angle = Mathf.Atan2(point.y, point.x);
                float sixFold = Mathf.Abs(Mathf.Cos(angle * 3f));
                float core = Mathf.Clamp01(1f - distance / radius);
                float branch = Mathf.SmoothStep(0.35f, 1f, sixFold) * Mathf.SmoothStep(0f, 1f, core);
                float sparkle = Mathf.PerlinNoise((x + 7f) * 0.21f, (y + 13f) * 0.21f) * 0.18f;
                float alpha = Mathf.Clamp01(core * 0.55f + branch * 0.5f + sparkle - 0.08f);
                alpha *= alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    static Color MultiplyAlpha(Color color, float alphaMultiplier)
    {
        color.a *= Mathf.Clamp01(alphaMultiplier);
        return color;
    }
}
