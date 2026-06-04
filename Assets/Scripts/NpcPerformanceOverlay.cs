using UnityEngine;

public class NpcPerformanceOverlay : MonoBehaviour
{
    public bool visible = true;
    public KeyCode toggleKey = KeyCode.F3;
    public float refreshInterval = 0.5f;
    [Header("Display")]
    public float uiScale = 1.8f;
    public int fontSize = 24;
    public Vector2 screenPadding = new Vector2(16f, 16f);
    public bool autoScaleForScreen = true;

    static int npcFixedUpdates;
    static int monsterFixedUpdates;
    static int monsterThinkUpdates;
    static int monsterDetectScans;
    static int pathRequests;
    static int pathSuccesses;
    static int pathCacheHits;
    static int pathVisitedNodes;
    static float pathMs;

    float nextRefreshTime;
    float fps;
    float frameMs;
    int shownNpcFixedUpdates;
    int shownMonsterFixedUpdates;
    int shownMonsterThinkUpdates;
    int shownMonsterDetectScans;
    int shownPathRequests;
    int shownPathSuccesses;
    int shownPathCacheHits;
    int shownPathVisitedNodes;
    float shownPathMs;
    int villagerCount;
    int monsterCount;
    GUIStyle labelStyle;
    GUIStyle boxStyle;

    public static void RecordNpcFixedUpdate()
    {
        npcFixedUpdates++;
    }

    public static void RecordMonsterFixedUpdate()
    {
        monsterFixedUpdates++;
    }

    public static void RecordMonsterThinkUpdate()
    {
        monsterThinkUpdates++;
    }

    public static void RecordMonsterDetectScan()
    {
        monsterDetectScans++;
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
        shownMonsterFixedUpdates = monsterFixedUpdates;
        shownMonsterThinkUpdates = monsterThinkUpdates;
        shownMonsterDetectScans = monsterDetectScans;
        shownPathRequests = pathRequests;
        shownPathSuccesses = pathSuccesses;
        shownPathCacheHits = pathCacheHits;
        shownPathVisitedNodes = pathVisitedNodes;
        shownPathMs = pathMs;

        npcFixedUpdates = 0;
        monsterFixedUpdates = 0;
        monsterThinkUpdates = 0;
        monsterDetectScans = 0;
        pathRequests = 0;
        pathSuccesses = 0;
        pathCacheHits = 0;
        pathVisitedNodes = 0;
        pathMs = 0f;

        villagerCount =
            FindObjectsByType<VillagerAI>(
                FindObjectsInactive.Exclude).Length;

        monsterCount =
            FindObjectsByType<MonsterAI>(
                FindObjectsInactive.Exclude).Length;
    }

    void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        EnsureStyles();

        float scale = GetScale();
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

        float inverseScale = 1f / scale;
        Rect rect = new Rect(
            screenPadding.x * inverseScale,
            screenPadding.y * inverseScale,
            Mathf.Min(560f, Screen.width * inverseScale - screenPadding.x * 2f),
            250f);

        GUI.Box(rect, "", boxStyle);

        GUILayout.BeginArea(
            new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f));

        GUILayout.Label("NPC PERF DEBUG (F3)", labelStyle);
        GUILayout.Label("FPS: " + Mathf.RoundToInt(fps) + " | Frame: " + frameMs.ToString("0.0") + " ms", labelStyle);
        GUILayout.Label("Villagers active: " + villagerCount + " | NPC FixedUpdate ticks: " + shownNpcFixedUpdates, labelStyle);
        GUILayout.Label("Monsters active: " + monsterCount + " | Fixed: " + shownMonsterFixedUpdates + " | Think: " + shownMonsterThinkUpdates + " | Detect: " + shownMonsterDetectScans, labelStyle);
        GUILayout.Label("Path requests: " + shownPathRequests + " | success: " + shownPathSuccesses + " | cache: " + shownPathCacheHits, labelStyle);
        GUILayout.Label("A* nodes: " + shownPathVisitedNodes + " | path time: " + shownPathMs.ToString("0.00") + " ms", labelStyle);

        GUILayout.EndArea();
        GUI.matrix = oldMatrix;
    }

    float GetScale()
    {
        float scale = Mathf.Max(0.75f, uiScale);

        if (autoScaleForScreen)
        {
            scale *= Mathf.Clamp(Screen.width / 1280f, 1f, 2.4f);
        }

        return scale;
    }

    void EnsureStyles()
    {
        int scaledFontSize = Mathf.Max(16, fontSize);

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
        }

        labelStyle.fontSize = scaledFontSize;
        labelStyle.normal.textColor = Color.white;
        labelStyle.wordWrap = false;

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
        }
    }
}