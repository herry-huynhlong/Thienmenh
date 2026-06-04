using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerWalletTextUI : MonoBehaviour
{
    public PlayerWallet wallet;

    public TMP_Text text;
    public Image currencyIconImage;
    public Sprite currencyIcon;
    public bool showCurrencyShortName = true;

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
        RefreshIcon();

        if (text == null ||
            wallet == null)
        {
            return;
        }

        text.text =
            FormatLinhThach(wallet.LinhThach);
    }

    void Refresh(int amount)
    {
        if (text == null ||
            currencyIconImage == null)
        {
            AutoFindReferences();
        }

        RefreshIcon();

        if (text != null)
        {
            text.text =
                FormatLinhThach(amount);
        }
    }

    string FormatLinhThach(long amount)
    {
        string textValue =
            FormatCompactAmount(amount);

        return showCurrencyShortName
            ? textValue + " " + NpcEconomy.CurrencyShortName
            : textValue;
    }

    string FormatCompactAmount(long amount)
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

    void RefreshIcon()
    {
        if (currencyIconImage == null)
        {
            return;
        }

        if (currencyIcon != null)
        {
            currencyIconImage.sprite = currencyIcon;
        }

        currencyIconImage.enabled =
            currencyIconImage.sprite != null;
    }

    void AutoFindReferences()
    {
        if (text == null)
        {
            text =
                GetComponent<TMP_Text>();
        }

        if (currencyIconImage == null)
        {
            currencyIconImage =
                GetComponent<Image>();
        }

        if (wallet == null)
        {
            wallet =
                FindObjectOfType<PlayerWallet>(true);
        }
    }
}