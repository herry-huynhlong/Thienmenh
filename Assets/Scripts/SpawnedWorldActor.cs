using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpawnedWorldActor : MonoBehaviour
{
    static readonly Dictionary<string, SpawnedWorldActor> assignedPersistentIds =
        new Dictionary<string, SpawnedWorldActor>();

    public string persistentId = "";
    public bool isHiddenAtHome;
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
                out SpawnedWorldActor previousOwner) &&
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
                     out SpawnedWorldActor owner) &&
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
                out SpawnedWorldActor owner) &&
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
                   out SpawnedWorldActor owner) &&
               owner != null &&
               owner != this);

        return candidate;
    }
}
