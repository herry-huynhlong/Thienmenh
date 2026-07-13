using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTeleport : MonoBehaviour
{
    [Header("Scene")]
    public string targetScene;

    [Header("Spawn")]
    public Vector2 spawnPosition;

    [Header("Safety")]
    [Min(1f)] public float loadTimeoutSeconds = 15f;

    [Header("Transition")]
    [Min(0f)] public float fadeOutSeconds = 0.2f;
    [Min(0f)] public float fadeInSeconds = 0.2f;
    [Min(0f)] public float settleDelaySeconds = 0.05f;
    public Color fadeColor = Color.black;

    bool isLoading = false;

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (isLoading)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        StartCoroutine(
            LoadScene(other.gameObject));
    }

    System.Collections.IEnumerator
        LoadScene(GameObject player)
    {
        if (player == null)
        {
            yield break;
        }

        isLoading = true;
        try
        {
            bool shouldFadeBackIn = false;

            if (!IsValidTargetScene(targetScene))
            {
                Debug.LogWarning(
                    "DoorTeleport ignored invalid targetScene: " +
                    targetScene);
                yield break;
            }

            Scene currentScene =
                SceneManager.GetActiveScene();

            if (!currentScene.IsValid() ||
                !currentScene.isLoaded)
            {
                Debug.LogWarning(
                    "DoorTeleport could not resolve the current active scene before loading " +
                    targetScene);
                yield break;
            }

            if (fadeOutSeconds > 0f || fadeInSeconds > 0f)
            {
                yield return SceneTransitionOverlay.FadeTo(
                    1f,
                    fadeOutSeconds,
                    fadeColor);
                shouldFadeBackIn = true;

                if (settleDelaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(settleDelaySeconds);
                }
            }

            FullGameSaveController.EnsureInstance().SaveFullGame(true);
            GameSaveSystem.FlushPendingCommit();

            AsyncOperation loadOperation =
                SceneManager.LoadSceneAsync(
                    targetScene,
                    LoadSceneMode.Additive);

            if (loadOperation == null)
            {
                Debug.LogWarning(
                    "DoorTeleport failed to start async load for scene " +
                    targetScene);
                yield return FadeBackIfNeeded(shouldFadeBackIn);
                yield break;
            }

            float timeoutAt =
                Time.unscaledTime + Mathf.Max(1f, loadTimeoutSeconds);
            bool loadTimedOut = false;

            while (!loadOperation.isDone)
            {
                if (Time.unscaledTime >= timeoutAt)
                {
                    Debug.LogWarning(
                        "DoorTeleport timed out while loading scene " +
                        targetScene);
                    loadTimedOut = true;
                    break;
                }

                yield return null;
            }

            if (loadTimedOut)
            {
                yield return FadeBackIfNeeded(shouldFadeBackIn);
                yield break;
            }

            Scene newScene =
                SceneManager.GetSceneByName(
                    targetScene);

            if (!newScene.IsValid() ||
                !newScene.isLoaded)
            {
                Debug.LogWarning(
                    "DoorTeleport loaded scene is invalid or not ready: " +
                    targetScene);
                yield return FadeBackIfNeeded(shouldFadeBackIn);
                yield break;
            }

            if (!SceneManager.SetActiveScene(newScene))
            {
                Debug.LogWarning(
                    "DoorTeleport could not set active scene to " +
                    targetScene);
                yield return FadeBackIfNeeded(shouldFadeBackIn);
                yield break;
            }

            player.transform.position =
                spawnPosition;

            yield return null;

            FullGameSaveController.EnsureInstance().SaveFullGame(true);

            AsyncOperation unloadOperation =
                SceneManager.UnloadSceneAsync(
                    currentScene);

            if (unloadOperation == null)
            {
                Debug.LogWarning(
                    "DoorTeleport could not unload previous scene " +
                    currentScene.name);
            }
            else
            {
                float unloadTimeoutAt =
                    Time.unscaledTime + Mathf.Max(1f, loadTimeoutSeconds);

                while (!unloadOperation.isDone)
                {
                    if (Time.unscaledTime >= unloadTimeoutAt)
                    {
                        Debug.LogWarning(
                            "DoorTeleport timed out while unloading scene " +
                            currentScene.name);
                        break;
                    }

                    yield return null;
                }
            }

            yield return FadeBackIfNeeded(shouldFadeBackIn);
        }
        finally
        {
            isLoading = false;
        }
    }

    bool IsValidTargetScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) &&
            sceneName != "PersistentScene" &&
            sceneName != "MainMenu" &&
            sceneName != "LiteMapScene" &&
            Application.CanStreamedLevelBeLoaded(sceneName);
    }

    System.Collections.IEnumerator FadeBackIfNeeded(bool shouldFadeBackIn)
    {
        if (!shouldFadeBackIn)
        {
            yield break;
        }

        if (settleDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(settleDelaySeconds);
        }

        yield return SceneTransitionOverlay.FadeTo(
            0f,
            fadeInSeconds,
            fadeColor);
    }
}
