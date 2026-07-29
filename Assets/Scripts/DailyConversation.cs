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
    [HideInInspector]
    public bool pauseMovementDuringConversation;
    [HideInInspector]
    public bool setActionDuringConversation;

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
        if (Time.time < nextTalkTime ||
            IsDead() ||
            !AllowsLegacySocial(gameObject))
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

            if (UnityObjectIdUtility.GetRuntimeId(this) >
                UnityObjectIdUtility.GetRuntimeId(other))
            {
                continue;
            }

            StartConversation(other);
            return;
        }
    }

    void StartConversation(DailyConversation other)
    {
        if (!AllowsLegacySocial(gameObject) ||
            !AllowsLegacySocial(other.gameObject))
        {
            return;
        }

        nextTalkTime = Time.time + conversationCooldown;
        other.nextTalkTime = Time.time + other.conversationCooldown;

        NpcSpeechController.TryShowSpeech(
            gameObject,
            other.gameObject,
            ResolveDialogueCategory(false));
        NpcSpeechController.TryShowSpeech(
            other.gameObject,
            gameObject,
            other.ResolveDialogueCategory(true));
    }

    bool CanTalkWith(DailyConversation other)
    {
        if (other == null)
        {
            return false;
        }

        if (!AllowsLegacySocial(gameObject) ||
            !AllowsLegacySocial(other.gameObject))
        {
            return false;
        }

        if (!requireFriendlyRelationship)
        {
            return true;
        }

        if (UsesMinorDialogue() ||
            other.UsesMinorDialogue())
        {
            return true;
        }

        NpcRelationshipGraph graph = GetComponent<NpcRelationshipGraph>();
        if (graph == null)
        {
            return false;
        }

        NpcSocialRelationship relationship = graph.Find(other.gameObject);
        if (relationship == null)
        {
            return false;
        }

        return Mathf.Max(relationship.affection, relationship.alliance) >=
            minRelationshipToTalk;
    }

    string ResolveDialogueCategory(bool reply)
    {
        int age = ResolveConversationAge();
        if (age >= 0 && age < 10)
        {
            return reply ? "child_babble_reply" : "child_babble_open";
        }

        if (age >= 10 && age <= 15)
        {
            return reply ? "teen_reply" : "teen_opening";
        }

        return reply ? replyDialogueKey : greetingDialogueKey;
    }

    bool UsesMinorDialogue()
    {
        int age = ResolveConversationAge();
        return age >= 0 && age <= 15;
    }

    int ResolveConversationAge()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.GetAge();
        }

        NPCIdentity identity = GetComponent<NPCIdentity>();
        return identity != null
            ? identity.GetCurrentAge()
            : -1;
    }

    bool IsDead()
    {
        IDamageable damageable = GetComponent<IDamageable>();
        return damageable != null && damageable.IsDead;
    }

    bool AllowsLegacySocial(GameObject npc)
    {
        if (npc != null)
        {
            VillagerAI villager = npc.GetComponent<VillagerAI>();
            if (villager != null)
            {
                if (villager.ageGroup == VillagerAgeGroup.Child)
                {
                    return true;
                }

                if (villager.ageGroup == VillagerAgeGroup.Teen)
                {
                    return true;
                }
            }
        }

        return NpcScheduleController.AllowsSocial(
            npc,
            NpcSocialChannel.LegacyDailyConversation);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
