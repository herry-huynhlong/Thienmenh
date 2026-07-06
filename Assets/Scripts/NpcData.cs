using System;
using System.Collections.Generic;
using UnityEngine;

public class NpcData : MonoBehaviour
{
    static readonly Dictionary<string, NpcData> assignedPersistentIds =
        new Dictionary<string, NpcData>();

    public string npcName = "Nguoi dan";
    public string persistentId = "";

    public string realm = "Mortals";

    public int hp = 100;

    public int maxHp = 100;

    public string currentAction = "";
    string registeredPersistentId = "";

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
        UnregisterPersistentId();
    }

    public void EnsurePersistentId()
    {
        if (!string.IsNullOrWhiteSpace(registeredPersistentId) &&
            registeredPersistentId != persistentId &&
            assignedPersistentIds.TryGetValue(
                registeredPersistentId,
                out NpcData previousOwner) &&
            previousOwner == this)
        {
            assignedPersistentIds.Remove(registeredPersistentId);
            registeredPersistentId = "";
        }

        if (string.IsNullOrWhiteSpace(persistentId))
        {
            persistentId = CreatePersistentId();
        }
        else if (assignedPersistentIds.TryGetValue(
                     persistentId,
                     out NpcData owner) &&
                 owner != null &&
                 owner != this)
        {
            persistentId = CreatePersistentId();
        }

        assignedPersistentIds[persistentId] = this;
        registeredPersistentId = persistentId;
    }

    void UnregisterPersistentId()
    {
        if (string.IsNullOrWhiteSpace(registeredPersistentId))
        {
            return;
        }

        if (assignedPersistentIds.TryGetValue(
                registeredPersistentId,
                out NpcData owner) &&
            owner == this)
        {
            assignedPersistentIds.Remove(registeredPersistentId);
        }

        registeredPersistentId = "";
    }

    string CreatePersistentId()
    {
        string candidate;

        do
        {
            candidate = Guid.NewGuid().ToString("N");
        }
        while (assignedPersistentIds.TryGetValue(
                   candidate,
                   out NpcData owner) &&
               owner != null &&
               owner != this);

        return candidate;
    }
}
