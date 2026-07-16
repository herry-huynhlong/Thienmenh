using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Escort participants, dialogue, anchors and follow positions.
public partial class NpcTaskProvider
{
    bool IsEscortTask(RunningNpcTask task)
    {
        return task != null &&
            task.offer != null &&
            task.offer.taskType == NpcTaskType.Escort;
    }

    bool IsEscortConfigured(NpcTaskOffer offer)
    {
        return offer != null &&
            offer.taskType == NpcTaskType.Escort &&
            escortMeetPoint != null &&
            (IsFrontierWatcherEscortOffer(offer) ||
                escortCompletionPoint != null);
    }

    bool IsFrontierWatcherEscortOffer(NpcTaskOffer offer)
    {
        return offer != null &&
            offer.taskType == NpcTaskType.Escort &&
            !string.IsNullOrWhiteSpace(offer.customTargetId);
    }

    Vector3 GetEscortCompanionPosition(NpcTaskOffer offer)
    {
        if (escortAnchorPositionsCaptured)
        {
            return escortMeetAnchorPosition;
        }

        if (escortMeetPoint != null)
        {
            return escortMeetPoint.position;
        }

        return GetFallbackWorkPosition();
    }

    Vector3 GetEscortGreetingPosition(RunningNpcTask task)
    {
        if (task == null)
        {
            return Vector3.zero;
        }

        Vector3 companionPosition = GetEscortCompanionPosition(task.offer);
        Vector3 leaderPosition = task.npc != null
            ? task.npc.transform.position
            : companionPosition + Vector3.left;
        Vector2 awayFromCompanion = (Vector2)(leaderPosition - companionPosition);
        if (awayFromCompanion.sqrMagnitude < 0.01f)
        {
            awayFromCompanion = Random.insideUnitCircle.normalized;
        }
        else
        {
            awayFromCompanion.Normalize();
        }

        float greetingOffset = Mathf.Max(0.65f, escortFollowDistance * 0.75f);
        return companionPosition + (Vector3)(awayFromCompanion * greetingOffset);
    }

    Vector3 GetEscortCompletionPosition(NpcTaskOffer offer)
    {
        if (IsFrontierWatcherEscortOffer(offer))
        {
            Vector3 fallback =
                escortCompletionPoint != null
                    ? escortCompletionPoint.position
                    : GetFallbackWorkPosition();
            return FrontierDefenseCoordinator.GetCurrentAssigneePosition(
                offer.customTargetId,
                fallback);
        }

        if (escortAnchorPositionsCaptured)
        {
            return escortCompletionAnchorPosition;
        }

        if (escortCompletionPoint != null)
        {
            return escortCompletionPoint.position;
        }

        return GetProviderPosition();
    }

    GameObject ResolveEscortCompletionNpc(
        NpcTaskOffer offer,
        GameObject exclude = null)
    {
        if (IsFrontierWatcherEscortOffer(offer))
        {
            GameObject watcher =
                FrontierDefenseCoordinator.GetCurrentAssignee(
                    offer.customTargetId);
            return watcher != null &&
                watcher != exclude &&
                watcher.activeInHierarchy &&
                !NpcRoleUtility.IsDead(watcher)
                ? watcher
                : null;
        }

        return FindEscortNpcAtPoint(escortCompletionPoint, exclude);
    }

    Vector3 GetEscortCompletionGreetingPosition(RunningNpcTask task)
    {
        if (task == null)
        {
            return Vector3.zero;
        }

        Vector3 completionPosition = GetEscortCompletionPosition(task.offer);
        Vector3 leaderPosition = task.npc != null
            ? task.npc.transform.position
            : completionPosition + Vector3.left;
        Vector2 awayFromCompletion = (Vector2)(leaderPosition - completionPosition);
        if (awayFromCompletion.sqrMagnitude < 0.01f)
        {
            awayFromCompletion = Random.insideUnitCircle.normalized;
        }
        else
        {
            awayFromCompletion.Normalize();
        }

        float greetingOffset = Mathf.Max(0.65f, escortFollowDistance * 0.75f);
        return completionPosition + (Vector3)(awayFromCompletion * greetingOffset);
    }

