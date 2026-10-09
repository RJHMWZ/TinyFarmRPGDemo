using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class FarmLoopPlayModeTests
{
    private GameRoot root;
    private string saveDirectory;
    private SaveService previousStorage;
    private GameSettings preferences;
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync(GameSceneNames.MainMenu);
        yield return null;
        root = GameRoot.Instance;
        preferences = root.Settings.Snapshot;
        var settings = preferences.Copy(); settings.pauseWhenUnfocused = false; settings.worldZoom = 1; settings.uiScale = 1;
        root.Settings.Preview(settings);
        previousStorage = root.Saves;
        saveDirectory = Path.Combine(Path.GetTempPath(), "TinyFarmSceneTests", Guid.NewGuid().ToString("N"));
        typeof(GameRoot).GetField("<Saves>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, new SaveService(saveDirectory));
        var profile = new CharacterCreationProfile { playerName = "Scene Farmer" };
        var catalog = Resources.FindObjectsOfTypeAll<GameDataCatalog>().First();
        var options = new System.Collections.Generic.List<CharacterAppearanceDatabase.Entry>();
        foreach (var category in new[] { PlayerAppearanceCategory.Skin, PlayerAppearanceCategory.Clothes, PlayerAppearanceCategory.Eyes, PlayerAppearanceCategory.Hair })
        {
            catalog.CharacterAppearances.GetEntries(category, PlayerGender.Male, options);
            if (options.Count > 0) profile.SetPartId(category, options[0].Id);
        }
        root.Session.SetLoadedGame(0, GameSaveData.CreateNew(profile));
        yield return SceneManager.LoadSceneAsync(GameSceneNames.Gameplay);
        yield return null;
        root.InputModes.SetMode(GameInputMode.Gameplay);
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null)
        {
            root.UI.Close(FarmHubPanel.PanelId);
            root.UI.Close(SettingsPanel.PanelId);
            root.Settings.Preview(preferences);
            root.Session.ClearLoadedGame();
        }
        yield return SceneManager.LoadSceneAsync(GameSceneNames.MainMenu);
        yield return null;
        if (root != null) typeof(GameRoot).GetField("<Saves>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, previousStorage);
        if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
    }
    [UnityTest]
    public IEnumerator World_HasFarmStationsAndRuntimeClock()
    {
        var farm = FarmRuntime.Active;
        Assert.That(farm, Is.Not.Null);
        Assert.That(farm.World.Targets.Count(x => x.Kind == "plot"), Is.EqualTo(40));
        foreach (string kind in new[] { "shop", "shipping", "storage", "sleep", "crafting", "villager", "wood", "stone", "berry" })
            Assert.That(farm.World.Targets.Any(x => x.Kind == kind), Is.True, kind);
        Capture("farm-world.png", 1600, 900);
        var panel = (FarmHubPanel)root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = "journal" });
        yield return null;
        Assert.That(root.Pause.IsPaused, Is.True);
        Assert.That(root.InputModes.CurrentMode, Is.EqualTo(GameInputMode.UserInterface));
        int minute = farm.Game.Save.world.minute;
        yield return null;
        Assert.That(farm.Game.Save.world.minute, Is.EqualTo(minute));
        Capture("farm-journal.png", 1600, 900);
        root.UI.Close(FarmHubPanel.PanelId);
        Assert.That(root.Pause.IsPaused, Is.False);
        Assert.That(root.InputModes.CurrentMode, Is.EqualTo(GameInputMode.Gameplay));
    }
    [UnityTest]
    public IEnumerator Panels_ExecuteRealTransactionsAndReleasePause()
    {
        var game = FarmRuntime.Active.Game;
        var panel = (FarmHubPanel)root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = "shop" });
        yield return null;
        int before = game.Save.player.money;
        Click(panel, "Row0");
        Assert.That(game.Save.player.money, Is.LessThan(before));
        Capture("farm-shop.png", 1280, 720);
        root.UI.Close(FarmHubPanel.PanelId);
        panel = (FarmHubPanel)root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = "storage" });
        yield return null;
        Click(panel, "Slot4"); Click(panel, "Action1");
        Assert.That(game.Storage.Count("parsnip-seed"), Is.EqualTo(15));
        Assert.That(game.Inventory.Count("parsnip-seed"), Is.Zero);
        Click(panel, "Action0"); Click(panel, "Slot0"); Click(panel, "Action1");
        Assert.That(game.Inventory.Count("parsnip-seed"), Is.EqualTo(15));
        root.UI.Close(FarmHubPanel.PanelId);
        panel = (FarmHubPanel)root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = "backpack" });
        yield return null;
        Click(panel, "Slot4"); Click(panel, "Slot5");
        Assert.That(game.Save.inventory.slots[5].itemId, Is.EqualTo("parsnip-seed"), "Two clicks should pick up and place a stack without a separate Move command.");
        Capture("farm-backpack-4x3.png", 1024, 768);
        root.UI.Close(FarmHubPanel.PanelId);
        Assert.That(root.Pause.IsPaused, Is.False);
    }
    [UnityTest]
    public IEnumerator Sleeping_PersistsFarmAndReloadsSceneState()
    {
        var game = FarmRuntime.Active.Game;
        game.Execute(() => game.Farming.UsePlot(0, 0));
        game.Execute(() => game.Farming.UsePlot(0, 4));
        game.Execute(() => game.Farming.UsePlot(0, 1));
        var panel = (FarmHubPanel)root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = "sleep" });
        yield return null;
        Click(panel, "Row0");
        Assert.That(root.Saves.TryLoad(0, out var loaded), Is.True);
        Assert.That(loaded.farm.plots[0].growth, Is.EqualTo(1));
        Assert.That(loaded.world.day, Is.EqualTo(2));
        root.Session.SetLoadedGame(0, loaded);
        yield return SceneManager.LoadSceneAsync(GameSceneNames.Gameplay);
        yield return null;
        Assert.That(FarmRuntime.Active.Game.Save.farm.plots[0].seedId, Is.EqualTo("parsnip-seed"));
        Assert.That(FarmRuntime.Active.Game.Save.farm.plots[0].growth, Is.EqualTo(1));
    }
    private static void Click(FarmHubPanel panel, string name) => panel.GetComponentsInChildren<Button>(true).First(x => x.name == name).onClick.Invoke();
    private static void Capture(string filename, int width, int height)
    {
        string directory = Environment.GetEnvironmentVariable("TINYFARM_FARM_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Camera camera = Camera.main;
        Canvas canvas = UIRoot.Active.GetComponent<Canvas>();
        var mode = canvas.renderMode; var previousCamera = canvas.worldCamera; float distance = canvas.planeDistance; int sorting = canvas.sortingOrder;
        var target = new RenderTexture(width, height, 24);
        var previousTarget = camera.targetTexture; var active = RenderTexture.active;
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        canvas.sortingOrder = 2000;
        Canvas.ForceUpdateCanvases();
        // Match the panel's Update fit calculation after changing the capture aspect ratio.
        FarmHubPanel panel = Object.FindObjectOfType<FarmHubPanel>();
        if (panel != null) panel.SendMessage("Update");
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG());
        canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = distance; canvas.sortingOrder = sorting;
        camera.targetTexture = previousTarget; RenderTexture.active = active;
        Object.Destroy(texture); Object.Destroy(target);
    }
}
