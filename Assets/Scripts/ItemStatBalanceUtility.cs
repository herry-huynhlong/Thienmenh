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
        item.damageBonusPercent = 0;
        item.armorBonusPercent = 0;
        item.maxHpBonusPercent = 0;
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
                item.attackTechniqueCooldown = 4.5f;
                item.defenseTechniqueCooldown = 8f;
                item.movementTechniqueCooldown = 6f;
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
        item.damageBonusPercent = Roll(
            item.grade,
            new IntRange(20, 30),
            new IntRange(35, 50),
            new IntRange(60, 80),
            new IntRange(100, 140),
            rng);
        item.armorBonusPercent = Roll(
            item.grade,
            new IntRange(15, 25),
            new IntRange(30, 45),
            new IntRange(50, 70),
            new IntRange(80, 120),
            rng);
        item.maxHpBonusPercent = Roll(
            item.grade,
            new IntRange(5, 10),
            new IntRange(10, 18),
            new IntRange(18, 30),
            new IntRange(30, 50),
            rng);
        item.effectResistanceBonus = Roll(
            item.grade,
            new IntRange(0, 3),
            new IntRange(3, 8),
            new IntRange(8, 18),
            new IntRange(18, 30),
            rng);

        if (item.armorBonusPercent > item.damageBonusPercent)
        {
            item.equipmentSlot = EquipmentSlot.Armor;
            item.artifactKind = ArtifactKind.Armor;
        }
        else if (item.maxHpBonusPercent > item.damageBonusPercent)
        {
            item.equipmentSlot = EquipmentSlot.Accessory;
            item.artifactKind = ArtifactKind.Amulet;
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
            item.cultivationBonus = Roll(
                item.grade,
                new IntRange(10000, 35000),
                new IntRange(150000, 600000),
                new IntRange(2500000, 10000000),
                new IntRange(60000000, 350000000),
                rng);
        }
        else if (pillRoll <= 6)
        {
            item.pillKind = PillKind.Cultivation;
            item.cultivationBonus = Roll(
                item.grade,
                new IntRange(18000, 70000),
                new IntRange(280000, 1200000),
                new IntRange(4500000, 18000000),
                new IntRange(120000000, 700000000),
                rng);
        }
        else
        {
            item.pillKind = PillKind.Heal;
            item.hpBonus = Roll(
                item.grade,
                new IntRange(50, 140),
                new IntRange(180, 500),
                new IntRange(700, 2200),
                new IntRange(2500, 9000),
                rng);
        }

        if (RollChance(item.grade, rng, 0.12f, 0.18f, 0.25f, 0.35f))
        {
            item.damageBonusPercent = Roll(
                item.grade,
                new IntRange(4, 8),
                new IntRange(8, 14),
                new IntRange(14, 22),
                new IntRange(25, 40),
                rng);
        }

        if (RollChance(item.grade, rng, 0.16f, 0.22f, 0.3f, 0.45f))
        {
            item.maxHpBonusPercent = Roll(
                item.grade,
                new IntRange(3, 6),
                new IntRange(6, 10),
                new IntRange(10, 16),
                new IntRange(18, 28),
                rng);
        }

        if (RollChance(item.grade, rng, 0.08f, 0.12f, 0.18f, 0.26f))
        {
            item.armorBonusPercent = Roll(
                item.grade,
                new IntRange(3, 6),
                new IntRange(6, 10),
                new IntRange(10, 16),
                new IntRange(18, 28),
                rng);
        }
    }

    static void RollFood(StatItemData item, System.Random rng)
    {
        item.hpBonus = Roll(
            item.grade,
            new IntRange(12, 40),
            new IntRange(40, 120),
            new IntRange(140, 400),
            new IntRange(600, 1600),
            rng);
        item.cultivationBonus = Roll(
            item.grade,
            new IntRange(400, 1400),
            new IntRange(4000, 18000),
            new IntRange(70000, 320000),
            new IntRange(700000, 4200000),
            rng);

        if (RollChance(item.grade, rng, 0.2f, 0.25f, 0.3f, 0.4f))
        {
            item.damageBonusPercent = Roll(
                item.grade,
                new IntRange(5, 10),
                new IntRange(10, 16),
                new IntRange(16, 24),
                new IntRange(25, 40),
                rng);
            item.maxHpBonusPercent = Roll(
                item.grade,
                new IntRange(3, 6),
                new IntRange(6, 10),
                new IntRange(10, 16),
                new IntRange(16, 25),
                rng);
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

        int baseCultivation = Roll(
            item.grade,
            new IntRange(5000, 24000),
            new IntRange(90000, 420000),
            new IntRange(2200000, 12000000),
            new IntRange(70000000, 420000000),
            rng);
        item.cultivationBonus = Mathf.RoundToInt(baseCultivation * efficiency);

        if (RollChance(item.grade, rng, 0.1f, 0.14f, 0.2f, 0.28f))
        {
            item.damageBonusPercent = Roll(
                item.grade,
                new IntRange(2, 5),
                new IntRange(4, 8),
                new IntRange(8, 14),
                new IntRange(14, 22),
                rng);
        }
    }


    static void RollPrice(StatItemData item, System.Random rng)
    {
        switch (item.itemType)
        {
            case ItemType.PhapBao:
                item.price = RollPriceRange(item.grade, 1200, 3200, 10000, 32000, 100000, 380000, 3500000, 18000000, rng);
                break;

            case ItemType.CongPhap:
                item.price = RollPriceRange(item.grade, 4000, 14000, 60000, 220000, 900000, 3600000, 45000000, 280000000, rng);
                break;

            case ItemType.DanDuoc:
                RollPillPrice(item, rng);
                break;

            case ItemType.ThucPham:
                item.price = RollPriceRange(item.grade, 60, 220, 600, 2800, 9000, 45000, 250000, 1600000, rng);
                break;

            case ItemType.VatLieu:
                if (item.materialKind == MaterialKind.Herb)
                {
                    item.price = RollPriceRange(item.grade, 250, 1000, 3000, 15000, 35000, 180000, 1000000, 6000000, rng);
                }
                else
                {
                    item.price = RollPriceRange(item.grade, 100, 450, 1000, 6000, 12000, 70000, 300000, 2000000, rng);
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
                item.price = RollPriceRange(item.grade, 3000, 10000, 40000, 160000, 900000, 3600000, 35000000, 180000000, rng);
                break;

            case PillKind.Cultivation:
                item.price = RollPriceRange(item.grade, 1200, 5000, 18000, 70000, 260000, 1200000, 12000000, 65000000, rng);
                break;

            case PillKind.PermanentAttack:
            case PillKind.PermanentDefense:
            case PillKind.PermanentMaxHP:
                item.price = RollPriceRange(item.grade, 4000, 14000, 70000, 260000, 1200000, 5200000, 60000000, 320000000, rng);
                break;

            default:
                item.price = RollPriceRange(item.grade, 250, 900, 3500, 14000, 40000, 180000, 2000000, 12000000, rng);
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
             modifier.statType == StatType.MaxHPPercent ||
             modifier.statType == StatType.AttackPercent ||
             modifier.statType == StatType.DefensePercent ||
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



