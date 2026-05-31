using UnityEngine;

public class GoldenEnergyEffect : MonoBehaviour
{
    [Header("Xoay cột linh khí")]
    public bool rotateEnergy = true;
    public float rotateSpeed = 10f;

    [Header("Linh khí bay lên")]
    public ParticleSystem auraParticles;

    [Header("Nhấp nháy nhẹ")]
    public bool pulseScale = true;
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.05f;

    private Vector3 baseScale;

    void Start()
    {
        baseScale = transform.localScale;

        // Tự bật particle khi game chạy
        if (auraParticles != null)
        {
            auraParticles.Play();
        }
    }

    void Update()
    {
        RotateEnergy();

        PulseEffect();
    }

    void RotateEnergy()
    {
        if (!rotateEnergy) return;

        transform.Rotate(
            0,
            0,
            rotateSpeed * Time.deltaTime
        );
    }

    // rotateSpeed
    // Tốc độ xoay của cột linh khí
    //
    // transform.Rotate
    // Xoay object quanh trục Z

    void PulseEffect()
    {
        if (!pulseScale) return;

        float scaleOffset =
            Mathf.Sin(Time.time * pulseSpeed)
            * pulseAmount;

        transform.localScale =
            baseScale +
            Vector3.one * scaleOffset;
    }

    // pulseSpeed
    // Tốc độ nhấp nháy
    //
    // pulseAmount
    // Độ phóng to thu nhỏ
    //
    // Mathf.Sin
    // Tạo hiệu ứng scale mượt như đang thở
}