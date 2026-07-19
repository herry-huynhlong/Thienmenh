using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class UiTextLocalizer : MonoBehaviour
{
    [SerializeField] TMP_Text targetText;
    [SerializeField] string category = "worldSigns";
    [SerializeField] string key;
    [SerializeField] string fallback;

    void Reset()
    {
        targetText = GetComponent<TMP_Text>();
    }

    void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    void OnEnable()
    {
        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
        LocalizationSettings.LanguageChanged += HandleLanguageChanged;
        WarnIfLikelyWorldLabelIsMisconfigured();
        RefreshText();
    }

    void OnDisable()
    {
        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
    }

    public void RefreshText()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (targetText == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        string fallbackValue =
            string.IsNullOrWhiteSpace(fallback)
                ? targetText.text
                : fallback;

        targetText.text =
            UiText.Get(
                string.IsNullOrWhiteSpace(category)
                    ? "worldSigns"
                    : category,
                key,
                fallbackValue);
    }

    void HandleLanguageChanged()
    {
        RefreshText();
    }

    void WarnIfLikelyWorldLabelIsMisconfigured()
    {
        if (targetText == null)
        {
            return;
        }

        TextMeshProUGUI uiText = targetText as TextMeshProUGUI;
        if (uiText == null)
        {
            return;
        }

        Canvas canvas = uiText.GetComponentInParent<Canvas>();
        if (canvas == null ||
            canvas.renderMode == RenderMode.WorldSpace)
        {
            return;
        }

        Transform canvasParent = canvas.transform.parent;
        if (canvasParent == null ||
            canvasParent.GetComponentInParent<Canvas>() != null)
        {
            return;
        }

        Debug.LogWarning(
            "UiTextLocalizer on '" +
            gameObject.name +
            "' is using TextMeshProUGUI under a non-world-space Canvas nested in world objects. " +
            "This setup often disappears or renders in the wrong place during Play mode. " +
            "Use TextMeshPro (3D) for world labels, or switch the parent Canvas to World Space.",
            gameObject);
    }
}
