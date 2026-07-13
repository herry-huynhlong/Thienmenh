using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class EventSystemGuard : MonoBehaviour
{
    static EventSystemGuard instance;
    float nextEnforceTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static EventSystem EnsureSingle()
    {
        EnsureInstance();
        return instance.EnforceSingleEventSystem();
    }

    static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindAnyObjectByType<EventSystemGuard>();
        if (instance != null)
        {
            DontDestroyOnLoad(instance.gameObject);
            SceneManager.sceneLoaded -= instance.HandleSceneLoaded;
            SceneManager.sceneLoaded += instance.HandleSceneLoaded;
            return;
        }

        GameObject guardObject = new GameObject("EventSystemGuard");
        instance = guardObject.AddComponent<EventSystemGuard>();
        DontDestroyOnLoad(guardObject);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Update()
    {
        if (Time.unscaledTime < nextEnforceTime)
        {
            return;
        }

        nextEnforceTime = Time.unscaledTime + 0.25f;
        EnforceSingleEventSystem();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        nextEnforceTime = 0f;
        EnforceSingleEventSystem();
    }

    EventSystem EnforceSingleEventSystem()
    {
        EventSystem[] systems =
            FindObjectsByType<EventSystem>(FindObjectsInactive.Include);

        if (systems == null || systems.Length == 0)
        {
            return CreateEventSystem();
        }

        EventSystem keep = ChoosePreferred(systems);
        if (keep == null)
        {
            keep = CreateEventSystem();
        }

        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem candidate = systems[i];
            if (candidate == null || candidate == keep)
            {
                continue;
            }

            candidate.enabled = false;

            BaseInputModule[] modules =
                candidate.GetComponents<BaseInputModule>();
            for (int moduleIndex = 0;
                 moduleIndex < modules.Length;
                 moduleIndex++)
            {
                BaseInputModule module = modules[moduleIndex];
                if (module != null)
                {
                    module.enabled = false;
                }
            }

            if (candidate.gameObject.activeSelf)
            {
                candidate.gameObject.SetActive(false);
            }
        }

        if (!keep.gameObject.activeSelf)
        {
            keep.gameObject.SetActive(true);
        }

        keep.enabled = true;

        BaseInputModule[] keepModules = EnsureInputModules(keep);
        for (int i = 0; i < keepModules.Length; i++)
        {
            BaseInputModule module = keepModules[i];
            if (module != null)
            {
                module.enabled = true;
            }
        }

        return keep;
    }

    BaseInputModule[] EnsureInputModules(EventSystem eventSystem)
    {
        BaseInputModule[] modules =
            eventSystem.GetComponents<BaseInputModule>();

        if (modules != null && modules.Length > 0)
        {
            return modules;
        }

        eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        return eventSystem.GetComponents<BaseInputModule>();
    }

    EventSystem ChoosePreferred(EventSystem[] systems)
    {
        EventSystem inactiveInputSystem = null;
        EventSystem activeStandalone = null;
        EventSystem firstActive = null;

        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system == null)
            {
                continue;
            }

            bool isActive = system.gameObject.activeInHierarchy;
            if (firstActive == null && isActive)
            {
                firstActive = system;
            }

            BaseInputModule[] modules =
                system.GetComponents<BaseInputModule>();

            for (int moduleIndex = 0;
                 moduleIndex < modules.Length;
                 moduleIndex++)
            {
                BaseInputModule module = modules[moduleIndex];
                if (module == null)
                {
                    continue;
                }

                bool isInputSystemModule =
                    module.GetType().Name.Contains(
                        "InputSystemUIInputModule");

                if (isInputSystemModule)
                {
                    if (isActive)
                    {
                        return system;
                    }

                    if (inactiveInputSystem == null)
                    {
                        inactiveInputSystem = system;
                    }
                }
                else if (activeStandalone == null &&
                         module is StandaloneInputModule &&
                         isActive)
                {
                    activeStandalone = system;
                }
            }
        }

        if (inactiveInputSystem != null)
        {
            return inactiveInputSystem;
        }

        if (activeStandalone != null)
        {
            return activeStandalone;
        }

        if (firstActive != null)
        {
            return firstActive;
        }

        return systems[0];
    }

    EventSystem CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        EventSystem eventSystem =
            eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        return eventSystem;
    }
}
