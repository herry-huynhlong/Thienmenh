using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Advertisements;
using TMPro;

public class RewardedLTAdsButton : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
{
    [Header("UI")]
    [SerializeField] private Button watchAdButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Wallet")]
    [SerializeField] private PlayerWallet playerWallet;

    [Header("Reward")]
    [SerializeField] private int rewardLinhThach = 500;

    [Header("Unity Ads Ad Unit ID")]
    [SerializeField] private string androidAdUnitId = "Rewarded_Android";
    [SerializeField] private string iosAdUnitId = "Rewarded_iOS";

    private string adUnitId;
    private bool adLoaded;

    private void Awake()
    {
#if UNITY_IOS
        adUnitId = iosAdUnitId;
#elif UNITY_ANDROID
        adUnitId = androidAdUnitId;
#elif UNITY_EDITOR
        adUnitId = androidAdUnitId;
#else
        adUnitId = androidAdUnitId;
#endif

        if (watchAdButton == null)
            watchAdButton = GetComponent<Button>();

        if (playerWallet == null)
            playerWallet = FindAnyObjectByType<PlayerWallet>();

        if (watchAdButton != null)
        {
            watchAdButton.interactable = false;
            watchAdButton.onClick.RemoveListener(ShowAd);
            watchAdButton.onClick.AddListener(ShowAd);
        }
        SetStatusByKey("loading");
    }

    private void OnEnable()
    {
        AdsInitializer.OnAdsInitialized += LoadAd;

        if (AdsInitializer.IsInitialized)
            LoadAd();
    }

    private void OnDisable()
    {
        AdsInitializer.OnAdsInitialized -= LoadAd;
    }

    private void OnDestroy()
    {
        if (watchAdButton != null)
            watchAdButton.onClick.RemoveListener(ShowAd);
    }

    public void LoadAd()
    {
        if (string.IsNullOrEmpty(adUnitId))
        {
            SetStatusByKey("missingAdUnit");
            return;
        }

        adLoaded = false;

        if (watchAdButton != null)
            watchAdButton.interactable = false;
        SetStatusByKey("loading");
        Advertisement.Load(adUnitId, this);
    }

    public void ShowAd()
    {
        if (!adLoaded)
        {
            SetStatusByKey("notReady");
            return;
        }

        adLoaded = false;

        if (watchAdButton != null)
            watchAdButton.interactable = false;
        SetStatusByKey("opening");
        Advertisement.Show(adUnitId, this);
    }

    public void OnUnityAdsAdLoaded(string loadedAdUnitId)
    {
        if (loadedAdUnitId != adUnitId)
            return;

        adLoaded = true;

        if (watchAdButton != null)
            watchAdButton.interactable = true;

        SetStatus(UiText.Format("rewardedAds", "readyRewardFormat", rewardLinhThach));
        Debug.Log("Rewarded Ads đã tải xong: " + loadedAdUnitId);
    }

    public void OnUnityAdsFailedToLoad(string failedAdUnitId, UnityAdsLoadError error, string message)
    {
        if (failedAdUnitId != adUnitId)
            return;

        adLoaded = false;

        if (watchAdButton != null)
            watchAdButton.interactable = false;
        SetStatusByKey("loadFailed");
        Debug.LogWarning("Load Ads lỗi: " + error + " - " + message);
    }

    public void OnUnityAdsShowStart(string shownAdUnitId)
    {
        SetStatusByKey("watching");
    }

    public void OnUnityAdsShowClick(string shownAdUnitId)
    {
    }

    public void OnUnityAdsShowFailure(string failedAdUnitId, UnityAdsShowError error, string message)
    {
        if (failedAdUnitId != adUnitId)
            return;
        SetStatusByKey("showFailed");
        Debug.LogWarning("Show Ads lỗi: " + error + " - " + message);

        LoadAd();
    }

    public void OnUnityAdsShowComplete(string shownAdUnitId, UnityAdsShowCompletionState showCompletionState)
    {
        if (shownAdUnitId != adUnitId)
            return;

        if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            GiveReward();
        }
        else
        {
            SetStatusByKey("incomplete");
            Debug.Log("Người chơi chưa xem xong quảng cáo, không cộng LT.");
        }

        LoadAd();
    }

    private void GiveReward()
    {
        if (playerWallet == null)
            playerWallet = FindAnyObjectByType<PlayerWallet>();

        if (playerWallet != null)
        {
            playerWallet.AddRewardedVideoCurrency(rewardLinhThach);

            SetStatus(UiText.Format("rewardedAds", "rewardGrantedFormat", rewardLinhThach));
            Debug.Log("Người chơi xem quảng cáo xong, nhận +" + rewardLinhThach + " LT.");
        }
        else
        {
            SetStatusByKey("walletMissing");
            Debug.LogWarning("Không tìm thấy PlayerWallet nên chưa cộng được Linh Thạch.");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void SetStatusByKey(string key)
    {
        SetStatus(UiText.Get("rewardedAds", key));
    }
}
