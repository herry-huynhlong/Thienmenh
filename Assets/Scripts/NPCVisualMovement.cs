using UnityEngine;

public class NPCVisualAnimation : MonoBehaviour
{
    [Header("Kéo thả đủ 6 file Hoạt ảnh (Tam giác xanh) vào đây")]
    public AnimationClip downWalkClip; 
    public AnimationClip upWalkClip;   
    public AnimationClip sideWalkClip; 
    [HideInInspector] public AnimationClip rightWalkClip;
    [HideInInspector] public AnimationClip leftWalkClip;
    public AnimationClip downIdleClip; 
    public AnimationClip upIdleClip;   
    public AnimationClip sideIdleClip; 
    [HideInInspector] public AnimationClip rightIdleClip;
    [HideInInspector] public AnimationClip leftIdleClip;

    [Header("Side Facing")]
    public bool sideSpriteFacesRight = false;
    public bool invertSideFlip;
    [Header("Vertical Facing")]
    public bool invertVerticalFacing = false;
    public float directionDeadZone = 0.08f;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private AnimatorOverrideController overrideController;
    private string overrideClipName = "OverrideTargetState";
    private Vector2 lastDirection = Vector2.down;
    private AnimationClip currentClip;

    public static NPCVisualAnimation EnsureOn(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        NPCVisualAnimation visual = owner.GetComponent<NPCVisualAnimation>();
        if (visual == null)
        {
            visual = owner.AddComponent<NPCVisualAnimation>();
        }

        if (HasCompleteClips(visual))
        {
            return visual;
        }

        TryAssignClipsFromAnimator(visual);

        if (HasCompleteClips(visual))
        {
            return visual;
        }

        NPCVisualAnimation template = FindTemplate(visual);
        if (template != null)
        {
            CopyTemplate(template, visual);
        }

        return visual;
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (!HasCompleteClips(this))
        {
            TryAssignClipsFromAnimator(this);
        }

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
            lastDirection = GetFacingDirection(moveDirection);
        }

        if (isIdling)
        {
            if (lastDirection == Vector2.up) clipToPlay = upIdleClip;
            else if (lastDirection == Vector2.down) clipToPlay = downIdleClip;
            else
            {
                clipToPlay = GetSideIdleClip(lastDirection);
            }
        }
        else
        {
            if (lastDirection == Vector2.up) clipToPlay = upWalkClip;
            else if (lastDirection == Vector2.down) clipToPlay = downWalkClip;
            else
            {
                clipToPlay = GetSideWalkClip(lastDirection);
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

    private Vector2 GetFacingDirection(Vector2 direction)
    {
        Vector2 facing = GetCardinalDirection(direction);

        if (!invertVerticalFacing)
        {
            return facing;
        }

        if (facing == Vector2.up)
        {
            return Vector2.down;
        }

        if (facing == Vector2.down)
        {
            return Vector2.up;
        }

        return facing;
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

    AnimationClip GetSideWalkClip(Vector2 direction)
    {
        if (direction.x > directionDeadZone && rightWalkClip != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }
            return rightWalkClip;
        }

        if (direction.x < -directionDeadZone && leftWalkClip != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }
            return leftWalkClip;
        }

        ApplySideFlip(direction);
        return sideWalkClip;
    }

    AnimationClip GetSideIdleClip(Vector2 direction)
    {
        if (direction.x > directionDeadZone && rightIdleClip != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }
            return rightIdleClip;
        }

        if (direction.x < -directionDeadZone && leftIdleClip != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }
            return leftIdleClip;
        }

