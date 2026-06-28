using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ItemType
{
    DanDuoc,
    PhapBao,
    VatLieu,
    CongPhap,
    ThucPham
}

public enum ItemGrade
{
    Ha,
    Trung,
    Thuong,
    Tien
}

public enum CultivationManualMastery
{
    None,
    TieuThanh,
    TrungThanh,
    DaiThanh
}

public enum RawUsePolicy
{
    Allowed,
    Risky,
    Forbidden
}

public enum NpcItemIntent
{
    Auto,
    PreferUseRaw,
    PreferRefine,
    PreferSell,
    Keep
}

public enum ItemUseStyle
{
    Auto,
    Consumable,
    RawMaterial,
    DurableEquipment,
    StudyManual
}

public enum EquipmentSlot
{
    None,
    Weapon,
    Armor,
    Accessory
}

public enum ArtifactKind
{
    None,
    Sword,
    Saber,
    Spear,
    Bow,
    Staff,
    Armor,
    Robe,
    Shield,
    Ring,
    Amulet,
    Talisman
}

public enum PillKind
{
    None,
    Heal,
    Cultivation,
    Breakthrough,
    PermanentAttack,
    PermanentDefense,
    PermanentMaxHP,
    Detox,
    Poison,
    TribulationProtection
}

public enum ManualKind
{
    None,
    Attack,
    Defense,
    Movement,
    Cultivation,
    Mixed
}

public enum MaterialKind
{
    None,
    Herb,
    Ore,
    BeastCore,
    BeastPart,
    SpiritStone,
    CraftingPart
}

public enum FoodKind
{
    None,
    Meal,
    Meat,
    Fish,
    Grain,
    SpiritFruit
}
public enum ItemConversionType
{
    None,
    RefinePill,
    ForgeArtifact,
    Study,
    Sell
}

[System.Flags]
public enum ItemTargetType
{
    None = 0,
    Player = 1,
    Npc = 2,
    Monster = 4,
    All = Player | Npc | Monster
}

public enum StatType
{
    MaxHP,
    CurrentHP,
    Attack,
    Defense,
    EffectResistance,
    MoveSpeed,
    Damage,
    Cultivation,
    Breakthrough,
    Money,
    SpiritStone,
    Pill
}

[System.Serializable]
public class StatModifier
{
    public StatType statType;
    public int intValue;
    public float floatValue;
}

[CreateAssetMenu(
    fileName = "NewStatItem",
    menuName = "ThienMenh/Stat Item")]
public class StatItemData : ScriptableObject
{
    [Header("Info")]
    public string itemId;
    public string itemName;
    [TextArea]
    public string description;
    public ItemType itemType = ItemType.DanDuoc;
    public ItemGrade grade = ItemGrade.Ha;
    public ItemTargetType validTargets = ItemTargetType.All;
    public int price;
    public Sprite icon;
    public bool consumeOnUse = true;

    [Header("Phan Loai Chi Tiet")]
    public EquipmentSlot equipmentSlot = EquipmentSlot.None;
    public ArtifactKind artifactKind = ArtifactKind.None;
    public PillKind pillKind = PillKind.None;
    public ManualKind manualKind = ManualKind.None;
    public MaterialKind materialKind = MaterialKind.None;
    public FoodKind foodKind = FoodKind.None;

    [Header("Sử Dụng & Chuyển Hóa")]
    public ItemUseStyle useStyle = ItemUseStyle.Auto;
    public ItemConversionType conversionType = ItemConversionType.None;
    public bool canUseDirectly = true;
    public bool canBeSold = true;
    public bool canBeRefinedIntoPill;
    public bool canBeForgedIntoArtifact;
    public bool canBeStudied = true;
    [Range(0f, 1f)]
    public float rawUseEfficiency = 0.35f;
    [Range(0f, 1f)]
    public float useSuccessChance = 1f;

    [Header("Dùng Sống")]
    public RawUsePolicy rawUsePolicy = RawUsePolicy.Allowed;
    [Min(0)] public int rawToxicityDamage;

    [Header("Quyết Định NPC")]
    public NpcItemIntent npcIntent = NpcItemIntent.Auto;
    [Min(0f)] public float refineValueMultiplier = 1.5f;
    [Min(0f)] public float sellValueMultiplier = 1f;

