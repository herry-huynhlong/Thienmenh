using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class NPCVisualResolver : MonoBehaviour
{
    public Animator animator;
    public NPCIdentity identity;

    public static NPCVisualResolver EnsureOn(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        NPCVisualResolver resolver =
            owner.GetComponent<NPCVisualResolver>() ??
            owner.GetComponentInParent<NPCVisualResolver>(true) ??
            owner.GetComponentInChildren<NPCVisualResolver>(true);

        if (resolver == null && Application.isPlaying)
        {
            resolver = owner.AddComponent<NPCVisualResolver>();
        }

        if (resolver != null)
        {
            resolver.CacheReferences();
        }

        return resolver;
    }

    void Reset()
    {
        CacheReferences();
    }

    void Awake()
    {
        CacheReferences();
        RefreshVisual();
    }

    void Start()
    {
        RefreshVisual();
    }

    void OnValidate()
    {
        CacheReferences();
    }

    public void RefreshVisual()
    {
        CacheReferences();

        if (identity == null)
        {
            return;
        }

        RuntimeAnimatorController sceneController =
            animator != null ? animator.runtimeAnimatorController : null;

        NPCManualAgeVisualLock ageVisualLock =
            NPCManualAgeVisualLock.FindOn(gameObject);
        if (ageVisualLock != null &&
            ageVisualLock.lockVisualController)
        {
            ageVisualLock.ApplyLocks();
            return;
        }

        RuntimeAnimatorController controller =
            identity.visualProfile != null
                ? identity.visualProfile.GetController(
                    identity.gender,
                    ResolveVisualLifeStage())
                : sceneController;

        if (controller == null)
        {
            return;
        }

        if (animator == null)
        {
            return;
        }

        if (AreControllersEquivalent(sceneController, controller))
        {
            return;
        }

        NPCVisualAnimation visualAnimation =
            animator.GetComponent<NPCVisualAnimation>();
        if (visualAnimation == null)
        {
            visualAnimation =
                GetComponent<NPCVisualAnimation>() ??
                GetComponentInParent<NPCVisualAnimation>(true) ??
                GetComponentInChildren<NPCVisualAnimation>(true);
        }

        if (visualAnimation != null)
        {
            visualAnimation.RebindAnimatorController(controller);
        }
        else
        {
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    LifeStage ResolveVisualLifeStage()
    {
        if (!Application.isPlaying || identity == null)
        {
            return identity != null ? identity.lifeStage : LifeStage.Youth;
        }

        int currentAge = identity.GetCurrentAge();
        NPCLifecycle lifecycle =
            GetComponent<NPCLifecycle>() ??
            GetComponentInParent<NPCLifecycle>(true) ??
            GetComponentInChildren<NPCLifecycle>(true);

        if (lifecycle != null)
        {
            return lifecycle.ResolveLifeStage(currentAge);
        }

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

    static bool AreControllersEquivalent(
        RuntimeAnimatorController currentController,
        RuntimeAnimatorController targetController)
    {
        if (currentController == null || targetController == null)
        {
            return false;
        }

        if (currentController == targetController)
        {
            return true;
        }

        AnimatorOverrideController currentOverride =
            currentController as AnimatorOverrideController;
        if (currentOverride != null &&
            currentOverride.runtimeAnimatorController == targetController)
        {
            return true;
        }

        AnimatorOverrideController targetOverride =
            targetController as AnimatorOverrideController;
        if (targetOverride != null &&
            targetOverride.runtimeAnimatorController == currentController)
        {
            return true;
        }

        return false;
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

        if (animator == null)
        {
            animator =
                GetComponent<Animator>() ??
                GetComponentInChildren<Animator>(true) ??
                GetComponentInParent<Animator>(true);
        }
    }
}
