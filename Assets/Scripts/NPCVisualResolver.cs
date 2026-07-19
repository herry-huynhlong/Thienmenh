using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class NPCVisualResolver : MonoBehaviour
{
    public Animator animator;
    public NPCIdentity identity;

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

        RuntimeAnimatorController controller =
            identity.visualProfile != null
                ? identity.visualProfile.GetController(
                    identity.gender,
                    identity.lifeStage)
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
