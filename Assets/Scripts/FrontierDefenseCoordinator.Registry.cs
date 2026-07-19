using System.Collections.Generic;
using UnityEngine;

public partial class FrontierDefenseCoordinator
{
    public static void RegisterPost(FrontierWatchPost post)
    {
        if (post == null)
        {
            return;
        }

        EnsureInstance();
        if (!posts.Contains(post))
        {
            posts.Add(post);
        }
    }

    public static void UnregisterPost(FrontierWatchPost post)
    {
        if (post == null)
        {
            return;
        }

        posts.Remove(post);
    }

    public static void RegisterBattleLine(FrontierBattleLine line)
    {
        if (line == null)
        {
            return;
        }

        EnsureInstance();
        if (!battleLines.Contains(line))
        {
            battleLines.Add(line);
        }
    }

    public static void UnregisterBattleLine(FrontierBattleLine line)
    {
        if (line == null)
        {
            return;
        }

        battleLines.Remove(line);
    }

    public static bool IsOfferAvailable(string postId)
    {
        FrontierWatchPost post = FindPost(postId);
        return post != null &&
            IsCanonicalWatchPost(post) &&
            post.isActiveAndEnabled &&
            !post.IsOccupied &&
            !post.assignmentInProgress;
    }

    public static bool IsOfferStartAvailable(string postId)
    {
        return IsOfferStartAvailable(postId, null);
    }

    public static bool IsOfferStartAvailable(
        string postId,
        GameObject npc)
    {
        FrontierWatchPost post = FindPost(postId);
        return post != null &&
            IsCanonicalWatchPost(post) &&
            post.isActiveAndEnabled &&
            !post.IsOccupied &&
            (!post.assignmentInProgress ||
            post.pendingAssignee == null ||
            post.pendingAssignee == npc);
    }

