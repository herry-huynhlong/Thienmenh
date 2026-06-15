using UnityEngine;

public class AuraRotate : MonoBehaviour
{
    public float speed = 30f;

    void Update()
    {
        transform.Rotate(0, 0, speed * Time.deltaTime);
    }
}