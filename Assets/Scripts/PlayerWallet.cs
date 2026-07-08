using UnityEngine;
using System;

public enum WalletGrantSource
{
    BankPurchase,
    RewardedVideo,
    Admin,
    Quest,
    Other
}

public class PlayerWallet : MonoBehaviour
{
    const string WalletSaveKey = "ThienMenh.Save.PlayerWallet.LinhThach";

    static int sharedMoney;
    static bool hasSharedMoney;
    public static event Action<int> OnAnyWalletChanged;

    [Header("Currency")]
    [InspectorName("Linh Thạch")]
    public int money = 1000;
    public string currencyName = "Linh Thạch";
    public bool saveWallet = true;

    public event Action<int> OnChanged;

    void Awake()
    {
        InitializeSharedMoney();
    }

    public int LinhThach
    {
        get
        {
            InitializeSharedMoney();
            return sharedMoney;
        }
    }

    public bool CanPay(int amount)
    {
        InitializeSharedMoney();

        return amount >= 0 &&
            sharedMoney >= amount;
    }

    public bool Pay(int amount)
    {
        if (!CanPay(amount))
        {
            return false;
        }

        sharedMoney -= amount;
        money = sharedMoney;
        Save();
        NotifyChanged();
        return true;
    }

    public void AddMoney(int amount)
    {
        AddLinhThach(amount, WalletGrantSource.Other);
    }

    public void AddLinhThach(int amount)
    {
        AddLinhThach(amount, WalletGrantSource.Other);
    }

    public void AddPurchasedCurrency(int amount)
    {
        AddLinhThach(amount, WalletGrantSource.BankPurchase);
    }

    public void AddRewardedVideoCurrency(int amount)
    {
        AddLinhThach(amount, WalletGrantSource.RewardedVideo);
    }

    public void AddLinhThach(
        int amount,
        WalletGrantSource source)
    {
        InitializeSharedMoney();

        sharedMoney += amount;

        if (sharedMoney < 0)
        {
            sharedMoney = 0;
        }

        money = sharedMoney;

        Save();
        NotifyChanged();
    }

    public void Save()
    {
        if (!saveWallet)
        {
            return;
        }

        InitializeSharedMoney();

        PlayerPrefs.SetInt(WalletSaveKey, sharedMoney);
        GameSaveSystem.MarkSaveExists();
        GameSaveSystem.QueuePendingCommit();
    }

    public void Load()
    {
        if (!saveWallet ||
            !GameSaveSystem.HasSave ||
            !PlayerPrefs.HasKey(WalletSaveKey))
        {
            return;
        }

        money =
            Mathf.Max(0, PlayerPrefs.GetInt(WalletSaveKey, money));

        sharedMoney = money;
        hasSharedMoney = true;
    }

    public static void ClearSave()
    {
        PlayerPrefs.DeleteKey(WalletSaveKey);
        ResetRuntime();
    }

    public static void ResetRuntime()
    {
        sharedMoney = 0;
        hasSharedMoney = false;
    }

    void NotifyChanged()
    {
        OnChanged?.Invoke(sharedMoney);
        OnAnyWalletChanged?.Invoke(sharedMoney);
    }

    void InitializeSharedMoney()
    {
        if (hasSharedMoney)
        {
            money = sharedMoney;
            return;
        }

        if (saveWallet &&
            GameSaveSystem.HasSave &&
            PlayerPrefs.HasKey(WalletSaveKey))
        {
            money =
                Mathf.Max(0, PlayerPrefs.GetInt(WalletSaveKey, money));
        }
        else
        {
            money = Mathf.Max(0, money);
        }

        sharedMoney = money;
        hasSharedMoney = true;
    }
}
