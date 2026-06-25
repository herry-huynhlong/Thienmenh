using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class UIButtonToggleTarget : MonoBehaviour
{
    public enum HideDirection
    {
        Right,
        Left,
        Up,
        Down,
        Custom
    }

    [Header("Auto Slot Layout")]
    public bool useAutoSlotLayout = true;

    [Tooltip("0 là nút ngoài cùng bên phải, 1 là nút kế bên trái, 2 tiếp tục qua trái...")]
    public int slotIndex = 0;

    [Tooltip("Vị trí nút đầu tiên tính từ góc phải trên của Canvas.")]
    public Vector2 startOffset = new Vector2(-80f, -80f);

    [Tooltip("Khoảng cách ngang giữa các nút.")]
    public float spacingX = 110f;

    [Tooltip("Bật: slot tăng thì đi qua trái. Tắt: slot tăng thì đi qua phải.")]
    public bool goLeft = true;

    [Header("Responsive Size")]
    public bool forceButtonSize = true;
    public Vector2 buttonSize = new Vector2(100f, 100f);

    [Tooltip("Tự né tai thỏ / mép an toàn trên điện thoại.")]
    public bool useSafeArea = true;

    [Header("Slide Settings")]
    public HideDirection hideDirection = HideDirection.Right;
    public float hideDistance = 140f;
    public Vector2 customHiddenOffset;

    [Header("Animation")]
    public float duration = 0.28f;
    public int order = 0;
    public float hiddenScale = 0.85f;

    [Header("State")]
    public bool startVisible = true;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private Vector2 openedPosition;
    private Vector2 hiddenPosition;
    private Vector3 openedScale;

    private Coroutine currentRoutine;
    private bool prepared;

    public int Order => order;

    private void Awake()
    {
        Prepare();
    }

    private void Start()
    {
        Prepare();

        if (GlobalUIButtonSwitch.Instance != null)
        {
            SetInstant(GlobalUIButtonSwitch.Instance.IsVisible);
        }
        else
        {
            SetInstant(startVisible);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        prepared = false;

        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        Prepare();

        if (Application.isPlaying)
        {
            if (GlobalUIButtonSwitch.Instance != null)
            {
                SetInstant(GlobalUIButtonSwitch.Instance.IsVisible);
            }
            else
            {
                SetInstant(startVisible);
            }
        }
        else
        {
            SetInstant(startVisible);
        }
    }
#endif

    private void Prepare()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (rectTransform == null || canvasGroup == null)
        {
            return;
        }

        if (useAutoSlotLayout)
        {
            ApplySlotLayout();
        }

        openedPosition = rectTransform.anchoredPosition;
        openedScale = rectTransform.localScale;

        hiddenPosition = openedPosition + GetHiddenOffset();

        prepared = true;
    }

    private void ApplySlotLayout()
    {
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        float direction = goLeft ? -1f : 1f;

        Vector2 finalPosition = new Vector2(
            startOffset.x + direction * spacingX * slotIndex,
            startOffset.y
        );

        if (useSafeArea)
        {
            finalPosition += GetSafeAreaTopRightOffset();
        }

        rectTransform.anchoredPosition = finalPosition;

        if (forceButtonSize)
        {
            rectTransform.sizeDelta = buttonSize;
        }
    }

    private Vector2 GetSafeAreaTopRightOffset()
    {
        Rect safeArea = Screen.safeArea;

        float rightInset = Screen.width - safeArea.xMax;
        float topInset = Screen.height - safeArea.yMax;

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return Vector2.zero;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        if (canvasRect == null)
        {
            return Vector2.zero;
        }

        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return Vector2.zero;
        }

        float scaleX = canvasRect.rect.width / Screen.width;
        float scaleY = canvasRect.rect.height / Screen.height;

        return new Vector2(-rightInset * scaleX, -topInset * scaleY);
    }

    private Vector2 GetHiddenOffset()
    {
        switch (hideDirection)
        {
            case HideDirection.Right:
                return new Vector2(hideDistance, 0f);

            case HideDirection.Left:
                return new Vector2(-hideDistance, 0f);

            case HideDirection.Up:
                return new Vector2(0f, hideDistance);

            case HideDirection.Down:
                return new Vector2(0f, -hideDistance);

            case HideDirection.Custom:
                return customHiddenOffset;
        }

        return new Vector2(hideDistance, 0f);
    }

    public void SetInstant(bool visible)
    {
        Prepare();

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        rectTransform.anchoredPosition = visible ? openedPosition : hiddenPosition;
        rectTransform.localScale = visible ? openedScale : openedScale * hiddenScale;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    public void AnimateVisible(bool visible, float delay)
    {
        Prepare();

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }

        currentRoutine = StartCoroutine(AnimateRoutine(visible, delay));
    }

    private IEnumerator AnimateRoutine(bool visible, float delay)
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        Vector2 fromPos = rectTransform.anchoredPosition;
        Vector2 toPos = visible ? openedPosition : hiddenPosition;

        Vector3 fromScale = rectTransform.localScale;
        Vector3 toScale = visible ? openedScale : openedScale * hiddenScale;

        float fromAlpha = canvasGroup.alpha;
        float toAlpha = visible ? 1f : 0f;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float eased = visible ? EaseOutBack(t) : EaseInCubic(t);

            rectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, eased);
            rectTransform.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);

            yield return null;
        }

        rectTransform.anchoredPosition = toPos;
        rectTransform.localScale = toScale;
        canvasGroup.alpha = toAlpha;

        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;

        currentRoutine = null;
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}