using System;
using System.Collections.Generic;
using UnityEngine;

public enum EntityKind
{
    Player,
    Villager,
    Cultivator,
    Beast
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
    public EntityKind kind = EntityKind.Villager;
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
        "Ly Thanh", "Tran An", "Mac Phong", "Han Vu", "Dao Minh", "Lam Kiet"
    };

    static readonly string[] femaleNames =
    {
        "Linh Nhi", "Ngoc Dao", "Thanh Van", "Tieu Mai", "Lan Anh", "Bich Ha"
    };

    static readonly string[] beastNames =
    {
        "Lang Yeu", "Ho Yeu", "Xa Tinh", "Ung Yeu", "Hac Bao", "Doc Lang"
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
        profile.currentGoal = kind == EntityKind.Beast ? EntityGoal.Hunt : EntityGoal.Survive;
    }

    static void FillIdentity(EntityIdentity identity, EntityKind kind)
    {
        identity.kind = kind;
        identity.gender = kind == EntityKind.Beast
            ? EntityGender.Unknown
            : WeightedGender();
        identity.age = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(1, 80)
            : UnityEngine.Random.Range(14, 91);

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
            ? UnityEngine.Random.Range(0.8f, 1.6f)
            : UnityEngine.Random.Range(0.8f, 1.2f) * gradePower;
        talent.luck = UnityEngine.Random.Range(0.7f, 1.3f) * gradePower;
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
                return 1.35f;
            case TalentGrade.FireSpiritRoot:
                return 1.8f;
            case TalentGrade.SwordHeart:
                return 2.2f;
            case TalentGrade.SaintBody:
                return 3f;
            case TalentGrade.ChildOfHeaven:
                return 5f;
            default:
                return 1f;
        }
    }

    static void FillStats(EntityStats stats, EntityTalent talent, EntityKind kind)
    {
        stats.realm = WeightedRealm(kind);
        stats.realmStage = UnityEngine.Random.Range(1, 10);

        int realmPower = 1;
        for (int i = 0; i < (int)stats.realm; i++)
        {
            realmPower *= 10;
        }

        float talentPower = TalentPower(talent.grade);
        int baseHp = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(70, 180)
            : UnityEngine.Random.Range(70, 130);
        int baseAttack = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(8, 22)
            : UnityEngine.Random.Range(4, 16);

        stats.maxHP = Mathf.Max(1, Mathf.RoundToInt(baseHp * realmPower * talentPower));
        stats.currentHP = stats.maxHP;
        stats.attack = Mathf.Max(1, Mathf.RoundToInt(baseAttack * realmPower * talent.combatMultiplier));
        stats.defense = Mathf.Max(0, Mathf.RoundToInt(UnityEngine.Random.Range(1, 8) * realmPower * 0.7f));
        stats.effectResistance = Mathf.RoundToInt(UnityEngine.Random.Range(0, 8) * talentPower);
        stats.moveSpeed = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(1.6f, 3.2f)
            : UnityEngine.Random.Range(1.2f, 2.1f);
        stats.cultivationExp = UnityEngine.Random.Range(0, 80) * Mathf.Max(1, (int)stats.realm + 1);
        stats.money = kind == EntityKind.Beast ? 0 : UnityEngine.Random.Range(5, 220);
        stats.spiritStone = kind == EntityKind.Beast ? 0 : UnityEngine.Random.Range(0, 12);
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

        if (roll < 0.50f) return CultivationRealm.Mortal;
        if (roll < 0.80f) return CultivationRealm.QiRefining;
        if (roll < 0.92f) return CultivationRealm.Foundation;
        if (roll < 0.97f) return CultivationRealm.GoldenCore;
        if (roll < 0.99f) return CultivationRealm.NascentSoul;
        if (roll < 0.998f) return CultivationRealm.SoulFormation;
        return CultivationRealm.Tribulation;
    }

    static void FillPersonality(EntityPersonality personality, EntityKind kind)
    {
        personality.greed = UnityEngine.Random.Range(0, 101);
        personality.bravery = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(35, 101)
            : UnityEngine.Random.Range(0, 101);
        personality.kindness = kind == EntityKind.Beast ? 0 : UnityEngine.Random.Range(0, 101);
        personality.sociability = kind == EntityKind.Beast ? 0 : UnityEngine.Random.Range(0, 101);
        personality.diligence = UnityEngine.Random.Range(0, 101);
        personality.hotTemper = UnityEngine.Random.Range(0, 101);
        personality.funSeeking = kind == EntityKind.Beast ? 0 : UnityEngine.Random.Range(0, 101);
        personality.loneliness = kind == EntityKind.Beast ? 0 : UnityEngine.Random.Range(0, 101);
        personality.cultivationDesire = kind == EntityKind.Beast
            ? UnityEngine.Random.Range(0, 40)
            : UnityEngine.Random.Range(0, 101);
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
            : Mathf.Clamp(UnityEngine.Random.Range(0f, 45f) + personality.sociability * 0.25f, 0f, 100f);
        needs.cultivationNeed = Mathf.Clamp(
            UnityEngine.Random.Range(0f, 35f) + personality.cultivationDesire * 0.35f,
            0f,
            100f);
    }
}
