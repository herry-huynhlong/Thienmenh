using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class MainMenuBackgroundVideoController : MonoBehaviour
{
    const float RevealFailSafeSeconds = 8f;

    public VideoPlayer videoPlayer;
    public RawImage videoBackground;
    public Color loadingClearColor = Color.black;

    RenderTexture targetTexture;
    bool isRevealReady;
    bool hasReceivedVideoFrame;
    float revealWaitStartTime;

    public bool IsRevealReady
    {
        get { return isRevealReady; }
    }

    void Awake()
    {
        TryBindReferences();
        PrepareAndPlayEarly();
    }

    void OnEnable()
    {
        TryBindReferences();
        PrepareAndPlayEarly();
    }

    void OnDisable()
    {
        UnsubscribeVideoEvents();
    }

    void Update()
    {
        if (isRevealReady)
        {
            return;
        }

        if (videoPlayer == null || videoBackground == null)
        {
            if (HasWaitTimedOut())
            {
                isRevealReady = true;
            }

            return;
        }

        if (videoPlayer.isPrepared && !videoPlayer.isPlaying)
        {
            StartPlayback();
        }

        if (hasReceivedVideoFrame)
        {
            isRevealReady = true;
            return;
        }

        if (HasWaitTimedOut())
        {
            Debug.LogWarning(
                "[MainMenuBackgroundVideoController] Reveal fallback triggered before video reached ready threshold.");
            isRevealReady = true;
        }
    }

    void TryBindReferences()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (videoBackground == null)
        {
            GameObject backgroundObject =
                GameObject.Find("MenuVideoBackground");
            if (backgroundObject != null)
            {
                videoBackground =
                    backgroundObject.GetComponent<RawImage>();
            }
        }
    }

    void PrepareAndPlayEarly()
    {
        if (videoPlayer == null || videoBackground == null)
        {
            isRevealReady = false;
            return;
        }

        targetTexture = videoPlayer.targetTexture;
        isRevealReady = false;
        hasReceivedVideoFrame = false;
        revealWaitStartTime = Time.unscaledTime;

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.sendFrameReadyEvents = true;
        if (targetTexture != null)
        {
            videoBackground.texture = targetTexture;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = targetTexture;
            GL.Clear(true, true, loadingClearColor);
            RenderTexture.active = previous;
        }

        videoBackground.enabled = true;

        SubscribeVideoEvents();

        if (videoPlayer.isPrepared)
        {
            StartPlayback();
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        videoPlayer.time = 0d;
        videoPlayer.Prepare();
    }

    void SubscribeVideoEvents()
    {
        if (videoPlayer == null)
        {
            return;
        }

        UnsubscribeVideoEvents();
        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.frameReady += OnFrameReady;
        videoPlayer.errorReceived += OnErrorReceived;
    }

    void UnsubscribeVideoEvents()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.prepareCompleted -= OnPrepareCompleted;
        videoPlayer.frameReady -= OnFrameReady;
        videoPlayer.errorReceived -= OnErrorReceived;
    }

    void OnPrepareCompleted(VideoPlayer source)
    {
        if (source != videoPlayer)
        {
            return;
        }

        StartPlayback();
    }

    void OnFrameReady(VideoPlayer source, long frameIndex)
    {
        if (source != videoPlayer)
        {
            return;
        }

        if (frameIndex >= 0)
        {
            hasReceivedVideoFrame = true;
        }
    }

    void OnErrorReceived(VideoPlayer source, string message)
    {
        Debug.LogWarning(
            "[MainMenuBackgroundVideoController] Video error: " + message);
        isRevealReady = true;
    }

    void StartPlayback()
    {
        if (videoPlayer == null)
        {
            isRevealReady = true;
            return;
        }

        if (!videoPlayer.isPlaying)
        {
            videoPlayer.time = 0d;
            videoPlayer.Play();
        }
    }

    bool HasWaitTimedOut()
    {
        return Time.unscaledTime - revealWaitStartTime >=
            RevealFailSafeSeconds;
    }
}
