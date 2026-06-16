using UnityEngine;

public static class PickupVisualUtility
{
    public static void ApplySprite(
        GameObject target,
        Sprite sprite,
        int sortingOrder,
        float visualSize = 0.45f)
    {
        if (target == null || sprite == null)
        {
            return;
        }

        SpriteRenderer renderer =
            target.GetComponentInChildren<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = target.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        NormalizeRendererSize(renderer, visualSize);
    }

    public static void NormalizeRendererSize(
        SpriteRenderer renderer,
        float visualSize = 0.45f)
    {
        if (renderer == null || renderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = renderer.sprite.bounds.size;
        float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);

        if (largestSide <= 0f)
        {
            return;
        }

        renderer.transform.localScale =
            Vector3.one * (Mathf.Max(0.05f, visualSize) / largestSide);
    }
}
