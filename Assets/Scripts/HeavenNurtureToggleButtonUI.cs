using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HeavenNurtureToggleButtonUI : MonoBehaviour
{
    static bool installedSceneHook;

    [SerializeField] GameObject heavenPanel;
    [SerializeField] Button toggleButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallForLoadedScene()
    {
        TryInstallOnButton();

        if (!installedSceneHook)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            installedSceneHook = true;
        }
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallOnButton();
    }

    static void TryInstallOnButton()
    {
        GameObject buttonObject = GameObject.Find("HeavenToggleButton");
        if (buttonObject == null ||
            buttonObject.GetComponent<HeavenNurtureToggleButtonUI>() != null)
        {
            return;
        }

        buttonObject.AddComponent<HeavenNurtureToggleButtonUI>();
    }

    void Awake()
    {
        AutoResolveReferences();
        BindButton();
    }

    void OnEnable()
    {
        AutoResolveReferences();
        BindButton();
    }

    void OnDestroy()
    {
        UnbindButton();
    }

    public void TogglePanel()
    {
        AutoResolveReferences();

        if (heavenPanel == null)
        {
            return;
        }

        heavenPanel.SetActive(!heavenPanel.activeSelf);
    }

    void BindButton()
    {
        if (toggleButton == null)
        {
            return;
        }

        toggleButton.onClick.RemoveListener(TogglePanel);
        toggleButton.onClick.AddListener(TogglePanel);
    }

    void UnbindButton()
    {
        if (toggleButton == null)
        {
            return;
        }

        toggleButton.onClick.RemoveListener(TogglePanel);
    }

    void AutoResolveReferences()
    {
        if (toggleButton == null)
        {
            toggleButton = GetComponent<Button>();
        }

        if (heavenPanel == null)
        {
            GameObject panelObject = FindSceneObject("HeavenNurturePanel");
            if (panelObject != null)
            {
                heavenPanel = panelObject;
            }
        }
    }

    static GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject direct = GameObject.Find(objectName);
        if (direct != null)
        {
            return direct;
        }

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null ||
                candidate.name != objectName ||
                !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            return candidate.gameObject;
        }

        return null;
    }
}
