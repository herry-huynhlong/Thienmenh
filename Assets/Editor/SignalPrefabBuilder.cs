using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SignalPrefabBuilder
{
    const string SourceSpriteSheetPath = "Assets/UI/fire/tinhieu.png";
    const string TargetPrefabPath = "Assets/Prefabs/WorldSignalEffect.prefab";
    const int MinimumFrameCount = 12;

    [MenuItem("Tools/Campfire/Create World Signal Prefab")]
    public static void CreateWorldSignalPrefab()
    {
        AssetDatabase.ImportAsset(
            SourceSpriteSheetPath,
            ImportAssetOptions.ForceUpdate);

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(SourceSpriteSheetPath);
        var slices =
            SpriteSheetMetaUtility.LoadFrameSlicesFromMeta(
                SourceSpriteSheetPath + ".meta",
                "tinhieu_")
            .OrderBy(GetFrameIndex)
            .ToList();

        if (texture == null)
        {
            Debug.LogError("Could not load texture at " + SourceSpriteSheetPath);
            return;
        }

        if (slices.Count < MinimumFrameCount)
        {
            Debug.LogError(
                "Signal prefab builder needs at least " +
                MinimumFrameCount +
                " frame slices at " +
                SourceSpriteSheetPath +
                ". Found=" + slices.Count);
            return;
        }

        GameObject root = new GameObject("WorldSignalEffect");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        WorldSignalEffect signal = root.AddComponent<WorldSignalEffect>();

        signal.targetRenderer = renderer;
        signal.sourceTexture = texture;
        signal.runtimeSlices = slices
            .Take(MinimumFrameCount)
            .Select(slice => new WorldSignalEffect.RuntimeFrameSlice
            {
                name = slice.name,
                rect = slice.rect,
                pivot = new Vector2(0.5f, 0f),
                pixelsPerUnit = 100f
            })
            .ToArray();
        signal.frames = new Sprite[signal.runtimeSlices.Length];
        signal.framesPerSecond = 14f;
        signal.destroyOnComplete = true;
        signal.hideRendererOnComplete = true;
        signal.autoStartOnEnable = true;

        PrefabUtility.SaveAsPrefabAsset(root, TargetPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Created signal prefab at " + TargetPrefabPath);
    }

    static int GetFrameIndex(SpriteSheetMetaUtility.FrameSliceData slice)
    {
        if (string.IsNullOrWhiteSpace(slice.name))
        {
            return int.MaxValue;
        }

        int underscoreIndex = slice.name.LastIndexOf('_');
        if (underscoreIndex < 0 ||
            underscoreIndex >= slice.name.Length - 1)
        {
            return int.MaxValue;
        }

        return int.TryParse(
            slice.name.Substring(underscoreIndex + 1),
            out int frameIndex)
            ? frameIndex
            : int.MaxValue;
    }
}
