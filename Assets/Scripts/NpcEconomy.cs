using System.Globalization;
using UnityEngine;

public enum NpcTradeContext
{
    MarketBuy,
    MarketSell,
    NpcToNpc,
    ProduceBuy,
    CounterBrokerBuy,
    CounterBrokerSell
}

public static class NpcEconomy
{
    public const string CurrencyName = "Linh Thạch";
    public const string CurrencyShortName = "LT";
    public const int CurrencyCultivationExp = CultivationProgression.SpiritStoneBaseExp;

    public static int GetItemValue(StatItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        int manualPrice =
            Mathf.Max(0, item.price);

        if (manualPrice > 0)
        {
            return manualPrice;
        }

        int basePrice =
            GetBasePrice(item.itemType, item.grade);

        int statValue =
            GetStatValue(item);

        int value =
            basePrice + statValue;

        if (item.breakthroughRealm)
        {
            value += GetGradeMultiplier(item.grade) * 250;
        }

        if (item.isTemporary)
        {
            value =
                Mathf.RoundToInt(value * 0.65f);
        }

        return Mathf.Max(1, value);
    }

    public static int GetTradePrice(
        StatItemData item,
        NpcTradeContext context)
    {
        int value =
            GetItemValue(item);

        if (value <= 0)
        {
            return 0;
        }

        float multiplier = 1f;

        switch (context)
        {
            case NpcTradeContext.MarketBuy:
                multiplier = 1.2f;
                break;

            case NpcTradeContext.MarketSell:
                multiplier = 1f;
                break;

            case NpcTradeContext.NpcToNpc:
                multiplier = 1f;
                break;

            case NpcTradeContext.ProduceBuy:
                multiplier = 0.6f;
                break;

            case NpcTradeContext.CounterBrokerBuy:
                multiplier = 1.35f;
                break;

            case NpcTradeContext.CounterBrokerSell:
                multiplier = 0.9f;
                break;
        }

        return Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
    }

    public static int GetNpcBuyPrice(
        StatItemData item,
        GameObject buyer,
        NpcTradeContext context)
    {
        int price =
            GetTradePrice(item, context);

        if (price <= 0)
        {
            return 0;
        }

        float need =
            GetNpcNeedMultiplier(item, buyer);

        return Mathf.Max(1, Mathf.RoundToInt(price * need));
    }

    public static bool CanTradeNormally(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        return true;
    }

    public static bool CanAffordNpc(GameObject npc, int amount)
    {
        return GetNpcMoney(npc) >= Mathf.Max(0, amount);
    }

    public static bool CanAffordNpcLinhThach(GameObject npc, int amount)
    {
        return CanAffordNpc(npc, amount);
    }

    public static int GetNpcMoney(GameObject npc)
    {
        if (npc == null)
        {
            return 0;
        }

        if (TryGetDedicatedVillagerWallet(
                npc,
                out VillagerAI dedicatedVillager))
        {
            return GetDedicatedVillagerWalletAmount(
                npc,
                dedicatedVillager);
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();
        NpcCounterBroker broker =
            npc.GetComponent<NpcCounterBroker>();
        if (broker != null &&
            smartNpc != null)
        {
            return broker.CurrentMoney;
        }

        NpcTradeAgent tradeAgent =
            npc.GetComponent<NpcTradeAgent>();
        if (tradeAgent != null &&
            tradeAgent.useSpiritStoneCurrency)
        {
            return Mathf.Max(0, tradeAgent.spiritStone);
        }

        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.money;
        }

        return smartNpc != null ? smartNpc.money : 0;
    }

    public static int GetNpcLinhThach(GameObject npc)
    {
        return GetNpcMoney(npc);
    }

    public static bool UsesDedicatedProfessionWallet(GameObject npc)
    {
        return TryGetDedicatedVillagerWallet(npc, out _);
    }

