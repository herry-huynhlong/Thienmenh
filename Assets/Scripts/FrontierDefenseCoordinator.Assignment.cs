using System.Collections.Generic;
using UnityEngine;

public partial class FrontierDefenseCoordinator
{
    void Update()
    {
        assignTimer += Time.deltaTime;
        if (assignTimer < Mathf.Max(0.5f, assignCheckIntervalSeconds))
        {
            return;
        }

        assignTimer = 0f;
        AssignVacantPosts();
        UpdateDangerStates();
        UpdateBeastWave();
    }

    void AssignVacantPosts()
    {
        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null ||
                !post.isActiveAndEnabled ||
                !IsCanonicalWatchPost(post))
            {
                continue;
            }

            if (post.currentAssignee != null &&
                !NpcRoleUtility.IsDead(post.currentAssignee))
            {
                ClearVacancyTracking(GetResolvedPostId(post));
                continue;
            }

            if (post.currentAssignee != null)
            {
                HandleWatcherDeath(post, post.currentAssignee);
            }

            post.currentAssignee = null;
            post.pendingAssignee = null;

            string postId = GetResolvedPostId(post);
            MarkPostVacant(postId);
            if (!autoAssignVacantPosts)
            {
                post.assignmentInProgress = false;
                nextAssignmentTimes[postId] = 0f;
                continue;
            }

            if (nextAssignmentTimes.TryGetValue(postId, out float nextTime) &&
                Time.time < nextTime)
            {
                continue;
            }

            if (post.assignmentInProgress)
            {
                continue;
            }

