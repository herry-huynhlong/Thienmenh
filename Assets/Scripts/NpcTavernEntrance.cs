using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class NpcTavernEntrance : MonoBehaviour
{
    [Header("Tavern")]
    public GameObject tavernProviderObject;
    public NpcTaskProvider tavernProvider;
    public Transform doorPoint;
    public Transform insideReleasePoint;

    [Header("Visitor Search")]
    public bool attractHungryNpc = true;
    public bool attractTaskSeekers = true;
    public float searchRadius = 8f;
    public float checkInterval = 3f;
    public LayerMask npcLayers = ~0;

    [Header("Needs")]
    [Range(0f, 100f)]
    public float hungerThreshold = 65f;
    public int minMoneyForMeal = 1;

    [Header("Movement")]
    public float arriveDistance = 0.35f;
    public float fallbackMoveSpeed = 1.6f;
    public bool pauseBaseAiWhileEntering = true;

    readonly List<NpcTavernVisit> visitors = new List<NpcTavernVisit>();
    float checkTimer;

    void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    void Update()
    {
        UpdateVisitors();

        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval)
        {
            return;
        }

        checkTimer = 0f;
        TryInviteVisitor();
    }

    void TryInviteVisitor()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                searchRadius,
                npcLayers);

        foreach (Collider2D hit in hits)
        {
            GameObject npc = GetNpcFromHit(hit);
            if (npc == null || HasVisitor(npc) || NpcRoleUtility.IsDead(npc))
            {
                continue;
            }

            string reason;
            if (!ShouldVisitTavern(npc, out reason))
            {
                continue;
            }

            StartVisit(npc, reason);
            return;
        }
    }

    void StartVisit(GameObject npc, string reason)
    {
        NpcTavernVisit visit = new NpcTavernVisit
        {
            npc = npc,
            reason = reason,
            doorPosition = GetDoorPosition()
        };

        PauseBaseAi(visit);
        visitors.Add(visit);

        NpcRoleUtility.SetAction(
            npc,
            NpcText.Action("goTavern"));
    }

    void UpdateVisitors()
    {
        for (int i = visitors.Count - 1; i >= 0; i--)
        {
            NpcTavernVisit visit = visitors[i];
            if (visit == null || visit.npc == null || NpcRoleUtility.IsDead(visit.npc))
            {
                FinishVisit(i, false);
                continue;
            }

            NpcRoleUtility.MoveTowards(
                visit.npc,
                visit.doorPosition,
                fallbackMoveSpeed);

            NpcRoleUtility.SetAction(
                visit.npc,
                visit.reason);

            if (Vector2.Distance(visit.npc.transform.position, visit.doorPosition) <= arriveDistance)
            {
                visit.waitingAtDoorTime += Time.deltaTime;
            }
            else
            {
                visit.waitingAtDoorTime = 0f;
            }

            if (visit.waitingAtDoorTime > 4f)
            {
                FinishVisit(i, false);
            }
        }
    }

    public void OnDoorTeleported(GameObject npc)
    {
        for (int i = visitors.Count - 1; i >= 0; i--)
        {
            NpcTavernVisit visit = visitors[i];
            if (visit != null && visit.npc == npc)
            {
                FinishVisit(i, true);
                return;
            }
        }
    }

    void FinishVisit(int index, bool entered)
    {
        NpcTavernVisit visit = visitors[index];
        visitors.RemoveAt(index);
        ResumeBaseAi(visit);

        if (entered && visit != null && visit.npc != null)
        {
            if (insideReleasePoint != null)
            {
                visit.npc.transform.position = insideReleasePoint.position;
            }

            NpcRoleUtility.SetAction(
                visit.npc,
                NpcText.Action("goTavern"));

            ResolveTavernProvider();
            if (tavernProvider != null)
            {
                tavernProvider.TryHandleVisitor(visit.npc);
            }
        }
    }

    bool ShouldVisitTavern(GameObject npc, out string reason)
    {
        reason = string.Empty;

        if (attractHungryNpc && NeedsMeal(npc))
        {
            reason = NpcText.Action("eatAtShop");
            return true;
        }

        if (attractTaskSeekers && HasAvailableTaskFor(npc))
        {
            reason = NpcText.Action("goTaskProviderDaily");
            return true;
        }

        return false;
    }

    bool NeedsMeal(GameObject npc)
    {
        if (NpcEconomy.GetNpcMoney(npc) < Mathf.Max(0, minMoneyForMeal))
        {
            return false;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.hunger >= hungerThreshold;
        }

        return false;
    }

    bool HasAvailableTaskFor(GameObject npc)
    {
        if (tavernProvider == null || tavernProvider.offers == null)
        {
            return false;
        }

        foreach (NpcTaskOffer offer in tavernProvider.offers)
        {
            if (offer != null &&
                NpcRoleUtility.MeetsRealm(npc, offer.minRealm, offer.minRealmStage))
            {
                return true;
            }
        }

        return false;
    }

    void ResolveTavernProvider()
    {
        if (tavernProviderObject != null)
        {
            tavernProvider = tavernProviderObject.GetComponent<NpcTaskProvider>();
        }
    }

    GameObject GetNpcFromHit(Collider2D hit)
    {
        if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
        {
            return null;
        }

        VillagerAI villager = hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc = hit.GetComponentInParent<SmartNpcAI>();
        return smartNpc != null ? smartNpc.gameObject : null;
    }

    bool HasVisitor(GameObject npc)
    {
        foreach (NpcTavernVisit visit in visitors)
        {
            if (visit != null && visit.npc == npc)
            {
                return true;
            }
        }

        return false;
    }

    Vector3 GetDoorPosition()
    {
        return doorPoint != null ? doorPoint.position : transform.position;
    }

    void PauseBaseAi(NpcTavernVisit visit)
    {
        if (!pauseBaseAiWhileEntering || visit == null || visit.npc == null)
        {
            return;
        }

        visit.pausedBaseAi = visit.npc.GetComponent<VillagerAI>();
        if (visit.pausedBaseAi == null)
        {
            visit.pausedBaseAi = visit.npc.GetComponent<SmartNpcAI>();
        }

        if (visit.pausedBaseAi == null)
        {
            return;
        }

        visit.pausedBaseAiWasEnabled = visit.pausedBaseAi.enabled;
        visit.pausedBaseAi.enabled = false;
    }

    void ResumeBaseAi(NpcTavernVisit visit)
    {
        if (visit == null || visit.pausedBaseAi == null)
        {
            return;
        }

        visit.pausedBaseAi.enabled = visit.pausedBaseAiWasEnabled;
        visit.pausedBaseAi = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, searchRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(GetDoorPosition(), 0.25f);

        if (insideReleasePoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(insideReleasePoint.position, 0.25f);
        }
    }
}

class NpcTavernVisit
{
    public GameObject npc;
    public string reason;
    public Vector3 doorPosition;
    public float waitingAtDoorTime;
    public Behaviour pausedBaseAi;
    public bool pausedBaseAiWasEnabled;
}
