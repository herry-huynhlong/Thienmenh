using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class DayNightLightingSystem : MonoBehaviour
{
    public static DayNightLightingSystem Instance { get; private set; }

    [Header("Target")]
    public Light2D globalLight;
    public Camera targetCamera;
    public Image darknessOverlay;

    [Header("Intensity")]
    public float nightIntensity = 0.56f;
    public float dawnIntensity = 0.74f;
    public float dayIntensity = 1f;
    public float eveningIntensity = 0.72f;
    public float transitionSpeed = 2f;

    [Header("Color")]
    public Color nightColor = new Color(0.28f, 0.33f, 0.60f);
    public Color dawnColor = new Color(1f, 0.72f, 0.50f);
    public Color dayColor = Color.white;
    public Color eveningColor = new Color(1f, 0.56f, 0.35f);

    [Header("Screen Darkness Overlay")]
    public bool useScreenOverlay = true;
    public float nightOverlayAlpha = 0.14f;
    public float dawnOverlayAlpha = 0.04f;
    public float dayOverlayAlpha = 0f;
    public float eveningOverlayAlpha = 0.06f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        FindTargets();
        ApplyImmediate();
    }

    void Update()
    {
        if (globalLight == null || targetCamera == null)
        {
            FindTargets();
        }

        if (useScreenOverlay && darknessOverlay == null)
        {
            EnsureOverlay();
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return;
        }

        float targetIntensity = GetTargetIntensity(timeSystem.CurrentHour);
        Color targetColor = GetTargetColor(timeSystem.CurrentHour);

        if (globalLight != null)
        {
            globalLight.intensity =
                Mathf.Lerp(
                    globalLight.intensity,
                    targetIntensity,
                    Time.deltaTime * transitionSpeed);

            globalLight.color =
                Color.Lerp(
                    globalLight.color,
                    targetColor,
                    Time.deltaTime * transitionSpeed);
        }

        if (targetCamera != null)
        {
            Color cameraColor =
                Color.Lerp(
                    Color.black,
                    targetColor,
                    Mathf.Clamp01(targetIntensity));

            targetCamera.backgroundColor =
                Color.Lerp(
                    targetCamera.backgroundColor,
                    cameraColor,
                    Time.deltaTime * transitionSpeed);
        }

        if (darknessOverlay != null)
        {
            float overlayAlpha = GetTargetOverlayAlpha(timeSystem.CurrentHour);
            Color overlayColor = darknessOverlay.color;
            overlayColor.a =
                Mathf.Lerp(
                    overlayColor.a,
                    overlayAlpha,
                    Time.deltaTime * transitionSpeed);
            darknessOverlay.color = overlayColor;
        }
    }

    public void ApplyImmediate()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return;
        }

        float intensity = GetTargetIntensity(timeSystem.CurrentHour);
        Color color = GetTargetColor(timeSystem.CurrentHour);

        if (globalLight != null)
        {
            globalLight.intensity = intensity;
            globalLight.color = color;
        }

        if (targetCamera != null)
        {
            targetCamera.backgroundColor =
                Color.Lerp(
                    Color.black,
                    color,
                    Mathf.Clamp01(intensity));
        }

        if (useScreenOverlay)
        {
            EnsureOverlay();
        }

        if (darknessOverlay != null)
        {
            Color overlayColor = darknessOverlay.color;
            overlayColor.a = GetTargetOverlayAlpha(timeSystem.CurrentHour);
            darknessOverlay.color = overlayColor;
        }
    }

    void FindTargets()
    {
        if (globalLight == null)
        {
            Light2D[] lights =
                Object.FindObjectsByType<Light2D>(
                    FindObjectsInactive.Exclude);

            foreach (Light2D light in lights)
            {
                if (light != null &&
                    light.lightType == Light2D.LightType.Global)
                {
                    globalLight = light;
                    break;
                }
            }
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (useScreenOverlay && darknessOverlay == null)
        {
            EnsureOverlay();
        }
    }

    void EnsureOverlay()
    {
        if (darknessOverlay != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("DayNightDarknessOverlayCanvas");
        DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject overlayObject = new GameObject("DayNightDarknessOverlay");
        overlayObject.transform.SetParent(canvasObject.transform, false);

        darknessOverlay = overlayObject.AddComponent<Image>();
        darknessOverlay.raycastTarget = false;
        darknessOverlay.color = new Color(0f, 0f, 0f, 0f);

        RectTransform rect = darknessOverlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    float GetTargetIntensity(float hour)
    {
        if (hour < 5f) return nightIntensity;
        if (hour < 7f) return Mathf.Lerp(nightIntensity, dawnIntensity, (hour - 5f) / 2f);
        if (hour < 17f) return dayIntensity;
        if (hour < 19f) return Mathf.Lerp(dayIntensity, eveningIntensity, (hour - 17f) / 2f);
        if (hour < 22f) return eveningIntensity;
        return Mathf.Lerp(eveningIntensity, nightIntensity, (hour - 22f) / 2f);
    }

    Color GetTargetColor(float hour)
    {
        if (hour < 5f) return nightColor;
        if (hour < 7f) return Color.Lerp(nightColor, dawnColor, (hour - 5f) / 2f);
        if (hour < 17f) return dayColor;
        if (hour < 19f) return Color.Lerp(dayColor, eveningColor, (hour - 17f) / 2f);
        if (hour < 22f) return eveningColor;
        return Color.Lerp(eveningColor, nightColor, (hour - 22f) / 2f);
    }

    float GetTargetOverlayAlpha(float hour)
    {
        if (hour < 5f) return nightOverlayAlpha;
        if (hour < 7f) return Mathf.Lerp(nightOverlayAlpha, dawnOverlayAlpha, (hour - 5f) / 2f);
        if (hour < 17f) return dayOverlayAlpha;
        if (hour < 19f) return Mathf.Lerp(dayOverlayAlpha, eveningOverlayAlpha, (hour - 17f) / 2f);
        if (hour < 22f) return eveningOverlayAlpha;
        return Mathf.Lerp(eveningOverlayAlpha, nightOverlayAlpha, (hour - 22f) / 2f);
    }
}
