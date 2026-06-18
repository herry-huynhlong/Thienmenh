using UnityEngine;

public class NpcTradeAgent : MonoBehaviour
{
    public ItemInventory inventory;
    public LayerMask npcLayers = ~0;
    public float tradeRadius = 1.2f;
    public float tradeInterval = 5f;
    [Range(0, 100)]
    public int tradeChance = 25;
    public bool buyUsefulItemsFromMarketTrader = true;
    public float counterTradeCooldown = 45f;
    [Range(0f, 1f)] public float maxMoneySpendRatio = 0.65f;
    public int maxOwnedConsumableBeforeBuying = 3;
    public int riskyItemValueMultiplier = 120;

    [Header("Market Trader")]
    public bool isMarketTrader;
    public bool buyProduceFromVillagers = true;
    public bool acceptAllMaterials = true;
    public StatItemData[] acceptedProduce;
    public int maxProduceUnitsPerTrade = 4;
    [Range(1, 100)]
    public int buyPricePercent = 70;

    float tradeTimer;
    float nextCounterTradeTime;

    public bool IsMarketTrader => isMarketTrader;

    void Awake()
    {
        EnsureInventory();
    }

    void EnsureInventory()
    {
        if (inventory == null)
        {
            inventory = GetComponent<ItemInventory>();
        }

        if (inventory == null)
        {
            inventory = gameObject.AddComponent<ItemInventory>();
        }

        inventory.UsePrivateNpcRuntimeItems(false);
    }

    void Update()
    {
        if (NpcRoleUtility.IsDead(gameObject) ||
            NpcRoleUtility.IsInCombat(gameObject))
        {
            return;
        }

        if (!NpcScheduleController.AllowsTrade(gameObject))
        {
            return;
        }

        tradeTimer += Time.deltaTime;

        if (Time.time < nextCounterTradeTime)
        {
            return;
        }

        if (tradeTimer < tradeInterval)
        {
            return;
        }

        tradeTimer = 0f;

        if (Random.Range(0, 100) >= tradeChance)
        {
            return;
        }

        TryTradeWithNearbyNpc();
    }

    void TryTradeWithNearbyNpc()
    {
        if (inventory == null)
        {
            return;
        }

        if (!isMarketTrader &&
            NpcCounterBroker.TryTradeWithActiveBroker(this))
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                tradeRadius,
                npcLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.gameObject == gameObject)
            {
                continue;
            }

            NpcTradeAgent seller =
                hit.GetComponentInParent<NpcTradeAgent>();

            if (seller == null ||
                seller == this ||
                seller.inventory == null)
            {
                continue;
            }

            if (!isMarketTrader &&
                buyUsefulItemsFromMarketTrader &&
                !seller.isMarketTrader)
            {
                continue;
            }

