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

        GameObject playerRoot =
            ResolvePlayerRoot(other.gameObject);

        if (playerRoot == null ||
            !playerRoot.CompareTag("Player"))
        {
            return;
        }

        StartCoroutine(
            LoadScene(playerRoot));
    }

    System.Collections.IEnumerator
        LoadScene(GameObject player)
    {
        if (player == null)
        {
            yield break;
        }

        player = ResolvePlayerRoot(player);

        if (player == null)
        {
            yield break;
        }

        isLoading = true;
        try
        {
            bool shouldFadeBackIn = false;
            Scene currentScene =
                player.scene;

            if (!IsValidTargetScene(targetScene))
            {
                Debug.LogWarning(
                    "DoorTeleport ignored invalid targetScene: " +
                    targetScene);
                yield break;
            }

            if (!currentScene.IsValid() ||
                !currentScene.isLoaded)
            {
                Debug.LogWarning(
                    "DoorTeleport could not resolve the player's current scene before loading " +
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

            if (player.transform.parent != null)
            {
                Debug.LogWarning(
                    "DoorTeleport requires the player root to be unparented before cross-scene teleport.",
                    player);
                StartCoroutine(
                    CleanupLateLoadedScene(
                        targetScene,
                        loadOperation));
                yield return FadeBackIfNeeded(shouldFadeBackIn);
                yield break;
            }

            DontDestroyOnLoad(player);

            bool loadCompletedInTime = false;
            yield return WaitForOperationOrTimeout(
                loadOperation,
                targetScene,
                "loading",
                result => loadCompletedInTime = result);

            if (!loadCompletedInTime)
            {
                StartCoroutine(
                    CleanupLateLoadedScene(
                        targetScene,
                        loadOperation));
                RestorePlayerToScene(player, currentScene);
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
                RestorePlayerToScene(player, currentScene);
                StartCoroutine(
                    CleanupLateLoadedScene(
                        targetScene,
                        null));
                yield return FadeBackIfNeeded(shouldFadeBackIn);
                yield break;
            }

            if (!SceneManager.SetActiveScene(newScene))
            {
                Debug.LogWarning(
                    "DoorTeleport could not set active scene to " +
                    targetScene);
                RestorePlayerToScene(player, currentScene);
                StartCoroutine(
                    CleanupLateLoadedScene(
                        targetScene,
                        null));
                yield return FadeBackIfNeeded(shouldFadeBackIn);
                yield break;
            }

            MovePlayerToScene(player, newScene);
            player.transform.position =
                spawnPosition;

            yield return null;

            FullGameSaveController.EnsureInstance().SaveFullGame(true);

            AsyncOperation unloadOperation = null;

            if (currentScene != newScene &&
                currentScene.IsValid() &&
                currentScene.isLoaded)
            {
                unloadOperation =
                    SceneManager.UnloadSceneAsync(
                        currentScene);
            }

            if (unloadOperation == null)
            {
                if (currentScene != newScene &&
                    currentScene.IsValid() &&
                    currentScene.isLoaded)
                {
                    Debug.LogWarning(
                        "DoorTeleport could not unload previous scene " +
                        currentScene.name);
                }
            }
            else
            {
                bool unloadCompletedInTime = false;
                yield return WaitForOperationOrTimeout(
                    unloadOperation,
                    currentScene.name,
                    "unloading",
                    result => unloadCompletedInTime = result);

                if (!unloadCompletedInTime)
                {
                    Debug.LogWarning(
                        "DoorTeleport will keep the previous scene loaded because unloading timed out: " +
                        currentScene.name);
                }
            }

            yield return FadeBackIfNeeded(shouldFadeBackIn);
        }
        finally
        {
            isLoading = false;
        }
    }

    System.Collections.IEnumerator
        WaitForOperationOrTimeout(
            AsyncOperation operation,
            string sceneName,
            string action,
            System.Action<bool> onCompleted)
    {
        if (operation == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        float timeoutAt =
            Time.unscaledTime + Mathf.Max(1f, loadTimeoutSeconds);

        while (!operation.isDone)
        {
            if (Time.unscaledTime >= timeoutAt)
            {
                Debug.LogWarning(
                    "DoorTeleport timed out while " +
                    action +
                    " scene " +
                    sceneName);
                onCompleted?.Invoke(false);
                yield break;
            }

            yield return null;
        }

        onCompleted?.Invoke(true);
    }

    System.Collections.IEnumerator
        CleanupLateLoadedScene(
            string sceneName,
            AsyncOperation loadOperation)
    {
        if (loadOperation != null)
        {
            float cleanupTimeoutAt =
                Time.unscaledTime + Mathf.Max(1f, loadTimeoutSeconds);

            while (!loadOperation.isDone &&
                Time.unscaledTime < cleanupTimeoutAt)
            {
                yield return null;
            }
        }

        Scene loadedScene =
            SceneManager.GetSceneByName(sceneName);

        if (!loadedScene.IsValid() ||
            !loadedScene.isLoaded)
        {
            yield break;
        }

        AsyncOperation unloadOperation =
            SceneManager.UnloadSceneAsync(loadedScene);

        if (unloadOperation == null)
        {
            Debug.LogWarning(
                "DoorTeleport cleanup could not unload late-loaded scene " +
                sceneName);
            yield break;
        }

        while (!unloadOperation.isDone)
        {
            yield return null;
        }
    }

    void RestorePlayerToScene(
        GameObject player,
        Scene scene)
    {
        if (player == null ||
            !scene.IsValid() ||
            !scene.isLoaded)
        {
            return;
        }

        MovePlayerToScene(player, scene);
    }

    void MovePlayerToScene(
        GameObject player,
        Scene scene)
    {
        if (player == null ||
            !scene.IsValid() ||
            !scene.isLoaded ||
            player.scene == scene)
        {
            return;
        }

        SceneManager.MoveGameObjectToScene(
            player,
            scene);
    }

    GameObject ResolvePlayerRoot(GameObject actor)
    {
        if (actor == null)
        {
            return null;
        }

        Rigidbody2D rb =
            actor.GetComponentInParent<Rigidbody2D>();
        if (rb != null)
        {
            return rb.gameObject;
        }

        Transform root =
            actor.transform.root;
        return root != null ? root.gameObject : actor;
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
