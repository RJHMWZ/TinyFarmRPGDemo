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

    private void ApplyCursorState()
    {
        // The farming loop is pointer-driven on desktop: the player aims at plots, stations and
        // the hotbar while still moving with the keyboard. Hiding the cursor in Gameplay made the
        // mouse appear broken even though pointer input was still being processed.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