    public static void SetDedicatedProfessionWalletAmount(
        GameObject npc,
        int amount)
    {
        if (!TryGetDedicatedVillagerWallet(
                npc,
                out VillagerAI dedicatedVillager))
        {
            return;
        }

        SetDedicatedVillagerWalletAmount(
            npc,
            dedicatedVillager,
            amount);
    }

    public static void AddNpcMoney(GameObject npc, int amount)
    {
        if (npc == null)
        {
            return;
        }

        if (TryGetDedicatedVillagerWallet(
                npc,
                out VillagerAI dedicatedVillager))
        {
            SetDedicatedVillagerWalletAmount(
                npc,
                dedicatedVillager,
                GetDedicatedVillagerWalletAmount(
                    npc,
                    dedicatedVillager) + amount);
            return;
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();
        NpcCounterBroker broker =
            npc.GetComponent<NpcCounterBroker>();
        if (broker != null &&
            smartNpc != null)
        {
            if (amount >= 0)
            {
                broker.AddBrokerRevenue(amount);
            }
            else
            {
                int spendAmount = -amount;
                if (!broker.TrySpendBrokerMoney(spendAmount))
                {
                    int available = broker.CurrentMoney;
                    if (available > 0)
                    {
                        broker.TrySpendBrokerMoney(available);
                    }
                }
            }

            return;
        }

        NpcTradeAgent tradeAgent =
            npc.GetComponent<NpcTradeAgent>();
        if (tradeAgent != null &&
            tradeAgent.useSpiritStoneCurrency)
        {
            tradeAgent.AddMoney(amount);
            return;
        }

        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            villager.money =
                Mathf.Max(0, villager.money + amount);
            return;
        }

        if (smartNpc != null)
        {
            smartNpc.money =
                Mathf.Max(0, smartNpc.money + amount);
        }
    }

    public static void AddNpcLinhThach(GameObject npc, int amount)
    {
        AddNpcMoney(npc, amount);
    }

    static bool TryGetDedicatedVillagerWallet(
        GameObject npc,
        out VillagerAI villager)
    {
        villager = null;

        if (npc == null)
        {
            return false;
        }

        villager = npc.GetComponent<VillagerAI>();
        if (villager == null)
        {
            return false;
        }

        NpcFixedBlacksmithController fixedBlacksmith =
            npc.GetComponent<NpcFixedBlacksmithController>();
        if (fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.UseDedicatedRoutine)
        {
            return true;
        }

        NpcFixedAlchemistController fixedAlchemist =
            npc.GetComponent<NpcFixedAlchemistController>();
        return fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.UseDedicatedRoutine;
    }

    static int GetDedicatedVillagerWalletAmount(
        GameObject npc,
        VillagerAI villager)
    {
        if (villager == null)
        {
            return 0;
        }

        int villagerWallet =
            Mathf.Max(0, villager.money);

        if (villager.entityProfile != null &&
            villager.entityProfile.stats != null)
        {
            villagerWallet =
                Mathf.Max(
                    villagerWallet,
                    Mathf.Max(
                        villager.entityProfile.stats.money,
                        villager.entityProfile.stats.spiritStone));
        }

        int minimumWallet =
            GetDedicatedVillagerMinimumWallet(npc);
        if (minimumWallet > 0 &&
            villagerWallet < minimumWallet)
        {
            SetDedicatedVillagerWalletAmount(
                npc,
                villager,
                minimumWallet);
            return minimumWallet;
        }

        return villagerWallet;
    }

    static void SetDedicatedVillagerWalletAmount(
        GameObject npc,
        VillagerAI villager,
        int amount)
    {
        if (villager == null)
        {
            return;
        }

        int walletAmount = Mathf.Max(0, amount);
        villager.money = walletAmount;
        villager.spiritStone = walletAmount;

        if (villager.entityProfile != null &&
            villager.entityProfile.stats != null)
        {
            villager.entityProfile.stats.money = walletAmount;
            villager.entityProfile.stats.spiritStone = walletAmount;
        }

        NpcTradeAgent tradeAgent =
            npc != null
                ? npc.GetComponent<NpcTradeAgent>()
                : null;
        if (tradeAgent != null)
        {
            tradeAgent.spiritStone = walletAmount;
        }
    }

