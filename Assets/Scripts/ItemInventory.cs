using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemStack
{
    public StatItemData item;
    public int amount = 1;
}

public class ItemInventory : MonoBehaviour
{
    public List<ItemStack> items =
        new List<ItemStack>();

    public void AddItem(StatItemData item, int amount = 1)
    {
        if (item == null ||
            amount <= 0)
        {
            return;
        }

        ItemStack stack =
            items.Find(entry => entry.item == item);

        if (stack != null)
        {
            stack.amount += amount;
            return;
        }

        items.Add(
            new ItemStack
            {
                item = item,
                amount = amount
            });
    }

    public bool UseItemOn(int itemIndex, GameObject target)
    {
        if (target == null ||
            itemIndex < 0 ||
            itemIndex >= items.Count)
        {
            return false;
        }

        ItemStack stack =
            items[itemIndex];

        if (stack == null ||
            stack.item == null ||
            stack.amount <= 0)
        {
            return false;
        }

        if (!stack.item.ApplyTo(target))
        {
            return false;
        }

        if (stack.item.consumeOnUse)
        {
            stack.amount -= 1;

            if (stack.amount <= 0)
            {
                items.RemoveAt(itemIndex);
            }
        }

        return true;
    }
}
