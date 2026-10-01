using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Coordinates frontend screens. Scene loading and save IO remain in services.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    private enum SavePanelMode { Create, Load }

    [Header("Frontend screens")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject savePanel;
    [SerializeField] private CharacterCreationController characterCreation;

    private readonly UnityAction[] slotActions = new UnityAction[SaveService.SlotCount];
    private readonly SaveMetadata[] slotMetadata = new SaveMetadata[SaveService.SlotCount];
    private readonly bool[] slotHasSave = new bool[SaveService.SlotCount];
    private readonly bool[] slotOccupied = new bool[SaveService.SlotCount];
    private MainMenuView menuView;
    private SaveSelectView saveView;
    private bool bindingsRegistered;
    private SavePanelMode panelMode;
    private int selectedSlot = -1;
    private int pendingDeleteSlot = -1;
    private bool overwriteConfirmed;

    public bool IsConfigured => menuPanel != null && savePanel != null && characterCreation != null &&
                                menuPanel.GetComponent<MainMenuView>() != null &&
                                savePanel.GetComponent<SaveSelectView>() != null;

    private void Awake()
    {
        if (!ResolveViews())
        {
            enabled = false;
            return;
        }

        Bind(menuView.CreateGameButton, CreateNewGame);
        Bind(menuView.LoadGameButton, OpenSavePanel);
        Bind(menuView.ExitButton, ExitGame);
        // Settings remain visible in the authored layout but are intentionally unavailable until
        // the settings service and localization milestone are implemented.
        menuView.SettingsButton.interactable = false;
        BindSavePanel();
        bindingsRegistered = true;

        if (characterCreation != null)
        {
            characterCreation.Configure(this);
            characterCreation.gameObject.SetActive(false);
        }
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        if (!bindingsRegistered) return;

        if (menuView != null)
        {
            Unbind(menuView.CreateGameButton, CreateNewGame);
            Unbind(menuView.LoadGameButton, OpenSavePanel);
            Unbind(menuView.ExitButton, ExitGame);
        }

        if (saveView == null) return;
        Unbind(saveView.PrimaryActionButton, ConfirmSelectedSlot);
        Unbind(saveView.DeleteButton, DeleteSelectedSlot);
        Unbind(saveView.BackButton, CloseSavePanel);
        for (int i = 0; i < slotActions.Length; i++)
            Unbind(saveView.GetSlotButton(i), slotActions[i]);
    }

    private void CreateNewGame() => OpenSavePanel(SavePanelMode.Create);
    private void OpenSavePanel() => OpenSavePanel(SavePanelMode.Load);

    internal void ShowMainMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (characterCreation != null) characterCreation.gameObject.SetActive(false);
        CloseSavePanel();
    }

    private void CloseSavePanel()
    {
        if (savePanel != null) savePanel.SetActive(false);
    }

    private void ConfirmSelectedSlot()
    {
        if (selectedSlot < 0) return;
        if (panelMode == SavePanelMode.Create)
        {
            SaveService saves = GetSaveService();
            if (saves == null) return;
            if (saves.IsSlotOccupied(selectedSlot) && !overwriteConfirmed)
            {
                overwriteConfirmed = true;
                saveView.Description.text += "\nThis slot already has data. Click Create again to overwrite it.";
                return;
            }

            GameRoot root = GameRoot.Instance;
            if (root == null) return;
            root.Flow.SelectNewGameSlot(selectedSlot);
            CloseSavePanel();
            menuPanel.SetActive(false);
            if (characterCreation != null) characterCreation.BeginNewGame();
            return;
        }

        GameRoot.Instance?.Flow.LoadGame(selectedSlot);
    }

    private void DeleteSelectedSlot()
    {
        SaveService saves = GetSaveService();
        if (selectedSlot < 0 || saves == null || !saves.IsSlotOccupied(selectedSlot)) return;
        if (pendingDeleteSlot != selectedSlot)
        {
            pendingDeleteSlot = selectedSlot;
            saveView.DeleteButtonLabel.text = "Confirm Delete";
            saveView.Description.text += "\nClick Confirm Delete to permanently remove this save.";
            return;
        }

        if (!saves.Delete(selectedSlot))
        {
            saveView.Description.text += "\nThe save could not be deleted. Check the log for details.";
            return;
        }
        pendingDeleteSlot = -1;
        RefreshSavePanel();
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private bool ResolveViews()
    {
        if (menuPanel == null || savePanel == null || characterCreation == null)
        {
            Debug.LogError("MainMenuController screen references are incomplete.", this);
            return false;
        }

        menuView = menuPanel.GetComponent<MainMenuView>();
        saveView = savePanel.GetComponent<SaveSelectView>();
        if (menuView == null || !menuView.IsConfigured)
        {
            Debug.LogError("MainMenuView is missing or has incomplete references.", menuPanel);
            return false;
        }
        if (saveView == null || !saveView.IsConfigured)
        {
            Debug.LogError("SaveSelectView is missing or has incomplete references.", savePanel);
            return false;
        }
        return true;
    }

    private void BindSavePanel()
    {
        for (int i = 0; i < slotActions.Length; i++)
        {
            int capturedSlot = i;
            slotActions[i] = () => SelectSlot(capturedSlot);
            Bind(saveView.GetSlotButton(i), slotActions[i]);
        }
        Bind(saveView.PrimaryActionButton, ConfirmSelectedSlot);
        Bind(saveView.DeleteButton, DeleteSelectedSlot);
        Bind(saveView.BackButton, CloseSavePanel);
        savePanel.SetActive(false);
    }

    private void OpenSavePanel(SavePanelMode mode)
    {
        panelMode = mode;
        selectedSlot = -1;
        pendingDeleteSlot = -1;
        overwriteConfirmed = false;
        RefreshSavePanel();
        savePanel.SetActive(true);
        savePanel.transform.SetAsLastSibling();
    }

    private void RefreshSavePanel()
    {
        SaveService saves = GetSaveService();
        if (saves == null) return;

        bool loading = panelMode == SavePanelMode.Load;
        saveView.PanelTitle.text = loading ? "Choose a Save to Load" : "Choose a Slot for New Game";
        saveView.PrimaryActionLabel.text = loading ? "Load" : "Create";
        saveView.DeleteButtonLabel.text = "Delete";

        for (int i = 0; i < saveView.SlotCount; i++)
        {
            slotHasSave[i] = saves.TryReadMetadata(i, out slotMetadata[i]);
            slotOccupied[i] = saves.IsSlotOccupied(i);
            saveView.GetSlotLabel(i).text = slotHasSave[i]
                ? "Slot " + (i + 1) + "  -  " + DisplayName(slotMetadata[i])
                : slotOccupied[i]
                    ? "Slot " + (i + 1) + "  -  Unavailable"
                    : "Slot " + (i + 1) + "  -  Empty";

            Button button = saveView.GetSlotButton(i);
            if (button.image != null)
                button.image.color = i == selectedSlot ? new Color(1f, 0.82f, 0.42f, 1f) : Color.white;
        }

        bool selected = selectedSlot >= 0;
        SaveMetadata selectedMetadata = selected ? slotMetadata[selectedSlot] : null;
        bool selectedHasSave = selected && slotHasSave[selectedSlot];
        bool selectedOccupied = selected && slotOccupied[selectedSlot];
        saveView.Description.text = !selected
            ? "Select one of the four save slots.\nSelecting a slot will not start the game."
            : selectedHasSave
                ? "Slot " + (selectedSlot + 1) + "\nCharacter: " + DisplayName(selectedMetadata) + "\nSave data is available."
                : selectedOccupied
                    ? "Slot " + (selectedSlot + 1) + "\nSave data exists but cannot be loaded by this game version."
                : "Slot " + (selectedSlot + 1) + "\nEmpty slot";
        saveView.PrimaryActionButton.interactable = selected && (!loading || selectedHasSave);
        saveView.DeleteButton.interactable = selectedOccupied;
    }

    private void SelectSlot(int slotIndex)
    {
        selectedSlot = slotIndex;
        pendingDeleteSlot = -1;
        overwriteConfirmed = false;
        RefreshSavePanel();
    }

    private static string DisplayName(SaveMetadata metadata)
    {
        return metadata == null || string.IsNullOrWhiteSpace(metadata.playerName) ? "Unnamed" : metadata.playerName;
    }

    private SaveService GetSaveService()
    {
        GameRoot root = GameRoot.Instance;
        if (root != null) return root.Saves;
        Debug.LogError("GameRoot is missing; save operations are unavailable.", this);
        return null;
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button != null && action != null) button.onClick.AddListener(action);
    }

    private static void Unbind(Button button, UnityAction action)
    {
        if (button != null && action != null) button.onClick.RemoveListener(action);
    }
}
