using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MenuVortexParticles : MonoBehaviour
{
    [Header("Particle")]
    public int particleCount = 18;
    public float radius = 45f;
    public float rotateSpeed = 35f;
    public float pulseSpeed = 1.4f;
    public float particleSize = 5f;

    [Header("Color")]
    public Color particleColor = new Color(0.55f, 0.75f, 1f, 0.75f);

    private readonly List<RectTransform> particles = new List<RectTransform>();

    private void Start()
    {
        CreateParticles();
    }

    private void Update()
    {
        float time = Time.unscaledTime;

        for (int i = 0; i < particles.Count; i++)
        {
            RectTransform p = particles[i];

            float angle = time * rotateSpeed + i * (360f / particleCount);
            float rad = angle * Mathf.Deg2Rad;

            float pulse = Mathf.Sin(time * pulseSpeed + i) * 8f;
            float r = radius + pulse;

            p.anchoredPosition = new Vector2(
                Mathf.Cos(rad) * r,
                Mathf.Sin(rad) * r * 0.55f
            );

            float s = particleSize * (0.7f + Mathf.Sin(time * 2f + i) * 0.25f);
            p.sizeDelta = new Vector2(s, s);
        }
    }

    private void CreateParticles()
    {
        for (int i = 0; i < particleCount; i++)
        {
            GameObject obj = new GameObject("VortexParticle");
            obj.transform.SetParent(transform, false);

            Image img = obj.AddComponent<Image>();
            img.color = particleColor;
            img.raycastTarget = false;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(particleSize, particleSize);

            particles.Add(rt);
        }
    }
}