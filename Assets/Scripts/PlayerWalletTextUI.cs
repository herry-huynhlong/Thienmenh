using TMPro;
using UnityEngine;

public class PlayerWalletTextUI : MonoBehaviour
{
    public PlayerWallet wallet;
    public TMP_Text text;
    public string prefix = "Linh Thach: ";
    public string suffix = " LT";

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
            prefix +
            wallet.LinhThach +
            suffix;
    }

    void Refresh(int amount)
    {
        if (text == null)
        {
            AutoFindReferences();
        }

        if (text != null)
        {
            text.text = prefix + amount + suffix;
        }
    }

    void AutoFindReferences()
    {
        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }

        if (wallet == null)
        {
            wallet =
                FindObjectOfType<PlayerWallet>(true);
        }
    }
}
