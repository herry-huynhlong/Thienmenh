using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NewGameIntroPanel : MonoBehaviour
{
    [Header("Panel")]
    public GameObject introPanel;

    [Header("Texts")]
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text pageText;
    public Color introTitleColor = new Color(1f, 1f, 1f, 0.96f);
    public Color introBodyColor = new Color(1f, 0.97f, 0.9f, 0.98f);
    public Color introPageColor = new Color(1f, 1f, 1f, 0.9f);
    public Color introTextOutlineColor = new Color(0f, 0f, 0f, 0.85f);
    [Range(0f, 1f)]
    public float introTextOutlineWidth = 0.22f;

    [Header("Buttons")]
    public Button backButton;
    public Button nextButton;
    public Button enterWorldButton;
    public Button closeButton;

    [Header("Scene")]
    public string firstGameScene = "PersistentScene";

    [Header("Typing Effect")]
    public bool useTypingEffect = true;
    public float charDelay = 0.035f;
    public float lineDelay = 0.25f;

    [Header("Intro Pages")]
    [TextArea(2, 5)]
    public string[] pageTitles;

    [TextArea(5, 12)]
    public string[] pageBodies;

    [Header("Intro Video")]
    public RawImage videoBackground;
    public RenderTexture introRenderTexture;
    public VideoPlayer introVideoPlayer;
    public VideoClip[] introVideos;
    public bool hideLegacyBackgroundWhenVideoEnabled = true;
    public Vector2Int runtimeIntroVideoSize = new Vector2Int(540, 960);
    [Range(0.1f, 2f)]
    public float introVideoPlaybackSpeed = 0.5f;
    public Image videoDimOverlay;
    [Range(0f, 1f)]
    public float videoDimAlpha = 0.28f;
    public GameObject menuRootToHide;

    private int currentPage;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private string currentFullBodyText;
    private GameObject legacyBackgroundObject;
    private int pendingVideoPage = -1;
    private bool shouldBeginTextAfterVideo;
    private int bodyTextStartedPage = -1;
    private Coroutine waitForFirstFrameCoroutine;
    private Coroutine delayedShowIntroCoroutine;
    private Coroutine primeFirstVideoCoroutine;
    private bool introVideoHasFrame;
    private bool firstIntroVideoReady;
    private bool firstIntroVideoPreparing;
    private bool firstIntroVideoPreloadFailed;
    private bool pageVideoLoading;
    private int loadingVideoPage = -1;
    private int visiblePage = -1;
    private int configuredVideoPage = -1;
    private int preparingVideoPage = -1;
    private int preparedVideoPage = -1;
    private int activeVideoPage = -1;
    private readonly Dictionary<Transform, bool> menuChildVisibilityBeforeIntro =
        new Dictionary<Transform, bool>();

    private void Awake()
    {
        TryAutoAssignIntroAssetsInEditor();
        TryAutoBindVideoReferences();
        EnsureMenuRootActive();
        ApplyIntroTextReadability();
        ApplyIntroRaycastRules();
        ConfigureIntroVideoPlayer();

        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }

        ApplyMenuVisibility(false);

        if (backButton != null)
        {
            backButton.onClick.AddListener(BackPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextPage);
        }

        if (enterWorldButton != null)
        {
            enterWorldButton.onClick.AddListener(EnterWorld);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideIntro);
        }

        ReloadLocalizedIntroContent();

        if (bodyText != null)
        {
            bodyText.text = "";
        }

        RefreshButtons();
        LocalizationSettings.LanguageChanged += HandleLanguageChanged;
    }

    private void OnEnable()
    {
        EnsureMenuRootActive();
    }

    private void OnDestroy()
    {
        LocalizationSettings.LanguageChanged -= HandleLanguageChanged;
        StopDelayedShowIntro();
        StopPrimeFirstVideo();
        StopIntroVideo();
    }

    private void OnValidate()
    {
        TryAutoBindVideoReferences();
        TryAutoAssignIntroAssetsInEditor();
        ApplyIntroTextReadability();
        ApplyIntroRaycastRules();
    }

    public bool IsReadyToOpenImmediately
    {
        get
        {
            return !HasIntroVideoForPage(0) ||
                firstIntroVideoReady ||
                firstIntroVideoPreloadFailed;
        }
    }

    public float IntroPreloadProgress
    {
        get
        {
            if (!HasIntroVideoForPage(0) ||
                firstIntroVideoReady ||
                firstIntroVideoPreloadFailed)
            {
                return 1f;
            }

            return firstIntroVideoPreparing ? 0.5f : 0f;
        }
    }

    public void PreloadIntroForMainMenu()
    {
        TryAutoAssignIntroAssetsInEditor();
        TryAutoBindVideoReferences();
        ConfigureIntroVideoPlayer();
        PrewarmFirstIntroVideo();
    }

    public void ShowIntro()
    {
        StopDelayedShowIntro();
        StopTyping();
        StopPrimeFirstVideo();
        ReloadLocalizedIntroContent();
        currentPage = 0;
        visiblePage = -1;
        pageVideoLoading = false;
        loadingVideoPage = -1;
        bodyTextStartedPage = -1;
        TryAutoAssignIntroAssetsInEditor();
        TryAutoBindVideoReferences();
        ApplyIntroTextReadability();
        ApplyIntroRaycastRules();
        ConfigureIntroVideoPlayer();
        if (IsReadyToOpenImmediately)
        {
            if (!firstIntroVideoReady)
            {
                ResetIntroVideoForShow();
            }
        }
        else
        {
            PrewarmFirstIntroVideo();
        }

        if (titleText != null)
        {
            titleText.text = string.Empty;
        }

        if (bodyText != null)
        {
            bodyText.text = string.Empty;
        }

        if (pageText != null)
        {
            pageText.text = string.Empty;
        }

        ApplyLegacyBackgroundState(false);
        SetVideoBackgroundVisible(false);
        ApplyMenuVisibility(false);

        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }

        OpenIntroImmediately();
    }

    public void HideIntro()
    {
        StopDelayedShowIntro();
        StopTyping();
        StopIntroVideo();
        pageVideoLoading = false;
        loadingVideoPage = -1;
        ApplyLegacyBackgroundState(false);
        SetVideoBackgroundVisible(false);
        ApplyMenuVisibility(false);

        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }

        PreloadIntroForMainMenu();
    }

    public void PrepareForMainMenuReveal()
    {
        StopDelayedShowIntro();
        StopTyping();
        pageVideoLoading = false;
        loadingVideoPage = -1;
        ApplyLegacyBackgroundState(false);
        SetVideoBackgroundVisible(false);
        ApplyMenuVisibility(false);

        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }

        PreloadIntroForMainMenu();
    }

    private IEnumerator ShowIntroWhenPreloadReady()
    {
        float elapsed = 0f;
        const float timeoutSeconds = 8f;

        while (!IsReadyToOpenImmediately && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        delayedShowIntroCoroutine = null;

        if (!IsReadyToOpenImmediately)
        {
            StopPrimeFirstVideo();
            firstIntroVideoPreloadFailed = true;
        }

        OpenIntroImmediately();
    }

    private void OpenIntroImmediately()
    {
        RefreshPage();
    }

    public void NextPage()
    {
        if (IsPageVideoLoading())
        {
            return;
        }

        if (isTyping)
        {
            FinishTypingImmediately();
            return;
        }

        if (pageBodies == null || pageBodies.Length == 0)
        {
            return;
        }

        currentPage++;

        if (currentPage >= pageBodies.Length)
        {
            currentPage = pageBodies.Length - 1;
        }

        RefreshPage();
    }

    public void BackPage()
    {
        if (IsPageVideoLoading())
        {
            return;
        }

        if (isTyping)
        {
            FinishTypingImmediately();
            return;
        }

        currentPage--;

        if (currentPage < 0)
        {
            currentPage = 0;
        }

        RefreshPage();
    }

    private void RefreshPage()
    {
        if (pageBodies == null || pageBodies.Length == 0)
        {
            return;
        }

        StopTyping();
        bodyTextStartedPage = -1;

        currentFullBodyText = pageBodies[currentPage];
        shouldBeginTextAfterVideo = HasIntroVideoForPage(currentPage);

        if (shouldBeginTextAfterVideo)
        {
            pageVideoLoading = true;
            loadingVideoPage = currentPage;
            ApplyCurrentPageTextState(true);
            RevealIntroShell();
            RefreshButtons();
            PlayVideoForPage(currentPage);
            return;
        }

        pageVideoLoading = false;
        loadingVideoPage = -1;
        ApplyCurrentPageTextState(false);
        PlayVideoForPage(currentPage);
    }

    private IEnumerator TypeBodyText(string fullText)
    {
        isTyping = true;

        int[] textElementIndexes =
            StringInfo.ParseCombiningCharacters(fullText);

        if (bodyText != null)
        {
            bodyText.text = string.Empty;
            bodyText.maxVisibleCharacters = int.MaxValue;
        }

        for (int textElementIndex = 0;
             textElementIndex < textElementIndexes.Length;
             textElementIndex++)
        {
            int startIndex = textElementIndexes[textElementIndex];
            int nextIndex =
                textElementIndex + 1 < textElementIndexes.Length
                    ? textElementIndexes[textElementIndex + 1]
                    : fullText.Length;
            int length = nextIndex - startIndex;
            string textElement = fullText.Substring(startIndex, length);

            if (bodyText != null)
            {
                bodyText.text += textElement;
            }

            if (textElement == "\n")
            {
                yield return new WaitForSeconds(lineDelay);
            }
            else
            {
                yield return new WaitForSeconds(charDelay);
            }
        }

        isTyping = false;
        typingCoroutine = null;

        RefreshButtons();
    }

    private void FinishTypingImmediately()
    {
        shouldBeginTextAfterVideo = false;
        StopTyping();
        bodyTextStartedPage = currentPage;

        if (bodyText != null)
        {
            bodyText.text = currentFullBodyText;
        }

        RefreshButtons();
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
    }

    private void BeginBodyText()
    {
        if (bodyText == null)
        {
            return;
        }

        if (bodyTextStartedPage == currentPage &&
            (isTyping || bodyText.text == currentFullBodyText))
        {
            return;
        }

        shouldBeginTextAfterVideo = false;
        bodyTextStartedPage = currentPage;
        StopTyping();

        if (useTypingEffect)
        {
            bodyText.text = string.Empty;
            typingCoroutine = StartCoroutine(TypeBodyText(currentFullBodyText));
        }
        else
        {
            bodyText.text = currentFullBodyText;
        }
    }

    private void ApplyCurrentPageTextState(bool waitForVideoBeforeBody)
    {
        if (pageBodies == null ||
            currentPage < 0 ||
            currentPage >= pageBodies.Length)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.text = GetLocalizedPageTitle();
        }

        currentFullBodyText = pageBodies[currentPage];
        shouldBeginTextAfterVideo = waitForVideoBeforeBody;
        bodyTextStartedPage = -1;
        visiblePage = currentPage;

        if (bodyText != null)
        {
            bodyText.text = waitForVideoBeforeBody
                ? string.Empty
                : currentFullBodyText;
        }

        ApplyLocalizedPageIndicator();
        RefreshButtons();
    }

    private bool IsPageVideoLoading()
    {
        return pageVideoLoading &&
            loadingVideoPage >= 0 &&
            loadingVideoPage == currentPage;
    }

    private void RefreshButtons()
    {
        if (pageBodies == null || pageBodies.Length == 0)
        {
            return;
        }

        bool isLoading = IsPageVideoLoading();
        int pageForButtons =
            isLoading && visiblePage >= 0
                ? visiblePage
                : currentPage;
        bool isFirstPage = pageForButtons <= 0;
        bool isLastPage = pageForButtons >= pageBodies.Length - 1;

        if (backButton != null)
        {
            backButton.gameObject.SetActive(!isFirstPage);
            backButton.interactable = !isLoading;
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(!isLastPage);
            nextButton.interactable = !isLoading;
        }

        if (enterWorldButton != null)
        {
            enterWorldButton.gameObject.SetActive(isLastPage);
            enterWorldButton.interactable = !isLoading;
        }
    }

    private void EnterWorld()
    {
        if (isTyping)
        {
            FinishTypingImmediately();
            return;
        }

        StopIntroVideo();

        MainMenuManager mainMenuManager =
            FindAnyObjectByType<MainMenuManager>();

        if (mainMenuManager != null)
        {
            mainMenuManager.StartNewGameNow();
            return;
        }

        PlayerPrefs.SetInt("ThienMenh_NewGame", 1);
        PlayerPrefs.Save();

        if (LoadingSceneController.TryLoadLoadingScene(firstGameScene))
        {
            return;
        }

        SceneManager.LoadScene(firstGameScene);
    }

    private void CreateDefaultIntroIfEmpty()
    {
        if (TryLoadIntroFromJson())
        {
            return;
        }

        if (pageBodies != null && pageBodies.Length > 0)
        {
            return;
        }

        pageTitles = UiText.Lines("intro", "defaultPageTitles");
        pageBodies = UiText.Lines("intro", "defaultPageBodies");
    }

    private void ReloadLocalizedIntroContent()
    {
        pageTitles = null;
        pageBodies = null;
        CreateDefaultIntroIfEmpty();
    }

    private void HandleLanguageChanged()
    {
        ReloadLocalizedIntroContent();

        if (introPanel != null && introPanel.activeSelf)
        {
            currentPage = Mathf.Clamp(
                currentPage,
                0,
                pageBodies != null && pageBodies.Length > 0
                    ? pageBodies.Length - 1
                    : 0);
            RefreshPage();
        }
    }

    private string GetLocalizedPageTitle()
    {
        if (pageTitles != null &&
            currentPage < pageTitles.Length &&
            !string.IsNullOrWhiteSpace(pageTitles[currentPage]))
        {
            return pageTitles[currentPage];
        }

        return UiText.Get("intro", "fallbackTitle");
    }

    private void ApplyLocalizedPageIndicator()
    {
        if (pageText == null ||
            pageBodies == null ||
            pageBodies.Length == 0)
        {
            return;
        }

        pageText.text = UiText.Format(
            "intro",
            "pageIndicatorFormat",
            currentPage + 1,
            pageBodies.Length);
    }

    private bool TryLoadIntroFromJson()
    {
        string[] loadedBodies =
            UiText.Lines("intro", "pageBodies");
        if (loadedBodies == null ||
            loadedBodies.Length == 0)
        {
            return false;
        }

        string[] loadedTitles =
            UiText.Lines("intro", "pageTitles");

        pageBodies = loadedBodies;
        pageTitles =
            loadedTitles != null &&
            loadedTitles.Length > 0
                ? loadedTitles
                : pageTitles;
        return true;
    }

    private void PlayVideoForPage(int pageIndex)
    {
        if (!HasIntroVideoForPage(pageIndex))
        {
            pendingVideoPage = -1;
            StopIntroVideo();
            SetVideoBackgroundVisible(false);
            ApplyLegacyBackgroundState(false);
            RevealIntroPanelWithoutVideo();
            return;
        }

        TryAutoBindVideoReferences();
        ConfigureIntroVideoPlayer();
        if (introVideoPlayer == null)
        {
            pendingVideoPage = -1;
            shouldBeginTextAfterVideo = false;
            RevealIntroPanelWithoutVideo();
            return;
        }

        pendingVideoPage = pageIndex;

        if (!ConfigureIntroVideoSourceForPage(pageIndex))
        {
            pendingVideoPage = -1;
            RevealIntroPanelWithoutVideo();
            return;
        }

        bool showedPreloadedFrame =
            ShowPreloadedIntroFrame(pageIndex);
        if (showedPreloadedFrame &&
            shouldBeginTextAfterVideo)
        {
            BeginBodyText();
        }

        if (IsPreparedForPage(pageIndex))
        {
            StartPreparedVideo(pageIndex, showedPreloadedFrame);
            return;
        }

        introVideoHasFrame = false;
        StopWaitingForFirstFrame();
        preparingVideoPage = pageIndex;
        introVideoPlayer.Stop();
        introVideoPlayer.Prepare();
    }

    private void OnIntroVideoPrepared(VideoPlayer source)
    {
        if (source == null ||
            source != introVideoPlayer)
        {
            return;
        }

        preparedVideoPage =
            preparingVideoPage >= 0
                ? preparingVideoPage
                : configuredVideoPage;
        preparingVideoPage = -1;

        if (pendingVideoPage < 0 ||
            pendingVideoPage != currentPage ||
            !IsPreparedForPage(pendingVideoPage))
        {
            return;
        }

        StartPreparedVideo(pendingVideoPage);
    }

    private void OnIntroVideoFrameReady(
        VideoPlayer source,
        long frameIndex)
    {
        if (source == null ||
            source != introVideoPlayer ||
            frameIndex < 0)
        {
            return;
        }

        introVideoHasFrame = true;

        // The render texture can still contain the cleared black frame in this
        // callback. WaitForFirstRenderedVideoFrame reveals after Unity presents
        // the decoded frame to the target texture.
    }

    private void StopIntroVideo()
    {
        pendingVideoPage = -1;
        activeVideoPage = -1;
        preparingVideoPage = -1;
        introVideoHasFrame = false;
        firstIntroVideoReady = false;
        firstIntroVideoPreparing = false;
        pageVideoLoading = false;
        loadingVideoPage = -1;
        preparedVideoPage = -1;
        StopWaitingForFirstFrame();
        StopPrimeFirstVideo();

        if (introVideoPlayer == null)
        {
            return;
        }

        introVideoPlayer.Stop();
    }

    private bool HasIntroVideoForPage(int pageIndex)
    {
        return introVideos != null &&
            pageIndex >= 0 &&
            pageIndex < introVideos.Length &&
            introVideos[pageIndex] != null;
    }

    private void ConfigureIntroVideoPlayer()
    {
        TryAutoBindVideoReferences();

        if (introVideoPlayer == null)
        {
            introVideoPlayer = GetComponent<VideoPlayer>();
            if (introVideoPlayer == null)
            {
                introVideoPlayer = gameObject.AddComponent<VideoPlayer>();
            }
        }

        if (introVideoPlayer == null)
        {
            return;
        }

        EnsureIntroRenderTexture();
        if (introRenderTexture == null &&
            videoBackground != null &&
            videoBackground.texture is RenderTexture rawImageTexture)
        {
            introRenderTexture = rawImageTexture;
        }

        introVideoPlayer.playOnAwake = false;
        introVideoPlayer.waitForFirstFrame = true;
        introVideoPlayer.skipOnDrop = true;
        introVideoPlayer.isLooping = true;
        introVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        introVideoPlayer.playbackSpeed =
            Mathf.Max(0.1f, introVideoPlaybackSpeed);
        introVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        introVideoPlayer.sendFrameReadyEvents = true;
        introVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        introVideoPlayer.targetTexture = introRenderTexture;
        introVideoPlayer.errorReceived -= OnIntroVideoErrorReceived;
        introVideoPlayer.errorReceived += OnIntroVideoErrorReceived;
        introVideoPlayer.prepareCompleted -= OnIntroVideoPrepared;
        introVideoPlayer.prepareCompleted += OnIntroVideoPrepared;
        introVideoPlayer.frameReady -= OnIntroVideoFrameReady;
        introVideoPlayer.frameReady += OnIntroVideoFrameReady;
    }

    private bool ConfigureIntroVideoSourceForPage(int pageIndex)
    {
        if (introVideoPlayer == null ||
            !HasIntroVideoForPage(pageIndex))
        {
            return false;
        }

        string editorVideoUrl =
            GetEditorIntroVideoUrl(pageIndex);
        if (!string.IsNullOrEmpty(editorVideoUrl))
        {
            if (pageIndex != 0)
            {
                firstIntroVideoReady = false;
            }

            if (introVideoPlayer.source != VideoSource.Url ||
                introVideoPlayer.url != editorVideoUrl)
            {
                preparedVideoPage = -1;
                preparingVideoPage = -1;
                introVideoPlayer.source = VideoSource.Url;
                introVideoPlayer.clip = null;
                introVideoPlayer.url = editorVideoUrl;
            }

            configuredVideoPage = pageIndex;
            return true;
        }

        VideoClip clip = introVideos[pageIndex];
        if (clip == null)
        {
            return false;
        }

        if (pageIndex != 0)
        {
            firstIntroVideoReady = false;
        }

        if (introVideoPlayer.source != VideoSource.VideoClip ||
            introVideoPlayer.clip != clip)
        {
            preparedVideoPage = -1;
            preparingVideoPage = -1;
            introVideoPlayer.source = VideoSource.VideoClip;
            introVideoPlayer.url = string.Empty;
            introVideoPlayer.clip = clip;
        }

        configuredVideoPage = pageIndex;
        return true;
    }

    private void StartPreparedVideo(int pageIndex, bool hasVisibleFrame = false)
    {
        if (introVideoPlayer == null ||
            pageIndex != currentPage ||
            !IsPreparedForPage(pageIndex))
        {
            return;
        }

        pendingVideoPage = -1;
        activeVideoPage = pageIndex;
        introVideoHasFrame = false;
        pageVideoLoading = true;
        loadingVideoPage = pageIndex;
        StopWaitingForFirstFrame();
        if (!hasVisibleFrame)
        {
            introVideoPlayer.time = 0d;
        }
        introVideoPlayer.Play();

        if (hasVisibleFrame)
        {
            RevealPreparedVideoFrame(pageIndex);
            return;
        }

        waitForFirstFrameCoroutine =
            StartCoroutine(WaitForFirstRenderedVideoFrame(pageIndex));
    }

    private void OnIntroVideoErrorReceived(
        VideoPlayer source,
        string message)
    {
        if (source == null ||
            source != introVideoPlayer)
        {
            return;
        }

        Debug.LogWarning(
            $"[NewGameIntroPanel] Intro video error page={currentPage} message='{message}'");

        if (firstIntroVideoPreparing)
        {
            firstIntroVideoPreparing = false;
            firstIntroVideoPreloadFailed = true;
            firstIntroVideoReady = false;
        }

        pendingVideoPage = -1;
        activeVideoPage = -1;
        preparingVideoPage = -1;
        introVideoHasFrame = false;
        pageVideoLoading = false;
        loadingVideoPage = -1;
        preparedVideoPage = -1;
        SetVideoBackgroundVisible(false);
        ApplyLegacyBackgroundState(false);

        if (pendingVideoPage == currentPage ||
            pageVideoLoading ||
            delayedShowIntroCoroutine != null ||
            introPanel != null && introPanel.activeSelf)
        {
            RevealIntroFallbackForCurrentPage("video error");
        }
        else
        {
            KeepIntroHiddenWhileVideoLoads();
        }
    }

    private bool IsPreparedForPage(int pageIndex)
    {
        return introVideoPlayer != null &&
            introVideoPlayer.isPrepared &&
            configuredVideoPage == pageIndex &&
            preparedVideoPage == pageIndex;
    }

    private void SetVideoBackgroundVisible(bool visible)
    {
        if (videoBackground != null)
        {
            videoBackground.enabled = visible;
        }

        if (videoDimOverlay != null)
        {
            videoDimOverlay.enabled = visible;
        }
    }

    private bool ShowPreloadedIntroFrame(int pageIndex)
    {
        if (pageIndex != 0 ||
            !firstIntroVideoReady ||
            videoBackground == null ||
            introRenderTexture == null)
        {
            return false;
        }

        videoBackground.texture = introRenderTexture;
        ApplyLegacyBackgroundState(true);
        SetVideoBackgroundVisible(true);
        return true;
    }

    private IEnumerator WaitForFirstRenderedVideoFrame(int pageIndex)
    {
        float elapsed = 0f;
        const float timeoutSeconds = 30f;

        while (elapsed < timeoutSeconds)
        {
            if (introVideoPlayer == null ||
                pageIndex != currentPage ||
                activeVideoPage != pageIndex ||
                !IsPreparedForPage(pageIndex))
            {
                waitForFirstFrameCoroutine = null;
                yield break;
            }

            if (introVideoHasFrame)
            {
                yield return new WaitForEndOfFrame();
                yield return null;

                if (introVideoPlayer == null ||
                    pageIndex != currentPage ||
                    activeVideoPage != pageIndex ||
                    !IsPreparedForPage(pageIndex))
                {
                    waitForFirstFrameCoroutine = null;
                    yield break;
                }

                RevealPreparedVideoFrame(pageIndex);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        waitForFirstFrameCoroutine = null;
        RevealIntroFallbackForCurrentPage("video frame timeout");
    }

    private void RevealIntroFallbackForCurrentPage(string reason)
    {
        Debug.LogWarning(
            $"[NewGameIntroPanel] Fallback intro page={currentPage}: {reason}");

        pendingVideoPage = -1;
        activeVideoPage = -1;
        preparingVideoPage = -1;
        introVideoHasFrame = false;
        pageVideoLoading = false;
        loadingVideoPage = -1;
        SetVideoBackgroundVisible(false);
        ApplyLegacyBackgroundState(false);
        ApplyCurrentPageTextState(true);
        RevealIntroShell();
        BeginBodyText();
    }

    private void RevealPreparedVideoFrame(int pageIndex)
    {
        StopWaitingForFirstFrame();

        if (introVideoPlayer == null ||
            introRenderTexture == null ||
            pageIndex != currentPage ||
            !IsPreparedForPage(pageIndex))
        {
            return;
        }

        if (videoBackground != null &&
            videoBackground.texture != introRenderTexture)
        {
            videoBackground.texture = introRenderTexture;
        }

        pageVideoLoading = false;
        loadingVideoPage = -1;
        ApplyCurrentPageTextState(true);
        RevealIntroShell();
        ApplyLegacyBackgroundState(true);
        SetVideoBackgroundVisible(true);

        if (shouldBeginTextAfterVideo &&
            pageIndex == currentPage)
        {
            BeginBodyText();
        }
    }

    private void RevealIntroPanelWithoutVideo()
    {
        if (HasIntroVideoForPage(currentPage))
        {
            KeepIntroHiddenWhileVideoLoads();
            return;
        }

        RevealIntroShell();
        SetVideoBackgroundVisible(false);
        ApplyLegacyBackgroundState(false);
        BeginBodyText();
    }

    private void KeepIntroHiddenWhileVideoLoads()
    {
        StopWaitingForFirstFrame();
        StopTyping();
        ApplyMenuVisibility(false);
        SetVideoBackgroundVisible(false);
        ApplyLegacyBackgroundState(false);

        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }
    }

    private void RevealIntroShell()
    {
        if (introPanel != null && !introPanel.activeSelf)
        {
            introPanel.SetActive(true);
        }

        ApplyMenuVisibility(true);
    }

    private void StopWaitingForFirstFrame()
    {
        if (waitForFirstFrameCoroutine != null)
        {
            StopCoroutine(waitForFirstFrameCoroutine);
            waitForFirstFrameCoroutine = null;
        }
    }

    private void StopDelayedShowIntro()
    {
        if (delayedShowIntroCoroutine != null)
        {
            StopCoroutine(delayedShowIntroCoroutine);
            delayedShowIntroCoroutine = null;
        }
    }

    private void PrewarmFirstIntroVideo()
    {
        if (!HasIntroVideoForPage(0))
        {
            firstIntroVideoReady = true;
            firstIntroVideoPreparing = false;
            firstIntroVideoPreloadFailed = false;
            return;
        }

        if (firstIntroVideoReady ||
            firstIntroVideoPreparing ||
            primeFirstVideoCoroutine != null)
        {
            return;
        }

        if (!isActiveAndEnabled ||
            !gameObject.activeInHierarchy)
        {
            firstIntroVideoPreparing = false;
            firstIntroVideoPreloadFailed = true;
            return;
        }

        primeFirstVideoCoroutine = StartCoroutine(PrimeFirstIntroVideo());
    }

    private void StopPrimeFirstVideo()
    {
        if (primeFirstVideoCoroutine != null)
        {
            StopCoroutine(primeFirstVideoCoroutine);
            primeFirstVideoCoroutine = null;
        }

        firstIntroVideoPreparing = false;
    }

    private IEnumerator PrimeFirstIntroVideo()
    {
        firstIntroVideoReady = false;
        firstIntroVideoPreloadFailed = false;
        firstIntroVideoPreparing = true;

        TryAutoAssignIntroAssetsInEditor();
        TryAutoBindVideoReferences();
        ConfigureIntroVideoPlayer();

        if (introVideoPlayer == null ||
            introRenderTexture == null ||
            !ConfigureIntroVideoSourceForPage(0))
        {
            firstIntroVideoPreparing = false;
            firstIntroVideoPreloadFailed = true;
            primeFirstVideoCoroutine = null;
            yield break;
        }

        introVideoPlayer.targetTexture = introRenderTexture;
        pendingVideoPage = -1;
        activeVideoPage = -1;
        introVideoHasFrame = false;
        preparedVideoPage = -1;
        preparingVideoPage = 0;
        introVideoPlayer.Stop();
        introVideoPlayer.Prepare();

        float prepareElapsed = 0f;
        const float prepareTimeoutSeconds = 10f;
        while (!IsPreparedForPage(0) &&
               prepareElapsed < prepareTimeoutSeconds)
        {
            prepareElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsPreparedForPage(0))
        {
            firstIntroVideoPreparing = false;
            firstIntroVideoPreloadFailed = true;
            primeFirstVideoCoroutine = null;
            yield break;
        }

        activeVideoPage = 0;
        introVideoHasFrame = false;
        introVideoPlayer.time = 0d;
        introVideoPlayer.Play();

        float frameElapsed = 0f;
        const float frameTimeoutSeconds = 10f;
        while (!introVideoHasFrame &&
               frameElapsed < frameTimeoutSeconds)
        {
            frameElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!introVideoHasFrame)
        {
            introVideoPlayer.Pause();
            activeVideoPage = -1;
            firstIntroVideoPreparing = false;
            firstIntroVideoPreloadFailed = true;
            primeFirstVideoCoroutine = null;
            yield break;
        }

        yield return new WaitForEndOfFrame();
        yield return null;

        introVideoPlayer.Pause();
        activeVideoPage = -1;
        firstIntroVideoReady = true;
        firstIntroVideoPreparing = false;
        primeFirstVideoCoroutine = null;
    }

    private void ResetIntroVideoForShow()
    {
        pendingVideoPage = -1;
        preparingVideoPage = -1;
        StopWaitingForFirstFrame();

        activeVideoPage = -1;
        introVideoHasFrame = false;
        preparedVideoPage = -1;

        if (introVideoPlayer != null)
        {
            introVideoPlayer.Stop();
        }
    }

    private string GetEditorIntroVideoUrl(int pageIndex)
    {
#if UNITY_EDITOR
        if (introVideos == null ||
            pageIndex < 0 ||
            pageIndex >= introVideos.Length ||
            introVideos[pageIndex] == null)
        {
            return null;
        }

        string assetPath =
            AssetDatabase.GetAssetPath(introVideos[pageIndex]);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        string fullPath =
            System.IO.Path.GetFullPath(assetPath);
        return new System.Uri(fullPath).AbsoluteUri;
#else
        return null;
#endif
    }

    private void TryAutoBindVideoReferences()
    {
        if (introPanel == null)
        {
            return;
        }

        if (videoBackground == null)
        {
            Transform videoBackgroundTransform =
                FindDescendantByName(introPanel.transform, "VideoBackground");
            if (videoBackgroundTransform != null)
            {
                videoBackground =
                    videoBackgroundTransform.GetComponent<RawImage>();
            }

            if (videoBackground == null)
            {
                videoBackground =
                    introPanel.GetComponentInChildren<RawImage>(true);
            }
        }

        if (videoBackground == null)
        {
            videoBackground = CreateRuntimeVideoBackground();
        }

        if (videoDimOverlay == null)
        {
            Transform overlayTransform =
                FindDescendantByName(introPanel.transform, "VideoDimOverlay");
            if (overlayTransform != null)
            {
                videoDimOverlay = overlayTransform.GetComponent<Image>();
            }
        }

        if (videoDimOverlay == null)
        {
            videoDimOverlay = CreateRuntimeVideoDimOverlay();
        }

        ApplyVideoDimOverlayStyle();

        if (legacyBackgroundObject == null)
        {
            Transform legacyBackgroundTransform =
                FindDescendantByName(introPanel.transform, "Background");
            if (legacyBackgroundTransform != null &&
                (videoBackground == null ||
                 legacyBackgroundTransform.gameObject !=
                 videoBackground.gameObject))
            {
                legacyBackgroundObject =
                    legacyBackgroundTransform.gameObject;
            }
        }
    }

    private void ApplyLegacyBackgroundState(bool videoEnabled)
    {
        if (!hideLegacyBackgroundWhenVideoEnabled)
        {
            return;
        }

        if (legacyBackgroundObject == null)
        {
            TryAutoBindVideoReferences();
        }

        if (legacyBackgroundObject != null)
        {
            legacyBackgroundObject.SetActive(!videoEnabled);
        }
    }

    private Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindDescendantByName(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void ApplyMenuVisibility(bool introVisible)
    {
        if (menuRootToHide == null)
        {
            menuRootToHide = FindMenuRootToHide();
        }

        if (menuRootToHide == null)
        {
            return;
        }

        if (introPanel != null &&
            (menuRootToHide == introPanel ||
             introPanel.transform.IsChildOf(menuRootToHide.transform)))
        {
            return;
        }

        EnsureMenuRootActive();
        SetMenuContentVisible(!introVisible);
    }

    private void EnsureMenuRootActive()
    {
        if (menuRootToHide == null)
        {
            menuRootToHide = FindMenuRootToHide();
        }

        if (menuRootToHide != null && !menuRootToHide.activeSelf)
        {
            menuRootToHide.SetActive(true);
        }
    }

    private void SetMenuContentVisible(bool visible)
    {
        if (menuRootToHide == null)
        {
            return;
        }

        Transform root = menuRootToHide.transform;
        if (root == null)
        {
            return;
        }

        if (visible)
        {
            foreach (KeyValuePair<Transform, bool> entry in
                     menuChildVisibilityBeforeIntro)
            {
                if (entry.Key == null)
                {
                    continue;
                }

                if (entry.Key.gameObject.activeSelf != entry.Value)
                {
                    entry.Key.gameObject.SetActive(entry.Value);
                }
            }

            menuChildVisibilityBeforeIntro.Clear();
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (!menuChildVisibilityBeforeIntro.ContainsKey(child))
            {
                menuChildVisibilityBeforeIntro[child] =
                    child.gameObject.activeSelf;
            }

            if (child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private GameObject FindMenuRootToHide()
    {
        if (introPanel == null)
        {
            return null;
        }

        Transform parent = introPanel.transform.parent;
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null ||
                child == introPanel.transform)
            {
                continue;
            }

            if (child.name == "SafeAreaRoot")
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private RawImage CreateRuntimeVideoBackground()
    {
        if (introPanel == null)
        {
            return null;
        }

        GameObject backgroundObject = new GameObject(
            "VideoBackground",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter));

        RectTransform rectTransform =
            backgroundObject.GetComponent<RectTransform>();
        rectTransform.SetParent(introPanel.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.SetSiblingIndex(0);

        RawImage rawImage = backgroundObject.GetComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.maskable = true;
        rawImage.enabled = false;

        AspectRatioFitter aspectFitter =
            backgroundObject.GetComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspectFitter.aspectRatio = 0.5625f;

        return rawImage;
    }

    private Image CreateRuntimeVideoDimOverlay()
    {
        if (introPanel == null)
        {
            return null;
        }

        GameObject overlayObject = new GameObject(
            "VideoDimOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform rectTransform =
            overlayObject.GetComponent<RectTransform>();
        rectTransform.SetParent(introPanel.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.SetSiblingIndex(1);

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.raycastTarget = false;
        overlayImage.maskable = true;
        overlayImage.enabled = false;

        return overlayImage;
    }

    private void ApplyVideoDimOverlayStyle()
    {
        if (videoDimOverlay == null)
        {
            return;
        }

        Color overlayColor = Color.black;
        overlayColor.a = Mathf.Clamp01(videoDimAlpha);
        videoDimOverlay.color = overlayColor;
        videoDimOverlay.raycastTarget = false;
    }

    private void ApplyIntroTextReadability()
    {
        ApplyTextReadability(titleText, introTitleColor);
        ApplyTextReadability(bodyText, introBodyColor);
        ApplyTextReadability(pageText, introPageColor);
    }

    private void ApplyIntroRaycastRules()
    {
        if (introPanel == null)
        {
            return;
        }

        Graphic[] graphics =
            introPanel.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            bool isButtonGraphic =
                graphic.GetComponent<Button>() != null;
            graphic.raycastTarget = isButtonGraphic;
        }
    }

    private void ApplyTextReadability(TMP_Text textComponent, Color faceColor)
    {
        if (textComponent == null)
        {
            return;
        }

        textComponent.color = faceColor;
        textComponent.outlineColor = introTextOutlineColor;
        textComponent.outlineWidth = Mathf.Clamp01(introTextOutlineWidth);
        textComponent.raycastTarget = false;
    }

    private void EnsureIntroRenderTexture()
    {
        if (introRenderTexture != null)
        {
            return;
        }

        int width = Mathf.Max(1, runtimeIntroVideoSize.x);
        int height = Mathf.Max(1, runtimeIntroVideoSize.y);

        introRenderTexture = new RenderTexture(width, height, 0)
        {
            name = "RuntimeIntroVideo_RT"
        };
        introRenderTexture.Create();
    }

    private void TryAutoAssignIntroAssetsInEditor()
    {
#if UNITY_EDITOR
        if (introRenderTexture == null)
        {
            introRenderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                "Assets/Intro/Intro_LT.renderTexture");
            if (introRenderTexture == null)
            {
                introRenderTexture =
                    AssetDatabase.LoadAssetAtPath<RenderTexture>(
                        "Assets/Art/Menu/IntroVideo_RT.renderTexture");
            }
        }

        string[] videoPaths =
        {
            "Assets/Intro/Intro_01_HoangCo.mp4",
            "Assets/Intro/Intro_02_HaoKiep.mp4",
            "Assets/Intro/Intro_03_ThienDaoHySinh.mp4",
            "Assets/Intro/Intro_04_TanYThucTinh.mp4"
        };

        if (introVideos == null || introVideos.Length != videoPaths.Length)
        {
            VideoClip[] resizedVideos =
                new VideoClip[videoPaths.Length];
            if (introVideos != null)
            {
                int copyCount =
                    Mathf.Min(introVideos.Length, resizedVideos.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    resizedVideos[i] = introVideos[i];
                }
            }

            introVideos = resizedVideos;
        }

        for (int i = 0; i < videoPaths.Length; i++)
        {
            if (introVideos[i] == null)
            {
                introVideos[i] =
                    AssetDatabase.LoadAssetAtPath<VideoClip>(videoPaths[i]);
            }
        }
#endif
    }
}