    [Header("Độ Bền")]
    public int maxDurability;
    public int durabilityLossPerUse = 1;
    public bool breaksAtZero = true;

    [Header("Đan Dược")]
    public int hpBonus;
    public int cultivationBonus;
    public bool breakthroughRealm;

    [Header("Hiệu Ứng NPC Đặc Biệt")]
    public bool awakenVillagerToSmartNpc;
    public CultivationRealm awakenTargetRealm = CultivationRealm.QiRefining;
    [Range(1, 9)]
    public int awakenTargetStage = 1;

    [Header("Pháp Bảo")]
    public int damageBonus;
    public int armorBonus;
    public int effectResistanceBonus;

    [Header("Công Pháp")]
    public bool canBeTaught = true;
    public int studyProgressPerUse = 1;
    public float manualBreakAfterYears = 10f;
    [Range(0f, 1f)]
    public float tieuThanhPower = 0.3f;
    [Range(0f, 1f)]
    public float trungThanhPower = 0.6f;
    [Range(0f, 1f)]
    public float daiThanhPower = 1f;

    [Header("Hiệu Ứng Khi NPC Mua")]
    public bool playBuyEffect;
    public GameObject buyEffectPrefab;
    public Vector3 buyEffectOffset = new Vector3(0f, 0.15f, 0f);
    public float buyEffectScale = 1f;

    [Header("Hiệu ứng Khi Nhặt")]
    public bool playPickupEffect = true;
    public GameObject pickupEffectPrefab;
    public Vector3 pickupEffectOffset = new Vector3(0f, 0.15f, 0f);
    public float pickupEffectScale = 1f;

    [Header("Buff Tạm Thời")]
    public bool isTemporary;
    public float duration = 10f;

    [Header("Tùy Chỉnh Thêm")]
    public List<StatModifier> modifiers =
        new List<StatModifier>();

    public string ItemId =>
        string.IsNullOrWhiteSpace(itemId)
        ? name
        : itemId.Trim();

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureItemId();
    }

    void EnsureItemId()
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            itemId = itemId.Trim();
            return;
        }

        string path = AssetDatabase.GetAssetPath(this);
        string guid = !string.IsNullOrEmpty(path)
            ? AssetDatabase.AssetPathToGUID(path)
            : "";

        itemId = !string.IsNullOrEmpty(guid)
            ? guid
            : System.Guid.NewGuid().ToString("N");

        EditorUtility.SetDirty(this);
    }
