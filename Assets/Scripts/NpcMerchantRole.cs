using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ItemInventory))]
[RequireComponent(typeof(NpcTradeAgent))]
[RequireComponent(typeof(NpcSpecialProfession))]
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
    bool setupQueued;

    void Awake()
    {
    }

    void Start()
    {
        QueueEnsureMerchantSetup();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            QueueEnsureMerchantSetup();
        }
    }

    void QueueEnsureMerchantSetup()
    {
        if (!Application.isPlaying ||
            !isActiveAndEnabled ||
            !gameObject.activeInHierarchy)
        {
            setupQueued = false;
            return;
        }

        if (setupQueued)
        {
            return;
        }

        setupQueued = true;
        StartCoroutine(EnsureMerchantSetupNextFrame());
    }

    IEnumerator EnsureMerchantSetupNextFrame()
    {
        yield return null;
        setupQueued = false;
        EnsureMerchantSetup();
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
            return;
        }

        tradeAgent = GetComponent<NpcTradeAgent>();
        if (tradeAgent == null)
        {
            return;
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
            return;
        }

        profession.professionName = "Thương Nhân";
        profession.lockVillagerJob = forceVillagerJobTrader;
        profession.villagerJob = VillagerJob.Trader;
    }
}
