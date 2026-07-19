using UnityEngine;

public partial class VillagerAI
{
    public void ThinkTrader()
    {
        if (TryHandleTraderImmediateNeeds())
        {
            return;
        }

        TryTradeOrTaskOrIdle();
    }

    public bool TryRunMarketRoleThink()
    {
        ThinkTrader();
        return true;
    }

    public void RunMarketRoleTrade()
    {
        GoTrade();
    }

    public void RunMarketRoleSellGoods()
    {
        GoSellGoods();
    }

    public void RunMarketRoleBuyGoods()
    {
        GoBuyGoods();
    }

    public void RunMarketRoleTaskProviderVisit()
    {
        TryScheduledTaskOrWait();
    }
}
