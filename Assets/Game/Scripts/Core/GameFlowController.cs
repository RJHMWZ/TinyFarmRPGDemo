using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Coordinates top-level flows without allowing UI panels to control scene objects.</summary>
public sealed class GameFlowController : MonoBehaviour
{
    private bool busy;

    private IEnumerator Start()
    {
        if (GameRoot.Instance == null)
        {
            Debug.LogError("GameRoot is unavailable; the top-level game flow cannot start.", this);
            yield break;
        }

        yield return null;
        if (SceneManager.GetActiveScene().name == GameSceneNames.Boot)
            yield return TransitionTo(GameSceneNames.MainMenu, GameInputMode.UserInterface);
        else
            yield return GameRoot.Instance.Transitions.FadeFromBlack(0.15f);
    }

    public void SelectNewGameSlot(int slotIndex)
    {
        GameRoot.Instance.Session.SelectSlot(slotIndex);
    }

    /// <summary>Persists the new profile before entering gameplay. Returns false when the request was rejected.</summary>
    public bool StartNewGame(CharacterCreationProfile profile)
    {
        if (busy || profile == null) return false;
        int slot = GameRoot.Instance.Session.ActiveSlot;
        GameSaveData data = GameSaveData.CreateNew(profile);
        if (!GameRoot.Instance.Saves.Save(slot, data)) return false;
        GameRoot.Instance.Session.SetLoadedGame(slot, data);
        StartCoroutine(TransitionTo(GameSceneNames.Gameplay, GameInputMode.Gameplay));
        return true;
    }

    public void LoadGame(int slotIndex)
    {
        if (busy) return;
        if (!GameRoot.Instance.Saves.TryLoad(slotIndex, out GameSaveData data))
        {
            Debug.LogWarning("Cannot load missing or damaged save slot " + (slotIndex + 1), this);
            return;
        }
        GameRoot.Instance.Session.SetLoadedGame(slotIndex, data);
        string targetScene = string.IsNullOrWhiteSpace(data.world.currentScene) ? GameSceneNames.Gameplay : data.world.currentScene;
        if (!GameRoot.Instance.Scenes.CanLoad(targetScene))
        {
            Debug.LogWarning("Saved scene is unavailable; loading the default gameplay scene instead: " + targetScene, this);
            targetScene = GameSceneNames.Gameplay;
        }
        StartCoroutine(TransitionTo(targetScene, GameInputMode.Gameplay));
    }

    public void ReturnToMainMenu()
    {
        if (busy) return;
        GameRoot.Instance.Session.ClearLoadedGame();
        StartCoroutine(TransitionTo(GameSceneNames.MainMenu, GameInputMode.UserInterface));
    }

    private IEnumerator TransitionTo(string sceneName, GameInputMode inputMode)
    {
        if (busy) yield break;
        if (!GameRoot.Instance.Scenes.CanLoad(sceneName))
        {
            Debug.LogError("Cannot transition to a scene that is not in Build Settings: " + sceneName, this);
            yield break;
        }

        busy = true;
        try
        {
            GameRoot.Instance.InputModes.SetMode(GameInputMode.Disabled);
            yield return GameRoot.Instance.Transitions.FadeToBlack();
            yield return GameRoot.Instance.Scenes.LoadSingle(sceneName);
            yield return null;
            GameRoot.Instance.InputModes.SetMode(inputMode);
            yield return GameRoot.Instance.Transitions.FadeFromBlack();
        }
        finally
        {
            busy = false;
        }
    }
}
