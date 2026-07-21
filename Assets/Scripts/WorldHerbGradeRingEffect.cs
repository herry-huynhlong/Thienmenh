using UnityEngine;

[DisallowMultipleComponent]
public class WorldHerbGradeRingEffect : MonoBehaviour
{
    const string RingChildPrefix = "Ring_";
    static readonly Color[] ThuongSpectrumColors =
    {
        new Color(1f, 0.28f, 0.28f, 1f),
        new Color(1f, 0.58f, 0.18f, 1f),
        new Color(1f, 0.9f, 0.25f, 1f),
        new Color(0.35f, 1f, 0.42f, 1f),
        new Color(0.28f, 0.76f, 1f, 1f),
        new Color(0.42f, 0.45f, 1f, 1f),
        new Color(0.86f, 0.34f, 1f, 1f)
    };

    struct RingState
    {
        public float normalizedOffset;
        public float baseRotation;
        public int rotationDirection;
        public float sizeMultiplier;
    }

    [Header("Runtime")]
    [SerializeField] WorldStatItemPickup pickup;
    [SerializeField] GrowingHerbNode herbNode;
    [SerializeField] Sprite[] frames = new Sprite[0];
    [SerializeField] ItemGrade grade;
    [SerializeField] int sortingLayerId;
    [SerializeField] int sortingOrder = 60;

    [Header("Shape")]
    [SerializeField] int ringCount = 4;
    [SerializeField] float ringLifetimeSeconds = 1.5f;
    [SerializeField] float startWorldSize = 0.26f;
    [SerializeField] float endWorldSize = 0.92f;
    [SerializeField] float verticalOffset = 0.03f;
    [SerializeField] float rotationSpeed = 48f;
    [SerializeField] float peakAlpha = 0.72f;
    [SerializeField] float frameStartNormalized = 0f;
    [SerializeField] float frameEndNormalized = 1f;
    [SerializeField] Color tint = Color.white;

    SpriteRenderer[] ringRenderers = new SpriteRenderer[0];
    RingState[] ringStates = new RingState[0];
    float startedScaledTime;
    bool configured;
    float herbWorldSize = 0.45f;
    GrowingHerbVisualStage lastAppliedStage = (GrowingHerbVisualStage)(-1);
    ItemGrade lastAppliedGrade = (ItemGrade)(-1);
    bool useSpectrumTint;
    float spectrumShiftSpeed = 0.18f;

    void Awake()
    {
        EnsureRingPool();
        RefreshRenderers();
    }

    void OnEnable()
    {
        if (startedScaledTime <= 0f)
        {
            startedScaledTime = Time.time;
        }

        EnsureRingPool();
        RefreshRenderers();
    }

    void OnDisable()
    {
        SetRenderersVisible(false);
    }

    void Update()
    {
        if (!configured ||
            frames == null ||
            frames.Length == 0)
        {
            SetRenderersVisible(false);
            return;
        }

        if (pickup != null &&
            pickup.amount <= 0)
        {
            SetRenderersVisible(false);
            return;
        }

        ApplyDynamicProfile();
        EnsureRingPool();
        RefreshRenderers();
    }

    public void Configure(
        WorldStatItemPickup configuredPickup,
        GrowingHerbNode configuredHerbNode,
        Sprite[] configuredFrames,
        ItemGrade configuredGrade,
        int configuredSortingLayerId,
        int configuredSortingOrder,
        float configuredHerbWorldSize)
    {
        pickup = configuredPickup;
        herbNode = configuredHerbNode;
        frames = FilterFrames(configuredFrames);
        grade = configuredGrade;
        sortingLayerId = configuredSortingLayerId;
        sortingOrder = configuredSortingOrder;
        herbWorldSize = Mathf.Max(0.05f, configuredHerbWorldSize);

        ApplyDynamicProfile(forceRefresh: true);

        startedScaledTime = Time.time;
        configured = frames != null && frames.Length > 0;
        EnsureRingPool();
        RefreshRenderers();
    }

    void ApplyDynamicProfile(bool forceRefresh = false)
    {
        GrowingHerbVisualStage stage =
            herbNode != null
                ? herbNode.CurrentVisualStage
                : GrowingHerbVisualStage.Mature;

        if (!forceRefresh &&
            stage == lastAppliedStage &&
            grade == lastAppliedGrade)
        {
            return;
        }

        float sizeFactor =
            Mathf.Max(
                1.1f,
                herbWorldSize / 0.34f);

        ApplyGradeProfile(grade, sizeFactor);
        ApplyStageProfile(stage);

        verticalOffset = Mathf.Max(0.02f, herbWorldSize * 0.08f);
        lastAppliedStage = stage;
        lastAppliedGrade = grade;
    }

