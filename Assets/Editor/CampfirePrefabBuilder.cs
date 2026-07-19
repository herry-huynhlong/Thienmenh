using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CampfirePrefabBuilder
{
    const string SourceSpriteSheetPath = "Assets/UI/fire/fire.png";
    const string TargetPrefabPath = "Assets/Prefabs/WorldCampfire.prefab";
    const int BuildStartFrame = 0;
    const int BuildEndFrame = 5;
    const int BurnStartFrame = 15;
    const int BurnEndFrame = 23;
    const int MinimumBurnFrameCount = 4;

    [MenuItem("Tools/Campfire/Create World Campfire Prefab")]
    public static void CreateWorldCampfirePrefab()
    {
        AssetDatabase.ImportAsset(
            SourceSpriteSheetPath,
            ImportAssetOptions.ForceUpdate);

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(SourceSpriteSheetPath);
        var slices =
            SpriteSheetMetaUtility.LoadFrameSlicesFromMeta(
                SourceSpriteSheetPath + ".meta",
                "fire_");

        if (texture == null)
        {
            Debug.LogError("Could not load texture at " + SourceSpriteSheetPath);
            return;
        }

        var orderedSlices = slices
            .Where(slice => TryParseFrameId(slice.name, out _))
            .OrderBy(slice => GetFrameId(slice.name))
            .ToList();
        int buildSliceCount =
            orderedSlices.Count(slice =>
            {
                int id = GetFrameId(slice.name);
                return id >= BuildStartFrame &&
                    id <= BuildEndFrame;
            });
        int burnSliceCount =
            orderedSlices.Count(slice =>
            {
                int id = GetFrameId(slice.name);
                return id >= BurnStartFrame &&
                    id <= BurnEndFrame;
            });

        if (buildSliceCount < (BuildEndFrame - BuildStartFrame + 1) ||
            burnSliceCount < MinimumBurnFrameCount)
        {
            Debug.LogError(
                "Campfire prefab builder needs build frames fire_" +
                BuildStartFrame +
                "-fire_" +
                BuildEndFrame +
                " and at least " +
                MinimumBurnFrameCount +
                " burn slices in fire_" +
                BurnStartFrame +
                "-fire_" +
                BurnEndFrame +
                " at " +
                SourceSpriteSheetPath +
                ". BuildFound=" + buildSliceCount +
                " BurnFound=" + burnSliceCount);
            return;
        }

        GameObject root = new GameObject("WorldCampfire");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        WorldCampfire campfire = root.AddComponent<WorldCampfire>();

        campfire.targetRenderer = renderer;
        campfire.sourceTexture = texture;
        campfire.runtimeSlices = orderedSlices
            .Select(slice => new WorldCampfire.RuntimeFrameSlice
            {
                name = slice.name,
                rect = slice.rect,
                pivot = slice.pivot,
                pixelsPerUnit = 100f
            })
            .ToArray();
        campfire.frames = new Sprite[campfire.runtimeSlices.Length];
        campfire.buildStartFrame = BuildStartFrame;
        campfire.buildEndFrame = BuildEndFrame;
        campfire.burnStartFrame = BurnStartFrame;
        campfire.burnEndFrame = BurnEndFrame;
        campfire.burnDurationWorldHours = 8f;

        PrefabUtility.SaveAsPrefabAsset(root, TargetPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Created campfire prefab at " + TargetPrefabPath);
    }

    static bool TryParseFrameId(string name, out int frameId)
    {
        frameId = -1;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        int underscoreIndex = name.LastIndexOf('_');
        string suffix =
            underscoreIndex >= 0 &&
            underscoreIndex < name.Length - 1
                ? name.Substring(underscoreIndex + 1)
                : name;
        return int.TryParse(suffix, out frameId);
    }

    static int GetFrameId(string name)
    {
        int frameId;
        return TryParseFrameId(name, out frameId)
            ? frameId
            : int.MaxValue;
    }
}
