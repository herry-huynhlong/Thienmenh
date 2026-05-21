using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    public float lifeTime = 0.6f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }
}