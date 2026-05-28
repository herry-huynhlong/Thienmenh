using UnityEngine;

public class NpcMerchantRole : MonoBehaviour
{
    [Header("Merchant Role")]
    public bool configureTradeAgent = true;
    public bool isMarketTrader = true;
    public bool buyProduceFromVillagers = true;
    public bool buyUsefulItemsFromMarketTrader;
    [Range(0, 100)]
    public int tradeChance = 45;
    public float tradeRadius = 1.5f;
    public float tradeInterval = 4f;

    [Header("Villager Link")]
    public bool forceVillagerJobTrader = true;

    NpcTradeAgent tradeAgent;

    void Awake()
    {
        EnsureMerchantSetup();
    }

    void Start()
    {
        EnsureMerchantSetup();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            EnsureMerchantSetup();
        }
    }

    [ContextMenu("Apply Merchant Setup")]
    public void EnsureMerchantSetup()
    {
        if (!configureTradeAgent)
        {
            return;
        }

        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        tradeAgent = GetComponent<NpcTradeAgent>();
        if (tradeAgent == null)
        {
            tradeAgent = gameObject.AddComponent<NpcTradeAgent>();
        }

        tradeAgent.inventory = inventory;
        tradeAgent.isMarketTrader = isMarketTrader;
        tradeAgent.buyProduceFromVillagers = buyProduceFromVillagers;
        tradeAgent.buyUsefulItemsFromMarketTrader =
            buyUsefulItemsFromMarketTrader;
        tradeAgent.tradeChance = tradeChance;
        tradeAgent.tradeRadius = tradeRadius;
        tradeAgent.tradeInterval = tradeInterval;

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null &&
            forceVillagerJobTrader)
        {
            villager.job = VillagerJob.Trader;
        }

        NpcSpecialProfession profession =
            GetComponent<NpcSpecialProfession>();
        if (profession == null)
        {
            profession = gameObject.AddComponent<NpcSpecialProfession>();
        }

        profession.professionName = "Thuong Nhan";
        profession.lockVillagerJob = forceVillagerJobTrader;
        profession.villagerJob = VillagerJob.Trader;
    }
}
