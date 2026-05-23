using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTeleport : MonoBehaviour
{
    [Header("Scene")]
    public string targetScene;

    [Header("Spawn")]
    public Vector2 spawnPosition;

    private void OnTriggerEnter2D(
        Collider2D other)
    {
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
        SceneManager.LoadScene(
            targetScene);

        yield return null;

        player.transform.position =
            spawnPosition;
    }
}