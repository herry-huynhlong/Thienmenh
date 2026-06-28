using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalUIButtonSwitch : MonoBehaviour
{
    public static GlobalUIButtonSwitch Instance { get; private set; }

    private const string SaveKey = "Global_UI_Buttons_Visible";

    [Header("Default")]
    [SerializeField] private bool defaultVisible = true;
    [SerializeField] private bool rememberState = true;

    [Header("Animation")]
    [SerializeField] private float staggerDelay = 0.045f;
    [SerializeField] private bool reverseCloseOrder = true;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (rememberState)
        {
            IsVisible = PlayerPrefs.GetInt(SaveKey, defaultVisible ? 1 : 0) == 1;
        }
        else
        {
            IsVisible = defaultVisible;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplyAllTargetsInstant();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyAfterSceneLoaded());
    }

    private IEnumerator ApplyAfterSceneLoaded()
    {
        yield return null;
        ApplyAllTargetsInstant();
    }

    public void ToggleAllButtons()
    {
        SetAllButtonsVisible(!IsVisible, true);
    }

    public void SetAllButtonsVisible(bool visible, bool animate)
    {
        IsVisible = visible;

        if (rememberState)
        {
            PlayerPrefs.SetInt(SaveKey, visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (animate)
        {
            ApplyAllTargetsAnimated();
        }
        else
        {
            ApplyAllTargetsInstant();
        }
    }

    public void ApplyAllTargetsInstant()
    {
        UIButtonToggleTarget[] targets = FindToggleTargets();

        foreach (UIButtonToggleTarget target in targets)
        {
            if (target != null)
            {
                target.SetInstant(IsVisible);
            }
        }
    }

    public void ApplyAllTargetsAnimated()
    {
        UIButtonToggleTarget[] targets = FindToggleTargets();

        int maxOrder = 0;

        foreach (UIButtonToggleTarget target in targets)
        {
            if (target != null && target.Order > maxOrder)
            {
                maxOrder = target.Order;
            }
        }

        foreach (UIButtonToggleTarget target in targets)
        {
            if (target == null)
            {
                continue;
            }

            int order = target.Order;

            if (!IsVisible && reverseCloseOrder)
            {
                order = maxOrder - target.Order;
            }

            float delay = Mathf.Max(0, order) * staggerDelay;

            target.AnimateVisible(IsVisible, delay);
        }
    }

    private UIButtonToggleTarget[] FindToggleTargets()
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<UIButtonToggleTarget>(
            FindObjectsInactive.Include
        );
#else
        return FindObjectsByType<UIButtonToggleTarget>(
            FindObjectsInactive.Include);
#endif
    }

    public static GlobalUIButtonSwitch GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject obj = new GameObject("Global UI Button Switch");
        Instance = obj.AddComponent<GlobalUIButtonSwitch>();
        return Instance;
    }
}
