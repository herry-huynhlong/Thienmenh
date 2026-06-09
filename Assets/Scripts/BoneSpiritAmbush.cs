using System.Collections;
using UnityEngine;

public class BoneSpiritAmbush : MonoBehaviour
{
    [Header("Visual")]
    public GameObject cloudVisual;
    public GameObject boneVisual;

    [Header("AI")]
    public MonsterAI monsterAI;
    public Animator boneAnimator;

    [Header("Detect")]
    public LayerMask targetLayers;
    public float detectRadius = 4f;

    [Header("Return To Cloud")]
    public bool returnToCloudWhenNoTarget = true;
    public float returnCheckRadius = 6f;
    public float noTargetReturnDelay = 3f;
    public bool returnToSpawnPoint = true;

    [Header("Patrol")]
    public float patrolRadius = 2.5f;
    public float patrolSpeed = 1.2f;
    public float waitTime = 0.8f;

    [Header("Transform")]
    public float transformDelay = 0.8f;
    public float reformCloudDelay = 0.6f;

    Vector3 startPosition;
    Vector3 patrolTarget;

    bool transforming;
    bool manifested;
    float waitTimer;
    float noTargetTimer;

    void Awake()
    {
        startPosition = transform.position;

        if (monsterAI == null)
        {
            monsterAI = GetComponent<MonsterAI>();
        }

        SetCloudModeInstant();

        PickNewPatrolTarget();
    }

    void Update()
    {
        if (transforming)
        {
            return;
        }

        if (manifested)
        {
            CheckReturnToCloud();
            return;
        }

        PatrolAsCloud();

        Transform target = FindTarget(detectRadius);

        if (target != null)
        {
            StartCoroutine(ManifestRoutine());
        }
    }

    void PatrolAsCloud()
    {
        float distance = Vector3.Distance(transform.position, patrolTarget);

        if (distance <= 0.1f)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= waitTime)
            {
                waitTimer = 0f;
                PickNewPatrolTarget();
            }

            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            patrolTarget,
            patrolSpeed * Time.deltaTime
        );
    }

    void PickNewPatrolTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;

        patrolTarget = startPosition + new Vector3(
            randomCircle.x,
            randomCircle.y,
            0f
        );
    }

    Transform FindTarget(float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            radius,
            targetLayers
        );

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (!hit.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (hit.transform == transform)
            {
                continue;
            }

            if (hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return hit.transform;
        }

        return null;
    }

    IEnumerator ManifestRoutine()
    {
        transforming = true;

        if (cloudVisual != null)
        {
            cloudVisual.SetActive(true);
        }

        yield return new WaitForSeconds(transformDelay);

        SetBoneModeInstant();

        manifested = true;
        transforming = false;
        noTargetTimer = 0f;
    }

    void CheckReturnToCloud()
    {
        if (!returnToCloudWhenNoTarget)
        {
            return;
        }

        Transform target = FindTarget(returnCheckRadius);

        if (target != null)
        {
            noTargetTimer = 0f;
            return;
        }

        noTargetTimer += Time.deltaTime;

        if (noTargetTimer >= noTargetReturnDelay)
        {
            StartCoroutine(ReturnToCloudRoutine());
        }
    }

    IEnumerator ReturnToCloudRoutine()
    {
        transforming = true;
        manifested = false;
        noTargetTimer = 0f;

        if (monsterAI != null)
        {
            monsterAI.enabled = false;
        }

        yield return new WaitForSeconds(reformCloudDelay);

        if (returnToSpawnPoint)
        {
            transform.position = startPosition;
        }

        SetCloudModeInstant();

        PickNewPatrolTarget();

        transforming = false;
    }

    void SetCloudModeInstant()
    {
        if (monsterAI != null)
        {
            monsterAI.enabled = false;
        }

        if (cloudVisual != null)
        {
            cloudVisual.SetActive(true);
        }

        if (boneVisual != null)
        {
            boneVisual.SetActive(false);
        }

        manifested = false;
    }

    void SetBoneModeInstant()
    {
        if (cloudVisual != null)
        {
            cloudVisual.SetActive(false);
        }

        if (boneVisual != null)
        {
            boneVisual.SetActive(true);
        }

        if (boneAnimator != null)
        {
            boneAnimator.Play(0);
        }

        if (monsterAI != null)
        {
            monsterAI.enabled = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, returnCheckRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            Application.isPlaying ? startPosition : transform.position,
            patrolRadius
        );
    }
}