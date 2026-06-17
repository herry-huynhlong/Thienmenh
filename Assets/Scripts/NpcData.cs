using System;
using System.Collections.Generic;
using UnityEngine;

public class NpcData : MonoBehaviour
{
    static readonly HashSet<string> assignedPersistentIds =
        new HashSet<string>();

    public string npcName = "Nguoi dan";
    public string persistentId = "";

    public string realm = "Mortals";

    public int hp = 100;

    public int maxHp = 100;

    public string currentAction = "";

    void Awake()
    {
        EnsurePersistentId();
    }

    void OnValidate()
    {
        EnsurePersistentId();
    }

    void OnDestroy()
    {
        if (!string.IsNullOrWhiteSpace(persistentId))
        {
            assignedPersistentIds.Remove(persistentId);
        }
    }

    public void EnsurePersistentId()
    {
        if (string.IsNullOrWhiteSpace(persistentId) ||
            assignedPersistentIds.Contains(persistentId))
        {
            persistentId = Guid.NewGuid().ToString("N");
        }

        assignedPersistentIds.Add(persistentId);
    }
}
