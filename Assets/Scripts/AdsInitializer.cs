using UnityEngine;
using UnityEngine.Advertisements;
using System;

public class AdsInitializer : MonoBehaviour, IUnityAdsInitializationListener
{
    public static bool IsInitialized { get; private set; }
    public static event Action OnAdsInitialized;

    [Header("Unity Ads Game ID")]
    [SerializeField] private string androidGameId = "6131599";
    [SerializeField] private string iosGameId = "6131598";

    [Header("Test Mode")]
    [SerializeField] private bool testMode = true;

    private string gameId;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        InitializeAds();
    }

    private void InitializeAds()
    {
#if UNITY_IOS
        gameId = iosGameId;
#elif UNITY_ANDROID
        gameId = androidGameId;
#elif UNITY_EDITOR
        gameId = androidGameId;
#else
        gameId = androidGameId;
#endif

        if (string.IsNullOrEmpty(gameId))
        {
            Debug.LogWarning("Chưa nhập Unity Ads Game ID.");
            return;
        }

        if (!Advertisement.isSupported)
        {
            Debug.LogWarning("Thiết bị hoặc nền tảng hiện tại không hỗ trợ Unity Ads.");
            return;
        }

        if (!Advertisement.isInitialized)
        {
            Advertisement.Initialize(gameId, testMode, this);
        }
        else
        {
            IsInitialized = true;
            OnAdsInitialized?.Invoke();
        }
    }

    public void OnInitializationComplete()
    {
        IsInitialized = true;
        Debug.Log("Unity Ads đã khởi tạo xong.");
        OnAdsInitialized?.Invoke();
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        IsInitialized = false;
        Debug.LogError("Unity Ads khởi tạo lỗi: " + error + " - " + message);
    }
}