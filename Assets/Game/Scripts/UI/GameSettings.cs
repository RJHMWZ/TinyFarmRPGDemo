using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Device preferences, independent of character saves. Owned by SettingsService.</summary>
[Serializable]
public sealed class GameSettings
{
    public float masterVolume = 0.8f;
    public bool muteWhenUnfocused = true;
    public int windowMode = 1;
    public int resolutionWidth;
    public int resolutionHeight;
    public bool vSync = true;
    public int frameRate = 60;
    public float uiScale = 1f;
    public float worldZoom = 1f;
    public bool clock24Hour;
    public bool showControlHints = true;
    public bool pauseWhenUnfocused = true;
    public int moveUp = (int)Key.W;
    public int moveDown = (int)Key.S;
    public int moveLeft = (int)Key.A;
    public int moveRight = (int)Key.D;
    public int backpackKey = (int)Key.I;

    public GameSettings Copy() => (GameSettings)MemberwiseClone();

    public void Normalize()
    {
        masterVolume = ClampFinite(masterVolume, 0f, 1f, 0.8f);
        uiScale = ClampFinite(uiScale, 0.8f, 1.2f, 1f);
        worldZoom = ClampFinite(worldZoom, 0.75f, 1.5f, 1f);
        windowMode = Mathf.Clamp(windowMode, 0, 2);
        if (resolutionWidth < 640 || resolutionHeight < 480 || resolutionWidth > 16384 || resolutionHeight > 16384)
            resolutionWidth = resolutionHeight = 0;
        if (frameRate != 30 && frameRate != 60 && frameRate != 120 && frameRate != 144 && frameRate != -1)
            frameRate = 60;
        int[] keys = { moveUp, moveDown, moveLeft, moveRight, backpackKey };
        bool valid = true;
        for (int i = 0; i < keys.Length; i++)
        {
            valid &= IsBindableKey((Key)keys[i]);
            for (int j = 0; j < i; j++) valid &= keys[i] != keys[j];
        }
        if (!valid)
        {
            moveUp = (int)Key.W;
            moveDown = (int)Key.S;
            moveLeft = (int)Key.A;
            moveRight = (int)Key.D;
            backpackKey = (int)Key.I;
        }
    }

    // Arrow keys and Escape remain available for navigation and recovery.
    public static bool IsBindableKey(Key key) => key >= Key.A && key <= Key.Z;

    public bool HasSameDisplay(GameSettings other) => other != null && windowMode == other.windowMode &&
        resolutionWidth == other.resolutionWidth && resolutionHeight == other.resolutionHeight;

    private static float ClampFinite(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
}
