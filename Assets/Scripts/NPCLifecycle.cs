using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
public class NPCLifecycle : MonoBehaviour
{
    [Header("Age Thresholds")]
    public int babyMaxAge = 3;
    public int childMaxAge = 8;
    public int youthMaxAge = 18;
    public int middleMaxAge = 45;

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
        SyncLifeStageFromAge(true);
    }

    void OnEnable()
    {
        SubscribeToWorldTime(false);
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
        SyncLifeStageFromAge(false);
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

        int currentAge = identity.GetCurrentAge();
        if (entityProfile != null &&
            entityProfile.identity != null)
        {
            entityProfile.identity.age = currentAge;
            entityProfile.identity.birthAbsoluteDay =
                identity.birthAbsoluteDay;
            entityProfile.identity.hasBirthAbsoluteDay = true;
        }

        LifeStage newStage = ResolveLifeStage(currentAge);
        bool changed = identity.lifeStage != newStage;
        identity.lifeStage = newStage;

        if ((changed || forceRefresh) &&
            visualResolver != null &&
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

    bool HasIdentityAgeSource()
    {
        return identity != null &&
            (identity.hasBirthAbsoluteDay ||
             identity.age > 0 ||
             identity.lifeStage != LifeStage.Youth ||
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
