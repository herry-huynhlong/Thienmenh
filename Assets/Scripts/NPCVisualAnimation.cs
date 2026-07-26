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

    public AnimationClip cultivateClip;
    [HideInInspector] public AnimationClip cultivateDownClip;
    [HideInInspector] public AnimationClip cultivateUpClip;
    [HideInInspector] public AnimationClip cultivateSideClip;
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
    public float diagonalAxisTieThreshold = 0.12f;

    [Header("Debug")]
    public bool debugVisualLogs;

    [Header("Animator States")]
    public string walkDownState;
    public string walkUpState;
    public string walkSideState;
    public string walkRightState;
    public string walkLeftState;
    public string idleDownState;
    public string idleUpState;
    public string idleSideState;
    public string idleRightState;
    public string idleLeftState;
    public string attackDownState;
    public string attackUpState;
    public string attackSideState;
    public string attackRightState;
    public string attackLeftState;
    public string cultivateStateName;
    public string cultivateDownState;
    public string cultivateUpState;
    public string cultivateSideState;
    public string cultivateRightState;
    public string cultivateLeftState;
    public string dieDownState;
    public string dieUpState;
    public string dieSideState;
    public string dieRightState;
    public string dieLeftState;

    Animator animator;
    SpriteRenderer spriteRenderer;
    AnimatorOverrideController overrideController;
    string overrideClipName = "OverrideTargetState";
    int overrideStateHash;
    Vector2 lastDirection = Vector2.down;
    AnimationClip currentClip;
    string currentStateName;
    bool? lastLoggedIdleState;
    string lastLoggedAction;
    Vector2? lastLoggedInputDirection;
    readonly string[] movingBoolParameters =
    {
        "IsMoving",
        "isMoving",
        "moving",
        "move",
        "walk"
    };
    readonly string[] moveXParameters =
    {
        "MoveX",
        "moveX",
        "dirX",
        "horizontal",
        "inputX"
    };
    readonly string[] moveYParameters =
    {
        "MoveY",
        "moveY",
        "dirY",
        "vertical",
        "inputY"
    };
    readonly string[] speedParameters =
    {
        "Speed",
        "speed",
        "moveSpeed",
        "velocity",
        "moveMagnitude"
    };

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
        currentStateName = null;

        if (controller == null)
        {
            overrideController = null;
            return;
        }

        ResetResolvedVisualBindings();
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

    void ResetResolvedVisualBindings()
    {
        downWalkClip = null;
        upWalkClip = null;
        sideWalkClip = null;
        rightWalkClip = null;
        leftWalkClip = null;

        downIdleClip = null;
        upIdleClip = null;
        sideIdleClip = null;
        rightIdleClip = null;
        leftIdleClip = null;

        attackDownClip = null;
        attackUpClip = null;
        attackSideClip = null;
        rightAttackClip = null;
        leftAttackClip = null;

        cultivateClip = null;
        cultivateDownClip = null;
        cultivateUpClip = null;
        cultivateSideClip = null;
        rightCultivateClip = null;
        leftCultivateClip = null;

        dieDownClip = null;
        dieUpClip = null;
        dieSideClip = null;
        rightDieClip = null;
        leftDieClip = null;

        walkDownState = null;
        walkUpState = null;
        walkSideState = null;
        walkRightState = null;
        walkLeftState = null;
        idleDownState = null;
        idleUpState = null;
        idleSideState = null;
        idleRightState = null;
        idleLeftState = null;
        attackDownState = null;
        attackUpState = null;
        attackSideState = null;
        attackRightState = null;
        attackLeftState = null;
        cultivateStateName = null;
        cultivateDownState = null;
        cultivateUpState = null;
        cultivateSideState = null;
        cultivateRightState = null;
        cultivateLeftState = null;
        dieDownState = null;
        dieUpState = null;
        dieSideState = null;
        dieRightState = null;
        dieLeftState = null;

        sideSpriteFacesRight = false;
        invertSideFlip = false;
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

        ActionCategory actionCategory = ResolveActionCategory(currentAction);
        ApplyAnimatorParameters(
            moveDirection,
            isIdling,
            actionCategory);
        LogInputState(moveDirection, isIdling, currentAction);

        AnimationClip clipToPlay = null;

        if (actionCategory != ActionCategory.None)
        {
            clipToPlay = GetActionClip(actionCategory, lastDirection);
            string stateToPlay =
                GetActionStateName(actionCategory, lastDirection);

            if (ShouldLogVisualDebug())
            {
                Debug.Log(
                    "[NPCVisualAnimation] Action resolve object=" +
                    gameObject.name +
                    " action=" + currentAction +
                    " category=" + actionCategory +
                    " direction=" + lastDirection +
                    " state=" + stateToPlay +
                    " clip=" + DescribeClip(clipToPlay));
            }

            LogResolvedVisualSelection(
                "action",
                moveDirection,
                stateToPlay,
                clipToPlay);

            if (PlayVisual(stateToPlay, clipToPlay))
            {
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
            if (UsesDirectionalMovementParameters())
            {
                return;
            }

            clipToPlay = isIdling
                ? GetIdleClip(lastDirection)
                : GetWalkClip(lastDirection);
            string stateToPlay = isIdling
                ? GetIdleStateName(lastDirection)
                : GetWalkStateName(lastDirection);

            LogResolvedVisualSelection(
                isIdling ? "idle" : "walk",
                moveDirection,
                stateToPlay,
                clipToPlay);

            if (PlayVisual(stateToPlay, clipToPlay))
            {
                return;
            }

            if (clipToPlay == null &&
                ShouldLogVisualDebug())
            {
                Debug.LogWarning(
                    "[NPCVisualAnimation] Missing movement clip object=" +
                    gameObject.name +
                    " idle=" + isIdling +
                    " direction=" + lastDirection +
                    " action=" + currentAction);
            }
        }
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
        string stateToPlay =
            GetActionStateName(actionCategory, lastDirection);
        if (ShouldLogVisualDebug())
        {
            Debug.Log(
                "[NPCVisualAnimation] Replay resolve object=" +
                gameObject.name +
                " action=" + currentAction +
                " category=" + actionCategory +
                " direction=" + lastDirection +
                " state=" + stateToPlay +
                " clip=" + DescribeClip(clipToPlay));
        }

        if (string.IsNullOrWhiteSpace(stateToPlay) && clipToPlay == null)
        {
            Debug.LogWarning(
                "[NPCVisualAnimation] Replay action clip missing object=" +
                gameObject.name +
                " action=" + currentAction +
                " direction=" + lastDirection);
            return;
        }

        currentClip = null;
        currentStateName = null;
        PlayVisual(stateToPlay, clipToPlay);
    }

    Vector2 GetCardinalDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <
            directionDeadZone * directionDeadZone)
        {
            return lastDirection;
        }

        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        if (Mathf.Abs(absX - absY) <= diagonalAxisTieThreshold)
        {
            if (absX >= absY &&
                absX > directionDeadZone)
            {
                return direction.x < 0f ? Vector2.left : Vector2.right;
            }

            if (absY > directionDeadZone)
            {
                return direction.y < 0f ? Vector2.down : Vector2.up;
            }
        }

        if (absX > absY)
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
            MatchesAction(currentAction, "forging") ||
            MatchesAction(currentAction, "fixedAlchemistRefining") ||
            MatchesAction(currentAction, "fixedBlacksmithForging") ||
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

    bool PlayVisual(string stateName, AnimationClip clipToPlay)
    {
        if (TryPlayState(stateName))
        {
            return true;
        }

        if (clipToPlay != null)
        {
            PlayClip(clipToPlay);
            return true;
        }

        return false;
    }

    bool TryPlayState(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName) ||
            animator == null)
        {
            return false;
        }

        int stateHash = GetExistingStateHash(stateName);
        if (stateHash == 0)
        {
            return false;
        }

        if (currentStateName == stateName &&
            IsAnimatorInState(stateHash))
        {
            return true;
        }

        if (ShouldLogVisualDebug())
        {
            Debug.Log(
                "[NPCVisualAnimation] Play state object=" +
                gameObject.name +
                " state=" + stateName +
                " facing=" + lastDirection);
        }

        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
        currentStateName = stateName;
        currentClip = null;
        return true;
    }

    bool UsesDirectionalMovementParameters()
    {
        return HasAnyParameter(
                   moveXParameters,
                   AnimatorControllerParameterType.Float) &&
            HasAnyParameter(
                moveYParameters,
                AnimatorControllerParameterType.Float);
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
        currentStateName = null;
    }

    void LogInputState(
        Vector2 moveDirection,
        bool isIdling,
        string currentAction)
    {
        if (!ShouldLogVisualDebug())
        {
            return;
        }

        bool actionChanged =
            !string.Equals(
                lastLoggedAction,
                currentAction,
                System.StringComparison.Ordinal);
        bool idleChanged =
            !lastLoggedIdleState.HasValue ||
            lastLoggedIdleState.Value != isIdling;
        bool directionChanged =
            !lastLoggedInputDirection.HasValue ||
            Vector2.Distance(
                lastLoggedInputDirection.Value,
                moveDirection) > 0.01f;

        if (!actionChanged &&
            !idleChanged &&
            !directionChanged)
        {
            return;
        }

        Debug.Log(
            "[NPCVisualAnimation] Input object=" +
            gameObject.name +
            " idle=" + isIdling +
            " moveDirection=" + moveDirection +
            " facing=" + lastDirection +
            " action=" + currentAction);

        lastLoggedAction = currentAction;
        lastLoggedIdleState = isIdling;
        lastLoggedInputDirection = moveDirection;
    }

    void ApplyAnimatorParameters(
        Vector2 moveDirection,
        bool isIdling)
    {
        ApplyAnimatorParameters(
            moveDirection,
            isIdling,
            ActionCategory.None);
    }

    void ApplyAnimatorParameters(
        Vector2 moveDirection,
        bool isIdling,
        ActionCategory actionCategory)
    {
        if (animator == null)
        {
            return;
        }

        bool suppressDirectionalMovement =
            actionCategory != ActionCategory.None &&
            UsesDirectionalMovementParameters();
        if (suppressDirectionalMovement)
        {
            // Parameter-driven controllers such as dao_si use Any State
            // transitions from Speed/MoveX/MoveY. Keep these neutral while an
            // explicit action state like Cultivate is active so the controller
            // does not immediately jump back to idle/walk.
            SetFirstBoolParameter(movingBoolParameters, false);
            SetFirstFloatParameter(moveXParameters, 0f);
            SetFirstFloatParameter(moveYParameters, 0f);
            SetFirstFloatParameter(speedParameters, 0f);
            ApplyDirectionalFlipForParameterizedAnimator(lastDirection);
            return;
        }

        Vector2 parameterDirection =
            moveDirection.sqrMagnitude >
            directionDeadZone * directionDeadZone
                ? moveDirection.normalized
                : lastDirection;
        float speed = isIdling ? 0f : moveDirection.magnitude;

        SetFirstBoolParameter(movingBoolParameters, !isIdling);
        SetFirstFloatParameter(moveXParameters, parameterDirection.x);
        SetFirstFloatParameter(moveYParameters, parameterDirection.y);
        SetFirstFloatParameter(speedParameters, speed);

        if (UsesDirectionalMovementParameters())
        {
            ApplyDirectionalFlipForParameterizedAnimator(
                parameterDirection);
        }
    }

    void ApplyDirectionalFlipForParameterizedAnimator(
        Vector2 direction)
    {
        if (spriteRenderer == null ||
            Mathf.Abs(direction.x) < directionDeadZone)
        {
            return;
        }

        bool hasRightVariant =
            rightWalkClip != null ||
            rightIdleClip != null ||
            !string.IsNullOrWhiteSpace(walkRightState) ||
            !string.IsNullOrWhiteSpace(idleRightState);
        bool hasLeftVariant =
            leftWalkClip != null ||
            leftIdleClip != null ||
            !string.IsNullOrWhiteSpace(walkLeftState) ||
            !string.IsNullOrWhiteSpace(idleLeftState);

        if (direction.x > directionDeadZone)
        {
            if (hasRightVariant)
            {
                spriteRenderer.flipX = false;
                return;
            }

            if (hasLeftVariant)
            {
                spriteRenderer.flipX = true;
                return;
            }
        }

        if (direction.x < -directionDeadZone)
        {
            if (hasLeftVariant)
            {
                spriteRenderer.flipX = false;
                return;
            }

            if (hasRightVariant)
            {
                spriteRenderer.flipX = true;
                return;
            }
        }

        ApplySideFlip(direction);
    }

    void LogResolvedVisualSelection(
        string channel,
        Vector2 inputDirection,
        string stateName,
        AnimationClip clip)
    {
        if (!ShouldLogVisualDebug())
        {
            return;
        }

        Debug.LogWarning(
            "[NPCVisualAnimation] Resolved object=" +
            gameObject.name +
            " channel=" + channel +
            " inputDir=" + inputDirection +
            " inputMag=" + inputDirection.magnitude.ToString("F3") +
            " facing=" + lastDirection +
            " state=" +
            (string.IsNullOrWhiteSpace(stateName) ? "null" : stateName) +
            " clip=" + DescribeClip(clip) +
            " pos=" + transform.position +
            " flipX=" +
            (spriteRenderer != null ? spriteRenderer.flipX.ToString() : "no-sprite"));
    }

    void SetFirstBoolParameter(
        string[] parameterNames,
        bool value)
    {
        if (parameterNames == null || animator == null)
        {
            return;
        }

        for (int i = 0; i < parameterNames.Length; i++)
        {
            string parameterName = parameterNames[i];
            if (!HasParameter(
                parameterName,
                AnimatorControllerParameterType.Bool))
            {
                continue;
            }

            animator.SetBool(parameterName, value);
            return;
        }
    }

    void SetFirstFloatParameter(
        string[] parameterNames,
        float value)
    {
        if (parameterNames == null || animator == null)
        {
            return;
        }

        for (int i = 0; i < parameterNames.Length; i++)
        {
            string parameterName = parameterNames[i];
            if (!HasParameter(
                parameterName,
                AnimatorControllerParameterType.Float))
            {
                continue;
            }

            animator.SetFloat(parameterName, value);
            return;
        }
    }

    bool HasParameter(
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (string.IsNullOrWhiteSpace(parameterName) ||
            animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == parameterType &&
                parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    bool HasAnyParameter(
        string[] parameterNames,
        AnimatorControllerParameterType parameterType)
    {
        if (parameterNames == null)
        {
            return false;
        }

        for (int i = 0; i < parameterNames.Length; i++)
        {
            if (HasParameter(parameterNames[i], parameterType))
            {
                return true;
            }
        }

        return false;
    }

    bool IsAnimatorInState(int stateHash)
    {
        if (animator == null || stateHash == 0)
        {
            return false;
        }

        AnimatorStateInfo currentState =
            animator.GetCurrentAnimatorStateInfo(0);
        return currentState.shortNameHash == stateHash ||
            currentState.fullPathHash == stateHash;
    }

    int GetExistingStateHash(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return 0;
        }

        return FindStateHashCandidate(
            stateName,
            stateName.Replace("_Idle", "_idle"),
            stateName.Replace("_idle", "_Idle"),
            stateName.Replace("_Walk", "_walk"),
            stateName.Replace("_walk", "_Walk"),
            stateName.Replace("_Attack", "_attack"),
            stateName.Replace("_attack", "_Attack"),
            stateName.Replace("_Dead", "_dead"),
            stateName.Replace("_dead", "_Dead"));
    }

    int FindStateHashCandidate(params string[] candidates)
    {
        if (animator == null || candidates == null)
        {
            return 0;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            int shortHash = Animator.StringToHash(candidate);
            if (animator.HasState(0, shortHash))
            {
                return shortHash;
            }

            int fullPathHash =
                Animator.StringToHash("Base Layer." + candidate);
            if (animator.HasState(0, fullPathHash))
            {
                return fullPathHash;
            }
        }

        return 0;
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
                EnsureCultivateBindings();
                return GetCultivateClip();

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

    AnimationClip GetCultivateClip()
    {
        return cultivateClip ??
            cultivateSideClip ??
            rightCultivateClip ??
            leftCultivateClip ??
            cultivateUpClip ??
            cultivateDownClip;
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

    string GetWalkStateName(Vector2 direction)
    {
        return GetDirectionalStateName(
            walkUpState,
            walkDownState,
            walkSideState,
            walkRightState,
            walkLeftState,
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

    string GetIdleStateName(Vector2 direction)
    {
        return GetDirectionalStateName(
            idleUpState,
            idleDownState,
            idleSideState,
            idleRightState,
            idleLeftState,
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

        if (direction.x > directionDeadZone && leftClip != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = true;
            }

            return leftClip;
        }

        if (direction.x < -directionDeadZone && rightClip != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = true;
            }

            return rightClip;
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

    string GetDirectionalStateName(
        string upState,
        string downState,
        string sideState,
        string rightState,
        string leftState,
        Vector2 direction)
    {
        if (direction.x > directionDeadZone &&
            !string.IsNullOrWhiteSpace(rightState))
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }

            return rightState;
        }

        if (direction.x < -directionDeadZone &&
            !string.IsNullOrWhiteSpace(leftState))
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }

            return leftState;
        }

        if (direction.x > directionDeadZone &&
            !string.IsNullOrWhiteSpace(leftState))
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = true;
            }

            return leftState;
        }

        if (direction.x < -directionDeadZone &&
            !string.IsNullOrWhiteSpace(rightState))
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = true;
            }

            return rightState;
        }

        if (direction == Vector2.up &&
            !string.IsNullOrWhiteSpace(upState))
        {
            ApplySideFlip(direction);
            return upState;
        }

        if (direction == Vector2.down &&
            !string.IsNullOrWhiteSpace(downState))
        {
            ApplySideFlip(direction);
            return downState;
        }

        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
            if (direction.y > 0f &&
                !string.IsNullOrWhiteSpace(upState))
            {
                ApplySideFlip(direction);
                return upState;
            }

            if (direction.y < 0f &&
                !string.IsNullOrWhiteSpace(downState))
            {
                ApplySideFlip(direction);
                return downState;
            }
        }

        ApplySideFlip(direction);
        return sideState;
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
            if (ShouldLogVisualDebug())
            {
                Debug.LogWarning(
                    "[NPCVisualAnimation] Missing Animator on object=" +
                    gameObject.name);
            }
            return false;
        }

        RuntimeAnimatorController controller =
            animator.runtimeAnimatorController;
        if (controller == null)
        {
            if (ShouldLogVisualDebug())
            {
                Debug.LogWarning(
                    "[NPCVisualAnimation] Missing RuntimeAnimatorController object=" +
                    gameObject.name);
            }
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

        if (overrideController == null &&
            ShouldLogVisualDebug())
        {
            Debug.LogWarning(
                "[NPCVisualAnimation] Failed to create AnimatorOverrideController object=" +
                gameObject.name +
                " controller=" + controller.name);
        }

        return true;
    }

    void EnsureCultivateBindings()
    {
        if (!string.IsNullOrWhiteSpace(cultivateStateName) &&
            cultivateClip != null)
        {
            return;
        }

        RuntimeAnimatorController controller =
            animator != null
                ? animator.runtimeAnimatorController
                : null;
        AnimationClip[] clips =
            controller != null
                ? controller.animationClips
                : null;
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        TryAssignActionClipsFromAnimator(this, clips);

        cultivateStateName = CoalesceFirstStateName(
            cultivateStateName,
            FindBestStateNameFromAnimator(clips, "cultivate"),
            FindBestStateNameFromAnimator(clips, "meditate"),
            FindBestStateNameFromAnimator(clips, "sit"));
        cultivateSideState = CoalesceFirstStateName(
            cultivateSideState,
            FindBestStateNameFromAnimator(clips, "cultivate", "side"),
            cultivateStateName,
            FindBestStateNameFromAnimator(clips, "meditate"));
        cultivateRightState = CoalesceFirstStateName(
            cultivateRightState,
            FindBestStateNameFromAnimator(clips, "cultivate", "right"));
        cultivateLeftState = CoalesceFirstStateName(
            cultivateLeftState,
            FindBestStateNameFromAnimator(clips, "cultivate", "left"));
        cultivateUpState = CoalesceFirstStateName(
            cultivateUpState,
            FindBestStateNameFromAnimator(clips, "cultivate", "up"));
        cultivateDownState = CoalesceFirstStateName(
            cultivateDownState,
            FindBestStateNameFromAnimator(clips, "cultivate", "down"));
    }

    string GetActionStateName(
        ActionCategory actionCategory,
        Vector2 direction)
    {
        switch (actionCategory)
        {
            case ActionCategory.Attack:
                return GetDirectionalStateName(
                    attackUpState,
                    attackDownState,
                    attackSideState,
                    attackRightState,
                    attackLeftState,
                    direction);

            case ActionCategory.Cultivate:
                EnsureCultivateBindings();
                if (!string.IsNullOrWhiteSpace(cultivateStateName))
                {
                    return cultivateStateName;
                }

                return GetDirectionalStateName(
                    cultivateUpState,
                    cultivateDownState,
                    cultivateSideState,
                    cultivateRightState,
                    cultivateLeftState,
                    direction);

            case ActionCategory.Die:
                return GetDirectionalStateName(
                    dieUpState,
                    dieDownState,
                    dieSideState,
                    dieRightState,
                    dieLeftState,
                    direction);
        }

        return null;
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
        visual.walkDownState = CoalesceStateName(
            visual.walkDownState,
            visual.downWalkClip);

        if (visual.upWalkClip == null)
        {
            visual.upWalkClip = FindBestClip(clips, "walk", "up");
            assignedFromAnimator |= visual.upWalkClip != null;
        }
        visual.walkUpState = CoalesceStateName(
            visual.walkUpState,
            visual.upWalkClip);

        if (visual.rightWalkClip == null)
        {
            visual.rightWalkClip = FindBestClip(clips, "walk", "right");
        }
        visual.walkRightState = CoalesceStateName(
            visual.walkRightState,
            visual.rightWalkClip);

        if (visual.leftWalkClip == null)
        {
            visual.leftWalkClip = FindBestClip(clips, "walk", "left");
        }
        visual.walkLeftState = CoalesceStateName(
            visual.walkLeftState,
            visual.leftWalkClip);

        if (visual.sideWalkClip == null)
        {
            AnimationClip genericWalk = FindBestClip(clips, "walk", "side");
            visual.sideWalkClip =
                visual.rightWalkClip ??
                visual.leftWalkClip ??
                genericWalk;
        }
        visual.walkSideState = CoalesceStateName(
            visual.walkSideState,
            visual.sideWalkClip);

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
        visual.idleDownState = CoalesceStateName(
            visual.idleDownState,
            visual.downIdleClip);

        if (visual.upIdleClip == null)
        {
            visual.upIdleClip =
                FindBestClip(clips, "idle", "up") ??
                FindBestClip(clips, "lie", "up");
            assignedFromAnimator |= visual.upIdleClip != null;
        }
        visual.idleUpState = CoalesceStateName(
            visual.idleUpState,
            visual.upIdleClip);

        if (visual.rightIdleClip == null)
        {
            visual.rightIdleClip =
                FindBestClip(clips, "idle", "right") ??
                FindBestClip(clips, "lie", "right");
        }
        visual.idleRightState = CoalesceStateName(
            visual.idleRightState,
            visual.rightIdleClip);

        if (visual.leftIdleClip == null)
        {
            visual.leftIdleClip =
                FindBestClip(clips, "idle", "left") ??
                FindBestClip(clips, "lie", "left");
        }
        visual.idleLeftState = CoalesceStateName(
            visual.idleLeftState,
            visual.leftIdleClip);

        if (visual.sideIdleClip == null)
        {
            AnimationClip genericIdle = FindBestClip(clips, "idle", "side");
            visual.sideIdleClip =
                visual.rightIdleClip ??
                visual.leftIdleClip ??
                genericIdle;
        }
        visual.idleSideState = CoalesceStateName(
            visual.idleSideState,
            visual.sideIdleClip);

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
        visual.attackDownState = CoalesceStateName(
            visual.attackDownState,
            visual.attackDownClip);

        if (visual.attackUpClip == null)
        {
            visual.attackUpClip =
                FindBestClip(clips, "attack", "up") ??
                FindBestClip(clips, "fight", "up") ??
                FindBestClip(clips, "hit", "up");
        }
        visual.attackUpState = CoalesceStateName(
            visual.attackUpState,
            visual.attackUpClip);

        if (visual.rightAttackClip == null)
        {
            visual.rightAttackClip =
                FindBestClip(clips, "attack", "right") ??
                FindBestClip(clips, "fight", "right") ??
                FindBestClip(clips, "hit", "right");
        }
        visual.attackRightState = CoalesceStateName(
            visual.attackRightState,
            visual.rightAttackClip);

        if (visual.leftAttackClip == null)
        {
            visual.leftAttackClip =
                FindBestClip(clips, "attack", "left") ??
                FindBestClip(clips, "fight", "left") ??
                FindBestClip(clips, "hit", "left");
        }
        visual.attackLeftState = CoalesceStateName(
            visual.attackLeftState,
            visual.leftAttackClip);

        if (visual.attackSideClip == null)
        {
            visual.attackSideClip =
                visual.rightAttackClip ??
                visual.leftAttackClip ??
                FindBestClip(clips, "attack", "side") ??
                FindBestClip(clips, "fight", "side");
        }
        visual.attackSideState = CoalesceStateName(
            visual.attackSideState,
            visual.attackSideClip);

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
        visual.cultivateDownState = CoalesceStateName(
            visual.cultivateDownState,
            visual.cultivateDownClip);

        if (visual.cultivateUpClip == null)
        {
            visual.cultivateUpClip =
                FindBestClip(clips, "cultivate", "up") ??
                FindBestClip(clips, "lie", "up") ??
                FindBestClip(clips, "sit", "up") ??
                FindBestClip(clips, "meditate", "up");
        }
        visual.cultivateUpState = CoalesceStateName(
            visual.cultivateUpState,
            visual.cultivateUpClip);

        if (visual.cultivateClip == null)
        {
            visual.cultivateClip =
                FindBestClip(clips, "cultivate") ??
                FindBestClip(clips, "lie") ??
                FindBestClip(clips, "sit") ??
                FindBestClip(clips, "meditate");
        }
        visual.cultivateStateName = CoalesceStateName(
            visual.cultivateStateName,
            visual.cultivateClip);

        if (visual.rightCultivateClip == null)
        {
            visual.rightCultivateClip =
                FindBestClip(clips, "cultivate", "right") ??
                FindBestClip(clips, "lie", "right") ??
                FindBestClip(clips, "sit", "right") ??
                FindBestClip(clips, "meditate", "right");
        }
        visual.cultivateRightState = CoalesceStateName(
            visual.cultivateRightState,
            visual.rightCultivateClip);

        if (visual.leftCultivateClip == null)
        {
            visual.leftCultivateClip =
                FindBestClip(clips, "cultivate", "left") ??
                FindBestClip(clips, "lie", "left") ??
                FindBestClip(clips, "sit", "left") ??
                FindBestClip(clips, "meditate", "left");
        }
        visual.cultivateLeftState = CoalesceStateName(
            visual.cultivateLeftState,
            visual.leftCultivateClip);

        if (visual.cultivateSideClip == null)
        {
            visual.cultivateSideClip =
                visual.cultivateClip ??
                visual.rightCultivateClip ??
                visual.leftCultivateClip ??
                FindBestClip(clips, "cultivate", "side") ??
                FindBestClip(clips, "lie", "side") ??
                FindBestClip(clips, "sit", "side") ??
                FindBestClip(clips, "meditate", "side");
            visual.cultivateSideState = CoalesceStateName(
                visual.cultivateSideState,
                visual.cultivateSideClip);

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

        if (visual.cultivateClip == null)
        {
            visual.cultivateClip =
                visual.cultivateSideClip ??
                visual.rightCultivateClip ??
                visual.leftCultivateClip ??
                visual.cultivateUpClip ??
                visual.cultivateDownClip;
        }

        if (visual.dieDownClip == null)
        {
            visual.dieDownClip =
                FindBestClip(clips, "die", "down") ??
                FindBestClip(clips, "death", "down") ??
                FindBestClip(clips, "dead", "down");
        }
        visual.dieDownState = CoalesceStateName(
            visual.dieDownState,
            visual.dieDownClip);

        if (visual.dieUpClip == null)
        {
            visual.dieUpClip =
                FindBestClip(clips, "die", "up") ??
                FindBestClip(clips, "death", "up") ??
                FindBestClip(clips, "dead", "up");
        }
        visual.dieUpState = CoalesceStateName(
            visual.dieUpState,
            visual.dieUpClip);

        if (visual.rightDieClip == null)
        {
            visual.rightDieClip =
                FindBestClip(clips, "die", "right") ??
                FindBestClip(clips, "death", "right") ??
                FindBestClip(clips, "dead", "right");
        }
        visual.dieRightState = CoalesceStateName(
            visual.dieRightState,
            visual.rightDieClip);

        if (visual.leftDieClip == null)
        {
            visual.leftDieClip =
                FindBestClip(clips, "die", "left") ??
                FindBestClip(clips, "death", "left") ??
                FindBestClip(clips, "dead", "left");
        }
        visual.dieLeftState = CoalesceStateName(
            visual.dieLeftState,
            visual.leftDieClip);

        if (visual.dieSideClip == null)
        {
            visual.dieSideClip =
                visual.rightDieClip ??
                visual.leftDieClip ??
                FindBestClip(clips, "die", "side") ??
                FindBestClip(clips, "death", "side") ??
                FindBestClip(clips, "dead", "side");
            visual.dieSideState = CoalesceStateName(
                visual.dieSideState,
                visual.dieSideClip);

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

    string FindBestStateNameFromAnimator(
        AnimationClip[] clips,
        params string[] keywords)
    {
        if (animator == null ||
            clips == null ||
            keywords == null ||
            keywords.Length == 0)
        {
            return string.Empty;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null ||
                !NameMatchesKeywords(clip.name, keywords))
            {
                continue;
            }

            if (GetExistingStateHash(clip.name) != 0)
            {
                return clip.name;
            }
        }

        return string.Empty;
    }

    static bool NameMatchesKeywords(
        string value,
        params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            keywords == null ||
            keywords.Length == 0)
        {
            return false;
        }

        string normalizedValue = NormalizeClipName(value);
        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = NormalizeClipName(keywords[i]);
            if (string.IsNullOrEmpty(keyword) ||
                !normalizedValue.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }

    static string CoalesceFirstStateName(
        string existingStateName,
        params string[] candidates)
    {
        if (!string.IsNullOrWhiteSpace(existingStateName))
        {
            return existingStateName;
        }

        if (candidates == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    static string CoalesceStateName(
        string existingStateName,
        AnimationClip clip)
    {
        if (!string.IsNullOrWhiteSpace(existingStateName))
        {
            return existingStateName;
        }

        return clip != null ? clip.name : string.Empty;
    }
}
