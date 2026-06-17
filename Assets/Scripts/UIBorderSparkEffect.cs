using UnityEngine;
using UnityEngine.UI;

public class UIBorderSparkEffect : MonoBehaviour
{
    [Header("Target")]
    public RectTransform target;

    [Header("Behavior")]
    public bool autoPlay = true;
    public bool loop = true;
    public bool drawBorder = false;
    public bool reverseDirection = false;
    public bool useUnscaledTime = true;
    public bool hideWhenDisabled = true;

    [Header("Motion")]
    public float speed = 320f;
    public float borderInset = 4f;
    public float borderThickness = 3f;
    public float sparkSize = 18f;

    [Header("Look")]
    public Color borderColor = new Color(1f, 0.9f, 0.45f, 0.18f);
    public Color sparkColor = new Color(1f, 0.98f, 0.75f, 1f);
    public Color glowColor = new Color(1f, 0.82f, 0.2f, 0.35f);
    public Vector2 glowOffset = new Vector2(0f, 0f);
    public Vector2 shadowDistance = new Vector2(0f, 0f);

    RectTransform overlayRoot;
    RectTransform sparkRoot;
    RectTransform sparkGlowRoot;
    Image sparkCore;
    Image sparkGlow;
    Image topBorder;
    Image bottomBorder;
    Image leftBorder;
    Image rightBorder;

    Sprite uiSprite;
    Sprite sparkSprite;
    float travelDistance;
    float pathLength;
    float cachedWidth;
    float cachedHeight;
    bool built;
    bool playing;

