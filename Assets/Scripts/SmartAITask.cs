using System;
using UnityEngine;

public enum SmartAITaskGoal
{
    None = 0,
    Cultivate = 1,
    DoMission = 2,
    FreeHuntAndGather = 3,
    TradeBuySell = 4,
    Combat = 10,
    LowHpRecovery = 20,
    HeavenlyGift = 30,
    Treasure = 40,
    NeedPotion = 50,
    Pursued = 60,
    CriticalBreakthrough = 80
}

public enum SmartAITaskPriority
{
    Low = 10,
    Normal = 20,
    Important = 40,
    Emergency = 80,
    Critical = 100
}

[Serializable]
public class SmartAITask
{
    public SmartAITaskGoal goal = SmartAITaskGoal.None;
    public SmartAITaskPriority priority = SmartAITaskPriority.Low;
    public bool canBeInterrupted = true;
    public string reason = string.Empty;
    public float createdTime;

    public bool IsValid => goal != SmartAITaskGoal.None;
    public bool IsEmergency => priority >= SmartAITaskPriority.Emergency;

    public SmartAITask Clone()
    {
        return new SmartAITask
        {
            goal = goal,
            priority = priority,
            canBeInterrupted = canBeInterrupted,
            reason = reason,
            createdTime = createdTime
        };
    }
}
