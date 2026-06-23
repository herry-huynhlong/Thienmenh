using UnityEngine;
using UnityEngine.UI;

public class WheelBulbBlinkOnly : MonoBehaviour
{
    [Header("Bulbs")]
    public Image[] bulbs;

    [Header("Blink Settings")]
    public bool isSpinning = false;
    public float idleSpeed = 3f;
    public float spinSpeed = 14f;
    public int trailLength = 4;

    [Header("Alpha")]
    public float offAlpha = 0.05f;
    public float idleAlpha = 0.35f;
    public float onAlpha = 1f;

    [Header("Colors")]
    public Color[] colors =
    {
        new Color(1f, 0.25f, 0.25f, 1f), // đỏ
        new Color(1f, 0.6f, 0.15f, 1f),  // cam
        new Color(1f, 0.95f, 0.25f, 1f), // vàng
        new Color(0.25f, 1f, 0.4f, 1f),  // xanh lá
        new Color(0.2f, 0.8f, 1f, 1f),   // xanh lam
        new Color(0.75f, 0.35f, 1f, 1f), // tím
        new Color(1f, 0.35f, 0.75f, 1f), // hồng
    };

    float timer;

    void Reset()
    {
        AutoCollectBulbs();
    }

    [ContextMenu("Auto Collect Bulbs")]
    public void AutoCollectBulbs()
    {
        bulbs = GetComponentsInChildren<Image>(true);
    }

    void Update()
    {
        if (bulbs == null || bulbs.Length == 0) return;

        float speed = isSpinning ? spinSpeed : idleSpeed;
        timer += Time.deltaTime * speed;

        AnimateBulbs();
    }

    void AnimateBulbs()
    {
        int count = bulbs.Length;
        int head = Mathf.FloorToInt(timer) % count;

        for (int i = 0; i < count; i++)
        {
            if (bulbs[i] == null) continue;

            int distance = CircularDistance(i, head, count);

            float alpha = offAlpha;

            if (isSpinning)
            {
                if (distance == 0)
                {
                    alpha = onAlpha;
                }
                else if (distance < trailLength)
                {
                    float t = 1f - distance / (float)trailLength;
                    alpha = Mathf.Lerp(offAlpha, onAlpha, t);
                }
            }
            else
            {
                float pulse = (Mathf.Sin(timer + i * 0.7f) + 1f) * 0.5f;
                alpha = Mathf.Lerp(offAlpha, idleAlpha, pulse);
            }

            Color c = GetColor(i + Mathf.FloorToInt(timer));
            c.a = alpha;
            bulbs[i].color = c;
        }
    }

    Color GetColor(int index)
    {
        if (colors == null || colors.Length == 0)
            return Color.white;

        return colors[Mathf.Abs(index) % colors.Length];
    }

    int CircularDistance(int a, int b, int count)
    {
        int d = Mathf.Abs(a - b);
        return Mathf.Min(d, count - d);
    }

    public void SetSpinning(bool value)
    {
        isSpinning = value;
    }

    public void TurnOff()
    {
        if (bulbs == null) return;

        foreach (Image img in bulbs)
        {
            if (img == null) continue;
            Color c = img.color;
            c.a = offAlpha;
            img.color = c;
        }
    }
}