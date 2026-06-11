#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class VillagerMovementSmokeTest
{
    const string SessionKey = "Codex_VillagerMovementSmokeTest_Ran";

    static VillagerMovementSmokeTest()
    {
        if (Application.isBatchMode)
        {
            return;
        }

        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        EditorApplication.delayCall += RunOnce;
    }

    public static void RunFromCommandLine()
    {
        RunSmokeTest();
    }

    static void RunOnce()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        RunSmokeTest();
    }

    static void RunSmokeTest()
    {
        GameObject npc = null;
        GameObject target = null;
        GameObject wall = null;
        SimulationMode2D previousSimulationMode = Physics2D.simulationMode;

        try
        {
            wall = new GameObject("VillagerSmokeWall");
            BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
            wall.transform.position = new Vector3(2f, 0f, 0f);
            wallCollider.size = new Vector2(1f, 4f);

            target = new GameObject("VillagerSmokeTarget");
            target.transform.position = new Vector3(6f, 0f, 0f);

            npc = new GameObject("VillagerSmokeNpc");
            npc.transform.position = Vector3.zero;
            npc.AddComponent<CircleCollider2D>().radius = 0.2f;
            Rigidbody2D rb = npc.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            VillagerAI ai = npc.AddComponent<VillagerAI>();
            ai.generateFromEntityProfile = false;
            ai.useKinematicNpcMovement = false;
            ai.keepInsideNpcMapArea = false;
            ai.allowCrossNpcMapAreas = true;
            ai.useSmartPathfinding = false;
            ai.useLocalDetour = false;
            ai.keepInsideCombinedNpcMapAreas = false;
            ai.currentTarget = target.transform;
            ai.currentAction = "Smoke";
            ai.moveSpeed = 1.6f;

            InvokeNonPublic(ai, "Awake");
            InvokeNonPublic(ai, "ApplyRuntimePathPerformanceLimits");
            InvokeNonPublic(ai, "ConfigureRigidbody");
            InvokeNonPublic(ai, "Start");

            Physics2D.simulationMode = SimulationMode2D.Script;

            float startDistance =
                Vector2.Distance(npc.transform.position, target.transform.position);

            for (int i = 0; i < 420; i++)
            {
                InvokeMoveToPosition(ai, target.transform.position);
                InvokeNonPublic(ai, "FixedUpdate");
                Physics2D.Simulate(Time.fixedDeltaTime);
            }

            float endDistance =
                Vector2.Distance(npc.transform.position, target.transform.position);

            bool movedForward = endDistance < startDistance;
            bool routedAroundWall = npc.transform.position.x > 2.2f;

            string result =
                "[VillagerMovementSmokeTest] startDist=" + startDistance.ToString("0.00") +
                ", endDist=" + endDistance.ToString("0.00") +
                ", movedForward=" + movedForward +
                ", routedAroundWall=" + routedAroundWall +
                ", finalPos=" + npc.transform.position.ToString("F2");

            Debug.Log(result);
            WriteResult(result);
        }
        catch (Exception ex)
        {
            string error = "[VillagerMovementSmokeTest] " + ex;
            Debug.LogError(error);
            WriteResult(error);
        }
        finally
        {
            Physics2D.simulationMode = previousSimulationMode;

            if (npc != null)
            {
                UnityEngine.Object.DestroyImmediate(npc);
            }

            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }

            if (wall != null)
            {
                UnityEngine.Object.DestroyImmediate(wall);
            }
        }
    }

    static void WriteResult(string message)
    {
        try
        {
            string path =
                Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        "..",
                        "Logs",
                        "VillagerMovementSmokeTest.txt"));

            File.WriteAllText(path, message);
        }
        catch
        {
        }
    }

    static void InvokeMoveToPosition(VillagerAI instance, Vector3 position)
    {
        if (instance == null)
        {
            return;
        }

        MethodInfo method =
            typeof(VillagerAI).GetMethod(
                "MoveToPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);

        if (method != null)
        {
            method.Invoke(instance, new object[] { position });
        }
    }

    static void InvokeNonPublic(object instance, string methodName)
    {
        if (instance == null)
        {
            return;
        }

        MethodInfo method =
            instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

        if (method != null)
        {
            method.Invoke(instance, null);
        }
    }
}
#endif
