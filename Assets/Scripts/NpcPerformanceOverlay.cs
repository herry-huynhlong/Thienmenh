using UnityEngine;

public class NpcPerformanceOverlay : MonoBehaviour
{
    public bool visible = true;
    public KeyCode toggleKey = KeyCode.F3;
    public float refreshInterval = 0.5f;

    static int npcFixedUpdates;
    static int pathRequests;
    static int pathSuccesses;
    static int pathCacheHits;
    static int pathVisitedNodes;
    static float pathMs;

    float nextRefreshTime;
    float fps;
    float frameMs;
    int shownNpcFixedUpdates;
    int shownPathRequests;
    int shownPathSuccesses;
    int shownPathCacheHits;
    int shownPathVisitedNodes;
    float shownPathMs;
    int villagerCount;

    public static void RecordNpcFixedUpdate()
    {
        npcFixedUpdates++;
    }

    public static void RecordPathRequest()
    {
        pathRequests++;
    }

    public static void RecordPathCacheHit()
    {
        pathCacheHits++;
    }

    public static void RecordPathResult(
        bool success,
        int visitedNodes,
        float elapsedMs)
    {
        if (success)
        {
            pathSuccesses++;
        }

        pathVisitedNodes += Mathf.Max(0, visitedNodes);
        pathMs += Mathf.Max(0f, elapsedMs);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            visible = !visible;
        }

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        float interval =
            Mathf.Max(0.05f, refreshInterval);

        nextRefreshTime =
            Time.unscaledTime + interval;

        fps =
            Time.unscaledDeltaTime > 0f
            ? 1f / Time.unscaledDeltaTime
            : 0f;

        frameMs =
            Time.unscaledDeltaTime * 1000f;

        shownNpcFixedUpdates = npcFixedUpdates;
        shownPathRequests = pathRequests;
        shownPathSuccesses = pathSuccesses;
        shownPathCacheHits = pathCacheHits;
        shownPathVisitedNodes = pathVisitedNodes;
        shownPathMs = pathMs;

        npcFixedUpdates = 0;
        pathRequests = 0;
        pathSuccesses = 0;
        pathCacheHits = 0;
        pathVisitedNodes = 0;
        pathMs = 0f;

        villagerCount =
            FindObjectsByType<VillagerAI>(
                FindObjectsInactive.Exclude).Length;
    }

    void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        GUI.color = Color.white;

        Rect rect =
            new Rect(12f, 12f, 360f, 150f);

        GUI.Box(rect, "");

        GUILayout.BeginArea(
            new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f));

        GUILayout.Label("NPC PERF DEBUG (F3)");
        GUILayout.Label("FPS: " + Mathf.RoundToInt(fps) + " | Frame: " + frameMs.ToString("0.0") + " ms");
        GUILayout.Label("Villagers active: " + villagerCount + " | FixedUpdate ticks: " + shownNpcFixedUpdates);
        GUILayout.Label("Path requests: " + shownPathRequests + " | success: " + shownPathSuccesses + " | cache: " + shownPathCacheHits);
        GUILayout.Label("A* nodes: " + shownPathVisitedNodes + " | path time: " + shownPathMs.ToString("0.00") + " ms");

        GUILayout.EndArea();
    }
}
