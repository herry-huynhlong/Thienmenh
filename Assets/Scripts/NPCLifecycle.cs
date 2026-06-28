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
    public NPCVisualResolver visualResolver;

    void Reset()
    {
        CacheReferences();
    }

    void Awake()
    {
        CacheReferences();
        SyncLifeStageFromAge(true);
    }

    void Start()
    {
        SyncLifeStageFromAge(true);
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

        identity.age = Mathf.Max(0, newAge);
        SyncLifeStageFromAge(true);
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

        LifeStage newStage = ResolveLifeStage(identity.age);
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

    void CacheReferences()
    {
        if (identity == null)
        {
            identity =
                GetComponent<NPCIdentity>() ??
                GetComponentInParent<NPCIdentity>(true) ??
                GetComponentInChildren<NPCIdentity>(true);
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
