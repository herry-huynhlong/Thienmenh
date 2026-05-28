using UnityEngine;

public class NpcSpecialProfession : MonoBehaviour
{
    public string professionName = "Nguoi dac biet";
    public bool lockVillagerJob;
    public VillagerJob villagerJob = VillagerJob.None;

    void Start()
    {
        Apply();
    }

    [ContextMenu("Apply Special Profession")]
    public void Apply()
    {
        if (!lockVillagerJob)
        {
            return;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.job = villagerJob;
        }
    }
}