            if (TryBuyOneUsefulItem(seller))
            {
                return;
            }
        }
    }

    bool TryBuyOneUsefulItem(NpcTradeAgent seller)
    {
        if (isMarketTrader)
        {
            return false;
        }

        if (seller == null ||
            NpcRoleUtility.IsInCombat(gameObject) ||
            NpcRoleUtility.IsInCombat(seller.gameObject))
        {
            return false;
        }

        StatItemData itemToBuy = null;
        int priceToPay = 0;
        float bestScore = 0f;

        foreach (ItemStack stack in seller.inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !stack.item.CanUseOn(gameObject))
            {
                continue;
            }

            int price =
                NpcEconomy.GetNpcBuyPrice(
                    stack.item,
                    gameObject,
                    NpcTradeContext.NpcToNpc);

            if (GetMoney() < price)
            {
                continue;
            }

            float score =
                GetBuyScore(stack.item, price);

            if (score <= bestScore)
            {
                continue;
            }

            itemToBuy = stack.item;
            priceToPay = price;
            bestScore = score;
        }

        if (itemToBuy == null ||
            !RemoveSellerItem(seller, itemToBuy))
        {
            return false;
        }

        AddMoney(-priceToPay);
        seller.AddMoney(priceToPay);
        ReceiveBoughtItem(itemToBuy);
        NpcSocialEventBus.PublishTradeCompleted(
            gameObject,
            seller.gameObject,
            itemToBuy,
            priceToPay);

        return true;
    }

    public bool TryBuyProduceFrom(
        VillagerAI seller,
        ItemInventory sellerInventory)
    {
        if (!isMarketTrader ||
            !buyProduceFromVillagers ||
            seller == null ||
            sellerInventory == null ||
            inventory == null ||
            NpcRoleUtility.IsInCombat(gameObject) ||
            NpcRoleUtility.IsInCombat(seller.gameObject))
        {
            return false;
        }

        int boughtUnits = 0;

        for (int i = sellerInventory.items.Count - 1; i >= 0; i--)
        {
            if (boughtUnits >= maxProduceUnitsPerTrade)
            {
                break;
            }

            ItemStack stack =
                sellerInventory.items[i];

            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                !NpcEconomy.CanTradeNormally(stack.item) ||
                !AcceptsProduce(stack.item))
            {
                continue;
            }

            int amount =
                Mathf.Min(
                    stack.amount,
                    maxProduceUnitsPerTrade - boughtUnits);

            int unitPrice =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        NpcEconomy.GetTradePrice(
                            stack.item,
                            NpcTradeContext.ProduceBuy) *
                        buyPricePercent / 100f));

            int totalPrice =
                unitPrice * amount;

            if (GetMoney() < totalPrice)
            {
                continue;
            }

            if (!sellerInventory.RemoveItem(stack.item, amount))
            {
                continue;
            }

            AddMoney(-totalPrice);
            seller.money += totalPrice;
            inventory.AddItem(stack.item, amount);
            NpcSocialEventBus.PublishTradeCompleted(
                gameObject,
                seller.gameObject,
                stack.item,
                totalPrice);
            ItemLifecycleSystem.Notify(
                ItemLifecycleEventType.Sold,
                stack.item,
                seller.gameObject,
                gameObject);
            boughtUnits += amount;
        }

        return boughtUnits > 0;
    }

    bool AcceptsProduce(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (acceptAllMaterials &&
            (item.itemType == ItemType.VatLieu ||
            item.itemType == ItemType.ThucPham))
        {
            return true;
        }

        if (acceptedProduce == null)
        {
            return false;
        }

        foreach (StatItemData accepted in acceptedProduce)
        {
            if (accepted == item)
            {
                return true;
            }
        }

        return false;
    }

    public bool RemoveOwnedItem(
        StatItemData item,
        int amount,
        ItemLifecycleEventType reason)
    {
        NpcItemCollector collector =
            GetComponent<NpcItemCollector>();

        if (collector != null)
        {
            return collector.RemoveOwnedItem(
                item,
                amount,
                reason);
        }

        return inventory != null &&
            inventory.RemoveItem(item, amount);
    }

    bool RemoveSellerItem(
        NpcTradeAgent seller,
        StatItemData item)
    {
        return seller.RemoveOwnedItem(
            item,
            1,
            ItemLifecycleEventType.Sold);
    }

    public void ReceiveBoughtItem(StatItemData item)
    {
        ReceiveBoughtItem(item, true);
    }

    public void ReceiveBoughtItem(
        StatItemData item,
        bool considerUse)
    {
        EnsureInventory();
        ItemEffectSpawner.PlayPickupEffect(item, transform);

        NpcItemCollector collector =
            GetCollectorForBoughtItem(item, considerUse);

        if (collector != null)
        {
            collector.ReceiveItem(
                item,
                ItemLifecycleEventType.Sold,
                considerUse);
            return;
        }

        inventory.AddItem(item, 1);
    }

    NpcItemCollector GetCollectorForBoughtItem(
        StatItemData item,
        bool considerUse)
    {
        NpcItemCollector collector =
            GetComponent<NpcItemCollector>();

        if (collector == null &&
            considerUse &&
            item != null &&
            item.ShouldNpcUseDirectly())
        {
            collector = gameObject.AddComponent<NpcItemCollector>();
            collector.canPickupItems = false;
            collector.autoUsePickedItems = false;
        }

        if (collector != null)
        {
            collector.inventory = inventory;
        }

        return collector;
    }

    public float GetBuyScore(StatItemData item, int price)
    {
        if (item == null ||
            price <= 0)
        {
            return 0f;
        }

        int money = GetMoney();
        if (money < price ||
            price > Mathf.Max(1, Mathf.RoundToInt(money * Mathf.Clamp01(maxMoneySpendRatio))))
        {
            return 0f;
        }

        float score = 0.5f;
        bool nearBreakthrough = NpcEconomy.IsNearBreakthrough(gameObject);

        if (item.itemType == ItemType.DanDuoc)
        {
            if (nearBreakthrough)
            {
                score += item.breakthroughRealm ? 80f : 35f;
            }
            else if (item.cultivationBonus > 0)
            {
                score += 12f;
            }
            else if (item.hpBonus > 0)
            {
                score += 8f;
            }

            if (inventory != null &&
                inventory.GetAmount(item) >= maxOwnedConsumableBeforeBuying)
            {
                score *= 0.2f;
            }
        }
        else if (item.itemType == ItemType.PhapBao)
        {
            score += IsCombatRole() ? 25f : 3f;
            score += Mathf.Max(0, item.damageBonus) * 1.5f;
            score += Mathf.Max(0, item.armorBonus) * 1.2f;

            if (AlreadyHasUsefulEquipment(item))
            {
                score *= 0.25f;
            }
        }
        else if (item.itemType == ItemType.CongPhap)
        {
            score += 14f + Mathf.Max(0, item.studyProgressPerUse) * 2f;
        }
        else if (item.itemType == ItemType.ThucPham)
        {
            VillagerAI villager = GetComponent<VillagerAI>();
            SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
            float hunger = villager != null ? villager.hunger : smartNpc != null ? smartNpc.hunger : 0f;
            score += hunger >= 55f ? 14f : 2f;
        }

        score += Mathf.Max(
            item.GetNpcUseScore(),
            item.GetNpcConversionScore() * 0.15f);

        if (item.ShouldNpcPreferSell())
        {
            score *= 0.35f;
        }

        float wealthRatio = money / Mathf.Max(1f, price);
        return score * Mathf.Clamp(wealthRatio, 0.25f, 4f);
    }

    public bool CanUseCounterTrade()
    {
        return Time.time >= nextCounterTradeTime;
    }

    public void MarkCounterTradeHandled(float cooldownMultiplier = 1f)
    {
        nextCounterTradeTime = Time.time +
            Mathf.Max(1f, counterTradeCooldown * Mathf.Max(0.1f, cooldownMultiplier));
        tradeTimer = 0f;
    }

    public bool ShouldSellToCounter(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (!item.canBeSold)
        {
            return false;
        }

        if (item.itemType == ItemType.VatLieu ||
            item.itemType == ItemType.ThucPham)
        {
            return true;
        }

        if (!item.CanUseOn(gameObject))
        {
            return true;
        }

        if (IsTooRiskyToKeep(item))
        {
            return true;
        }

        if (IsOutclassedEquipment(item))
        {
            return true;
        }

        return item.ShouldNpcPreferSell();
    }

    bool IsTooRiskyToKeep(StatItemData item)
    {
        if (item == null ||
            IsCombatRole())
        {
            return false;
        }

        int itemValue =
            NpcEconomy.GetItemValue(item);

        int realmPower =
            Mathf.Max(1, NpcRoleUtility.GetRealmPower(gameObject));

        return itemValue >=
            realmPower * Mathf.Max(1, riskyItemValueMultiplier);
    }

    bool IsOutclassedEquipment(StatItemData item)
    {
        if (item == null ||
            item.itemType != ItemType.PhapBao ||
            inventory == null)
        {
            return false;
        }

        EquipmentSlot slot =
            item.GetResolvedEquipmentSlot();

        if (slot == EquipmentSlot.None)
        {
            return false;
        }

        float itemScore =
            item.GetEquipmentUseScore();

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.item == item ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.PhapBao ||
                stack.item.GetResolvedEquipmentSlot() != slot)
            {
                continue;
            }

            if (stack.item.GetEquipmentUseScore() > itemScore)
            {
                return true;
            }
        }

        return false;
    }

    bool IsCombatRole()
    {
        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null && smartNpc.enabled)
        {
            return smartNpc.canFight;
        }

        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.job == VillagerJob.Guard ||
                villager.job == VillagerJob.Hunter;
        }

        return false;
    }

    bool AlreadyHasUsefulEquipment(StatItemData item)
    {
        if (item == null ||
            item.itemType != ItemType.PhapBao ||
            inventory == null)
        {
            return false;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.PhapBao)
            {
                continue;
            }

            bool sameRole = item.damageBonus > 0
                ? stack.item.damageBonus >= item.damageBonus
                : stack.item.armorBonus >= item.armorBonus;

            if (sameRole)
            {
                return true;
            }
        }

        return false;
    }
    public int GetMoney()
    {
        return NpcEconomy.GetNpcMoney(gameObject);
    }

    public void AddMoney(int amount)
    {
        NpcEconomy.AddNpcMoney(gameObject, amount);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, tradeRadius);
    }
}
