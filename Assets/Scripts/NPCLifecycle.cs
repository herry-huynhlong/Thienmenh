using UnityEngine;

public static class NpcLifeStageDefaults
{
    public const int BabyMaxAge = 3;
    public const int ChildMaxAge = 8;
    public const int YouthMaxAge = 18;
    public const int MiddleMaxAge = 45;
    public const int AdultMinAge = YouthMaxAge + 1;
    public const int ElderMinAge = MiddleMaxAge + 1;
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
public class NPCLifecycle : MonoBehaviour
{
    [Header("Age Thresholds")]
    public int babyMaxAge = NpcLifeStageDefaults.BabyMaxAge;
    public int childMaxAge = NpcLifeStageDefaults.ChildMaxAge;
    public int youthMaxAge = NpcLifeStageDefaults.YouthMaxAge;
    public int middleMaxAge = NpcLifeStageDefaults.MiddleMaxAge;

    [Header("Rapid Runtime Child Growth")]
    public bool autoEnableRapidGrowthForRuntimeFamilyChildren = false;
    public bool useRapidRuntimeGrowth;
    public int rapidGrowthStartAbsoluteDay = int.MinValue;
    [Min(1)] public int rapidGrowthDurationDays = 3;
    [Min(0.05f)] public float rapidGrowthBabyScale = 0.7f;
    [Min(0.05f)] public float rapidGrowthChildScale = 0.6f;
    public Vector3 rapidGrowthAdultScale = Vector3.zero;

    public NPCIdentity identity;
    public EntityProfile entityProfile;
    public NPCVisualResolver visualResolver;

    WorldTimeSystem subscribedTimeSystem;

    void Reset()
    {
        CacheReferences();
    }

    void Awake()
    {
        CacheReferences();
        EnsureRapidGrowthAdultScale();
        SyncLifeStageFromAge(true);
    }

    void OnEnable()
    {
        SubscribeToWorldTime(false);
        SyncLifeStageFromAge(true);
    }

    void Start()
    {
        SubscribeToWorldTime(true);
        SyncLifeStageFromAge(true);
    }

    void OnDisable()
    {
        UnsubscribeFromWorldTime();
    }

    void OnValidate()
    {
        CacheReferences();
        EnsureRapidGrowthAdultScale();
        SyncLifeStageFromAge(false);
    }

    public void ConfigureRapidRuntimeGrowth(
        int startAbsoluteDay,
        int durationDays,
        float babyScale,
        float childScale)
    {
        useRapidRuntimeGrowth = true;
        rapidGrowthStartAbsoluteDay = Mathf.Max(1, startAbsoluteDay);
        rapidGrowthDurationDays = Mathf.Max(1, durationDays);
        rapidGrowthBabyScale = Mathf.Max(0.05f, babyScale);
        rapidGrowthChildScale = Mathf.Max(0.05f, childScale);
        EnsureRapidGrowthAdultScale();
        SyncLifeStageFromAge(true);
    }

    public void SetAge(int newAge)
    {
        CacheReferences();

        if (identity == null)
        {
            return;
        }

        identity.SetCurrentAge(newAge);
        SyncLifeStageFromAge(true);
    }

    public void RefreshAgeNow(bool forceRefresh = false)
    {
        SyncLifeStageFromAge(forceRefresh);
    }

    public LifeStage ResolveLifeStage(int age)
    {
        age = Mathf.Max(0, age);

        if (age <= babyMaxAge)
        {
            return LifeStage.Baby;
        }

        if (age <= childMaxAge)
        {
            return LifeStage.Child;
        }

        if (age <= youthMaxAge)
        {
            return LifeStage.Youth;
        }

        if (age <= middleMaxAge)
        {
            return LifeStage.Middle;
        }

        return LifeStage.Old;
    }

    void SyncLifeStageFromAge(bool forceRefresh)
    {
        CacheReferences();
        EnsureRapidGrowthAdultScale();

        if (identity == null)
        {
            return;
        }

        bool identityHasAgeSource = HasIdentityAgeSource();
        bool profileHasAgeSource = HasProfileAgeSource();
        if (!identityHasAgeSource && profileHasAgeSource)
        {
            identity.age = entityProfile.identity.age;
            identity.birthAbsoluteDay =
                entityProfile.identity.birthAbsoluteDay;
            identity.hasBirthAbsoluteDay =
                entityProfile.identity.hasBirthAbsoluteDay;
            identityHasAgeSource = true;
        }

        if (!identityHasAgeSource)
        {
            return;
        }

        NPCManualAgeVisualLock ageVisualLock =
            NPCManualAgeVisualLock.FindOn(gameObject);
        if (ageVisualLock != null &&
            ageVisualLock.lockAge)
        {
            ageVisualLock.ApplyLockedIdentity(identity);

            if (entityProfile != null)
            {
                ageVisualLock.ApplyLockedEntityProfile(entityProfile);
            }

            if (visualResolver != null &&
                Application.isPlaying &&
                ageVisualLock.lockVisualController)
            {
                ageVisualLock.ApplyLocks();
            }

            return;
        }

        TryAutoEnableRapidGrowthForRuntimeChild();
        ApplyRapidRuntimeGrowth();

        int currentAge = identity.GetCurrentAge();
        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            entityProfile.identity.age = currentAge;
            entityProfile.identity.birthAbsoluteDay =
                identity.birthAbsoluteDay;
            entityProfile.identity.hasBirthAbsoluteDay = true;
        }

        ApplyRapidGrowthScale(identity.lifeStage);

        if (visualResolver != null &&
            Application.isPlaying)
        {
            visualResolver.RefreshVisual();
        }

