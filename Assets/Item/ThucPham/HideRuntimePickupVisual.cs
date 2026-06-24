using UnityEngine;

public class HideRuntimePickupVisual : MonoBehaviour
{
    [Header("Hide Visual Only")]
    public bool hideOnStart = true;
    public bool hideSpriteRenderer = true;
    public bool hideAnimator = true;
    public bool hideParticleSystem = true;

    void Start()
    {
        if (hideOnStart)
        {
            HideVisual();
        }
    }

    public void HideVisual()
    {
        if (hideSpriteRenderer)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        if (hideAnimator)
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);

            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = false;
            }
        }

        if (hideParticleSystem)
        {
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}