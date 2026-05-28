using UnityEngine;

public class VillageNpcSetupTool : MonoBehaviour
{
    public bool includeInactive = true;

    [ContextMenu("Reload All Child Villagers")]
    public void ReloadAllChildVillagers()
    {
        VillagerAI[] villagers =
            GetComponentsInChildren<VillagerAI>(includeInactive);

        foreach (VillagerAI villager in villagers)
        {
            if (villager == null)
            {
                continue;
            }

            villager.ReloadGeneratedProfile();
            EnsureInventoryIsUnique(villager.gameObject);
        }
    }

    [ContextMenu("Reload All Child Smart NPCs")]
    public void ReloadAllChildSmartNpcs()
    {
        SmartNpcAI[] npcs =
            GetComponentsInChildren<SmartNpcAI>(includeInactive);

        foreach (SmartNpcAI npc in npcs)
        {
            if (npc == null)
            {
                continue;
            }

            npc.ReloadGeneratedProfile();
            EnsureInventoryIsUnique(npc.gameObject);
        }
    }

    void EnsureInventoryIsUnique(GameObject target)
    {
        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            return;
        }

        inventory.shareRuntimeItems = false;

        if (string.IsNullOrEmpty(inventory.runtimeKey))
        {
            inventory.runtimeKey = target.name;
        }
    }

    [ContextMenu("Scatter Child Villagers Around Parent")]
    public void ScatterChildVillagersAroundParent()
    {
        VillagerAI[] villagers =
            GetComponentsInChildren<VillagerAI>(includeInactive);

        float radius = 3f;

        for (int i = 0; i < villagers.Length; i++)
        {
            VillagerAI villager =
                villagers[i];

            if (villager == null)
            {
                continue;
            }

            float angle =
                villagers.Length <= 0
                ? 0f
                : Mathf.PI * 2f * i / villagers.Length;

            Vector3 offset =
                new Vector3(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle),
                    0f) * radius;

            villager.transform.position =
                transform.position + offset;
        }
    }
}
