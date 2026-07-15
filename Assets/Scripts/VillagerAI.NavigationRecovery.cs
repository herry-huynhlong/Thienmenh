using UnityEngine;

// Core navigation velocity/animation update kept separate from specialized partials.
public partial class VillagerAI
{
    void ApplySmoothVelocity()
    {
        if (rb == null)
        {
            return;
        }

        float rate =
            desiredVelocity.sqrMagnitude > rb.linearVelocity.sqrMagnitude
            ? movementAcceleration
            : movementDeceleration;

        rb.linearVelocity =
            Vector2.MoveTowards(
                rb.linearVelocity,
                desiredVelocity,
                rate * Time.fixedDeltaTime);
    }

    void UpdateVisualAnimation()
    {
        if (visualAnimation == null)
        {
            return;
        }

        Vector2 animationVelocity =
            rb != null
            ? rb.linearVelocity
            : desiredVelocity;

        bool isIdle =
            animationVelocity.sqrMagnitude <=
            animationIdleSpeed * animationIdleSpeed;

        Vector2 direction =
            isIdle
            ? Vector2.zero
            : animationVelocity.normalized;

        if (visualAnimation.debugVisualLogs)
        {
            Debug.Log(
                "[VillagerAI] Visual input object=" +
                gameObject.name +
                " velocity=" + animationVelocity +
                " speed=" + animationVelocity.magnitude.ToString("F3") +
                " idleThreshold=" + animationIdleSpeed.ToString("F3") +
                " isIdle=" + isIdle +
                " desiredVelocity=" + desiredVelocity +
                " action=" + currentAction);
        }

        visualAnimation.UpdateNPCAnimation(direction, isIdle, currentAction);
    }
}
