using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shared title/gameplay settings window. UIService owns pause and input lifetime.</summary>
public sealed class SettingsPanel : UIPanel
{
    public const string PanelId = "settings";
    [SerializeField] private RectTransform card;
    [SerializeField] private Button[] tabs;
    [SerializeField] private GameObject[] pages;
    [SerializeField] private SettingsOptionRow[] rows;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button defaultsButton;
    [SerializeField] private TMP_Text status;
    [SerializeField] private GameObject displayConfirmation;
    [SerializeField] private TMP_Text countdown;
    [SerializeField] private Button keepDisplayButton;
    [SerializeField] private Button revertDisplayButton;
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private SettingsService service;
    private GameSettings original;
    private GameSettings draft;
    private GameObject previousSelection;
    private float confirmationDeadline;
    private int bindingIndex = -1;
    private bool opened;
    private bool confirming;
    private int selectedPage;
    private int openedFrame;

    public override void OnCreate()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].onClick.AddListener(() => SelectPage(index));
        }
        applyButton.onClick.AddListener(Apply);
        backButton.onClick.AddListener(Back);
        defaultsButton.onClick.AddListener(RestoreDefaults);
        keepDisplayButton.onClick.AddListener(KeepDisplay);
        revertDisplayButton.onClick.AddListener(RevertDisplay);
        BindRows();
    }

    public override void OnOpen(object args)
    {
        service = GameRoot.Instance.Settings;
        original = service.Snapshot;
        draft = original.Copy();
        opened = true;
        openedFrame = Time.frameCount;
        confirming = false;
        bindingIndex = -1;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        displayConfirmation.SetActive(false);
        SetMainInteractable(true);
        BuildResolutions();
        SelectPage(0);
        Refresh();
        status.text = "Shared by title and farm. Apply saves; Back discards changes.";
    }

    public override void OnClose()
    {
        if (!opened) return;
        opened = false;
        if (service != null)
        {
            service.Preview(original);
            if (confirming) service.ApplyDisplay(original);
        }
        confirming = false;
        bindingIndex = -1;
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
    }

    private void OnDisable() => OnClose();

    private void Update()
    {
        if (!opened) return;
        if (openedFrame == Time.frameCount) return;
        RectTransform parent = transform as RectTransform;
        float fit = Mathf.Min(1f, Mathf.Min((parent.rect.width - 24f) / 1080f, (parent.rect.height - 24f) / 760f));
        card.localScale = Vector3.one * Mathf.Max(0.1f, fit);
        displayConfirmation.transform.GetChild(0).localScale = card.localScale;
        Keyboard keyboard = Keyboard.current;
        bool back = keyboard != null && keyboard.escapeKey.wasPressedThisFrame ||
            Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        if (confirming)
        {
            countdown.text = "Keep these display settings?\nReverting in " +
                Mathf.CeilToInt(Mathf.Max(0f, confirmationDeadline - Time.unscaledTime)) + " seconds";
            if (back || Time.unscaledTime >= confirmationDeadline) RevertDisplay();
            return;
        }
        if (bindingIndex >= 0)
        {
            if (back) { bindingIndex = -1; Refresh(); status.text = "Key change cancelled."; return; }
            if (keyboard != null)
                foreach (var key in keyboard.allKeys)
                    if (key != null && key.wasPressedThisFrame) { TryBindKey(key.keyCode); break; }
            return;
        }
        if (back) { Back(); return; }
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftShoulder.wasPressedThisFrame) SelectPage((selectedPage + 3) % 4);
            if (Gamepad.current.rightShoulder.wasPressedThisFrame) SelectPage((selectedPage + 1) % 4);
        }
    }

    private void SelectPage(int index)
    {
        if (confirming) return;
        bindingIndex = -1;
        selectedPage = index;
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            tabs[i].image.color = i == index ? CozyUi.Leaf : CozyUi.WoodLight;
        }
        tabs[index].Select();
        if (draft != null) Refresh();
    }

    private void BindRows()
    {
        rows[0].Bind(step => { draft.windowMode = (draft.windowMode + step + 3) % 3; Changed(); }, null);
        rows[1].Bind(CycleResolution, null);
        rows[2].Bind(step => { draft.vSync = !draft.vSync; Changed(); }, null);
        rows[3].Bind(step =>
        {
            int[] rates = { 30, 60, 120, 144, -1 };
            int index = System.Array.IndexOf(rates, draft.frameRate);
            draft.frameRate = rates[(index + step + rates.Length) % rates.Length];
            Changed();
        }, null);
        rows[4].Bind(null, value => { draft.masterVolume = value / 100f; Changed(); });
        rows[5].Bind(step => { draft.muteWhenUnfocused = !draft.muteWhenUnfocused; Changed(); }, null);
        rows[6].Bind(null, value => { draft.uiScale = value / 100f; Changed(); });
        rows[7].Bind(null, value => { draft.worldZoom = value / 100f; Changed(); });
        rows[8].Bind(step => { draft.clock24Hour = !draft.clock24Hour; Changed(); }, null);
        rows[9].Bind(step => { draft.showControlHints = !draft.showControlHints; Changed(); }, null);
        rows[10].Bind(step => { draft.pauseWhenUnfocused = !draft.pauseWhenUnfocused; Changed(); }, null);
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            rows[11 + i].Bind(step =>
            {
                bindingIndex = index;
                status.text = "Press a letter (A-Z). Escape cancels. Arrow keys stay available.";
                Refresh();
            }, null);
        }
    }

    private void Changed()
    {
        service.Preview(draft);
        Refresh();
        status.text = "Previewing changes. Apply to save; Back to discard.";
    }

    private void Refresh()
    {
        rows[0].Refresh(new[] { "Windowed", "Borderless", "Fullscreen" }[draft.windowMode]);
        rows[1].Refresh(draft.resolutionWidth == 0 ? "Desktop" : draft.resolutionWidth + " x " + draft.resolutionHeight);
        rows[2].Refresh(OnOff(draft.vSync));
        rows[3].Refresh(draft.vSync ? "VSync controlled" : draft.frameRate < 0 ? "Unlimited" : draft.frameRate + " FPS", 0f, !draft.vSync);
        rows[4].Refresh(Percent(draft.masterVolume), draft.masterVolume * 100f);
        rows[5].Refresh(OnOff(draft.muteWhenUnfocused));
        rows[6].Refresh(Percent(draft.uiScale), draft.uiScale * 100f);
        rows[7].Refresh(Percent(draft.worldZoom), draft.worldZoom * 100f);
        rows[8].Refresh(draft.clock24Hour ? "24 hour" : "12 hour");
        rows[9].Refresh(OnOff(draft.showControlHints));
        rows[10].Refresh(OnOff(draft.pauseWhenUnfocused));
        int[] keys = GetKeys();
        for (int i = 0; i < keys.Length; i++) rows[11 + i].Refresh(bindingIndex == i ? "Press a key..." : ((Key)keys[i]).ToString());
        RefreshNavigation();
    }

    private void TryBindKey(Key key)
    {
        if (!GameSettings.IsBindableKey(key)) { status.text = "Choose a letter (A-Z); Escape cancels."; return; }
        int[] keys = GetKeys();
        for (int i = 0; i < keys.Length; i++)
            if (i != bindingIndex && keys[i] == (int)key) { status.text = key + " is already assigned. Choose another key."; return; }
        switch (bindingIndex)
        {
            case 0: draft.moveUp = (int)key; break;
            case 1: draft.moveDown = (int)key; break;
            case 2: draft.moveLeft = (int)key; break;
            case 3: draft.moveRight = (int)key; break;
            case 4: draft.backpackKey = (int)key; break;
        }
        bindingIndex = -1;
        Changed();
    }

    private int[] GetKeys() => new[] { draft.moveUp, draft.moveDown, draft.moveLeft, draft.moveRight, draft.backpackKey };
    private static string OnOff(bool value) => value ? "On" : "Off";
    private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

    private void BuildResolutions()
    {
        resolutions.Clear();
        resolutions.Add(Vector2Int.zero);
        foreach (Resolution resolution in Screen.resolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (size.x >= 640 && size.y >= 480 && !resolutions.Contains(size)) resolutions.Add(size);
        }
        var currentSize = new Vector2Int(draft.resolutionWidth, draft.resolutionHeight);
        if (!resolutions.Contains(currentSize)) resolutions.Add(currentSize);
    }

    private void CycleResolution(int step)
    {
        int index = resolutions.IndexOf(new Vector2Int(draft.resolutionWidth, draft.resolutionHeight));
        Vector2Int next = resolutions[(index + step + resolutions.Count) % resolutions.Count];
        draft.resolutionWidth = next.x;
        draft.resolutionHeight = next.y;
        Changed();
    }

    private void Apply()
    {
        if (confirming || bindingIndex >= 0) return;
        if (!draft.HasSameDisplay(original))
        {
            confirming = true;
            confirmationDeadline = Time.unscaledTime + 15f;
            service.ApplyDisplay(draft);
            displayConfirmation.SetActive(true);
            SetMainInteractable(false);
            revertDisplayButton.Select();
            return;
        }
        SaveDraft();
    }

    private void SaveDraft()
    {
        service.Commit(draft);
        original = service.Snapshot;
        status.text = "Settings saved on this device.";
    }

    private void KeepDisplay()
    {
        if (!confirming) return;
        confirming = false;
        displayConfirmation.SetActive(false);
        SetMainInteractable(true);
        SaveDraft();
        applyButton.Select();
    }

    private void RevertDisplay()
    {
        if (!confirming) return;
        confirming = false;
        displayConfirmation.SetActive(false);
        SetMainInteractable(true);
        service.ApplyDisplay(original);
        draft = original.Copy();
        service.Preview(draft);
        Refresh();
        status.text = "Display change cancelled. Previous settings restored.";
        applyButton.Select();
    }

    private void RestoreDefaults()
    {
        if (confirming) return;
        bindingIndex = -1;
        draft = new GameSettings();
        Changed();
        status.text = "Default settings previewed. Apply to save; Back to undo.";
    }

    private void Back()
    {
        if (confirming) { RevertDisplay(); return; }
        GameRoot.Instance.UI.Close(PanelId);
    }

    private void SetMainInteractable(bool interactable)
    {
        foreach (Selectable selectable in card.GetComponentsInChildren<Selectable>(true))
            selectable.interactable = interactable;
        if (interactable) Refresh();
    }

    private void RefreshNavigation()
    {
        // Explicit links keep keyboard/controller focus inside the modal, away from the menu underneath.
        Selectable[] controls = card.GetComponentsInChildren<Selectable>();
        Canvas.ForceUpdateCanvases();
        foreach (Selectable control in controls)
        {
            Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = FindNeighbor(control, controls, Vector2.up);
            navigation.selectOnDown = FindNeighbor(control, controls, Vector2.down);
            navigation.selectOnLeft = FindNeighbor(control, controls, Vector2.left);
            navigation.selectOnRight = FindNeighbor(control, controls, Vector2.right);
            control.navigation = navigation;
        }
        Navigation confirmationNavigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = revertDisplayButton, selectOnRight = revertDisplayButton,
            selectOnUp = revertDisplayButton, selectOnDown = revertDisplayButton };
        keepDisplayButton.navigation = confirmationNavigation;
        confirmationNavigation.selectOnLeft = confirmationNavigation.selectOnRight =
            confirmationNavigation.selectOnUp = confirmationNavigation.selectOnDown = keepDisplayButton;
        revertDisplayButton.navigation = confirmationNavigation;
    }

    private static Selectable FindNeighbor(Selectable source, Selectable[] controls, Vector2 direction)
    {
        Vector3 center = source.transform.TransformPoint(((RectTransform)source.transform).rect.center);
        Selectable best = null;
        float bestScore = float.NegativeInfinity;
        foreach (Selectable candidate in controls)
        {
            if (candidate == source || !candidate.IsInteractable()) continue;
            Vector2 offset = candidate.transform.TransformPoint(((RectTransform)candidate.transform).rect.center) - center;
            float dot = Vector2.Dot(direction, offset);
            if (dot <= 0.01f) continue;
            float score = dot / offset.sqrMagnitude;
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }
}
