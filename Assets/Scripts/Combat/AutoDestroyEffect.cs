using UnityEngine;

public class AutoDestroyEffect : MonoBehaviour
{
    public float destroyAfter = 0.5f;

    private void Start()
    {
        Destroy(gameObject, destroyAfter);
    }
}