using UnityEngine;

public class Fireball : MonoBehaviour
{
    public float speed = 8f;

    Vector2 direction;

    public void SetDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized;

        Destroy(gameObject, 5f);
    }

    void Update()
    {
        transform.Translate(
            direction *
            speed *
            Time.deltaTime);
    }
}