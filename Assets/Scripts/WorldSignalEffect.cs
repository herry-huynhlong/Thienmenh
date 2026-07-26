using System.Collections.Generic;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldSignalEffect : MonoBehaviour
{
    [System.Serializable]
    public struct RuntimeFrameSlice
    {
        public string name;
        public Rect rect;
        public Vector2 pivot;
        public float pixelsPerUnit;
    }

    [Header("Visual")]
    public SpriteRenderer targetRenderer;
    public Sprite[] frames = new Sprite[12];
    public Texture2D sourceTexture;
    public RuntimeFrameSlice[] runtimeSlices = new RuntimeFrameSlice[0];

    [Header("Playback")]
    [Min(0.1f)] public float framesPerSecond = 14f;
    public bool autoStartOnEnable = true;
    public bool destroyOnComplete = true;
    public bool hideRendererOnComplete = true;

    float startedScaledTime = float.NaN;
    bool completed;
    readonly List<Sprite> generatedSprites = new List<Sprite>();

    void Awake()
    {
        EnsureRenderer();
        EnsureConfiguredFrames();
    }

    void OnEnable()
    {
        EnsureRenderer();
        EnsureConfiguredFrames();

        if (autoStartOnEnable)
        {
            PlayNowIfNeeded();
        }
    }

    void OnDestroy()
    {
        CleanupGeneratedSprites();
    }

    void Update()
    {
        EnsureRenderer();
        EnsureConfiguredFrames();
        PlayNowIfNeeded();

        if (completed ||
            targetRenderer == null ||
            frames == null ||
            frames.Length == 0)
        {
            return;
        }

        float elapsedSeconds =
            Mathf.Max(0f, Time.time - startedScaledTime);
        int frameIndex =
            Mathf.FloorToInt(
                elapsedSeconds *
                Mathf.Max(0.1f, framesPerSecond));

        if (frameIndex >= frames.Length)
        {
            Complete();
            return;
        }

        SetFrame(frameIndex);
    }

    public void PlayNow()
    {
        startedScaledTime = Time.time;
        completed = false;

        if (targetRenderer != null)
        {
            targetRenderer.enabled = true;
        }

        SetFrame(0);
    }

    void PlayNowIfNeeded()
    {
        if (!float.IsNaN(startedScaledTime))
        {
            return;
        }

        PlayNow();
    }

    void SetFrame(int frameIndex)
    {
        if (targetRenderer == null ||
            frames == null ||
            frameIndex < 0 ||
            frameIndex >= frames.Length)
        {
            return;
        }

        Sprite sprite = frames[frameIndex];
        if (sprite != null)
        {
            targetRenderer.sprite = sprite;
        }
    }

    void Complete()
    {
        completed = true;

        if (hideRendererOnComplete &&
            targetRenderer != null)
        {
            targetRenderer.enabled = false;
        }

        if (destroyOnComplete)
        {
            Destroy(gameObject);
        }
    }

    void EnsureRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }
    }

    void EnsureConfiguredFrames()
    {
        if (HasUsableFrames())
        {
            return;
        }

        TryGenerateFramesFromRuntimeSlices();

        if (HasUsableFrames())
        {
            return;
        }

        throw new InvalidOperationException(
            "WorldSignalEffect requires configured sprite frames. " +
            "Runtime frame generation from sourceTexture/runtimeSlices failed.");
    }

    bool HasUsableFrames()
    {
        if (frames == null ||
            frames.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    void TryGenerateFramesFromRuntimeSlices()
    {
        if (sourceTexture == null ||
            runtimeSlices == null ||
            runtimeSlices.Length == 0)
        {
            return;
        }

        if (frames == null ||
            frames.Length != runtimeSlices.Length)
        {
            frames = new Sprite[runtimeSlices.Length];
        }

        for (int i = 0; i < runtimeSlices.Length; i++)
        {
            if (frames[i] != null)
            {
                continue;
            }

            RuntimeFrameSlice slice = runtimeSlices[i];
            if (slice.rect.width <= 0f ||
                slice.rect.height <= 0f)
            {
                continue;
            }

            float safePixelsPerUnit =
                slice.pixelsPerUnit > 0f
                    ? slice.pixelsPerUnit
                    : 100f;
            Sprite sprite =
                Sprite.Create(
                    sourceTexture,
                    slice.rect,
                    slice.pivot,
                    safePixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
            sprite.name =
                string.IsNullOrWhiteSpace(slice.name)
                    ? "SignalFrame_" + i
                    : slice.name.Trim();
            generatedSprites.Add(sprite);
            frames[i] = sprite;
        }
    }

    void CleanupGeneratedSprites()
    {
        for (int i = 0; i < generatedSprites.Count; i++)
        {
            Sprite sprite = generatedSprites[i];
            if (sprite == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(sprite);
            }
            else
            {
                DestroyImmediate(sprite);
            }
        }

        generatedSprites.Clear();
    }
}
