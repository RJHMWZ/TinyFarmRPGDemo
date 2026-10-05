using UnityEngine;
using UnityEngine.UI;

/// <summary>Binds behaviour to the already-authored main-menu presentation.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuPresentation : MonoBehaviour
{
    private Button settingsButton;
    private bool initialized;

    public void Initialize(MainMenuView view)
    {
        if (initialized || view == null) return;
        initialized = true;
        settingsButton = view.SettingsButton;
        settingsButton.interactable = true;
        settingsButton.onClick.AddListener(OpenSettings);
    }

    private void OpenSettings()
    {
        GameRoot root = GameRoot.Instance;
        if (root != null && !root.UI.IsOpen(SettingsPanel.PanelId)) root.UI.Open(SettingsPanel.PanelId);
    }

    private void OnDestroy()
    {
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
    }
}
