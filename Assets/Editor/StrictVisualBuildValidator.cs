using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StrictVisualBuildValidator :
    IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        ValidateWeatherResources();
        ValidateEnabledScenes();
    }

    static void ValidateWeatherResources()
    {
        WeatherVisualSystem.ValidateRequiredSpriteResource(
            "thoitiet/rain",
            "WeatherVisualSystem rain");
        WeatherVisualSystem.ValidateRequiredSpriteResource(
            "thoitiet/snow",
            "WeatherVisualSystem snow");
        WeatherAccumulationSystem.ValidateRequiredSpriteResource(
            "thoitiet/vungnuoc",
            "WeatherAccumulationSystem puddles");
        WeatherAccumulationSystem.ValidateRequiredSpriteResource(
            "thoitiet/loptuyet",
            "WeatherAccumulationSystem snow caps");
    }

    static void ValidateEnabledScenes()
    {
        EditorBuildSettingsScene[] buildScenes =
            EditorBuildSettings.scenes;

        for (int i = 0; i < buildScenes.Length; i++)
        {
            EditorBuildSettingsScene buildScene = buildScenes[i];
            if (buildScene == null ||
                !buildScene.enabled ||
                string.IsNullOrWhiteSpace(buildScene.path))
            {
                continue;
            }

            Scene openedScene =
                EditorSceneManager.OpenScene(
                    buildScene.path,
                    OpenSceneMode.Additive);

            try
            {
                ValidateSceneComponents(openedScene);
            }
            finally
            {
                EditorSceneManager.CloseScene(openedScene, true);
            }
        }
    }

    static void ValidateSceneComponents(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            WorldSignalEffect[] signals =
                root.GetComponentsInChildren<WorldSignalEffect>(true);
            for (int j = 0; j < signals.Length; j++)
            {
                ValidateFrames(
                    scene,
                    signals[j] != null ? signals[j].gameObject : null,
                    "WorldSignalEffect",
                    signals[j] != null ? signals[j].frames : null);
            }

            WorldMarriageHome[] homes =
                root.GetComponentsInChildren<WorldMarriageHome>(true);
            for (int j = 0; j < homes.Length; j++)
            {
                ValidateFrames(
                    scene,
                    homes[j] != null ? homes[j].gameObject : null,
                    "WorldMarriageHome",
                    homes[j] != null ? homes[j].frames : null);
            }

            WorldCampfire[] campfires =
                root.GetComponentsInChildren<WorldCampfire>(true);
            for (int j = 0; j < campfires.Length; j++)
            {
                ValidateFrames(
                    scene,
                    campfires[j] != null ? campfires[j].gameObject : null,
                    "WorldCampfire",
                    campfires[j] != null ? campfires[j].frames : null);
            }
        }
    }

    static void ValidateFrames(
        Scene scene,
        GameObject owner,
        string componentName,
        Sprite[] frames)
    {
        if (HasUsableSprite(frames))
        {
            return;
        }

        throw new BuildFailedException(
            componentName +
            " in scene '" +
            scene.path +
            "' on object '" +
            (owner != null ? owner.name : "(null)") +
            "' has no configured sprite frames. " +
            "Runtime-generated frames are disabled.");
    }

    static bool HasUsableSprite(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                return true;
            }
        }

        return false;
    }
}
