using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class FullGameSaveControllerRuntimeTests
{
    const string FullSaveKey = "ThienMenh.Save.FullGame";
    const string FullSaveBackupKey = "ThienMenh.Save.FullGame.Backup";

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();
        ClearControllerInstance();
        DestroyRespawnTestObjects();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyRespawnTestObjects();
        PlayerPrefs.DeleteAll();
        ClearControllerInstance();
    }

    [Test]
    public void TryReadSaveData_FallsBackToBackupWhenPrimaryIsInvalid()
    {
        Component controller = CreateController();

        PlayerPrefs.SetString(FullSaveKey, "{invalid");
        PlayerPrefs.SetString(
            FullSaveBackupKey,
            "{\"saveVersion\":1,\"sceneName\":\"Lang\",\"favoriteNpcs\":[]}");

        bool loaded = InvokeTryReadSaveData(
            controller,
            out object data);

        Assert.IsTrue(loaded);
        Assert.NotNull(data);
        Assert.AreEqual("Lang", GetStringField(data, "sceneName"));
    }

    [Test]
    public void TryReadSaveData_MigratesLegacyChildRespawnMetadata()
    {
        Component controller = CreateController();
        PlayerPrefs.SetString(
            FullSaveKey,
            "{\"saveVersion\":2,\"sceneName\":\"Lang\",\"npcStates\":[{" +
            "\"hasIdentity\":true,\"npcId\":\"legacy-child\"," +
            "\"motherId\":\"legacy-mother\",\"displayName\":\"Legacy Child\"}]}");

        bool loaded = InvokeTryReadSaveData(controller, out object data);

        Assert.IsTrue(loaded);
        Assert.AreEqual(4, GetField<int>(data, "saveVersion"));
        IList states = GetField<IList>(data, "npcStates");
        Assert.AreEqual(1, states.Count);
        Assert.IsTrue(GetField<bool>(states[0], "isRuntimeSpawn"));
        Assert.IsTrue(GetField<bool>(states[0], "respawnFromFullSave"));
        Assert.AreEqual(
            "legacy-mother",
            GetField<string>(states[0], "respawnTemplateNpcId"));
    }

    [UnityTest]
    public IEnumerator ApplyFullSaveAfterSceneLoad_AlwaysClearsApplyingLoad()
    {
        Component controller = CreateController();
        PlayerPrefs.SetString(FullSaveKey, "{invalid");

        IEnumerator routine = InvokeApplyRoutine(controller);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }

        Assert.IsFalse(GetApplyingLoad(controller));
    }

    [Test]
    public void ApplyNpcStates_RespawnsRegisteredPrefabOnlyOnce()
    {
        Component controller = CreateController();
        Type identityType = ResolveGameType("NPCIdentity");
        Type actorType = ResolveGameType("SpawnedWorldActor");
        Assert.NotNull(identityType);
        Assert.NotNull(actorType);

        GameObject prefab = new GameObject("RespawnTestPrefab");
        Component prefabIdentity = prefab.AddComponent(identityType);
        SetField(prefabIdentity, "npcId", "respawn-prefab-template");
        prefab.AddComponent(actorType);

        string prefabKey = "test:respawn-prefab";
        MethodInfo registerPrefab = actorType.GetMethod(
            "RegisterRespawnPrefab",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(registerPrefab);
        registerPrefab.Invoke(null, new object[] { prefabKey, prefab });

        object data = CreateFullSaveData();
        object saved = CreateRuntimeNpcState(
            "respawn-prefab-child",
            "respawn-prefab-actor",
            prefabKey,
            "",
            "RespawnTestRestoredPrefab");
        AddNpcState(data, saved);

        InvokeApplyNpcStates(controller, data);
        InvokeApplyNpcStates(controller, data);

        List<Component> restored =
            FindIdentitiesByNpcId("respawn-prefab-child");
        Assert.AreEqual(1, restored.Count);
        Component actor = restored[0].GetComponent(actorType);
        Assert.NotNull(actor);
        Assert.AreEqual(
            "respawn-prefab-actor",
            GetField<string>(actor, "persistentId"));
    }

    [Test]
    public void ApplyNpcStates_FallsBackToTemplateNpc()
    {
        Component controller = CreateController();
        Type identityType = ResolveGameType("NPCIdentity");
        Assert.NotNull(identityType);

        GameObject mother = new GameObject("RespawnTestMother");
        Component motherIdentity = mother.AddComponent(identityType);
        SetField(motherIdentity, "npcId", "respawn-template-mother");
        SetField(motherIdentity, "npcName", "Template Mother");

        object data = CreateFullSaveData();
        object saved = CreateRuntimeNpcState(
            "respawn-template-child",
            "respawn-template-actor",
            "missing:test-prefab",
            "respawn-template-mother",
            "RespawnTestRestoredTemplate");
        AddNpcState(data, saved);

        InvokeApplyNpcStates(controller, data);
        InvokeApplyNpcStates(controller, data);

        List<Component> restored =
            FindIdentitiesByNpcId("respawn-template-child");
        Assert.AreEqual(1, restored.Count);
        Assert.AreEqual(
            "RespawnTestRestoredTemplate",
            restored[0].gameObject.name);
    }

    [Test]
    public void SaveNpcState_CapturesRuntimeRespawnMetadata()
    {
        Component controller = CreateController();
        Type identityType = ResolveGameType("NPCIdentity");
        Type actorType = ResolveGameType("SpawnedWorldActor");
        Assert.NotNull(identityType);
        Assert.NotNull(actorType);

        GameObject npc = new GameObject("RespawnTestSaveSource");
        Component identity = npc.AddComponent(identityType);
        SetField(identity, "npcId", "respawn-save-child");
        SetField(identity, "motherId", "respawn-save-mother");
        Component actor = npc.AddComponent(actorType);
        SetField(actor, "spawnedAtRuntime", true);
        SetField(actor, "respawnFromFullSave", true);
        SetField(actor, "prefabKey", "test:save-prefab");
        SetField(
            actor,
            "respawnTemplateNpcId",
            "respawn-save-mother");

        object data = CreateFullSaveData();
        MethodInfo saveNpcState = controller.GetType().GetMethod(
            "SaveNpcState",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(saveNpcState);
        saveNpcState.Invoke(
            controller,
            new object[]
            {
                data,
                npc,
                new HashSet<GameObject>()
            });

        IList states = GetField<IList>(data, "npcStates");
        Assert.AreEqual(1, states.Count);
        object saved = states[0];
        Assert.IsTrue(GetField<bool>(saved, "isRuntimeSpawn"));
        Assert.IsTrue(GetField<bool>(saved, "respawnFromFullSave"));
        Assert.AreEqual(
            "test:save-prefab",
            GetField<string>(saved, "prefabKey"));
        Assert.AreEqual(
            "respawn-save-mother",
            GetField<string>(saved, "respawnTemplateNpcId"));
    }

    static Component CreateController()
    {
        Type controllerType = ResolveGameType("FullGameSaveController");
        Assert.NotNull(controllerType, "Failed to resolve FullGameSaveController type.");

        GameObject gameObject = new GameObject("FullGameSaveControllerTest");
        return gameObject.AddComponent(controllerType);
    }

    static bool InvokeTryReadSaveData(
        Component controller,
        out object data)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "TryReadSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        object[] parameters = { null };
        bool loaded = (bool)method.Invoke(controller, parameters);
        data = parameters[0];
        return loaded;
    }

    static IEnumerator InvokeApplyRoutine(
        Component controller)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "ApplyFullSaveAfterSceneLoad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (IEnumerator)method.Invoke(controller, null);
    }

    static void InvokeApplyNpcStates(
        Component controller,
        object data)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "ApplyNpcStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(controller, new[] { data });
    }

    static object CreateFullSaveData()
    {
        Type dataType = ResolveGameType("FullGameSaveData");
        Assert.NotNull(dataType);
        return Activator.CreateInstance(dataType);
    }

    static object CreateRuntimeNpcState(
        string npcId,
        string actorId,
        string prefabKey,
        string templateNpcId,
        string objectName)
    {
        Type stateType = ResolveGameType("SavedNpcStateData");
        Assert.NotNull(stateType);
        object saved = Activator.CreateInstance(stateType);

        SetField(saved, "stateKey", "identity:" + npcId);
        SetField(saved, "npcId", npcId);
        SetField(saved, "worldActorPersistentId", actorId);
        SetField(saved, "displayName", objectName);
        SetField(saved, "position", new Vector3(7f, 11f, 0f));
        SetField(saved, "activeSelf", true);
        SetField(saved, "hasIdentity", true);
        SetField(saved, "npcName", objectName);
        SetField(saved, "isRuntimeSpawn", true);
        SetField(saved, "respawnFromFullSave", true);
        SetField(saved, "prefabKey", prefabKey);
        SetField(saved, "respawnTemplateNpcId", templateNpcId);
        SetField(saved, "savedObjectName", objectName);
        return saved;
    }

    static void AddNpcState(object data, object saved)
    {
        IList states = GetField<IList>(data, "npcStates");
        Assert.NotNull(states);
        states.Add(saved);
    }

    static List<Component> FindIdentitiesByNpcId(string npcId)
    {
        List<Component> result = new List<Component>();
        Type identityType = ResolveGameType("NPCIdentity");
        UnityEngine.Object[] identities =
            UnityEngine.Object.FindObjectsByType(
                identityType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < identities.Length; i++)
        {
            Component identity = identities[i] as Component;
            if (identity != null &&
                string.Equals(
                    GetField<string>(identity, "npcId"),
                    npcId,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(identity);
            }
        }

        return result;
    }

    static bool GetApplyingLoad(Component controller)
    {
        FieldInfo field = controller.GetType().GetField(
            "applyingLoad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (bool)field.GetValue(controller);
    }

    static void ClearControllerInstance()
    {
        Type controllerType = ResolveGameType("FullGameSaveController");
        if (controllerType == null)
        {
            return;
        }

        FieldInfo instanceField = controllerType.GetField(
            "instance",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(instanceField);

        Component instance =
            instanceField.GetValue(null) as Component;
        if (instance != null)
        {
            UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }

        instanceField.SetValue(null, null);
    }

    static string GetStringField(
        object target,
        string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field, target.GetType().Name + "." + fieldName + " was not found.");
        return field.GetValue(target) as string;
    }

    static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(
            field,
            target.GetType().Name + "." + fieldName + " was not found.");
        return (T)field.GetValue(target);
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(
            field,
            target.GetType().Name + "." + fieldName + " was not found.");
        field.SetValue(target, value);
    }

    static void DestroyRespawnTestObjects()
    {
        Type identityType = ResolveGameType("NPCIdentity");
        if (identityType == null)
        {
            return;
        }

        UnityEngine.Object[] identities =
            UnityEngine.Object.FindObjectsByType(
                identityType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        HashSet<GameObject> destroyed = new HashSet<GameObject>();
        for (int i = 0; i < identities.Length; i++)
        {
            Component identity = identities[i] as Component;
            GameObject gameObject = identity != null
                ? identity.gameObject
                : null;
            if (gameObject == null ||
                !gameObject.name.StartsWith(
                    "RespawnTest",
                    StringComparison.Ordinal) ||
                !destroyed.Add(gameObject))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    static Type ResolveGameType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
