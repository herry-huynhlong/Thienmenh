using System;
using System.Collections.Generic;
using UnityEngine;

public static class ItemStatBalanceUtility
{
    const float QualityMin = 0.85f;
    const float QualityMax = 1.15f;
    const float ImmortalQualityMin = 0.9f;
    const float ImmortalQualityMax = 1.35f;

    struct IntRange
    {
        public readonly int Min;
        public readonly int Max;

        public IntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }

    public static void ApplyBalancedStats(StatItemData item, int seed = 0)
    {
        if (item == null)
        {
            return;
        }

        System.Random rng = new System.Random(BuildSeed(item, seed));
        ResetBalancedStats(item);
        ApplyCommonUseRules(item);

        switch (item.itemType)
        {
            case ItemType.PhapBao:
                RollArtifact(item, rng);
                break;
            case ItemType.CongPhap:
                RollManual(item, rng);
                break;
            case ItemType.DanDuoc:
                RollPill(item, rng);
                break;
            case ItemType.ThucPham:
                RollFood(item, rng);
                break;
            case ItemType.VatLieu:
                RollMaterial(item, rng);
                break;
        }

        RollPrice(item, rng);
    }

    static void ResetBalancedStats(StatItemData item)
    {
        item.hpBonus = 0;
        item.cultivationBonus = 0;
        item.breakthroughRealm = false;
        item.damageBonus = 0;
        item.armorBonus = 0;
        item.effectResistanceBonus = 0;
        item.isTemporary = false;
        item.durationScaledSeconds =
            Mathf.Max(1f, item.durationScaledSeconds);
        item.price = 0;
        item.equipmentSlot = EquipmentSlot.None;
        item.artifactKind = ArtifactKind.None;
        item.pillKind = PillKind.None;
        item.manualKind = ManualKind.None;
        item.materialKind = MaterialKind.None;
        item.foodKind = FoodKind.None;

        RemoveBalancedModifiers(item);
    }

    static void ApplyCommonUseRules(StatItemData item)
    {
        item.useStyle = item.GetResolvedUseStyle();
        item.canUseDirectly = true;
        item.canBeSold = true;
        item.canBeRefinedIntoPill = false;
        item.canBeForgedIntoArtifact = false;
        item.canBeStudied = item.itemType == ItemType.CongPhap;
        item.canBeTaught = item.itemType == ItemType.CongPhap;
        item.rawUsePolicy = RawUsePolicy.Allowed;
        item.rawToxicityDamage = 0;
        item.rawUseEfficiency = 1f;
        item.useSuccessChance = 1f;
        item.npcIntent = NpcItemIntent.Auto;
        item.durabilityLossPerUse = Mathf.Max(1, item.durabilityLossPerUse);
        item.breaksAtZero = true;

        switch (item.itemType)
        {
            case ItemType.PhapBao:
                item.consumeOnUse = false;
                item.useStyle = ItemUseStyle.DurableEquipment;
                item.equipmentSlot = EquipmentSlot.Weapon;
                item.artifactKind = ArtifactKind.Sword;
                item.canBeStudied = false;
                item.canBeTaught = false;
                break;
            case ItemType.CongPhap:
                item.consumeOnUse = false;
                item.useStyle = ItemUseStyle.StudyManual;
                item.manualKind = ManualKind.Mixed;
                break;
            case ItemType.VatLieu:
                item.consumeOnUse = true;
                item.useStyle = ItemUseStyle.RawMaterial;
                item.materialKind = MaterialKind.Herb;
                item.canBeRefinedIntoPill = true;
                item.canBeForgedIntoArtifact = true;
                item.rawUseEfficiency = 0.35f;
                item.rawUsePolicy = RawUsePolicy.Risky;
                item.useSuccessChance = 0.85f;
                break;
            default:
                item.consumeOnUse = true;
                item.useStyle = ItemUseStyle.Consumable;
                if (item.itemType == ItemType.ThucPham)
                {
                    item.foodKind = FoodKind.Meal;
                }
                item.canBeStudied = false;
                item.canBeTaught = false;
                break;
        }
    }

    static void RollArtifact(StatItemData item, System.Random rng)
    {
        item.damageBonus = Roll(item.grade, new IntRange(8, 16), new IntRange(18, 35), new IntRange(45, 80), new IntRange(140, 260), rng);
        item.armorBonus = Roll(item.grade, new IntRange(2, 6), new IntRange(6, 14), new IntRange(18, 35), new IntRange(60, 120), rng);
        item.effectResistanceBonus = Roll(item.grade, new IntRange(0, 3), new IntRange(3, 8), new IntRange(8, 18), new IntRange(25, 50), rng);

        if (item.armorBonus > item.damageBonus)
        {
            item.equipmentSlot = EquipmentSlot.Armor;
            item.artifactKind = ArtifactKind.Armor;
        }
    }

