using System.Collections.Generic;
using UnityEngine;

public class NpcRageNearbyAttack : MonoBehaviour
{
    [Header("Rage")]
    public bool rageEnabled = true;
    public bool rageOnAwake = true;
    public bool stopWhenDead = true;

    [Header("Target")]
    public LayerMask targetLayers = ~0;
    public float searchRadius = 2f;
    public float attackRange = 1.2f;
    public int maxTargetsPerBurst = 3;
    public bool includeMonsters = false;

    [Header("Damage")]
    public int damage = 10;
    public float attackInterval = 1f;
    public bool useCombatTechniqueModifier = true;
    [TextArea]
    public string attackReason = "";
    public bool callNearbyNpcsToRage = true;
    public float rageBroadcastRadius = 4f;
    public float rageBroadcastCooldown = 0.75f;

    [Header("Animation")]
    public Animator animator;
    public string attackTriggerName = "attack";
    public bool useAttackAnimation = false;
    public bool faceNearestTarget = true;
    public bool syncNpcAction = true;
    public string attackActionName = "attackMonsterNamed";

    [Header("FX")]
    public Transform attackOrigin;
    public GameObject impactPrefab;

    float nextAttackTime;
    float nextRageBroadcastTime;
    string cachedVillagerAction;
    string cachedSmartNpcAction;
    VillagerAI villagerAI;
    SmartNpcAI smartNpcAI;
    NPCVisualAnimation visualAnimation;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        villagerAI = GetComponent<VillagerAI>();
        smartNpcAI = GetComponent<SmartNpcAI>();
        visualAnimation = GetComponent<NPCVisualAnimation>();
        if (visualAnimation == null)
        {
            visualAnimation = GetComponentInChildren<NPCVisualAnimation>();
        }

        if (attackOrigin == null)
        {
            attackOrigin = transform;
        }

        if (rageOnAwake)
        {
            rageEnabled = true;
        }

