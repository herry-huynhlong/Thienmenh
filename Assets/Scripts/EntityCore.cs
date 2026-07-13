using System;
using System.Collections.Generic;
using UnityEngine;

public enum EntityKind
{
    Player,
    Commoner,
    Cultivator,
    Beast,
    Animal
}

public enum EntityGender
{
    Unknown,
    Male,
    Female
}

public enum TalentGrade
{
    MortalBody,
    SpiritRoot,
    FireSpiritRoot,
    SwordHeart,
    SaintBody,
    ChildOfHeaven
}

public enum EntityMood
{
    Calm,
    Happy,
    Angry,
    Sad,
    Lonely,
    Afraid,
    Tired
}

public enum EntityGoal
{
    Survive,
    Work,
    Socialize,
    Trade,
    Cultivate,
    Hunt,
    Rest,
    Revenge,
    Flee,
    GuardTerritory
}

[Serializable]
public class EntityIdentity
{
    public string entityName;
    public EntityKind kind;
    public EntityGender gender;
    public int age;
}

[Serializable]
public class EntityStats
{
    public CultivationRealm realm;
    [Range(1, 9)]
    public int realmStage = 1;
    public int maxHP = 100;
    public int currentHP = 100;
    public int attack = 10;
    public int defense = 5;
    public int effectResistance;
    public float moveSpeed = 1.6f;
    public int cultivationExp;
    [InspectorName("Linh Thạch")]
    public int money;
    public int spiritStone;

    public int RealmPower
    {
        get
        {
            int power = 1;
            for (int i = 0; i < (int)realm; i++)
            {
                power *= 10;
            }

            return Mathf.Max(1, power) * Mathf.Max(1, realmStage);
        }
    }
}

[Serializable]
public class EntityTalent
{
    public TalentGrade grade;
    [Range(1, 100)]
    public int comprehension = 10;
    public float cultivationSpeed = 1f;
    public float combatMultiplier = 1f;
    public float luck = 1f;
}

[Serializable]
public class EntityPersonality
{
    [Range(0, 100)] public int greed;
    [Range(0, 100)] public int bravery;
    [Range(0, 100)] public int kindness;
    [Range(0, 100)] public int sociability;
    [Range(0, 100)] public int diligence;
    [Range(0, 100)] public int hotTemper;
    [Range(0, 100)] public int funSeeking;
    [Range(0, 100)] public int loneliness;
    [Range(0, 100)] public int cultivationDesire;
}

[Serializable]
public class EntityEmotion
{
    public EntityMood mood = EntityMood.Calm;
    [Range(0, 100)] public float happiness = 50f;
    [Range(0, 100)] public float anger;
    [Range(0, 100)] public float sadness;
    [Range(0, 100)] public float fear;
    [Range(0, 100)] public float loneliness;
}

[Serializable]
public class EntityNeeds
{
    [Range(0, 100)] public float hunger;
    [Range(0, 100)] public float fatigue;
    [Range(0, 100)] public float socialNeed;
    [Range(0, 100)] public float cultivationNeed;
}

[Serializable]
public class EntityMemory
{
    public string subjectId;
    public string eventType;
    public int emotionalWeight;
    public float worldHour;
    public int worldDay;
}

[Serializable]
public class EntityRelationship
{
    public string targetId;
    public int friendship;
    public int hatred;
    public int fear;
    public int respect;
    public int love;
}

public class EntityProfile : MonoBehaviour
{
    public bool generateOnAwake = true;
    public bool lockGeneratedValues;
    public EntityKind kind = EntityKind.Commoner;
    public EntityIdentity identity = new EntityIdentity();
    public EntityStats stats = new EntityStats();
    public EntityTalent talent = new EntityTalent();
    public EntityPersonality personality = new EntityPersonality();
    public EntityEmotion emotion = new EntityEmotion();
    public EntityNeeds needs = new EntityNeeds();
    public EntityGoal currentGoal = EntityGoal.Survive;
    public List<EntityMemory> memories = new List<EntityMemory>();
    public List<EntityRelationship> relationships = new List<EntityRelationship>();

    void Awake()
    {
        if (generateOnAwake && !lockGeneratedValues)
        {
            EntityGenerator.FillProfile(this, kind);
            lockGeneratedValues = true;
        }
    }

