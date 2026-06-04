using UnityEngine;

public class GamePerformanceSettings : MonoBehaviour
{
    public int targetFrameRate = 60;
    public float fixedDeltaTime = 1f / 30f;
    public float maximumDeltaTime = 0.08f;
    public bool disableVSync = true;
    public bool applyOnAwake = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyDefaultsBeforeSceneLoad()
    {
        ApplyDefaults();
    }

    void Awake()
    {
        if (applyOnAwake)
        {
            Apply(
                targetFrameRate,
                fixedDeltaTime,
                maximumDeltaTime,
                disableVSync);
        }
    }

    [ContextMenu("Apply Performance Settings")]
    public void ApplyFromInspector()
    {
        Apply(
            targetFrameRate,
            fixedDeltaTime,
            maximumDeltaTime,
            disableVSync);
    }

    public static void ApplyDefaults()
    {
        Apply(60, 1f / 30f, 0.08f, true);
    }

    public static void Apply(
        int targetFps,
        float fixedStep,
        float maxStep,
        bool disableSync)
    {
        if (disableSync)
        {
            QualitySettings.vSyncCount = 0;
        }

        Application.targetFrameRate = Mathf.Max(30, targetFps);
        Time.fixedDeltaTime = Mathf.Clamp(fixedStep, 1f / 60f, 1f / 20f);
        Time.maximumDeltaTime = Mathf.Clamp(maxStep, Time.fixedDeltaTime, 0.2f);
    }
}