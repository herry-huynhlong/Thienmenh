using TMPro;
using UnityEngine;

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
}
