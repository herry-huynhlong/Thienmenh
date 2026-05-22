using UnityEngine;

public class TestClick : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Đã click màn hình");
        }
    }
}