using UnityEngine;

public class NpcSpecialProfession : MonoBehaviour
{
    public string professionName = "Người đặc biệt";
    [Min(1)] public int jobLevel = 1;
    public bool lockVillagerJob;
    public VillagerJob villagerJob = VillagerJob.None;

    void Start()
    {
        Apply();
    }

    [ContextMenu("Apply Special Profession")]
    public void Apply()
    {
        jobLevel = Mathf.Max(1, jobLevel);

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
