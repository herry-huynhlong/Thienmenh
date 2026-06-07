using UnityEngine;

public class CultivationTechniqueEffect : MonoBehaviour
{
    [Header("Thời gian hiệu ứng")]
    public float duration = 1.6f;

    [Header("Các phần xoay")]
    public Transform magicCircle;
    public Transform runeRoot;

    public float circleRotateSpeed = 90f;
    public float runeRotateSpeed = -140f;

    [Header("Phóng to / thu nhỏ")]
    public Transform auraLight;
    public Vector3 startScale = new Vector3(0.4f, 0.4f, 1f);
    public Vector3 maxScale = new Vector3(1.2f, 1.2f, 1f);

    [Header("Sprite cần mờ dần")]
    public SpriteRenderer[] fadeSprites;

    [Header("Particle linh khí")]
    public ParticleSystem[] particles;

    private float timer;

    private void Start()
    {
        timer = 0f;

        if (auraLight != null)
            auraLight.localScale = startScale;

        SetAlpha(0f);

        foreach (ParticleSystem p in particles)
        {
            if (p != null)
                p.Play();
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = timer / duration;
        t = Mathf.Clamp01(t);

        if (magicCircle != null)
            magicCircle.Rotate(0f, 0f, circleRotateSpeed * Time.deltaTime);

        if (runeRoot != null)
            runeRoot.Rotate(0f, 0f, runeRotateSpeed * Time.deltaTime);

        if (auraLight != null)
        {
            float pulse = Mathf.Sin(t * Mathf.PI);
            auraLight.localScale = Vector3.Lerp(startScale, maxScale, pulse);
        }

        float alpha = 1f;

        if (t < 0.25f)
        {
            alpha = Mathf.Lerp(0f, 1f, t / 0.25f);
        }
        else if (t > 0.75f)
        {
            alpha = Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f);
        }

        SetAlpha(alpha);

        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void SetAlpha(float alpha)
    {
        foreach (SpriteRenderer sr in fadeSprites)
        {
            if (sr == null)
                continue;

            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}