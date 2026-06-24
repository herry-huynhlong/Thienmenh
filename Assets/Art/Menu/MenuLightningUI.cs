using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuLightningUI : MonoBehaviour
{
    [Header("Root")]
    public RectTransform lightningRoot;
    public Image flashImage;

    [Header("Timing")]
    public float minInterval = 4f;
    public float maxInterval = 8f;

    [Header("Lightning")]
    public Color lightningColor = new Color(0.65f, 0.85f, 1f, 1f);
    public float thickness = 4f;
    public int segmentCount = 7;
    public float jaggedAmount = 35f;
    public float visibleTime = 0.08f;

    [Header("Flash")]
    public Color flashColor = new Color(0.55f, 0.75f, 1f, 0.22f);
    public float flashFadeTime = 0.18f;

    private readonly List<Image> activeSegments = new List<Image>();

    private void Start()
    {
        if (lightningRoot == null)
        {
            lightningRoot = GetComponent<RectTransform>();
        }

        if (flashImage != null)
        {
            SetAlpha(flashImage, 0f);
        }

        StartCoroutine(LightningLoop());
    }

    private IEnumerator LightningLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(minInterval, maxInterval));

            PlayLightning();

            yield return new WaitForSecondsRealtime(visibleTime);

            ClearLightning();

            if (flashImage != null)
            {
                yield return StartCoroutine(FadeFlash());
            }
        }
    }

    private void PlayLightning()
    {
        ClearLightning();

        if (flashImage != null)
        {
            flashImage.color = flashColor;
        }

        Rect rect = lightningRoot.rect;

        // Vị trí sét: từ góc trên phải đánh xuống giữa phải
        Vector2 start = new Vector2(rect.width * 0.32f, rect.height * 0.42f);
        Vector2 end = new Vector2(rect.width * 0.12f, -rect.height * 0.15f);

        List<Vector2> points = new List<Vector2>();
        points.Add(start);

        for (int i = 1; i < segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            Vector2 p = Vector2.Lerp(start, end, t);

            p.x += Random.Range(-jaggedAmount, jaggedAmount);
            p.y += Random.Range(-jaggedAmount, jaggedAmount);

            points.Add(p);
        }

        points.Add(end);

        for (int i = 0; i < points.Count - 1; i++)
        {
            CreateSegment(points[i], points[i + 1], thickness);
        }

        // Nhánh phụ nhỏ
        if (Random.value > 0.35f)
        {
            int branchIndex = Random.Range(2, points.Count - 2);
            Vector2 branchStart = points[branchIndex];
            Vector2 branchEnd = branchStart + new Vector2(
                Random.Range(-90f, 80f),
                Random.Range(-80f, 30f)
            );

            CreateSegment(branchStart, branchEnd, thickness * 0.55f);
        }
    }

    private void CreateSegment(Vector2 a, Vector2 b, float lineThickness)
    {
        GameObject obj = new GameObject("LightningSegment");
        obj.transform.SetParent(lightningRoot, false);

        Image img = obj.AddComponent<Image>();
        img.color = lightningColor;
        img.raycastTarget = false;

        RectTransform rt = img.rectTransform;

        Vector2 dir = b - a;
        float length = dir.magnitude;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = a;
        rt.sizeDelta = new Vector2(length, lineThickness);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        activeSegments.Add(img);
    }

    private IEnumerator FadeFlash()
    {
        float startAlpha = flashColor.a;
        float t = 0f;

        while (t < flashFadeTime)
        {
            t += Time.unscaledDeltaTime;

            float a = Mathf.Lerp(startAlpha, 0f, t / flashFadeTime);
            SetAlpha(flashImage, a);

            yield return null;
        }

        SetAlpha(flashImage, 0f);
    }

    private void ClearLightning()
    {
        foreach (Image img in activeSegments)
        {
            if (img != null)
            {
                Destroy(img.gameObject);
            }
        }

        activeSegments.Clear();
    }

    private void SetAlpha(Image img, float alpha)
    {
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}