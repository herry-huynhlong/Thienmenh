using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : MonoBehaviour
{
    const string DefaultLoadingSceneName = "Loading";
    const string PersistentBootstrapSceneName = "PersistentScene";
    const string DefaultGameplaySceneName = "Lang";
    const string LiteMapSceneName = "LiteMapScene";
    const string ConfigResourcePath = "Loading/LoadingSceneConfig";
    const float FinalRevealDelaySeconds = 1f;
    const float TargetWarmupTimeoutSeconds = 20f;

    static string pendingTargetSceneName;

    LoadingSceneConfig config;
    Image backgroundImage;
    Image backgroundOverlayImage;
    Image progressFill;
    TMP_Text percentText;
    TMP_Text loadingText;
    Text percentLegacyText;
    Text loadingLegacyText;
    RectTransform loadingTextRect;
    Vector2 loadingTextBasePosition;
    Sprite[] backgroundFrames;
    string loadingBaseText = "Dang tai";
    float progressValue;
    float backgroundFadeTimer;
    float loadingTextTimer;
    int backgroundIndex;
    bool isLoadingSceneActive;
    bool isBackgroundFadeActive;

    public static bool TryLoadLoadingScene(string targetSceneName)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(DefaultLoadingSceneName))
        {
            return false;
        }

        EnsureLoadingSceneBootstrapRegistered();
        pendingTargetSceneName = targetSceneName;
        SceneManager.LoadScene(DefaultLoadingSceneName, LoadSceneMode.Single);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterLoadingSceneBootstrap()
    {
        EnsureLoadingSceneBootstrapRegistered();
    }

    static void EnsureLoadingSceneBootstrapRegistered()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapCurrentLoadingScene()
    {
        TryBootstrapLoadingScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryBootstrapLoadingScene(scene);
    }

    static void TryBootstrapLoadingScene(Scene scene)
    {
        if (!scene.IsValid() ||
            scene.name != DefaultLoadingSceneName ||
            FindLoadingSceneControllerInScene(scene) != null)
        {
            return;
        }

        GameObject host = FindRootGameObject(scene, "LoadingScene");
        if (host == null)
        {
            host = new GameObject("LoadingSceneController");
            SceneManager.MoveGameObjectToScene(host, scene);
        }

        host.AddComponent<LoadingSceneController>();
    }

    static LoadingSceneController FindLoadingSceneControllerInScene(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            LoadingSceneController controller =
                roots[i].GetComponentInChildren<LoadingSceneController>(true);
            if (controller != null)
            {
                return controller;
            }
        }

        return null;
    }

    static GameObject FindRootGameObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i];
            }
        }

        return null;
    }

    void Awake()
    {
        config = Resources.Load<LoadingSceneConfig>(ConfigResourcePath);
        isLoadingSceneActive =
            SceneManager.GetActiveScene().name == GetLoadingSceneName();

        if (!isLoadingSceneActive)
        {
            enabled = false;
            return;
        }

        AutoBindReferences();
        ApplyConfig();
        EnsureBackgroundFadeImage();
        ConfigureProgressFill();
        CaptureLoadingLabelState();
        ApplyProgress(0f);
        SetBackgroundFrameImmediate(0);
    }

    void Update()
    {
        if (!isLoadingSceneActive)
        {
            return;
        }

        UpdateBackgroundFade();
        UpdateLoadingLabelAnimation();
    }

    void Start()
    {
        if (!isLoadingSceneActive)
        {
            return;
        }

        StartCoroutine(LoadTargetSceneAsync());
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        AutoBindReferences();
        ApplyConfig();
        ConfigureProgressFill();
    }

    void AutoBindReferences()
    {
        Transform root = FindBindingRoot();
        if (root == null)
        {
            return;
        }

        if (backgroundImage == null)
        {
            backgroundImage = FindImage(root, "Image");
            if (backgroundImage == null)
            {
                Image[] images = root.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    Image candidate = images[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.name == "ProgressFill" ||
                        candidate.name == "ProgressBarBackground")
                    {
                        continue;
                    }

                    backgroundImage = candidate;
                    break;
                }
            }
        }

        if (progressFill == null)
        {
            progressFill = FindImage(root, "ProgressFill");
        }

        if (percentText == null)
        {
            percentText = FindTmpText(root, "PercentText");
        }

        if (percentLegacyText == null)
        {
            percentLegacyText = FindLegacyText(root, "PercentText");
        }

        if (loadingText == null)
        {
            loadingText = FindTmpText(root, "dangtai");
            if (loadingText == null)
            {
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text candidate = texts[i];
                    if (candidate == null || candidate == percentText)
                    {
                        continue;
                    }

                    loadingText = candidate;
                    break;
                }
            }
        }

        if (loadingLegacyText == null)
        {
            loadingLegacyText = FindLegacyText(root, "dangtai");
            if (loadingLegacyText == null)
            {
                Text[] texts = root.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    Text candidate = texts[i];
                    if (candidate == null || candidate == percentLegacyText)
                    {
                        continue;
                    }

                    loadingLegacyText = candidate;
                    break;
                }
            }
        }

        if (loadingTextRect == null)
        {
            if (loadingText != null)
            {
                loadingTextRect = loadingText.rectTransform;
            }
            else if (loadingLegacyText != null)
            {
                loadingTextRect =
                    loadingLegacyText.GetComponent<RectTransform>();
            }
        }
    }

    void ApplyConfig()
    {
        if (config == null)
        {
            backgroundFrames = null;
            return;
        }

        backgroundFrames = config.backgroundFrames;
    }

    Transform FindBindingRoot()
    {
        GameObject namedRoot = GameObject.Find("LoadingScene");
        if (namedRoot != null)
        {
            return namedRoot.transform;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    IEnumerator LoadTargetSceneAsync()
    {
        string targetSceneName = ConsumeTargetSceneName();
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[LoadingSceneController] Missing target scene.");
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning(
                $"[LoadingSceneController] Cannot load scene '{targetSceneName}'.");
            yield break;
        }

        bool shouldLoadBehindOverlay =
            ShouldLoadTargetBehindLoadingOverlay(targetSceneName);
        if (shouldLoadBehindOverlay)
        {
            DisableLoadingSceneDuplicateComponents();
        }

        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync(
                targetSceneName,
                shouldLoadBehindOverlay
                    ? LoadSceneMode.Additive
                    : LoadSceneMode.Single);

        if (loadOperation == null)
        {
            yield break;
        }

        if (!shouldLoadBehindOverlay)
        {
            loadOperation.allowSceneActivation = false;
        }

        float elapsed = 0f;
        float targetWarmupElapsed = 0f;
        bool loggedWarmupTimeout = false;
        float minimumDuration = GetMinimumLoadingSeconds();

        while (true)
        {
            float sceneLoadProgress = shouldLoadBehindOverlay
                ? (loadOperation.isDone ? 1f : Mathf.Clamp01(loadOperation.progress))
                : Mathf.Clamp01(loadOperation.progress / 0.9f);
            float warmupProgress = shouldLoadBehindOverlay
                ? GetTargetWarmupProgress(targetSceneName)
                : 1f;
            float actualProgress = shouldLoadBehindOverlay
                ? Mathf.Clamp01(sceneLoadProgress * 0.8f + warmupProgress * 0.2f)
                : sceneLoadProgress;
            float timedProgress = minimumDuration > 0f
                ? Mathf.Clamp01(elapsed / minimumDuration)
                : 1f;
            float displayedProgress = Mathf.Min(actualProgress, timedProgress);
            ApplyProgress(displayedProgress);

            bool targetRevealReady =
                !shouldLoadBehindOverlay ||
                IsTargetSceneReadyForReveal(targetSceneName);
            bool targetSceneReady =
                sceneLoadProgress >= 1f &&
                warmupProgress >= 1f &&
                timedProgress >= 1f &&
                targetRevealReady;
            bool sceneLoadedButRevealPending =
                shouldLoadBehindOverlay &&
                sceneLoadProgress >= 1f &&
                timedProgress >= 1f &&
                !targetRevealReady;

            if (sceneLoadedButRevealPending)
            {
                targetWarmupElapsed += Time.unscaledDeltaTime;
            }
            else
            {
                targetWarmupElapsed = 0f;
            }

            if (sceneLoadedButRevealPending &&
                targetWarmupElapsed >= TargetWarmupTimeoutSeconds)
            {
                if (!loggedWarmupTimeout)
                {
                    Debug.LogWarning(
                        "[LoadingSceneController] Target warmup timeout. " +
                        BuildTargetWarmupStatus(targetSceneName));
                    loggedWarmupTimeout = true;
                }

                break;
            }

            if (targetSceneReady)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyProgress(1f);
        yield return WaitForRevealDelay();

        if (!shouldLoadBehindOverlay)
        {
            loadOperation.allowSceneActivation = true;
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        if (shouldLoadBehindOverlay)
        {
            yield return RevealLoadedTargetScene(targetSceneName);
        }
    }

    void DisableLoadingSceneDuplicateComponents()
    {
        Scene loadingScene = SceneManager.GetSceneByName(GetLoadingSceneName());
        if (!loadingScene.IsValid() || !loadingScene.isLoaded)
        {
            return;
        }

        GameObject[] roots = loadingScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            AudioListener[] audioListeners =
                roots[i].GetComponentsInChildren<AudioListener>(true);
            for (int listenerIndex = 0;
                 listenerIndex < audioListeners.Length;
                 listenerIndex++)
            {
                audioListeners[listenerIndex].enabled = false;
            }

            EventSystem[] eventSystems =
                roots[i].GetComponentsInChildren<EventSystem>(true);
            for (int eventSystemIndex = 0;
                 eventSystemIndex < eventSystems.Length;
                 eventSystemIndex++)
            {
                EventSystem eventSystem = eventSystems[eventSystemIndex];
                if (eventSystem == null)
                {
                    continue;
                }

                BaseInputModule[] modules =
                    eventSystem.GetComponents<BaseInputModule>();
                for (int moduleIndex = 0;
                     moduleIndex < modules.Length;
                     moduleIndex++)
                {
                    if (modules[moduleIndex] != null)
                    {
                        modules[moduleIndex].enabled = false;
                    }
                }

                eventSystem.enabled = false;
                if (eventSystem.gameObject.activeSelf)
                {
                    eventSystem.gameObject.SetActive(false);
                }
            }
        }
    }

    bool ShouldLoadTargetBehindLoadingOverlay(string targetSceneName)
    {
        return IsMainMenuScene(targetSceneName) ||
            IsPersistentBootstrapScene(targetSceneName);
    }

    bool IsMainMenuScene(string sceneName)
    {
        return string.Equals(
            sceneName,
            "MainMenu",
            System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsPersistentBootstrapScene(string sceneName)
    {
        return string.Equals(
            sceneName,
            PersistentBootstrapSceneName,
            System.StringComparison.OrdinalIgnoreCase);
    }

    bool IsTargetSceneReadyForReveal(string targetSceneName)
    {
        if (IsMainMenuScene(targetSceneName))
        {
            MainMenuBackgroundVideoController controller =
                FindAnyObjectByType<MainMenuBackgroundVideoController>(
                    FindObjectsInactive.Include);

            if (controller == null)
            {
                return false;
            }

            return controller.IsRevealReady &&
                IsNewGameIntroReadyForReveal();
        }

        if (IsPersistentBootstrapScene(targetSceneName))
        {
            return IsPersistentGameplayReadyForReveal();
        }

        if (!ShouldLoadTargetBehindLoadingOverlay(targetSceneName))
        {
            return true;
        }

        return true;
    }

    float GetTargetWarmupProgress(string targetSceneName)
    {
        if (IsMainMenuScene(targetSceneName))
        {
            float backgroundProgress = 0f;
            MainMenuBackgroundVideoController backgroundController =
                FindAnyObjectByType<MainMenuBackgroundVideoController>(
                    FindObjectsInactive.Include);
            if (backgroundController != null &&
                backgroundController.IsRevealReady)
            {
                backgroundProgress = 1f;
            }

            NewGameIntroPanel introPanel =
                FindAnyObjectByType<NewGameIntroPanel>(
                    FindObjectsInactive.Include);
            float introProgress =
                introPanel != null
                    ? introPanel.IntroPreloadProgress
                    : 0f;

            return Mathf.Clamp01((backgroundProgress + introProgress) * 0.5f);
        }

        if (IsPersistentBootstrapScene(targetSceneName))
        {
            return GetPersistentGameplayWarmupProgress();
        }

        if (!ShouldLoadTargetBehindLoadingOverlay(targetSceneName))
        {
            return 1f;
        }

        return 1f;
    }

    bool IsNewGameIntroReadyForReveal()
    {
        NewGameIntroPanel introPanel =
            FindAnyObjectByType<NewGameIntroPanel>(
                FindObjectsInactive.Include);

        return introPanel != null &&
            introPanel.IsReadyToOpenImmediately;
    }

    bool IsPersistentGameplayReadyForReveal()
    {
        return FindRevealGameplayScene().IsValid();
    }

    float GetPersistentGameplayWarmupProgress()
    {
        Scene persistentScene =
            SceneManager.GetSceneByName(PersistentBootstrapSceneName);
        float bootstrapProgress =
            persistentScene.IsValid() && persistentScene.isLoaded
                ? 0.5f
                : 0f;

        return bootstrapProgress +
            (FindRevealGameplayScene().IsValid() ? 0.5f : 0f);
    }

    string BuildTargetWarmupStatus(string targetSceneName)
    {
        if (IsMainMenuScene(targetSceneName))
        {
            MainMenuBackgroundVideoController backgroundController =
                FindAnyObjectByType<MainMenuBackgroundVideoController>(
                    FindObjectsInactive.Include);
            NewGameIntroPanel introPanel =
                FindAnyObjectByType<NewGameIntroPanel>(
                    FindObjectsInactive.Include);

            string backgroundStatus =
                backgroundController == null
                    ? "background=missing"
                    : "backgroundReady=" + backgroundController.IsRevealReady;
            string introStatus =
                introPanel == null
                    ? "intro=missing"
                    : "introReady=" + introPanel.IsReadyToOpenImmediately +
                      " introProgress=" +
                      introPanel.IntroPreloadProgress.ToString("0.00");

            return backgroundStatus + " " + introStatus;
        }

        if (IsPersistentBootstrapScene(targetSceneName))
        {
            Scene persistentScene =
                SceneManager.GetSceneByName(PersistentBootstrapSceneName);
            Scene gameplayScene = FindRevealGameplayScene();
            string expectedGameplayScene = ResolveExpectedGameplaySceneName();

            return "persistentLoaded=" +
                (persistentScene.IsValid() && persistentScene.isLoaded) +
                " expectedGameplay=" + expectedGameplayScene +
                " loadedGameplay=" +
                (gameplayScene.IsValid() ? gameplayScene.name : "missing") +
                " activeScene=" + SceneManager.GetActiveScene().name;
        }

        return "target=" + targetSceneName;
    }

    Scene FindRevealGameplayScene()
    {
        string expectedGameplayScene = ResolveExpectedGameplaySceneName();
        Scene expectedScene =
            SceneManager.GetSceneByName(expectedGameplayScene);
        if (expectedScene.IsValid() && expectedScene.isLoaded)
        {
            return expectedScene;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (IsGameplayScene(activeScene))
        {
            return activeScene;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (IsGameplayScene(scene))
            {
                return scene;
            }
        }

        return default;
    }

    string ResolveExpectedGameplaySceneName()
    {
        if (GameSaveSystem.ShouldLoadSavedGame)
        {
            string savedScene =
                GameSaveSystem.LoadCurrentScene(DefaultGameplaySceneName);
            if (IsGameplaySceneName(savedScene))
            {
                return savedScene;
            }
        }

        return DefaultGameplaySceneName;
    }

    bool IsGameplayScene(Scene scene)
    {
        return scene.IsValid() &&
            scene.isLoaded &&
            IsGameplaySceneName(scene.name);
    }

    bool IsGameplaySceneName(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) &&
            !string.Equals(
                sceneName,
                DefaultLoadingSceneName,
                System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                sceneName,
                PersistentBootstrapSceneName,
                System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                sceneName,
                LiteMapSceneName,
                System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                sceneName,
                "MainMenu",
                System.StringComparison.OrdinalIgnoreCase);
    }

    IEnumerator RevealLoadedTargetScene(string targetSceneName)
    {
        Scene revealScene =
            IsPersistentBootstrapScene(targetSceneName)
                ? FindRevealGameplayScene()
                : SceneManager.GetSceneByName(targetSceneName);
        if (revealScene.IsValid() && revealScene.isLoaded)
        {
            SceneManager.SetActiveScene(revealScene);
            RestoreMainMenuForReveal(targetSceneName);
        }

        yield return null;

        AsyncOperation unloadOperation =
            SceneManager.UnloadSceneAsync(GetLoadingSceneName());

        while (unloadOperation != null && !unloadOperation.isDone)
        {
            yield return null;
        }
    }

    void RestoreMainMenuForReveal(string targetSceneName)
    {
        if (!ShouldLoadTargetBehindLoadingOverlay(targetSceneName))
        {
            return;
        }

        NewGameIntroPanel introPanel =
            FindAnyObjectByType<NewGameIntroPanel>(
                FindObjectsInactive.Include);
        if (introPanel != null)
        {
            introPanel.PrepareForMainMenuReveal();
        }
    }

    IEnumerator WaitForRevealDelay()
    {
        float timer = 0f;
        while (timer < FinalRevealDelaySeconds)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void ApplyProgress(float value)
    {
        progressValue = Mathf.Clamp01(value);
        int percent = Mathf.RoundToInt(progressValue * 100f);
        string percentValue = percent + "%";

        if (progressFill != null)
        {
            progressFill.fillAmount = progressValue;
        }

        if (percentText != null)
        {
            percentText.text = percentValue;
        }

        if (percentLegacyText != null)
        {
            percentLegacyText.text = percentValue;
        }

        UpdateBackgroundFromProgress();
    }

    void ConfigureProgressFill()
    {
        if (progressFill == null)
        {
            return;
        }

        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillClockwise = true;
        progressFill.fillAmount = progressValue;
    }

    void CaptureLoadingLabelState()
    {
        if (loadingText != null)
        {
            if (!string.IsNullOrWhiteSpace(loadingText.text))
            {
                loadingBaseText = loadingText.text.TrimEnd('.', ' ');
            }

            if (loadingText.rectTransform != null)
            {
                loadingTextRect = loadingText.rectTransform;
            }
        }
        else if (loadingLegacyText != null)
        {
            if (!string.IsNullOrWhiteSpace(loadingLegacyText.text))
            {
                loadingBaseText = loadingLegacyText.text.TrimEnd('.', ' ');
            }

            loadingTextRect = loadingLegacyText.GetComponent<RectTransform>();
        }

        if (loadingTextRect != null)
        {
            loadingTextBasePosition = loadingTextRect.anchoredPosition;
        }
    }

    void UpdateBackgroundFromProgress()
    {
        if (backgroundImage == null ||
            backgroundFrames == null ||
            backgroundFrames.Length == 0)
        {
            return;
        }

        int targetIndex = ResolveBackgroundIndexFromProgress(progressValue);
        if (targetIndex == backgroundIndex)
        {
            return;
        }

        backgroundIndex = targetIndex;
        QueueBackgroundFrame(backgroundIndex);
    }

    void QueueBackgroundFrame(int index)
    {
        if (backgroundImage == null ||
            backgroundFrames == null ||
            backgroundFrames.Length == 0)
        {
            return;
        }

        int safeIndex = Mathf.Clamp(index, 0, backgroundFrames.Length - 1);
        Sprite frame = backgroundFrames[safeIndex];
        if (frame == null)
        {
            return;
        }

        float fadeDuration = GetBackgroundCrossFadeSeconds();
        if (fadeDuration <= 0f || backgroundOverlayImage == null)
        {
            SetBackgroundSprite(backgroundImage, frame);
            return;
        }

        if (isBackgroundFadeActive)
        {
            CompleteBackgroundFade();
        }

        SetBackgroundSprite(backgroundOverlayImage, frame);
        SetImageAlpha(backgroundOverlayImage, 0f);
        backgroundOverlayImage.enabled = true;
        backgroundFadeTimer = 0f;
        isBackgroundFadeActive = true;
    }

    void SetBackgroundFrameImmediate(int index)
    {
        if (backgroundImage == null ||
            backgroundFrames == null ||
            backgroundFrames.Length == 0)
        {
            return;
        }

        int safeIndex = Mathf.Clamp(index, 0, backgroundFrames.Length - 1);
        Sprite frame = backgroundFrames[safeIndex];
        if (frame == null)
        {
            return;
        }

        SetBackgroundSprite(backgroundImage, frame);

        if (backgroundOverlayImage != null)
        {
            backgroundOverlayImage.enabled = false;
            SetImageAlpha(backgroundOverlayImage, 0f);
        }

        backgroundFadeTimer = 0f;
        isBackgroundFadeActive = false;
        backgroundIndex = safeIndex;
    }

    void UpdateBackgroundFade()
    {
        if (!isBackgroundFadeActive ||
            backgroundOverlayImage == null ||
            backgroundImage == null)
        {
            return;
        }

        float fadeDuration = GetBackgroundCrossFadeSeconds();
        if (fadeDuration <= 0f)
        {
            CompleteBackgroundFade();
            return;
        }

        backgroundFadeTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(backgroundFadeTimer / fadeDuration);
        SetImageAlpha(backgroundOverlayImage, t);

        if (t >= 1f)
        {
            CompleteBackgroundFade();
        }
    }

    void CompleteBackgroundFade()
    {
        if (backgroundOverlayImage == null)
        {
            isBackgroundFadeActive = false;
            backgroundFadeTimer = 0f;
            return;
        }

        if (backgroundOverlayImage.sprite != null)
        {
            SetBackgroundSprite(backgroundImage, backgroundOverlayImage.sprite);
        }

        backgroundOverlayImage.enabled = false;
        SetImageAlpha(backgroundOverlayImage, 0f);
        backgroundFadeTimer = 0f;
        isBackgroundFadeActive = false;
    }

    void EnsureBackgroundFadeImage()
    {
        if (backgroundImage == null || backgroundOverlayImage != null)
        {
            return;
        }

        GameObject overlayObject = new GameObject(
            backgroundImage.name + "_Fade",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform overlayRect =
            overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(backgroundImage.transform.parent, false);
        CopyRectTransform(backgroundImage.rectTransform, overlayRect);
        overlayRect.SetSiblingIndex(
            backgroundImage.transform.GetSiblingIndex() + 1);

        backgroundOverlayImage = overlayObject.GetComponent<Image>();
        CopyImageSettings(backgroundImage, backgroundOverlayImage);
        backgroundOverlayImage.raycastTarget = false;
        backgroundOverlayImage.enabled = false;
        SetImageAlpha(backgroundOverlayImage, 0f);
    }

    void SetBackgroundSprite(Image targetImage, Sprite frame)
    {
        if (targetImage == null || frame == null)
        {
            return;
        }

        targetImage.sprite = frame;
        targetImage.preserveAspect = false;
    }

    void CopyRectTransform(
        RectTransform source,
        RectTransform target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    void CopyImageSettings(
        Image source,
        Image target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.color = source.color;
        target.material = source.material;
        target.type = source.type;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.useSpriteMesh = source.useSpriteMesh;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.maskable = source.maskable;
        target.preserveAspect = source.preserveAspect;
    }

    void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    void UpdateLoadingLabelAnimation()
    {
        string animatedText =
            loadingBaseText + new string('.', GetDotCount());

        if (loadingText != null)
        {
            loadingText.text = animatedText;
        }

        if (loadingLegacyText != null)
        {
            loadingLegacyText.text = animatedText;
        }

        if (loadingTextRect != null)
        {
            float offsetY =
                Mathf.Sin(Time.unscaledTime * GetLoadingBounceSpeed()) *
                GetLoadingBounceAmplitude();
            loadingTextRect.anchoredPosition =
                loadingTextBasePosition + Vector2.up * offsetY;
        }
    }

    int GetDotCount()
    {
        float frameDuration = GetLoadingTextFrameSeconds();
        if (frameDuration <= 0f)
        {
            return 0;
        }

        loadingTextTimer += Time.unscaledDeltaTime;
        int dotCount = Mathf.FloorToInt(loadingTextTimer / frameDuration) % 4;
        if (loadingTextTimer >= frameDuration * 4f)
        {
            loadingTextTimer = 0f;
        }

        return dotCount;
    }

    string ConsumeTargetSceneName()
    {
        if (!string.IsNullOrWhiteSpace(pendingTargetSceneName))
        {
            string targetSceneName = pendingTargetSceneName;
            pendingTargetSceneName = null;
            return targetSceneName;
        }

        if (config != null &&
            !string.IsNullOrWhiteSpace(config.defaultTargetScene))
        {
            return config.defaultTargetScene;
        }

        return "MainMenu";
    }

    float GetMinimumLoadingSeconds()
    {
        if (config != null && config.minimumLoadingSeconds > 0f)
        {
            return config.minimumLoadingSeconds;
        }

        return 10f;
    }

    string GetLoadingSceneName()
    {
        if (config != null &&
            !string.IsNullOrWhiteSpace(config.loadingSceneName))
        {
            return config.loadingSceneName;
        }

        return DefaultLoadingSceneName;
    }

    float GetLoadingTextFrameSeconds()
    {
        if (config != null && config.loadingTextFrameSeconds > 0f)
        {
            return config.loadingTextFrameSeconds;
        }

        return 0.2f;
    }

    float GetBackgroundCrossFadeSeconds()
    {
        if (config != null && config.backgroundCrossFadeSeconds > 0f)
        {
            return config.backgroundCrossFadeSeconds;
        }

        return 0.22f;
    }

    int ResolveBackgroundIndexFromProgress(float progress)
    {
        int frameCount = backgroundFrames != null
            ? backgroundFrames.Length
            : 0;

        if (frameCount <= 1)
        {
            return 0;
        }

        float safeProgress = Mathf.Clamp01(progress);
        if (safeProgress >= 0.9999f)
        {
            return frameCount - 1;
        }

        int preFinalFrameCount = frameCount - 1;
        float scaledProgress = safeProgress * preFinalFrameCount;
        return Mathf.Clamp(
            Mathf.FloorToInt(scaledProgress),
            0,
            preFinalFrameCount - 1);
    }

    float GetLoadingBounceAmplitude()
    {
        if (config != null)
        {
            return Mathf.Max(0f, config.loadingBounceAmplitude);
        }

        return 8f;
    }

    float GetLoadingBounceSpeed()
    {
        if (config != null && config.loadingBounceSpeed > 0f)
        {
            return config.loadingBounceSpeed;
        }

        return 4f;
    }

    Image FindImage(Transform root, string objectName)
    {
        Transform target = FindDescendant(root, objectName);
        return target != null ? target.GetComponent<Image>() : null;
    }

    TMP_Text FindTmpText(Transform root, string objectName)
    {
        Transform target = FindDescendant(root, objectName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    Text FindLegacyText(Transform root, string objectName)
    {
        Transform target = FindDescendant(root, objectName);
        return target != null ? target.GetComponent<Text>() : null;
    }

    Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDescendant(root.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
