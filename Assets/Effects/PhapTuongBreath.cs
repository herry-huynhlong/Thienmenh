using UnityEngine;
using UnityEngine.UI;

public class PhapTuongBreath : MonoBehaviour
{
    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;
    private Graphic graphic;

    public float scaleAmount = 0.08f;
    public float breathSpeed = 1.2f;

    void Start()
    {
        originalScale = transform.localScale;
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            graphic = GetComponent<Graphic>();
        }

        if (spriteRenderer == null && graphic == null)
        {
            enabled = false;
        }
    }

    void Update()
    {
        if (spriteRenderer == null && graphic == null)
        {
            return;
        }

        float t = (Mathf.Sin(Time.time * breathSpeed) + 1f) * 0.5f;

        transform.localScale = originalScale * (1f + t * scaleAmount);

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(0.15f, 0.25f, t);
            spriteRenderer.color = color;
            return;
        }

        Color graphicColor = graphic.color;
        graphicColor.a = Mathf.Lerp(0.15f, 0.25f, t);
        graphic.color = graphicColor;
    }
}
