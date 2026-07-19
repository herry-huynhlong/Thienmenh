using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThienKiepStrikePrefab : MonoBehaviour
{
    [Serializable]
    public struct FrameRef
    {
        public Sprite sprite;
    }

    [Header("Audio")]
    public AudioSource cloudRumbleAudio;
    public AudioSource thunderAudio;

    [Header("Loi Kiep Visual")]
    public SpriteRenderer loiKiepRenderer;
    public SpriteRenderer loiKiepBlendRenderer;
    public FrameRef[] loiKiepFrames = new FrameRef[10];
    public Vector3 loiKiepLocalOffset;
    public bool centerFramesHorizontally = true;
    public bool forceSpriteUnlitMaterial = true;
    public string sortingLayerName = "Effects";
    public int sortingOrder = 500;
    [Min(0.5f)] public float gatherMinDurationScaledSeconds = 3f;
    [Min(0.5f)] public float gatherMaxDurationScaledSeconds = 5f;
    [Min(0.01f)] public float gatherFrameDurationScaledSeconds = 0.08f;
    [Min(0.01f)] public float frameCrossfadeScaledSeconds = 0.16f;
    [Min(0.01f)] public float strikeFrameDurationScaledSeconds = 0.06f;
    [Min(0.01f)] public float dissipateFrameDurationScaledSeconds = 0.08f;
    [Min(1)] public int strikeLoopCount = 2;
    public bool destroyAfterSequence = true;
    public bool autoAlignStrikeFramesToSpriteBottom = true;
    [Min(0f)] public float strikeImpactBottomPaddingWorld = 0f;
    [Min(0f)] public float strikeImpactToCenterWorld = 1.64f;

    [Header("Standalone Damage")]
    public int damage = 80;
    public float damageRadius = 0.9f;
    public LayerMask damageLayers;

    bool gatheringComplete;
    bool dissipating;
    bool standaloneStarted;
    Vector3 impactPosition;
    bool hasImpactPosition;
    Vector3 gatherPosition;
    Vector3 strikePosition;
    bool hasGatherPosition;
    bool hasStrikePosition;

    static Material cachedSpriteUnlitMaterial;

    void Awake()
    {
        EnsureRenderer();
        SetRendererVisible(false);
    }

    public void Play(int newDamage, LayerMask newDamageLayers)
    {
        Play(newDamage, newDamageLayers, transform.position);
    }

    public void Play(
        int newDamage,
        LayerMask newDamageLayers,
        Vector3 newImpactPosition)
    {
        damage = newDamage;
        damageLayers = newDamageLayers;
        impactPosition = newImpactPosition;
        hasImpactPosition = true;

        if (standaloneStarted)
        {
            return;
        }

        standaloneStarted = true;
        StartCoroutine(PlayStandaloneRoutine());
    }

    public void PrepareAt(Vector3 newImpactPosition)
    {
        PrepareAt(newImpactPosition, newImpactPosition);
    }

    public void PrepareAt(
        Vector3 newGatherPosition,
        Vector3 newStrikePosition)
    {
        impactPosition = newStrikePosition;
        hasImpactPosition = true;
        gatherPosition = newGatherPosition;
        strikePosition = newStrikePosition;
        hasGatherPosition = true;
        hasStrikePosition = true;
        transform.position = newGatherPosition;
        EnsureRenderer();
        SetRendererVisible(false);
        gatheringComplete = false;
        dissipating = false;
    }

    public IEnumerator BeginLoiKiep(Vector3 newImpactPosition)
    {
        yield return BeginLoiKiep(newImpactPosition, newImpactPosition);
    }

    public IEnumerator BeginLoiKiep(
        Vector3 newGatherPosition,
        Vector3 newStrikePosition)
    {
        PrepareAt(newGatherPosition, newStrikePosition);

        if (cloudRumbleAudio != null)
        {
            cloudRumbleAudio.Stop();
            cloudRumbleAudio.Play();
        }

        float gatherFrameDuration =
            ResolveGatherFrameDurationScaledSeconds();

        for (int i = 0; i <= 7; i++)
        {
            yield return ShowFrameForDuration(
                i,
                gatherFrameDuration,
                smoothTransition: i > 0);
        }

        SetFrame(7);
        gatheringComplete = true;
    }

    public IEnumerator PlayStrikeFlash(Action onImpact = null)
    {
        EnsureRenderer();

        if (!gatheringComplete)
        {
            yield return BeginLoiKiep(GetImpactPosition());
        }

        bool impactTriggered = false;
        int loops = Mathf.Max(1, strikeLoopCount);

        for (int i = 0; i < loops; i++)
        {
            SetFrame(8);

            if (!impactTriggered)
            {
                impactTriggered = true;
                if (thunderAudio != null)
                {
                    thunderAudio.Stop();
                    thunderAudio.Play();
                }

                onImpact?.Invoke();
            }

            yield return GameTime.WaitForScaledSeconds(
                strikeFrameDurationScaledSeconds);

            SetFrame(9);
            yield return GameTime.WaitForScaledSeconds(
                strikeFrameDurationScaledSeconds);
        }

        SetFrame(7);
    }

    public IEnumerator EndLoiKiep()
    {
        if (dissipating)
        {
            yield break;
        }

        dissipating = true;

        if (cloudRumbleAudio != null)
        {
            cloudRumbleAudio.Stop();
        }

        for (int i = 7; i >= 0; i--)
        {
            yield return ShowFrameForDuration(
                i,
                dissipateFrameDurationScaledSeconds,
                smoothTransition: i < 7);
        }

        SetRendererVisible(false);

        if (destroyAfterSequence)
        {
            Destroy(gameObject);
        }
    }

    IEnumerator PlayStandaloneRoutine()
    {
        yield return BeginLoiKiep(GetImpactPosition());
        yield return PlayStrikeFlash(DamageAround);
        yield return EndLoiKiep();
    }

    void DamageAround()
    {
        if (damage <= 0)
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                GetImpactPosition(),
                damageRadius,
                damageLayers);

        HashSet<GameObject> damagedObjects = new HashSet<GameObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                !DamageSystem.TryResolveReceiver(
                    hit.gameObject,
                    out IDamageable damageable) ||
                damageable.IsDead)
            {
                continue;
            }

            GameObject root = damageable.DamageTransform != null
                ? damageable.DamageTransform.gameObject
                : hit.gameObject;

            if (root == null ||
                !damagedObjects.Add(root))
            {
                continue;
            }

            DamageContext context = DamageContext.Environment(
                damage,
                this,
                DamageType.HeavenlyTribulation,
                "loi_kiep_strike_prefab",
                GetImpactPosition());
            DamageSystem.Apply(damageable, context);
        }
    }

    void EnsureRenderer()
    {
        if (loiKiepRenderer != null)
        {
            ApplyRendererSettings(loiKiepRenderer, 0);
            EnsureBlendRenderer();
            return;
        }

        loiKiepRenderer =
            GetComponent<SpriteRenderer>();
        if (loiKiepRenderer == null)
        {
            Transform existing =
                transform.Find("LoiKiepVisual");
            GameObject rendererObject =
                existing != null
                    ? existing.gameObject
                    : new GameObject("LoiKiepVisual");
            if (rendererObject.transform.parent != transform)
            {
                rendererObject.transform.SetParent(transform, false);
            }

            loiKiepRenderer =
                rendererObject.GetComponent<SpriteRenderer>();
            if (loiKiepRenderer == null)
            {
                loiKiepRenderer =
                    rendererObject.AddComponent<SpriteRenderer>();
            }

            rendererObject.transform.localScale = Vector3.one;
        }

        ApplyRendererSettings(loiKiepRenderer, 0);
        EnsureBlendRenderer();
    }

    void EnsureBlendRenderer()
    {
        if (loiKiepBlendRenderer == null)
        {
            Transform existing =
                transform.Find("LoiKiepBlendVisual");
            GameObject rendererObject =
                existing != null
                    ? existing.gameObject
                    : new GameObject("LoiKiepBlendVisual");
            if (rendererObject.transform.parent != transform)
            {
                rendererObject.transform.SetParent(transform, false);
            }

            loiKiepBlendRenderer =
                rendererObject.GetComponent<SpriteRenderer>();
            if (loiKiepBlendRenderer == null)
            {
                loiKiepBlendRenderer =
                    rendererObject.AddComponent<SpriteRenderer>();
            }

            rendererObject.transform.localScale = Vector3.one;
        }

        ApplyRendererSettings(loiKiepBlendRenderer, -1);
        loiKiepBlendRenderer.enabled = false;
    }

    void ApplyRendererSettings(
        SpriteRenderer renderer,
        int sortingOrderOffset)
    {
        if (renderer == null)
        {
            return;
        }

        if (forceSpriteUnlitMaterial)
        {
            Material material = GetSpriteUnlitMaterial();
            if (material != null &&
                renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
            }
        }

        renderer.sortingOrder = sortingOrder + sortingOrderOffset;
        if (!string.IsNullOrWhiteSpace(sortingLayerName) &&
            (SortingLayer.NameToID(sortingLayerName) != 0 ||
            string.Equals(
                sortingLayerName,
                "Default",
                StringComparison.Ordinal)))
        {
            renderer.sortingLayerName = sortingLayerName;
        }
    }

    Material GetSpriteUnlitMaterial()
    {
        if (cachedSpriteUnlitMaterial != null)
        {
            return cachedSpriteUnlitMaterial;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        cachedSpriteUnlitMaterial =
            new Material(shader)
            {
                name = "Runtime_ThienKiep_SpriteUnlit"
            };
        return cachedSpriteUnlitMaterial;
    }

    void SetFrame(int index)
    {
        EnsureRenderer();

        if (!TryGetFrameSprite(index, out Sprite sprite))
        {
            return;
        }

        SetRendererFrame(loiKiepRenderer, sprite, index);
        SetRendererAlpha(loiKiepRenderer, 1f);
        if (loiKiepBlendRenderer != null)
        {
            loiKiepBlendRenderer.enabled = false;
        }
        SetRendererVisible(true);
    }

    void SetRendererVisible(bool visible)
    {
        if (loiKiepRenderer != null)
        {
            loiKiepRenderer.enabled = visible;
        }

        if (!visible &&
            loiKiepBlendRenderer != null)
        {
            loiKiepBlendRenderer.enabled = false;
        }
    }

    Vector3 GetImpactPosition()
    {
        return hasImpactPosition
            ? impactPosition
            : transform.position;
    }

    Vector3 ResolveFrameWorldPosition(int frameIndex)
    {
        if (frameIndex >= 8 &&
            hasStrikePosition)
        {
            return strikePosition +
                Vector3.up * ResolveStrikeFrameCenterOffset(frameIndex);
        }

        if (hasGatherPosition)
        {
            return gatherPosition;
        }

        if (hasStrikePosition)
        {
            return strikePosition;
        }

        return transform.position;
    }

    float ResolveStrikeFrameCenterOffset(int frameIndex)
    {
        if (!autoAlignStrikeFramesToSpriteBottom ||
            !TryGetFrameSprite(frameIndex, out Sprite sprite) ||
            sprite == null)
        {
            return strikeImpactToCenterWorld;
        }

        float centerToBottomWorld =
            Mathf.Max(0f, -sprite.bounds.min.y);
        return centerToBottomWorld +
            Mathf.Max(0f, strikeImpactBottomPaddingWorld);
    }

    float ResolveGatherFrameDurationScaledSeconds()
    {
        const int gatherFrameCount = 8;

        float minDuration =
            Mathf.Max(0.5f, gatherMinDurationScaledSeconds);
        float maxDuration =
            Mathf.Max(minDuration, gatherMaxDurationScaledSeconds);

        if (maxDuration <= minDuration + 0.001f)
        {
            return minDuration / gatherFrameCount;
        }

        float totalDuration =
            UnityEngine.Random.Range(minDuration, maxDuration);
        return totalDuration / gatherFrameCount;
    }

    IEnumerator ShowFrameForDuration(
        int index,
        float totalDurationScaledSeconds,
        bool smoothTransition)
    {
        EnsureRenderer();

        if (!TryGetFrameSprite(index, out Sprite nextSprite))
        {
            yield return GameTime.WaitForScaledSeconds(
                totalDurationScaledSeconds);
            yield break;
        }

        float totalDuration =
            Mathf.Max(0f, totalDurationScaledSeconds);
        bool canBlend =
            smoothTransition &&
            loiKiepRenderer != null &&
            loiKiepBlendRenderer != null &&
            loiKiepRenderer.enabled &&
            loiKiepRenderer.sprite != null &&
            loiKiepRenderer.sprite != nextSprite;

        if (!canBlend)
        {
            SetFrame(index);
            if (totalDuration > 0f)
            {
                yield return GameTime.WaitForScaledSeconds(totalDuration);
            }

            yield break;
        }

        Sprite previousSprite = loiKiepRenderer.sprite;
        Color previousColor = loiKiepRenderer.color;

        loiKiepBlendRenderer.sprite = previousSprite;
        ApplyRendererSettings(loiKiepBlendRenderer, -1);
        if (loiKiepBlendRenderer.transform != transform)
        {
            loiKiepBlendRenderer.transform.localPosition = loiKiepLocalOffset;
        }
        SetRendererAlpha(loiKiepBlendRenderer, previousColor.a);
        loiKiepBlendRenderer.enabled = true;

        SetRendererFrame(loiKiepRenderer, nextSprite, index);
        SetRendererAlpha(loiKiepRenderer, 0f);
        SetRendererVisible(true);

        float blendDuration =
            Mathf.Min(
                totalDuration,
                Mathf.Max(0.01f, frameCrossfadeScaledSeconds));
        float elapsed = 0f;

        while (elapsed < blendDuration)
        {
            elapsed += GameTime.ScaledDeltaSeconds;
            float t = blendDuration <= 0.001f
                ? 1f
                : Mathf.Clamp01(elapsed / blendDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            SetRendererAlpha(loiKiepRenderer, eased);
            SetRendererAlpha(loiKiepBlendRenderer, 1f - eased);
            yield return null;
        }

        SetRendererAlpha(loiKiepRenderer, 1f);
        loiKiepBlendRenderer.enabled = false;

        float remainingDuration =
            totalDuration - blendDuration;
        if (remainingDuration > 0f)
        {
            yield return GameTime.WaitForScaledSeconds(remainingDuration);
        }
    }

    bool TryGetFrameSprite(int index, out Sprite sprite)
    {
        sprite = null;

        if (loiKiepFrames == null ||
            index < 0 ||
            index >= loiKiepFrames.Length)
        {
            return false;
        }

        sprite = loiKiepFrames[index].sprite;
        return sprite != null;
    }

    void SetRendererFrame(
        SpriteRenderer renderer,
        Sprite sprite,
        int frameIndex)
    {
        if (renderer == null ||
            sprite == null)
        {
            return;
        }

        renderer.sprite = sprite;
        ApplyRendererSettings(
            renderer,
            renderer == loiKiepBlendRenderer ? -1 : 0);

        Vector3 localPosition = loiKiepLocalOffset;
        Vector3 frameWorldPosition =
            ResolveFrameWorldPosition(frameIndex);

        if (renderer.transform == transform)
        {
            transform.position =
                frameWorldPosition + localPosition;
        }
        else
        {
            transform.position = frameWorldPosition;
            renderer.transform.localPosition = localPosition;
        }
    }

    void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.color = color;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetImpactPosition(), damageRadius);
    }
}
