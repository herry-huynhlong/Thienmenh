using UnityEngine;

public class DailyConversation : MonoBehaviour
{
    [Header("Detect")]
    public float talkRadius = 1.2f;
    public LayerMask npcLayers = ~0;
    public float scanInterval = 0.5f;
    public float conversationCooldown = 6f;

    [Header("Dialogue")]
    public string[] greetingLines =
    {
        "Đạo hữu gần đây thế nào?",
        "Hôm nay có thu hoạch gì không?",
        "Nghe nói gần đây yêu thú xuất hiện nhiều."
    };

    float scanTimer;
    float nextTalkTime;

    void Update()
    {
        scanTimer -= Time.deltaTime;

        if (scanTimer > 0)
        {
            return;
        }

        scanTimer = scanInterval;

        TryTalk();
    }

    void TryTalk()
    {
        if (Time.time < nextTalkTime)
        {
            return;
        }

        if (IsDead())
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                talkRadius,
                npcLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.gameObject == gameObject)
            {
                continue;
            }

            DailyConversation other =
                hit.GetComponentInParent<DailyConversation>();

            if (other == null ||
                other.gameObject == gameObject ||
                other.IsDead() ||
                Time.time < other.nextTalkTime)
            {
                continue;
            }

            if (GetInstanceID() > other.GetInstanceID())
            {
                continue;
            }

            StartConversation(other);
            return;
        }
    }

    void StartConversation(DailyConversation other)
    {
        nextTalkTime =
            Time.time + conversationCooldown;

        other.nextTalkTime =
            Time.time + other.conversationCooldown;

        string myLine =
            GetRandomLine();

        string otherLine =
            other.GetRandomLine();

        SetAction("Nói chuyện: " + myLine);
        other.SetAction("Nói chuyện: " + otherLine);

        Debug.Log(
            GetDisplayName() +
            " noi voi " +
            other.GetDisplayName() +
            ": " +
            myLine);
    }

    string GetRandomLine()
    {
        if (greetingLines == null ||
            greetingLines.Length == 0)
        {
            return "Dao huu.";
        }

        return greetingLines[
            Random.Range(0, greetingLines.Length)];
    }

    bool IsDead()
    {
        IDamageable damageable =
            GetComponent<IDamageable>();

        return damageable != null &&
            damageable.IsDead;
    }

    string GetDisplayName()
    {
        SmartNpcAI ai =
            GetComponent<SmartNpcAI>();

        if (ai != null)
        {
            return ai.npcName;
        }

        return name;
    }

    void SetAction(string action)
    {
        SmartNpcAI ai =
            GetComponent<SmartNpcAI>();

        if (ai != null)
        {
            ai.currentAction = action;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position,
            talkRadius);
    }
}