    static void RollManual(StatItemData item, System.Random rng)
    {
        item.damageBonus = Roll(item.grade, new IntRange(4, 10), new IntRange(10, 24), new IntRange(28, 60), new IntRange(90, 180), rng);
        item.armorBonus = Roll(item.grade, new IntRange(2, 6), new IntRange(6, 16), new IntRange(18, 45), new IntRange(70, 140), rng);
        item.cultivationBonus = Roll(item.grade, new IntRange(3000, 10000), new IntRange(50000, 180000), new IntRange(700000, 3000000), new IntRange(50000000, 500000000), rng);
        item.effectResistanceBonus = Mathf.RoundToInt(item.armorBonus * 0.35f);
        item.manualKind = ManualKind.Mixed;
        item.studyProgressPerUse = Mathf.Max(1, 1 + (int)item.grade);
        item.tieuThanhPower = 0.3f;
        item.trungThanhPower = 0.6f;
        item.daiThanhPower = 1f;
    }

    static void RollPill(StatItemData item, System.Random rng)
    {
        int pillRoll = StableHash(item.itemName + item.name) % 10;

        if (pillRoll <= 1)
        {
            item.pillKind = PillKind.Breakthrough;
            item.breakthroughRealm = true;
            item.cultivationBonus = Roll(item.grade, new IntRange(12000, 45000), new IntRange(180000, 900000), new IntRange(3000000, 15000000), new IntRange(150000000, 900000000), rng);
        }
        else if (pillRoll <= 6)
        {
            item.pillKind = PillKind.Cultivation;
            item.cultivationBonus = Roll(item.grade, new IntRange(30000, 120000), new IntRange(450000, 1800000), new IntRange(6000000, 27000000), new IntRange(300000000, 1500000000), rng);
        }
        else
        {
            item.pillKind = PillKind.Heal;
            item.hpBonus = Roll(item.grade, new IntRange(30, 80), new IntRange(100, 250), new IntRange(400, 1000), new IntRange(2000, 6000), rng);
        }

        if (RollChance(item.grade, rng, 0.12f, 0.18f, 0.25f, 0.35f))
        {
            item.damageBonus = Roll(item.grade, new IntRange(1, 3), new IntRange(3, 8), new IntRange(8, 20), new IntRange(30, 80), rng);
        }

        if (RollChance(item.grade, rng, 0.16f, 0.22f, 0.3f, 0.45f))
        {
            SetIntModifier(item, StatType.MaxHP, Roll(item.grade, new IntRange(5, 20), new IntRange(20, 60), new IntRange(80, 200), new IntRange(300, 1000), rng));
        }
    }

    static void RollFood(StatItemData item, System.Random rng)
    {
        item.hpBonus = Roll(item.grade, new IntRange(10, 30), new IntRange(30, 80), new IntRange(100, 250), new IntRange(500, 1200), rng);
        item.cultivationBonus = Roll(item.grade, new IntRange(500, 1800), new IntRange(6000, 30000), new IntRange(120000, 600000), new IntRange(1000000, 8000000), rng);

        if (RollChance(item.grade, rng, 0.2f, 0.25f, 0.3f, 0.4f))
        {
            item.damageBonus = Roll(item.grade, new IntRange(2, 5), new IntRange(5, 12), new IntRange(15, 35), new IntRange(50, 100), rng);
            item.isTemporary = true;
            item.durationScaledSeconds = RollFloat(rng, 20f, 90f);
        }
    }

    static void RollMaterial(StatItemData item, System.Random rng)
    {
        float efficiency = RollMaterialEfficiency(item.grade, rng);
        item.rawUseEfficiency = efficiency;
        item.useSuccessChance = Mathf.Clamp(0.65f + efficiency, 0.7f, 1f);
        item.rawToxicityDamage = Roll(item.grade, new IntRange(2, 8), new IntRange(6, 18), new IntRange(15, 40), new IntRange(40, 120), rng);

        int baseCultivation = Roll(item.grade, new IntRange(7500, 37500), new IntRange(150000, 750000), new IntRange(4500000, 30000000), new IntRange(300000000, 1500000000), rng);
        item.cultivationBonus = Mathf.RoundToInt(baseCultivation * efficiency);
    }


    static void RollPrice(StatItemData item, System.Random rng)
    {
        switch (item.itemType)
        {
            case ItemType.PhapBao:
                item.price = RollPriceRange(item.grade, 800, 2000, 8000, 25000, 80000, 300000, 5000000, 30000000, rng);
                break;

            case ItemType.CongPhap:
                item.price = RollPriceRange(item.grade, 3000, 10000, 50000, 180000, 700000, 3000000, 50000000, 500000000, rng);
                break;

            case ItemType.DanDuoc:
                RollPillPrice(item, rng);
                break;

            case ItemType.ThucPham:
                item.price = RollPriceRange(item.grade, 80, 300, 1000, 5000, 20000, 100000, 2000000, 20000000, rng);
                break;

            case ItemType.VatLieu:
                if (item.materialKind == MaterialKind.Herb)
                {
                    item.price = RollPriceRange(item.grade, 500, 2500, 10000, 50000, 300000, 2000000, 20000000, 300000000, rng);
                }
                else
                {
                    item.price = RollPriceRange(item.grade, 100, 500, 2000, 8000, 30000, 150000, 20000000, 100000000, rng);
                }
                break;

            default:
                item.price = RollPriceRange(item.grade, 100, 500, 1000, 5000, 10000, 50000, 1000000, 10000000, rng);
                break;
        }
    }

