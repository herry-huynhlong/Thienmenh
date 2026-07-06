using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterDirectionalAnimator : MonoBehaviour
{
    static readonly string[] Actions = { "walk", "lie", "attack" };
    static readonly string[] Directions = { "down", "left", "right", "up" };

    [Header("Components")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("State Names")]
    public string prefix = "yeuthu";
    public bool autoDetectPrefix = true;
    public bool playLieWhenIdle = true;

    [Header("Timing")]
    public float attackLockTime = 0.45f;

    string activePrefix = "yeuthu";
    string lastDirection = "down";
    string currentState = "";
    bool isMoving;
    bool isAttacking;
    bool isDead;

    bool ShouldLogAnimationDebug()
    {
        MonsterAI monster = GetComponent<MonsterAI>();
        return monster != null &&
            (monster.debugFlowLogs || monster.name.IndexOf("YeuThu", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        ResolveAnimationPrefix();
        RefreshMovementState();
    }

    void OnValidate()
    {
        activePrefix = NormalizePrefix(prefix);
    }

    public void SetMoveDirection(Vector2 direction)
    {
        if (isDead || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        lastDirection = ResolveDirection(direction);
    }

    public void SetMoving(bool moving)
    {
        if (isDead)
        {
            return;
        }

        isMoving = moving;
        if (!isAttacking)
        {
            RefreshMovementState();
        }
    }

    public void PlayAttack(Vector2 direction)
    {
        if (isDead || isAttacking)
        {
            if (ShouldLogAnimationDebug())
            {
                Debug.LogWarning(
                    "[MonsterDirectionalAnimator] Skip PlayAttack object=" +
                    gameObject.name +
                    " isDead=" + isDead +
                    " isAttacking=" + isAttacking +
                    " currentState=" + currentState);
            }
            return;
        }

        SetMoveDirection(direction);
        if (ShouldLogAnimationDebug())
        {
            Debug.LogWarning(
                "[MonsterDirectionalAnimator] PlayAttack object=" +
                gameObject.name +
                " direction=" + direction.ToString("F2") +
                " resolvedDirection=" + lastDirection +
                " attackState=" + BuildStateName("attack") +
                " controller=" +
                (animator != null && animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "null"));
        }
        StartCoroutine(AttackRoutine());
    }

    public void PlayRespawn()
    {
        isDead = false;
        isAttacking = false;
        isMoving = false;
        currentState = "";
        StopAllCoroutines();
        ResolveAnimationPrefix();
        RefreshMovementState();
    }

    public void PlayDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        isAttacking = false;
        isMoving = false;
        StopAllCoroutines();
        PlayState(BuildStateName("lie"));
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        string attackState = BuildStateName("attack");
        if (ShouldLogAnimationDebug())
        {
            Debug.LogWarning(
                "[MonsterDirectionalAnimator] AttackRoutine start object=" +
                gameObject.name +
                " state=" + attackState +
                " attackLockTime=" + attackLockTime.ToString("0.00"));
        }

        PlayState(attackState);
        yield return new WaitForSeconds(Mathf.Max(0f, attackLockTime));
        isAttacking = false;
        if (ShouldLogAnimationDebug())
        {
            Debug.LogWarning(
                "[MonsterDirectionalAnimator] AttackRoutine end object=" +
                gameObject.name +
                " nextState=" + (isMoving ? BuildStateName("walk") : BuildStateName("lie")));
        }
        RefreshMovementState();
    }

    void RefreshMovementState()
    {
        if (isDead || animator == null)
        {
            return;
        }

        if (isMoving)
        {
            PlayState(BuildStateName("walk"));
        }
        else if (playLieWhenIdle)
        {
            PlayState(BuildStateName("lie"));
        }
    }

    void ResolveAnimationPrefix()
    {
        activePrefix = NormalizePrefix(prefix);

        if (!autoDetectPrefix || animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        HashSet<string> clipNames = GetClipNames();
        if (HasAnyDirectionalClip(clipNames, activePrefix))
        {
            return;
        }

        string detectedPrefix = DetectBestPrefix(clipNames);
        if (!string.IsNullOrEmpty(detectedPrefix))
        {
            activePrefix = detectedPrefix;
        }
    }

    HashSet<string> GetClipNames()
    {
        HashSet<string> clipNames = new HashSet<string>();
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && !string.IsNullOrEmpty(clips[i].name))
            {
                clipNames.Add(clips[i].name);
            }
        }

        return clipNames;
    }

    bool HasAnyDirectionalClip(HashSet<string> clipNames, string statePrefix)
    {
        if (string.IsNullOrEmpty(statePrefix))
        {
            return false;
        }

        for (int actionIndex = 0; actionIndex < Actions.Length; actionIndex++)
        {
            for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                if (clipNames.Contains(statePrefix + "_" + Actions[actionIndex] + "_" + Directions[directionIndex]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    string DetectBestPrefix(HashSet<string> clipNames)
    {
        Dictionary<string, int> scores = new Dictionary<string, int>();

        foreach (string clipName in clipNames)
        {
            for (int actionIndex = 0; actionIndex < Actions.Length; actionIndex++)
            {
                for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
                {
                    string suffix = "_" + Actions[actionIndex] + "_" + Directions[directionIndex];
                    if (!clipName.EndsWith(suffix))
                    {
                        continue;
                    }

                    string candidate = clipName.Substring(0, clipName.Length - suffix.Length);
                    if (string.IsNullOrEmpty(candidate))
                    {
                        continue;
                    }

                    scores.TryGetValue(candidate, out int score);
                    scores[candidate] = score + 1;
                }
            }
        }

        string bestPrefix = "";
        int bestScore = 0;
        foreach (KeyValuePair<string, int> score in scores)
        {
            if (score.Value > bestScore)
            {
                bestPrefix = score.Key;
                bestScore = score.Value;
            }
        }

        return bestPrefix;
    }

    string BuildStateName(string action)
    {
        return activePrefix + "_" + action + "_" + lastDirection;
    }

    string NormalizePrefix(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "yeuthu" : value.Trim();
    }

    string ResolveDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x > 0f ? "right" : "left";
        }

        return direction.y > 0f ? "up" : "down";
    }

    void PlayState(string stateName)
    {
        if (animator == null)
        {
            if (ShouldLogAnimationDebug())
            {
                Debug.LogWarning(
                    "[MonsterDirectionalAnimator] Missing Animator object=" +
                    gameObject.name +
                    " requestedState=" + stateName);
            }
            return;
        }

        if (currentState == stateName)
        {
            if (ShouldLogAnimationDebug())
            {
                Debug.LogWarning(
                    "[MonsterDirectionalAnimator] Skip same state object=" +
                    gameObject.name +
                    " state=" + stateName);
            }
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        bool hasState =
            animator.runtimeAnimatorController != null &&
            animator.HasState(0, stateHash);

        if (ShouldLogAnimationDebug())
        {
            Debug.LogWarning(
                "[MonsterDirectionalAnimator] PlayState object=" +
                gameObject.name +
                " state=" + stateName +
                " hasState=" + hasState +
                " currentState=" + currentState +
                " controller=" +
                (animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "null"));
        }

        currentState = stateName;
        animator.Play(stateName, 0, 0f);
    }
}
