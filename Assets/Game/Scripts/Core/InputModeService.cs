using System;
using UnityEngine;

public enum GameInputMode
{
    Gameplay,
    UserInterface,
    Dialogue,
    Disabled
}

/// <summary>Single source of truth for whether gameplay or UI currently owns input.</summary>
public sealed class InputModeService : MonoBehaviour
{
    public GameInputMode CurrentMode { get; private set; } = GameInputMode.Disabled;
    public event Action<GameInputMode> ModeChanged;

    private void OnEnable() => ApplyCursorState();

    public void SetMode(GameInputMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        ApplyCursorState();
        ModeChanged?.Invoke(mode);
    }

    private void ApplyCursorState() => Cursor.visible = CurrentMode != GameInputMode.Gameplay;
}