    [ContextMenu("Reload Generated Profile")]
    public void ReloadGeneratedProfile()
    {
        EntityGenerator.FillProfile(this, kind);
        lockGeneratedValues = true;
        memories.Clear();
        relationships.Clear();
    }

    public void Remember(string subjectId, string eventType, int emotionalWeight)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        memories.Add(new EntityMemory
        {
            subjectId = subjectId,
            eventType = eventType,
            emotionalWeight = emotionalWeight,
            worldHour = timeSystem != null ? timeSystem.CurrentHour : 0f,
            worldDay = timeSystem != null ? timeSystem.CurrentDay : 0
        });
    }

    public EntityRelationship GetRelationship(string targetId)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            return null;
        }

        for (int i = 0; i < relationships.Count; i++)
        {
            if (relationships[i].targetId == targetId)
            {
                return relationships[i];
            }
        }

        EntityRelationship relationship = new EntityRelationship
        {
            targetId = targetId
        };
        relationships.Add(relationship);
        return relationship;
    }
}

public static class EntityGenerator
{
    static readonly string[] maleNames =
    {
        "Lý Thanh", "Trần An", "Mạc Phong", "Hàn Vũ", "Đạo Minh", "Lâm Kiệt"
    };

    static readonly string[] femaleNames =
    {
        "Linh Nhi", "Ngọc Dao", "Thanh Vân", "Tiểu Mai", "Lan Anh", "Bích Hà"
    };

    static readonly string[] beastNames =
    {
        "Lang Yêu", "Hổ Yêu", "Xà Tinh", "Ưng Yêu", "Hắc Báo", "Độc Lang"
    };

    static readonly string[] animalNames =
    {
        "Lá»™c Tráº¯ng", "HÆ°Æ¡u Núi", "Thá» XÃ¡m", "DÃª Suá»‘i", "TrÃ¢u Rá»«ng", "Ngá»±a Sá»«ng"
    };

    public static EntityProfile EnsureProfile(GameObject owner, EntityKind kind)
    {
        EntityProfile profile = owner.GetComponent<EntityProfile>();
        if (profile == null)
        {
            profile = owner.AddComponent<EntityProfile>();
            profile.kind = kind;
        }

        if (!profile.lockGeneratedValues)
        {
            profile.kind = kind;
            FillProfile(profile, kind);
            profile.lockGeneratedValues = true;
        }

        return profile;
    }

    public static void FillProfile(EntityProfile profile, EntityKind kind)
    {
        if (profile == null)
        {
            return;
        }

        profile.kind = kind;
        FillIdentity(profile.identity, kind);
        FillTalent(profile.talent, kind);
        FillStats(profile.stats, profile.talent, kind);
        FillPersonality(profile.personality, kind);
        FillEmotion(profile.emotion);
        FillNeeds(profile.needs, profile.personality, kind);
        if (kind == EntityKind.Beast)
        {
            profile.currentGoal = EntityGoal.Hunt;
        }
        else if (kind == EntityKind.Cultivator)
        {
            profile.currentGoal = EntityGoal.Cultivate;
        }
        else
        {
            profile.currentGoal = EntityGoal.Work;
        }
    }

    static void FillIdentity(EntityIdentity identity, EntityKind kind)
    {
        identity.kind = kind;
        identity.gender = kind == EntityKind.Beast
            ? EntityGender.Unknown
            : WeightedGender();
        identity.age = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(1, 80)
            : UnityEngine.Random.Range(14, 80);

        if (kind == EntityKind.Beast)
        {
            identity.entityName = beastNames[UnityEngine.Random.Range(0, beastNames.Length)];
            return;
        }

        string[] names = identity.gender == EntityGender.Female ? femaleNames : maleNames;
        identity.entityName = names[UnityEngine.Random.Range(0, names.Length)];
    }

    static EntityGender WeightedGender()
    {
        return UnityEngine.Random.value < 0.5f ? EntityGender.Male : EntityGender.Female;
    }

    static void FillTalent(EntityTalent talent, EntityKind kind)
    {
        talent.grade = WeightedTalent();
        float gradePower = TalentPower(talent.grade);
        talent.comprehension = Mathf.Clamp(
            Mathf.RoundToInt(UnityEngine.Random.Range(8f, 45f) * gradePower),
            1,
            100);
        talent.cultivationSpeed = gradePower;
        talent.combatMultiplier = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(0.95f, 1.2f)
            : UnityEngine.Random.Range(0.9f, 1.15f);
        talent.luck = UnityEngine.Random.Range(0.9f, 1.1f) * gradePower;
    }

