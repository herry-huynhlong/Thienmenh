using UnityEngine;
using UnityEngine.SceneManagement;

public class toi_bentau : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Da cham");

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player cham cua");
            SceneManager.LoadScene("BenTau");
        }
    }
}