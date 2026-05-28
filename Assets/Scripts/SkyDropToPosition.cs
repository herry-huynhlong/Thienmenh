using System;
using UnityEngine;

public class SkyDropToPosition : MonoBehaviour
{
    public Vector3 targetPosition;
    public float fallSpeed = 8f;
    public float stopDistance = 0.03f;
    public float spinSpeed = 360f;
    public float startScale = 0.65f;
    public float landScaleBounce = 1.18f;
    public float landBounceDuration = 0.18f;

    Vector3 baseScale;
    float landedTime = -1f;
    bool landed;
    Action onLanded;

    public void Setup(Vector3 target, float speed)
    {
        Setup(target, speed, null);
    }

    public void Setup(Vector3 target, float speed, Action landedCallback)
    {
        targetPosition = target;
        fallSpeed = Mathf.Max(0.1f, speed);
        baseScale = transform.localScale;
        transform.localScale = baseScale * startScale;
        onLanded = landedCallback;
    }

    void Update()
    {
        if (landed)
        {
            AnimateLanding();
            return;
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                fallSpeed * Time.deltaTime);

        transform.Rotate(
            0f,
            0f,
            spinSpeed * Time.deltaTime);

        float distance =
            Vector3.Distance(transform.position, targetPosition);

        float scaleProgress =
            1f - Mathf.Clamp01(distance / 6f);

        transform.localScale =
            Vector3.Lerp(
                baseScale * startScale,
                baseScale,
                scaleProgress);

        if (Vector3.Distance(transform.position, targetPosition) <= stopDistance)
        {
            transform.position = targetPosition;
            transform.rotation = Quaternion.identity;
            landed = true;
            landedTime = Time.time;
            onLanded?.Invoke();
            onLanded = null;
        }
    }

    void AnimateLanding()
    {
        float t =
            Mathf.Clamp01(
                (Time.time - landedTime) /
                Mathf.Max(0.01f, landBounceDuration));

        float bounce =
            Mathf.Sin(t * Mathf.PI);

        transform.localScale =
            baseScale *
            Mathf.Lerp(1f, landScaleBounce, bounce);

        if (t >= 1f)
        {
            transform.localScale = baseScale;
            Destroy(this);
        }
    }
}