    static void RollPillPrice(StatItemData item, System.Random rng)
    {
        switch (item.pillKind)
        {
            case PillKind.Breakthrough:
                item.price = RollPriceRange(item.grade, 2500, 8000, 30000, 120000, 700000, 3000000, 30000000, 150000000, rng);
                break;

            case PillKind.Cultivation:
                item.price = RollPriceRange(item.grade, 1000, 4000, 15000, 60000, 200000, 900000, 10000000, 50000000, rng);
                break;

            case PillKind.PermanentAttack:
            case PillKind.PermanentDefense:
            case PillKind.PermanentMaxHP:
                item.price = RollPriceRange(item.grade, 3000, 12000, 60000, 250000, 1000000, 5000000, 100000000, 1000000000, rng);
                break;

            default:
                item.price = RollPriceRange(item.grade, 300, 1000, 4000, 15000, 50000, 180000, 10000000, 80000000, rng);
                break;
        }
    }

    static int RollPriceRange(
        ItemGrade grade,
        int haMin,
        int haMax,
        int trungMin,
        int trungMax,
        int thuongMin,
        int thuongMax,
        int tienMin,
        int tienMax,
        System.Random rng)
    {
        return RollPlain(
            grade,
            new IntRange(haMin, haMax),
            new IntRange(trungMin, trungMax),
            new IntRange(thuongMin, thuongMax),
            new IntRange(tienMin, tienMax),
            rng);
    }

    static int RollPlain(ItemGrade grade, IntRange ha, IntRange trung, IntRange thuong, IntRange tien, System.Random rng)
    {
        IntRange range = SelectRange(grade, ha, trung, thuong, tien);
        return Mathf.Max(1, rng.Next(range.Min, range.Max + 1));
    }
    static int Roll(ItemGrade grade, IntRange ha, IntRange trung, IntRange thuong, IntRange tien, System.Random rng)
    {
        IntRange range = SelectRange(grade, ha, trung, thuong, tien);
        int raw = rng.Next(range.Min, range.Max + 1);
        return Mathf.Max(0, Mathf.RoundToInt(raw * RollQuality(grade, rng)));
    }

    static IntRange SelectRange(ItemGrade grade, IntRange ha, IntRange trung, IntRange thuong, IntRange tien)
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

    static bool RollChance(ItemGrade grade, System.Random rng, float ha, float trung, float thuong, float tien)
    {
        float chance = ha;
        switch (grade)
        {
            case ItemGrade.Trung:
                chance = trung;
                break;
            case ItemGrade.Thuong:
                chance = thuong;
                break;
            case ItemGrade.Tien:
                chance = tien;
                break;
        }

        return rng.NextDouble() <= chance;
    }

    static float RollQuality(ItemGrade grade, System.Random rng)
    {
        float min = grade == ItemGrade.Tien ? ImmortalQualityMin : QualityMin;
        float max = grade == ItemGrade.Tien ? ImmortalQualityMax : QualityMax;
        return RollFloat(rng, min, max);
    }

    static float RollFloat(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    static float RollMaterialEfficiency(ItemGrade grade, System.Random rng)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return RollFloat(rng, 0.25f, 0.4f);
            case ItemGrade.Thuong:
                return RollFloat(rng, 0.3f, 0.5f);
            case ItemGrade.Tien:
                return RollFloat(rng, 0.35f, 0.6f);
            default:
                return RollFloat(rng, 0.2f, 0.35f);
        }
    }

    static void SetIntModifier(StatItemData item, StatType statType, int value)
    {
        if (value == 0)
        {
            return;
        }

        if (item.modifiers == null)
        {
            item.modifiers = new List<StatModifier>();
        }

        item.modifiers.Add(new StatModifier { statType = statType, intValue = value, floatValue = 0f });
    }

    static void RemoveBalancedModifiers(StatItemData item)
    {
        if (item.modifiers == null)
        {
            item.modifiers = new List<StatModifier>();
            return;
        }

        item.modifiers.RemoveAll(modifier =>
            modifier != null &&
            (modifier.statType == StatType.MaxHP ||
             modifier.statType == StatType.MoveSpeed));
    }

    static int BuildSeed(StatItemData item, int seed)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + seed;
            hash = hash * 31 + StableHash(item.name);
            hash = hash * 31 + StableHash(item.itemName);
            hash = hash * 31 + (int)item.itemType;
            hash = hash * 31 + (int)item.grade;
            return hash;
        }
    }

    static int StableHash(string value)
    {
        unchecked
        {
            int hash = (int)2166136261;

            if (string.IsNullOrEmpty(value))
            {
                return hash;
            }

            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }

            return hash;
        }
    }
}



