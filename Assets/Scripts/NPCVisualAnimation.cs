using System.Collections.Generic;
using UnityEngine;

public class NPCVisualAnimation : MonoBehaviour
{
    [Header("Movement Clips")]
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

    [Header("Action Clips")]
    public AnimationClip attackDownClip;
    public AnimationClip attackUpClip;
    public AnimationClip attackSideClip;
    [HideInInspector] public AnimationClip rightAttackClip;
    [HideInInspector] public AnimationClip leftAttackClip;

    public AnimationClip cultivateDownClip;
    public AnimationClip cultivateUpClip;
    public AnimationClip cultivateSideClip;
    [HideInInspector] public AnimationClip rightCultivateClip;
    [HideInInspector] public AnimationClip leftCultivateClip;

    public AnimationClip dieDownClip;
    public AnimationClip dieUpClip;
    public AnimationClip dieSideClip;
    [HideInInspector] public AnimationClip rightDieClip;
    [HideInInspector] public AnimationClip leftDieClip;

    [Header("Side Facing")]
    public bool sideSpriteFacesRight = false;
    public bool invertSideFlip;

    [Header("Vertical Facing")]
    public bool invertVerticalFacing = false;
    public float directionDeadZone = 0.08f;

    [Header("Debug")]
    public bool debugVisualLogs;

    Animator animator;
    SpriteRenderer spriteRenderer;
    AnimatorOverrideController overrideController;
    string overrideClipName = "OverrideTargetState";
    int overrideStateHash;
    Vector2 lastDirection = Vector2.down;
    AnimationClip currentClip;

    enum ActionCategory
    {
        None,
        Attack,
        Cultivate,
        Die
    }

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

