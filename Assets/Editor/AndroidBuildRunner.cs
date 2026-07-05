using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class AndroidBuildRunner
{
    [MenuItem("Tools/Build/Build Android APK")]
    public static void BuildApk()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new BuildFailedException(
                "No enabled scenes were found in EditorBuildSettings.");
        }

        string outputDirectory = Path.GetFullPath("Builds/Android");
        Directory.CreateDirectory(outputDirectory);

        string apkPath = Path.Combine(outputDirectory, "Thienmenh.apk");

        EditorUserBuildSettings.buildAppBundle = false;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            targetGroup = BuildTargetGroup.Android,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                "Android build failed: " +
                report.summary.result +
                " output=" + apkPath);
        }

        UnityEngine.Debug.Log(
            "[AndroidBuildRunner] Build succeeded: " + apkPath);
    }
}
