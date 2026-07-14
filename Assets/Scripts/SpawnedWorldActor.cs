using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpawnedWorldActor : MonoBehaviour
{
    static readonly Dictionary<string, SpawnedWorldActor> assignedPersistentIds =
        new Dictionary<string, SpawnedWorldActor>();
    static readonly Dictionary<string, GameObject> registeredRespawnPrefabs =
        new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    public string persistentId = "";
    public bool isHiddenAtHome;

    [Header("Full Save Respawn")]
    public bool spawnedAtRuntime;
    public bool respawnFromFullSave;
    public string prefabKey = "";
    public string respawnTemplateNpcId = "";

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

    public void ConfigureRuntimeRespawn(
        string sourcePrefabKey,
        GameObject sourcePrefab,
        string templateNpcId)
    {
        spawnedAtRuntime = true;
        respawnFromFullSave = true;
        prefabKey = sourcePrefabKey ?? "";
        respawnTemplateNpcId = templateNpcId ?? "";

        if (sourcePrefab != null &&
            !string.IsNullOrWhiteSpace(prefabKey))
        {
            RegisterRespawnPrefab(prefabKey, sourcePrefab);
        }

        EnsurePersistentId();
    }

    public static void RegisterRespawnPrefab(
        string sourcePrefabKey,
        GameObject prefab)
    {
        if (string.IsNullOrWhiteSpace(sourcePrefabKey) || prefab == null)
        {
            return;
        }

        registeredRespawnPrefabs[sourcePrefabKey.Trim()] = prefab;
    }

    public static GameObject ResolveRespawnPrefab(string sourcePrefabKey)
    {
        if (string.IsNullOrWhiteSpace(sourcePrefabKey))
        {
            return null;
        }

        string normalizedKey = sourcePrefabKey.Trim();
        if (registeredRespawnPrefabs.TryGetValue(
                normalizedKey,
                out GameObject registeredPrefab))
        {
            if (registeredPrefab != null)
            {
                return registeredPrefab;
            }

            registeredRespawnPrefabs.Remove(normalizedKey);
        }

        const string resourcesPrefix = "resources:";
        string resourcesPath =
            normalizedKey.StartsWith(
                resourcesPrefix,
                StringComparison.OrdinalIgnoreCase)
                ? normalizedKey.Substring(resourcesPrefix.Length)
                : normalizedKey;

        GameObject resourcePrefab =
            Resources.Load<GameObject>(resourcesPath);
        if (resourcePrefab != null)
        {
            registeredRespawnPrefabs[normalizedKey] = resourcePrefab;
        }

        return resourcePrefab;
    }

    public static string BuildDefaultPrefabKey(
        string category,
        GameObject prefab)
    {
        string prefix = string.IsNullOrWhiteSpace(category)
            ? "npc"
            : category.Trim();
        string prefabName = prefab != null &&
            !string.IsNullOrWhiteSpace(prefab.name)
                ? prefab.name.Trim()
                : "default";
        return prefix + ":" + prefabName;
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
