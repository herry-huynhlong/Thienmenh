using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(ItemInventory))]
[RequireComponent(typeof(NpcTradeAgent))]
[RequireComponent(typeof(NpcSpecialProfession))]
[RequireComponent(typeof(NpcAlchemyAgent))]
public class NpcAlchemyRole : MonoBehaviour
{
    [Header("Alchemy Role")]
    public bool configureTradeAgent = true;
    public bool configureAlchemyAgent = true;
    public bool autoBuyMaterialsFromMarketTraders = true;
    public bool autoAlchemy = true;
    public bool preferCultivateWhenIdle = true;
    public bool sellToNearbyNpcBuyers = true;
    public bool sellToVanBaoLau = true;
    public Transform alchemyStandPoint;
    public Transform alchemyFacingPoint;
    public Vector2 defaultFacingDirection = Vector2.down;
    public bool snapToAlchemyStandPoint = false;
    public bool keepAtAlchemyStandPoint = false;
    public bool lockFacingToAlchemyPoint = true;

    [Header("Profession")]
    [FormerlySerializedAs("forceVillagerJobWorker")]
    public bool forceVillagerJobProfession = true;
    public string professionName = "Luyen Dan Su";

    NpcTradeAgent tradeAgent;
    NpcAlchemyAgent alchemyAgent;
    bool setupQueued;

    void Start()
    {
        QueueEnsureAlchemySetup();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            QueueEnsureAlchemySetup();
        }
    }

    void QueueEnsureAlchemySetup()
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
        StartCoroutine(EnsureAlchemySetupNextFrame());
    }

    System.Collections.IEnumerator EnsureAlchemySetupNextFrame()
    {
        yield return null;
        setupQueued = false;
        EnsureAlchemySetup();
    }

    [ContextMenu("Apply Alchemy Setup")]
    public void EnsureAlchemySetup()
    {
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            return;
        }

        tradeAgent = GetComponent<NpcTradeAgent>();
        alchemyAgent = GetComponent<NpcAlchemyAgent>();
        if (tradeAgent == null || alchemyAgent == null)
        {
            return;
        }

        if (configureTradeAgent)
        {
            tradeAgent.inventory = inventory;
            tradeAgent.buyUsefulItemsFromMarketTrader = autoBuyMaterialsFromMarketTraders;
        }

        if (configureAlchemyAgent)
        {
            snapToAlchemyStandPoint = false;
            keepAtAlchemyStandPoint = false;

            alchemyAgent.inventory = inventory;
            alchemyAgent.tradeAgent = tradeAgent;
            alchemyAgent.autoAlchemy = autoAlchemy;
            alchemyAgent.preferCultivateWhenIdle = preferCultivateWhenIdle;
            alchemyAgent.autoSellFinishedGoods = true;
            alchemyAgent.sellToNearbyNpcBuyers = sellToNearbyNpcBuyers;
            alchemyAgent.sellToVanBaoLau = sellToVanBaoLau;
            alchemyAgent.alchemyStandPoint = alchemyStandPoint;
            alchemyAgent.alchemyFacingPoint = alchemyFacingPoint;
            alchemyAgent.defaultFacingDirection = defaultFacingDirection;
            alchemyAgent.snapToAlchemyStandPoint = snapToAlchemyStandPoint;
            alchemyAgent.keepAtAlchemyStandPoint = keepAtAlchemyStandPoint;
            alchemyAgent.lockFacingToAlchemyPoint = lockFacingToAlchemyPoint;
            alchemyAgent.autoBuyMaterialsFromMarketTraders =
                autoBuyMaterialsFromMarketTraders;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null && forceVillagerJobProfession)
        {
            villager.job = VillagerJob.Alchemist;
        }

        NpcSpecialProfession profession = GetComponent<NpcSpecialProfession>();
        if (profession != null)
        {
            profession.professionName = professionName;
            profession.lockVillagerJob = forceVillagerJobProfession;
            profession.villagerJob = VillagerJob.Alchemist;
        }
    }
}
