using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TribulationVisualDiagnostics
{
    const string PrefabPath = "Assets/Prefabs/PF_ThienKiepStrike.prefab";
    const string OutputFolder = "Temp/TribulationDiagnostics";
    const string RequestFileName = "CodexTribulationCapture.request";

    static TribulationVisualDiagnostics()
    {
        EditorApplication.update -= TryHandleRequest;
        EditorApplication.update += TryHandleRequest;
    }

    [MenuItem("Tools/Debug/Capture Tribulation Frames")]
    public static void CaptureTribulationFramesMenu()
    {
        CaptureTribulationFrames();
    }

    static void TryHandleRequest()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName ??
            Directory.GetCurrentDirectory();
        string requestPath =
            Path.Combine(projectRoot, "Temp", RequestFileName);

        if (!File.Exists(requestPath) ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        File.Delete(requestPath);
        CaptureTribulationFrames();
    }

    public static void CaptureTribulationFrames()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName ??
            Directory.GetCurrentDirectory();
        string outputFolder =
            Path.Combine(projectRoot, OutputFolder);
        Directory.CreateDirectory(outputFolder);

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                "TribulationVisualDiagnostics: missing prefab at " +
                PrefabPath);
            WriteSummary(
                outputFolder,
                "ERROR: Missing prefab at " + PrefabPath);
            return;
        }

        SceneSetup[] previousSceneSetup =
            EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            Camera camera = CreateCamera();
            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "TribulationVisualDiagnostics: failed to instantiate prefab.");
                WriteSummary(
                    outputFolder,
                    "ERROR: Failed to instantiate prefab.");
                return;
            }

            instance.transform.position = Vector3.zero;

            ThienKiepStrikePrefab strike =
                instance.GetComponent<ThienKiepStrikePrefab>();
            if (strike == null)
            {
                Debug.LogError(
                    "TribulationVisualDiagnostics: prefab is missing ThienKiepStrikePrefab.");
                WriteSummary(
                    outputFolder,
                    "ERROR: Prefab is missing ThienKiepStrikePrefab.");
                return;
            }

            MethodInfo setFrame =
                typeof(ThienKiepStrikePrefab).GetMethod(
                    "SetFrame",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (setFrame == null)
            {
                Debug.LogError(
                    "TribulationVisualDiagnostics: could not reflect SetFrame.");
                WriteSummary(
                    outputFolder,
                    "ERROR: Could not reflect SetFrame.");
                return;
            }

            strike.PrepareAt(Vector3.zero, Vector3.zero);

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("PrefabPath=" + PrefabPath);
            summary.AppendLine("CaptureTime=" + DateTime.Now.ToString("O"));

            for (int i = 0; i < 10; i++)
            {
                setFrame.Invoke(strike, new object[] { i });
                RenderTexture.active = null;

                SpriteRenderer renderer = strike.loiKiepRenderer;
                Sprite sprite = renderer != null ? renderer.sprite : null;
                Bounds bounds =
                    renderer != null ? renderer.bounds : default;

                summary.AppendLine();
                summary.AppendLine("Frame=" + i);
                summary.AppendLine("Enabled=" + (renderer != null && renderer.enabled));
                summary.AppendLine("Sprite=" + (sprite != null ? sprite.name : "null"));
                summary.AppendLine("RootPosition=" + strike.transform.position);
                summary.AppendLine(
                    "RendererLocalPosition=" +
                    (renderer != null
                        ? renderer.transform.localPosition.ToString()
                        : "null"));
                summary.AppendLine("BoundsCenter=" + bounds.center);
                summary.AppendLine("BoundsSize=" + bounds.size);

                string outputPath =
                    Path.Combine(outputFolder, "frame_" + i + ".png");
                CaptureCamera(camera, outputPath);
            }

            WriteSummary(outputFolder, summary.ToString());
            Debug.Log(
                "TribulationVisualDiagnostics: captured frames to " +
                outputFolder);
        }
        finally
        {
            if (previousSceneSetup != null &&
                previousSceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }
    }

    static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("DiagnosticCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.18f, 0.22f, 0.18f, 1f);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 50f;
        camera.cullingMask = ~0;
        return camera;
    }

    static void CaptureCamera(Camera camera, string outputPath)
    {
        const int width = 1024;
        const int height = 1024;

        RenderTexture renderTexture =
            new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D texture =
            new Texture2D(width, height, TextureFormat.ARGB32, false);

        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        File.WriteAllBytes(outputPath, png);

        camera.targetTexture = null;
        RenderTexture.active = previous;

        UnityEngine.Object.DestroyImmediate(renderTexture);
        UnityEngine.Object.DestroyImmediate(texture);
    }

    static void WriteSummary(string outputFolder, string summary)
    {
        File.WriteAllText(
            Path.Combine(outputFolder, "summary.txt"),
            summary);
    }
}