    static int GetDedicatedVillagerMinimumWallet(
        GameObject npc)
    {
        if (npc == null)
        {
            return 0;
        }

        NpcFixedBlacksmithController fixedBlacksmith =
            npc.GetComponent<NpcFixedBlacksmithController>();
        if (fixedBlacksmith != null &&
            fixedBlacksmith.enabled &&
            fixedBlacksmith.UseDedicatedRoutine &&
            fixedBlacksmith.grantStartingMoneyOnStart)
        {
            return Mathf.Max(0, fixedBlacksmith.startingMoney);
        }

        NpcFixedAlchemistController fixedAlchemist =
            npc.GetComponent<NpcFixedAlchemistController>();
        if (fixedAlchemist != null &&
            fixedAlchemist.enabled &&
            fixedAlchemist.UseDedicatedRoutine &&
            fixedAlchemist.grantStartingMoneyOnStart)
        {
            return Mathf.Max(0, fixedAlchemist.startingMoney);
        }

        return 0;
    }

    public static bool IsNearBreakthrough(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.breakthroughNeed > 0 &&
                smartNpc.cultivation >=
                smartNpc.breakthroughNeed * 8L / 10L;
        }

        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            long need =
                villager.ExpToNextRealm();

            return need > 0 &&
                villager.cultivationExp >=
                need * 8L / 10L;
        }

        CharacterStats stats =
            npc.GetComponent<CharacterStats>();

        if (stats != null)
        {
            long need =
                stats.ExpToNextRealm();

            return need > 0 &&
                stats.cultivationExp >=
                need * 8L / 10L;
        }

        return false;
    }

    public static string FormatPrice(StatItemData item)
    {
        return FormatCurrency(GetItemValue(item));
    }

    public static string FormatTradePrice(
        StatItemData item,
        NpcTradeContext context)
    {
        return FormatCurrency(GetTradePrice(item, context));
    }

    public static string FormatCurrency(int amount)
    {
        return FormatCompactAmount(amount) +
            " " +
            CurrencyShortName;
    }

    public static string FormatCurrencyWithName(int amount)
    {
        return FormatCompactAmount(amount) +
            " " +
            CurrencyName;
    }

    public static string FormatCompactAmount(long amount)
    {
        if (amount == long.MinValue)
        {
            amount = long.MaxValue;
        }

        bool negative = amount < 0;
        ulong absValue = (ulong)(negative ? -amount : amount);

        string formatted;
        if (absValue < 1000UL)
        {
            formatted = absValue.ToString(CultureInfo.InvariantCulture);
        }
        else if (absValue < 1000000UL)
        {
            formatted = FormatCompactUnit(absValue, 1000UL, "k");
        }
        else if (absValue < 1000000000UL)
        {
            formatted = FormatCompactUnit(absValue, 1000000UL, "m");
        }
        else if (absValue < 1000000000000UL)
        {
            formatted = FormatCompactUnit(absValue, 1000000000UL, "b");
        }
        else
        {
            formatted = FormatCompactUnit(absValue, 1000000000000UL, "t");
        }

        return negative ? "-" + formatted : formatted;
    }

    static string FormatCompactAmount(int amount)
    {
        return FormatCompactAmount((long)amount);
    }

    static string FormatCompactUnit(
        ulong amount,
        ulong unit,
        string suffix)
    {
        ulong whole = amount / unit;
        ulong remainder = amount % unit;
        ulong leading = remainder / (unit / 10UL);

        if (leading == 0UL)
        {
            return whole.ToString(CultureInfo.InvariantCulture) + suffix;
        }

        return whole.ToString(CultureInfo.InvariantCulture) +
            suffix +
            leading.ToString(CultureInfo.InvariantCulture);
    }

    static int GetBasePrice(
        ItemType type,
        ItemGrade grade)
    {
        switch (type)
        {
            case ItemType.DanDuoc:
                return GetByGrade(grade, 1000, 15000, 200000, 100000000);

            case ItemType.CongPhap:
                return GetByGrade(grade, 3000, 50000, 700000, 50000000);

            case ItemType.PhapBao:
                return GetByGrade(grade, 800, 8000, 80000, 5000000);

            case ItemType.ThucPham:
                return GetByGrade(grade, 80, 1000, 20000, 2000000);

            case ItemType.VatLieu:
                return GetByGrade(grade, 100, 2000, 30000, 20000000);

            default:
                return GetByGrade(grade, 100, 1000, 10000, 1000000);
        }
    }

    static int GetStatValue(StatItemData item)
    {
        int gradeMultiplier =
            GetGradeMultiplier(item.grade);

        int value = 0;
        int cultivationValue = GetCultivationExpValue(item);

        value += Mathf.Max(0, item.hpBonus) * 2;
        value += Mathf.Max(0, item.damageBonus) * 16;
        value += Mathf.Max(0, item.armorBonus) * 16;
        value += Mathf.Max(0, item.damageBonusPercent) * 42;
        value += Mathf.Max(0, item.armorBonusPercent) * 34;
        value += Mathf.Max(0, item.maxHpBonusPercent) * 26;
        value += Mathf.Max(0, item.effectResistanceBonus) * 12;

        if (item.modifiers != null)
        {
            foreach (StatModifier modifier in item.modifiers)
            {
                if (modifier == null)
                {
                    continue;
                }

                value +=
                    Mathf.Abs(modifier.intValue) * 10;

                value +=
                    Mathf.RoundToInt(
                        Mathf.Abs(modifier.floatValue) * 240f);
            }
        }

        return value * gradeMultiplier + cultivationValue;
    }


    static int GetCultivationExpValue(StatItemData item)
    {
        int exp = Mathf.Max(0, item.cultivationBonus);
        if (exp <= 0)
        {
            return 0;
        }

        float efficiency = 1f;

        switch (item.itemType)
        {
            case ItemType.DanDuoc:
                efficiency = item.pillKind == PillKind.Cultivation ? 3f : 1f;
                break;

            case ItemType.VatLieu:
                efficiency = 1.5f;
                break;

            case ItemType.ThucPham:
                efficiency = 0.6f;
                break;

            case ItemType.CongPhap:
                efficiency = 0.35f;
                break;
        }

        float divisor =
            Mathf.Max(1f, CurrencyCultivationExp * efficiency);

        return Mathf.Max(1, Mathf.RoundToInt(exp / divisor));
    }

    static float GetNpcNeedMultiplier(
        StatItemData item,
        GameObject buyer)
    {
        if (item == null ||
            buyer == null)
        {
            return 1f;
        }

        if (item.itemType == ItemType.DanDuoc &&
            IsNearBreakthrough(buyer))
        {
            return item.breakthroughRealm ? 3.5f : 2f;
        }

        if (item.itemType == ItemType.DanDuoc)
        {
            return 1.25f;
        }

        if (item.itemType == ItemType.CongPhap)
        {
            return 1.05f;
        }

        if (item.itemType == ItemType.PhapBao)
        {
            return 1.1f;
        }

        return 1f;
    }

    static int GetGradeMultiplier(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return 5;

            case ItemGrade.Thuong:
                return 25;

            case ItemGrade.Tien:
                return 180;

            default:
                return 1;
        }
    }

    static int GetByGrade(
        ItemGrade grade,
        int ha,
        int trung,
        int thuong,
        int tien)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return trung;

            case ItemGrade.Thuong:
                return thuong;

            case ItemGrade.Tien:
                return tien;

            default:
                return ha;
        }
    }
}

