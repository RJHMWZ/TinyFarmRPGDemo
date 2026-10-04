using System;
using UnityEngine;

/// <summary>Owns preferences and runtime previews. Only Commit writes device settings to disk.</summary>
[DisallowMultipleComponent]
public sealed class SettingsService : MonoBehaviour
{
    public const string PreferenceKey = "settings.device.v1";
    private GameSettings current;
    private bool focused = true;
    public event Action Changed;
    public GameSettings Snapshot => current.Copy();

    private void Awake()
    {
        current = Load();
        ApplyRuntime();
        ApplyDisplay(current);
    }

    public static GameSettings Load()
    {
        GameSettings data = null;
        if (PlayerPrefs.HasKey(PreferenceKey))
        {
            try { data = JsonUtility.FromJson<GameSettings>(PlayerPrefs.GetString(PreferenceKey)); }
            catch (ArgumentException) { Debug.LogWarning("Device settings were invalid; using defaults."); }
        }
        if (data == null)
        {
            data = new GameSettings
            {
                masterVolume = PlayerPrefs.GetFloat("settings.master-volume", 0.8f),
                windowMode = PlayerPrefs.GetInt("settings.fullscreen", 1) != 0 ? 1 : 0,
                vSync = PlayerPrefs.GetInt("settings.vsync", 1) != 0
            };
        }
        data.Normalize();
        return data;
    }

    public void Preview(GameSettings data)
    {
        current = data.Copy();
        current.Normalize();
        ApplyRuntime();
        Changed?.Invoke();
    }

    public void Commit(GameSettings data)
    {
        Preview(data);
        PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(current));
        PlayerPrefs.Save();
    }

    public void ApplyDisplay(GameSettings data)
    {
        if (Application.isEditor) return;
        FullScreenMode mode = data.windowMode == 0 ? FullScreenMode.Windowed :
            data.windowMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.ExclusiveFullScreen;
        int width = data.resolutionWidth > 0 ? data.resolutionWidth : Screen.currentResolution.width;
        int height = data.resolutionHeight > 0 ? data.resolutionHeight : Screen.currentResolution.height;
        Screen.SetResolution(width, height, mode);
    }

    private void ApplyRuntime()
    {
        AudioListener.volume = !focused && current.muteWhenUnfocused ? 0f : current.masterVolume;
        QualitySettings.vSyncCount = current.vSync ? 1 : 0;
        Application.targetFrameRate = current.vSync ? -1 : current.frameRate;
        RefreshFocusPause();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        if (current != null) ApplyRuntime();
    }

    private void Update() => RefreshFocusPause();

    private void RefreshFocusPause()
    {
        GameRoot root = GameRoot.Instance;
        if (root != null && root.Pause != null)
            root.Pause.SetPaused(this, !focused && current.pauseWhenUnfocused && root.Session.CurrentSave != null);
    }

    private void OnDestroy()
    {
        if (GameRoot.Instance != null && GameRoot.Instance.Pause != null)
            GameRoot.Instance.Pause.SetPaused(this, false);
    }
}
