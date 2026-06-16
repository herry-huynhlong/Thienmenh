using UnityEngine;

[RequireComponent(typeof(ItemInventory))]
[RequireComponent(typeof(NpcTradeAgent))]
[RequireComponent(typeof(NpcSpecialProfession))]
[RequireComponent(typeof(NpcForgeAgent))]
public class NpcForgeRole : MonoBehaviour
{
    [Header("Forge Role")]
    public bool configureTradeAgent = true;
    public bool configureForgeAgent = true;
    public bool autoBuyMaterialsFromMarketTraders = true;
    public bool autoForge = true;
    public bool preferCultivateWhenIdle = true;
    public bool sellToNearbyNpcBuyers = true;
    public bool sellToVanBaoLau = true;
    [Header("Daily Routine")]
    [Min(0f)] public float forgeDailyHours = 6f;
    [Min(0f)] public float cultivateDailyHours = 10f;
    public Transform forgeStandPoint;
    public Transform marketPoint;
    public Transform forgeFacingPoint;
    public Vector2 defaultFacingDirection = Vector2.down;
    public bool snapToForgeStandPoint = false;
    public bool keepAtForgeStandPoint = false;
    public bool lockFacingToForgePoint = true;

    [Header("Trade")]
    [Range(0, 100)]
    public int tradeChance = 45;
    public float tradeRadius = 1.6f;
    public float tradeInterval = 4f;
    [Range(0f, 1f)]
    public float maxMoneySpendRatio = 0.8f;

    [Header("Profession")]
    public bool forceVillagerJobWorker = true;
    public string professionName = "Lo Ren";

    NpcTradeAgent tradeAgent;
    NpcForgeAgent forgeAgent;
    bool setupQueued;

    void Start()
    {
        QueueEnsureForgeSetup();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            QueueEnsureForgeSetup();
        }
    }

    void QueueEnsureForgeSetup()
    {
        if (setupQueued)
        {
            return;
        }

        setupQueued = true;
        StartCoroutine(EnsureForgeSetupNextFrame());
    }

    System.Collections.IEnumerator EnsureForgeSetupNextFrame()
    {
        yield return null;
        setupQueued = false;
        EnsureForgeSetup();
    }

    [ContextMenu("Apply Forge Setup")]
    public void EnsureForgeSetup()
    {
        ItemInventory inventory = GetComponent<ItemInventory>();
        if (inventory == null)
        {
            return;
        }

        tradeAgent = GetComponent<NpcTradeAgent>();
        forgeAgent = GetComponent<NpcForgeAgent>();
        if (tradeAgent == null || forgeAgent == null)
        {
            return;
        }

        if (configureTradeAgent)
        {
            tradeAgent.inventory = inventory;
            tradeAgent.tradeChance = tradeChance;
            tradeAgent.tradeRadius = tradeRadius;
            tradeAgent.tradeInterval = tradeInterval;
            tradeAgent.maxMoneySpendRatio = maxMoneySpendRatio;
            tradeAgent.buyUsefulItemsFromMarketTrader = autoBuyMaterialsFromMarketTraders;
        }

        if (configureForgeAgent)
        {
            snapToForgeStandPoint = false;
            keepAtForgeStandPoint = false;

            forgeAgent.inventory = inventory;
            forgeAgent.tradeAgent = tradeAgent;
            forgeAgent.autoForge = autoForge;
            forgeAgent.preferCultivateWhenIdle = preferCultivateWhenIdle;
            forgeAgent.autoSellFinishedGoods = true;
            forgeAgent.sellToNearbyNpcBuyers = sellToNearbyNpcBuyers;
            forgeAgent.sellToVanBaoLau = sellToVanBaoLau;
            forgeAgent.forgeStandPoint = forgeStandPoint;
            forgeAgent.forgeFacingPoint = forgeFacingPoint;
            forgeAgent.defaultFacingDirection = defaultFacingDirection;
            forgeAgent.snapToForgeStandPoint = snapToForgeStandPoint;
            forgeAgent.keepAtForgeStandPoint = keepAtForgeStandPoint;
            forgeAgent.lockFacingToForgePoint = lockFacingToForgePoint;
            forgeAgent.forgeDurationMinGameHours =
                Mathf.Max(0f, forgeDailyHours);
            forgeAgent.forgeDurationMaxGameHours =
                Mathf.Max(
                    forgeAgent.forgeDurationMinGameHours,
                    forgeDailyHours);
            forgeAgent.autoBuyMaterialsFromMarketTraders =
                autoBuyMaterialsFromMarketTraders;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            if (forceVillagerJobWorker)
            {
                villager.job = VillagerJob.Worker;
            }

            if (forgeStandPoint != null)
            {
                villager.workPoint = forgeStandPoint;
            }
            if (marketPoint != null)
            {
                villager.marketPoint = marketPoint;
            }

            villager.autonomousWorkEnabled = true;
            villager.dailyRoutineEnabled = true;
            villager.dailyCultivationMinHours =
                Mathf.Max(0f, cultivateDailyHours);
            villager.dailyCultivationMaxHours =
                Mathf.Max(
                    villager.dailyCultivationMinHours,
                    cultivateDailyHours);
        }

        NpcSpecialProfession profession = GetComponent<NpcSpecialProfession>();
        if (profession != null)
        {
            profession.professionName = professionName;
            profession.lockVillagerJob = forceVillagerJobWorker;
            profession.villagerJob = VillagerJob.Worker;
        }
    }
}
