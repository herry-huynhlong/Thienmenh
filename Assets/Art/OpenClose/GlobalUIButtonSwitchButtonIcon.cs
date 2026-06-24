using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class GlobalUIButtonSwitchButtonIcon : MonoBehaviour
{
    [Header("Icon")]
    public Image iconImage;
    public Sprite collapsedSprite; // Khi các nút đang thu vào: hiện <
    public Sprite openedSprite;    // Khi các nút đang mở ra: hiện >

    [Header("Click Effect")]
    public RectTransform visualRoot;
    public float punchScale = 1.12f;
    public float punchDuration = 0.16f;

    private Button button;
    private Vector3 normalScale;
    private Coroutine punchRoutine;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);

        if (visualRoot == null && iconImage != null)
        {
            visualRoot = iconImage.rectTransform;
        }

        if (visualRoot != null)
        {
            normalScale = visualRoot.localScale;
        }
    }

    private void Start()
    {
        RefreshIcon();
    }

    private void OnEnable()
    {
        RefreshIcon();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }

    private void OnClick()
    {
        GlobalUIButtonSwitch switcher = GlobalUIButtonSwitch.GetOrCreate();

        switcher.ToggleAllButtons();

        RefreshIcon();
        PlayPunch();
    }

    private void RefreshIcon()
    {
        if (iconImage == null)
        {
            return;
        }

        GlobalUIButtonSwitch switcher = GlobalUIButtonSwitch.GetOrCreate();

        iconImage.sprite = switcher.IsVisible ? openedSprite : collapsedSprite;
    }

    private void PlayPunch()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
        }

        punchRoutine = StartCoroutine(PunchRoutine());
    }

    private IEnumerator PunchRoutine()
    {
        float timer = 0f;

        Vector3 from = normalScale;
        Vector3 peak = normalScale * punchScale;

        while (timer < punchDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / punchDuration);

            if (t < 0.5f)
            {
                float p = t / 0.5f;
                visualRoot.localScale = Vector3.Lerp(from, peak, p);
            }
            else
            {
                float p = (t - 0.5f) / 0.5f;
                visualRoot.localScale = Vector3.Lerp(peak, from, p);
            }

            yield return null;
        }

        visualRoot.localScale = normalScale;
        punchRoutine = null;
    }
}