    void Awake()
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }

        BuildIfNeeded();
    }

    void OnEnable()
    {
        BuildIfNeeded();

        if (autoPlay)
        {
            Play();
        }
        else
        {
            SetVisible(false);
        }
    }

    void OnDisable()
    {
        Stop();

        if (hideWhenDisabled)
        {
            SetVisible(false);
        }
    }

    void LateUpdate()
    {
        if (!built)
        {
            BuildIfNeeded();
            return;
        }

        if (target == null)
        {
            return;
        }

        if (target.hasChanged)
        {
            target.hasChanged = false;
            RefreshGeometry();
        }

        if (!playing)
        {
            return;
        }

        Advance();
    }

    void OnRectTransformDimensionsChange()
    {
        if (built)
        {
            RefreshGeometry();
        }
    }

    public void Play()
    {
        BuildIfNeeded();

        if (!built)
        {
            return;
        }

        playing = true;
        SetVisible(true);

        if (pathLength <= 0f)
        {
            RefreshGeometry();
        }

        travelDistance = reverseDirection ? pathLength : 0f;
        UpdateSpark(travelDistance);
    }

    public void Stop()
    {
        playing = false;
    }

    public void Restart()
    {
        Stop();
        Play();
    }

    void Advance()
    {
        if (pathLength <= 0f)
        {
            RefreshGeometry();
            if (pathLength <= 0f)
            {
                return;
            }
        }

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (delta <= 0f)
        {
            return;
        }

        float direction = reverseDirection ? -1f : 1f;
        travelDistance += direction * speed * delta;

        if (loop)
        {
            travelDistance = Mathf.Repeat(travelDistance, pathLength);
        }
        else
        {
            if (travelDistance < 0f || travelDistance > pathLength)
            {
                travelDistance = Mathf.Clamp(travelDistance, 0f, pathLength);
                UpdateSpark(travelDistance);
                Stop();
                return;
            }
        }

        UpdateSpark(travelDistance);
    }

    void BuildIfNeeded()
    {
        if (built)
        {
            return;
        }

        if (target == null)
        {
            target = transform as RectTransform;
        }

        if (target == null)
        {
            return;
        }

        uiSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        sparkSprite = CreateSparkSprite();

        GameObject rootObject = new GameObject("UIBorderSparkOverlay", typeof(RectTransform));
        rootObject.transform.SetParent(target, false);
        overlayRoot = rootObject.GetComponent<RectTransform>();
        overlayRoot.anchorMin = Vector2.zero;
        overlayRoot.anchorMax = Vector2.one;
        overlayRoot.offsetMin = Vector2.zero;
        overlayRoot.offsetMax = Vector2.zero;
        overlayRoot.localScale = Vector3.one;
        overlayRoot.localRotation = Quaternion.identity;
        overlayRoot.SetAsLastSibling();

        if (drawBorder)
        {
            leftBorder = CreateBorderEdge("LeftBorder", overlayRoot, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(borderInset, 0f), new Vector2(borderThickness, 0f), borderColor);
            rightBorder = CreateBorderEdge("RightBorder", overlayRoot, new Vector2(1f, 0.5f), new Vector2(1f, 0f), new Vector2(-borderInset, 0f), new Vector2(borderThickness, 0f), borderColor);
        }

        sparkGlowRoot = CreateSparkNode("SparkGlow", overlayRoot, glowColor, sparkSize * 1.6f, true);
        sparkRoot = CreateSparkNode("SparkCore", overlayRoot, sparkColor, sparkSize, false);

        if (sparkGlowRoot != null)
        {
            sparkGlowRoot.anchoredPosition = Vector2.zero;
        }

        if (sparkRoot != null)
        {
            sparkRoot.anchoredPosition = Vector2.zero;
        }

        built = true;
        RefreshGeometry();
        SetVisible(autoPlay);
    }

    void RefreshGeometry()
    {
        if (!built || target == null)
        {
            return;
        }

        Rect rect = target.rect;
        cachedWidth = Mathf.Max(1f, rect.width);
        cachedHeight = Mathf.Max(1f, rect.height);
        pathLength = 2f * (cachedWidth + cachedHeight);

        if (drawBorder)
        {
            float verticalSize = Mathf.Max(1f, cachedHeight - borderInset * 2f);
            SetBorderSize(leftBorder, new Vector2(borderThickness, verticalSize), new Vector2(-(cachedWidth * 0.5f - borderThickness * 0.5f), 0f));
            SetBorderSize(rightBorder, new Vector2(borderThickness, verticalSize), new Vector2(cachedWidth * 0.5f - borderThickness * 0.5f, 0f));
        }

        if (sparkGlowRoot != null)
        {
            sparkGlowRoot.sizeDelta = new Vector2(sparkSize * 1.6f, sparkSize * 1.6f);
        }

        if (sparkRoot != null)
        {
            sparkRoot.sizeDelta = new Vector2(sparkSize, sparkSize);
        }

        if (playing)
        {
            UpdateSpark(travelDistance);
        }
    }

    void UpdateSpark(float distance)
    {
        if (pathLength <= 0f)
        {
            return;
        }

        float t = Mathf.Repeat(distance, pathLength);
        float halfW = cachedWidth * 0.5f - borderInset;
        float halfH = cachedHeight * 0.5f - borderInset;
        halfW = Mathf.Max(1f, halfW);
        halfH = Mathf.Max(1f, halfH);

        Vector2 position;
        float angle;

        float top = cachedWidth;
        float right = top + cachedHeight;
        float bottom = right + cachedWidth;

        if (t < top)
        {
            position = new Vector2(-halfW + t, halfH);
            angle = 0f;
        }
        else if (t < right)
        {
            position = new Vector2(halfW, halfH - (t - top));
            angle = -90f;
        }
        else if (t < bottom)
        {
            position = new Vector2(halfW - (t - right), -halfH);
            angle = 180f;
        }
        else
        {
            position = new Vector2(-halfW, -halfH + (t - bottom));
            angle = 90f;
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.08f;

        if (sparkGlowRoot != null)
        {
            sparkGlowRoot.anchoredPosition = position + glowOffset;
            sparkGlowRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            sparkGlowRoot.localScale = Vector3.one * (pulse * 1.15f);
        }

        if (sparkRoot != null)
        {
            sparkRoot.anchoredPosition = position;
            sparkRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            sparkRoot.localScale = Vector3.one * pulse;
        }
    }

    Image CreateBorderEdge(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color)
    {
        GameObject edgeObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        edgeObject.transform.SetParent(parent, false);

        RectTransform rect = edgeObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = edgeObject.GetComponent<Image>();
        image.sprite = uiSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    RectTransform CreateSparkNode(
        string name,
        Transform parent,
        Color color,
        float size,
        bool glow)
    {
        GameObject sparkObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sparkObject.transform.SetParent(parent, false);

        RectTransform rect = sparkObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);

        Image image = sparkObject.GetComponent<Image>();
        image.sprite = sparkSprite != null ? sparkSprite : uiSprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;

        if (glow)
        {
            Shadow shadow = sparkObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(1f, 0.95f, 0.55f, 0.5f);
            shadow.effectDistance = shadowDistance;
            shadow.useGraphicAlpha = true;
        }

        return rect;
    }

    Sprite CreateSparkSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center.x) / radius;
                float dy = (y - center.y) / radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float core = Mathf.Exp(-dist * dist * 18f);
                float cross = Mathf.Exp(-(dx * dx) * 48f) + Mathf.Exp(-(dy * dy) * 48f);
                float diagA = Mathf.Exp(-((dx - dy) * (dx - dy)) * 28f);
                float diagB = Mathf.Exp(-((dx + dy) * (dx + dy)) * 28f);
                float spike = Mathf.Max(cross, Mathf.Max(diagA, diagB));

                float alpha = Mathf.Clamp01(core * 0.85f + spike * 0.35f);
                if (alpha < 0.01f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                Color color = Color.white;
                color.a = alpha;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        Rect rect = new Rect(0f, 0f, size, size);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
    }

    void SetBorderSize(Image image, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;
    }

    void SetVisible(bool visible)
    {
        if (overlayRoot != null)
        {
            overlayRoot.gameObject.SetActive(visible);
        }
    }
}
