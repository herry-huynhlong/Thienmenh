using UnityEngine;

public enum Gender
{
    Male,
    Female
}

public enum LifeStage
{
    Baby,
    Child,
    Youth,
    Middle,
    Old
}

public static class NpcAgeUtility
{
    public const int DaysPerYear = 1;

    public static int CurrentAbsoluteDay
    {
        get
        {
            WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
            if (timeSystem != null)
            {
                return Mathf.Max(1, timeSystem.CurrentAbsoluteDay);
            }

            WorldTimeSystem[] loadedSystems =
                Object.FindObjectsByType<WorldTimeSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < loadedSystems.Length; i++)
            {
                WorldTimeSystem loadedSystem = loadedSystems[i];
                if (loadedSystem != null)
                {
                    return Mathf.Max(
                        1,
                        loadedSystem.CurrentAbsoluteDay);
                }
            }

            if (GameSaveSystem.TryLoadWorldTimeAbsoluteDay(
                    out int savedAbsoluteDay,
                    out _))
            {
                return Mathf.Max(1, savedAbsoluteDay);
            }

            return 1;
        }
    }

    public static int DeriveBirthAbsoluteDayFromCurrentAge(int currentAge)
    {
        return ClampToInt(
            (long)CurrentAbsoluteDay -
            (long)Mathf.Max(0, currentAge) * DaysPerYear);
    }

    public static int DeriveBirthAbsoluteDayFromWorldStartAge(int worldStartAge)
    {
        return ClampToInt(
            1L -
            (long)Mathf.Max(0, worldStartAge) * DaysPerYear);
    }

    public static int CalculateAge(int birthAbsoluteDay)
    {
        long elapsedDays =
            (long)CurrentAbsoluteDay - birthAbsoluteDay;
        if (elapsedDays <= 0L)
        {
            return 0;
        }

        long age = elapsedDays / DaysPerYear;
        return age >= int.MaxValue ? int.MaxValue : (int)age;
    }

    public static int GetCurrentAge(EntityIdentity identity)
    {
        if (identity == null)
        {
            return 0;
        }

        if (!identity.hasBirthAbsoluteDay)
        {
            identity.birthAbsoluteDay =
                DeriveBirthAbsoluteDayFromCurrentAge(identity.age);
            identity.hasBirthAbsoluteDay = true;
        }

        identity.age = CalculateAge(identity.birthAbsoluteDay);
        return identity.age;
    }

    public static void SetCurrentAge(EntityIdentity identity, int currentAge)
    {
        if (identity == null)
        {
            return;
        }

        identity.age = Mathf.Max(0, currentAge);
        identity.birthAbsoluteDay =
            DeriveBirthAbsoluteDayFromCurrentAge(identity.age);
        identity.hasBirthAbsoluteDay = true;
    }

    static int ClampToInt(long value)
    {
        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)value;
    }
}

[DisallowMultipleComponent]
public class NPCIdentity : MonoBehaviour
{
    [Header("Identity")]
    public string npcId;
    public string npcName;
    public Gender gender = Gender.Male;
    public int age;
    [Tooltip("Absolute world day used to calculate age. The companion flag keeps day 0 valid.")]
    public int birthAbsoluteDay;
    [HideInInspector]
    public bool hasBirthAbsoluteDay;
    public LifeStage lifeStage = LifeStage.Youth;
    public NPCVisualProfile visualProfile;
    public string homeId;
    [Header("Travel")]
    public bool allowAutomaticGateTravel = true;

    [Header("Family")]
    public string fatherId;
    public string motherId;
    public string spouseId;

    void Awake()
    {
        EnsureNpcId();
        SyncDerivedFieldsFromAge();
    }

    public int GetCurrentAge()
    {
        NPCManualAgeVisualLock ageVisualLock =
            NPCManualAgeVisualLock.FindOn(gameObject);
        if (ageVisualLock != null &&
            ageVisualLock.lockAge)
        {
            ageVisualLock.ApplyLockedIdentity(this);
            return age;
        }

        EnsureBirthAbsoluteDay();
        age = NpcAgeUtility.CalculateAge(birthAbsoluteDay);
        return age;
    }

    public void SetCurrentAge(int currentAge)
    {
        NPCManualAgeVisualLock ageVisualLock =
            NPCManualAgeVisualLock.FindOn(gameObject);
        if (ageVisualLock != null &&
            ageVisualLock.lockAge)
        {
            ageVisualLock.ApplyLockedIdentity(this);
            return;
        }

        age = Mathf.Max(0, currentAge);
        birthAbsoluteDay =
            NpcAgeUtility.DeriveBirthAbsoluteDayFromCurrentAge(age);
        hasBirthAbsoluteDay = true;
    }

    public void EnsureBirthAbsoluteDay()
    {
        NPCManualAgeVisualLock ageVisualLock =
            NPCManualAgeVisualLock.FindOn(gameObject);
        if (ageVisualLock != null &&
            ageVisualLock.lockAge)
        {
            ageVisualLock.ApplyLockedIdentity(this);
            return;
        }

        if (hasBirthAbsoluteDay)
        {
            return;
        }

        birthAbsoluteDay =
            NpcAgeUtility.DeriveBirthAbsoluteDayFromCurrentAge(age);
        hasBirthAbsoluteDay = true;
    }

    void OnValidate()
    {
        if (!gameObject.scene.IsValid())
        {
            return;
        }

        EnsureNpcId();
        SyncDerivedFieldsFromAge();
    }

    void SyncDerivedFieldsFromAge()
    {
        age = Mathf.Max(0, age);
        birthAbsoluteDay =
            NpcAgeUtility.DeriveBirthAbsoluteDayFromCurrentAge(age);
        hasBirthAbsoluteDay = true;
        lifeStage = ResolveLifeStage(age);
    }

    static LifeStage ResolveLifeStage(int currentAge)
    {
        if (currentAge <= NpcLifeStageDefaults.BabyMaxAge)
        {
            return LifeStage.Baby;
        }

        if (currentAge <= NpcLifeStageDefaults.ChildMaxAge)
        {
            return LifeStage.Child;
        }

        if (currentAge <= NpcLifeStageDefaults.YouthMaxAge)
        {
            return LifeStage.Youth;
        }

        if (currentAge <= NpcLifeStageDefaults.MiddleMaxAge)
        {
            return LifeStage.Middle;
        }

        return LifeStage.Old;
    }

    void EnsureNpcId()
    {
        if (!string.IsNullOrWhiteSpace(npcId) &&
            !HasDuplicateNpcId(npcId))
        {
            return;
        }

        npcId = System.Guid.NewGuid().ToString("N");
    }

    bool HasDuplicateNpcId(string candidateId)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return false;
        }

        NPCIdentity[] identities =
            FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include);

        for (int i = 0; i < identities.Length; i++)
        {
            NPCIdentity identity = identities[i];
            if (identity == null || identity == this)
            {
                continue;
            }

            if (string.Equals(
                    identity.npcId,
                    candidateId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public static class NpcGateTravelPolicy
{
    public static bool AllowsAutomaticGateTravel(GameObject actor)
    {
        if (actor == null)
        {
            return true;
        }

        NPCIdentity identity =
            actor.GetComponent<NPCIdentity>() ??
            actor.GetComponentInParent<NPCIdentity>(true) ??
            actor.GetComponentInChildren<NPCIdentity>(true);

        return identity == null ||
            identity.allowAutomaticGateTravel;
    }
}
