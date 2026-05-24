using System.Collections.Generic;
using UnityEngine;

public class NpcItemCollector : MonoBehaviour
{
    [Header("Inventory")]
    public ItemInventory inventory;
    public CharacterStats characterStats;

    [Header("Pickup")]
    public bool canPickupItems = true;
    public bool autoUsePickedItems = true;
    public LayerMask pickupLayers = ~0;
    public float pickupRadius = 0.45f;
    public float scanInterval = 0.5f;

    readonly List<StatItemData> equippedItems =
        new List<StatItemData>();

    float scanTimer;

    void Awake()
    {
        AutoFindReferences();
    }

    void Update()
    {
        if (!canPickupItems)
        {
            return;
        }

        scanTimer += Time.deltaTime;

        if (scanTimer < scanInterval)
        {
            return;
        }

        scanTimer = 0f;
        ScanForNearbyPickup();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canPickupItems)
        {
            return;
        }

        TryPickup(other.GetComponentInParent<WorldStatItemPickup>());
    }

    void ScanForNearbyPickup()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                pickupRadius,
                pickupLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (TryPickup(
                    hit.GetComponentInParent<WorldStatItemPickup>()))
            {
                return;
            }
        }
    }

    bool TryPickup(WorldStatItemPickup pickup)
    {
        AutoFindReferences();

        if (pickup == null ||
            !pickup.allowNpcPickup ||
            pickup.item == null ||
            inventory == null ||
            !pickup.TryTake(1))
        {
            return false;
        }

        inventory.AddItem(pickup.item, 1);

        if (autoUsePickedItems)
        {
            TryUsePickedItem(pickup.item);
        }

        return true;
    }

    void TryUsePickedItem(StatItemData item)
    {
        if (item == null ||
            !item.CanUseOn(gameObject))
        {
            return;
        }

        bool applied =
            item.ApplyTo(gameObject);

        if (!applied)
        {
            return;
        }

        if (item.consumeOnUse)
        {
            inventory.RemoveItem(item, 1);
            return;
        }

        equippedItems.Add(item);
    }

    public bool UnequipForSale(StatItemData item)
    {
        if (item == null ||
            !equippedItems.Remove(item))
        {
            return false;
        }

        item.RemoveFrom(gameObject);
        return true;
    }

    void AutoFindReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();

            if (inventory == null)
            {
                inventory = gameObject.AddComponent<ItemInventory>();
                inventory.shareRuntimeItems = false;
            }
        }

        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
