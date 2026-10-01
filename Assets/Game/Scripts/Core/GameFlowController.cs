using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Coordinates top-level flows without allowing UI panels to control scene objects.</summary>
public sealed class GameFlowController : MonoBehaviour
{
    private bool busy;

    private IEnumerator Start()
    {
        yield return null;
        if (SceneManager.GetActiveScene().name == GameSceneNames.Boot)
            yield return TransitionTo(GameSceneNames.MainMenu, GameInputMode.UserInterface);
        else
            yield return GameRoot.Instance.Transitions.FadeFromBlack(0.15f);
    }

    public void SelectNewGameSlot(int slotIndex)
    {
        GameRoot.Instance.Session.SelectSlot(slotIndex);
        CharacterCreationSave.SetActiveSlot(slotIndex);
    }

    public void StartNewGame(CharacterCreationProfile profile)
    {
        if (busy || profile == null) return;
        int slot = GameRoot.Instance.Session.ActiveSlot;
        GameSaveData data = GameSaveData.CreateNew(profile);
        if (!GameRoot.Instance.Saves.Save(slot, data)) return;
        GameRoot.Instance.Session.SetLoadedGame(slot, data);
        StartCoroutine(TransitionTo(GameSceneNames.Gameplay, GameInputMode.Gameplay));
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
        CharacterCreationSave.SetActiveSlot(slotIndex);
        string targetScene = string.IsNullOrWhiteSpace(data.world.currentScene) ? GameSceneNames.Gameplay : data.world.currentScene;
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
        busy = true;
        GameRoot.Instance.InputModes.SetMode(GameInputMode.Disabled);
        yield return GameRoot.Instance.Transitions.FadeToBlack();
        yield return GameRoot.Instance.Scenes.LoadSingle(sceneName);
        yield return null;
        GameRoot.Instance.InputModes.SetMode(inputMode);
        yield return GameRoot.Instance.Transitions.FadeFromBlack();
        busy = false;
    }
}
