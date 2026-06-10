using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopCardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float hoverScale = 1.08f;
    public float speed = 12f;
    public Image glowImage;

    Vector3 normalScale;
    Vector3 targetScale;

    void Awake()
    {
        normalScale = transform.localScale;
        targetScale = normalScale;

        if (glowImage != null)
        {
            glowImage.raycastTarget = false;
            Color c = glowImage.color;
            c.a = 0f;
            glowImage.color = c;
        }
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.unscaledDeltaTime * speed
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("Hover vào: " + gameObject.name);
        targetScale = normalScale * hoverScale;

        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = 0.45f;
            glowImage.color = c;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("Hover ra: " + gameObject.name);
        targetScale = normalScale;

        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = 0f;
            glowImage.color = c;
        }
    }
}