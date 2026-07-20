using System.Collections.Generic;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldMarriageHome : MonoBehaviour
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
    public Sprite[] frames = new Sprite[8];
    public Texture2D sourceTexture;
    public RuntimeFrameSlice[] runtimeSlices = new RuntimeFrameSlice[0];

    [Header("Build")]
    [Min(0.1f)] public float buildDurationWorldDays = 2f;
    public bool startBuilt;

    int buildStartAbsoluteDay = int.MinValue;
    float buildStartHour;
    bool constructionComplete;
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

        if (startBuilt)
        {
            SetBuiltImmediate();
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

        if (constructionComplete ||
            targetRenderer == null ||
            frames == null ||
            frames.Length == 0 ||
            buildStartAbsoluteDay == int.MinValue)
        {
            return;
        }

        float elapsedWorldHours = GetElapsedWorldHours();
        float totalWorldHours =
            Mathf.Max(
                0.1f,
                buildDurationWorldDays * 24f);
        float progress =
            Mathf.Clamp01(
                elapsedWorldHours / totalWorldHours);
        int frameIndex =
            Mathf.Clamp(
                Mathf.FloorToInt(progress * frames.Length),
                0,
                frames.Length - 1);
        SetFrame(frameIndex);

        if (progress >= 1f)
        {
            constructionComplete = true;
            SetFrame(frames.Length - 1);
        }
    }

    public void BeginConstruction(
        int startAbsoluteDay,
        float startHour,
        float durationWorldDays)
    {
        buildStartAbsoluteDay = Mathf.Max(1, startAbsoluteDay);
        buildStartHour = Mathf.Repeat(startHour, 24f);
        buildDurationWorldDays = Mathf.Max(0.1f, durationWorldDays);
        constructionComplete = false;
        startBuilt = false;
        SetFrame(0);
    }

    public void SetBuiltImmediate()
    {
        buildStartAbsoluteDay = int.MinValue;
        buildStartHour = 0f;
        constructionComplete = true;
        startBuilt = true;

        if (frames != null &&
            frames.Length > 0)
        {
            SetFrame(frames.Length - 1);
        }
    }

    float GetElapsedWorldHours()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null ||
            buildStartAbsoluteDay == int.MinValue)
        {
            return 0f;
        }

        float currentAbsoluteHours =
            (timeSystem.CurrentAbsoluteDay - 1) * 24f +
            timeSystem.CurrentHour;
        float startAbsoluteHours =
            (buildStartAbsoluteDay - 1) * 24f +
            buildStartHour;
        return Mathf.Max(0f, currentAbsoluteHours - startAbsoluteHours);
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

        throw new InvalidOperationException(
            "WorldMarriageHome requires configured sprite frames. " +
            "Runtime frame generation from sourceTexture/runtimeSlices is disabled.");
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
