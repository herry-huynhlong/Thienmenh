using UnityEngine;

[ExecuteAlways]
public class ResponsivePanelFitter : MonoBehaviour
{
    public RectTransform targetRoot;

    [Header("Design Size")]
    public Vector2 designSize =
        new Vector2(520f, 760f);

    [Header("Screen Padding")]
    public float leftPadding = 8f;
    public float rightPadding = 8f;
    public float topPadding = 8f;
    public float bottomReservedHeight = 90f;

    [Header("Scale")]
    public float minScale = 0.75f;
    public float maxScale = 1.15f;
    public bool preferWidthFit = true;
    public bool useSafeArea = true;

    int lastScreenWidth;
    int lastScreenHeight;
    Rect lastSafeArea;

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        if (lastScreenWidth == Screen.width &&
            lastScreenHeight == Screen.height &&
            lastSafeArea == Screen.safeArea)
        {
            return;
        }

        Apply();
    }

    [ContextMenu("Apply Responsive Fit")]
    public void Apply()
    {
        if (targetRoot == null)
        {
            targetRoot =
                GetComponent<RectTransform>();
        }

        if (targetRoot == null)
        {
            return;
        }

        RectTransform parent =
            targetRoot.parent as RectTransform;

        Rect availableRect =
            parent != null
            ? parent.rect
            : new Rect(0f, 0f, Screen.width, Screen.height);

        Rect safeArea =
            new Rect(
                0f,
                0f,
                availableRect.width,
                availableRect.height);

        float availableWidth =
            safeArea.width -
            leftPadding -
            rightPadding;

        float availableHeight =
            safeArea.height -
            topPadding -
            bottomReservedHeight;

        if (availableWidth <= 0f ||
            availableHeight <= 0f ||
            designSize.x <= 0f ||
            designSize.y <= 0f)
        {
            return;
        }

        float widthScale =
            availableWidth / designSize.x;

        float heightScale =
            availableHeight / designSize.y;

        float scale =
            preferWidthFit
            ? widthScale
            : Mathf.Min(widthScale, heightScale);

        scale =
            Mathf.Clamp(
                scale,
                minScale,
                maxScale);

        targetRoot.anchorMin =
            new Vector2(0.5f, 0.5f);

        targetRoot.anchorMax =
            new Vector2(0.5f, 0.5f);

        targetRoot.pivot =
            new Vector2(0.5f, 0.5f);

        targetRoot.sizeDelta =
            designSize;

        targetRoot.localScale =
            new Vector3(scale, scale, 1f);

        float centerX =
            leftPadding +
            availableWidth * 0.5f -
            availableRect.width * 0.5f;

        float centerY =
            bottomReservedHeight +
            availableHeight * 0.5f -
            availableRect.height * 0.5f;

        targetRoot.anchoredPosition =
            new Vector2(centerX, centerY);

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
    }
}
