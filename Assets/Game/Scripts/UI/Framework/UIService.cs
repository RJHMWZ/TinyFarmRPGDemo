using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Loads panels, assigns layers, and maintains the UI back stack.</summary>
public sealed class UIService : MonoBehaviour
{
    [SerializeField] private UIPanelCatalog catalog;
    private readonly Dictionary<string, UIPanel> instances = new Dictionary<string, UIPanel>();
    private readonly List<string> stack = new List<string>();
    private UIRoot root;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveRoot();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SetUiPause(false);
    }

    public void Configure(UIPanelCatalog value) => catalog = value;

    public UIPanel Open(string id, object args = null)
    {
        ResolveRoot();
        if (root == null || catalog == null)
        {
            Debug.LogError("UIRoot or UIPanelCatalog is not available.", this);
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
        GameRoot.Instance?.InputModes.SetMode(GameInputMode.UserInterface);
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
        stack.Remove(id);
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
            GameRoot.Instance?.InputModes.SetMode(GameInputMode.Gameplay);
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        instances.Clear();
        stack.Clear();
        SetUiPause(false);
        ResolveRoot();
    }

    private void ResolveRoot()
    {
        root = FindObjectOfType<UIRoot>();
    }
}
