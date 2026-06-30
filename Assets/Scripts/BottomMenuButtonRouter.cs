using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public class BottomMenuButtonRouter : MonoBehaviour
{
    static BottomMenuButtonRouter instance;

    Vector3 pointerDownPosition;
    bool pointerStartedOnMapButton;
    bool pointerStartedOnInventoryButton;
    bool pointerStartedOnShopButton;
    bool inventoryWasOpenOnPointerDown;
    bool shopWasOpenOnPointerDown;
    float lastMapOpenTime = -10f;
    float lastInventoryFallbackTime = -10f;
    float lastShopFallbackTime = -10f;
    Coroutine inventoryFallbackRoutine;
    Coroutine shopFallbackRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (instance != null)
        {
            instance.BindSoon();
            return;
        }

        GameObject routerObject = new GameObject("BottomMenuButtonRouter");
        DontDestroyOnLoad(routerObject);
        instance = routerObject.AddComponent<BottomMenuButtonRouter>();
    }

    void OnEnable()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindSoon();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this)
        {
            instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindSoon();
    }

    void Update()
    {
        HandleDirectBottomButtonClick();
    }

    void BindSoon()
    {
        if (isActiveAndEnabled)
        {
            StartCoroutine(BindAfterUiAwake());
        }
    }

    IEnumerator BindAfterUiAwake()
    {
        yield return null;
        yield return null;
        BindButtons();
    }
    void BindButtons()
    {
        Button[] buttons =
            FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            string key = GetButtonKey(button.transform);
            if (!IsBottomMenuButton(button.transform, key))
            {
                continue;
            }

            if (IsInventoryButton(key))
            {
                button.onClick.RemoveListener(ToggleInventory);
                if (!HasValidPersistentClick(button))
                {
                    button.onClick.AddListener(ToggleInventory);
                }
                continue;
            }

            if (IsShopButton(key))
            {
                button.onClick.RemoveListener(ToggleShop);
                if (!HasValidPersistentClick(button))
                {
                    button.onClick.AddListener(ToggleShop);
                }
                continue;
            }

            if (IsMapButton(key))
            {
                button.onClick.RemoveListener(OpenMap);
                if (!HasValidPersistentClick(button))
                {
                    button.onClick.AddListener(OpenMap);
                }
                continue;
            }

            if (IsStoryButton(key))
            {
                button.onClick.RemoveListener(ToggleStory);
                if (!HasValidPersistentClick(button))
                {
                    button.onClick.AddListener(ToggleStory);
                }
            }
        }
    }
    bool HasValidPersistentClick(Button button)
    {
        if (button == null)
        {
            return false;
        }

        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentTarget(i) != null &&
                !string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)))
            {
                return true;
            }
        }

        return false;
    }
    void HandleDirectBottomButtonClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            pointerDownPosition = Input.mousePosition;
            pointerStartedOnMapButton = IsPointerOverNamedButton(Input.mousePosition, IsMapButton);
            pointerStartedOnInventoryButton = IsPointerOverNamedButton(Input.mousePosition, IsInventoryButton);
            pointerStartedOnShopButton = IsPointerOverNamedButton(Input.mousePosition, IsShopButton);

            InventoryPanelUI inventoryPanel = FindBestInventoryPanel();
            inventoryWasOpenOnPointerDown = inventoryPanel != null && inventoryPanel.IsOpen;

            ShopPanelUI shopPanel = FindBestShopPanel();
            shopWasOpenOnPointerDown = shopPanel != null &&
                shopPanel.panelRoot != null &&
                shopPanel.panelRoot.activeInHierarchy;
        }

        if (!Input.GetMouseButtonUp(0))
        {
            return;
        }

        if ((Input.mousePosition - pointerDownPosition).magnitude > 12f)
        {
            return;
        }

        if (pointerStartedOnInventoryButton &&
            IsPointerOverNamedButton(Input.mousePosition, IsInventoryButton))
        {
            QueueInventoryFallbackToggle(inventoryWasOpenOnPointerDown);
            return;
        }

        if (pointerStartedOnShopButton &&
            IsPointerOverNamedButton(Input.mousePosition, IsShopButton))
        {
            QueueShopFallbackToggle(shopWasOpenOnPointerDown);
            return;
        }

        if (pointerStartedOnMapButton &&
            IsPointerOverNamedButton(Input.mousePosition, IsMapButton))
        {
            OpenMap();
        }
    }

    void QueueInventoryFallbackToggle(bool wasOpenOnPointerDown)
    {
        if (inventoryFallbackRoutine != null)
        {
            StopCoroutine(inventoryFallbackRoutine);
        }

        inventoryFallbackRoutine =
            StartCoroutine(ToggleInventoryAfterButtonEvent(wasOpenOnPointerDown));
    }

    IEnumerator ToggleInventoryAfterButtonEvent(bool wasOpenOnPointerDown)
    {
        yield return null;

        inventoryFallbackRoutine = null;

        if (Time.unscaledTime - lastInventoryFallbackTime < 0.35f)
        {
            yield break;
        }

        InventoryPanelUI panel = FindBestInventoryPanel();
        if (panel == null)
        {
            Debug.LogWarning("BottomMenuButtonRouter: No InventoryPanelUI found for direct Balo click.");
            yield break;
        }

        if (panel.IsOpen != wasOpenOnPointerDown)
        {
            yield break;
        }

        lastInventoryFallbackTime = Time.unscaledTime;

        if (wasOpenOnPointerDown)
        {
            panel.Close();
        }
        else
        {
            panel.Open();
        }
    }
    void QueueShopFallbackToggle(bool wasOpenOnPointerDown)
    {
        if (shopFallbackRoutine != null)
        {
            StopCoroutine(shopFallbackRoutine);
        }

        shopFallbackRoutine =
            StartCoroutine(ToggleShopAfterButtonEvent(wasOpenOnPointerDown));
    }

    IEnumerator ToggleShopAfterButtonEvent(bool wasOpenOnPointerDown)
    {
        yield return null;

        shopFallbackRoutine = null;

        if (Time.unscaledTime - lastShopFallbackTime < 0.35f)
        {
            yield break;
        }

        ShopPanelUI panel = FindBestShopPanel();
        if (panel == null || panel.panelRoot == null)
        {
            yield break;
        }

        bool isOpen = panel.panelRoot.activeInHierarchy;
        if (isOpen != wasOpenOnPointerDown)
        {
            yield break;
        }

        lastShopFallbackTime = Time.unscaledTime;

        if (wasOpenOnPointerDown)
        {
            panel.Close();
        }
        else
        {
            panel.Open();
        }
    }
    bool IsPointerOverNamedButton(Vector2 screenPosition, Func<string, bool> nameMatcher)
    {
        Button[] buttons =
            FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            RectTransform rectTransform = button != null ? button.transform as RectTransform : null;
            if (button == null ||
                !button.gameObject.activeInHierarchy ||
                !button.interactable ||
                rectTransform == null)
            {
                continue;
            }

            string key = GetButtonKey(button.transform);
            if (!IsBottomMenuButton(button.transform, key))
            {
                continue;
            }
            if (!nameMatcher(key))
            {
                continue;
            }

            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
            {
                return true;
            }
        }

        return false;
    }

    string GetButtonKey(Transform buttonTransform)
    {
        return NormalizeName(buttonTransform != null ? buttonTransform.name : string.Empty);
    }

    string NormalizeName(string value)
    {
        return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    bool IsBottomMenuButton(Transform buttonTransform, string key)
    {
        if (buttonTransform == null)
        {
            return false;
        }

        bool supportedButton =
            IsInventoryButton(key) ||
            IsShopButton(key) ||
            IsMapButton(key) ||
            IsStoryButton(key);

        return supportedButton &&
            HasAncestorNamed(buttonTransform, "menupanel");
    }

    bool HasAncestorNamed(Transform current, string normalizedName)
    {
        while (current != null)
        {
            if (NormalizeName(current.name) == normalizedName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    bool IsInventoryButton(string key)
    {
        return key.Contains("balo") || key.Contains("inventory") || key.Contains("bag");
    }

    bool IsShopButton(string key)
    {
        return key.Contains("shop") || key.Contains("cuahang") || key.Contains("market");
    }

    bool IsMapButton(string key)
    {
        return key.Contains("map") || key.Contains("worldmap");
    }

    bool IsStoryButton(string key)
    {
        return key.Contains("story") || key.Contains("log") || key.Contains("event");
    }

    public void ToggleInventory()
    {
        InventoryPanelUI panel = FindBestInventoryPanel();
        if (panel == null)
        {
            Debug.LogWarning("BottomMenuButtonRouter: No InventoryPanelUI found for Balo button.");
            return;
        }

        panel.Toggle();
    }

    public void ToggleShop()
    {
        ShopPanelUI panel = FindBestShopPanel();
        if (panel != null)
        {
            panel.Toggle();
            return;
        }

        OpenShopUI opener = FindAnyObjectByType<OpenShopUI>(FindObjectsInactive.Include);
        if (opener != null)
        {
            opener.OpenShop();
            return;
        }

        Debug.LogWarning("BottomMenuButtonRouter: No ShopPanelUI or OpenShopUI found for Shop button.");
    }

    public void OpenMap()
    {
        if (Time.unscaledTime - lastMapOpenTime < 0.5f)
        {
            return;
        }

        lastMapOpenTime = Time.unscaledTime;
        CloseOpenPanels();

        string mapSceneName = "LiteMapScene";
        string returnScene = GetCurrentGameplaySceneName(mapSceneName);

        PlayerPrefs.SetString("LastScene", returnScene);
        PlayerPrefs.SetString("MapReturnScene", returnScene);
        PlayerPrefs.Save();

        Scene mapScene =
            SceneManager.GetSceneByName(mapSceneName);

        if (!mapScene.isLoaded)
        {
            SceneManager.LoadScene(
                mapSceneName,
                LoadSceneMode.Additive);

            mapScene =
                SceneManager.GetSceneByName(mapSceneName);
        }

        if (mapScene.IsValid() && mapScene.isLoaded)
        {
            SceneManager.SetActiveScene(mapScene);
        }
    }

    public void ToggleStory()
    {
        UIWorldStoryManager storyManager = UIWorldStoryManager.Instance;

        if (storyManager == null)
        {
            storyManager = FindAnyObjectByType<UIWorldStoryManager>(FindObjectsInactive.Include);
        }

        if (storyManager != null)
        {
            storyManager.ToggleStoryPanel();
            return;
        }

        GameObject storyPanel = FindNamedObject("StoryPanel", "WorldStoryPanel");
        if (storyPanel != null)
        {
            storyPanel.SetActive(!storyPanel.activeSelf);
            return;
        }

        Debug.LogWarning("BottomMenuButtonRouter: No story panel found for Story button.");
    }

    InventoryPanelUI FindBestInventoryPanel()
    {
        InventoryPanelUI[] panels =
            FindObjectsByType<InventoryPanelUI>(FindObjectsInactive.Include);

        InventoryPanelUI playerWalletPanel = null;
        InventoryPanelUI exactBaloPanel = null;
        InventoryPanelUI exactBalo = null;
        InventoryPanelUI nameMatch = null;
        InventoryPanelUI fallback = null;

        foreach (InventoryPanelUI panel in panels)
        {
            if (panel == null ||
                !panel.enabled ||
                HasAncestorNamed(panel.transform, "menupanel"))
            {
                continue;
            }

            string nameKey = panel.name.ToLowerInvariant();
            bool hasPlayerWallet =
                panel.GetComponent<PlayerWallet>() != null ||
                (panel.panelRoot != null &&
                panel.panelRoot.GetComponent<PlayerWallet>() != null);

            if (hasPlayerWallet &&
                !panel.readOnly &&
                playerWalletPanel == null)
            {
                playerWalletPanel = panel;
            }

            if (nameKey == "balopanel")
            {
                exactBaloPanel = panel;
            }
            else if (nameKey == "balo")
            {
                exactBalo = panel;
            }
            else if ((nameKey.Contains("balo") || nameKey.Contains("inventory")) &&
                nameMatch == null)
            {
                nameMatch = panel;
            }

            if (fallback == null && !panel.readOnly)
            {
                fallback = panel;
            }
        }

        if (playerWalletPanel != null)
        {
            return playerWalletPanel;
        }

        if (exactBaloPanel != null)
        {
            return exactBaloPanel;
        }

        if (exactBalo != null)
        {
            return exactBalo;
        }

        if (nameMatch != null)
        {
            return nameMatch;
        }

        return fallback;
    }
    ShopPanelUI FindBestShopPanel()
    {
        ShopPanelUI[] panels =
            FindObjectsByType<ShopPanelUI>(FindObjectsInactive.Include);

        if (panels.Length == 0)
        {
            return null;
        }

        foreach (ShopPanelUI panel in panels)
        {
            if (panel != null && panel.name.ToLowerInvariant().Contains("shop"))
            {
                return panel;
            }
        }

        return panels[0];
    }

    void CloseOpenPanels()
    {
        InventoryPanelUI inventoryPanel = FindBestInventoryPanel();
        if (inventoryPanel != null)
        {
            inventoryPanel.Close();
        }

        ShopPanelUI shopPanel = FindBestShopPanel();
        if (shopPanel != null)
        {
            shopPanel.Close();
        }
    }

    string GetCurrentGameplaySceneName(string mapSceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded || scene.name == "PersistentScene" || scene.name == mapSceneName)
            {
                continue;
            }

            return scene.name;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.isLoaded && activeScene.name != "PersistentScene" && activeScene.name != mapSceneName)
        {
            return activeScene.name;
        }

        return "Lang";
    }

    GameObject FindNamedObject(params string[] names)
    {
        Transform[] transforms =
            FindObjectsByType<Transform>(FindObjectsInactive.Include);

        foreach (Transform found in transforms)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (found.name == names[i])
                {
                    return found.gameObject;
                }
            }
        }

        return null;
    }
}
