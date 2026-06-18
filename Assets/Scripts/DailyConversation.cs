using UnityEngine;

public class DailyConversation : MonoBehaviour
{
    [Header("Detect")]
    public float talkRadius = 1.2f;
    public LayerMask npcLayers = ~0;
    public float scanInterval = 3f;
    public float conversationCooldown = 25f;
    public bool requireFriendlyRelationship = true;
    public int minRelationshipToTalk = 8;

    [Header("Dialogue")]
    public string greetingDialogueKey = "greetings";
    public string replyDialogueKey = "replies";

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
        if (Time.time < nextTalkTime || IsDead())
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(transform.position, talkRadius, npcLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
            {
                continue;
            }

            DailyConversation other =
                hit.GetComponentInParent<DailyConversation>();

            if (other == null ||
                other.gameObject == gameObject ||
                other.IsDead() ||
                Time.time < other.nextTalkTime ||
                !CanTalkWith(other) ||
                !other.CanTalkWith(this))
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
        nextTalkTime = Time.time + conversationCooldown;
        other.nextTalkTime = Time.time + other.conversationCooldown;

        string myLine = GetRandomLine(greetingDialogueKey);
        string otherLine = other.GetRandomLine(other.replyDialogueKey);

        NpcRoleUtility.StopForConversation(gameObject);
        NpcRoleUtility.StopForConversation(other.gameObject);

        SetAction(NpcText.ActionFormat("conversationLine", myLine));
        other.SetAction(NpcText.ActionFormat("conversationLine", otherLine));

        Debug.Log(
            GetDisplayName() +
            " nói với " +
            other.GetDisplayName() +
            ": " +
            myLine);
    }

    bool CanTalkWith(DailyConversation other)
    {
        if (other == null)
        {
            return false;
        }

        if (!requireFriendlyRelationship)
        {
            return true;
        }

        NpcRelationshipGraph graph = GetComponent<NpcRelationshipGraph>();
        if (graph == null)
        {
            return false;
        }

        NpcSocialRelationship relationship = graph.Get(other.gameObject);
        if (relationship == null)
        {
            return false;
        }

        return Mathf.Max(relationship.affection, relationship.alliance) >=
            minRelationshipToTalk;
    }

    string GetRandomLine(string key)
    {
        return NpcText.DialogueLine(
            key,
            NpcText.Dialogue("dailyFallback"));
    }

    bool IsDead()
    {
        IDamageable damageable = GetComponent<IDamageable>();
        return damageable != null && damageable.IsDead;
    }

    string GetDisplayName()
    {
        return NpcRoleUtility.GetDisplayName(gameObject);
    }

    void SetAction(string action)
    {
        NpcRoleUtility.SetAction(gameObject, action);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
