using UnityEngine;
using UnityEngine.UI;

public class PhapTuongBreath : MonoBehaviour
{
    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;
    private Graphic graphic;
    private Color originalSpriteColor;
    private Color originalGraphicColor;

    public float scaleAmount = 0.08f;
    public float breathSpeed = 1.2f;
    public bool animateAlpha = false;
    [Range(0f, 1f)] public float minAlpha = 0.15f;
    [Range(0f, 1f)] public float maxAlpha = 0.25f;

    void Start()
    {
        originalScale = transform.localScale;
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            graphic = GetComponent<Graphic>();
        }

        if (spriteRenderer != null)
        {
            originalSpriteColor = spriteRenderer.color;
        }
        else if (graphic != null)
        {
            originalGraphicColor = graphic.color;
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
            if (animateAlpha)
            {
                Color color = originalSpriteColor;
                color.a = Mathf.Lerp(minAlpha, maxAlpha, t);
                spriteRenderer.color = color;
            }
            return;
        }

        if (!animateAlpha)
        {
            return;
        }

        Color graphicColor = originalGraphicColor;
        graphicColor.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        graphic.color = graphicColor;
    }
}