    void ApplyGradeProfile(ItemGrade currentGrade, float sizeFactor)
    {
        switch (currentGrade)
        {
            case ItemGrade.Ha:
                ringCount = 3;
                ringLifetimeSeconds = 1.25f;
                startWorldSize = 0.42f * sizeFactor;
                endWorldSize = 1.28f * sizeFactor;
                rotationSpeed = 34f;
                peakAlpha = 0.56f;
                frameStartNormalized = 0.18f;
                frameEndNormalized = 1f;
                tint = new Color(0.92f, 0.92f, 0.92f, 1f);
                useSpectrumTint = false;
                break;

            case ItemGrade.Trung:
                ringCount = 4;
                ringLifetimeSeconds = 1.55f;
                startWorldSize = 0.5f * sizeFactor;
                endWorldSize = 1.55f * sizeFactor;
                rotationSpeed = 44f;
                peakAlpha = 0.82f;
                frameStartNormalized = 0.12f;
                frameEndNormalized = 1f;
                tint = new Color(0.98f, 0.99f, 1f, 1f);
                useSpectrumTint = false;
                break;

            case ItemGrade.Thuong:
            case ItemGrade.Tien:
                ringCount = 5;
                ringLifetimeSeconds = 1.7f;
                startWorldSize = 0.58f * sizeFactor;
                endWorldSize = 1.82f * sizeFactor;
                rotationSpeed = 48f;
                peakAlpha = 0.92f;
                frameStartNormalized = 0.08f;
                frameEndNormalized = 1f;
                tint = Color.white;
                useSpectrumTint = true;
                break;
        }
    }

    void ApplyStageProfile(GrowingHerbVisualStage stage)
    {
        switch (stage)
        {
            case GrowingHerbVisualStage.Small:
                ringCount = Mathf.Max(1, ringCount - 1);
                ringLifetimeSeconds *= 1.08f;
                startWorldSize *= 0.55f;
                endWorldSize *= 0.72f;
                rotationSpeed *= 0.78f;
                peakAlpha *= 0.3f;
                frameStartNormalized =
                    Mathf.Lerp(frameStartNormalized, frameEndNormalized, 0.45f);
                frameEndNormalized =
                    Mathf.Lerp(frameStartNormalized, frameEndNormalized, 0.82f);
                tint = Color.Lerp(tint, Color.white, 0.35f);
                break;

            case GrowingHerbVisualStage.Mid:
                ringCount = Mathf.Max(2, ringCount);
                ringLifetimeSeconds *= 1.02f;
                startWorldSize *= 0.82f;
                endWorldSize *= 0.92f;
                rotationSpeed *= 0.92f;
                peakAlpha *= 0.62f;
                frameStartNormalized =
                    Mathf.Lerp(frameStartNormalized, frameEndNormalized, 0.2f);
                break;

            case GrowingHerbVisualStage.Mature:
                break;
        }
    }

    void EnsureRingPool()
    {
        int desiredCount = Mathf.Clamp(ringCount, 1, 4);
        if (ringRenderers.Length == desiredCount &&
            ringStates.Length == desiredCount &&
            HasAllRenderers())
        {
            return;
        }

        ringRenderers = new SpriteRenderer[desiredCount];
        ringStates = new RingState[desiredCount];

        for (int i = 0; i < desiredCount; i++)
        {
            Transform child = transform.Find(RingChildPrefix + i);
            if (child == null)
            {
                GameObject ringObject = new GameObject(RingChildPrefix + i);
                ringObject.transform.SetParent(transform, false);
                child = ringObject.transform;
            }

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder + i;
            renderer.color = Color.clear;
            renderer.enabled = false;

            ringRenderers[i] = renderer;
            ringStates[i] = BuildRingState(i, desiredCount);
        }

        for (int i = desiredCount; ; i++)
        {
            Transform extra = transform.Find(RingChildPrefix + i);
            if (extra == null)
            {
                break;
            }

            extra.gameObject.SetActive(false);
        }
    }

    RingState BuildRingState(int index, int totalCount)
    {
        float offsetStep = totalCount > 0
            ? 1f / totalCount
            : 0.25f;

        return new RingState
        {
            normalizedOffset = index * offsetStep,
            baseRotation = index * 37f,
            rotationDirection = index % 2 == 0 ? 1 : -1,
            sizeMultiplier = 1f + index * 0.08f
        };
    }

