using TMPro;
using UnityEngine;

public class WorldClockTextUI : MonoBehaviour
{
    public TMP_Text clockText;
    public string prefix = "";

    void Awake()
    {
        if (clockText == null)
        {
            clockText = GetComponent<TMP_Text>();
        }
    }

    void OnEnable()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            timeSystem.OnHourChanged += Refresh;
            timeSystem.OnDayChanged += RefreshDay;
        }

        Refresh(0);
    }

    void OnDisable()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            timeSystem.OnHourChanged -= Refresh;
            timeSystem.OnDayChanged -= RefreshDay;
        }
    }

    void Update()
    {
        Refresh(0);
    }

    void RefreshDay(int day)
    {
        Refresh(0);
    }

    void Refresh(int hour)
    {
        if (clockText == null)
        {
            return;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            clockText.text =
                GetResolvedPrefix() +
                UiText.Get(
                    "worldClock",
                    "defaultWhenUnavailable");
            return;
        }

        clockText.text = GetResolvedPrefix() + timeSystem.GetClockText();
    }

    string GetResolvedPrefix()
    {
        return UiText.Get("worldClock", "prefix", prefix);
    }
}
