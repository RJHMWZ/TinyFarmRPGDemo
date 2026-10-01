using UnityEngine;
using UnityEngine.UI;

/// <summary>Serialized references owned by the title menu prefab.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuView : MonoBehaviour
{
    [SerializeField] private Button createGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    public Button CreateGameButton => createGameButton;
    public Button LoadGameButton => loadGameButton;
    public Button SettingsButton => settingsButton;
    public Button ExitButton => exitButton;

    public bool IsConfigured =>
        createGameButton != null && loadGameButton != null &&
        settingsButton != null && exitButton != null;
}
