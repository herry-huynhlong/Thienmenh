using UnityEngine;

public class PlayerWalletGrantButton : MonoBehaviour
{
    public PlayerWallet wallet;
    public int amount = 100;
    public WalletGrantSource source = WalletGrantSource.RewardedVideo;

    public void Grant()
    {
        if (wallet == null)
        {
            wallet =
                FindObjectOfType<PlayerWallet>(true);
        }

        if (wallet == null)
        {
            Debug.LogWarning("PlayerWalletGrantButton missing PlayerWallet.");
            return;
        }

        wallet.AddLinhThach(amount, source);

        ShopPanelUI shopPanel =
            FindObjectOfType<ShopPanelUI>(true);

        if (shopPanel != null)
        {
            shopPanel.RefreshMoney();
        }
    }

    public void GrantFromBank()
    {
        source = WalletGrantSource.BankPurchase;
        Grant();
    }

    public void GrantFromRewardedVideo()
    {
        source = WalletGrantSource.RewardedVideo;
        Grant();
    }
}
