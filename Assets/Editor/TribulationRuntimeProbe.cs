using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TribulationRuntimeProbe
{
    const string RequestFileName = "CodexTribulationProbe.request";
    const string ResultFileName = "CodexTribulationProbe.result.txt";
    const string ScreenshotFileName = "CodexTribulationProbe.png";

    static bool probeRunning;
    static double probeStartedAt;
    static double nextSampleAt;
    static StringBuilder probeLog;
    static string projectRoot;
    static string resultPath;
    static string screenshotPath;

    static TribulationRuntimeProbe()
    {
        projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName ??
            Directory.GetCurrentDirectory();
        resultPath = Path.Combine(projectRoot, "Temp", ResultFileName);
        screenshotPath = Path.Combine(projectRoot, "Temp", ScreenshotFileName);

        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    static void Update()
    {
        if (!probeRunning)
        {
            TryStartProbeFromRequest();
            return;
        }

        if (!Application.isPlaying)
        {
            AppendLine("Play mode ended before probe finished.");
            FinishProbe();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now >= nextSampleAt)
        {
            SampleRuntimeState();
            nextSampleAt = now + 0.15d;
        }

        if (now - probeStartedAt >= 2.2d)
        {
            FinishProbe();
        }
    }

    static void TryStartProbeFromRequest()
    {
        string requestPath =
            Path.Combine(projectRoot, "Temp", RequestFileName);
        if (!File.Exists(requestPath) ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        File.Delete(requestPath);
        Directory.CreateDirectory(Path.Combine(projectRoot, "Temp"));

        if (!Application.isPlaying)
        {
            File.WriteAllText(
                resultPath,
                "status=not_playing" + Environment.NewLine +
                "message=Unity editor is not in Play Mode.");
            return;
        }

        probeLog = new StringBuilder();
        probeRunning = true;
        probeStartedAt = EditorApplication.timeSinceStartup;
        nextSampleAt = probeStartedAt;

        AppendLine("status=running");
        AppendLine("startedAt=" + DateTime.Now.ToString("O"));
        AppendLine("isPlaying=" + Application.isPlaying);

        HeavenlyTribulationSystem system =
            UnityEngine.Object.FindAnyObjectByType<HeavenlyTribulationSystem>(
                FindObjectsInactive.Include);
        if (system == null)
        {
            GameObject systemObject =
                new GameObject("Heavenly Tribulation System (Probe)");
            system =
                systemObject.AddComponent<HeavenlyTribulationSystem>();
            AppendLine("systemCreatedByProbe=true");
        }

        AppendLine("system=" + system.name);
        AppendLine("debugTestPoint=" + DescribeObject(system.debugTestPoint));
        AppendLine("debugTestTarget=" + DescribeObject(system.debugTestTarget));

        try
        {
            system.DebugPlayLoiKiepVisual();
            ScreenCapture.CaptureScreenshot(screenshotPath);
            AppendLine("debugVisualTriggered=true");
            AppendLine("screenshot=" + screenshotPath);
        }
        catch (Exception ex)
        {
            AppendLine("exception=" + ex);
            FinishProbe();
        }
    }

    static void SampleRuntimeState()
    {
        ThienKiepStrikePrefab[] visuals =
            UnityEngine.Object.FindObjectsByType<ThienKiepStrikePrefab>(
                FindObjectsInactive.Include);

        AppendLine(
            "sample=" + DateTime.Now.ToString("HH:mm:ss.fff") +
            " count=" + visuals.Length);

        for (int i = 0; i < visuals.Length; i++)
        {
            ThienKiepStrikePrefab visual = visuals[i];
            if (visual == null)
            {
                continue;
            }

            SpriteRenderer renderer = visual.loiKiepRenderer;
            string spriteName =
                renderer != null && renderer.sprite != null
                    ? renderer.sprite.name
                    : "null";
            string enabled =
                renderer != null && renderer.enabled
                    ? "true"
                    : "false";
            string activeInHierarchy =
                visual.gameObject.activeInHierarchy
                    ? "true"
                    : "false";

            AppendLine(
                "visual[" + i + "] " +
                "name=" + visual.name +
                " active=" + activeInHierarchy +
                " pos=" + visual.transform.position +
                " rendererEnabled=" + enabled +
                " sprite=" + spriteName +
                " sortingLayer=" + (renderer != null ? renderer.sortingLayerName : "null") +
                " sortingOrder=" + (renderer != null ? renderer.sortingOrder.ToString() : "null"));
        }
    }

    static string DescribeObject(UnityEngine.Object value)
    {
        return value == null
            ? "null"
            : value.name;
    }

    static void AppendLine(string value)
    {
        probeLog?.AppendLine(value);
    }

    static void FinishProbe()
    {
        probeRunning = false;
        if (probeLog == null)
        {
            return;
        }

        probeLog.AppendLine("finishedAt=" + DateTime.Now.ToString("O"));
        File.WriteAllText(resultPath, probeLog.ToString());
        probeLog = null;
    }
}
