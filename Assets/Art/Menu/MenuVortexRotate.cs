using UnityEngine;
using UnityEngine.UI;

public class MenuVortexRotate : MonoBehaviour
{
    [Header("Rotate")]
    public float rotateSpeed = -12f;

    [Header("Breath")]
    public bool useBreath = true;
    public float scaleAmount = 0.04f;
    public float scaleSpeed = 1.2f;

    [Header("Alpha Pulse")]
    public bool useAlphaPulse = true;
    public float alphaMin = 0.65f;
    public float alphaMax = 0.95f;
    public float alphaSpeed = 1.4f;

    private RectTransform rectTransform;
    private Image image;
    private Vector3 baseScale;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        baseScale = rectTransform.localScale;

        if (image != null)
        {
            image.raycastTarget = false;
        }
    }

    private void Update()
    {
        float time = Time.unscaledTime;

        rectTransform.Rotate(0f, 0f, rotateSpeed * Time.unscaledDeltaTime);

        if (useBreath)
        {
            float scale = 1f + Mathf.Sin(time * scaleSpeed) * scaleAmount;
            rectTransform.localScale = baseScale * scale;
        }

        if (useAlphaPulse && image != null)
        {
            float t = (Mathf.Sin(time * alphaSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(alphaMin, alphaMax, t);

            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }
    }
}