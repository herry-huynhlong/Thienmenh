using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RangedProjectile2D : MonoBehaviour
{
    public enum SpriteDefaultDirection
    {
        Right,
        Left,
        Up,
        Down
    }

    [Header("Move")]
    public float speed = 6f;
    public float lifeTime = 3f;

    [Header("Rotation")]
    public bool rotateToDirection = true;

    [Tooltip("Hướng gốc của ảnh hỏa cầu khi Rotation Z = 0. Nếu ảnh đang bay xuống thì chọn Down.")]
    public SpriteDefaultDirection spriteDefaultDirection = SpriteDefaultDirection.Down;

    [Header("Damage")]
    public int damage = 20;
    public LayerMask hitLayers;
    public bool destroyOnHit = true;

    [Header("Explosion")]
    public GameObject impactPrefab;
    public bool useSplashDamage = false;
    public float splashRadius = 0.8f;

    private Vector2 moveDir;
    private GameObject owner;
    private bool initialized;

    public void Init(Vector2 direction, GameObject ownerObject, int skillDamage)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.down;

        moveDir = direction.normalized;
        owner = ownerObject;
        damage = skillDamage;
        initialized = true;

        UpdateRotationByDirection();

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (!initialized) return;

        transform.position += (Vector3)(moveDir * speed * Time.deltaTime);
    }

    private void UpdateRotationByDirection()
    {
        if (!rotateToDirection) return;

        float targetAngle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;

        float baseAngle = 0f;

        switch (spriteDefaultDirection)
        {
            case SpriteDefaultDirection.Right:
                baseAngle = 0f;
                break;

            case SpriteDefaultDirection.Up:
                baseAngle = 90f;
                break;

            case SpriteDefaultDirection.Left:
                baseAngle = 180f;
                break;

            case SpriteDefaultDirection.Down:
                baseAngle = -90f;
                break;
        }

        float finalAngle = targetAngle - baseAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized) return;

        if (owner != null)
        {
            if (other.gameObject == owner) return;
            if (other.transform.IsChildOf(owner.transform)) return;
        }

        bool canHit = (hitLayers.value & (1 << other.gameObject.layer)) != 0;
        if (!canHit) return;

        Explode(other.transform.position);
    }

    private void Explode(Vector3 hitPosition)
    {
        if (impactPrefab != null)
        {
            Instantiate(impactPrefab, hitPosition, Quaternion.identity);
        }

        if (useSplashDamage)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(hitPosition, splashRadius, hitLayers);

            foreach (Collider2D hit in hits)
            {
                if (owner != null)
                {
                    if (hit.gameObject == owner) continue;
                    if (hit.transform.IsChildOf(owner.transform)) continue;
                }

                ApplyDamage(hit.gameObject);
            }
        }
        else
        {
            ApplyDamageFromColliderPosition(hitPosition);
        }

        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyDamageFromColliderPosition(Vector3 hitPosition)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPosition, 0.15f, hitLayers);

        foreach (Collider2D hit in hits)
        {
            if (owner != null)
            {
                if (hit.gameObject == owner) continue;
                if (hit.transform.IsChildOf(owner.transform)) continue;
            }

            ApplyDamage(hit.gameObject);
            break;
        }
    }

    private void ApplyDamage(GameObject target)
    {
        target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        target.SendMessage("ReceiveDamage", damage, SendMessageOptions.DontRequireReceiver);
        target.SendMessage("ApplyDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    private void OnDrawGizmosSelected()
    {
        if (!useSplashDamage) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
}