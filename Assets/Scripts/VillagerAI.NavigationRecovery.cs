using UnityEngine;

// Core navigation velocity/animation update kept separate from specialized partials.
public partial class VillagerAI
{
    void ApplySmoothVelocity()
    {
        NpcVisualMotionResolver.ApplySmoothVelocity(
            rb,
            desiredVelocity,
            movementAcceleration,
            movementDeceleration,
            Time.fixedDeltaTime);
    }

    void UpdateVisualAnimation()
    {
        if (visualAnimation == null)
        {
            return;
        }

        NpcVisualMotionState motionState =
            NpcVisualMotionResolver.Resolve(
                rb != null ? rb.linearVelocity : desiredVelocity,
                desiredVelocity,
                animationIdleSpeed,
                allowDesiredVelocityFallback: false);
        Vector2 animationVelocity =
            motionState.AnimationVelocity;
        bool isIdle = motionState.IsIdle;
        Vector2 direction =
            motionState.LocomotionDirection;

        if (visualAnimation.debugVisualLogs)
        {
            Debug.Log(
                "[VillagerAI] Visual input object=" +
                gameObject.name +
                " velocity=" + animationVelocity +
                " speed=" + animationVelocity.magnitude.ToString("F3") +
                " idleThreshold=" + animationIdleSpeed.ToString("F3") +
                " isIdle=" + isIdle +
                " fallback=" + motionState.UsedDesiredVelocityFallback +
                " desiredVelocity=" + desiredVelocity +
                " action=" + currentAction);
        }

        visualAnimation.UpdateNPCAnimation(direction, isIdle, currentAction);
    }
}
