using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTeleport : MonoBehaviour
{
    [Header("Scene")]
    public string targetScene;

    [Header("Spawn")]
    public Vector2 spawnPosition;

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
        isLoading = true;

        FullGameSaveController.EnsureInstance().SaveFullGame(true);
        GameSaveSystem.SaveCurrentScene(targetScene);
        GameSaveSystem.FlushPendingCommit();

        Scene currentScene =
            SceneManager.GetActiveScene();

        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync(
                targetScene,
                LoadSceneMode.Additive);

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        Scene newScene =
            SceneManager.GetSceneByName(
                targetScene);

        SceneManager.SetActiveScene(
            newScene);

        player.transform.position =
            spawnPosition;

        yield return null;

        FullGameSaveController.EnsureInstance().SaveFullGame(true);

        SceneManager.UnloadSceneAsync(
            currentScene);

        isLoading = false;
    }
}
