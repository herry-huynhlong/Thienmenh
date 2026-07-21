using UnityEngine;

[DisallowMultipleComponent]
public class NPCManualAgeVisualLock : MonoBehaviour
{
    public bool lockAge = true;
    public bool lockVisualController = true;
    public bool captureCurrentStateOnReset = true;

    [Min(0)] public int lockedAge;
    [HideInInspector] public LifeStage lockedLifeStage = LifeStage.Youth;
    [HideInInspector] public RuntimeAnimatorController lockedController;

    NPCIdentity identity;
    Animator animator;

    public static NPCManualAgeVisualLock FindOn(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        return owner.GetComponent<NPCManualAgeVisualLock>() ??
            owner.GetComponentInParent<NPCManualAgeVisualLock>(true) ??
            owner.GetComponentInChildren<NPCManualAgeVisualLock>(true);
    }

    void Reset()
    {
        CacheReferences();

        if (captureCurrentStateOnReset)
        {
            CaptureCurrentState();
        }
    }

    void Awake()
    {
        CacheReferences();
        EnsureCapturedState();
        ApplyLocks();
    }

    void OnValidate()
    {
        CacheReferences();

        if (!Application.isPlaying)
        {
            EnsureCapturedState();
        }
    }

    [ContextMenu("Capture Current State")]
    public void CaptureCurrentState()
    {
        CacheReferences();

        if (identity != null)
        {
            lockedAge = Mathf.Max(0, identity.age);
            lockedLifeStage = identity.lifeStage;
        }

        if (animator != null)
        {
            lockedController = animator.runtimeAnimatorController;
        }
    }

    public void ApplyLocks()
    {
        CacheReferences();

        if (lockAge && identity != null)
        {
            ApplyLockedIdentity(identity);
        }

        if (lockVisualController &&
            animator != null &&
            lockedController != null &&
            animator.runtimeAnimatorController != lockedController)
        {
            animator.runtimeAnimatorController = lockedController;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    public void ApplyLockedIdentity(NPCIdentity targetIdentity)
    {
        if (!lockAge || targetIdentity == null)
        {
            return;
        }

        targetIdentity.age = Mathf.Max(0, lockedAge);
        targetIdentity.birthAbsoluteDay =
            NpcAgeUtility.DeriveBirthAbsoluteDayFromCurrentAge(
                targetIdentity.age);
        targetIdentity.hasBirthAbsoluteDay = true;
        targetIdentity.lifeStage = ResolveLifeStage(targetIdentity.age);
    }

    public void ApplyLockedEntityProfile(EntityProfile profile)
    {
        if (!lockAge ||
            profile == null ||
            profile.identity == null)
        {
            return;
        }

        profile.identity.age = Mathf.Max(0, lockedAge);
        NpcAgeUtility.SetCurrentAge(profile.identity, profile.identity.age);
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

    void EnsureCapturedState()
    {
        if (!captureCurrentStateOnReset)
        {
            return;
        }

        bool missingAgeState = lockedAge <= 0 && lockedLifeStage == LifeStage.Youth;
        bool missingController = lockedController == null;
        if (!missingAgeState &&
            !missingController)
        {
            return;
        }

        CaptureCurrentState();
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
}
