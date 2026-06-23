using UnityEngine;

public class UIPulse : MonoBehaviour
{
    public float pulseSpeed = 2.5f;
    public float pulseAmount = 0.06f;

    private Vector3 baseScale;

    private void Start()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float scale = 1f + t * pulseAmount;
        transform.localScale = baseScale * scale;
    }
}