        RebindAnimatorController(
            animator != null ? animator.runtimeAnimatorController : null);
    }

    public void RebindAnimatorController(
        RuntimeAnimatorController controller)
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (animator == null)
        {
            return;
        }

        currentClip = null;

        if (controller == null)
        {
            overrideController = null;
            return;
        }

        animator.runtimeAnimatorController = controller;
        TryAssignClipsFromAnimator(this);

        AnimationClip[] originalClips = controller.animationClips;
        if (originalClips != null &&
            originalClips.Length > 0)
        {
            overrideController =
                new AnimatorOverrideController(controller);
            overrideClipName = originalClips[0].name;
            animator.runtimeAnimatorController = overrideController;
        }
        else
        {
            overrideController = null;
        }

        animator.Rebind();
        animator.Update(0f);
        AnimatorStateInfo currentState =
            animator.GetCurrentAnimatorStateInfo(0);
        overrideStateHash = currentState.fullPathHash != 0
            ? currentState.fullPathHash
            : currentState.shortNameHash;

        if (ShouldLogVisualDebug())
        {
            Debug.Log(
                "[NPCVisualAnimation] Rebind object=" +
                gameObject.name +
                " controller=" +
                (controller != null ? controller.name : "null") +
                " overrideClipName=" + overrideClipName +
                " stateHash=" + overrideStateHash +
                " attackDown=" + DescribeClip(attackDownClip) +
                " attackUp=" + DescribeClip(attackUpClip) +
                " attackSide=" + DescribeClip(attackSideClip) +
                " dieDown=" + DescribeClip(dieDownClip));
        }
    }

    public void UpdateNPCAnimation(
        Vector2 moveDirection,
        bool isIdling,
        string currentAction = "")
    {
        if (!EnsureRuntimeBinding())
        {
            return;
        }

        if (moveDirection.sqrMagnitude >
            directionDeadZone * directionDeadZone)
        {
            lastDirection = GetFacingDirection(moveDirection);
        }

        AnimationClip clipToPlay = null;
        ActionCategory actionCategory = ResolveActionCategory(currentAction);

        if (actionCategory != ActionCategory.None)
        {
            clipToPlay = GetActionClip(actionCategory, lastDirection);

            if (ShouldLogVisualDebug())
            {
                Debug.Log(
                    "[NPCVisualAnimation] Action resolve object=" +
                    gameObject.name +
                    " action=" + currentAction +
                    " category=" + actionCategory +
                    " direction=" + lastDirection +
                    " clip=" + DescribeClip(clipToPlay));
            }

            if (clipToPlay != null)
            {
                PlayClip(clipToPlay);
                return;
            }

            Debug.LogWarning(
                "[NPCVisualAnimation] Missing action clip object=" +
                gameObject.name +
                " action=" + currentAction +
                " direction=" + lastDirection);

            if (actionCategory == ActionCategory.Die)
            {
                return;
            }
        }

        if (clipToPlay == null)
        {
            clipToPlay = isIdling
                ? GetIdleClip(lastDirection)
                : GetWalkClip(lastDirection);
        }

        PlayClip(clipToPlay);
    }

    public void SetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <
            directionDeadZone * directionDeadZone)
        {
            return;
        }

        lastDirection = GetFacingDirection(direction);
        currentClip = null;
    }

    public void SetFacingTarget(Vector3 targetPosition)
    {
        SetFacingDirection(targetPosition - transform.position);
    }

    public void ReplayActionAnimation(string currentAction)
    {
        if (!EnsureRuntimeBinding())
        {
            return;
        }

        ActionCategory actionCategory = ResolveActionCategory(currentAction);
        if (actionCategory == ActionCategory.None)
        {
            return;
        }

        AnimationClip clipToPlay =
            GetActionClip(actionCategory, lastDirection);
        if (ShouldLogVisualDebug())
        {
            Debug.Log(
                "[NPCVisualAnimation] Replay resolve object=" +
                gameObject.name +
                " action=" + currentAction +
                " category=" + actionCategory +
                " direction=" + lastDirection +
                " clip=" + DescribeClip(clipToPlay));
        }

        if (clipToPlay == null)
        {
            Debug.LogWarning(
                "[NPCVisualAnimation] Replay action clip missing object=" +
                gameObject.name +
                " action=" + currentAction +
                " direction=" + lastDirection);
            return;
        }

        currentClip = null;
        PlayClip(clipToPlay);
    }

    Vector2 GetCardinalDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <
            directionDeadZone * directionDeadZone)
        {
            return lastDirection;
        }

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x < 0 ? Vector2.left : Vector2.right;
        }

        return direction.y < 0 ? Vector2.down : Vector2.up;
    }

    Vector2 GetFacingDirection(Vector2 direction)
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

    ActionCategory ResolveActionCategory(string currentAction)
    {
        if (string.IsNullOrEmpty(currentAction))
        {
            return ActionCategory.None;
        }

        if (MatchesAction(currentAction, "dead") ||
            MatchesAction(currentAction, "oldAgeDeath"))
        {
            return ActionCategory.Die;
        }

        if (MatchesAction(currentAction, "attackMonsterNamed", true) ||
            MatchesAction(currentAction, "attackMonster", true) ||
            MatchesAction(currentAction, "attack", true) ||
            MatchesAction(currentAction, "fightBlockingMonster") ||
            MatchesAction(currentAction, "guardSpiritHerbMonster") ||
            MatchesAction(currentAction, "clearHarvestMonster") ||
            MatchesAction(currentAction, "outerSkirmishNamed", true) ||
            MatchesAction(currentAction, "fight") ||
            MatchesAction(currentAction, "rob") ||
            MatchesAction(currentAction, "revenge"))
        {
            return ActionCategory.Attack;
        }

        if (MatchesAction(currentAction, "cultivateAbsorbQi") ||
            MatchesAction(currentAction, "cultivate") ||
            MatchesAction(currentAction, "breakthrough") ||
            MatchesAction(currentAction, "breakthroughTo", true) ||
            MatchesAction(currentAction, "waitTribulation") ||
            MatchesAction(currentAction, "waitLightning") ||
            MatchesAction(currentAction, "waitLightningNamed", true))
        {
            return ActionCategory.Cultivate;
        }

        return ActionCategory.None;
    }

    bool MatchesAction(string action, string key, bool allowPrefix = false)
    {
        if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (string.Equals(
                action,
                key,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string pattern = NpcText.Action(key);
        if (!string.IsNullOrEmpty(pattern) &&
            string.Equals(
                action,
                pattern,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowPrefix)
        {
            return action.IndexOf(
                       key,
                       System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!string.IsNullOrEmpty(pattern) &&
                action.IndexOf(
                    pattern,
                    System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (ActionMatchesPrefix(action, key))
        {
            return true;
        }

        return ActionMatchesPrefix(action, pattern);
    }

    bool ActionMatchesPrefix(string action, string pattern)
    {
        if (string.IsNullOrEmpty(action) ||
            string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        int placeholderIndex = pattern.IndexOf('{');
        if (placeholderIndex < 0)
        {
            return action.StartsWith(
                pattern,
                System.StringComparison.OrdinalIgnoreCase);
        }

        string prefix = pattern.Substring(0, placeholderIndex).TrimEnd();
        return !string.IsNullOrEmpty(prefix) &&
            action.StartsWith(
                prefix,
                System.StringComparison.OrdinalIgnoreCase);
    }

    void PlayClip(AnimationClip clipToPlay)
    {
        if (!EnsureRuntimeBinding())
        {
            return;
        }

        if (clipToPlay == null || currentClip == clipToPlay)
        {
            return;
        }

        if (ShouldLogVisualDebug())
        {
            Debug.Log(
                "[NPCVisualAnimation] Play object=" +
                gameObject.name +
                " override=" + overrideClipName +
                " clip=" + DescribeClip(clipToPlay) +
                " facing=" + lastDirection);
        }

        overrideController[overrideClipName] = clipToPlay;
        if (overrideStateHash != 0)
        {
            animator.Play(overrideStateHash, 0, 0f);
        }
        animator.Update(0f);
        currentClip = clipToPlay;
    }

    AnimationClip GetActionClip(ActionCategory actionCategory, Vector2 direction)
    {
        switch (actionCategory)
        {
            case ActionCategory.Attack:
            {
                AnimationClip attackClip = GetDirectionalClip(
                    attackUpClip,
                    attackDownClip,
                    attackSideClip,
                    rightAttackClip,
                    leftAttackClip,
                    direction);
                return attackClip ?? GetAnyCombatClip(
                    attackUpClip,
                    attackDownClip,
                    attackSideClip,
                    rightAttackClip,
                    leftAttackClip);
            }

            case ActionCategory.Cultivate:
                return GetDirectionalClip(
                    cultivateUpClip,
                    cultivateDownClip,
                    cultivateSideClip,
                    rightCultivateClip,
                    leftCultivateClip,
                    direction);

            case ActionCategory.Die:
            {
                AnimationClip dieClip = GetDirectionalClip(
                    dieUpClip,
                    dieDownClip,
                    dieSideClip,
                    rightDieClip,
                    leftDieClip,
                    direction);
                return dieClip ?? GetAnyCombatClip(
                    dieUpClip,
                    dieDownClip,
                    dieSideClip,
                    rightDieClip,
                    leftDieClip);
            }
        }

        return null;
    }

    AnimationClip GetAnyCombatClip(
        AnimationClip upClip,
        AnimationClip downClip,
        AnimationClip sideClip,
        AnimationClip rightClip,
        AnimationClip leftClip)
    {
        return rightClip ??
            leftClip ??
            sideClip ??
            upClip ??
            downClip;
    }

    AnimationClip GetWalkClip(Vector2 direction)
    {
        return GetDirectionalClip(
            upWalkClip,
            downWalkClip,
            sideWalkClip,
            rightWalkClip,
            leftWalkClip,
            direction);
    }

    AnimationClip GetIdleClip(Vector2 direction)
    {
        return GetDirectionalClip(
            upIdleClip,
            downIdleClip,
            sideIdleClip,
            rightIdleClip,
            leftIdleClip,
            direction);
    }

    AnimationClip GetDirectionalClip(
        AnimationClip upClip,
        AnimationClip downClip,
        AnimationClip sideClip,
        AnimationClip rightClip,
        AnimationClip leftClip,
        Vector2 direction)
    {
        if (direction.x > directionDeadZone && rightClip != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }

            return rightClip;
        }

        if (direction.x < -directionDeadZone && leftClip != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }

            return leftClip;
        }

        if (direction == Vector2.up && upClip != null)
        {
            ApplySideFlip(direction);
            return upClip;
        }

        if (direction == Vector2.down && downClip != null)
        {
            ApplySideFlip(direction);
            return downClip;
        }

        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
            if (direction.y > 0f && upClip != null)
            {
                ApplySideFlip(direction);
                return upClip;
            }

            if (direction.y < 0f && downClip != null)
            {
                ApplySideFlip(direction);
                return downClip;
            }
        }

        ApplySideFlip(direction);
        return sideClip;
    }

    void ApplySideFlip(Vector2 direction)
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

    bool EnsureRuntimeBinding()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (animator == null)
        {
            return false;
        }

        RuntimeAnimatorController controller =
            animator.runtimeAnimatorController;
        if (controller == null)
        {
            overrideController = null;
            return false;
        }

        if (overrideController == null)
        {
            RebindAnimatorController(controller);
        }
        else
        {
            TryAssignActionClipsFromAnimator(
                this,
                controller.animationClips);
        }

        return overrideController != null;
    }

    bool ShouldLogVisualDebug()
    {
        return debugVisualLogs;
    }

    static string DescribeClip(AnimationClip clip)
    {
        return clip != null ? clip.name : "null";
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
            Object.FindObjectsByType<NPCVisualAnimation>(
                FindObjectsInactive.Exclude);

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

        target.attackDownClip = source.attackDownClip;
        target.attackUpClip = source.attackUpClip;
        target.attackSideClip = source.attackSideClip;
        target.rightAttackClip = source.rightAttackClip;
        target.leftAttackClip = source.leftAttackClip;

        target.cultivateDownClip = source.cultivateDownClip;
        target.cultivateUpClip = source.cultivateUpClip;
        target.cultivateSideClip = source.cultivateSideClip;
        target.rightCultivateClip = source.rightCultivateClip;
        target.leftCultivateClip = source.leftCultivateClip;

        target.dieDownClip = source.dieDownClip;
        target.dieUpClip = source.dieUpClip;
        target.dieSideClip = source.dieSideClip;
        target.rightDieClip = source.rightDieClip;
        target.leftDieClip = source.leftDieClip;

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
            visualAnimator != null
                ? visualAnimator.runtimeAnimatorController
                : null;

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

        if (visual.rightWalkClip == null)
        {
            visual.rightWalkClip = FindBestClip(clips, "walk", "right");
        }

        if (visual.leftWalkClip == null)
        {
            visual.leftWalkClip = FindBestClip(clips, "walk", "left");
        }

        if (visual.sideWalkClip == null)
        {
            AnimationClip genericWalk = FindBestClip(clips, "walk", "side");
            visual.sideWalkClip =
                visual.rightWalkClip ??
                visual.leftWalkClip ??
                genericWalk;
        }

        assignedFromAnimator |=
            visual.rightWalkClip != null ||
            visual.leftWalkClip != null ||
            visual.sideWalkClip != null;

        if (visual.rightWalkClip != null &&
            visual.sideWalkClip == visual.rightWalkClip)
        {
            visual.sideSpriteFacesRight = true;
        }
        else if (visual.leftWalkClip != null &&
            visual.sideWalkClip == visual.leftWalkClip)
        {
            visual.sideSpriteFacesRight = false;
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

        if (visual.rightIdleClip == null)
        {
            visual.rightIdleClip =
                FindBestClip(clips, "idle", "right") ??
                FindBestClip(clips, "lie", "right");
        }

        if (visual.leftIdleClip == null)
        {
            visual.leftIdleClip =
                FindBestClip(clips, "idle", "left") ??
                FindBestClip(clips, "lie", "left");
        }

        if (visual.sideIdleClip == null)
        {
            AnimationClip genericIdle = FindBestClip(clips, "idle", "side");
            visual.sideIdleClip =
                visual.rightIdleClip ??
                visual.leftIdleClip ??
                genericIdle;
        }

        assignedFromAnimator |=
            visual.rightIdleClip != null ||
            visual.leftIdleClip != null ||
            visual.sideIdleClip != null;

        if (visual.rightIdleClip != null &&
            visual.sideIdleClip == visual.rightIdleClip)
        {
            visual.sideSpriteFacesRight = true;
        }
        else if (visual.leftIdleClip != null &&
            visual.sideIdleClip == visual.leftIdleClip)
        {
            visual.sideSpriteFacesRight = false;
        }

        TryAssignActionClipsFromAnimator(visual, clips);

        if (assignedFromAnimator)
        {
            visual.invertVerticalFacing = false;
            visual.invertSideFlip = false;
        }
    }

    static void TryAssignActionClipsFromAnimator(
        NPCVisualAnimation visual,
        AnimationClip[] clips)
    {
        if (visual == null || clips == null || clips.Length == 0)
        {
            return;
        }

        if (visual.attackDownClip == null)
        {
            visual.attackDownClip =
                FindBestClip(clips, "attack", "down") ??
                FindBestClip(clips, "fight", "down") ??
                FindBestClip(clips, "hit", "down");
        }

        if (visual.attackUpClip == null)
        {
            visual.attackUpClip =
                FindBestClip(clips, "attack", "up") ??
                FindBestClip(clips, "fight", "up") ??
                FindBestClip(clips, "hit", "up");
        }

        if (visual.rightAttackClip == null)
        {
            visual.rightAttackClip =
                FindBestClip(clips, "attack", "right") ??
                FindBestClip(clips, "fight", "right") ??
                FindBestClip(clips, "hit", "right");
        }

        if (visual.leftAttackClip == null)
        {
            visual.leftAttackClip =
                FindBestClip(clips, "attack", "left") ??
                FindBestClip(clips, "fight", "left") ??
                FindBestClip(clips, "hit", "left");
        }

        if (visual.attackSideClip == null)
        {
            visual.attackSideClip =
                visual.rightAttackClip ??
                visual.leftAttackClip ??
                FindBestClip(clips, "attack", "side") ??
                FindBestClip(clips, "fight", "side");
        }

        if (visual.rightAttackClip != null &&
            visual.attackSideClip == visual.rightAttackClip)
        {
            visual.sideSpriteFacesRight = true;
        }
        else if (visual.leftAttackClip != null &&
            visual.attackSideClip == visual.leftAttackClip)
        {
            visual.sideSpriteFacesRight = false;
        }

        if (visual.cultivateDownClip == null)
        {
            visual.cultivateDownClip =
                FindBestClip(clips, "cultivate", "down") ??
                FindBestClip(clips, "lie", "down") ??
                FindBestClip(clips, "sit", "down") ??
                FindBestClip(clips, "meditate", "down");
        }

        if (visual.cultivateUpClip == null)
        {
            visual.cultivateUpClip =
                FindBestClip(clips, "cultivate", "up") ??
                FindBestClip(clips, "lie", "up") ??
                FindBestClip(clips, "sit", "up") ??
                FindBestClip(clips, "meditate", "up");
        }

        if (visual.cultivateSideClip == null)
        {
            visual.rightCultivateClip =
                FindBestClip(clips, "cultivate", "right") ??
                FindBestClip(clips, "lie", "right") ??
                FindBestClip(clips, "sit", "right") ??
                FindBestClip(clips, "meditate", "right");
            visual.leftCultivateClip =
                FindBestClip(clips, "cultivate", "left") ??
                FindBestClip(clips, "lie", "left") ??
                FindBestClip(clips, "sit", "left") ??
                FindBestClip(clips, "meditate", "left");
            visual.cultivateSideClip =
                visual.rightCultivateClip ??
                visual.leftCultivateClip ??
                FindBestClip(clips, "cultivate", "side") ??
                FindBestClip(clips, "lie", "side") ??
                FindBestClip(clips, "sit", "side") ??
                FindBestClip(clips, "meditate", "side") ??
                FindBestClip(clips, "cultivate") ??
                FindBestClip(clips, "lie") ??
                FindBestClip(clips, "sit") ??
                FindBestClip(clips, "meditate");

            if (visual.rightCultivateClip != null &&
                visual.cultivateSideClip == visual.rightCultivateClip)
            {
                visual.sideSpriteFacesRight = true;
            }
            else if (visual.leftCultivateClip != null &&
                visual.cultivateSideClip == visual.leftCultivateClip)
            {
                visual.sideSpriteFacesRight = false;
            }
        }

        if (visual.dieDownClip == null)
        {
            visual.dieDownClip =
                FindBestClip(clips, "die", "down") ??
                FindBestClip(clips, "death", "down") ??
                FindBestClip(clips, "dead", "down");
        }

        if (visual.dieUpClip == null)
        {
            visual.dieUpClip =
                FindBestClip(clips, "die", "up") ??
                FindBestClip(clips, "death", "up") ??
                FindBestClip(clips, "dead", "up");
        }

        if (visual.dieSideClip == null)
        {
            visual.rightDieClip =
                FindBestClip(clips, "die", "right") ??
                FindBestClip(clips, "death", "right") ??
                FindBestClip(clips, "dead", "right");
            visual.leftDieClip =
                FindBestClip(clips, "die", "left") ??
                FindBestClip(clips, "death", "left") ??
                FindBestClip(clips, "dead", "left");
            visual.dieSideClip =
                visual.rightDieClip ??
                visual.leftDieClip ??
                FindBestClip(clips, "die", "side") ??
                FindBestClip(clips, "death", "side") ??
                FindBestClip(clips, "dead", "side");

            if (visual.rightDieClip != null &&
                visual.dieSideClip == visual.rightDieClip)
            {
                visual.sideSpriteFacesRight = true;
            }
            else if (visual.leftDieClip != null &&
                visual.dieSideClip == visual.leftDieClip)
            {
                visual.sideSpriteFacesRight = false;
            }
        }
    }

    static AnimationClip FindBestClip(
        AnimationClip[] clips,
        params string[] keywords)
    {
        if (clips == null ||
            keywords == null ||
            keywords.Length == 0)
        {
            return null;
        }

        string[] normalizedKeywords = new string[keywords.Length];
        for (int i = 0; i < keywords.Length; i++)
        {
            normalizedKeywords[i] = NormalizeClipName(keywords[i]);
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            string clipName = NormalizeClipName(clip.name);
            bool matches = true;

            for (int j = 0; j < normalizedKeywords.Length; j++)
            {
                string keyword = normalizedKeywords[j];
                if (string.IsNullOrEmpty(keyword) ||
                    !clipName.Contains(keyword))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
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
