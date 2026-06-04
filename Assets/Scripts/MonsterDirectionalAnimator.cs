using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterDirectionalAnimator : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("State Names")]
    public string prefix = "yeuthu";
    public bool playLieWhenIdle = true;

    [Header("Timing")]
    public float attackLockTime = 0.45f;

    string lastDirection = "down";
    string currentState = "";
    bool isMoving;
    bool isAttacking;
    bool isDead;

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

        RefreshMovementState();
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
            return;
        }

        SetMoveDirection(direction);
        StartCoroutine(AttackRoutine());
    }

    public void PlayRespawn()
    {
        isDead = false;
        isAttacking = false;
        isMoving = false;
        currentState = "";
        StopAllCoroutines();
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
        PlayState(BuildStateName("attack"));
        yield return new WaitForSeconds(Mathf.Max(0f, attackLockTime));
        isAttacking = false;
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

    string BuildStateName(string action)
    {
        return prefix + "_" + action + "_" + lastDirection;
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
        if (animator == null || currentState == stateName)
        {
            return;
        }

        currentState = stateName;
        animator.Play(stateName, 0, 0f);
    }
}