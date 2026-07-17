using UnityEngine;

public class SpiritQiIconAnimator : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] float rotationSpeedDegreesPerSecond = 5f;

    [Header("Pulse")]
    [SerializeField] float pulseScaleAmount = 0.03f;
    [SerializeField] float pulseSpeed = 1.5f;

    Vector3 initialScale = Vector3.one;
    Quaternion initialRotation = Quaternion.identity;
    bool initialized;
    float pulseTime;

    void Awake()
    {
        CacheInitialState();
    }

    void OnEnable()
    {
        CacheInitialState();
        pulseTime = 0f;
        transform.localScale = initialScale;
    }

    void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        transform.Rotate(0f, 0f, rotationSpeedDegreesPerSecond * deltaTime);

        pulseTime += deltaTime * pulseSpeed;
        float scaleFactor = 1f + Mathf.Sin(pulseTime * Mathf.PI * 2f) * pulseScaleAmount;
        transform.localScale = initialScale * scaleFactor;
    }

    void OnDisable()
    {
        if (!initialized)
        {
            return;
        }

        pulseTime = 0f;
        transform.localScale = initialScale;
        transform.localRotation = initialRotation;
    }

    void CacheInitialState()
    {
        if (initialized)
        {
            return;
        }

        initialScale = transform.localScale;
        initialRotation = transform.localRotation;
        initialized = true;
    }
}
