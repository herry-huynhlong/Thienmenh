using UnityEngine;

public class PhapTuongBreath : MonoBehaviour
{
    Vector3 originalScale;
    SpriteRenderer sr;

    public float scaleAmount = 0.08f;
    public float breathSpeed = 1.2f;

    void Start()
    {
        originalScale = transform.localScale;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * breathSpeed) + 1f) * 0.5f;

        // Scale phập phồng
        transform.localScale =
            originalScale *
            (1f + t * scaleAmount);

        // Alpha dao động nhẹ
        Color c = sr.color;
        c.a = Mathf.Lerp(0.15f, 0.25f, t);
        sr.color = c;
    }
}