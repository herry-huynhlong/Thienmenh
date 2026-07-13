using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SceneTransitionOverlay : MonoBehaviour
{
    static SceneTransitionOverlay instance;

    Canvas overlayCanvas;
    Image overlayImage;
    float currentAlpha;

    public static SceneTransitionOverlay EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<SceneTransitionOverlay>();
        if (instance != null)
        {
            instance.EnsureVisuals();
            return instance;
        }

        GameObject overlayObject = new GameObject("SceneTransitionOverlay");
        instance = overlayObject.AddComponent<SceneTransitionOverlay>();
        instance.EnsureVisuals();
        return instance;
    }

    public static IEnumerator FadeTo(
        float targetAlpha,
        float duration,
        Color color)
    {
        SceneTransitionOverlay overlay = EnsureInstance();
        yield return overlay.FadeRoutine(targetAlpha, duration, color);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureVisuals();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    void EnsureVisuals()
    {
        DontDestroyOnLoad(gameObject);

        if (overlayCanvas == null)
        {
            overlayCanvas = GetComponent<Canvas>();
            if (overlayCanvas == null)
            {
                overlayCanvas = gameObject.AddComponent<Canvas>();
            }
        }

        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;
        overlayCanvas.pixelPerfect = false;

        if (GetComponent<CanvasScaler>() == null)
        {
            gameObject.AddComponent<CanvasScaler>();
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        if (overlayImage == null)
        {
            Transform existing = transform.Find("Fade");
            if (existing != null)
            {
                overlayImage = existing.GetComponent<Image>();
            }
        }

        if (overlayImage == null)
        {
            GameObject imageObject = new GameObject(
                "Fade",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(transform, false);
            overlayImage = imageObject.GetComponent<Image>();
        }

        RectTransform rect = overlayImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        overlayImage.raycastTarget = false;
        overlayImage.color = new Color(0f, 0f, 0f, currentAlpha);
        overlayImage.enabled = currentAlpha > 0f;
    }

    IEnumerator FadeRoutine(
        float targetAlpha,
        float duration,
        Color color)
    {
        EnsureVisuals();

        float safeTargetAlpha = Mathf.Clamp01(targetAlpha);
        float safeDuration = Mathf.Max(0f, duration);
        Color fadeColor = color;
        fadeColor.a = 1f;

        if (safeDuration <= 0f)
        {
            currentAlpha = safeTargetAlpha;
            ApplyColor(fadeColor);
            yield break;
        }

        float startAlpha = currentAlpha;
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            currentAlpha = Mathf.Lerp(startAlpha, safeTargetAlpha, t);
            ApplyColor(fadeColor);
            yield return null;
        }

        currentAlpha = safeTargetAlpha;
        ApplyColor(fadeColor);
    }

    void ApplyColor(Color color)
    {
        if (overlayImage == null)
        {
            return;
        }

        Color nextColor = color;
        nextColor.a = currentAlpha;
        overlayImage.color = nextColor;
        overlayImage.enabled = currentAlpha > 0.001f;
    }
}