    bool TryShowEscortDialogue(
        GameObject speaker,
        GameObject target,
        string category,
        out NpcDialogueSelection selection)
    {
        selection = default;
        if (speaker == null || string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        return NpcSpeechController.TryShowSpeech(
            speaker,
            target,
            category,
            out selection);
    }

    bool UpdateEscortGreetingDialogue(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.escortCompanionNpc == null)
        {
            return false;
        }

        task.remainingTime -= Time.deltaTime;

        if (task.escortGreetingConversationStep == 0)
        {
            TryShowEscortDialogue(
                task.npc,
                task.escortCompanionNpc,
                "escort_greeting",
                out _);
            TryShowEscortDialogue(
                task.escortCompanionNpc,
                task.npc,
                "escort_reply",
                out _);
            task.remainingTime = Mathf.Max(1.0f, escortGreetingDuration * 0.42f);
            task.escortGreetingConversationStep = 1;
            return true;
        }

        if (task.escortGreetingConversationStep == 1)
        {
            if (task.remainingTime > 0f)
            {
                return true;
            }

            TryShowEscortDialogue(
                task.npc,
                task.escortCompanionNpc,
                "escort_greeting",
                out _);
            TryShowEscortDialogue(
                task.escortCompanionNpc,
                task.npc,
                "escort_reply",
                out _);
            task.remainingTime = Mathf.Max(1.0f, escortGreetingDuration * 0.42f);
            task.escortGreetingConversationStep = 2;
            return true;
        }

        if (task.escortGreetingConversationStep == 2)
        {
            if (task.remainingTime > 0f)
            {
                return true;
            }

            task.escortGreetingConversationStep = 3;
            return false;
        }

        return task.escortGreetingConversationStep < 3;
    }

    bool UpdateEscortDeliveryDialogue(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null ||
            task.escortCompanionNpc == null ||
            task.escortCompletionNpc == null)
        {
            return false;
        }

        task.remainingTime -= Time.deltaTime;

        if (task.escortDeliveryConversationStep == 0)
        {
            string deliveryCategory =
                GetEscortDeliveryCategory(task);
            string replyCategory =
                GetEscortDeliveryReplyCategory(task);
            TryShowEscortDialogue(
                task.escortCompanionNpc,
                task.escortCompletionNpc,
                deliveryCategory,
                out _);
            TryShowEscortDialogue(
                task.escortCompletionNpc,
                task.escortCompanionNpc,
                replyCategory,
                out _);
            task.remainingTime = Mathf.Max(1.0f, escortGreetingDuration * 0.42f);
            task.escortDeliveryConversationStep = 1;
            return true;
        }

        if (task.escortDeliveryConversationStep == 1)
        {
            if (task.remainingTime > 0f)
            {
                return true;
            }

            string deliveryCategory =
                GetEscortDeliveryCategory(task);
            string replyCategory =
                GetEscortDeliveryReplyCategory(task);
            TryShowEscortDialogue(
                task.escortCompanionNpc,
                task.escortCompletionNpc,
                deliveryCategory,
                out _);
            TryShowEscortDialogue(
                task.escortCompletionNpc,
                task.escortCompanionNpc,
                replyCategory,
                out _);
            task.remainingTime = Mathf.Max(1.0f, escortGreetingDuration * 0.42f);
            task.escortDeliveryConversationStep = 2;
            return true;
        }

        if (task.escortDeliveryConversationStep == 2)
        {
            if (task.remainingTime > 0f)
            {
                return true;
            }

            task.escortDeliveryConversationStep = 3;
            return false;
        }