    public static bool IsVacancyUrgent(string postId)
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        FrontierWatchPost post = FindPost(postId);
        return coordinator != null &&
            post != null &&
            IsCanonicalWatchPost(post) &&
            post.isActiveAndEnabled &&
            !post.IsOccupied;
    }

    public static CultivationRealm GetEffectiveMinimumRealm(
        FrontierWatchPost post)
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        if (coordinator != null &&
            coordinator.relaxFrontierWatchRequirements)
        {
            return CultivationRealm.Mortal;
        }

        return post != null
            ? post.minimumRealm
            : CultivationRealm.QiRefining;
    }

    public static int GetEffectiveMinimumRealmStage(
        FrontierWatchPost post)
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        if (coordinator != null &&
            coordinator.relaxFrontierWatchRequirements)
        {
            return 1;
        }

        return post != null
            ? Mathf.Clamp(
                post.minimumRealmStage,
                1,
                CultivationProgression.MaxStage)
            : 1;
    }

    public static int GetMinimumFrontierWatchBravery()
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        return coordinator != null
            ? Mathf.Clamp(coordinator.minimumFrontierWatchBravery, 0, 100)
            : 0;
    }

    public static bool ShouldUrgentVacancyBypassStateDecline(string postId)
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        return coordinator != null &&
            coordinator.urgentVacancyBypassesFatigueAndHunger &&
            IsVacancyUrgent(postId);
    }

    public static float GetUrgentVacancyAcceptanceBonus(string postId)
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        if (coordinator == null ||
            !IsVacancyUrgent(postId))
        {
            return 0f;
        }

        return Mathf.Max(0f, coordinator.urgentVacancyAcceptanceBonus);
    }

    public static float GetUrgentVacancyCandidateScoreBonus(string postId)
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        if (coordinator == null ||
            !IsVacancyUrgent(postId))
        {
            return 0f;
        }

        return Mathf.Max(0f, coordinator.urgentVacancyCandidateScoreBonus);
    }

    public static void AppendVisibleOffersForProvider(
        NpcTaskProvider provider,
        List<NpcTaskOffer> targetOffers)
    {
        FrontierDefenseCoordinator coordinator = EnsureInstance();
        if (coordinator == null ||
            provider == null ||
            targetOffers == null)
        {
            return;
        }

        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null ||
                !post.isActiveAndEnabled ||
                !IsCanonicalWatchPost(post) ||
                post.IsOccupied ||
                post.assignmentInProgress)
            {
                continue;
            }

            NpcTaskProvider resolvedProvider =
                post.assignedProvider != null
                    ? post.assignedProvider
                    : NpcTaskProvider.FindNearestProvider(
                        post.transform.position);
            if (resolvedProvider != provider)
            {
                continue;
            }

            string postId = GetResolvedPostId(post);
            bool alreadyAdded = false;
            for (int j = 0; j < targetOffers.Count; j++)
            {
                NpcTaskOffer existing = targetOffers[j];
                if (!IsFrontierWatchOffer(existing) ||
                    !string.Equals(
                        existing.customTargetId,
                        postId,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                alreadyAdded = true;
                break;
            }

            if (!alreadyAdded)
            {
                targetOffers.Add(coordinator.BuildOffer(post));
            }
        }
    }

    public static Vector3 GetPrimaryPosition(
        string postId,
        Vector3 fallbackPosition)
    {
        FrontierWatchPost post = FindPost(postId);
        return post != null
            ? post.GetPrimaryPosition()
            : fallbackPosition;
    }

    public static Vector3 GetSecondaryPatrolPosition(
        string postId,
        Vector3 fallbackPosition)
    {
        FrontierWatchPost post = FindPost(postId);
        return post != null
            ? post.GetSecondaryPosition()
            : fallbackPosition;
    }

    public static Vector3 GetRestPosition(
        string postId,
        Vector3 fallbackPosition)
    {
        FrontierWatchPost post = FindPost(postId);
        return post != null
            ? post.GetRestPosition(fallbackPosition)
            : fallbackPosition;
    }

    public static Vector3 GetNightRestPosition(
        string postId,
        Vector3 fallbackPosition)
    {
        FrontierWatchPost post = FindPost(postId);
        if (post == null)
        {
            return fallbackPosition;
        }

        if (post.campfirePoint != null)
        {
            return post.campfirePoint.position;
        }

        return post.GetRestPosition(fallbackPosition);
    }

    public static void GetDailyPatrolSchedule(
        string postId,
        out float morningStartHour,
        out float morningEndHour,
        out float afternoonStartHour,
        out float afternoonEndHour,
        out bool hideDuringNightRest)
    {
        FrontierWatchPost post = FindPost(postId);
        if (post == null)
        {
            morningStartHour = 8f;
            morningEndHour = 12f;
            afternoonStartHour = 15f;
            afternoonEndHour = 18f;
            hideDuringNightRest = true;
            return;
        }

        morningStartHour =
            Mathf.Clamp(post.morningPatrolStartHour, 0f, 23.99f);
        morningEndHour =
            Mathf.Clamp(
                post.morningPatrolEndHour,
                morningStartHour,
                23.99f);
        afternoonStartHour =
            Mathf.Clamp(
                post.afternoonPatrolStartHour,
                morningEndHour,
                23.99f);
        afternoonEndHour =
            Mathf.Clamp(
                post.afternoonPatrolEndHour,
                afternoonStartHour,
                23.99f);
        hideDuringNightRest = post.hideDuringNightRest;
    }

    public static float GetRestDurationSeconds(string postId)
    {
        FrontierWatchPost post = FindPost(postId);
        float worldHours =
            post != null
                ? Mathf.Max(0.25f, post.restDurationWorldHours)
                : 1f;
        return Mathf.Max(
            1f,
            GameTime.WorldHoursToScaledSeconds(worldHours));
    }

    public static bool TryGetCampfireSetup(
        string postId,
        out GameObject prefab,
        out Vector3 buildPosition,
        out Vector3 spawnOffset,
        out float blockRadius,
        out float buildHour,
        out float buildDurationWorldHours)
    {
        prefab = null;
        buildPosition = Vector3.zero;
        spawnOffset = Vector3.zero;
        blockRadius = 0f;
        buildHour = 18f;
        buildDurationWorldHours = 0.2f;

        FrontierWatchPost post = FindPost(postId);
        if (post == null ||
            !post.buildCampfireOnFirstRest ||
            post.campfirePrefab == null ||
            post.campfirePoint == null)
        {
            return false;
        }

        prefab = post.campfirePrefab;
        buildPosition = post.campfirePoint.position;
        spawnOffset = post.campfireSpawnOffset;
        blockRadius = Mathf.Max(0f, post.campfireBlockRadius);
        buildHour = Mathf.Clamp(post.campfireBuildHour, 0f, 23.99f);
        buildDurationWorldHours =
            Mathf.Max(0.05f, post.campfireBuildDurationWorldHours);
        return true;
    }

    public static GameObject GetCurrentAssignee(string postId)
    {
        FrontierWatchPost post = FindPost(postId);
        if (post == null ||
            post.currentAssignee == null ||
            NpcRoleUtility.IsDead(post.currentAssignee))
        {
            return null;
        }

        return post.currentAssignee;
    }

    public static Vector3 GetCurrentAssigneePosition(
        string postId,
        Vector3 fallbackPosition)
    {
        GameObject assignee = GetCurrentAssignee(postId);
        return assignee != null
            ? assignee.transform.position
            : fallbackPosition;
    }

    public static Vector3 GetNextPatrolTarget(
        string postId,
        ref int patrolIndex,
        ref int patrolDirection,
        Vector3 fallbackPosition)
    {
        FrontierWatchPost post = FindPost(postId);
        if (post == null)
        {
            return fallbackPosition;
        }

        if (post.patrolPoints == null ||
            post.patrolPoints.Length == 0)
        {
            Vector3 primaryPosition =
                post.GetPrimaryPosition();
            Vector3 secondaryPosition =
                post.GetSecondaryPosition();

            if (Vector2.Distance(
                    primaryPosition,
                    secondaryPosition) <= 0.05f)
            {
                patrolIndex = 0;
                patrolDirection = 1;
                return primaryPosition;
            }

            patrolIndex = Mathf.Clamp(patrolIndex, 0, 1);
            return patrolIndex <= 0
                ? primaryPosition
                : secondaryPosition;
        }

        int clampedDirection = patrolDirection >= 0 ? 1 : -1;
        patrolDirection = clampedDirection;
        patrolIndex =
            Mathf.Clamp(
                patrolIndex,
                0,
                post.patrolPoints.Length - 1);
        return post.GetPatrolPoint(
            patrolIndex,
            fallbackPosition);
    }

    public static void AdvancePatrolIndex(
        string postId,
        ref int patrolIndex,
        ref int patrolDirection)
    {
        FrontierWatchPost post = FindPost(postId);
        if (post == null ||
            post.patrolPoints == null ||
            post.patrolPoints.Length <= 1)
        {
            if (post == null)
            {
                patrolIndex = 0;
                patrolDirection = 1;
                return;
            }

            Vector3 primaryPosition =
                post.GetPrimaryPosition();
            Vector3 secondaryPosition =
                post.GetSecondaryPosition();
            if (Vector2.Distance(
                    primaryPosition,
                    secondaryPosition) <= 0.05f)
            {
                patrolIndex = 0;
                patrolDirection = 1;
                return;
            }

            patrolIndex = patrolIndex <= 0 ? 1 : 0;
            patrolDirection = patrolIndex <= 0 ? 1 : -1;
            return;
        }

        patrolIndex += patrolDirection >= 0 ? 1 : -1;
        if (patrolIndex >= post.patrolPoints.Length)
        {
            patrolIndex = post.patrolPoints.Length - 2;
            patrolDirection = -1;
        }
        else if (patrolIndex < 0)
        {
            patrolIndex = 1;
            patrolDirection = 1;
        }
    }

    public static FrontierDefenseCoordinator EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        FrontierDefenseCoordinator existing =
            FindAnyObjectByType<FrontierDefenseCoordinator>(
                FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject created = new GameObject("FrontierDefenseCoordinator");
        Instance = created.AddComponent<FrontierDefenseCoordinator>();
        return Instance;
    }

    float IncreaseThreat(float amount)
    {
        currentFrontierThreat =
            Mathf.Clamp(
                currentFrontierThreat + Mathf.Max(0f, amount),
                0f,
                Mathf.Max(1f, maxFrontierThreat));

        return Mathf.Clamp(
            currentFrontierThreat /
            Mathf.Max(1f, maxFrontierThreat) * 100f,
            0f,
            100f);
    }

    void AddUrgentLog(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            WorldEventManager.Instance == null)
        {
            return;
        }

        WorldEventManager.Instance.AddLog(content, 2, true);
    }

    static bool IsFrontierWatchOffer(NpcTaskOffer offer)
    {
        return offer != null &&
            offer.taskType == NpcTaskType.FrontierWatch &&
            string.Equals(
                offer.customTaskId,
                "frontier_watch",
                System.StringComparison.OrdinalIgnoreCase);
    }

    static bool IsCanonicalWatchPost(FrontierWatchPost post)
    {
        if (post == null)
        {
            return false;
        }

        return FindCanonicalWatchPost(post) == post;
    }

    static FrontierWatchPost FindCanonicalWatchPost(
        FrontierWatchPost source)
    {
        if (source == null)
        {
            return null;
        }

        string groupKey = GetWatchPostGroupKey(source);
        FrontierWatchPost best = source;
        string bestId = GetResolvedPostId(source);

        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost candidate = posts[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !string.Equals(
                    GetWatchPostGroupKey(candidate),
                    groupKey,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            string candidateId = GetResolvedPostId(candidate);
            bool candidateHasProvider =
                candidate.assignedProvider != null;
            bool bestHasProvider =
                best != null &&
                best.assignedProvider != null;
            if (candidateHasProvider && !bestHasProvider)
            {
                best = candidate;
                bestId = candidateId;
                continue;
            }

            if (candidateHasProvider == bestHasProvider &&
                string.Compare(
                    candidateId,
                    bestId,
                    System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                best = candidate;
                bestId = candidateId;
            }
        }

        return best;
    }

    static string GetWatchPostGroupKey(FrontierWatchPost post)
    {
        if (post == null)
        {
            return string.Empty;
        }

        string taskKey = string.IsNullOrWhiteSpace(post.taskName)
            ? "frontier_watch"
            : post.taskName.Trim().ToLowerInvariant();
        string primaryKey = post.primaryPoint != null
            ? "p:" + UnityObjectIdUtility.GetRuntimeId(post.primaryPoint)
            : "p@:" + QuantizePositionKey(post.GetPrimaryPosition());
        string secondaryKey = post.secondaryPoint != null
            ? "s:" + UnityObjectIdUtility.GetRuntimeId(post.secondaryPoint)
            : "s@:" + QuantizePositionKey(post.GetSecondaryPosition());
        return taskKey + "|" + primaryKey + "|" + secondaryKey;
    }

    static string QuantizePositionKey(Vector3 position)
    {
        return Mathf.RoundToInt(position.x * 100f) + "_" +
            Mathf.RoundToInt(position.y * 100f) + "_" +
            Mathf.RoundToInt(position.z * 100f);
    }

    public static string ResolveCanonicalPostId(string postId)
    {
        FrontierWatchPost post = FindExactPost(postId);
        if (post == null)
        {
            return string.IsNullOrWhiteSpace(postId)
                ? string.Empty
                : postId.Trim();
        }

        FrontierWatchPost canonicalPost =
            FindCanonicalWatchPost(post);
        return GetResolvedPostId(canonicalPost ?? post);
    }

    static FrontierWatchPost FindPost(string postId)
    {
        FrontierWatchPost post = FindExactPost(postId);
        if (post == null)
        {
            return null;
        }

        FrontierWatchPost canonicalPost =
            FindCanonicalWatchPost(post);
        return canonicalPost ?? post;
    }

    static FrontierWatchPost FindExactPost(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId))
        {
            return null;
        }

        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost candidate = posts[i];
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(
                    GetResolvedPostId(candidate),
                    postId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    static string GetResolvedPostId(FrontierWatchPost post)
    {
        if (post == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(post.postId)
            ? post.postId.Trim()
            : post.gameObject.name;
    }
}