        if (rageEnabled)
        {
            BroadcastRageToNearbyNpcs();
        }
    }

    void Update()
    {
        if (!rageEnabled)
        {
            return;
        }

        if (stopWhenDead && NpcRoleUtility.IsDead(gameObject))
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        List<NearbyTarget> targets = FindNearbyTargets();
        if (targets.Count == 0)
        {
            RestoreNpcAction();
            return;
        }

        nextAttackTime = Time.time + Mathf.Max(0.1f, attackInterval);

        SyncAttackAction();
        BroadcastRageToNearbyNpcs();

        if (useAttackAnimation &&
            animator != null &&
            !string.IsNullOrWhiteSpace(attackTriggerName))
        {
            animator.SetTrigger(attackTriggerName);
        }

        if (faceNearestTarget)
        {
            FaceTarget(targets[0].targetTransform);
        }

        int hits = Mathf.Min(maxTargetsPerBurst, targets.Count);
        for (int i = 0; i < hits; i++)
        {
            AttackTarget(targets[i]);
        }
    }

    void LateUpdate()
    {
        if (!rageEnabled || !syncNpcAction)
        {
            return;
        }

        SyncAttackAction();
    }

    public void StartRage()
    {
        rageEnabled = true;
        BroadcastRageToNearbyNpcs();
    }

    public void StopRage()
    {
        rageEnabled = false;
        RestoreNpcAction();
    }

    public void ToggleRage()
    {
        rageEnabled = !rageEnabled;
    }

    List<NearbyTarget> FindNearbyTargets()
    {
        List<NearbyTarget> found = new List<NearbyTarget>();
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            origin,
            Mathf.Max(searchRadius, attackRange),
            targetLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                hit.isTrigger ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null ||
                damageable.IsDead ||
                damageable.DamageTransform == null)
            {
                continue;
            }

            GameObject targetObject = damageable.DamageTransform.gameObject;
            if (targetObject == gameObject)
            {
                continue;
            }

            if (!includeMonsters &&
                targetObject.GetComponentInParent<MonsterAI>() != null)
            {
                continue;
            }

            if (targetObject.GetComponentInParent<VillagerAI>() == null &&
                targetObject.GetComponentInParent<SmartNpcAI>() == null &&
                targetObject.GetComponentInParent<MonsterAI>() == null)
            {
                continue;
            }

            float distance = Vector2.Distance(origin, damageable.DamageTransform.position);
            if (distance > Mathf.Max(0.1f, attackRange))
            {
                continue;
            }

            found.Add(new NearbyTarget(damageable, damageable.DamageTransform, distance));
        }

        found.Sort((a, b) => a.distance.CompareTo(b.distance));

        List<NearbyTarget> targets = new List<NearbyTarget>(found.Count);
        for (int i = 0; i < found.Count; i++)
        {
            targets.Add(found[i]);
        }

        return targets;
    }

    void AttackTarget(NearbyTarget target)
    {
        if (target.targetTransform == null)
        {
            return;
        }

        if (impactPrefab != null)
        {
            Instantiate(impactPrefab, target.targetTransform.position, Quaternion.identity);
        }

        if (useCombatTechniqueModifier)
        {
            NpcRoleUtility.Damage(
                gameObject,
                target.targetTransform.gameObject,
                Mathf.Max(1, damage),
                string.IsNullOrWhiteSpace(attackReason)
                    ? NpcText.Dialogue("attackReasonFallback")
                    : attackReason);
            return;
        }

        NpcRoleUtility.SetCombatAttackAction(
            gameObject,
            target.targetTransform.gameObject);

        DamageContext context = DamageContext.Attack(
            Mathf.Max(1, damage),
            gameObject,
            this,
            DamageSourceCategory.Npc,
            DamageType.Physical,
            string.IsNullOrWhiteSpace(attackReason)
                ? NpcText.Dialogue("attackReasonFallback")
                : attackReason,
            target.targetTransform.position,
            false);
        context.applyOutgoingModifiers = false;
        DamageSystem.Apply(target.damageable, context);
    }

    void BroadcastRageToNearbyNpcs()
    {
        if (!callNearbyNpcsToRage ||
            Time.time < nextRageBroadcastTime)
        {
            return;
        }

        nextRageBroadcastTime =
            Time.time + Mathf.Max(0.1f, rageBroadcastCooldown);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            Mathf.Max(0.1f, rageBroadcastRadius),
            targetLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                hit.gameObject == gameObject ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            NpcRageNearbyAttack rage = hit.GetComponentInParent<NpcRageNearbyAttack>();
            if (rage == null || rage == this)
            {
                continue;
            }

            rage.StartRage();
        }
    }

    void SyncAttackAction()
    {
        if (!syncNpcAction)
        {
            return;
        }

        string resolvedAttackAction = ResolveAttackAction();

        if (villagerAI != null)
        {
            if (string.IsNullOrEmpty(cachedVillagerAction))
            {
                cachedVillagerAction = villagerAI.currentAction;
            }

            villagerAI.currentAction = resolvedAttackAction;
        }

        if (smartNpcAI != null)
        {
            if (string.IsNullOrEmpty(cachedSmartNpcAction))
            {
                cachedSmartNpcAction = smartNpcAI.currentAction;
            }

            smartNpcAI.RequestEmergencyTask(
                SmartAITaskGoal.Combat,
                SmartAITaskPriority.Emergency,
                false,
                "rage nearby attack");
            smartNpcAI.ForceSetCurrentAction(resolvedAttackAction);
        }
    }

    string ResolveAttackAction()
    {
        if (!string.IsNullOrWhiteSpace(attackActionName))
        {
            return NpcText.Action(attackActionName);
        }

        return NpcText.Action("attackMonsterNamed");
    }

    void RestoreNpcAction()
    {
        if (villagerAI != null && !string.IsNullOrEmpty(cachedVillagerAction))
        {
            villagerAI.currentAction = cachedVillagerAction;
        }

        if (smartNpcAI != null && !string.IsNullOrEmpty(cachedSmartNpcAction))
        {
            smartNpcAI.ForceSetCurrentAction(cachedSmartNpcAction);
        }
    }

    void FaceTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (visualAnimation != null)
        {
            visualAnimation.SetFacingTarget(target.position);
        }

        Vector3 scale = transform.localScale;
        Vector2 direction = target.position - transform.position;
        if (direction.x > 0.05f)
        {
            scale.x = Mathf.Abs(scale.x);
        }
        else if (direction.x < -0.05f)
        {
            scale.x = -Mathf.Abs(scale.x);
        }

        transform.localScale = scale;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, Mathf.Max(searchRadius, attackRange));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, Mathf.Max(attackRange, 0.1f));
    }

    struct NearbyTarget
    {
        public readonly IDamageable damageable;
        public readonly Transform targetTransform;
        public readonly float distance;

        public NearbyTarget(
            IDamageable targetDamageable,
            Transform targetTransform,
            float targetDistance)
        {
            damageable = targetDamageable;
            this.targetTransform = targetTransform;
            distance = targetDistance;
        }
    }
}
