using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public StatItemData item;
    public int amount = 1;
    public bool destroyAfterPickup = true;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (item == null)
        {
            return;
        }

        GameObject target =
            other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory != null)
        {
            inventory.AddItem(item, amount);
        }
        else if (!item.ApplyTo(target))
        {
            return;
        }

        if (destroyAfterPickup)
        {
            Destroy(gameObject);
        }
    }
}