        ApplySideFlip(direction);
        return sideIdleClip;
    }

    static bool HasCompleteClips(NPCVisualAnimation visual)
    {
        return visual != null &&
            visual.downWalkClip != null &&
            visual.upWalkClip != null &&
            visual.sideWalkClip != null &&
            visual.downIdleClip != null &&
            visual.upIdleClip != null &&
            visual.sideIdleClip != null;
    }

    static NPCVisualAnimation FindTemplate(NPCVisualAnimation target)
    {
        NPCVisualAnimation[] visuals =
            Object.FindObjectsByType<NPCVisualAnimation>(FindObjectsInactive.Exclude);

        for (int i = 0; i < visuals.Length; i++)
        {
            NPCVisualAnimation candidate = visuals[i];
            if (candidate == null ||
                candidate == target ||
                !HasCompleteClips(candidate))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    static void CopyTemplate(NPCVisualAnimation source, NPCVisualAnimation target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.downWalkClip = source.downWalkClip;
        target.upWalkClip = source.upWalkClip;
        target.sideWalkClip = source.sideWalkClip;
        target.rightWalkClip = source.rightWalkClip;
        target.leftWalkClip = source.leftWalkClip;
        target.downIdleClip = source.downIdleClip;
        target.upIdleClip = source.upIdleClip;
        target.sideIdleClip = source.sideIdleClip;
        target.rightIdleClip = source.rightIdleClip;
        target.leftIdleClip = source.leftIdleClip;
        target.sideSpriteFacesRight = source.sideSpriteFacesRight;
        target.invertSideFlip = source.invertSideFlip;
        target.invertVerticalFacing = source.invertVerticalFacing;
        target.directionDeadZone = source.directionDeadZone;
    }

    static void TryAssignClipsFromAnimator(NPCVisualAnimation visual)
    {
        if (visual == null)
        {
            return;
        }

        Animator visualAnimator = visual.GetComponent<Animator>();
        RuntimeAnimatorController controller =
            visualAnimator != null ? visualAnimator.runtimeAnimatorController : null;

        if (controller == null)
        {
            return;
        }

        AnimationClip[] clips = controller.animationClips;
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        bool assignedFromAnimator = false;

        if (visual.downWalkClip == null)
        {
            visual.downWalkClip = FindBestClip(clips, "walk", "down");
            assignedFromAnimator |= visual.downWalkClip != null;
        }

        if (visual.upWalkClip == null)
        {
            visual.upWalkClip = FindBestClip(clips, "walk", "up");
            assignedFromAnimator |= visual.upWalkClip != null;
        }

        if (visual.sideWalkClip == null)
        {
            AnimationClip rightWalk = FindBestClip(clips, "walk", "right");
            AnimationClip leftWalk = FindBestClip(clips, "walk", "left");
            AnimationClip genericWalk = FindBestClip(clips, "walk", "side");
            visual.rightWalkClip = rightWalk;
            visual.leftWalkClip = leftWalk;
            visual.sideWalkClip = rightWalk ?? leftWalk ?? genericWalk;
            assignedFromAnimator |= visual.sideWalkClip != null;

            if (rightWalk != null && visual.sideWalkClip == rightWalk)
            {
                visual.sideSpriteFacesRight = true;
            }
            else if (leftWalk != null && visual.sideWalkClip == leftWalk)
            {
                visual.sideSpriteFacesRight = false;
            }
        }

        if (visual.downIdleClip == null)
        {
            visual.downIdleClip =
                FindBestClip(clips, "idle", "down") ??
                FindBestClip(clips, "lie", "down");
            assignedFromAnimator |= visual.downIdleClip != null;
        }

        if (visual.upIdleClip == null)
        {
            visual.upIdleClip =
                FindBestClip(clips, "idle", "up") ??
                FindBestClip(clips, "lie", "up");
            assignedFromAnimator |= visual.upIdleClip != null;
        }

        if (visual.sideIdleClip == null)
        {
            AnimationClip rightIdle =
                FindBestClip(clips, "idle", "right") ??
                FindBestClip(clips, "lie", "right");
            AnimationClip leftIdle =
                FindBestClip(clips, "idle", "left") ??
                FindBestClip(clips, "lie", "left");
            AnimationClip genericIdle = FindBestClip(clips, "idle", "side");
            visual.rightIdleClip = rightIdle;
            visual.leftIdleClip = leftIdle;
            visual.sideIdleClip = rightIdle ?? leftIdle ?? genericIdle;
            assignedFromAnimator |= visual.sideIdleClip != null;

            if (rightIdle != null && visual.sideIdleClip == rightIdle)
            {
                visual.sideSpriteFacesRight = true;
            }
            else if (leftIdle != null && visual.sideIdleClip == leftIdle)
            {
                visual.sideSpriteFacesRight = false;
            }
        }

        if (assignedFromAnimator)
        {
            visual.invertVerticalFacing = false;
            visual.invertSideFlip = false;
        }
    }

    static AnimationClip FindBestClip(
        AnimationClip[] clips,
        string primaryKeyword,
        string directionKeyword)
    {
        if (clips == null)
        {
            return null;
        }

        string primary = NormalizeClipName(primaryKeyword);
        string direction = NormalizeClipName(directionKeyword);

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            string clipName = NormalizeClipName(clip.name);
            if (clipName.Contains(primary) &&
                clipName.Contains(direction))
            {
                return clip;
            }
        }

        return null;
    }

    static string NormalizeClipName(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("_", "").Replace(" ", "").ToLowerInvariant();
    }
}
