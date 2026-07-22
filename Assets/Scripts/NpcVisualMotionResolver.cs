using UnityEngine;

public readonly struct NpcVisualMotionState
{
    public NpcVisualMotionState(
        Vector2 animationVelocity,
        Vector2 locomotionDirection,
        bool isIdle,
        bool hasDesiredMotion,
        bool usedDesiredVelocityFallback)
    {
        AnimationVelocity = animationVelocity;
        LocomotionDirection = locomotionDirection;
        IsIdle = isIdle;
        HasDesiredMotion = hasDesiredMotion;
        UsedDesiredVelocityFallback = usedDesiredVelocityFallback;
    }

    public Vector2 AnimationVelocity { get; }
    public Vector2 LocomotionDirection { get; }
    public bool IsIdle { get; }
    public bool HasDesiredMotion { get; }
    public bool UsedDesiredVelocityFallback { get; }
}

public static class NpcVisualMotionResolver
{
    public static void ApplySmoothVelocity(
        Rigidbody2D rb,
        Vector2 desiredVelocity,
        float acceleration,
        float deceleration,
        float deltaTime)
    {
        if (rb == null)
        {
            return;
        }

        float rate =
            desiredVelocity.sqrMagnitude > rb.linearVelocity.sqrMagnitude
            ? acceleration
            : deceleration;

        rb.linearVelocity =
            Vector2.MoveTowards(
                rb.linearVelocity,
                desiredVelocity,
                rate * deltaTime);
    }

    public static NpcVisualMotionState Resolve(
        Vector2 rigidbodyVelocity,
        Vector2 desiredVelocity,
        float idleSpeed,
        bool allowDesiredVelocityFallback = true)
    {
        float idleSpeedSqr = idleSpeed * idleSpeed;
        Vector2 animationVelocity = rigidbodyVelocity;
        bool hasDesiredMotion =
            desiredVelocity.sqrMagnitude > idleSpeedSqr;
        bool usedDesiredVelocityFallback = false;

        if (allowDesiredVelocityFallback &&
            animationVelocity.sqrMagnitude <= idleSpeedSqr &&
            hasDesiredMotion)
        {
            animationVelocity = desiredVelocity;
            usedDesiredVelocityFallback = true;
        }

        bool isIdle =
            animationVelocity.sqrMagnitude <= idleSpeedSqr;
        Vector2 locomotionDirection =
            isIdle
            ? Vector2.zero
            : animationVelocity.normalized;

        return new NpcVisualMotionState(
            animationVelocity,
            locomotionDirection,
            isIdle,
            hasDesiredMotion,
            usedDesiredVelocityFallback);
    }
}
