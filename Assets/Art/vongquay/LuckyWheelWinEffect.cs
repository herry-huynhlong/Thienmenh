using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LuckyWheelWinEffect : MonoBehaviour
{
    [Header("References")]
    public RectTransform effectRoot;
    public RectTransform rewardTarget;

    [Header("Effect Settings")]
    public string itemIconChildName = "ItemIcon";
    public Vector2 finalIconSize = new Vector2(150f, 150f);

    public float flyDuration = 0.35f;
    public float popDuration = 0.25f;
    public float stayDuration = 0.65f;
    public float fadeDuration = 0.25f;

    [Header("Firework")]
    public int sparkCount = 45;
    public float sparkMinDistance = 70f;
    public float sparkMaxDistance = 190f;
    public float sparkDuration = 0.75f;
    public Vector2 sparkSize = new Vector2(18f, 18f);

    public Color[] fireworkColors =
    {
        new Color(1f, 0.85f, 0.2f, 1f),
        new Color(1f, 0.35f, 0.2f, 1f),
        new Color(0.3f, 0.8f, 1f, 1f),
        new Color(0.8f, 0.35f, 1f, 1f),
        new Color(0.35f, 1f, 0.55f, 1f),
        new Color(1f, 0.35f, 0.75f, 1f),
    };

    Sprite sparkSprite;

    void Awake()
    {
        if (effectRoot == null)
        {
            effectRoot = transform as RectTransform;
        }

        sparkSprite = CreateSoftCircleSprite();
    }

    public void PlayFromSlot(RectTransform slot)
    {
        if (slot == null)
        {
            Debug.LogWarning("LuckyWheelWinEffect: slot null.");
            return;
        }

        Image itemIcon = FindChildImage(slot, itemIconChildName);

        if (itemIcon == null || itemIcon.sprite == null)
        {
            Debug.LogWarning("LuckyWheelWinEffect: Không tìm thấy ItemIcon hoặc ItemIcon chưa có sprite.");
            return;
        }

        StartCoroutine(PlayRoutine(itemIcon));
    }

    IEnumerator PlayRoutine(Image sourceIcon)
    {
        GameObject go = new GameObject("Win_Item_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(effectRoot, false);

        Image cloneImage = go.GetComponent<Image>();
        cloneImage.sprite = sourceIcon.sprite;
        cloneImage.preserveAspect = true;
        cloneImage.raycastTarget = false;
        cloneImage.color = Color.white;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector3 startWorldPos = sourceIcon.rectTransform.position;
        Vector3 targetWorldPos = rewardTarget != null ? rewardTarget.position : effectRoot.position;

        rt.position = startWorldPos;
        rt.sizeDelta = sourceIcon.rectTransform.rect.size;
        rt.localScale = Vector3.one;

        Vector2 startSize = rt.sizeDelta;

        float timer = 0f;

        while (timer < flyDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / flyDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            rt.position = Vector3.Lerp(startWorldPos, targetWorldPos, eased);
            rt.sizeDelta = Vector2.Lerp(startSize, finalIconSize, eased);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, eased);

            yield return null;
        }

        rt.position = targetWorldPos;
        rt.sizeDelta = finalIconSize;

        timer = 0f;

        while (timer < popDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / popDuration);

            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.28f;
            rt.localScale = Vector3.one * scale;

            yield return null;
        }

        rt.localScale = Vector3.one;

        PlayFirework(targetWorldPos);
        PlayFirework(targetWorldPos + new Vector3(-120f, 55f, 0f));
        PlayFirework(targetWorldPos + new Vector3(120f, 55f, 0f));

        yield return new WaitForSeconds(stayDuration);

        timer = 0f;
        Color c = cloneImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / fadeDuration);

            c.a = Mathf.Lerp(1f, 0f, t);
            cloneImage.color = c;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, t);

            yield return null;
        }

        Destroy(go);
    }

    void PlayFirework(Vector3 worldPos)
    {
        for (int i = 0; i < sparkCount; i++)
        {
            GameObject go = new GameObject("Firework_Spark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(effectRoot, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = worldPos;
            rt.sizeDelta = sparkSize;

            Image img = go.GetComponent<Image>();
            img.sprite = sparkSprite;
            img.raycastTarget = false;
            img.color = fireworkColors[i % fireworkColors.Length];

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(sparkMinDistance, sparkMaxDistance);

            Vector3 endWorldPos = worldPos + new Vector3(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance,
                0f
            );

            StartCoroutine(SparkRoutine(rt, img, worldPos, endWorldPos));
        }
    }

    IEnumerator SparkRoutine(RectTransform rt, Image img, Vector3 start, Vector3 end)
    {
        float timer = 0f;
        Color c = img.color;

        Vector3 startScale = Vector3.one * Random.Range(0.7f, 1.25f);
        Vector3 endScale = Vector3.one * Random.Range(0.1f, 0.35f);

        while (timer < sparkDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / sparkDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            rt.position = Vector3.Lerp(start, end, eased);
            rt.localScale = Vector3.Lerp(startScale, endScale, t);
            rt.localEulerAngles += new Vector3(0f, 0f, 480f * Time.deltaTime);

            c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;

            yield return null;
        }

        Destroy(rt.gameObject);
    }

    Image FindChildImage(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child.GetComponent<Image>();
            }

            Image found = FindChildImage(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    Sprite CreateSoftCircleSprite()
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
                float alpha = Mathf.Pow(1f - t, 2.4f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }
}