using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CharacterMovementAnimator : MonoBehaviour
{
    public bool disableWhenNpcVisualAnimationExists = true;

    [Header("Component")]
    public Rigidbody2D targetRigidbody;
    public Animator targetAnimator;
    public SpriteRenderer targetSpriteRenderer;
    public Transform visualRoot;

    [Header("Auto Detect Animation")]
    public bool autoDetectStateNames = true;

    [Tooltip("Để trống thì code tự tìm. Nếu sai thì nhập ví dụ: DaoSi, LaoBa, OngGia")]
    public string animationPrefix = "";

    public bool logMissingState = true;

    [Header("State Names")]
    public string downIdleState = "DaoSi_Idle_Down";
    public string upIdleState = "DaoSi_Idle_Up";
    public string sideIdleState = "DaoSi_Idle_Right";

    public string downWalkState = "DaoSi_Walk_Down";

    [Tooltip("Nếu NPC chưa có Walk_Up thì code sẽ tự dùng Idle_Up tạm.")]
    public string upWalkState = "DaoSi_Idle_Up";

    public string sideWalkState = "DaoSi_Walk_Right";

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

    [Header("Flip trái phải")]
    public bool flipSideByDirection = true;
    public bool useSpriteRendererFlip = true;

    [Tooltip("Animation ngang đang nhìn sang phải thì bật true. Nếu code tự detect clip Left thì nó sẽ tự đổi false.")]
    public bool sideSpriteFacesRight = true;

    Vector2 lastDirection = Vector2.down;
    Vector3 lastPosition;
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

        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (visualRoot == null && targetSpriteRenderer != null)
        {
            visualRoot = targetSpriteRenderer.transform;
        }

        if (targetSpriteRenderer == null && visualRoot != null)
        {
            targetSpriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
        }

        if (autoDetectStateNames)
        {
            AutoDetectAnimationStates();
        }

        lastPosition = transform.position;

        Transform flipTarget = visualRoot != null ? visualRoot : transform;

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

    void AutoDetectAnimationStates()
    {
        if (targetAnimator == null ||
            targetAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        List<string> clipNames = GetAnimationClipNames();

        if (clipNames.Count <= 0)
        {
            return;
        }

        string prefix = animationPrefix;

        if (string.IsNullOrEmpty(prefix))
        {
            prefix = DetectBestPrefix(clipNames);
        }

        if (string.IsNullOrEmpty(prefix))
        {
            return;
        }

        animationPrefix = prefix;

        string foundDownIdle = FindClipName(clipNames, prefix, "Idle", "Down");
        string foundUpIdle = FindClipName(clipNames, prefix, "Idle", "Up");

        string foundRightIdle = FindClipName(clipNames, prefix, "Idle", "Right");
        string foundLeftIdle = FindClipName(clipNames, prefix, "Idle", "Left");

        string foundDownWalk = FindClipName(clipNames, prefix, "Walk", "Down");
        string foundUpWalk = FindClipName(clipNames, prefix, "Walk", "Up");

        string foundRightWalk = FindClipName(clipNames, prefix, "Walk", "Right");
        string foundLeftWalk = FindClipName(clipNames, prefix, "Walk", "Left");

        if (!string.IsNullOrEmpty(foundDownIdle))
        {
            downIdleState = foundDownIdle;
        }

        if (!string.IsNullOrEmpty(foundUpIdle))
        {
            upIdleState = foundUpIdle;
        }

        if (!string.IsNullOrEmpty(foundRightIdle))
        {
            sideIdleState = foundRightIdle;
            sideSpriteFacesRight = true;
        }
        else if (!string.IsNullOrEmpty(foundLeftIdle))
        {
            sideIdleState = foundLeftIdle;
            sideSpriteFacesRight = false;
        }

        if (!string.IsNullOrEmpty(foundDownWalk))
        {
            downWalkState = foundDownWalk;
        }

        if (!string.IsNullOrEmpty(foundUpWalk))
        {
            upWalkState = foundUpWalk;
        }
        else if (!string.IsNullOrEmpty(foundUpIdle))
        {
            upWalkState = foundUpIdle;
        }

        if (!string.IsNullOrEmpty(foundRightWalk))
        {
            sideWalkState = foundRightWalk;
            sideSpriteFacesRight = true;
        }
        else if (!string.IsNullOrEmpty(foundLeftWalk))
        {
            sideWalkState = foundLeftWalk;
            sideSpriteFacesRight = false;
        }
    }

    List<string> GetAnimationClipNames()
    {
        List<string> names = new List<string>();

        if (targetAnimator == null ||
            targetAnimator.runtimeAnimatorController == null)
        {
            return names;
        }

        AnimationClip[] clips = targetAnimator.runtimeAnimatorController.animationClips;

        foreach (AnimationClip clip in clips)
        {
            if (clip == null)
                continue;

            if (!names.Contains(clip.name))
            {
                names.Add(clip.name);
            }
        }

        return names;
    }

    string DetectBestPrefix(List<string> clipNames)
    {
        Dictionary<string, int> scores = new Dictionary<string, int>();

        string[] suffixes =
        {
            "_Idle_Down",
            "_Idle_Up",
            "_Idle_Left",
            "_Idle_Right",
            "_Walk_Down",
            "_Walk_Up",
            "_Walk_Left",
            "_Walk_Right",

            "_down_Idle",
            "_up_Idle",
            "_left_Idle",
            "_right_Idle",
            "_down_walk",
            "_up_walk",
            "_left_walk",
            "_right_walk"
        };

        foreach (string clipName in clipNames)
        {
            foreach (string suffix in suffixes)
            {
                if (clipName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    string prefix = clipName.Substring(0, clipName.Length - suffix.Length);

                    if (string.IsNullOrEmpty(prefix))
                        continue;

                    if (!scores.ContainsKey(prefix))
                    {
                        scores[prefix] = 0;
                    }

                    scores[prefix] += 10;
                }
            }
        }

        string bestPrefix = "";
        int bestScore = -1;

        foreach (KeyValuePair<string, int> pair in scores)
        {
            int score = pair.Value;

            string normalizedPrefix = NormalizeName(pair.Key);
            string normalizedObjectName = NormalizeName(gameObject.name);

            if (!string.IsNullOrEmpty(normalizedPrefix) &&
                normalizedObjectName.Contains(normalizedPrefix))
            {
                score += 100;
            }

            if (targetSpriteRenderer != null &&
                targetSpriteRenderer.sprite != null)
            {
                string normalizedSpriteName = NormalizeName(targetSpriteRenderer.sprite.name);

                if (!string.IsNullOrEmpty(normalizedPrefix) &&
                    normalizedSpriteName.Contains(normalizedPrefix))
                {
                    score += 50;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestPrefix = pair.Key;
            }
        }

        return bestPrefix;
    }

    string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        value = value.ToLowerInvariant();

        string result = "";

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (char.IsLetterOrDigit(c))
            {
                result += c;
            }
        }

        return result;
    }

    string FindClipName(List<string> clipNames, string prefix, string action, string direction)
    {
        string[] possibleNames =
        {
            prefix + "_" + action + "_" + direction,
            prefix + "_" + direction + "_" + action,
            prefix + "_" + action.ToLower() + "_" + direction.ToLower(),
            prefix + "_" + direction.ToLower() + "_" + action.ToLower()
        };

        foreach (string possibleName in possibleNames)
        {
            foreach (string clipName in clipNames)
            {
                if (string.Equals(clipName, possibleName, StringComparison.OrdinalIgnoreCase))
                {
                    return clipName;
                }
            }
        }

        return "";
    }

    Vector2 GetVelocity()
    {
        Vector2 velocity = Vector2.zero;

        if (targetRigidbody != null)
        {
            velocity = targetRigidbody.linearVelocity;
        }

        if (velocity.sqrMagnitude <= movingThreshold * movingThreshold &&
            Time.deltaTime > 0f)
        {
            Vector3 positionDelta = transform.position - lastPosition;
            velocity = positionDelta / Time.deltaTime;
        }

        lastPosition = transform.position;
        return velocity;
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
            if (logMissingState)
            {
                Debug.LogWarning("Animator không có state tên: " + stateName, gameObject);
            }

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

        string lowerWalk = stateName.Replace("_Walk", "_walk");
        hash = Animator.StringToHash(lowerWalk);

        if (targetAnimator.HasState(0, hash))
        {
            return hash;
        }

        string upperWalk = stateName.Replace("_walk", "_Walk");
        hash = Animator.StringToHash(upperWalk);

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
        if (!flipSideByDirection)
        {
            return;
        }

        bool isSideDirection =
            Mathf.Abs(lastDirection.x) > Mathf.Abs(lastDirection.y);

        if (!isSideDirection)
        {
            ResetFlipWhenVertical();
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
            : transform;

        if (flipTarget == null)
        {
            return;
        }

        Vector3 scale = flipTarget.localScale;
        scale.x = usePositiveScale ? originalScaleX : -originalScaleX;
        flipTarget.localScale = scale;
    }

    void ResetFlipWhenVertical()
    {
        if (useSpriteRendererFlip && targetSpriteRenderer != null)
        {
            targetSpriteRenderer.flipX = false;
            return;
        }

        Transform flipTarget =
            visualRoot != null && visualRoot != transform
            ? visualRoot
            : transform;

        if (flipTarget == null)
        {
            return;
        }

        Vector3 scale = flipTarget.localScale;
        scale.x = originalScaleX;
        flipTarget.localScale = scale;
    }

    void SetParameters(bool isMoving, Vector2 velocity, float speed)
    {
        Vector2 direction = isMoving ? velocity.normalized : lastDirection;

        SetBoolIfExists(isMovingParameter, isMoving);
        SetFloatIfExists(moveXParameter, direction.x);
        SetFloatIfExists(moveYParameter, direction.y);
        SetFloatIfExists(speedParameter, speed);
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