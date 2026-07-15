using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class TimeHudToggleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RectTransform hudRoot;
    [SerializeField] RectTransform toggleButtonRect;
    [SerializeField] Button toggleButton;

    [Header("State")]
    [SerializeField] bool startExpanded = true;

    [Header("Layout")]
    [SerializeField] float collapsedTopPadding = 4f;
    [SerializeField] float animationSpeed = 12f;

    RectTransform[] contentItems = System.Array.Empty<RectTransform>();
    CanvasGroup[] contentGroups = System.Array.Empty<CanvasGroup>();
    Vector2 expandedHudSize;
    Vector2 expandedHudPosition;
    Vector2 expandedButtonPosition;
    float collapsedHudHeight;
    float currentHudHeight = -1f;
    bool isExpanded;
    bool initialized;

    void Awake()
    {
        EnsureReferences();
        CacheContentItems();
        CaptureExpandedLayout();
        SetExpandedState(startExpanded, true);
    }

    void OnEnable()
    {
        EnsureReferences();
        CacheContentItems();
        CaptureExpandedLayout();
        SetExpandedState(startExpanded, true);
    }

    void Update()
    {
        if (!initialized)
        {
            EnsureReferences();
            CacheContentItems();
            CaptureExpandedLayout();
            SetExpandedState(startExpanded, true);
        }

        AnimateState();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureReferences();
    }
#endif

    public void Toggle()
    {
        SetExpandedState(!isExpanded, false);
    }

    public void Expand()
    {
        SetExpandedState(true, false);
    }

    public void Collapse()
    {
        SetExpandedState(false, false);
    }

    void EnsureReferences()
    {
        if (hudRoot == null)
        {
            hudRoot = transform as RectTransform;
        }

        if (toggleButton == null)
        {
            toggleButton = FindButtonByName("Button");
            if (toggleButton == null)
            {
                toggleButton = GetComponentInChildren<Button>(true);
            }
        }

        if (toggleButtonRect == null && toggleButton != null)
        {
            toggleButtonRect = toggleButton.transform as RectTransform;
        }
    }

    void CacheContentItems()
    {
        if (hudRoot == null || toggleButtonRect == null)
        {
            return;
        }

        int contentCount = 0;
        for (int i = hudRoot.childCount - 1; i >= 0; i--)
        {
            RectTransform child = hudRoot.GetChild(i) as RectTransform;
            if (child == null || child == toggleButtonRect)
            {
                continue;
            }

            contentCount++;
        }

        RectTransform[] cachedItems = new RectTransform[contentCount];
        int index = 0;
        for (int i = 0; i < hudRoot.childCount; i++)
        {
            RectTransform child = hudRoot.GetChild(i) as RectTransform;
            if (child == null || child == toggleButtonRect)
            {
                continue;
            }

            cachedItems[index++] = child;
        }

        contentItems = cachedItems;
        contentGroups = new CanvasGroup[cachedItems.Length];

        for (int i = 0; i < cachedItems.Length; i++)
        {
            RectTransform item = cachedItems[i];
            if (item == null)
            {
                continue;
            }

            CanvasGroup group = item.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = item.gameObject.AddComponent<CanvasGroup>();
            }

            contentGroups[i] = group;
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(Toggle);
            toggleButton.onClick.AddListener(Toggle);
        }

        EnsureMask();
    }

    void CaptureExpandedLayout()
    {
        if (hudRoot == null || toggleButtonRect == null)
        {
            return;
        }

        expandedHudSize = hudRoot.sizeDelta;
        expandedHudPosition = hudRoot.anchoredPosition;
        expandedButtonPosition = toggleButtonRect.anchoredPosition;
        collapsedHudHeight = Mathf.Max(
            toggleButtonRect.rect.height + collapsedTopPadding * 2f,
            1f);
        initialized = true;
    }

    void SetExpandedState(bool expanded, bool immediate)
    {
        isExpanded = expanded;

        if (currentHudHeight < 0f || immediate)
        {
            currentHudHeight = expanded ? expandedHudSize.y : collapsedHudHeight;
        }

        ApplyLayout(immediate);
    }

    void AnimateState()
    {
        if (!initialized || hudRoot == null || toggleButtonRect == null)
        {
            return;
        }

        float targetHeight = isExpanded ? expandedHudSize.y : collapsedHudHeight;
        currentHudHeight = Mathf.Lerp(
            currentHudHeight,
            targetHeight,
            Time.unscaledDeltaTime * Mathf.Max(0.01f, animationSpeed));

        if (Mathf.Abs(currentHudHeight - targetHeight) < 0.1f)
        {
            currentHudHeight = targetHeight;
        }

        ApplyLayout(false);
    }

    void ApplyLayout(bool immediate)
    {
        if (hudRoot == null || toggleButtonRect == null)
        {
            return;
        }

        float resolvedHeight = currentHudHeight >= 0f
            ? currentHudHeight
            : expandedHudSize.y;

        Vector2 rootSize = hudRoot.sizeDelta;
        rootSize.y = resolvedHeight;
        hudRoot.sizeDelta = rootSize;

        Vector2 rootPosition = expandedHudPosition;
        rootPosition.y = expandedHudPosition.y + (expandedHudSize.y - resolvedHeight);
        hudRoot.anchoredPosition = rootPosition;

        ApplyContentVisibility();

        float buttonHalfHeight = toggleButtonRect.rect.height * 0.5f;
        Vector2 buttonPosition = expandedButtonPosition;
        buttonPosition.y = isExpanded
            ? expandedButtonPosition.y
            : -buttonHalfHeight - collapsedTopPadding;
        toggleButtonRect.anchoredPosition = buttonPosition;

        if (immediate)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(hudRoot);
        }
    }

    void ApplyContentVisibility()
    {
        if (contentGroups == null || contentGroups.Length == 0)
        {
            return;
        }

        float targetAlpha = isExpanded ? 1f : 0f;

        for (int i = 0; i < contentGroups.Length; i++)
        {
            CanvasGroup group = contentGroups[i];
            if (group == null)
            {
                continue;
            }

            group.alpha = targetAlpha;
            group.interactable = isExpanded;
            group.blocksRaycasts = isExpanded;
        }
    }

    void EnsureMask()
    {
        if (hudRoot == null)
        {
            return;
        }

        RectMask2D mask = hudRoot.GetComponent<RectMask2D>();
        if (mask == null)
        {
            hudRoot.gameObject.AddComponent<RectMask2D>();
        }
    }

    Button FindButtonByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null &&
                string.Equals(children[i].name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return children[i].GetComponent<Button>();
            }
        }

        return null;
    }
}
