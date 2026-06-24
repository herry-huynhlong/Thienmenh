using UnityEngine;
using UnityEngine.Video;

public class MenuThunderSync : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public AudioSource thunderSource;
    public AudioClip thunderClip;

    [Header("Thunder Times In Video")]
    public float[] thunderTimes =
    {
        1.65f,
        4.55f,
        7.05f,
        8.85f
    };

    [Header("Settings")]
    public float thunderDelay = 0f;
    public float volume = 0.85f;

    private int nextThunderIndex = 0;
    private double lastVideoTime = 0;

    private void Update()
    {
        if (videoPlayer == null || thunderSource == null || thunderClip == null)
        {
            return;
        }

        if (!videoPlayer.isPlaying)
        {
            return;
        }

        double currentTime = videoPlayer.time;

        // Khi video loop lại từ cuối về đầu
        if (currentTime < lastVideoTime)
        {
            nextThunderIndex = 0;
        }

        lastVideoTime = currentTime;

        if (nextThunderIndex >= thunderTimes.Length)
        {
            return;
        }

        float targetTime = thunderTimes[nextThunderIndex] + thunderDelay;

        if (currentTime >= targetTime)
        {
            thunderSource.PlayOneShot(thunderClip, volume);
            nextThunderIndex++;
        }
    }
}