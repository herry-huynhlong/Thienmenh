using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorldClockTextUI : MonoBehaviour
{
    const string TimeCategory = "worldClock";

#if UNITY_EDITOR
    const string TimeIconSpriteSheetPath = "Assets/UI/Time/iconthoitiet.png";
#endif

    public TMP_Text clockText;
    public string prefix = "";

    [Header("Split HUD")]
    [SerializeField] TMP_Text worldNameText;
    [SerializeField] TMP_Text yearText;
    [SerializeField] TMP_Text dateText;
    [SerializeField] TMP_Text timeText;
    [SerializeField] Image dayNightIcon;
    [SerializeField] Sprite daySprite;
    [SerializeField] Sprite nightSprite;
    [SerializeField] Sprite sunriseSprite;
    [SerializeField] Sprite sunsetSprite;
    [SerializeField] Sprite rainSprite;
    [SerializeField] Sprite snowSprite;

    void Awake()
    {
        if (clockText == null)
        {
            clockText = GetComponent<TMP_Text>();
        }

        AutoBindSplitHudReferences();
        EnsureIconSpritesAssigned();
    }

    void OnEnable()
    {
        Refresh();
    }

    void Update()
    {
        AutoBindSplitHudReferences();
        EnsureIconSpritesAssigned();
        Refresh();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (clockText == null)
        {
            clockText = GetComponent<TMP_Text>();
        }

        AutoBindSplitHudReferences();
        EnsureIconSpritesAssigned();
    }
#endif

    void Refresh()
    {
        bool hasSplitHud = HasSplitHud();
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;

        if (timeSystem == null)
        {
            if (hasSplitHud)
            {
                RefreshSplitUnavailable();
            }
            else if (clockText != null)
            {
                clockText.text =
                    GetResolvedPrefix() +
                    UiText.Get(
                        TimeCategory,
                        "defaultWhenUnavailable");
            }

            return;
        }

        if (hasSplitHud)
        {
            RefreshSplitHud(timeSystem);
            return;
        }

        if (clockText != null)
        {
            clockText.text =
                GetResolvedPrefix() + timeSystem.GetClockText();
        }
    }

    void RefreshSplitUnavailable()
    {
        if (worldNameText != null)
        {
            worldNameText.text =
                UiText.Get(
                    TimeCategory,
                    "continentName");
        }

        if (yearText != null)
        {
            yearText.text = string.Empty;
        }

        if (dateText != null)
        {
            dateText.text = string.Empty;
        }

        if (timeText != null)
        {
            timeText.text = string.Empty;
        }

        if (dayNightIcon != null)
        {
            dayNightIcon.sprite = null;
            dayNightIcon.enabled = false;
        }
    }

    void RefreshSplitHud(WorldTimeSystem timeSystem)
    {
        if (timeSystem == null)
        {
            return;
        }

        int hour = Mathf.FloorToInt(timeSystem.currentHour);
        int minute = Mathf.FloorToInt((timeSystem.currentHour - hour) * 60f);

        if (worldNameText != null)
        {
            worldNameText.text =
                UiText.Get(
                    TimeCategory,
                    "continentName",
                    timeSystem.continentName);
        }

        if (yearText != null)
        {
            yearText.text =
                timeSystem.IsOneGameDayPerYearCalendar ||
                timeSystem.currentYear > 1
                    ? UiText.Format(
                        TimeCategory,
                        "yearOnlyFormat",
                        timeSystem.currentYear)
                    : string.Empty;
        }

        if (dateText != null)
        {
            timeSystem.GetDisplayCalendarDate(
                out int displayMonth,
                out int displayDay);
            dateText.text =
                UiText.Format(
                    TimeCategory,
                    "monthDayFormat",
                    displayMonth,
                    displayDay);
        }

        if (timeText != null)
        {
            timeText.text =
                UiText.Format(
                    TimeCategory,
                    "timeOnlyFormat",
                    hour,
                    minute);
        }

        RefreshIcon(
            timeSystem,
            WeatherSystem.Instance);
    }

    void RefreshIcon(
        WorldTimeSystem timeSystem,
        WeatherSystem weatherSystem)
    {
        if (dayNightIcon == null || timeSystem == null)
        {
            return;
        }

        Sprite resolvedSprite =
            ResolveIconSprite(
                timeSystem,
                weatherSystem != null
                    ? weatherSystem.CurrentWeather
                    : WorldWeather.Clear);

        dayNightIcon.sprite = resolvedSprite;
        dayNightIcon.enabled = resolvedSprite != null;
    }

    Sprite ResolveIconSprite(
        WorldTimeSystem timeSystem,
        WorldWeather weather)
    {
        switch (weather)
        {
            case WorldWeather.Rain:
            case WorldWeather.Thunder:
                return rainSprite;
            case WorldWeather.Snow:
                return snowSprite;
        }

        switch (timeSystem.CurrentPhase)
        {
            case WorldTimePhase.Dawn:
                return sunriseSprite;
            case WorldTimePhase.Morning:
            case WorldTimePhase.Noon:
            case WorldTimePhase.Afternoon:
                return daySprite;
            case WorldTimePhase.Evening:
                return sunsetSprite;
            default:
                return nightSprite;
        }
    }

    void AutoBindSplitHudReferences()
    {
        Transform timeHudRoot = FindTimeHudRoot();
        if (timeHudRoot == null)
        {
            return;
        }

        if (worldNameText == null)
        {
            if (string.Equals(
                    gameObject.name,
                    "WorldNameText",
                    StringComparison.OrdinalIgnoreCase) &&
                clockText != null)
            {
                worldNameText = clockText;
            }
            else
            {
                worldNameText =
                    FindComponentInChildren<TMP_Text>(
                        timeHudRoot,
                        "WorldNameText");
            }
        }

        if (yearText == null)
        {
            yearText =
                FindComponentInChildren<TMP_Text>(
                    timeHudRoot,
                    "YearText");
        }

        if (dateText == null)
        {
            dateText =
                FindComponentInChildren<TMP_Text>(
                    timeHudRoot,
                    "DateText");
        }

        if (timeText == null)
        {
            timeText =
                FindComponentInChildren<TMP_Text>(
                    timeHudRoot,
                    "TimeText");

            if (timeText == null)
            {
                timeText =
                    FindComponentInChildren<TMP_Text>(
                        timeHudRoot,
                        "time");
            }
        }

        if (dayNightIcon == null)
        {
            dayNightIcon =
                FindComponentInChildren<Image>(
                    timeHudRoot,
                    "DayNightIcon");
        }
    }

    Transform FindTimeHudRoot()
    {
        Transform current = transform;
        while (current != null)
        {
            if (string.Equals(
                    current.name,
                    "TimeHUD",
                    StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    static T FindComponentInChildren<T>(
        Transform root,
        string objectName)
        where T : Component
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(
                    child.name,
                    objectName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return child.GetComponent<T>();
            }

            T found = FindComponentInChildren<T>(child, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    bool HasSplitHud()
    {
        return FindTimeHudRoot() != null &&
            (worldNameText != null ||
            yearText != null ||
            dateText != null ||
            timeText != null ||
            dayNightIcon != null);
    }

    void EnsureIconSpritesAssigned()
    {
        if (daySprite != null &&
            nightSprite != null &&
            sunriseSprite != null &&
            sunsetSprite != null &&
            rainSprite != null &&
            snowSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(TimeIconSpriteSheetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite == null)
            {
                continue;
            }

            switch (sprite.name)
            {
                case "iconthoitiet_0":
                    daySprite = sprite;
                    break;
                case "iconthoitiet_1":
                    nightSprite = sprite;
                    break;
                case "iconthoitiet_2":
                    sunriseSprite = sprite;
                    break;
                case "iconthoitiet_3":
                    rainSprite = sprite;
                    break;
                case "iconthoitiet_4":
                    snowSprite = sprite;
                    break;
                case "iconthoitiet_5":
                    sunsetSprite = sprite;
                    break;
            }
        }
#endif
    }

    string GetResolvedPrefix()
    {
        return UiText.Get(TimeCategory, "prefix", prefix);
    }
}
