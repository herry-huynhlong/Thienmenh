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
            NpcEconomy.FormatCompactAmount(amount);

        return showCurrencyShortName
            ? textValue + " " + NpcEconomy.CurrencyShortName
            : textValue;
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
                FindAnyObjectByType<PlayerWallet>(
                    FindObjectsInactive.Include);
        }
    }
}
