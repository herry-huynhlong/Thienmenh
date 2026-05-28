using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AlchemyRecipe
{
    public StatItemData material;
    public int materialAmount = 1;
    public StatItemData pill;
    public int pillAmount = 1;
}

public class NpcAlchemyAgent : MonoBehaviour
{
    public ItemInventory inventory;
    public bool autoRefine = true;
    public float refineInterval = 12f;
    public List<AlchemyRecipe> recipes =
        new List<AlchemyRecipe>();

    float refineTimer;

    void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }
    }

    void Update()
    {
        if (!autoRefine)
        {
            return;
        }

        refineTimer += Time.deltaTime;

        if (refineTimer < refineInterval)
        {
            return;
        }

        refineTimer = 0f;
        TryRefineAny();
    }

    public bool TryRefineAny()
    {
        if (inventory == null)
        {
            return false;
        }

        foreach (AlchemyRecipe recipe in recipes)
        {
            if (TryRefine(recipe))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryRefine(AlchemyRecipe recipe)
    {
        if (recipe == null ||
            recipe.material == null ||
            recipe.pill == null ||
            recipe.pill.itemType != ItemType.DanDuoc ||
            inventory == null)
        {
            return false;
        }

        int materialAmount =
            Mathf.Max(1, recipe.materialAmount);

        if (inventory.GetAmount(recipe.material) < materialAmount)
        {
            return false;
        }

        if (!inventory.RemoveItem(recipe.material, materialAmount))
        {
            return false;
        }

        int pillAmount =
            Mathf.Max(1, recipe.pillAmount);

        inventory.AddItem(recipe.pill, pillAmount);

        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Refined,
            recipe.material,
            gameObject);

        ItemLifecycleSystem.Notify(
            ItemLifecycleEventType.Created,
            recipe.pill,
            gameObject);

        return true;
    }
}
