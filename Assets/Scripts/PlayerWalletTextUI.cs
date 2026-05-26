using TMPro;
using UnityEngine;

public class PlayerWalletTextUI : MonoBehaviour
{
    public PlayerWallet wallet;

    public TMP_Text text;

    void Awake()
    {
        AutoFindReferences();

        Refresh();
    }

    void OnEnable()
    {
        PlayerWallet.OnAnyWalletChanged += Refresh;

        AutoFindReferences();

        Refresh();
    }

    void OnDisable()
    {
        PlayerWallet.OnAnyWalletChanged -= Refresh;
    }

    public void Refresh()
    {
        AutoFindReferences();

        if (text == null ||
            wallet == null)
        {
            return;
        }

        text.text =
            FormatMoney(wallet.LinhThach);
    }

    void Refresh(int amount)
    {
        if (text == null)
        {
            AutoFindReferences();
        }

        if (text != null)
        {
            text.text =
                FormatMoney(amount);
        }
    }

    string FormatMoney(long amount)
    {
        if (amount >= 1000000000000)
        {
            return
                (amount / 1000000000000f)
                .ToString("0.#") + "T";
        }

        if (amount >= 1000000000)
        {
            return
                (amount / 1000000000f)
                .ToString("0.#") + "B";
        }

        if (amount >= 1000000)
        {
            return
                (amount / 1000000f)
                .ToString("0.#") + "M";
        }

        if (amount >= 1000)
        {
            return
                (amount / 1000f)
                .ToString("0.#") + "K";
        }

        return amount.ToString();
    }

    void AutoFindReferences()
    {
        if (text == null)
        {
            text =
                GetComponent<TMP_Text>();
        }

        if (wallet == null)
        {
            wallet =
                FindObjectOfType<PlayerWallet>(true);
        }
    }
}