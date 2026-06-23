using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LuckyWheelEntryAliveEffect : MonoBehaviour
{
    public RectTransform target;
    public RectTransform sparkRoot;

    public float floatHeight = 8f;
    public float floatSpeed = 2.2f;

    public float pulseAmount = 0.07f;
    public float pulseSpeed = 3f;

    public float tiltAmount = 5f;
    public float tiltSpeed = 2.4f;

    public float sparkInterval = 0.45f;
    public int sparkPerBurst = 4;

    Vector2 startPos;
    Vector3 startScale;
    Sprite sparkSprite;
    float sparkTimer;

    void Awake()
    {
        if (target == null)
            target = transform as RectTransform;

        if (target != null)
        {
            startPos = target.anchoredPosition;
            startScale = target.localScale;
        }

        sparkSprite = CreateSparkSprite();
    }

    void Update()
    {
        AnimateIcon();
        AnimateSpark();
    }

    void AnimateIcon()
    {
        if (target == null) return;

        float y = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        float tilt = Mathf.Sin(Time.time * tiltSpeed) * tiltAmount;

        target.anchoredPosition = startPos + new Vector2(0f, y);
        target.localScale = startScale * scale;
        target.localEulerAngles = new Vector3(0f, 0f, tilt);
    }

    void AnimateSpark()
    {
        if (sparkRoot == null) return;

        sparkTimer += Time.deltaTime;

        if (sparkTimer >= sparkInterval)
        {
            sparkTimer = 0f;
            SpawnSparks();
        }
    }

    void SpawnSparks()
    {
        for (int i = 0; i < sparkPerBurst; i++)
        {
            GameObject go = new GameObject("EntrySpark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(sparkRoot, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(10f, 10f);

            Image img = go.GetComponent<Image>();
            img.sprite = sparkSprite;
            img.raycastTarget = false;
            img.color = Random.ColorHSV(0.08f, 0.16f, 0.75f, 1f, 0.9f, 1f);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(45f, 75f);

            Vector2 start = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 end = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(20f, 40f);

            rt.anchoredPosition = start;

            StartCoroutine(SparkRoutine(rt, img, start, end));
        }
    }

    IEnumerator SparkRoutine(RectTransform rt, Image img, Vector2 start, Vector2 end)
    {
        float timer = 0f;
        float duration = 0.65f;
        Color c = img.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            rt.anchoredPosition = Vector2.Lerp(start, end, t);
            rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.2f, t);

            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Destroy(rt.gameObject);
    }

    Sprite CreateSparkSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float t = Mathf.Clamp01(dist / radius);
                float alpha = Mathf.Pow(1f - t, 2.5f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}