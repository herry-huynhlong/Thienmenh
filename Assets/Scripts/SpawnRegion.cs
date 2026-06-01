using UnityEngine;

public class SpawnRegion : MonoBehaviour
{
    public Vector2 size = new Vector2(12f, 8f);

    public Vector3 RandomPoint()
    {
        Vector2 offset = new Vector2(
            Random.Range(-size.x * 0.5f, size.x * 0.5f),
            Random.Range(-size.y * 0.5f, size.y * 0.5f));

        return transform.position + (Vector3)offset;
    }

    void OnValidate()
    {
        size.x = Mathf.Max(0f, size.x);
        size.y = Mathf.Max(0f, size.y);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
