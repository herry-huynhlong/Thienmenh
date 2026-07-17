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
            post.isActiveAndEnabled &&
            !post.IsOccupied &&
            !post.assignmentInProgress;
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
                post.IsOccupied)
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
            return post.GetPrimaryPosition();
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
            patrolIndex = 0;
            patrolDirection = 1;
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

    static FrontierWatchPost FindPost(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId))
        {
            return null;
        }

        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null)
            {
                continue;
            }

            if (string.Equals(
                    GetResolvedPostId(post),
                    postId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return post;
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
