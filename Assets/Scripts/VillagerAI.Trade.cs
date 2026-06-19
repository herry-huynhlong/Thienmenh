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
}
