using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class FrontierEventTriggerButton :
    MonoBehaviour,
    IPointerClickHandler,
    ISubmitHandler
{
    [SerializeField] Button targetButton;
    [SerializeField] TMP_Text label;
    [SerializeField] bool autoLocalizeLabel = true;
    [SerializeField] float triggerCooldownSeconds = 0.2f;

    float lastTriggerTime = -10f;

    void Awake()
    {
        ResolveReferences();
        BindButton();
        RefreshLabel();
    }

    void OnEnable()
    {
        LocalizationSettings.LanguageChanged += HandleLanguageChanged;
        ResolveReferences();
        BindButton();
        RefreshLabel();
    }

    void OnDisable()
    {
        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;

        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(TriggerEventNow);
        }
    }

    public void TriggerEventNow()
    {
        if (Time.unscaledTime - lastTriggerTime <
            Mathf.Max(0.01f, triggerCooldownSeconds))
        {
            return;
        }

        lastTriggerTime = Time.unscaledTime;
        FrontierDefenseCoordinator coordinator =
            FrontierDefenseCoordinator.EnsureInstance();
        if (coordinator != null)
        {
            coordinator.TriggerBeastWaveNow();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        TriggerEventNow();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        TriggerEventNow();
    }

    void HandleLanguageChanged()
    {
        RefreshLabel();
    }

    void ResolveReferences()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }
    }

    void BindButton()
    {
        if (targetButton == null)
        {
            return;
        }

        targetButton.onClick.RemoveListener(TriggerEventNow);
        targetButton.onClick.AddListener(TriggerEventNow);
    }

    void RefreshLabel()
    {
        if (!autoLocalizeLabel ||
            label == null)
        {
            return;
        }

        label.text =
            UiText.Get(
                "frontierDefense",
                "manualTriggerButton",
                "Kich hoat thu trieu");
    }
}