            TryAssignPost(post);
        }
    }

    void TryAssignPost(FrontierWatchPost post)
    {
        if (post == null)
        {
            return;
        }

        NpcTaskProvider provider =
            post.assignedProvider != null
                ? post.assignedProvider
                : NpcTaskProvider.FindNearestProvider(
                    post.transform.position);
        if (provider == null)
        {
            return;
        }

        NpcTaskOffer offer = BuildOffer(post);
        List<GameObject> candidates =
            FindCandidateOrder(post);
        if (candidates.Count == 0)
        {
            post.assignmentInProgress = false;
            SetRetry(post);
            return;
        }

        post.assignmentInProgress = true;
        post.pendingAssignee = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            post.pendingAssignee = candidate;
            if (provider.TryStartPlannedTask(candidate, offer))
            {
                return;
            }
        }

        post.pendingAssignee = null;
        post.assignmentInProgress = false;
        SetRetry(post);
    }

    List<GameObject> FindCandidateOrder(FrontierWatchPost post)
    {
        List<GameObject> candidates =
            new List<GameObject>();
        if (post == null)
        {
            return candidates;
        }

        SmartNpcAI[] npcs =
            FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude);

        for (int i = 0; i < npcs.Length; i++)
        {
            SmartNpcAI npc = npcs[i];
            if (IsEligibleCandidate(npc, post))
            {
                candidates.Add(npc.gameObject);
            }
        }

        VillagerAI[] villagers =
            FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude);

        for (int i = 0; i < villagers.Length; i++)
        {
            VillagerAI villager = villagers[i];
            if (IsEligibleCandidate(villager, post))
            {
                candidates.Add(villager.gameObject);
            }
        }

        candidates.Sort((left, right) =>
            ScoreCandidate(right, post).CompareTo(
                ScoreCandidate(left, post)));

        return candidates;
    }

    float ScoreCandidate(
        GameObject npc,
        FrontierWatchPost post)
    {
        if (npc == null ||
            post == null)
        {
            return float.NegativeInfinity;
        }

        float bravery = 0f;
        CultivationRealm realm = CultivationRealm.Mortal;
        int realmStage = 1;

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null && smartNpc.enabled)
        {
            bravery = smartNpc.bravery;
            realm = smartNpc.realm;
            realmStage = smartNpc.realmStage;
        }
        else
        {
            VillagerAI villager = npc.GetComponent<VillagerAI>();
            if (villager != null)
            {
                bravery = villager.bravery;
                realm = villager.realm;
                realmStage = villager.realmStage;
            }
        }

        float distance =
            Vector2.Distance(
                npc.transform.position,
                post.GetPrimaryPosition());
        return bravery * 0.6f +
            CultivationProgression.GetRealmPower(
                realm,
                realmStage) *
            0.02f -
            distance * 0.12f +
            FrontierDefenseCoordinator.GetUrgentVacancyCandidateScoreBonus(
                GetResolvedPostId(post));
    }

    bool IsEligibleCandidate(
        SmartNpcAI npc,
        FrontierWatchPost post)
    {
        CultivationRealm minimumRealm =
            FrontierDefenseCoordinator.GetEffectiveMinimumRealm(post);
        int minimumStage =
            FrontierDefenseCoordinator.GetEffectiveMinimumRealmStage(post);

        return npc != null &&
            npc.enabled &&
            !npc.IsDead &&
            npc.canFight &&
            NpcRoleUtility.MeetsRealm(
                npc.gameObject,
                minimumRealm,
                minimumStage);
    }

    bool IsEligibleCandidate(
        VillagerAI npc,
        FrontierWatchPost post)
    {
        if (npc == null ||
            post == null ||
            !npc.enabled ||
            npc.IsDead ||
            npc.ageGroup != VillagerAgeGroup.Adult)
        {
            return false;
        }

        CultivationRealm minimumRealm =
            FrontierDefenseCoordinator.GetEffectiveMinimumRealm(post);
        int minimumStage =
            FrontierDefenseCoordinator.GetEffectiveMinimumRealmStage(post);

        return NpcRoleUtility.MeetsRealm(
                npc.gameObject,
                minimumRealm,
                minimumStage) &&
            npc.bravery >=
                FrontierDefenseCoordinator.GetMinimumFrontierWatchBravery();
    }

    NpcTaskOffer BuildOffer(FrontierWatchPost post)
    {
        CultivationRealm minimumRealm =
            FrontierDefenseCoordinator.GetEffectiveMinimumRealm(post);
        int minimumStage =
            FrontierDefenseCoordinator.GetEffectiveMinimumRealmStage(post);
        NpcTaskRank rewardRank =
            ResolveOfferRank(
                post,
                minimumRealm,
                minimumStage);
        StatItemData rewardItem = post.rewardItem;
        if (rewardItem == null &&
            post.autoResolveLowGradeReward)
        {
            rewardItem = ResolveLowGradeRewardItem();
        }

        return new NpcTaskOffer
        {
            taskName = string.IsNullOrWhiteSpace(post.taskName)
                ? "Tran thu Ma Thu Son Mach"
                : post.taskName,
            taskType = NpcTaskType.FrontierWatch,
            audience = NpcTaskAudience.AnyNpc,
            rank = rewardRank,
            minRealm = minimumRealm,
            minRealmStage = minimumStage,
            rewardSpiritStone = Mathf.Max(0, post.rewardSpiritStone),
            rewardItem = rewardItem,
            rewardItemAmount =
                rewardItem != null
                    ? Mathf.Max(1, post.rewardItemAmount)
                    : 0,
            workDurationWorldHours =
                Mathf.Max(24f, post.shiftDurationDays * 24f),
            customTaskId = "frontier_watch",
            customTargetId = GetResolvedPostId(post)
        };
    }

    NpcTaskRank ResolveOfferRank(
        FrontierWatchPost post,
        CultivationRealm minimumRealm,
        int minimumStage)
    {
        if (post == null)
        {
            return NpcTaskRank.Ha;
        }

        // When frontier requirements are relaxed so ordinary NPCs can fill an
        // urgent empty post, keep the offer rank low as well. Otherwise the
        // auto-assign rank gate still treats it like a high-tier mission.
        if (minimumRealm == CultivationRealm.Mortal &&
            minimumStage <= 1)
        {
            return NpcTaskRank.Ha;
        }

        return post.rewardRank;
    }

    StatItemData ResolveLowGradeRewardItem()
    {
        if (cachedLowGradeRewardItem != null)
        {
            return cachedLowGradeRewardItem;
        }

        StatItemData[] items =
            Resources.LoadAll<StatItemData>(string.Empty);
        for (int i = 0; i < items.Length; i++)
        {
            StatItemData item = items[i];
            if (item == null ||
                item.grade != ItemGrade.Ha)
            {
                continue;
            }

            if (item.itemType == ItemType.DanDuoc ||
                item.itemType == ItemType.VatLieu)
            {
                cachedLowGradeRewardItem = item;
                return cachedLowGradeRewardItem;
            }
        }

        return null;
    }

    void HandleTaskStarted(
        GameObject npc,
        NpcTaskOffer offer)
    {
        if (!IsFrontierWatchOffer(offer))
        {
            return;
        }

        FrontierWatchPost post =
            FindPost(offer.customTargetId);
        if (post == null)
        {
            return;
        }

        post.currentAssignee = npc;
        post.pendingAssignee = null;
        post.assignmentInProgress = false;
        string postId = GetResolvedPostId(post);
        nextAssignmentTimes[postId] = 0f;
        ClearVacancyTracking(postId);
    }

    void HandleTaskFinished(
        GameObject npc,
        NpcTaskOffer offer,
        bool completed)
    {
        if (!IsFrontierWatchOffer(offer))
        {
            return;
        }

        FrontierWatchPost post =
            FindPost(offer.customTargetId);
        if (post == null)
        {
            return;
        }

        if (!completed)
        {
            HandleWatcherDeath(post, npc);
        }

        if (post.currentAssignee == npc)
        {
            post.currentAssignee = null;
        }

        if (post.pendingAssignee == npc)
        {
            post.pendingAssignee = null;
        }

        post.assignmentInProgress = false;
        MarkPostVacant(GetResolvedPostId(post));
        SetRetry(post);
    }

    void UpdateDangerStates()
    {
        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null ||
                !post.isActiveAndEnabled ||
                !IsCanonicalWatchPost(post))
            {
                continue;
            }

            if (post.currentAssignee != null &&
                !NpcRoleUtility.IsDead(post.currentAssignee))
            {
                ClearVacancyTracking(GetResolvedPostId(post));
                continue;
            }

            if (post.currentAssignee != null)
            {
                HandleWatcherDeath(post, post.currentAssignee);
                post.currentAssignee = null;
            }

            post.pendingAssignee = null;

            string postId = GetResolvedPostId(post);
            PostDangerState state = GetOrCreateDangerState(postId);
            if (state.vacantSinceTime < 0f)
            {
                state.vacantSinceTime = Time.time;
            }

            float firstDelay =
                GameTime.WorldHoursToScaledSeconds(vacancyAlertDelayWorldHours);
            float repeatDelay =
                GameTime.WorldHoursToScaledSeconds(repeatedVacancyAlertWorldHours);
            float elapsed = Mathf.Max(0f, Time.time - state.vacantSinceTime);

            if (elapsed < firstDelay)
            {
                continue;
            }

            int expectedAlertCount = 1;
            if (repeatDelay > 0.01f)
            {
                expectedAlertCount +=
                    Mathf.FloorToInt(
                        Mathf.Max(0f, elapsed - firstDelay) / repeatDelay);
            }

            while (state.vacancyAlertCount < expectedAlertCount)
            {
                state.vacancyAlertCount++;
                RaiseVacancyAlert(post, state.vacancyAlertCount);
            }
        }
    }

    void SetRetry(FrontierWatchPost post)
    {
        if (post == null)
        {
            return;
        }

        nextAssignmentTimes[GetResolvedPostId(post)] =
            Time.time + Mathf.Max(1f, retryAssignmentDelaySeconds);
    }

    void MarkPostVacant(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId))
        {
            return;
        }

        PostDangerState state = GetOrCreateDangerState(postId);
        if (state.vacantSinceTime < 0f)
        {
            state.vacantSinceTime = Time.time;
        }
    }

    void ClearVacancyTracking(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId) ||
            !postDangerStates.TryGetValue(postId, out PostDangerState state))
        {
            return;
        }

        state.vacantSinceTime = -1f;
        state.vacancyAlertCount = 0;
    }

    PostDangerState GetOrCreateDangerState(string postId)
    {
        if (!postDangerStates.TryGetValue(postId, out PostDangerState state) ||
            state == null)
        {
            state = new PostDangerState();
            postDangerStates[postId] = state;
        }

        return state;
    }

    void HandleWatcherDeath(
        FrontierWatchPost post,
        GameObject npc)
    {
        if (post == null ||
            npc == null ||
            !NpcRoleUtility.IsDead(npc))
        {
            return;
        }

        int instanceId =
            UnityObjectIdUtility.GetRuntimeId(npc);
        if (!alertedWatcherInstanceIds.Add(instanceId))
        {
            return;
        }

        float threatPercent =
            IncreaseThreat(threatIncreaseOnWatcherDeath);
        TriggerWatcherDeathSignal(post, npc);
        AddUrgentLog(
            UiText.Format(
                "frontierDefense",
                "watcherDeathAlert",
                post.GetDisplayName(),
                threatPercent.ToString("0")));
    }

    void RaiseVacancyAlert(
        FrontierWatchPost post,
        int vacancyStage)
    {
        if (post == null)
        {
            return;
        }

        float threatPercent =
            IncreaseThreat(threatIncreaseOnVacancyAlert);
        AddUrgentLog(
            UiText.Format(
                "frontierDefense",
                vacancyStage > 1
                    ? "vacancyEscalationAlert"
                    : "vacancyAlert",
                post.GetDisplayName(),
                threatPercent.ToString("0")));
    }
}
