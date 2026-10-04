using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameSettingsTests
{
    private string previousJson;
    private bool hadSettings;

    [SetUp]
    public void SetUp()
    {
        hadSettings = PlayerPrefs.HasKey(SettingsService.PreferenceKey);
        previousJson = PlayerPrefs.GetString(SettingsService.PreferenceKey);
    }

    [TearDown]
    public void TearDown()
    {
        if (hadSettings) PlayerPrefs.SetString(SettingsService.PreferenceKey, previousJson);
        else PlayerPrefs.DeleteKey(SettingsService.PreferenceKey);
        PlayerPrefs.Save();
    }

    [Test]
    public void Load_NormalizesCorruptAndOutOfRangePreferences()
    {
        PlayerPrefs.SetString(SettingsService.PreferenceKey,
            "{\"masterVolume\":5,\"uiScale\":20,\"worldZoom\":0,\"windowMode\":99,\"frameRate\":13,\"resolutionWidth\":2,\"resolutionHeight\":3,\"moveUp\":0}");
        GameSettings settings = SettingsService.Load();
        Assert.That(settings.masterVolume, Is.EqualTo(1f));
        Assert.That(settings.uiScale, Is.EqualTo(1.2f));
        Assert.That(settings.worldZoom, Is.EqualTo(0.75f));
        Assert.That(settings.frameRate, Is.EqualTo(60));
        Assert.That(settings.resolutionWidth, Is.Zero);
        Assert.That(settings.moveUp, Is.EqualTo((int)Key.W));
    }

    [Test]
    public void Normalize_RejectsNonFiniteValuesAndDuplicateBindings()
    {
        var settings = new GameSettings { masterVolume = float.NaN, uiScale = float.PositiveInfinity, backpackKey = (int)Key.W };
        settings.Normalize();
        Assert.That(settings.masterVolume, Is.EqualTo(0.8f));
        Assert.That(settings.uiScale, Is.EqualTo(1f));
        Assert.That(settings.backpackKey, Is.EqualTo((int)Key.I));
    }

    [Test]
    public void Load_RoundTripsAllDevicePreferences()
    {
        var settings = new GameSettings
        {
            masterVolume = 0.35f, muteWhenUnfocused = false, windowMode = 0,
            resolutionWidth = 1280, resolutionHeight = 720, vSync = false, frameRate = 120,
            uiScale = 1.1f, worldZoom = 1.25f, clock24Hour = true, showControlHints = false,
            pauseWhenUnfocused = false, backpackKey = (int)Key.B
        };
        PlayerPrefs.SetString(SettingsService.PreferenceKey, JsonUtility.ToJson(settings));
        Assert.That(JsonUtility.ToJson(SettingsService.Load()), Is.EqualTo(JsonUtility.ToJson(settings)));
    }

    [Test]
    public void SnapshotCopy_DoesNotMutateOriginal()
    {
        var original = new GameSettings();
        GameSettings draft = original.Copy();
        draft.masterVolume = 0f;
        draft.windowMode = 0;
        Assert.That(original.masterVolume, Is.EqualTo(0.8f));
        Assert.That(draft.HasSameDisplay(original), Is.False);
    }
}
