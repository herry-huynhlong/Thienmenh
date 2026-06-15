using System.Collections.Generic;
using UnityEngine;

public class TreasureHeatSystem : MonoBehaviour
{
    static TreasureHeatSystem instance;

    public float scanInterval = 4f;
    public float baseRadius = 5f;
    public float immortalRadius = 14f;
    public float stealDistance = 0.9f;
    public float storyCooldown = 30f;
    public float aggressionChance = 0.45f;
    public float visibleTreasureMultiplier = 2.5f;
    public float hiddenTreasureMultiplier = 0.35f;
    public float marketExposureMultiplier = 3f;
    public float shopkeeperGreedMultiplier = 1.8f;
    public bool enableRobbery = false;

    readonly Dictionary<StatItemData, float> lastStoryTimeByItem =
        new Dictionary<StatItemData, float>();

    float scanTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject systemObject =
            new GameObject("Treasure Heat System");

        DontDestroyOnLoad(systemObject);
        instance =
            systemObject.AddComponent<TreasureHeatSystem>();
    }

    public static void NotifyNpcReceivedItem(
        GameObject owner,
        StatItemData item)
    {
        EnsureRuntimeInstance();

        if (instance == null ||
            owner == null ||
            item == null)
        {
            return;
        }

        instance.HandleNpcReceivedItem(owner, item);
    }

    void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        scanTimer += Time.deltaTime;

        if (scanTimer < scanInterval)
        {
            return;
        }

        scanTimer = 0f;
        ScanTreasureOwners();
    }

    void ScanTreasureOwners()
    {
        ItemInventory[] inventories =
            FindObjectsOfType<ItemInventory>(true);

        foreach (ItemInventory inventory in inventories)
        {
            if (inventory == null ||
                !inventory.gameObject.activeInHierarchy ||
                !IsNpc(inventory.gameObject))
            {
                continue;
            }

            TreasureThreat threat =
                GetStrongestThreat(inventory);

            if (!threat.IsValid)
            {
                continue;
            }

            AnnounceThreat(inventory.gameObject, threat);
            TryTriggerRobbery(inventory, threat);
        }
    }

    void HandleNpcReceivedItem(
        GameObject owner,
        StatItemData item)
    {
        ItemInventory inventory =
            owner.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            return;
        }

        TreasureThreat threat =
            GetThreatForItem(
                owner,
                item);

        if (!threat.IsValid)
        {
            return;
        }

        AnnounceThreat(owner, threat);
        TryTriggerRobbery(inventory, threat);
    }

    TreasureThreat GetStrongestThreat(ItemInventory inventory)
    {
        TreasureThreat best =
            default;

        long ownerPower =
            System.Math.Max(1L, GetPower(inventory.gameObject));

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            TreasureThreat threat =
                BuildThreat(
                    inventory.gameObject,
                    stack.item,
                    ownerPower);

            if (threat.heat <= best.heat)
            {
                continue;
            }

            best = threat;
        }

        return best;
    }

    TreasureThreat GetThreatForItem(
        GameObject owner,
        StatItemData item)
    {
        if (owner == null ||
            item == null)
        {
            return default;
        }

        long ownerPower =
            System.Math.Max(1L, GetPower(owner));

        return BuildThreat(owner, item, ownerPower);
    }

    TreasureThreat BuildThreat(
        GameObject owner,
        StatItemData item,
        long ownerPower)
    {
        long value =
            NpcEconomy.GetItemValue(item);

        float heat =
            value / Mathf.Max(1f, ownerPower * 1200f);

        float exposure =
            GetExposureMultiplier(owner, item);

        heat *= exposure;

        if (item.grade == ItemGrade.Tien)
        {
            heat += 1000f;
        }
        else if (item.grade == ItemGrade.Thuong)
        {
            heat += 8f;
        }
        else if (item.grade == ItemGrade.Trung)
        {
            heat += 2f;
        }

        return new TreasureThreat
        {
            item = item,
            heat = heat,
            exposure = exposure,
            value = value
        };
    }

    void AnnounceThreat(
        GameObject owner,
        TreasureThreat threat)
    {
        if (WorldEventManager.Instance == null ||
            threat.item == null)
        {
            return;
        }

        if (lastStoryTimeByItem.TryGetValue(
                threat.item,
                out float lastTime) &&
            Time.time - lastTime < storyCooldown)
        {
            return;
        }

        lastStoryTimeByItem[threat.item] = Time.time;

        if (threat.item.grade == ItemGrade.Tien)
        {
            WorldEventManager.Instance.AddLog(
                "Tiên phẩm hiện thế, máu tanh mưa máu nổi lên. " +
                GetNpcName(owner) +
                " đang mang " +
                ItemText.Name(threat.item) +
                ".",
                2);
            return;
        }

        if (threat.exposure >= marketExposureMultiplier &&
            threat.heat >= 4f)
        {
            WorldEventManager.Instance.AddLog(
                GetNpcName(owner) +
                " mang " +
                ItemText.Name(threat.item) +
                " đi giao dịch, bị kẻ có tâm để mắt.",
                threat.item.grade == ItemGrade.Thuong ? 2 : 1);
            return;
        }

        if (threat.heat >= 10f)
        {
            WorldEventManager.Instance.AddLog(
                "Bảo vật làm người vô tội thành có tội. " +
                GetNpcName(owner) +
                " đang bị để mắt vì " +
                ItemText.Name(threat.item) +
                ".",
                threat.item.grade == ItemGrade.Thuong ? 2 : 1);
        }
    }

    void TryTriggerRobbery(
        ItemInventory ownerInventory,
        TreasureThreat threat)
    {
        if (!enableRobbery ||
            ownerInventory == null ||
            threat.item == null ||
            threat.heat < 3f)
        {
            return;
        }

        GameObject owner =
            ownerInventory.gameObject;

        GameObject robber =
            FindRobber(owner, threat);

        if (robber == null)
        {
            return;
        }

        if (Random.value > GetRobberyChance(threat))
        {
            MoveTowardRobberyTarget(robber, owner, threat);
            return;
        }

        float distance =
            Vector2.Distance(
                robber.transform.position,
                owner.transform.position);

        if (distance > stealDistance)
        {
            MoveTowardRobberyTarget(robber, owner, threat);
            return;
        }

        NpcItemCollector ownerCollector =
            owner.GetComponent<NpcItemCollector>();

        bool removed =
            ownerCollector != null
            ? ownerCollector.RemoveOwnedItem(
                threat.item,
                1,
                ItemLifecycleEventType.Stolen)
            : ownerInventory.RemoveItem(threat.item, 1);

        if (!removed)
        {
            return;
        }

        NpcItemCollector robberCollector =
            robber.GetComponent<NpcItemCollector>();

        if (robberCollector != null)
        {
            robberCollector.ReceiveItem(
                threat.item,
                ItemLifecycleEventType.Stolen,
                true);
        }
        else
        {
            ItemInventory robberInventory =
                robber.GetComponent<ItemInventory>();

            if (robberInventory == null)
            {
                robberInventory =
                    robber.AddComponent<ItemInventory>();

                robberInventory.shareRuntimeItems = false;
            }

            robberInventory.AddItem(threat.item, 1);
        }

        DamageVictim(owner, robber, threat);

        if (WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.AddLog(
                GetNpcName(robber) +
                " đã cướp " +
                ItemText.Name(threat.item) +
                " từ " +
                GetNpcName(owner) +
                ".",
                threat.item.grade == ItemGrade.Tien ? 2 : 1);
        }
    }

    GameObject FindRobber(
        GameObject owner,
        TreasureThreat threat)
    {
        float radius =
            threat.item.grade == ItemGrade.Tien
            ? immortalRadius
            : baseRadius + Mathf.Clamp(threat.heat, 0f, 8f);

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                owner.transform.position,
                radius);

        GameObject best = null;
        long bestPower = 0;
        long ownerPower =
            GetPower(owner);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            GameObject candidate =
                GetNpcRoot(hit);

            if (candidate == null ||
                candidate == owner)
            {
                continue;
            }

            long candidatePower =
                GetPower(candidate);

            float greed =
                GetRobberGreedMultiplier(candidate, owner, threat);

            long effectivePower =
                (long)System.Math.Round(candidatePower * greed);

            if (effectivePower <= ownerPower ||
                effectivePower <= bestPower)
            {
                continue;
            }

            best = candidate;
            bestPower = effectivePower;
        }

        return best;
    }

    void MoveTowardRobberyTarget(
        GameObject robber,
        GameObject owner,
        TreasureThreat threat)
    {
        VillagerAI villager =
            robber.GetComponent<VillagerAI>();

        if (villager != null)
        {
            villager.ForceTreasureHunt(
                owner.transform,
                threat.item);
            return;
        }

        SmartNpcAI smartNpc =
            robber.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            smartNpc.ForceTreasureHunt(
                owner.transform,
                threat.item);
        }
    }

    void DamageVictim(
        GameObject owner,
        GameObject robber,
        TreasureThreat threat)
    {
        int damage =
            threat.item.grade == ItemGrade.Tien
            ? 999999
            : threat.item.grade == ItemGrade.Thuong
                ? 80
                : 25;

        damage = NpcCombatTechniqueSystem.ModifyOutgoingDamage(
            robber,
            owner,
            damage);

        VillagerAI villager =
            owner.GetComponent<VillagerAI>();

        if (villager != null)
        {
            villager.TakeDamage(damage);
            return;
        }

        SmartNpcAI smartNpc =
            owner.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            smartNpc.TakeDamage(damage);
        }
    }

    float GetRobberyChance(TreasureThreat threat)
    {
        if (threat.item.grade == ItemGrade.Tien)
        {
            return 1f;
        }

        float exposureBonus =
            Mathf.Max(0f, threat.exposure - 1f) * 0.12f;

        return Mathf.Clamp01(
            aggressionChance +
            threat.heat * 0.04f +
            exposureBonus);
    }

    float GetExposureMultiplier(
        GameObject owner,
        StatItemData item)
    {
        float exposure = 1f;

        switch (item.itemType)
        {
            case ItemType.PhapBao:
                exposure *= visibleTreasureMultiplier;
                break;

            case ItemType.DanDuoc:
                exposure *= hiddenTreasureMultiplier;
                break;

            case ItemType.CongPhap:
                exposure *= 0.75f;
                break;
        }

        if (IsAtMarketOrSelling(owner))
        {
            exposure *= marketExposureMultiplier;
        }

        return Mathf.Max(0.05f, exposure);
    }

    float GetRobberGreedMultiplier(
        GameObject robber,
        GameObject owner,
        TreasureThreat threat)
    {
        float greed = 1f;

        NpcTradeAgent tradeAgent =
            robber.GetComponent<NpcTradeAgent>();

        if (tradeAgent != null &&
            tradeAgent.IsMarketTrader &&
            IsAtMarketOrSelling(owner))
        {
            greed *= shopkeeperGreedMultiplier;
        }

        if (threat.item.itemType == ItemType.PhapBao)
        {
            greed *= 1.25f;
        }

        if (threat.item.grade == ItemGrade.Tien)
        {
            greed *= 10f;
        }

        return greed;
    }

    bool IsAtMarketOrSelling(GameObject owner)
    {
        VillagerAI villager =
            owner.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return ContainsTradeAction(villager.currentAction);
        }

        SmartNpcAI smartNpc =
            owner.GetComponent<SmartNpcAI>();

        return smartNpc != null &&
            ContainsTradeAction(smartNpc.currentAction);
    }

    bool ContainsTradeAction(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        return action.Contains("bán") ||
            action.Contains("Bán") ||
            action.Contains("buôn") ||
            action.Contains("Buôn") ||
            action.Contains("giao dịch") ||
            action.Contains("Giao dịch") ||
            action.Contains("chợ") ||
            action.Contains("Chợ");
    }

    bool IsNpc(GameObject target)
    {
        return target.GetComponent<VillagerAI>() != null ||
            target.GetComponent<SmartNpcAI>() != null;
    }

    GameObject GetNpcRoot(Collider2D hit)
    {
        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();

        return smartNpc != null ? smartNpc.gameObject : null;
    }

    long GetPower(GameObject target)
    {
        if (target == null)
        {
            return 1;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return Mathf.Max(
                1,
                Mathf.RoundToInt(
                    CultivationProgression.GetStatPower(
                        smartNpc.realm,
                        smartNpc.realmStage,
                        EntityKind.Cultivator)));
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return ((long)Mathf.Max(0, (int)villager.realm) *
                CultivationProgression.MaxStage) +
                Mathf.Clamp(
                    villager.realmStage,
                    1,
                    CultivationProgression.MaxStage);
        }

        return 1;
    }

    string GetNpcName(GameObject target)
    {
        if (target == null)
        {
            return "Vô danh";
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        return target.name;
    }

    struct TreasureThreat
    {
        public StatItemData item;
        public float heat;
        public float exposure;
        public long value;

        public bool IsValid =>
            item != null &&
            heat >= 1.5f;
    }
}
