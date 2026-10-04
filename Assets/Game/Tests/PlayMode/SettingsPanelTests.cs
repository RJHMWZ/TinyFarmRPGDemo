using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class SettingsPanelTests
{
    private GameSettings before;
    private string previousJson;
    private bool hadSettings;
    private Keyboard testKeyboard;
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
    private InputSettings.BackgroundBehavior previousBackgroundInput;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        hadSettings = PlayerPrefs.HasKey(SettingsService.PreferenceKey);
        previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        previousBackgroundInput = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        previousJson = PlayerPrefs.GetString(SettingsService.PreferenceKey);
        if (GameRoot.Instance != null)
        {
            Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
        }
        yield return SceneManager.LoadSceneAsync(GameSceneNames.MainMenu);
        yield return null;
        before = GameRoot.Instance.Settings.Snapshot;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        GameRoot root = GameRoot.Instance;
        root.UI.Close(SettingsPanel.PanelId);
        root.Settings.Preview(before);
        if (testKeyboard != null) { InputSystem.RemoveDevice(testKeyboard); testKeyboard = null; }
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        InputSystem.settings.backgroundBehavior = previousBackgroundInput;
        if (hadSettings) PlayerPrefs.SetString(SettingsService.PreferenceKey, previousJson);
        else PlayerPrefs.DeleteKey(SettingsService.PreferenceKey);
        PlayerPrefs.Save();
        root.Session.ClearLoadedGame();
        Scene empty = SceneManager.CreateScene("SettingsTestCleanup");
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(previous);
        Object.Destroy(root.gameObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Preview_BackRollsBack_ApplyPersistsAcrossScenes()
    {
        MainMenuView menu = Object.FindObjectOfType<MainMenuView>();
        menu.SettingsButton.onClick.Invoke();
        yield return null;
        SettingsPanel panel = Object.FindObjectOfType<SettingsPanel>();
        Assert.That(panel, Is.Not.Null);
        CapturePanel(panel, "settings-title-display.png", 1920, 1080);
        Click(panel, "TabAUDIO");
        CapturePanel(panel, "settings-title-audio.png", 1280, 720);
        Slider volume = panel.GetComponentsInChildren<Slider>(true).First(s => s.transform.parent.name == "MasterVolume");
        float newVolume = before.masterVolume > 0.5f ? 25f : 75f;
        volume.value = newVolume;
        Assert.That(AudioListener.volume, Is.EqualTo(newVolume / 100f).Within(0.01f));
        Click(panel, "Back");
        Assert.That(GameRoot.Instance.Settings.Snapshot.masterVolume, Is.EqualTo(before.masterVolume));

        menu.SettingsButton.onClick.Invoke();
        yield return null;
        Click(panel, "TabAUDIO");
        volume.value = newVolume;
        Click(panel, "Apply");
        Click(panel, "Back");
        Assert.That(SettingsService.Load().masterVolume, Is.EqualTo(newVolume / 100f).Within(0.01f));

        GameRoot root = GameRoot.Instance;
        root.Session.SetLoadedGame(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Settings Test" }));
        yield return SceneManager.LoadSceneAsync(GameSceneNames.Gameplay);
        yield return null;
        root.InputModes.SetMode(GameInputMode.Gameplay);
        Object.FindObjectsOfType<Button>().First(b => b.name == "MenuButton").onClick.Invoke();
        Object.FindObjectsOfType<Button>().First(b => b.name == "Settings").onClick.Invoke();
        yield return null;
        panel = Object.FindObjectOfType<SettingsPanel>();
        Assert.That(root.Settings.Snapshot.masterVolume, Is.EqualTo(newVolume / 100f).Within(0.01f));
        Assert.That(root.Pause.IsPaused, Is.True);
        Click(panel, "Back");
        Assert.That(root.Pause.IsPaused, Is.True, "The underlying farm menu still owns a pause request.");
        Object.FindObjectsOfType<Button>().First(b => b.name == "Resume").onClick.Invoke();
        Assert.That(root.Pause.IsPaused, Is.False);
        Assert.That(root.InputModes.CurrentMode, Is.EqualTo(GameInputMode.Gameplay));
    }

    [UnityTest]
    public IEnumerator DisplayChange_RevertRestoresOriginalWithoutSaving()
    {
        SettingsPanel panel = (SettingsPanel)GameRoot.Instance.UI.Open(SettingsPanel.PanelId);
        yield return null;
        panel.transform.Find("Card/DISPLAYPage/WindowMode/Next").GetComponent<Button>().onClick.Invoke();
        Click(panel, "Apply");
        Assert.That(panel.transform.Find("DisplayConfirmation").gameObject.activeSelf, Is.True);
        Click(panel, "RevertDisplay");
        Assert.That(GameRoot.Instance.Settings.Snapshot.windowMode, Is.EqualTo(before.windowMode));
        Assert.That(PlayerPrefs.GetString(SettingsService.PreferenceKey), Is.EqualTo(previousJson));
    }

    [UnityTest]
    public IEnumerator DisplayChange_TimesOutWhilePaused()
    {
        SettingsPanel panel = (SettingsPanel)GameRoot.Instance.UI.Open(SettingsPanel.PanelId);
        yield return null;
        panel.transform.Find("Card/DISPLAYPage/WindowMode/Next").GetComponent<Button>().onClick.Invoke();
        Click(panel, "Apply");
        Assert.That(Time.timeScale, Is.Zero);
        yield return new WaitForSecondsRealtime(15.2f);
        Assert.That(panel.transform.Find("DisplayConfirmation").gameObject.activeSelf, Is.False);
        Assert.That(GameRoot.Instance.Settings.Snapshot.windowMode, Is.EqualTo(before.windowMode));
    }

    [UnityTest]
    public IEnumerator GameplayPreferences_PreviewAndRollbackAffectHudAndCamera()
    {
        GameRoot root = GameRoot.Instance;
        root.Session.SetLoadedGame(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Zoom Test" }));
        yield return SceneManager.LoadSceneAsync(GameSceneNames.Gameplay);
        yield return null;
        Camera camera = Camera.main;
        float originalSize = camera.orthographicSize;
        Vector3 originalScale = GameObject.Find("Hotbar").transform.localScale;
        SettingsPanel panel = (SettingsPanel)root.UI.Open(SettingsPanel.PanelId);
        yield return null;
        Click(panel, "TabGAMEPLAY");
        panel.GetComponentsInChildren<Slider>(true).First(s => s.transform.parent.name == "WorldZoom").value = 150f;
        panel.GetComponentsInChildren<Slider>(true).First(s => s.transform.parent.name == "UiScale").value = 120f;
        Assert.That(camera.orthographicSize, Is.EqualTo(5f / 1.5f).Within(0.001f));
        Assert.That(GameObject.Find("Hotbar").transform.localScale.x, Is.EqualTo(1.2f));
        CapturePanel(panel, "settings-gameplay.png", 1920, 1080);
        Click(panel, "TabCONTROLS");
        CapturePanel(panel, "settings-controls-4x3.png", 1024, 768);
        Click(panel, "Back");
        Assert.That(camera.orthographicSize, Is.EqualTo(originalSize).Within(0.001f));
        Assert.That(GameObject.Find("Hotbar").transform.localScale, Is.EqualTo(originalScale));
    }

    private static void CapturePanel(SettingsPanel panel, string filename, int width, int height)
    {
        string directory = System.Environment.GetEnvironmentVariable("TINYFARM_SETTINGS_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        var go = new GameObject("SettingsPreviewCamera", typeof(Camera));
        Camera camera = go.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(30, 45, 37, 255);
        camera.cullingMask = 1 << 5;
        camera.orthographic = true;
        camera.orthographicSize = height / 2f;
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previousTarget = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, filename), texture.EncodeToPNG());
        RenderTexture.active = previousTarget;
        canvas.renderMode = previousMode;
        canvas.worldCamera = previousCamera;
        canvas.planeDistance = previousDistance;
        camera.targetTexture = null;
        Object.Destroy(texture);
        Object.Destroy(target);
        Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Rebind_RejectsConflictAndMovesWithNewKey()
    {
        GameRoot root = GameRoot.Instance;
        root.Settings.Preview(new GameSettings());
        root.Session.SetLoadedGame(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Input Test" }));
        yield return SceneManager.LoadSceneAsync(GameSceneNames.Gameplay);
        yield return null;
        root.InputModes.SetMode(GameInputMode.Gameplay);
        testKeyboard = InputSystem.AddDevice<Keyboard>();
        testKeyboard.MakeCurrent();
        SettingsPanel panel = (SettingsPanel)root.UI.Open(SettingsPanel.PanelId);
        yield return null;
        Click(panel, "TabCONTROLS");
        panel.transform.Find("Card/CONTROLSPage/Binding0/Next").GetComponent<Button>().onClick.Invoke();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.S));
        yield return null;
        Assert.That(root.Settings.Snapshot.moveUp, Is.EqualTo((int)Key.W));
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        yield return null;
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.K));
        yield return null;
        yield return null;
        Assert.That(root.Settings.Snapshot.moveUp, Is.EqualTo((int)Key.K));
        Click(panel, "Apply");
        Click(panel, "Back");
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        yield return null;
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.K));
        yield return null;
        yield return null;
        Assert.That(Object.FindObjectOfType<PlayerMovement>().MoveInput.y, Is.GreaterThan(0.9f));
    }

    private static void Click(SettingsPanel panel, string name) =>
        panel.GetComponentsInChildren<Button>(true).First(button => button.name == name).onClick.Invoke();
}
