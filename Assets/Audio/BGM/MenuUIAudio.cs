using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class MenuUIAudio : MonoBehaviour
{
    [Header("UI Sounds")]
    [SerializeField] private AudioClip clickClip;

    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 0.7f;

    private AudioSource audioSource;

    private static MenuUIAudio instance;

    private void Awake()
    {
        // Không tạo trùng Audio Manager khi chuyển Scene
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    public void PlayClick()
    {
        if (audioSource == null || clickClip == null)
            return;

        audioSource.PlayOneShot(clickClip, clickVolume);
    }
}