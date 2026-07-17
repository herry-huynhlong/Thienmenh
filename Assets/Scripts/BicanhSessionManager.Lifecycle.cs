using System.Collections.Generic;
using UnityEngine;

public partial class BicanhSessionManager
{
    void Update()
    {
        if (!sessionRunning)
        {
            TryAutoStartFromWorldEvent();
            return;
        }

        if (sessionDurationSeconds > 0f && Time.time >= sessionEndAt)
        {
            EndSession("timeout");
            return;
        }

        ProcessParticipantEliminations();
        ProcessDungeonDeaths();

        if (sessionDeathCount >= maxParticipantDeaths)
        {
            EndSession("death-limit");
            return;
        }
    }

    [ContextMenu("Begin Bicanh Session")]
    public void BeginSession()
    {
        if (sessionRunning)
        {
            return;
        }

        if (requireValidFallbackReturnPoint &&
            fallbackReturnPoint == null)
        {
            Debug.LogWarning("[Bicanh] Thieu fallbackReturnPoint hop le.");
            return;
        }

        RefreshSpawnPoints();

        List<GameObject> eligibleParticipants = CollectEligibleParticipants();
        if (eligibleParticipants.Count == 0)
        {
            Debug.LogWarning("[Bicanh] Khong tim thay participant hop le.");
            return;
        }

        if (cachedSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[Bicanh] Chua co spawn point hop le.");
            return;
        }

        snapshots.Clear();
        spawnedThisSession.Clear();
        occupiedSpawnPositions.Clear();

        List<Transform> shuffledSpawnPoints =
            new List<Transform>(cachedSpawnPoints);
        Shuffle(shuffledSpawnPoints);

        for (int i = 0; i < eligibleParticipants.Count; i++)
        {
            GameObject entity = eligibleParticipants[i];
            if (entity == null)
            {
                continue;
            }

            Transform spawnPoint =
                shuffledSpawnPoints[i % shuffledSpawnPoints.Count];
            Vector3 spawnPosition =
                GetSpawnPosition(spawnPoint, i);
            CaptureAndApply(entity, spawnPosition);
        }

        sessionRunning = snapshots.Count > 0;
        if (!sessionRunning)
        {
            Debug.LogWarning("[Bicanh] Khong tao duoc session.");
            return;
        }

        sessionEndAt = Time.time + Mathf.Max(1f, sessionDurationSeconds);
        activeEventName = "Bicanh";
        sessionDeathCount = 0;
        handledSecretRealmEvent = true;
        DisableSpawnAreaColliders();

        Debug.Log(
            $"[Bicanh] Session bat dau: {snapshots.Count} participant.");
    }

    [ContextMenu("End Bicanh Session")]
    public void EndSession()
    {
        EndSession("manual");
    }

    [ContextMenu("End Bicanh Session (Player)")]
    public void EndSessionByPlayer()
    {
        EndSession("player");
    }

    public void EndSession(string reason)
    {
        if (!sessionRunning)
        {
            return;
        }

        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            RestoreParticipant(snapshots[i], false);
        }

        snapshots.Clear();
        spawnedThisSession.Clear();
        occupiedSpawnPositions.Clear();
        RestoreSpawnAreaColliders();
        sessionRunning = false;
        activeEventName = "";
        sessionEndAt = 0f;
        sessionDeathCount = 0;

        Debug.Log($"[Bicanh] Session ket thuc: {reason}");
    }

    void ProcessDungeonDeaths()
    {
        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            ParticipantSnapshot snapshot = snapshots[i];
            if (snapshot == null ||
                snapshot.entity == null ||
                !IsDead(snapshot.entity))
            {
                continue;
            }

            HandleDungeonDeath(snapshot);
            snapshots.RemoveAt(i);
        }
    }

    void ProcessParticipantEliminations()
    {
        if (eliminationHpRatio <= 0f)
        {
            return;
        }

        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            ParticipantSnapshot snapshot = snapshots[i];
            if (snapshot == null ||
                snapshot.entity == null ||
                IsDead(snapshot.entity) ||
                !ShouldEliminateParticipant(snapshot))
            {
                continue;
            }

            ReturnParticipantToPreviousMap(
                snapshot,
                "near-death");
            snapshots.RemoveAt(i);
        }
    }

    void HandleDungeonDeath(ParticipantSnapshot snapshot)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        ReturnParticipantToPreviousMap(snapshot, "defeated");
    }

    public bool TryEliminateParticipant(
        GameObject entity,
        string reason = "eliminated")
    {
        if (!sessionRunning || entity == null)
        {
            return false;
        }

        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            ParticipantSnapshot snapshot = snapshots[i];
            if (snapshot == null ||
                snapshot.entity != entity)
            {
                continue;
            }

            ReturnParticipantToPreviousMap(snapshot, reason);
            snapshots.RemoveAt(i);
            return true;
        }

        return false;
    }

    void ReturnParticipantToPreviousMap(
        ParticipantSnapshot snapshot,
        string reason)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        GameObject entity = snapshot.entity;
        bool wasDead = IsDead(entity);
        bool countTowardLimit =
            snapshot.smartNpc != null ||
            snapshot.villager != null;

        if (countTowardLimit)
        {
            sessionDeathCount += 1;
        }

        RestoreParticipant(snapshot, true);

        if (wasDead)
        {
            ApplyReturnRecoveryState(snapshot);
        }
        else
        {
            ApplyReturnActionState(snapshot);
        }

        spawnedThisSession.Remove(entity);

        Vector3 returnPosition = ResolveReturnPosition(snapshot);
        Debug.Log(
            "[Bicanh] " +
            entity.name +
            " bi loai (" +
            reason +
            "), tra ve " +
            returnPosition + ".");
    }
}
