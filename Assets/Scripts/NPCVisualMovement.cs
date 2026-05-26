using UnityEngine;

public class NPCVisualAnimation : MonoBehaviour
{
    [Header("Kéo thả đủ 6 file Hoạt ảnh (Tam giác xanh) vào đây")]
    public AnimationClip downWalkClip; 
    public AnimationClip upWalkClip;   
    public AnimationClip sideWalkClip; 
    public AnimationClip downIdleClip; 
    public AnimationClip upIdleClip;   
    public AnimationClip sideIdleClip; 

    [Header("Side Facing")]
    public bool sideSpriteFacesRight = false;
    public bool invertSideFlip;
    public float directionDeadZone = 0.08f;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private AnimatorOverrideController overrideController;
    private string overrideClipName = "OverrideTargetState";
    private Vector2 lastDirection = Vector2.down;
    private AnimationClip currentClip;

    void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            AnimationClip[] originalClips = animator.runtimeAnimatorController.animationClips;
            if (originalClips.Length > 0)
            {
                overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
                overrideClipName = originalClips[0].name; 
                animator.runtimeAnimatorController = overrideController;
            }
        }
    }

    // HÀM ĐỂ SCRIPT DI CHUYỂN KHÁC GỌI VÀO (BẮT BUỘC PHẢI CÓ)
    public void UpdateNPCAnimation(Vector2 moveDirection, bool isIdling)
    {
        if (animator == null || overrideController == null) return;

        AnimationClip clipToPlay = null;

        if (!isIdling)
        {
            lastDirection = GetCardinalDirection(moveDirection);
        }

        if (isIdling)
        {
            if (lastDirection == Vector2.up) clipToPlay = upIdleClip;
            else if (lastDirection == Vector2.down) clipToPlay = downIdleClip;
            else
            {
                clipToPlay = sideIdleClip;
                ApplySideFlip(lastDirection);
            }
        }
        else
        {
            if (lastDirection == Vector2.up) clipToPlay = upWalkClip;
            else if (lastDirection == Vector2.down) clipToPlay = downWalkClip;
            else
            {
                clipToPlay = sideWalkClip;
                ApplySideFlip(lastDirection);
            }
        }

        if (clipToPlay != null && currentClip != clipToPlay)
        {
            overrideController[overrideClipName] = clipToPlay;
            animator.Play(overrideClipName, 0, 0f);
            currentClip = clipToPlay;
        }
    }

    private Vector2 GetCardinalDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < directionDeadZone * directionDeadZone)
        {
            return lastDirection;
        }

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x < 0 ? Vector2.left : Vector2.right;
        }

        return direction.y < 0 ? Vector2.down : Vector2.up;
    }

    private void ApplySideFlip(Vector2 direction)
    {
        if (spriteRenderer == null ||
            Mathf.Abs(direction.x) < directionDeadZone)
        {
            return;
        }

        bool movingRight = direction.x > 0f;
        bool shouldFlip =
            sideSpriteFacesRight
            ? !movingRight
            : movingRight;

        spriteRenderer.flipX =
            invertSideFlip
            ? !shouldFlip
            : shouldFlip;
    }
}
