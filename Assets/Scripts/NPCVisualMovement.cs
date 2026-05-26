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

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private AnimatorOverrideController overrideController;
    private string overrideClipName = "OverrideTargetState";
    private Vector2 lastDirection = Vector2.down;

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

        if (isIdling)
        {
            if (lastDirection == Vector2.up) clipToPlay = upIdleClip;
            else if (lastDirection == Vector2.down) clipToPlay = downIdleClip;
            else if (lastDirection == Vector2.right || lastDirection == Vector2.left) clipToPlay = sideIdleClip;
        }
        else
        {
            lastDirection = moveDirection; // Lưu lại hướng đi cuối cùng

            if (moveDirection.y > 0.1f) clipToPlay = upWalkClip;
            else if (moveDirection.y < -0.1f) clipToPlay = downWalkClip;
            else if (Mathf.Abs(moveDirection.x) > 0.1f) 
            {
                clipToPlay = sideWalkClip;
                
                // Tự động lật mặt trái/phải dựa vào hướng đi thực tế
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = (moveDirection.x < 0); // Đi sang trái thì lật hình
                }
            }
        }

        if (clipToPlay != null && overrideController[overrideClipName] != clipToPlay)
        {
            overrideController[overrideClipName] = clipToPlay;
            animator.Play(overrideClipName, 0, 0f);
        }
    }
}