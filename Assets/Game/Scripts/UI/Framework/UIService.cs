using System.Collections.Generic;
using UnityEngine;

/// <summary>Loads panels, assigns layers, and maintains the UI back stack.</summary>
public sealed class UIService : MonoBehaviour
{
    [SerializeField] private UIPanelCatalog catalog;
    private readonly Dictionary<string, UIPanel> instances = new Dictionary<string, UIPanel>();
    private readonly List<string> stack = new List<string>();
    private UIRoot root;
    private GameInputMode inputModeBeforeUi;
    private bool ownsInputMode;

    private void OnEnable()
    {
        UIRoot.ActiveChanged += OnRootChanged;
        ResolveRoot();
    }

    private void OnDisable()
    {
        UIRoot.ActiveChanged -= OnRootChanged;
        SetUiPause(false);
        RestoreInputMode();
    }

    public UIPanel Open(string id, object args = null)
    {
        ResolveRoot();
        if (root == null || !root.IsConfigured || catalog == null)
        {
            Debug.LogError("UIRoot is missing or incomplete, or UIPanelCatalog is not available.", this);
            return null;
        }

        UIPanelCatalog.Entry definition = catalog.Find(id);
        if (definition == null || definition.prefab == null)
        {
            Debug.LogError("Unknown UI panel: " + id, this);
            return null;
        }

        if (!instances.TryGetValue(id, out UIPanel panel) || panel == null)
        {
            panel = Instantiate(definition.prefab, root.GetLayer(definition.layer));
            panel.name = definition.prefab.name;
            panel.OnCreate();
            instances[id] = panel;
        }

        stack.Remove(id);
        if (stack.Count > 0 && instances.TryGetValue(stack[stack.Count - 1], out UIPanel previous) && previous != null)
            previous.OnBlur();
        panel.SetVisible(true);
        panel.transform.SetAsLastSibling();
        panel.OnOpen(args);
        panel.OnFocus();
        stack.Add(id);

        RefreshPresentationState();
        AcquireInputMode();
        return panel;
    }

    public void CloseTop()
    {
        if (stack.Count == 0) return;
        Close(stack[stack.Count - 1]);
    }

    public void Close(string id)
    {
        UIPanelCatalog.Entry definition = catalog != null ? catalog.Find(id) : null;
        if (definition == null) return;
        if (!stack.Remove(id)) return;
        if (instances.TryGetValue(id, out UIPanel panel) && panel != null)
        {
            panel.OnClose();
            if (definition.lifetime == UIPanelLifetime.Transient)
            {
                instances.Remove(id);
                Destroy(panel.gameObject);
            }
            else
                panel.SetVisible(false);
        }

        RefreshPresentationState();

        if (stack.Count > 0 && instances.TryGetValue(stack[stack.Count - 1], out UIPanel next) && next != null)
            next.OnFocus();
        else
            RestoreInputMode();
    }

    private void SetHudVisible(bool visible)
    {
        RectTransform hud = root != null ? root.GetLayer(UILayer.Hud) : null;
        if (hud != null) hud.gameObject.SetActive(visible);
    }

    private void RefreshPresentationState()
    {
        bool anyPaused = false;
        bool anyHidesHud = false;
        for (int i = 0; i < stack.Count; i++)
        {
            UIPanelCatalog.Entry remaining = catalog != null ? catalog.Find(stack[i]) : null;
            anyPaused |= remaining != null && remaining.pauseGameplay;
            anyHidesHud |= remaining != null && remaining.hideHud;
        }

        SetUiPause(anyPaused);
        SetHudVisible(!anyHidesHud);
    }

    private void SetUiPause(bool paused)
    {
        PauseService pause = GameRoot.Instance != null ? GameRoot.Instance.Pause : null;
        if (pause != null) pause.SetPaused(this, paused);
    }

    private void OnRootChanged(UIRoot nextRoot)
    {
        if (nextRoot == root) return;

        RestoreInputMode();
        instances.Clear();
        stack.Clear();
        SetUiPause(false);
        root = nextRoot;
    }

    private void AcquireInputMode()
    {
        InputModeService inputModes = GameRoot.Instance != null ? GameRoot.Instance.InputModes : null;
        if (inputModes == null) return;

        if (!ownsInputMode)
        {
            inputModeBeforeUi = inputModes.CurrentMode;
            ownsInputMode = true;
        }
        inputModes.SetMode(GameInputMode.UserInterface);
    }

    /// <summary>
    /// Restores the mode that was active before the first panel opened. If another system took
    /// input ownership while the UI was open, its newer mode is preserved.
    /// </summary>
    private void RestoreInputMode()
    {
        if (!ownsInputMode) return;
        InputModeService inputModes = GameRoot.Instance != null ? GameRoot.Instance.InputModes : null;
        if (inputModes != null && inputModes.CurrentMode == GameInputMode.UserInterface)
            inputModes.SetMode(inputModeBeforeUi);
        ownsInputMode = false;
    }

    private void ResolveRoot()
    {
        root = UIRoot.Active;
    }
}