    void RefreshRenderers()
    {
        if (!configured ||
            frames == null ||
            frames.Length == 0)
        {
            SetRenderersVisible(false);
            return;
        }

        if (!HasSynchronizedRingPool())
        {
            EnsureRingPool();
        }

        if (!HasSynchronizedRingPool())
        {
            SetRenderersVisible(false);
            return;
        }

        float safeLifetime = Mathf.Max(0.1f, ringLifetimeSeconds);
        float elapsed = Mathf.Max(0f, Time.time - startedScaledTime);
        int activeRingCount =
            Mathf.Min(
                ringRenderers.Length,
                ringStates.Length);

        for (int i = 0; i < activeRingCount; i++)
        {
            SpriteRenderer renderer = ringRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            RingState state = ringStates[i];
            float normalizedTime =
                Mathf.Repeat(
                    elapsed / safeLifetime + state.normalizedOffset,
                    1f);
            float sampledFrameTime =
                Mathf.Lerp(
                    Mathf.Clamp01(frameStartNormalized),
                    Mathf.Clamp(frameEndNormalized, 0f, 1f),
                    normalizedTime);
            int frameIndex =
                Mathf.Clamp(
                    Mathf.FloorToInt(sampledFrameTime * frames.Length),
                    0,
                    frames.Length - 1);

            Sprite frame = frames[frameIndex];
            if (frame == null)
            {
                renderer.enabled = false;
                continue;
            }

            float alpha =
                Mathf.Sin(normalizedTime * Mathf.PI) *
                Mathf.Max(0f, peakAlpha);
            if (alpha <= 0.01f)
            {
                renderer.enabled = false;
                continue;
            }

            float easedTime =
                normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            float worldSize =
                Mathf.Lerp(startWorldSize, endWorldSize, easedTime) *
                Mathf.Max(0.1f, state.sizeMultiplier);
            float spriteLargestSide =
                Mathf.Max(frame.bounds.size.x, frame.bounds.size.y);
            float spriteScale =
                spriteLargestSide > 0f
                    ? worldSize / spriteLargestSide
                    : 1f;

            renderer.enabled = true;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder + i;
            renderer.sprite = frame;
            renderer.color = ResolveRingColor(i, normalizedTime, alpha);

            Transform ringTransform = renderer.transform;
            ringTransform.localPosition = Vector3.up * verticalOffset;
            ringTransform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    state.baseRotation +
                    state.rotationDirection *
                    rotationSpeed *
                    elapsed);
            ringTransform.localScale = Vector3.one * spriteScale;
            ringTransform.gameObject.SetActive(true);
        }

        for (int i = activeRingCount; i < ringRenderers.Length; i++)
        {
            if (ringRenderers[i] != null)
            {
                ringRenderers[i].enabled = false;
            }
        }
    }

    Sprite[] FilterFrames(Sprite[] configuredFrames)
    {
        if (configuredFrames == null ||
            configuredFrames.Length == 0)
        {
            return new Sprite[0];
        }

        int validCount = 0;
        for (int i = 0; i < configuredFrames.Length; i++)
        {
            if (configuredFrames[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == configuredFrames.Length)
        {
            return configuredFrames;
        }

        Sprite[] filtered = new Sprite[validCount];
        int index = 0;
        for (int i = 0; i < configuredFrames.Length; i++)
        {
            Sprite frame = configuredFrames[i];
            if (frame == null)
            {
                continue;
            }

            filtered[index++] = frame;
        }

        return filtered;
    }

    bool HasAllRenderers()
    {
        for (int i = 0; i < ringRenderers.Length; i++)
        {
            if (ringRenderers[i] == null)
            {
                return false;
            }
        }

        return ringRenderers.Length > 0;
    }

    bool HasSynchronizedRingPool()
    {
        return ringRenderers != null &&
            ringStates != null &&
            ringRenderers.Length > 0 &&
            ringRenderers.Length == ringStates.Length &&
            HasAllRenderers();
    }

    void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < ringRenderers.Length; i++)
        {
            if (ringRenderers[i] == null)
            {
                continue;
            }

            ringRenderers[i].enabled = visible;
        }
    }

    Color ResolveRingColor(int ringIndex, float normalizedTime, float alpha)
    {
        Color baseColor = tint;
        if (useSpectrumTint && ThuongSpectrumColors.Length > 0)
        {
            float palettePosition =
                Mathf.Repeat(
                    ringIndex / Mathf.Max(1f, ringRenderers.Length) +
                    normalizedTime * spectrumShiftSpeed,
                    1f) * ThuongSpectrumColors.Length;
            int fromIndex =
                Mathf.Clamp(
                    Mathf.FloorToInt(palettePosition),
                    0,
                    ThuongSpectrumColors.Length - 1);
            int toIndex = (fromIndex + 1) % ThuongSpectrumColors.Length;
            float blend = palettePosition - fromIndex;
            baseColor =
                Color.Lerp(
                    ThuongSpectrumColors[fromIndex],
                    ThuongSpectrumColors[toIndex],
                    blend);
        }

        return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
    }
}