        if (Application.isPlaying)
        {
            VillagerAI villager =
                GetComponent<VillagerAI>() ??
                GetComponentInParent<VillagerAI>(true) ??
                GetComponentInChildren<VillagerAI>(true);
            if (villager != null)
            {
                villager.SyncNpcIdentityData();
            }
        }
    }

    void ApplyRapidRuntimeGrowth()
    {
        if (!useRapidRuntimeGrowth ||
            identity == null)
        {
            return;
        }

        int currentDay = NpcAgeUtility.CurrentAbsoluteDay;
        if (rapidGrowthStartAbsoluteDay <= 0)
        {
            rapidGrowthStartAbsoluteDay = currentDay;
        }

        int adultAge = Mathf.Max(18, youthMaxAge);
        int elapsedDays =
            Mathf.Max(0, currentDay - rapidGrowthStartAbsoluteDay);
        int durationDays = Mathf.Max(1, rapidGrowthDurationDays);

        if (elapsedDays >= durationDays)
        {
            identity.SetCurrentAge(adultAge);
            if (entityProfile != null &&
                entityProfile.identity != null)
            {
                NpcAgeUtility.SetCurrentAge(
                    entityProfile.identity,
                    adultAge);
            }

            useRapidRuntimeGrowth = false;
            return;
        }

        int acceleratedAge = 0;
        if (elapsedDays > 0)
        {
            float t =
                durationDays <= 1
                    ? 1f
                    : Mathf.Clamp01(
                        (elapsedDays - 1f) /
                        Mathf.Max(1f, durationDays - 1f));
            acceleratedAge =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        Mathf.Lerp(
                            Mathf.Max(4, childMaxAge),
                            adultAge - 1,
                            t)),
                    Mathf.Max(4, childMaxAge),
                    adultAge - 1);
        }

        identity.SetCurrentAge(acceleratedAge);
        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            NpcAgeUtility.SetCurrentAge(
                entityProfile.identity,
                acceleratedAge);
        }
    }

    void ApplyRapidGrowthScale(LifeStage stage)
    {
        EnsureRapidGrowthAdultScale();

        if (rapidGrowthAdultScale != Vector3.zero)
        {
            transform.localScale = rapidGrowthAdultScale;
        }
    }

    void EnsureRapidGrowthAdultScale()
    {
        if (rapidGrowthAdultScale == Vector3.zero)
        {
            rapidGrowthAdultScale = transform.localScale;
        }
    }

    void TryAutoEnableRapidGrowthForRuntimeChild()
    {
        if (!autoEnableRapidGrowthForRuntimeFamilyChildren ||
            useRapidRuntimeGrowth ||
            identity == null)
        {
            return;
        }

        bool hasParents =
            !string.IsNullOrWhiteSpace(identity.fatherId) ||
            !string.IsNullOrWhiteSpace(identity.motherId);
        if (!hasParents)
        {
            return;
        }

        int currentAge = identity.GetCurrentAge();
        int adultAge = Mathf.Max(18, youthMaxAge);
        if (currentAge >= adultAge)
        {
            return;
        }

        useRapidRuntimeGrowth = true;
        if (rapidGrowthStartAbsoluteDay <= 0)
        {
            rapidGrowthStartAbsoluteDay = NpcAgeUtility.CurrentAbsoluteDay;
        }

        rapidGrowthDurationDays = Mathf.Max(1, rapidGrowthDurationDays);
        rapidGrowthBabyScale = Mathf.Max(0.05f, rapidGrowthBabyScale);
        rapidGrowthChildScale = Mathf.Max(0.05f, rapidGrowthChildScale);
    }

    bool HasIdentityAgeSource()
    {
        return identity != null &&
            (identity.hasBirthAbsoluteDay ||
             identity.age > 0 ||
             !string.IsNullOrWhiteSpace(identity.npcName) ||
             !string.IsNullOrWhiteSpace(identity.fatherId) ||
             !string.IsNullOrWhiteSpace(identity.motherId));
    }

    bool HasProfileAgeSource()
    {
        return entityProfile != null &&
            entityProfile.identity != null &&
            (entityProfile.identity.hasBirthAbsoluteDay ||
             entityProfile.identity.age > 0 ||
             !string.IsNullOrWhiteSpace(
                 entityProfile.identity.entityName));
    }

    void SubscribeToWorldTime(bool ensureInstance)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null && ensureInstance && Application.isPlaying)
        {
            timeSystem = WorldTimeSystem.EnsureInstance();
        }

        if (subscribedTimeSystem == timeSystem)
        {
            return;
        }

        UnsubscribeFromWorldTime();
        subscribedTimeSystem = timeSystem;
        if (subscribedTimeSystem != null)
        {
            subscribedTimeSystem.OnDayChanged += HandleDayChanged;
        }
    }

    void UnsubscribeFromWorldTime()
    {
        if (subscribedTimeSystem != null)
        {
            subscribedTimeSystem.OnDayChanged -= HandleDayChanged;
            subscribedTimeSystem = null;
        }
    }

    void HandleDayChanged(int absoluteDay)
    {
        SyncLifeStageFromAge(false);
    }

    void CacheReferences()
    {
        if (identity == null)
        {
            identity =
                GetComponent<NPCIdentity>() ??
                GetComponentInParent<NPCIdentity>(true) ??
                GetComponentInChildren<NPCIdentity>(true);
        }

        if (entityProfile == null)
        {
            entityProfile =
                GetComponent<EntityProfile>() ??
                GetComponentInParent<EntityProfile>(true) ??
                GetComponentInChildren<EntityProfile>(true);
        }

        if (visualResolver == null)
        {
            visualResolver =
                GetComponent<NPCVisualResolver>() ??
                GetComponentInParent<NPCVisualResolver>(true) ??
                GetComponentInChildren<NPCVisualResolver>(true);
        }
    }
}
