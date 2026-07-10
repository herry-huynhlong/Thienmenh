using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WeatherVisualSystem : MonoBehaviour
{
    public static WeatherVisualSystem Instance { get; private set; }

    [Header("Target")]
    public Camera targetCamera;
    public float worldZ;
    public float screenMargin = 2f;
    public int sortingOrder = 25000;

    [Header("Rain")]
    public float rainRate = 180f;
    public float thunderRainRate = 300f;
    public Color rainColor = new Color(0.62f, 0.78f, 1f, 0.62f);
    public string rainResourcePath = "thoitiet/rain";
    public string rainEditorAssetPath = "Assets/UI/thoitiet/rain.png";
    public float rainSpriteAnimationFps = 14f;

    [Header("Snow")]
    public float snowRate = 60f;
    public Color snowColor = new Color(1f, 1f, 1f, 0.86f);
    public string snowResourcePath = "thoitiet/snow";
    public string snowEditorAssetPath = "Assets/UI/thoitiet/snow.png";
    public float snowSpriteAnimationFps = 10f;

    [Header("Spiritual Qi")]
    public float qiRate = 45f;
    public Color qiColor = new Color(0.54f, 1f, 0.82f, 0.55f);

    [Header("Thunder")]
    public float thunderFlashMinDelay = 4f;
    public float thunderFlashMaxDelay = 8f;
    public float thunderFlashFadeSpeed = 3.8f;

    ParticleSystem rainParticles;
    ParticleSystem snowParticles;
    ParticleSystem qiParticles;
    Image flashImage;
    WeatherSystem subscribedWeather;
    WorldWeather activeWeather = (WorldWeather)(-1);
    float thunderTimer;
    float thunderFlashAlpha;
    Sprite[] rainSprites;
    Sprite[] snowSprites;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

        SetEmission(rainParticles, GetRainRate(weather));
        SetEmission(snowParticles, weather == WorldWeather.Snow ? snowRate : 0f);
        SetEmission(qiParticles, weather == WorldWeather.DenseSpiritualQi ? qiRate : 0f);

        if (weather != WorldWeather.Thunder)
        {
            thunderTimer = 0f;
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
        rainSprites = LoadSprites(rainResourcePath, rainEditorAssetPath);
        snowSprites = LoadSprites(snowResourcePath, snowEditorAssetPath);
        rainParticles = CreateRainParticles();
        snowParticles = CreateSnowParticles();
        qiParticles = CreateQiParticles();
        BuildThunderFlashOverlay();
    }

    ParticleSystem CreateRainParticles()
    {
        ParticleSystem particles = CreateParticleObject("Weather Rain");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.15f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.09f);
        main.startColor = rainColor;
        main.maxParticles = 1300;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-3.4f, -1.6f);
        velocity.y = new ParticleSystem.MinMaxCurve(-18f, -24f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = sortingOrder;

        if (rainSprites != null && rainSprites.Length > 0)
        {
            ConfigureRainSpriteParticles(particles, renderer, rainSprites);
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.2f;
            renderer.velocityScale = 0.18f;
            AssignMaterial(renderer, rainColor, CreateRainTexture());
        }

        return particles;
    }

    ParticleSystem CreateSnowParticles()
    {
        ParticleSystem particles = CreateParticleObject("Weather Snow");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = snowColor;
        main.maxParticles = 900;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.65f, 0.65f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.9f, -1.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.32f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = sortingOrder;

        if (snowSprites != null && snowSprites.Length > 0)
        {
            ConfigureSnowSpriteParticles(particles, renderer, snowSprites);
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            AssignMaterial(renderer, snowColor, CreateSoftCircleTexture(24));
        }

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
        SetFullScreenEmitter(snowParticles, emitterWidth, height + screenMargin * 2f);
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
            thunderTimer -= Time.unscaledDeltaTime;
            if (thunderTimer <= 0f)
            {
                thunderTimer = Random.Range(
                    Mathf.Max(0.5f, thunderFlashMinDelay),
                    Mathf.Max(thunderFlashMinDelay + 0.5f, thunderFlashMaxDelay));
                thunderFlashAlpha = Random.Range(0.28f, 0.46f);
            }
        }

        thunderFlashAlpha = Mathf.MoveTowards(
            thunderFlashAlpha,
            0f,
            Time.unscaledDeltaTime * thunderFlashFadeSpeed);

        SetFlashAlpha(thunderFlashAlpha);
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

    void ConfigureRainSpriteParticles(
        ParticleSystem particles,
        ParticleSystemRenderer renderer,
        Sprite[] sprites)
    {
        ParticleSystem.MainModule main = particles.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.lengthScale = 1f;
        renderer.velocityScale = 0f;

        Texture2D texture = sprites[0] != null ? sprites[0].texture : null;
        AssignMaterial(renderer, rainColor, texture);
        ConfigureSpriteAnimation(particles, sprites, rainSpriteAnimationFps);
    }

    void ConfigureSnowSpriteParticles(
        ParticleSystem particles,
        ParticleSystemRenderer renderer,
        Sprite[] sprites)
    {
        ParticleSystem.MainModule main = particles.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.52f);

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.lengthScale = 1f;
        renderer.velocityScale = 0f;

        Texture2D texture = sprites[0] != null ? sprites[0].texture : null;
        AssignMaterial(renderer, snowColor, texture);
        ConfigureSpriteAnimation(particles, sprites, snowSpriteAnimationFps);
    }

    void ConfigureSpriteAnimation(
        ParticleSystem particles,
        Sprite[] sprites,
        float fps)
    {
        ParticleSystem.TextureSheetAnimationModule sheet =
            particles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.timeMode = ParticleSystemAnimationTimeMode.FPS;
        sheet.fps = Mathf.Max(1f, fps);

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                sheet.AddSprite(sprites[i]);
            }
        }
    }

    Sprite[] LoadSprites(string resourcePath, string editorAssetPath)
    {
        Sprite[] resourceSprites = Resources.LoadAll<Sprite>(resourcePath);
        if (resourceSprites != null && resourceSprites.Length > 0)
        {
            return resourceSprites;
        }

#if UNITY_EDITOR
        Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(
            editorAssetPath);
        List<Sprite> sprites = new List<Sprite>();

        for (int i = 0; i < subAssets.Length; i++)
        {
            if (subAssets[i] is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        if (sprites.Count > 0)
        {
            return sprites.ToArray();
        }
#endif

        return null;
    }

    Texture2D CreateRainTexture()
    {
        Texture2D texture = new Texture2D(2, 16, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < texture.height; y++)
        {
            float alpha = Mathf.Sin((y + 0.5f) / texture.height * Mathf.PI);
            Color color = new Color(1f, 1f, 1f, alpha);
            texture.SetPixel(0, y, color);
            texture.SetPixel(1, y, color);
        }

        texture.Apply();
        return texture;
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
}
