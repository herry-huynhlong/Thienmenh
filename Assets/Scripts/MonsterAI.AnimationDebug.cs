using UnityEngine;

public partial class MonsterAI
{
    void FaceDirection(Vector2 direction)
    {
        if (directionalAnimator != null)
        {
            directionalAnimator.SetMoveDirection(direction);
            return;
        }

        if (direction.x < -0.01f)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (direction.x > 0.01f)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
    }

    void SetMovingAnimation(bool isMoving)
    {
        if (directionalAnimator != null)
        {
            directionalAnimator.SetMoving(isMoving);
            return;
        }

        if (animator != null &&
            useAnimation)
        {
            SetAnimatorBoolIfExists("isMoving", isMoving);
        }
    }

    bool HasAnimatorParameter(
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (animator == null ||
            string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == parameterType &&
                parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    void SetAnimatorBoolIfExists(string parameterName, bool value)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    void SetAnimatorTriggerIfExists(string parameterName)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameterName);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center =
            Application.isPlaying
            ? startPosition
            : (Vector2)transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, roamRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, territoryRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, returnHomeDistance);
    }
}
