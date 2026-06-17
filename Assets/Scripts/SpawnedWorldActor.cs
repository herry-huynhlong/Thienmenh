using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpawnedWorldActor : MonoBehaviour
{
    static readonly HashSet<string> assignedPersistentIds =
        new HashSet<string>();

    public string persistentId = "";
    public bool isHiddenAtHome;

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