        return task.escortDeliveryConversationStep < 3;
    }

    string GetEscortDeliveryCategory(RunningNpcTask task)
    {
        return task != null &&
            IsFrontierWatcherEscortOffer(task.offer)
            ? "frontier_watch_delivery"
            : "escort_delivery";
    }

    string GetEscortDeliveryReplyCategory(RunningNpcTask task)
    {
        return task != null &&
            IsFrontierWatcherEscortOffer(task.offer)
            ? "frontier_watch_delivery_reply"
            : "escort_delivery_reply";
    }

    void CaptureEscortAnchorPositions()
    {
        if (escortAnchorPositionsCaptured)
        {
            return;
        }

        escortMeetAnchorPosition = escortMeetPoint != null
            ? escortMeetPoint.position
            : transform.position;
        escortCompletionAnchorPosition = escortCompletionPoint != null
            ? escortCompletionPoint.position
            : transform.position;
        escortAnchorPositionsCaptured = true;
    }

    void FreezeEscortAnchors()
    {
        FreezeEscortAnchor(escortMeetPoint != null ? escortMeetPoint.gameObject : null);

        if (escortCompletionPoint != null &&
            (escortMeetPoint == null ||
                escortCompletionPoint.gameObject != escortMeetPoint.gameObject))
        {
            FreezeEscortAnchor(escortCompletionPoint.gameObject);
        }
    }

    void FreezeEscortAnchor(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        NpcRoleUtility.StopForConversation(npc);

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.enabled = false;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.enabled = false;
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null)
        {
            mover.enabled = false;
        }

        Rigidbody2D rb = npc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        NpcRoleUtility.SetAction(npc, TaskAction("pausedTask"));
    }

    GameObject FindEscortNpcAtPoint(Transform point, GameObject exclude = null)
    {
        if (point == null)
        {
            return null;
        }

        float searchRadius = Mathf.Max(0.5f, escortNpcSearchRadius);
        Vector3 pointPosition = point.position;
        GameObject best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude))
        {
            if (villager == null ||
                villager.gameObject == exclude ||
                NpcRoleUtility.IsDead(villager.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(pointPosition, villager.transform.position);
            if (distance <= searchRadius && distance < bestDistance)
            {
                bestDistance = distance;
                best = villager.gameObject;
            }
        }

        foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude))
        {
            if (smartNpc == null ||
                smartNpc.gameObject == exclude ||
                NpcRoleUtility.IsDead(smartNpc.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(pointPosition, smartNpc.transform.position);
            if (distance <= searchRadius && distance < bestDistance)
            {
                bestDistance = distance;
                best = smartNpc.gameObject;
            }
        }

        return best;
    }

    GameObject FindEscortNpcAtPoint(Vector3 pointPosition, GameObject exclude = null)
    {
        float searchRadius = Mathf.Max(0.5f, escortNpcSearchRadius);
        GameObject best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude))
        {
            if (villager == null ||
                villager.gameObject == exclude ||
                NpcRoleUtility.IsDead(villager.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(pointPosition, villager.transform.position);
            if (distance <= searchRadius && distance < bestDistance)
            {
                bestDistance = distance;
                best = villager.gameObject;
            }
        }

        foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude))
        {
            if (smartNpc == null ||
                smartNpc.gameObject == exclude ||
                NpcRoleUtility.IsDead(smartNpc.gameObject))
            {
                continue;
            }

            float distance = Vector2.Distance(pointPosition, smartNpc.transform.position);
            if (distance <= searchRadius && distance < bestDistance)
            {
                bestDistance = distance;
                best = smartNpc.gameObject;
            }
        }

        return best;
    }

    bool HasEscortParticipantsAvailable(NpcTaskOffer offer)
    {
        return FindEscortNpcAtPoint(GetEscortCompanionPosition(offer)) != null &&
            ResolveEscortCompletionNpc(offer) != null;
    }

    Vector3 GetEscortFollowPosition(RunningNpcTask task)
    {
        if (task == null ||
            task.npc == null)
        {
            return Vector3.zero;
        }

        Vector3 leader = task.npc.transform.position;
        Vector3 destination = GetEscortCompletionPosition(task.offer);
        Vector2 awayFromDestination = (Vector2)(leader - destination);
        if (awayFromDestination.sqrMagnitude < 0.01f)
        {
            awayFromDestination = Random.insideUnitCircle.normalized;
        }
        else
        {
            awayFromDestination.Normalize();
        }

        return leader +
            (Vector3)(awayFromDestination *
                Mathf.Max(0.5f, escortFollowDistance));
    }

    void PrepareEscortTask(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null)
        {
            return;
        }

        task.escortConfirmed = false;
        task.escortGreetingConversationStarted = false;
        task.escortDeliveryConversationStarted = false;
        task.escortGreetingConversationStep = 0;
        task.escortDeliveryConversationStep = 0;
        task.escortDepartedFromCompanion = false;
        task.escortCompanionNpc = FindEscortNpcAtPoint(escortMeetPoint, task.npc);
        task.escortCompletionNpc =
            ResolveEscortCompletionNpc(task.offer, task.npc);
        task.escortCompanionHomePosition = GetEscortCompanionPosition(task.offer);
        task.workPosition = GetEscortCompanionPosition(task.offer);
        task.escortAvoidUntilTime = 0f;
        task.escortThreatMonster = null;

        if (task.escortCompanionNpc == null ||
            task.escortCompletionNpc == null)
        {
            task.stage = TavernTaskStage.ReturningToTurnIn;
            task.remainingTime = 0f;
            return;
        }

        PauseBaseAi(
            task.escortCompanionNpc,
            out task.escortPausedCompanionBaseAi,
            out task.escortPausedCompanionBaseAiWasEnabled);

        if (task.escortCompanionNpc != null)
        {
            NpcRoleUtility.StopForConversation(task.escortCompanionNpc);
            NpcRoleUtility.SetAction(
                task.escortCompanionNpc,
                TaskAction("followTaskRoute"));
        }
    }

}
