using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneLoader : MonoBehaviour
{
    public IEnumerator LoadSingle(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            Debug.LogError("Scene is missing from Build Settings: " + sceneName, this);
            yield break;
        }
        while (!operation.isDone) yield return null;
    }
}
