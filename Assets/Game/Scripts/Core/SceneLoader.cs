using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneLoader : MonoBehaviour
{
    public bool CanLoad(string sceneName) =>
        !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

    public IEnumerator LoadSingle(string sceneName)
    {
        if (!CanLoad(sceneName))
        {
            Debug.LogError("Scene is missing from Build Settings: " + sceneName, this);
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            Debug.LogError("Unity could not start loading scene: " + sceneName, this);
            yield break;
        }
        while (!operation.isDone) yield return null;
    }
}
