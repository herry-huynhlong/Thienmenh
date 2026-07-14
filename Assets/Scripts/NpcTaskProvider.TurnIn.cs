using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Objective turn-in, stored task goods and daily receiver transfer.
public partial class NpcTaskProvider
{
    bool ConsumeTaskItems(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null ||
            GetTaskRequiredItem(task) == null ||
            !task.offer.consumeRequiredItemsOnTurnIn)
        {
            return true;
        }

        ItemInventory inventory = task.npc != null
            ? task.npc.GetComponent<ItemInventory>()
            : null;

        if (inventory == null ||
            inventory.GetAmount(GetTaskRequiredItem(task)) < GetRequiredAmount(task))
        {
            return false;
        }

        StatItemData item = GetTaskRequiredItem(task);
        int amount = GetRequiredAmount(task);

        if (!inventory.RemoveItem(item, amount))
        {
            return false;
        }

        StoreTurnedInTaskGoods(item, amount);
        return true;
    }

    void StoreTurnedInTaskGoods(StatItemData item, int amount)
    {
        if (!storeTurnedInTaskGoods ||
            item == null ||
            amount <= 0)
        {
            return;
        }

        EnsureProviderInventory();

        if (inventory != null)
        {
            inventory.AddItem(item, amount);
        }

        PendingTaskGoods pending =
            pendingTaskGoods.Find(entry => entry != null && entry.item == item);

        if (pending == null)
        {
            pending = new PendingTaskGoods { item = item };
            pendingTaskGoods.Add(pending);
        }

        pending.amount += amount;
    }

    void UpdateTaskGoodsDailyTransfer()
    {
        if (!transferTaskGoodsToCounterAtDayEnd)
        {
            return;
        }

        int currentDay = GetCurrentWorldDay();
        if (currentDay < 0)
        {
            return;
        }

        if (lastTaskGoodsTransferDay < 0)
        {
            lastTaskGoodsTransferDay = currentDay;
            return;
        }

        if (currentDay == lastTaskGoodsTransferDay)
        {
            return;
        }

        TransferTaskGoodsToReceiver();
        lastTaskGoodsTransferDay = currentDay;
    }

    int GetCurrentWorldDay()
    {
        return WorldTimeSystem.Instance != null
            ? WorldTimeSystem.Instance.CurrentDay
            : -1;
    }

    void TransferTaskGoodsToReceiver()
    {
        if (pendingTaskGoods.Count == 0)
        {
            return;
        }

        NpcCounterBroker receiver = taskGoodsReceiver;
        if (receiver == null && useActiveCounterBrokerIfReceiverMissing)
        {
            receiver = NpcCounterBroker.Active;
        }

        if (receiver == null)
        {
            return;
        }

        ItemInventory receiverInventory = receiver.inventory;
        if (receiverInventory == null)
        {
            receiverInventory = receiver.GetComponent<ItemInventory>();
        }

        if (receiverInventory == null)
        {
            receiverInventory = receiver.gameObject.AddComponent<ItemInventory>();
            receiverInventory.shareRuntimeItems = false;
        }

        EnsureProviderInventory();

        for (int i = pendingTaskGoods.Count - 1; i >= 0; i--)
        {
            PendingTaskGoods pending = pendingTaskGoods[i];
            if (pending == null ||
                pending.item == null ||
                pending.amount <= 0)
            {
                pendingTaskGoods.RemoveAt(i);
                continue;
            }

            int transferAmount = pending.amount;
            if (inventory != null)
            {
                transferAmount = Mathf.Min(
                    transferAmount,
                    inventory.GetAmount(pending.item));
            }

            if (transferAmount <= 0)
            {
                pendingTaskGoods.RemoveAt(i);
                continue;
            }

            if (inventory != null)
            {
                inventory.RemoveItem(pending.item, transferAmount);
            }

            receiverInventory.AddItem(pending.item, transferAmount);
            pending.amount -= transferAmount;

            if (pending.amount <= 0)
            {
                pendingTaskGoods.RemoveAt(i);
            }
        }
    }

}