#endif

    public List<StatModifier> GetAllModifiers()
    {
        return GetAllModifiers(1f);
    }

    public List<StatModifier> GetAllModifiers(float powerMultiplier)
    {
        List<StatModifier> result =
            new List<StatModifier>();

        float useMultiplier =
            GetDirectUsePowerMultiplier() *
            Mathf.Max(0f, powerMultiplier);

        AddModifier(
            result,
            StatType.CurrentHP,
            Mathf.RoundToInt(hpBonus * useMultiplier));

        AddModifier(
            result,
            StatType.Cultivation,
            Mathf.RoundToInt(cultivationBonus * useMultiplier));

        if (itemType == ItemType.VatLieu &&
            rawToxicityDamage > 0)
        {
            AddModifier(
                result,
                StatType.CurrentHP,
                -rawToxicityDamage);
        }

        if (breakthroughRealm)
        {
            AddModifier(result, StatType.Breakthrough, 1);
        }

        AddModifier(
            result,
            StatType.Attack,
            Mathf.RoundToInt(damageBonus * useMultiplier));

        AddModifier(
            result,
            StatType.Defense,
            Mathf.RoundToInt(armorBonus * useMultiplier));

        AddModifier(
            result,
            StatType.EffectResistance,
            Mathf.RoundToInt(effectResistanceBonus * useMultiplier));

        foreach (StatModifier modifier in modifiers)
        {
            if (modifier == null)
            {
                continue;
            }

            result.Add(
                new StatModifier
                {
                    statType = modifier.statType,
                    intValue = Mathf.RoundToInt(modifier.intValue * useMultiplier),
                    floatValue = modifier.floatValue * useMultiplier
                });
        }

        return result;
    }

    void AddModifier(
        List<StatModifier> targetModifiers,
        StatType statType,
        int value)
    {
        if (value == 0)
        {
            return;
        }

        targetModifiers.Add(
            new StatModifier
            {
                statType = statType,
                intValue = value
            });
    }

    public bool ApplyTo(GameObject target)
    {
        if (!CanUseOn(target))
        {
            return false;
        }

        if (isTemporary)
        {
            TimedStatItemBuff buff =
                target.AddComponent<TimedStatItemBuff>();

            buff.StartBuff(this);

            return true;
        }

        return ApplyTo(target, 1);
    }

    public bool RemoveFrom(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        return ApplyTo(target, -1);
    }

    public bool CanUseOn(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (IsBlockedCultivationPillForCommoner(target))
        {
            return false;
        }

        if (awakenVillagerToSmartNpc)
        {
            SmartNpcAI smartNpc =
                target.GetComponent<SmartNpcAI>();

            if (smartNpc != null &&
                smartNpc.enabled)
            {
                return false;
            }

            VillagerAI villager =
                target.GetComponent<VillagerAI>();

            return villager != null &&
                villager.enabled &&
                !villager.IsDead;
        }

        if (!CanUseDirectly())
        {
            return false;
        }

        return GetTargetType(target) != ItemTargetType.None &&
            (validTargets & GetTargetType(target)) != 0;
    }

    public bool CanUseDirectly()
    {
        return canUseDirectly &&
            rawUsePolicy != RawUsePolicy.Forbidden &&
            GetResolvedUseStyle() != ItemUseStyle.Auto;
    }

    public bool IsRiskyRawUse()
    {
        return rawUsePolicy == RawUsePolicy.Risky ||
            rawToxicityDamage > 0 ||
            useSuccessChance < 1f;
    }

    public bool ShouldNpcUseDirectly()
    {
        if (!CanUseDirectly())
        {
            return false;
        }

        if (npcIntent == NpcItemIntent.PreferUseRaw)
        {
            return !IsRiskyRawUse();
        }

        if (npcIntent == NpcItemIntent.PreferRefine ||
            npcIntent == NpcItemIntent.PreferSell ||
            npcIntent == NpcItemIntent.Keep)
        {
            return false;
        }

        ItemUseStyle resolvedStyle =
            GetResolvedUseStyle();

        if (resolvedStyle == ItemUseStyle.RawMaterial)
        {
            return !canBeRefinedIntoPill &&
                rawUseEfficiency >= 0.75f &&
                !IsRiskyRawUse();
        }

        if (resolvedStyle == ItemUseStyle.Consumable)
        {
            return GetNpcUseScore() > 0f &&
                !IsRiskyRawUse();
        }

        if (resolvedStyle == ItemUseStyle.DurableEquipment)
        {
            return GetEquipmentUseScore() > 0f;
        }

        if (resolvedStyle == ItemUseStyle.StudyManual)
        {
            return canBeStudied &&
                GetNpcUseScore() > 0f &&
                !IsRiskyRawUse();
        }

        return false;
    }

    public bool ShouldNpcPreferRefine()
    {
        if (!canBeRefinedIntoPill)
        {
            return false;
        }

        return conversionType == ItemConversionType.RefinePill ||
            npcIntent == NpcItemIntent.PreferRefine ||
            (npcIntent == NpcItemIntent.Auto &&
            GetResolvedUseStyle() == ItemUseStyle.RawMaterial &&
            (rawUseEfficiency < 0.75f || IsRiskyRawUse()));
    }

    public bool ShouldNpcPreferForge()
    {
        if (!canBeForgedIntoArtifact)
        {
            return false;
        }

        return conversionType == ItemConversionType.ForgeArtifact ||
            (npcIntent == NpcItemIntent.Auto &&
            GetResolvedUseStyle() == ItemUseStyle.RawMaterial &&
            GetNpcConversionScore() > GetNpcUseScore());
    }

    public bool ShouldNpcPreferStudy()
    {
        if (!canBeStudied)
        {
            return false;
        }

        return conversionType == ItemConversionType.Study ||
            (npcIntent == NpcItemIntent.Auto &&
            GetResolvedUseStyle() == ItemUseStyle.StudyManual &&
            GetNpcUseScore() > 0f &&
            !IsRiskyRawUse());
    }

    public bool ShouldNpcPreferSell()
    {
        if (!canBeSold)
        {
            return false;
        }

        if (npcIntent == NpcItemIntent.PreferSell ||
            conversionType == ItemConversionType.Sell)
        {
            return true;
        }

        ItemUseStyle resolvedStyle =
            GetResolvedUseStyle();

        if (npcIntent == NpcItemIntent.Auto &&
            (resolvedStyle == ItemUseStyle.DurableEquipment ||
            resolvedStyle == ItemUseStyle.StudyManual) &&
            GetNpcUseScore() > 0f)
        {
            return false;
        }

        return npcIntent == NpcItemIntent.Auto &&
            GetNpcSellScore() > Mathf.Max(
                GetNpcUseScore(),
                GetNpcConversionScore());
    }

    public float GetEquipmentUseScore()
    {
        return Mathf.Max(0f, damageBonus) +
            Mathf.Max(0f, armorBonus) +
            Mathf.Max(0f, effectResistanceBonus);
    }

    public float GetNpcUseScore()
    {
        if (!CanUseDirectly())
        {
            return 0f;
        }

        float score =
            Mathf.Max(0f, hpBonus) * 0.5f +
            Mathf.Max(0f, cultivationBonus) * 1.2f +
            Mathf.Max(0f, damageBonus) +
            Mathf.Max(0f, armorBonus) +
            Mathf.Max(0f, effectResistanceBonus);

        if (breakthroughRealm)
        {
            score += 20f;
        }

        if (GetResolvedUseStyle() == ItemUseStyle.RawMaterial)
        {
            score *= Mathf.Clamp01(rawUseEfficiency);
        }

        if (GetResolvedUseStyle() == ItemUseStyle.StudyManual)
        {
            score += Mathf.Max(0, studyProgressPerUse) * 2f;
        }

        score *= Mathf.Clamp01(useSuccessChance);
        score -= rawToxicityDamage * 1.5f;

        return Mathf.Max(0f, score);
    }

    public float GetNpcConversionScore()
    {
        return Mathf.Max(
            GetNpcRefineScore(),
            GetNpcForgeScore());
    }

    public float GetNpcRefineScore()
    {
        if (!canBeRefinedIntoPill)
        {
            return 0f;
        }

        return Mathf.Max(0f, price) *
            Mathf.Max(0f, refineValueMultiplier);
    }

    public float GetNpcSellScore()
    {
        if (!canBeSold)
        {
            return 0f;
        }

        return Mathf.Max(0f, price) *
            Mathf.Max(0f, sellValueMultiplier);
    }

    public float GetNpcForgeScore()
    {
        if (!canBeForgedIntoArtifact)
        {
            return 0f;
        }

        return Mathf.Max(0f, price) *
            Mathf.Max(0f, refineValueMultiplier);
    }

    public EquipmentSlot GetResolvedEquipmentSlot()
    {
        if (itemType != ItemType.PhapBao)
        {
            return EquipmentSlot.None;
        }

        if (equipmentSlot != EquipmentSlot.None)
        {
            return equipmentSlot;
        }

        switch (artifactKind)
        {
            case ArtifactKind.Sword:
            case ArtifactKind.Saber:
            case ArtifactKind.Spear:
            case ArtifactKind.Bow:
            case ArtifactKind.Staff:
                return EquipmentSlot.Weapon;
            case ArtifactKind.Armor:
            case ArtifactKind.Robe:
            case ArtifactKind.Shield:
                return EquipmentSlot.Armor;
            case ArtifactKind.Ring:
            case ArtifactKind.Amulet:
            case ArtifactKind.Talisman:
                return EquipmentSlot.Accessory;
        }

        if (damageBonus >= Mathf.Max(armorBonus, effectResistanceBonus))
        {
            return EquipmentSlot.Weapon;
        }

        return EquipmentSlot.Armor;
    }

    public bool IsManualBroken(ItemStack stack)
    {
        if (itemType != ItemType.CongPhap ||
            stack == null)
        {
            return false;
        }

        float usedYears = Mathf.Max(
            stack.manualUseYears,
            GameSaveSystem.GetManualUseYears(this));

        return manualBreakAfterYears > 0f &&
            usedYears >= manualBreakAfterYears;
    }
    public ItemUseStyle GetResolvedUseStyle()
    {
        if (useStyle != ItemUseStyle.Auto)
        {
            return useStyle;
        }

        switch (itemType)
        {
            case ItemType.DanDuoc:
            case ItemType.ThucPham:
                return ItemUseStyle.Consumable;
            case ItemType.VatLieu:
                return ItemUseStyle.RawMaterial;
            case ItemType.PhapBao:
                return ItemUseStyle.DurableEquipment;
            case ItemType.CongPhap:
                return ItemUseStyle.StudyManual;
            default:
                return ItemUseStyle.Auto;
        }
    }

    float GetDirectUsePowerMultiplier()
    {
        if (itemType == ItemType.VatLieu &&
            canBeRefinedIntoPill)
        {
            return Mathf.Clamp01(rawUseEfficiency);
        }

        return 1f;
    }

    public bool ConsumesWhenUsed()
    {
        return itemType == ItemType.DanDuoc ||
            itemType == ItemType.ThucPham ||
            consumeOnUse;
    }

    public bool UsesDurability()
    {
        return itemType == ItemType.PhapBao ||
            maxDurability > 0;
    }

    public int GetMaxDurability()
    {
        if (!UsesDurability())
        {
            return 0;
        }

        if (maxDurability > 0)
        {
            return maxDurability;
        }

        switch (grade)
        {
            case ItemGrade.Trung:
                return 180;
            case ItemGrade.Thuong:
                return 500;
            case ItemGrade.Tien:
                return 1200;
            default:
                return 80;
        }
    }

    public int GetDurabilityLossPerUse()
    {
        return Mathf.Max(1, durabilityLossPerUse);
    }

    public bool RollUseSuccess()
    {
        return Random.value <= Mathf.Clamp01(useSuccessChance);
    }

    public bool ApplyTo(GameObject target, int direction)
    {
        return ApplyTo(target, direction, 1f);
    }

    public bool ApplyTo(
        GameObject target,
        int direction,
        float powerMultiplier)
    {
        if (target == null)
        {
            return false;
        }

        if (IsBlockedCultivationPillForCommoner(target))
        {
            return false;
        }

        if (awakenVillagerToSmartNpc &&
            direction > 0)
        {
            return NpcCultivationAwakeningUtility.TryConvertVillagerToSmartNpc(
                target,
                awakenTargetRealm,
                awakenTargetStage);
        }

        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            characterStats.ApplyItem(
                this,
                direction,
                powerMultiplier);
            return true;
        }

        SmartNpcAI npc =
            target.GetComponent<SmartNpcAI>();

        if (npc != null)
        {
            npc.ApplyItem(this, direction, powerMultiplier);
            return true;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            villager.ApplyItem(this, direction, powerMultiplier);
            return true;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            monster.ApplyItem(this, direction, powerMultiplier);
            return true;
        }

        PlayerHealth player =
            target.GetComponent<PlayerHealth>();

        if (player != null)
        {
            player.ApplyItem(this, direction, powerMultiplier);
            return true;
        }

        return false;
    }

    bool IsBlockedCultivationPillForCommoner(GameObject target)
    {
        if (target == null ||
            itemType != ItemType.DanDuoc ||
            awakenVillagerToSmartNpc)
        {
            return false;
        }

        if (pillKind != PillKind.Cultivation &&
            pillKind != PillKind.Breakthrough)
        {
            return false;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();
        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        return villager != null &&
            villager.enabled &&
            (smartNpc == null || !smartNpc.enabled);
    }

    ItemTargetType GetTargetType(GameObject target)
    {
        if (target.GetComponent<SmartNpcAI>() != null)
        {
            return ItemTargetType.Npc;
        }

        if (target.GetComponent<VillagerAI>() != null)
        {
            return ItemTargetType.Npc;
        }

        if (target.GetComponent<MonsterAI>() != null)
        {
            return ItemTargetType.Monster;
        }

        if (target.GetComponent<PlayerHealth>() != null ||
            target.GetComponent<CharacterStats>() != null)
        {
            return ItemTargetType.Player;
        }

        return ItemTargetType.None;
    }
}
