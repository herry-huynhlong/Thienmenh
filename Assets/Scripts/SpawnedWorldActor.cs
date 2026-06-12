using System;
using UnityEngine;

[DisallowMultipleComponent]
public class SpawnedWorldActor : MonoBehaviour
{
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

    public void EnsurePersistentId()
    {
        if (!string.IsNullOrWhiteSpace(persistentId))
        {
            return;
        }

        persistentId = Guid.NewGuid().ToString("N");
    }
}