    static TalentGrade WeightedTalent()
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.60f) return TalentGrade.MortalBody;
        if (roll < 0.85f) return TalentGrade.SpiritRoot;
        if (roll < 0.93f) return TalentGrade.FireSpiritRoot;
        if (roll < 0.97f) return TalentGrade.SwordHeart;
        if (roll < 0.99f) return TalentGrade.SaintBody;
        return TalentGrade.ChildOfHeaven;
    }

    static float TalentPower(TalentGrade grade)
    {
        switch (grade)
        {
            case TalentGrade.SpiritRoot:
                return 1.05f;
            case TalentGrade.FireSpiritRoot:
                return 1.1f;
            case TalentGrade.SwordHeart:
                return 1.15f;
            case TalentGrade.SaintBody:
                return 1.2f;
            case TalentGrade.ChildOfHeaven:
                return 1.3f;
            default:
                return 1f;
        }
    }

    static void FillStats(EntityStats stats, EntityTalent talent, EntityKind kind)
    {
        stats.realm = WeightedRealm(kind);
        stats.realmStage = UnityEngine.Random.Range(1, 10);

        float talentPower = TalentPower(talent.grade);
        int majorRealmIndex = Mathf.Max(0, (int)stats.realm);
        int minorStageIndex = Mathf.Clamp(stats.realmStage, 1, CultivationProgression.MaxStage) - 1;
        float baseMoveSpeed;
        int baseMoney;
        int baseSpiritStone;

        if (kind == EntityKind.Beast)
        {
            baseMoveSpeed = UnityEngine.Random.Range(1.6f, 3.2f);
            baseMoney = 0;
            baseSpiritStone = 0;
        }
        else if (kind == EntityKind.Cultivator)
        {
            baseMoveSpeed = UnityEngine.Random.Range(1.2f, 2.1f);
            baseMoney = UnityEngine.Random.Range(5, 220);
            baseSpiritStone = UnityEngine.Random.Range(0, 12);
        }
        else
        {
            baseMoveSpeed = UnityEngine.Random.Range(1.0f, 1.8f);
            baseMoney = UnityEngine.Random.Range(2, 140);
            baseSpiritStone = UnityEngine.Random.Range(0, 5);
        }

        if (kind == EntityKind.Beast)
        {
            stats.maxHP =
                CombatStatCalculator.ClampToInt(
                    CombatStatCalculator.CalculateMonsterHp(
                        majorRealmIndex,
                        minorStageIndex));
            stats.attack =
                CombatStatCalculator.ClampToInt(
                    CombatStatCalculator.CalculateMonsterAttack(
                        majorRealmIndex,
                        minorStageIndex));
            stats.defense =
                CombatStatCalculator.ClampToInt(
                    CombatStatCalculator.CalculateMonsterDefense(
                        majorRealmIndex,
                        minorStageIndex));
        }
        else
        {
            stats.maxHP =
                CombatStatCalculator.ClampToInt(
                    CombatStatCalculator.CalculateNpcHp(
                        majorRealmIndex,
                        minorStageIndex));
            stats.attack =
                CombatStatCalculator.ClampToInt(
                    CombatStatCalculator.CalculateNpcAttack(
                        majorRealmIndex,
                        minorStageIndex));
            stats.defense =
                CombatStatCalculator.ClampToInt(
                    CombatStatCalculator.CalculateNpcDefense(
                        majorRealmIndex,
                        minorStageIndex));
        }

        stats.maxHP = Mathf.Max(1, stats.maxHP);
        stats.attack = Mathf.Max(1, stats.attack);
        stats.defense = Mathf.Max(0, stats.defense);
        stats.currentHP = stats.maxHP;
        stats.effectResistance = Mathf.RoundToInt(UnityEngine.Random.Range(0, 8) * talentPower);
        stats.moveSpeed = baseMoveSpeed;
        stats.cultivationExp = UnityEngine.Random.Range(0, 80) * Mathf.Max(1, (int)stats.realm + 1);
        stats.money = baseMoney;
        stats.spiritStone = baseSpiritStone;
    }

    static CultivationRealm WeightedRealm(EntityKind kind)
    {
        float roll = UnityEngine.Random.value;
        if (kind == EntityKind.Beast)
        {
            if (roll < 0.55f) return CultivationRealm.Mortal;
            if (roll < 0.82f) return CultivationRealm.QiRefining;
            if (roll < 0.94f) return CultivationRealm.Foundation;
            if (roll < 0.985f) return CultivationRealm.GoldenCore;
            if (roll < 0.997f) return CultivationRealm.NascentSoul;
            return CultivationRealm.SoulFormation;
        }

        if (kind == EntityKind.Commoner)
        {
            if (roll < 0.72f) return CultivationRealm.Mortal;
            if (roll < 0.92f) return CultivationRealm.QiRefining;
            if (roll < 0.985f) return CultivationRealm.Foundation;
            if (roll < 0.997f) return CultivationRealm.GoldenCore;
            if (roll < 0.999f) return CultivationRealm.NascentSoul;
            return CultivationRealm.SoulFormation;
        }

        if (roll < 0.70f) return CultivationRealm.QiRefining;
        if (roll < 0.90f) return CultivationRealm.Foundation;
        if (roll < 0.99f) return CultivationRealm.GoldenCore;
        return CultivationRealm.NascentSoul;
    }

    static void FillPersonality(EntityPersonality personality, EntityKind kind)
    {
        personality.greed = UnityEngine.Random.Range(0, 101);
        personality.bravery = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(35, 101)
            : kind == EntityKind.Commoner
                ? UnityEngine.Random.Range(5, 71)
                : UnityEngine.Random.Range(0, 101);
        personality.kindness = kind == EntityKind.Beast ? 0 : UnityEngine.Random.Range(25, 101);
        personality.sociability = kind == EntityKind.Beast
            ? 0
            : kind == EntityKind.Commoner
                ? UnityEngine.Random.Range(45, 101)
                : UnityEngine.Random.Range(10, 85);
        personality.diligence = kind == EntityKind.Commoner
            ? UnityEngine.Random.Range(40, 101)
            : UnityEngine.Random.Range(10, 101);
        personality.hotTemper = UnityEngine.Random.Range(0, 101);
        personality.funSeeking = kind == EntityKind.Beast
            ? 0
            : kind == EntityKind.Commoner
                ? UnityEngine.Random.Range(35, 101)
                : UnityEngine.Random.Range(0, 65);
        personality.loneliness = kind == EntityKind.Beast
            ? 0
            : kind == EntityKind.Commoner
                ? UnityEngine.Random.Range(20, 91)
                : UnityEngine.Random.Range(0, 70);
        personality.cultivationDesire = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(0, 40)
            : kind == EntityKind.Commoner
                ? UnityEngine.Random.Range(0, 35)
                : UnityEngine.Random.Range(45, 101);
    }

    static void FillEmotion(EntityEmotion emotion)
    {
        emotion.happiness = UnityEngine.Random.Range(35f, 75f);
        emotion.anger = UnityEngine.Random.Range(0f, 25f);
        emotion.sadness = UnityEngine.Random.Range(0f, 25f);
        emotion.fear = UnityEngine.Random.Range(0f, 20f);
        emotion.loneliness = UnityEngine.Random.Range(0f, 45f);
        emotion.mood = EntityMood.Calm;
    }

    static void FillNeeds(EntityNeeds needs, EntityPersonality personality, EntityKind kind)
    {
        needs.hunger = UnityEngine.Random.Range(0f, 35f);
        needs.fatigue = UnityEngine.Random.Range(0f, 35f);
        needs.socialNeed = kind == EntityKind.Beast
            ? 0f
            : kind == EntityKind.Commoner
                ? Mathf.Clamp(UnityEngine.Random.Range(10f, 60f) + personality.sociability * 0.3f, 0f, 100f)
                : Mathf.Clamp(UnityEngine.Random.Range(0f, 45f) + personality.sociability * 0.25f, 0f, 100f);
        needs.cultivationNeed = Mathf.Clamp(
            UnityEngine.Random.Range(0f, kind == EntityKind.Commoner ? 20f : 35f) + personality.cultivationDesire * 0.35f,
            0f,
            100f);
    }
}
