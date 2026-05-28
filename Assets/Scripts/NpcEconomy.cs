using UnityEngine;

public enum NpcTradeContext
{
    MarketBuy,
    MarketSell,
    NpcToNpc,
    ProduceBuy
}

public static class NpcEconomy
{
    public const string CurrencyShortName = "LT";

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
                multiplier = 1.15f;
                break;

            case NpcTradeContext.MarketSell:
                multiplier = 0.7f;
                break;

            case NpcTradeContext.NpcToNpc:
                multiplier = 1f;
                break;

            case NpcTradeContext.ProduceBuy:
                multiplier = 0.6f;
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

        return item.grade != ItemGrade.Tien;
    }

    public static bool CanAffordNpc(GameObject npc, int amount)
    {
        return GetNpcMoney(npc) >= Mathf.Max(0, amount);
    }

    public static int GetNpcMoney(GameObject npc)
    {
        if (npc == null)
        {
            return 0;
        }

        VillagerAI villager =
            npc.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.money;
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();

        return smartNpc != null ? smartNpc.money : 0;
    }

    public static void AddNpcMoney(GameObject npc, int amount)
    {
        if (npc == null)
        {
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

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            smartNpc.money =
                Mathf.Max(0, smartNpc.money + amount);
        }
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
        if (item != null &&
            item.grade == ItemGrade.Tien)
        {
            return "Co duyen";
        }

        return GetItemValue(item) + " " + CurrencyShortName;
    }

    public static string FormatTradePrice(
        StatItemData item,
        NpcTradeContext context)
    {
        if (!CanTradeNormally(item))
        {
            return "Co duyen";
        }

        return GetTradePrice(item, context) +
            " " +
            CurrencyShortName;
    }

    static int GetBasePrice(
        ItemType type,
        ItemGrade grade)
    {
        switch (type)
        {
            case ItemType.DanDuoc:
                return GetByGrade(grade, 1800, 30000, 500000, 0);

            case ItemType.CongPhap:
                return GetByGrade(grade, 9000, 120000, 1800000, 0);

            case ItemType.PhapBao:
                return GetByGrade(grade, 16000, 260000, 4200000, 0);

            case ItemType.ThucPham:
                return 20;

            case ItemType.VatLieu:
                return GetByGrade(grade, 80, 1200, 18000, 0);

            default:
                return GetByGrade(grade, 50, 300, 2000, 30000);
        }
    }

    static int GetStatValue(StatItemData item)
    {
        int gradeMultiplier =
            GetGradeMultiplier(item.grade);

        int value = 0;
        value += Mathf.Max(0, item.hpBonus) * 2;
        value += Mathf.Max(0, item.cultivationBonus) * 8;
        value += Mathf.Max(0, item.damageBonus) * 16;
        value += Mathf.Max(0, item.armorBonus) * 16;
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
                    Mathf.RoundToInt(Mathf.Abs(modifier.floatValue) * 20f);
            }
        }

        return value * gradeMultiplier;
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
