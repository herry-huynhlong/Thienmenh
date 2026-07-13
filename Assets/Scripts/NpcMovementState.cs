using System;
using UnityEngine;

public enum NpcMovementStatus
{
    None,
    Pending,
    Moving,
    Arrived,
    Paused,
    Blocked,
    Failed,
    Timeout,
    Cancelled
}

public enum NpcMovementFailureReason
{
    None,
    Arrived,
    Waiting,
    Conversation,
    CrowdYield,
    ObstacleBlocked,
    CrowdBlocked,
    Stuck,
    OutsideAllowedArea,
    NoClearTarget,
    Teleported,
    ReplacedTarget,
    Timeout
}

[Serializable]
public struct NpcMovementResult
{
    public int requestId;
    public NpcMovementStatus status;
    public NpcMovementFailureReason reason;
    public Vector2 target;
    public string action;
    public float startedAt;
    public float finishedAt;
    public float elapsedSeconds;

    public bool IsTerminal =>
        status == NpcMovementStatus.Arrived ||
        status == NpcMovementStatus.Failed ||
        status == NpcMovementStatus.Timeout ||
        status == NpcMovementStatus.Cancelled;

    public bool Succeeded => status == NpcMovementStatus.Arrived;
}

public interface INpcMovementResultProvider
{
    NpcMovementResult CurrentMovement { get; }
    NpcMovementResult LastMovementResult { get; }
}
