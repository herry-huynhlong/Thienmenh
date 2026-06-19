using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ItemInventory))]
public class NpcSharedInventoryLink : MonoBehaviour
{
    [Header("Shared Inventory")]
    public bool enableSharedInventory = true;
    public string sharedInventoryKey = "VillageMarketShared";
    public bool clearSharedInventoryWhenBinding;

    ItemInventory inventory;

    void Awake()
    {
        ApplySharedInventoryBinding();
    }

    void OnEnable()
    {
        ApplySharedInventoryBinding();
    }

    void Start()
    {
        ApplySharedInventoryBinding();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplySharedInventoryBinding();
        }
    }
#endif

    [ContextMenu("Apply Shared Inventory Binding")]
    public void ApplySharedInventoryBinding()
    {
        if (!enableSharedInventory)
        {
            return;
        }

        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }

        if (inventory == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sharedInventoryKey))
        {
            sharedInventoryKey = "VillageMarketShared";
        }

        inventory.UseSharedRuntimeItems(
            sharedInventoryKey,
            clearSharedInventoryWhenBinding);
    }
}
