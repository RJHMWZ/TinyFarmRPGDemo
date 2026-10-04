using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class FarmSystemsPlayModeTests
{
    [UnityTest]
    public IEnumerator MainMenu_CreatesBackdropAndEnablesSettings()
    {
        yield return SceneManager.LoadSceneAsync(GameSceneNames.MainMenu, LoadSceneMode.Single);
        yield return null;

        GameObject backdrop = GameObject.Find("MainMenuBackdrop");
        Assert.That(backdrop, Is.Not.Null);
        Assert.That(backdrop.GetComponent<RawImage>().texture, Is.Not.Null);

        MainMenuView menu = Object.FindObjectOfType<MainMenuView>();
        Assert.That(menu, Is.Not.Null);
        Assert.That(menu.SettingsButton.interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator Gameplay_CreatesClockHotbarAndBackpackButton()
    {
        GameRoot root = GameRoot.Instance;
        if (root == null)
        {
            root = new GameObject("GameRoot-Test").AddComponent<GameRoot>();
            yield return null;
        }
        GameSaveData save = GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Test Farmer" });
        root.Session.SetLoadedGame(0, save);

        yield return SceneManager.LoadSceneAsync(GameSceneNames.Gameplay, LoadSceneMode.Single);
        yield return null;

        Assert.That(Object.FindObjectOfType<GameplayHudController>(), Is.Not.Null);
        Assert.That(GameObject.Find("ClockCard"), Is.Not.Null);
        Assert.That(GameObject.Find("Hotbar"), Is.Not.Null);
        Assert.That(GameObject.Find("BackpackButton"), Is.Not.Null);
    }
}
