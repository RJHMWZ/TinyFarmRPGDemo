using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cozy-farm gameplay shell: clock, money, hotbar, backpack, save/settings and exit controls.
/// It is attached by GameplayEntryPoint so older gameplay scenes gain the UI without reauthoring.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayHudController : MonoBehaviour
{
    private Transform player;
    private GameSaveData save;
    private RectTransform hudLayer;
    private RectTransform windowLayer;
    private RectTransform toastLayer;
    private TMP_Text clockText;
    private TMP_Text dateText;
    private TMP_Text moneyText;
    private readonly TMP_Text[] hotbarLabels = new TMP_Text[12];
    private readonly Image[] hotbarImages = new Image[12];
    private GameObject modal;
    private float hudRefreshTimer;
    private TMP_Text energyText;
    private TMP_Text interactionText;
    private GameObject tutorialCard;
    private TMP_Text tutorialHeader;
    private TMP_Text tutorialTitle;
    private TMP_Text tutorialInstruction;
    private FarmRuntime farm;
    private bool initialized;
    private SettingsService settings;
    private GameSettings preferences;
    private TMP_Text backpackButtonLabel;
    private TMP_Text menuButtonLabel;
    private Camera worldCamera;
    private float baseCameraSize;
    private GameInputMode inputModeBeforeModal;
    private bool ownsModalInput;
    private Coroutine toastRoutine;
    private GameObject activeToast;

    public void Initialize(Transform playerTransform)
    {
        if (initialized) return;
        GameRoot root = GameRoot.Instance;
        UIRoot uiRoot = UIRoot.Active;
        if (root == null || uiRoot == null || !uiRoot.IsConfigured || root.Session.CurrentSave == null)
        {
            Debug.LogError("Gameplay HUD requires a loaded save and configured UIRoot.", this);
            return;
        }

        initialized = true;
        player = playerTransform;
        save = root.Session.CurrentSave;
        save.Normalize();
        farm = FarmRuntime.Active;
        if (farm != null) farm.Game.Feedback += OnFarmFeedback;
        hudLayer = uiRoot.GetLayer(UILayer.Hud);
        windowLayer = uiRoot.GetLayer(UILayer.Window);
        toastLayer = uiRoot.GetLayer(UILayer.Toast);
        BuildHud();
        settings = root.Settings;
        worldCamera = Camera.main;
        if (worldCamera != null) baseCameraSize = worldCamera.orthographicSize;
        settings.Changed += ApplyPreferences;
        ApplyPreferences();
        RefreshHud();
    }

    private void Update()
    {
        if (!initialized || save == null) return;
        HandleShortcuts();

        hudRefreshTimer += Time.unscaledDeltaTime;
        if (hudRefreshTimer >= 0.25f)
        {
            hudRefreshTimer = 0f;
            RefreshHud();
        }
    }

    private void HandleShortcuts()
    {
        GameRoot root = GameRoot.Instance;
        if (root == null || root.UI.IsOpen(SettingsPanel.PanelId) || root.UI.LastClosedFrame == Time.frameCount) return;
        if (root.UI.IsOpen(FarmHubPanel.PanelId)) return;
        if (root.InputModes.CurrentMode == GameInputMode.Disabled || root.InputModes.CurrentMode == GameInputMode.Dialogue) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (modal != null) CloseModal();
            else OpenSystemMenu();
        }
        else if (keyboard[(Key)preferences.backpackKey].wasPressedThisFrame)
        {
            if (modal != null) CloseModal();
            else OpenBackpack();
        }
    }

    private void BuildHud()
    {
        RectTransform clockCard = CozyUi.Rect(hudLayer, "ClockCard", Vector2.one, Vector2.one,
            Vector2.one, new Vector2(-28f, -28f), new Vector2(350f, 152f));
        CozyUi.Panel(clockCard, CozyUi.Cream);
        dateText = CozyUi.Text(clockCard, "Date", "", 26f, TextAlignmentOptions.Center, CozyUi.Ink,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -22f), new Vector2(310f, 45f));
        dateText.enableWordWrapping = false;
        dateText.enableAutoSizing = true;
        dateText.fontSizeMin = 18f;
        dateText.fontSizeMax = 26f;
        clockText = CozyUi.Text(clockCard, "Time", "", 47f, TextAlignmentOptions.Center, CozyUi.Ink,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 18f), new Vector2(310f, 70f));

        RectTransform moneyCard = CozyUi.Rect(hudLayer, "MoneyCard", new Vector2(1f, 1f), Vector2.one,
            Vector2.one, new Vector2(-28f, -198f), new Vector2(350f, 72f));
        CozyUi.Panel(moneyCard, CozyUi.Cream);
        moneyText = CozyUi.Text(moneyCard, "Money", "", 30f, TextAlignmentOptions.Center, CozyUi.Ink,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -8f));

        CozyUi.Button(hudLayer, "BackpackButton", "BACKPACK  [I]", Vector2.up, Vector2.up, Vector2.up,
            new Vector2(28f, -28f), new Vector2(290f, 68f), OpenBackpack, out TextMeshProUGUI backpackLabel);
        backpackButtonLabel = backpackLabel;
        CozyUi.Button(hudLayer, "MenuButton", "MENU  [ESC]", Vector2.up, Vector2.up, Vector2.up,
            new Vector2(28f, -112f), new Vector2(290f, 68f), OpenSystemMenu, out TextMeshProUGUI menuLabel);
        menuButtonLabel = menuLabel;

        RectTransform energyCard = CozyUi.Rect(hudLayer, "EnergyCard", Vector2.up, Vector2.up, Vector2.up,
            new Vector2(28f, -196f), new Vector2(290f, 82f));
        CozyUi.Panel(energyCard, CozyUi.Cream);
        energyText = CozyUi.Text(energyCard, "Energy", "", 25f, TextAlignmentOptions.Center, CozyUi.Ink,
            Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, new Vector2(-16f, -8f));

        RectTransform objectiveCard = CozyUi.Rect(hudLayer, "FirstDayObjective", Vector2.up, Vector2.up, Vector2.up,
            new Vector2(28f, -298f), new Vector2(490f, 176f));
        tutorialCard = objectiveCard.gameObject;
        CozyUi.Panel(objectiveCard, new Color(0.96f, 0.91f, 0.72f, 0.96f));
        tutorialHeader = CozyUi.Text(objectiveCard, "Step", "", 18f, TextAlignmentOptions.Left, CozyUi.Leaf,
            Vector2.up, Vector2.one, Vector2.up, new Vector2(22f, -14f), new Vector2(-44f, 30f));
        tutorialTitle = CozyUi.Text(objectiveCard, "Objective", "", 27f, TextAlignmentOptions.Left, CozyUi.Ink,
            Vector2.up, Vector2.one, Vector2.up, new Vector2(22f, -47f), new Vector2(-44f, 42f));
        tutorialInstruction = CozyUi.Text(objectiveCard, "Instruction", "", 20f, TextAlignmentOptions.TopLeft, CozyUi.Ink,
            Vector2.up, Vector2.one, Vector2.up, new Vector2(22f, -91f), new Vector2(-44f, 68f));
        RectTransform hintCard = CozyUi.Rect(hudLayer, "InteractionHint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0, 146f), new Vector2(940f, 54f));
        CozyUi.Panel(hintCard, new Color(0.15f, 0.21f, 0.13f, 0.9f), false).raycastTarget = false;
        interactionText = CozyUi.Text(hintCard, "Hint", "", 22f, TextAlignmentOptions.Center, CozyUi.Cream,
            Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, new Vector2(-16, -4));

        RectTransform hotbar = CozyUi.Rect(hudLayer, "Hotbar", new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1010f, 98f));
        CozyUi.Panel(hotbar, new Color32(86, 52, 34, 235));
        for (int i = 0; i < hotbarLabels.Length; i++)
        {
            int slotIndex = i;
            Button button = CozyUi.Button(hotbar, "Slot" + (i + 1), "", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-454f + i * 82.5f, 0f), new Vector2(72f, 72f),
                () => SelectHotbarSlot(slotIndex), out TextMeshProUGUI label);
            label.fontSize = 17f;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 17f;
            label.overflowMode = TextOverflowModes.Ellipsis;
            hotbarLabels[i] = label;
            hotbarImages[i] = button.image;
        }
    }

    private void RefreshHud()
    {
        if (save == null) return;
        dateText.text = GameCalendar.SeasonName(save.world.season) + "  " + save.world.day +
                        "   Year " + save.world.year + "   -   " + WeatherName(save.world.weatherId);
        clockText.text = preferences != null && preferences.clock24Hour
            ? save.world.hour.ToString("00") + ":" + save.world.minute.ToString("00")
            : ((save.world.hour + 11) % 12 + 1) + ":" + save.world.minute.ToString("00") + (save.world.hour < 12 ? " AM" : " PM");
        moneyText.text = "GOLD   " + save.player.money.ToString("N0") + " g";
        if (energyText != null) energyText.text = "ENERGY  " + save.farm.energy + "/100\nFarming Lv." + (farm != null ? farm.Game.Progression.Level : 1);
        if (interactionText != null) interactionText.text = preferences != null && !preferences.showControlHints ? "" : farm != null ? farm.InteractionHint : "";
        if (tutorialCard != null && farm != null)
        {
            FirstDayTutorial tutorial = farm.Game.Tutorial;
            tutorialCard.SetActive(!tutorial.IsComplete);
            if (!tutorial.IsComplete)
            {
                TutorialObjective objective = tutorial.Objective;
                tutorialHeader.text = "FIRST DAY  " + tutorial.DisplayStep + "/" + FirstDayTutorial.ObjectiveCount +
                    "     " + Mathf.Min(objective.Progress, objective.Target) + "/" + objective.Target;
                tutorialTitle.text = objective.Title;
                tutorialInstruction.text = objective.Instruction;
            }
        }
        for (int i = 0; i < hotbarLabels.Length; i++)
        {
            InventorySlotSaveData slot = save.inventory.slots[i];
            string content = slot.IsEmpty ? "-" : ShortName(slot.displayName);
            if (!slot.IsEmpty && slot.count > 1) content += " x" + slot.count;
            hotbarLabels[i].text = (i + 1) + "\n" + content;
            hotbarImages[i].color = i == save.inventory.selectedHotbarSlot ? CozyUi.Gold : CozyUi.WoodLight;
        }
    }

    private void SelectHotbarSlot(int index)
    {
        if (farm != null) farm.Game.SelectHotbarSlot(Mathf.Clamp(index, 0, 11));
        else save.inventory.selectedHotbarSlot = Mathf.Clamp(index, 0, 11);
        RefreshHud();
    }

    private void OpenBackpack()
    {
        if (!CanOpenModal()) return;
        GameRoot.Instance.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = "backpack" });
    }

    private void OpenSystemMenu()
    {
        if (!CanOpenModal()) return;
        GameObject root = CozyUi.ModalRoot(windowLayer, "SystemMenu");
        RectTransform card = CozyUi.Rect(root.transform, "Card", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(570f, 760f));
        CozyUi.Panel(card, CozyUi.Cream);
        CozyUi.Text(card, "Title", "FARM MENU", 44f, TextAlignmentOptions.Center, CozyUi.Ink,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -38f), new Vector2(480f, 70f));
        AddMenuButton(card, "Resume", "RESUME", -145f, CloseModal);
        AddMenuButton(card, "Save", "SAVE GAME", -235f, SaveNow);
        AddMenuButton(card, "Backpack", "BACKPACK", -325f, () => { CloseModal(); OpenBackpack(); });
        AddMenuButton(card, "Settings", "SETTINGS", -415f, OpenSettings);
        AddMenuButton(card, "TitleScreen", "SAVE & TITLE", -505f, () => ConfirmExit(false));
        AddMenuButton(card, "Quit", "SAVE & QUIT", -595f, () => ConfirmExit(true));
        CozyUi.Text(card, "Hint", "Esc closes this menu", 20f, TextAlignmentOptions.Center,
            new Color32(105, 82, 62, 255), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(460f, 36f));
        SetModal(root);
    }

    private void AddMenuButton(Transform parent, string name, string label, float y, UnityEngine.Events.UnityAction action)
    {
        CozyUi.Button(parent, name, label, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(410f, 66f), () => action(), out _);
    }

    private void OpenSettings()
    {
        GameRoot.Instance?.UI.Open(SettingsPanel.PanelId);
    }

    private void ConfirmExit(bool quit)
    {
        ReplaceModal(null);
        GameObject root = CozyUi.ModalRoot(windowLayer, "ExitConfirmation");
        RectTransform card = CozyUi.Rect(root.transform, "Card", Vector2.one * 0.5f, Vector2.one * 0.5f,
            Vector2.one * 0.5f, Vector2.zero, new Vector2(760f, 380f));
        CozyUi.Panel(card, CozyUi.Cream);
        CozyUi.Text(card, "Title", quit ? "SAVE & QUIT?" : "RETURN TO TITLE?", 36f,
            TextAlignmentOptions.Center, CozyUi.Ink, Vector2.up, Vector2.one, Vector2.one * 0.5f,
            new Vector2(0f, -72f), new Vector2(-60f, 65f));
        CozyUi.Text(card, "Hint", "Your farm will be saved before leaving.\nIf saving fails, you will stay here.", 25f,
            TextAlignmentOptions.Center, CozyUi.Ink, Vector2.one * 0.5f, Vector2.one * 0.5f,
            Vector2.one * 0.5f, Vector2.zero, new Vector2(680f, 100f));
        CozyUi.Button(card, "CancelExit", "CANCEL", Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(42f, 40f), new Vector2(310f, 62f),
            () => { ReplaceModal(null); OpenSystemMenu(); }, out _);
        CozyUi.Button(card, "ConfirmExit", quit ? "SAVE & QUIT" : "SAVE & TITLE", Vector2.right,
            Vector2.right, Vector2.right, new Vector2(-42f, 40f), new Vector2(310f, 62f),
            () => { if (quit) SaveAndQuit(); else SaveAndReturnToTitle(); }, out _);
        SetModal(root);
    }

    private bool CanOpenModal() => initialized && windowLayer != null && modal == null &&
        !GameRoot.Instance.UI.IsOpen(SettingsPanel.PanelId) && !GameRoot.Instance.UI.IsOpen(FarmHubPanel.PanelId);

    private void SetModal(GameObject next)
    {
        modal = next;
        SetPaused(true);
    }

    private void ReplaceModal(GameObject next)
    {
        if (modal != null) Destroy(modal);
        modal = next;
    }

    private void CloseModal()
    {
        ReplaceModal(null);
        SetPaused(false);
    }

    private void SetPaused(bool paused)
    {
        GameRoot root = GameRoot.Instance;
        if (root == null) return;
        root.Pause.SetPaused(this, paused);
        if (paused)
        {
            if (!ownsModalInput) inputModeBeforeModal = root.InputModes.CurrentMode;
            ownsModalInput = true;
            root.InputModes.SetMode(GameInputMode.UserInterface);
        }
        else if (ownsModalInput)
        {
            if (root.InputModes.CurrentMode == GameInputMode.UserInterface)
                root.InputModes.SetMode(inputModeBeforeModal);
            ownsModalInput = false;
        }
    }

    private void SaveNow() => TrySaveNow();

    private bool TrySaveNow()
    {
        if (farm != null) return farm.SaveNow();
        GameRoot root = GameRoot.Instance;
        if (root == null || save == null) return false;
        if (player != null)
        {
            save.player.positionX = player.position.x;
            save.player.positionY = player.position.y;
        }
        save.world.currentScene = SceneManager.GetActiveScene().name;
        bool saved = root.Saves.Save(root.Session.ActiveSlot, save);
        ShowToast(saved ? "Game saved." : "Save failed. Check the log.");
        return saved;
    }

    private void SaveAndReturnToTitle()
    {
        if (!TrySaveNow()) return;
        CloseModal();
        GameRoot.Instance?.Flow.ReturnToMainMenu();
    }

    private void SaveAndQuit()
    {
        if (!TrySaveNow()) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowToast(string message)
    {
        if (toastLayer == null) return;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        if (activeToast != null) Destroy(activeToast);
        toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    private IEnumerator ToastRoutine(string message)
    {
        RectTransform panel = CozyUi.Rect(toastLayer, "Toast", new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(880f, 96f));
        activeToast = panel.gameObject;
        CozyUi.Panel(panel, new Color32(67, 91, 59, 245));
        CozyUi.Text(panel, "Text", message, 26f, TextAlignmentOptions.Center, CozyUi.Cream,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -8f));
        yield return new WaitForSecondsRealtime(2.2f);
        if (panel != null) Destroy(panel.gameObject);
        activeToast = null;
        toastRoutine = null;
    }

    private void OnFarmFeedback(FarmResult result)
    {
        if (GameRoot.Instance != null && GameRoot.Instance.UI.IsOpen(FarmHubPanel.PanelId)) return;
        ShowToast(result.Message);
    }

    private void OnDestroy()
    {
        if (settings != null) settings.Changed -= ApplyPreferences;
        if (farm != null && farm.Game != null) farm.Game.Feedback -= OnFarmFeedback;
        if (GameRoot.Instance != null) GameRoot.Instance.Pause.SetPaused(this, false);
    }

    private void ApplyPreferences()
    {
        if (hudLayer == null || clockText == null) return;
        preferences = settings.Snapshot;
        foreach (Transform child in hudLayer) child.localScale = Vector3.one * preferences.uiScale;
        backpackButtonLabel.text = "BACKPACK" + (preferences.showControlHints ? "  [" + (Key)preferences.backpackKey + "]" : "");
        menuButtonLabel.text = "MENU" + (preferences.showControlHints ? "  [ESC]" : "");
        if (worldCamera != null && worldCamera.orthographic)
            worldCamera.orthographicSize = baseCameraSize / preferences.worldZoom;
        RefreshHud();
    }

    private static string WeatherName(string weatherId)
    {
        if (string.IsNullOrWhiteSpace(weatherId)) return "Sunny";
        return char.ToUpperInvariant(weatherId[0]) + weatherId.Substring(1);
    }

    private static string ShortName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Item";
        if (value.Length <= 10) return value;
        return value.Substring(0, 9) + ".";
    }
}
