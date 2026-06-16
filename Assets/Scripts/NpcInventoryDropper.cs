using System.Collections.Generic;
using UnityEngine;

public static class NpcInventoryDropper
{
    public static void DropAll(GameObject owner, float radius = 0.55f)
    {
        if (owner == null)
        {
            return;
        }

        ItemInventory inventory =
            owner.GetComponent<ItemInventory>();

        if (inventory == null ||
            inventory.items == null ||
            inventory.items.Count == 0)
        {
            return;
        }

        List<ItemStack> stacks =
            new List<ItemStack>(inventory.items);

        foreach (ItemStack stack in stacks)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            Vector2 offset =
                Random.insideUnitCircle *
                Mathf.Max(0f, radius);

            Vector3 position =
                owner.transform.position +
                new Vector3(offset.x, offset.y, 0f);

            DropItem(position, stack.item, stack.amount);
        }

        inventory.items.Clear();
        inventory.MarkDirty();
    }

    static void DropItem(
        Vector3 position,
        StatItemData item,
        int amount)
    {
        if (item == null ||
            amount <= 0)
        {
            return;
        }

        if (HeavenSystem.Instance != null)
        {
            HeavenSystem.Instance.DropItemAt(position, item, amount);
            return;
        }

        GameObject itemObject =
            new GameObject("Dropped Item - " + item.itemName);

        itemObject.transform.position = position;

        WorldStatItemPickup pickup =
            itemObject.AddComponent<WorldStatItemPickup>();

        pickup.item = item;
        pickup.amount = Mathf.Max(1, amount);
        pickup.allowNpcPickup = true;
        pickup.allowPlayerPickup = false;

        CircleCollider2D collider =
            itemObject.AddComponent<CircleCollider2D>();

        collider.isTrigger = true;
        collider.radius = 0.25f;

        PickupVisualUtility.ApplySprite(itemObject, item.icon, 20);
    }
}
