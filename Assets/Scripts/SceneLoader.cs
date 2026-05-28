using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    IEnumerator Start()
    {
        Scene langScene = SceneManager.GetSceneByName("Lang");

        if (!langScene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                "Lang",
                LoadSceneMode.Additive);

            if (loadOperation != null)
            {
                yield return loadOperation;
            }

            langScene = SceneManager.GetSceneByName("Lang");
        }

        if (langScene.IsValid() && langScene.isLoaded)
        {
            SceneManager.SetActiveScene(langScene);
        }
    }
}