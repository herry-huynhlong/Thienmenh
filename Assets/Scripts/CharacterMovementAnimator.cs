using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CharacterMovementAnimator : MonoBehaviour
{
    public bool disableWhenNpcVisualAnimationExists = true;
    public Rigidbody2D targetRigidbody;
    public Animator targetAnimator;
    public SpriteRenderer targetSpriteRenderer;
    public Transform visualRoot;

    [Header("State Names")]
    public string downIdleState = "F1_down_Idle";
    public string upIdleState = "F1_up_Idle";
    public string sideIdleState = "F1_side_idle";
    public string downWalkState = "F1_down_walk";
    public string upWalkState = "F1_up_walk";
    public string sideWalkState = "F1_side_walk";

    [Header("Parameters Optional")]
    public bool setAnimatorParameters = true;
    public string isMovingParameter = "isMoving";
    public string moveXParameter = "moveX";
    public string moveYParameter = "moveY";
    public string speedParameter = "speed";

    [Header("Tuning")]
    public float movingThreshold = 0.02f;
    public float directionChangeThreshold = 0.15f;
    public float sideFlipCooldown = 0.12f;
    public bool flipSideByDirection = true;
    public bool useSpriteRendererFlip = true;
    public bool sideSpriteFacesRight = true;

    Vector2 lastDirection = Vector2.down;
    int currentStateHash;
    float originalScaleX = 1f;
    float nextFlipTime;

    void Awake()
    {
        if (disableWhenNpcVisualAnimationExists &&
            GetComponent<NPCVisualAnimation>() != null)
        {
            enabled = false;
            return;
        }

        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }

        if (visualRoot == null)
        {
            if (targetSpriteRenderer == null)
            {
                targetSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (targetSpriteRenderer != null)
            {
                visualRoot = targetSpriteRenderer.transform;
            }
        }

        if (targetSpriteRenderer == null && visualRoot != null)
        {
            targetSpriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
        }

        Transform flipTarget =
            visualRoot != null
            ? visualRoot
            : transform;

        originalScaleX =
            Mathf.Approximately(flipTarget.localScale.x, 0f)
            ? 1f
            : Mathf.Abs(flipTarget.localScale.x);
    }

    void Update()
    {
        Vector2 velocity = GetVelocity();
        float speed = velocity.magnitude;
        bool isMoving = speed > movingThreshold;

        if (isMoving)
        {
            UpdateLastDirection(velocity.normalized);
        }

        if (setAnimatorParameters)
        {
            SetParameters(isMoving, velocity, speed);
        }

        PlayDirectionalState(isMoving);
        ApplySideFlip();
    }

    void UpdateLastDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < directionChangeThreshold * directionChangeThreshold)
        {
            return;
        }

        bool currentSide =
            Mathf.Abs(lastDirection.x) > Mathf.Abs(lastDirection.y);

        bool nextSide =
            Mathf.Abs(direction.x) > Mathf.Abs(direction.y);

        if (currentSide &&
            nextSide &&
            Mathf.Sign(lastDirection.x) != Mathf.Sign(direction.x) &&
            Time.time < nextFlipTime)
        {
            return;
        }

        if (currentSide &&
            nextSide &&
            Mathf.Sign(lastDirection.x) != Mathf.Sign(direction.x))
        {
            nextFlipTime = Time.time + sideFlipCooldown;
        }

        lastDirection = direction;
    }

    Vector2 GetVelocity()
    {
        if (targetRigidbody == null)
        {
            return Vector2.zero;
        }

        return targetRigidbody.linearVelocity;
    }

    void SetParameters(bool isMoving, Vector2 velocity, float speed)
    {
        SetBoolIfExists(isMovingParameter, isMoving);
        SetFloatIfExists(moveXParameter, isMoving ? velocity.normalized.x : lastDirection.x);
        SetFloatIfExists(moveYParameter, isMoving ? velocity.normalized.y : lastDirection.y);
        SetFloatIfExists(speedParameter, speed);
    }

    void PlayDirectionalState(bool isMoving)
    {
        if (targetAnimator == null)
        {
            return;
        }

        string stateName = GetStateName(isMoving);
        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        int stateHash = GetExistingStateHash(stateName);
        if (stateHash == 0)
        {
            return;
        }

        if (stateHash == currentStateHash)
        {
            return;
        }

        targetAnimator.Play(stateHash, 0);
        currentStateHash = stateHash;
    }

    int GetExistingStateHash(string stateName)
    {
        int hash = Animator.StringToHash(stateName);
        if (targetAnimator.HasState(0, hash))
        {
            return hash;
        }

        string lowerIdle = stateName.Replace("_Idle", "_idle");
        hash = Animator.StringToHash(lowerIdle);
        if (targetAnimator.HasState(0, hash))
        {
            return hash;
        }

        string upperIdle = stateName.Replace("_idle", "_Idle");
        hash = Animator.StringToHash(upperIdle);
        if (targetAnimator.HasState(0, hash))
        {
            return hash;
        }

        return 0;
    }

    string GetStateName(bool isMoving)
    {
        bool verticalDominant =
            Mathf.Abs(lastDirection.y) >= Mathf.Abs(lastDirection.x);

        if (verticalDominant)
        {
            if (lastDirection.y > 0f)
            {
                return isMoving ? upWalkState : upIdleState;
            }

            return isMoving ? downWalkState : downIdleState;
        }

        return isMoving ? sideWalkState : sideIdleState;
    }

    void ApplySideFlip()
    {
        if (!flipSideByDirection ||
            Mathf.Abs(lastDirection.x) <= Mathf.Abs(lastDirection.y))
        {
            return;
        }

        bool movingRight = lastDirection.x > 0f;
        bool usePositiveScale = sideSpriteFacesRight ? movingRight : !movingRight;

        if (useSpriteRendererFlip && targetSpriteRenderer != null)
        {
            targetSpriteRenderer.flipX = !usePositiveScale;
            return;
        }

        Transform flipTarget =
            visualRoot != null && visualRoot != transform
            ? visualRoot
            : null;

        if (flipTarget == null)
        {
            return;
        }

        Vector3 scale = flipTarget.localScale;
        scale.x = usePositiveScale ? originalScaleX : -originalScaleX;
        flipTarget.localScale = scale;
    }

    void SetBoolIfExists(string parameterName, bool value)
    {
        if (string.IsNullOrEmpty(parameterName) ||
            targetAnimator == null ||
            !HasParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            return;
        }

        targetAnimator.SetBool(parameterName, value);
    }

    void SetFloatIfExists(string parameterName, float value)
    {
        if (string.IsNullOrEmpty(parameterName) ||
            targetAnimator == null ||
            !HasParameter(parameterName, AnimatorControllerParameterType.Float))
        {
            return;
        }

        targetAnimator.SetFloat(parameterName, value);
    }

    bool HasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
        {
            if (parameter.type == type &&
                parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }
}
