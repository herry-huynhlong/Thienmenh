#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class NpcMapMoverSmokeTest
{
    const string SessionKey = "Codex_NpcMapMoverSmokeTest_Ran";

    static NpcMapMoverSmokeTest()
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
            wall = new GameObject("NpcMapMoverSmokeWall");
            BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
            wall.transform.position = new Vector3(2f, 0f, 0f);
            wallCollider.size = new Vector2(1f, 4f);

            target = new GameObject("NpcMapMoverSmokeTarget");
            target.transform.position = new Vector3(6f, 0f, 0f);

            npc = new GameObject("NpcMapMoverSmokeNpc");
            npc.transform.position = Vector3.zero;
            npc.AddComponent<CircleCollider2D>().radius = 0.2f;
            Rigidbody2D rb = npc.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            NpcMapMover2D mover = npc.AddComponent<NpcMapMover2D>();
            InvokeNonPublic(mover, "Awake");

            mover.useKinematicNpcMovement = false;
            mover.keepInsideWanderRadius = false;
            mover.keepInsideMapBounds = false;
            mover.allowCrossMapAreas = true;
            mover.autoResolveMapArea = false;
            mover.useObstacleAvoidance = true;
            mover.currentTarget = target.transform.position;
            mover.SetMoveTarget(target.transform.position, "Smoke", true);

            InvokeNonPublic(mover, "ConfigureRigidbody");

            Physics2D.simulationMode = SimulationMode2D.Script;

            float startDistance =
                Vector2.Distance(npc.transform.position, target.transform.position);

            for (int i = 0; i < 360; i++)
            {
                InvokeNonPublic(mover, "Update");
                InvokeNonPublic(mover, "FixedUpdate");
                Physics2D.Simulate(Time.fixedDeltaTime);
            }

            float endDistance =
                Vector2.Distance(npc.transform.position, target.transform.position);

            bool movedForward = endDistance < startDistance;
            bool routedAroundWall = npc.transform.position.x > 2.2f;

            string result =
                "[NpcMapMoverSmokeTest] startDist=" + startDistance.ToString("0.00") +
                ", endDist=" + endDistance.ToString("0.00") +
                ", movedForward=" + movedForward +
                ", routedAroundWall=" + routedAroundWall +
                ", finalPos=" + npc.transform.position.ToString("F2");

            Debug.Log(result);
            WriteResult(result);
        }
        catch (Exception ex)
        {
            string error = "[NpcMapMoverSmokeTest] " + ex;
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
                        "NpcMapMoverSmokeTest.txt"));

            File.WriteAllText(path, message);
        }
        catch
        {
            // Console log is the fallback.
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
